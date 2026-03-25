using System;
using System.Collections.Generic;

namespace Stakeout.Simulation.Traces;

public enum TraceType
{
    Item, Sighting, Mark, Condition, Record
}

public class Trace
{
    public int Id { get; set; }
    public TraceType TraceType { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedByPersonId { get; set; }
    public int? LocationId { get; set; }
    public int? AttachedToPersonId { get; set; }
    public string Description { get; set; }
    public Dictionary<string, object> Data { get; set; }
}
