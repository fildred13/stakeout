using System;
using System.Collections.Generic;
using Stakeout.Simulation.Actions;

namespace Stakeout.Simulation.Scheduling;

public class ScheduleEntry
{
    public ActionType Action { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int? TargetAddressId { get; set; }
    public int? FromAddressId { get; set; }
    public int? TargetSublocationId { get; set; }
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
        foreach (var entry in Entries)
        {
            if (SpanContains(entry.StartTime, entry.EndTime, timeOfDay))
                return entry;
        }
        return Entries[^1];
    }

    private static bool SpanContains(TimeSpan start, TimeSpan end, TimeSpan time)
    {
        if (start <= end)
            return time >= start && time < end;
        return time >= start || time < end;
    }
}
