using System;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Brain;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Brain;

public class DayPlanTests
{
    private static PlannedAction MakeAction(string name, TimeSpan windowStart, TimeSpan windowEnd, TimeSpan duration)
    {
        return new PlannedAction
        {
            Action = new WaitAction(duration, name),
            TargetAddressId = 1,
            TimeWindowStart = windowStart,
            TimeWindowEnd = windowEnd,
            Duration = duration,
            DisplayText = name
        };
    }

    [Fact]
    public void Empty_DayPlan_CurrentIsNull()
    {
        var plan = new DayPlan();
        Assert.Null(plan.Current);
    }

    [Fact]
    public void Current_ReturnsFirstEntry()
    {
        var plan = new DayPlan();
        plan.Entries.Add(new DayPlanEntry
        {
            StartTime = TimeSpan.FromHours(6),
            EndTime = TimeSpan.FromHours(7),
            PlannedAction = MakeAction("test", TimeSpan.FromHours(6), TimeSpan.FromHours(7), TimeSpan.FromHours(1))
        });
        Assert.NotNull(plan.Current);
        Assert.Equal("test", plan.Current.PlannedAction.DisplayText);
    }

    [Fact]
    public void AdvanceToNext_MovesToSecondEntry()
    {
        var plan = new DayPlan();
        plan.Entries.Add(new DayPlanEntry
        {
            StartTime = TimeSpan.FromHours(6),
            EndTime = TimeSpan.FromHours(7),
            PlannedAction = MakeAction("first", TimeSpan.FromHours(6), TimeSpan.FromHours(7), TimeSpan.FromHours(1))
        });
        plan.Entries.Add(new DayPlanEntry
        {
            StartTime = TimeSpan.FromHours(8),
            EndTime = TimeSpan.FromHours(9),
            PlannedAction = MakeAction("second", TimeSpan.FromHours(8), TimeSpan.FromHours(9), TimeSpan.FromHours(1))
        });

        var next = plan.AdvanceToNext();
        Assert.Equal("second", next.PlannedAction.DisplayText);
        Assert.Equal(DayPlanEntryStatus.Completed, plan.Entries[0].Status);
    }

    [Fact]
    public void AdvanceToNext_PastEnd_ReturnsNull()
    {
        var plan = new DayPlan();
        plan.Entries.Add(new DayPlanEntry
        {
            StartTime = TimeSpan.FromHours(6),
            EndTime = TimeSpan.FromHours(7),
            PlannedAction = MakeAction("only", TimeSpan.FromHours(6), TimeSpan.FromHours(7), TimeSpan.FromHours(1))
        });

        var next = plan.AdvanceToNext();
        Assert.Null(next);
    }
}
