# STAKEOUT Architecture Reassessment (2026-10-26)

A fresh look at the simulation after P1-P5, written as if a new team were taking the project over. It covers
what the game needs from its simulation, how the current engine measures up (with measurements), what to keep,
what to replace, and a proposed sequence of work. Nothing here is decided yet; the open decisions are listed at the end.

## 1. What the game needs from the simulation

STAKEOUT is a detective game. The player never sees the simulation directly; they reconstruct it from evidence.
So the simulation's real product is not "NPCs that look busy on the map" but **a history of what happened that is
consistent with the laws of the real world, and that evidence can be drawn from**. That gives seven requirements:

| # | Requirement | Why the game needs it |
|---|---|---|
| R1 | **One ground truth.** Who and what was where, with whom, from when to when, at the resolution evidence can observe (street, vehicle, building, room) | Every clue must be explainable; alibis must be checkable |
| R2 | **Evidence is derived from truth, never written separately.** Cameras, witnesses, phone records, fingerprints and receipts are observers of the truth | A clue can never contradict what happened; "two shadowy figures in the car" falls out of the car having two occupants |
| R3 | **Physical co-presence.** People who do something together share the same place and vehicle at the same time | The date, a heist crew, a kidnapping, a tail |
| R4 | **Deterministic and replayable.** Same seed gives the same history, whatever the frame rate or time speed | Replay of solved cases (design doc), save/load, bug reports, tests |
| R5 | **A living, fully populated city that can fast-forward.** Waiting for a case, sleeping, long stakeouts | Open businesses have staff; streets have the traffic that makes a suspect hard to spot |
| R6 | **A past.** Crimes and relationships start before the player arrives | "Cases start running before the player knows" (`cases_and_crimes.md`) |
| R7 | **Cheap authoring of many scenarios.** The progression doc lists ~100 case types, from missing pets to coups | Each new crime must be data plus a little code, not a new bespoke state machine |

## 2. Where the current engine stands

### What works and should be kept
- **Location model:** Address, Location, SubLocation, AccessPoints, Fixtures, tags, and composable templates. It
  matches the menu-driven interior design in `sublocations_rethink.md`.
- **City generator:** a decent procedural grid with named streets, plot types, urban/suburban weighting.
- **Businesses:** hours, positions, shifts.
- **The trace taxonomy** (Mark, Item, Sighting, Record, Fingerprint, Condition) and the idea that fixtures hold traces.
- **Priority-driven daily planning** as a concept (sleep, work, drives competing for time).
- **Engineering habits:** a pure C# simulation with 401 xUnit tests, architecture docs, SOP skills.

### Problems, with evidence
Measurements come from a throwaway probe that runs the real engine (`NpcBrain`, `ActionRunner`, `DoorLockingService`)
on Boston (seed 42).

**P-1. Movement leaves no truth behind, and is not physical.** Trips are straight lines between address pixel
coordinates, through buildings, at one speed. The position is overwritten every frame and never recorded; the only
record is `DepartedAddress`/`ArrivedAtAddress` with a string description. No vehicles, no roads, no pedestrians.
"Who drove past the bank at 21:14?" cannot be answered, even in principle.

**P-2. Joint activities are not joint.** The date (P5's showcase), traced second by second on a Wednesday evening:
- Jack (the driver) reaches Susan's house before 18:00, but she is still out. While "waiting", he starts driving
  home and is back two seconds later, **at least six times in six minutes** (each replan cancels the trip mid-street).
- They leave for the diner in **two separate trips** from her house and arrive 1 second apart.
- One minute after arriving, **Susan drives home, checks her answering machine, and drives back** (18:21-19:00).
  Jack eats "dinner" alone for 39 minutes.
- They go home in two separate trips; she gets home one second before he "drops her off".
- Every time she arrives home she checks the answering machine again (old messages are never marked heard).

A camera on her street would show her leaving and returning alone four times. This is not a missing feature
(the `Vehicle` entity) but an architecture that has no notion of shared presence: each person is an independent
agent, and coordination is done by objectives flipping each other's `NeedsReplan` flags and phase variables.

**P-3. The simulation depends on the frame rate.** Same seed, same 60 NPCs, two game days: stepping at 1 s
(normal speed) and at 30 s (what 60 fps produces at 1800x) give the same number of trips, but **38% of them happen at
a different minute**. Over a week the histories drift apart. Replay, save/load and "fast-forward to the scanner
alert" all need R4.

**P-4. Evidence is written, not observed.** Objectives call `TraceEmitter` with free text ("X was seen running") and
no observer: a sighting with no witness, at a park nobody else visited. Nothing guarantees that a trace matches
what happened, or that what happened leaves the traces it should (the date leaves none).

**P-5. The city is empty, and the generator cannot fill it.** Boston has 709 houses, 7,936 apartment units, 523
businesses and 3,877 jobs. The game spawns 7 people. `PersonGenerator` throws after 954 people (it claims a fresh
apartment building for every other tenant), so 24-hour diners are open with no staff. Everyone eats at the
*first* diner in the dictionary, and every new hire takes the *first* open position. Progressive growth (spawn on
demand) is the root cause: anything spawned later may contradict what the player already saw, and nothing tracks
what the player has already seen.

**P-6. It is expensive where it should be cheap.** Every NPC is ticked every frame. 600 NPCs cost 2.4 s of CPU per
game day at 1-second steps. A fully populated Boston (roughly 10-20k residents) would take on the order of a minute
per game day, and a week of pre-history 5-10 minutes. Larger steps are faster but break R4 (P-3).

**P-7. The code drifted from its own constitution.** `simulation_overhaul_plan.md` promised routines as a function
of time ("free to fast-forward"), an intent stack with an `ActionSequence` builder, and Groups that suspend members'
own plans. What shipped: per-frame ticking, a greedy 24-hour replanner, `ActionSequence` and `MoveToAction` used
only in tests, `OnSuspend`/`OnResume` never called, and Group as a phase flag. The crime system was gutted in P3 and
never rebuilt. The benchmark project's tick loop has been an empty TODO since P3, so it measures nothing.

**P-8. Coordination does not scale to the case list.** Dating alone took four objectives (`MaintainRelationship`,
`OrganizeDate`, `GoOnDate`, `CallBack`), three phone actions, special cases in `ActionRunner`, and objectives that
reach into other people's objective lists. A heist, a cartel handoff or a kidnapping would each need their own
version. R7 calls for one general mechanism.

**P-9. The player is not in the world.** `Player` is a separate class with its own copy of the travel code.
Witnesses can never remember the detective, cameras can never catch them, and tailing would need a third movement
model.

### Verdict
The data model and the city are a good foundation. The **time model, movement model, agent coordination and
evidence model** need to be replaced, not patched. Adding a shared car to the current engine would be a fifth
special case in `ActionRunner` and would leave P-1, P-3, P-4 and P-6 in place. Every later project (backfill,
crimes, stakeouts, replay) depends on these four, so this is the moment to change them, before P6-P8 build on top.

## 3. Proposed architecture: truth first

```
 Agents (needs, routines, intentions)
      | plan agendas, make commitments (via communication)
      v
 Activities  -- one object per joint activity, shared by every participant
      | scheduled as events in a discrete-event simulation
      v
 Physical world  -- containment: person in room / vehicle / street; vehicles on road graph
      | every change appends a fact
      v
 History (ground truth)  -- append-only, indexed by place+time and entity+time
      | queried by
      v
 Observers  -- cameras, witnesses, phone company, fingerprints, receipts  -->  Evidence  -->  Player
```

### 3.1 Time: a discrete-event simulation
The world advances by processing timestamped events from a priority queue (ties broken by a sequence number), not by
ticking every NPC every frame. Godot only feeds the clock; at any speed, the same events happen at the same instants.
A trip is one event at departure and one at arrival, and positions in between are computed from the route (so the
city view still animates smoothly). Fast-forward means "process events until time T". Rough budget: 10-30 events per
person per day, so a 15k-person city is a few hundred thousand events per game day, which should run in
around a second (to be confirmed by the first benchmark).

Randomness comes from one master seed split into independent streams (keyed by entity and purpose), so adding a
new NPC or behavior does not reshuffle everyone else's dice. Iteration is always in a stable order.

### 3.2 Space: containment and roads
Everything is always inside exactly one container: a Location or SubLocation in a building, a Vehicle, or a street
segment (on foot). Position comes from the container. **Passengers do not travel; vehicles do**, and passengers ride
inside. That makes R3 hold by construction: if Susan is in Jack's car, she is wherever the car is.

Roads become a graph extracted from the `CityGrid`. Trips follow routes on it, by car or on foot, at street speeds.
Each trip records which road segments it used and when, which is what cameras and street witnesses need.

Inside buildings, the earlier decision stands: no room-to-room pathfinding. A person is placed in a room when they
arrive, chosen by the activity (customer: dining room; cook: kitchen; resident at 2 am: bedroom).

### 3.3 Activities, commitments and communication (replaces Objectives/Groups/NeedsReplan)
- **Activity:** an interval with a type, a place (or vehicle), and participants with roles. *One* object shared by
  all participants: `Drive(car: Jack's, driver: Jack, passengers: [Susan], route)`, `Dine(Diner 2653, [Jack, Susan])`.
- **Agenda:** each agent's planned sequence of activities. Planning stays priority-driven (sleep, work, drives), as today.
- **Commitment:** an agreement between agents ("Jack picks Susan up at 18:30 Wednesday, dinner at Rosie's").
  Both agendas refer to the same commitment and plan around it as a fixed anchor. Commitments are created, changed
  or cancelled only through **communication** (a phone call, a note, talking face to face), which is itself an
  activity and leaves facts (toll records, an answering machine tape).
- **Execution:** when a joint activity starts, it checks its preconditions against the world (is the passenger at
  the pickup?). If one fails, the activity has defined outcomes (wait up to N minutes, call, give up), and agents
  replan from there. No objective ever edits another person's objectives.

The date becomes: a phone call creates a commitment; at 18:30 Jack drives to Susan's; a single `Drive` takes both to
the diner; a single `Dine` holds both; a single `Drive` brings her home. A heist is the same machinery with more
participants and roles, plus `Split`/`Join` (a crew separates into parallel activities and meets again).

### 3.4 History: the ground truth
Every state change appends a typed fact: presence intervals (who was in which container when), trips (vehicle,
occupants, route, times), communications (who, to whom, medium, when, what was agreed), interactions (touched door
handle, opened safe, bought coffee with cash), conditions (injury, death). Facts are typed records, not strings.
History is append-only, so **replay of a solved case is a query**, and the city view can scrub through it.

### 3.5 Evidence: observers over history
Evidence is computed from history by observers with known limits:
- **Camera** (fixture with a view of a street segment or room): which vehicles and people passed, at what fidelity
  (night: "dark sedan, two occupants"; day: plate and faces).
- **Witness:** agents who were co-present remember salient facts with probabilities, decay and errors, which become
  interview answers. Lies are a deliberate distortion on top of a true memory.
- **Phone company:** toll records from communication facts; answering machine tapes as fixtures.
- **Physical traces:** fingerprints from interaction facts (with the existing smudge/decay rules), receipts from
  purchases, marks from violence.

Most evidence can be computed **when the player investigates**, as a pure function of history and seed. It is
consistent by construction, costs nothing for places nobody examines, and decay is applied at query time. Once the
player has seen a piece of evidence it is stored, so it can never change.

### 3.6 Population, the past, and the player
- **Fully populate the playable city at world creation:** households (couples, families, roommates), everyone housed,
  every staff position filled or deliberately vacant, cars owned. With the event simulation this is affordable;
  the city size can be tuned so jobs and homes balance.
- **The past is simulated, not backfilled.** The world starts N days (for example 14) before the player's first
  morning and fast-forwards. Crimes already in progress have real histories. Long-ago background
  ("dating for 90 days", "owes the bookie $2,000") is stated as initial facts, not simulated.
- **The player is an agent** in the same world: same containment, trips and presence facts, so witnesses and
  cameras can see the detective too, and tailing uses the same road model.

## 4. What happens to the existing code

| Area | Fate |
|---|---|
| Entities, location hierarchy, address templates, fixtures | Keep; add Vehicle as a container and households |
| City generator | Keep; add road-graph extraction; rebalance housing vs jobs |
| Businesses and positions | Keep; staffing done at world creation |
| `GameClock` | Replaced by the event scheduler (Godot drives it) |
| `NpcBrain`, `DayPlan`, `ActionRunner`, `IAction`, `ActionSequence` | Replaced by agenda planner + activities + scheduler |
| Objectives (Sleep, WorkShift, EatOut, GoForARun) | Their logic is ported as routine/needs rules |
| Date objectives, phone actions, Group, PendingInvitation | Replaced by communication + commitments + joint activities |
| `Trace`, `TraceEmitter`, `EventJournal` | Replaced by History facts + observers; trace types survive as evidence kinds |
| Crime system | Rebuilt on activities/commitments (it is a stub today anyway) |
| `Player` | Becomes a Person-like agent |
| UI scenes (city view, evidence board, menus) | Kept; city view reads positions from the new model |
| Tests | Old engine tests retire with their code; data-model and generator tests stay |

The new core is built next to the old one (`src/world/` or similar) and the game switches over at the end of
milestone 3, so the game keeps running throughout. The old engine is then deleted.

## 5. Proposed milestones

Each milestone ends with something you can watch in the game and with **invariant tests**: checks that run over a
simulated week of the full city and fail on any physically impossible history. Examples: nobody is in two places;
every passenger is in a vehicle whose driver is present; nobody moves faster than their vehicle; every phone answered
was answered by someone at that phone; every open business has staff on site; every piece of evidence traces back to
a fact. These invariants are how "logical consistency" stays true as the game grows.

| # | Milestone | Done when |
|---|---|---|
| M0 | **Decide and prepare.** Settle the decisions below; merge `project-5` into `main` as the legacy baseline; replace the design sections of `simulation_overhaul_plan.md` with this architecture | You have approved the direction |
| M1 | **Core engine.** Event scheduler, seeded RNG streams, containment, road graph and routing, trips with analytic positions, History store with indexes, a real benchmark | Same seed gives byte-identical history at 1x and 1800x; one person drives across town along roads and the trip is in History |
| M2 | **Living city.** Households and full population, homes, jobs, cars; routines for sleep, work, meals, errands with sensible venue choice; city view shows cars on roads; fast-forward | A week of full Boston runs in a few seconds and passes all invariants |
| M3 | **Talking and doing things together.** Communication (calls, answering machine), commitments, joint activities, the date rebuilt on them; switch the game to the new engine and delete the old one | The date passes a test written as a detective question: a camera on the route sees one car with two people |
| M4 | **Evidence.** Observer framework; cameras, witness memory, phone records, fingerprints, receipts; an investigation API for the UI | "Who was at Rosie's Diner between 7 and 9 pm?" answered from evidence, with honest gaps and errors |
| M5 | **The past and the player.** Pre-run history before day one; player as an agent; save/load | A new game opens on a world with two weeks of real history |
| M6 | **First real case.** Murder scenario on activities/commitments (casting, motive, planning, execution, cover-up), proof requirements tied to evidence kinds, police scanner alert | A murder can be committed, investigated through evidence, and replayed |
| M7 | **Player screens.** Address/interior menus, stakeout as a live History stream, evidence board integration, then tailing | Matches the original P8 scope |

M1-M3 replace P5's remaining gap and the original P6; M4-M6 replace P7; M7 is the original P8.

## 6. Decisions needed

1. **Direction:** rebuild the time/movement/coordination/evidence core as described (recommended), or keep the current
   engine and extend it.
2. **Population:** fully populated playable city (recommended) or keep progressive spawning. If full: target size
   (rebalance the generator to roughly 5-10k residents, or keep ~20k).
3. **Other cities:** NYC fully simulated too, or simulated only when relevant (a call to or a trip involving it).
4. **Granularity:** street-segment movement plus room placement on arrival (recommended), or finer.
5. **Process:** the earlier specs and plans ran to 34k lines. Suggest shorter specs that state the invariants and
   acceptance questions, with plans kept to the task list.
