using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Businesses;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Scheduling;
using Xunit;
using CityEntity = Stakeout.Simulation.Entities.City;

namespace Stakeout.Tests.Simulation.Scheduling;

public class DoorLockingServiceTests
{
    private static (SimulationState state, Business biz) SetupResolved()
    {
        AddressTemplateRegistry.RegisterAll();
        BusinessTemplateRegistry.RegisterAll();
        var state = new SimulationState();
        var city = new CityEntity { Id = state.GenerateEntityId(), Name = "Boston", CountryName = "US" };
        state.Cities[city.Id] = city;
        var cityGen = new CityGenerator(seed: 42);
        state.CityGrids[city.Id] = cityGen.Generate(state, city);

        var gen = new PersonGenerator(new MapConfig());
        var biz = state.Businesses.Values.First();
        BusinessResolver.Resolve(state, biz, gen);

        return (state, biz);
    }

    [Fact]
    public void UpdateDoorStates_DuringBusinessHours_UnlocksEntrance()
    {
        var (state, biz) = SetupResolved();
        var hours = biz.Hours.First(h => h.OpenTime.HasValue);
        var duringHours = new DateTime(2026, 3, 30).Date + hours.OpenTime.Value + TimeSpan.FromHours(1);
        while (duringHours.DayOfWeek != hours.Day)
            duringHours = duringHours.AddDays(1);

        DoorLockingService.UpdateDoorStates(state, duringHours);

        var locations = state.GetLocationsForAddress(biz.AddressId);
        var entrance = locations.SelectMany(l => l.AccessPoints)
            .FirstOrDefault(ap => ap.HasTag("main_entrance"));
        if (entrance != null)
            Assert.False(entrance.IsLocked);
    }

    [Fact]
    public void UpdateDoorStates_OutsideBusinessHours_LocksEntrance()
    {
        var (state, biz) = SetupResolved();
        var officeBiz = state.Businesses.Values.FirstOrDefault(b => b.Type == BusinessType.Office);
        if (officeBiz == null) return;
        if (!officeBiz.IsResolved)
        {
            var gen = new PersonGenerator(new MapConfig());
            BusinessResolver.Resolve(state, officeBiz, gen);
        }

        // Saturday = closed for offices
        var saturday = new DateTime(2026, 3, 28, 12, 0, 0);
        while (saturday.DayOfWeek != DayOfWeek.Saturday)
            saturday = saturday.AddDays(1);

        DoorLockingService.UpdateDoorStates(state, saturday);

        var locations = state.GetLocationsForAddress(officeBiz.AddressId);
        var entrance = locations.SelectMany(l => l.AccessPoints)
            .FirstOrDefault(ap => ap.HasTag("main_entrance"));
        if (entrance != null)
            Assert.True(entrance.IsLocked);
    }

    [Fact]
    public void UpdateDoorStates_SkipsUnresolvedBusinesses()
    {
        AddressTemplateRegistry.RegisterAll();
        BusinessTemplateRegistry.RegisterAll();
        var state = new SimulationState();
        var city = new CityEntity { Id = state.GenerateEntityId(), Name = "Boston", CountryName = "US" };
        state.Cities[city.Id] = city;
        var cityGen = new CityGenerator(seed: 42);
        state.CityGrids[city.Id] = cityGen.Generate(state, city);

        var now = new DateTime(2026, 3, 30, 12, 0, 0);
        DoorLockingService.UpdateDoorStates(state, now);
        // Should not throw
    }
}
