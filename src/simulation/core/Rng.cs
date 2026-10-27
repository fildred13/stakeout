using System;

namespace Stakeout.Simulation.Core;

/// <summary>
/// Deterministic randomness keyed by purpose. Every decision draws from its own stream
/// (seed + keys), so adding a person or a behavior never reshuffles anyone else's dice.
/// Never use string.GetHashCode or new Random() in the simulation.
/// </summary>
public static class Rng
{
    public static ulong Mix(ulong x)
    {
        // splitmix64 finalizer
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        return x ^ (x >> 31);
    }

    public static ulong Hash(int seed, long a, long b = 0, long c = 0, long d = 0)
    {
        ulong h = Mix((ulong)(uint)seed);
        h = Mix(h ^ (ulong)a);
        h = Mix(h ^ (ulong)b);
        h = Mix(h ^ (ulong)c);
        h = Mix(h ^ (ulong)d);
        return h;
    }

    /// <summary>A uniform value in [0, 1) for one keyed decision.</summary>
    public static double Unit(int seed, long a, long b = 0, long c = 0, long d = 0) =>
        (Hash(seed, a, b, c, d) >> 11) * (1.0 / (1UL << 53));

    public static bool Chance(double p, int seed, long a, long b = 0, long c = 0, long d = 0) =>
        Unit(seed, a, b, c, d) < p;

    /// <summary>A System.Random for a sequence of draws belonging to one keyed decision.</summary>
    public static Random For(int seed, long a, long b = 0, long c = 0, long d = 0) =>
        new Random((int)(Hash(seed, a, b, c, d) & 0x7FFFFFFF));

    /// <summary>Stable key for a string (FNV-1a), for purposes named in text.</summary>
    public static long Key(string s)
    {
        ulong h = 14695981039346656037UL;
        foreach (char ch in s)
        {
            h ^= ch;
            h *= 1099511628211UL;
        }
        return (long)h;
    }

    public static T Pick<T>(this Random random, System.Collections.Generic.IReadOnlyList<T> list) =>
        list[random.Next(list.Count)];
}

/// <summary>Named purposes for keyed randomness, so streams never collide by accident.</summary>
public static class Purpose
{
    public const long Population = 1;
    public const long DayPlan = 2;
    public const long Social = 3;
    public const long Call = 4;
    public const long Crime = 5;
    public const long Witness = 6;
    public const long Prints = 7;
    public const long Camera = 8;
    public const long Interview = 9;
    public const long Scene = 10;
    public const long Discovery = 11;
    public const long Reaction = 12;
    public const long Player = 13;
}
