using System;
using System.Collections.Generic;
using Godot;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Brain;
using Stakeout.Simulation.Objectives;

namespace Stakeout.Simulation.Entities;

public class Person
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? CurrentCityId { get; set; }
    public int HomeAddressId { get; set; }
    public int? HomeLocationId { get; set; }
    public int? BusinessId { get; set; }
    public int? PositionId { get; set; }
    public int? CurrentAddressId { get; set; }
    public int? CurrentLocationId { get; set; }
    public int? CurrentSubLocationId { get; set; }
    public Vector2 CurrentPosition { get; set; }
    public TravelInfo TravelInfo { get; set; }
    public TimeSpan PreferredSleepTime { get; set; }
    public TimeSpan PreferredWakeTime { get; set; }
    public bool IsAlive { get; set; } = true;
    public List<int> InventoryItemIds { get; set; } = new();
    public int? HomePhoneFixtureId { get; set; }
    public int? VehicleId { get; set; }
    public bool NeedsReplan { get; set; }

    // New P3 fields
    public List<Objective> Objectives { get; set; } = new();
    public DayPlan DayPlan { get; set; }
    public IAction CurrentActivity { get; set; }
    public List<string> Traits { get; set; } = new();

    public string FullName => $"{FirstName} {LastName}";
}
