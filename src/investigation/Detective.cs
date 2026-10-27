using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Agents;
using Stakeout.Simulation.Crime;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.History;
using Stakeout.Simulation.Observers;

namespace Stakeout.Investigation;

/// <summary>What the player knows about a person.</summary>
public class KnownPerson
{
    public int PersonId { get; init; }
    public bool Met { get; set; }
    public bool AddressKnown { get; set; }
    public bool PhoneKnown { get; set; }
    public bool EmployerKnown { get; set; }
    public bool VehicleKnown { get; set; }
    public bool ReferencePrints { get; set; }
    public List<string> Notes { get; } = new();
}

/// <summary>The player's view of the case they are working.</summary>
public class CaseView
{
    public MurderCase Truth { get; init; }
    public int VictimId => Truth.VictimId;
    public int SceneAddressId => Truth.SceneAddressId;
    public int SceneLocationId => Truth.SceneLocationId;
    public DateTime ReportedAt { get; init; }
    /// <summary>The night the case is about, as the player would put it: from the evening before the report.</summary>
    public DateTime NightStart { get; init; }
    public DateTime NightEnd => ReportedAt;
    public bool Closed { get; set; }
    public Verdict Verdict { get; set; }
    public HashSet<int> QuestionedSuspects { get; } = new();
}

public enum Access { Open, Refused, Locked }

/// <summary>
/// The detective: a person in the world plus everything the player has learned. Every action happens at the
/// world's current time and then lets time pass, so the world keeps moving while the player works. Results come
/// only from observers over History, never from the crime record directly.
/// </summary>
public partial class Detective
{
    public World World { get; }
    public Agent Agent { get; }
    public Person Me => Agent.Person;
    public Fingerprints Prints { get; }
    public WitnessMemory Memory { get; }
    public Cameras Cameras { get; }

    public List<Clue> Clues { get; } = new();
    public Dictionary<int, KnownPerson> People { get; } = new();
    public HashSet<int> KnownAddresses { get; } = new();
    public CaseView Case { get; private set; }
    public int Cash { get; set; } = 400;
    public int Wiretaps { get; set; } = 1;
    public List<string> Log { get; } = new();

    /// <summary>Messages for the player: alerts, results that arrive later, wiretap transcripts.</summary>
    public event Action<string> Message;

    private int _nextClueId = 1;
    private int? _doorLocationId;
    private readonly HashSet<int> _doorAnswerers = new();
    private readonly HashSet<int> _unlocked = new();

    public Detective(World world, Agent player)
    {
        World = world;
        Agent = player;
        Prints = new Fingerprints(world);
        Memory = new WitnessMemory(world);
        Cameras = new Cameras(world);
        KnownAddresses.Add(Me.HomeAddressId);
        world.Crimes.Reported += OnReported;
        foreach (var reported in world.Crimes.Cases.Where(c => c.Status == CaseStatus.Reported))
            OnReported(reported);
    }

    public DateTime Now => World.Now;
    public bool Travelling => Agent.Trip != null;
    public int? AddressId => Agent.AddressId;
    public int? LocationId => Agent.LocationId;
    public SimulationState State => World.State;

    internal void Say(string text)
    {
        Log.Add($"{Now:ddd HH:mm}  {text}");
        Message?.Invoke(text);
    }

    /// <summary>Lets game time pass while the detective does something.</summary>
    public void Spend(TimeSpan duration)
    {
        if (duration > TimeSpan.Zero) World.AdvanceTo(Now + duration);
    }

    public Clue AddClue(Clue clue)
    {
        Clues.Add(clue);
        foreach (var id in clue.NamedPersonIds) Learn(id);
        foreach (var a in clue.NamedAddressIds) KnownAddresses.Add(a);
        return clue;
    }

    internal int NextClueId() => _nextClueId++;

    public KnownPerson Learn(int personId)
    {
        if (!People.TryGetValue(personId, out var known))
        {
            known = new KnownPerson { PersonId = personId };
            People[personId] = known;
        }
        return known;
    }

    public bool Knows(int personId) => People.ContainsKey(personId);

    /// <summary>How the player sees someone standing in front of them: by name once met, otherwise by looks.</summary>
    public string NameOrLook(int personId) =>
        People.TryGetValue(personId, out var k) && k.Met ? State.People[personId].FullName : Looks(personId);

    public string Looks(int personId)
    {
        var a = State.People[personId].Appearance;
        var d = new PersonDescription
        {
            Sex = a.Sex, AgeDecade = a.Age / 10 * 10, Height = a.Height, Build = a.Build, Hair = a.Hair,
            Glasses = a.Glasses, Mustache = a.Mustache, Beard = a.Beard
        };
        return d.Text;
    }

    // ---- the case ----

    private void OnReported(MurderCase murder)
    {
        if (Case != null && !Case.Closed) return;
        var reported = murder.ReportedAt ?? Now;
        var nightStart = (reported.Hour < 17 ? reported.Date.AddDays(-1) : reported.Date).AddHours(17);
        Case = new CaseView { Truth = murder, ReportedAt = reported, NightStart = nightStart };
        var victim = State.People[murder.VictimId];
        var known = Learn(victim.Id);
        known.AddressKnown = true;
        known.ReferencePrints = true; // the coroner fingerprints the body
        known.Notes.Add("The victim.");
        KnownAddresses.Add(murder.SceneAddressId);
        string where = State.Addresses[murder.SceneAddressId].Type == AddressType.ApartmentBuilding
            ? $"{State.AddressLine(murder.SceneAddressId)}, {State.Locations[murder.SceneLocationId].Name}"
            : State.AddressLine(murder.SceneAddressId);
        var clue = AddClue(new Clue
        {
            Id = NextClueId(), Kind = ClueKind.Scene, Title = "Police scanner", FoundAt = reported, Source = "Police radio",
            Text = $"{reported:HH:mm} - Homicide reported at {where}. Victim identified as {victim.FullName}, " +
                   $"{victim.Appearance.Age}, who lived there alone. Found by a neighbor. Lt. Hayes is on scene.",
            AboutPersonId = victim.Id, NamedPersonIds = { victim.Id }, NamedAddressIds = { murder.SceneAddressId }
        });
        Say($"SCANNER: Homicide at {where}. Victim: {victim.FullName}.");
        _ = clue;
    }

    // ---- getting around ----

    public Location ArrivalLocation(int addressId) => World.Movement.ArrivalLocation(addressId);

    /// <summary>Starts a trip to an address, by car if it is parked here. Returns the trip; time passes if <paramref name="wait"/>.</summary>
    public TripFact TravelTo(int addressId, bool wait = true)
    {
        if (Travelling) throw new InvalidOperationException("Already on the way somewhere.");
        if (AddressId == addressId)
        {
            Leave();
            return null;
        }
        EndConversation();
        StopStakeout();
        _unlocked.Clear();
        KnownAddresses.Add(addressId);
        var vehicle = World.Movement.AvailableVehicle(Agent);
        var trip = World.Movement.StartTrip(new[] { Agent }, vehicle, addressId, _ =>
        {
            World.Movement.Settle(Agent, ArrivalLocation(addressId).Id, null, ActivityKind.Investigate, "looking around");
        });
        if (wait) World.AdvanceTo(trip.Arrive);
        return trip;
    }

    public IReadOnlyList<Location> RoomsHere() =>
        AddressId.HasValue ? State.GetLocationsForAddress(AddressId.Value) : new List<Location>();

    public Access CanEnter(int locationId, out string reason)
    {
        reason = "";
        if (!AddressId.HasValue || Travelling) { reason = "You are on the road."; return Access.Refused; }
        var loc = State.Locations[locationId];
        if (loc.AddressId != AddressId) { reason = "That is somewhere else."; return Access.Refused; }
        if (LocationId == locationId) return Access.Open;
        if (Case != null && locationId == Case.SceneLocationId && !Case.Closed)
        {
            reason = "The officer at the door lifts the tape for you.";
            return Access.Open;
        }
        if (loc.Id == Me.HomeLocationId || loc.HasTag("exterior") || _unlocked.Contains(locationId)) return Access.Open;
        var business = State.BusinessAt(loc.AddressId);
        bool open = business != null && Planner.IsOpen(business, Now);
        bool officeHours = business == null && loc.Name == "Lobby" && Now.Hour >= 7 && Now.Hour < 22;
        if (loc.HasTag("publicly_accessible") && (open || officeHours || business == null && !loc.HasTag("home")))
            return Access.Open;
        if (loc.HasTag("home"))
        {
            var inside = World.AgentsIn(loc.Id).Where(a => a.Id != Agent.Id && a.Person.IsAlive).ToList();
            if (inside.Count > 0)
            {
                reason = "Someone is inside. You could knock.";
                return Access.Refused;
            }
            var door = loc.AccessPoints.FirstOrDefault(a => a.HasTag("main_entrance"));
            if (door != null && (!door.IsLocked || door.IsBroken)) return Access.Open;
            reason = "The door is locked.";
            return Access.Locked;
        }
        var staff = World.AgentsIn(loc.Id).Where(a => a.Id != Agent.Id && a.Person.IsAlive).ToList();
        if (open && staff.Count > 0)
        {
            reason = "Staff only. Someone tells you to stay out.";
            return Access.Refused;
        }
        if (open || officeHours)
        {
            reason = "Staff only. Not while the place is open.";
            return Access.Refused;
        }
        reason = "Closed and locked.";
        return Access.Locked;
    }

    public bool Enter(int locationId)
    {
        var access = CanEnter(locationId, out var reason);
        if (access != Access.Open)
        {
            Say(reason);
            return false;
        }
        EndConversation();
        if (!string.IsNullOrEmpty(reason)) Say(reason);
        World.Movement.Settle(Agent, locationId, null, ActivityKind.Investigate, "investigating");
        Spend(TimeSpan.FromMinutes(1));
        return true;
    }

    /// <summary>Picks the lock of a location. The UI runs the minigame and passes the outcome.</summary>
    public bool PickLock(int locationId, bool success)
    {
        var loc = State.Locations[locationId];
        var door = loc.AccessPoints.FirstOrDefault(a => a.HasTag("main_entrance")) ?? loc.AccessPoints.FirstOrDefault();
        if (!success)
        {
            World.History.Noises.Add(new NoiseFact
            {
                AddressId = loc.AddressId, LocationId = locationId, Time = Now, Kind = NoiseKind.Crash, SourcePersonId = Agent.Id
            });
            Spend(TimeSpan.FromMinutes(3));
            Say("The pick slips. That was loud.");
            return false;
        }
        if (door != null)
            World.History.ForcedEntries.Add(new ForcedEntryFact
            {
                PersonId = Agent.Id, AccessPointId = door.Id, LocationId = locationId, Time = Now, Picked = true
            });
        Spend(TimeSpan.FromMinutes(5));
        _unlocked.Add(locationId);
        Say("The lock gives.");
        return Enter(locationId);
    }

    public void Leave()
    {
        if (!AddressId.HasValue) return;
        EndConversation();
        World.Movement.Settle(Agent, ArrivalLocation(AddressId.Value).Id, null, ActivityKind.Investigate, "outside");
        Spend(TimeSpan.FromMinutes(1));
    }

    // ---- talking ----

    /// <summary>Knocks on a home's door. Returns the people who answer.</summary>
    public List<int> Knock(int locationId)
    {
        EndConversation();
        var inside = World.AgentsIn(locationId).Where(a => a.Person.IsAlive && a.Id != Agent.Id).ToList();
        var answer = inside.Where(a => !a.Asleep).Select(a => a.Id).ToList();
        if (answer.Count == 0 && inside.Count > 0 && Simulation.Core.Rng.Chance(0.5, World.Seed, Simulation.Core.Purpose.Player, locationId, Now.Ticks))
            answer.Add(inside[0].Id);
        Spend(TimeSpan.FromMinutes(1));
        if (answer.Count == 0)
        {
            Say("Nobody answers.");
            return answer;
        }
        _doorLocationId = locationId;
        foreach (var id in answer) _doorAnswerers.Add(id);
        Say($"{string.Join(" and ", answer.Select(NameOrLook))} {(answer.Count == 1 ? "comes" : "come")} to the door.");
        return answer;
    }

    private void EndConversation()
    {
        _doorLocationId = null;
        _doorAnswerers.Clear();
    }

    /// <summary>People the detective can talk to right now.</summary>
    public List<int> PeopleHere()
    {
        var result = new List<int>();
        if (LocationId.HasValue && !Travelling)
            result.AddRange(World.AgentsIn(LocationId.Value).Where(a => a.Id != Agent.Id && a.Person.IsAlive).Select(a => a.Id));
        if (_doorLocationId.HasValue)
            result.AddRange(_doorAnswerers.Where(id => World.Agents[id].LocationId == _doorLocationId && !result.Contains(id)));
        return result;
    }

    public bool CanTalkTo(int personId) => PeopleHere().Contains(personId) && State.People[personId].IsAlive;
}
