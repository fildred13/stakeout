using System;
using System.Collections.Generic;

namespace Stakeout.Simulation.Entities;

public enum BusinessType { Diner, DiveBar, Office }

public class Business
{
    public int Id { get; set; }
    public int AddressId { get; set; }
    public string Name { get; set; }
    public BusinessType Type { get; set; }
    public List<BusinessHours> Hours { get; set; } = new();
    public List<Position> Positions { get; set; } = new();
    public bool IsResolved { get; set; }
}

public class BusinessHours
{
    public DayOfWeek Day { get; set; }
    public TimeSpan? OpenTime { get; set; }
    public TimeSpan? CloseTime { get; set; }
}

public class Position
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string Role { get; set; }
    public TimeSpan ShiftStart { get; set; }
    public TimeSpan ShiftEnd { get; set; }
    public DayOfWeek[] WorkDays { get; set; }
    public int? AssignedPersonId { get; set; }
}
