# Changes: Simulation Basics

## New Files

### `stakeout.csproj`
- Created .NET 8 project file with Godot.NET.Sdk/4.4.0, root namespace `Stakeout`.

### `src/simulation/GameClock.cs`
- Tracks in-game `DateTime` starting at Jan 1, 1984 00:00:00.
- `Tick(double deltaSec)` advances time at 1:1 real-time.
- Exposes `ElapsedSeconds` for trigger checks.

### `src/simulation/entities/Person.cs`
- Data class with `Id`, `FirstName`, `LastName`, `CreatedAt`, `FullName` (computed).

### `src/simulation/SimulationState.cs`
- Central world state. Holds `GameClock`, `Dictionary<int, Person>` registry, monotonic ID generator.

### `src/simulation/data/NameData.cs`
- Static arrays of 50 first names and 50 last names (1980s America flavor).

### `src/simulation/PersonGenerator.cs`
- `GeneratePerson(SimulationState)` creates a Person with random name, assigns ID, stamps creation time, adds to state.

### `src/simulation/SimulationManager.cs`
- Godot `Node` that owns `SimulationState`. Ticks the clock each frame.
- After 1 second of elapsed time, generates one Person and fires `PersonAdded` event.

### `scenes/simulation_debug/SimulationDebug.tscn` + `.cs`
- Debug UI scene with clock display (HH:MM:SS) and ItemList for people.
- Creates `SimulationManager` as child node on ready.
- Updates clock label every frame, adds people to list via event subscription.

## Modified Files

### `scenes/main_menu/MainMenu.cs`
- `_OnNewCareerPressed()` now calls `ChangeSceneToFile` to load SimulationDebug scene.
