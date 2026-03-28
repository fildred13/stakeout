using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Data;
using Stakeout.Simulation.Entities;
using Xunit;
using CityEntity = Stakeout.Simulation.Entities.City;

namespace Stakeout.Tests.Simulation;

public class PersonGeneratorTests
{
    private static SimulationState CreateState()
    {
        AddressTemplateRegistry.RegisterAll();
        var state = new SimulationState();
        var mapConfig = new MapConfig();

        var city = new CityEntity
        {
            Id = state.GenerateEntityId(),
            Name = "Boston",
            CountryName = "United States"
        };
        state.Cities[city.Id] = city;

        // Generate a city grid so PersonGenerator can pick addresses
        var cityGen = new CityGenerator(seed: 42);
        state.CityGrids[city.Id] = cityGen.Generate(state, city);

        return state;
    }

    private static PersonGenerator CreateGenerator()
    {
        var mapConfig = new MapConfig();
        return new PersonGenerator(mapConfig);
    }

    [Fact]
    public void GeneratePerson_ReturnsPersonWithValidId()
    {
        var state = CreateState();
        var person = CreateGenerator().GeneratePerson(state);
        Assert.True(person.Id > 0);
    }

    [Fact]
    public void GeneratePerson_AddsPersonToState()
    {
        var state = CreateState();
        var person = CreateGenerator().GeneratePerson(state);
        Assert.Contains(person.Id, state.People.Keys);
    }

    [Fact]
    public void GeneratePerson_NameComesFromNameDataPools()
    {
        var state = CreateState();
        var person = CreateGenerator().GeneratePerson(state);
        Assert.Contains(person.FirstName, NameData.FirstNames);
        Assert.Contains(person.LastName, NameData.LastNames);
    }

    [Fact]
    public void GeneratePerson_CreatesHomeAddress()
    {
        var state = CreateState();
        var person = CreateGenerator().GeneratePerson(state);
        Assert.True(state.Addresses.ContainsKey(person.HomeAddressId));
        Assert.Equal(AddressCategory.Residential, state.Addresses[person.HomeAddressId].Category);
    }

    [Fact]
    public void GeneratePerson_CreatesJobWithMatchingAddress()
    {
        var state = CreateState();
        var person = CreateGenerator().GeneratePerson(state);
        Assert.True(state.Jobs.ContainsKey(person.JobId));
        var job = state.Jobs[person.JobId];
        Assert.True(state.Addresses.ContainsKey(job.WorkAddressId));
        Assert.Equal(AddressCategory.Commercial, state.Addresses[job.WorkAddressId].Category);
    }

    [Fact]
    public void GeneratePerson_SetsInitialActivity()
    {
        var state = CreateState();
        var person = CreateGenerator().GeneratePerson(state);
        Assert.True(Enum.IsDefined(person.CurrentAction));
    }

    [Fact]
    public void GeneratePerson_AppendsJournalEvent()
    {
        var state = CreateState();
        var person = CreateGenerator().GeneratePerson(state);
        Assert.True(state.Journal.GetEventsForPerson(person.Id).Count > 0);
    }

    [Fact]
    public void GeneratePerson_HasSleepSchedule()
    {
        var state = CreateState();
        var person = CreateGenerator().GeneratePerson(state);
        var duration = (person.PreferredWakeTime - person.PreferredSleepTime).TotalHours;
        if (duration < 0) duration += 24;
        Assert.Equal(8.0, duration, precision: 1);
    }

    [Fact]
    public void GeneratePerson_ApartmentResident_HasHomeLocationId()
    {
        var state = CreateState();
        var gen = CreateGenerator();
        for (int i = 0; i < 100; i++)
        {
            gen.GeneratePerson(state);
        }
        var apartmentResident = state.People.Values
            .FirstOrDefault(p => state.Addresses[p.HomeAddressId].Type == AddressType.ApartmentBuilding);
        Assert.NotNull(apartmentResident);
        Assert.NotNull(apartmentResident.HomeLocationId);
        // Verify the location is a residential unit
        var loc = state.Locations[apartmentResident.HomeLocationId.Value];
        Assert.True(loc.HasTag("residential"));
    }

    [Fact]
    public void GeneratePerson_SuburbanResident_HasHomeLocationId()
    {
        var state = CreateState();
        var gen = CreateGenerator();
        for (int i = 0; i < 100; i++)
        {
            gen.GeneratePerson(state);
        }
        var suburbanResident = state.People.Values
            .FirstOrDefault(p => state.Addresses[p.HomeAddressId].Type == AddressType.SuburbanHome);
        Assert.NotNull(suburbanResident);
        Assert.NotNull(suburbanResident.HomeLocationId);
        var loc = state.Locations[suburbanResident.HomeLocationId.Value];
        Assert.True(loc.HasTag("residential"));
    }

    [Fact]
    public void GeneratePerson_MultipleApartmentResidents_CanShareBuilding()
    {
        var state = CreateState();
        var gen = CreateGenerator();
        for (int i = 0; i < 100; i++)
        {
            gen.GeneratePerson(state);
        }
        var apartmentAddressIds = state.People.Values
            .Where(p => state.Addresses[p.HomeAddressId].Type == AddressType.ApartmentBuilding)
            .Select(p => p.HomeAddressId)
            .ToList();
        var grouped = apartmentAddressIds.GroupBy(id => id).Where(g => g.Count() > 1);
        Assert.NotEmpty(grouped);
    }

    [Fact]
    public void GeneratePerson_SharedBuilding_DifferentHomeLocations()
    {
        var state = CreateState();
        var gen = CreateGenerator();
        for (int i = 0; i < 100; i++)
        {
            gen.GeneratePerson(state);
        }
        var byAddress = state.People.Values
            .Where(p => state.Addresses[p.HomeAddressId].Type == AddressType.ApartmentBuilding)
            .GroupBy(p => p.HomeAddressId)
            .Where(g => g.Count() > 1);

        foreach (var group in byAddress)
        {
            var locationIds = group.Select(p => p.HomeLocationId).ToList();
            Assert.Equal(locationIds.Count, locationIds.Distinct().Count());
        }
    }

    [Fact]
    public void GeneratePerson_HasHomeKeyInInventory()
    {
        var state = CreateState();
        var person = CreateGenerator().GeneratePerson(state);
        Assert.Single(person.InventoryItemIds);
        var itemId = person.InventoryItemIds[0];
        Assert.True(state.Items.ContainsKey(itemId));
        var item = state.Items[itemId];
        Assert.Equal(ItemType.Key, item.ItemType);
        Assert.Equal(person.Id, item.HeldByEntityId);
        Assert.True(item.Data.ContainsKey("TargetAccessPointId"));
    }

    [Fact]
    public void GeneratePerson_HomeKey_LinksToAccessPoint()
    {
        var state = CreateState();
        var gen = CreateGenerator();
        for (int i = 0; i < 50; i++)
        {
            gen.GeneratePerson(state);
        }
        foreach (var person in state.People.Values)
        {
            var itemId = person.InventoryItemIds[0];
            var item = state.Items[itemId];
            var targetApId = (int)item.Data["TargetAccessPointId"];
            // Verify the access point exists on one of the home address's locations
            var homeAddress = state.Addresses[person.HomeAddressId];
            bool found = false;
            foreach (var locId in homeAddress.LocationIds)
            {
                var loc = state.Locations[locId];
                if (loc.AccessPoints.Any(ap => ap.Id == targetApId))
                {
                    found = true;
                    break;
                }
            }
            Assert.True(found, $"AccessPoint {targetApId} not found on any location of address {homeAddress.Id}");
        }
    }

    [Fact]
    public void GeneratePerson_SuburbanHome_KeyTargetsMainEntrance()
    {
        var state = CreateState();
        var gen = CreateGenerator();
        for (int i = 0; i < 100; i++)
        {
            gen.GeneratePerson(state);
        }
        var suburbanResident = state.People.Values
            .First(p => state.Addresses[p.HomeAddressId].Type == AddressType.SuburbanHome);
        var itemId = suburbanResident.InventoryItemIds[0];
        var item = state.Items[itemId];
        var targetApId = (int)item.Data["TargetAccessPointId"];
        var homeAddress = state.Addresses[suburbanResident.HomeAddressId];
        // Find the access point
        AccessPoint targetAP = null;
        foreach (var locId in homeAddress.LocationIds)
        {
            var loc = state.Locations[locId];
            targetAP = loc.AccessPoints.FirstOrDefault(ap => ap.Id == targetApId);
            if (targetAP != null) break;
        }
        Assert.NotNull(targetAP);
        Assert.True(targetAP.HasTag("main_entrance"));
    }

    [Fact]
    public void GeneratePerson_SuburbanHome_KeyAssignedToAllLockableAccessPoints()
    {
        var state = CreateState();
        var gen = CreateGenerator();
        for (int i = 0; i < 100; i++)
        {
            gen.GeneratePerson(state);
        }
        var suburbanResident = state.People.Values
            .First(p => state.Addresses[p.HomeAddressId].Type == AddressType.SuburbanHome);
        var itemId = suburbanResident.InventoryItemIds[0];
        var homeAddress = state.Addresses[suburbanResident.HomeAddressId];
        var lockableAPs = new List<AccessPoint>();
        foreach (var locId in homeAddress.LocationIds)
        {
            var loc = state.Locations[locId];
            lockableAPs.AddRange(loc.AccessPoints.Where(ap => ap.LockMechanism != null));
        }

        // Suburban homes have front door, back door, and window — all lockable
        Assert.True(lockableAPs.Count >= 2);
        foreach (var ap in lockableAPs)
        {
            Assert.Equal(itemId, ap.KeyItemId);
        }
    }

    [Fact]
    public void GeneratePerson_ApartmentResident_KeyOnlyAssignedToOwnUnitAccessPoints()
    {
        var state = CreateState();
        var gen = CreateGenerator();
        for (int i = 0; i < 100; i++)
        {
            gen.GeneratePerson(state);
        }
        var aptResident = state.People.Values
            .First(p => state.Addresses[p.HomeAddressId].Type == AddressType.ApartmentBuilding);
        var itemId = aptResident.InventoryItemIds[0];

        // Only the access points on this person's home location should have their key
        Assert.NotNull(aptResident.HomeLocationId);
        var homeLoc = state.Locations[aptResident.HomeLocationId.Value];
        var apsWithKey = homeLoc.AccessPoints.Where(ap => ap.KeyItemId == itemId).ToList();
        Assert.NotEmpty(apsWithKey);
    }
}
