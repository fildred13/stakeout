using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Agents;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;
using Stakeout.Simulation.History;

namespace Stakeout.Investigation;

public enum PoliceRequest { PhoneRecords, RunPrint, CoronerReport, GunRegistry, LabTest }

public partial class Detective
{
    private readonly HashSet<string> _requested = new();
    public List<(int PhoneId, DateTime Since, string Where)> ActiveTaps { get; } = new();

    /// <summary>A telephone the detective can use here: the office, a home they are in, or a payphone at a diner or bar.</summary>
    public bool CanUsePhone()
    {
        if (Travelling || !LocationId.HasValue) return false;
        if (State.Phone(LocationId.Value) != null) return true;
        var biz = State.BusinessAt(AddressId.Value);
        return biz != null && biz.Type is BusinessType.Diner or BusinessType.DiveBar && State.Locations[LocationId.Value].HasTag("publicly_accessible");
    }

    private Fixture PhoneForCalls() =>
        LocationId.HasValue ? State.Phone(LocationId.Value) ?? State.Fixtures[Me.HomePhoneId.Value] : State.Fixtures[Me.HomePhoneId.Value];

    // ---- phone book ----

    public List<Person> PhoneBook(string name)
    {
        var q = name.Trim().ToLowerInvariant();
        if (q.Length < 3) return new List<Person>();
        var hits = State.People.Values.Where(p => !p.IsPlayer && p.HomePhoneId.HasValue && p.FullName.ToLowerInvariant().Contains(q))
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName).Take(12).ToList();
        foreach (var p in hits.Where(p => p.FullName.ToLowerInvariant() == q))
        {
            var k = Learn(p.Id);
            k.PhoneKnown = true;
            k.AddressKnown = true;
            KnownAddresses.Add(p.HomeAddressId);
        }
        return hits;
    }

    public void LearnListing(int personId)
    {
        var p = State.People[personId];
        var k = Learn(personId);
        k.PhoneKnown = true;
        k.AddressKnown = true;
        KnownAddresses.Add(p.HomeAddressId);
    }

    /// <summary>Criss-cross directory: who is listed at a number.</summary>
    public string ReverseLookup(string number)
    {
        var phone = World.Phones.ByNumber(number);
        if (phone == null) return $"{number}: not listed.";
        var residents = State.People.Values.Where(p => p.HomePhoneId == phone.Id && !p.IsPlayer).ToList();
        if (residents.Count > 0)
        {
            foreach (var r in residents) LearnListing(r.Id);
            return $"{number}: {string.Join(" and ", residents.Select(r => r.FullName))}, {State.AddressLine(residents[0].HomeAddressId)}.";
        }
        int loc = World.Phones.PhoneLocation(phone);
        int address = State.Locations[loc].AddressId;
        KnownAddresses.Add(address);
        return $"{number}: {State.PlaceName(address)}.";
    }

    /// <summary>Rings a number to see who picks up. The call is real: it is on the phone company's records too.</summary>
    public string CallNumber(string number)
    {
        if (!CanUsePhone()) return "There is no phone here.";
        var target = World.Phones.ByNumber(number);
        if (target == null) return "The operator: that number is not in service.";
        var owner = State.People.Values.FirstOrDefault(p => p.HomePhoneId == target.Id);
        var phone = PhoneForCalls();
        int loc = World.Phones.PhoneLocation(phone);
        var answeredBy = World.AgentsIn(World.Phones.PhoneLocation(target)).FirstOrDefault(a => a.Person.IsAlive && !a.Asleep);
        CallOutcome outcome = answeredBy != null ? CallOutcome.Answered
            : World.Phones.HasMachine(target) ? CallOutcome.AnsweringMachine : CallOutcome.NoAnswer;
        World.History.AddCall(new CallFact
        {
            Id = World.NextFactId(), CallerId = Agent.Id, FromPhoneId = phone.Id, ToPhoneId = target.Id, Time = Now,
            DurationSeconds = 40, Outcome = outcome, AnsweredById = answeredBy?.Id, IntendedId = owner?.Id ?? 0,
            Transcript = answeredBy != null ? $"{answeredBy.Person.FirstName}: Hello? ... Hello?" : null
        });
        _ = loc;
        Spend(TimeSpan.FromMinutes(1));
        if (answeredBy != null) return $"{(Knows(answeredBy.Id) ? answeredBy.Person.FullName : $"A {answeredBy.Person.ManWoman}'s voice")}: \"Hello?\" You hang up.";
        return outcome == CallOutcome.AnsweringMachine ? "The answering machine picks up. Nobody home." : "It rings and rings. Nobody home.";
    }

    // ---- Lt. Hayes ----

    /// <summary>Asks the police contact for help. Results arrive later as messages and clues.</summary>
    public string AskPolice(PoliceRequest request, int? target = null)
    {
        if (!CanUsePhone()) return "You need a phone.";
        if (Case == null) return "Hayes: \"What case?\"";
        string key = $"{request}:{target}";
        if (!_requested.Add(key)) return "Hayes: \"I'm on it already. Give me time.\"";
        Spend(TimeSpan.FromMinutes(5));
        switch (request)
        {
            case PoliceRequest.PhoneRecords:
            {
                var phone = State.Fixtures[target.Value];
                var ready = NextMorning(9);
                World.Scheduler.At(ready, () => DeliverPhoneRecords(phone));
                return $"Hayes: \"Toll records for {phone.PhoneNumber}? I'll lean on the phone company. Tomorrow morning.\"";
            }
            case PoliceRequest.RunPrint:
            {
                var print = Clues.First(c => c.Id == target);
                World.Scheduler.After(TimeSpan.FromHours(3), () => DeliverPrintRun(print));
                return $"Hayes: \"I'll have the lab run print {print.PrintLabel} against the files. Few hours.\"";
            }
            case PoliceRequest.CoronerReport:
            {
                var murder = Case.Truth;
                var ready = (murder.ReportedAt ?? Now).AddHours(9);
                if (ready < Now.AddHours(1)) ready = Now.AddHours(1);
                World.Scheduler.At(ready, DeliverCoroner);
                return "Hayes: \"The autopsy's scheduled. I'll call you with the report.\"";
            }
            case PoliceRequest.GunRegistry:
            {
                var person = State.People[target.Value];
                var gun = State.Items.Values.FirstOrDefault(i => i.ItemType == ItemType.Handgun && i.OwnerPersonId == person.Id
                                                                  && i.Data.TryGetValue("registered", out var r) && (bool)r);
                string text = gun != null
                    ? $"Gun registry: {person.FullName} has a {gun.Name} registered to {person.HimHer}."
                    : $"Gun registry: nothing registered to {person.FullName}.";
                var clue = AddClue(new Clue
                {
                    Id = NextClueId(), Kind = ClueKind.Registry, Title = $"Gun registry: {person.FullName}", Text = text,
                    FoundAt = Now, Source = "Lt. Hayes", AboutPersonId = person.Id, ItemId = gun?.Id
                });
                _ = clue;
                return $"Hayes: \"{text}\"";
            }
            case PoliceRequest.LabTest:
            {
                var item = State.Items[target.Value];
                if (item.HeldByPersonId != Agent.Id) return "You don't have that.";
                World.Scheduler.At(NextMorning(10), () => DeliverLab(item));
                return $"Hayes: \"Send the {item.Name} over. The lab will have something tomorrow.\"";
            }
        }
        return "";
    }

    private DateTime NextMorning(int hour)
    {
        var t = Now.Date.AddHours(hour);
        if (t <= Now.AddHours(2)) t = t.AddDays(1);
        return t;
    }

    private void DeliverPhoneRecords(Fixture phone)
    {
        var since = Now.AddDays(-7);
        var calls = World.History.CallsOf(phone.Id)
            .Where(c => c.Time >= since && c.Time <= Now && c.Outcome != CallOutcome.Emergency)
            .OrderBy(c => c.Time)
            .Select(c =>
            {
                bool outgoing = c.FromPhoneId == phone.Id;
                var other = State.Fixtures[outgoing ? c.ToPhoneId : c.FromPhoneId];
                return (c.Time, other.PhoneNumber ?? "unlisted", c.DurationSeconds, outgoing, other.Id);
            }).ToList();
        string owner = State.People.Values.FirstOrDefault(p => p.HomePhoneId == phone.Id)?.FullName ?? phone.PhoneNumber;
        var lines = calls.Select(c => $"{c.Time:ddd dd HH:mm} {(c.outgoing ? "to  " : "from")} {c.Item2} ({c.DurationSeconds / 60}m{c.DurationSeconds % 60:D2}s)");
        AddClue(new Clue
        {
            Id = NextClueId(), Kind = ClueKind.PhoneRecord, Title = $"Toll records: {owner}",
            Text = calls.Count == 0 ? "No calls in the last week." : string.Join("\n", lines), FoundAt = Now, Source = "Phone company via Lt. Hayes",
            Calls = calls.Select(c => (c.Time, c.Item2, c.DurationSeconds, c.outgoing, c.Id)).ToList(),
            SurfaceId = phone.Id
        });
        Say($"Hayes calls: the phone records for {owner} are in your notebook.");
    }

    private void DeliverPrintRun(Clue print)
    {
        var person = State.People[print.PrintPersonId.Value];
        if (person.Has(Traits.CriminalRecord) || person.Id == Case?.VictimId)
        {
            var known = Learn(person.Id);
            known.ReferencePrints = true;
            Say($"Hayes calls: print {print.PrintLabel} is on file. {person.FullName}{(person.Id == Case?.VictimId ? ", the victim" : ", with a record")}.");
            MatchPrintsFor(person.Id);
        }
        else Say($"Hayes calls: print {print.PrintLabel} isn't in the files.");
    }

    private void DeliverCoroner()
    {
        var murder = Case.Truth;
        var death = murder.DeathTime.Value;
        var rng = Simulation.Core.Rng.For(World.Seed, Simulation.Core.Purpose.Scene, murder.Id, 2);
        var center = death.AddMinutes(rng.Next(-20, 21));
        var from = Round(center.AddMinutes(-45));
        var to = Round(center.AddMinutes(60));
        string weapon = murder.Weapon switch
        {
            WeaponKind.Knife => "a single-edged knife, about seven inches",
            WeaponKind.BluntObject => "a heavy object, swung more than once",
            _ => $"a bullet, recovered: {(State.Items[murder.WeaponItemId.Value].Name.Contains(".38") ? ".38 caliber" : "9mm")}"
        };
        AddClue(new Clue
        {
            Id = NextClueId(), Kind = ClueKind.Coroner, Title = "Coroner's report", FoundAt = Now, Source = "Medical examiner",
            Text = $"Death caused by {weapon}. Stomach contents and lividity narrow the time of death to between {DayClock(from)} and {DayClock(to)}.",
            TimeFrom = from, TimeTo = to, AboutPersonId = murder.VictimId
        });
        Say("Hayes calls: the coroner's report is in.");
    }

    private static string DayClock(DateTime t) => Simulation.Observers.Describe.Clock(t) + " " + t.ToString("dddd");

    private void DeliverLab(Item item)
    {
        var lines = new List<string>();
        var prints = Prints.Lift(item.Id, Now);
        var known = prints.Where(p => People.TryGetValue(p.PersonId, out var k) && k.ReferencePrints || State.People[p.PersonId].Has(Traits.CriminalRecord)).ToList();
        if (prints.Count == 0) lines.Add("No usable prints.");
        foreach (var p in prints)
        {
            var person = State.People[p.PersonId];
            bool identifiable = known.Contains(p);
            lines.Add(identifiable ? $"A print matching {person.FullName}." : "An unidentified print.");
            if (identifiable)
            {
                var clue = AddClue(new Clue
                {
                    Id = NextClueId(), Kind = ClueKind.PrintMatch, Title = $"{item.Name}: {person.FullName}'s print",
                    Text = $"The lab found {person.FullName}'s print on the {item.Name}.", FoundAt = Now, Source = "Police lab",
                    AboutPersonId = person.Id, MatchedPersonId = person.Id, PrintPersonId = person.Id, SurfaceId = item.Id,
                    SurfaceName = item.Name, OnWeapon = Case != null && Case.Truth.WeaponItemId == item.Id
                });
                clue.NamedPersonIds.Add(person.Id);
            }
        }
        if (item.Bloody && Case != null)
        {
            bool victims = Case.Truth.WeaponItemId == item.Id || Case.Truth.BloodyClothesItemId == item.Id;
            lines.Add(victims ? "The blood is human, type matching the victim." : "The stains are not blood.");
            AddClue(new Clue
            {
                Id = NextClueId(), Kind = ClueKind.Lab, Title = $"Lab: {item.Name}", FoundAt = Now, Source = "Police lab",
                Text = string.Join(" ", lines), ItemId = item.Id, Bloody = victims,
                FoundAtHomeOfPersonId = Clues.FirstOrDefault(c => c.ItemId == item.Id)?.FoundAtHomeOfPersonId
            });
        }
        if (item.ItemType == ItemType.Handgun && Case?.Truth.Weapon == WeaponKind.Handgun)
        {
            bool match = Case.Truth.WeaponItemId == item.Id;
            AddClue(new Clue
            {
                Id = NextClueId(), Kind = ClueKind.Ballistics, Title = $"Ballistics: {item.Name}", FoundAt = Now, Source = "Police lab",
                Text = match ? "Test-fired. The rifling matches the bullet taken from the victim." : "Test-fired. No match with the bullet from the victim.",
                ItemId = item.Id, Confirms = match,
                FoundAtHomeOfPersonId = Clues.FirstOrDefault(c => c.ItemId == item.Id)?.FoundAtHomeOfPersonId
            });
            lines.Add(match ? "Ballistics: a match." : "Ballistics: no match.");
        }
        Say($"Hayes calls about the {item.Name}: {string.Join(" ", lines)}");
    }

    // ---- wiretaps ----

    public string BuyWiretap()
    {
        if (Cash < 150) return "You can't afford it.";
        if (!CanUsePhone()) return "You need a phone to place the order.";
        Cash -= 150;
        Spend(TimeSpan.FromMinutes(45));
        Wiretaps++;
        return "Your supplier sends a kid round with a tap. $150.";
    }

    public string PlantWiretap(int phoneFixtureId)
    {
        if (Wiretaps <= 0) return "You don't have a wiretap.";
        var phone = State.Fixtures[phoneFixtureId];
        int loc = World.Phones.PhoneLocation(phone);
        if (LocationId != loc) return "You need to be at that phone.";
        if (PeopleHere().Count > 0) return "Not with people watching.";
        if (World.Phones.Taps.ContainsKey(phone.Id)) return "There's already a tap on this line.";
        Wiretaps--;
        string where = State.PlaceName(AddressId.Value);
        ActiveTaps.Add((phone.Id, Now, where));
        World.Phones.Taps[phone.Id] = call => OnTappedCall(phone, where, call);
        Spend(TimeSpan.FromMinutes(10));
        return $"You unscrew the mouthpiece and fit the tap. Every call on {phone.PhoneNumber} will be recorded.";
    }

    private void OnTappedCall(Fixture phone, string where, CallFact call)
    {
        if (call.CallerId == Agent.Id) return;
        var caller = State.People[call.CallerId];
        string text = call.Outcome switch
        {
            CallOutcome.Answered => call.Transcript,
            CallOutcome.AnsweringMachine => $"(message) {call.Transcript}",
            _ => "(no answer)"
        };
        var clue = AddClue(new Clue
        {
            Id = NextClueId(), Kind = ClueKind.Wiretap, Title = $"Wiretap {where}, {call.Time:ddd HH:mm}", Text = text, FoundAt = call.Time,
            Source = $"Tap on {phone.PhoneNumber}", CallerId = call.CallerId, CallTime = call.Time, AboutPersonId = call.CallerId
        });
        clue.NamedPersonIds.Add(call.CallerId);
        if (call.AnsweredById.HasValue) clue.NamedPersonIds.Add(call.AnsweredById.Value);
        Say($"WIRETAP ({where}): {text}");
    }
}
