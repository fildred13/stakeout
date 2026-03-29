using System;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;

namespace Stakeout.Simulation.Actions;

// Note: TraceEmitter is a static class. Actions call TraceEmitter.EmitSighting(ctx.State, ...) directly.
public class ActionContext
{
    public Person Person { get; init; }
    public SimulationState State { get; init; }
    public EventJournal EventJournal { get; init; }
    public Random Random { get; init; }
    public DateTime CurrentTime { get; init; }
}
