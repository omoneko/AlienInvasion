# Alien Invasion mod - design

- Date: 2026-07-09
- Target: Cities: Skylines (2015 / Unity / .NET Framework 3.5)
- Status: design approved; the real APIs to be verified by decompiling during the planning phase

## 1. Overview

A disaster mod paying homage to the alien invasion from SimCity 4. It is triggered **by hand,
through a summon tool**, or **at random**, and plays out in this order:

1. The mothership descends from high above towards the target.
2. It strikes repeatedly with lightning, opening a crater directly beneath it.
3. The mothership climbs away and disappears.
4. Three tripod-shaped craft appear.
5. The tripods roam freely at random, destroying a few buildings at a time with their beams,
   near where they are heading.
6. The tripods disappear after a set time.
7. The crater and the tripods' trails are left **red with contamination for one in-game year** -
   red ground plus a red glow.

The custom 3D models - the mothership, the tripods and the red decal - ship with the mod inside
an **AssetBundle**.

## 2. Approach

**Mod-driven (option A)**: the custom models are loaded from the AssetBundle, created as
GameObjects, and **the mod drives their position and heading directly every tick**. That gives
flight and movement free of the road network and of pathfinding. Destruction, effects and
contamination reuse the `DisasterHelpers`, effect handling and `NaturalResourceManager` work
already established in Nuclear Meltdown.

The Vehicle/VehicleAI route (option B) was rejected: it drags in road-based pathfinding.

## 3. Technical requirements

- Language and framework: C# on .NET Framework 3.5, built with MSBuild
- References: `ICities.dll`, `Assembly-CSharp.dll`, `UnityEngine.dll`, `ColossalManaged.dll`
- Harmony: the `CitiesHarmony.API` NuGet package
- **AssetBundle**: built with the same Unity version the game uses, to be identified during
  planning. It holds the prefabs for the mothership, the tripods and the red decal.
- Deployed to `%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\AlienInvasion\`
- Interface: `ICities.IUserMod`

## 4. Architecture

```
AlienInvasion/
├─ AlienInvasion.csproj
├─ Assets/alieninvasion.bundle           # prefabs: mothership, tripods, red decal
├─ Core/  (no Unity dependency, tested with xUnit)
│   ├─ InvasionState.cs                   # the state enum and the transition logic
│   ├─ MovementMath.cs                    # random walk directions, interpolation, clamping to bounds
│   ├─ GridMath.cs                        # world to pollution cell, and enumerating cells by radius or trail
│   ├─ ContaminationZone.cs               # a contamination zone: centre, radius, start time
│   └─ ZoneSerializer.cs                  # serialises the zones to byte[]
└─ Game/  (the game integration layer)
    ├─ Mod.cs                             # IUserMod, Harmony, loading the AssetBundle
    ├─ ModConfig.cs                       # constants: durations, radii, probabilities, one year, three tripods
    ├─ AssetLoader.cs                     # fetches the prefabs from the AssetBundle
    ├─ InvasionTrigger.cs                 # the manual tool plus the random timer
    ├─ InvasionManager.cs                 # coordinates the active invasion and drives the state machine
    ├─ Mothership.cs                      # descend, strike, crater, climb away
    ├─ Tripod.cs                          # appear, roam, destroy with the beam, disappear
    ├─ Effects.cs                         # resolves and plays the lightning, beam, explosion and red glow
    ├─ RedContaminationVisual.cs          # places and removes the red decal props
    ├─ ContaminationManager.cs            # the zone ledger, and applying, holding and clearing it
    ├─ PollutionField.cs                  # reads and writes NaturalResourceManager's pollution
    ├─ Simulation/InvasionThreadingExtension.cs   # drives the invasion and the contamination each tick
    └─ Serialization/InvasionDataExtension.cs     # saves and loads the zone ledger; the invasion state is not persisted and resets on level load
```

Dependencies point one way: `Game/*` depends on `Core/*`, and `Core/*` depends on nothing else.

## 5. The sequence of events (state machine)

`InvasionState`:
```
Descending → Bombarding → Ascending → TripodDeploy → TripodsActive → Done
```
`InvasionThreadingExtension` calls `InvasionManager.Update()` every tick, advancing the timers,
the interpolation and everything else. Phase 1 covers `Descending → Bombarding → Ascending →
Done`, skipping past the tripods; phase 2 implements the tripod states.

## 6. Triggering (by hand and at random)

- **By hand**: a dedicated button or tool picks a point on the map and starts it, through
  `InvasionManager.StartInvasion(position)`.
- **At random**: `InvasionThreadingExtension` draws at a low probability every so often and
  starts one at a random point on the map. The frequency, and whether it happens at all, are set
  in `ModConfig`. The randomness uses the game's deterministic RNG,
  `SimulationManager.m_randomizer`.
- Registering as one of CS's own random disasters is complicated, so the draw is done by the mod
  instead.

## 7. The mothership (phase 1)

- The mothership prefab from the AssetBundle is created as a GameObject high above the target.
- **Descent**: the Y coordinate is interpolated down to the hover altitude, over
  `ModConfig.DescendSeconds`.
- **Bombarding**: every so many ticks, a lightning effect plays at a random point directly below
  and the crater deepens a little, accumulating through `DisasterHelpers.MakeCrater`, while the
  buildings beneath are destroyed. This lasts `ModConfig.BombardSeconds`.
- Afterwards it **climbs**, again by interpolating Y, and the GameObject is destroyed.
- A **red contamination zone lasting one year** is registered at the crater.

## 8. The tripods (phase 2)

- Once the mothership is gone, **three** of them (`ModConfig.TripodCount`) are created as
  GameObjects near the crater.
- Each tick every tripod moves in its current random direction at `ModConfig.TripodSpeed`, turning
  at random intervals and clamping or reflecting at the map bounds.
- **Beam destruction**: every `ModConfig.BeamIntervalTicks`, a few buildings near where it is
  heading are **destroyed locally** - `DisasterHelpers.DestroyBuildings` with a small radius, or
  `CollapseBuilding` - along with a beam effect.
- **Contaminated trail**: a small red contamination zone is stamped at the current position at
  regular intervals.
- After `ModConfig.TripodActiveSeconds` they disappear and their GameObjects are destroyed.

## 9. Contamination and the red presentation

- `ContaminationManager`, `PollutionField` and `ZoneSerializer` are reused from Nuclear Meltdown.
- **It lifts on one condition only**: one in-game year passing (`ModConfig.ExpiryYears = 1`).
  **There is no decontamination facility.**
- It is reasserted every tick against the natural decay, and released after a year.
- **Red ground**: CS's own ground pollution has a fixed colour, so `RedContaminationVisual`
  places the **red contamination decal** - a flat prop from the AssetBundle - over the polluted
  cells and removes it when the zone is released.
- **Red effect**: a red glow or haze is kept up over the contaminated area.
- If the gameplay consequences matter - land value falling, and so on - the standard ground
  pollution is applied underneath as well, through `PollutionField`.
- Exactly how the red is rendered - the API for placing props or decals, and whether an effect's
  colour can be set - is to be verified against the real APIs during planning.

## 10. Persistence and safety

- Only the contamination zone ledger is saved and restored, through `ISerializableData`. An
  invasion in progress is not persisted in phase 1: every level load forces it back to Idle
  through `InvasionManager.ResetForNewLevel()`. This is a deliberate simplification that stops
  a previous level's leftover state interfering after switching saves - it is discarded rather
  than resumed.
- Every tick, creation, effect and serialisation path is wrapped in try/catch, so no exception
  propagates into the game.
- **If the AssetBundle or a prefab cannot be read**: it is logged and that piece of the
  presentation is skipped, without taking the game with it.
- No console output is left behind; logging goes through `Debug.Log` with the `[AlienInvasion]`
  prefix only.

## 11. 3D model requirements (for the Blender work)

- **Mothership**: an enormous disc, on the scale of the one from Independence Day. Low poly with
  LODs, `_d/_n/_s/_i/_a` textures, pivot at the centre.
- **Tripod**: a three-legged walker. Low poly with LODs, pivot at ground level.
- **Red contamination decal**: a single simple red plane, translucency allowed.
- Production: Blender to FBX, then **into an AssetBundle** in Unity, using the same Unity version
  as CS. (The asset editor is a useful reference for vehicles, but this mod ships its own
  bundle.)

## 12. Implementation phases

- **Phase 1 (mothership)**: the project skeleton and AssetBundle loading, triggering by hand and
  at random, the descent, the lightning and crater, the climb away, the red contamination lasting
  a year, and saving and loading.
- **Phase 2 (tripods)**: three appear, roam and destroy locally with their beams, leave a red
  trail, and disappear.

## 13. Testing

- Core - the transitions, the random walk, clamping to bounds, the cell maths and serialisation -
  is written test-first with xUnit.
- The game integration layer is covered by a successful MSBuild plus verification in game; it
  cannot be unit tested.
- APIs to verify by decompiling: `DisasterHelpers.MakeCrater` and `DestroyBuildings`,
  `NaturalResourceManager`, playing effects, loading an AssetBundle, placing props and decals,
  `ISerializableData` and `IThreadingExtension`, and where the lightning effect comes from.

## 14. Open questions (to be resolved during planning)

- ~~The exact Unity version CS uses, for building the AssetBundle.~~ **Resolved**: `Cities.exe`
  is a Unity 5.6.7 build (FileVersion 5.6.7.3267). The community standard (cslmodding.info)
  recommends Unity Editor 5.6.6 for building AssetBundles, and the 5.6.x releases are compatible
  with each other, so **Unity 5.6.6f2** it is.
- Where the lightning effect comes from - whether an existing EffectInfo has one or it must be
  built.
- The API for placing and removing the red decal or prop, perhaps through `PropManager`.
- Whether an effect's colour can be set to red, or whether a custom red effect is needed.
- Balancing the tripods' beam destruction: the exact radius and how many buildings go at once.

## 15. Out of scope (YAGNI)

- Making the tripods proper game units, selectable and with an info panel.
- Decontamination facilities; time alone clears it.
- A multilingual UI; the initial wording is kept minimal.
