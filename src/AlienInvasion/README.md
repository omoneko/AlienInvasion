# Alien Invasion (Cities: Skylines Mod)

> This file covers the mod source directory. The repository root README is the authoritative
> description of the mod and of the current AssetBundle procedure; where the two disagree, follow
> the root.

A mothership arrives, strikes repeatedly with lightning to tear a crater open, destroys the
buildings around it and leaves red radioactive contamination behind. The manual trigger key is
**F7**, which can be changed through `ManualTriggerKey` in `Game/ModConfig.cs`. It also occurs at
random, with a low probability.

## Building the AssetBundle (turning the Blender models into a form the game can use)

1. Install **Unity Editor 5.6.6f2**, from the
   [Unity Archive](https://unity3d.com/get-unity/download/archive). It is compatible with the
   Unity 5.6.7 engine Cities: Skylines runs on.
2. Open `models/source/models.blend` in Blender and **export as FBX**: `MotherShip`, `TriPod`,
   and the flat object used for the red decal.
   - Note that `MotherShip` and `TriPod` already have their materials set up: the `MetallicGray`
     base material plus custom accent colours. Blender's FBX exporter carries those assignments
     across automatically, so they survive the import into Unity. The goal of this step is a
     clean export and a check that the materials came through - there is nothing to recreate.
   - The pivot of `MotherShip` is at its geometric centre; this is correct and needs no change.
   - Put the decal object's pivot at its centre, on the ground plane.
   - Apply each object's scale with `Ctrl+A -> Scale` before exporting. An unapplied scale makes
     the behaviour after the FBX import hard to predict.
3. Open `unity-project` in Unity Editor 5.6.6f2.
4. Import the exported FBX files into `unity-project/Assets/` and check the materials applied
   correctly (`_d` colour, `_n` normal, `_s` specular, `_i` illumination, `_a` alpha).
5. Make a prefab of each model and name them **exactly** `Mothership` and `ContaminationDecal`,
   matching `MothershipPrefabName` and `RedDecalPrefabName` in `Game/ModConfig.cs`.
6. Set the **AssetBundle name** to `alieninvasion` on each prefab, through the AssetBundle
   dropdown at the bottom right of the Inspector.
7. Run **AlienInvasion -> Build AssetBundle** from the Unity menu.
8. It produces `unity-project/AssetBundles/alieninvasion`, with no extension. Rename it to
   **`alieninvasion.bundle`** and put it at `src/AlienInvasion/Assets/alieninvasion.bundle`.
9. Run `build.ps1` again and it is deployed into the mod folder.

The mod builds and runs without the AssetBundle; only the mothership and decal visuals are
skipped, and the log says `AssetBundle not found`.

## Building and deploying
```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```
The result is deployed to
`%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\AlienInvasion\`.

## Checking it works in game
1. Enable "Alien Invasion" under Content Manager -> Mods.
2. Press **F7** in game, or wait for a random occurrence.
3. The mothership descends, strikes with lightning while the crater forms, the surrounding
   buildings are destroyed, and it climbs away again.
4. Check that red contamination is left behind. Without the AssetBundle you see the standard
   ground pollution only.
5. Check that the contamination lifts on its own once the configured time has passed.
6. Save and reload, and check that the contamination survives.

## Settings
The constants live in `Game/ModConfig.cs`: the trigger key, the probabilities, the radii, the
durations and so on.

## Logs
Search the output log in `%LOCALAPPDATA%\Colossal Order\Cities_Skylines\` for `[AlienInvasion]`.
