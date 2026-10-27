# Investigation

## Purpose
The player's side of the game as a plain C# API: the detective is a person in the world who travels, enters rooms,
talks, searches and makes calls, collecting clues; the DA weighs the case. The UI and the tests both use this API.

## Key Files
| File | Role |
|------|------|
| `src/investigation/Detective.cs` | State (clues, known people and places, cash, wiretaps, the case), travel, access rules, knocking |
| `src/investigation/Detective.Interviews.cs` | Topics, alibis (true, or the killer's and accomplice's lies), sightings, motives, line-ups, prints |
| `src/investigation/Detective.Scene.cs` | Look around, body, searching fixtures, tapes, dusting (print sets), bagging, doors |
| `src/investigation/Detective.Phone.cs` | Lt. Hayes requests (records, coroner, print runs, gun registry, lab, ballistics), phone book, wiretaps |
| `src/investigation/Detective.Surveillance.cs` | Stakeouts (live History events), following a car, security tapes |
| `src/investigation/Clue.cs` | A clue: player text plus structured fields (and hidden truth) for the DA |
| `src/investigation/CaseEvaluator.cs` | Scores a case in four pillars, caps, doubts; `Submit` and the truth `Reconstruct` |
| `src/investigation/GameSession.cs` | New game (world, player, pre-run, murder), the command log, save/load |
| `src/investigation/AutoDetective.cs` | Scripted investigator using only player-visible information (tests, debugging) |
| `src/simulation/generation/PlayerSetup.cs` | Puts the detective in the world: office flat, phone, car |

## How It Works
1. Every action runs at the world's current time, then spends game time (`Spend`), so the city moves on.
2. Results come from observers or static, public data (map, phone book); never from `MurderCase` directly,
   except the scene's own facts (body, door) the police would show you.
3. Each player action goes through `GameSession.Do(verb, args)` and is logged with its time. A save is the seed plus
   that log; loading rebuilds the world and replays it, landing in the identical state.
4. `CaseEvaluator.Assess`: opportunity, motive, physical, deception, 60 points each at most; no motive caps the odds
   at 40%, no real opportunity at 30%; an alibi that holds up subtracts. Probability = 1 - exp(-total/55).

## Key Decisions
- **The player is an agent:** their trips, presence and calls are facts; witnesses and cameras can see them.
- **Command log saves:** tiny files, no serialisation of the world, exact by construction (`SaveLoadTests`).
- **Solvability is tested:** `InvestigationTests` runs `AutoDetective` on fixed seeds (`bot` mode of the benchmarks
  runs it on many: about 90% right suspect, 35% conviction for the bot).

## Connection Points
- Reads **observers**, drives **movement**; `World.Crimes.Reported` opens the case; the **game shell** is its UI.
