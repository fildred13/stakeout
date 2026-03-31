using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Businesses;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Entities;
using Xunit;
using CityEntity = Stakeout.Simulation.Entities.City;

namespace Stakeout.Tests.Simulation.Businesses;

public class BusinessResolverTests
{
    private static (SimulationState state, PersonGenerator gen) Setup()
    {
        AddressTemplateRegistry.RegisterAll();
        BusinessTemplateRegistry.RegisterAll();
        var state = new SimulationState();
        var city = new CityEntity { Id = state.GenerateEntityId(), Name = "Boston", CountryName = "US" };
        state.Cities[city.Id] = city;
        var cityGen = new CityGenerator(seed: 42);
        state.CityGrids[city.Id] = cityGen.Generate(state, city);
        var gen = new PersonGenerator(new MapConfig());
        return (state, gen);
    }

    [Fact]
    public void Resolve_FillsAllPositions()
    {
        var (state, gen) = Setup();
        var biz = state.Businesses.Values.First();
        var emptyCount = biz.Positions.Count(p => p.AssignedPersonId == null);

        var spawned = BusinessResolver.Resolve(state, biz, gen);

        Assert.Equal(emptyCount, spawned.Count);
        Assert.All(biz.Positions, p => Assert.NotNull(p.AssignedPersonId));
    }

    [Fact]
    public void Resolve_SetsIsResolved()
    {
        var (state, gen) = Setup();
        var biz = state.Businesses.Values.First();

        BusinessResolver.Resolve(state, biz, gen);

        Assert.True(biz.IsResolved);
    }

    [Fact]
    public void Resolve_SkipsAlreadyAssignedPositions()
    {
        var (state, gen) = Setup();
        var biz = state.Businesses.Values.First();

        var person = gen.GeneratePerson(state, new SpawnRequirements
        {
            BusinessId = biz.Id,
            PositionId = biz.Positions[0].Id
        });

        var remainingEmpty = biz.Positions.Count(p => p.AssignedPersonId == null);
        var spawned = BusinessResolver.Resolve(state, biz, gen);

        Assert.Equal(remainingEmpty, spawned.Count);
    }

    [Fact]
    public void Resolve_AlreadyResolved_ReturnsEmpty()
    {
        var (state, gen) = Setup();
        var biz = state.Businesses.Values.First();

        BusinessResolver.Resolve(state, biz, gen);
        var second = BusinessResolver.Resolve(state, biz, gen);

        Assert.Empty(second);
    }

    [Fact]
    public void Resolve_SpawnedPeople_HaveWorkShiftObjective()
    {
        var (state, gen) = Setup();
        var biz = state.Businesses.Values.First();

        var spawned = BusinessResolver.Resolve(state, biz, gen);

        Assert.All(spawned, p =>
            Assert.Contains(p.Objectives, o => o is Stakeout.Simulation.Objectives.WorkShiftObjective));
    }
}
