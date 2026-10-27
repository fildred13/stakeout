namespace Stakeout.Simulation.Entities;

public enum VehicleBody { Sedan, Coupe, Wagon, Pickup, Van, Hatchback }

public class Vehicle
{
    public int Id { get; set; }
    public int OwnerPersonId { get; set; }
    public string Make { get; set; }
    public string Model { get; set; }
    public VehicleBody Body { get; set; }
    public string Color { get; set; }
    public bool DarkColor { get; set; }
    public string Plate { get; set; }

    public string Description => $"{Color} {Make} {Model}";
    public string NightDescription => $"{(DarkColor ? "dark" : "light-colored")} {Body.ToString().ToLowerInvariant()}";
}
