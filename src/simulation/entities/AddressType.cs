using Stakeout.Simulation.City;

namespace Stakeout.Simulation.Entities;

public enum AddressType { SuburbanHome, Diner, DiveBar, Office, ApartmentBuilding, Park, Airport }

public enum AddressCategory { Residential, Commercial, Public }

public static class AddressTypeExtensions
{
    public static AddressCategory GetCategory(this AddressType type)
    {
        return type switch
        {
            AddressType.SuburbanHome => AddressCategory.Residential,
            AddressType.ApartmentBuilding => AddressCategory.Residential,
            AddressType.Park => AddressCategory.Public,
            AddressType.Airport => AddressCategory.Public,
            _ => AddressCategory.Commercial
        };
    }

    public static PlotType ToPlotType(this AddressType type) => type switch
    {
        AddressType.SuburbanHome => PlotType.SuburbanHome,
        AddressType.ApartmentBuilding => PlotType.ApartmentBuilding,
        AddressType.Office => PlotType.Office,
        AddressType.Diner => PlotType.Diner,
        AddressType.DiveBar => PlotType.DiveBar,
        AddressType.Park => PlotType.Park,
        AddressType.Airport => PlotType.Airport,
        _ => throw new System.InvalidOperationException($"{type} has no PlotType mapping")
    };
}
