using System;
using Stakeout.Simulation.Actions;

namespace Stakeout.Simulation.Objectives;

public class PlannedAction
{
    public IAction Action { get; init; }
    public int TargetAddressId { get; init; }
    public TimeSpan TimeWindowStart { get; init; }
    public TimeSpan TimeWindowEnd { get; init; }
    public TimeSpan Duration { get; init; }
    public string DisplayText { get; init; }
    public Objective SourceObjective { get; init; }
}
