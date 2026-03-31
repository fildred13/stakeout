using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Objectives;

public class GoOnDateObjective : Objective
{
    public int GroupId { get; }

    public override int Priority => 70;
    public override ObjectiveSource Source => ObjectiveSource.Social;

    public GoOnDateObjective(int groupId)
    {
        GroupId = groupId;
    }

    public override List<PlannedAction> GetActions(Person person, SimulationState state,
        DateTime planStart, DateTime planEnd)
    {
        if (!state.Groups.TryGetValue(GroupId, out var group)) return new List<PlannedAction>();
        if (group.Status == GroupStatus.Disbanded) return new List<PlannedAction>();

        bool isDriver = person.Id == group.DriverPersonId;

        return group.CurrentPhase switch
        {
            GroupPhase.DriverEnRoute => GetDriverEnRouteActions(person, group, isDriver, planStart),
            GroupPhase.AtPickup => GetAtPickupActions(group, planStart),
            GroupPhase.DrivingToVenue => GetDrivingToVenueActions(group, planStart),
            GroupPhase.AtVenue => GetAtVenueActions(group),
            GroupPhase.DrivingBack => GetDrivingBackActions(person, group, planStart),
            _ => new List<PlannedAction>()
        };
    }

    private List<PlannedAction> GetDriverEnRouteActions(Person person, Group group, bool isDriver, DateTime planStart)
    {
        if (isDriver)
        {
            return new List<PlannedAction>
            {
                new()
                {
                    Action = new WaitAction(TimeSpan.FromMinutes(1), "driving to pick up date"),
                    TargetAddressId = group.PickupAddressId,
                    TimeWindowStart = group.PickupTime - TimeSpan.FromHours(2),
                    TimeWindowEnd = group.PickupTime + TimeSpan.FromHours(1),
                    Duration = TimeSpan.FromMinutes(1),
                    DisplayText = "driving to pick up date",
                    SourceObjective = this
                }
            };
        }
        else
        {
            // Passenger: wait at home until pickup time
            var waitDuration = group.PickupTime - planStart;
            if (waitDuration <= TimeSpan.Zero) waitDuration = TimeSpan.FromMinutes(1);
            return new List<PlannedAction>
            {
                new()
                {
                    Action = new WaitAction(waitDuration, "waiting to be picked up"),
                    TargetAddressId = person.HomeAddressId,
                    TimeWindowStart = planStart,
                    TimeWindowEnd = group.PickupTime + TimeSpan.FromHours(1),
                    Duration = waitDuration,
                    DisplayText = "waiting to be picked up",
                    SourceObjective = this
                }
            };
        }
    }

    private List<PlannedAction> GetAtPickupActions(Group group, DateTime planStart)
    {
        return new List<PlannedAction>
        {
            new()
            {
                Action = new WaitAction(TimeSpan.FromMinutes(1), "getting in the car"),
                TargetAddressId = group.PickupAddressId,
                TimeWindowStart = planStart,
                TimeWindowEnd = planStart + TimeSpan.FromHours(2),
                Duration = TimeSpan.FromMinutes(1),
                DisplayText = "getting in the car",
                SourceObjective = this
            }
        };
    }

    private List<PlannedAction> GetDrivingToVenueActions(Group group, DateTime planStart)
    {
        return new List<PlannedAction>
        {
            new()
            {
                Action = new WaitAction(TimeSpan.FromMinutes(1), "driving to dinner"),
                TargetAddressId = group.MeetupAddressId,
                TimeWindowStart = planStart,
                TimeWindowEnd = planStart + TimeSpan.FromHours(3),
                Duration = TimeSpan.FromMinutes(1),
                DisplayText = "driving to dinner",
                SourceObjective = this
            }
        };
    }

    private List<PlannedAction> GetAtVenueActions(Group group)
    {
        return new List<PlannedAction>
        {
            new()
            {
                Action = new WaitAction(TimeSpan.FromHours(2), "having dinner"),
                TargetAddressId = group.MeetupAddressId,
                TimeWindowStart = group.MeetupTime,
                TimeWindowEnd = group.MeetupTime + TimeSpan.FromHours(4),
                Duration = TimeSpan.FromHours(2),
                DisplayText = "having dinner",
                SourceObjective = this
            }
        };
    }

    private List<PlannedAction> GetDrivingBackActions(Person person, Group group, DateTime planStart)
    {
        return new List<PlannedAction>
        {
            new()
            {
                Action = new WaitAction(TimeSpan.FromMinutes(1), "heading home"),
                TargetAddressId = group.PickupAddressId,  // passenger's home = pickup address
                TimeWindowStart = planStart,
                TimeWindowEnd = planStart + TimeSpan.FromHours(4),
                Duration = TimeSpan.FromMinutes(1),
                DisplayText = "heading home",
                SourceObjective = this
            }
        };
    }

    public override void OnActionCompletedWithState(PlannedAction action, Person person,
        SimulationState state, bool success)
    {
        if (!success) return;
        if (!state.Groups.TryGetValue(GroupId, out var group)) return;

        bool isDriver = person.Id == group.DriverPersonId;
        if (!isDriver) return;  // only driver advances the phase

        var passengerId = group.MemberPersonIds.First(id => id != person.Id);
        var passenger = state.People[passengerId];

        switch (group.CurrentPhase)
        {
            case GroupPhase.DriverEnRoute:
                group.CurrentPhase = GroupPhase.AtPickup;
                person.NeedsReplan = true;
                passenger.NeedsReplan = true;
                break;

            case GroupPhase.AtPickup:
                // Only advance if the passenger is actually at the pickup address.
                // If she's not there yet, keep the driver waiting (NeedsReplan re-schedules the wait).
                if (passenger.CurrentAddressId != group.PickupAddressId)
                {
                    person.NeedsReplan = true;
                    break;
                }
                group.CurrentPhase = GroupPhase.DrivingToVenue;
                person.NeedsReplan = true;
                passenger.NeedsReplan = true;
                break;

            case GroupPhase.DrivingToVenue:
                group.CurrentPhase = GroupPhase.AtVenue;
                person.NeedsReplan = true;
                passenger.NeedsReplan = true;
                break;

            case GroupPhase.AtVenue:
                group.CurrentPhase = GroupPhase.DrivingBack;
                person.NeedsReplan = true;
                passenger.NeedsReplan = true;
                break;

            case GroupPhase.DrivingBack:
                group.CurrentPhase = GroupPhase.Complete;
                group.Status = GroupStatus.Disbanded;
                person.Objectives.RemoveAll(o => o is GoOnDateObjective g && g.GroupId == GroupId);
                passenger.Objectives.RemoveAll(o => o is GoOnDateObjective g && g.GroupId == GroupId);
                person.NeedsReplan = true;
                passenger.NeedsReplan = true;
                break;
        }
    }
}
