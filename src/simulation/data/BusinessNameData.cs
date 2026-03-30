using System;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Data;

public static class BusinessNameData
{
    private static readonly string[] DinerAdjectives =
        { "Golden", "Silver", "Lucky", "Blue", "Red", "Sunny", "Happy", "Cozy" };
    private static readonly string[] DinerNouns =
        { "Spoon", "Plate", "Griddle", "Skillet", "Fork", "Cup", "Kettle", "Pan" };

    private static readonly string[] BarAdjectives =
        { "Rusty", "Blind", "Brass", "Iron", "Broken", "Crooked", "Lucky", "Dusty", "Salty", "Smoky" };
    private static readonly string[] BarNouns =
        { "Nail", "Pig", "Monkey", "Anchor", "Barrel", "Lantern", "Horseshoe", "Parrot", "Compass", "Rudder" };

    private static readonly string[] OfficeSuffixes =
        { "Associates", "Partners", "Group", "Consulting", "Solutions", "Industries", "Corp", "Holdings" };

    public static string GenerateName(BusinessType type, Random random)
    {
        return type switch
        {
            BusinessType.Diner => GenerateDinerName(random),
            BusinessType.DiveBar => GenerateBarName(random),
            BusinessType.Office => GenerateOfficeName(random),
            _ => $"Business #{random.Next(1000)}"
        };
    }

    private static string GenerateDinerName(Random random)
    {
        var pattern = random.Next(3);
        return pattern switch
        {
            0 => $"{NameData.FirstNames[random.Next(NameData.FirstNames.Length)]}'s Diner",
            1 => $"{NameData.LastNames[random.Next(NameData.LastNames.Length)]}'s",
            _ => $"The {Pick(DinerAdjectives, random)} {Pick(DinerNouns, random)}"
        };
    }

    private static string GenerateBarName(Random random)
    {
        return $"The {Pick(BarAdjectives, random)} {Pick(BarNouns, random)}";
    }

    private static string GenerateOfficeName(Random random)
    {
        var pattern = random.Next(2);
        return pattern switch
        {
            0 => $"{Pick(NameData.LastNames, random)} & {Pick(NameData.LastNames, random)}",
            _ => $"{Pick(NameData.LastNames, random)} {Pick(OfficeSuffixes, random)}"
        };
    }

    private static string Pick(string[] array, Random random) => array[random.Next(array.Length)];
}
