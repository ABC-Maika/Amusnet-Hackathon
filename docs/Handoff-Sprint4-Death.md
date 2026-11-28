# Handoff: Sprint 4 (Death, Anger, Rebellion, Predator, HUD)

Branch: `Kris4o` (merged with `origin/main` twice, latest includes Sprint 2 village, wind, weather, `ShelterState`).
**Nothing below has been compiled or run in Unity yet.** First step next session: let Unity compile, read the Console, fix errors.
Nothing is committed or pushed yet (all new files are untracked).

## Task status (Hacky board)
| Task | State |
|---|---|
| [A] Death system "Martyr's Last Flutter" | Code written, untested |
| [B] Villager anger escalation | Code written, untested. Not wired to a real mess score |
| [C] Death assets | Frog and Flycatcher models exist in `Assets/Art/` (untracked). Spider/web, swatter, net, zapper lamp not made. Kill animations only for frog and bird |
| [D] Animal rebellion + spirit trail | Code written, untested |
| [E] Death design | `docs/DeathDesign.md` written (proposed values, open questions inside) |

None are marked complete on the board yet.

## What exists (all under `My project/Assets/`)
**Death framework** `Scripts/Death/`
- `DeathCause` base class (per-killer tunables and hooks), `DeathManager` (`Die(cause, butterfly)`: slow-mo 15%, follow camera, ~3 s steering, landing, chain reaction radius 12, legacy, events `DeathStarted/LegacyAwarded/DeathFinished`), `MartyrSteering`, `MartyrCamera`, `LandingDetector`.
- `LegacyBank` (PlayerPrefs): legacy total, deaths, run multiplier `1 + 0.01*legacy` capped x2, last 8 death points.
- `IChainReactionTarget`: `float TriggerChain(Vector3 origin, float power)`. The real `Interactable` should implement it.
- `DebugDeathCause` (K to die), `DeathTestSim.cs` (SimFlyer, SimChaseCamera, SimMessProp, HUD text). Temporary, delete when real flight/camera/messables land.

**Killers** `Scripts/Death/Killers/`
- `ProximityKiller`: Idle -> Warning -> Telegraph -> Attack. Bar full = always plays the Strike animation; kills only if the butterfly is within `strikeRadius`, after `killDelay` 0.2 s. Reads `ShelterState` (hiding cancels). Exposes `ChargeProgress`.
- `KillerWarningBar`: world-space loading bar above a killer's head (auto-created).
- `FrogDeath`: 2.5 s warning + 1.5 s wind-up = 4 s bar, strike radius 3, warn radius 6. Someone added `AimUp` tongue aiming in `LateUpdate` (keep it).
- `BirdDeath`: the predator's kill.

**Predator** `Scripts/Death/Predator/`
- `PredatorMeter`: rises 2.5%/s flying, falls 12%/s hidden (ShelterState or hold H), 1%/s resting. 55% = "land on a flower" message, 80% = "hide" message + 10 s countdown, countdown 0 or 100% = bird kills.
- `PredatorBird`: Flycatcher circles the butterfly, tightens with the meter, closes in during the countdown, then dives, plays Attack, eats the butterfly, flies off; the butterfly's spirit falls and the Martyr's Last Flutter plays. Tune `modelYawOffset` (orientation unknown) and `targetSize` (2.2 m).
- **Overlap warning:** Yavkata's Sprint 5 [A] is also "predator system". Keep one. `PredatorMeter` exposes `Percent`, `CurrentStage`, `CountdownLeft`.

**Anger** `Scripts/NPC/`
- `AngerLevels`: tiers Calm 0 / Annoyed 0.25 / Angry 0.55 / Furious 0.8, tool flags (swatter, net, trap, close windows/gates), `SetMess(score/goal)`, `IAngerReactive`, `CloseOnAnger`. `AngerDebugDriver`: `[` and `]` change mess (test only).

**Animals** `Scripts/Animals/`
- `RebelAnimal`: hold E within 4 m for 1.2 s to recruit; allies walk to nearby `IChainReactionTarget`s and trigger them; raises static `AllyMess(animal, value)` and `Recruited`.
- `SpiritTrail`: orb leads to previous death points, one spot per 25 legacy (max 4).

**HUD** `Scripts/UI/DeathHud.cs` + `Editor/DeathHudBuilder.cs`
- Canvas: legacy + allies panel, anger panel (tier colours, ticks, tools list), bird-head predator panel (faint silhouette fills to full opacity, status SAFE/ALERT/DANGER/HIDDEN), warning banner + big countdown, red danger vignette, recruit toast, controls hint (fades 14 s), Martyr overlay (steer timer, killer name), "+N REBEL LEGACY" popup.
- `UI/Predator/BirdHead.png`: head cut out of `Character Art/Flycatcher.png` (rough; wing bit at top). Generated sprites live in `UI/Generated/`.

**Editor menus** (`Editor/`)
- `Death > Populate Test Scene` (terrain, real `Butterfly.prefab` + SimFlyer, props, DeathManager rig). `Death > Add Sim Animals, Frog and Spirit Trail`. `Death > Add Anger and Predator UI` (re-runnable; rebuilds canvas, adds AngerLevels, PredatorMeter, PredatorBird).
- Test scene: `Scenes/Sprint4 - Death system.unity` (untracked).

## Test controls
A/D turn, W/S fly, Space/Shift up/down, K die, H hold = hide, E hold near animal = recruit, `[` `]` village anger.

## Open items / next steps
1. Compile in Unity, fix errors, run the three editor menus in the death scene, play-test every system.
2. Existing frog in the scene keeps old timings: right-click FrogDeath > Reset (or set 2.5 / 1.5).
3. Orcho (asked via `ask_teammate`, id 9532a759...) replied only "k". Still need: `MessManager` API (call `AngerLevels.Instance.SetMess(score/goal)`, listen to `RebelAnimal.AllyMess`) and `Interactable` implementing `IChainReactionTarget`. Ask again with a concrete question.
4. Tell Yavkata about the predator overlap, and that GameFlow.Lose() should hook to `DeathManager.DeathFinished`.
5. Decide: after the bird eats the butterfly, keep the spirit-fall Martyr sequence (current) or plain game over.
6. Open design questions in `docs/DeathDesign.md` (legacy after a win, frog as predator, run length and cap).
7. Missing killers: spider/web, swatter, net, zapper lamp (reuse `ProximityKiller`; zapper needs `ProximityKiller.IsNight` set from Orcho's... emkata's day-night). Hide flag: `ProximityKiller.ButterflyHidden` or `ShelterState`.
8. Commit and push `Kris4o` (including `Assets/Art/Flycatcher`, `Assets/Art/Frog`, scene, generated UI sprites and their `.meta` files). Note: I moved my untracked `Scripts.meta` aside (/tmp) to merge; main's `Scripts.meta` is the one in use.
9. Delete temporary test code when real systems exist: `DeathTestSim.cs`, `DebugDeathCause`, `AngerDebugDriver`, H-to-hide.
