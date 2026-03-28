using System.Collections.Generic;

namespace Stakeout.Simulation.Entities;

public enum ItemType
{
    Key
}

public class Item
{
    public int Id { get; set; }
    public ItemType ItemType { get; set; }
    public int? HeldByEntityId { get; set; }
    public int? LocationAddressId { get; set; }
    public int? LocationId { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
    // TODO: Project 2 (Traces) — fingerprint tracking will be rebuilt
}
