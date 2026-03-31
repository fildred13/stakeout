using System;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Scheduling;

public static class SleepScheduleCalculator
{
    private static readonly TimeSpan DefaultSleepTime = new(22, 0, 0);
    private static readonly TimeSpan DefaultWakeTime = new(6, 0, 0);
    private static readonly TimeSpan SleepDuration = new(8, 0, 0);

    public static (TimeSpan sleepTime, TimeSpan wakeTime) Compute(Position position, float commuteHours)
    {
        var commute = TimeSpan.FromHours(commuteHours);
        var workBlockStart = Mod24(position.ShiftStart - commute);
        var workBlockEnd = Mod24(position.ShiftEnd + commute);

        if (!Overlaps(DefaultSleepTime, DefaultWakeTime, workBlockStart, workBlockEnd))
        {
            return (DefaultSleepTime, DefaultWakeTime);
        }

        // Place sleep immediately after work block ends
        var sleepTime = workBlockEnd;
        var wakeTime = Mod24(sleepTime + SleepDuration);

        // If this new sleep window overlaps the work block, try placing sleep before work
        if (Overlaps(sleepTime, wakeTime, workBlockStart, workBlockEnd))
        {
            wakeTime = workBlockStart;
            sleepTime = Mod24(wakeTime - SleepDuration);
        }

        return (sleepTime, wakeTime);
    }

    private static bool Overlaps(TimeSpan aStart, TimeSpan aEnd, TimeSpan bStart, TimeSpan bEnd)
    {
        var aRanges = ToMinuteRanges(aStart, aEnd);
        var bRanges = ToMinuteRanges(bStart, bEnd);

        foreach (var a in aRanges)
        foreach (var b in bRanges)
        {
            if (a.start < b.end && b.start < a.end)
                return true;
        }
        return false;
    }

    private static (int start, int end)[] ToMinuteRanges(TimeSpan start, TimeSpan end)
    {
        int s = (int)start.TotalMinutes;
        int e = (int)end.TotalMinutes;

        if (s < e)
            return [(s, e)];

        return [(s, 1440), (0, e)];
    }

    private static TimeSpan Mod24(TimeSpan t)
    {
        var totalMinutes = ((int)t.TotalMinutes % 1440 + 1440) % 1440;
        return TimeSpan.FromMinutes(totalMinutes);
    }
}
