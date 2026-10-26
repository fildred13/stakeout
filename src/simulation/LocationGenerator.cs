using System;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation;

/// <summary>
/// Lazily generates address interiors. Addresses themselves are placed by CityGenerator;
/// their Locations are only built when someone first needs them.
/// </summary>
public static class LocationGenerator
{
    /// <summary>
    /// Generates the interior (locations) for an address that was placed on the city grid
    /// but hasn't had its interior resolved yet. No-op if already resolved.
    /// </summary>
    public static void ResolveAddressInterior(Address address, SimulationState state, Random random)
    {
        if (address.LocationIds.Count > 0) return;

        var template = AddressTemplateRegistry.Get(address.Type);
        template?.Generate(address, state, random);
    }

    /// <summary>
    /// Picks a random unresolved address of the given type from the specified city grid
    /// and generates its interior. Used by PersonGenerator and SimulationManager
    /// to claim addresses for people and the player.
    /// </summary>
    public static Address PickAndResolveAddress(SimulationState state, AddressType type, Random random, int cityId)
    {
        if (!state.CityGrids.TryGetValue(cityId, out var cityGrid))
            throw new InvalidOperationException($"No city grid found for city {cityId}");

        var plotType = type.ToPlotType();
        var unresolvedIds = cityGrid.GetUnresolvedAddressIdsByType(plotType, state.Addresses);
        if (unresolvedIds.Count == 0)
            throw new InvalidOperationException($"No unresolved {type} addresses available on the city grid");

        var addressId = unresolvedIds[random.Next(unresolvedIds.Count)];
        var address = state.Addresses[addressId];
        ResolveAddressInterior(address, state, random);
        return address;
    }

    /// <summary>
    /// Overload that picks the first available city grid. For backward compatibility.
    /// </summary>
    public static Address PickAndResolveAddress(SimulationState state, AddressType type, Random random)
    {
        int cityId = 0;
        foreach (var key in state.CityGrids.Keys) { cityId = key; break; }
        return PickAndResolveAddress(state, type, random, cityId);
    }
}
