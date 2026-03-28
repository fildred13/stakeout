using System;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Sublocations;
using Xunit;

namespace Stakeout.Tests.Simulation.Entities;

public class ConnectionPropertiesTests
{
    [Fact]
    public void LockableProperty_Defaults()
    {
        var prop = new LockableProperty();
        Assert.False(prop.IsLocked);
        Assert.Null(prop.KeyItemId);
    }

    [Fact]
    public void ConcealableProperty_Defaults()
    {
        var prop = new ConcealableProperty();
        Assert.False(prop.IsDiscovered);
        Assert.Equal(0f, prop.DiscoveryDifficulty);
    }

    [Fact]
    public void TransparentProperty_Defaults()
    {
        var prop = new TransparentProperty();
        Assert.False(prop.CanSeeThrough);
        Assert.False(prop.CanShootThrough);
        Assert.False(prop.CanHearThrough);
    }

    [Fact]
    public void BreakableProperty_Defaults()
    {
        var prop = new BreakableProperty();
        Assert.Equal(1.0f, prop.Durability);
        Assert.False(prop.IsBroken);
    }

    [Fact]
    public void SublocationConnection_NewDefaults()
    {
        var conn = new SublocationConnection();
        Assert.Equal(0, conn.Id);
        Assert.Equal(ConnectionType.OpenPassage, conn.Type);
        Assert.Null(conn.Name);
        Assert.Empty(conn.Tags);
        Assert.Null(conn.Lockable);
        Assert.Null(conn.Concealable);
        Assert.Null(conn.Transparent);
        Assert.Null(conn.Breakable);
    }

    [Fact]
    public void SublocationConnection_HasTag_TagPresent_ReturnsTrue()
    {
        var conn = new SublocationConnection { Tags = new[] { "exterior", "main" } };
        Assert.True(conn.HasTag("exterior"));
        Assert.True(conn.HasTag("main"));
    }

    [Fact]
    public void SublocationConnection_HasTag_TagAbsent_ReturnsFalse()
    {
        var conn = new SublocationConnection { Tags = new[] { "exterior" } };
        Assert.False(conn.HasTag("locked"));
    }

    [Fact]
    public void FingerprintSurface_DefaultsToEmptyLists()
    {
        var surface = new FingerprintSurface();
        Assert.Empty(surface.SideATraceIds);
        Assert.Empty(surface.SideBTraceIds);
    }

    [Fact]
    public void SublocationConnection_FingerprintSurface_NullByDefault()
    {
        var conn = new SublocationConnection();
        Assert.Null(conn.Fingerprints);
    }

    [Fact]
    public void Item_FingerprintSurface_NullByDefault()
    {
        var item = new Item();
        Assert.Null(item.Fingerprints);
    }

    [Fact]
    public void GeneratedConnections_AllHaveFingerprintSurface()
    {
        var state = new SimulationState(new GameClock(new DateTime(1980, 1, 1)));
        var address = new Address
        {
            Id = state.GenerateEntityId(),
            Type = AddressType.SuburbanHome,
            GridX = 2,
            GridY = 2,
            Number = 1,
            StreetId = 1
        };
        state.Addresses[address.Id] = address;
        var generator = new SuburbanHomeGenerator();
        generator.Generate(address, state, new Random(42));

        foreach (var conn in address.Connections)
        {
            Assert.NotNull(conn.Fingerprints);
        }
    }
}
