using System;
using System.Collections.Generic;
using Godot;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Scheduling;

namespace Stakeout.Simulation.Entities;

public class Person
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime CreatedAt { get; set; }
    public int HomeAddressId { get; set; }
    public int JobId { get; set; }
    public int? CurrentAddressId { get; set; }
    public int? CurrentSublocationId { get; set; }
    public Vector2 CurrentPosition { get; set; }
    public ActionType CurrentAction { get; set; }
    public TravelInfo TravelInfo { get; set; }
    public TimeSpan PreferredSleepTime { get; set; }
    public TimeSpan PreferredWakeTime { get; set; }
    public bool IsAlive { get; set; } = true;
    public List<Objective> Objectives { get; set; } = new();
    public DailySchedule Schedule { get; set; }
    public bool NeedsScheduleRebuild { get; set; }

    public string FullName => $"{FirstName} {LastName}";
}
