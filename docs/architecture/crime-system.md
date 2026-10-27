# Crime System

## Purpose
Turns a real tension between two people into a murder that happens in the simulated world, with a cover-up,
a discovery, and a police report that starts the player's case.

## Key Files
| File | Role |
|------|------|
| `src/simulation/crime/CrimeSystem.cs` | Casting, plotting (the phone call or a break-in), weapons, cover-up, stash, false alibi, discovery, police report |
| `src/simulation/crime/MurderActivity.cs` | The killing as a joint activity: invited, uninvited (waits for the victim) or break-in |
| `src/simulation/crime/DiscoveryActivity.cs` | A neighbour (or a worried friend) finds the body and phones the police |
| `src/simulation/crime/MurderCase.cs` | The truth about one murder (never shown to the player directly) |
| `src/simulation/entities/Relationship.cs` | `Tension`: debt, jealousy, affair, embezzlement, grudge; who knows about it |

## How It Works
1. `ScheduleMurder(day)` casts at noon: tensions (not grudges) whose target lives alone and keeps a normal night,
   scored by intensity, motive kind, violent trait and having a car. `TryPlot` needs an evening when both are free
   and the victim is still up (invited), or the victim asleep at home at about 2:30 (break-in).
2. Invited: the killer phones from home (three tries) to announce a visit; answered or heard on the machine, the
   victim commits to staying home. Unanswered, the killer turns up anyway and waits; if the victim never comes
   home, `FallBackToBreakIn`.
3. `MurderActivity`: touches (door, table), cigarettes, an argument noise, then the kill (weapon item, noise,
   `DeathFact`, the victim's presence becomes Dead). Cover-up: careful killers wipe; the weapon is dropped, dumped
   in a park trash can on the way home (injected appointment) or stashed at home with bloody clothes (`OnSettled`).
4. Next morning the killer may ask a friend for a false alibi (a recorded call). After the detective questions the
   killer, they phone the accomplice again and may dump a weapon kept at home (`OnSuspectQuestioned`).
5. From 6:45 the next morning, neighbours at home are tried every 20 minutes; the finder goes in, screams, goes
   home and makes an Emergency call. `Reported` fires (the scanner alert); the coroner removes the body 5 hours later.

## Key Decisions
- **Motives come from the population, not from the crime:** tensions, their witnesses and their paper trails
  (IOUs, letters, ledgers) exist from world creation, along with weaker red herrings.
- **The crime is built from the same activities as everyday life**, so it obeys the same physics and leaves the
  same kinds of facts.

## Connection Points
- Uses **agents** and **social coordination**; facts are read by **observers**; `Reported` starts the
  **investigation**. `GameSession.NewGame` schedules the murder for the last night before the player's first morning.
