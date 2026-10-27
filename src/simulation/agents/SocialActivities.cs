using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;
using Stakeout.Simulation.History;
using Stakeout.Simulation.Space;

namespace Stakeout.Simulation.Agents;

/// <summary>
/// A dinner date: the driver picks the passenger up at home, they drive to the venue in one car, eat at one
/// table, and the driver brings the passenger home before going home alone.
/// </summary>
public class DateActivity : JointActivity
{
    public int DriverId { get; }
    public int PassengerId { get; }
    public int VenueAddressId { get; }
    public int PassengerHomeAddressId { get; }
    public int DinnerMinutes { get; }

    public override string Name => "date";

    public DateActivity(World world, int driverId, int passengerId, int venueAddressId, DateTime pickup, int dinnerMinutes)
        : base(world)
    {
        DriverId = driverId;
        PassengerId = passengerId;
        VenueAddressId = venueAddressId;
        DinnerMinutes = dinnerMinutes;
        PassengerHomeAddressId = world.State.People[passengerId].HomeAddressId;
        MeetingAddressId = PassengerHomeAddressId;
        Start = pickup;
        ParticipantIds.Add(driverId);
        ParticipantIds.Add(passengerId);
        var leg = world.Movement.TravelEstimate(PassengerHomeAddressId, venueAddressId, TravelMode.Drive);
        ExpectedDuration = leg + leg + TimeSpan.FromMinutes(dinnerMinutes + 10);
    }

    public override string Describe(Agent agent)
    {
        var other = World.State.People[agent.Id == DriverId ? PassengerId : DriverId];
        return $"date with {other.FirstName} at {World.State.PlaceName(VenueAddressId)}";
    }

    public override (int LocationId, int? SubLocationId) WaitingSpot(Agent agent) =>
        agent.Id == PassengerId
            ? World.Places.HomeRoom(agent.Person, Spot.HomeLiving)
            : (World.Movement.ArrivalLocation(MeetingAddressId).Id, null);

    protected override void Run(List<Agent> present)
    {
        var driver = AgentOf(DriverId);
        var passenger = AgentOf(PassengerId);
        var pair = new[] { driver, passenger };
        MoveTogether(pair, VenueAddressId, () =>
        {
            var room = World.State.FindLocationByTag(VenueAddressId, "service_area")
                       ?? World.Movement.ArrivalLocation(VenueAddressId);
            foreach (var a in pair)
                World.Movement.GoInside(a, room.Id, null, ActivityKind.Date, Describe(a));
            var business = World.State.BusinessAt(VenueAddressId);
            if (business != null)
                World.History.Purchases.Add(new PurchaseFact
                {
                    PersonId = DriverId, BusinessId = business.Id, Time = Now.AddMinutes(DinnerMinutes - 5),
                    AmountCents = 1800 + (DriverId % 7) * 150, Cash = DriverId % 3 != 0
                });
            After(TimeSpan.FromMinutes(DinnerMinutes), () =>
                MoveTogether(pair, PassengerHomeAddressId, () =>
                {
                    Release(driver);
                    var (loc, sub) = World.Places.HomeRoom(passenger.Person, Spot.HomeLiving);
                    World.Movement.GoInside(passenger, loc, sub, ActivityKind.Home, "home after a date", Finish);
                }));
        });
    }

    public override int EndAddressFor(Agent agent) => PassengerHomeAddressId;
}

/// <summary>A friend comes over to the host's home for the evening.</summary>
public class VisitActivity : JointActivity
{
    public int HostId { get; }
    public int GuestId { get; }
    public int Minutes { get; }
    public int HomeLocationId { get; }

    public override string Name => "visit";

    public VisitActivity(World world, int hostId, int guestId, DateTime start, int minutes) : base(world)
    {
        HostId = hostId;
        GuestId = guestId;
        Minutes = minutes;
        var host = world.State.People[hostId];
        MeetingAddressId = host.HomeAddressId;
        HomeLocationId = host.HomeLocationId;
        Start = start;
        ExpectedDuration = TimeSpan.FromMinutes(minutes + 5);
        ParticipantIds.Add(hostId);
        ParticipantIds.Add(guestId);
    }

    public override string Describe(Agent agent) => agent.Id == HostId
        ? $"{World.State.People[GuestId].FirstName} visiting"
        : $"visiting {World.State.People[HostId].FirstName}";

    public override (int LocationId, int? SubLocationId) WaitingSpot(Agent agent) =>
        agent.Id == HostId
            ? World.Places.HomeRoom(agent.Person, Spot.HomeLiving)
            : (World.Movement.ArrivalLocation(MeetingAddressId).Id, null);

    protected override void Run(List<Agent> present)
    {
        var host = AgentOf(HostId);
        var guest = AgentOf(GuestId);
        var (loc, living) = World.Places.HomeRoom(host.Person, Spot.HomeLiving);
        var door = MainDoor(loc);
        World.Movement.SetActivity(host, ActivityKind.Host, Describe(host));
        World.Movement.GoInside(guest, loc, living, ActivityKind.Visit, Describe(guest), () =>
        {
            if (door.HasValue) Touch(guest, door.Value);
            var table = living.HasValue
                ? World.State.GetFixturesForSubLocation(living.Value).FirstOrDefault(f => f.Type == FixtureType.Table)
                : null;
            if (table != null) Touch(guest, table.Id);
        });
        After(TimeSpan.FromMinutes(Minutes), () =>
        {
            if (door.HasValue) Touch(guest, door.Value);
            Finish();
        });
    }

    public override int EndAddressFor(Agent agent) => MeetingAddressId;
}
