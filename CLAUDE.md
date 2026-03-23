# CLAUDE.md

## Project Overview

STAKEOUT is a 1984-themed detective investigation game built in Godot 4.6 using C# (.NET) and the GL Compatibility renderer. The game is inspired by Sid Meier's Covert Action and Shadows of Doubt — a "game of minigames" focused on investigation mechanics (stakeouts, wiretapping, crime scene analysis, suspect tailing, etc.) presented through menu-heavy interactive screens, and powered by a detailed underlying dynamic simulation.

## Working Conventions

- **Git commands:** Run each git command as a separate Bash call — never chain git commands together with `&&` or `;`, and never prefix with `cd`. Permission rules match on command prefix, so chaining (e.g., `cd ... && git add ...`) or prefixing with `cd` breaks the match and triggers unnecessary permission prompts. The working directory is already the project root.
- **All shell commands:** Never prefix commands with `cd "path" &&`. The working directory is already the project root (`h:/Dropbox/sean-tower/Documents/git/stakeout`). Use commands directly (e.g., `dotnet test stakeout.tests/ -v minimal`, not `cd "H:\..." && dotnet test ...`).
- **Feedback scope:** All feedback given during this project is specific to this project. Store project-specific notes here in CLAUDE.md, not in external memory systems.