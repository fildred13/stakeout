using System;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Events;

public enum SimulationEventType
{
    DepartedAddress,
    ArrivedAtAddress,
    StartedWorking,
    StoppedWorking,
    FellAsleep,
    WokeUp,
    ActivityChanged
}

public class SimulationEvent
{
    public DateTime Timestamp { get; set; }
    public int PersonId { get; set; }
    public SimulationEventType EventType { get; set; }
    public int? FromAddressId { get; set; }
    public int? ToAddressId { get; set; }
    public int? AddressId { get; set; }
    public ActivityType? OldActivity { get; set; }
    public ActivityType? NewActivity { get; set; }
}
