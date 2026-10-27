using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Core;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;
using Stakeout.Simulation.History;

namespace Stakeout.Simulation.Agents;

/// <summary>What a caller wants from a call, and how each outcome plays out.</summary>
public class CallPurpose
{
    /// <summary>The person the caller wants to speak to.</summary>
    public int ForPersonId { get; init; }
    /// <summary>Runs when the intended person picks up. Returns the conversation as a transcript.</summary>
    public Func<Agent, string> OnAnswered { get; init; }
    /// <summary>What the caller says to a machine (null: hang up without a message).</summary>
    public string MachineMessage { get; init; }
    /// <summary>Runs when the intended person hears the message on their machine.</summary>
    public Action<Agent> OnMessageHeard { get; init; }
    /// <summary>Runs when nobody (or the wrong person) answers.</summary>
    public Action<CallOutcome> OnFailed { get; init; }
}

/// <summary>
/// Telephone calls between fixtures. A phone is answered only by someone awake in the room's location;
/// otherwise its answering machine (if any) takes a message. Every call leaves a phone-company record.
/// </summary>
public class PhoneSystem
{
    private readonly World _world;
    private readonly Dictionary<int, CallPurpose> _pendingMessages = new();
    private readonly HashSet<int> _machines = new();
    private readonly Dictionary<string, int> _byNumber = new();

    public PhoneSystem(World world)
    {
        _world = world;
    }

    /// <summary>Wiretap listeners: phone fixture id → callback.</summary>
    public Dictionary<int, Action<CallFact>> Taps { get; } = new();

    public void Index()
    {
        _byNumber.Clear();
        _machines.Clear();
        foreach (var f in _world.State.Fixtures.Values)
        {
            if (f.Type == FixtureType.Telephone && f.PhoneNumber != null) _byNumber[f.PhoneNumber] = f.Id;
            if (f.Type == FixtureType.AnsweringMachine) _machines.Add(PhoneLocation(f));
        }
    }

    public Fixture ByNumber(string number) =>
        number != null && _byNumber.TryGetValue(number, out var id) ? _world.State.Fixtures[id] : null;

    public int PhoneLocation(Fixture phone) =>
        phone.LocationId ?? _world.State.SubLocations[phone.SubLocationId.Value].LocationId;

    public bool HasMachine(Fixture phone) => _machines.Contains(PhoneLocation(phone));

    /// <summary>The phone the agent could use where they stand.</summary>
    public Fixture PhoneAt(Agent agent) =>
        agent.Trip == null && agent.LocationId.HasValue ? _world.State.Phone(agent.LocationId.Value) : null;

    public CallFact Call(Agent caller, Fixture toPhone, CallPurpose purpose)
    {
        var fromPhone = PhoneAt(caller)
                        ?? throw new InvalidOperationException($"{caller} has no phone at hand");
        var now = _world.Now;
        int phoneLocation = PhoneLocation(toPhone);
        var present = _world.AgentsIn(phoneLocation)
            .Where(a => a.Person.IsAlive && !a.Asleep && a.Id != caller.Id)
            .OrderBy(a => a.Id == purpose.ForPersonId ? 0 : 1)
            .ToList();
        var rng = Rng.For(_world.Seed, Purpose.Call, caller.Id, now.Ticks);

        CallFact fact;
        if (present.Count > 0 && present[0].Id == purpose.ForPersonId)
        {
            var callee = present[0];
            string transcript = purpose.OnAnswered?.Invoke(callee) ?? "(small talk)";
            fact = Record(caller, fromPhone, toPhone, now, rng.Next(90, 420), CallOutcome.Answered, callee.Id,
                transcript, purpose.ForPersonId);
        }
        else if (present.Count > 0)
        {
            var other = present[0];
            var wanted = _world.State.People[purpose.ForPersonId];
            fact = Record(caller, fromPhone, toPhone, now, rng.Next(30, 90), CallOutcome.Answered, other.Id,
                $"{caller.Person.FirstName}: Is {wanted.FirstName} there? {other.Person.FirstName}: No, {wanted.HeShe} is out.",
                purpose.ForPersonId);
            purpose.OnFailed?.Invoke(CallOutcome.Answered);
        }
        else if (HasMachine(toPhone) && purpose.MachineMessage != null)
        {
            fact = Record(caller, fromPhone, toPhone, now, rng.Next(20, 60), CallOutcome.AnsweringMachine, null,
                purpose.MachineMessage, purpose.ForPersonId);
            _pendingMessages[fact.Id] = purpose;
        }
        else
        {
            fact = Record(caller, fromPhone, toPhone, now, 30, CallOutcome.NoAnswer, null, null, purpose.ForPersonId);
            purpose.OnFailed?.Invoke(CallOutcome.NoAnswer);
        }
        return fact;
    }

    private CallFact Record(Agent caller, Fixture from, Fixture to, DateTime time, int seconds, CallOutcome outcome,
        int? answeredBy, string transcript, int forPersonId)
    {
        var fact = new CallFact
        {
            Id = _world.NextFactId(), CallerId = caller.Id, FromPhoneId = from.Id, ToPhoneId = to.Id, Time = time,
            DurationSeconds = seconds, Outcome = outcome, AnsweredById = answeredBy, Transcript = transcript,
            IntendedId = forPersonId
        };
        _world.History.AddCall(fact);
        if (Taps.TryGetValue(from.Id, out var tapFrom)) tapFrom(fact);
        if (to.Id != from.Id && Taps.TryGetValue(to.Id, out var tapTo)) tapTo(fact);
        return fact;
    }

    /// <summary>Messages on the machine at a phone's location that are still on the tape.</summary>
    public IEnumerable<CallFact> TapeAt(Fixture phone, DateTime time) =>
        _world.History.CallsOf(phone.Id)
            .Where(c => c.ToPhoneId == phone.Id && c.Outcome == CallOutcome.AnsweringMachine && c.Time <= time
                        && (c.ErasedAt == null || c.ErasedAt > time));

    /// <summary>Someone arrived home: they play back the messages left for them.</summary>
    public void CheckMachine(Agent agent)
    {
        var phone = _world.State.PhoneOf(agent.Person);
        if (phone == null || !HasMachine(phone)) return;
        foreach (var call in TapeAt(phone, _world.Now).ToList())
        {
            if (call.Heard || call.IntendedId != agent.Id) continue;
            call.Heard = true;
            // Most people erase a message once they have dealt with it.
            if (Rng.Chance(0.5, _world.Seed, Purpose.Call, call.Id, 7)) call.ErasedAt = _world.Now.AddMinutes(2);
            if (_pendingMessages.Remove(call.Id, out var purpose)) purpose.OnMessageHeard?.Invoke(agent);
        }
    }
}
