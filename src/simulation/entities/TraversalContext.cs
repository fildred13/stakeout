namespace Stakeout.Simulation.Entities;

public class TraversalContext
{
    public Person Traveler { get; set; }

    public bool CanTraverse(SublocationConnection conn)
    {
        if (conn.Lockable?.IsLocked == true)
            return false;
        if (conn.Concealable != null && !conn.Concealable.IsDiscovered)
            return false;
        return true;
    }
}
