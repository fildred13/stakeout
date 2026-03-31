using System;
namespace Stakeout.Simulation.Entities;

public enum RelationshipType { Dating, Friend, CriminalAssociate }

public class Relationship
{
    public int Id { get; set; }
    public int PersonAId { get; set; }
    public int PersonBId { get; set; }
    public RelationshipType Type { get; set; }
    public DateTime StartedAt { get; set; }
}
