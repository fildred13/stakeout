using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Actions.Telephone;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Actions;

public class AcceptPhoneCallActionTests
{
    [Fact]
    public void AcceptPhoneCallAction_CallsOnAccepted_OnCallerOrganizeDateObjective()
    {
        var state = new SimulationState(new GameClock(new DateTime(1984, 1, 2, 14, 0, 0)));

        var callerHome = new Address { Id = state.GenerateEntityId(), GridX = 2, GridY = 2 };
        var recipientHome = new Address { Id = state.GenerateEntityId(), GridX = 8, GridY = 8 };
        var diner = new Address { Id = state.GenerateEntityId(), GridX = 5, GridY = 5 };
        state.Addresses[callerHome.Id] = callerHome;
        state.Addresses[recipientHome.Id] = recipientHome;
        state.Addresses[diner.Id] = diner;

        var caller = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = callerHome.Id,
            CurrentAddressId = callerHome.Id,
            PreferredSleepTime = TimeSpan.FromHours(23),
            PreferredWakeTime = TimeSpan.FromHours(7)
        };
        caller.Objectives.Add(new SleepObjective { Id = state.GenerateEntityId() });
        var recipient = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = recipientHome.Id,
            CurrentAddressId = recipientHome.Id,
            PreferredSleepTime = TimeSpan.FromHours(23),
            PreferredWakeTime = TimeSpan.FromHours(7)
        };
        recipient.Objectives.Add(new SleepObjective { Id = state.GenerateEntityId() });
        state.People[caller.Id] = caller;
        state.People[recipient.Id] = recipient;

        var group = new Group
        {
            Id = state.GenerateEntityId(),
            Type = GroupType.Date,
            Status = GroupStatus.Forming,
            DriverPersonId = caller.Id,
            PickupAddressId = recipientHome.Id,
            PickupTime = new DateTime(1984, 1, 2, 17, 50, 0),
            MeetupAddressId = diner.Id,
            MeetupTime = new DateTime(1984, 1, 2, 19, 0, 0),
            MemberPersonIds = new List<int> { caller.Id, recipient.Id }
        };
        state.Groups[group.Id] = group;

        var organizeObj = new OrganizeDateObjective(
            recipient.Id, diner.Id,
            new DateTime(1984, 1, 2, 12, 0, 0),
            new DateTime(1984, 1, 2, 19, 0, 0),
            new DateTime(1984, 1, 2, 17, 50, 0))
        {
            Id = state.GenerateEntityId()
        };
        organizeObj.SetGroupId(group.Id);
        caller.Objectives.Add(organizeObj);

        var inv = new PendingInvitation
        {
            Id = state.GenerateEntityId(),
            FromPersonId = caller.Id,
            ToPersonId = recipient.Id,
            Type = InvitationType.DateInvitation,
            ProposedGroupId = group.Id,
            CreatedAt = state.Clock.CurrentTime
        };
        state.AddPendingInvitation(inv);

        var action = new AcceptPhoneCallAction(inv);
        var ctx = new ActionContext
        {
            Person = recipient,
            State = state,
            EventJournal = state.Journal,
            Random = new Random(1),
            CurrentTime = state.Clock.CurrentTime
        };

        action.OnStart(ctx);
        for (int i = 0; i < 3; i++)
            action.Tick(ctx, TimeSpan.FromMinutes(1));
        action.OnComplete(ctx);

        // Invitation is still in the pending list (consumption happens in ActionRunner, not in AcceptPhoneCallAction)
        // The action's responsibility is just to call OnAccepted on the OrganizeDateObjective

        // Group should be active
        Assert.Equal(GroupStatus.Active, group.Status);

        // Both people should have GoOnDateObjective
        Assert.Contains(caller.Objectives, o => o is GoOnDateObjective);
        Assert.Contains(recipient.Objectives, o => o is GoOnDateObjective);

        // Both should have NeedsReplan
        Assert.True(caller.NeedsReplan);
        Assert.True(recipient.NeedsReplan);
    }
}
