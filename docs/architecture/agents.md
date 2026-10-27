# Agents: Planning and Execution

## Purpose
Gives every person a believable day (sleep, work, meals out, drinks, walks, time at home) and carries it out in
the physical world, travelling from wherever they actually are.

## Key Files
| File | Role |
|------|------|
| `src/simulation/agents/Agent.cs` | Runtime state: whereabouts, activity, agenda (`Appointment` list), held-by joint activity, commitments; `VehicleRuntime` |
| `src/simulation/agents/Planner.cs` | `PlanFrom(agent, now)`: agenda until the next wake-up; `NextSleep`, `Shifts`, `IsOpen` |
| `src/simulation/agents/Executor.cs` | Departs in time to arrive by each appointment, settles the agent, hands it to joint activities |
| `src/simulation/agents/Places.cs` | Which room an appointment uses (home rooms, workplace by role, customer area, park) |
| `src/simulation/agents/Movement.cs` | Settle / GoInside / StartTrip; travel estimates; which car is available |

## How It Works
1. `Planner.PlanFrom` collects fixed blocks: commitments (joint activities), work shifts, injected errands, and sleep
   carved around them. It fills the gaps with outings (dinner, breakfast, lunch, bar, park) chosen by traits, opening
   hours and travel feasibility, and with time at home. Choices use `Rng.For(seed, DayPlan, person, day)`, so
   replanning the same day makes the same choices.
2. `Executor.Continue` takes the current appointment: if the agent is elsewhere, it schedules the departure at
   `arriveBy - travel estimate`; on arrival it settles the agent (`Places.Resolve`) and schedules leaving.
3. A joint-activity block: the agent waits at the meeting point and calls `ParticipantReady`; from then the activity
   holds the agent until it releases them, and the agent replans from where it is.
4. Commitments added mid-day trigger `CommitmentsChanged` (replan now, or on arrival if travelling).
5. `World.OnSettled` hooks: answering-machine check at home, social calls in the evening, the crime system's stash.

## Key Decisions
- **Agenda of appointments, travel inserted at run time:** estimates and actual trips use the same routes, so
  people arrive on time without any per-frame steering.
- **No idling where you were:** gaps are filled with time at home when there is time to get home.
- **Joint activities own their participants while running:** no objective ever edits another person's plans.
- **Sleep from the shift:** day workers wake 95 minutes before their shift; evening and night workers sleep after it.

## Connection Points
- Scheduled by the **core**; joint activities and calls in **social coordination**; murder plans inject
  appointments (`Agent.Injected`) via the **crime system**. Extending: `add-npc-activity` skill.
