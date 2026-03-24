# CLAUDE.md

## Project Overview

STAKEOUT is a 1984-themed detective investigation game built in Godot 4.6 using C# (.NET) and the GL Compatibility renderer. The game is inspired by Sid Meier's Covert Action and Shadows of Doubt — a "game of minigames" focused on investigation mechanics (stakeouts, wiretapping, crime scene analysis, suspect tailing, etc.) presented through menu-heavy interactive screens, and powered by a detailed underlying dynamic simulation.

## Working Conventions

### CRITICAL: No `cd` prefixing — applies to ALL agents and subagents

The working directory is already the project root (`h:/Dropbox/sean-tower/Documents/git/stakeout`). **NEVER** prefix any command with `cd`. This includes:

- `cd "path" && git add ...` — WRONG
- `cd "path" && dotnet test ...` — WRONG
- `git add src/foo.cs` — CORRECT
- `dotnet test stakeout.tests/ -v minimal` — CORRECT

**Why this matters:** Permission rules match on command prefix. `Bash(git add:*)` matches `git add foo` but NOT `cd ... && git add foo`. Prefixing with `cd` breaks every permission rule and forces the user to manually approve commands that should be auto-approved. This has been a recurring problem with subagents.

**If you are dispatching a subagent**, you MUST include this instruction in the subagent's prompt: "CRITICAL: Never prefix shell commands with `cd`. The working directory is already the project root. Run commands directly (e.g., `git add file.cs`, not `cd path && git add file.cs`). This breaks permission matching and is strictly prohibited."

### Other conventions

- **Git commands:** Run each git command as a separate Bash call — never chain with `&&` or `;`.
- **Feedback scope:** All feedback given during this project is specific to this project. Store project-specific notes here in CLAUDE.md, not in external memory systems.

### Documentation

- **Session orientation:** Before exploring code for a task, check `docs/architecture/` for a relevant subsystem map. If one exists, read it first — it tells you where to look and why things are shaped that way. Check `docs/design/` if you need to understand design intent or player experience goals.
- **Doc maintenance:** When finishing a development branch, invoke the `update-docs` skill before completing the branch. This checks whether architecture maps, design docs, or SOP skills need updating based on what changed.
- **Architecture doc format:** Each architecture doc follows this structure: **Purpose** (1-2 sentences), **Key Files** (table of file → role), **How It Works** (5-10 lines on data/control flow), **Key Decisions** (one-line rationale each), **Connection Points** (what other subsystems it talks to). Target 30-60 lines per doc.
