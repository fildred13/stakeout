using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Stakeout.Investigation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;
using Stakeout.UI;

/// <summary>The left panel: what the detective can do where they are.</summary>
public partial class GameShell
{
    private void RebuildHere()
    {
        if (_here == null) return;
        Ui.Clear(_here);
        var state = D.State;

        if (D.Travelling)
        {
            var trip = D.Agent.Trip;
            _here.AddChild(Ui.Heading("On the road"));
            _here.AddChild(Ui.Label($"To {state.PlaceName(trip.ToAddressId)}", 16, Ui.Text, wrap: true));
            _here.AddChild(Ui.Label($"Arriving around {trip.Arrive:h:mm tt}.", 16, Ui.Dim));
            return;
        }
        if (!D.AddressId.HasValue) return;

        if (D.OnStakeout)
        {
            BuildStakeout();
            return;
        }

        var address = state.Addresses[D.AddressId.Value];
        var business = state.BusinessAt(address.Id);
        _here.AddChild(Ui.Heading(business?.Name ?? state.AddressLine(address.Id)));
        string kind = address.Type switch
        {
            AddressType.ApartmentBuilding => "Apartment building",
            AddressType.SuburbanHome => "House",
            AddressType.DiveBar => "Bar",
            _ => address.Type.ToString()
        };
        string sub = business != null ? $"{kind}, {state.AddressLine(address.Id)}" : kind;
        if (business != null) sub += Stakeout.Simulation.Agents.Planner.IsOpen(business, Session.World.Now) ? " - open" : " - closed";
        if (address.Id == D.Me.HomeAddressId) sub += " - your office";
        if (D.Case != null && address.Id == D.Case.SceneAddressId) sub += " - CRIME SCENE";
        _here.AddChild(Ui.Label(sub, 16, Ui.Dim, wrap: true));

        var loc = state.Locations[D.LocationId.Value];
        _here.AddChild(Ui.Spacer(4));
        _here.AddChild(Ui.Label($"You are in: {loc.Name}", 16, Ui.Amber));

        BuildPeople();
        BuildRoomActions(loc);
        BuildElsewhere(address, loc);
    }

    private void Section(string title)
    {
        _here.AddChild(Ui.Spacer(6));
        _here.AddChild(Ui.Label(title, 16, Ui.Dim));
    }

    private void BuildPeople()
    {
        var people = D.PeopleHere();
        if (people.Count == 0) return;
        Section("People");
        foreach (var id in people.Take(14))
        {
            int pid = id;
            var agent = Session.World.Agents[pid];
            string doing = agent.Asleep ? " (asleep)" : agent.Activity switch
            {
                Stakeout.Simulation.History.ActivityKind.Work => " (working)",
                Stakeout.Simulation.History.ActivityKind.Dine => " (eating)",
                Stakeout.Simulation.History.ActivityKind.Drink => " (drinking)",
                Stakeout.Simulation.History.ActivityKind.Date => " (on a date)",
                _ => ""
            };
            _here.AddChild(Ui.Button($"Talk to {D.NameOrLook(pid)}{doing}", () => OpenInterview(pid)));
        }
        if (people.Count > 14) _here.AddChild(Ui.Label($"...and {people.Count - 14} more.", 16, Ui.Dim));
    }

    private void BuildRoomActions(Location loc)
    {
        Section("This room");
        _here.AddChild(Ui.Button("Look around", () => Act(() => ((List<string>)Session.Do("look")).ForEach(Say))));
        if (D.BodyHere) _here.AddChild(Ui.Button("Examine the body", () => Act(() => Report(Session.Do("body") as Clue))));
        foreach (var item in D.ItemsLyingHere().ToList())
        {
            int itemId = item.Id;
            _here.AddChild(Ui.Button($"Bag the {item.Name.ToLowerInvariant()}", () => Act(() => Report(Session.Do("collect", itemId) as Clue))));
        }
        bool office = loc.Id == D.Me.HomeLocationId;
        foreach (var f in D.FixturesHere().ToList())
        {
            var fixture = f;
            if (office && fixture.Type != FixtureType.Telephone) continue;
            switch (fixture.Type)
            {
                case FixtureType.Camera:
                    _here.AddChild(Ui.Button($"Watch the {fixture.Name.ToLowerInvariant()} tape", () => OpenFootage(fixture.Id)));
                    break;
                case FixtureType.Telephone:
                    _here.AddChild(Ui.Button($"{D.FixtureLabel(fixture)}: use it", OpenPhone, D.CanUsePhone()));
                    if (fixture.PhoneNumber != null && loc.Id != D.Me.HomeLocationId)
                        _here.AddChild(Ui.Button($"{D.FixtureLabel(fixture)}: plant a wiretap", () => Act(() => Say((string)Session.Do("planttap", fixture.Id))),
                            D.Wiretaps > 0, D.Wiretaps > 0 ? "Records every call on this line" : "You have no wiretaps. Buy one from the phone."));
                    break;
                case FixtureType.AnsweringMachine:
                    _here.AddChild(Ui.Button($"{D.FixtureLabel(fixture)}: play the tape", () => Act(() => ReportAll((List<Clue>)Session.Do("search", fixture.Id)))));
                    break;
                default:
                    _here.AddChild(Ui.Button($"Search: {D.FixtureLabel(fixture)}", () => Act(() => ReportAll((List<Clue>)Session.Do("search", fixture.Id)))));
                    break;
            }
        }
        if (D.CanUsePhone() && !D.FixturesHere().Any(f => f.Type == FixtureType.Telephone))
            _here.AddChild(Ui.Button("Use the payphone", OpenPhone));
        var surfaces = D.SurfacesHere();
        if (surfaces.Count > 0)
            _here.AddChild(Ui.Button("Dust for fingerprints...", () => ShowSurfaceMenu(surfaces)));
        foreach (var ap in loc.AccessPoints)
        {
            int apId = ap.Id;
            _here.AddChild(Ui.Button($"Examine the {ap.Name.ToLowerInvariant()}", () => Act(() => Report(Session.Do("door", apId) as Clue))));
        }
    }

    private void ShowSurfaceMenu(List<Surface> surfaces)
    {
        var menu = new PopupMenu();
        for (int i = 0; i < surfaces.Count; i++) menu.AddItem(surfaces[i].Name, i);
        menu.IdPressed += id =>
        {
            int surfaceId = surfaces[(int)id].Id;
            Act(() => ReportAll((List<Clue>)Session.Do("dust", surfaceId)));
            menu.QueueFree();
        };
        menu.PopupHide += () => menu.QueueFree();
        AddChild(menu);
        menu.Position = (Vector2I)GetGlobalMousePosition();
        menu.Popup();
    }

    private void BuildElsewhere(Address address, Location here)
    {
        var state = D.State;
        var rooms = D.RoomsHere().Where(l => l.Id != here.Id).ToList();
        if (rooms.Count > 0)
        {
            Section("Elsewhere in the building");
            foreach (var room in rooms.OrderBy(r => r.Floor ?? -1).ThenBy(r => r.Name))
            {
                var r = room;
                var access = D.CanEnter(r.Id, out var reason);
                var row = Ui.HBox(4);
                var name = Ui.Label(r.Name + (D.Case != null && r.Id == D.Case.SceneLocationId ? "  (scene)" : ""), 16,
                    D.Case != null && r.Id == D.Case.SceneLocationId ? Ui.Red : Ui.Text);
                name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                name.ClipText = true;
                row.AddChild(name);
                bool home = r.HasTag("home");
                if (access == Access.Open) row.AddChild(Ui.Button("Go in", () => Act(() => Session.Do("enter", r.Id))));
                if (home && r.Id != D.Me.HomeLocationId) row.AddChild(Ui.Button("Knock", () => Act(() => Knock(r.Id))));
                if (access == Access.Locked) row.AddChild(Ui.Button("Pick lock", () => OpenLockpick(r.Id), tooltip: reason));
                if (access == Access.Refused && !home) row.AddChild(Ui.Label("staff only", 16, Ui.Dim));
                _here.AddChild(row);
            }
        }

        Section("Outside");
        if (!here.HasTag("exterior") && here.Id != D.ArrivalLocation(address.Id).Id)
            _here.AddChild(Ui.Button("Step outside", () => Act(() => Session.Do("leave"))));
        _here.AddChild(Ui.Button("Stake out this place", StartStakeout, tooltip: "Watch who comes and goes while time runs"));
        _here.AddChild(Ui.Button("Drive somewhere (pick on the map)", () => ShowTab("Map")));
        if (address.Id != D.Me.HomeAddressId)
            _here.AddChild(Ui.Button("Drive back to the office", () => TravelTo(D.Me.HomeAddressId)));
        _here.AddChild(Ui.Button("Pin this address to the board", () => PinToBoard(Stakeout.Evidence.EvidenceEntityType.Address, address.Id)));
    }

    private void Knock(int locationId)
    {
        var answered = (List<int>)Session.Do("knock", locationId);
        if (answered.Count == 1) OpenInterview(answered[0]);
    }

    // ---- stakeout ----

    private void StartStakeout()
    {
        if (Session.Do("stakeout") is true) SetSpeed(600);
        AfterAction();
    }

    private void BuildStakeout()
    {
        var state = D.State;
        _here.AddChild(Ui.Heading("Stakeout"));
        _here.AddChild(Ui.Label($"Watching {state.PlaceName(D.AddressId.Value)}. Let time run; arrivals and departures are logged.", 16, Ui.Dim, wrap: true));
        var speeds = Ui.HBox(4);
        foreach (var (label, s) in new[] { ("Slow", 60.0), ("Normal", 600.0), ("Fast", 3600.0) })
            speeds.AddChild(Ui.Button(label, () => SetSpeed(s)));
        _here.AddChild(speeds);
        _here.AddChild(Ui.Button("End the stakeout", () =>
        {
            SetSpeed(0);
            Report(Session.Do("endstakeout") as Clue);
            AfterAction();
        }));
        _here.AddChild(Ui.Spacer(6));
        var log = D.StakeoutLog;
        for (int i = log.Count - 1; i >= Math.Max(0, log.Count - 14); i--)
        {
            var entry = log[i];
            int index = i;
            _here.AddChild(Ui.Label($"{entry.Time:HH:mm} {entry.Text}", 16, Ui.Text, wrap: true));
            if (entry.Departure != null && entry.Departure.VehicleId.HasValue && Session.World.Now - entry.Time < TimeSpan.FromMinutes(3))
                _here.AddChild(Ui.Button("   Follow them", () =>
                {
                    var trip = Session.Do("follow", index) as Stakeout.Simulation.History.TripFact;
                    if (trip != null) _travelSpeed = Math.Max(120, (trip.Arrive - Session.World.Now).TotalSeconds / 2.5);
                    AfterAction();
                }));
        }
    }

    // ---- helpers ----

    private void Act(Action action)
    {
        if (D.Travelling) return;
        action();
        AfterAction();
    }

    public void Report(Clue clue)
    {
        if (clue == null) return;
        AppendLog($"[{clue.Title}] {clue.Text}", Ui.Amber);
    }

    public void ReportAll(IEnumerable<Clue> clues)
    {
        foreach (var c in clues) Report(c);
    }
}
