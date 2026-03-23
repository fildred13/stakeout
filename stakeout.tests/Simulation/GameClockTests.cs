using System;
using Stakeout.Simulation;
using Xunit;

namespace Stakeout.Tests.Simulation;

public class GameClockTests
{
    [Fact]
    public void Constructor_Default_StartsAt1980Jan1_0830()
    {
        var clock = new GameClock();

        Assert.Equal(new DateTime(1980, 1, 1, 8, 30, 0), clock.CurrentTime);
        Assert.Equal(0.0, clock.ElapsedSeconds);
    }

    [Fact]
    public void Constructor_CustomStartTime_UsesProvidedTime()
    {
        var start = new DateTime(1980, 6, 15, 12, 0, 0);
        var clock = new GameClock(start);

        Assert.Equal(start, clock.CurrentTime);
    }

    [Fact]
    public void Tick_OneSecond_AdvancesCurrentTimeByOneSecond()
    {
        var start = new DateTime(1980, 1, 1, 0, 0, 0);
        var clock = new GameClock(start);

        clock.Tick(1.0);

        Assert.Equal(new DateTime(1980, 1, 1, 0, 0, 1), clock.CurrentTime);
        Assert.Equal(1.0, clock.ElapsedSeconds);
    }

    [Fact]
    public void Tick_MultipleCalls_AccumulatesTime()
    {
        var start = new DateTime(1980, 1, 1, 0, 0, 0);
        var clock = new GameClock(start);

        clock.Tick(0.5);
        clock.Tick(0.5);
        clock.Tick(1.0);

        Assert.Equal(2.0, clock.ElapsedSeconds);
        Assert.Equal(new DateTime(1980, 1, 1, 0, 0, 2), clock.CurrentTime);
    }

    [Fact]
    public void Tick_LargeDelta_AdvancesCorrectly()
    {
        var start = new DateTime(1980, 1, 1, 0, 0, 0);
        var clock = new GameClock(start);

        clock.Tick(3600.0); // one hour

        Assert.Equal(new DateTime(1980, 1, 1, 1, 0, 0), clock.CurrentTime);
        Assert.Equal(3600.0, clock.ElapsedSeconds);
    }

    [Fact]
    public void TimeScale_DefaultsToOne()
    {
        var clock = new GameClock();

        Assert.Equal(1.0f, clock.TimeScale);
    }
}
