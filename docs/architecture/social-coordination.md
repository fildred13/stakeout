# Social Coordination: Calls, Commitments, Joint Activities

## Purpose
Lets people do things together (dates, visits, a crime) with real co-presence, and arrange them only through
communication that leaves a record.

## Key Files
| File | Role |
|------|------|
| `src/simulation/agents/JointActivity.cs` | Base class: participants, meeting point, start, patience, holding/releasing agents, `MoveTogether`, `Touch` |
| `src/simulation/agents/SocialActivities.cs` | `DateActivity` (pickup, one car to the diner, one table, drop-off) and `VisitActivity` |
| `src/simulation/agents/PhoneSystem.cs` | Calls between phone fixtures; who answers; answering machines and tapes; wiretap hooks |
| `src/simulation/agents/SocialLife.cs` | Evening calls to arrange dates and visits; call-backs after messages; `IsFree` |

## How It Works
1. When someone settles at home in the evening, `SocialLife` may schedule a call to a date or a friend.
2. `PhoneSystem.Call` looks at who is awake in the callee's phone location. The intended person answers and agrees
   or declines (`CallPurpose.OnAnswered`); someone else takes a "not here"; a machine records the message; or nobody
   answers. Every call is a `CallFact` (toll records), with a transcript (tapes, wiretaps).
3. Agreement creates the joint activity and `Commit()`s it: every participant gets the block in their agenda.
4. At the start time the activity checks who is ready at the meeting point (with patience), then runs its script:
   `MoveTogether` (leader's car if parked there, else on foot or by cab) and staying in one room together.
5. Messages are heard when the person gets home (`CheckMachine`); half are erased, the rest stay on the tape.

## Key Decisions
- **Commitments only through communication:** every arrangement leaves a phone record, so it can become evidence.
- **One object per joint activity:** participants share trips and rooms, so a camera sees one car with two people.
- **Activities define their own failure:** wait with patience, then proceed (`CanBeginWith`) or abort and release.

## Connection Points
- **Agents** execute the blocks; **crime system** reuses the same machinery (MurderActivity, DiscoveryActivity).
- **Observers** read `CallFact`s (phone records, tapes); **investigation** taps lines via `PhoneSystem.Taps`.
- Adding a joint activity: `add-npc-activity` skill.
