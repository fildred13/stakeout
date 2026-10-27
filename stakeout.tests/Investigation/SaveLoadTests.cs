using System;
using System.Linq;
using Stakeout.Investigation;
using Xunit;

namespace Stakeout.Tests.Investigation;

public class SaveLoadTests
{
    [Fact]
    public void ASaveIsTheSeedPlusTheCommandsAndLoadsToTheSameState()
    {
        var session = GameSession.NewGame(5);
        var d = session.Detective;
        while (d.Case == null) session.AdvanceTo(session.World.Now.AddMinutes(20));

        var trip = (Stakeout.Simulation.History.TripFact)session.Do("travel", d.Case.SceneAddressId);
        session.AdvanceTo(trip.Arrive.AddMinutes(3));
        session.Do("enter", d.Case.SceneLocationId);
        session.Do("look");
        session.Do("body");
        foreach (var f in d.FixturesHere().ToList()) session.Do("search", f.Id);
        foreach (var s in d.SurfacesHere().Take(4).ToList()) session.Do("dust", s.Id);
        session.Do("leave");
        session.AdvanceTo(session.World.Now.AddMinutes(37));

        var json = session.Save();
        var loaded = GameSession.Load(json);

        Assert.Equal(session.World.Now, loaded.World.Now);
        Assert.Equal(d.Clues.Select(c => c.Text), loaded.Detective.Clues.Select(c => c.Text));
        Assert.Equal(session.World.History.Trips.Count, loaded.World.History.Trips.Count);
        Assert.Equal(session.World.History.Trips[^1].Depart, loaded.World.History.Trips[^1].Depart);
        Assert.Equal(d.Agent.LocationId, loaded.Detective.Agent.LocationId);
    }
}
