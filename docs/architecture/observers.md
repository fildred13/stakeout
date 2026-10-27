# Observers

## Purpose
Turn History into evidence with honest limits: what a witness could have noticed, what a camera recorded, which
prints are still on a surface. Pure functions of History, the seed and the time of asking.

## Key Files
| File | Role |
|------|------|
| `src/simulation/observers/WitnessMemory.cs` | `Recall(witness, address, from, to, askedAt)`: people in the same room or lobby, visitors seen through a peephole, cars pulling up, noises through walls |
| `src/simulation/observers/Fingerprints.cs` | `Lift(surface, at)`: bare-handed touches since the last wipe, freshest first, plus residents' implied prints |
| `src/simulation/observers/Cameras.cs` | Street cameras (trips crossing the watched cell) and lobby cameras (presence), 72-hour tapes |
| `src/simulation/observers/Descriptions.cs` | `PersonDescription` (partial, sometimes wrong) and `Fit`; vehicle descriptions by day and night |
| `src/simulation/agents/PhoneSystem.cs` | `TapeAt` (answering machine tapes); toll records come straight from `CallFact`s |

## How It Works
- Each possible memory is a keyed coin flip (`Rng.Chance(seed, Witness, witness, subject, minute)`), so asking twice
  gives the same answer. Probabilities depend on knowing the person, time together, being observant, darkness,
  and fade with days since (`exp(-days/14)`). Sleepers only hear loud noises.
- Strangers are described, acquaintances named (`KnowsByName`: relationships, household, coworkers, some neighbours,
  regulars at the witness's workplace).
- Prints: up to four people per surface; later touches smudge older ones; legibility fades over 25 days.
- Cameras: by day make, colour and often part of the plate; at night shape and how many people are inside.

## Key Decisions
- **Lazy evidence:** nothing is computed for places nobody examines; once the player sees something it is stored as
  a clue, so it never changes.
- **Truth stays attached:** observations carry the true person or vehicle id, hidden from the player, so the DA
  can judge whether a clue really points at the accused.

## Connection Points
- Read by the **investigation** layer (`Detective`); facts come from **core**, **agents**, **crime**.
