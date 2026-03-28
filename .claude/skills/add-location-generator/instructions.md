# Add Location Generator

Steps to add a new sublocation generator (e.g., HospitalGenerator, WarehouseGenerator).

## Files to create/modify

1. **Create generator:** `src/simulation/sublocations/<Name>Generator.cs`
2. **Register it:** `src/simulation/LocationGenerator.cs` — add to the location type → generator mapping
3. **Create tests:** `stakeout.tests/Simulation/Sublocations/<Name>GeneratorTests.cs`

## Generator structure

Every generator implements `ISublocationGenerator` and follows this pattern:

```csharp
public class <Name>Generator : ISublocationGenerator
{
    public SublocationGraph Generate(Address address, SimulationState state, Random rng)
    {
        var subs = new Dictionary<int, Sublocation>();
        var conns = new List<SublocationConnection>();

        // Local helpers — copy these exactly
        Sublocation Make(string name, string[] tags, int? floor) { ... }
        void Connect(Sublocation from, Sublocation to, SublocationConnection template = null) { ... }

        // 1. Create nodes (rooms, areas)
        var road = Make("Road", new[] { "road" }, 0);  // Always start with road

        // 2. Wire connections between nodes
        Connect(road, lobby, new SublocationConnection
        {
            Type = ConnectionType.Door,
            Name = "Front Door",
            Tags = new[] { "entrance" },
            Lockable = new LockableProperty { Mechanism = LockMechanism.Key },
            Breakable = new BreakableProperty()
        });

        return new SublocationGraph(subs, conns);
    }
}
```

## Required tags

Each generator must provide sublocations with these tags so decomposition strategies can find them:

- `"road"` — the exterior road node (always present)
- `"entrance"` — on the connection tag (door/gate) OR sublocation tag (lobby) for the main entry point
- Other common tags: `"work_area"`, `"food"`, `"restroom"`, `"social"`, `"bedroom"`, `"kitchen"`, `"living"`, `"private"`, `"storage"`
- Optional: `"staff_entry"` (back door for staff), `"covert_entry"` (for intrusion scenarios — can be on a connection or a sublocation like an alley)

## Connection conventions

- Connections are edges, not nodes. People are never "at" a door.
- Use `ConnectionType.OpenPassage` (default) for unlabeled passages between rooms.
- Use `ConnectionType.Door` with `Name` for named doors. Add `Lockable`/`Breakable` for exterior doors.
- Use `ConnectionType.Stairs` with `Name` for floor transitions between hallways.
- Entry-point tags go on the connection, not the sublocation behind it.
- Exception: if the entry point is a real place (e.g., an alley), the tag stays on the sublocation.

## Multi-floor buildings

See `ApartmentBuildingGenerator.cs` or `OfficeGenerator.cs` for the pattern:
- Single elevator node (`floor=null`) connected to each floor via Door edges
- Stairs are edges between consecutive floor hallways
- Use `Make(..., floor: n)` to assign floor numbers

## Test checklist

- Graph has expected sublocation count
- `FindByTag("road")` returns the road node
- `FindEntryPoint("entrance")` returns non-null
- `FindConnectionByTag("entrance")` returns the entrance connection (if tag is on connection)
- Key sublocations are reachable via `FindPath` from road
- Connection types and properties are correct on exterior doors
