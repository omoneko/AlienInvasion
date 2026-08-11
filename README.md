# Alien Invasion - a Cities: Skylines (2015) mod

A disaster mod paying homage to the alien invasion from SimCity 4. A mothership descends, tears
a sinkhole open with lightning and wrecks the surrounding city; three tripods then roam at
random, destroying buildings with their lasers and leaving a red contamination in their wake.

- Target: Cities: Skylines (2015) / .NET Framework 3.5
- Triggering: by hand (the **F7 key** or the **summon button**, then click a spot), plus an
  optional random occurrence which is **off by default**
- Contamination: lifts on its own after **2 in-game months**; nothing decontaminates it

## What happens

1. The mothership descends from high above and hovers, spinning.
2. It strikes repeatedly with lightning, opening a **sinkhole equivalent to a scale 5.5
   disaster** directly beneath it and destroying the buildings in range.
3. The mothership climbs to its loitering altitude and waits overhead.
4. **Three tripods** appear near the sinkhole and roam freely at random.
5. Each fires its laser at intervals, **destroying buildings** near where it stands and stamping
   red contamination along its trail.
6. Once their active period is over the tripods vanish, the mothership leaves, and the
   contamination remains for two months.

Every number - durations, radii, how many tripods, the probabilities - is a constant in
`src/AlienInvasion/Game/ModConfig.cs`.

## Project layout

```
src/AlienInvasion/
  Core/    Pure logic with no Unity dependency: the state machine, the walking maths, the
           contamination zones and serialisation. Covered by xUnit tests.
  Game/    The game integration layer: mothership, tripods, effects, contamination,
           triggering and saving.
tests/AlienInvasion.Core.Tests/   Unit tests for Core
models/                            Blender sources (.blend) and the exported FBX (models/export/)
unity-project/                     Unity project used to build the AssetBundle
docs/specs, docs/plans             Design documents and implementation plans
```

## Building and deploying the mod

```powershell
.\build.ps1
```
This builds `AlienInvasion.dll` with MSBuild and deploys it to
`%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\AlienInvasion\`.
If `src\AlienInvasion\Assets\alieninvasion.bundle` exists it is deployed alongside; without it
the visuals are skipped and only the logic runs. See the AssetBundle steps below to produce it.

Running the Core tests:
```powershell
dotnet test tests\AlienInvasion.Core.Tests\AlienInvasion.Core.Tests.csproj
```

## Building the AssetBundle (mothership, tripods and the red decal)

The models themselves ship inside an AssetBundle, which has to be built with the same Unity
version the game uses.

- Required: **Unity Editor 5.6.6f2**. The game runs Unity 5.6.7, and the 5.6.x releases are
  compatible with each other.
- The model FBX files are already exported: `models/export/Mothership.fbx` and
  `models/export/Tripod.fbx`, written out of Blender at the origin, -Z forward and Y up, with
  the pivot at ground level.

Steps:
1. Open `unity-project/` in Unity 5.6.6f2.
2. Import `Mothership.fbx` and `Tripod.fbx` into `Assets/`.
   - If the scale looks wrong, adjust either the Scale Factor in the import settings or
     `ModConfig.MothershipScale` / `TripodScale` on the game side. The defaults are a mothership
     about 199 m across and a tripod about 65 m tall.
3. Create a **prefab** from each FBX and name them **exactly** as follows, since the code looks
   them up by name:
   - mothership -> `Mothership`
   - tripod -> `Tripod`
   - red contamination decal -> `ContaminationDecal`
     (no FBX needed - a quad with a translucent red material is enough)
4. Select all three prefabs and, in the **AssetBundle** field at the bottom of the Inspector,
   assign the new bundle name `alieninvasion.bundle`.
   - The matching constants in `ModConfig` are `MothershipPrefabName="Mothership"`,
     `TripodPrefabName="Tripod"`, `RedDecalPrefabName="ContaminationDecal"` and
     `AssetBundleFileName="alieninvasion.bundle"`.
5. Run **AlienInvasion -> Build AssetBundle** from the menu; the script is
   `unity-project/Assets/Editor/BuildAssetBundles.cs`. It produces
   `unity-project/AssetBundles/alieninvasion.bundle`.
6. Copy the result to `src/AlienInvasion/Assets/alieninvasion.bundle` and run `.\build.ps1`
   again, which deploys the bundle into the mod folder.

Restart the game after deploying the bundle and the mothership, the tripods and the red decal
will appear. Without the bundle the mod does not crash: it skips the visuals and every part of
the logic still runs.

## Controls

| Control | What it does |
|------|------|
| **F7** | Opens the placement tool; left click a spot to start the invasion. Rebindable in the options. |
| **Summon button** | The same, from the button on screen |
| Automatic | Off by default. Switch it on in the options and an invasion arrives every so many in-game days on average, at a random spot. |

## Options

- **Invasion** - the summon hotkey, and how many invasions may run at once (1-5).
- **Random invasions** - off by default, because they destroy buildings without warning. Once on,
  the average gap is set in in-game days: it is a roll once a day rather than a schedule, so the
  actual gaps vary. It runs on the game clock, so it stops while paused and follows the game
  speed, and nothing starts while another invasion is still running.
- **Aftermath** - whether the red contamination is left behind. Turning it off stops new
  contamination; it does not clear what an earlier invasion left in the save.
- **Sound** - the arrival sound and the tripod footsteps separately, plus a volume.

Before this existed, random invasions were always on but rolled on real time at odds that put the
average gap at about 114 hours of play - so in practice nobody ever met one. The schedule now runs
on the game clock at a rate the player sets.
