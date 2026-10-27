# Evidence Board

## Purpose
The player's corkboard: pinned people, places and clues joined by red twine. A pure data model plus a pan/zoom scene.

## Key Files
| File | Role |
|------|------|
| `src/evidence/EvidenceBoard.cs` | Items dictionary + connections list, add/remove with cascading deletes |
| `src/evidence/EvidenceItem.cs` | One pinned item: board-local id, entity type and id, board position |
| `src/evidence/EvidenceConnection.cs` | Undirected edge, normalized (From < To), value equality |
| `src/evidence/EvidenceEntityType.cs` | Person, Address, Clue |
| `scenes/evidence_board/EvidenceBoardScene.cs` | Corkboard scene: polaroids, twine, dossier on click |
| `scenes/evidence_board/DossierWindow.cs` | What the detective knows about a pinned item (never more) |

## How It Works
Items map an entity (Person id, Address id, or the detective's Clue id) to a board position. The shell pins things
from the map, the People tab, interviews and the notebook (`GameShell.PinToBoard`); the "Board" button opens the
scene. Connections are made by dragging from one thumbtack to another.

## Key Decisions
- **Separate id space from the simulation**; `HasItem` prevents duplicates.
- **Dossiers show player knowledge only** (`Detective.People`, clues), so the board never leaks the truth.
- **Board layout is UI state:** it is not part of the command-log save (a fresh board after loading).

## Connection Points
- Owned by `GameManager` (reset with each game); reads `GameSession.Detective` for captions and dossiers.
