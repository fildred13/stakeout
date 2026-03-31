# Project 2: Fixtures & Trace System — Design Spec

## Context

This is Project 2 from the Simulation Overhaul Master Plan (`docs/future_plans/simulation_overhaul_plan.md`). It builds on Project 1 (Location Hierarchy), which delivered Address → Location → SubLocation with tags, access points, and composable address type templates.

## Goal

Introduce fixtures (stable interactable objects at locations) and redesign the trace system (dynamic evidence from NPC actions/events). Provide the TraceEmitter API that P3's action engine will call, and the InvestigationQuery interface that P8's UI will consume. Remove FingerprintService.

## Design Decisions

### Fixtures Are Serialized

Fixtures are generated from address templates (same as Locations/SubLocations) and stored in `SimulationState.Fixtures`. They ARE serialized in saves — this avoids the complexity of deterministic ID regeneration and keeps trace → fixture references stable across save/load. This departs from the master plan's original "not serialized" note in favor of simplicity.

### Fixture Entity

```csharp
public class Fixture
{
    public int Id { get; set; }
    public int? LocationId { get; set; }        // set if attached to a Location
    public int? SubLocationId { get; set; }      // set if attached to a SubLocation
    public string Name { get; set; }             // "Trash Can"
    public FixtureType Type { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
}

public enum FixtureType
{
    TrashCan,
    // Future: Computer, Mailbox, Safe, FilingCabinet, AnsweringMachine,
    //         Dresser, Desk, Shelf, Vehicle
}
```

- Exactly one of `LocationId` or `SubLocationId` is set.
- Only `TrashCan` is implemented initially. The enum comment preserves future ideas.
- Tags follow the same pattern as Location/SubLocation.

### Trace Entity

```csharp
public class Trace
{
    public int Id { get; set; }
    public TraceType Type { get; set; }
    public string Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Where
    public int? LocationId { get; set; }
    public int? SubLocationId { get; set; }
    public int? FixtureId { get; set; }          // trace is inside/on a fixture

    // Who
    public int? CreatedByPersonId { get; set; }
    public int? AttachedToPersonId { get; set; }  // e.g., wound on a corpse

    // Decay
    public int? DecayDays { get; set; }           // null = permanent
}

public enum TraceType
{
    Mark,           // blood pool, scuff marks, broken glass
    Item,           // dropped receipt, forgotten jacket
    Sighting,       // witness report of someone being somewhere
    Record,         // email, letter, phone message
    Fingerprint,    // on a surface, door, fixture
    Condition,      // wound, cause of death — attached to a person
}
```

### Three Decay Paths

1. **Time-based:** `DecayDays` is set. Trace expires when `CreatedAt + DecayDays < currentTime`. Fingerprints default to 7 days.
2. **Permanent:** `DecayDays` is null. Persists until explicitly deactivated. Example: bullet wound.
3. **Event-based:** An action sets `IsActive = false`. Example: crime scene cleanup removes a blood pool.

Decay is evaluated at query time, not per-tick. No iteration over all traces each tick.

### No Reverse References

Fixtures, locations, and persons do NOT store lists of their trace IDs. Instead, `SimulationState` provides query helpers that filter the traces dictionary:

- `GetTracesForLocation(int locationId)`
- `GetTracesForSubLocation(int subLocationId)`
- `GetTracesForFixture(int fixtureId)`
- `GetTracesForPerson(int personId)`

These filter out inactive and time-expired traces. This matches the existing pattern (e.g., `GetSubLocationsForLocation()`).

Similarly for fixtures:
- `GetFixturesForLocation(int locationId)`
- `GetFixturesForSubLocation(int subLocationId)`

### TraceEmitter API

Static utility with one method per common emission pattern. Each creates a Trace, adds it to state, returns the ID.

```csharp
public static class TraceEmitter
{
    public static int EmitFingerprint(SimulationState state, int personId,
        int locationId, string description);

    public static int EmitFingerprintOnFixture(SimulationState state, int personId,
        int fixtureId, string description);

    public static int EmitMark(SimulationState state, int locationId,
        int? subLocationId, string description, int? decayDays = null);

    public static int EmitItem(SimulationState state, int? personId,
        int locationId, int? fixtureId, string description, int? decayDays = null);

    public static int EmitCondition(SimulationState state, int personId,
        string description);

    public static int EmitRecord(SimulationState state, int fixtureId,
        int? personId, string description);

    public static int EmitSighting(SimulationState state, int personId,
        int locationId, string description, int? decayDays = null);
}
```

Fingerprints default to `DecayDays = 7`. Conditions default to permanent (`DecayDays = null`).

### InvestigationQuery

Static query service that merges fixtures and traces for a location. The future UI (P8) wraps this with timing, difficulty, and reveal mechanics.

```csharp
public static class InvestigationQuery
{
    public static InvestigationResult GetDiscoveriesForLocation(SimulationState state,
        int locationId, DateTime currentTime);

    public static InvestigationResult GetDiscoveriesForSubLocation(SimulationState state,
        int subLocationId, DateTime currentTime);

    public static List<Trace> GetFixtureTraces(SimulationState state,
        int fixtureId, DateTime currentTime);

    public static List<Trace> GetPersonTraces(SimulationState state,
        int personId, DateTime currentTime);
}

public class InvestigationResult
{
    public List<Fixture> Fixtures { get; set; }
    public List<Trace> Traces { get; set; }   // non-fixture traces at this location
}
```

All methods filter out `IsActive = false` and time-expired traces.

### Template Integration

Fixtures are generated inside existing address templates using a new `LocationBuilders` helper:

```csharp
public static Fixture CreateFixture(SimulationState state, FixtureType type,
    string name, int? locationId, int? subLocationId, string[] tags = null);
```

Each address type gets trash cans in sensible locations (kitchen, alley, break room, etc.). Only `TrashCan` for now.

### Removals

- **FingerprintService** — deleted entirely. Responsibilities absorbed by `TraceEmitter.EmitFingerprint()`.
- **Existing Trace.Data dictionary** — replaced by explicit nullable fields on the redesigned Trace entity.

## Files Affected

### New Files
- `src/simulation/fixtures/Fixture.cs` — entity
- `src/simulation/fixtures/FixtureType.cs` — enum
- `src/simulation/traces/TraceEmitter.cs` — emission API
- `src/simulation/traces/InvestigationQuery.cs` — query service
- `src/simulation/traces/InvestigationResult.cs` — query result type

### Modified Files
- `src/simulation/traces/Trace.cs` — redesigned with explicit fields
- `src/simulation/traces/TraceType.cs` — updated enum (if separate file, otherwise in Trace.cs)
- `src/SimulationState.cs` — add Fixtures dictionary + query helpers
- `src/simulation/addresses/LocationBuilders.cs` — add CreateFixture helper
- All 7 address templates — add trash can fixtures
- `src/simulation/crimes/SerialKillerTemplate.cs` — update trace references if needed

### Deleted Files
- `src/simulation/traces/FingerprintService.cs`

### Test Files
- `stakeout.tests/Simulation/Fixtures/FixtureTests.cs`
- `stakeout.tests/Simulation/Traces/TraceEmitterTests.cs`
- `stakeout.tests/Simulation/Traces/InvestigationQueryTests.cs`
- Update existing trace tests for new Trace shape
