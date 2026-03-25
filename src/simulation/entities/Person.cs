using System;
using Godot;
using Stakeout.Simulation.Actions;

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
    public Vector2 CurrentPosition { get; set; }
    public ActionType CurrentAction { get; set; }
    public TravelInfo TravelInfo { get; set; }
    public TimeSpan PreferredSleepTime { get; set; }
    public TimeSpan PreferredWakeTime { get; set; }

    public string FullName => $"{FirstName} {LastName}";
}
