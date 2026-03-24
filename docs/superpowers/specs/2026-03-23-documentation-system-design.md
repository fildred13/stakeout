# Documentation System Design

## Problem

As STAKEOUT grows, every new session requires expensive cold-start discovery — grepping, reading wrong files, reconstructing how subsystems connect. Design intent lives only in the developer's head or in point-in-time specs that get superseded. Extensible patterns (job types, crime templates) require remembering non-obvious multi-file coordination that gets lost between sessions.

## Goals

1. **Minimize token waste** — give the AI agent a fast entrypoint into the code so it orients in seconds, not minutes
2. **Preserve design intent** — capture *why* things are shaped the way they are, not just *what* they do
3. **Codify extensible patterns** — when a system is designed to be extended (new job types, new entities), capture the procedure as a skill
4. **Keep maintenance cost low** — docs must earn their keep; no speculative documentation
5. **Human-readable** — terse and structured, but with enough "why" context that a human can skim cold

## Non-Goals

- Comprehensive documentation of every class or file
- Modifying the superpowers workflow (specs, plans, brainstorming)
- Auto-generating documentation
- Documenting single-file utilities, enums, or data classes

## Design

### 1. Documentation Structure

#### `docs/design/` — Design Intent (existing, unchanged)

What the game *should feel like*, player experience, gameplay philosophy. Organized by topic (`overview.md`, `UI.md`, `gameplay/`, `simulation/`). Written in natural prose. Updated when design intent changes.

#### `docs/architecture/` — Subsystem Index Maps (new)

One file per subsystem. Only created when a subsystem has 3+ files with non-obvious relationships — i.e., when cold-start discovery would be expensive.

**Format (each file, 30-60 lines):**

```markdown
# [Subsystem Name]

## Purpose
1-2 sentences on what this subsystem does.

## Key Files
| File | Role |
|------|------|
| `path/to/file.cs` | One-line description |

## How It Works
Brief description of data/control flow between the files. 5-10 lines.

## Key Decisions
- Decision: rationale (1 line each)

## Connection Points
- Talks to [other subsystem] via [mechanism]
```

**Creation threshold:** A subsystem earns a doc when it has 3+ files with non-obvious relationships. Single-file systems, enums, and data classes are self-documenting.

**Initial docs to create:**
- `simulation-core.md` — SimulationManager, SimulationState, core entities, generators
- `simulation-scheduling.md` — Goal, DailySchedule, ScheduleBuilder, PersonBehavior, SleepScheduleCalculator
- Event journal (EventJournal, SimulationEvent) is folded into `simulation-core.md` — only 2 files, below the standalone threshold
- `evidence-board.md` — EvidenceBoard, EvidenceItem, EvidenceConnection

### 2. Project Skills

#### `update-docs` Skill

**Location:** `.claude/skills/update-docs/SKILL.md` (project skill, alongside other project skills).

Invoked when finishing a development branch, before `finishing-a-development-branch`. Checklist:

1. What subsystems were touched in this branch? (check git diff)
2. For each touched subsystem:
   - Does an architecture doc exist? If yes, does it need updating?
   - If no doc exists, has the subsystem crossed the complexity threshold (3+ files, non-obvious relationships)?
3. Did the feature change any design intent or player experience? If so, update the relevant `docs/design/` file.
4. Did this feature introduce an extensible pattern? If so, flag it — create an SOP skill if one doesn't exist.

Most invocations result in "nothing to update" and take seconds.

#### Developer SOP Skills

Created at build time when implementing a system component that is **naturally extensible** — designed to have more instances added over time, where adding an instance requires coordinated changes across multiple files.

**Trigger:** The system has a pattern where adding a new instance requires coordinated multi-file changes. Examples: crime templates, job types, location types, entity types, minigame screens.

**Not a trigger:** One-off systems, or things where adding an instance is obvious (e.g., a single enum with no downstream effects).

**Location:** `.claude/skills/` alongside other project skills.

**Format:** Step-by-step procedure with the specific files and changes required.

### 3. CLAUDE.md Additions

Three new sections (~15-20 lines total):

**Session orientation:** Before exploring code for a task, check `docs/architecture/` for a relevant subsystem map. Read it first to orient. Check `docs/design/` for design intent or player experience goals.

**Doc maintenance:** When finishing a development branch, invoke the `update-docs` skill before completing the branch.

**Architecture doc format:** Brief format reference (Purpose, Key files, How it works, Key decisions, Connection points) for consistency.

### 4. Integration with Superpowers Workflow

Doc maintenance slots into the existing workflow as one step before finishing:

```
brainstorming → spec → plan → implement → UPDATE DOCS → finish branch
```

- **Brainstorming/planning:** Reference `docs/architecture/` and `docs/design/` to understand current state (driven by CLAUDE.md instruction, no skill changes)
- **Plan writing:** If the feature introduces an extensible pattern, include an SOP skill creation step in the plan
- **Finishing:** Invoke `update-docs` before `finishing-a-development-branch`

No superpowers skills are modified.

### 5. Cleanup

- Remove `.claude/skills/archive-session/` — obsolete, replaced by superpowers workflow

## Maintenance Philosophy

- Docs are part of "done", not a separate activity
- A doc that's too long is too verbose — keep architecture maps at 30-60 lines
- A stale doc is worse than no doc — the `update-docs` skill prevents drift
- Create docs when they earn their keep, not speculatively
- The code is always the truth; docs are an index into the code
