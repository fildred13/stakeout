using System;
using System.Collections.Generic;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation;

namespace Stakeout.Simulation.Objectives;

public enum ObjectiveType
{
    MaintainJob,
    GetSleep,
    DefaultIdle,
    CommitMurder
}

public enum ObjectiveSource
{
    CoreNeed,
    Trait,
    CrimeTemplate,
    Assignment
}

public enum ObjectiveStatus
{
    Active,
    Completed,
    Blocked,
    Cancelled
}

public enum StepStatus
{
    Pending,
    Active,
    Completed,
    Failed
}

public class ObjectiveStep
{
    public string Description { get; set; }
    public StepStatus Status { get; set; } = StepStatus.Pending;
    public ActionType? ActionType { get; set; }
    public bool IsInstant { get; set; }
    public Func<Objective, SimulationState, SimTask> ResolveFunc { get; set; }
}

public class Objective
{
    public int Id { get; set; }
    public ObjectiveType Type { get; set; }
    public ObjectiveSource Source { get; set; }
    public int? SourceEntityId { get; set; }
    public int Priority { get; set; }
    public ObjectiveStatus Status { get; set; } = ObjectiveStatus.Active;
    public List<ObjectiveStep> Steps { get; set; } = new();
    public int CurrentStepIndex { get; set; }
    public bool IsRecurring { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();

    public ObjectiveStep CurrentStep =>
        CurrentStepIndex < Steps.Count ? Steps[CurrentStepIndex] : null;

    public bool AdvanceStep()
    {
        if (CurrentStepIndex < Steps.Count)
            Steps[CurrentStepIndex].Status = StepStatus.Completed;
        CurrentStepIndex++;
        if (CurrentStepIndex >= Steps.Count)
        {
            Status = ObjectiveStatus.Completed;
            return false;
        }
        Steps[CurrentStepIndex].Status = StepStatus.Active;
        return true;
    }
}
