using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Data;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation;

public class PersonGeneratorTests
{
    private static SimulationState CreateState()
    {
        var state = new SimulationState();
        var mapConfig = new MapConfig();
        var locationGen = new LocationGenerator(mapConfig);
        locationGen.GenerateCityScaffolding(state);
        return state;
    }

    private static PersonGenerator CreateGenerator()
    {
        var mapConfig = new MapConfig();
        return new PersonGenerator(new LocationGenerator(mapConfig), mapConfig);
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
    public void GeneratePerson_ReturnsDailySchedule()
    {
        var state = CreateState();
        var person = CreateGenerator().GeneratePerson(state);
        var schedule = person.Schedule;
        Assert.NotNull(schedule);
        Assert.True(schedule.Entries.Count > 0);
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
    public void GeneratePerson_HasCoreNeedObjectives()
    {
        var state = CreateState();
        var person = CreateGenerator().GeneratePerson(state);
        Assert.Equal(3, person.Objectives.Count);
        Assert.Contains(person.Objectives, o => o.Type == ObjectiveType.GetSleep);
        Assert.Contains(person.Objectives, o => o.Type == ObjectiveType.MaintainJob);
        Assert.Contains(person.Objectives, o => o.Type == ObjectiveType.DefaultIdle);
    }

    [Fact]
    public void GeneratePerson_ApartmentResident_HasHomeUnitTag()
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
        Assert.NotNull(apartmentResident.HomeUnitTag);
        Assert.StartsWith("unit_f", apartmentResident.HomeUnitTag);
    }

    [Fact]
    public void GeneratePerson_SuburbanResident_HasNullHomeUnitTag()
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
        Assert.Null(suburbanResident.HomeUnitTag);
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
    public void GeneratePerson_SharedBuilding_DifferentUnitTags()
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
            var tags = group.Select(p => p.HomeUnitTag).ToList();
            Assert.Equal(tags.Count, tags.Distinct().Count());
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
        Assert.True(item.Data.ContainsKey("TargetConnectionId"));
    }

    [Fact]
    public void GeneratePerson_HomeKey_LinksToEntranceConnection()
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
            var targetConnId = (int)item.Data["TargetConnectionId"];
            var homeAddress = state.Addresses[person.HomeAddressId];
            var conn = homeAddress.Connections.First(c => c.Id == targetConnId);
            Assert.NotNull(conn.Lockable);
            Assert.Equal(itemId, conn.Lockable.KeyItemId);
        }
    }

    [Fact]
    public void GeneratePerson_SuburbanHome_KeyTargetsEntranceDoor()
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
        var targetConnId = (int)item.Data["TargetConnectionId"];
        var homeAddress = state.Addresses[suburbanResident.HomeAddressId];
        var conn = homeAddress.Connections.First(c => c.Id == targetConnId);
        Assert.True(conn.HasTag("entrance"));
    }

    [Fact]
    public void GeneratePerson_ApartmentResident_KeyTargetsUnitDoor()
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
        var item = state.Items[itemId];
        var targetConnId = (int)item.Data["TargetConnectionId"];
        var homeAddress = state.Addresses[aptResident.HomeAddressId];
        var conn = homeAddress.Connections.First(c => c.Id == targetConnId);
        Assert.True(conn.HasTag(aptResident.HomeUnitTag));
    }
}
