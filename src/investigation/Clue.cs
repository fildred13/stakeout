using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Observers;

namespace Stakeout.Investigation;

public enum ClueKind
{
    Scene, Body, Coroner, Print, PrintMatch, Item, Document, Tape, Statement, Sighting, Alibi, AlibiCheck,
    Motive, Camera, PhoneRecord, Wiretap, Lineup, Registry, Ballistics, Lab, Stakeout, Refusal
}

/// <summary>Where someone claims to have been, in their own words, with the people and places they named.</summary>
public class AlibiClaim
{
    public int PersonId { get; init; }
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public string Text { get; init; }
    /// <summary>People the claim says can vouch for it.</summary>
    public List<int> WitnessIds { get; } = new();
    /// <summary>Places the claim names (bars, diners, homes).</summary>
    public List<int> AddressIds { get; } = new();
    public bool AloneAtHome { get; init; }
    /// <summary>Truth: is the claim consistent with History for the whole window?</summary>
    public bool True { get; init; }
}

/// <summary>
/// One thing the player has found out. Text is what the player reads. The structured fields are what the
/// district attorney weighs; fields marked "truth" are hidden from the player and only used to judge whether a
/// piece of evidence really points at someone (e.g. which car a camera actually filmed).
/// </summary>
public class Clue
{
    public int Id { get; init; }
    public ClueKind Kind { get; init; }
    public string Title { get; init; }
    public string Text { get; set; }
    public DateTime FoundAt { get; init; }
    public string Source { get; init; }

    /// <summary>Whose statement, prints, alibi or belongings this is about (as far as the player knows).</summary>
    public int? AboutPersonId { get; set; }
    /// <summary>Who gave the statement.</summary>
    public int? WitnessId { get; init; }
    /// <summary>People this clue names, so the player can follow them up.</summary>
    public List<int> NamedPersonIds { get; } = new();
    public List<int> NamedAddressIds { get; } = new();

    public Sighting Sighting { get; init; }
    public List<CameraRecord> Footage { get; init; }
    public int? CameraAddressId { get; init; }

    public string PrintLabel { get; init; }
    public int? SurfaceId { get; init; }
    public string SurfaceName { get; init; }
    /// <summary>Truth: whose print it is.</summary>
    public int? PrintPersonId { get; init; }
    public bool OnWeapon { get; init; }
    public bool AtScene { get; init; }
    /// <summary>Set when the player has matched the print to a person.</summary>
    public int? MatchedPersonId { get; set; }

    public int? ItemId { get; init; }
    public bool Bloody { get; init; }
    public int? FoundAtHomeOfPersonId { get; init; }

    public DateTime? TimeFrom { get; init; }
    public DateTime? TimeTo { get; init; }

    public TensionKind? MotiveKind { get; init; }
    public int? MotivePersonId { get; init; }

    public AlibiClaim Alibi { get; init; }
    public bool? Confirms { get; init; }
    /// <summary>Truth: was the witness lying.</summary>
    public bool Lie { get; init; }

    /// <summary>Truth: the caller of a taped message or recorded call.</summary>
    public int? CallerId { get; init; }
    public DateTime? CallTime { get; init; }
    public string CallerNumber { get; init; }
    public List<(DateTime Time, string Other, int Seconds, bool Outgoing, int OtherPhoneId)> Calls { get; init; }

    public bool? Identified { get; init; }

    public override string ToString() => $"[{Kind}] {Title}: {Text}";
}
