# Truth-First Rebuild and Murder Prototype (M0-M7)

Implements `docs/future_plans/2026-10-26-architecture-reassessment.md` in one pass, ending in a playable murder case.
The user approved the direction and asked for all milestones without intervention (2026-10-26).

## Decisions taken (reassessment section 6)
- Rebuild the core. Old engine (actions, brain, objectives, groups, traces, journal, GameClock, crimes) is deleted.
- Full population of one city (Boston) at world creation: every job filled, ~15% non-workers, many units vacant.
  No NYC; other cities are out of scope.
- Movement on road cells (the CityGrid road cells are the graph); people are placed in a room on arrival.
- The world is pre-run for several days before the player's first morning (1984).
- Branch `rebuild` from `project-5`; `main` untouched (merging stays the user's call).

## Engine (pure C#, `src/simulation/`)
| Piece | Where | Rule |
|---|---|---|
| Scheduler | `core/` | Priority queue on (time, seq). `World.AdvanceTo(t)` runs every event <= t. Godot only feeds t |
| RNG | `core/Rng` | `Rng.For(seed, keys...)` with a stable hash (never `string.GetHashCode`), so each decision has its own stream |
| Roads | `space/RoadGraph`, `Router` | Nodes are road cells; A* with cached routes; each address has an access road cell |
| Whereabouts | `space/` | Each person is Inside(address, location, sublocation), InVehicle(vehicle) or OnFoot(trip). Vehicles are Parked(address) or Moving(trip). Passengers never travel; the vehicle does |
| History | `history/` | Append-only typed facts: Presence, VehicleParked, Trip, Call, Touch, Noise, Death, ItemMoved, ForcedEntry, Purchase |
| Agents | `agents/` | Daily agenda of appointments (place, arrive-by, leave-at, kind); travel is inserted by the executor from the actual position |
| Joint activities | `agents/` | One object holding several people: Visit, Date (pickup, drive, dine, drive back), Murder. Participants' agendas hold a block; the activity drives them during it |
| Communication | `agents/` | Calls between phone fixtures; answered only if someone is there and awake; else answering machine or retry. Commitments are created only by calls |
| Observers | `observers/` | Cameras, witness memory, neighbours' street view, phone records, fingerprints, scene examination; pure functions of History + seed, stored once seen |

Invariants (tested over a simulated week, `InvariantTests`): one place per person at a time; presence intervals
contiguous; passengers only in moving vehicles with their driver; nobody arrives faster than the route allows;
answered calls were answered by someone at that phone; open diners have staff; joint-activity participants are
co-located; every clue references facts.

## The case
Casting picks, from pre-generated social tensions, a killer/victim pair with the strongest motive; the victim lives
alone. Motives: **debt**, **jealous ex**, **affair** (threat to tell the spouse), **workplace** (embezzlement).
Victims also have weaker tensions with others (red herrings). Approaches: **invited** (killer phones to arrange a
visit, victim lets them in) or **break-in** (late night, forced back door/window). Weapons: knife from the victim's
kitchen, blunt object at the scene, or the killer's own handgun (gunshot heard by neighbours). After the kill the
killer may wipe prints, takes or dumps the weapon, drives home or to a bar; with a close friend, phones next morning
to set up a false alibi. A neighbour finds the body the next morning and calls the police; the player gets a scanner alert.

Trail of clues the player can follow (all derived from History):
1. **Scene:** body (cause, time-of-death window from body temperature; the coroner narrows it next day),
   door (forced or not), signs of a guest (two glasses), weapon or missing knife, fingerprints on surfaces
   (killer's and innocent recent visitors'), answering machine tape, address book (suspect pool), diary/letters (motives).
2. **Neighbours and co-workers:** what they saw (people in the lobby/hall, cars parked outside, noises with times)
   and what they know about the victim's conflicts.
3. **Suspects:** alibis (true, or the killer's cover story), refusals, car and appearance observed in person,
   reference prints.
4. **Checks:** alibi witnesses (bar staff, the friend who lies), cameras near the scene (night: colour, make, occupant
   count; day: partial plate), phone records via the police contact, gun registry, line-up IDs ("is this the man?").
5. **Covert:** pick locks to search a suspect's home (weapon, bloody clothes, letters); buy and plant wiretaps
   (the killer phones the accomplice after being questioned); stakeouts log arrivals and departures live.
6. **Case file:** pick the accused and the evidence; the DA rates it.

## Conviction model (LA Noire-like, explainable)
Each clue in the case is checked against the accused and yields findings in four pillars:
- **Opportunity** (placed at or near the scene in the time-of-death window, or alibi broken)
- **Motive** (documents, witness statements about the conflict)
- **Physical** (prints at scene, prints on weapon, weapon/clothes found, ballistics)
- **Identification** (line-up ID, matching car/description at the scene)
Points per finding (strong vs weak), capped per pillar. Probability = 1 - exp(-score/50), capped at 35% without
Opportunity and at 55% without Motive. Contradicting evidence (a verified alibi for the whole window) subtracts.
The DA explains each finding and what is missing. After the verdict the truth is replayed from History, so a
wrongly convicted suspect is revealed.

## Player
The player is a Person in the same world (home office, car, witnesses and neighbours can notice them). Actions cost
game time. Tools: notebook, fingerprint kit, lockpicks, phone (police contact, phone book, catalogue: wiretaps),
cash. Save files are the seed plus the timestamped command log, replayed on load.

## Progress (2026-10-27)
- [x] M1 core engine + benchmark  - [x] M2 living city  - [x] M3 joint activities, date, switch-over
- [x] M4 observers  - [x] M5 pre-run, player agent, save/load (command log)  - [x] M6 murder + case evaluation + bot detective
- [x] M7 UI: city map, rooms, interviews, case file, stakeout + tailing, wiretap, lockpicking, phone, verdict
- [x] docs

Deviations from the plan: taxis for people without cars; no NYC; the DA formula is 1 - exp(-total/55) with pillar
caps; the evidence board layout is not saved (UI state). Open ideas: more crime types, police as agents, player
reputation, tailing on foot, choosing which tapes to buy, multi-case careers.
