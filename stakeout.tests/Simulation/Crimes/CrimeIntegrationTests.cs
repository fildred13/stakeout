// stakeout.tests/Simulation/Crimes/CrimeIntegrationTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Crimes;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Scheduling;
using Stakeout.Simulation.Traces;
using Xunit;

namespace Stakeout.Tests.Simulation.Crimes;

public class CrimeIntegrationTests
{
    [Fact]
    public void FullCrimePipeline_SerialKiller_VictimDiesAndTracesProduced()
    {
        // Setup: state with 2 people
        var state = new SimulationState(new GameClock(new DateTime(1980, 1, 1, 0, 0, 0)));
        var street = new Street { Id = state.GenerateEntityId(), Name = "Oak St", CityId = 1 };
        state.Streets[street.Id] = street;

        var homeA = new Address { Id = state.GenerateEntityId(), Position = new Vector2(100, 100),
            Type = AddressType.SuburbanHome, Number = 10, StreetId = street.Id };
        var homeB = new Address { Id = state.GenerateEntityId(), Position = new Vector2(500, 100),
            Type = AddressType.SuburbanHome, Number = 20, StreetId = street.Id };
        var work = new Address { Id = state.GenerateEntityId(), Position = new Vector2(300, 300),
            Type = AddressType.Office, Number = 1, StreetId = street.Id };
        state.Addresses[homeA.Id] = homeA;
        state.Addresses[homeB.Id] = homeB;
        state.Addresses[work.Id] = work;

        var personA = new Person
        {
            Id = state.GenerateEntityId(), FirstName = "Alice", LastName = "A",
            IsAlive = true, HomeAddressId = homeA.Id, JobId = 0,
            CurrentAddressId = homeA.Id, CurrentPosition = homeA.Position,
            CurrentAction = ActionType.Sleep
        };
        var personB = new Person
        {
            Id = state.GenerateEntityId(), FirstName = "Bob", LastName = "B",
            IsAlive = true, HomeAddressId = homeB.Id, JobId = 0,
            CurrentAddressId = homeB.Id, CurrentPosition = homeB.Position,
            CurrentAction = ActionType.Sleep
        };
        state.People[personA.Id] = personA;
        state.People[personB.Id] = personB;

        // Step 1: Generate crime
        var generator = new CrimeGenerator();
        var crime = generator.Generate(CrimeTemplateType.SerialKiller, state);
        Assert.NotNull(crime);
        Assert.Equal(CrimeStatus.InProgress, crime.Status);

        // Step 2: Identify killer and resolve objectives (which picks victim)
        var killerId = crime.Roles["Killer"].Value;
        var killer = state.People[killerId];
        var tasks = ObjectiveResolver.ResolveTasks(killer.Objectives, state);

        // Victim should now be assigned
        Assert.NotNull(crime.Roles["Victim"]);
        var victimId = crime.Roles["Victim"].Value;
        var victim = state.People[victimId];
        Assert.NotEqual(killerId, victimId);

        // Step 3: Should have a KillPerson task
        var killTask = tasks.FirstOrDefault(t => t.ActionType == ActionType.KillPerson);
        Assert.NotNull(killTask);
        Assert.Equal(victim.HomeAddressId, killTask.TargetAddressId);

        // Step 4: Simulate the kill action directly
        var murderObjective = killer.Objectives.First(o => o.Type == ObjectiveType.CommitMurder);
        ActionExecutor.Execute(killTask, killer, murderObjective, state);

        // Step 5: Verify results
        Assert.False(victim.IsAlive);
        Assert.True(state.Traces.Count >= 2); // condition + mark
        Assert.Contains(state.Traces.Values, t => t.TraceType == TraceType.Condition);
        Assert.Contains(state.Traces.Values, t => t.TraceType == TraceType.Mark);
    }
}
