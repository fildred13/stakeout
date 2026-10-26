using System;
using System.Collections.Generic;

namespace Stakeout.Simulation.Crimes;

public class CrimeGenerator
{
    private readonly Random _random;
    private readonly Dictionary<CrimeTemplateType, ICrimeTemplate> _templates = new()
    {
        { CrimeTemplateType.SerialKiller, new SerialKillerTemplate() }
    };

    public CrimeGenerator(Random random)
    {
        _random = random;
    }

    public Crime Generate(CrimeTemplateType templateType, SimulationState state)
    {
        if (!_templates.TryGetValue(templateType, out var template)) return null;
        return template.Instantiate(state, _random);
    }

    public IEnumerable<ICrimeTemplate> GetAvailableTemplates() => _templates.Values;
}
