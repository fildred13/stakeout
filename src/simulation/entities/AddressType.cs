namespace Stakeout.Simulation.Entities;

public enum AddressType { SuburbanHome, Diner, DiveBar, Office }

public enum AddressCategory { Residential, Commercial }

public static class AddressTypeExtensions
{
    public static AddressCategory GetCategory(this AddressType type)
    {
        return type switch
        {
            AddressType.SuburbanHome => AddressCategory.Residential,
            _ => AddressCategory.Commercial
        };
    }
}
