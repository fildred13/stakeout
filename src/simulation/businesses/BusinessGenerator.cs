using System;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Businesses;

public static class BusinessGenerator
{
    public static Business CreateBusiness(SimulationState state, Address address, Random random)
    {
        var businessType = AddressTypeToBusinessType(address.Type);
        if (businessType == null) return null;

        var template = BusinessTemplateRegistry.Get(businessType.Value);
        if (template == null) return null;

        var business = new Business
        {
            Id = state.GenerateEntityId(),
            AddressId = address.Id,
            Name = template.GenerateName(random),
            Type = businessType.Value,
            Hours = template.GenerateHours(),
            Positions = template.GeneratePositions(state, random),
            IsResolved = false
        };

        foreach (var pos in business.Positions)
            pos.BusinessId = business.Id;

        state.Businesses[business.Id] = business;
        return business;
    }

    public static void CreateBusinessesForCity(SimulationState state, Random random)
    {
        foreach (var address in state.Addresses.Values)
        {
            if (address.Category != AddressCategory.Commercial) continue;
            if (BusinessExistsForAddress(state, address.Id)) continue;
            CreateBusiness(state, address, random);
        }
    }

    private static bool BusinessExistsForAddress(SimulationState state, int addressId)
    {
        foreach (var biz in state.Businesses.Values)
        {
            if (biz.AddressId == addressId) return true;
        }
        return false;
    }

    private static BusinessType? AddressTypeToBusinessType(AddressType type)
    {
        return type switch
        {
            AddressType.Diner => BusinessType.Diner,
            AddressType.DiveBar => BusinessType.DiveBar,
            AddressType.Office => BusinessType.Office,
            _ => null
        };
    }
}
