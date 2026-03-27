namespace Stakeout.Simulation.Entities;

public enum LockMechanism
{
    Key,
    Combination,
    Keypad,
    Electronic
}

public enum ConcealmentMethod
{
    Rug,
    Bookshelf,
    Leaves,
    FalseWall,
    Bushes
}

public class LockableProperty
{
    public LockMechanism Mechanism { get; set; }
    public bool IsLocked { get; set; } = false;
    public int? KeyItemId { get; set; } = null;
}

public class ConcealableProperty
{
    public ConcealmentMethod Method { get; set; }
    public bool IsDiscovered { get; set; } = false;
    public float DiscoveryDifficulty { get; set; } = 0f;
}

public class TransparentProperty
{
    public bool CanSeeThrough { get; set; } = false;
    public bool CanShootThrough { get; set; } = false;
    public bool CanHearThrough { get; set; } = false;
}

public class BreakableProperty
{
    public float Durability { get; set; } = 1.0f;
    public bool IsBroken { get; set; } = false;
}
