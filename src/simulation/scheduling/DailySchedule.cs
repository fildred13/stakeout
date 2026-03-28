using System;
using System.Collections.Generic;
using Stakeout.Simulation.Actions;

namespace Stakeout.Simulation.Scheduling;

// TODO: Project 3 — this system will be rebuilt as part of the simulation overhaul.

public class ScheduleEntry
{
    public ActionType Action { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int? TargetAddressId { get; set; }
    public int? FromAddressId { get; set; }
    public int? TargetSublocationId { get; set; }
    public int? ViaConnectionId { get; set; }
    public string UnitTag { get; set; }
}

public class ScheduleGroup
{
    public ActionType Action { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int? TargetAddressId { get; set; }
    public int? FromAddressId { get; set; }
    public List<ScheduleEntry> Children { get; } = new();
}

public class DailySchedule
{
    public List<ScheduleEntry> Entries { get; } = new();
    public List<ScheduleGroup> Groups { get; } = new();

    public ScheduleEntry GetEntryAtTime(TimeSpan timeOfDay)
    {
        throw new System.NotImplementedException();
    }
}
