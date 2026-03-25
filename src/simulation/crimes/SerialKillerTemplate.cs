using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Objectives;

namespace Stakeout.Simulation.Crimes;

public class SerialKillerTemplate : ICrimeTemplate
{
    public CrimeTemplateType Type => CrimeTemplateType.SerialKiller;
    public string Name => "Serial Killer";

    public Crime Instantiate(SimulationState state)
    {
        var alivePeople = state.People.Values.Where(p => p.IsAlive).ToList();
        if (alivePeople.Count < 2) return null;

        var rng = new Random();
        var killer = alivePeople[rng.Next(alivePeople.Count)];

        var crime = new Crime
        {
            Id = state.GenerateEntityId(),
            TemplateType = CrimeTemplateType.SerialKiller,
            CreatedAt = DateTime.Now,
            Roles = new Dictionary<string, int?>
            {
                { "Killer", killer.Id },
                { "Victim", null }
            }
        };

        var objective = new Objective
        {
            Id = state.GenerateEntityId(),
            Type = ObjectiveType.CommitMurder,
            Source = ObjectiveSource.CrimeTemplate,
            SourceEntityId = crime.Id,
            Priority = 40,
            IsRecurring = false
        };

        // Step 0: Choose victim (instant)
        objective.Steps.Add(new ObjectiveStep
        {
            Description = "Choose victim",
            IsInstant = true,
            ResolveFunc = (obj, st) =>
            {
                var alive = st.People.Values.Where(p => p.IsAlive && p.Id != killer.Id).ToList();
                if (alive.Count == 0) return null;
                var victim = alive[new Random().Next(alive.Count)];
                obj.Data["VictimId"] = victim.Id;
                crime.Roles["Victim"] = victim.Id;
                return null;
            }
        });

        // Step 1: Kill victim
        objective.Steps.Add(new ObjectiveStep
        {
            Description = "Kill victim",
            ActionType = ActionType.KillPerson,
            ResolveFunc = (obj, st) =>
            {
                var victimId = (int)obj.Data["VictimId"];
                var victim = st.People[victimId];
                return new SimTask
                {
                    Id = st.GenerateEntityId(),
                    ObjectiveId = obj.Id,
                    StepIndex = 1,
                    ActionType = ActionType.KillPerson,
                    Priority = 40,
                    WindowStart = new TimeSpan(1, 0, 0),
                    WindowEnd = new TimeSpan(1, 30, 0),
                    TargetAddressId = victim.HomeAddressId,
                    ActionData = new Dictionary<string, object>
                    {
                        { "VictimId", victimId }
                    }
                };
            }
        });

        // Step 2: Go home
        objective.Steps.Add(new ObjectiveStep
        {
            Description = "Go home",
            ActionType = ActionType.Idle,
            ResolveFunc = (obj, st) =>
            {
                return new SimTask
                {
                    Id = st.GenerateEntityId(),
                    ObjectiveId = obj.Id,
                    StepIndex = 2,
                    ActionType = ActionType.Idle,
                    Priority = 40,
                    WindowStart = new TimeSpan(1, 30, 0),
                    WindowEnd = new TimeSpan(3, 0, 0),
                    TargetAddressId = killer.HomeAddressId
                };
            }
        });

        killer.Objectives.Add(objective);
        crime.ObjectiveIds.Add(objective.Id);
        state.Crimes[crime.Id] = crime;

        return crime;
    }
}
