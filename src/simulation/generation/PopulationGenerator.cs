using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Core;
using Stakeout.Simulation.Data;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;

namespace Stakeout.Simulation.Generation;

/// <summary>
/// Fills the city at world creation: one person per job plus non-workers, grouped into households and housed;
/// cars, phones and answering machines; friendships, dating, exes, affairs; and the tensions between people
/// that can become motives. Nothing is spawned later, so nothing can contradict what the player has seen.
/// </summary>
public class PopulationGenerator
{
    private readonly World _world;
    private readonly SimulationState _state;
    private readonly Random _rng;
    private readonly HashSet<string> _fullNames = new();
    private readonly HashSet<string> _plates = new();
    private readonly HashSet<string> _numbers = new();

    public PopulationGenerator(World world)
    {
        _world = world;
        _state = world.State;
        _rng = Rng.For(world.Seed, Purpose.Population, 1);
    }

    public void Populate(double nonWorkerShare = 0.18)
    {
        var workers = new List<Person>();
        foreach (var business in _state.Businesses.Values.OrderBy(b => b.Id))
            foreach (var position in business.Positions)
            {
                var person = NewPerson(position.Role == "ceo" ? 40 : 21, position.Role == "ceo" ? 64 : 62);
                person.BusinessId = business.Id;
                person.PositionId = position.Id;
                position.AssignedPersonId = person.Id;
                workers.Add(person);
            }
        int nonWorkers = (int)(workers.Count * nonWorkerShare);
        var everyone = new List<Person>(workers);
        for (int i = 0; i < nonWorkers; i++)
            everyone.Add(_rng.NextDouble() < 0.6 ? NewPerson(60, 78) : NewPerson(21, 59));

        FormHouseholds(everyone);
        foreach (var p in everyone)
        {
            AssignSchedule(p);
            AssignWorkFloor(p);
            AssignFavorites(p);
            AssignVehicle(p);
        }
        BuildSocialGraph(everyone);
        BuildTensions(everyone);
        PlaceCameras();
        PlaceDocuments(everyone);
        PlaceGuns(everyone);
    }

    private void PlaceGuns(List<Person> everyone)
    {
        foreach (var p in everyone.Where(p => p.Has(Traits.GunOwner)).OrderBy(p => p.Id))
        {
            var nightstand = _state.AllFixturesIn(p.HomeLocationId).FirstOrDefault(f => f.Type == FixtureType.Nightstand);
            if (nightstand == null) continue;
            var gun = new Item
            {
                Id = _state.GenerateEntityId(), ItemType = ItemType.Handgun, OwnerPersonId = p.Id,
                Name = _rng.NextDouble() < 0.6 ? ".38 Smith & Wesson revolver" : "9mm Browning pistol",
                FixtureId = nightstand.Id
            };
            gun.Data["registered"] = _rng.NextDouble() < 0.8;
            _state.Items[gun.Id] = gun;
        }
    }

    // ---- people ----

    private Person NewPerson(int minAge, int maxAge, Sex? sex = null, string lastName = null)
    {
        var s = sex ?? (_rng.NextDouble() < 0.5 ? Sex.Male : Sex.Female);
        string first, last;
        int attempts = 0;
        do
        {
            first = s == Sex.Male ? _rng.Pick(NameData.MaleFirstNames) : _rng.Pick(NameData.FemaleFirstNames);
            last = lastName != null && attempts < 20 ? lastName : _rng.Pick(NameData.LastNames);
            attempts++;
        } while (!_fullNames.Add($"{first} {last}") && attempts < 200);

        int age = _rng.Next(minAge, maxAge + 1);
        var appearance = new Appearance
        {
            Sex = s,
            Age = age,
            Height = (Height)WeightedIndex(s == Sex.Male ? new[] { 0.2, 0.5, 0.3 } : new[] { 0.35, 0.5, 0.15 }),
            Build = (Build)WeightedIndex(new[] { 0.3, 0.45, 0.25 }),
            Hair = age > 58 && _rng.NextDouble() < 0.6
                ? (s == Sex.Male && _rng.NextDouble() < 0.3 ? HairColor.Bald : HairColor.Gray)
                : (HairColor)WeightedIndex(new[] { 0.25, 0.42, 0.2, 0.08, 0.05, s == Sex.Male ? 0.04 : 0.0 }),
            Glasses = _rng.NextDouble() < 0.28,
            Mustache = s == Sex.Male && _rng.NextDouble() < 0.3,
            Beard = s == Sex.Male && _rng.NextDouble() < 0.1
        };
        var person = new Person
        {
            Id = _state.GenerateEntityId(),
            FirstName = first,
            LastName = last,
            Appearance = appearance
        };
        foreach (var (trait, p) in new[]
                 {
                     (Traits.Drinker, 0.22), (Traits.Foodie, 0.2), (Traits.Runner, 0.15), (Traits.Homebody, 0.2),
                     (Traits.Smoker, 0.32), (Traits.Violent, 0.08), (Traits.Careful, 0.3), (Traits.Observant, 0.2),
                     (Traits.GunOwner, 0.12), (Traits.CriminalRecord, 0.06)
                 })
            if (_rng.NextDouble() < p) person.Traits.Add(trait);
        if (person.Has(Traits.Violent) && _rng.NextDouble() < 0.4) person.Traits.Add(Traits.CriminalRecord);
        if (person.Has(Traits.Smoker)) person.CigaretteBrand = _rng.Pick(VehicleData.CigaretteBrands);
        _state.People[person.Id] = person;
        return person;
    }

    private int WeightedIndex(double[] weights)
    {
        double total = weights.Sum(), r = _rng.NextDouble() * total;
        for (int i = 0; i < weights.Length; i++)
        {
            if (r < weights[i]) return i;
            r -= weights[i];
        }
        return weights.Length - 1;
    }

    // ---- households and homes ----

    private void FormHouseholds(List<Person> everyone)
    {
        var houses = new List<(int Address, int Location)>();
        var apartments = new List<(int Address, int Location)>();
        foreach (var address in _state.Addresses.Values.OrderBy(a => a.Id))
        {
            if (address.Type == AddressType.SuburbanHome)
            {
                var interior = _state.FindLocationByTag(address.Id, "home");
                if (interior != null) houses.Add((address.Id, interior.Id));
            }
            else if (address.Type == AddressType.ApartmentBuilding)
            {
                foreach (var loc in _state.GetLocationsForAddress(address.Id).Where(l => l.HasTag("home")))
                    apartments.Add((address.Id, loc.Id));
            }
        }
        Shuffle(houses);
        Shuffle(apartments);
        int houseIndex = 0, aptIndex = 0;

        (int, int) TakeHome(bool preferHouse)
        {
            if (preferHouse && houseIndex < houses.Count) return houses[houseIndex++];
            if (aptIndex < apartments.Count) return apartments[aptIndex++];
            if (houseIndex < houses.Count) return houses[houseIndex++];
            throw new InvalidOperationException("The city has run out of homes");
        }

        var pool = everyone.OrderBy(_ => _rng.Next()).ToList();
        var used = new HashSet<int>();
        foreach (var person in pool)
        {
            if (used.Contains(person.Id)) continue;
            used.Add(person.Id);
            var members = new List<Person> { person };
            double roll = _rng.NextDouble();
            if (roll < 0.34)
            {
                // A couple: usually opposite sex, close in age; many share a surname.
                var partner = pool.FirstOrDefault(p => !used.Contains(p.Id)
                    && p.Appearance.Sex != person.Appearance.Sex && Math.Abs(p.Appearance.Age - person.Appearance.Age) <= 8);
                if (partner != null)
                {
                    used.Add(partner.Id);
                    members.Add(partner);
                    if (_rng.NextDouble() < 0.65)
                    {
                        var husband = person.Appearance.Sex == Sex.Male ? person : partner;
                        var wife = husband == person ? partner : person;
                        _fullNames.Remove(wife.FullName);
                        if (_fullNames.Add($"{wife.FirstName} {husband.LastName}")) wife.LastName = husband.LastName;
                        else _fullNames.Add(wife.FullName);
                    }
                    _state.AddRelationship(new Relationship
                    {
                        Id = _state.GenerateEntityId(), PersonAId = person.Id, PersonBId = partner.Id,
                        Type = RelationshipType.Partner, StartedAt = _world.Now.AddDays(-_rng.Next(200, 9000))
                    });
                }
            }
            else if (roll < 0.40 && person.Appearance.Age < 40)
            {
                var roommate = pool.FirstOrDefault(p => !used.Contains(p.Id)
                    && p.Appearance.Sex == person.Appearance.Sex && p.Appearance.Age < 40);
                if (roommate != null)
                {
                    used.Add(roommate.Id);
                    members.Add(roommate);
                }
            }

            bool couple = members.Count == 2 && _state.GetRelationship(members[0].Id, members[1].Id) != null;
            bool preferHouse = couple ? _rng.NextDouble() < 0.6 : _rng.NextDouble() < 0.12;
            var (addressId, locationId) = TakeHome(preferHouse);
            var household = new Household { Id = _state.GenerateEntityId(), AddressId = addressId, LocationId = locationId };
            _state.Households[household.Id] = household;
            var phone = _state.Phone(locationId);
            household.PhoneId = phone.Id;
            phone.PhoneNumber = NewNumber();
            if (_rng.NextDouble() < 0.42)
            {
                var machine = LocationBuilders.CreateFixture(_state, FixtureType.AnsweringMachine, "Answering Machine",
                    phone.LocationId, phone.SubLocationId);
                _ = machine;
            }
            foreach (var m in members)
            {
                household.MemberIds.Add(m.Id);
                m.HouseholdId = household.Id;
                m.HomeAddressId = addressId;
                m.HomeLocationId = locationId;
                m.HomePhoneId = phone.Id;
            }
        }

        // Businesses get phone numbers too.
        foreach (var business in _state.Businesses.Values.OrderBy(b => b.Id))
        {
            var phone = _state.GetLocationsForAddress(business.AddressId)
                .Select(l => _state.Phone(l.Id)).FirstOrDefault(f => f != null);
            if (phone != null) phone.PhoneNumber = NewNumber();
        }
    }

    private string NewNumber()
    {
        string n;
        do n = $"555-{_rng.Next(0, 10000):D4}";
        while (!_numbers.Add(n));
        return n;
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ---- schedules, workplaces, habits, cars ----

    private void AssignSchedule(Person p)
    {
        var position = _state.PositionOf(p);
        double jitter = _rng.Next(-20, 21);
        if (position == null)
        {
            var wake = TimeSpan.FromHours(p.Appearance.Age > 60 ? 6.5 : 7.5) + TimeSpan.FromMinutes(jitter * 2);
            p.WakeTime = wake;
            p.SleepTime = Normalize(wake - TimeSpan.FromHours(p.Has(Traits.NightOwl) ? 7 : 8));
            return;
        }
        var start = position.ShiftStart;
        if (start >= TimeSpan.FromHours(4) && start <= TimeSpan.FromHours(15))
        {
            p.WakeTime = Normalize(start - TimeSpan.FromMinutes(95 + jitter));
            p.SleepTime = Normalize(p.WakeTime - TimeSpan.FromHours(8));
        }
        else
        {
            p.SleepTime = Normalize(position.ShiftEnd + TimeSpan.FromMinutes(70 + jitter));
            p.WakeTime = Normalize(p.SleepTime + TimeSpan.FromHours(8));
        }
    }

    private static TimeSpan Normalize(TimeSpan t)
    {
        double minutes = Math.Round(t.TotalMinutes / 5) * 5;
        return TimeSpan.FromMinutes(((minutes % 1440) + 1440) % 1440);
    }

    private void AssignWorkFloor(Person p)
    {
        if (!p.BusinessId.HasValue) return;
        var business = _state.Businesses[p.BusinessId.Value];
        if (business.Type != BusinessType.Office) return;
        var floors = _state.GetLocationsForAddress(business.AddressId).Where(l => l.HasTag("commercial")).ToList();
        if (floors.Count > 0) p.WorkLocationId = floors[_rng.Next(floors.Count)].Id;
    }

    private void AssignFavorites(Person p)
    {
        var home = _state.Addresses[p.HomeAddressId];
        var anchor = p.BusinessId.HasValue ? _state.Addresses[_state.Businesses[p.BusinessId.Value].AddressId] : home;
        int Dist(Address a, Address b) => Math.Abs(a.GridX - b.GridX) + Math.Abs(a.GridY - b.GridY);

        List<int> Near(AddressType type, int count)
        {
            return _state.Addresses.Values.Where(a => a.Type == type)
                .Select(a => (a, score: Math.Min(Dist(a, home), Dist(a, anchor) + 4) + _rng.Next(0, 18)))
                .OrderBy(x => x.score).ThenBy(x => x.a.Id).Take(count).Select(x => x.a.Id).ToList();
        }

        p.FavoriteDinerIds = Near(AddressType.Diner, 3);
        p.FavoriteBarIds = Near(AddressType.DiveBar, 2);
        p.FavoriteParkId = Near(AddressType.Park, 1).FirstOrDefault();
    }

    private void AssignVehicle(Person p)
    {
        double chance = p.Appearance.Age > 70 ? 0.6 : 0.86;
        if (_rng.NextDouble() >= chance) return;
        var (make, model, body) = _rng.Pick(VehicleData.Models);
        var (color, dark) = _rng.Pick(VehicleData.Colors);
        string plate;
        do plate = $"{_rng.Next(100, 1000)}-{(char)('A' + _rng.Next(26))}{(char)('A' + _rng.Next(26))}{(char)('A' + _rng.Next(26))}";
        while (!_plates.Add(plate));
        var vehicle = new Vehicle
        {
            Id = _state.GenerateEntityId(), OwnerPersonId = p.Id, Make = make, Model = model, Body = body,
            Color = color, DarkColor = dark, Plate = plate
        };
        _state.Vehicles[vehicle.Id] = vehicle;
        p.VehicleId = vehicle.Id;
    }

    // ---- social graph ----

    private void Relate(int a, int b, RelationshipType type, int minDaysAgo, int maxDaysAgo)
    {
        if (a == b || _state.GetRelationship(a, b) != null) return;
        _state.AddRelationship(new Relationship
        {
            Id = _state.GenerateEntityId(), PersonAId = a, PersonBId = b, Type = type,
            StartedAt = _world.Now.AddDays(-_rng.Next(minDaysAgo, maxDaysAgo))
        });
    }

    private bool HasPartner(Person p) =>
        _state.RelationshipsOf(p.Id).Any(r => r.Type is RelationshipType.Partner or RelationshipType.Dating);

    private void BuildSocialGraph(List<Person> everyone)
    {
        var byId = everyone.OrderBy(p => p.Id).ToList();
        var byAddress = everyone.GroupBy(p => p.HomeAddressId).ToDictionary(g => g.Key, g => g.ToList());

        // Friends: coworkers, neighbours and old acquaintances.
        foreach (var p in byId)
        {
            int want = _rng.Next(2, 5);
            int have = _state.RelatedPeople(p.Id, RelationshipType.Friend).Count();
            for (int i = have; i < want; i++)
            {
                Person friend = null;
                double r = _rng.NextDouble();
                if (r < 0.45 && p.BusinessId.HasValue)
                {
                    var coworkers = _state.Coworkers(p.Id).ToList();
                    if (coworkers.Count > 0) friend = coworkers[_rng.Next(coworkers.Count)];
                }
                else if (r < 0.6 && byAddress.TryGetValue(p.HomeAddressId, out var neighbours) && neighbours.Count > 1)
                {
                    friend = neighbours[_rng.Next(neighbours.Count)];
                }
                friend ??= byId[_rng.Next(byId.Count)];
                if (friend.HouseholdId == p.HouseholdId) continue;
                Relate(p.Id, friend.Id, RelationshipType.Friend, 60, 7000);
            }
        }

        // Dating among singles who live apart.
        var singles = byId.Where(p => !HasPartner(p) && p.Appearance.Age < 60).OrderBy(_ => _rng.Next()).ToList();
        var taken = new HashSet<int>();
        foreach (var p in singles)
        {
            if (taken.Contains(p.Id) || _rng.NextDouble() > 0.42) continue;
            var match = singles.FirstOrDefault(o => !taken.Contains(o.Id) && o.Id != p.Id
                && o.Appearance.Sex != p.Appearance.Sex && Math.Abs(o.Appearance.Age - p.Appearance.Age) <= 10
                && o.HouseholdId != p.HouseholdId && _state.GetRelationship(p.Id, o.Id) == null);
            if (match == null) continue;
            taken.Add(p.Id);
            taken.Add(match.Id);
            Relate(p.Id, match.Id, RelationshipType.Dating, 14, 400);
        }

        // Exes.
        foreach (var p in byId)
        {
            if (_rng.NextDouble() > 0.22) continue;
            var ex = byId[_rng.Next(byId.Count)];
            if (ex.Appearance.Sex == p.Appearance.Sex || Math.Abs(ex.Appearance.Age - p.Appearance.Age) > 12) continue;
            if (ex.HouseholdId == p.HouseholdId) continue;
            Relate(p.Id, ex.Id, RelationshipType.Ex, 30, 1500);
        }

        // Affairs: someone in a couple sees a single person on the side.
        foreach (var p in byId)
        {
            bool partnered = _state.RelatedPeople(p.Id, RelationshipType.Partner).Any();
            if (!partnered || _rng.NextDouble() > 0.06) continue;
            var lover = singles.FirstOrDefault(o => o.Appearance.Sex != p.Appearance.Sex
                && Math.Abs(o.Appearance.Age - p.Appearance.Age) <= 15 && _state.GetRelationship(p.Id, o.Id) == null
                && _rng.NextDouble() < 0.05);
            if (lover != null) Relate(p.Id, lover.Id, RelationshipType.Affair, 20, 300);
        }
    }

    private void BuildTensions(List<Person> everyone)
    {
        var byId = everyone.OrderBy(p => p.Id).ToList();

        Tension Add(TensionKind kind, int aggressor, int target, double intensity, int amount = 0, int? third = null)
        {
            var t = new Tension
            {
                Id = _state.GenerateEntityId(), Kind = kind, AggressorId = aggressor, TargetId = target,
                Intensity = Math.Round(intensity, 2), AmountDollars = amount, ThirdPartyId = third
            };
            _state.Tensions[t.Id] = t;
            return t;
        }

        void Know(Tension t, IEnumerable<int> people, double p)
        {
            foreach (var id in people)
                if (id != t.AggressorId && id != t.TargetId && _rng.NextDouble() < p) t.KnownBy.Add(id);
        }

        IEnumerable<int> Friends(int id) => _state.RelatedPeople(id, RelationshipType.Friend);
        IEnumerable<int> Partners(int id) => _state.RelationshipsOf(id)
            .Where(r => r.Type is RelationshipType.Partner or RelationshipType.Dating).Select(r => r.Other(id));

        foreach (var rel in _state.Relationships.Values.OrderBy(r => r.Id).ToList())
        {
            switch (rel.Type)
            {
                case RelationshipType.Ex:
                {
                    // The one who was left still wants the other back; worse if the other has moved on.
                    bool aDumped = _rng.NextDouble() < 0.5;
                    int aggressor = aDumped ? rel.PersonAId : rel.PersonBId;
                    int target = rel.Other(aggressor);
                    var newPartner = Partners(target).FirstOrDefault();
                    double intensity = 0.15 + _rng.NextDouble() * 0.55 + (newPartner != 0 ? 0.2 : 0);
                    var t = Add(TensionKind.Jealousy, aggressor, target, intensity, third: newPartner == 0 ? null : newPartner);
                    Know(t, Friends(target), 0.65);
                    Know(t, Partners(target), 0.8);
                    Know(t, Friends(aggressor), 0.3);
                    break;
                }
                case RelationshipType.Friend when _rng.NextDouble() < 0.06:
                {
                    // A loan between friends that was never repaid. The borrower is the one with a reason to act.
                    bool aBorrowed = _rng.NextDouble() < 0.5;
                    int borrower = aBorrowed ? rel.PersonAId : rel.PersonBId;
                    int lender = rel.Other(borrower);
                    int amount = _rng.Next(5, 61) * 100;
                    var t = Add(TensionKind.Debt, borrower, lender, 0.2 + _rng.NextDouble() * 0.5 + amount / 30000.0, amount);
                    Know(t, Friends(lender), 0.5);
                    Know(t, Partners(lender), 0.9);
                    Know(t, Partners(borrower), 0.4);
                    break;
                }
                case RelationshipType.Affair:
                {
                    // The partnered one risks everything if the lover talks.
                    int married = _state.RelatedPeople(rel.PersonAId, RelationshipType.Partner).Any() ? rel.PersonAId : rel.PersonBId;
                    int lover = rel.Other(married);
                    var spouse = _state.RelatedPeople(married, RelationshipType.Partner).FirstOrDefault();
                    var t = Add(TensionKind.Affair, married, lover, 0.35 + _rng.NextDouble() * 0.55, third: spouse == 0 ? null : spouse);
                    var confidant = Friends(lover).OrderBy(_ => _rng.Next()).FirstOrDefault();
                    if (confidant != 0) t.KnownBy.Add(confidant);
                    break;
                }
            }
        }

        // Workplace trouble: embezzlement at some offices, petty grudges elsewhere.
        foreach (var business in _state.Businesses.Values.OrderBy(b => b.Id))
        {
            var staff = business.Positions.Where(p => p.AssignedPersonId.HasValue)
                .Select(p => p.AssignedPersonId.Value).ToList();
            if (staff.Count < 3) continue;
            if (business.Type == BusinessType.Office && _rng.NextDouble() < 0.18)
            {
                int thief = staff[_rng.Next(staff.Count)];
                int witness = staff[_rng.Next(staff.Count)];
                if (thief != witness)
                {
                    var t = Add(TensionKind.Embezzlement, thief, witness, 0.4 + _rng.NextDouble() * 0.5, _rng.Next(20, 200) * 100);
                    Know(t, Friends(witness), 0.4);
                    Know(t, Partners(witness), 0.7);
                }
            }
            if (_rng.NextDouble() < 0.35)
            {
                int a = staff[_rng.Next(staff.Count)], b = staff[_rng.Next(staff.Count)];
                if (a != b)
                {
                    var t = Add(TensionKind.Grudge, a, b, 0.1 + _rng.NextDouble() * 0.35);
                    Know(t, staff, 0.5);
                }
            }
        }
    }

    // ---- cameras and documents ----

    private void PlaceCameras()
    {
        foreach (var f in _state.Fixtures.Values.Where(f => f.HasTag("exterior_camera")).OrderBy(f => f.Id).ToList())
        {
            int addressId = _state.Locations[f.LocationId.Value].AddressId;
            f.WatchedCell = _world.Roads.AccessCell(addressId);
        }
    }

    private void PlaceDocuments(List<Person> everyone)
    {
        foreach (var p in everyone.OrderBy(p => p.Id))
        {
            var desk = DeskOf(p);
            if (desk == null) continue;
            var book = new Item
            {
                Id = _state.GenerateEntityId(), ItemType = ItemType.AddressBook, OwnerPersonId = p.Id,
                Name = $"{p.FirstName}'s address book", FixtureId = desk.Id
            };
            foreach (var rel in _state.RelationshipsOf(p.Id).OrderBy(r => r.Id))
            {
                var other = _state.People[rel.Other(p.Id)];
                if (other.HouseholdId == p.HouseholdId) continue;
                var phone = _state.PhoneOf(other);
                book.Text.Add($"{other.FullName} - {phone?.PhoneNumber ?? "no phone"} - {_state.HomeLine(other)}");
            }
            if (p.BusinessId.HasValue)
            {
                var biz = _state.Businesses[p.BusinessId.Value];
                book.Text.Add($"Work: {biz.Name} - {_state.AddressLine(biz.AddressId)}");
            }
            _state.Items[book.Id] = book;
        }

        // Paper trails for some tensions.
        foreach (var t in _state.Tensions.Values.OrderBy(t => t.Id))
        {
            var aggressor = _state.People[t.AggressorId];
            var target = _state.People[t.TargetId];
            var desk = DeskOf(target);
            if (desk == null) continue;
            Item doc = t.Kind switch
            {
                TensionKind.Debt when _rng.NextDouble() < 0.7 => new Item
                {
                    ItemType = ItemType.Letter, Name = "A handwritten IOU",
                    Text = { $"I.O.U. ${t.AmountDollars:N0}. Will repay by the end of the summer. - {aggressor.FullName}" }
                },
                TensionKind.Jealousy when _rng.NextDouble() < 0.6 => new Item
                {
                    ItemType = ItemType.Letter, Name = "Letters, tied with a rubber band",
                    Text =
                    {
                        $"{target.FirstName} - I can't stop thinking about you. You owe me a chance to talk. - {aggressor.FirstName}",
                        $"You can't just pretend I don't exist. I saw you with someone. We are not done. - {aggressor.FirstName[0]}."
                    }
                },
                TensionKind.Affair when _rng.NextDouble() < 0.65 => new Item
                {
                    ItemType = ItemType.Letter, Name = "Love letters, unsigned except for an initial",
                    Text =
                    {
                        $"My darling, I counted the hours until Thursday. Nobody can know. Yours, {aggressor.FirstName[0]}.",
                        $"Please be patient. I will tell her when the time is right. {aggressor.FirstName[0]}."
                    }
                },
                TensionKind.Embezzlement when _rng.NextDouble() < 0.6 => new Item
                {
                    ItemType = ItemType.Ledger, Name = "Photocopied ledger pages with notes in the margin",
                    Text = { $"Transfers to an account nobody recognizes, signed off by {aggressor.FullName}. Margin note: \"${t.AmountDollars:N0} so far. Talk to the boss Monday.\"" }
                },
                _ => null
            };
            if (doc == null) continue;
            doc.Id = _state.GenerateEntityId();
            doc.OwnerPersonId = target.Id;
            doc.FixtureId = desk.Id;
            doc.Data["tension"] = t.Id;
            _state.Items[doc.Id] = doc;
        }
    }

    private Fixture DeskOf(Person p) =>
        _state.AllFixturesIn(p.HomeLocationId).FirstOrDefault(f => f.Type == FixtureType.Drawer);
}
