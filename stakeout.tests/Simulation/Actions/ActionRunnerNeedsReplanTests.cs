using System;
using System.Collections.Generic;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Brain;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Actions;

public class ActionRunnerNeedsReplanTests
{
    [Fact]
    public void NeedsReplan_True_CausesNewDayPlan_OnNextTick()
    {
        var state = new SimulationState(new GameClock(new DateTime(1984, 1, 2, 12, 0, 0)));
        var mapConfig = new MapConfig();

        var homeAddr = new Address { Id = state.GenerateEntityId(), Type = AddressType.SuburbanHome, GridX = 5, GridY = 5 };
        state.Addresses[homeAddr.Id] = homeAddr;

        var person = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = homeAddr.Id,
            CurrentAddressId = homeAddr.Id,
            CurrentPosition = homeAddr.Position,
            PreferredSleepTime = TimeSpan.FromHours(23),
            PreferredWakeTime = TimeSpan.FromHours(7)
        };
        person.Objectives.Add(new SleepObjective { Id = state.GenerateEntityId() });
        state.People[person.Id] = person;

        person.DayPlan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime, mapConfig);
        var originalPlan = person.DayPlan;

        person.NeedsReplan = true;

        var runner = new ActionRunner(mapConfig);
        state.Clock.Tick(60);
        runner.Tick(person, state, TimeSpan.FromMinutes(1));

        Assert.False(person.NeedsReplan);
        Assert.NotSame(originalPlan, person.DayPlan);
    }

    [Fact]
    public void PendingInvitation_AtActionBoundary_InjectsAcceptPhoneCallAction()
    {
        var state = new SimulationState(new GameClock(new DateTime(1984, 1, 2, 12, 0, 0)));
        var mapConfig = new MapConfig();

        var homeAddr = new Address { Id = state.GenerateEntityId(), Type = AddressType.SuburbanHome, GridX = 5, GridY = 5 };
        state.Addresses[homeAddr.Id] = homeAddr;

        var person = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = homeAddr.Id,
            CurrentAddressId = homeAddr.Id,
            CurrentPosition = homeAddr.Position,
            PreferredSleepTime = TimeSpan.FromHours(23),
            PreferredWakeTime = TimeSpan.FromHours(7)
        };
        person.Objectives.Add(new SleepObjective { Id = state.GenerateEntityId() });
        state.People[person.Id] = person;

        // Create a forming group so AcceptPhoneCallAction can find it
        var group = new Stakeout.Simulation.Entities.Group
        {
            Id = state.GenerateEntityId(),
            Type = Stakeout.Simulation.Entities.GroupType.Date,
            Status = Stakeout.Simulation.Entities.GroupStatus.Forming,
            MemberPersonIds = new System.Collections.Generic.List<int> { 999, person.Id }
        };
        state.Groups[group.Id] = group;

        var inv = new Stakeout.Simulation.Entities.PendingInvitation
        {
            Id = state.GenerateEntityId(),
            FromPersonId = 999,
            ToPersonId = person.Id,
            Type = Stakeout.Simulation.Entities.InvitationType.DateInvitation,
            ProposedGroupId = group.Id,
            CreatedAt = state.Clock.CurrentTime
        };
        state.AddPendingInvitation(inv);

        person.DayPlan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime, mapConfig);

        var runner = new ActionRunner(mapConfig);
        // Tick enough for the first plan entry to start and complete, then invitation should be injected
        for (int i = 0; i < 120; i++)
        {
            state.Clock.Tick(60);
            runner.Tick(person, state, TimeSpan.FromMinutes(1));
            if (person.CurrentActivity?.Name == "AcceptPhoneCall")
                break;
        }

        Assert.Equal("AcceptPhoneCall", person.CurrentActivity?.Name);
        var remaining = state.PendingInvitationsByPersonId.TryGetValue(person.Id, out var inv2) ? inv2 : new List<PendingInvitation>();
        Assert.Empty(remaining);
    }
}
