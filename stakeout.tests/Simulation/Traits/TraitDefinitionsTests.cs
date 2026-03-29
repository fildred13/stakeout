using System.Linq;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Traits;
using Xunit;

namespace Stakeout.Tests.Simulation.Traits;

public class TraitDefinitionsTests
{
    [Fact]
    public void Runner_CreatesGoForARunObjective()
    {
        var objectives = TraitDefinitions.CreateObjectivesForTrait("runner");
        Assert.Single(objectives);
        Assert.IsType<GoForARunObjective>(objectives[0]);
    }

    [Fact]
    public void Foodie_CreatesEatOutObjective()
    {
        var objectives = TraitDefinitions.CreateObjectivesForTrait("foodie");
        Assert.Single(objectives);
        Assert.IsType<EatOutObjective>(objectives[0]);
    }

    [Fact]
    public void UnknownTrait_ReturnsEmpty()
    {
        var objectives = TraitDefinitions.CreateObjectivesForTrait("unknown");
        Assert.Empty(objectives);
    }

    [Fact]
    public void GetAllTraitNames_ContainsRunnerAndFoodie()
    {
        var names = TraitDefinitions.GetAllTraitNames();
        Assert.Contains("runner", names);
        Assert.Contains("foodie", names);
    }
}
