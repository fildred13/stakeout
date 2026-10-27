using System;
using System.Linq;
using Stakeout.Simulation.History;

namespace Stakeout.Simulation.Agents;

/// <summary>
/// Carries out each agent's agenda: waits, departs in time to arrive by the appointment, settles into the
/// right room, and hands the agent to a joint activity when one starts. Travel is always computed from where
/// the agent actually is.
/// </summary>
public class Executor
{
    private readonly World _world;

    public Executor(World world)
    {
        _world = world;
    }

    /// <summary>Throws away the agenda and plans again from the agent's current position and time.</summary>
    public void Replan(Agent agent)
    {
        if (agent.ExternallyControlled || !agent.Person.IsAlive || agent.HeldBy != null) return;
        agent.Pending?.Cancel();
        agent.Pending = null;
        if (agent.Trip != null) return; // re-entered on arrival
        agent.Plan = _world.Planner.PlanFrom(agent, _world.Now);
        agent.PlanIndex = 0;
        Continue(agent);
    }

    /// <summary>Acts on the current appointment: go there, or settle there, or wait to leave.</summary>
    public void Continue(Agent agent)
    {
        if (agent.ExternallyControlled || !agent.Person.IsAlive || agent.HeldBy != null || agent.Trip != null) return;
        agent.Pending?.Cancel();
        agent.Pending = null;

        var now = _world.Now;
        while (agent.PlanIndex < agent.Plan.Count)
        {
            var next = agent.Plan[agent.PlanIndex];
            bool expired = next.LeaveAt <= now && next.Joint == null;
            bool jointGone = next.Joint != null && (next.Joint.Finished || next.LeaveAt.AddHours(2) <= now);
            if (!expired && !jointGone) break;
            agent.PlanIndex++;
        }
        if (agent.PlanIndex >= agent.Plan.Count)
        {
            Replan(agent);
            return;
        }

        var ap = agent.Plan[agent.PlanIndex];
        if (agent.AddressId == ap.AddressId)
        {
            Engage(agent, ap);
            return;
        }

        var mode = _world.Movement.ModeFor(agent);
        var travel = _world.Movement.TravelEstimate(agent.AddressId.Value, ap.AddressId, mode);
        var departAt = ap.ArriveBy - travel;
        if (ap.Optional && departAt < now.AddMinutes(-15))
        {
            agent.PlanIndex++;
            Continue(agent);
            return;
        }
        if (departAt <= now)
            Depart(agent, ap);
        else
            agent.Pending = _world.Scheduler.At(departAt, () => Depart(agent, ap));
    }

    private void Depart(Agent agent, Appointment ap)
    {
        if (agent.HeldBy != null || !agent.Person.IsAlive || agent.Trip != null) return;
        agent.Pending = null;
        if (agent.AddressId == ap.AddressId)
        {
            Engage(agent, ap);
            return;
        }
        var vehicle = _world.Movement.AvailableVehicle(agent);
        _world.Movement.StartTrip(new[] { agent }, vehicle, ap.AddressId, _ =>
        {
            if (agent.HeldBy != null || !agent.Person.IsAlive) return;
            if (agent.ReplanOnArrival)
            {
                agent.ReplanOnArrival = false;
                Replan(agent);
                return;
            }
            if (agent.CurrentAppointment == ap) Engage(agent, ap);
            else Continue(agent);
        });
    }

    private void Engage(Agent agent, Appointment ap)
    {
        if (ap.Joint != null)
        {
            var (loc, sub) = _world.Places.WaitingSpot(agent, ap.Joint);
            _world.Movement.GoInside(agent, loc, sub, ActivityKind.Waiting, $"waiting: {ap.Label}",
                () => ap.Joint.ParticipantReady(agent));
            return;
        }

        var (locationId, subLocationId) = _world.Places.Resolve(agent, ap);
        _world.Movement.GoInside(agent, locationId, subLocationId, ap.Kind, ap.Label, () =>
        {
            ap.OnArrive?.Invoke(agent);
            _world.OnSettled(agent, ap);
        });
        var leave = ap.LeaveAt > _world.Now ? ap.LeaveAt : _world.Now;
        agent.Pending = _world.Scheduler.At(leave, () =>
        {
            agent.Pending = null;
            if (agent.CurrentAppointment == ap) agent.PlanIndex++;
            Continue(agent);
        });
    }

    /// <summary>Takes an agent out of their own agenda for a joint activity that is starting now.</summary>
    public void Hold(Agent agent, JointActivity activity)
    {
        agent.Pending?.Cancel();
        agent.Pending = null;
        agent.HeldBy = activity;
    }

    /// <summary>Gives the agent back to their own agenda.</summary>
    public void Release(Agent agent, JointActivity activity)
    {
        if (agent.HeldBy == activity) agent.HeldBy = null;
        agent.Commitments.Remove(activity);
        if (!agent.Person.IsAlive || agent.ExternallyControlled) return;
        if (agent.Trip != null) return;
        Replan(agent);
    }

    /// <summary>Called when a commitment is added or cancelled, so the agenda accounts for it.</summary>
    public void CommitmentsChanged(Agent agent)
    {
        if (agent.HeldBy != null) return; // picked up on release
        if (agent.Trip != null)
        {
            agent.ReplanOnArrival = true;
            return;
        }
        Replan(agent);
    }

    /// <summary>Stops everything an agent was doing (death, arrest).</summary>
    public void Stop(Agent agent)
    {
        agent.Pending?.Cancel();
        agent.Pending = null;
        agent.Plan.Clear();
        agent.PlanIndex = 0;
        foreach (var c in agent.Commitments.ToList()) c.ParticipantLost(agent);
        agent.Commitments.Clear();
    }
}
