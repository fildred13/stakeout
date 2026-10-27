using System;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.History;

namespace Stakeout.Simulation.Crime;

public enum MurderApproach { Invited, Uninvited, BreakIn }
public enum WeaponFate { LeftAtScene, Dumped, KeptAtHome }
public enum CaseStatus { Planned, Committed, Reported, Aborted }

/// <summary>
/// The truth about one murder, filled in as it happens. The player never reads this directly: everything
/// they learn comes through observers. It exists so the crime can be replayed and the verdict judged.
/// </summary>
public class MurderCase
{
    public int Id { get; init; }
    public int KillerId { get; init; }
    public int VictimId { get; init; }
    public Tension Motive { get; init; }
    public MurderApproach Approach { get; set; }
    public WeaponKind Weapon { get; init; }
    public DateTime PlannedFor { get; set; }
    public CaseStatus Status { get; set; } = CaseStatus.Planned;

    public int SceneAddressId { get; init; }
    public int SceneLocationId { get; init; }
    public int? SceneSubLocationId { get; set; }
    public DateTime? DeathTime { get; set; }
    public int? WeaponItemId { get; set; }
    public WeaponFate? WeaponFate { get; set; }
    public int? WeaponFixtureId { get; set; }
    public int? BloodyClothesItemId { get; set; }
    public bool WipedPrints { get; set; }
    public bool Gloves { get; set; }

    /// <summary>The friend who agreed to lie about the killer's whereabouts, if any.</summary>
    public int? AccompliceId { get; set; }
    public bool AlibiAgreed { get; set; }

    public int? DiscovererId { get; set; }
    public DateTime? DiscoveredAt { get; set; }
    public DateTime? ReportedAt { get; set; }
    public DateTime? BodyRemovedAt { get; set; }

    public string MotiveSummary(SimulationState state)
    {
        var killer = state.People[KillerId];
        var victim = state.People[VictimId];
        return Motive.Kind switch
        {
            TensionKind.Debt => $"{killer.FullName} owed {victim.FullName} ${Motive.AmountDollars:N0} and could not pay.",
            TensionKind.Jealousy => $"{killer.FullName} could not accept that {victim.FirstName} had left {killer.HimHer}.",
            TensionKind.Affair => $"{killer.FullName} was having an affair with {victim.FullName}, who threatened to tell {killer.HisHer} partner.",
            TensionKind.Embezzlement => $"{victim.FullName} had found out that {killer.FullName} was stealing from their employer.",
            _ => $"{killer.FullName} held a grudge against {victim.FullName}."
        };
    }
}
