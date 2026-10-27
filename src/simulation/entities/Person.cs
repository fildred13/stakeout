using System;
using System.Collections.Generic;

namespace Stakeout.Simulation.Entities;

public enum Sex { Male, Female }
public enum Height { Short, Average, Tall }
public enum Build { Slim, Average, Heavy }
public enum HairColor { Black, Brown, Blond, Red, Gray, Bald }

/// <summary>What a witness can see of a person.</summary>
public class Appearance
{
    public Sex Sex { get; set; }
    public int Age { get; set; }
    public Height Height { get; set; }
    public Build Build { get; set; }
    public HairColor Hair { get; set; }
    public bool Glasses { get; set; }
    public bool Mustache { get; set; }
    public bool Beard { get; set; }

    public string AgeBand => Age switch
    {
        < 30 => "in their twenties",
        < 40 => "in their thirties",
        < 50 => "in their forties",
        < 60 => "in their fifties",
        _ => "in their sixties"
    };
}

public class Person
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public Appearance Appearance { get; set; } = new();
    public int HouseholdId { get; set; }
    public int HomeAddressId { get; set; }
    public int HomeLocationId { get; set; }
    public int? BusinessId { get; set; }
    public int? PositionId { get; set; }
    public int? VehicleId { get; set; }
    public int? HomePhoneId { get; set; }
    public HashSet<string> Traits { get; set; } = new();
    public string CigaretteBrand { get; set; }
    public TimeSpan WakeTime { get; set; }
    public TimeSpan SleepTime { get; set; }
    public List<int> FavoriteDinerIds { get; set; } = new();
    public List<int> FavoriteBarIds { get; set; } = new();
    public int? FavoriteParkId { get; set; }
    /// <summary>Office workers sit on one floor of their building.</summary>
    public int? WorkLocationId { get; set; }
    public bool IsPlayer { get; set; }
    public bool IsAlive { get; set; } = true;
    public List<int> InventoryItemIds { get; set; } = new();

    public string FullName => $"{FirstName} {LastName}";
    public bool Has(string trait) => Traits.Contains(trait);
    public string HeShe => Appearance.Sex == Sex.Male ? "he" : "she";
    public string HimHer => Appearance.Sex == Sex.Male ? "him" : "her";
    public string HisHer => Appearance.Sex == Sex.Male ? "his" : "her";
    public string ManWoman => Appearance.Sex == Sex.Male ? "man" : "woman";
}

public static class Traits
{
    public const string Drinker = "drinker";
    public const string Foodie = "foodie";
    public const string Runner = "runner";
    public const string Homebody = "homebody";
    public const string Smoker = "smoker";
    public const string Violent = "violent";
    public const string Careful = "careful";
    public const string Observant = "observant";
    public const string NightOwl = "night_owl";
    public const string GunOwner = "gun_owner";
    public const string CriminalRecord = "criminal_record";
}

/// <summary>People who share a home.</summary>
public class Household
{
    public int Id { get; set; }
    public int AddressId { get; set; }
    public int LocationId { get; set; }
    public List<int> MemberIds { get; } = new();
    public int PhoneId { get; set; }
}
