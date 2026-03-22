---
name: archive-session
description: Use when a feature is complete and the session folder needs to be archived to docs/plans. Invoked as /archive-session with an optional title argument.
---

# Archive Session

Move the contents (except .gitignore) of `/session/` to `/docs/plans/` with the correct naming convention, then leave `/session/` empty (except the .gitignore) and ready for the next feature.

## Steps

1. **Verify session has content.** List files in `/session/`. If empty, tell the user there's nothing to archive and stop.

2. **Determine the title.** If the user provided an argument (e.g. `/archive-session add inventory system`), join the words with underscores as the title. Otherwise, read `/session/requirements.md` (or `/session/plan.md` as fallback) and derive a short 2-4 word snake_case title from the feature described.

3. **Determine the folder name.** The format is `YYYY-MM-DD_NN_<title>` where:
   - `YYYY-MM-DD` is today's date
   - `NN` is a two-digit sequence ID starting at `01`, incrementing past any existing folders for today's date in `/docs/plans/`
   - `<title>` is the snake_case title from step 2

4. **Create the target folder** at `/docs/plans/<folder_name>/`.

5. **Move all files** from `/session/` into the new folder, except the .gitignore. Delete any temp files (e.g. `*.tmp.*`) rather than archiving them.

6. **Confirm the session folder is empty.** If `/session/` doesn't exist, recreate it. It must exist and be empty (except for .gitignore) when done.

7. **Report** the archive path to the user so they know where the files went.
