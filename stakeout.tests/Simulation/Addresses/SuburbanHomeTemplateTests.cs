using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Addresses;

public class SuburbanHomeTemplateTests
{
    private (SimulationState state, Address address) Generate(int seed = 42)
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, CityId = 1, Type = AddressType.SuburbanHome };
        state.Addresses[1] = address;
        new SuburbanHomeTemplate().Generate(address, state, new Random(seed));
        return (state, address);
    }

    [Fact]
    public void Generate_HasFrontYard()
    {
        var (state, addr) = Generate();
        var yard = state.FindLocationByTag(addr.Id, "exterior");
        Assert.NotNull(yard);
    }

    [Fact]
    public void Generate_HasInteriorWithEntrance()
    {
        var (state, addr) = Generate();
        var interior = state.FindLocationByTag(addr.Id, "entrance");
        Assert.NotNull(interior);
    }

    [Fact]
    public void Generate_HasLockedFrontDoor()
    {
        var (state, addr) = Generate();
        var interior = state.FindLocationByTag(addr.Id, "entrance");
        var door = interior.AccessPoints.FirstOrDefault(ap => ap.HasTag("main_entrance"));
        Assert.NotNull(door);
        Assert.True(door.IsLocked);
    }

    [Fact]
    public void Generate_HasBedroom()
    {
        var (state, addr) = Generate();
        var interior = state.FindLocationByTag(addr.Id, "residential");
        var bedroom = interior.SubLocationIds
            .Select(id => state.SubLocations[id])
            .FirstOrDefault(s => s.HasTag("bedroom"));
        Assert.NotNull(bedroom);
    }

    [Fact]
    public void Generate_HasKitchenLivingBathroom()
    {
        var (state, addr) = Generate();
        var interior = state.FindLocationByTag(addr.Id, "residential");
        var subs = interior.SubLocationIds.Select(id => state.SubLocations[id]).ToList();
        Assert.Contains(subs, s => s.HasTag("kitchen"));
        Assert.Contains(subs, s => s.HasTag("living"));
        Assert.Contains(subs, s => s.HasTag("restroom"));
    }

    [Fact]
    public void Generate_Has2To3Bedrooms()
    {
        var (state, addr) = Generate();
        var interior = state.FindLocationByTag(addr.Id, "residential");
        var bedrooms = interior.SubLocationIds
            .Select(id => state.SubLocations[id])
            .Where(s => s.HasTag("bedroom"))
            .ToList();
        Assert.InRange(bedrooms.Count, 2, 3);
    }

    [Fact]
    public void Generate_HasCovertEntry()
    {
        var (state, addr) = Generate();
        var allAccessPoints = state.GetLocationsForAddress(addr.Id)
            .SelectMany(l => l.AccessPoints);
        Assert.Contains(allAccessPoints, ap => ap.HasTag("covert_entry"));
    }

    [Fact]
    public void Generate_AllLocationsHaveCorrectAddressId()
    {
        var (state, addr) = Generate();
        foreach (var loc in state.GetLocationsForAddress(addr.Id))
        {
            Assert.Equal(addr.Id, loc.AddressId);
        }
    }
}
