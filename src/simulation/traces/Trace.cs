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

public enum RecordKind
{
    Document,       // letter, note, email
    CallLog,        // incoming/outgoing call entry on a phone
    PhoneMessage,   // answering machine message asking for a call back
}

public class Trace
{
    public int Id { get; set; }
    public TraceType Type { get; set; }
    public RecordKind? RecordKind { get; set; }  // set only for Type == Record
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
