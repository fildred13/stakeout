using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation;

public class SimulationStateTests
{
    [Fact]
    public void Constructor_InitializesClockAndEmptyCollections()
    {
        var state = new SimulationState();

        Assert.NotNull(state.Clock);
        Assert.Empty(state.People);
        Assert.Empty(state.Countries);
        Assert.Empty(state.Cities);
        Assert.Empty(state.Streets);
        Assert.Empty(state.Addresses);
        Assert.Null(state.Player);
    }

    [Fact]
    public void GenerateEntityId_FirstCall_Returns1()
    {
        var state = new SimulationState();

        Assert.Equal(1, state.GenerateEntityId());
    }

    [Fact]
    public void GenerateEntityId_MultipleCalls_ReturnsIncrementing()
    {
        var state = new SimulationState();

        var id1 = state.GenerateEntityId();
        var id2 = state.GenerateEntityId();
        var id3 = state.GenerateEntityId();

        Assert.Equal(1, id1);
        Assert.Equal(2, id2);
        Assert.Equal(3, id3);
    }
}
