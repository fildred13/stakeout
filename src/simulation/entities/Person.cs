using System;

namespace Stakeout.Simulation.Entities;

public class Person
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime CreatedAt { get; set; }
    public int HomeAddressId { get; set; }
    public int WorkAddressId { get; set; }
    public int CurrentAddressId { get; set; }

    public string FullName => $"{FirstName} {LastName}";
}
