using System;
using Stakeout.Simulation.Objectives;

namespace Stakeout.Simulation.Brain;

public enum DayPlanEntryStatus
{
    Pending,
    Active,
    Completed,
    Skipped
}

public class DayPlanEntry
{
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public PlannedAction PlannedAction { get; init; }
    public DayPlanEntryStatus Status { get; set; } = DayPlanEntryStatus.Pending;
}
