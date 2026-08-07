# Alien Invasion Phase 2 implementation plan (the tripods)

- Date: 2026-07-10
- Branch: `feature/phase2-tripods`, based on `12403b3`
- Target: Cities: Skylines (2015), a .NET Framework 3.5 mod, with Core free of Unity and tested with xUnit
- Design basis: `docs/specs/2026-07-09-alien-invasion-design.md` §8 and §9

## Purpose

What follows the Phase 1 mothership, implemented in the order the user asked for:
1. **Finishing the UFO model's presentation** - its rotation and scale
2. **Summoning the tripods** - once the mothership ascends, three appear near the crater and roam freely
3. **Destroying buildings with the laser** - local destruction ahead of them, leaving red contamination in their wake

The visuals themselves - the Tripod prefab - come from the AssetBundle. As with the mothership,
the design must hold to skipping creation and carrying on when the prefab is not there, so it
works without the bundle having been built.

## Global constraints (common to every task; the standard review holds them to)

- **The thread boundary, strictly**: follow the discipline `InvasionManager` already keeps.
  - The main thread, through `InvasionThreadingExtension.OnUpdate` into `InvasionManager.UpdateVisual`:
    creating, destroying and moving GameObjects and Transforms; **writing** `_state`, the phase timers
    and the tripod positions; and playing effects through `Effects.*`.
  - The simulation thread, through `OnAfterSimulationTick` into `InvasionManager.UpdateSimulation`:
    writing to `DisasterHelpers` and to `NaturalResourceManager` for the contamination, and nothing else.
    It **must not** touch GameObjects, Transforms, `Effects.*` or write `_state`.
  - The state and the positions have a single writer, always the main thread. The simulation thread only reads them - the same benign race, within the same tolerance, as the existing `_target` and `_craterProgress`.
- **Immutability and naming**: match the style of the existing code. Core stays net35-safe - no tuples, no Span, no `UnityEngine.*` - and positions are passed as `float x, z`.
- **It works without the bundle**: treat `AssetLoader.GetPrefab(name)` as something that can return null and stay null-safe.
- **No exception reaches the game**: creation, movement, destruction and effects are all wrapped in try/catch and only logged.
- **No console output**: log through `ModConfig.Log` and `LogError` only.
- **Anything tunable is a `ModConfig` constant**; no magic numbers.
- **Phase 1's behaviour is unchanged**: the mothership flow, the contamination save and load, and the level reset all stay as they are.
- The build is checked with `build.ps1` under MSBuild, and Core with `dotnet test`. The game integration layer cannot be unit tested, so a successful build is the bar.

## The state machine, after the change

```
Idle → Descending → Bombarding → Ascending → TripodDeploy → TripodsActive → Done → Idle
```
Phase 1 went straight from `Ascending` to `Done`. Two states go in between: `TripodDeploy`, a
single frame that creates the three tripods, and `TripodsActive`, which carries the movement, the
beam destruction and the trail of contamination.

---

## Task 1 - Core: the pure maths of the tripods' walk, test-first

**New:** `src/AlienInvasion/Core/TripodWalk.cs`, free of Unity, written test-first with xUnit.

The pure functions needed, all static, all in `float`, `out` allowed but no tuples:

1. `void Rotate(float dx, float dz, float angleRad, out float ndx, out float ndz)`
   Rotates the 2D unit direction (dx, dz) by angleRad: `ndx = dx*cos - dz*sin`, `ndz = dx*sin + dz*cos`.
2. `void BounceAxis(float pos, float dir, float half, out float newPos, out float newDir)`
   Reflects one axis off the boundary. With `pos > half` it gives `newPos=half, newDir=-|dir|`, pointing inward;
   with `pos < -half`, `newPos=-half, newDir=+|dir|`. Inside the range nothing changes.
3. `float StepComponent(float pos, float dirComponent, float speed, float dt)`
   `= pos + dirComponent*speed*dt`.

What the tests cover, in `tests/AlienInvasion.Core.Tests/TripodWalkTests.cs`:
- Rotate: 90 degrees takes (1,0) to (0,1) within 1e-4, and 360 degrees leaves it unchanged.
- BounceAxis: past the upper boundary it clamps to half and flips the direction negative; the lower boundary mirrors that; inside the range nothing changes.
- StepComponent: advances by a known amount.

Confirm the new file is picked up by the `<Compile Include>` links in `AlienInvasion.Core.csproj`, which is how the test project already references Core, registering it the same way as the other Core files.

---

## Task 2 - Core: adding the tripod stages to the state machine, test-first

Change `src/AlienInvasion/Core/InvasionState.cs`:
- Add `TripodDeploy` and `TripodsActive` to `enum InvasionState`, between `Ascending` and `Done`.
  The result: `Idle, Descending, Bombarding, Ascending, TripodDeploy, TripodsActive, Done`.
- `InvasionStateMachine.Next`:
  `Ascending` to `TripodDeploy`, `TripodDeploy` to `TripodsActive`, `TripodsActive` to `Done`, `Done` to `Idle`.

In `tests/AlienInvasion.Core.Tests/`, updating the existing state machine tests or adding them if there are none:
- A test walking the new full cycle from `Idle` through `TripodsActive` and `Done` back to `Idle` with Next.
- `CanTransition`, for both the valid and the invalid transitions.

---

## Task 3 - Game: finishing the UFO model's presentation (rotation and scale)

Added to `ModConfig`:
```
public const float MothershipSpinDegPerSec = 20f;  // the mothership's horizontal spin, in degrees per second
public const float MothershipScale = 1f;           // the scale applied when the prefab is created; tuned in game
```
`src/AlienInvasion/Game/Mothership.cs`:
- On creation, `_gameObject.transform.localScale = Vector3.one * ModConfig.MothershipScale`.
- Add `public void Spin(float dt)`: when `_gameObject != null`, call
  `transform.Rotate(0f, ModConfig.MothershipSpinDegPerSec * dt, 0f, Space.World)`, staying null-safe.
`src/AlienInvasion/Game/InvasionManager.cs`:
- Call `_ship.Spin(realTimeDelta)` every frame in `UpdateVisual` during Descending, Bombarding and Ascending alike,
  inside each Update helper, where `_ship != null` is already checked.
- The existing altitude interpolation, timers and transitions are unchanged.

This cannot be unit tested, so a successful `build.ps1` is the bar. Without the bundle, both Spin and the scale are null-safe no-ops.

---

## Task 4 - Game: the tripods themselves and summoning them (three of them, roaming)

Added to `ModConfig`:
```
public const string TripodPrefabName = "Tripod";
public const int   TripodCount = 3;
public const float TripodSpeed = 30f;               // horizontal speed, in units per second
public const float TripodActiveSeconds = 40f;       // how long they stay from appearing to vanishing
public const float TripodTurnIntervalSeconds = 2.5f;// how often they change direction
public const float TripodTurnMaxDeg = 60f;          // the largest turn in one change, either way
public const float TripodScale = 1f;
public const float TripodSpawnScatter = 40f;        // how far from the crater's centre they are scattered
public const float TripodMapHalfExtent = 8500f;     // the movement boundary, roughly the map radius; the same value the existing random trigger uses
```
**New:** `src/AlienInvasion/Game/Tripod.cs`, the same null-safe GameObject wrapper the mothership uses:
- The constructor `Tripod(Vector3 groundPos)` instantiates `AssetLoader.GetPrefab(TripodPrefabName)`, skipping it when that is null,
  applies the scale, picks a random initial heading as a unit direction (dx, dz), and clamps the position to the ground.
- Its state: `float _dirX, _dirZ` as a unit direction, plus `Vector3 Position { get; }`.
- `public void Advance(float dt)`, on the main thread: advance x and z with `TripodWalk.StepComponent`, then
  reflect off the `TripodMapHalfExtent` boundary with `TripodWalk.BounceAxis`, then match the ground height with
  `TerrainManager.instance.SampleRawHeightSmoothWithWater(pos, false, 0f)` and update `transform.position`.
- `public void Turn(float angleRad)`: turns using `TripodWalk.Rotate`.
- `public void Destroy()`: destroys the GameObject, null-safely.

**New:** `src/AlienInvasion/Game/TripodManager.cs`, static, with creation, movement and destruction on the main thread only, as with the mothership:
- `static Tripod[] _tripods`, `static float _activeElapsed` and `static float _turnTimer`.
- `Spawn(Vector3 craterCenter)`, main thread: scatters `TripodCount` of them and sets `_activeElapsed=0`.
- `UpdateVisual(float dt)`, main thread: calls Advance on each tripod, and once `_turnTimer` passes its threshold turns each of them by
  `Random.Range(-TripodTurnMaxDeg, +TripodTurnMaxDeg)`. Then `_activeElapsed += dt`.
- `bool IsFinished { get { return _activeElapsed >= TripodActiveSeconds; } }`.
- `DespawnAll()`, main thread: destroys them all and nulls the array.
- `static Vector3[] SnapshotPositions()`, a read-only view for the simulation thread to use for the destruction in Task 5.
  Task 4 keeps the position array and Task 5 reads it; only the structure goes in here.
- `ResetForNewLevel()`: effectively DespawnAll, called from `InvasionManager.ResetForNewLevel`.

Wiring it into `InvasionManager`:
- Added to the state switch in `UpdateVisual`:
  - When `Ascending` finishes, `Next()` as before, now into TripodDeploy. `_ship.Destroy()` stays where it is.
  - `case TripodDeploy`: call `TripodManager.Spawn(_target)`, then `Next()` straight away into TripodsActive, with `_phaseElapsed=0`.
  - `case TripodsActive`: `TripodManager.UpdateVisual(realTimeDelta)`, and once `TripodManager.IsFinished`,
    `TripodManager.DespawnAll()` then `Next()` into Done.
- Add `TripodManager.ResetForNewLevel()` to `ResetForNewLevel`.
- Reaching `Done` returns to Idle exactly as it does now.

This cannot be unit tested, so a successful `build.ps1` is the bar. Without the bundle the movement logic - Advance, Turn and the timers -
still runs without a GameObject and reaches Done correctly after `TripodActiveSeconds`; confirmed in review.

---

## Task 5 - Game: the laser destruction and the red contamination trail

Added to `ModConfig`:
```
public const float BeamIntervalSeconds = 1.5f;        // how often a beam fires, which is the local destruction
public const float BeamDestroyRadius = 25f;           // the radius one beam destroys, locally
public const float TripodTrailContamRadius = 30f;     // the radius of the contamination left in their wake
public const float TripodTrailContamIntervalSeconds = 3f; // how often the trail contamination is stamped down
public const float BeamSkyOffset = 60f;               // the top of the drawn beam, above the tripod's head
```
Added to `src/AlienInvasion/Game/Effects.cs`:
- `public static void PlayBeam(Vector3 groundPoint, Vector3 from)`:
  Reuses the LineRenderer approach of the existing `PlayLightningStrike` to draw a thin red beam for a moment,
  coloured `new Color(1f,0.1f,0.1f)` and caching the material the same way. Main thread only.

`TripodManager`:
- On the main thread, `UpdateVisual` advances `_beamTimer` and, past the threshold, calls `Effects.PlayBeam` from above each tripod down to where it stands. Drawing only.
- On the simulation thread, `UpdateSimulation` - the **new** `TripodManager.UpdateSimulation()`:
  - `_beamDestroyTimer`, the interval counter on the simulation side, either `BeamIntervalSeconds` converted to ticks or measured in elapsed seconds,
    destroys locally around each tripod's current position - read from the snapshot the main thread wrote - with
    `DisasterHelpers.DestroyStuff(seed, null, pos, BeamDestroyRadius, BeamDestroyRadius, 0f, …)`,
    keeping strictly to **the preRadius=totalRadius workaround** as in Phase 1. Never pass 0.
  - `_trailTimer` adds a
    `ContaminationZone(x, z, TripodTrailContamRadius, nowTicks)` through `ContaminationManager.AddZone` at each current position, every `TripodTrailContamIntervalSeconds`.
    The existing upkeep, the two-month expiry and the decal syncing all then apply unchanged.

Wiring it into `InvasionManager.UpdateSimulation`:
- `case TripodsActive`, on the simulation thread: call `TripodManager.UpdateSimulation()`, which touches only DisasterHelpers and the contamination.
- The position snapshot is the simulation thread reading `Tripod.Position` as the main thread wrote it; the main thread is the single writer.
  That is the same benign race as the mothership's `_target`, and a comment says so.

This cannot be unit tested, so a successful `build.ps1` is the bar. That the beam reliably removes buildings, with the preRadius workaround in place,
is confirmed in code review.

---

## Done when

- Every task reviews clean, `build.ps1` succeeds and deploys the mod, and `dotnet test` is fully green.
- The overall review, on the strongest model, finds nothing Critical or Important; anything it does find is cleared in one round of fixes.
- Without the AssetBundle built, the whole run - startup, the invasion, the tripods roaming, them vanishing, and the two months of contamination - goes through without an exception.

## Out of scope for Phase 2

- Actually building the AssetBundle, which is work in Unity 5.6.6f2, handled separately through an FBX export and a written procedure.
- Making the tripods proper game units, selectable and with an info panel.
- Balancing exactly how many buildings the beam destroys; that is tuned through ModConfig in game.
