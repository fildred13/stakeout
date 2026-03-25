using System;
using System.Collections.Generic;

namespace Stakeout.Simulation.Crimes;

public enum CrimeTemplateType { SerialKiller }
public enum CrimeStatus { InProgress, Completed, Failed }

public class Crime
{
    public int Id { get; set; }
    public CrimeTemplateType TemplateType { get; set; }
    public DateTime CreatedAt { get; set; }
    public CrimeStatus Status { get; set; } = CrimeStatus.InProgress;
    public Dictionary<string, int?> Roles { get; set; } = new();
    public List<int> RelatedTraceIds { get; set; } = new();
    public List<int> ObjectiveIds { get; set; } = new();
}
