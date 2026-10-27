using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Stakeout.Investigation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;
using Stakeout.UI;

/// <summary>Interview, telephone, lockpicking, security tape and verdict dialogs.</summary>
public partial class GameShell
{
    // ---- interviews ----

    public void OpenInterview(int personId)
    {
        SetSpeed(0);
        var person = D.State.People[personId];
        var box = Ui.VBox(8);
        var header = Ui.Label("", 24, Ui.Amber);
        var looks = Ui.Label(D.Looks(personId), 16, Ui.Dim, wrap: true);
        box.AddChild(header);
        box.AddChild(looks);

        var columns = Ui.HBox(12);
        box.AddChild(columns);
        var topics = Ui.VBox(4);
        topics.CustomMinimumSize = new Vector2(300, 0);
        columns.AddChild(topics);
        var transcriptPanel = Ui.PanelBox(new Color(0.05f, 0.045f, 0.04f), Ui.Line, 10);
        transcriptPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        transcriptPanel.CustomMinimumSize = new Vector2(460, 420);
        var transcript = Ui.Rich("", 22, Ui.Hand, fit: false);
        transcript.ScrollFollowing = true;
        transcript.SizeFlagsVertical = SizeFlags.ExpandFill;
        transcriptPanel.AddChild(transcript);
        columns.AddChild(transcriptPanel);

        string night = D.Case != null ? D.Case.NightStart.ToString("dddd") + " night" : "last night";
        string victim = D.Case != null ? D.State.People[D.Case.VictimId].FirstName : "the victim";

        void UpdateHeader()
        {
            header.Text = D.People.TryGetValue(personId, out var k) && k.Met ? person.FullName : "A stranger";
        }

        string Question(Topic topic, int? subject) => topic switch
        {
            Topic.Introduce => "Who are you, if you don't mind me asking?",
            Topic.Victim => $"How did you know {victim}?",
            Topic.Whereabouts => $"Where were you {night}?",
            Topic.SawAnything => $"Did you see or hear anything unusual {night}?",
            Topic.Enemies => $"Did {victim} have trouble with anyone?",
            Topic.AboutPerson => $"What can you tell me about {D.State.People[subject.Value].FullName}?",
            Topic.ShowPerson => $"Was the person you saw like this: {D.Looks(subject.Value)}?",
            Topic.ConfirmAlibi => $"Was {D.State.People[subject.Value].FullName} with you {night}?",
            Topic.Prints => "Would you mind giving me your fingerprints? To rule you out.",
            Topic.PalmGlass => "Cigarette?",
            _ => "..."
        };

        void Write(Topic topic, int? subject, string answer)
        {
            transcript.AppendText($"[color=#{Ui.Hex(Ui.Blue)}]You: {Ui.Escape(Question(topic, subject))}[/color]\n");
            transcript.AppendText($"[color=#{Ui.Hex(Ui.Ink.Lightened(0.7f))}]{Ui.Escape(answer)}[/color]\n\n");
        }

        void AskTopic(string _, Topic topic, int? subject = null)
        {
            if (!D.CanTalkTo(personId))
            {
                transcript.AppendText($"[color=#{Ui.Hex(Ui.Dim)}](They're gone.)[/color]\n");
                return;
            }
            var result = (InterviewResult)Session.Do("ask", (long)personId, (long)topic, subject ?? 0);
            Write(topic, subject, result.Answer);
            AppendLog($"{D.NameOrLook(personId)}: {result.Answer}", Ui.Text);
            UpdateHeader();
        }

        if (D.Transcripts.TryGetValue(personId, out var earlier))
            foreach (var (topic, subject, answer) in earlier) Write(topic, subject, answer);

        topics.AddChild(Ui.Button("Who are you?", () => AskTopic("Who are you, if you don't mind me asking?", Topic.Introduce)));
        if (D.Case != null)
        {
            topics.AddChild(Ui.Button($"About {victim}...", () => AskTopic($"How did you know {victim}?", Topic.Victim)));
            topics.AddChild(Ui.Button($"Where were you {night}?", () => AskTopic($"Where were you {night}?", Topic.Whereabouts)));
            topics.AddChild(Ui.Button("See or hear anything?", () => AskTopic($"Did you see or hear anything unusual {night}?", Topic.SawAnything)));
            topics.AddChild(Ui.Button($"Did {victim} have enemies?", () => AskTopic($"Did {victim} have trouble with anyone?", Topic.Enemies)));
        }
        topics.AddChild(Ui.Spacer(4));
        var known = D.People.Keys.Where(id => id != personId && id != D.Agent.Id).OrderBy(id => D.State.People[id].LastName).ToList();
        topics.AddChild(PersonPicker("Ask about...", known, id =>
            AskTopic($"What can you tell me about {D.State.People[id].FullName}?", Topic.AboutPerson, id)));
        var met = known.Where(id => D.People[id].Met).ToList();
        topics.AddChild(PersonPicker("Describe someone: is this who you saw?", met, id =>
            AskTopic($"Was the person you saw like this: {D.Looks(id)}?", Topic.ShowPerson, id)));
        topics.AddChild(PersonPicker($"Was this person with you {night}?", known, id =>
            AskTopic($"Was {D.State.People[id].FullName} with you {night}?", Topic.ConfirmAlibi, id)));
        topics.AddChild(Ui.Spacer(4));
        topics.AddChild(Ui.Button("Ask for fingerprints", () => AskTopic("Would you mind giving me your fingerprints? To rule you out.", Topic.Prints)));
        topics.AddChild(Ui.Button("Offer a cigarette (palm the pack)", () => AskTopic("Cigarette?", Topic.PalmGlass)));
        topics.AddChild(Ui.Spacer(8));
        topics.AddChild(Ui.Button("Pin to the board", () => PinToBoard(Stakeout.Evidence.EvidenceEntityType.Person, personId)));
        topics.AddChild(Ui.Button("Done", CloseModal));

        UpdateHeader();
        ShowModal(box, 820);
    }

    private Control PersonPicker(string label, List<int> people, Action<int> chosen)
    {
        var b = Ui.Button(label + (people.Count == 0 ? " (nobody yet)" : ""), null, people.Count > 0);
        b.Pressed += () =>
        {
            var menu = new PopupMenu();
            for (int i = 0; i < people.Count && i < 40; i++) menu.AddItem(D.State.People[people[i]].FullName, i);
            menu.IdPressed += id =>
            {
                chosen(people[(int)id]);
                menu.QueueFree();
            };
            menu.PopupHide += () => menu.QueueFree();
            AddChild(menu);
            menu.Position = (Vector2I)GetGlobalMousePosition();
            menu.Popup();
        };
        return b;
    }

    // ---- telephone ----

    public void OpenPhone()
    {
        SetSpeed(0);
        var box = Ui.VBox(6);
        box.AddChild(Ui.Heading("Telephone"));
        var output = Ui.Rich("", 16);
        output.CustomMinimumSize = new Vector2(0, 60);

        void Show(object result)
        {
            var text = result as string ?? "";
            output.Text = Ui.Escape(text);
            if (text.Length > 0) Say(text);
        }

        if (D.Case != null)
        {
            box.AddChild(Ui.Label("Lt. Hayes, Homicide (owes you a favor or two)", 16, Ui.Amber));
            var hayes = Ui.HBox(4);
            hayes.AddChild(Ui.Button("Coroner's report", () => Show(Session.Do("police", (long)PoliceRequest.CoronerReport))));
            var numbers = D.People.Values.Where(k => k.PhoneKnown || k.PersonId == D.Case.VictimId)
                .Select(k => D.State.People[k.PersonId]).Where(p => p.HomePhoneId.HasValue).ToList();
            hayes.AddChild(PersonPicker("Phone records...", numbers.Select(p => p.Id).ToList(),
                id => Show(Session.Do("police", (long)PoliceRequest.PhoneRecords, D.State.People[id].HomePhoneId.Value))));
            box.AddChild(hayes);
            var hayes2 = Ui.HBox(4);
            var prints = D.Clues.Where(c => c.Kind == ClueKind.Print && c.MatchedPersonId == null).ToList();
            var printButton = Ui.Button("Run a print...", null, prints.Count > 0);
            printButton.Pressed += () => Pick(prints.Select(c => c.Title).ToList(), i => Show(Session.Do("police", (long)PoliceRequest.RunPrint, prints[i].Id)));
            hayes2.AddChild(printButton);
            hayes2.AddChild(PersonPicker("Gun registry...", D.People.Keys.Where(id => id != D.Agent.Id).ToList(),
                id => Show(Session.Do("police", (long)PoliceRequest.GunRegistry, id))));
            var bagged = D.Bagged().ToList();
            var lab = Ui.Button("Send to the lab...", null, bagged.Count > 0);
            lab.Pressed += () => Pick(bagged.Select(i => i.Name).ToList(), i => Show(Session.Do("police", (long)PoliceRequest.LabTest, bagged[i].Id)));
            hayes2.AddChild(lab);
            box.AddChild(hayes2);
        }

        box.AddChild(Ui.Spacer(6));
        box.AddChild(Ui.Label("Phone book", 16, Ui.Amber));
        var bookRow = Ui.HBox(4);
        var name = new LineEdit { PlaceholderText = "Name", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        bookRow.AddChild(name);
        var results = Ui.VBox(2);
        bookRow.AddChild(Ui.Button("Look up", () =>
        {
            Ui.Clear(results);
            var hits = (List<Person>)Session.Do("phonebook", name.Text);
            if (hits.Count == 0) results.AddChild(Ui.Label("No listing.", 16, Ui.Dim));
            foreach (var p in hits)
            {
                int pid = p.Id;
                var row = Ui.HBox(4);
                var l = Ui.Label($"{p.FullName}  {D.State.PhoneOf(p)?.PhoneNumber}  {D.State.HomeLine(p)}", 16);
                l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                row.AddChild(l);
                row.AddChild(Ui.Button("Note it", () => { Session.Do("listing", pid); Say($"Noted: {p.FullName}, {D.State.HomeLine(p)}."); }));
                results.AddChild(row);
            }
        }));
        box.AddChild(bookRow);
        box.AddChild(results);

        var reverseRow = Ui.HBox(4);
        var number = new LineEdit { PlaceholderText = "555-0000", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        reverseRow.AddChild(number);
        reverseRow.AddChild(Ui.Button("Whose number?", () => Show(Session.Do("reverse", number.Text.Trim()))));
        reverseRow.AddChild(Ui.Button("Ring it", () => Show(Session.Do("call", number.Text.Trim()))));
        box.AddChild(reverseRow);

        box.AddChild(Ui.Spacer(6));
        box.AddChild(Ui.Label("Your supplier", 16, Ui.Amber));
        box.AddChild(Ui.Button($"Order a wiretap ($150) - you have {D.Wiretaps}", () => Show(Session.Do("buytap"))));
        box.AddChild(output);
        box.AddChild(Ui.Button("Hang up", CloseModal));
        ShowModal(box, 760);
    }

    private void Pick(List<string> options, Action<int> chosen)
    {
        var menu = new PopupMenu();
        for (int i = 0; i < options.Count && i < 40; i++) menu.AddItem(options[i], i);
        menu.IdPressed += id =>
        {
            chosen((int)id);
            menu.QueueFree();
        };
        menu.PopupHide += () => menu.QueueFree();
        AddChild(menu);
        menu.Position = (Vector2I)GetGlobalMousePosition();
        menu.Popup();
    }

    // ---- lockpicking ----

    public void OpenLockpick(int locationId)
    {
        SetSpeed(0);
        var box = Ui.VBox(8);
        var loc = D.State.Locations[locationId];
        box.AddChild(Ui.Heading($"Picking the lock: {loc.Name}"));
        box.AddChild(Ui.Label("Set each pin when the needle is in the bright band. Two slips and the lock jams loudly.", 16, Ui.Dim, wrap: true));
        var game = new LockpickGame();
        game.CustomMinimumSize = new Vector2(600, 140);
        box.AddChild(game);
        var buttons = Ui.HBox(8);
        var set = Ui.Button("Set pin  [Space]", game.TrySet);
        buttons.AddChild(set);
        buttons.AddChild(Ui.Button("Give up", CloseModal));
        box.AddChild(buttons);
        game.Finished += success =>
        {
            CloseModal();
            Session.Do("picklock", locationId, success ? 1 : 0);
            AfterAction();
        };
        ShowModal(box, 660);
        set.GrabFocus();
    }

    // ---- security tapes ----

    public void OpenFootage(int cameraId)
    {
        SetSpeed(0);
        var camera = D.State.Fixtures[cameraId];
        var box = Ui.VBox(6);
        box.AddChild(Ui.Heading(camera.Name));
        box.AddChild(Ui.Label("The tapes go back three days. Which stretch do you want to watch?", 16, Ui.Dim, wrap: true));
        var output = Ui.Rich("", 16, fit: false);
        output.CustomMinimumSize = new Vector2(0, 320);
        var now = Session.World.Now;
        void Watch(DateTime from, DateTime to)
        {
            var clue = Session.Do("footage", cameraId, from.Ticks, to.Ticks) as Clue;
            output.Text = clue == null ? "No tape for you." : Ui.Escape(clue.Text);
            AfterAction();
        }
        var row = Ui.HBox(4);
        if (D.Case != null)
            row.AddChild(Ui.Button($"{D.Case.NightStart:dddd} night", () => Watch(D.Case.NightStart, D.Case.NightEnd)));
        row.AddChild(Ui.Button("Last 6 hours", () => Watch(now.AddHours(-6), now)));
        row.AddChild(Ui.Button("Last 24 hours", () => Watch(now.AddHours(-24), now)));
        box.AddChild(row);
        box.AddChild(output);
        box.AddChild(Ui.Button("Done", CloseModal));
        ShowModal(box, 760);
    }

    // ---- scanner and help ----

    public void ShowScannerAlert()
    {
        if (D.Case == null) return;
        var box = Ui.VBox(10);
        box.AddChild(Ui.Label("POLICE SCANNER", 32, Ui.Red));
        var scanner = D.Clues.FirstOrDefault(c => c.Title == "Police scanner");
        box.AddChild(Ui.Label(scanner?.Text ?? "", 16, Ui.Text, wrap: true));
        box.AddChild(Ui.Label("Lt. Hayes owes you. If you get there while the body's still on scene, he'll let you look around.", 16, Ui.Dim, wrap: true));
        var row = Ui.HBox(8);
        row.AddChild(Ui.Button("Drive to the scene", () =>
        {
            CloseModal();
            if (!D.Travelling) TravelTo(D.Case.SceneAddressId);
        }));
        row.AddChild(Ui.Button("Later", CloseModal));
        box.AddChild(row);
        ShowModal(box, 640);
    }

    public void ShowHelp()
    {
        SetSpeed(0);
        var box = Ui.VBox(6);
        box.AddChild(Ui.Heading("How to work a case"));
        var text = Ui.Rich(string.Join("\n", new[]
        {
            "[color=#e8a840]Time[/color] stands still until you act or press play (space). Every action takes game minutes; the city keeps living around you.",
            "[color=#e8a840]The scene[/color]: examine the body (time of death), search the furniture (address book, letters, the answering machine tape), dust for prints, examine the doors, bag anything bloody.",
            "[color=#e8a840]Neighbors[/color]: knock on doors. Ask what they saw or heard that night, who the victim had trouble with. Memories fade, so go early.",
            "[color=#e8a840]Suspects[/color]: find them at home in the evening or at work by day. Ask where they were. Ask for prints, or palm a cigarette pack. Then check their alibi with the people they named.",
            "[color=#e8a840]Line-ups[/color]: once you have met a suspect, describe them to a witness who saw a stranger.",
            "[color=#e8a840]The phone[/color] (office, homes, diner and bar payphones): Lt. Hayes gets you phone records, the coroner's report, gun registry, lab tests and print runs. The phone book finds anyone. Order wiretaps.",
            "[color=#e8a840]Covert work[/color]: pick a lock when nobody's home and search the place. Plant a wiretap on a suspect's phone - and see who they call after you've questioned them.",
            "[color=#e8a840]Surveillance[/color]: stake out an address and watch who comes and goes; follow a car that leaves. Office and apartment buildings keep three days of security tape.",
            "[color=#e8a840]The case file[/color]: pick a suspect and the evidence. The DA scores opportunity, motive, physical evidence and exposed lies. Charge when you're sure: you only get one shot.",
            "",
            "Everyone in this city is real: they work, eat, drink, date and sleep, and everything you find came from what actually happened."
        }), 16, fit: true);
        box.AddChild(text);
        box.AddChild(Ui.Button("Got it", CloseModal));
        ShowModal(box, 820);
    }

    // ---- the verdict ----

    public void ShowVerdict(Verdict verdict)
    {
        SetSpeed(0);
        var box = Ui.VBox(8);
        var accused = D.State.People[verdict.Assessment.AccusedId];
        box.AddChild(Ui.Label(verdict.Outcome, 24, verdict.Convicted && verdict.RightPerson ? Ui.Green : Ui.Red, wrap: true));
        box.AddChild(Ui.Label($"The DA put your case at {verdict.Assessment.Probability:P0}. {verdict.Assessment.Summary}", 16, Ui.Dim, wrap: true));
        string judgement = verdict.RightPerson
            ? verdict.Convicted ? "You got the right person." : $"{accused.FullName} did do it."
            : verdict.Convicted ? $"But {accused.FullName} didn't do it." : $"Just as well: {accused.FullName} didn't do it.";
        box.AddChild(Ui.Label(judgement, 24, Ui.Amber, wrap: true));
        box.AddChild(Ui.Heading("What really happened"));
        var truth = Ui.Rich(string.Join("\n", verdict.Truth.Select(Ui.Escape)), 16, fit: false);
        truth.CustomMinimumSize = new Vector2(0, 300);
        box.AddChild(truth);
        var buttons = Ui.HBox(8);
        buttons.AddChild(Ui.Button("Keep looking around this city", CloseModal));
        buttons.AddChild(Ui.Button("New case (new city)", () =>
        {
            Game.EndGame();
            Game.PendingSeed = (int)(Time.GetUnixTimeFromSystem() % 100000);
            GetTree().ChangeSceneToFile("res://scenes/loading/Loading.tscn");
        }));
        buttons.AddChild(Ui.Button("Main menu", () =>
        {
            Game.EndGame();
            GetTree().ChangeSceneToFile("res://scenes/main_menu/MainMenu.tscn");
        }));
        box.AddChild(buttons);
        ShowModal(box, 860);
    }
}

/// <summary>A tension wrench and a needle: press when the needle crosses the bright band.</summary>
public partial class LockpickGame : Control
{
    public event Action<bool> Finished;
    private const int Pins = 4;
    private int _set;
    private int _slips;
    private double _t;
    private float _bandCenter = 0.5f;
    private float _bandWidth = 0.16f;
    private readonly Random _rng = new();
    private bool _done;
    private string _flash = "";
    private double _flashTime;

    public override void _Process(double delta)
    {
        if (_done) return;
        _t += delta * (1.1 + _set * 0.35);
        _flashTime -= delta;
        QueueRedraw();
    }

    private float Needle => (float)(0.5 + 0.5 * Math.Sin(_t * 2.4));

    public void TrySet()
    {
        if (_done) return;
        if (Math.Abs(Needle - _bandCenter) <= _bandWidth / 2)
        {
            _set++;
            _flash = "click";
            _bandCenter = 0.2f + (float)_rng.NextDouble() * 0.6f;
            _bandWidth = Math.Max(0.08f, _bandWidth - 0.02f);
        }
        else
        {
            _slips++;
            _flash = "slip!";
        }
        _flashTime = 0.6;
        if (_set >= Pins || _slips >= 2)
        {
            _done = true;
            var success = _set >= Pins;
            GetTree().CreateTimer(0.4).Timeout += () => Finished?.Invoke(success);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false } k && k.Keycode == Key.Space)
        {
            TrySet();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Draw()
    {
        var size = Size;
        float barY = 50, barH = 30;
        DrawRect(new Rect2(0, barY, size.X, barH), new Color(0.1f, 0.09f, 0.08f));
        DrawRect(new Rect2((_bandCenter - _bandWidth / 2) * size.X, barY, _bandWidth * size.X, barH), new Color(Ui.Amber, 0.8f));
        float x = Needle * size.X;
        DrawLine(new Vector2(x, barY - 10), new Vector2(x, barY + barH + 10), Ui.Text, 3);
        for (int i = 0; i < Pins; i++)
            DrawRect(new Rect2(i * 40 + 4, 6, 30, 30), i < _set ? Ui.Green : new Color(0.3f, 0.27f, 0.24f));
        for (int i = 0; i < _slips; i++) DrawString(Ui.Pixel, new Vector2(size.X - 90 + i * 40, 30), "X", HorizontalAlignment.Left, -1, 32, Ui.Red);
        if (_flashTime > 0) DrawString(Ui.Pixel, new Vector2(size.X / 2 - 30, 120), _flash, HorizontalAlignment.Left, -1, 32, _flash == "click" ? Ui.Green : Ui.Red);
    }
}
