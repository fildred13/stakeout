using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.History;

namespace Stakeout.Simulation.Agents;

/// <summary>
/// One activity shared by several people (a date, a visit, a crime). Each participant's agenda holds a block
/// for it; they make their own way to the meeting point, and from the moment they get there the activity
/// moves them: all participants travel in one trip and stay in one place, so they are together by construction.
/// </summary>
public abstract class JointActivity
{
    protected readonly World World;
    private readonly HashSet<int> _ready = new();
    private bool _committed;

    public int Id { get; }
    public abstract string Name { get; }
    public DateTime Start { get; protected set; }
    public TimeSpan ExpectedDuration { get; protected set; }
    public DateTime EstimatedEnd => Start + ExpectedDuration;
    public int MeetingAddressId { get; protected set; }
    public List<int> ParticipantIds { get; } = new();
    public TimeSpan Patience { get; protected set; } = TimeSpan.FromMinutes(40);
    public bool Begun { get; private set; }
    public bool Finished { get; private set; }
    public bool Aborted { get; private set; }

    protected JointActivity(World world)
    {
        World = world;
        Id = world.NextFactId();
    }

    public IEnumerable<Agent> Participants => ParticipantIds.Select(id => World.Agents[id]);
    protected Agent AgentOf(int id) => World.Agents[id];
    protected DateTime Now => World.Now;

    /// <summary>Adds the activity to every participant's commitments and schedules its start.</summary>
    public void Commit()
    {
        if (_committed) return;
        _committed = true;
        World.Joints.Add(this);
        World.Scheduler.At(Start, OnStartTime);
        foreach (var agent in Participants)
        {
            agent.Commitments.Add(this);
            World.Executor.CommitmentsChanged(agent);
        }
    }

    public virtual Appointment BlockFor(Agent agent) => new()
    {
        AddressId = MeetingAddressId,
        Spot = Spot.Entrance,
        ArriveBy = Start,
        LeaveAt = EstimatedEnd,
        Kind = ActivityKind.Waiting,
        Joint = this,
        Label = Describe(agent)
    };

    public virtual string Describe(Agent agent) => Name;
    public virtual int EndAddressFor(Agent agent) => MeetingAddressId;

    /// <summary>Where a participant waits at the meeting point until everyone is there.</summary>
    public virtual (int LocationId, int? SubLocationId) WaitingSpot(Agent agent) =>
        (World.Movement.ArrivalLocation(MeetingAddressId).Id, null);

    protected virtual bool CanBeginWith(IReadOnlyCollection<int> ready) => ready.Count == ParticipantIds.Count;

    public bool IsReady(int personId) => _ready.Contains(personId);

    public void ParticipantReady(Agent agent)
    {
        if (Finished)
        {
            World.Executor.Release(agent, this);
            return;
        }
        _ready.Add(agent.Id);
        World.Executor.Hold(agent, this);
        if (Begun)
        {
            OnLateArrival(agent);
            return;
        }
        if (Now >= Start && _ready.Count == ParticipantIds.Count) Begin();
    }

    protected virtual void OnLateArrival(Agent agent) => World.Executor.Release(agent, this);

    private void OnStartTime()
    {
        if (Begun || Finished) return;
        if (_ready.Count == ParticipantIds.Count)
        {
            Begin();
            return;
        }
        World.Scheduler.At(Start + Patience, () =>
        {
            if (Begun || Finished) return;
            if (CanBeginWith(_ready)) Begin();
            else Abort();
        });
    }

    private void Begin()
    {
        Begun = true;
        Run(Participants.Where(a => _ready.Contains(a.Id)).ToList());
    }

    /// <summary>The script, run once everyone needed is present. Must end with <see cref="Finish"/>.</summary>
    protected abstract void Run(List<Agent> present);

    /// <summary>A participant died or was otherwise removed before or during the activity.</summary>
    public virtual void ParticipantLost(Agent agent)
    {
        if (!Begun) Abort();
    }

    public void Abort()
    {
        if (Finished) return;
        Finished = true;
        Aborted = true;
        foreach (var agent in Participants)
        {
            agent.Commitments.Remove(this);
            if (agent.HeldBy == this) World.Executor.Release(agent, this);
            else World.Executor.CommitmentsChanged(agent);
        }
    }

    protected void Finish()
    {
        if (Finished) return;
        Finished = true;
        foreach (var agent in Participants)
        {
            agent.Commitments.Remove(this);
            if (agent.HeldBy == this) World.Executor.Release(agent, this);
        }
    }

    protected void Release(Agent agent) => World.Executor.Release(agent, this);

    // ---- script helpers ----

    /// <summary>
    /// Moves a group standing together to another address in one trip: the leader's car if it is here,
    /// otherwise on foot side by side.
    /// </summary>
    protected void MoveTogether(IReadOnlyList<Agent> group, int toAddressId, Action then)
    {
        var vehicle = World.Movement.AvailableVehicle(group[0]);
        World.Movement.StartTrip(group, vehicle, toAddressId, _ =>
        {
            if (!Finished) then();
        });
    }

    protected void After(TimeSpan delay, Action then) =>
        World.Scheduler.After(delay, () =>
        {
            if (!Finished) then();
        });

    protected void Touch(Agent agent, int surfaceId, bool gloved = false) =>
        World.History.AddTouch(new TouchFact { PersonId = agent.Id, SurfaceId = surfaceId, Time = Now, Gloved = gloved });

    /// <summary>The main door of a home location, which visitors touch on the way in and out.</summary>
    protected int? MainDoor(int locationId) =>
        World.State.Locations[locationId].AccessPoints.FirstOrDefault(a => a.HasTag("main_entrance"))?.Id;
}
