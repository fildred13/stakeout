using System;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;

namespace Stakeout.Simulation.Addresses;

public class ParkTemplate : IAddressTemplate
{
    public void Generate(Address address, SimulationState state, Random random)
    {
        LocationBuilders.ExteriorParkingLot(state, address);

        LocationBuilders.CreateLocation(state, address, "Main Entrance",
            new[] { "exterior", "publicly_accessible", "entrance" });
        LocationBuilders.CreateLocation(state, address, "Jogging Path",
            new[] { "exterior", "publicly_accessible" });
        var picnic = LocationBuilders.CreateLocation(state, address, "Picnic Area",
            new[] { "exterior", "publicly_accessible", "social", "food" });
        LocationBuilders.CreateFixture(state, FixtureType.TrashCan, "Trash Can", locationId: picnic.Id, subLocationId: null);
        LocationBuilders.CreateLocation(state, address, "Playground",
            new[] { "exterior", "publicly_accessible", "social" });
        LocationBuilders.CreateLocation(state, address, "Wooded Area",
            new[] { "exterior", "publicly_accessible", "covert_entry" });
        LocationBuilders.CreateLocation(state, address, "Restroom Building",
            new[] { "publicly_accessible", "restroom" });
    }
}
