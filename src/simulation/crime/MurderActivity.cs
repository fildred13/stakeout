using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Agents;
using Stakeout.Simulation.Core;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;
using Stakeout.Simulation.History;

namespace Stakeout.Simulation.Crime;

/// <summary>
/// The killer goes to the victim's home and kills them. Invited: the victim expects the visit and lets the
/// killer in. Uninvited: the killer turns up and waits for the victim if needed. Break-in: at night, through
/// a forced door or window, while the victim sleeps. Every step writes facts (touches, noises, death, items).
/// </summary>
public class MurderActivity : JointActivity
{
    private readonly CrimeSystem _crimes;
    public MurderCase Case { get; }
    private bool _entered;
    private bool _waitingForVictim;

    public override string Name => "murder";

    public MurderActivity(World world, CrimeSystem crimes, MurderCase murder, DateTime start, bool victimExpectsVisit)
        : base(world)
    {
        _crimes = crimes;
        Case = murder;
        MeetingAddressId = murder.SceneAddressId;
        Start = start;
        ExpectedDuration = TimeSpan.FromMinutes(murder.Approach == MurderApproach.BreakIn ? 25 : 60);
        Patience = TimeSpan.FromMinutes(20);
        ParticipantIds.Add(murder.KillerId);
        if (victimExpectsVisit) ParticipantIds.Add(murder.VictimId);
    }

    private Agent Killer => AgentOf(Case.KillerId);
    private Agent Victim => AgentOf(Case.VictimId);
    private Person VictimPerson => World.State.People[Case.VictimId];

    public override string Describe(Agent agent) => agent.Id == Case.VictimId
        ? $"expecting {World.State.People[Case.KillerId].FirstName}"
        : $"going to see {VictimPerson.FirstName}";

    public override Appointment BlockFor(Agent agent)
    {
        var block = base.BlockFor(agent);
        if (agent.Id != Case.VictimId) return block;
        // The victim stays home for the visit, from a little before it.
        return new Appointment
        {
            AddressId = block.AddressId, Spot = Spot.HomeLiving, ArriveBy = Start.AddMinutes(-15),
            LeaveAt = Start.AddMinutes(90), Kind = ActivityKind.Host, Joint = this, Label = block.Label
        };
    }

    public override (int LocationId, int? SubLocationId) WaitingSpot(Agent agent) =>
        agent.Id == Case.VictimId
            ? World.Places.HomeRoom(agent.Person, Spot.HomeLiving)
            : (World.Movement.ArrivalLocation(MeetingAddressId).Id, null);

    protected override bool CanBeginWith(IReadOnlyCollection<int> ready) => ready.Contains(Case.KillerId);

    private bool VictimHome() =>
        Victim.Person.IsAlive && Victim.Trip == null && Victim.LocationId == Case.SceneLocationId;

    protected override void Run(List<Agent> present)
    {
        if (Case.Approach == MurderApproach.BreakIn)
        {
            BreakIn();
            return;
        }
        if (VictimHome())
        {
            Enter();
            return;
        }
        // Wait outside in the car for the victim to come home.
        _waitingForVictim = true;
        World.Movement.SetActivity(Killer, ActivityKind.Waiting, "waiting outside");
        for (int minutes = 5; minutes <= 90; minutes += 5)
        {
            int m = minutes;
            After(TimeSpan.FromMinutes(m), () =>
            {
                if (_entered || !_waitingForVictim) return;
                if (VictimHome()) Enter();
                else if (m == 90)
                {
                    _waitingForVictim = false;
                    _crimes.FallBackToBreakIn(Case);
                    Abort();
                }
            });
        }
    }

    protected override void OnLateArrival(Agent agent)
    {
        if (agent.Id == Case.VictimId && _waitingForVictim && !_entered)
        {
            Enter();
            return;
        }
        if (agent.Id == Case.VictimId && _entered) return; // already being dealt with
        base.OnLateArrival(agent);
    }

    private void HoldVictim()
    {
        if (Victim.HeldBy != this) World.Executor.Hold(Victim, this);
    }

    private void Enter()
    {
        if (_entered) return;
        _entered = true;
        _waitingForVictim = false;
        HoldVictim();
        Case.Approach = ParticipantIds.Contains(Case.VictimId) ? MurderApproach.Invited : MurderApproach.Uninvited;
        var (loc, living) = World.Places.HomeRoom(VictimPerson, Spot.HomeLiving);
        var door = MainDoor(loc);
        var killerPerson = Killer.Person;
        var rng = Rng.For(World.Seed, Purpose.Crime, Case.Id, 2);

        // The victim opens the door; they talk in the living room.
        World.Movement.Settle(Victim, loc, living, ActivityKind.Host, $"talking with {killerPerson.FirstName}");
        World.Movement.GoInside(Killer, loc, living, ActivityKind.Crime, $"talking with {VictimPerson.FirstName}", () =>
        {
            if (door.HasValue) Touch(Killer, door.Value);
            var table = TableIn(living);
            if (table != null && (!killerPerson.Has(Traits.Careful) || rng.NextDouble() < 0.35)) Touch(Killer, table.Id);
            if (killerPerson.Has(Traits.Smoker) && rng.NextDouble() < 0.7) _crimes.LeaveCigarette(Case, Killer, loc, living);
        });

        int talk = rng.Next(12, 35);
        if (rng.NextDouble() < 0.65)
            After(TimeSpan.FromMinutes(talk / 2), () =>
                World.History.Noises.Add(new NoiseFact
                {
                    AddressId = Case.SceneAddressId, LocationId = loc, Time = Now, Kind = NoiseKind.Argument,
                    SourcePersonId = Case.KillerId
                }));
        After(TimeSpan.FromMinutes(talk), () => Kill(loc, living, rng));
    }

    private void BreakIn()
    {
        var rng = Rng.For(World.Seed, Purpose.Crime, Case.Id, 3);
        var victimPerson = VictimPerson;
        if (!VictimHome() || !Victim.Asleep)
        {
            Case.Status = CaseStatus.Aborted;
            Abort();
            _crimes.Recast(Case);
            return;
        }
        _entered = true;
        HoldVictim();
        var home = World.State.Locations[Case.SceneLocationId];
        var killerPerson = Killer.Person;
        Case.Gloves = killerPerson.Has(Traits.Careful) ? rng.NextDouble() < 0.75 : rng.NextDouble() < 0.25;

        var entry = home.AccessPoints.FirstOrDefault(a => a.HasTag("covert_entry") && a.Type == AccessPointType.Door)
                    ?? home.AccessPoints.FirstOrDefault(a => a.HasTag("covert_entry"))
                    ?? home.AccessPoints.First(a => a.HasTag("main_entrance"));
        bool picked = entry.Type == AccessPointType.Door && rng.NextDouble() < 0.5;
        World.History.ForcedEntries.Add(new ForcedEntryFact
        {
            PersonId = Case.KillerId, AccessPointId = entry.Id, LocationId = home.Id, Time = Now, Picked = picked
        });
        entry.IsBroken = !picked;
        entry.IsLocked = false;
        if (!picked)
            World.History.Noises.Add(new NoiseFact
            {
                AddressId = Case.SceneAddressId, LocationId = home.Id, Time = Now,
                Kind = entry.Type == AccessPointType.Window ? NoiseKind.BreakingGlass : NoiseKind.Crash,
                SourcePersonId = Case.KillerId
            });
        Touch(Killer, entry.Id, Case.Gloves);

        var bedroom = Victim.SubLocationId;
        World.Movement.GoInside(Killer, home.Id, bedroom, ActivityKind.Crime, "in the bedroom");
        After(TimeSpan.FromMinutes(3), () => Kill(home.Id, bedroom, rng));
    }

    private Fixture TableIn(int? subLocationId) => subLocationId.HasValue
        ? World.State.GetFixturesForSubLocation(subLocationId.Value).FirstOrDefault(f => f.Type == FixtureType.Table)
        : null;

    private void Kill(int locationId, int? subLocationId, Random rng)
    {
        if (!Victim.Person.IsAlive) return;
        if (Killer.LocationId != locationId) World.Movement.Settle(Killer, locationId, subLocationId, ActivityKind.Crime, "");
        var weapon = _crimes.TakeWeapon(Case, Killer, locationId);
        Touch(Killer, weapon.Id, Case.Gloves);

        World.History.Noises.Add(new NoiseFact
        {
            AddressId = Case.SceneAddressId, LocationId = locationId, Time = Now,
            Kind = Case.Weapon == WeaponKind.Handgun ? NoiseKind.Gunshot : NoiseKind.Scream, SourcePersonId = Case.VictimId
        });
        _crimes.KillVictim(Case, Victim, Killer, locationId, Victim.SubLocationId ?? subLocationId, weapon);

        // Cover-up, then leave.
        int cleanup = rng.Next(3, 11);
        After(TimeSpan.FromMinutes(cleanup), () =>
        {
            _crimes.CoverUp(Case, Killer, locationId, rng);
            Finish();
        });
    }

    public override void ParticipantLost(Agent agent)
    {
        if (agent.Id == Case.VictimId && _entered) return; // the victim dying is the point
        base.ParticipantLost(agent);
    }
}
