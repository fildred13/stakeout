using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Stakeout.Investigation;
using Stakeout.Simulation.Entities;
using Stakeout.UI;

/// <summary>The detective's notebook: every clue, newest first, in the detective's handwriting.</summary>
public partial class NotebookView : HBoxContainer
{
    private readonly GameShell _shell;
    private readonly ItemList _list;
    private readonly RichTextLabel _page;
    private readonly OptionButton _filter;
    private readonly Button _pin;
    private List<Clue> _shown = new();
    private Clue _selected;

    private static readonly (string Name, ClueKind[] Kinds)[] Filters =
    {
        ("Everything", null),
        ("The scene", new[] { ClueKind.Scene, ClueKind.Body, ClueKind.Coroner, ClueKind.Item, ClueKind.Tape }),
        ("Statements", new[] { ClueKind.Statement, ClueKind.Sighting, ClueKind.Motive, ClueKind.Lineup, ClueKind.Refusal }),
        ("Alibis", new[] { ClueKind.Alibi, ClueKind.AlibiCheck }),
        ("Prints", new[] { ClueKind.Print, ClueKind.PrintMatch }),
        ("Papers", new[] { ClueKind.Document, ClueKind.PhoneRecord, ClueKind.Registry }),
        ("Surveillance", new[] { ClueKind.Camera, ClueKind.Stakeout, ClueKind.Wiretap }),
        ("Lab", new[] { ClueKind.Lab, ClueKind.Ballistics })
    };

    public NotebookView(GameShell shell)
    {
        _shell = shell;
        AddThemeConstantOverride("separation", 0);
        var left = Ui.VBox(4);
        left.CustomMinimumSize = new Vector2(380, 0);
        _filter = new OptionButton();
        foreach (var (name, _) in Filters) _filter.AddItem(name);
        _filter.ItemSelected += _ => Refresh();
        left.AddChild(_filter);
        _list = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill };
        _list.ItemSelected += i => Show(_shown[(int)i]);
        left.AddChild(_list);
        AddChild(left);

        var paper = Ui.PanelBox(Ui.Paper, new Color(0.6f, 0.55f, 0.45f), 18);
        paper.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var pageBox = Ui.VBox(8);
        _page = Ui.Rich("", 24, Ui.Hand, fit: false);
        _page.AddThemeColorOverride("default_color", Ui.Ink);
        _page.SizeFlagsVertical = SizeFlags.ExpandFill;
        pageBox.AddChild(_page);
        _pin = Ui.Button("Pin to the evidence board", () =>
        {
            if (_selected != null) _shell.PinToBoard(Stakeout.Evidence.EvidenceEntityType.Clue, _selected.Id);
        });
        pageBox.AddChild(_pin);
        paper.AddChild(pageBox);
        AddChild(paper);
    }

    public void Refresh()
    {
        var kinds = Filters[Math.Max(0, _filter.Selected)].Kinds;
        _shown = _shell.D.Clues.Where(c => kinds == null || kinds.Contains(c.Kind)).OrderByDescending(c => c.FoundAt).ThenByDescending(c => c.Id).ToList();
        _list.Clear();
        foreach (var c in _shown) _list.AddItem($"{c.FoundAt:ddd HH:mm}  {c.Title}");
        if (_selected != null && _shown.Contains(_selected))
        {
            _list.Select(_shown.IndexOf(_selected));
            Show(_selected);
        }
        else if (_shown.Count > 0)
        {
            _list.Select(0);
            Show(_shown[0]);
        }
        else _page.Text = "Nothing written down yet.";
    }

    public void Select(Clue clue)
    {
        _filter.Selected = 0;
        _selected = clue;
        Refresh();
    }

    private void Show(Clue c)
    {
        _selected = c;
        var named = c.NamedPersonIds.Distinct().Select(id => _shell.D.State.People[id].FullName).ToList();
        _page.Text = $"[b]{Ui.Escape(c.Title)}[/b]\n[font_size=18]{c.FoundAt:dddd h:mm tt} - {Ui.Escape(c.Source)}[/font_size]\n\n" +
                     Ui.Escape(c.Text) +
                     (named.Count > 0 ? $"\n\n[font_size=18]Names: {Ui.Escape(string.Join(", ", named))}[/font_size]" : "");
    }
}

/// <summary>Everyone the detective has heard of, and what is known about them.</summary>
public partial class PeopleView : HBoxContainer
{
    private readonly GameShell _shell;
    private readonly ItemList _list;
    private readonly VBoxContainer _detail;
    private List<int> _ids = new();
    private int? _selected;

    public PeopleView(GameShell shell)
    {
        _shell = shell;
        _list = new ItemList { CustomMinimumSize = new Vector2(320, 0) };
        _list.ItemSelected += i => Show(_ids[(int)i]);
        AddChild(_list);
        var panel = Ui.PanelBox(Ui.Panel, Ui.Line, 14);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _detail = Ui.VBox(6);
        _detail.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_detail);
        panel.AddChild(scroll);
        AddChild(panel);
    }

    public void Refresh()
    {
        var d = _shell.D;
        _ids = d.People.Keys.Where(id => id != d.Agent.Id)
            .OrderByDescending(id => d.Case != null && id == d.Case.VictimId)
            .ThenByDescending(id => d.Clues.Count(c => c.AboutPersonId == id || c.NamedPersonIds.Contains(id)))
            .ToList();
        _list.Clear();
        foreach (var id in _ids)
        {
            var p = d.State.People[id];
            string tag = d.Case != null && id == d.Case.VictimId ? "  (victim)" : d.People[id].Met ? "" : "  (not met)";
            _list.AddItem(p.FullName + tag);
        }
        if (_selected.HasValue && _ids.Contains(_selected.Value))
        {
            _list.Select(_ids.IndexOf(_selected.Value));
            Show(_selected.Value);
        }
        else if (_ids.Count > 0)
        {
            _list.Select(0);
            Show(_ids[0]);
        }
    }

    public void Select(int personId)
    {
        _selected = personId;
        Refresh();
    }

    private void Show(int id)
    {
        _selected = id;
        Ui.Clear(_detail);
        var d = _shell.D;
        var state = d.State;
        var p = state.People[id];
        var k = d.People[id];
        _detail.AddChild(Ui.Label(p.FullName, 32, Ui.Amber));
        if (k.Met) _detail.AddChild(Ui.Label(d.Looks(id), 16, Ui.Dim, wrap: true));
        else _detail.AddChild(Ui.Label("You haven't met them in person.", 16, Ui.Dim));
        if (k.AddressKnown) _detail.AddChild(Ui.Label($"Lives at {state.HomeLine(p)}", 16));
        if (k.PhoneKnown && p.HomePhoneId.HasValue) _detail.AddChild(Ui.Label($"Phone: {state.Fixtures[p.HomePhoneId.Value].PhoneNumber}", 16));
        if (k.EmployerKnown && p.BusinessId.HasValue)
        {
            var biz = state.Businesses[p.BusinessId.Value];
            _detail.AddChild(Ui.Label($"Works at {biz.Name}, {state.AddressLine(biz.AddressId)}", 16));
        }
        if (k.VehicleKnown && p.VehicleId.HasValue) _detail.AddChild(Ui.Label($"Drives a {state.Vehicles[p.VehicleId.Value].Description}", 16));
        _detail.AddChild(Ui.Label(k.ReferencePrints ? "You have their fingerprints." : "No fingerprints on file.", 16, k.ReferencePrints ? Ui.Green : Ui.Dim));
        foreach (var note in k.Notes) _detail.AddChild(Ui.Label(note, 16, Ui.Dim, wrap: true));

        var row = Ui.HBox(4);
        if (k.AddressKnown) row.AddChild(Ui.Button("Drive to their home", () => _shell.TravelTo(p.HomeAddressId), !d.Travelling));
        if (k.EmployerKnown && p.BusinessId.HasValue)
            row.AddChild(Ui.Button("Drive to their work", () => _shell.TravelTo(state.Businesses[p.BusinessId.Value].AddressId), !d.Travelling));
        if (!k.AddressKnown) row.AddChild(Ui.Button("Look them up", () =>
        {
            _shell.Session.Do("phonebook", p.FullName);
            Show(id);
        }));
        row.AddChild(Ui.Button("Pin to board", () => _shell.PinToBoard(Stakeout.Evidence.EvidenceEntityType.Person, id)));
        _detail.AddChild(row);

        _detail.AddChild(Ui.Spacer(8));
        _detail.AddChild(Ui.Heading("In your notebook"));
        foreach (var c in d.Clues.Where(c => c.AboutPersonId == id || c.NamedPersonIds.Contains(id) || c.WitnessId == id).OrderBy(c => c.FoundAt).Take(30))
        {
            var clue = c;
            _detail.AddChild(Ui.Button($"{c.Title}", () => _shell.ShowNotebookAt(clue)));
        }
    }
}

/// <summary>The case file: who to charge, with what, and what the DA thinks of it.</summary>
public partial class CaseFileView : HBoxContainer
{
    private readonly GameShell _shell;
    private readonly VBoxContainer _left;
    private readonly VBoxContainer _right;
    private readonly HashSet<int> _excluded = new();
    private int? _accused;

    public CaseFileView(GameShell shell)
    {
        _shell = shell;
        AddThemeConstantOverride("separation", 8);
        var leftPanel = Ui.PanelBox(Ui.Panel, Ui.Line, 12);
        leftPanel.CustomMinimumSize = new Vector2(430, 0);
        var leftScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _left = Ui.VBox(4);
        _left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        leftScroll.AddChild(_left);
        leftPanel.AddChild(leftScroll);
        AddChild(leftPanel);
        var rightPanel = Ui.PanelBox(Ui.Panel, Ui.Line, 12);
        rightPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var rightScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _right = Ui.VBox(6);
        _right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        rightScroll.AddChild(_right);
        rightPanel.AddChild(rightScroll);
        AddChild(rightPanel);
    }

    public void Refresh()
    {
        Ui.Clear(_left);
        Ui.Clear(_right);
        var d = _shell.D;
        if (d.Case == null)
        {
            _left.AddChild(Ui.Label("No case yet. Keep the radio on.", 16, Ui.Dim));
            return;
        }
        var state = d.State;
        var victim = state.People[d.Case.VictimId];
        _left.AddChild(Ui.Heading("The case"));
        _left.AddChild(Ui.Label($"{victim.FullName}, found dead at {state.HomeLine(victim)}.", 16, Ui.Text, wrap: true));
        _left.AddChild(Ui.Label($"Reported {d.Case.ReportedAt:dddd h:mm tt}.", 16, Ui.Dim));
        var tod = d.Clues.FirstOrDefault(c => c.Kind == ClueKind.Coroner) ?? d.Clues.FirstOrDefault(c => c.Kind == ClueKind.Body);
        _left.AddChild(Ui.Label(tod != null ? $"Time of death: {tod.TimeFrom:dddd h:mm tt} to {tod.TimeTo:dddd h:mm tt}." : "Time of death: unknown. Examine the body or get the coroner's report.", 16, Ui.Dim, wrap: true));

        if (d.Case.Closed && d.Case.Verdict != null)
        {
            _left.AddChild(Ui.Spacer(8));
            _left.AddChild(Ui.Label(d.Case.Verdict.Outcome, 16, Ui.Amber, wrap: true));
            _left.AddChild(Ui.Button("See what really happened", () => _shell.ShowVerdict(d.Case.Verdict)));
            return;
        }

        _left.AddChild(Ui.Spacer(8));
        _left.AddChild(Ui.Heading("Who did it?"));
        var suspects = d.People.Keys.Where(id => id != d.Case.VictimId && id != d.Agent.Id)
            .OrderByDescending(id => d.Clues.Count(c => c.AboutPersonId == id || c.MotivePersonId == id)).ToList();
        var pick = new OptionButton();
        pick.AddItem("(choose a suspect)", -1);
        foreach (var id in suspects) pick.AddItem(state.People[id].FullName, id);
        if (_accused.HasValue && suspects.Contains(_accused.Value)) pick.Select(suspects.IndexOf(_accused.Value) + 1);
        pick.ItemSelected += i =>
        {
            int id = pick.GetItemId((int)i);
            _accused = id < 0 ? null : id;
            Refresh();
        };
        _left.AddChild(pick);

        _left.AddChild(Ui.Spacer(8));
        _left.AddChild(Ui.Heading("Evidence to hand over"));
        _left.AddChild(Ui.Label("Untick anything you don't want the DA to see.", 16, Ui.Dim, wrap: true));
        foreach (var c in d.Clues.OrderBy(c => c.Kind).ThenBy(c => c.FoundAt))
        {
            var clue = c;
            var check = new CheckBox { Text = $"{c.Title}", ButtonPressed = !_excluded.Contains(c.Id), ClipText = true };
            check.Toggled += on =>
            {
                if (on) _excluded.Remove(clue.Id); else _excluded.Add(clue.Id);
                ShowAssessment();
            };
            _left.AddChild(check);
        }
        ShowAssessment();
    }

    private IEnumerable<int> Chosen() => _shell.D.Clues.Select(c => c.Id).Where(id => !_excluded.Contains(id));

    private void ShowAssessment()
    {
        Ui.Clear(_right);
        var d = _shell.D;
        _right.AddChild(Ui.Heading("District Attorney"));
        if (!_accused.HasValue)
        {
            _right.AddChild(Ui.Label("\"Bring me a name and the evidence, and I'll tell you if it'll stand up.\"", 16, Ui.Dim, wrap: true));
            _right.AddChild(Ui.Spacer(8));
            _right.AddChild(Ui.Label("A case needs four things: opportunity (they were there, or lied about it), motive, physical evidence, and lies exposed. Each piece of evidence adds weight; a missing pillar caps your chances.", 16, Ui.Dim, wrap: true));
            return;
        }
        var a = CaseEvaluator.Assess(d, _accused.Value, Chosen());
        var name = d.State.People[_accused.Value].FullName;
        _right.AddChild(Ui.Label($"The People v. {name}", 24, Ui.Amber));
        _right.AddChild(Ui.Label($"Chance of conviction: {a.Probability:P0}", 32, a.Probability >= 0.65 ? Ui.Green : a.Probability >= 0.5 ? Ui.Amber : Ui.Red));
        _right.AddChild(Ui.Label(a.Summary, 16, Ui.Text, wrap: true));
        var pillars = Ui.HBox(12);
        foreach (var (pillar, score) in a.Scores)
            pillars.AddChild(Ui.Label($"{pillar}: {score}", 16, score > 0 ? Ui.Text : Ui.Dim));
        _right.AddChild(pillars);
        _right.AddChild(Ui.Spacer(6));
        foreach (var f in a.Findings) _right.AddChild(Ui.Label($"+{f.Points}  {f.Text}", 16, Ui.Text, wrap: true));
        foreach (var doubt in a.Doubts) _right.AddChild(Ui.Label($"-{doubt.Points}  {doubt.Text}", 16, Ui.Red, wrap: true));
        foreach (var m in a.Missing) _right.AddChild(Ui.Label($"Missing: {m}", 16, Ui.Dim, wrap: true));
        _right.AddChild(Ui.Spacer(10));
        _right.AddChild(Ui.Button($"Hand the case to the DA: charge {name}", () =>
        {
            var args = new List<long> { _accused.Value };
            args.AddRange(Chosen().Select(x => (long)x));
            var verdict = (Verdict)_shell.Session.Do("submit", args.ToArray());
            _shell.ShowVerdict(verdict);
        }));
        _right.AddChild(Ui.Label("Once charged, the case is closed for good.", 16, Ui.Dim));
    }
}
