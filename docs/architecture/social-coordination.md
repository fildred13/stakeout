# Social Coordination (Relationships, Phones, Groups)

## Purpose
Lets NPCs coordinate with each other: relationships create recurring social goals, phone calls carry invitations
(or leave answering-machine messages), and Groups move several people through a shared activity. The first use
is dating (P5); the same pieces are meant for criminal meetings and heists (P7).

## Key Files
| File | Role |
|------|------|
| `src/simulation/entities/Relationship.cs` | Dating / Friend / CriminalAssociate between two people |
| `src/simulation/entities/Group.cs` | Shared activity: members, driver, pickup and meetup address/time, GroupPhase |
| `src/simulation/entities/PendingInvitation.cs` | A ringing phone: from, to, proposed Group |
| `src/simulation/entities/Vehicle.cs` | Car entity (placeholder; travel does not use it yet) |
| `src/simulation/objectives/MaintainRelationshipObjective.cs` | Dating partners: weekly, adds an OrganizeDateObjective for 7pm at a diner |
| `src/simulation/objectives/OrganizeDateObjective.cs` | State machine: NeedToCall, AwaitingAnswer, MessageLeft, DateOrganized; creates the Group |
| `src/simulation/objectives/GoOnDateObjective.cs` | Per member; returns actions for the current GroupPhase (driver en route, pickup, venue, drive back) |
| `src/simulation/objectives/CallBackObjective.cs` | Return call after hearing a message (priority 10) |
| `src/simulation/actions/telephone/PhoneCallAction.cs` | Caller side: call-log traces; invitation if recipient is home, else a `RecordKind.PhoneMessage` trace |
| `src/simulation/actions/telephone/AcceptPhoneCallAction.cs` | Recipient side: accepts, calls `OrganizeDateObjective.OnAccepted` |
| `src/simulation/actions/telephone/CheckAnsweringMachineAction.cs` | Reads phone messages from known contacts, adds CallBackObjectives |

## How It Works
1. `SimulationState.AddRelationship` (Dating) gives both partners a MaintainRelationshipObjective.
2. When due, one partner gets an OrganizeDateObjective; it creates a Forming Group and plans a PhoneCallAction from home.
3. If the partner is home, a PendingInvitation is queued; `ActionRunner` interrupts them with AcceptPhoneCallAction.
   On accept the Group becomes Active, both members get a GoOnDateObjective and `NeedsReplan = true`.
4. If not home, a phone-message trace is left. Arriving home sets `Person.NeedsAnsweringMachineCheck`; the check adds
   a CallBackObjective, whose call reaches the original organizer and continues at step 3.
5. GoOnDateObjective advances `Group.CurrentPhase` as each phase's action completes; the driver must verify the
   passenger is at the pickup. After the drive back the Group is Disbanded.

## Key Decisions
- **Coordination through objectives + replan,** not a separate group scheduler: each member still plans with `NpcBrain`.
- **Phones are Fixtures with Traces:** every call leaves evidence (call logs, messages) the player can later find.
- **Interrupts are narrow:** only an incoming call preempts an activity.
- **Duplicate-organizer guard:** MaintainRelationship skips if either partner is already organizing; acceptance completes the other's competing OrganizeDate.

## Connection Points
- **NPC brain** (`npc-brain.md`) schedules all of these objectives; `ActionRunner` owns the interrupts.
- **Traces** (`crime-system.md`): phone records are the main trace producer today.
- **Tests:** `stakeout.tests/Simulation/Groups/DateCoordinationIntegrationTests.cs` runs full date scenarios with real generated people.
