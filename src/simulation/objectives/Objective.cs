using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Objectives;

public abstract class Objective
{
    public int Id { get; set; }
    public abstract int Priority { get; }
    public abstract ObjectiveSource Source { get; }
    public ObjectiveStatus Status { get; set; } = ObjectiveStatus.Active;
    public List<Objective> Children { get; } = new();

    public abstract List<PlannedAction> GetActions(
        Person person,
        SimulationState state,
        DateTime planStart,
        DateTime planEnd);

    public virtual void OnActionCompleted(PlannedAction action, bool success) { }

    public virtual void OnActionCompletedWithState(
        PlannedAction action, Person person, SimulationState state, bool success) { }

    /// <summary>
    /// Called by ActionRunner after an action completes successfully.
    /// Override to emit traces at the action's location.
    /// </summary>
    public virtual void EmitTraces(PlannedAction action, Person person, SimulationState state) { }
}
