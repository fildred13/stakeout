// src/simulation/actions/ActionType.cs
namespace Stakeout.Simulation.Actions;

public enum ActionType
{
    Idle,           // was AtHome
    Work,           // was Working
    TravelByCar,    // was TravellingByCar
    Sleep,          // was Sleeping
    KillPerson
}
