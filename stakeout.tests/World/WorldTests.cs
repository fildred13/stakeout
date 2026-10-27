using System;
using System.Linq;
using System.Text;
using Stakeout.Simulation;
using Stakeout.Simulation.Agents;
using Stakeout.Simulation.Diagnostics;
using Stakeout.Simulation.Generation;
using Stakeout.Simulation.History;
using SimWorld = Stakeout.Simulation.World;
using Xunit;

namespace Stakeout.Tests.WorldSim;

/// <summary>A week of a fully populated city, shared by the tests that only read it.</summary>
public class WeekFixture
{
    public SimWorld World { get; }

    public WeekFixture()
    {
        World = WorldBuilder.Build(7);
        World.Start();
        World.AdvanceTo(World.Now.AddDays(7));
    }
}

public class WorldWeekTests : IClassFixture<WeekFixture>
{
    private readonly SimWorld _world;

    public WorldWeekTests(WeekFixture fixture)
    {
        _world = fixture.World;
    }

    [Fact]
    public void AWeekOfTheCityBreaksNoLawOfPhysics()
    {
        var violations = new InvariantChecker(_world).CheckAll();
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    [Fact]
    public void EveryJobIsFilledAndEveryoneHasAHome()
    {
        Assert.All(_world.State.Businesses.Values.SelectMany(b => b.Positions), p => Assert.NotNull(p.AssignedPersonId));
        Assert.All(_world.State.People.Values, p => Assert.True(p.HomeLocationId > 0));
        Assert.True(_world.State.People.Count > 3000);
    }

    [Fact]
    public void DatesHappenAndTheCoupleRidesInOneCar()
    {
        var dates = _world.Joints.OfType<DateActivity>().Where(d => d.Finished && !d.Aborted).ToList();
        Assert.True(dates.Count > 20, $"only {dates.Count} dates");
        foreach (var date in dates)
        {
            var toVenue = _world.History.TripsOf(date.DriverId)
                .First(t => t.Depart >= date.Start.AddMinutes(-1) && t.ToAddressId == date.VenueAddressId);
            Assert.NotNull(toVenue.VehicleId);
            Assert.Equal(new[] { date.DriverId, date.PassengerId }, toVenue.PersonIds);
        }
    }

    [Fact]
    public void ArrangementsComeFromPhoneCalls()
    {
        var calls = _world.History.Calls;
        Assert.Contains(calls, c => c.Outcome == CallOutcome.Answered);
        Assert.Contains(calls, c => c.Outcome == CallOutcome.AnsweringMachine);
        foreach (var date in _world.Joints.OfType<DateActivity>().Take(30))
        {
            // Someone in the couple spoke to the other before the date existed.
            bool spoke = calls.Any(c => c.Time < date.Start &&
                                        ((c.CallerId == date.DriverId && c.AnsweredById == date.PassengerId) ||
                                         (c.CallerId == date.PassengerId && c.AnsweredById == date.DriverId) ||
                                         (c.Outcome == CallOutcome.AnsweringMachine &&
                                          (c.CallerId == date.DriverId || c.CallerId == date.PassengerId))));
            Assert.True(spoke, $"date {date.Id} was arranged without a call");
        }
    }
}

public class DeterminismTests
{
    private static string Signature(SimWorld world)
    {
        var sb = new StringBuilder();
        foreach (var t in world.History.Trips)
            sb.Append(t.Depart.Ticks).Append(':').Append(string.Join(",", t.PersonIds)).Append('>').Append(t.ToAddressId).Append(';');
        foreach (var c in world.History.Calls)
            sb.Append(c.Time.Ticks).Append(c.CallerId).Append(c.Outcome).Append(';');
        return sb.ToString();
    }

    [Fact]
    public void SameSeedGivesTheSameHistoryAtAnyTimeScale()
    {
        var jump = WorldBuilder.Build(11);
        jump.Start();
        jump.AdvanceTo(jump.Now.AddDays(2));

        var steps = WorldBuilder.Build(11);
        steps.Start();
        var end = steps.Now.AddDays(2);
        var rng = new Random(3);
        while (steps.Now < end)
        {
            // Irregular frame times, like a game running at varying speed.
            var next = steps.Now.AddSeconds(rng.Next(1, 200));
            steps.AdvanceTo(next < end ? next : end);
        }

        Assert.True(jump.History.Trips.Count > 1000);
        Assert.Equal(Signature(jump), Signature(steps));
    }
}
