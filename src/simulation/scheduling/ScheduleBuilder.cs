using System;
using System.Collections.Generic;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;

namespace Stakeout.Simulation.Scheduling;

public static class ScheduleBuilder
{
    public static DailySchedule Build(GoalSet goalSet, Address home, Address work, MapConfig config)
    {
        // Step 1: For each minute of the day, determine the winning goal
        var minuteGoals = new GoalType[1440];
        for (int m = 0; m < 1440; m++)
        {
            var time = TimeSpan.FromMinutes(m);
            minuteGoals[m] = GetWinningGoal(goalSet, time);
        }

        // Step 2: Merge consecutive minutes with the same goal into blocks
        var blocks = MergeIntoBlocks(minuteGoals);

        // Step 3: Handle midnight wrapping - if first and last block have same goal, merge them
        if (blocks.Count > 1 && blocks[0].GoalType == blocks[^1].GoalType)
        {
            blocks[^1] = blocks[^1] with { EndMinute = blocks[0].EndMinute };
            blocks.RemoveAt(0);
        }

        // Step 4: Convert goal blocks to activity entries, inserting travel where needed
        var travelMinutes = (int)Math.Ceiling(config.ComputeTravelTimeHours(home.Position, work.Position) * 60);

        var schedule = new DailySchedule();
        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            var prevBlock = blocks[(i - 1 + blocks.Count) % blocks.Count];

            var currentAddress = GetAddressForGoal(block.GoalType, home, work);
            var prevAddress = GetAddressForGoal(prevBlock.GoalType, home, work);

            var blockStart = block.StartMinute;
            var blockEnd = block.EndMinute;

            // Insert travel if location changes
            if (currentAddress.Id != prevAddress.Id)
            {
                var travelStart = blockStart;
                var travelEnd = Mod1440(blockStart + travelMinutes);

                schedule.Entries.Add(new ScheduleEntry
                {
                    Action = ActionType.TravelByCar,
                    StartTime = TimeSpan.FromMinutes(travelStart),
                    EndTime = TimeSpan.FromMinutes(travelEnd),
                    FromAddressId = prevAddress.Id,
                    TargetAddressId = currentAddress.Id
                });

                blockStart = travelEnd;
            }

            schedule.Entries.Add(new ScheduleEntry
            {
                Action = GoalTypeToActivity(block.GoalType),
                StartTime = TimeSpan.FromMinutes(blockStart),
                EndTime = TimeSpan.FromMinutes(blockEnd)
            });
        }

        // Sort entries by start time for consistent ordering
        schedule.Entries.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

        return schedule;
    }

    private static GoalType GetWinningGoal(GoalSet goalSet, TimeSpan time)
    {
        var winnerType = GoalType.BeAtHome;
        int winnerPriority = int.MinValue;
        foreach (var goal in goalSet.Goals)
        {
            if (!IsGoalActive(goal, time))
                continue;
            if (goal.Priority > winnerPriority)
            {
                winnerType = goal.Type;
                winnerPriority = goal.Priority;
            }
        }
        return winnerType;
    }

    private static bool IsGoalActive(Goal goal, TimeSpan time)
    {
        var start = goal.WindowStart;
        var end = goal.WindowEnd;

        // WindowStart == WindowEnd means always active (24h)
        if (start == end)
            return true;

        if (start <= end)
            return time >= start && time < end;

        // Wraps midnight
        return time >= start || time < end;
    }

    private record GoalBlock(GoalType GoalType, int StartMinute, int EndMinute);

    private static List<GoalBlock> MergeIntoBlocks(GoalType[] minuteGoals)
    {
        var blocks = new List<GoalBlock>();
        int blockStart = 0;
        var currentType = minuteGoals[0];

        for (int m = 1; m < 1440; m++)
        {
            if (minuteGoals[m] != currentType)
            {
                blocks.Add(new GoalBlock(currentType, blockStart, m));
                blockStart = m;
                currentType = minuteGoals[m];
            }
        }
        // Final block wraps to 1440 (which equals minute 0 of next day)
        blocks.Add(new GoalBlock(currentType, blockStart, 1440));

        return blocks;
    }

    private static Address GetAddressForGoal(GoalType goalType, Address home, Address work)
    {
        return goalType == GoalType.BeAtWork ? work : home;
    }

    private static ActionType GoalTypeToActivity(GoalType goalType)
    {
        return goalType switch
        {
            GoalType.BeAtWork => ActionType.Work,
            GoalType.Sleep => ActionType.Sleep,
            _ => ActionType.Idle
        };
    }

    private static int Mod1440(int minutes)
    {
        return ((minutes % 1440) + 1440) % 1440;
    }

    // --- Task-based API ---

    public static DailySchedule BuildFromTasks(List<SimTask> tasks, Dictionary<int, Address> addresses, MapConfig config)
    {
        // Step 1: For each minute of the day, determine the winning task
        var minuteTasks = new SimTask[1440];
        for (int m = 0; m < 1440; m++)
        {
            var time = TimeSpan.FromMinutes(m);
            minuteTasks[m] = GetWinningTask(tasks, time);
        }

        // Step 2: Merge consecutive minutes with the same task into blocks
        var blocks = MergeIntoTaskBlocks(minuteTasks);

        // Step 3: Handle midnight wrapping - if first and last block have same task, merge them
        if (blocks.Count > 1 && blocks[0].Task.Id == blocks[^1].Task.Id)
        {
            blocks[^1] = blocks[^1] with { EndMinute = blocks[0].EndMinute };
            blocks.RemoveAt(0);
        }

        // Step 4: Convert task blocks to activity entries, inserting travel where needed
        var schedule = new DailySchedule();
        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            var prevBlock = blocks[(i - 1 + blocks.Count) % blocks.Count];

            var currentAddressId = block.Task.TargetAddressId;
            var prevAddressId = prevBlock.Task.TargetAddressId;

            var blockStart = block.StartMinute;
            var blockEnd = block.EndMinute;

            // Insert travel if location changes
            if (currentAddressId != prevAddressId && currentAddressId.HasValue && prevAddressId.HasValue)
            {
                var fromAddr = addresses[prevAddressId.Value];
                var toAddr = addresses[currentAddressId.Value];
                var travelMinutes = (int)Math.Ceiling(config.ComputeTravelTimeHours(fromAddr.Position, toAddr.Position) * 60);

                var travelStart = blockStart;
                var travelEnd = Mod1440(blockStart + travelMinutes);

                schedule.Entries.Add(new ScheduleEntry
                {
                    Action = ActionType.TravelByCar,
                    StartTime = TimeSpan.FromMinutes(travelStart),
                    EndTime = TimeSpan.FromMinutes(travelEnd),
                    FromAddressId = prevAddressId.Value,
                    TargetAddressId = currentAddressId.Value
                });

                blockStart = travelEnd;
            }

            schedule.Entries.Add(new ScheduleEntry
            {
                Action = block.Task.ActionType,
                StartTime = TimeSpan.FromMinutes(blockStart),
                EndTime = TimeSpan.FromMinutes(blockEnd),
                TargetAddressId = currentAddressId
            });
        }

        // Sort entries by start time for consistent ordering
        schedule.Entries.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

        return schedule;
    }

    private static SimTask GetWinningTask(List<SimTask> tasks, TimeSpan time)
    {
        SimTask winner = null;
        int winnerPriority = int.MinValue;
        foreach (var task in tasks)
        {
            if (!IsTaskActive(task, time))
                continue;
            if (task.Priority > winnerPriority)
            {
                winner = task;
                winnerPriority = task.Priority;
            }
        }
        return winner;
    }

    private static bool IsTaskActive(SimTask task, TimeSpan time)
    {
        var start = task.WindowStart;
        var end = task.WindowEnd;

        // WindowStart == WindowEnd means always active (24h)
        if (start == end)
            return true;

        if (start <= end)
            return time >= start && time < end;

        // Wraps midnight
        return time >= start || time < end;
    }

    private record TaskBlock(SimTask Task, int StartMinute, int EndMinute);

    private static List<TaskBlock> MergeIntoTaskBlocks(SimTask[] minuteTasks)
    {
        var blocks = new List<TaskBlock>();
        int blockStart = 0;
        var currentTask = minuteTasks[0];

        for (int m = 1; m < 1440; m++)
        {
            if (minuteTasks[m].Id != currentTask.Id)
            {
                blocks.Add(new TaskBlock(currentTask, blockStart, m));
                blockStart = m;
                currentTask = minuteTasks[m];
            }
        }
        // Final block wraps to 1440 (which equals minute 0 of next day)
        blocks.Add(new TaskBlock(currentTask, blockStart, 1440));

        return blocks;
    }
}
