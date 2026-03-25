using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;

namespace Stakeout.Simulation.Scheduling.Decomposition;

public class InhabitDecomposition : IDecompositionStrategy
{
    public List<ScheduleEntry> Decompose(SimTask task, SublocationGraph graph,
        TimeSpan startTime, TimeSpan endTime, Random rng)
    {
        bool isMorning = startTime.Hours < 12;

        var bedroom = graph.FindByTag("bedroom");
        var restroom = graph.FindByTag("restroom");
        var kitchen = graph.FindByTag("kitchen");
        var entrance = graph.FindByTag("entrance");

        var roomSequence = new List<Sublocation>();

        if (isMorning)
        {
            // Morning: bedroom → restroom → kitchen → entrance
            roomSequence.AddRange(BuildMorningRoomList(bedroom, restroom, kitchen, entrance));
        }
        else
        {
            // Evening: entrance → kitchen → living → restroom → bedroom
            var living = graph.FindByTag("living");
            roomSequence.AddRange(BuildEveningRoomList(entrance, kitchen, living, restroom, bedroom));
        }

        // Build full sublocation sequence using pathfinding between rooms
        var sublocationSequence = BuildPathSequence(roomSequence, graph);

        return AssignTimes(sublocationSequence, task.TargetAddressId, task.ActionType, startTime, endTime);
    }

    private List<Sublocation> BuildMorningRoomList(
        Sublocation bedroom, Sublocation restroom, Sublocation kitchen, Sublocation entrance)
    {
        var rooms = new List<Sublocation>();
        if (bedroom != null) rooms.Add(bedroom);
        if (restroom != null) rooms.Add(restroom);
        if (kitchen != null) rooms.Add(kitchen);
        if (entrance != null) rooms.Add(entrance);
        return rooms;
    }

    private List<Sublocation> BuildEveningRoomList(
        Sublocation entrance, Sublocation kitchen, Sublocation living,
        Sublocation restroom, Sublocation bedroom)
    {
        var rooms = new List<Sublocation>();
        if (entrance != null) rooms.Add(entrance);
        if (kitchen != null) rooms.Add(kitchen);
        if (living != null) rooms.Add(living);
        if (restroom != null) rooms.Add(restroom);
        if (bedroom != null) rooms.Add(bedroom);
        return rooms;
    }

    private List<Sublocation> BuildPathSequence(List<Sublocation> rooms, SublocationGraph graph)
    {
        if (rooms.Count == 0) return new List<Sublocation>();

        var sequence = new List<Sublocation> { rooms[0] };

        for (int i = 1; i < rooms.Count; i++)
        {
            var path = graph.FindPath(rooms[i - 1].Id, rooms[i].Id);
            if (path.Count > 1)
                sequence.AddRange(path.Skip(1));
            else if (path.Count == 1 && path[0].Id != rooms[i - 1].Id)
                sequence.Add(path[0]);
        }

        return sequence;
    }

    private List<ScheduleEntry> AssignTimes(List<Sublocation> sublocations, int? addressId,
        ActionType actionType, TimeSpan startTime, TimeSpan endTime)
    {
        if (sublocations.Count == 0) return new List<ScheduleEntry>();

        var totalDuration = endTime - startTime;
        var slotDuration = TimeSpan.FromTicks(totalDuration.Ticks / sublocations.Count);

        var entries = new List<ScheduleEntry>();
        var current = startTime;

        for (int i = 0; i < sublocations.Count; i++)
        {
            var slotEnd = (i == sublocations.Count - 1) ? endTime : current + slotDuration;
            entries.Add(new ScheduleEntry
            {
                Action = actionType,
                StartTime = current,
                EndTime = slotEnd,
                TargetAddressId = addressId,
                TargetSublocationId = sublocations[i].Id
            });
            current = slotEnd;
        }

        return entries;
    }
}
