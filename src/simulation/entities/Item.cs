using System.Collections.Generic;

namespace Stakeout.Simulation.Entities;

public enum ItemType
{
    Key, Knife, Handgun, BluntObject, Diary, AddressBook, Letter, Ledger, Photo, BloodyClothes,
    Wiretap, Glass, CigaretteButt, Receipt
}

/// <summary>
/// A movable object. It is held by a person, kept in a fixture, or lying in a room. Items are touch
/// surfaces too: their id is the surface id for fingerprints.
/// </summary>
public class Item
{
    public int Id { get; set; }
    public ItemType ItemType { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    /// <summary>Readable content (letters, diary entries, address book lines).</summary>
    public List<string> Text { get; set; } = new();
    public int? HeldByPersonId { get; set; }
    public int? FixtureId { get; set; }
    public int? LocationId { get; set; }
    public int? SubLocationId { get; set; }
    public int? OwnerPersonId { get; set; }
    public bool Bloody { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}
