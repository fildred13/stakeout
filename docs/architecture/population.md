# Population

## Purpose
Fills the whole city at world creation so nothing is spawned later: everyone housed, every job filled, cars,
phones, friendships and the conflicts that become motives.

## Key Files
| File | Role |
|------|------|
| `src/simulation/generation/PopulationGenerator.cs` | People, households, homes, schedules, favourite places, cars, social graph, tensions, documents, guns, cameras |
| `src/simulation/entities/Person.cs` | Person, `Appearance` (what witnesses see), traits, `Household` |
| `src/simulation/entities/Relationship.cs` | Partner, Dating, Ex, Friend, Affair; `Tension` with `KnownBy` |
| `src/simulation/entities/Vehicle.cs`, `Item.cs` | Cars (make, colour, plate); items (documents, weapons, evidence) |
| `src/simulation/data/NameData.cs`, `VehicleData.cs` | Names, 1980s cars, colours, cigarette brands |

## How It Works
1. One person per business position, plus 18% non-workers (mostly retirees). Unique full names.
2. Households: couples (often sharing a surname), young roommates, singles; couples prefer houses, singles flats.
   Many apartment units stay vacant. Each household gets a phone number; 42% an answering machine.
3. Schedules from the shift; favourite diners, bars and a park near home or work; 86% own a car.
4. Social graph: 2-4 friends (coworkers, neighbours, anyone), dating among singles, exes, affairs.
5. Tensions: exes (jealousy), loans between friends (debt), affairs, embezzlement at some offices, workplace grudges.
   Each has people who know about it, and often a paper trail in the target's desk.
6. Address books in every home; registered or unregistered guns for gun owners; office street cameras watch the road.

## Key Decisions
- **Full population up front** (about 4,600 people for seed 42): no contradictions with what the player saw, staffed
  businesses, real witnesses. The event engine makes it affordable.
- **Motives and their witnesses are generated with the social graph**, so interviews have real answers.

## Connection Points
- Called by `WorldBuilder`; consumed by **agents** (schedules, favourites), **crime** (tensions), **observers**
  (appearance, vehicles), **investigation** (documents, phone book).
