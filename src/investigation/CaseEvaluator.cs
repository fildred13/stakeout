using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Core;
using Stakeout.Simulation.Crime;
using Stakeout.Simulation.Diagnostics;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.History;
using Stakeout.Simulation.Observers;

namespace Stakeout.Investigation;

public enum Pillar { Opportunity, Motive, Physical, Deception }

public record Finding(Pillar Pillar, int Points, string Text, int ClueId);

/// <summary>The district attorney's reading of a case against one suspect.</summary>
public class CaseAssessment
{
    public int AccusedId { get; init; }
    public List<Finding> Findings { get; } = new();
    public List<(int Points, string Text)> Doubts { get; } = new();
    public Dictionary<Pillar, int> Scores { get; } = new();
    public int Total { get; set; }
    public double Probability { get; set; }
    public List<string> Missing { get; } = new();
    public string Summary { get; set; }
}

public class Verdict
{
    public CaseAssessment Assessment { get; init; }
    public bool Charged { get; init; }
    public bool Convicted { get; init; }
    public bool RightPerson { get; init; }
    public string Outcome { get; init; }
    public List<string> Truth { get; } = new();
}

/// <summary>
/// Weighs the evidence the player chose against the accused, like a DA would: each clue that really points at
/// them adds points to one of four pillars (opportunity, motive, physical, deception), capped per pillar; missing
/// pillars cap the odds; an alibi that holds up counts against. Whether a clue points at the accused is decided
/// by the truth behind it (whose print, which car), but only clues the player found and chose count.
/// </summary>
public static class CaseEvaluator
{
    private const int PillarCap = 60;

    public static CaseAssessment Assess(Detective d, int accusedId, IEnumerable<int> clueIds)
    {
        var state = d.State;
        var world = d.World;
        var accused = state.People[accusedId];
        var murder = d.Case.Truth;
        var chosen = clueIds.Select(id => d.Clues.First(c => c.Id == id)).ToList();
        var a = new CaseAssessment { AccusedId = accusedId };
        foreach (Pillar p in Enum.GetValues<Pillar>()) a.Scores[p] = 0;

        // The time of death as the police know it: the coroner if available, the scene estimate otherwise.
        var tod = d.Clues.Where(c => c.Kind == ClueKind.Coroner).Concat(d.Clues.Where(c => c.Kind == ClueKind.Body)).FirstOrDefault();
        var todFrom = tod?.TimeFrom ?? d.Case.NightStart;
        var todTo = tod?.TimeTo ?? d.Case.NightEnd;
        bool InWindow(DateTime t, double slackHours = 1.5) => t >= todFrom.AddHours(-slackHours) && t <= todTo.AddHours(slackHours);
        string pronoun = accused.HeShe;
        string name = accused.FullName;
        bool close = state.GetRelationship(accusedId, murder.VictimId) != null;
        var sceneAddress = state.Addresses[murder.SceneAddressId];
        bool NearScene(int addressId)
        {
            var x = state.Addresses[addressId];
            return Math.Abs(x.GridX - sceneAddress.GridX) + Math.Abs(x.GridY - sceneAddress.GridY) <= 5;
        }
        var car = accused.VehicleId.HasValue ? state.Vehicles[accused.VehicleId.Value] : null;

        void Add(Pillar pillar, int points, string text, Clue c) => a.Findings.Add(new Finding(pillar, points, text, c.Id));

        bool liedAboutWhereabouts = false;
        var countedPrints = new HashSet<(int, int)>();
        int motiveStatements = 0;
        bool placedAway = false;
        var alibi = chosen.FirstOrDefault(c => c.Kind == ClueKind.Alibi && c.AboutPersonId == accusedId)?.Alibi;

        foreach (var c in chosen)
        {
            switch (c.Kind)
            {
                case ClueKind.PrintMatch or ClueKind.Print when c.MatchedPersonId == accusedId && (c.AtScene || c.OnWeapon)
                                                                && countedPrints.Add((c.SurfaceId ?? 0, accusedId)):
                    bool forcedDoor = c.SurfaceId.HasValue && world.History.ForcedEntries.Any(f => f.AccessPointId == c.SurfaceId && f.PersonId != d.Agent.Id)
                                      && d.Clues.Any(x => x.Kind == ClueKind.Scene && x.Text.Contains("orced"));
                    if (c.OnWeapon) Add(Pillar.Physical, 35, $"{name}'s print is on the murder weapon.", c);
                    else if (forcedDoor) Add(Pillar.Physical, 22, $"{name}'s print is on the {c.SurfaceName}, the door that was forced.", c);
                    else if (c.AtScene) Add(Pillar.Physical, close ? 8 : 15,
                        $"{name}'s print was on the {c.SurfaceName} at the scene{(close ? " (the defense will say " + pronoun + " visited before)" : "")}.", c);
                    break;

                case ClueKind.Item when c.FoundAtHomeOfPersonId == accusedId && c.Bloody:
                {
                    var item = state.Items[c.ItemId.Value];
                    int pts = item.ItemType == ItemType.BloodyClothes ? 25 : 30;
                    Add(Pillar.Physical, pts, $"Bloodied {item.Name.ToLowerInvariant()} found in {name}'s home.", c);
                    break;
                }
                case ClueKind.Lab when c.Bloody && c.FoundAtHomeOfPersonId == accusedId:
                    Add(Pillar.Physical, 10, "The lab matched the blood to the victim's type.", c);
                    break;
                case ClueKind.Ballistics when c.Confirms == true:
                {
                    var gun = state.Items[c.ItemId.Value];
                    if (gun.OwnerPersonId == accusedId || c.FoundAtHomeOfPersonId == accusedId)
                        Add(Pillar.Physical, 40, $"Ballistics: the bullet came from {name}'s gun.", c);
                    break;
                }
                case ClueKind.Registry when c.AboutPersonId == accusedId && c.ItemId.HasValue && murder.Weapon == WeaponKind.Handgun:
                {
                    var gun = state.Items[c.ItemId.Value];
                    var used = state.Items[murder.WeaponItemId.Value];
                    if (gun.Name.Contains(".38") == used.Name.Contains(".38"))
                        Add(Pillar.Physical, 8, $"{name} owns a gun of the right caliber.", c);
                    break;
                }
                case ClueKind.Item when c.AtScene && state.Items.TryGetValue(c.ItemId ?? -1, out var butt) && butt.ItemType == ItemType.CigaretteButt
                                        && accused.CigaretteBrand != null && butt.Name.StartsWith(accused.CigaretteBrand):
                    Add(Pillar.Physical, 5, $"A {accused.CigaretteBrand} butt at the scene; {name} smokes {accused.CigaretteBrand}.", c);
                    break;

                case ClueKind.Sighting when c.Sighting != null:
                {
                    var s = c.Sighting;
                    if (s.Kind == SightingKind.Person && s.KnownByName && s.PersonId == accusedId)
                    {
                        bool inWindow = InWindow(s.TrueTime);
                        Add(Pillar.Opportunity, inWindow ? 30 : 10, $"{state.People[s.WitnessId].FullName} saw {name} there, {s.When}.", c);
                        placedAway = true;
                    }
                    else if (s.Kind == SightingKind.Vehicle && car != null && s.VehicleId == car.Id)
                    {
                        bool named = s.KnownByName || s.VehicleText.Contains(car.Make);
                        Add(Pillar.Opportunity, named ? 20 : 6, $"A car {(named ? "matching" : "consistent with")} {name}'s {car.Description} was seen outside, {s.When}.", c);
                        placedAway |= named;
                    }
                    else if (s.Kind == SightingKind.Person && !s.KnownByName && s.Description != null && s.Description.Fit(accused) >= 0.75)
                        Add(Pillar.Opportunity, 6, $"The description \"{s.Description.Text}\" fits {name}.", c);
                    break;
                }
                case ClueKind.Lineup when c.AboutPersonId == accusedId && c.Identified == true:
                {
                    bool inWindow = c.TimeFrom.HasValue && InWindow(c.TimeFrom.Value);
                    Add(Pillar.Opportunity, inWindow ? 30 : 12, $"{state.People[c.WitnessId.Value].FullName} picked out {name} as the person {(c.Sighting?.Where ?? "near the scene")}.", c);
                    placedAway = true;
                    break;
                }
                case ClueKind.Camera when c.Footage != null && c.CameraAddressId.HasValue && NearScene(c.CameraAddressId.Value):
                {
                    var hit = c.Footage.Where(r => InWindow(r.Time, 2)).FirstOrDefault(r => (car != null && r.VehicleId == car.Id) || r.PersonIds.Contains(accusedId));
                    if (hit == null) break;
                    int pts;
                    string how;
                    if (hit.VehicleId.HasValue && car != null && hit.VehicleId == car.Id)
                    {
                        pts = hit.Night ? 5 : hit.PlateSeen != null ? 22 : 12;
                        how = hit.Night ? $"a car shaped like {name}'s" : hit.PlateSeen != null ? $"{name}'s car, plate {hit.PlateSeen}..." : $"a {car.Color} {car.Make} like {name}'s";
                    }
                    else
                    {
                        pts = hit.Descriptions.Any(x => x.Fit(accused) >= 0.75) ? 12 : 4;
                        how = $"someone who looks like {name}";
                    }
                    Add(Pillar.Opportunity, pts, $"A camera near the scene filmed {how} at {hit.Time:HH:mm}.", c);
                    placedAway |= pts >= 12;
                    break;
                }
                case ClueKind.Tape when c.CallerId == accusedId && c.Text.Contains(accused.FirstName):
                    Add(Pillar.Opportunity, 25, $"The tape: {name} told the victim {pronoun} was coming over that night.", c);
                    break;
                case ClueKind.PhoneRecord when c.Calls != null && accused.HomePhoneId.HasValue:
                {
                    var death = murder.DeathTime ?? d.Case.NightEnd;
                    var call = c.Calls.FirstOrDefault(x => x.OtherPhoneId == accused.HomePhoneId && x.Time <= death && x.Time >= death.AddHours(-14));
                    if (call != default)
                        Add(Pillar.Opportunity, 10, $"Phone records: a call between the victim and {name} at {call.Time:HH:mm} that day.", c);
                    var after = c.Calls.FirstOrDefault(x => x.OtherPhoneId == accused.HomePhoneId && x.Time > death);
                    _ = after;
                    break;
                }
                case ClueKind.Wiretap when c.CallerId == accusedId && (c.Text.Contains("story") || c.Text.Contains("If anybody asks") || c.Text.Contains("Remember what we said")):
                    Add(Pillar.Deception, 35, $"On a wiretap, {name} tells a friend to stick to a false story.", c);
                    liedAboutWhereabouts = true;
                    break;
                case ClueKind.AlibiCheck when c.AboutPersonId == accusedId && c.Confirms == false && alibi != null && alibi.WitnessIds.Contains(c.WitnessId ?? -1):
                    Add(Pillar.Deception, 30, $"{name}'s alibi fell apart: {state.People[c.WitnessId.Value].FullName} says {pronoun} wasn't there.", c);
                    liedAboutWhereabouts = true;
                    break;
                case ClueKind.Motive when c.MotivePersonId == accusedId && motiveStatements++ < 2:
                    Add(Pillar.Motive, 15, $"{state.People[c.WitnessId.Value].FullName}: {Trim(c.Text)}", c);
                    break;
                case ClueKind.Document when c.MotivePersonId == accusedId:
                    Add(Pillar.Motive, c.NamedPersonIds.Contains(accusedId) ? 25 : 12,
                        c.NamedPersonIds.Contains(accusedId) ? $"In writing: {c.Title.ToLowerInvariant()} from {name}." : $"{c.Title}, signed with {name}'s initial.", c);
                    break;
            }
        }

        if (alibi != null)
        {
            var alibiClue = chosen.First(c => c.Kind == ClueKind.Alibi && c.AboutPersonId == accusedId);
            if (alibi.AloneAtHome && placedAway)
            {
                a.Findings.Add(new Finding(Pillar.Deception, 25, $"{name} claimed to be home alone, but the evidence puts {accused.HimHer} out.", alibiClue.Id));
                liedAboutWhereabouts = true;
            }
            else if (alibi.AloneAtHome)
                a.Findings.Add(new Finding(Pillar.Opportunity, 5, $"Nobody can vouch for {name} that night.", alibiClue.Id));
        }

        // An alibi that holds up is the defense's best friend, unless it is exposed as a lie.
        foreach (var c in chosen.Where(c => c.Kind == ClueKind.AlibiCheck && c.AboutPersonId == accusedId && c.Confirms == true))
        {
            bool covers = c.TimeFrom.HasValue && c.TimeTo.HasValue && c.TimeFrom <= todTo && c.TimeTo >= todFrom;
            if (!covers) continue;
            bool exposed = liedAboutWhereabouts && chosen.Any(w => w.Kind == ClueKind.Wiretap && w.CallerId == accusedId);
            if (!exposed) a.Doubts.Add((30, $"{state.People[c.WitnessId.Value].FullName} swears {name} was with them when it happened."));
        }

        foreach (var f in a.Findings) a.Scores[f.Pillar] += f.Points;
        foreach (Pillar p in Enum.GetValues<Pillar>()) a.Scores[p] = Math.Min(a.Scores[p], PillarCap);
        int total = a.Scores.Values.Sum() - a.Doubts.Sum(x => x.Points);
        a.Total = Math.Max(0, total);
        double prob = 1 - Math.Exp(-a.Total / 55.0);
        // "Nobody can vouch for them" alone does not put anyone at a scene.
        int realOpportunity = a.Findings.Where(f => f.Pillar == Pillar.Opportunity && f.Points > 5).Sum(f => f.Points);
        bool opportunity = realOpportunity >= 10 || a.Scores[Pillar.Deception] > 0;
        if (a.Scores[Pillar.Motive] == 0)
        {
            prob = Math.Min(prob, 0.4);
            a.Missing.Add($"Why would {name} do it? Show me a motive.");
        }
        if (!opportunity)
        {
            prob = Math.Min(prob, 0.3);
            a.Missing.Add($"Nothing puts {name} at the scene that night, or catches {accused.HimHer} lying about it.");
        }
        if (a.Scores[Pillar.Physical] == 0)
        {
            if (a.Scores[Pillar.Opportunity] < 30) prob = Math.Min(prob, 0.6);
            a.Missing.Add("No physical evidence: prints, the weapon, bloody clothes.");
        }
        a.Probability = Math.Round(prob, 2);
        a.Summary = a.Probability switch
        {
            >= 0.85 => "\"This is a lock. I'll take it to the grand jury tomorrow.\"",
            >= 0.65 => "\"It's a good case. A jury should convict.\"",
            >= 0.5 => "\"It's a coin toss in front of a jury. I'd rather have more.\"",
            >= 0.3 => "\"You've got a theory, not a case.\"",
            _ => "\"I can't charge anybody on this.\""
        };
        return a;
    }

    private static string Trim(string s) => s.Length > 120 ? s[..117] + "..." : s;

    /// <summary>Hands the case to the DA. The verdict and the truth of what happened.</summary>
    public static Verdict Submit(Detective d, int accusedId, IEnumerable<int> clueIds)
    {
        var a = Assess(d, accusedId, clueIds);
        var murder = d.Case.Truth;
        bool charged = a.Probability >= 0.5;
        bool convicted = charged && Rng.Unit(d.World.Seed, Purpose.Interview, murder.Id, accusedId, a.Total) < a.Probability;
        bool right = accusedId == murder.KillerId;
        var accused = d.State.People[accusedId];
        string outcome = !charged ? $"The DA won't charge {accused.FullName}. The case stays open."
            : convicted ? $"{accused.FullName} is convicted of murder." : $"{accused.FullName} is tried and acquitted.";
        var verdict = new Verdict { Assessment = a, Charged = charged, Convicted = convicted, RightPerson = right, Outcome = outcome };
        verdict.Truth.AddRange(Reconstruct(d.World, murder));
        d.Case.Closed = true;
        d.Case.Verdict = verdict;
        return verdict;
    }

    /// <summary>What really happened, read straight from History.</summary>
    public static List<string> Reconstruct(World world, MurderCase murder)
    {
        var state = world.State;
        var killer = state.People[murder.KillerId];
        var victim = state.People[murder.VictimId];
        var lines = new List<string>
        {
            $"The killer was {killer.FullName}.",
            murder.MotiveSummary(state),
            murder.Approach switch
            {
                MurderApproach.Invited => $"{killer.FirstName} phoned ahead and {victim.FirstName} let {killer.HimHer} in.",
                MurderApproach.Uninvited => $"{killer.FirstName} turned up unannounced; {victim.FirstName} opened the door.",
                _ => $"{killer.FirstName} broke in while {victim.FirstName} slept."
            },
            $"Weapon: {state.Items[murder.WeaponItemId.Value].Name.ToLowerInvariant()}, " + murder.WeaponFate switch
            {
                WeaponFate.LeftAtScene => "dropped at the scene.",
                WeaponFate.Dumped => murder.WeaponFixtureId.HasValue ? $"dumped in a trash can at {state.PlaceName(state.Locations[state.LocationOfFixture(state.Fixtures[murder.WeaponFixtureId.Value])].AddressId)}." : "thrown away.",
                _ => $"hidden in {killer.FirstName}'s home."
            },
            murder.WipedPrints ? $"{killer.FirstName} wiped some surfaces clean." : $"{killer.FirstName} didn't think to wipe anything.",
        };
        if (murder.AlibiAgreed && murder.AccompliceId.HasValue)
            lines.Add($"{state.People[murder.AccompliceId.Value].FullName} agreed to lie about where {killer.FirstName} was.");
        lines.Add($"{killer.FirstName}'s night, from the record:");
        var from = (murder.DeathTime ?? murder.PlannedFor).AddHours(-3);
        lines.AddRange(Timeline.ForPerson(world, killer.Id, from, from.AddHours(6)).Select(l => "  " + l.Text));
        return lines;
    }
}
