using System;
using System.Collections.Generic;
using System.Linq;

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

        // TODO: Project 3 — crime objectives will be created via the new Objective subclass system
        state.Crimes[crime.Id] = crime;

        return crime;
    }
}
