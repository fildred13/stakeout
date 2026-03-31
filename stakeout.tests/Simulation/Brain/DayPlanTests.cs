using System;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Brain;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Brain;

public class DayPlanTests
{
    private static readonly DateTime BaseDate = new DateTime(1980, 1, 1);

    private static PlannedAction MakeAction(string name, DateTime windowStart, DateTime windowEnd, TimeSpan duration)
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
            StartTime = BaseDate + TimeSpan.FromHours(6),
            EndTime = BaseDate + TimeSpan.FromHours(7),
            PlannedAction = MakeAction("test", BaseDate + TimeSpan.FromHours(6), BaseDate + TimeSpan.FromHours(7), TimeSpan.FromHours(1))
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
            StartTime = BaseDate + TimeSpan.FromHours(6),
            EndTime = BaseDate + TimeSpan.FromHours(7),
            PlannedAction = MakeAction("first", BaseDate + TimeSpan.FromHours(6), BaseDate + TimeSpan.FromHours(7), TimeSpan.FromHours(1))
        });
        plan.Entries.Add(new DayPlanEntry
        {
            StartTime = BaseDate + TimeSpan.FromHours(8),
            EndTime = BaseDate + TimeSpan.FromHours(9),
            PlannedAction = MakeAction("second", BaseDate + TimeSpan.FromHours(8), BaseDate + TimeSpan.FromHours(9), TimeSpan.FromHours(1))
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
            StartTime = BaseDate + TimeSpan.FromHours(6),
            EndTime = BaseDate + TimeSpan.FromHours(7),
            PlannedAction = MakeAction("only", BaseDate + TimeSpan.FromHours(6), BaseDate + TimeSpan.FromHours(7), TimeSpan.FromHours(1))
        });

        var next = plan.AdvanceToNext();
        Assert.Null(next);
    }
}
