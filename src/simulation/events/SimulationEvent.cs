using System;
using Stakeout.Simulation.Actions;

namespace Stakeout.Simulation.Events;

public enum SimulationEventType
{
    DepartedAddress,
    ArrivedAtAddress,
    StartedWorking,
    StoppedWorking,
    FellAsleep,
    WokeUp,
    ActionChanged,
    PersonDied,
    CrimeCommitted,
    ObjectiveStarted,
    ObjectiveCompleted,
    TaskStarted,
    TaskCompleted
}

public class SimulationEvent
{
    public DateTime Timestamp { get; set; }
    public int PersonId { get; set; }
    public SimulationEventType EventType { get; set; }
    public int? FromAddressId { get; set; }
    public int? ToAddressId { get; set; }
    public int? AddressId { get; set; }
    public ActionType? OldAction { get; set; }
    public ActionType? NewAction { get; set; }
}
