using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Observers;

/// <summary>
/// What an observer took in about a person. Fields an observer did not catch are null; caught fields can be
/// wrong (a neighbouring value), in proportion to how good a look they had.
/// </summary>
public class PersonDescription
{
    public Sex? Sex { get; set; }
    public int? AgeDecade { get; set; }
    public Height? Height { get; set; }
    public Build? Build { get; set; }
    public HairColor? Hair { get; set; }
    public bool? Glasses { get; set; }
    public bool? Mustache { get; set; }
    public bool? Beard { get; set; }

    public string Text
    {
        get
        {
            var parts = new List<string>();
            string who = Sex switch { Entities.Sex.Male => "man", Entities.Sex.Female => "woman", _ => "person" };
            string size = Height switch { Entities.Height.Tall => "tall ", Entities.Height.Short => "short ", _ => "" };
            string build = Build switch { Entities.Build.Heavy => "heavyset ", Entities.Build.Slim => "thin ", _ => "" };
            string head = $"a {size}{build}{who}".Replace("a a", "an a");
            if (AgeDecade.HasValue) head += $" in {(Sex == Entities.Sex.Male ? "his" : Sex == Entities.Sex.Female ? "her" : "their")} {AgeDecade}s";
            parts.Add(head);
            if (Hair.HasValue) parts.Add(Hair == HairColor.Bald ? "bald" : $"{Hair.ToString().ToLowerInvariant()} hair");
            if (Glasses == true) parts.Add("glasses");
            if (Mustache == true) parts.Add("a mustache");
            if (Beard == true) parts.Add("a beard");
            return parts.Count == 1 ? parts[0] : parts[0] + ", " + string.Join(", ", parts.GetRange(1, parts.Count - 1));
        }
    }

    /// <summary>How well a real person fits this description: 1 = every caught detail matches, 0 = something contradicts.</summary>
    public double Fit(Person person)
    {
        var a = person.Appearance;
        if (Sex.HasValue && Sex != a.Sex) return 0;
        int checks = 0, hits = 0;
        void Check(bool? ok)
        {
            if (!ok.HasValue) return;
            checks++;
            if (ok.Value) hits++;
        }
        Check(AgeDecade.HasValue ? Math.Abs(AgeDecade.Value - a.Age / 10 * 10) <= 10 : null);
        Check(Height.HasValue ? Height == a.Height : null);
        Check(Build.HasValue ? Build == a.Build : null);
        Check(Hair.HasValue ? Hair == a.Hair || (Hair == HairColor.Brown && a.Hair == HairColor.Black) || (Hair == HairColor.Black && a.Hair == HairColor.Brown) : null);
        Check(Glasses.HasValue ? Glasses == a.Glasses : null);
        Check(Mustache.HasValue ? Mustache == a.Mustache : null);
        Check(Beard.HasValue ? Beard == a.Beard : null);
        return checks == 0 ? 0.5 : (double)hits / checks;
    }
}

public static class Describe
{
    /// <summary>A description of a person by an observer with a given quality of look (0-1).</summary>
    public static PersonDescription Person(Person person, Random rng, double quality)
    {
        var a = person.Appearance;
        var d = new PersonDescription();
        double see = 0.45 + quality * 0.5;
        double right = 0.7 + quality * 0.28;
        d.Sex = rng.NextDouble() < 0.98 ? a.Sex : Flip(a.Sex);
        if (rng.NextDouble() < see) d.AgeDecade = Math.Clamp(a.Age / 10 * 10 + (rng.NextDouble() < right ? 0 : rng.NextDouble() < 0.5 ? -10 : 10), 20, 70);
        if (rng.NextDouble() < see) d.Height = rng.NextDouble() < right ? a.Height : Shift(a.Height, rng);
        if (rng.NextDouble() < see * 0.8) d.Build = rng.NextDouble() < right ? a.Build : Shift(a.Build, rng);
        if (rng.NextDouble() < see) d.Hair = rng.NextDouble() < right ? a.Hair : Shift(a.Hair, rng);
        if (a.Glasses && rng.NextDouble() < see) d.Glasses = true;
        if (a.Mustache && rng.NextDouble() < see) d.Mustache = true;
        if (a.Beard && rng.NextDouble() < see) d.Beard = true;
        return d;
    }

    private static Sex Flip(Sex s) => s == Sex.Male ? Sex.Female : Sex.Male;

    private static T Shift<T>(T value, Random rng) where T : struct, Enum
    {
        var values = Enum.GetValues<T>();
        int i = Array.IndexOf(values, value);
        int j = Math.Clamp(i + (rng.NextDouble() < 0.5 ? -1 : 1), 0, values.Length - 1);
        if (j == i) j = i == 0 ? 1 : i - 1;
        return values[j];
    }

    public static bool IsDark(DateTime t) => t.Hour < 7 || t.Hour >= 18;

    /// <summary>What someone sees of a car: make, model and colour by day; only shade and shape at night.</summary>
    public static string Vehicle(Vehicle v, DateTime t, Random rng, bool closeLook)
    {
        if (IsDark(t)) return $"a {v.NightDescription}";
        string text = rng.NextDouble() < 0.75 ? $"a {v.Color} {v.Make} {v.Model}" : $"a {v.Color} {v.Body.ToString().ToLowerInvariant()}";
        if (closeLook && rng.NextDouble() < 0.4) text += $", plate starting {v.Plate[..3]}";
        return text;
    }

    public static string Clock(DateTime t) => t.ToString("h:mm tt").ToLowerInvariant();

    /// <summary>A time as a person remembers it: rounded, sometimes off.</summary>
    public static string Approximate(DateTime t, Random rng, int errorMinutes)
    {
        var shifted = t.AddMinutes(rng.Next(-errorMinutes, errorMinutes + 1));
        var rounded = new DateTime(shifted.Year, shifted.Month, shifted.Day, shifted.Hour, shifted.Minute / 15 * 15, 0);
        return $"around {Clock(rounded)}";
    }
}
