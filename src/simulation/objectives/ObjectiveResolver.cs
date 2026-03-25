using System;
using System.Collections.Generic;
using Stakeout.Simulation.Actions;

namespace Stakeout.Simulation.Objectives;

public static class ObjectiveResolver
{
    public static List<SimTask> ResolveTasks(List<Objective> objectives, SimulationState state)
    {
        var tasks = new List<SimTask>();

        foreach (var objective in objectives)
        {
            if (objective.Status == ObjectiveStatus.Completed ||
                objective.Status == ObjectiveStatus.Blocked ||
                objective.Status == ObjectiveStatus.Cancelled)
                continue;

            if (objective.IsRecurring)
            {
                var step = objective.CurrentStep;
                if (step != null)
                {
                    var task = step.ResolveFunc(objective, state);
                    if (task != null)
                        tasks.Add(task);
                }
            }
            else
            {
                // Process any instant steps first
                while (objective.CurrentStep != null && objective.CurrentStep.IsInstant)
                {
                    var instantStep = objective.CurrentStep;
                    instantStep.ResolveFunc(objective, state);
                    bool hasMore = objective.AdvanceStep();
                    if (!hasMore)
                        break;
                }

                // Resolve current non-instant step into a task
                if (objective.Status != ObjectiveStatus.Completed && objective.CurrentStep != null)
                {
                    var task = objective.CurrentStep.ResolveFunc(objective, state);
                    if (task != null)
                        tasks.Add(task);
                }
            }
        }

        return tasks;
    }

    public static Objective CreateGetSleepObjective(TimeSpan sleepTime, TimeSpan wakeTime, int homeAddressId)
    {
        var objective = new Objective
        {
            Type = ObjectiveType.GetSleep,
            Source = ObjectiveSource.CoreNeed,
            Priority = 30,
            IsRecurring = true,
            Status = ObjectiveStatus.Active
        };

        objective.Steps.Add(new ObjectiveStep
        {
            Description = "Sleep at home",
            ActionType = ActionType.Sleep,
            IsInstant = false,
            ResolveFunc = (obj, state) => new SimTask
            {
                ObjectiveId = obj.Id,
                StepIndex = 0,
                ActionType = ActionType.Sleep,
                Priority = obj.Priority,
                WindowStart = sleepTime,
                WindowEnd = wakeTime,
                TargetAddressId = homeAddressId
            }
        });

        return objective;
    }

    public static Objective CreateMaintainJobObjective(TimeSpan shiftStart, TimeSpan shiftEnd, int workAddressId)
    {
        var objective = new Objective
        {
            Type = ObjectiveType.MaintainJob,
            Source = ObjectiveSource.CoreNeed,
            Priority = 20,
            IsRecurring = true,
            Status = ObjectiveStatus.Active
        };

        objective.Steps.Add(new ObjectiveStep
        {
            Description = "Work at job",
            ActionType = ActionType.Work,
            IsInstant = false,
            ResolveFunc = (obj, state) => new SimTask
            {
                ObjectiveId = obj.Id,
                StepIndex = 0,
                ActionType = ActionType.Work,
                Priority = obj.Priority,
                WindowStart = shiftStart,
                WindowEnd = shiftEnd,
                TargetAddressId = workAddressId
            }
        });

        return objective;
    }

    public static Objective CreateDefaultIdleObjective(int homeAddressId)
    {
        var objective = new Objective
        {
            Type = ObjectiveType.DefaultIdle,
            Source = ObjectiveSource.CoreNeed,
            Priority = 10,
            IsRecurring = true,
            Status = ObjectiveStatus.Active
        };

        objective.Steps.Add(new ObjectiveStep
        {
            Description = "Idle at home",
            ActionType = ActionType.Idle,
            IsInstant = false,
            ResolveFunc = (obj, state) => new SimTask
            {
                ObjectiveId = obj.Id,
                StepIndex = 0,
                ActionType = ActionType.Idle,
                Priority = obj.Priority,
                WindowStart = TimeSpan.Zero,
                WindowEnd = TimeSpan.Zero,
                TargetAddressId = homeAddressId
            }
        });

        return objective;
    }
}
