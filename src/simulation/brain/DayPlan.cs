using System.Collections.Generic;

namespace Stakeout.Simulation.Brain;

public class DayPlan
{
    public List<DayPlanEntry> Entries { get; } = new();
    public int CurrentIndex { get; set; }

    public DayPlanEntry Current =>
        CurrentIndex < Entries.Count ? Entries[CurrentIndex] : null;

    public DayPlanEntry AdvanceToNext()
    {
        if (CurrentIndex < Entries.Count)
            Entries[CurrentIndex].Status = DayPlanEntryStatus.Completed;
        CurrentIndex++;
        return Current;
    }
}
