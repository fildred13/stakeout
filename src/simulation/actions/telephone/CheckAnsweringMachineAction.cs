using System;
using System.Linq;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Traces;

namespace Stakeout.Simulation.Actions.Telephone;

public class CheckAnsweringMachineAction : IAction
{
    private TimeSpan _elapsed = TimeSpan.Zero;
    private static readonly TimeSpan CheckDuration = TimeSpan.FromMinutes(2);

    public string Name => "CheckAnsweringMachine";
    public string DisplayText => "Checking answering machine";

    public void OnStart(ActionContext ctx) { }

    public ActionStatus Tick(ActionContext ctx, TimeSpan delta)
    {
        _elapsed += delta;
        return _elapsed >= CheckDuration ? ActionStatus.Completed : ActionStatus.Running;
    }

    public void OnComplete(ActionContext ctx)
    {
        if (!ctx.Person.HomePhoneFixtureId.HasValue) return;

        var messageTraces = ctx.State.GetTracesForFixture(
            ctx.Person.HomePhoneFixtureId.Value, ctx.CurrentTime)
            .Where(t => t.Type == TraceType.Record
                     && t.Description != null
                     && t.Description.Contains("please call back")
                     && t.CreatedByPersonId.HasValue)
            .ToList();

        if (!ctx.State.RelationshipsByPersonId.TryGetValue(ctx.Person.Id, out var relationships))
            return;
        var knownContactIds = relationships
            .Select(r => r.PersonAId == ctx.Person.Id ? r.PersonBId : r.PersonAId)
            .ToHashSet();

        foreach (var trace in messageTraces)
        {
            var callerId = trace.CreatedByPersonId!.Value;
            if (!knownContactIds.Contains(callerId)) continue;

            // "Unread" = no existing CallBackObjective for this caller
            if (ctx.Person.Objectives.OfType<CallBackObjective>().Any(o => o.TargetPersonId == callerId))
                continue;

            var caller = ctx.State.People[callerId];
            if (!caller.HomePhoneFixtureId.HasValue) continue;

            ctx.Person.Objectives.Add(new CallBackObjective(callerId,
                caller.HomePhoneFixtureId.Value, caller.HomeAddressId)
            {
                Id = ctx.State.GenerateEntityId()
            });
        }
    }
}
