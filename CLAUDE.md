# CLAUDE.md

## Project Overview

STAKEOUT is a 1984-themed detective investigation game built in Godot 4.6 using C# (.NET) and the GL Compatibility renderer. The game is inspired by Sid Meier's Covert Action and Shadows of Doubt — a "game of minigames" focused on investigation mechanics (stakeouts, wiretapping, crime scene analysis, suspect tailing, etc.) presented through menu-heavy interactive screens, and powered by a detailed underlying dynamic simulation.

## Status and Roadmap

The truth-first rebuild (`docs/future_plans/2026-10-26-architecture-reassessment.md`, spec
`docs/superpowers/specs/2026-10-26-truth-first-rebuild.md`) replaced the old P1-P8 engine on branch `rebuild`: a
discrete-event simulation of a fully populated Boston, murders that grow out of real tensions, evidence observed from
History, and a playable investigation UI. `todo.md` holds loose ideas and bugs seen in playtesting. Each project gets
a spec in `docs/superpowers/specs/` and a plan in `docs/superpowers/plans/`.

## Build, Test, Run

- Build everything: `dotnet build stakeout.sln` (game, tests, benchmarks; should be 0 warnings)
- Tests (xUnit, ~190, about 30 s; a simulated week and two solved cases): `dotnet test stakeout.tests/`
- Benchmark: `dotnet run --project stakeout.benchmarks -c Release -- 42 7` (build a city, run 7 days, check invariants).
  `MURDER=1` adds a murder and prints the timelines; `-- bot 1 2 3` runs the scripted detective on those seeds (`CLUES=1` prints its clues)
- Godot editor/game: `../shared/tools/Godot_v4.6.1-stable_mono_win64/` (the VS Code "Play" launch config uses it)
- UI test run (screenshots of the opening of a case): `Godot_v4.6.1-stable_mono_win64_console.exe --path . --position 5000,100 --resolution 1280x720 -- --ui-test=<dir> --seed=5`,
  with a temporary `override.cfg` containing `[display]` `window/size/no_focus=true` (gitignored; delete it after).
  Never launch it while the user is using the PC: it must not take focus or cover the screen.

## Code Layout

- `src/simulation/` - pure C# simulation (uses `Godot.Vector2` only): `core/` scheduler and RNG, `space/` roads,
  `history/` facts, `agents/` planning and joint activities, `crime/`, `observers/`, `generation/`, `diagnostics/`.
- `src/investigation/` - the player's API (`Detective`, `GameSession`, `CaseEvaluator`, `AutoDetective`).
- `src/evidence/` - evidence board data model. `scenes/` - Godot UI, built in code (`scenes/ui/Ui.cs` theme).
- Determinism: simulation code uses `Rng.For/Unit/Chance(world.Seed, Purpose.X, keys...)` and the world's time; never
  `new Random()`, `DateTime.Now` or unordered iteration where order matters. Player actions go through `GameSession.Do`.
- Extending the sim: use the `add-address-type` and `add-npc-activity` skills. Run `InvariantChecker` after changes.

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
