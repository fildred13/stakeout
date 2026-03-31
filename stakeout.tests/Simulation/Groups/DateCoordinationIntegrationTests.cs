using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
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
}
