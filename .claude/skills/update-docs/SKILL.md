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
