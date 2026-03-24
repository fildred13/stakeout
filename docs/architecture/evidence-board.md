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

Connections are bidirectional — EvidenceConnection normalizes the two item IDs so the smaller is always `FromItemId`. This gives set-like deduplication via Equals/GetHashCode. Removing an item cascades to remove all its connections via `RemoveAllConnections()`.

`HasItem()` allows callers to check whether a simulation entity is already pinned before adding a duplicate.

The board has its own ID space (`_nextItemId`) separate from simulation entity IDs.

## Key Decisions
- **Separate ID space from simulation:** Board item IDs are independent. The same simulation entity could theoretically appear multiple times (not currently enforced, but `HasItem()` check exists).
- **Connections use value-equality semantics:** Normalized ordering + Equals/GetHashCode override means connection identity is purely structural, not reference-based.
- **Pure data model, no UI:** The board stores positions but has no rendering logic. UI scenes read from this model.

## Connection Points
- **Created by:** GameManager alongside SimulationState — currently independent, no automatic population
- **Links to simulation via:** EvidenceItem.EntityType + EntityId (foreign key to Person.Id or Address.Id in SimulationState)
- **Will be populated by:** UI layer (not yet implemented) based on player interactions and discoveries
