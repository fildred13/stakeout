using System.Collections.Generic;

namespace Stakeout.Simulation.Entities;

public class City
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string CountryName { get; set; }
    public List<int> AddressIds { get; } = new();
    public int? AirportAddressId { get; set; }
}
