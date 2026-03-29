using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Objectives;

public class EatOutObjective : Objective
{
    private static readonly TimeSpan MealDuration = TimeSpan.FromMinutes(30);

    public override int Priority => 40;
    public override ObjectiveSource Source => ObjectiveSource.Trait;

    public override List<PlannedAction> GetActionsForToday(Person person, SimulationState state, DateTime currentDate)
    {
        var dinerId = FindRestaurant(person, state);
        if (dinerId == null) return new List<PlannedAction>();

        return new List<PlannedAction>
        {
            new()
            {
                Action = new WaitAction(MealDuration, "eating at the counter"),
                TargetAddressId = dinerId.Value,
                TimeWindowStart = TimeSpan.FromHours(11),
                TimeWindowEnd = TimeSpan.FromHours(14),
                Duration = MealDuration,
                DisplayText = "eating at the counter",
                SourceObjective = this
            }
        };
    }

    public override void EmitTraces(PlannedAction action, Person person, SimulationState state)
    {
        if (!person.CurrentAddressId.HasValue) return;
        var locations = state.GetLocationsForAddress(person.CurrentAddressId.Value);
        if (locations.Count > 0)
        {
            // Sighting at diner
            Traces.TraceEmitter.EmitSighting(state, person.Id,
                locations[0].Id, $"{person.FullName} was seen eating", decayDays: 3);

            // Receipt in trash fixture (if one exists)
            var fixtures = state.GetFixturesForLocation(locations[0].Id);
            var trash = fixtures.FirstOrDefault(f => f.Type == Fixtures.FixtureType.TrashCan);
            if (trash != null)
            {
                Traces.TraceEmitter.EmitItem(state, person.Id,
                    locations[0].Id, trash.Id, "crumpled receipt", decayDays: 7);
            }
        }
    }

    private static int? FindRestaurant(Person person, SimulationState state)
    {
        if (!person.CurrentCityId.HasValue) return null;
        var city = state.Cities[person.CurrentCityId.Value];
        return city.AddressIds
            .Select(id => state.Addresses[id])
            .FirstOrDefault(a => a.Type == AddressType.Diner)?.Id;
    }
}
