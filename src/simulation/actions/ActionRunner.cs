using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions.Telephone;
using Stakeout.Simulation.Brain;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;
using Stakeout.Simulation.Traces;

namespace Stakeout.Simulation.Actions;

public class ActionRunner
{
    private readonly MapConfig _mapConfig;
    private readonly Dictionary<int, Random> _personRandoms = new();
    private readonly Dictionary<int, bool> _pendingAnsweringMachineCheck = new();

    public ActionRunner(MapConfig mapConfig)
    {
        _mapConfig = mapConfig;
    }

    public void Tick(Person person, SimulationState state, TimeSpan delta)
    {
        if (person.NeedsReplan)
        {
            person.NeedsReplan = false;
            person.CurrentActivity = null;
            if (person.TravelInfo != null)
            {
                // Cancel ongoing travel — person stays at their current interpolated position
                person.TravelInfo = null;
                person.CurrentAddressId = null;
            }
            person.DayPlan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime, _mapConfig);
        }

        if (person.DayPlan == null) return;

        // If traveling, update travel
        if (person.TravelInfo != null)
        {
            UpdateTravel(person, state);
            return;
        }

        // If currently doing an activity, check for phone interrupts first
        if (person.CurrentActivity != null)
        {
            // A ringing phone interrupts the current activity (person answers immediately)
            if (HasPendingInvitation(person, state))
            {
                person.CurrentActivity = null;
                TryInjectPendingInvitation(person, state);
                return;
            }

            var ctx = CreateContext(person, state);
            var entry = person.DayPlan.Current;
            // Respect the scheduled end time to prevent cumulative drift
            var pastEndTime = entry != null && state.Clock.CurrentTime >= entry.EndTime;
            var status = pastEndTime ? ActionStatus.Completed : person.CurrentActivity.Tick(ctx, delta);
            if (status == ActionStatus.Completed || status == ActionStatus.Failed)
            {
                person.CurrentActivity.OnComplete(ctx);
                LogActivityCompleted(person, state);

                if (entry?.PlannedAction?.SourceObjective != null)
                {
                    var obj = entry.PlannedAction.SourceObjective;
                    var success = status == ActionStatus.Completed;
                    obj.OnActionCompleted(entry.PlannedAction, success);
                    obj.OnActionCompletedWithState(entry.PlannedAction, person, state, success);
                    if (success)
                        obj.EmitTraces(entry.PlannedAction, person, state);
                }

                person.CurrentActivity = null;
                person.DayPlan.AdvanceToNext();
                // Try to start next entry immediately
                StartNextEntry(person, state);
            }
            return;
        }

        // No activity, not traveling — start next plan entry
        StartNextEntry(person, state);
    }

    private static bool HasPendingInvitation(Person person, SimulationState state)
    {
        return state.PendingInvitationsByPersonId.TryGetValue(person.Id, out var invitations)
            && invitations.Count > 0;
    }

    private bool TryInjectPendingInvitation(Person person, SimulationState state)
    {
        if (!state.PendingInvitationsByPersonId.TryGetValue(person.Id, out var invitations)
            || invitations.Count == 0)
            return false;

        var inv = invitations[0];
        invitations.RemoveAt(0);

        var action = new AcceptPhoneCallAction(inv);
        var ctx = CreateContext(person, state);
        action.OnStart(ctx);
        person.CurrentActivity = action;

        state.Journal.Append(new SimulationEvent
        {
            Timestamp = state.Clock.CurrentTime,
            PersonId = person.Id,
            EventType = SimulationEventType.ActivityStarted,
            AddressId = person.CurrentAddressId,
            Description = action.DisplayText
        });

        return true;
    }

    private bool TryInjectAnsweringMachineCheck(Person person, SimulationState state)
    {
        if (!_pendingAnsweringMachineCheck.TryGetValue(person.Id, out var pending) || !pending)
            return false;

        _pendingAnsweringMachineCheck[person.Id] = false;

        var action = new CheckAnsweringMachineAction();
        var ctx = CreateContext(person, state);
        action.OnStart(ctx);
        person.CurrentActivity = action;

        state.Journal.Append(new SimulationEvent
        {
            Timestamp = state.Clock.CurrentTime,
            PersonId = person.Id,
            EventType = SimulationEventType.ActivityStarted,
            AddressId = person.CurrentAddressId,
            Description = action.DisplayText
        });

        return true;
    }

    private void StartNextEntry(Person person, SimulationState state)
    {
        if (TryInjectPendingInvitation(person, state))
            return;

        if (TryInjectAnsweringMachineCheck(person, state))
            return;

        var entry = person.DayPlan.Current;
        if (entry == null) return;

        var targetAddressId = entry.PlannedAction.TargetAddressId;

        if (person.CurrentAddressId != targetAddressId)
        {
            BeginTravel(person, state, targetAddressId);
        }
        else
        {
            StartActivity(person, state, entry);
        }
    }

    private void StartActivity(Person person, SimulationState state, DayPlanEntry entry)
    {
        var action = entry.PlannedAction.Action;
        var ctx = CreateContext(person, state);
        action.OnStart(ctx);
        person.CurrentActivity = action;
        entry.Status = DayPlanEntryStatus.Active;

        state.Journal.Append(new SimulationEvent
        {
            Timestamp = state.Clock.CurrentTime,
            PersonId = person.Id,
            EventType = SimulationEventType.ActivityStarted,
            AddressId = person.CurrentAddressId,
            Description = action.DisplayText
        });
    }

    private void BeginTravel(Person person, SimulationState state, int destinationAddressId)
    {
        var destAddress = state.Addresses[destinationAddressId];
        var currentTime = state.Clock.CurrentTime;

        // Log departure if currently at an address (not already traveling)
        if (person.CurrentAddressId.HasValue && person.CurrentAddressId != 0)
        {
            state.Journal.Append(new SimulationEvent
            {
                Timestamp = currentTime,
                PersonId = person.Id,
                EventType = SimulationEventType.DepartedAddress,
                FromAddressId = person.CurrentAddressId,
                ToAddressId = destinationAddressId
            });
        }

        var fromPosition = person.CurrentPosition;
        var travelHours = _mapConfig.ComputeTravelTimeHours(fromPosition, destAddress.Position);
        var arrivalTime = currentTime.AddHours(travelHours);

        person.TravelInfo = new TravelInfo
        {
            FromPosition = fromPosition,
            ToPosition = destAddress.Position,
            DepartureTime = currentTime,
            ArrivalTime = arrivalTime,
            FromAddressId = person.CurrentAddressId ?? 0,
            ToAddressId = destinationAddressId
        };

        person.CurrentAddressId = 0; // In transit
    }

    private void UpdateTravel(Person person, SimulationState state)
    {
        var travel = person.TravelInfo;
        var currentTime = state.Clock.CurrentTime;

        if (currentTime >= travel.ArrivalTime)
        {
            person.CurrentPosition = travel.ToPosition;
            person.CurrentAddressId = travel.ToAddressId;
            person.TravelInfo = null;

            // Check for answering machine messages when arriving home
            if (travel.ToAddressId == person.HomeAddressId && person.HomePhoneFixtureId.HasValue)
            {
                var messageTraces = state.GetTracesForFixture(person.HomePhoneFixtureId.Value, state.Clock.CurrentTime)
                    .Where(t => t.Type == TraceType.Record
                             && t.Description != null
                             && t.Description.Contains("please call back"))
                    .ToList();

                if (messageTraces.Count > 0)
                {
                    _pendingAnsweringMachineCheck[person.Id] = true;
                }
            }

            state.Journal.Append(new SimulationEvent
            {
                Timestamp = currentTime,
                PersonId = person.Id,
                EventType = SimulationEventType.ArrivedAtAddress,
                AddressId = travel.ToAddressId
            });

            // Start the activity now that we've arrived
            StartNextEntry(person, state);
        }
        else
        {
            var totalSeconds = (travel.ArrivalTime - travel.DepartureTime).TotalSeconds;
            var elapsedSeconds = (currentTime - travel.DepartureTime).TotalSeconds;
            var progress = Math.Clamp(elapsedSeconds / totalSeconds, 0.0, 1.0);
            person.CurrentPosition = travel.FromPosition.Lerp(travel.ToPosition, (float)progress);
        }
    }

    private void LogActivityCompleted(Person person, SimulationState state)
    {
        state.Journal.Append(new SimulationEvent
        {
            Timestamp = state.Clock.CurrentTime,
            PersonId = person.Id,
            EventType = SimulationEventType.ActivityCompleted,
            AddressId = person.CurrentAddressId,
            Description = person.CurrentActivity?.DisplayText
        });
    }

    private ActionContext CreateContext(Person person, SimulationState state)
    {
        if (!_personRandoms.TryGetValue(person.Id, out var random))
        {
            random = new Random(person.Id);
            _personRandoms[person.Id] = random;
        }

        return new ActionContext
        {
            Person = person,
            State = state,
            EventJournal = state.Journal,
            Random = random,
            CurrentTime = state.Clock.CurrentTime
        };
    }
}
