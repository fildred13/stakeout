using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Brain;
using Stakeout.Simulation.Businesses;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;
using Stakeout.Simulation.Objectives;
using Xunit;
using CityEntity = Stakeout.Simulation.Entities.City;

namespace Stakeout.Tests.Simulation.Scheduling;

/// <summary>
/// Integration tests that verify player-observable NPC scheduling behavior over multi-day runs.
/// These tests assert on journal events (what actually happened), not on plan entries (what was intended).
/// </summary>
public class NpcSchedulingIntegrationTests
{
    // 1984-01-02 is a Monday — day offsets 0-4 = Mon-Fri, 5-6 = Sat-Sun
    private static readonly DateTime SimStart = new(1984, 1, 2, 2, 0, 0); // Mid-sleep on Monday

    private static SimulationState CreateState()
    {
        AddressTemplateRegistry.RegisterAll();
        BusinessTemplateRegistry.RegisterAll();
        var state = new SimulationState(new GameClock(SimStart));
        var city = new CityEntity { Id = state.GenerateEntityId(), Name = "Boston", CountryName = "US" };
        state.Cities[city.Id] = city;
        state.CityGrids[city.Id] = new CityGenerator(seed: 42).Generate(state, city);
        return state;
    }

    /// <summary>
    /// Creates a person assigned to an office_worker position with no traits.
    /// Using SpawnRequirements ensures they get the exact role we want.
    /// Clearing traits keeps the test deterministic (no runner or foodie objectives competing).
    /// </summary>
    private static Person CreateOfficeWorkerNoTraits(SimulationState state)
    {
        Business officeBiz = null;
        Position officePos = null;

        foreach (var biz in state.Businesses.Values)
        {
            foreach (var pos in biz.Positions)
            {
                if (pos.Role == "office_worker" && pos.AssignedPersonId == null)
                {
                    officeBiz = biz;
                    officePos = pos;
                    break;
                }
            }
            if (officeBiz != null) break;
        }

        Assert.NotNull(officeBiz); // Test infra check: office businesses must exist in generated city

        var person = new PersonGenerator(new MapConfig()).GeneratePerson(
            state,
            new SpawnRequirements { BusinessId = officeBiz.Id, PositionId = officePos.Id });

        // Clear traits: we want a controlled test with only SleepObjective + WorkShiftObjective
        person.Traits.Clear();
        person.Objectives.RemoveAll(o => o.Source == ObjectiveSource.Trait);

        return person;
    }

    /// <summary>
    /// Runs the simulation for N minutes using the same continuous-replanning pattern as
    /// SimulationManager._Process. The mapConfig is shared so that NpcBrain.PlanDay uses
    /// the same travel time estimates as ActionRunner — preventing scheduling drift.
    /// </summary>
    private static void RunSimulation(Person person, SimulationState state, int minutes)
    {
        var mapConfig = new MapConfig();
        var runner = new ActionRunner(mapConfig);

        for (int i = 0; i < minutes; i++)
        {
            state.Clock.Tick(60); // 1 minute per tick

            if (person.DayPlan == null || person.DayPlan.IsExhausted)
                person.DayPlan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime, mapConfig);

            runner.Tick(person, state, TimeSpan.FromMinutes(1));
        }
    }

    /// <summary>
    /// Core scheduling test: an office worker (9am-5pm, Mon-Fri, no traits) should:
    /// - Start exactly one work session roughly at 9am on each weekday
    /// - Complete that work session roughly at 5pm on each weekday
    /// - Have no work sessions on Saturday or Sunday
    ///
    /// Tolerance: ±1 hour from 9am start / 5pm end.
    /// Assertions are on ActivityStarted/ActivityCompleted events (player-observable), not plan entries.
    /// </summary>
    [Fact]
    public void OfficeWorker_WorksOnWeekdays_NotOnWeekends_Over7Days()
    {
        // Sanity check: confirm our start date is actually Monday
        Assert.Equal(DayOfWeek.Monday, SimStart.Date.DayOfWeek);

        var state = CreateState();
        var person = CreateOfficeWorkerNoTraits(state);

        RunSimulation(person, state, 7 * 24 * 60);

        var allEvents = state.Journal.GetEventsForPerson(person.Id);
        var startDate = SimStart.Date; // 1984-01-02 (Monday)

        for (int dayOffset = 0; dayOffset < 7; dayOffset++)
        {
            var date = startDate.AddDays(dayOffset);
            var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

            var workStarted = allEvents
                .Where(e => e.EventType == SimulationEventType.ActivityStarted
                         && e.Description == "working as office_worker"
                         && e.Timestamp.Date == date)
                .ToList();

            var workCompleted = allEvents
                .Where(e => e.EventType == SimulationEventType.ActivityCompleted
                         && e.Description == "working as office_worker"
                         && e.Timestamp.Date == date)
                .ToList();

            if (isWeekend)
            {
                Assert.Empty(workStarted);
            }
            else
            {
                // Exactly one work session per weekday
                Assert.Single(workStarted);
                Assert.Single(workCompleted);

                // Starts roughly at 9am (±1 hour tolerance for commute timing)
                var startHour = workStarted[0].Timestamp.Hour;
                Assert.True(startHour >= 8 && startHour <= 10,
                    $"Work started at {workStarted[0].Timestamp:HH:mm} on {date:ddd yyyy-MM-dd}, expected roughly 9:00am (08:00-10:00)");

                // Ends roughly at 5pm (±1 hour tolerance)
                var endHour = workCompleted[0].Timestamp.Hour;
                Assert.True(endHour >= 16 && endHour <= 18,
                    $"Work ended at {workCompleted[0].Timestamp:HH:mm} on {date:ddd yyyy-MM-dd}, expected roughly 5:00pm (16:00-18:00)");
            }
        }
    }
}
