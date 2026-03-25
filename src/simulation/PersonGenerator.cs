using System;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Data;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;
using Stakeout.Simulation.Scheduling;

namespace Stakeout.Simulation;

public class PersonGenerator
{
    private readonly Random _random = new();
    private readonly LocationGenerator _locationGenerator;
    private readonly MapConfig _mapConfig;

    public PersonGenerator(LocationGenerator locationGenerator, MapConfig mapConfig)
    {
        _locationGenerator = locationGenerator;
        _mapConfig = mapConfig;
    }

    public (Person person, DailySchedule schedule) GeneratePerson(SimulationState state)
    {
        // 1. Pick job type and generate matching address
        var jobType = PickJobType();
        var addressType = JobTypeToAddressType(jobType);
        var workAddress = _locationGenerator.GenerateAddress(state, addressType);

        // 2. Generate home address
        var homeAddress = _locationGenerator.GenerateAddress(state, AddressType.SuburbanHome);

        // 3. Create Job
        var job = CreateJob(state, jobType, workAddress.Id);
        state.Jobs[job.Id] = job;

        // 4. Compute commute and sleep schedule
        var commuteHours = _mapConfig.ComputeTravelTimeHours(homeAddress.Position, workAddress.Position);
        var (sleepTime, wakeTime) = SleepScheduleCalculator.Compute(job, commuteHours);

        // 5. Build schedule
        var goalSet = GoalSetBuilder.Build(job, sleepTime, wakeTime);
        var schedule = ScheduleBuilder.Build(goalSet, homeAddress, workAddress, _mapConfig);

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
            JobId = job.Id,
            CurrentAddressId = currentAddressId,
            CurrentPosition = currentPosition,
            CurrentAction = initialActivity,
            PreferredSleepTime = sleepTime,
            PreferredWakeTime = wakeTime
        };
        state.People[person.Id] = person;

        // 8. Log initial event
        state.Journal.Append(new SimulationEvent
        {
            Timestamp = state.Clock.CurrentTime,
            PersonId = person.Id,
            EventType = SimulationEventType.ActionChanged,
            NewAction = initialActivity
        });

        return (person, schedule);
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
}
