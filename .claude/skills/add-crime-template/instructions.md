# Adding a New Crime Template

Step-by-step guide for adding a new crime type to the system.

## Files to Create/Modify

1. **Create** `src/simulation/crimes/<TemplateName>Template.cs` — implements `ICrimeTemplate`
2. **Modify** `src/simulation/crimes/Crime.cs` — add new value to `CrimeTemplateType` enum
3. **Modify** `src/simulation/crimes/CrimeGenerator.cs` — register the template in the `_templates` dictionary
4. **Modify** `src/simulation/actions/ActionType.cs` — add any new ActionType values needed
5. **Modify** `src/simulation/actions/ActionExecutor.cs` — add execution logic for new ActionTypes
6. **Modify** `src/simulation/objectives/Objective.cs` — add new `ObjectiveType` value if needed
7. **Create** `stakeout.tests/Simulation/Crimes/<TemplateName>TemplateTests.cs` — tests for instantiation

## Steps

### 1. Add CrimeTemplateType enum value

In `src/simulation/crimes/Crime.cs`, add the new type:
```csharp
public enum CrimeTemplateType
{
    SerialKiller,
    NewType      // <-- add here
}
```

### 2. Add ObjectiveType if needed

In `src/simulation/objectives/Objective.cs`, add to ObjectiveType enum if the crime uses a new objective type.

### 3. Add ActionType values if needed

In `src/simulation/actions/ActionType.cs`, add any new physical actions NPCs will perform.

### 4. Implement ActionExecutor logic for new actions

In `src/simulation/actions/ActionExecutor.cs`, add a case to the Execute method for each new ActionType. This is where world state changes happen and Traces are produced.

### 5. Create the template class

In `src/simulation/crimes/<TemplateName>Template.cs`, implement `ICrimeTemplate`:
- `Type` — return the new CrimeTemplateType value
- `Name` — human-readable name
- `Instantiate(SimulationState state)` — cast NPCs, create Crime record, inject Objectives

Follow the SerialKillerTemplate pattern:
- Pick NPCs for roles (check IsAlive, not self, etc.)
- Create Crime record with Roles dictionary (null for roles populated later)
- Add Crime to `state.Crimes`
- Create Objectives with sequential ObjectiveSteps
- Mark instant steps with `IsInstant = true` and provide a `ResolveFunc`
- Add Objectives to the cast member's `Objectives` list
- Flag `NeedsScheduleRebuild = true` on affected people

### 6. Register in CrimeGenerator

In `src/simulation/crimes/CrimeGenerator.cs`, add to the constructor:
```csharp
_templates[CrimeTemplateType.NewType] = new NewTypeTemplate();
```

### 7. Write tests

Test that `Instantiate()` creates a Crime with correct roles, injects objectives, and that ObjectiveResolver can resolve the objectives into tasks.

## Reference

- See `SerialKillerTemplate.cs` for the canonical example
- See `docs/architecture/crime-system.md` for architecture overview
- Trace types: Item, Sighting, Mark, Condition, Record (defined in `src/simulation/traces/Trace.cs`)
