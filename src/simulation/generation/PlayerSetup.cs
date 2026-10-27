using System;
using System.Linq;
using Stakeout.Simulation.Agents;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.History;

namespace Stakeout.Simulation.Generation;

/// <summary>Puts the detective into the world as a person like any other: a flat downtown that doubles as the office, and a car.</summary>
public static class PlayerSetup
{
    public static Agent AddPlayer(World world)
    {
        var state = world.State;
        var grid = state.CityGrids[world.CityId];
        int cx = grid.Width / 2, cy = grid.Height / 2;
        var occupied = state.Households.Values.Select(h => h.LocationId).ToHashSet();
        var unit = state.Locations.Values
            .Where(l => l.HasTag("home") && !occupied.Contains(l.Id) && state.Addresses[l.AddressId].Type == AddressType.ApartmentBuilding)
            .OrderBy(l => Math.Abs(state.Addresses[l.AddressId].GridX - cx) + Math.Abs(state.Addresses[l.AddressId].GridY - cy))
            .ThenBy(l => l.Floor).ThenBy(l => l.Id)
            .First();

        var me = new Person
        {
            Id = state.GenerateEntityId(), FirstName = "Sam", LastName = "Carver", IsPlayer = true,
            Appearance = new Appearance { Sex = Sex.Male, Age = 41, Height = Height.Tall, Build = Build.Average, Hair = HairColor.Brown },
            HomeAddressId = unit.AddressId, HomeLocationId = unit.Id, WakeTime = TimeSpan.FromHours(7), SleepTime = TimeSpan.FromHours(23)
        };
        state.People[me.Id] = me;
        var household = new Household { Id = state.GenerateEntityId(), AddressId = unit.AddressId, LocationId = unit.Id };
        household.MemberIds.Add(me.Id);
        state.Households[household.Id] = household;
        me.HouseholdId = household.Id;
        var phone = state.Phone(unit.Id);
        phone.PhoneNumber = "555-0187";
        household.PhoneId = phone.Id;
        me.HomePhoneId = phone.Id;
        unit.Name = $"{unit.Name} (Carver Investigations)";

        var car = new Vehicle
        {
            Id = state.GenerateEntityId(), OwnerPersonId = me.Id, Make = "Ford", Model = "LTD", Body = VehicleBody.Sedan,
            Color = "brown", DarkColor = true, Plate = "318-SPD"
        };
        state.Vehicles[car.Id] = car;
        me.VehicleId = car.Id;
        var runtime = new VehicleRuntime(car);
        world.Vehicles[car.Id] = runtime;
        world.Movement.Park(runtime, unit.AddressId);
        world.Phones.Index();

        var agent = new Agent(me) { ExternallyControlled = true };
        world.Agents[me.Id] = agent;
        world.PlayerId = me.Id;
        agent.AddressId = unit.AddressId;
        var (loc, sub) = world.Places.HomeRoom(me, Spot.HomeLiving);
        world.Movement.Settle(agent, loc, sub, ActivityKind.Home, "in the office");
        return agent;
    }
}
