using System;
using System.Collections.Generic;
namespace Stakeout.Simulation.Entities;

public enum GroupType { Date, CriminalMeeting }
public enum GroupStatus { Forming, Active, Disbanded }
public enum GroupPhase { DriverEnRoute, AtPickup, DrivingToVenue, AtVenue, DrivingBack, Complete }

public class Group
{
    public int Id { get; set; }
    public GroupType Type { get; set; }
    public GroupStatus Status { get; set; }
    public List<int> MemberPersonIds { get; set; } = new();
    public int? DriverPersonId { get; set; }
    public int PickupAddressId { get; set; }
    public DateTime PickupTime { get; set; }
    public int MeetupAddressId { get; set; }
    public DateTime MeetupTime { get; set; }
    public GroupPhase CurrentPhase { get; set; }
}
