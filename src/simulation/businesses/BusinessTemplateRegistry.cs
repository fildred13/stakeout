using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Businesses;

public static class BusinessTemplateRegistry
{
    private static readonly Dictionary<BusinessType, IBusinessTemplate> _templates = new();

    public static void Register(BusinessType type, IBusinessTemplate template)
    {
        _templates[type] = template;
    }

    public static IBusinessTemplate Get(BusinessType type)
    {
        return _templates.TryGetValue(type, out var template) ? template : null;
    }

    public static void RegisterAll()
    {
        Register(BusinessType.Diner, new DinerBusinessTemplate());
        Register(BusinessType.DiveBar, new DiveBarBusinessTemplate());
        Register(BusinessType.Office, new OfficeBusinessTemplate());
    }
}
