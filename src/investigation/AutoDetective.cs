using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;

namespace Stakeout.Investigation;

/// <summary>
/// A scripted investigator that plays through the same API as the player and uses only what the player could
/// know (clues, the phone book, the city map). Used by tests to prove cases are solvable, and as a debugging aid.
/// It never reads the crime record or History.
/// </summary>
public class AutoDetective
{
    private readonly Detective _d;
    private readonly HashSet<int> _interviewed = new();
    private readonly HashSet<(int, int)> _shown = new();
    private readonly HashSet<(int, int)> _confirmed = new();
    public List<string> Steps { get; } = new();
    public int MaxDays { get; set; } = 4;

    public AutoDetective(Detective detective)
    {
        _d = detective;
    }

    private void Note(string s) => Steps.Add($"{_d.Now:ddd HH:mm} {s}");

    public Verdict Run()
    {
        var deadline = _d.Now.AddDays(MaxDays);
        while (_d.Case == null && _d.Now < deadline) _d.Spend(TimeSpan.FromMinutes(30));
        if (_d.Case == null)
        {
            Note("No case reported.");
            return null;
        }
        Note($"Case: {_d.State.People[_d.Case.VictimId].FullName}");
        WorkScene();
        CanvassNeighbours();
        CheckCameras();
        _d.TravelTo(_d.Me.HomeAddressId);
        _d.Enter(_d.Me.HomeLocationId);
        _d.AskPolice(PoliceRequest.CoronerReport);
        var victimPhone = _d.State.PhoneOf(_d.State.People[_d.Case.VictimId]);
        if (victimPhone != null) _d.AskPolice(PoliceRequest.PhoneRecords, victimPhone.Id);
        foreach (var print in _d.Clues.Where(c => c.Kind == ClueKind.Print && c.MatchedPersonId == null).ToList())
            _d.AskPolice(PoliceRequest.RunPrint, print.Id);

        for (int day = 0; day < MaxDays && _d.Now < deadline; day++)
        {
            if (day > 0)
            {
                // Mornings, while people are at work: search the suspects' homes and tap their phones.
                var morning = _d.Now.Date.AddDays(_d.Now.Hour < 9 ? 0 : 1).AddHours(9).AddMinutes(30);
                if (_d.Now < morning) _d.Spend(morning - _d.Now);
                foreach (var suspect in Suspects().Take(3).ToList()) TrySearchHome(suspect);
            }
            foreach (var suspect in Suspects().Take(5).ToList())
            {
                if (_d.Now >= deadline) break;
                QuestionSuspect(suspect);
            }
            ReturnToWitnesses();
            CheckAlibis();
            FollowPhoneRecords();
            var (best, assessment) = BestCase();
            if (assessment != null) Note($"Best case: {_d.State.People[best].FullName} at {assessment.Probability:P0}");
            if (assessment != null && assessment.Probability >= 0.65) break;
        }
        var (accused, a) = BestCase();
        if (a == null)
        {
            Note("No suspect at all.");
            return null;
        }
        var verdict = CaseEvaluator.Submit(_d, accused, _d.Clues.Select(c => c.Id));
        Note($"Accused {_d.State.People[accused].FullName}: {verdict.Outcome} (right person: {verdict.RightPerson})");
        return verdict;
    }

    private void WorkScene()
    {
        _d.TravelTo(_d.Case.SceneAddressId);
        _d.Enter(_d.Case.SceneLocationId);
        _d.LookAround();
        _d.ExamineBody();
        var loc = _d.State.Locations[_d.Case.SceneLocationId];
        foreach (var ap in loc.AccessPoints) _d.ExamineDoor(ap.Id);
        foreach (var f in _d.FixturesHere().ToList()) _d.Search(f.Id);
        foreach (var s in _d.SurfacesHere().Where(s => !s.Name.Contains("Trash")).ToList()) _d.DustForPrints(s.Id);
        foreach (var item in _d.ItemsLyingHere().ToList()) _d.Collect(item.Id);
        Note($"Scene worked: {_d.Clues.Count} clues");
    }

    private IEnumerable<int> NeighbourHomes()
    {
        var scene = _d.State.Addresses[_d.Case.SceneAddressId];
        if (scene.Type == AddressType.ApartmentBuilding)
            return _d.State.GetLocationsForAddress(scene.Id).Where(l => l.HasTag("home") && l.Id != _d.Case.SceneLocationId)
                .OrderBy(l => Math.Abs((l.Floor ?? 0) - (_d.State.Locations[_d.Case.SceneLocationId].Floor ?? 0))).Select(l => l.Id);
        return _d.State.Addresses.Values.Where(a => a.Type == AddressType.SuburbanHome && a.Id != scene.Id
                && Math.Abs(a.GridX - scene.GridX) + Math.Abs(a.GridY - scene.GridY) <= 2)
            .SelectMany(a => _d.State.GetLocationsForAddress(a.Id).Where(l => l.HasTag("home"))).Select(l => l.Id);
    }

    private void CanvassNeighbours()
    {
        foreach (var homeId in NeighbourHomes().Take(30).ToList())
        {
            var home = _d.State.Locations[homeId];
            if (_d.AddressId != home.AddressId) _d.TravelTo(home.AddressId);
            foreach (var person in _d.Knock(homeId)) Interview(person, witness: true);
        }
        Note($"Canvass done: {_d.Clues.Count} clues");
    }

    private void CheckCameras()
    {
        var scene = _d.State.Addresses[_d.Case.SceneAddressId];
        var withCameras = _d.State.Addresses.Values
            .Where(a => Math.Abs(a.GridX - scene.GridX) + Math.Abs(a.GridY - scene.GridY) <= 5)
            .Where(a => _d.State.GetLocationsForAddress(a.Id).Any(l => _d.State.AllFixturesIn(l.Id).Any(f => f.Type == FixtureType.Camera)))
            .OrderBy(a => Math.Abs(a.GridX - scene.GridX) + Math.Abs(a.GridY - scene.GridY)).Take(3).ToList();
        foreach (var a in withCameras)
        {
            _d.TravelTo(a.Id);
            foreach (var cam in _d.CamerasHere().ToList())
                _d.ViewFootage(cam.Id, _d.Case.NightStart, _d.Case.NightEnd);
        }
        Note($"Cameras checked at {withCameras.Count} buildings");
    }

    private void Interview(int person, bool witness)
    {
        if (!_d.CanTalkTo(person)) return;
        _d.Ask(person, Topic.Introduce);
        _d.Ask(person, Topic.Victim);
        if (witness) _d.Ask(person, Topic.SawAnything);
        _d.Ask(person, Topic.Enemies);
        _interviewed.Add(person);
    }

    /// <summary>People with a motive, named at the scene or by witnesses, most mentioned first.</summary>
    private IEnumerable<int> Suspects()
    {
        var score = new Dictionary<int, double>();
        void Bump(int id, double s)
        {
            if (id == _d.Case.VictimId || id == _d.Agent.Id) return;
            score[id] = score.GetValueOrDefault(id) + s;
        }
        foreach (var c in _d.Clues)
        {
            if (c.MotivePersonId.HasValue) Bump(c.MotivePersonId.Value, c.Kind == ClueKind.Document ? 3 : 2);
            if (c.Kind == ClueKind.PrintMatch && c.AtScene) Bump(c.MatchedPersonId.Value, 2);
            if (c.Kind == ClueKind.Sighting && c.Sighting?.KnownByName == true && c.Sighting.PersonId.HasValue) Bump(c.Sighting.PersonId.Value, 2);
            if (c.Kind == ClueKind.Tape && c.CallerId.HasValue) Bump(c.CallerId.Value, 1);
            foreach (var n in c.NamedPersonIds) Bump(n, 0.3);
        }
        return score.OrderByDescending(kv => kv.Value).Select(kv => kv.Key);
    }

    /// <summary>Finds a suspect at home or at work and questions them.</summary>
    private void QuestionSuspect(int suspect)
    {
        if (_interviewed.Contains(suspect) && _d.Clues.Any(c => c.Kind == ClueKind.Alibi && c.AboutPersonId == suspect)) return;
        var person = _d.State.People[suspect];
        if (!_d.People.TryGetValue(suspect, out var known) || !known.AddressKnown) _d.PhoneBook(person.FullName);
        foreach (var attempt in Places(suspect))
        {
            _d.TravelTo(attempt.Address);
            if (attempt.Home) _d.Knock(attempt.Location);
            else _d.Enter(attempt.Location);
            if (_d.CanTalkTo(suspect)) break;
        }
        if (!_d.CanTalkTo(suspect))
        {
            Note($"Couldn't find {person.FullName}");
            return;
        }
        _d.Ask(suspect, Topic.Introduce);
        _d.Ask(suspect, Topic.Victim);
        _d.Ask(suspect, Topic.Whereabouts);
        _d.Ask(suspect, Topic.Prints);
        if (!_d.People[suspect].ReferencePrints) _d.Ask(suspect, Topic.PalmGlass);
        _d.Ask(suspect, Topic.Enemies);
        _interviewed.Add(suspect);
        Note($"Questioned {person.FullName}");
    }

    private IEnumerable<(int Address, int Location, bool Home)> Places(int personId)
    {
        var p = _d.State.People[personId];
        bool workHours = _d.Now.Hour >= 9 && _d.Now.Hour < 17 && _d.Now.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
        var home = (p.HomeAddressId, p.HomeLocationId, true);
        (int, int, bool)? work = null;
        if (p.BusinessId.HasValue && _d.People.TryGetValue(personId, out var k) && k.EmployerKnown)
        {
            var biz = _d.State.Businesses[p.BusinessId.Value];
            var loc = _d.State.GetLocationsForAddress(biz.AddressId).FirstOrDefault(l => l.HasTag("service_area"))
                      ?? _d.State.GetLocationsForAddress(biz.AddressId).First();
            work = (biz.AddressId, loc.Id, false);
        }
        if (workHours && work.HasValue) { yield return work.Value; yield return home; }
        else { yield return home; if (work.HasValue) yield return work.Value; }
    }

    /// <summary>While the suspect is out, pick their lock and look around; plant a tap on their phone.</summary>
    private readonly HashSet<int> _searched = new();

    private void TrySearchHome(int suspect)
    {
        if (_searched.Contains(suspect)) return;
        var p = _d.State.People[suspect];
        if (_d.AddressId != p.HomeAddressId) _d.TravelTo(p.HomeAddressId);
        var access = _d.CanEnter(p.HomeLocationId, out _);
        if (access == Access.Refused) return;
        bool inside = access == Access.Open ? _d.Enter(p.HomeLocationId) : _d.PickLock(p.HomeLocationId, true);
        if (!inside) return;
        _searched.Add(suspect);
        Note($"Searched {p.FullName}'s home");
        var door = _d.SurfacesHere().FirstOrDefault(x => x.Name.Contains("Front Door"));
        if (door != null) _d.DustForPrints(door.Id);
        foreach (var f in _d.FixturesHere().Where(f => f.Type is FixtureType.Closet or FixtureType.Hamper or FixtureType.Nightstand or FixtureType.Drawer).ToList())
            _d.Search(f.Id);
        foreach (var c in _d.Clues.Where(c => c.FoundAtHomeOfPersonId == suspect && c.Bloody && c.ItemId.HasValue).ToList())
        {
            _d.Collect(c.ItemId.Value);
            _d.AskPolice(PoliceRequest.LabTest, c.ItemId.Value);
        }
        var phone = _d.FixturesHere().FirstOrDefault(f => f.Type == FixtureType.Telephone);
        if (phone != null)
        {
            if (_d.Wiretaps == 0) _d.BuyWiretap();
            _d.PlantWiretap(phone.Id);
        }
        _d.Leave();
    }

    private void ReturnToWitnesses()
    {
        var witnesses = _d.Clues.Where(c => c.Kind == ClueKind.Sighting && c.Sighting?.Kind == Simulation.Observers.SightingKind.Person && c.Sighting.KnownByName == false)
            .Select(c => c.WitnessId.Value).Distinct().ToList();
        var suspects = Suspects().Take(4).Where(s => _d.People.TryGetValue(s, out var k) && k.Met).ToList();
        foreach (var w in witnesses)
        {
            var todo = suspects.Where(s => !_shown.Contains((w, s))).ToList();
            if (todo.Count == 0) continue;
            var person = _d.State.People[w];
            _d.TravelTo(person.HomeAddressId);
            _d.Knock(person.HomeLocationId);
            if (!_d.CanTalkTo(w)) continue;
            foreach (var s in todo)
            {
                _d.Ask(w, Topic.ShowPerson, s);
                _shown.Add((w, s));
            }
        }
    }

    private void CheckAlibis()
    {
        foreach (var alibi in _d.Clues.Where(c => c.Kind == ClueKind.Alibi && c.Alibi != null).ToList())
        {
            foreach (var w in alibi.Alibi.WitnessIds)
            {
                if (_confirmed.Contains((w, alibi.AboutPersonId.Value)) || w == _d.Case.VictimId) continue;
                _confirmed.Add((w, alibi.AboutPersonId.Value));
                foreach (var place in Places(w))
                {
                    _d.TravelTo(place.Address);
                    if (place.Home) _d.Knock(place.Location); else _d.Enter(place.Location);
                    if (_d.CanTalkTo(w)) break;
                }
                if (!_d.CanTalkTo(w)) continue;
                _d.Ask(w, Topic.ConfirmAlibi, alibi.AboutPersonId.Value);
                _d.Ask(w, Topic.SawAnything);
            }
        }
    }

    private void FollowPhoneRecords()
    {
        foreach (var rec in _d.Clues.Where(c => c.Kind == ClueKind.PhoneRecord && c.Calls != null).ToList())
            foreach (var call in rec.Calls.Where(c => c.Time > _d.Case.NightStart.AddHours(-12)).Take(6))
                _d.ReverseLookup(call.Other);
    }

    private (int, CaseAssessment) BestCase()
    {
        CaseAssessment best = null;
        int bestId = 0;
        foreach (var s in Suspects().Take(8))
        {
            var a = CaseEvaluator.Assess(_d, s, _d.Clues.Select(c => c.Id));
            if (best == null || a.Probability > best.Probability) { best = a; bestId = s; }
        }
        return (bestId, best);
    }
}
