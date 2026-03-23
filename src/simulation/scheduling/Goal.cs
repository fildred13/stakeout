using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Scheduling;

public enum GoalType
{
    BeAtWork,
    BeAtHome,
    Sleep
}

public class Goal
{
    public GoalType Type { get; set; }
    public int Priority { get; set; }
    public TimeSpan WindowStart { get; set; }
    public TimeSpan WindowEnd { get; set; }
}

public class GoalSet
{
    public List<Goal> Goals { get; } = new();
}

public static class GoalSetBuilder
{
    public static GoalSet Build(Job job, TimeSpan sleepTime, TimeSpan wakeTime)
    {
        var goalSet = new GoalSet();

        goalSet.Goals.Add(new Goal
        {
            Type = GoalType.Sleep,
            Priority = 30,
            WindowStart = sleepTime,
            WindowEnd = wakeTime
        });

        goalSet.Goals.Add(new Goal
        {
            Type = GoalType.BeAtWork,
            Priority = 20,
            WindowStart = job.ShiftStart,
            WindowEnd = job.ShiftEnd
        });

        goalSet.Goals.Add(new Goal
        {
            Type = GoalType.BeAtHome,
            Priority = 10,
            WindowStart = TimeSpan.Zero,
            WindowEnd = TimeSpan.Zero  // Always active (24h)
        });

        return goalSet;
    }
}
