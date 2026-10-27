using System.Linq;
using Stakeout.Investigation;
using Stakeout.Simulation.Crime;
using Stakeout.Simulation.Diagnostics;
using Xunit;

namespace Stakeout.Tests.Investigation;

public class InvestigationTests
{
    [Theory]
    [InlineData(5)]
    [InlineData(13)]
    public void AScriptedDetectiveCanSolveTheMurderFromEvidenceAlone(int seed)
    {
        var session = GameSession.NewGame(seed);
        var bot = new AutoDetective(session.Detective);
        var verdict = bot.Run();

        Assert.NotNull(verdict);
        Assert.True(verdict.RightPerson, string.Join("\n", bot.Steps));
        Assert.True(verdict.Assessment.Probability >= 0.5, string.Join("\n", verdict.Assessment.Findings.Select(f => f.Text)));
        var violations = new InvariantChecker(session.World).CheckAll();
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    [Fact]
    public void TheMurderIsReportedOnTheFirstMorningWithATruthfulRecord()
    {
        var session = GameSession.NewGame(21);
        var world = session.World;
        while (session.Detective.Case == null && world.Now < world.Now.Date.AddDays(2))
            world.AdvanceTo(world.Now.AddMinutes(30));
        var murder = session.Detective.Case.Truth;

        Assert.Equal(CaseStatus.Reported, murder.Status);
        var death = world.History.Deaths.Single(d => d.VictimId == murder.VictimId);
        Assert.Equal(murder.KillerId, death.KillerId);
        // The killer was really there: in the victim's home at the moment of death.
        var killerThere = world.History.PresenceOfAt(murder.KillerId, death.Time);
        Assert.Equal(murder.SceneLocationId, killerThere.LocationId);
        // And the body lay where the neighbour found it.
        var found = world.History.PresenceOfAt(murder.DiscovererId.Value, murder.DiscoveredAt.Value.AddMinutes(1));
        Assert.Equal(murder.SceneLocationId, found.LocationId);
    }
}
