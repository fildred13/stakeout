using System.Linq;
using Stakeout.Simulation.Core;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Agents;

/// <summary>Chooses the room a person occupies for an appointment (no room-to-room pathfinding).</summary>
public class Places
{
    private readonly World _world;
    private SimulationState State => _world.State;

    public Places(World world)
    {
        _world = world;
    }

    public (int LocationId, int? SubLocationId) Resolve(Agent agent, Appointment ap)
    {
        var person = agent.Person;
        switch (ap.Spot)
        {
            case Spot.Room:
                return (ap.LocationId.Value, ap.SubLocationId);
            case Spot.HomeLiving:
            case Spot.HomeBedroom:
            case Spot.HomeKitchen:
                if (ap.AddressId == person.HomeAddressId) return HomeRoom(person, ap.Spot);
                break;
            case Spot.Workplace:
                return Workplace(person);
            case Spot.Customer:
            {
                var loc = State.FindLocationByTag(ap.AddressId, "service_area");
                if (loc != null) return (loc.Id, null);
                break;
            }
            case Spot.Outdoors:
            {
                var outdoor = State.GetLocationsForAddress(ap.AddressId)
                    .Where(l => l.HasTag("exterior") && !l.HasTag("parking")).ToList();
                if (outdoor.Count > 0)
                {
                    int pick = (int)(Rng.Unit(_world.Seed, Purpose.DayPlan, person.Id, ap.ArriveBy.Ticks) * outdoor.Count);
                    return (outdoor[pick].Id, null);
                }
                break;
            }
            case Spot.GuestOf:
            {
                var living = State.FindSubLocationByTag(ap.LocationId.Value, "living");
                return (ap.LocationId.Value, living?.Id);
            }
        }
        return (_world.Movement.ArrivalLocation(ap.AddressId).Id, null);
    }

    public (int LocationId, int? SubLocationId) HomeRoom(Person person, Spot spot)
    {
        var home = State.Locations[person.HomeLocationId];
        string tag = spot switch
        {
            Spot.HomeBedroom => "bedroom",
            Spot.HomeKitchen => "kitchen",
            _ => "living"
        };
        if (spot == Spot.HomeBedroom)
        {
            var bedrooms = State.GetSubLocationsForLocation(home.Id).Where(s => s.HasTag("bedroom")).ToList();
            if (bedrooms.Count > 0) return (home.Id, bedrooms[BedroomIndex(person) % bedrooms.Count].Id);
        }
        return (home.Id, State.FindSubLocationByTag(home.Id, tag)?.Id);
    }

    /// <summary>Partners share the first bedroom; roommates each get their own.</summary>
    private int BedroomIndex(Person person)
    {
        var household = State.Households[person.HouseholdId];
        var partner = State.RelatedPeople(person.Id, RelationshipType.Partner).FirstOrDefault();
        if (partner != 0 && household.MemberIds.Contains(partner)) return 0;
        return household.MemberIds.IndexOf(person.Id);
    }

    public (int LocationId, int? SubLocationId) Workplace(Person person)
    {
        var position = State.PositionOf(person);
        var business = State.Businesses[person.BusinessId.Value];
        var locations = State.GetLocationsForAddress(business.AddressId);
        string role = position?.Role ?? "";
        Location Named(string name) => locations.FirstOrDefault(l => l.Name == name);

        switch (business.Type)
        {
            case BusinessType.Diner:
                if (role == "cook") return (Named("Kitchen").Id, null);
                if (role == "manager") return (Named("Manager's Office").Id, null);
                return (Named("Dining Area").Id, null);
            case BusinessType.DiveBar:
                if (role == "manager") return (Named("Manager's Office").Id, null);
                return (Named("Bar Area").Id, null);
            case BusinessType.Office:
            {
                var floor = person.WorkLocationId.HasValue
                    ? State.Locations[person.WorkLocationId.Value]
                    : locations.First(l => l.HasTag("commercial"));
                string sub = role is "manager" or "ceo" ? "Manager's Office" : "Cubicle Area";
                var subLoc = State.GetSubLocationsForLocation(floor.Id).FirstOrDefault(s => s.Name == sub);
                return (floor.Id, subLoc?.Id);
            }
        }
        return (_world.Movement.ArrivalLocation(business.AddressId).Id, null);
    }

    public (int LocationId, int? SubLocationId) WaitingSpot(Agent agent, JointActivity activity) =>
        activity.WaitingSpot(agent);
}
