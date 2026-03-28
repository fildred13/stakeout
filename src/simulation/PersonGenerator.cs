using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Data;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Scheduling;

namespace Stakeout.Simulation;

public class PersonGenerator
{
    private readonly Random _random = new();
    private readonly MapConfig _mapConfig;

    public PersonGenerator(MapConfig mapConfig)
    {
        _mapConfig = mapConfig;
    }

    public Person GeneratePerson(SimulationState state)
    {
        // 1. Pick job type and claim a matching address from the grid
        var jobType = PickJobType();
        var addressType = JobTypeToAddressType(jobType);
        var workAddress = PickAndResolveAddress(state, addressType);

        // 2. Claim a home address from the grid (random residential type)
        var homeType = _random.NextDouble() < 0.5 ? AddressType.SuburbanHome : AddressType.ApartmentBuilding;
        var homeAddress = homeType == AddressType.ApartmentBuilding
            ? FindApartmentBuilding(state)
            : PickAndResolveAddress(state, homeType);
        string homeUnitTag = null;
        if (homeType == AddressType.ApartmentBuilding)
        {
            homeUnitTag = AssignVacantUnit(state, homeAddress);
        }

        // 3. Create Job
        var job = CreateJob(state, jobType, workAddress.Id);
        state.Jobs[job.Id] = job;

        // 4. Compute commute and sleep schedule
        var commuteHours = _mapConfig.ComputeTravelTimeHours(homeAddress.Position, workAddress.Position);
        var (sleepTime, wakeTime) = SleepScheduleCalculator.Compute(job, commuteHours);

        // 5. Build objectives and schedule from them
        var objectives = new List<Objective>
        {
            ObjectiveResolver.CreateGetSleepObjective(sleepTime, wakeTime, homeAddress.Id, homeUnitTag),
            ObjectiveResolver.CreateMaintainJobObjective(job.ShiftStart, job.ShiftEnd, workAddress.Id),
            ObjectiveResolver.CreateDefaultIdleObjective(homeAddress.Id, homeUnitTag)
        };

        var tasks = ObjectiveResolver.ResolveTasks(objectives, state);
        var schedule = ScheduleBuilder.BuildFromTasks(tasks, state, _mapConfig);

        // 6. Determine initial state from schedule and current time
        var timeOfDay = state.Clock.CurrentTime.TimeOfDay;
        var currentEntry = schedule.GetEntryAtTime(timeOfDay);
        var initialActivity = currentEntry.Action;

        int? currentAddressId;
        var currentPosition = homeAddress.Position;
        if (initialActivity == ActionType.TravelByCar)
        {
            initialActivity = ActionType.Idle;
            currentAddressId = homeAddress.Id;
        }
        else if (initialActivity == ActionType.Work)
        {
            currentAddressId = workAddress.Id;
            currentPosition = workAddress.Position;
        }
        else
        {
            currentAddressId = homeAddress.Id;
        }

        // 7. Create person
        var person = new Person
        {
            Id = state.GenerateEntityId(),
            FirstName = NameData.FirstNames[_random.Next(NameData.FirstNames.Length)],
            LastName = NameData.LastNames[_random.Next(NameData.LastNames.Length)],
            CreatedAt = state.Clock.CurrentTime,
            HomeAddressId = homeAddress.Id,
            HomeUnitTag = homeUnitTag,
            JobId = job.Id,
            CurrentAddressId = currentAddressId,
            CurrentPosition = currentPosition,
            CurrentAction = initialActivity,
            CurrentSublocationId = currentEntry.TargetSublocationId,
            PreferredSleepTime = sleepTime,
            PreferredWakeTime = wakeTime,
            Objectives = objectives,
            Schedule = schedule
        };
        state.People[person.Id] = person;

        // 8. Create home key
        CreateHomeKey(state, person, homeAddress);

        // 9. Log initial event
        state.Journal.Append(new SimulationEvent
        {
            Timestamp = state.Clock.CurrentTime,
            PersonId = person.Id,
            EventType = SimulationEventType.ActionChanged,
            NewAction = initialActivity
        });

        return person;
    }

    private JobType PickJobType()
    {
        var types = Enum.GetValues<JobType>();
        return types[_random.Next(types.Length)];
    }

    private static AddressType JobTypeToAddressType(JobType jobType)
    {
        return jobType switch
        {
            JobType.DinerWaiter => AddressType.Diner,
            JobType.OfficeWorker => AddressType.Office,
            JobType.Bartender => AddressType.DiveBar,
            _ => AddressType.Office
        };
    }

    private Job CreateJob(SimulationState state, JobType jobType, int workAddressId)
    {
        var (title, shiftStart, shiftEnd) = jobType switch
        {
            JobType.DinerWaiter => ("Waiter", GenerateDinerShiftStart(), TimeSpan.Zero),
            JobType.OfficeWorker => ("Office Worker", new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0)),
            JobType.Bartender => ("Bartender", new TimeSpan(16, 0, 0), new TimeSpan(2, 0, 0)),
            _ => ("Worker", new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0))
        };

        if (jobType == JobType.DinerWaiter)
        {
            var totalMinutes = ((int)shiftStart.TotalMinutes + 720) % 1440;
            shiftEnd = TimeSpan.FromMinutes(totalMinutes);
        }

        return new Job
        {
            Id = state.GenerateEntityId(),
            Type = jobType,
            Title = title,
            WorkAddressId = workAddressId,
            ShiftStart = shiftStart,
            ShiftEnd = shiftEnd,
            WorkDays = new[] { DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday,
                              DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday }
        };
    }

    private TimeSpan GenerateDinerShiftStart()
    {
        var startHour = 5 + _random.Next(17); // 5 to 21 inclusive
        return new TimeSpan(startHour, 0, 0);
    }

    private Address PickAndResolveAddress(SimulationState state, AddressType type)
    {
        return LocationGenerator.PickAndResolveAddress(state, type, _random);
    }

    private Address FindApartmentBuilding(SimulationState state)
    {
        // First, try to find a resolved apartment building with vacancy
        if (_random.NextDouble() < 0.5)
        {
            var apartments = new List<Address>();
            foreach (var addr in state.Addresses.Values)
            {
                if (addr.Type == AddressType.ApartmentBuilding && addr.Sublocations.Count > 0)
                    apartments.Add(addr);
            }
            if (apartments.Count > 0)
            {
                var candidate = apartments[_random.Next(apartments.Count)];
                if (HasVacancy(state, candidate))
                    return candidate;
            }
        }
        // Otherwise, pick an unresolved apartment building from the grid and resolve it
        return PickAndResolveAddress(state, AddressType.ApartmentBuilding);
    }

    private static bool HasVacancy(SimulationState state, Address building)
    {
        var occupiedTags = new HashSet<string>();
        foreach (var person in state.People.Values)
        {
            if (person.HomeAddressId == building.Id && person.HomeUnitTag != null)
                occupiedTags.Add(person.HomeUnitTag);
        }

        foreach (var sub in building.Sublocations.Values)
        {
            foreach (var tag in sub.Tags)
            {
                if (tag.StartsWith("unit_f") && !occupiedTags.Contains(tag))
                    return true;
            }
        }
        return false;
    }

    private string AssignVacantUnit(SimulationState state, Address building)
    {
        var occupiedTags = new HashSet<string>();
        foreach (var person in state.People.Values)
        {
            if (person.HomeAddressId == building.Id && person.HomeUnitTag != null)
                occupiedTags.Add(person.HomeUnitTag);
        }

        var vacantTags = new HashSet<string>();
        foreach (var sub in building.Sublocations.Values)
        {
            foreach (var tag in sub.Tags)
            {
                if (tag.StartsWith("unit_f") && !occupiedTags.Contains(tag))
                    vacantTags.Add(tag);
            }
        }

        if (vacantTags.Count == 0)
            throw new InvalidOperationException($"No vacant units in building {building.Id}");

        var list = new List<string>(vacantTags);
        return list[_random.Next(list.Count)];
    }

    private static void CreateHomeKey(SimulationState state, Person person, Address homeAddress)
    {
        // Find the primary entrance connection for this person's home
        SublocationConnection entranceConn = null;
        if (person.HomeUnitTag != null)
        {
            entranceConn = homeAddress.Connections
                .FirstOrDefault(c => c.Tags != null && c.Tags.Contains(person.HomeUnitTag));
        }
        else
        {
            entranceConn = homeAddress.Connections
                .FirstOrDefault(c => c.Tags != null && c.Tags.Contains("entrance"));
        }

        if (entranceConn?.Lockable == null) return;

        var key = new Item
        {
            Id = state.GenerateEntityId(),
            ItemType = ItemType.Key,
            HeldByEntityId = person.Id,
            Fingerprints = new FingerprintSurface(),
            Data = new Dictionary<string, object>
            {
                ["TargetConnectionId"] = entranceConn.Id
            }
        };
        state.Items[key.Id] = key;
        person.InventoryItemIds.Add(key.Id);

        // Assign key to all lockable connections belonging to this person's residence
        var residenceConnections = DoorLockingService.GetResidenceLockableConnections(homeAddress, person.HomeUnitTag);
        foreach (var conn in residenceConnections)
        {
            conn.Lockable.KeyItemId = key.Id;
        }
    }
}
