using System.Collections.Generic;

namespace Stakeout.Simulation.Crimes;

public class CrimeGenerator
{
    private readonly Dictionary<CrimeTemplateType, ICrimeTemplate> _templates = new()
    {
        { CrimeTemplateType.SerialKiller, new SerialKillerTemplate() }
    };

    public Crime Generate(CrimeTemplateType templateType, SimulationState state)
    {
        if (!_templates.TryGetValue(templateType, out var template)) return null;
        return template.Instantiate(state);
    }

    public IEnumerable<ICrimeTemplate> GetAvailableTemplates() => _templates.Values;
}
