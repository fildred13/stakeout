using System;

namespace Stakeout.Simulation.Traces;

public enum TraceType
{
    Mark,           // blood pool, scuff marks, broken glass
    Item,           // dropped receipt, forgotten jacket
    Sighting,       // witness report of someone being somewhere
    Record,         // email, letter, phone message
    Fingerprint,    // on a surface, door, fixture
    Condition,      // wound, cause of death — attached to a person
}

public class Trace
{
    public int Id { get; set; }
    public TraceType Type { get; set; }
    public string Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Where
    public int? LocationId { get; set; }
    public int? SubLocationId { get; set; }
    public int? FixtureId { get; set; }

    // Who
    public int? CreatedByPersonId { get; set; }
    public int? AttachedToPersonId { get; set; }

    // Decay
    public int? DecayDays { get; set; }
}
