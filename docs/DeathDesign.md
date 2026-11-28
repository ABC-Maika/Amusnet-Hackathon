# Death Design (Sprint 4 [E])

> **Update (Sprint 6):** the martyr fall and Rebel Legacy are gone. Predators (bird, frog, river fish) swallow the butterfly; the camera follows them while they digest (3-5 s), then follows their poop (bird, frog or fish poop), whose splat knocks nearby props over for score. Other killers (spider web, swatter, net, zapper lamp) make the butterfly go limp and fall. Recruited allies now boost the mess score multiplier. See `DeathManager.cs`.


Status: **proposed defaults**, all values are tunables on `DeathCause` / `DeathManager` / `LegacyBank` / `AngerLevels`. Playtest in Sprint 5 and adjust in Sprint 6.

## Core idea: the Martyr's Last Flutter
Death is not a fail screen, it is a payoff. When a killer gets the butterfly:
1. Killer's `OnDeathBegin` plays its kill animation (about 0.3s).
2. Slow-mo to 15% (ease in 0.15s), camera follows the fall.
3. About 3s of steering (WASD) to aim the landing at the biggest mess.
4. Time returns to normal, she lands, a chain reaction fires around the landing point (radius 8m).
5. Legacy is awarded, then `DeathFinished` leads to the game-over screen.

So the best deaths happen next to lots of messable objects.

## Killers

| Killer | Where it lives | Active when | Warning | Telegraph | Notes |
|---|---|---|---|---|---|
| Spider and web | Hedges, barn corners, window frames, outdoor lamps | Always (web is static) | Web glints at 4m | 0.8s (spider drops) | Web slows the butterfly; stuck 1.5s before spider arrives. Legacy x1.0 |
| Frog | Pond and creek edge | Low flight over water | Ripple + croak at 6m | 1.5s (tongue wind-up); bar over its head fills over 4 s total | Punishes flying low over water. Legacy x1.0 |
| Bird (the predator) | Sky, spawned by the predator meter | Predator meter reaches 100% | Shadow + screech from 70% | 1.5s (dive) | Escapable by hiding. Legacy x1.2 |
| Swatter | Villagers, from Annoyed tier | Villager within 3m, butterfly in view | Villager raises arm | 0.7s | Can be dodged by moving sideways. Legacy x1.0 |
| Net | Villagers, from Angry tier | Villager within 5m | Villager lunges, "whoosh" cue | 0.9s | Wider hit area than swatter. Legacy x1.1 |
| Zapper lamp | Porches and street lamps | Night only | Blue hum and glow at 8m, pulls butterfly in | 1.0s | Needs the night cycle. Legacy x1.3 |

Hazard locations (web, lamp, pond) are marked by the Sprint 2 [E] task. Place at most 2 hazards per 30m of flower trail so every route has a risk but a safe route always exists.

### Warning and telegraph rules
- Every killer gives **warning** (sound or visual, 1.5s typical) **before** its **telegraph** (the wind-up, 0.6 to 1.5s). The player must always have at least 1.5s total to react.
- Warning and telegraph times map to `DeathCause.warningTime` and `telegraphTime`. The killer's own AI uses them.
- Hiding in a hiding spot cancels any telegraph in progress.
- Anger scaling: warning times shrink 15% per anger tier above Calm (never below 0.8s total), so Furious feels dangerous but still fair.

## Legacy (the martyr effect)
- **Per death:** `(10 base + mess value caused by the landing chain) x killer legacy multiplier`. Added to `LegacyBank` (saved between runs).
- **Per next run:** mess score is multiplied by `1 + 0.01 x total legacy`, capped at x2.0 (cap reached at 100 legacy, about 4 to 6 deaths).
- **Cap reason:** the goal is about 15 minutes per run (Sprint 3 [E]); the bonus shortens it but cannot trivialise it.
- **Display:** after landing the HUD shows `Legacy +N`, and the run start screen shows the current multiplier.
- **Spirit trail (Sprint 4 [D]):** removed; the next run no longer gets a guide to earlier death spots.

## Open questions for the team
- Should legacy reset after a win? (proposal: keep it, a win ends the campaign anyway)
- Is the bird the only predator, or can the frog count as one too? (proposal: bird only; frog is a hazard)
- Final numbers for run length and legacy cap depend on Sprint 3 [E] mess values.

## Implementation map
| Piece | File |
|---|---|
| Killer base class | `Scripts/Death/DeathCause.cs` |
| Sequence, slow-mo, chain reaction | `Scripts/Death/DeathManager.cs` |
| Legacy storage and run multiplier | `Scripts/Death/LegacyBank.cs` |
| Anger tiers (swatter, net, trap, windows, gates) | `Scripts/NPC/AngerLevels.cs` |
