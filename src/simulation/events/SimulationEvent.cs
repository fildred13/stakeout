using System;

namespace Stakeout.Simulation.Events;

public enum SimulationEventType
{
    DepartedAddress,
    ArrivedAtAddress,
    FellAsleep,
    WokeUp,
    PersonDied,
    CrimeCommitted,
    ActivityStarted,
    ActivityCompleted,
    DayPlanned
}

public class SimulationEvent
{
    public DateTime Timestamp { get; set; }
    public int PersonId { get; set; }
    public SimulationEventType EventType { get; set; }
    public int? FromAddressId { get; set; }
    public int? ToAddressId { get; set; }
    public int? AddressId { get; set; }
    public string Description { get; set; }
}
