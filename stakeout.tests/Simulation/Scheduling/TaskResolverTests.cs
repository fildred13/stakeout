// stakeout.tests/Simulation/Scheduling/TaskResolverTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Scheduling;
using Stakeout.Simulation.Sublocations;
using Xunit;

namespace Stakeout.Tests.Simulation.Scheduling;

public class TaskResolverTests
{
    public TaskResolverTests()
    {
        SublocationGeneratorRegistry.RegisterAll();
    }

    private SimulationState CreateStateWithOffice()
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, Type = AddressType.Office };
        state.Addresses[1] = address;
        var gen = new OfficeGenerator();
        gen.Generate(address, state, new Random(42));
        return state;
    }

    [Fact]
    public void Resolve_WorkTask_ProducesEntriesWithSublocationIds()
    {
        var state = CreateStateWithOffice();
        var task = new SimTask
        {
            Id = 1, ActionType = ActionType.Work, Priority = 20,
            TargetAddressId = 1,
            WindowStart = new TimeSpan(9, 0, 0),
            WindowEnd = new TimeSpan(17, 0, 0)
        };

        var entries = TaskResolver.Resolve(task, state, new Random(42));

        Assert.NotEmpty(entries);
        Assert.All(entries, e => Assert.NotNull(e.TargetSublocationId));
        Assert.All(entries, e => Assert.Equal(1, e.TargetAddressId));
    }

    [Fact]
    public void Resolve_TaskWithNoSublocations_ReturnsEntryWithNullSublocation()
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, Type = AddressType.Office };
        state.Addresses[1] = address;
        // No sublocations generated

        var task = new SimTask
        {
            Id = 1, ActionType = ActionType.Work, Priority = 20,
            TargetAddressId = 1,
            WindowStart = new TimeSpan(9, 0, 0),
            WindowEnd = new TimeSpan(17, 0, 0)
        };

        var entries = TaskResolver.Resolve(task, state, new Random(42));

        Assert.NotEmpty(entries);
        Assert.All(entries, e => Assert.Null(e.TargetSublocationId));
    }

    [Fact]
    public void Resolve_SleepTask_UsesInhabitDecomposition()
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, Type = AddressType.SuburbanHome };
        state.Addresses[1] = address;
        var gen = new SuburbanHomeGenerator();
        gen.Generate(address, state, new Random(42));

        var task = new SimTask
        {
            Id = 1, ActionType = ActionType.Sleep, Priority = 30,
            TargetAddressId = 1,
            WindowStart = new TimeSpan(22, 0, 0),
            WindowEnd = new TimeSpan(6, 0, 0)
        };

        var entries = TaskResolver.Resolve(task, state, new Random(42));

        Assert.NotEmpty(entries);
        // Should contain bedroom entries for sleep
        var bedroomSubs = address.Sublocations.Values
            .Where(s => s.HasTag("bedroom"))
            .Select(s => s.Id)
            .ToHashSet();
        Assert.Contains(entries, e => bedroomSubs.Contains(e.TargetSublocationId ?? 0));
    }
}
