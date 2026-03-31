using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Objectives;

public class GoForARunObjective : Objective
{
    private static readonly TimeSpan RunDuration = TimeSpan.FromMinutes(45);

    public override int Priority => 20;
    public override ObjectiveSource Source => ObjectiveSource.Trait;

    public override List<PlannedAction> GetActions(Person person, SimulationState state, DateTime planStart, DateTime planEnd)
    {
        var parkId = FindPark(person, state);
        if (parkId == null) return new List<PlannedAction>();

        var windowStart = planStart + TimeSpan.FromHours(1);
        var windowEnd = planStart + TimeSpan.FromHours(6);
        if (windowEnd > planEnd) windowEnd = planEnd;
        if (windowStart >= windowEnd) return new List<PlannedAction>();

        return new List<PlannedAction>
        {
            new()
            {
                Action = new WaitAction(RunDuration, "running on the trails"),
                TargetAddressId = parkId.Value,
                TimeWindowStart = windowStart,
                TimeWindowEnd = windowEnd,
                Duration = RunDuration,
                DisplayText = "running on the trails",
                SourceObjective = this
            }
        };
    }

    public override void EmitTraces(PlannedAction action, Person person, SimulationState state)
    {
        if (person.CurrentAddressId.HasValue)
        {
            var locations = state.GetLocationsForAddress(person.CurrentAddressId.Value);
            if (locations.Count > 0)
            {
                Traces.TraceEmitter.EmitSighting(state, person.Id,
                    locations[0].Id, $"{person.FullName} was seen running", decayDays: 3);
            }
        }
    }

    private static int? FindPark(Person person, SimulationState state)
    {
        if (!person.CurrentCityId.HasValue) return null;
        var city = state.Cities[person.CurrentCityId.Value];
        return city.AddressIds
            .Select(id => state.Addresses[id])
            .FirstOrDefault(a => a.Type == AddressType.Park)?.Id;
    }
}
