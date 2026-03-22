# CLAUDE.md

## Project Overview

STAKEOUT is a 1984-themed detective investigation game built in Godot 4.6 using C# (.NET) and the GL Compatibility renderer. The game is inspired by Sid Meier's Covert Action and Shadows of Doubt — a "game of minigames" focused on investigation mechanics (stakeouts, wiretapping, crime scene analysis, suspect tailing, etc.) presented through menu-heavy interactive screens, and powered by a detailed underlying dynamic simulation.

## Workflow

For every change we make, we work out of the /session/ folder. It is our working scratch space for each feature we build, where we store requirements, implementation plans, and our changelog.

Our workflow is:

1. With a clean git tree and an empty session folder, create a /session/requirements.md file which describes the required changes.
2. Create a /session/plan.md file which describes the technical implementation details of how we will accomplish it. During iterating on the plan, we may decide to iterate on the requirements.
3. As soon as we begin modifying code, we must always update /session/changelog.md, which describes the changes we have made to the system so far like a timeline. This is important - we record what we're doing AS we are doing it so that if we get interrupted we can come back and immediately jump back in by reading all three of these files to know what the overall goal is, and what we've already tried/built etc. As we are implementing the changes, we may need to modify the plan and/or requirements.
4. Verify the changes with unit tests and then manual testing. Iterate as needed.
5. When the feature is complete, move the contents of /session/ to /docs/plans, into a folder that follows the naming scheme `<4 digit year>-<two digit month>-<two digit day>_<two digit integer id starting from 01 each day>_<brief title with underscores>.md`.
6. Commit to git.