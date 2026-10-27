using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Core;
using Stakeout.Simulation.Crime;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;
using Stakeout.Simulation.History;
using Stakeout.Simulation.Observers;

namespace Stakeout.Investigation;

/// <summary>Something in a room the detective can dust for prints.</summary>
public record Surface(int Id, string Name);

public partial class Detective
{
    private readonly HashSet<string> _seenOnce = new();

    private bool Once(string key) => _seenOnce.Add(key);

    public bool BodyHere => Case != null && LocationId == Case.SceneLocationId && Case.Truth.BodyRemovedAt == null
                            && Case.Truth.DeathTime.HasValue;

    /// <summary>What the detective sees in the current room.</summary>
    public List<string> LookAround()
    {
        var lines = new List<string>();
        if (!LocationId.HasValue) return lines;
        var loc = State.Locations[LocationId.Value];
        lines.Add($"{State.PlaceName(loc.AddressId)} - {loc.Name}");
        foreach (var id in PeopleHere()) lines.Add($"{NameOrLook(id)} is here{ActivityText(id)}.");
        if (BodyHere)
        {
            var victim = State.People[Case.VictimId];
            string room = Case.Truth.SceneSubLocationId.HasValue ? State.SubLocations[Case.Truth.SceneSubLocationId.Value].Name.ToLowerInvariant() : "room";
            lines.Add($"The body of {victim.FullName} lies in the {room}. A patrolman stands by the door.");
        }
        else if (Case != null && LocationId == Case.SceneLocationId)
            lines.Add("Chalk marks where the body lay. The coroner has taken it away.");
        foreach (var item in ItemsLyingHere())
            lines.Add($"On the floor: {item.Name}{(item.Bloody ? ", smeared with blood" : "")}.");
        Spend(TimeSpan.FromMinutes(2));
        return lines;
    }

    private string ActivityText(int personId)
    {
        var a = World.Agents[personId];
        return a.Activity switch
        {
            ActivityKind.Work => ", working",
            ActivityKind.Dine => ", eating",
            ActivityKind.Drink => ", drinking",
            ActivityKind.Sleep => ", asleep",
            ActivityKind.Date => ", on a date",
            _ => ""
        };
    }

    public IEnumerable<Item> ItemsLyingHere() =>
        LocationId.HasValue
            ? State.Items.Values.Where(i => i.LocationId == LocationId && i.FixtureId == null && i.HeldByPersonId == null)
            : Enumerable.Empty<Item>();

    public IEnumerable<Fixture> FixturesHere() =>
        LocationId.HasValue ? State.AllFixturesIn(LocationId.Value).Where(f => f.Type != FixtureType.Camera || f.HasTag("indoor_camera")) : Enumerable.Empty<Fixture>();

    public string FixtureLabel(Fixture f) =>
        f.SubLocationId.HasValue ? $"{State.SubLocations[f.SubLocationId.Value].Name}: {f.Name}" : f.Name;

    public List<Surface> SurfacesHere()
    {
        var list = new List<Surface>();
        if (!LocationId.HasValue) return list;
        var loc = State.Locations[LocationId.Value];
        foreach (var ap in loc.AccessPoints) list.Add(new Surface(ap.Id, $"{ap.Name} handle"));
        foreach (var f in FixturesHere().Where(f => f.Type is not (FixtureType.Camera)))
            list.Add(new Surface(f.Id, FixtureLabel(f)));
        foreach (var i in ItemsLyingHere()) list.Add(new Surface(i.Id, i.Name));
        foreach (var i in State.Items.Values.Where(i => i.HeldByPersonId == Agent.Id)) list.Add(new Surface(i.Id, $"{i.Name} (bagged)"));
        return list;
    }

    // ---- the body ----

    public Clue ExamineBody()
    {
        if (!BodyHere)
        {
            Say("There is no body here.");
            return null;
        }
        var murder = Case.Truth;
        var rng = Rng.For(World.Seed, Purpose.Scene, murder.Id, 1);
        var death = murder.DeathTime.Value;
        var center = death.AddMinutes(rng.Next(-50, 51));
        var from = Round(center.AddMinutes(-90));
        var to = Round(center.AddMinutes(105));
        string cause = murder.Weapon switch
        {
            WeaponKind.Knife => "Several stab wounds to the chest and abdomen. A single-edged blade, six or seven inches.",
            WeaponKind.BluntObject => "Blunt force trauma to the back of the head, more than one blow. Something heavy.",
            _ => "A single gunshot wound to the chest at close range. The bullet is still inside."
        };
        string struggle = murder.Approach == MurderApproach.BreakIn
            ? "No defensive wounds: the victim was in bed, likely asleep."
            : "Bruising on the forearms - defensive wounds. The victim saw it coming.";
        var clue = AddClue(new Clue
        {
            Id = NextClueId(), Kind = ClueKind.Body, Title = "The body", FoundAt = Now, Source = "Crime scene",
            Text = $"{cause} {struggle} Body temperature and rigor put the time of death between {from:dddd} {Describe.Clock(from)} and {(to.Date == from.Date ? "" : to.ToString("dddd") + " ")}{Describe.Clock(to)}.",
            TimeFrom = from, TimeTo = to, AboutPersonId = murder.VictimId
        });
        Spend(TimeSpan.FromMinutes(12));
        return clue;
    }

    // ---- searching ----

    public List<Clue> Search(int fixtureId)
    {
        var fixture = State.Fixtures[fixtureId];
        var found = new List<Clue>();
        string home = OwnerOfHere() is int owner ? $"{State.People[owner].FullName}'s home" : State.PlaceName(AddressId.Value);
        var items = State.Items.Values.Where(i => i.FixtureId == fixtureId).OrderBy(i => i.Id).ToList();

        if (fixture.Type == FixtureType.KnifeBlock)
        {
            var taken = State.Items.Values.FirstOrDefault(i => i.Data.TryGetValue("fromBlock", out var b) && (int)b == fixtureId);
            string text = taken != null ? "One slot in the knife block is empty. The other knives match a set." : "A full set of kitchen knives.";
            if (taken != null && Once($"block{fixtureId}"))
                found.Add(AddClue(new Clue
                {
                    Id = NextClueId(), Kind = ClueKind.Scene, Title = "Knife block", Text = text, FoundAt = Now, Source = home,
                    AtScene = Case != null && LocationId == Case.SceneLocationId
                }));
            Say(text);
        }
        else if (fixture.Type == FixtureType.AnsweringMachine)
        {
            found.AddRange(PlayTape(fixture));
        }
        else if (items.Count == 0)
        {
            Say($"{fixture.Name}: nothing of interest.");
        }

        foreach (var item in items)
        {
            if (!Once($"item{item.Id}")) continue;
            found.Add(AddClue(DescribeFind(item, home)));
        }
        Spend(TimeSpan.FromMinutes(6));
        return found;
    }

    /// <summary>The person whose home the detective is in, if any.</summary>
    private int? OwnerOfHere()
    {
        if (!LocationId.HasValue) return null;
        var loc = State.Locations[LocationId.Value];
        if (!loc.HasTag("home")) return null;
        var resident = State.People.Values.FirstOrDefault(p => p.HomeLocationId == loc.Id);
        return resident?.Id;
    }

    private Clue DescribeFind(Item item, string where)
    {
        int? owner = OwnerOfHere();
        var clue = new Clue
        {
            Id = NextClueId(), FoundAt = Now, Source = where, ItemId = item.Id, Bloody = item.Bloody,
            FoundAtHomeOfPersonId = owner, Kind = ClueKind.Item, Title = item.Name,
            AtScene = Case != null && LocationId == Case.SceneLocationId
        };
        switch (item.ItemType)
        {
            case ItemType.AddressBook:
                clue = new Clue
                {
                    Id = clue.Id, Kind = ClueKind.Document, Title = item.Name, FoundAt = Now, Source = where, ItemId = item.Id,
                    Text = "Entries: " + string.Join("; ", item.Text), FoundAtHomeOfPersonId = owner
                };
                foreach (var line in item.Text) LearnFromAddressBook(line);
                foreach (var rel in State.RelationshipsOf(item.OwnerPersonId ?? 0))
                    clue.NamedPersonIds.Add(rel.Other(item.OwnerPersonId.Value));
                break;
            case ItemType.Letter:
            case ItemType.Ledger:
            {
                var tension = item.Data.TryGetValue("tension", out var t) ? State.Tensions[(int)t] : null;
                clue = new Clue
                {
                    Id = clue.Id, Kind = ClueKind.Document, Title = item.Name, FoundAt = Now, Source = where, ItemId = item.Id,
                    Text = string.Join(" / ", item.Text), FoundAtHomeOfPersonId = owner,
                    MotiveKind = tension?.Kind, MotivePersonId = tension?.AggressorId
                };
                // Signed in full, the author is named; an initial alone only hints.
                if (tension != null && item.Text.Any(x => x.Contains(State.People[tension.AggressorId].FullName)))
                    clue.NamedPersonIds.Add(tension.AggressorId);
                break;
            }
            case ItemType.Handgun:
                clue.Text = item.Bloody ? $"{item.Name}. Recently fired; there is blood on the grip." : $"{item.Name}, loaded.";
                break;
            case ItemType.BloodyClothes:
                clue.Text = $"{item.Name}, stuffed at the bottom. Dark stains, still stiff.";
                break;
            case ItemType.Knife:
            case ItemType.BluntObject:
                clue.Text = item.Bloody ? $"{item.Name}, with dried blood on it." : item.Name;
                break;
            case ItemType.CigaretteButt:
                clue.Text = $"A {item.Name}.";
                break;
            default:
                clue.Text = item.Name;
                break;
        }
        return clue;
    }

    private void LearnFromAddressBook(string line)
    {
        // "Name - number - address"
        var parts = line.Split(" - ");
        if (parts.Length < 2) return;
        var person = State.People.Values.FirstOrDefault(p => p.FullName == parts[0]);
        if (person == null) return;
        var known = Learn(person.Id);
        known.PhoneKnown = true;
        known.AddressKnown = true;
        KnownAddresses.Add(person.HomeAddressId);
    }

    public List<Clue> PlayTape(Fixture machine)
    {
        var phone = State.AllFixturesIn(World.Phones.PhoneLocation(machine)).FirstOrDefault(f => f.Type == FixtureType.Telephone);
        var found = new List<Clue>();
        if (phone == null) return found;
        var tape = World.Phones.TapeAt(phone, Now).ToList();
        if (tape.Count == 0)
        {
            Say("The tape is blank.");
            return found;
        }
        foreach (var call in tape)
        {
            if (!Once($"tape{call.Id}")) continue;
            var clue = AddClue(new Clue
            {
                Id = NextClueId(), Kind = ClueKind.Tape, Title = $"Message, {call.Time:ddd h:mm tt}",
                Text = $"(beep) {call.Transcript}", FoundAt = Now, Source = "Answering machine tape",
                CallerId = call.CallerId, CallTime = call.Time, TimeFrom = call.Time, TimeTo = call.Time,
                CallerNumber = State.Fixtures[call.FromPhoneId].PhoneNumber
            });
            found.Add(clue);
            Say($"Tape: {call.Transcript}");
        }
        return found;
    }

    // ---- prints ----

    private readonly Dictionary<int, string> _printSets = new();

    /// <summary>Prints of one (unknown) person carry the same letter wherever they turn up.</summary>
    private string PrintSet(int personId)
    {
        if (!_printSets.TryGetValue(personId, out var label))
        {
            int n = _printSets.Count;
            label = n < 26 ? ((char)('A' + n)).ToString() : $"A{n - 25}";
            _printSets[personId] = label;
        }
        return label;
    }

    public List<Clue> DustForPrints(int surfaceId)
    {
        var found = new List<Clue>();
        var surface = SurfacesHere().FirstOrDefault(s => s.Id == surfaceId);
        if (surface == null) return found;
        var prints = Prints.Lift(surfaceId, Now);
        bool atScene = Case != null && LocationId == Case.SceneLocationId;
        bool weapon = Case != null && Case.Truth.WeaponItemId == surfaceId;
        Spend(TimeSpan.FromMinutes(10));
        if (prints.Count == 0)
        {
            Say($"{surface.Name}: nothing usable. Wiped, or never touched.");
            return found;
        }
        // In someone's own home, the prints all over it are presumably theirs.
        int? owner = OwnerOfHere();
        bool singleResident = owner.HasValue && !atScene && State.Households[State.People[owner.Value].HouseholdId].MemberIds.Count == 1;
        var notes = new List<string>();
        foreach (var print in prints)
        {
            string set = PrintSet(print.PersonId);
            bool victim = Case != null && print.PersonId == Case.VictimId;
            if (victim)
            {
                notes.Add($"set {set} (the victim's own)");
                continue;
            }
            if (singleResident && print.PersonId == owner) Learn(owner.Value).ReferencePrints = true;
            notes.Add($"set {set}");
            if (!Once($"print{surfaceId}:{print.PersonId}")) continue;
            var clue = AddClue(new Clue
            {
                Id = NextClueId(), Kind = ClueKind.Print, Title = $"Print set {set} on {surface.Name}",
                Text = $"{(print.Partial ? "A partial print" : "A clear print")} on the {surface.Name} ({State.PlaceName(AddressId.Value)}). Set {set}.",
                FoundAt = Now, Source = State.PlaceName(AddressId.Value), PrintLabel = set, SurfaceId = surfaceId,
                SurfaceName = surface.Name, PrintPersonId = print.PersonId, AtScene = atScene, OnWeapon = weapon,
                FoundAtHomeOfPersonId = owner
            });
            found.Add(clue);
        }
        Say($"{surface.Name}: {string.Join(", ", notes)}.");
        foreach (var known in People.Values.Where(k => k.ReferencePrints).ToList())
            found.AddRange(MatchPrintsFor(known.PersonId));
        return found;
    }

    /// <summary>Compares every lifted print with a person's known prints (card, police file, or their own home).</summary>
    internal List<Clue> MatchPrintsFor(int personId)
    {
        var matches = new List<Clue>();
        foreach (var print in Clues.Where(c => c.Kind == ClueKind.Print && c.PrintPersonId == personId && c.MatchedPersonId == null).ToList())
        {
            print.MatchedPersonId = personId;
            var person = State.People[personId];
            print.Text += $" Matches {person.FullName}.";
            if (!print.AtScene && !print.OnWeapon) continue;
            var clue = new Clue
            {
                Id = NextClueId(), Kind = ClueKind.PrintMatch, Title = $"{person.FullName}'s print on the {print.SurfaceName}",
                Text = $"Print set {print.PrintLabel} on the {print.SurfaceName}{(print.AtScene ? " at the scene" : "")} matches {person.FullName}.",
                FoundAt = Now, Source = "Fingerprint comparison", AboutPersonId = personId, PrintLabel = print.PrintLabel,
                SurfaceId = print.SurfaceId, SurfaceName = print.SurfaceName, PrintPersonId = personId,
                AtScene = print.AtScene, OnWeapon = print.OnWeapon, MatchedPersonId = personId
            };
            clue.NamedPersonIds.Add(personId);
            matches.Add(AddClue(clue));
            Say(clue.Text);
        }
        return matches;
    }

    // ---- evidence ----

    public Clue Collect(int itemId)
    {
        var item = State.Items[itemId];
        bool reachable = item.LocationId == LocationId && item.FixtureId == null
                         || item.FixtureId.HasValue && FixturesHere().Any(f => f.Id == item.FixtureId);
        if (!reachable || item.HeldByPersonId.HasValue) return null;
        item.FixtureId = null;
        item.LocationId = null;
        item.SubLocationId = null;
        item.HeldByPersonId = Agent.Id;
        World.History.ItemMoves.Add(new ItemMovedFact { ItemId = itemId, ByPersonId = Agent.Id, Time = Now, ToDescription = "evidence bag" });
        Say($"You bag the {item.Name}.");
        Spend(TimeSpan.FromMinutes(2));
        return Clues.FirstOrDefault(c => c.ItemId == itemId);
    }

    public IEnumerable<Item> Bagged() => State.Items.Values.Where(i => i.HeldByPersonId == Agent.Id);

    public Clue ExamineDoor(int accessPointId)
    {
        var ap = State.GetAccessPoint(accessPointId);
        if (ap == null) return null;
        var forced = World.History.ForcedEntries.Where(f => f.AccessPointId == accessPointId && f.PersonId != Agent.Id && f.Time <= Now).ToList();
        string text;
        if (forced.Count > 0)
            text = forced[^1].Picked
                ? $"{ap.Name}: fresh scratches around the keyhole. Someone picked this lock."
                : ap.Type == AccessPointType.Window ? $"{ap.Name}: the pane is broken from outside; glass on the floor inside."
                : $"{ap.Name}: pry marks on the frame. Forced open.";
        else
            text = $"{ap.Name}: no sign of forced entry.";
        bool scene = Case != null && LocationId == Case.SceneLocationId;
        if (scene && ap.HasTag("main_entrance")) text += " The neighbor says it was standing open in the morning.";
        Spend(TimeSpan.FromMinutes(3));
        if (!Once($"door{accessPointId}")) return null;
        return AddClue(new Clue
        {
            Id = NextClueId(), Kind = ClueKind.Scene, Title = ap.Name, Text = text, FoundAt = Now,
            Source = State.PlaceName(AddressId.Value), AtScene = scene
        });
    }
}
