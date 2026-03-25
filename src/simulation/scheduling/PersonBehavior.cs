using System;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;

namespace Stakeout.Simulation.Scheduling;

public class PersonBehavior
{
    private readonly MapConfig _mapConfig;

    public PersonBehavior(MapConfig mapConfig)
    {
        _mapConfig = mapConfig;
    }

    public void Update(Person person, DailySchedule schedule, SimulationState state)
    {
        var currentTime = state.Clock.CurrentTime;
        var timeOfDay = currentTime.TimeOfDay;
        var entry = schedule.GetEntryAtTime(timeOfDay);

        // If person is currently travelling, handle travel interpolation
        if (person.CurrentAction == ActionType.TravelByCar && person.TravelInfo != null)
        {
            UpdateTravel(person, state);
            return;
        }

        // If the scheduled activity differs from current, transition
        if (entry.Action != person.CurrentAction)
        {
            Transition(person, entry, state);
        }
    }

    private void UpdateTravel(Person person, SimulationState state)
    {
        var travel = person.TravelInfo;
        var currentTime = state.Clock.CurrentTime;

        if (currentTime >= travel.ArrivalTime)
        {
            // Arrived
            person.CurrentPosition = travel.ToPosition;
            person.CurrentAddressId = travel.ToAddressId;

            state.Journal.Append(new SimulationEvent
            {
                Timestamp = currentTime,
                PersonId = person.Id,
                EventType = SimulationEventType.ArrivedAtAddress,
                AddressId = travel.ToAddressId
            });

            person.TravelInfo = null;

            // Leave activity as TravellingByCar; the next Update call will
            // detect the mismatch with the schedule and transition properly.
        }
        else
        {
            // Interpolate position
            var totalSeconds = (travel.ArrivalTime - travel.DepartureTime).TotalSeconds;
            var elapsedSeconds = (currentTime - travel.DepartureTime).TotalSeconds;
            var progress = Math.Clamp(elapsedSeconds / totalSeconds, 0.0, 1.0);

            person.CurrentPosition = travel.FromPosition.Lerp(travel.ToPosition, (float)progress);
        }
    }

    private void Transition(Person person, ScheduleEntry entry, SimulationState state)
    {
        var currentTime = state.Clock.CurrentTime;
        var oldActivity = person.CurrentAction;

        // Log end of old activity
        LogActivityEnd(person, oldActivity, state);

        if (entry.Action == ActionType.TravelByCar)
        {
            // Start travel
            StartTravel(person, entry, state);
        }
        else
        {
            // Check if we need to travel to the target location
            var targetAddressId = GetTargetAddressId(person, entry);

            if (targetAddressId.HasValue && person.CurrentAddressId != targetAddressId)
            {
                // Need to travel first
                StartTravelToAddress(person, entry, targetAddressId.Value, state);
            }
            else
            {
                // Same location, switch activity directly
                person.CurrentAction = entry.Action;
                LogActivityStart(person, entry.Action, state);
            }
        }

        // Log activity changed
        state.Journal.Append(new SimulationEvent
        {
            Timestamp = currentTime,
            PersonId = person.Id,
            EventType = SimulationEventType.ActionChanged,
            OldAction = oldActivity,
            NewAction = person.CurrentAction
        });
    }

    private void StartTravel(Person person, ScheduleEntry entry, SimulationState state)
    {
        var currentTime = state.Clock.CurrentTime;

        var fromAddressId = entry.FromAddressId ?? person.CurrentAddressId ?? person.HomeAddressId;
        var toAddressId = entry.TargetAddressId ?? person.HomeAddressId;

        var fromAddress = state.Addresses[fromAddressId];
        var toAddress = state.Addresses[toAddressId];

        var travelHours = _mapConfig.ComputeTravelTimeHours(fromAddress.Position, toAddress.Position);
        var arrivalTime = currentTime.AddHours(travelHours);

        person.TravelInfo = new TravelInfo
        {
            FromPosition = fromAddress.Position,
            ToPosition = toAddress.Position,
            DepartureTime = currentTime,
            ArrivalTime = arrivalTime,
            FromAddressId = fromAddressId,
            ToAddressId = toAddressId
        };

        person.CurrentAction = ActionType.TravelByCar;
        person.CurrentAddressId = null;

        state.Journal.Append(new SimulationEvent
        {
            Timestamp = currentTime,
            PersonId = person.Id,
            EventType = SimulationEventType.DepartedAddress,
            FromAddressId = fromAddressId,
            ToAddressId = toAddressId
        });
    }

    private void StartTravelToAddress(Person person, ScheduleEntry entry, int targetAddressId, SimulationState state)
    {
        var currentTime = state.Clock.CurrentTime;

        var fromAddressId = person.CurrentAddressId ?? person.HomeAddressId;
        var fromAddress = state.Addresses[fromAddressId];
        var toAddress = state.Addresses[targetAddressId];

        var travelHours = _mapConfig.ComputeTravelTimeHours(fromAddress.Position, toAddress.Position);
        var arrivalTime = currentTime.AddHours(travelHours);

        person.TravelInfo = new TravelInfo
        {
            FromPosition = fromAddress.Position,
            ToPosition = toAddress.Position,
            DepartureTime = currentTime,
            ArrivalTime = arrivalTime,
            FromAddressId = fromAddressId,
            ToAddressId = targetAddressId
        };

        person.CurrentAction = ActionType.TravelByCar;
        person.CurrentAddressId = null;

        state.Journal.Append(new SimulationEvent
        {
            Timestamp = currentTime,
            PersonId = person.Id,
            EventType = SimulationEventType.DepartedAddress,
            FromAddressId = fromAddressId,
            ToAddressId = targetAddressId
        });
    }

    private int? GetTargetAddressId(Person person, ScheduleEntry entry)
    {
        return entry.TargetAddressId;
    }

    private void LogActivityEnd(Person person, ActionType activity, SimulationState state)
    {
        var eventType = activity switch
        {
            ActionType.Work => SimulationEventType.StoppedWorking,
            ActionType.Sleep => SimulationEventType.WokeUp,
            _ => (SimulationEventType?)null
        };

        if (eventType.HasValue)
        {
            state.Journal.Append(new SimulationEvent
            {
                Timestamp = state.Clock.CurrentTime,
                PersonId = person.Id,
                EventType = eventType.Value,
                AddressId = person.CurrentAddressId
            });
        }
    }

    private void LogActivityStart(Person person, ActionType activity, SimulationState state)
    {
        var eventType = activity switch
        {
            ActionType.Work => SimulationEventType.StartedWorking,
            ActionType.Sleep => SimulationEventType.FellAsleep,
            _ => (SimulationEventType?)null
        };

        if (eventType.HasValue)
        {
            state.Journal.Append(new SimulationEvent
            {
                Timestamp = state.Clock.CurrentTime,
                PersonId = person.Id,
                EventType = eventType.Value,
                AddressId = person.CurrentAddressId
            });
        }
    }
}
