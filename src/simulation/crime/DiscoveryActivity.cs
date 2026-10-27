using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Agents;
using Stakeout.Simulation.History;

namespace Stakeout.Simulation.Crime;

/// <summary>A neighbour (or a worried friend) notices the open door, finds the body and phones the police.</summary>
public class DiscoveryActivity : JointActivity
{
    private readonly CrimeSystem _crimes;
    private readonly MurderCase _case;
    private readonly int _personId;

    public override string Name => "discovery";

    public DiscoveryActivity(World world, CrimeSystem crimes, MurderCase murder, int personId) : base(world)
    {
        _crimes = crimes;
        _case = murder;
        _personId = personId;
        var person = world.State.People[personId];
        MeetingAddressId = person.HomeAddressId;
        Start = world.Now;
        ExpectedDuration = TimeSpan.FromMinutes(30);
        ParticipantIds.Add(personId);
    }

    public override string Describe(Agent agent) => "noticed something wrong next door";

    public override (int LocationId, int? SubLocationId) WaitingSpot(Agent agent) =>
        (agent.LocationId ?? agent.Person.HomeLocationId, agent.SubLocationId);

    protected override void Run(List<Agent> present)
    {
        var finder = AgentOf(_personId);
        if (finder.AddressId != _case.SceneAddressId)
            MoveTogether(new[] { finder }, _case.SceneAddressId, () => Find(finder));
        else
            Find(finder);
    }

    private void Find(Agent finder)
    {
        var door = MainDoor(_case.SceneLocationId);
        World.Movement.GoInside(finder, _case.SceneLocationId, _case.SceneSubLocationId, ActivityKind.Discover,
            "found the door open", () =>
            {
                if (door.HasValue) Touch(finder, door.Value);
                World.History.Noises.Add(new NoiseFact
                {
                    AddressId = _case.SceneAddressId, LocationId = _case.SceneLocationId, Time = Now,
                    Kind = NoiseKind.Scream, SourcePersonId = finder.Id
                });
                _case.DiscoveredAt = Now;
                After(TimeSpan.FromMinutes(3), () => GoHomeAndCall(finder));
            });
    }

    private void GoHomeAndCall(Agent finder)
    {
        var person = finder.Person;
        var (loc, sub) = World.Places.HomeRoom(person, Spot.HomeLiving);
        void Call()
        {
            World.Movement.GoInside(finder, loc, sub, ActivityKind.Phone, "calling the police", () =>
            {
                _crimes.ReportToPolice(_case, finder);
                After(TimeSpan.FromMinutes(20), Finish);
            });
        }
        if (finder.AddressId == person.HomeAddressId) Call();
        else MoveTogether(new[] { finder }, person.HomeAddressId, Call);
    }
}
