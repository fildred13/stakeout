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
/// Turns an existing tension into a murder: casts killer and victim, has the killer phone ahead or plan a
/// break-in, runs the killing as a joint activity, then the cover-up, the false alibi, the discovery of the
/// body by a neighbour and the call to the police.
/// </summary>
public class CrimeSystem
{
    private readonly World _world;
    private SimulationState State => _world.State;
    public List<MurderCase> Cases { get; } = new();

    /// <summary>The police were called: the scanner alert.</summary>
    public event Action<MurderCase> Reported;

    /// <summary>Phone number of the police dispatcher (a fixture in no room: the dispatcher is not simulated).</summary>
    public Fixture Dispatch { get; private set; }

    public CrimeSystem(World world)
    {
        _world = world;
        world.Settled += OnSettled;
    }

    private void OnSettled(Agent agent, Appointment ap)
    {
        if (agent.AddressId != agent.Person.HomeAddressId) return;
        foreach (var murder in Cases.Where(c => c.KillerId == agent.Id && c.Status is CaseStatus.Committed or CaseStatus.Reported))
            if (State.Items.Values.Any(i => i.HeldByPersonId == agent.Id && (i.Id == murder.WeaponItemId || i.Id == murder.BloodyClothesItemId)))
                StashAtHome(murder, agent);
    }

    private Fixture EnsureDispatch()
    {
        if (Dispatch != null) return Dispatch;
        Dispatch = new Fixture
        {
            Id = State.GenerateEntityId(), Name = "Police Dispatch", Type = FixtureType.Telephone, PhoneNumber = "911",
            Tags = new[] { "police" }
        };
        State.Fixtures[Dispatch.Id] = Dispatch;
        return Dispatch;
    }

    // ---- casting ----

    /// <summary>Schedules casting at noon of <paramref name="day"/> for a murder that night.</summary>
    public void ScheduleMurder(DateTime day)
    {
        EnsureDispatch();
        _world.Scheduler.At(day.Date.AddHours(12), () => CastMurder(day.Date, 0));
    }

    public MurderCase CastMurder(DateTime night, int attempt)
    {
        var rng = Rng.For(_world.Seed, Purpose.Crime, night.Ticks, attempt);
        var candidates = new List<(double Score, Tension Tension)>();
        foreach (var t in State.Tensions.Values.OrderBy(t => t.Id))
        {
            if (t.Kind == TensionKind.Grudge) continue;
            var killer = State.People[t.AggressorId];
            var victim = State.People[t.TargetId];
            if (!killer.IsAlive || !victim.IsAlive || killer.IsPlayer || victim.IsPlayer) continue;
            if (Cases.Any(c => c.VictimId == victim.Id || c.KillerId == killer.Id)) continue;
            if (State.Households[victim.HouseholdId].MemberIds.Count != 1) continue;
            if (killer.HomeAddressId == victim.HomeAddressId && State.Addresses[victim.HomeAddressId].Type == AddressType.SuburbanHome) continue;
            int sleepHour = victim.SleepTime.Hours;
            if (sleepHour is > 2 and < 21) continue; // keeps a normal night
            double kind = t.Kind switch
            {
                TensionKind.Affair => 1.1, TensionKind.Jealousy => 1.15, TensionKind.Embezzlement => 1.25, _ => 1.0
            };
            double score = t.Intensity * kind * (killer.Has(Traits.Violent) ? 1.6 : 1.0) * (killer.VehicleId.HasValue ? 1.2 : 0.7)
                           * (0.5 + rng.NextDouble());
            candidates.Add((score, t));
        }

        foreach (var (_, tension) in candidates.OrderByDescending(c => c.Score).Take(12))
        {
            var plot = TryPlot(tension, night, rng);
            if (plot != null) return plot;
        }
        if (attempt < 3) _world.Scheduler.At(night.AddDays(1).AddHours(12), () => CastMurder(night.AddDays(1), attempt + 1));
        return null;
    }

    private MurderCase TryPlot(Tension tension, DateTime night, Random rng)
    {
        var killerAgent = _world.Agents[tension.AggressorId];
        var victimAgent = _world.Agents[tension.TargetId];
        var killer = killerAgent.Person;
        var victim = victimAgent.Person;

        // The killer picks an evening hour when both of them are free and the victim is still up.
        var visit = night.AddHours(21).AddMinutes(rng.Next(-6, 10) * 5);
        bool invitedPossible = false;
        foreach (var offset in new[] { 0, -45, -90, 45 })
        {
            var t = visit.AddMinutes(offset);
            if (FreeForVisit(killerAgent, t, needAwakeAfter: false) && FreeForVisit(victimAgent, t, needAwakeAfter: true))
            {
                visit = t;
                invitedPossible = true;
                break;
            }
        }
        var breakInTime = night.AddDays(1).AddHours(2).AddMinutes(rng.Next(0, 13) * 5);
        bool breakInPossible = FreeForBreakIn(killerAgent, breakInTime) && AsleepAtHome(victim, breakInTime);
        if (!invitedPossible && !breakInPossible) return null;

        bool breakIn = !invitedPossible || (breakInPossible && rng.NextDouble() < (killer.Has(Traits.Careful) ? 0.35 : 0.2));
        var weapon = killer.Has(Traits.GunOwner) && GunOf(killer) != null && rng.NextDouble() < 0.6 ? WeaponKind.Handgun
            : rng.NextDouble() < 0.65 ? WeaponKind.Knife : WeaponKind.BluntObject;

        var murder = new MurderCase
        {
            Id = _world.NextFactId(), KillerId = killer.Id, VictimId = victim.Id, Motive = tension,
            Approach = breakIn ? MurderApproach.BreakIn : MurderApproach.Invited, Weapon = weapon,
            PlannedFor = breakIn ? breakInTime : visit,
            SceneAddressId = victim.HomeAddressId, SceneLocationId = victim.HomeLocationId
        };
        Cases.Add(murder);

        if (breakIn)
            new MurderActivity(_world, this, murder, breakInTime, victimExpectsVisit: false).Commit();
        else
            ArrangeVisit(murder, visit, rng);
        return murder;
    }

    /// <summary>Off work and without plans around the visit; the victim must still be up when it starts.</summary>
    private bool FreeForVisit(Agent agent, DateTime visit, bool needAwakeAfter)
    {
        var position = State.PositionOf(agent.Person);
        if (Planner.Shifts(position, visit.AddMinutes(-75), visit.AddMinutes(75)).Any()) return false;
        if (agent.Commitments.Any(c => c.Start < visit.AddMinutes(90) && c.EstimatedEnd > visit.AddMinutes(-90))) return false;
        var (sleepStart, wake) = Planner.NextSleep(agent.Person, visit.AddMinutes(-60));
        if (sleepStart < visit.AddMinutes(needAwakeAfter ? 30 : -60) && wake > visit) return false;
        return true;
    }

    private bool FreeForBreakIn(Agent agent, DateTime time)
    {
        var position = State.PositionOf(agent.Person);
        if (Planner.Shifts(position, time.AddHours(-2), time.AddHours(2)).Any()) return false;
        return !agent.Commitments.Any(c => c.Start < time.AddHours(2) && c.EstimatedEnd > time.AddHours(-2));
    }

    private bool AsleepAtHome(Person person, DateTime time)
    {
        var (start, end) = Planner.NextSleep(person, time);
        if (!(start <= time.AddMinutes(-60) && end > time.AddMinutes(60))) return false;
        var agent = _world.Agents[person.Id];
        return !agent.Commitments.Any(c => c.Start < time.AddHours(3) && c.EstimatedEnd > time.AddHours(-6));
    }

    private Item GunOf(Person killer) =>
        State.Items.Values.FirstOrDefault(i => i.ItemType == ItemType.Handgun && i.OwnerPersonId == killer.Id);

    /// <summary>The killer phones the victim during the evening to set up a visit.</summary>
    private void ArrangeVisit(MurderCase murder, DateTime visit, Random rng)
    {
        var killer = _world.Agents[murder.KillerId];
        var victim = _world.Agents[murder.VictimId];
        var day = visit.Date;
        var attempts = new[] { day.AddHours(17).AddMinutes(40), day.AddHours(18).AddMinutes(25), day.AddHours(19).AddMinutes(10) };
        bool settled = false;
        string at = visit.ToString("h:mm");
        string reason = murder.Motive.Kind switch
        {
            TensionKind.Debt => "about the money",
            TensionKind.Jealousy => "about us. Just talk",
            TensionKind.Affair => "about what you said. Don't do anything stupid",
            TensionKind.Embezzlement => "before you go to anyone at work",
            _ => "about everything"
        };

        void CommitVisit(bool expected)
        {
            if (settled) return;
            settled = true;
            new MurderActivity(_world, this, murder, visit, expected).Commit();
        }

        for (int i = 0; i < attempts.Length; i++)
        {
            int index = i;
            _world.Scheduler.At(attempts[i], () =>
            {
                if (settled || murder.Status != CaseStatus.Planned) return;
                bool last = index == attempts.Length - 1;
                if (_world.Phones.PhoneAt(killer) == null || killer.HeldBy != null || killer.AddressId != killer.Person.HomeAddressId)
                {
                    if (last) CommitVisit(false);
                    return;
                }
                _world.Phones.Call(killer, State.PhoneOf(victim.Person), new CallPurpose
                {
                    ForPersonId = victim.Id,
                    OnAnswered = callee =>
                    {
                        CommitVisit(true);
                        return $"{killer.Person.FirstName}: We need to talk {reason}. I'm coming over tonight, around {at}. " +
                               $"{callee.Person.FirstName}: ...Fine. {at}.";
                    },
                    MachineMessage = $"It's {killer.Person.FirstName}. We need to talk {reason}. I'll come by tonight around {at}.",
                    OnMessageHeard = _ => CommitVisit(true),
                    OnFailed = _ =>
                    {
                        if (last) CommitVisit(false);
                    }
                });
                // A message was left: if nobody hears it in time, the killer comes anyway.
                if (last) _world.Scheduler.At(visit.AddMinutes(-30), () => CommitVisit(false));
            });
        }
    }

    /// <summary>The victim never came home: the killer comes back in the night and breaks in.</summary>
    public void FallBackToBreakIn(MurderCase murder)
    {
        var time = murder.PlannedFor.Date.AddDays(1).AddHours(2).AddMinutes(30);
        var killer = _world.Agents[murder.KillerId];
        if (!FreeForBreakIn(killer, time) || !AsleepAtHome(State.People[murder.VictimId], time))
        {
            murder.Status = CaseStatus.Aborted;
            Recast(murder);
            return;
        }
        murder.Approach = MurderApproach.BreakIn;
        murder.PlannedFor = time;
        _world.Scheduler.After(TimeSpan.FromMinutes(1), () =>
            new MurderActivity(_world, this, murder, time, victimExpectsVisit: false).Commit());
    }

    public void Recast(MurderCase failed)
    {
        failed.Status = CaseStatus.Aborted;
        var next = failed.PlannedFor.Date.AddDays(1);
        _world.Scheduler.At(next.AddHours(12), () => CastMurder(next, 1));
    }

    // ---- the killing ----

    public void LeaveCigarette(MurderCase murder, Agent killer, int locationId, int? subLocationId)
    {
        var butt = new Item
        {
            Id = State.GenerateEntityId(), ItemType = ItemType.CigaretteButt,
            Name = $"{killer.Person.CigaretteBrand} cigarette butt", LocationId = locationId, SubLocationId = subLocationId,
            OwnerPersonId = killer.Id
        };
        State.Items[butt.Id] = butt;
        _world.History.AddTouch(new TouchFact { PersonId = killer.Id, SurfaceId = butt.Id, Time = _world.Now });
    }

    public Item TakeWeapon(MurderCase murder, Agent killer, int locationId)
    {
        Item weapon;
        switch (murder.Weapon)
        {
            case WeaponKind.Handgun:
                weapon = GunOf(killer.Person);
                weapon.FixtureId = null;
                weapon.HeldByPersonId = killer.Id;
                break;
            case WeaponKind.Knife when murder.Approach != MurderApproach.BreakIn:
            {
                var block = State.AllFixturesIn(locationId).FirstOrDefault(f => f.Type == FixtureType.KnifeBlock);
                weapon = NewItem(ItemType.Knife, "Kitchen knife from a matching set", murder.VictimId);
                weapon.HeldByPersonId = killer.Id;
                if (block != null)
                {
                    _world.History.AddTouch(new TouchFact { PersonId = killer.Id, SurfaceId = block.Id, Time = _world.Now, Gloved = murder.Gloves });
                    weapon.Data["fromBlock"] = block.Id;
                }
                break;
            }
            case WeaponKind.Knife:
                weapon = NewItem(ItemType.Knife, "Hunting knife", murder.KillerId);
                weapon.HeldByPersonId = killer.Id;
                break;
            default:
                weapon = murder.Approach == MurderApproach.BreakIn
                    ? NewItem(ItemType.BluntObject, "Tire iron", murder.KillerId)
                    : NewItem(ItemType.BluntObject, "Heavy brass candlestick", murder.VictimId);
                weapon.HeldByPersonId = killer.Id;
                break;
        }
        weapon.Bloody = true;
        murder.WeaponItemId = weapon.Id;
        _world.History.ItemMoves.Add(new ItemMovedFact
        {
            ItemId = weapon.Id, ByPersonId = killer.Id, Time = _world.Now, ToDescription = "in the killer's hand"
        });
        return weapon;
    }

    private Item NewItem(ItemType type, string name, int ownerId)
    {
        var item = new Item { Id = State.GenerateEntityId(), ItemType = type, Name = name, OwnerPersonId = ownerId };
        State.Items[item.Id] = item;
        return item;
    }

    public void KillVictim(MurderCase murder, Agent victim, Agent killer, int locationId, int? subLocationId, Item weapon)
    {
        var now = _world.Now;
        victim.Person.IsAlive = false;
        _world.Executor.Stop(victim);
        victim.HeldBy = null;
        victim.ExternallyControlled = true;
        _world.Movement.Settle(victim, locationId, subLocationId, ActivityKind.Dead, "dead");
        murder.DeathTime = now;
        murder.SceneSubLocationId = subLocationId;
        murder.Status = CaseStatus.Committed;
        _world.History.Deaths.Add(new DeathFact
        {
            VictimId = victim.Id, KillerId = killer.Id, Time = now, AddressId = murder.SceneAddressId,
            LocationId = locationId, SubLocationId = subLocationId, Weapon = murder.Weapon, WeaponItemId = weapon.Id
        });
        if (murder.Weapon != WeaponKind.Handgun && Rng.Chance(0.7, _world.Seed, Purpose.Crime, murder.Id, 9))
        {
            var clothes = NewItem(ItemType.BloodyClothes, $"Blood-stained {(killer.Person.Appearance.Sex == Sex.Male ? "shirt and trousers" : "blouse and slacks")}", killer.Id);
            clothes.HeldByPersonId = killer.Id;
            clothes.Bloody = true;
            murder.BloodyClothesItemId = clothes.Id;
        }
        ScheduleDiscovery(murder);
    }

    public void CoverUp(MurderCase murder, Agent killer, int locationId, Random rng)
    {
        var now = _world.Now;
        var person = killer.Person;
        var home = State.Locations[locationId];
        var door = home.AccessPoints.FirstOrDefault(a => a.HasTag("main_entrance"));
        var weapon = State.Items[murder.WeaponItemId.Value];

        if (person.Has(Traits.Careful) || rng.NextDouble() < 0.2)
        {
            murder.WipedPrints = true;
            var surfaces = new List<int> { weapon.Id };
            if (door != null) surfaces.Add(door.Id);
            surfaces.AddRange(State.AllFixturesIn(locationId).Where(f => f.Type is FixtureType.Table or FixtureType.KnifeBlock).Select(f => f.Id));
            foreach (var s in surfaces)
                if (rng.NextDouble() < 0.8) _world.History.AddWipe(new WipeFact { PersonId = killer.Id, SurfaceId = s, Time = now });
        }

        // The weapon: dropped in panic, dumped on the way home, or hidden at home.
        bool leave = murder.Weapon != WeaponKind.Handgun && rng.NextDouble() < 0.3;
        if (leave)
        {
            weapon.HeldByPersonId = null;
            weapon.LocationId = locationId;
            weapon.SubLocationId = murder.SceneSubLocationId;
            murder.WeaponFate = WeaponFate.LeftAtScene;
            _world.History.ItemMoves.Add(new ItemMovedFact { ItemId = weapon.Id, ByPersonId = killer.Id, Time = now, ToDescription = "dropped at the scene" });
        }
        else if (rng.NextDouble() < 0.5)
        {
            murder.WeaponFate = WeaponFate.Dumped;
            InjectDump(murder, killer, weapon);
        }
        else
        {
            murder.WeaponFate = WeaponFate.KeptAtHome;
        }

        // Whatever the killer still carries goes into the closet and the hamper once home (see OnSettled).

        if (door != null)
        {
            if (!murder.WipedPrints) _world.History.AddTouch(new TouchFact { PersonId = killer.Id, SurfaceId = door.Id, Time = now, Gloved = murder.Gloves });
            door.IsLocked = false;
        }
        ScheduleAlibi(murder);
    }

    private void InjectDump(MurderCase murder, Agent killer, Item weapon)
    {
        var victim = State.People[murder.VictimId];
        var killerHome = State.Addresses[killer.Person.HomeAddressId];
        // A park trash can roughly between the scene and home.
        var scene = State.Addresses[murder.SceneAddressId];
        int midX = (scene.GridX + killerHome.GridX) / 2, midY = (scene.GridY + killerHome.GridY) / 2;
        var park = State.Addresses.Values.Where(a => a.Type == AddressType.Park)
            .OrderBy(a => Math.Abs(a.GridX - midX) + Math.Abs(a.GridY - midY)).ThenBy(a => a.Id).FirstOrDefault();
        if (park == null)
        {
            murder.WeaponFate = WeaponFate.KeptAtHome;
            return;
        }
        var can = State.GetLocationsForAddress(park.Id).SelectMany(l => State.GetFixturesForLocation(l.Id))
            .FirstOrDefault(f => f.Type == FixtureType.TrashCan);
        if (can == null)
        {
            murder.WeaponFate = WeaponFate.KeptAtHome;
            return;
        }
        var now = _world.Now;
        killer.Injected.Add(new Appointment
        {
            AddressId = park.Id, Spot = Spot.Room, LocationId = can.LocationId, Kind = ActivityKind.Crime,
            ArriveBy = now, LeaveAt = now.AddMinutes(4), Label = "getting rid of something",
            OnArrive = a =>
            {
                if (weapon.HeldByPersonId != a.Id) return;
                weapon.HeldByPersonId = null;
                weapon.FixtureId = can.Id;
                murder.WeaponFixtureId = can.Id;
                _world.History.AddTouch(new TouchFact { PersonId = a.Id, SurfaceId = can.Id, Time = _world.Now, Gloved = murder.Gloves });
                _world.History.ItemMoves.Add(new ItemMovedFact { ItemId = weapon.Id, ByPersonId = a.Id, Time = _world.Now, ToDescription = $"trash can at {State.PlaceName(park.Id)}" });
                if (murder.BloodyClothesItemId.HasValue && Rng.Chance(0.5, _world.Seed, Purpose.Crime, murder.Id, 11))
                {
                    var clothes = State.Items[murder.BloodyClothesItemId.Value];
                    clothes.HeldByPersonId = null;
                    clothes.FixtureId = can.Id;
                }
            }
        });
        _ = victim;
    }

    private void StashAtHome(MurderCase murder, Agent killer)
    {
        var fixtures = State.AllFixturesIn(killer.Person.HomeLocationId).ToList();
        var closet = fixtures.FirstOrDefault(f => f.Type == FixtureType.Closet);
        var hamper = fixtures.FirstOrDefault(f => f.Type == FixtureType.Hamper);
        foreach (var item in State.Items.Values.Where(i => i.HeldByPersonId == killer.Id
                     && (i.Id == murder.WeaponItemId || i.Id == murder.BloodyClothesItemId)).ToList())
        {
            var target = item.ItemType == ItemType.BloodyClothes ? hamper : item.ItemType == ItemType.Handgun
                ? fixtures.FirstOrDefault(f => f.Type == FixtureType.Nightstand) ?? closet : closet;
            if (target == null) continue;
            item.HeldByPersonId = null;
            item.FixtureId = target.Id;
            if (item.Id == murder.WeaponItemId)
            {
                murder.WeaponFixtureId = target.Id;
                murder.WeaponFate = WeaponFate.KeptAtHome;
            }
            _world.History.ItemMoves.Add(new ItemMovedFact { ItemId = item.Id, ByPersonId = killer.Id, Time = _world.Now, ToDescription = target.Name });
        }
    }

    // ---- afterwards ----

    /// <summary>Next morning the killer phones a friend to cover for them.</summary>
    private void ScheduleAlibi(MurderCase murder)
    {
        var killer = _world.Agents[murder.KillerId];
        var victimFriends = State.RelatedPeople(murder.VictimId, RelationshipType.Friend).ToHashSet();
        var friend = State.RelatedPeople(murder.KillerId, RelationshipType.Friend)
            .Select(id => State.People[id])
            .Where(p => p.IsAlive && p.Id != murder.VictimId && !victimFriends.Contains(p.Id) && p.HomePhoneId.HasValue
                        && !State.Tensions.Values.Any(t => t.Involves(p.Id) && t.Involves(murder.KillerId)))
            .OrderBy(p => Rng.Unit(_world.Seed, Purpose.Crime, murder.Id, p.Id)).FirstOrDefault();
        if (friend == null) return;
        murder.AccompliceId = friend.Id;
        var morning = _world.Now.Date.AddDays(_world.Now.Hour < 12 ? 0 : 1);
        foreach (var hour in new[] { 7.5, 8.25, 18.5, 19.5 })
        {
            _world.Scheduler.At(morning.AddHours(hour), () =>
            {
                if (murder.AlibiAgreed || murder.AccompliceId == null) return;
                if (_world.Phones.PhoneAt(killer) == null || killer.AddressId != killer.Person.HomeAddressId || killer.Asleep) return;
                var friendAgent = _world.Agents[friend.Id];
                var night = murder.DeathTime.Value.AddHours(-6).ToString("dddd");
                _world.Phones.Call(killer, State.PhoneOf(friend), new CallPurpose
                {
                    ForPersonId = friend.Id,
                    OnAnswered = callee =>
                    {
                        bool agrees = Rng.Chance(0.75, _world.Seed, Purpose.Crime, murder.Id, 13);
                        murder.AlibiAgreed = agrees;
                        if (!agrees) murder.AccompliceId = null;
                        return agrees
                            ? $"{killer.Person.FirstName}: I need a favor. If anybody asks, I was at your place {night} night, eight till midnight. Please. I'll explain later. {callee.Person.FirstName}: ...Okay. Eight till midnight."
                            : $"{killer.Person.FirstName}: If anybody asks, I was at your place {night} night. {callee.Person.FirstName}: What? No. I'm not lying for you. What did you do?";
                    },
                    MachineMessage = null
                });
                _ = friendAgent;
            });
        }
    }

    /// <summary>After the detective questions the killer, they phone the accomplice (wiretaps catch it).</summary>
    public void OnSuspectQuestioned(int personId)
    {
        foreach (var murder in Cases.Where(c => c.KillerId == personId && c.Status == CaseStatus.Reported))
        {
            var killer = _world.Agents[personId];
            var rng = Rng.For(_world.Seed, Purpose.Reaction, murder.Id, _world.Now.Ticks);
            if (murder.AlibiAgreed && murder.AccompliceId.HasValue)
            {
                var friend = State.People[murder.AccompliceId.Value];
                for (int i = 1; i <= 6; i++)
                    _world.Scheduler.After(TimeSpan.FromMinutes(40 * i + rng.Next(0, 20)), () =>
                    {
                        if (_world.Phones.PhoneAt(killer) == null || killer.AddressId != killer.Person.HomeAddressId || killer.Asleep) return;
                        if (_world.History.Calls.Any(c => c.CallerId == killer.Id && c.IntendedId == friend.Id && c.Time > _world.Now.AddHours(-6) && c.Transcript != null && c.Transcript.Contains("detective"))) return;
                        _world.Phones.Call(killer, State.PhoneOf(friend), new CallPurpose
                        {
                            ForPersonId = friend.Id,
                            OnAnswered = callee => $"{killer.Person.FirstName}: A private detective came around asking where I was. Stick to the story, eight till midnight at your place. {callee.Person.FirstName}: I know, I know. What did you get me into?",
                            MachineMessage = $"It's {killer.Person.FirstName}. Some detective's asking questions. Remember what we said. Call me."
                        });
                    });
            }
            // A weapon hidden at home starts to feel dangerous.
            if (murder.WeaponFate == WeaponFate.KeptAtHome && rng.NextDouble() < 0.5)
                _world.Scheduler.After(TimeSpan.FromHours(rng.Next(3, 10)), () => DumpLater(murder));
        }
    }

    private void DumpLater(MurderCase murder)
    {
        if (murder.WeaponFate != WeaponFate.KeptAtHome) return;
        var killer = _world.Agents[murder.KillerId];
        if (!killer.Person.IsAlive || killer.HeldBy != null) return;
        var weapon = State.Items[murder.WeaponItemId.Value];
        if (weapon.FixtureId == null || !State.AllFixturesIn(killer.Person.HomeLocationId).Any(f => f.Id == weapon.FixtureId)) return;
        if (killer.AddressId != killer.Person.HomeAddressId) return;
        weapon.FixtureId = null;
        weapon.HeldByPersonId = killer.Id;
        murder.WeaponFate = WeaponFate.Dumped;
        InjectDump(murder, killer, weapon);
        _world.Executor.Replan(killer);
    }

    private void ScheduleDiscovery(MurderCase murder)
    {
        var death = murder.DeathTime.Value;
        var start = (death.Hour < 5 ? death.Date : death.Date.AddDays(1)).AddHours(6).AddMinutes(45);
        if (start < murder.DeathTime.Value.AddHours(6)) start = murder.DeathTime.Value.AddHours(6);
        for (int i = 0; i < 40; i++)
        {
            var t = start.AddMinutes(20 * i);
            bool fallback = i >= 18;
            _world.Scheduler.At(t, () => TryDiscover(murder, fallback));
        }
    }

    private void TryDiscover(MurderCase murder, bool fallback)
    {
        if (murder.DiscovererId.HasValue) return;
        var scene = State.Addresses[murder.SceneAddressId];
        IEnumerable<Agent> candidates;
        if (!fallback)
        {
            candidates = _world.Agents.Values.Where(a =>
            {
                var p = a.Person;
                if (!p.IsAlive || p.IsPlayer || p.Id == murder.KillerId || a.HeldBy != null || a.Trip != null || a.Asleep) return false;
                if (a.AddressId != p.HomeAddressId) return false;
                if (p.HomeLocationId == murder.SceneLocationId) return false;
                var home = State.Addresses[p.HomeAddressId];
                return home.Id == scene.Id || Math.Abs(home.GridX - scene.GridX) + Math.Abs(home.GridY - scene.GridY) <= 2;
            });
        }
        else
        {
            // Nobody nearby noticed: a worried friend comes round.
            var friends = State.RelatedPeople(murder.VictimId, RelationshipType.Friend)
                .Concat(State.RelatedPeople(murder.VictimId, RelationshipType.Dating)).ToHashSet();
            candidates = _world.Agents.Values.Where(a => friends.Contains(a.Id) && a.Person.IsAlive && a.Id != murder.KillerId
                && a.HeldBy == null && a.Trip == null && !a.Asleep && a.AddressId == a.Person.HomeAddressId);
        }
        var pick = candidates.OrderBy(a => Rng.Unit(_world.Seed, Purpose.Discovery, murder.Id, a.Id)).FirstOrDefault();
        if (pick == null) return;
        murder.DiscovererId = pick.Id;
        new DiscoveryActivity(_world, this, murder, pick.Id).Commit();
    }

    public void ReportToPolice(MurderCase murder, Agent caller)
    {
        var dispatch = EnsureDispatch();
        var phone = _world.Phones.PhoneAt(caller);
        var victim = State.People[murder.VictimId];
        string where = State.Addresses[murder.SceneAddressId].Type == AddressType.ApartmentBuilding
            ? $"{State.AddressLine(murder.SceneAddressId)}, {State.Locations[murder.SceneLocationId].Name}"
            : State.AddressLine(murder.SceneAddressId);
        _world.History.AddCall(new CallFact
        {
            Id = _world.NextFactId(), CallerId = caller.Id, FromPhoneId = phone?.Id ?? dispatch.Id, ToPhoneId = dispatch.Id,
            Time = _world.Now, DurationSeconds = 150, Outcome = CallOutcome.Emergency, IntendedId = caller.Id,
            Transcript = $"{caller.Person.FirstName}: I need the police. It's my neighbor, {victim.FullName}, at {where}. " +
                         $"The door was open... there's blood everywhere. I think {victim.HeShe}'s dead."
        });
        murder.ReportedAt = _world.Now;
        murder.Status = CaseStatus.Reported;
        // The coroner takes the body away a few hours later.
        _world.Scheduler.After(TimeSpan.FromHours(5), () =>
        {
            var body = _world.Agents[murder.VictimId];
            if (body.OpenPresence != null) _world.History.ClosePresence(body.OpenPresence, _world.Now);
            body.OpenPresence = null;
            _world.SetLocation(body, null);
            body.AddressId = null;
            murder.BodyRemovedAt = _world.Now;
        });
        Reported?.Invoke(murder);
    }
}

