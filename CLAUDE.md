# CLAUDE.md

## Project Overview

STAKEOUT is a 1984-themed detective investigation game built in Godot 4.6 using C# (.NET) and the GL Compatibility renderer. The game is inspired by Sid Meier's Covert Action and Shadows of Doubt — a "game of minigames" focused on investigation mechanics (stakeouts, wiretapping, crime scene analysis, suspect tailing, etc.) presented through menu-heavy interactive screens, and powered by a detailed underlying dynamic simulation.

## Status and Roadmap

Development follows the project sequence in `docs/future_plans/simulation_overhaul_plan.md` (P1-P8). P1-P5 are done
(locations, traces, NPC brain, businesses, groups/dating). Next up: P6 History Backfill & NPC Spawning, then P7 Crime
Templates (the crime system is currently a stub), then P8 Player Interaction UI. `todo.md` holds loose ideas and bugs
seen in playtesting. Each project gets a spec in `docs/superpowers/specs/` and a plan in `docs/superpowers/plans/`.

## Build, Test, Run

- Build everything: `dotnet build stakeout.sln` (game, tests, benchmarks; should be 0 warnings)
- Tests (xUnit, ~400, a few seconds): `dotnet test stakeout.tests/`
- Benchmarks: `dotnet run --project stakeout.benchmarks/` exists but is hollow (its tick loop is an empty TODO since P3, and it crashes at 1000 NPCs)
- Godot editor/game: `../shared/tools/Godot_v4.6.1-stable_mono_win64/` (the VS Code "Play" launch config uses it)

## Code Layout

- `src/simulation/` - pure C# simulation (no scene nodes except `SimulationManager`; uses `Godot.Vector2` only). All tests target this.
- `src/evidence/` - evidence board data model. `scenes/` - Godot UI scenes and their scripts.
- Simulation code takes a `Random` and reads `state.Clock.CurrentTime`; never `new Random()` or `DateTime.Now`, so worlds replay from a seed.
- Extending the sim: use the `add-address-type` and `add-npc-objective` skills.

## Working Conventions

### CRITICAL: No `cd` prefixing — applies to ALL agents and subagents

The working directory is already the project root (`G:/gamedev/stakeout`). **NEVER** prefix any command with `cd`. This includes:

- `cd "path" && git add ...` — WRONG
- `cd "path" && dotnet test ...` — WRONG
- `git add src/foo.cs` — CORRECT
- `dotnet test stakeout.tests/ -v minimal` — CORRECT

**Why this matters:** Permission rules match on command prefix. `Bash(git add:*)` matches `git add foo` but NOT `cd ... && git add foo`. Prefixing with `cd` breaks every permission rule and forces the user to manually approve commands that should be auto-approved. This has been a recurring problem with subagents.

**If you are dispatching a subagent**, you MUST include this instruction in the subagent's prompt: "CRITICAL: Never prefix shell commands with `cd`. The working directory is already the project root. Run commands directly (e.g., `git add file.cs`, not `cd path && git add file.cs`). This breaks permission matching and is strictly prohibited."

### Other conventions

- **Git commands:** Run each git command as a separate Bash call — never chain with `&&` or `;`. Make one call, and then the other. NEVER use `git -C`, because this also breaks permissioning. Just call `git`.
- **Feedback scope:** All feedback given during this project is specific to this project. Store project-specific notes here in CLAUDE.md, not in external memory systems.

### Documentation

- **Session orientation:** Before exploring code for a task, check `docs/architecture/` for a relevant subsystem map. If one exists, read it first — it tells you where to look and why things are shaped that way. Check `docs/design/` if you need to understand design intent or player experience goals.
- **Doc maintenance:** When finishing a development branch, invoke the `update-docs` skill before completing the branch. This checks whether architecture maps, design docs, or SOP skills need updating based on what changed.
- **Architecture doc format:** Each architecture doc follows this structure: **Purpose** (1-2 sentences), **Key Files** (table of file → role), **How It Works** (5-10 lines on data/control flow), **Key Decisions** (one-line rationale each), **Connection Points** (what other subsystems it talks to). Target 30-60 lines per doc.
