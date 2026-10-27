using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Data;

public static class VehicleData
{
    public static readonly (string Make, string Model, VehicleBody Body)[] Models =
    {
        ("Ford", "LTD", VehicleBody.Sedan), ("Ford", "Escort", VehicleBody.Hatchback),
        ("Chevrolet", "Caprice", VehicleBody.Sedan), ("Chevrolet", "Citation", VehicleBody.Hatchback),
        ("Dodge", "Aries", VehicleBody.Sedan), ("Plymouth", "Reliant", VehicleBody.Sedan),
        ("Buick", "Century", VehicleBody.Sedan), ("Oldsmobile", "Cutlass", VehicleBody.Coupe),
        ("Pontiac", "Firebird", VehicleBody.Coupe), ("Toyota", "Corolla", VehicleBody.Sedan),
        ("Honda", "Accord", VehicleBody.Sedan), ("Honda", "Civic", VehicleBody.Hatchback),
        ("Volkswagen", "Rabbit", VehicleBody.Hatchback), ("Volvo", "240", VehicleBody.Wagon),
        ("Ford", "Country Squire", VehicleBody.Wagon), ("Chevrolet", "C10", VehicleBody.Pickup),
        ("Ford", "F-150", VehicleBody.Pickup), ("Dodge", "Ram Van", VehicleBody.Van),
        ("Datsun", "280ZX", VehicleBody.Coupe), ("Mercury", "Cougar", VehicleBody.Coupe),
        ("Cadillac", "DeVille", VehicleBody.Sedan), ("AMC", "Eagle", VehicleBody.Wagon)
    };

    public static readonly (string Color, bool Dark)[] Colors =
    {
        ("black", true), ("navy blue", true), ("dark green", true), ("maroon", true), ("brown", true),
        ("gray", true), ("red", true), ("silver", false), ("white", false), ("beige", false),
        ("yellow", false), ("light blue", false)
    };

    public static readonly string[] CigaretteBrands = { "Marlboro", "Winston", "Camel", "Kool", "Salem", "Newport" };
}
