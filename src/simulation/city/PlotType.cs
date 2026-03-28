using System;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.City;

public enum PlotType
{
    Empty,
    Road,
    SuburbanHome,
    ApartmentBuilding,
    Office,
    Diner,
    DiveBar,
    Park,
    Airport
}

public enum FacingDirection
{
    North,
    South,
    East,
    West
}

public static class PlotTypeExtensions
{
    public static (int Width, int Height) GetSize(this PlotType type) => type switch
    {
        PlotType.ApartmentBuilding => (3, 3),
        PlotType.Office => (3, 3),
        PlotType.Park => (2, 2),
        PlotType.Airport => (10, 20),
        _ => (1, 1)
    };

    public static bool IsBuilding(this PlotType type) =>
        type != PlotType.Road && type != PlotType.Empty;

    public static AddressType ToAddressType(this PlotType type) => type switch
    {
        PlotType.SuburbanHome => AddressType.SuburbanHome,
        PlotType.ApartmentBuilding => AddressType.ApartmentBuilding,
        PlotType.Office => AddressType.Office,
        PlotType.Diner => AddressType.Diner,
        PlotType.DiveBar => AddressType.DiveBar,
        PlotType.Park => AddressType.Park,
        PlotType.Airport => AddressType.Airport,
        _ => throw new InvalidOperationException($"{type} is not a building type")
    };
}
