namespace Stakeout.Simulation.Entities;

public enum VehicleType { Car }

public class Vehicle
{
    public int Id { get; set; }
    public int OwnerPersonId { get; set; }
    public int CurrentAddressId { get; set; }
    public VehicleType Type { get; set; }
}
