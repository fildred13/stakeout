using System;
using System.Collections.Generic;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Objectives;

public class ObjectiveResolverTests
{
    private SimulationState MakeState() => new SimulationState();

    [Fact]
    public void CoreNeedObjectives_ProduceThreeTasks_WithCorrectPriorities()
    {
        var state = MakeState();
        var sleepObjective = ObjectiveResolver.CreateGetSleepObjective(
            new TimeSpan(22, 0, 0), new TimeSpan(7, 0, 0), homeAddressId: 1);
        var workObjective = ObjectiveResolver.CreateMaintainJobObjective(
            new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0), workAddressId: 2);
        var idleObjective = ObjectiveResolver.CreateDefaultIdleObjective(homeAddressId: 1);

        var objectives = new List<Objective> { sleepObjective, workObjective, idleObjective };
        var tasks = ObjectiveResolver.ResolveTasks(objectives, state);

        Assert.Equal(3, tasks.Count);

        // Find each task by action type
        var sleepTask = tasks.Find(t => t.ActionType == ActionType.Sleep);
        var workTask = tasks.Find(t => t.ActionType == ActionType.Work);
        var idleTask = tasks.Find(t => t.ActionType == ActionType.Idle);

        Assert.NotNull(sleepTask);
        Assert.NotNull(workTask);
        Assert.NotNull(idleTask);

        Assert.Equal(30, sleepTask.Priority);
        Assert.Equal(20, workTask.Priority);
        Assert.Equal(10, idleTask.Priority);
    }

    [Fact]
    public void GetSleepObjective_HasCorrectWindowAndAddress()
    {
        var state = MakeState();
        var sleepTime = new TimeSpan(22, 0, 0);
        var wakeTime = new TimeSpan(7, 0, 0);
        var homeAddressId = 5;

        var objective = ObjectiveResolver.CreateGetSleepObjective(sleepTime, wakeTime, homeAddressId);
        var tasks = ObjectiveResolver.ResolveTasks(new List<Objective> { objective }, state);

        Assert.Single(tasks);
        var task = tasks[0];
        Assert.Equal(ActionType.Sleep, task.ActionType);
        Assert.Equal(sleepTime, task.WindowStart);
        Assert.Equal(wakeTime, task.WindowEnd);
        Assert.Equal(homeAddressId, task.TargetAddressId);
    }

    [Fact]
    public void DefaultIdleObjective_HasZeroWindowTimes()
    {
        var state = MakeState();
        var objective = ObjectiveResolver.CreateDefaultIdleObjective(homeAddressId: 3);
        var tasks = ObjectiveResolver.ResolveTasks(new List<Objective> { objective }, state);

        Assert.Single(tasks);
        var task = tasks[0];
        Assert.Equal(TimeSpan.Zero, task.WindowStart);
        Assert.Equal(TimeSpan.Zero, task.WindowEnd);
    }

    [Fact]
    public void CompletedObjectives_ProduceNoTasks()
    {
        var state = MakeState();
        var objective = ObjectiveResolver.CreateGetSleepObjective(
            new TimeSpan(22, 0, 0), new TimeSpan(7, 0, 0), homeAddressId: 1);
        objective.Status = ObjectiveStatus.Completed;

        var tasks = ObjectiveResolver.ResolveTasks(new List<Objective> { objective }, state);

        Assert.Empty(tasks);
    }

    [Fact]
    public void BlockedObjectives_ProduceNoTasks()
    {
        var state = MakeState();
        var objective = ObjectiveResolver.CreateDefaultIdleObjective(homeAddressId: 1);
        objective.Status = ObjectiveStatus.Blocked;

        var tasks = ObjectiveResolver.ResolveTasks(new List<Objective> { objective }, state);

        Assert.Empty(tasks);
    }

    [Fact]
    public void CancelledObjectives_ProduceNoTasks()
    {
        var state = MakeState();
        var objective = ObjectiveResolver.CreateDefaultIdleObjective(homeAddressId: 1);
        objective.Status = ObjectiveStatus.Cancelled;

        var tasks = ObjectiveResolver.ResolveTasks(new List<Objective> { objective }, state);

        Assert.Empty(tasks);
    }

    [Fact]
    public void InstantSteps_ExecuteAndAdvance_BeforeNonInstantStep()
    {
        var state = MakeState();
        var instantExecuted = false;

        var objective = new Objective
        {
            Id = 1,
            Type = ObjectiveType.CommitMurder,
            Source = ObjectiveSource.CrimeTemplate,
            Priority = 50,
            IsRecurring = false,
            Status = ObjectiveStatus.Active
        };

        // Step 0: instant step
        objective.Steps.Add(new ObjectiveStep
        {
            Description = "Instant setup step",
            IsInstant = true,
            ResolveFunc = (obj, s) =>
            {
                instantExecuted = true;
                return null;
            }
        });

        // Step 1: non-instant step
        objective.Steps.Add(new ObjectiveStep
        {
            Description = "Travel to target",
            IsInstant = false,
            ActionType = ActionType.TravelByCar,
            ResolveFunc = (obj, s) => new SimTask
            {
                ObjectiveId = obj.Id,
                StepIndex = 1,
                ActionType = ActionType.TravelByCar,
                Priority = obj.Priority
            }
        });

        var tasks = ObjectiveResolver.ResolveTasks(new List<Objective> { objective }, state);

        Assert.True(instantExecuted, "Instant step should have been executed");
        Assert.Single(tasks);
        Assert.Equal(ActionType.TravelByCar, tasks[0].ActionType);
        // Objective should now be on step 1
        Assert.Equal(1, objective.CurrentStepIndex);
    }

    [Fact]
    public void MultipleInstantSteps_AllExecute_ThenResolvesFinalNonInstant()
    {
        var state = MakeState();
        int instantCount = 0;

        var objective = new Objective
        {
            Id = 2,
            Type = ObjectiveType.CommitMurder,
            Source = ObjectiveSource.CrimeTemplate,
            Priority = 50,
            IsRecurring = false,
            Status = ObjectiveStatus.Active
        };

        for (int i = 0; i < 3; i++)
        {
            objective.Steps.Add(new ObjectiveStep
            {
                Description = $"Instant step {i}",
                IsInstant = true,
                ResolveFunc = (obj, s) => { instantCount++; return null; }
            });
        }

        objective.Steps.Add(new ObjectiveStep
        {
            Description = "Final action",
            IsInstant = false,
            ActionType = ActionType.Idle,
            ResolveFunc = (obj, s) => new SimTask
            {
                ObjectiveId = obj.Id,
                StepIndex = 3,
                ActionType = ActionType.Idle,
                Priority = obj.Priority
            }
        });

        var tasks = ObjectiveResolver.ResolveTasks(new List<Objective> { objective }, state);

        Assert.Equal(3, instantCount);
        Assert.Single(tasks);
        Assert.Equal(ActionType.Idle, tasks[0].ActionType);
    }

    [Fact]
    public void SequentialObjective_AllInstantSteps_CompletesWithNoTask()
    {
        var state = MakeState();

        var objective = new Objective
        {
            Id = 3,
            Type = ObjectiveType.CommitMurder,
            Source = ObjectiveSource.CrimeTemplate,
            Priority = 50,
            IsRecurring = false,
            Status = ObjectiveStatus.Active
        };

        objective.Steps.Add(new ObjectiveStep
        {
            Description = "Only instant step",
            IsInstant = true,
            ResolveFunc = (obj, s) => null
        });

        var tasks = ObjectiveResolver.ResolveTasks(new List<Objective> { objective }, state);

        Assert.Empty(tasks);
        Assert.Equal(ObjectiveStatus.Completed, objective.Status);
    }

    [Fact]
    public void CreateGetSleepObjective_SetsUnitTagOnTask()
    {
        var state = new SimulationState();
        var objective = ObjectiveResolver.CreateGetSleepObjective(
            new TimeSpan(22, 0, 0), new TimeSpan(6, 0, 0), 1, "unit_f2_3");
        var tasks = ObjectiveResolver.ResolveTasks(new List<Objective> { objective }, state);
        Assert.Single(tasks);
        Assert.Equal("unit_f2_3", tasks[0].UnitTag);
    }

    [Fact]
    public void CreateGetSleepObjective_NullUnitTag_TaskHasNullUnitTag()
    {
        var state = new SimulationState();
        var objective = ObjectiveResolver.CreateGetSleepObjective(
            new TimeSpan(22, 0, 0), new TimeSpan(6, 0, 0), 1, null);
        var tasks = ObjectiveResolver.ResolveTasks(new List<Objective> { objective }, state);
        Assert.Single(tasks);
        Assert.Null(tasks[0].UnitTag);
    }

    [Fact]
    public void CreateDefaultIdleObjective_SetsUnitTagOnTask()
    {
        var state = new SimulationState();
        var objective = ObjectiveResolver.CreateDefaultIdleObjective(1, "unit_f1_5");
        var tasks = ObjectiveResolver.ResolveTasks(new List<Objective> { objective }, state);
        Assert.Single(tasks);
        Assert.Equal("unit_f1_5", tasks[0].UnitTag);
    }
}
