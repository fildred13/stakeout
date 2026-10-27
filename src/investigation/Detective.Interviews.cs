using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Core;
using Stakeout.Simulation.Crime;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.History;
using Stakeout.Simulation.Observers;

namespace Stakeout.Investigation;

public enum Topic { Introduce, Victim, Whereabouts, SawAnything, Enemies, AboutPerson, ShowPerson, Prints, PalmGlass, ConfirmAlibi }

public class InterviewResult
{
    public string Answer { get; set; }
    public List<Clue> Clues { get; } = new();
}

public partial class Detective
{
    private static readonly TimeSpan QuestionTime = TimeSpan.FromMinutes(4);

    /// <summary>Everything each person has been asked and what they answered, in order.</summary>
    public Dictionary<int, List<(Topic Topic, int? Subject, string Answer)>> Transcripts { get; } = new();

    private bool IsKiller(int personId) => Case != null && Case.Truth.KillerId == personId;
    private bool IsAccompliceFor(int personId, int killerId) =>
        Case != null && Case.Truth.KillerId == killerId && Case.Truth.AlibiAgreed && Case.Truth.AccompliceId == personId;

    /// <summary>Asks someone the detective can talk to about a topic. Time passes; answers come from what they saw and know, or from their lies.</summary>
    public InterviewResult Ask(int personId, Topic topic, int? subjectId = null)
    {
        if (!CanTalkTo(personId))
            return new InterviewResult { Answer = "They are not here." };
        var person = State.People[personId];
        var known = Learn(personId);
        bool firstMeeting = !known.Met;
        known.Met = true;
        var result = topic switch
        {
            Topic.Introduce => Introduce(person),
            Topic.Victim => AboutVictim(person),
            Topic.Whereabouts => Whereabouts(person),
            Topic.SawAnything => SawAnything(person),
            Topic.Enemies => Enemies(person),
            Topic.AboutPerson => AboutPerson(person, subjectId.Value),
            Topic.ShowPerson => ShowPerson(person, subjectId.Value),
            Topic.Prints => AskForPrints(person),
            Topic.PalmGlass => PalmGlass(person),
            Topic.ConfirmAlibi => ConfirmAlibi(person, subjectId.Value),
            _ => new InterviewResult { Answer = "..." }
        };
        if (firstMeeting && topic != Topic.Introduce)
            result.Answer = $"(\"{person.FullName}.\") {result.Answer}";
        foreach (var c in result.Clues) AddClue(c);
        if (!Transcripts.TryGetValue(personId, out var transcript)) Transcripts[personId] = transcript = new();
        transcript.Add((topic, subjectId, result.Answer));
        if (IsKiller(personId) && topic != Topic.Introduce && Case.QuestionedSuspects.Add(personId))
            World.Crimes.OnSuspectQuestioned(personId);
        Spend(QuestionTime);
        return result;
    }

    private Clue Statement(Person witness, ClueKind kind, string title, string text) => new()
    {
        Id = NextClueId(), Kind = kind, Title = title, Text = text, FoundAt = Now, WitnessId = witness.Id,
        Source = $"Interview with {witness.FullName}"
    };

    private InterviewResult Introduce(Person p)
    {
        var known = Learn(p.Id);
        var parts = new List<string> { $"I'm {p.FullName}." };
        if (AddressId == p.HomeAddressId)
        {
            known.AddressKnown = true;
            var home = State.Locations[p.HomeLocationId];
            parts.Add(State.Addresses[p.HomeAddressId].Type == AddressType.ApartmentBuilding ? $"I live in {home.Name}." : "This is my house.");
        }
        if (p.BusinessId.HasValue)
        {
            var biz = State.Businesses[p.BusinessId.Value];
            var pos = State.PositionOf(p);
            known.EmployerKnown = true;
            parts.Add(AddressId == biz.AddressId ? $"I work here, {Article(pos.Role)}." : $"I work at {biz.Name}, {Article(pos.Role)}.");
            KnownAddresses.Add(biz.AddressId);
        }
        return new InterviewResult { Answer = string.Join(" ", parts) };
    }

    private static string Article(string role)
    {
        string r = role.Replace('_', ' ');
        return (r.Length > 0 && "aeiou".Contains(r[0]) ? "an " : "a ") + r;
    }

    // ---- the victim ----

    private string RelationTo(int personId, int otherId, out bool close)
    {
        close = false;
        var rel = State.GetRelationship(personId, otherId);
        var p = State.People[personId];
        var o = State.People[otherId];
        if (rel != null)
        {
            close = rel.Type != RelationshipType.Ex;
            return rel.Type switch
            {
                RelationshipType.Partner => "partner",
                RelationshipType.Dating => "dating",
                RelationshipType.Ex => "ex",
                RelationshipType.Affair => "affair",
                _ => "friend"
            };
        }
        if (p.BusinessId.HasValue && p.BusinessId == o.BusinessId) return "coworker";
        if (p.HomeAddressId == o.HomeAddressId) return "neighbor";
        var a = State.Addresses[p.HomeAddressId];
        var b = State.Addresses[o.HomeAddressId];
        if (Math.Abs(a.GridX - b.GridX) + Math.Abs(a.GridY - b.GridY) <= 2) return "neighbor";
        return null;
    }

    private DateTime? LastSawAlive(int personId, int victimId)
    {
        var death = Case?.Truth.DeathTime ?? Now;
        DateTime? last = null;
        foreach (var mine in World.History.PresenceOf(personId, death.AddDays(-14), death))
        {
            foreach (var v in World.History.PresenceOf(victimId, mine.Start, mine.EndOr(death)))
            {
                if (v.LocationId != mine.LocationId || v.Activity == ActivityKind.Dead) continue;
                var t = v.Start > mine.Start ? v.Start : mine.Start;
                if (last == null || t > last) last = t;
            }
        }
        foreach (var trip in World.History.TripsOf(personId).Where(t => t.Depart < death && t.Depart > death.AddDays(-14)))
            if (trip.PersonIds.Contains(victimId) && (last == null || trip.Depart > last)) last = trip.Depart;
        return last;
    }

    private static string DaysAgo(DateTime when, DateTime now)
    {
        int days = (now.Date - when.Date).Days;
        return days switch
        {
            0 => "earlier today",
            1 => "yesterday",
            < 7 => $"on {when:dddd}",
            _ => "a couple of weeks ago"
        };
    }

    private InterviewResult AboutVictim(Person p)
    {
        if (Case == null) return new InterviewResult { Answer = "Who?" };
        var victim = State.People[Case.VictimId];
        var rel = RelationTo(p.Id, victim.Id, out _);
        var r = new InterviewResult();
        string relText;
        if (IsKiller(p.Id))
        {
            relText = Case.Truth.Motive.Kind switch
            {
                TensionKind.Affair => $"I knew {victim.FirstName} a little. From around.",
                TensionKind.Jealousy => $"We went out for a while. It was over months ago. I've moved on.",
                TensionKind.Debt => $"{victim.FirstName} was a friend. We weren't close lately.",
                TensionKind.Embezzlement => $"We work together. That's all.",
                _ => $"I knew {victim.HimHer}."
            };
            r.Answer = relText + " I haven't seen " + victim.HimHer + " in weeks. Terrible thing.";
            return r;
        }
        relText = rel switch
        {
            "partner" => $"{victim.FirstName} was my partner.",
            "dating" => $"We were seeing each other. {victim.FirstName} and I...",
            "ex" => $"We used to go out. It ended a while ago.",
            "affair" => $"We were... close. Please keep that between us.",
            "friend" => $"{victim.FirstName} was a friend of mine.",
            "coworker" => $"We worked together.",
            "neighbor" => $"{victim.HeShe.Substring(0, 1).ToUpper() + victim.HeShe[1..]} lived {(p.HomeAddressId != victim.HomeAddressId ? "next door" : State.Locations[p.HomeLocationId].Floor == State.Locations[victim.HomeLocationId].Floor ? "down the hall" : "in the building")}. We'd say hello.",
            _ => $"I didn't really know {victim.HimHer}."
        };
        var last = LastSawAlive(p.Id, victim.Id);
        string lastText = last.HasValue ? $" Last time I saw {victim.HimHer} was {DaysAgo(last.Value, Now)}, around {Describe.Clock(last.Value)}." : "";
        // Friends know who the victim was seeing.
        var named = new List<int>();
        if (rel is "friend" or "coworker" or "partner")
        {
            foreach (var other in State.RelationshipsOf(victim.Id).Where(x => x.Type is RelationshipType.Dating or RelationshipType.Partner))
            {
                int o = other.Other(victim.Id);
                if (o == p.Id) continue;
                relText += $" {victim.FirstName} was seeing {State.People[o].FullName}.";
                named.Add(o);
            }
        }
        r.Answer = relText + lastText;
        var clue = Statement(p, ClueKind.Statement, $"{p.FullName} on {victim.FirstName}", r.Answer);
        clue.AboutPersonId = victim.Id;
        clue.NamedPersonIds.AddRange(named);
        r.Clues.Add(clue);
        return r;
    }

    // ---- alibis ----

    /// <summary>A person's real whereabouts during a window, as they would describe them.</summary>
    public (string Text, List<int> Witnesses, List<int> Places, bool AloneAtHome) TrueWhereabouts(int personId, DateTime from, DateTime to)
    {
        var p = State.People[personId];
        var segments = new List<(DateTime Start, DateTime End, int Address, ActivityKind Activity)>();
        foreach (var f in World.History.PresenceOf(personId, from, to).OrderBy(f => f.Start))
        {
            if (f.Activity is ActivityKind.Travel) continue;
            var s = f.Start < from ? from : f.Start;
            var e = f.EndOr(to) > to ? to : f.EndOr(to);
            if (segments.Count > 0 && segments[^1].Address == f.AddressId)
                segments[^1] = (segments[^1].Start, e, f.AddressId, segments[^1].Activity == ActivityKind.Sleep ? f.Activity : segments[^1].Activity);
            else
                segments.Add((s, e, f.AddressId, f.Activity));
        }
        var witnesses = new List<int>();
        var places = new List<int>();
        var phrases = new List<string>();
        bool alone = true;
        foreach (var seg in segments.Where(s => s.End - s.Start >= TimeSpan.FromMinutes(20)))
        {
            string when = $"from about {Describe.Clock(Round(seg.Start))} to {Describe.Clock(Round(seg.End))}";
            bool wholeNight = seg.Start <= from.AddMinutes(30) && seg.End >= to.AddMinutes(-30);
            if (seg.Address == p.HomeAddressId)
            {
                var guests = World.History.PresenceAt(seg.Address, seg.Start, seg.End)
                    .Where(x => x.PersonId != personId && x.LocationId == p.HomeLocationId && x.Activity == ActivityKind.Visit)
                    .Select(x => x.PersonId).Distinct().ToList();
                var housemates = State.Households[p.HouseholdId].MemberIds.Where(id => id != personId).ToList();
                if (guests.Count > 0)
                {
                    phrases.Add($"at home {when}, {string.Join(" and ", guests.Select(g => State.People[g].FullName))} came over");
                    witnesses.AddRange(guests);
                    alone = false;
                }
                else phrases.Add(wholeNight ? "home, all night" : $"home {when}");
                witnesses.AddRange(housemates.Where(h => World.History.PresenceAt(seg.Address, seg.Start, seg.End).Any(x => x.PersonId == h)));
                if (housemates.Count > 0) alone = false;
                continue;
            }
            alone = false;
            places.Add(seg.Address);
            var biz = State.BusinessAt(seg.Address);
            var hostHome = State.People.Values.FirstOrDefault(o => o.HomeAddressId == seg.Address && State.GetRelationship(personId, o.Id) != null
                && World.History.PresenceAt(seg.Address, seg.Start, seg.End).Any(x => x.PersonId == o.Id));
            string companions = string.Join(" and ", World.History.TripsOf(personId)
                .Where(t => t.ToAddressId == seg.Address && t.Arrive <= seg.Start.AddMinutes(2) && t.Arrive >= seg.Start.AddMinutes(-2))
                .SelectMany(t => t.PersonIds).Where(id => id != personId).Distinct().Select(id => State.People[id].FullName));
            if (companions.Length > 0)
                witnesses.AddRange(World.History.TripsOf(personId).Where(t => t.ToAddressId == seg.Address && Math.Abs((t.Arrive - seg.Start).TotalMinutes) <= 2)
                    .SelectMany(t => t.PersonIds).Where(id => id != personId));
            string with = companions.Length > 0 ? $" with {companions}" : "";
            if (seg.Activity == ActivityKind.Work && biz != null)
                phrases.Add($"at work at {biz.Name} {when}");
            else if (biz != null)
                phrases.Add($"at {biz.Name}{with} {when}");
            else if (hostHome != null)
            {
                phrases.Add($"at {hostHome.FullName}'s place {when}");
                witnesses.Add(hostHome.Id);
            }
            else phrases.Add($"at {State.PlaceName(seg.Address)}{with} {when}");
        }
        string text = phrases.Count == 0 ? "home, all night" : string.Join(", then ", phrases);
        return (text, witnesses.Distinct().ToList(), places.Distinct().ToList(), alone);
    }

    private static DateTime Round(DateTime t) => new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute / 15 * 15, 0);

    private InterviewResult Whereabouts(Person p)
    {
        if (Case == null) return new InterviewResult { Answer = "Why do you ask?" };
        var from = Case.NightStart;
        var to = Case.NightEnd;
        string night = from.ToString("dddd");
        AlibiClaim claim;
        if (IsKiller(p.Id))
        {
            var friend = Case.Truth.AlibiAgreed && Case.Truth.AccompliceId.HasValue ? State.People[Case.Truth.AccompliceId.Value] : null;
            claim = friend != null
                ? new AlibiClaim
                {
                    PersonId = p.Id, From = from, To = to, True = false,
                    Text = $"{night} night? I was at {friend.FullName}'s place from about eight until midnight. Then home to bed.",
                    WitnessIds = { friend.Id }, AddressIds = { friend.HomeAddressId }
                }
                : new AlibiClaim
                {
                    PersonId = p.Id, From = from, To = to, True = false, AloneAtHome = true,
                    Text = $"{night} night I was home. All night. Alone, watching television."
                };
        }
        else if (Case.Truth.AlibiAgreed && Case.Truth.AccompliceId == p.Id)
        {
            var killer = State.People[Case.Truth.KillerId];
            var truth = TrueWhereabouts(p.Id, from, to);
            claim = new AlibiClaim
            {
                PersonId = p.Id, From = from, To = to, True = false,
                Text = $"I was home. {killer.FirstName} {killer.LastName} came over around eight and we watched the game until about midnight.",
                WitnessIds = { killer.Id }
            };
            _ = truth;
        }
        else
        {
            var truth = TrueWhereabouts(p.Id, from, to);
            claim = new AlibiClaim
            {
                PersonId = p.Id, From = from, To = to, True = true, AloneAtHome = truth.AloneAtHome,
                Text = $"{night} night I was {truth.Text}."
            };
            claim.WitnessIds.AddRange(truth.Witnesses);
            claim.AddressIds.AddRange(truth.Places);
        }
        var clue = Statement(p, ClueKind.Alibi, $"{p.FullName}'s whereabouts {night} night", claim.Text);
        clue.AboutPersonId = p.Id;
        clue.NamedPersonIds.AddRange(claim.WitnessIds);
        clue.NamedAddressIds.AddRange(claim.AddressIds);
        var withAlibi = new Clue
        {
            Id = clue.Id, Kind = clue.Kind, Title = clue.Title, Text = clue.Text, FoundAt = clue.FoundAt,
            WitnessId = clue.WitnessId, Source = clue.Source, AboutPersonId = p.Id, Alibi = claim,
            TimeFrom = from, TimeTo = to
        };
        withAlibi.NamedPersonIds.AddRange(clue.NamedPersonIds);
        withAlibi.NamedAddressIds.AddRange(clue.NamedAddressIds);
        return new InterviewResult { Answer = claim.Text, Clues = { withAlibi } };
    }

    private InterviewResult ConfirmAlibi(Person witness, int subjectId)
    {
        if (Case == null) return new InterviewResult { Answer = "About what?" };
        var subject = State.People[subjectId];
        var from = Case.NightStart;
        var to = Case.NightEnd;
        string night = from.ToString("dddd");
        string answer;
        bool? confirms;
        bool lie = false;
        DateTime? seenFrom = null, seenTo = null;
        if (IsAccompliceFor(witness.Id, subjectId))
        {
            answer = $"{subject.FirstName}? Yes, {subject.HeShe} was at my place {night} night. From about eight until midnight.";
            confirms = true;
            lie = true;
            seenFrom = from.Date.AddHours(20);
            seenTo = from.Date.AddHours(24);
        }
        else if (IsKiller(witness.Id))
        {
            answer = $"{subject.FirstName}? I wouldn't know where {subject.HeShe} was.";
            confirms = false;
        }
        else
        {
            // Were they really together, was the witness awake, and do they remember?
            DateTime? first = null, last = null;
            bool togetherAsleep = false, witnessAround = false;
            foreach (var mine in World.History.PresenceOf(witness.Id, from, to))
            {
                if (mine.Activity != ActivityKind.Sleep && mine.Activity != ActivityKind.Travel) witnessAround = true;
                foreach (var theirs in World.History.PresenceOf(subjectId, mine.Start, mine.EndOr(to)))
                {
                    if (theirs.LocationId != mine.LocationId) continue;
                    var s = theirs.Start > mine.Start ? theirs.Start : mine.Start;
                    var e = theirs.EndOr(to) < mine.EndOr(to) ? theirs.EndOr(to) : mine.EndOr(to);
                    if (e <= s) continue;
                    if (mine.Activity == ActivityKind.Sleep) { togetherAsleep = true; continue; }
                    if (first == null || s < first) first = s;
                    if (last == null || e > last) last = e;
                }
            }
            bool knows = Memory.KnowsByName(witness.Id, subjectId) || State.People[subjectId].FavoriteBarIds.Contains(AddressId ?? -1);
            if (first != null && Rng.Chance(knows ? 0.95 : 0.6, World.Seed, Purpose.Interview, witness.Id, subjectId, 1))
            {
                confirms = true;
                seenFrom = first;
                seenTo = last;
                answer = $"{subject.FirstName} {subject.LastName}? Yes, {subject.HeShe} was with me {night}, from about {Describe.Clock(Round(first.Value))} to {Describe.Clock(Round(last.Value))}."
                         + (togetherAsleep ? " After that I was asleep." : "");
            }
            else if (first != null)
            {
                confirms = null;
                answer = $"{night} night? I honestly couldn't tell you. I don't remember.";
            }
            else if (togetherAsleep)
            {
                confirms = null;
                answer = $"{night} night? I was asleep, I couldn't tell you who was where.";
            }
            else if (witnessAround)
            {
                confirms = false;
                answer = $"{night} night? No. {subject.FirstName} wasn't with me.";
            }
            else
            {
                confirms = null;
                answer = $"{night} night? I wasn't even there.";
            }
        }
        var clue = Statement(witness, ClueKind.AlibiCheck, $"{witness.FullName} on {subject.FullName}'s whereabouts", answer);
        var full = new Clue
        {
            Id = clue.Id, Kind = clue.Kind, Title = clue.Title, Text = clue.Text, FoundAt = clue.FoundAt, WitnessId = witness.Id,
            Source = clue.Source, AboutPersonId = subjectId, Confirms = confirms, Lie = lie, TimeFrom = seenFrom, TimeTo = seenTo
        };
        return new InterviewResult { Answer = answer, Clues = { full } };
    }

    // ---- what they saw ----

    private bool IsUnusual(Sighting s, int sceneAddressId, int witnessId)
    {
        if (s.Kind == SightingKind.Noise) return true;
        if (s.Kind == SightingKind.Vehicle) return true;
        var who = State.People[s.PersonId.Value];
        return who.HomeAddressId != sceneAddressId || s.Where.StartsWith("at the door");
    }

    private InterviewResult SawAnything(Person p)
    {
        if (Case == null) return new InterviewResult { Answer = "Saw what?" };
        string night = Case.NightStart.ToString("dddd");
        if (IsKiller(p.Id)) return new InterviewResult { Answer = $"{night} night? I wasn't anywhere near there." };
        var sightings = Memory.Recall(p.Id, Case.SceneAddressId, Case.NightStart, Case.Truth.DiscoveredAt ?? Case.NightEnd, Now)
            .Where(s => IsUnusual(s, Case.SceneAddressId, p.Id))
            .Where(s => !(s.Kind == SightingKind.Person && s.PersonId == Case.Truth.DiscovererId && s.TrueTime >= (Case.Truth.DiscoveredAt ?? DateTime.MaxValue).AddMinutes(-5)))
            .ToList();
        var r = new InterviewResult();
        if (sightings.Count == 0)
        {
            bool nearby = World.History.PresenceOf(p.Id, Case.NightStart, Case.NightEnd).Any(f => f.AddressId == Case.SceneAddressId);
            r.Answer = nearby ? "I didn't notice anything. I keep to myself." : $"I wasn't around there {night} night.";
            return r;
        }
        r.Answer = string.Join(" ", sightings.Select(s => s.Text));
        foreach (var s in sightings)
        {
            var clue = new Clue
            {
                Id = NextClueId(), Kind = ClueKind.Sighting, Title = $"{p.FullName} saw/heard", Text = s.Text, FoundAt = Now,
                WitnessId = p.Id, Source = $"Interview with {p.FullName}", Sighting = s, TimeFrom = s.TrueTime, TimeTo = s.TrueTime,
                AboutPersonId = s.KnownByName ? s.PersonId : null
            };
            if (s.KnownByName && s.PersonId.HasValue) clue.NamedPersonIds.Add(s.PersonId.Value);
            r.Clues.Add(clue);
        }
        return r;
    }

    // ---- motives ----

    private static string MotiveText(Tension t, SimulationState state, int victimId)
    {
        var other = state.People[t.AggressorId == victimId ? t.TargetId : t.AggressorId];
        var v = state.People[victimId];
        bool victimIsTarget = t.TargetId == victimId;
        return t.Kind switch
        {
            TensionKind.Debt when victimIsTarget => $"{other.FullName} owed {v.FirstName} money, ${t.AmountDollars:N0}. {v.FirstName} was getting angry about it, talked about going to a lawyer.",
            TensionKind.Debt => $"{v.FirstName} owed {other.FullName} money, I think.",
            TensionKind.Jealousy when victimIsTarget => $"{v.FirstName}'s ex, {other.FullName}, wouldn't let go. Kept calling, turning up.",
            TensionKind.Jealousy => $"{v.FirstName} still carried a torch for {other.FullName}.",
            TensionKind.Affair when victimIsTarget => $"{v.FirstName} was seeing a married {(other.Appearance.Sex == Sex.Male ? "man" : "woman")}. {other.FullName}. Said {v.HeShe}'d had enough of the sneaking around and would tell the {(other.Appearance.Sex == Sex.Male ? "wife" : "husband")}.",
            TensionKind.Affair => $"{v.FirstName} was having an affair with {other.FullName}.",
            TensionKind.Embezzlement when victimIsTarget => $"{v.FirstName} told me {v.HeShe}'d found something at work. Money going missing. {v.HeShe.Substring(0, 1).ToUpper() + v.HeShe[1..]} named {other.FullName}.",
            TensionKind.Embezzlement => $"There was talk at work about {v.FirstName} and missing money.",
            _ => $"{other.FullName} and {v.FirstName} couldn't stand each other at work."
        };
    }

    private InterviewResult Enemies(Person p)
    {
        if (Case == null) return new InterviewResult { Answer = "Enemies?" };
        var victimId = Case.VictimId;
        var r = new InterviewResult();
        var known = State.Tensions.Values.Where(t => t.Involves(victimId) && (t.KnownBy.Contains(p.Id) || t.Involves(p.Id)))
            .OrderByDescending(t => t.Intensity).ToList();
        if (IsKiller(p.Id))
        {
            // Points elsewhere.
            known = known.Where(t => !t.Involves(p.Id)).ToList();
            var other = State.Tensions.Values.Where(t => t.Involves(victimId) && !t.Involves(p.Id)).OrderByDescending(t => t.Intensity).FirstOrDefault();
            if (known.Count == 0 && other != null) known.Add(other);
        }
        else
        {
            // Nobody volunteers their own motive.
            known = known.Where(t => t.AggressorId != p.Id).ToList();
        }
        if (known.Count == 0)
        {
            r.Answer = "Enemies? Not that I know of.";
            return r;
        }
        var lines = new List<string>();
        foreach (var t in known.Take(3))
        {
            string text = MotiveText(t, State, victimId);
            lines.Add(text);
            int other = t.AggressorId == victimId ? t.TargetId : t.AggressorId;
            var clue = new Clue
            {
                Id = NextClueId(), Kind = ClueKind.Motive, Title = $"{p.FullName} on trouble in {State.People[victimId].FirstName}'s life",
                Text = text, FoundAt = Now, WitnessId = p.Id, Source = $"Interview with {p.FullName}", MotiveKind = t.Kind,
                MotivePersonId = t.AggressorId == victimId ? null : other, AboutPersonId = other
            };
            clue.NamedPersonIds.Add(other);
            r.Clues.Add(clue);
        }
        r.Answer = string.Join(" ", lines);
        return r;
    }

    // ---- other people ----

    private InterviewResult AboutPerson(Person witness, int subjectId)
    {
        var subject = State.People[subjectId];
        var r = new InterviewResult();
        if (subjectId == witness.Id)
        {
            r.Answer = "What about me?";
            return r;
        }
        var rel = RelationTo(witness.Id, subjectId, out _);
        bool knows = rel != null && (rel != "neighbor" || Memory.KnowsByName(witness.Id, subjectId));
        if (!knows)
        {
            r.Answer = $"{subject.FullName}? Never heard of {subject.HimHer}.";
            return r;
        }
        var lines = new List<string>();
        lines.Add(rel switch
        {
            "partner" => $"{subject.FirstName}'s my partner.",
            "dating" => $"{subject.FirstName} and I are seeing each other.",
            "ex" => $"We used to go out.",
            "affair" => $"I know {subject.FirstName}. Leave it at that.",
            "friend" => $"{subject.FirstName}'s a friend.",
            "coworker" => $"We work together.",
            _ => $"{subject.FirstName} lives near me."
        });
        var known = Learn(subjectId);
        if (subject.BusinessId.HasValue && rel != "neighbor")
        {
            var biz = State.Businesses[subject.BusinessId.Value];
            lines.Add($"Works at {biz.Name}.");
            known.EmployerKnown = true;
            KnownAddresses.Add(biz.AddressId);
        }
        if (subject.VehicleId.HasValue)
        {
            var v = State.Vehicles[subject.VehicleId.Value];
            lines.Add($"Drives a {v.Color} {v.Make} {v.Model}.");
            known.VehicleKnown = true;
        }
        if (rel is "friend" or "partner" or "dating" && subject.FavoriteBarIds.Count > 0 && subject.Has(Traits.Drinker))
        {
            var bar = State.BusinessAt(subject.FavoriteBarIds[0]);
            if (bar != null)
            {
                lines.Add($"You'll find {subject.HimHer} at {bar.Name} most nights.");
                KnownAddresses.Add(bar.AddressId);
            }
        }
        if (rel is "friend" or "partner" or "dating" or "neighbor")
        {
            known.AddressKnown = true;
            lines.Add($"Lives at {State.AddressLine(subject.HomeAddressId)}.");
            KnownAddresses.Add(subject.HomeAddressId);
        }
        if (Case != null)
        {
            foreach (var t in State.Tensions.Values.Where(t => t.Involves(subjectId) && t.Involves(Case.VictimId) && t.KnownBy.Contains(witness.Id)))
            {
                string text = MotiveText(t, State, Case.VictimId);
                lines.Add(text);
                var motive = new Clue
                {
                    Id = NextClueId(), Kind = ClueKind.Motive, Title = $"{witness.FullName} on {subject.FullName}",
                    Text = text, FoundAt = Now, WitnessId = witness.Id, Source = $"Interview with {witness.FullName}",
                    MotiveKind = t.Kind, MotivePersonId = t.AggressorId == Case.VictimId ? null : subjectId, AboutPersonId = subjectId
                };
                motive.NamedPersonIds.Add(subjectId);
                r.Clues.Add(motive);
            }
        }
        r.Answer = string.Join(" ", lines);
        var statement = Statement(witness, ClueKind.Statement, $"{witness.FullName} on {subject.FullName}", r.Answer);
        statement.AboutPersonId = subjectId;
        statement.NamedPersonIds.Add(subjectId);
        r.Clues.Insert(0, statement);
        return r;
    }

    private InterviewResult ShowPerson(Person witness, int subjectId)
    {
        var subject = State.People[subjectId];
        var r = new InterviewResult();
        if (!People.TryGetValue(subjectId, out var k) || !k.Met)
        {
            r.Answer = $"(You haven't met {subject.FullName}; you can't describe {subject.HimHer} well enough.)";
            return r;
        }
        if (Case == null) return r;
        var sightings = Memory.Recall(witness.Id, Case.SceneAddressId, Case.NightStart, Case.Truth.DiscoveredAt ?? Case.NightEnd, Now)
            .Where(s => s.Kind == SightingKind.Person && !s.KnownByName && IsUnusual(s, Case.SceneAddressId, witness.Id)).ToList();
        string desc = Looks(subjectId);
        bool? identified;
        if (sightings.Count == 0)
        {
            r.Answer = "I didn't see anybody, I told you.";
            identified = null;
        }
        else if (IsKiller(witness.Id))
        {
            r.Answer = "No.";
            identified = false;
        }
        else
        {
            var match = sightings.FirstOrDefault(s => s.PersonId == subjectId);
            var lookalike = sightings.FirstOrDefault(s => s.PersonId != subjectId && s.Description.Fit(subject) >= 0.8);
            if (match != null && Rng.Chance(0.85, World.Seed, Purpose.Interview, witness.Id, subjectId, 2))
            {
                r.Answer = $"{desc}... Yes. That's who I saw, {match.When}. I'm sure of it.";
                identified = true;
            }
            else if (lookalike != null && Rng.Chance(0.3, World.Seed, Purpose.Interview, witness.Id, subjectId, 3))
            {
                r.Answer = $"{desc}... Could be. I think so. It was {(Describe.IsDark(lookalike.TrueTime) ? "dark" : "quick")}.";
                identified = true;
                match = lookalike;
            }
            else
            {
                r.Answer = "No, that's not who I saw.";
                identified = false;
            }
            var clue = new Clue
            {
                Id = NextClueId(), Kind = ClueKind.Lineup, Title = $"{witness.FullName} shown {subject.FullName}", Text = r.Answer,
                FoundAt = Now, WitnessId = witness.Id, Source = $"Interview with {witness.FullName}", AboutPersonId = subjectId,
                Identified = identified, Sighting = match, TimeFrom = match?.TrueTime, TimeTo = match?.TrueTime,
                Lie = identified == true && match?.PersonId != subjectId
            };
            r.Clues.Add(clue);
        }
        return r;
    }

    /// <summary>Offers a cigarette or a glass of water and keeps what they touched: prints without asking.</summary>
    private InterviewResult PalmGlass(Person p)
    {
        var known = Learn(p.Id);
        var r = new InterviewResult();
        if (known.ReferencePrints)
        {
            r.Answer = "(You already have their prints.)";
            return r;
        }
        bool tried = !Once($"palm{p.Id}");
        if (!tried && Rng.Chance(0.75, World.Seed, Purpose.Interview, p.Id, 5))
        {
            known.ReferencePrints = true;
            r.Answer = $"(You offer {p.HimHer} a cigarette and a light, then pocket the pack {p.HeShe} handed back. Clean prints.)";
            r.Clues.AddRange(MatchPrintsFor(p.Id));
        }
        else r.Answer = tried
            ? $"({p.FirstName} watches your hands now. Not again.)"
            : $"({p.FirstName} waves the offer away. No luck.)";
        return r;
    }

    private InterviewResult AskForPrints(Person p)
    {
        var known = Learn(p.Id);
        var r = new InterviewResult();
        if (known.ReferencePrints)
        {
            r.Answer = "You already have my prints.";
            return r;
        }
        double p0 = IsKiller(p.Id) ? 0.2 : Case != null && Case.Truth.AccompliceId == p.Id ? 0.5 : 0.8;
        if (Rng.Chance(p0, World.Seed, Purpose.Interview, p.Id, 4))
        {
            known.ReferencePrints = true;
            r.Answer = "Fine. If it helps. (You roll each finger on the card.)";
            r.Clues.AddRange(MatchPrintsFor(p.Id));
        }
        else
        {
            r.Answer = "My fingerprints? Am I a suspect? No. Not without a lawyer.";
            var clue = Statement(p, ClueKind.Refusal, $"{p.FullName} refused fingerprints", r.Answer);
            clue.AboutPersonId = p.Id;
            r.Clues.Add(clue);
        }
        return r;
    }
}
