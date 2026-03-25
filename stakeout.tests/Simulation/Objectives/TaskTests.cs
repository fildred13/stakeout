using System;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Objectives;

public class TaskTests
{
    [Fact]
    public void Task_DefaultValues_AreCorrect()
    {
        var task = new SimTask
        {
            Id = 1, ObjectiveId = 10, StepIndex = 0,
            ActionType = ActionType.Work, Priority = 20,
            WindowStart = new TimeSpan(9, 0, 0),
            WindowEnd = new TimeSpan(17, 0, 0),
            TargetAddressId = 5
        };
        Assert.Equal(1, task.Id);
        Assert.Equal(10, task.ObjectiveId);
        Assert.Equal(ActionType.Work, task.ActionType);
        Assert.Equal(20, task.Priority);
        Assert.Null(task.ActionData);
    }

    [Fact]
    public void Task_NullTargetAddress_DefaultsToNull()
    {
        var task = new SimTask
        {
            Id = 1, ActionType = ActionType.Idle, Priority = 10,
            WindowStart = TimeSpan.Zero, WindowEnd = TimeSpan.Zero
        };
        Assert.Null(task.TargetAddressId);
    }
}
