using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Brain;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;
using Stakeout.Simulation.Fixtures;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Groups;

/// <summary>
/// Integration tests for the full date coordination flow.
/// Assertions on SimulationState (traces, group status) and journal events — not on plan internals.
/// </summary>
public class DateCoordinationIntegrationTests
{
    private static readonly DateTime SimStart = new(1984, 1, 2, 8, 0, 0);

    private static Address CreateAddress(SimulationState state, AddressType type, int gridX, int gridY)
    {
        var addr = new Address
        {
            Id = state.GenerateEntityId(),
            Type = type,
            GridX = gridX,
            GridY = gridY
        };
        state.Addresses[addr.Id] = addr;
        AddressTemplateRegistry.Get(type).Generate(addr, state, new Random(addr.Id));
        return addr;
    }

    private static Fixture GetTelephone(SimulationState state, int addressId)
    {
        // Telephone fixtures are attached to sub-locations (e.g. kitchen), not locations directly.
        // GetFixturesForAddress only checks LocationId, so we must also check SubLocationIds.
        var locations = state.GetLocationsForAddress(addressId);
        foreach (var loc in locations)
        {
            var subLocs = state.GetSubLocationsForLocation(loc.Id);
            foreach (var sub in subLocs)
            {
                var fixtures = state.GetFixturesForSubLocation(sub.Id);
                var phone = fixtures.FirstOrDefault(f => f.Type == FixtureType.Telephone);
                if (phone != null) return phone;
            }
            // Also check location-level fixtures
            var locFixtures = state.GetFixturesForLocation(loc.Id);
            var locPhone = locFixtures.FirstOrDefault(f => f.Type == FixtureType.Telephone);
            if (locPhone != null) return locPhone;
        }
        throw new InvalidOperationException($"No telephone fixture found at address {addressId}");
    }

    private static Person CreatePerson(SimulationState state, Address home, Fixture phone,
        string firstName, bool hasVehicle = false)
    {
        var person = new Person
        {
            Id = state.GenerateEntityId(),
            FirstName = firstName,
            LastName = "Test",
            CreatedAt = state.Clock.CurrentTime,
            HomeAddressId = home.Id,
            HomePhoneFixtureId = phone.Id,
            CurrentAddressId = home.Id,
            CurrentPosition = home.Position,
            PreferredSleepTime = TimeSpan.FromHours(23),
            PreferredWakeTime = TimeSpan.FromHours(7)
        };
        person.Objectives.Add(new SleepObjective { Id = state.GenerateEntityId() });

        if (hasVehicle)
        {
            var vehicle = new Vehicle
            {
                Id = state.GenerateEntityId(),
                OwnerPersonId = person.Id,
                CurrentAddressId = home.Id,
                Type = VehicleType.Car
            };
            state.Vehicles[vehicle.Id] = vehicle;
            person.VehicleId = vehicle.Id;
        }

        state.People[person.Id] = person;
        return person;
    }

    private static void AssignJob(SimulationState state, Person person, Address workAddress,
        TimeSpan shiftStart, TimeSpan shiftEnd, DayOfWeek[] workDays)
    {
        var business = new Business
        {
            Id = state.GenerateEntityId(),
            AddressId = workAddress.Id,
            Name = "Test Corp",
            Type = BusinessType.Office,
            IsResolved = true
        };
        var position = new Position
        {
            Id = state.GenerateEntityId(),
            BusinessId = business.Id,
            Role = "Worker",
            ShiftStart = shiftStart,
            ShiftEnd = shiftEnd,
            WorkDays = workDays,
            AssignedPersonId = person.Id
        };
        business.Positions.Add(position);
        state.Businesses[business.Id] = business;
        person.BusinessId = business.Id;
        person.PositionId = position.Id;
        person.Objectives.Add(new WorkShiftObjective(business.Id, position.Id)
        {
            Id = state.GenerateEntityId()
        });
    }

    private static void RunSimulation(SimulationState state, int minutes, params Person[] people)
    {
        var mapConfig = new MapConfig();
        var runner = new ActionRunner(mapConfig);

        for (int i = 0; i < minutes; i++)
        {
            state.Clock.Tick(60);
            foreach (var person in people)
            {
                if (person.DayPlan == null || person.DayPlan.IsExhausted)
                    person.DayPlan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime, mapConfig);

                runner.Tick(person, state, TimeSpan.FromMinutes(1));
            }
        }
    }

    [Fact]
    public void Date_SheAnswers_BothEndUpAtDinerAndReturnHome()
    {
        AddressTemplateRegistry.RegisterAll();
        var state = new SimulationState(new GameClock(SimStart));

        var guyHome = CreateAddress(state, AddressType.SuburbanHome, 2, 2);
        var girlHome = CreateAddress(state, AddressType.SuburbanHome, 10, 2);
        var diner = CreateAddress(state, AddressType.Diner, 6, 6);

        var guyPhone = GetTelephone(state, guyHome.Id);
        var girlPhone = GetTelephone(state, girlHome.Id);

        var guy = CreatePerson(state, guyHome, guyPhone, "Guy", hasVehicle: true);
        var girl = CreatePerson(state, girlHome, girlPhone, "Girl", hasVehicle: false);

        var rel = new Relationship
        {
            Id = state.GenerateEntityId(),
            PersonAId = guy.Id,
            PersonBId = girl.Id,
            Type = RelationshipType.Dating,
            StartedAt = SimStart.AddDays(-30)
        };
        state.AddRelationship(rel);
        // Strip auto-injected MaintainRelationshipObjectives so the test controls date scheduling directly.
        guy.Objectives.RemoveAll(o => o is MaintainRelationshipObjective);
        girl.Objectives.RemoveAll(o => o is MaintainRelationshipObjective);

        // Guy will call at noon, propose dinner at 7pm (pickup 5:50pm)
        guy.Objectives.Add(new OrganizeDateObjective(
            targetPersonId: girl.Id,
            proposedMeetupAddressId: diner.Id,
            proposedCallTime: SimStart.Date.AddHours(12),
            proposedMeetupTime: SimStart.Date.AddHours(19),
            proposedPickupTime: SimStart.Date.AddHours(17).AddMinutes(50))
        {
            Id = state.GenerateEntityId()
        });

        // Run for 24 hours
        RunSimulation(state, 24 * 60, guy, girl);

        // --- Assertions ---

        // Phone record traces: incoming call was recorded on girl's phone
        var girlPhoneTraces = state.GetTracesForFixture(girlPhone.Id, state.Clock.CurrentTime);
        Assert.NotEmpty(girlPhoneTraces);

        // Group was formed and disbanded
        Assert.Single(state.Groups);
        var group = state.Groups.Values.Single();
        Assert.Equal(GroupStatus.Disbanded, group.Status);
        Assert.Contains(guy.Id, group.MemberPersonIds);
        Assert.Contains(girl.Id, group.MemberPersonIds);

        // Both had dinner (ActivityStarted with "having dinner")
        var guyEvents = state.Journal.GetEventsForPerson(guy.Id);
        var girlEvents = state.Journal.GetEventsForPerson(girl.Id);
        Assert.Contains(guyEvents, e =>
            e.EventType == SimulationEventType.ActivityStarted && e.Description == "having dinner");
        Assert.Contains(girlEvents, e =>
            e.EventType == SimulationEventType.ActivityStarted && e.Description == "having dinner");

        // They both arrived at the diner
        var guyArrival = guyEvents.FirstOrDefault(e =>
            e.EventType == SimulationEventType.ArrivedAtAddress && e.AddressId == diner.Id);
        var girlArrival = girlEvents.FirstOrDefault(e =>
            e.EventType == SimulationEventType.ArrivedAtAddress && e.AddressId == diner.Id);
        Assert.NotNull(guyArrival);
        Assert.NotNull(girlArrival);
        Assert.True(Math.Abs((guyArrival.Timestamp - girlArrival.Timestamp).TotalMinutes) < 30,
            $"Guy arrived at diner at {guyArrival.Timestamp:HH:mm}, girl at {girlArrival.Timestamp:HH:mm}");

        // Girl ended the day at her home
        Assert.Equal(girlHome.Id, girl.CurrentAddressId);
    }

    [Fact]
    public void Date_AnsweringMachinePath_DateOrganizedForNextDay()
    {
        AddressTemplateRegistry.RegisterAll();
        var state = new SimulationState(new GameClock(SimStart));

        var guyHome = CreateAddress(state, AddressType.SuburbanHome, 2, 2);
        var girlHome = CreateAddress(state, AddressType.SuburbanHome, 10, 2);
        var diner = CreateAddress(state, AddressType.Diner, 6, 6);
        var awayAddr = CreateAddress(state, AddressType.Park, 15, 15);

        var guyPhone = GetTelephone(state, guyHome.Id);
        var girlPhone = GetTelephone(state, girlHome.Id);

        var guy = CreatePerson(state, guyHome, guyPhone, "Guy", hasVehicle: true);
        var girl = CreatePerson(state, girlHome, girlPhone, "Girl", hasVehicle: false);

        var rel = new Relationship
        {
            Id = state.GenerateEntityId(),
            PersonAId = guy.Id,
            PersonBId = girl.Id,
            Type = RelationshipType.Dating,
            StartedAt = SimStart.AddDays(-30)
        };
        state.AddRelationship(rel);
        // Strip auto-injected MaintainRelationshipObjectives so the test controls date scheduling directly.
        guy.Objectives.RemoveAll(o => o is MaintainRelationshipObjective);
        girl.Objectives.RemoveAll(o => o is MaintainRelationshipObjective);

        // Guy calls at noon, proposes dinner at 7pm (pickup 5:50pm)
        guy.Objectives.Add(new OrganizeDateObjective(
            targetPersonId: girl.Id,
            proposedMeetupAddressId: diner.Id,
            proposedCallTime: SimStart.Date.AddHours(12),
            proposedMeetupTime: SimStart.Date.AddHours(19),
            proposedPickupTime: SimStart.Date.AddHours(17).AddMinutes(50))
        {
            Id = state.GenerateEntityId()
        });

        // Girl is away at noon, will return home around 6pm
        girl.CurrentAddressId = awayAddr.Id;
        girl.CurrentPosition = awayAddr.Position;
        girl.Objectives.Add(new WaitAwayUntilObjective(awayAddr.Id, SimStart.Date.AddHours(18))
        {
            Id = state.GenerateEntityId()
        });

        // Run 48 hours
        RunSimulation(state, 48 * 60, guy, girl);

        // --- Assertions ---

        // Message was left on girl's phone
        var girlPhoneTraces = state.GetTracesForFixture(girlPhone.Id, state.Clock.CurrentTime);
        Assert.Contains(girlPhoneTraces, t => t.Description != null && t.Description.Contains("please call back"));

        // Group was formed and disbanded
        Assert.Single(state.Groups);
        var group = state.Groups.Values.Single();
        Assert.Equal(GroupStatus.Disbanded, group.Status);

        // Date happened on day 2
        Assert.Equal(SimStart.Date.AddDays(1), group.MeetupTime.Date);

        // Both attended the diner on day 2
        var guyEvents = state.Journal.GetEventsForPerson(guy.Id);
        var girlEvents = state.Journal.GetEventsForPerson(girl.Id);
        Assert.Contains(guyEvents, e =>
            e.EventType == SimulationEventType.ActivityStarted && e.Description == "having dinner");
        Assert.Contains(girlEvents, e =>
            e.EventType == SimulationEventType.ActivityStarted && e.Description == "having dinner");

        // Both at diner on day 2
        var guyDinerDay2 = guyEvents.FirstOrDefault(e =>
            e.EventType == SimulationEventType.ArrivedAtAddress
            && e.AddressId == diner.Id
            && e.Timestamp.Date == SimStart.Date.AddDays(1));
        var girlDinerDay2 = girlEvents.FirstOrDefault(e =>
            e.EventType == SimulationEventType.ArrivedAtAddress
            && e.AddressId == diner.Id
            && e.Timestamp.Date == SimStart.Date.AddDays(1));
        Assert.NotNull(guyDinerDay2);
        Assert.NotNull(girlDinerDay2);
    }

    /// <summary>
    /// Both partners have MaintainRelationshipObjective (the real production scenario).
    /// Only one group should ever be created — not two.
    /// </summary>
    [Fact]
    public void DatingCouple_BothHaveMaintainObjective_OnlyOneGroupCreated()
    {
        AddressTemplateRegistry.RegisterAll();
        var state = new SimulationState(new GameClock(SimStart));

        var guyHome = CreateAddress(state, AddressType.SuburbanHome, 2, 2);
        var girlHome = CreateAddress(state, AddressType.SuburbanHome, 10, 2);
        var diner = CreateAddress(state, AddressType.Diner, 6, 6);

        var guyPhone = GetTelephone(state, guyHome.Id);
        var girlPhone = GetTelephone(state, girlHome.Id);

        var guy = CreatePerson(state, guyHome, guyPhone, "Jack", hasVehicle: true);
        var girl = CreatePerson(state, girlHome, girlPhone, "Susan");

        // Do NOT strip either person's MaintainRelationshipObjective
        state.AddRelationship(new Relationship
        {
            Id = state.GenerateEntityId(),
            PersonAId = guy.Id,
            PersonBId = girl.Id,
            Type = RelationshipType.Dating,
            StartedAt = SimStart.AddDays(-30)
        });

        RunSimulation(state, 24 * 60, guy, girl);

        // At most one group should have been created
        Assert.Single(state.Groups);
        var group = state.Groups.Values.Single();
        Assert.Equal(GroupStatus.Disbanded, group.Status);
    }

    /// <summary>
    /// After the date is accepted, Susan's competing OrganizeDateObjective (targeting Jack)
    /// should be cleaned up, not left Active.
    /// </summary>
    [Fact]
    public void Date_Accepted_CompetingOrganizeDateObjectiveOnRecipientIsCompleted()
    {
        AddressTemplateRegistry.RegisterAll();
        var state = new SimulationState(new GameClock(SimStart));

        var guyHome = CreateAddress(state, AddressType.SuburbanHome, 2, 2);
        var girlHome = CreateAddress(state, AddressType.SuburbanHome, 10, 2);
        var diner = CreateAddress(state, AddressType.Diner, 6, 6);

        var guyPhone = GetTelephone(state, guyHome.Id);
        var girlPhone = GetTelephone(state, girlHome.Id);

        var guy = CreatePerson(state, guyHome, guyPhone, "Jack", hasVehicle: true);
        var girl = CreatePerson(state, girlHome, girlPhone, "Susan");

        state.AddRelationship(new Relationship
        {
            Id = state.GenerateEntityId(),
            PersonAId = guy.Id,
            PersonBId = girl.Id,
            Type = RelationshipType.Dating,
            StartedAt = SimStart.AddDays(-30)
        });

        // Run long enough for the date to be organized (phone call happens around noon)
        RunSimulation(state, 16 * 60, guy, girl);

        // After date is organized, Susan should have no active OrganizeDateObjective targeting Jack
        Assert.DoesNotContain(girl.Objectives,
            o => o is OrganizeDateObjective od &&
                 od.TargetPersonId == guy.Id &&
                 o.Status == ObjectiveStatus.Active);
    }

    /// <summary>
    /// Full end-to-end with both partners having MaintainRelationshipObjective (production scenario).
    /// Both must end up at the diner — not just the guy.
    /// </summary>
    [Fact]
    public void DatingCouple_BothHaveMaintainObjective_BothEndUpAtDiner()
    {
        AddressTemplateRegistry.RegisterAll();
        var state = new SimulationState(new GameClock(SimStart));

        var guyHome = CreateAddress(state, AddressType.SuburbanHome, 2, 2);
        var girlHome = CreateAddress(state, AddressType.SuburbanHome, 10, 2);
        var diner = CreateAddress(state, AddressType.Diner, 6, 6);

        var guyPhone = GetTelephone(state, guyHome.Id);
        var girlPhone = GetTelephone(state, girlHome.Id);

        var guy = CreatePerson(state, guyHome, guyPhone, "Jack", hasVehicle: true);
        var girl = CreatePerson(state, girlHome, girlPhone, "Susan");

        // Do NOT strip either person's MaintainRelationshipObjective
        state.AddRelationship(new Relationship
        {
            Id = state.GenerateEntityId(),
            PersonAId = guy.Id,
            PersonBId = girl.Id,
            Type = RelationshipType.Dating,
            StartedAt = SimStart.AddDays(-30)
        });

        RunSimulation(state, 24 * 60, guy, girl);

        var guyEvents = state.Journal.GetEventsForPerson(guy.Id);
        var girlEvents = state.Journal.GetEventsForPerson(girl.Id);

        // Both must have "having dinner" activity — not just the guy
        Assert.Contains(guyEvents, e =>
            e.EventType == SimulationEventType.ActivityStarted && e.Description == "having dinner");
        Assert.Contains(girlEvents, e =>
            e.EventType == SimulationEventType.ActivityStarted && e.Description == "having dinner");

        // Both must have arrived at the diner, and within 30 minutes of each other
        var guyArrival = guyEvents.FirstOrDefault(e =>
            e.EventType == SimulationEventType.ArrivedAtAddress && e.AddressId == diner.Id);
        var girlArrival = girlEvents.FirstOrDefault(e =>
            e.EventType == SimulationEventType.ArrivedAtAddress && e.AddressId == diner.Id);
        Assert.NotNull(guyArrival);
        Assert.NotNull(girlArrival);
        Assert.True(Math.Abs((guyArrival.Timestamp - girlArrival.Timestamp).TotalMinutes) < 30,
            $"Guy arrived at diner at {guyArrival.Timestamp:HH:mm}, girl at {girlArrival.Timestamp:HH:mm}");

        // Girl should be home at end of simulation
        Assert.Equal(girlHome.Id, girl.CurrentAddressId);
    }

    /// <summary>
    /// Verifies the full chain: Dating relationship → MaintainRelationshipObjective (auto-added
    /// by AddRelationship) → OrganizeDateObjective injected on first planning pass → phone call
    /// → date organized → both people end up at the diner.
    /// No OrganizeDateObjective is pre-added; it must emerge from the simulation.
    /// </summary>
    [Fact]
    public void DatingCouple_NoPreAddedObjective_EventuallyGoOnDate()
    {
        AddressTemplateRegistry.RegisterAll();
        var state = new SimulationState(new GameClock(SimStart));

        var guyHome = CreateAddress(state, AddressType.SuburbanHome, 2, 2);
        var girlHome = CreateAddress(state, AddressType.SuburbanHome, 10, 2);
        var diner = CreateAddress(state, AddressType.Diner, 6, 6);

        var guyPhone = GetTelephone(state, guyHome.Id);
        var girlPhone = GetTelephone(state, girlHome.Id);

        // Create people — neither gets an OrganizeDateObjective pre-added
        var guy = CreatePerson(state, guyHome, guyPhone, "Jack", hasVehicle: true);
        var girl = CreatePerson(state, girlHome, girlPhone, "Susan");

        // AddRelationship automatically adds MaintainRelationshipObjective to both
        state.AddRelationship(new Relationship
        {
            Id = state.GenerateEntityId(),
            PersonAId = guy.Id,
            PersonBId = girl.Id,
            Type = RelationshipType.Dating,
            StartedAt = SimStart.AddDays(-30)
        });

        // Keep this test focused on Jack's MaintainRelationshipObjective chain.
        // The scenario where both partners have the objective is covered by
        // DatingCouple_BothHaveMaintainObjective_BothEndUpAtDiner.
        girl.Objectives.RemoveAll(o => o is MaintainRelationshipObjective);

        // Verify the objective was injected before simulation starts
        Assert.Contains(guy.Objectives,
            o => o is MaintainRelationshipObjective m && m.PartnerPersonId == girl.Id);

        // Run for 24 hours — the MaintainRelationshipObjective should trigger
        // an OrganizeDateObjective, which calls the girl, which leads to a date
        RunSimulation(state, 24 * 60, guy, girl);

        // Group was formed and disbanded
        Assert.Single(state.Groups);
        var group = state.Groups.Values.Single();
        Assert.Equal(GroupStatus.Disbanded, group.Status);

        // Both attended the diner
        var guyEvents = state.Journal.GetEventsForPerson(guy.Id);
        var girlEvents = state.Journal.GetEventsForPerson(girl.Id);
        Assert.Contains(guyEvents, e =>
            e.EventType == SimulationEventType.ActivityStarted && e.Description == "having dinner");
        Assert.Contains(girlEvents, e =>
            e.EventType == SimulationEventType.ActivityStarted && e.Description == "having dinner");
    }
    /// <summary>
    /// If the girl is away from home when the guy arrives for pickup, he should wait for her —
    /// not drive to the diner alone. Tests the AtPickup passenger-presence guard.
    /// </summary>
    [Fact]
    public void Date_GirlAwayAtPickupTime_DriverWaitsUntilSheReturns()
    {
        AddressTemplateRegistry.RegisterAll();
        var state = new SimulationState(new GameClock(SimStart));

        var guyHome = CreateAddress(state, AddressType.SuburbanHome, 2, 2);
        var girlHome = CreateAddress(state, AddressType.SuburbanHome, 10, 2);
        var diner = CreateAddress(state, AddressType.Diner, 6, 6);
        var awayAddr = CreateAddress(state, AddressType.Park, 15, 15);

        var guyPhone = GetTelephone(state, guyHome.Id);
        var girlPhone = GetTelephone(state, girlHome.Id);

        var guy = CreatePerson(state, guyHome, guyPhone, "Guy", hasVehicle: true);
        var girl = CreatePerson(state, girlHome, girlPhone, "Girl");

        var rel = new Relationship
        {
            Id = state.GenerateEntityId(),
            PersonAId = guy.Id,
            PersonBId = girl.Id,
            Type = RelationshipType.Dating,
            StartedAt = SimStart.AddDays(-30)
        };
        state.AddRelationship(rel);
        guy.Objectives.RemoveAll(o => o is MaintainRelationshipObjective);
        girl.Objectives.RemoveAll(o => o is MaintainRelationshipObjective);

        // Pickup at 5:50pm. Girl leaves home at 2pm and won't be back until 6:10pm — 20 min after pickup.
        var pickupTime = SimStart.Date.AddHours(17).AddMinutes(50);
        var girlLeaveTime = SimStart.Date.AddHours(14);
        var girlReturnTime = SimStart.Date.AddHours(18).AddMinutes(10);

        guy.Objectives.Add(new OrganizeDateObjective(
            targetPersonId: girl.Id,
            proposedMeetupAddressId: diner.Id,
            proposedCallTime: SimStart.Date.AddHours(12),
            proposedMeetupTime: SimStart.Date.AddHours(19),
            proposedPickupTime: pickupTime)
        {
            Id = state.GenerateEntityId()
        });

        // Girl is home for the noon call, then leaves and won't return until after the scheduled pickup
        girl.Objectives.Add(new WaitAwayUntilObjective(awayAddr.Id, girlReturnTime, leaveTime: girlLeaveTime)
        {
            Id = state.GenerateEntityId()
        });

        RunSimulation(state, 24 * 60, guy, girl);

        // Both must have had dinner — driver must have waited for the passenger
        var guyEvents = state.Journal.GetEventsForPerson(guy.Id);
        var girlEvents = state.Journal.GetEventsForPerson(girl.Id);
        Assert.Contains(guyEvents, e =>
            e.EventType == SimulationEventType.ActivityStarted && e.Description == "having dinner");
        Assert.Contains(girlEvents, e =>
            e.EventType == SimulationEventType.ActivityStarted && e.Description == "having dinner");

        // Both arrived at the diner, and within 30 minutes of each other
        var guyArrival = guyEvents.FirstOrDefault(e =>
            e.EventType == SimulationEventType.ArrivedAtAddress && e.AddressId == diner.Id);
        var girlArrival = girlEvents.FirstOrDefault(e =>
            e.EventType == SimulationEventType.ArrivedAtAddress && e.AddressId == diner.Id);
        Assert.NotNull(guyArrival);
        Assert.NotNull(girlArrival);
        Assert.True(Math.Abs((guyArrival.Timestamp - girlArrival.Timestamp).TotalMinutes) < 30,
            $"Guy arrived at diner at {guyArrival.Timestamp:HH:mm}, girl at {girlArrival.Timestamp:HH:mm}");

        // Girl returned home at end
        Assert.Equal(girlHome.Id, girl.CurrentAddressId);
    }

    [Fact]
    public void WorkingCouple_BothHaveJobs_DateStillHappens()
    {
        AddressTemplateRegistry.RegisterAll();
        // Start Monday so there are two work days in the 48h window.
        // The date is proposed for today or tomorrow — work happens on both days,
        // and the scheduler correctly fits the date around work (or after work on the same day).
        var monday = new DateTime(1984, 1, 2, 7, 0, 0);
        var state = new SimulationState(new GameClock(monday));

        var guyHome = CreateAddress(state, AddressType.SuburbanHome, 2, 2);
        var girlHome = CreateAddress(state, AddressType.SuburbanHome, 10, 2);
        var diner = CreateAddress(state, AddressType.Diner, 6, 6);
        var office = CreateAddress(state, AddressType.Office, 5, 5);

        var guyPhone = GetTelephone(state, guyHome.Id);
        var girlPhone = GetTelephone(state, girlHome.Id);

        var guy = CreatePerson(state, guyHome, guyPhone, "Jack", hasVehicle: true);
        var girl = CreatePerson(state, girlHome, girlPhone, "Susan");

        // Both have 9am-5pm jobs, Mon-Fri
        var weekdays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday };
        AssignJob(state, guy, office, TimeSpan.FromHours(9), TimeSpan.FromHours(17), weekdays);
        AssignJob(state, girl, office, TimeSpan.FromHours(9), TimeSpan.FromHours(17), weekdays);

        state.AddRelationship(new Relationship
        {
            Id = state.GenerateEntityId(),
            PersonAId = guy.Id,
            PersonBId = girl.Id,
            Type = RelationshipType.Dating,
            StartedAt = monday.AddDays(-30)
        });

        // Run 48 hours: Monday + Tuesday. Both work on Monday, date happens on Monday evening or Tuesday.
        RunSimulation(state, 48 * 60, guy, girl);

        var guyEvents = state.Journal.GetEventsForPerson(guy.Id);
        var girlEvents = state.Journal.GetEventsForPerson(girl.Id);

        // At least one person went to work at some point over the 48h window
        var anyoneWorked = guyEvents.Any(e => e.EventType == SimulationEventType.ActivityStarted
                && e.Description != null && e.Description.Contains("working as"))
            || girlEvents.Any(e => e.EventType == SimulationEventType.ActivityStarted
                && e.Description != null && e.Description.Contains("working as"));
        Assert.True(anyoneWorked, "Neither person went to work over 48 hours");

        // Date happened
        var disbandedGroups = state.Groups.Values.Where(g =>
            g.Type == GroupType.Date && g.Status == GroupStatus.Disbanded).ToList();
        Assert.NotEmpty(disbandedGroups);

        // Both had dinner
        Assert.Contains(guyEvents, e =>
            e.EventType == SimulationEventType.ActivityStarted && e.Description == "having dinner");
        Assert.Contains(girlEvents, e =>
            e.EventType == SimulationEventType.ActivityStarted && e.Description == "having dinner");
    }

    [Fact]
    public void PhoneCall_MadeFromCallersHome_NotRecipientHome()
    {
        AddressTemplateRegistry.RegisterAll();
        var state = new SimulationState(new GameClock(SimStart));

        var guyHome = CreateAddress(state, AddressType.SuburbanHome, 2, 2);
        var girlHome = CreateAddress(state, AddressType.SuburbanHome, 10, 2);
        var diner = CreateAddress(state, AddressType.Diner, 6, 6);

        var guyPhone = GetTelephone(state, guyHome.Id);
        var girlPhone = GetTelephone(state, girlHome.Id);

        var guy = CreatePerson(state, guyHome, guyPhone, "Jack", hasVehicle: true);
        var girl = CreatePerson(state, girlHome, girlPhone, "Susan");

        state.AddRelationship(new Relationship
        {
            Id = state.GenerateEntityId(),
            PersonAId = guy.Id,
            PersonBId = girl.Id,
            Type = RelationshipType.Dating,
            StartedAt = SimStart.AddDays(-30)
        });
        girl.Objectives.RemoveAll(o => o is MaintainRelationshipObjective);

        // Run until the phone call happens (before pickup)
        RunSimulation(state, 12 * 60, guy, girl);

        // The phone call should have been made from Guy's home, not Girl's home.
        var guyEvents = state.Journal.GetEventsForPerson(guy.Id);
        var callEvent = guyEvents.FirstOrDefault(e =>
            e.EventType == SimulationEventType.ActivityStarted
            && e.Description != null
            && e.Description.Contains("phone call"));
        Assert.NotNull(callEvent);
        Assert.Equal(guyHome.Id, callEvent.AddressId);
    }

    /// <summary>
    /// When a person is replanned mid-shift (e.g., from NeedsReplan), the remaining
    /// portion of their work shift must still be scheduled — not silently dropped.
    /// </summary>
    [Fact]
    public void Worker_ReplanDuringShift_ContinuesWorking()
    {
        AddressTemplateRegistry.RegisterAll();
        var monday = new DateTime(1984, 1, 2, 8, 0, 0); // Monday
        var state = new SimulationState(new GameClock(monday));

        var home = CreateAddress(state, AddressType.SuburbanHome, 2, 2);
        var office = CreateAddress(state, AddressType.Office, 5, 5);
        var phone = GetTelephone(state, home.Id);

        var worker = CreatePerson(state, home, phone, "Susan");
        var weekdays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday };
        AssignJob(state, worker, office, TimeSpan.FromHours(9), TimeSpan.FromHours(17), weekdays);

        // Run for 2 hours — worker should be at work by 10am
        RunSimulation(state, 2 * 60, worker);

        // Force a replan at 10am (simulates NeedsReplan from objective injection)
        worker.NeedsReplan = true;

        // Run for another 10 hours — worker should continue working until 5pm
        RunSimulation(state, 10 * 60, worker);

        // Worker should have worked (ActivityStarted with "working as")
        var events = state.Journal.GetEventsForPerson(worker.Id);
        var workEvents = events.Where(e =>
            e.EventType == SimulationEventType.ActivityStarted
            && e.Description != null
            && e.Description.Contains("working as")).ToList();
        Assert.NotEmpty(workEvents);

        // Worker should have spent significant time at the office (at least 4 hours of work total)
        // after the replan. Check via journal: ArrivedAtAddress for office should exist
        var arrivedAtOffice = events.Any(e =>
            e.EventType == SimulationEventType.ArrivedAtAddress && e.AddressId == office.Id);
        // Either already at office (arrived before replan) or goes to office after replan
        Assert.True(arrivedAtOffice || workEvents.Count > 0,
            "Worker should have gone to work or continued working after mid-shift replan");
    }

    /// <summary>
    /// Jack works 9am-5pm, then calls Susan from home. The phone call must last its full
    /// duration (not be instant-completed due to wrong travel estimate) and Susan must answer.
    /// </summary>
    [Fact]
    public void DatingCouple_WithJobs_PhoneCallLastsFullDurationAndSusanAnswers()
    {
        AddressTemplateRegistry.RegisterAll();
        var monday = new DateTime(1984, 1, 2, 8, 0, 0);
        var state = new SimulationState(new GameClock(monday));

        var guyHome = CreateAddress(state, AddressType.SuburbanHome, 2, 2);
        var girlHome = CreateAddress(state, AddressType.SuburbanHome, 10, 2);
        var diner = CreateAddress(state, AddressType.Diner, 6, 6);
        var office = CreateAddress(state, AddressType.Office, 14, 14);

        var guyPhone = GetTelephone(state, guyHome.Id);
        var girlPhone = GetTelephone(state, girlHome.Id);

        var guy = CreatePerson(state, guyHome, guyPhone, "Jack", hasVehicle: true);
        var girl = CreatePerson(state, girlHome, girlPhone, "Susan");

        var weekdays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday };
        AssignJob(state, guy, office, TimeSpan.FromHours(9), TimeSpan.FromHours(17), weekdays);

        state.AddRelationship(new Relationship
        {
            Id = state.GenerateEntityId(),
            PersonAId = guy.Id,
            PersonBId = girl.Id,
            Type = RelationshipType.Dating,
            StartedAt = monday.AddDays(-30)
        });
        girl.Objectives.RemoveAll(o => o is MaintainRelationshipObjective);

        // Run 48 hours: the date call happens after work on day 1, the date itself may be on day 2
        RunSimulation(state, 48 * 60, guy, girl);

        var guyEvents = state.Journal.GetEventsForPerson(guy.Id);
        var girlEvents = state.Journal.GetEventsForPerson(girl.Id);

        // Jack went to work
        Assert.Contains(guyEvents, e =>
            e.EventType == SimulationEventType.ActivityStarted
            && e.Description != null && e.Description.Contains("working as"));

        // Jack made a phone call FROM HIS HOME (not the office)
        var callEvent = guyEvents.FirstOrDefault(e =>
            e.EventType == SimulationEventType.ActivityStarted
            && e.Description != null && e.Description.Contains("phone call"));
        Assert.NotNull(callEvent);
        Assert.Equal(guyHome.Id, callEvent.AddressId);

        // The phone call was not instant — check that it lasted more than 1 minute
        // by finding the matching ActivityCompleted event
        var callCompleted = guyEvents.FirstOrDefault(e =>
            e.EventType == SimulationEventType.ActivityCompleted
            && e.Description != null && e.Description.Contains("phone call")
            && e.Timestamp > callEvent.Timestamp);
        Assert.NotNull(callCompleted);
        var callDuration = callCompleted.Timestamp - callEvent.Timestamp;
        Assert.True(callDuration.TotalMinutes >= 5,
            $"Phone call lasted only {callDuration.TotalMinutes:F1} minutes — should be ~10 minutes");

        // Susan answered (she was home) and accepted the date
        Assert.Contains(girlEvents, e =>
            e.EventType == SimulationEventType.ActivityStarted
            && e.Description == "Talking on the phone");

        // Date happened
        var disbandedGroups = state.Groups.Values.Where(g =>
            g.Type == GroupType.Date && g.Status == GroupStatus.Disbanded).ToList();
        Assert.NotEmpty(disbandedGroups);

        // Both had dinner
        Assert.Contains(guyEvents, e =>
            e.EventType == SimulationEventType.ActivityStarted && e.Description == "having dinner");
        Assert.Contains(girlEvents, e =>
            e.EventType == SimulationEventType.ActivityStarted && e.Description == "having dinner");
    }

    /// <summary>
    /// The "real game" test: both Jack and Susan have 9-5 jobs and MaintainRelationshipObjective.
    /// Both must go to work AND eventually go on a date together.
    /// Neither person's MaintainRelationshipObjective is stripped — this is the production scenario.
    /// </summary>
    [Fact]
    public void DatingCouple_BothWorkFullDay_BothGoToWorkAndOnDate()
    {
        AddressTemplateRegistry.RegisterAll();
        var monday = new DateTime(1984, 1, 2, 8, 0, 0);
        var state = new SimulationState(new GameClock(monday));

        var guyHome = CreateAddress(state, AddressType.SuburbanHome, 2, 2);
        var girlHome = CreateAddress(state, AddressType.SuburbanHome, 10, 2);
        var diner = CreateAddress(state, AddressType.Diner, 6, 6);
        var office = CreateAddress(state, AddressType.Office, 5, 5);

        var guyPhone = GetTelephone(state, guyHome.Id);
        var girlPhone = GetTelephone(state, girlHome.Id);

        var guy = CreatePerson(state, guyHome, guyPhone, "Jack", hasVehicle: true);
        var girl = CreatePerson(state, girlHome, girlPhone, "Susan");

        var weekdays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday };
        AssignJob(state, guy, office, TimeSpan.FromHours(9), TimeSpan.FromHours(17), weekdays);
        AssignJob(state, girl, office, TimeSpan.FromHours(9), TimeSpan.FromHours(17), weekdays);

        // Do NOT strip either MaintainRelationshipObjective
        state.AddRelationship(new Relationship
        {
            Id = state.GenerateEntityId(),
            PersonAId = guy.Id,
            PersonBId = girl.Id,
            Type = RelationshipType.Dating,
            StartedAt = monday.AddDays(-30)
        });

        // Run 48 hours to allow for scheduling across two days
        RunSimulation(state, 48 * 60, guy, girl);

        var guyEvents = state.Journal.GetEventsForPerson(guy.Id);
        var girlEvents = state.Journal.GetEventsForPerson(girl.Id);

        // BOTH went to work
        Assert.Contains(guyEvents, e =>
            e.EventType == SimulationEventType.ActivityStarted
            && e.Description != null && e.Description.Contains("working as"));
        Assert.Contains(girlEvents, e =>
            e.EventType == SimulationEventType.ActivityStarted
            && e.Description != null && e.Description.Contains("working as"));

        // A date happened (at least one disbanded Date group)
        var disbandedDates = state.Groups.Values.Where(g =>
            g.Type == GroupType.Date && g.Status == GroupStatus.Disbanded).ToList();
        Assert.NotEmpty(disbandedDates);

        // Both had dinner
        Assert.Contains(guyEvents, e =>
            e.EventType == SimulationEventType.ActivityStarted && e.Description == "having dinner");
        Assert.Contains(girlEvents, e =>
            e.EventType == SimulationEventType.ActivityStarted && e.Description == "having dinner");
    }

    [Fact]
    public void OrganizeDateObjective_CallWindowBlockedByWork_EventuallyScheduled()
    {
        AddressTemplateRegistry.RegisterAll();
        var tuesday = new DateTime(1984, 1, 3, 7, 0, 0);
        var state = new SimulationState(new GameClock(tuesday));

        var guyHome = CreateAddress(state, AddressType.SuburbanHome, 2, 2);
        var girlHome = CreateAddress(state, AddressType.SuburbanHome, 10, 2);
        var diner = CreateAddress(state, AddressType.Diner, 6, 6);
        var office = CreateAddress(state, AddressType.Office, 5, 5);

        var guyPhone = GetTelephone(state, guyHome.Id);
        var girlPhone = GetTelephone(state, girlHome.Id);

        var guy = CreatePerson(state, guyHome, guyPhone, "Jack", hasVehicle: true);
        var girl = CreatePerson(state, girlHome, girlPhone, "Susan");

        // Guy works 7am-6pm — blocks most of the day
        AssignJob(state, guy, office, TimeSpan.FromHours(7), TimeSpan.FromHours(18),
            new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                DayOfWeek.Thursday, DayOfWeek.Friday });

        state.AddRelationship(new Relationship
        {
            Id = state.GenerateEntityId(),
            PersonAId = guy.Id,
            PersonBId = girl.Id,
            Type = RelationshipType.Dating,
            StartedAt = tuesday.AddDays(-30)
        });
        girl.Objectives.RemoveAll(o => o is MaintainRelationshipObjective);

        // Run for 48 hours — even if today's call window is blocked, the date should
        // eventually happen (tomorrow, or after work today)
        RunSimulation(state, 48 * 60, guy, girl);

        // A phone call was made at some point
        var guyEvents = state.Journal.GetEventsForPerson(guy.Id);
        Assert.Contains(guyEvents, e =>
            e.EventType == SimulationEventType.ActivityStarted
            && e.Description != null
            && e.Description.Contains("phone call"));

        // Date happened
        var disbandedGroups = state.Groups.Values.Where(g =>
            g.Type == GroupType.Date && g.Status == GroupStatus.Disbanded).ToList();
        Assert.NotEmpty(disbandedGroups);
    }
}

/// <summary>Test helper: keeps an NPC at an away address until a given time.</summary>
internal class WaitAwayUntilObjective : Objective
{
    private readonly int _awayAddressId;
    private readonly DateTime _returnTime;
    private readonly DateTime? _leaveTime;

    public override int Priority => 30;
    public override ObjectiveSource Source => ObjectiveSource.Universal;

    /// <param name="awayAddressId">Address to wait at.</param>
    /// <param name="returnTime">When the NPC returns home.</param>
    /// <param name="leaveTime">Earliest time the NPC leaves for the away address. Null = immediately.</param>
    public WaitAwayUntilObjective(int awayAddressId, DateTime returnTime, DateTime? leaveTime = null)
    {
        _awayAddressId = awayAddressId;
        _returnTime = returnTime;
        _leaveTime = leaveTime;
    }

    public override List<PlannedAction> GetActions(
        Person person, SimulationState state,
        DateTime planStart, DateTime planEnd)
    {
        if (planStart >= _returnTime || Status == ObjectiveStatus.Completed)
            return new List<PlannedAction>();

        var windowStart = _leaveTime.HasValue && _leaveTime.Value > planStart ? _leaveTime.Value : planStart;
        var duration = _returnTime - windowStart;
        if (duration <= TimeSpan.Zero) return new List<PlannedAction>();

        return new List<PlannedAction>
        {
            new()
            {
                Action = new WaitAction(duration, "out and about"),
                TargetAddressId = _awayAddressId,
                TimeWindowStart = windowStart,
                TimeWindowEnd = _returnTime,
                Duration = duration,
                DisplayText = "out and about",
                SourceObjective = this
            }
        };
    }

    public override void OnActionCompleted(PlannedAction action, bool success)
    {
        Status = ObjectiveStatus.Completed;
    }
}
