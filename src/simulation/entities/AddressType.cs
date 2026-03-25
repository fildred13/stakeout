namespace Stakeout.Simulation.Entities;

public enum AddressType { SuburbanHome, Diner, DiveBar, Office, ApartmentBuilding, Park }

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
            _ => AddressCategory.Commercial
        };
    }
}
