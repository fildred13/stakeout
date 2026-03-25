using System;
using System.Collections.Generic;
using Stakeout.Simulation.Actions;

namespace Stakeout.Simulation.Objectives;

public class SimTask
{
    public int Id { get; set; }
    public int ObjectiveId { get; set; }
    public int StepIndex { get; set; }
    public ActionType ActionType { get; set; }
    public int Priority { get; set; }
    public TimeSpan WindowStart { get; set; }
    public TimeSpan WindowEnd { get; set; }
    public int? TargetAddressId { get; set; }
    public Dictionary<string, object> ActionData { get; set; }
}
