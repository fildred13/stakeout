using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Addresses;

public static class AddressTemplateRegistry
{
    private static readonly Dictionary<AddressType, IAddressTemplate> _templates = new();

    public static void Register(AddressType type, IAddressTemplate template)
    {
        _templates[type] = template;
    }

    public static IAddressTemplate Get(AddressType type)
    {
        return _templates.TryGetValue(type, out var template) ? template : null;
    }

    public static void RegisterAll()
    {
        Register(AddressType.SuburbanHome, new SuburbanHomeTemplate());
        Register(AddressType.ApartmentBuilding, new ApartmentBuildingTemplate());
        Register(AddressType.Diner, new DinerTemplate());
        Register(AddressType.DiveBar, new DiveBarTemplate());
        Register(AddressType.Office, new OfficeTemplate());
        Register(AddressType.Park, new ParkTemplate());
        Register(AddressType.Airport, new AirportTemplate());
    }
}
