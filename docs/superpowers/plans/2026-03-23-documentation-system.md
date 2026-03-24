# Documentation System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Establish a lightweight documentation system with architecture index maps, an update-docs skill, and CLAUDE.md instructions to minimize cold-start discovery cost.

**Architecture:** Three architecture docs for existing subsystems, one project skill for doc maintenance, and CLAUDE.md additions for behavioral discipline. No code changes — purely docs, skills, and config.

**Tech Stack:** Markdown, Claude Code skills (SKILL.md format)

**Spec:** `docs/superpowers/specs/2026-03-23-documentation-system-design.md`

---

## File Structure

### New Files

| File | Responsibility |
|------|---------------|
| `docs/architecture/simulation-core.md` | Index map for simulation core: SimulationManager, SimulationState, entities, generators, event journal, GameClock, MapConfig |
| `docs/architecture/simulation-scheduling.md` | Index map for scheduling: Goal, DailySchedule, ScheduleBuilder, PersonBehavior, SleepScheduleCalculator |
| `docs/architecture/evidence-board.md` | Index map for evidence board: EvidenceBoard, EvidenceItem, EvidenceConnection |
| `.claude/skills/update-docs/SKILL.md` | Skill: checklist for updating docs when finishing a branch |

### Modified Files

| File | Changes |
|------|---------|
| `CLAUDE.md` | Add session orientation, doc maintenance, and architecture doc format sections |

### Deleted Files

| File | Reason |
|------|--------|
| `.claude/skills/archive-session/SKILL.md` | Obsolete — replaced by superpowers workflow |

---

### Task 1: Create `docs/architecture/simulation-core.md`

**Files:**
- Create: `docs/architecture/simulation-core.md`

- [ ] **Step 1: Write the simulation core architecture doc**

```markdown
# Simulation Core

## Purpose
The core simulation layer that manages all game world state: entities (people, locations, jobs), world generation, time progression, and event logging. Everything flows through SimulationState as the single source of truth.

## Key Files
| File | Role |
|------|------|
| `src/simulation/SimulationState.cs` | Central data store — dictionaries of all entities, GameClock, EventJournal, ID generation |
| `src/simulation/SimulationManager.cs` | Godot Node orchestrator — initializes world and Player in `_Ready()`, ticks clock and updates people in `_Process()`, emits events (PersonAdded, AddressAdded, PlayerCreated) for UI notification |
| `src/simulation/PersonGenerator.cs` | Creates a Person with home, job, work address, sleep schedule, daily schedule, and initial state |
| `src/simulation/LocationGenerator.cs` | Generates addresses, streets, cities — handles street reuse and realistic address numbering |
| `src/simulation/entities/Player.cs` | Player entity — home address and current address |
| `src/simulation/GameClock.cs` | Tracks in-game DateTime with a float TimeScale property (scaling applied by SimulationManager) |
| `src/simulation/MapConfig.cs` | Map bounds and distance-based travel time calculation |
| `src/simulation/events/EventJournal.cs` | Append-only event store, dual-indexed (global list + per-person dictionary) |
| `src/simulation/events/SimulationEvent.cs` | Immutable event record with timestamp, person ID, event type, and contextual IDs |
| `src/simulation/data/NameData.cs` | First/last name arrays for random person generation |
| `src/simulation/data/StreetData.cs` | Street name/suffix arrays for random address generation |
| `src/GameManager.cs` | Top-level Godot Node — creates SimulationState, EvidenceBoard, SimulationManager; wires them together |

## How It Works
GameManager creates a SimulationState (empty) and SimulationManager, adding it to the scene tree. On `_Ready()`, SimulationManager generates a city scaffold (Boston, USA) via LocationGenerator, spawns 5 NPCs via PersonGenerator, and creates a Player with a home address. It emits C# events (PersonAdded, AddressAdded, PlayerCreated) so UI layers can react to initialization.

Each frame, SimulationManager ticks the GameClock (applying TimeScale to delta), then calls PersonBehavior.Update() for each person with their schedule. All state changes are logged to the EventJournal.

All systems read/write through SimulationState — there is no direct system-to-system communication.

## Key Decisions
- **State-primary with event journal:** Entities hold mutable current state; the journal is a parallel append-only log for future replay/retroactive history. Chosen over event-sourced because the simulation needs fast current-state reads every frame.
- **Single SimulationState:** All entity dictionaries in one place for simple access patterns. No separate repositories per entity type.
- **PersonGenerator returns (Person, DailySchedule):** Schedule is stored in SimulationManager, not on Person, because schedules will eventually be rebuilt daily when reactive re-evaluation is added.
- **ID generation is centralized:** `SimulationState.GenerateEntityId()` provides monotonically increasing IDs across all entity types.

## Connection Points
- **Scheduling system** reads from SimulationState (jobs, addresses) and PersonBehavior writes back to Person entities and EventJournal
- **Evidence board** is created by GameManager alongside SimulationState but currently has no direct data flow from simulation (UI layer will bridge this)
- **UI/scenes** access simulation through GameManager's public `State` and `SimulationManager` properties
```

- [ ] **Step 2: Verify doc is within target length (30-60 lines)**

Count the lines. Trim if over 60, expand if under 30.

- [ ] **Step 3: Commit**

```bash
git add docs/architecture/simulation-core.md
git commit -m "docs: add simulation core architecture map"
```

---

### Task 2: Create `docs/architecture/simulation-scheduling.md`

**Files:**
- Create: `docs/architecture/simulation-scheduling.md`

- [ ] **Step 1: Write the scheduling architecture doc**

```markdown
# Simulation Scheduling

## Purpose
Gives each Person an AI "brain" that follows a daily schedule derived from prioritized goals. Handles goal resolution, schedule building, sleep computation, and per-frame activity execution with state transitions.

## Key Files
| File | Role |
|------|------|
| `src/simulation/scheduling/Goal.cs` | GoalType enum (BeAtWork, BeAtHome, Sleep), Goal class (type, priority, time window), GoalSet, GoalSetBuilder |
| `src/simulation/scheduling/SleepScheduleCalculator.cs` | Pure function: computes sleep/wake times from job shift + commute, handles midnight wrapping |
| `src/simulation/scheduling/ScheduleBuilder.cs` | Converts (GoalSet, addresses, MapConfig) → DailySchedule — minute-by-minute priority resolution, block merging, travel insertion |
| `src/simulation/scheduling/DailySchedule.cs` | DailySchedule (list of ScheduleEntry) and ScheduleEntry (activity, time window, target/from address IDs) |
| `src/simulation/scheduling/PersonBehavior.cs` | Per-frame updater: compares current activity to schedule, triggers transitions, interpolates travel, logs events to journal |

## How It Works
When a Person is created, PersonGenerator orchestrates the full pipeline:
1. GoalSetBuilder creates 3 goals: Sleep (priority 30), Work (priority 20), Home (priority 10, always active)
2. SleepScheduleCalculator computes sleep/wake times, adjusting around job shifts and commute
3. ScheduleBuilder resolves goals minute-by-minute — highest priority wins — then merges into contiguous blocks and inserts travel entries between location changes

Each frame, PersonBehavior.Update() checks the person's current activity against what the schedule says they should be doing. On mismatch, it triggers a transition: logging the end of the old activity, starting travel if a location change is needed, or switching directly. During travel, position is interpolated between origin and destination based on elapsed time.

## Key Decisions
- **Priority-based goal resolution:** Each minute has a winning goal. This is simple, deterministic, and easy to extend with new goal types (criminal plots, player interactions) by just adding goals with appropriate priorities.
- **Pre-computed daily schedules:** Schedules are built once per day (currently once at creation), not evaluated reactively. Reactive re-evaluation is a designed-for future extension.
- **Travel as explicit schedule entries:** ScheduleBuilder inserts TravellingByCar entries into the schedule, so PersonBehavior doesn't need to decide when to travel — it just follows the schedule.
- **Sleep priority is highest (30):** Ensures people sleep even if other goals overlap. Work (20) beats Home (10).

## Connection Points
- **Reads from:** SimulationState (Job shift hours, Address positions for commute calculation, MapConfig for travel times)
- **Writes to:** Person entity (CurrentActivity, CurrentPosition, CurrentAddressId, TravelInfo) and EventJournal (all transitions)
- **Called by:** SimulationManager._Process() invokes PersonBehavior.Update() each frame
- **Built by:** PersonGenerator orchestrates the full Goal → Sleep → Schedule pipeline during NPC creation
```

- [ ] **Step 2: Verify doc is within target length (30-60 lines)**

Count the lines. Trim if over 60, expand if under 30.

- [ ] **Step 3: Commit**

```bash
git add docs/architecture/simulation-scheduling.md
git commit -m "docs: add simulation scheduling architecture map"
```

---

### Task 3: Create `docs/architecture/evidence-board.md`

**Files:**
- Create: `docs/architecture/evidence-board.md`

- [ ] **Step 1: Write the evidence board architecture doc**

```markdown
# Evidence Board

## Purpose
A graph-based data model for the player's investigation corkboard. Tracks evidence items (linked to simulation entities) and connections between them (the "red twine"). Pure data layer — no UI logic.

## Key Files
| File | Role |
|------|------|
| `src/evidence/EvidenceBoard.cs` | Graph container: items dictionary + connections list, add/remove with cascading deletes |
| `src/evidence/EvidenceItem.cs` | One pinned item: board-local ID, entity type/ID (links to simulation), board position |
| `src/evidence/EvidenceConnection.cs` | Undirected edge between two items, normalized (FromItemId < ToItemId), value-equality semantics |
| `src/evidence/EvidenceEntityType.cs` | Enum: Person, Address (will grow as more entity types become evidence-worthy) |

## How It Works
EvidenceBoard maintains a dictionary of EvidenceItems (keyed by board-local ID) and a list of EvidenceConnections. Each EvidenceItem maps a simulation entity (Person or Address, by type + ID) to a position on the board.

Connections are bidirectional — EvidenceConnection normalizes the two item IDs so the smaller is always `FromItemId`. This gives set-like deduplication via Equals/GetHashCode. Removing an item cascades to remove all its connections.

The board has its own ID space (`_nextItemId`) separate from simulation entity IDs.

## Key Decisions
- **Separate ID space from simulation:** Board item IDs are independent. The same simulation entity could theoretically appear multiple times (not currently enforced, but `HasItem()` check exists).
- **Connections use value-equality semantics:** Normalized ordering + Equals/GetHashCode override means connection identity is purely structural, not reference-based.
- **Pure data model, no UI:** The board stores positions but has no rendering logic. UI scenes read from this model.

## Connection Points
- **Created by:** GameManager alongside SimulationState — currently independent, no automatic population
- **Links to simulation via:** EvidenceItem.EntityType + EntityId (foreign key to Person.Id or Address.Id in SimulationState)
- **Will be populated by:** UI layer (not yet implemented) based on player interactions and discoveries
```

- [ ] **Step 2: Verify doc is within target length (30-60 lines)**

Count the lines. Trim if over 60, expand if under 30.

- [ ] **Step 3: Commit**

```bash
git add docs/architecture/evidence-board.md
git commit -m "docs: add evidence board architecture map"
```

---

### Task 4: Create `update-docs` skill

**Files:**
- Create: `.claude/skills/update-docs/SKILL.md`

- [ ] **Step 1: Write the update-docs skill**

```markdown
---
name: update-docs
description: Use when finishing a development branch to check if architecture docs, design docs, or SOP skills need updating based on what changed.
---

# Update Docs

Run this checklist when finishing a development branch, before invoking the superpowers `finishing-a-development-branch` skill.

## Steps

1. **Identify touched subsystems.** Run `git diff main --name-only` (or the appropriate base branch) to see what files changed. Group them by subsystem (simulation-core, simulation-scheduling, evidence-board, or other).

2. **Check architecture docs.** For each touched subsystem:
   - Does `docs/architecture/<subsystem>.md` exist?
     - **Yes:** Read the doc and the changed files. Does the doc still accurately describe the key files, how it works, key decisions, and connection points? If not, update it.
     - **No:** Has this subsystem crossed the complexity threshold (3+ files with non-obvious relationships)? If yes, create a new architecture doc following the format in CLAUDE.md.

3. **Check design docs.** Did the feature change any design intent or player experience? If so, update the relevant file in `docs/design/`.

4. **Check for extensible patterns.** Did this feature introduce a naturally extensible pattern — a system designed to have more instances added over time, where adding an instance requires coordinated multi-file changes? If so, check if an SOP skill exists in `.claude/skills/`. If not, create one with step-by-step instructions for adding a new instance.

5. **Report.** Tell the user what was updated (or that nothing needed updating).

## Guidelines

- Keep architecture docs at 30-60 lines. If a doc is growing beyond that, it's too verbose — focus on the index/map, not exhaustive description.
- The code is the truth. Docs are an index into the code, not a replacement for reading it.
- Don't create docs speculatively. A subsystem needs 3+ files with non-obvious relationships to warrant an architecture doc.
- When in doubt about whether a design doc needs updating, check `docs/design/` to see if the current content is still accurate for the feature area you touched.
```

- [ ] **Step 2: Commit**

```bash
git add .claude/skills/update-docs/SKILL.md
git commit -m "feat: add update-docs skill for branch-finishing doc maintenance"
```

---

### Task 5: Update CLAUDE.md

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Add documentation sections to CLAUDE.md**

Add the following after the existing "Other conventions" section:

```markdown
### Documentation

- **Session orientation:** Before exploring code for a task, check `docs/architecture/` for a relevant subsystem map. If one exists, read it first — it tells you where to look and why things are shaped that way. Check `docs/design/` if you need to understand design intent or player experience goals.
- **Doc maintenance:** When finishing a development branch, invoke the `update-docs` skill before completing the branch. This checks whether architecture maps, design docs, or SOP skills need updating based on what changed.
- **Architecture doc format:** Each architecture doc follows this structure: **Purpose** (1-2 sentences), **Key Files** (table of file → role), **How It Works** (5-10 lines on data/control flow), **Key Decisions** (one-line rationale each), **Connection Points** (what other subsystems it talks to). Target 30-60 lines per doc.
```

- [ ] **Step 2: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: add documentation habits to CLAUDE.md"
```

---

### Task 6: Remove obsolete archive-session skill

**Files:**
- Delete: `.claude/skills/archive-session/SKILL.md`

- [ ] **Step 1: Delete the archive-session skill directory**

```bash
rm -rf .claude/skills/archive-session/
```

- [ ] **Step 2: Commit**

```bash
git add -A .claude/skills/archive-session/
git commit -m "chore: remove obsolete archive-session skill"
```
