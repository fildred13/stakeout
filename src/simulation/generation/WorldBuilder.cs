using System;
using System.Linq;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Agents;
using Stakeout.Simulation.Businesses;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Core;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Space;
using CityEntity = Stakeout.Simulation.Entities.City;

namespace Stakeout.Simulation.Generation;

/// <summary>Creates a complete, populated world from a seed. Same seed, same world.</summary>
public static class WorldBuilder
{
    public static readonly DateTime DefaultStart = new(1984, 10, 8, 3, 0, 0);

    public static World Build(int seed, DateTime? start = null)
    {
        AddressTemplateRegistry.RegisterAll();
        BusinessTemplateRegistry.RegisterAll();

        var state = new SimulationState();
        state.Countries.Add(new Country { Name = "United States" });
        var city = new CityEntity { Id = state.GenerateEntityId(), Name = "Boston", CountryName = "United States" };
        state.Cities[city.Id] = city;
        var grid = new CityGenerator(seed).Generate(state, city);
        state.CityGrids[city.Id] = grid;

        foreach (var address in state.Addresses.Values.OrderBy(a => a.Id).ToList())
            LocationGenerator.ResolveAddressInterior(address, state, Rng.For(seed, Purpose.Population, 100, address.Id));
        state.IndexBusinesses();

        var world = new World(seed, state, start ?? DefaultStart) { CityId = city.Id };
        world.Roads = new RoadGraph(grid, state.Addresses.Values);
        world.Router = new Router(world.Roads);

        new PopulationGenerator(world).Populate();
        state.IndexAccessPoints();
        world.Phones.Index();

        foreach (var vehicle in state.Vehicles.Values.OrderBy(v => v.Id))
            world.Vehicles[vehicle.Id] = new VehicleRuntime(vehicle);
        foreach (var person in state.People.Values.OrderBy(p => p.Id))
            world.Agents[person.Id] = new Agent(person);

        PlaceEveryoneAtHome(world);
        return world;
    }

    /// <summary>Everyone starts the world asleep or relaxing at home, their car outside.</summary>
    private static void PlaceEveryoneAtHome(World world)
    {
        foreach (var vehicle in world.Vehicles.Values)
        {
            var owner = world.State.People[vehicle.Vehicle.OwnerPersonId];
            world.Movement.Park(vehicle, owner.HomeAddressId);
        }
        foreach (var agent in world.Agents.Values.OrderBy(a => a.Id))
        {
            var person = agent.Person;
            var (sleepStart, wake) = Planner.NextSleep(person, world.Now);
            bool asleep = sleepStart <= world.Now && wake > world.Now;
            var (loc, sub) = world.Places.HomeRoom(person, asleep ? Spot.HomeBedroom : Spot.HomeLiving);
            agent.AddressId = person.HomeAddressId;
            world.Movement.Settle(agent, loc, sub, asleep ? Stakeout.Simulation.History.ActivityKind.Sleep
                : Stakeout.Simulation.History.ActivityKind.Home, asleep ? "asleep" : "at home");
        }
    }
}
