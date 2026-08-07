# Alien Invasion Mod — Phase 1 (Mothership) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** an alien invasion for Cities: Skylines (2015), triggered by a key or at random: a UFO mothership descends, hammers the ground with lightning, forms a crater and destroys the buildings around it, then ascends and vanishes, leaving red radioactive contamination behind for an in-game year.

**Architecture:** the pure logic - the state transitions, the position interpolation, the contamination cell maths, the expiry test and the serialisation - is separated into `Core/`, free of Unity and the game's types, and driven test-first with xUnit. The game integration layer - loading the AssetBundle, driving the mothership GameObject, the effects and writing the contamination - stays thin; no Harmony is needed, since the mod drives everything itself. **The thread split matters most**: anything touching a Unity object - GameObjects, Transforms, LineRenderers - happens in `OnUpdate`, on the main rendering thread, while `DisasterHelpers` and `NaturalResourceManager` - the crater, the destruction and the contamination - happen in `OnBeforeSimulationTick` and `OnAfterSimulationTick`, on the simulation thread. Anything shared across that boundary is a simple value-typed field - an enum, a float, a Vector3 - and each field is only ever written by one thread.

**Tech stack:** C# on .NET Framework 3.5 for the mod itself, built with MSBuild, referencing `ICities`, `Assembly-CSharp`, `UnityEngine` and `ColossalManaged`. The tests run on .NET 8 with xUnit, linking the Core sources. The 3D assets are packed into an AssetBundle with Unity 5.6.6f2 and shipped with the mod.

## Global Constraints

- The mod targets **.NET Framework 3.5**. `Core/` is compiled for both net35 and net8, so it must avoid anything net35 lacks, such as ValueTuple.
- The game DLLs come from `Cities_Data\Managed\` in the game's installation: `ICities.dll`, `Assembly-CSharp.dll`, `UnityEngine.dll` and `ColossalManaged.dll`, referenced with `Private=False`.
- **This mod needs no Harmony**, since it patches no existing method. The `ICities` interfaces - `IUserMod`, `ThreadingExtensionBase` and `SerializableDataExtensionBase` - are found by the game on its own.
- It deploys to `%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\AlienInvasion\`, with the AssetBundle shipped as `Assets\alieninvasion.bundle`.
- Logging goes through `UnityEngine.Debug.Log` with the `"[AlienInvasion] "` prefix and nowhere else.
- Every tick, GameObject creation, effect and serialisation path is wrapped in try/catch, so no exception propagates into the game.
- **If the AssetBundle or a prefab cannot be loaded, log it and skip that piece of the presentation** rather than dragging the game down. The build and the launch both succeed even before the `.bundle` exists.
- The real APIs, confirmed by decompiling, to be used exactly as written in the tasks below:
  - `UnityEngine.AssetBundle.LoadFromFile(string path)`, static, returning `AssetBundle`
  - `AssetBundle.LoadAsset<T>(string name)` where `T : UnityEngine.Object`
  - `UnityEngine.LineRenderer`: `startWidth`, `endWidth`, `positionCount` and `useWorldSpace`, all `extern` properties, plus `SetPosition(int, Vector3)` and `SetPositions(Vector3[])`. It inherits `material` from `Renderer`.
  - `UnityEngine.Shader.Find(string name)`, static, using the built-in shader `"Particles/Additive"`.
  - `UnityEngine.Object.FindObjectOfType<T>()`, static
  - `RainProperties`, a `MonoBehaviour`, has the field `public AudioInfo m_ThunderSound;`. It is played with `Singleton<AudioManager>.instance.AddEvent(Singleton<AudioManager>.instance.AmbientGroup, audioInfo, position, Vector3.zero, 200f, 1f, 1f)`.
  - `DisasterHelpers.MakeCrater(Vector2 position, float radius, float depth, bool raiseEdges)`, static
  - `DisasterHelpers.DestroyStuff(int seed, InstanceManager.Group group, Vector3 position, float totalRadius, float preRadius, float removeRadius, float destructionRadiusMin, float destructionRadiusMax, float burnRadiusMin, float burnRadiusMax)`, static. **A known trap:** `preRadius` acts as a gate, standing for how far the shockwave has reached, and passing `preRadius=0` makes **the internal distance test always false, so nothing is destroyed at all**. This was hit for real in the Nuclear Meltdown mod. Always pass `preRadius = totalRadius`.
  - `NaturalResourceManager.instance.m_naturalResources[index].m_pollution`, a public byte in an array of structs so it can be assigned in place, and `NaturalResourceManager.instance.AreaModifiedB(minX,minZ,maxX,maxZ)`. The grid constants: `CellSize=33.75f`, `Resolution=512`, `cell=Clamp((int)(world/33.75f+256f),0,511)` and `index=cellZ*512+cellX`.
  - `MeteorAI.m_impactEffect`, a public `EffectInfo`, can be reached through `PrefabCollection<VehicleInfo>` with `VehicleInfo.m_vehicleAI as MeteorAI`. It is borrowed for the flash of the explosion.
  - `Singleton<EffectManager>.instance.DispatchEffect(EffectInfo, InstanceID, EffectInfo.SpawnArea, Vector3, float, float, AudioGroup)`
  - `ThreadingExtensionBase`, in the `ICities` namespace: `OnUpdate(float realTimeDelta, float simulationTimeDelta)` on the **main rendering thread**, and `OnBeforeSimulationTick()` and `OnAfterSimulationTick()` on the **simulation thread**.
  - `SerializableDataExtensionBase`: `OnSaveData()` and `OnLoadData()`, with `serializableDataManager.SaveData(id, byte[])` and `LoadData(id)`.
- The AssetBundle is built with Unity **5.6.6f2**. `Cities.exe` is actually a 5.6.7 build, but that is interchangeable with the 5.6.6 the community standardised on.
- The contamination radius, the durations, the probabilities, the key and everything like them are gathered in one place as `ModConfig` constants so they can be tuned; this was a user requirement.
- **What was confirmed about the 3D models**, checked directly over the Blender MCP connection: `MotherShip`'s pivot is at its geometric centre, which is right. `TriPod`'s pivot started near the head rather than at the ground contact point, so the origin has been moved to the lowest point of the legs - the centre of the bottom of the bounding box - without moving the mesh itself. Neither model has any material assigned yet. `MotherShip` still has an unapplied Z scale of 0.1 and `TriPod` an unapplied Y scale of 1.5; both are best applied before the FBX export. All of this belongs to building the AssetBundle in Task 14 and does not block the C# work in this plan.

---

## File Structure

```
<repository root>/
├─ AlienInvasion.sln
├─ build.ps1
├─ models/source/                         # the Blender sources: MotherShip.stl, TriPod.stl, models.blend
├─ unity-project/                          # the Unity project that builds the AssetBundle, created in Task 14
│  └─ Assets/Editor/BuildAssetBundles.cs
├─ src/AlienInvasion/
│  ├─ AlienInvasion.csproj
│  ├─ Properties/AssemblyInfo.cs
│  ├─ Assets/alieninvasion.bundle          # built in Unity by the user and placed here
│  ├─ Core/                                 # no Unity dependency; this is what the tests cover
│  │   ├─ InvasionState.cs                  # the state enum and the transition rules
│  │   ├─ ContaminationZone.cs              # a struct: centre, radius, start time
│  │   ├─ GridMath.cs                       # coordinate conversion and enumerating the cells in a radius
│  │   ├─ ExpiryClock.cs                    # the N-year expiry test
│  │   ├─ ZoneSerializer.cs                 # serialising the zone ledger
│  │   └─ MovementMath.cs                   # altitude interpolation and easing
│  ├─ Game/
│  │   ├─ Mod.cs                            # IUserMod
│  │   ├─ ModConfig.cs                      # every constant
│  │   ├─ AssetLoader.cs                    # loading the AssetBundle
│  │   ├─ PollutionField.cs                 # writing to NaturalResourceManager
│  │   ├─ ContaminationManager.cs           # the contamination zone ledger
│  │   ├─ RedContaminationVisual.cs         # placing and removing the red decals
│  │   ├─ Effects.cs                        # the lightning bolts, the flash and the thunder
│  │   ├─ InvasionManager.cs                # the state machine that runs it all
│  │   ├─ Mothership.cs                     # driving the mothership GameObject
│  │   ├─ Simulation/InvasionThreadingExtension.cs
│  │   └─ Serialization/InvasionDataExtension.cs
│  └─ README.md
└─ tests/AlienInvasion.Core.Tests/
   ├─ AlienInvasion.Core.Tests.csproj
   ├─ InvasionStateTests.cs
   ├─ GridMathTests.cs
   ├─ ExpiryClockTests.cs
   ├─ ZoneSerializerTests.cs
   └─ MovementMathTests.cs
```

**Dependencies point one way:** `Game/*` depends on `Core/*`, and `Core/*` depends on nothing else.

---

## Task 1: Core - InvasionState and ContaminationZone

**Files:**
- Create: `src/AlienInvasion/Core/InvasionState.cs`
- Create: `src/AlienInvasion/Core/ContaminationZone.cs`
- Create: `tests/AlienInvasion.Core.Tests/AlienInvasion.Core.Tests.csproj`
- Create: `tests/AlienInvasion.Core.Tests/InvasionStateTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces:
  - `enum InvasionState { Idle, Descending, Bombarding, Ascending, Done }` in the `AlienInvasion.Core` namespace
  - `static class InvasionStateMachine { static bool CanTransition(InvasionState from, InvasionState to); static InvasionState Next(InvasionState current); }`, where the only permitted transitions run one way round the cycle `Idle`, `Descending`, `Bombarding`, `Ascending`, `Done`, `Idle`.
  - `struct ContaminationZone { public float CenterX; public float CenterZ; public float Radius; public long StartTicks; public ContaminationZone(float centerX, float centerZ, float radius, long startTicks); }`

- [ ] **Step 1: create the test project.**

`tests/AlienInvasion.Core.Tests/AlienInvasion.Core.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>disable</Nullable>
    <LangVersion>7.3</LangVersion>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="..\..\src\AlienInvasion\Core\**\*.cs" LinkBase="Core" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: write the failing tests.**

`tests/AlienInvasion.Core.Tests/InvasionStateTests.cs`:
```csharp
using AlienInvasion.Core;
using Xunit;

public class InvasionStateTests
{
    [Theory]
    [InlineData(InvasionState.Idle, InvasionState.Descending, true)]
    [InlineData(InvasionState.Descending, InvasionState.Bombarding, true)]
    [InlineData(InvasionState.Bombarding, InvasionState.Ascending, true)]
    [InlineData(InvasionState.Ascending, InvasionState.Done, true)]
    [InlineData(InvasionState.Done, InvasionState.Idle, true)]
    [InlineData(InvasionState.Idle, InvasionState.Bombarding, false)]
    [InlineData(InvasionState.Descending, InvasionState.Idle, false)]
    [InlineData(InvasionState.Bombarding, InvasionState.Done, false)]
    public void CanTransition_follows_linear_cycle(InvasionState from, InvasionState to, bool expected)
    {
        Assert.Equal(expected, InvasionStateMachine.CanTransition(from, to));
    }

    [Theory]
    [InlineData(InvasionState.Idle, InvasionState.Descending)]
    [InlineData(InvasionState.Descending, InvasionState.Bombarding)]
    [InlineData(InvasionState.Bombarding, InvasionState.Ascending)]
    [InlineData(InvasionState.Ascending, InvasionState.Done)]
    [InlineData(InvasionState.Done, InvasionState.Idle)]
    public void Next_returns_the_following_state(InvasionState current, InvasionState expected)
    {
        Assert.Equal(expected, InvasionStateMachine.Next(current));
    }

    [Fact]
    public void ContaminationZone_stores_fields()
    {
        var z = new ContaminationZone(10f, 20f, 60f, 123L);
        Assert.Equal(10f, z.CenterX);
        Assert.Equal(20f, z.CenterZ);
        Assert.Equal(60f, z.Radius);
        Assert.Equal(123L, z.StartTicks);
    }
}
```

- [ ] **Step 3: run the tests and confirm they fail.**

Run: `dotnet test tests/AlienInvasion.Core.Tests`
Expected: FAIL with a compile error, since `InvasionState`, `InvasionStateMachine` and `ContaminationZone` do not exist yet

- [ ] **Step 4: implement it.**

`src/AlienInvasion/Core/InvasionState.cs`:
```csharp
namespace AlienInvasion.Core
{
    /// <summary>How far a single invasion has progressed. A one-way cycle: Idle, Descending, Bombarding, Ascending, Done, Idle.</summary>
    public enum InvasionState
    {
        Idle,
        Descending,
        Bombarding,
        Ascending,
        Done
    }

    /// <summary>The state machine logic, which lets only the permitted InvasionState transitions through.</summary>
    public static class InvasionStateMachine
    {
        public static bool CanTransition(InvasionState from, InvasionState to)
        {
            return Next(from) == to;
        }

        public static InvasionState Next(InvasionState current)
        {
            switch (current)
            {
                case InvasionState.Idle: return InvasionState.Descending;
                case InvasionState.Descending: return InvasionState.Bombarding;
                case InvasionState.Bombarding: return InvasionState.Ascending;
                case InvasionState.Ascending: return InvasionState.Done;
                case InvasionState.Done: return InvasionState.Idle;
                default: return InvasionState.Idle;
            }
        }
    }
}
```

`src/AlienInvasion/Core/ContaminationZone.cs`:
```csharp
namespace AlienInvasion.Core
{
    /// <summary>A contamination zone: world-space centre, radius in metres, and the in-game time it started (DateTime.Ticks).</summary>
    public struct ContaminationZone
    {
        public float CenterX;
        public float CenterZ;
        public float Radius;
        public long StartTicks;

        public ContaminationZone(float centerX, float centerZ, float radius, long startTicks)
        {
            CenterX = centerX;
            CenterZ = centerZ;
            Radius = radius;
            StartTicks = startTicks;
        }
    }
}
```

- [ ] **Step 5: run the tests and confirm they pass.**

Run: `dotnet test tests/AlienInvasion.Core.Tests`
Expected: PASS (all of them)

- [ ] **Step 6: commit.**

```bash
git add src/AlienInvasion/Core/InvasionState.cs src/AlienInvasion/Core/ContaminationZone.cs tests/AlienInvasion.Core.Tests
git commit -m "feat: add the InvasionState state machine and ContaminationZone"
```

---

## Task 2: Core - GridMath (coordinate conversion and enumerating the cells in a radius)

**Files:**
- Create: `src/AlienInvasion/Core/GridMath.cs`
- Test: `tests/AlienInvasion.Core.Tests/GridMathTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces, on `static class GridMath` in the `AlienInvasion.Core` namespace:
  - `const float CellSize = 33.75f;`
  - `const int Resolution = 512;`
  - `int WorldToCell(float world)` → `Clamp((int)(world/33.75f+256f), 0, 511)`
  - `int CellIndex(int cellX, int cellZ)` → `cellZ*512+cellX`
  - `System.Collections.Generic.List<int> CellsInRadius(float centerX, float centerZ, float radiusMeters)` - lists every cell index inside the radius, without duplicates, testing the circle against the world distance to each cell's centre.

- [ ] **Step 1: write the failing tests.**

`tests/AlienInvasion.Core.Tests/GridMathTests.cs`:
```csharp
using System.Collections.Generic;
using AlienInvasion.Core;
using Xunit;

public class GridMathTests
{
    [Fact]
    public void WorldToCell_maps_origin_to_center()
    {
        Assert.Equal(256, GridMath.WorldToCell(0f));
    }

    [Fact]
    public void WorldToCell_clamps_out_of_range()
    {
        Assert.Equal(0, GridMath.WorldToCell(-100000f));
        Assert.Equal(511, GridMath.WorldToCell(100000f));
    }

    [Fact]
    public void CellIndex_is_row_major()
    {
        Assert.Equal(2 * 512 + 3, GridMath.CellIndex(3, 2));
    }

    [Fact]
    public void CellsInRadius_contains_center_cell()
    {
        var cells = GridMath.CellsInRadius(0f, 0f, 100f);
        int centerIndex = GridMath.CellIndex(256, 256);
        Assert.Contains(centerIndex, cells);
    }

    [Fact]
    public void CellsInRadius_excludes_cells_outside_radius()
    {
        var cells = GridMath.CellsInRadius(0f, 0f, 10f);
        foreach (var idx in cells)
        {
            int cz = idx / 512;
            int cx = idx % 512;
            Assert.InRange(cx, 255, 257);
            Assert.InRange(cz, 255, 257);
        }
    }

    [Fact]
    public void CellsInRadius_indices_are_unique()
    {
        var cells = GridMath.CellsInRadius(0f, 0f, 200f);
        var seen = new HashSet<int>();
        foreach (var idx in cells) Assert.True(seen.Add(idx), "duplicate index " + idx);
    }
}
```

- [ ] **Step 2: run the tests and confirm they fail.**

Run: `dotnet test tests/AlienInvasion.Core.Tests`
Expected: FAIL, `GridMath` is not defined yet

- [ ] **Step 3: implement.**

`src/AlienInvasion/Core/GridMath.cs`:
```csharp
using System.Collections.Generic;

namespace AlienInvasion.Core
{
    /// <summary>Coordinate maths for NaturalResourceManager's pollution grid: 512x512, 33.75 m cells.</summary>
    public static class GridMath
    {
        public const float CellSize = 33.75f;
        public const int Resolution = 512;

        public static int WorldToCell(float world)
        {
            int cell = (int)(world / CellSize + 256f);
            if (cell < 0) return 0;
            if (cell > Resolution - 1) return Resolution - 1;
            return cell;
        }

        public static int CellIndex(int cellX, int cellZ)
        {
            return cellZ * Resolution + cellX;
        }

        public static List<int> CellsInRadius(float centerX, float centerZ, float radiusMeters)
        {
            var result = new List<int>();
            if (radiusMeters <= 0f) return result;

            int cellRadius = (int)(radiusMeters / CellSize) + 1;
            int centerCellX = WorldToCell(centerX);
            int centerCellZ = WorldToCell(centerZ);

            for (int dz = -cellRadius; dz <= cellRadius; dz++)
            {
                int cz = centerCellZ + dz;
                if (cz < 0 || cz > Resolution - 1) continue;
                for (int dx = -cellRadius; dx <= cellRadius; dx++)
                {
                    int cx = centerCellX + dx;
                    if (cx < 0 || cx > Resolution - 1) continue;

                    float worldDx = dx * CellSize;
                    float worldDz = dz * CellSize;
                    float dist = (float)System.Math.Sqrt(worldDx * worldDx + worldDz * worldDz);
                    if (dist > radiusMeters) continue;

                    result.Add(CellIndex(cx, cz));
                }
            }
            return result;
        }
    }
}
```

- [ ] **Step 4: run the tests and confirm they pass.**

Run: `dotnet test tests/AlienInvasion.Core.Tests`
Expected: PASS (all of them)

- [ ] **Step 5: commit.**

```bash
git add src/AlienInvasion/Core/GridMath.cs tests/AlienInvasion.Core.Tests/GridMathTests.cs
git commit -m "feat: add GridMath, the coordinate conversion and radius enumeration"
```

---

## Task 3: Core - ExpiryClock (the N-year expiry test)

**Files:**
- Create: `src/AlienInvasion/Core/ExpiryClock.cs`
- Test: `tests/AlienInvasion.Core.Tests/ExpiryClockTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces, on `static class ExpiryClock` in the `AlienInvasion.Core` namespace:
  - `bool HasExpired(long startTicks, long nowTicks, int years)` - `now >= start.AddYears(years)`.

- [ ] **Step 1: write the failing tests.**

`tests/AlienInvasion.Core.Tests/ExpiryClockTests.cs`:
```csharp
using System;
using AlienInvasion.Core;
using Xunit;

public class ExpiryClockTests
{
    [Fact]
    public void Not_expired_before_years_elapse()
    {
        var start = new DateTime(2000, 1, 1);
        var now = new DateTime(2000, 12, 31);
        Assert.False(ExpiryClock.HasExpired(start.Ticks, now.Ticks, 1));
    }

    [Fact]
    public void Expired_exactly_at_boundary()
    {
        var start = new DateTime(2000, 1, 1);
        var now = new DateTime(2001, 1, 1);
        Assert.True(ExpiryClock.HasExpired(start.Ticks, now.Ticks, 1));
    }

    [Fact]
    public void Expired_after_boundary()
    {
        var start = new DateTime(2000, 6, 15);
        var now = new DateTime(2002, 1, 1);
        Assert.True(ExpiryClock.HasExpired(start.Ticks, now.Ticks, 1));
    }
}
```

- [ ] **Step 2: run the tests and confirm they fail.**

Run: `dotnet test tests/AlienInvasion.Core.Tests`
Expected: FAIL, `ExpiryClock` is not defined yet

- [ ] **Step 3: implement.**

`src/AlienInvasion/Core/ExpiryClock.cs`:
```csharp
using System;

namespace AlienInvasion.Core
{
    /// <summary>Decides when a contamination zone has aged out, based on in-game time.</summary>
    public static class ExpiryClock
    {
        public static bool HasExpired(long startTicks, long nowTicks, int years)
        {
            DateTime start = new DateTime(startTicks);
            DateTime expiry = start.AddYears(years);
            return nowTicks >= expiry.Ticks;
        }
    }
}
```

- [ ] **Step 4: run the tests and confirm they pass.**

Run: `dotnet test tests/AlienInvasion.Core.Tests`
Expected: PASS (all of them)

- [ ] **Step 5: commit.**

```bash
git add src/AlienInvasion/Core/ExpiryClock.cs tests/AlienInvasion.Core.Tests/ExpiryClockTests.cs
git commit -m "feat: add ExpiryClock, the N-year expiry test"
```

---

## Task 4: Core - ZoneSerializer (serialising the zone ledger)

**Files:**
- Create: `src/AlienInvasion/Core/ZoneSerializer.cs`
- Test: `tests/AlienInvasion.Core.Tests/ZoneSerializerTests.cs`

**Interfaces:**
- Consumes `ContaminationZone` from Task 1
- Produces, on `static class ZoneSerializer` in the `AlienInvasion.Core` namespace:
  - `const byte Version = 1;`
  - `byte[] Serialize(List<ContaminationZone> zones)`
  - `List<ContaminationZone> Deserialize(byte[] data)` - returns an empty list for null, too-short, unknown-version or corrupt data, and never throws.

- [ ] **Step 1: write the failing tests.**

`tests/AlienInvasion.Core.Tests/ZoneSerializerTests.cs`:
```csharp
using System.Collections.Generic;
using AlienInvasion.Core;
using Xunit;

public class ZoneSerializerTests
{
    [Fact]
    public void Round_trips_zones()
    {
        var zones = new List<ContaminationZone>
        {
            new ContaminationZone(100f, -200f, 60f, 630000000000000000L),
            new ContaminationZone(0f, 0f, 40f, 630000000000000001L),
        };
        byte[] bytes = ZoneSerializer.Serialize(zones);
        List<ContaminationZone> back = ZoneSerializer.Deserialize(bytes);

        Assert.Equal(2, back.Count);
        Assert.Equal(100f, back[0].CenterX);
        Assert.Equal(-200f, back[0].CenterZ);
        Assert.Equal(60f, back[0].Radius);
        Assert.Equal(630000000000000000L, back[0].StartTicks);
        Assert.Equal(630000000000000001L, back[1].StartTicks);
    }

    [Fact]
    public void Empty_list_round_trips()
    {
        byte[] bytes = ZoneSerializer.Serialize(new List<ContaminationZone>());
        Assert.Empty(ZoneSerializer.Deserialize(bytes));
    }

    [Fact]
    public void Null_input_returns_empty()
    {
        Assert.Empty(ZoneSerializer.Deserialize(null));
    }

    [Fact]
    public void Corrupt_input_returns_empty_without_throwing()
    {
        Assert.Empty(ZoneSerializer.Deserialize(new byte[] { 9, 9, 9 }));
    }
}
```

- [ ] **Step 2: run the tests and confirm they fail.**

Run: `dotnet test tests/AlienInvasion.Core.Tests`
Expected: FAIL, `ZoneSerializer` is not defined yet

- [ ] **Step 3: implement.**

`src/AlienInvasion/Core/ZoneSerializer.cs`:
```csharp
using System.Collections.Generic;
using System.IO;

namespace AlienInvasion.Core
{
    /// <summary>Serialises the contamination zone ledger to and from byte[] for the save game.</summary>
    public static class ZoneSerializer
    {
        public const byte Version = 1;

        public static byte[] Serialize(List<ContaminationZone> zones)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Version);
                w.Write(zones.Count);
                for (int i = 0; i < zones.Count; i++)
                {
                    var z = zones[i];
                    w.Write(z.CenterX);
                    w.Write(z.CenterZ);
                    w.Write(z.Radius);
                    w.Write(z.StartTicks);
                }
                w.Flush();
                return ms.ToArray();
            }
        }

        public static List<ContaminationZone> Deserialize(byte[] data)
        {
            var result = new List<ContaminationZone>();
            if (data == null || data.Length < 5) return result;
            try
            {
                using (var ms = new MemoryStream(data))
                using (var r = new BinaryReader(ms))
                {
                    byte version = r.ReadByte();
                    if (version != Version) return new List<ContaminationZone>();
                    int count = r.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        float cx = r.ReadSingle();
                        float cz = r.ReadSingle();
                        float radius = r.ReadSingle();
                        long start = r.ReadInt64();
                        result.Add(new ContaminationZone(cx, cz, radius, start));
                    }
                }
            }
            catch
            {
                return new List<ContaminationZone>();
            }
            return result;
        }
    }
}
```

- [ ] **Step 4: run the tests and confirm they pass.**

Run: `dotnet test tests/AlienInvasion.Core.Tests`
Expected: PASS (all of them)

- [ ] **Step 5: commit.**

```bash
git add src/AlienInvasion/Core/ZoneSerializer.cs tests/AlienInvasion.Core.Tests/ZoneSerializerTests.cs
git commit -m "feat: add ZoneSerializer, serialising and restoring the zone ledger"
```

---

## Task 5: Core - MovementMath (altitude interpolation and easing)

**Files:**
- Create: `src/AlienInvasion/Core/MovementMath.cs`
- Test: `tests/AlienInvasion.Core.Tests/MovementMathTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces, on `static class MovementMath` in the `AlienInvasion.Core` namespace:
  - `float EaseInOut(float t)` - turns `t` in 0-1 into a smooth acceleration and deceleration curve, the `3t²-2t³` smoothstep.
  - `float Lerp(float a, float b, float t)` - clamps `t` to 0-1 and interpolates linearly.
  - `bool IsNear(float a, float b, float epsilon)` - whether the difference is within `epsilon`. This is for the tripod movement in Phase 2; it is unused in this plan but tested as part of the public API.

- [ ] **Step 1: write the failing tests.**

`tests/AlienInvasion.Core.Tests/MovementMathTests.cs`:
```csharp
using AlienInvasion.Core;
using Xunit;

public class MovementMathTests
{
    [Fact]
    public void EaseInOut_endpoints_are_stable()
    {
        Assert.Equal(0f, MovementMath.EaseInOut(0f), 3);
        Assert.Equal(1f, MovementMath.EaseInOut(1f), 3);
    }

    [Fact]
    public void EaseInOut_midpoint_is_half()
    {
        Assert.Equal(0.5f, MovementMath.EaseInOut(0.5f), 3);
    }

    [Fact]
    public void Lerp_clamps_t_below_zero()
    {
        Assert.Equal(10f, MovementMath.Lerp(10f, 20f, -1f));
    }

    [Fact]
    public void Lerp_clamps_t_above_one()
    {
        Assert.Equal(20f, MovementMath.Lerp(10f, 20f, 2f));
    }

    [Fact]
    public void Lerp_interpolates_at_half()
    {
        Assert.Equal(15f, MovementMath.Lerp(10f, 20f, 0.5f));
    }

    [Fact]
    public void IsNear_true_within_epsilon()
    {
        Assert.True(MovementMath.IsNear(10.0f, 10.05f, 0.1f));
    }

    [Fact]
    public void IsNear_false_outside_epsilon()
    {
        Assert.False(MovementMath.IsNear(10.0f, 10.5f, 0.1f));
    }
}
```

- [ ] **Step 2: run the tests and confirm they fail.**

Run: `dotnet test tests/AlienInvasion.Core.Tests`
Expected: FAIL, `MovementMath` is not defined yet

- [ ] **Step 3: implement.**

`src/AlienInvasion/Core/MovementMath.cs`:
```csharp
namespace AlienInvasion.Core
{
    /// <summary>The pure maths used to interpolate the mothership's and the effects' positions.</summary>
    public static class MovementMath
    {
        public static float EaseInOut(float t)
        {
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return t * t * (3f - 2f * t);
        }

        public static float Lerp(float a, float b, float t)
        {
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return a + (b - a) * t;
        }

        public static bool IsNear(float a, float b, float epsilon)
        {
            float diff = a - b;
            if (diff < 0f) diff = -diff;
            return diff <= epsilon;
        }
    }
}
```

- [ ] **Step 4: run the tests and confirm they pass.**

Run: `dotnet test tests/AlienInvasion.Core.Tests`
Expected: PASS (all of them)

- [ ] **Step 5: commit.**

```bash
git add src/AlienInvasion/Core/MovementMath.cs tests/AlienInvasion.Core.Tests/MovementMathTests.cs
git commit -m "feat: add MovementMath, the altitude interpolation and easing"
```

---

## Task 6: the mod project (csproj, AssemblyInfo, ModConfig, Mod) and verifying the build

**Files:**
- Create: `src/AlienInvasion/AlienInvasion.csproj`
- Create: `src/AlienInvasion/Properties/AssemblyInfo.cs`
- Create: `src/AlienInvasion/Game/ModConfig.cs`
- Create: `src/AlienInvasion/Game/Mod.cs`
- Create: `AlienInvasion.sln`
- Create: `build.ps1`

**Interfaces:**
- Consumes: nothing
- Produces:
  - `static class ModConfig` in the `AlienInvasion.Game` namespace: every constant, defined in Step 2 below, plus `static void Log(string)` and `static void LogError(string)`.
  - `class Mod : IUserMod` with `Name` and `Description` as get-only properties.

- [ ] **Step 1: create the csproj.**

`src/AlienInvasion/AlienInvasion.csproj`:
```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(MSBuildToolsPath)\Microsoft.Common.props" Condition="Exists('$(MSBuildToolsPath)\Microsoft.Common.props')" />
  <PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Release</Configuration>
    <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
    <ProjectGuid>{C2A1B3D0-0000-4000-8000-000000000001}</ProjectGuid>
    <OutputType>Library</OutputType>
    <RootNamespace>AlienInvasion</RootNamespace>
    <AssemblyName>AlienInvasion</AssemblyName>
    <TargetFrameworkVersion>v3.5</TargetFrameworkVersion>
    <LangVersion>7.3</LangVersion>
    <FileAlignment>512</FileAlignment>
    <ManagedDLLPath>C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed</ManagedDLLPath>
  </PropertyGroup>
  <PropertyGroup Condition=" '$(Configuration)' == 'Release' ">
    <OutputPath>bin\Release\</OutputPath>
    <DefineConstants>TRACE</DefineConstants>
    <Optimize>true</Optimize>
    <DebugType>pdbonly</DebugType>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="System" />
    <Reference Include="System.Core" />
    <Reference Include="ICities">
      <HintPath>$(ManagedDLLPath)\ICities.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="Assembly-CSharp">
      <HintPath>$(ManagedDLLPath)\Assembly-CSharp.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="ColossalManaged">
      <HintPath>$(ManagedDLLPath)\ColossalManaged.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="UnityEngine">
      <HintPath>$(ManagedDLLPath)\UnityEngine.dll</HintPath>
      <Private>False</Private>
    </Reference>
  </ItemGroup>
  <ItemGroup>
    <Compile Include="Core\**\*.cs" />
    <Compile Include="Game\**\*.cs" />
    <Compile Include="Properties\AssemblyInfo.cs" />
  </ItemGroup>
  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
</Project>
```

- [ ] **Step 2: create AssemblyInfo and ModConfig.**

`src/AlienInvasion/Properties/AssemblyInfo.cs`:
```csharp
using System.Reflection;
[assembly: AssemblyTitle("AlienInvasion")]
[assembly: AssemblyProduct("AlienInvasion")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
```

`src/AlienInvasion/Game/ModConfig.cs`:
```csharp
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>Mod-wide constants and shared logging.</summary>
    public static class ModConfig
    {
        public const string LogPrefix = "[AlienInvasion] ";

        // --- AssetBundle ---
        public const string AssetBundleFileName = "alieninvasion.bundle";
        public const string MothershipPrefabName = "Mothership";
        public const string RedDecalPrefabName = "ContaminationDecal";

        // --- The mothership's flight ---
        public const float MothershipStartAltitude = 800f;   // the altitude it appears at, relative to the ground
        public const float MothershipHoverAltitude = 220f;   // the altitude it hovers at once it has descended
        public const float DescendSeconds = 6f;
        public const float BombardSeconds = 10f;
        public const float StrikeIntervalSeconds = 0.6f;
        public const float AscendSeconds = 5f;

        // --- The crater and the destruction. These accumulate through Bombarding and settle when it ends. ---
        public const float CraterRadiusMax = 90f;
        public const float CraterDepthMax = 22f;
        public const float StrikeScatterRadius = 15f;   // how far each strike is scattered from the centre
        public const float DestructionRadius = 70f;     // the radius buildings are destroyed within when Bombarding ends

        // --- The red contamination ---
        public const int ExpiryYears = 1;
        public const float ContaminationRadius = 90f;   // the radius of the contamination left where the crater is
        public const byte MaxPollution = 255;
        public const float RedDecalYOffset = 0.3f;

        // --- Triggering ---
        public const KeyCode ManualTriggerKey = KeyCode.F7;
        public const int RandomCheckIntervalTicks = 4096;
        public const int RandomChancePer10000 = 1;

        public static void Log(string msg)
        {
            Debug.Log(LogPrefix + msg);
        }

        public static void LogError(string msg)
        {
            Debug.LogError(LogPrefix + msg);
        }
    }
}
```

- [ ] **Step 3: create Mod.cs.**

`src/AlienInvasion/Game/Mod.cs`:
```csharp
using ICities;

namespace AlienInvasion.Game
{
    public class Mod : IUserMod
    {
        public string Name => "Alien Invasion";
        public string Description => "A UFO mothership descends, wrecks the city with lightning and a crater, and leaves radioactive contamination behind. Trigger it with the F7 key.";
    }
}
```

- [ ] **Step 4: create the solution file.**

`AlienInvasion.sln`:
```
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "AlienInvasion", "src\AlienInvasion\AlienInvasion.csproj", "{C2A1B3D0-0000-4000-8000-000000000001}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{C2A1B3D0-0000-4000-8000-000000000001}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{C2A1B3D0-0000-4000-8000-000000000001}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
EndGlobal
```

- [ ] **Step 5: create build.ps1**, saved as UTF-8 with a BOM, since it contains non-ASCII text.

`build.ps1`:
```powershell
$ErrorActionPreference = "Stop"
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
if (-not $msbuild) { throw "MSBuild not found" }

& $msbuild "src\AlienInvasion\AlienInvasion.csproj" /t:Restore,Build /p:Configuration=Release /v:minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$dll = "src\AlienInvasion\bin\Release\AlienInvasion.dll"
$modDir = Join-Path $env:LOCALAPPDATA "Colossal Order\Cities_Skylines\Addons\Mods\AlienInvasion"
New-Item -ItemType Directory -Force -Path $modDir | Out-Null
Copy-Item $dll $modDir -Force

$bundleDir = Join-Path $modDir "Assets"
New-Item -ItemType Directory -Force -Path $bundleDir | Out-Null
$bundleSrc = "src\AlienInvasion\Assets\alieninvasion.bundle"
if (Test-Path $bundleSrc) {
    Copy-Item $bundleSrc $bundleDir -Force
    Write-Host "Deployed the AssetBundle"
} else {
    Write-Host "Warning: $bundleSrc not found. The visuals - the mothership and the red decals - will be skipped at startup."
}
Write-Host "Deploy complete: $modDir"
```
This file **must be saved as UTF-8 with a BOM**, so PowerShell 5.1 reads its non-ASCII string literals correctly. This was hit for real in the Nuclear Meltdown mod.

- [ ] **Step 6: create the `src/AlienInvasion/Assets/` directory**, a placeholder for where the AssetBundle goes.

```bash
mkdir -p src/AlienInvasion/Assets
touch src/AlienInvasion/Assets/.gitkeep
```

- [ ] **Step 7: verify the build.**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: the build succeeds, `AlienInvasion.dll` is produced and copied into the mod folder. `alieninvasion.bundle` does not exist yet so a warning appears, which is expected; Task 14 provides it.

- [ ] **Step 8: commit.**

```bash
git add src/AlienInvasion/AlienInvasion.csproj src/AlienInvasion/Properties src/AlienInvasion/Game/ModConfig.cs src/AlienInvasion/Game/Mod.cs src/AlienInvasion/Assets/.gitkeep AlienInvasion.sln build.ps1
git commit -m "feat: add the mod project skeleton and the build and deploy script"
```

---

## Task 7: AssetLoader (loading the AssetBundle)

**Files:**
- Create: `src/AlienInvasion/Game/AssetLoader.cs`

**Interfaces:**
- Consumes `ModConfig` from Task 6
- Produces, on `static class AssetLoader` in the `AlienInvasion.Game` namespace:
  - `void Initialize(string modAssemblyDirectory)` - loads the `.bundle`, or just logs and carries on if it is not there.
  - `GameObject GetPrefab(string name)` - fetches a loaded prefab by name, or `null` if there is none.
  - `bool IsAvailable { get; }` - whether the AssetBundle loaded successfully.

- [ ] **Step 1: implement.**

`src/AlienInvasion/Game/AssetLoader.cs`:
```csharp
using System.IO;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>Loads the prefabs from the AssetBundle shipped with the mod, quietly skipping it if it is not there.</summary>
    public static class AssetLoader
    {
        private static AssetBundle _bundle;
        private static bool _initialized;

        public static bool IsAvailable
        {
            get { return _bundle != null; }
        }

        public static void Initialize(string modAssemblyDirectory)
        {
            if (_initialized) return;
            _initialized = true;
            try
            {
                string path = Path.Combine(Path.Combine(modAssemblyDirectory, "Assets"), ModConfig.AssetBundleFileName);
                if (!File.Exists(path))
                {
                    ModConfig.Log("AssetBundle not found at " + path + " — visuals will be skipped");
                    return;
                }
                _bundle = AssetBundle.LoadFromFile(path);
                if (_bundle == null)
                {
                    ModConfig.LogError("AssetBundle.LoadFromFile returned null for " + path);
                    return;
                }
                ModConfig.Log("AssetBundle loaded from " + path);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("AssetLoader.Initialize error: " + e);
            }
        }

        public static GameObject GetPrefab(string name)
        {
            if (_bundle == null) return null;
            try
            {
                return _bundle.LoadAsset<GameObject>(name);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("GetPrefab(" + name + ") error: " + e);
                return null;
            }
        }
    }
}
```

- [ ] **Step 2: update Mod.cs to call Initialize.**

Replace `src/AlienInvasion/Game/Mod.cs` with:
```csharp
using System.IO;
using System.Reflection;
using ICities;

namespace AlienInvasion.Game
{
    public class Mod : IUserMod
    {
        public string Name => "Alien Invasion";
        public string Description => "A UFO mothership descends, wrecks the city with lightning and a crater, and leaves radioactive contamination behind. Trigger it with the F7 key.";

        public void OnEnabled()
        {
            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            AssetLoader.Initialize(dir);
        }
    }
}
```

- [ ] **Step 3: verify the build.**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: the build succeeds.

- [ ] **Step 4: commit.**

```bash
git add src/AlienInvasion/Game/AssetLoader.cs src/AlienInvasion/Game/Mod.cs
git commit -m "feat: add AssetLoader, which loads the AssetBundle"
```

---

## Task 8: PollutionField and ContaminationManager (reading and writing the contamination, and the zone ledger)

**Files:**
- Create: `src/AlienInvasion/Game/PollutionField.cs`
- Create: `src/AlienInvasion/Game/ContaminationManager.cs`

**Interfaces:**
- Consumes `GridMath` and `ContaminationZone` from Core, plus `ModConfig`
- Produces:
  - `static class PollutionField`:
    - `void ApplyMax(int cellIndex, byte intensity)` - raises the cell's `m_pollution` to `Max(current, intensity)`.
    - `void ClearCell(int cellIndex)` - sets `m_pollution` to 0.
    - `void Refresh(int minX, int minZ, int maxX, int maxZ)` - calls `AreaModifiedB`.
  - `static class ContaminationManager`:
    - `List<ContaminationZone> Zones { get; }` - returns a copy, as a snapshot.
    - `void ReplaceAll(List<ContaminationZone> zones)` - used when restoring a save.
    - `void AddZone(ContaminationZone zone)` - adds it to the ledger and applies the initial contamination.
    - `void RemoveZoneAt(int index)`
    - `void ReassertZone(ContaminationZone zone)` - runs `ApplyMax` over the cells in the radius again, countering the natural decay.
    - `void ClearZone(ContaminationZone zone)` - zeroes the cells in the radius and refreshes.

- [ ] **Step 1: implement PollutionField.**

`src/AlienInvasion/Game/PollutionField.cs`:
```csharp
namespace AlienInvasion.Game
{
    /// <summary>A wrapper for reading and writing NaturalResourceManager's ground pollution cells.</summary>
    public static class PollutionField
    {
        public static void ApplyMax(int cellIndex, byte intensity)
        {
            var arr = NaturalResourceManager.instance.m_naturalResources;
            if (cellIndex < 0 || cellIndex >= arr.Length) return;
            if (arr[cellIndex].m_pollution < intensity)
            {
                arr[cellIndex].m_pollution = intensity;
            }
        }

        public static void ClearCell(int cellIndex)
        {
            var arr = NaturalResourceManager.instance.m_naturalResources;
            if (cellIndex < 0 || cellIndex >= arr.Length) return;
            arr[cellIndex].m_pollution = 0;
        }

        public static void Refresh(int minX, int minZ, int maxX, int maxZ)
        {
            NaturalResourceManager.instance.AreaModifiedB(minX, minZ, maxX, maxZ);
        }
    }
}
```

- [ ] **Step 2: implement ContaminationManager.**

`src/AlienInvasion/Game/ContaminationManager.cs`:
```csharp
using System.Collections.Generic;
using AlienInvasion.Core;

namespace AlienInvasion.Game
{
    /// <summary>The contamination zone ledger, and applying, holding and clearing it on the grid.</summary>
    public static class ContaminationManager
    {
        private static List<ContaminationZone> _zones = new List<ContaminationZone>();

        public static List<ContaminationZone> Zones
        {
            get { return new List<ContaminationZone>(_zones); }
        }

        public static void ReplaceAll(List<ContaminationZone> zones)
        {
            _zones = zones ?? new List<ContaminationZone>();
            for (int i = 0; i < _zones.Count; i++) ReassertZone(_zones[i]);
        }

        public static void AddZone(ContaminationZone zone)
        {
            _zones.Add(zone);
            ReassertZone(zone);
        }

        public static void RemoveZoneAt(int index)
        {
            if (index >= 0 && index < _zones.Count) _zones.RemoveAt(index);
        }

        public static void ReassertZone(ContaminationZone zone)
        {
            var cells = GridMath.CellsInRadius(zone.CenterX, zone.CenterZ, zone.Radius);
            for (int i = 0; i < cells.Count; i++) PollutionField.ApplyMax(cells[i], ModConfig.MaxPollution);
            RefreshZoneTexture(zone);
        }

        public static void ClearZone(ContaminationZone zone)
        {
            var cells = GridMath.CellsInRadius(zone.CenterX, zone.CenterZ, zone.Radius);
            for (int i = 0; i < cells.Count; i++) PollutionField.ClearCell(cells[i]);
            RefreshZoneTexture(zone);
        }

        public static void RefreshZoneTexture(ContaminationZone zone)
        {
            int cellRadius = (int)(zone.Radius / GridMath.CellSize) + 1;
            int cx = GridMath.WorldToCell(zone.CenterX);
            int cz = GridMath.WorldToCell(zone.CenterZ);
            int minX = Clamp(cx - cellRadius), maxX = Clamp(cx + cellRadius);
            int minZ = Clamp(cz - cellRadius), maxZ = Clamp(cz + cellRadius);
            PollutionField.Refresh(minX, minZ, maxX, maxZ);
        }

        private static int Clamp(int v)
        {
            if (v < 0) return 0;
            if (v > GridMath.Resolution - 1) return GridMath.Resolution - 1;
            return v;
        }
    }
}
```

- [ ] **Step 3: verify the build.**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: the build succeeds.

- [ ] **Step 4: commit.**

```bash
git add src/AlienInvasion/Game/PollutionField.cs src/AlienInvasion/Game/ContaminationManager.cs
git commit -m "feat: add PollutionField for writing the pollution grid and ContaminationManager for the zone ledger"
```

---

## Task 9: RedContaminationVisual (placing and removing the red decals)

**Files:**
- Create: `src/AlienInvasion/Game/RedContaminationVisual.cs`

**Interfaces:**
- Consumes `AssetLoader` and `ContaminationZone` from Core, plus `ModConfig`
- Produces, on `static class RedContaminationVisual`:
  - `void Sync(List<ContaminationZone> activeZones)` - creates and destroys the decal GameObjects to match the current list of zones, placing one decal per zone at `(CenterX, TerrainHeight, CenterZ)` scaled to `Radius*2`. **Main thread only**, since it touches GameObjects.
  - `void Clear()` - destroys every decal, for cleaning up when the mod is disabled or on an error.

- [ ] **Step 1: implement.**

`src/AlienInvasion/Game/RedContaminationVisual.cs`:
```csharp
using System.Collections.Generic;
using AlienInvasion.Core;
using ColossalFramework;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// Places and removes the red decal GameObjects that correspond to the contamination zones.
    /// It touches GameObjects directly, so it must be called from the main thread, in OnUpdate.
    /// </summary>
    public static class RedContaminationVisual
    {
        private static readonly Dictionary<int, GameObject> _decals = new Dictionary<int, GameObject>();

        public static void Sync(List<ContaminationZone> activeZones)
        {
            try
            {
                var wanted = new HashSet<int>();
                for (int i = 0; i < activeZones.Count; i++)
                {
                    ContaminationZone zone = activeZones[i];
                    int key = ZoneKey(zone);
                    wanted.Add(key);
                    if (!_decals.ContainsKey(key))
                    {
                        GameObject decal = SpawnDecal(zone);
                        if (decal != null) _decals[key] = decal;
                    }
                }

                var toRemove = new List<int>();
                foreach (var kv in _decals)
                {
                    if (!wanted.Contains(kv.Key)) toRemove.Add(kv.Key);
                }
                for (int i = 0; i < toRemove.Count; i++)
                {
                    DestroyDecal(toRemove[i]);
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("RedContaminationVisual.Sync error: " + e);
            }
        }

        public static void Clear()
        {
            var keys = new List<int>(_decals.Keys);
            for (int i = 0; i < keys.Count; i++) DestroyDecal(keys[i]);
        }

        private static GameObject SpawnDecal(ContaminationZone zone)
        {
            GameObject prefab = AssetLoader.GetPrefab(ModConfig.RedDecalPrefabName);
            if (prefab == null) return null;

            float y = Singleton<TerrainManager>.instance.SampleDetailHeight(new Vector3(zone.CenterX, 0f, zone.CenterZ));
            GameObject instance = Object.Instantiate(prefab);
            instance.transform.position = new Vector3(zone.CenterX, y + ModConfig.RedDecalYOffset, zone.CenterZ);
            instance.transform.localScale = new Vector3(zone.Radius * 2f, 1f, zone.Radius * 2f);
            return instance;
        }

        private static void DestroyDecal(int key)
        {
            GameObject go;
            if (_decals.TryGetValue(key, out go))
            {
                if (go != null) Object.Destroy(go);
                _decals.Remove(key);
            }
        }

        private static int ZoneKey(ContaminationZone zone)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + zone.CenterX.GetHashCode();
                hash = hash * 31 + zone.CenterZ.GetHashCode();
                hash = hash * 31 + zone.StartTicks.GetHashCode();
                return hash;
            }
        }
    }
}
```
Note that `TerrainManager.SampleDetailHeight` is the standard API for the terrain height. A zone's unique key is a hash of `(CenterX, CenterZ, StartTicks)` - value-based rather than by index, so the same zone keeps the same key even when `ReplaceAll` or the like moves it within the list.

- [ ] **Step 2: verify the build.**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: the build succeeds.

- [ ] **Step 3: commit.**

```bash
git add src/AlienInvasion/Game/RedContaminationVisual.cs
git commit -m "feat: add RedContaminationVisual, placing and removing the red decals"
```

---

## Task 10: Effects (the lightning bolts, the flash and the thunder)

**Files:**
- Create: `src/AlienInvasion/Game/Effects.cs`

**Interfaces:**
- Consumes: `ModConfig`
- Produces, on `static class Effects`:
  - `void PlayLightningStrike(Vector3 groundPoint, Vector3 skyPoint)` - **main thread only**. Shows a jagged `LineRenderer` bolt between `groundPoint` and `skyPoint` for a moment, plays the borrowed meteor impact effect where it lands, and plays `RainProperties.m_ThunderSound`.
  - The temporary GameObject it creates for the bolt is destroyed afterwards with `Object.Destroy(go, lifetime)`.

- [ ] **Step 1: implement.**

`src/AlienInvasion/Game/Effects.cs`:
```csharp
using ColossalFramework;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>Plays the lightning bolt, the impact flash and the thunder. All of it must be called from the main thread, in OnUpdate.</summary>
    public static class Effects
    {
        private const float BoltLifetime = 0.15f;
        private static Material _boltMaterial;
        private static RainProperties _rainProperties;
        private static bool _rainPropertiesSearched;

        public static void PlayLightningStrike(Vector3 groundPoint, Vector3 skyPoint)
        {
            try
            {
                SpawnBolt(groundPoint, skyPoint);
                PlayImpactBurst(groundPoint);
                PlayThunderSound(groundPoint);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("PlayLightningStrike error: " + e);
            }
        }

        private static void SpawnBolt(Vector3 groundPoint, Vector3 skyPoint)
        {
            if (_boltMaterial == null)
            {
                Shader shader = Shader.Find("Particles/Additive");
                if (shader != null) _boltMaterial = new Material(shader);
            }

            var go = new GameObject("AlienInvasion_LightningBolt");
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            if (_boltMaterial != null) line.material = _boltMaterial;
            line.startWidth = 4f;
            line.endWidth = 1.5f;
            line.startColor = new Color(0.8f, 0.9f, 1f, 1f);
            line.endColor = new Color(0.8f, 0.9f, 1f, 0.6f);

            const int segments = 6;
            line.positionCount = segments + 1;
            Vector3 dir = (groundPoint - skyPoint);
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector3 basePos = skyPoint + dir * t;
                if (i != 0 && i != segments)
                {
                    basePos.x += Random.Range(-8f, 8f);
                    basePos.z += Random.Range(-8f, 8f);
                }
                line.SetPosition(i, basePos);
            }

            Object.Destroy(go, BoltLifetime);
        }

        private static void PlayImpactBurst(Vector3 position)
        {
            EffectInfo effect = ResolveMeteorImpactEffect();
            if (effect == null) return;
            var spawnArea = new EffectInfo.SpawnArea(position, Vector3.up, 0f);
            Singleton<EffectManager>.instance.DispatchEffect(
                effect, default(InstanceID), spawnArea, Vector3.zero, 0f, 0.5f,
                Singleton<VehicleManager>.instance.m_audioGroup);
        }

        private static EffectInfo ResolveMeteorImpactEffect()
        {
            int count = PrefabCollection<VehicleInfo>.LoadedCount();
            for (int i = 0; i < count; i++)
            {
                VehicleInfo info = PrefabCollection<VehicleInfo>.GetLoaded((uint)i);
                if (info == null) continue;
                MeteorAI ai = info.m_vehicleAI as MeteorAI;
                if (ai != null && ai.m_impactEffect != null) return ai.m_impactEffect;
            }
            return null;
        }

        private static void PlayThunderSound(Vector3 position)
        {
            if (!_rainPropertiesSearched)
            {
                _rainPropertiesSearched = true;
                _rainProperties = Object.FindObjectOfType<RainProperties>();
            }
            if (_rainProperties == null || _rainProperties.m_ThunderSound == null) return;
            Singleton<AudioManager>.instance.AddEvent(
                Singleton<AudioManager>.instance.AmbientGroup, _rainProperties.m_ThunderSound,
                position, Vector3.zero, 200f, 1f, 1f);
        }
    }
}
```

- [ ] **Step 2: verify the build.**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: the build succeeds.

- [ ] **Step 3: commit.**

```bash
git add src/AlienInvasion/Game/Effects.cs
git commit -m "feat: add Effects, playing the lightning bolt, the flash and the thunder"
```

---

## Task 11: Mothership and InvasionManager (the state machine that runs it all)

**Files:**
- Create: `src/AlienInvasion/Game/Mothership.cs`
- Create: `src/AlienInvasion/Game/InvasionManager.cs`

**Interfaces:**
- Consumes `AssetLoader`, `Effects`, `ContaminationManager`, plus `InvasionState`, `MovementMath` and `ContaminationZone` from Core, and `ModConfig`
- Produces:
  - `class Mothership`: the constructor `Mothership(Vector3 targetPosition)`, `void SetAltitude(float altitudeAboveTarget)` which updates the GameObject's position on the main thread, `Vector3 SkyPointForBolt()`, `void Destroy()` and the `Vector3 Position` property.
  - `static class InvasionManager`:
    - `bool IsActive { get; }`
    - `void StartInvasion(Vector3 targetPosition)` - accepted only while idle.
    - `void UpdateVisual(float realTimeDelta)` - **main thread only**. Interpolates the mothership's position, advances the phase timers and decides the phase transitions from Descending through Bombarding and Ascending to Done.
    - `void UpdateSimulation()` - **simulation thread only**. Grows the crater while the state is `Bombarding`, and **exactly once**, right after the transition from `Bombarding` to `Ascending`, destroys the buildings and registers the contamination zone.

- [ ] **Step 1: implement Mothership.**

`src/AlienInvasion/Game/Mothership.cs`:
```csharp
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>The mothership's GameObject and position. Everything touching the GameObject must be called from the main thread, in OnUpdate.</summary>
    public class Mothership
    {
        private GameObject _gameObject;
        private readonly Vector3 _targetGround;

        public Vector3 Position { get; private set; }

        public Mothership(Vector3 targetGround)
        {
            _targetGround = targetGround;
            Position = targetGround + new Vector3(0f, ModConfig.MothershipStartAltitude, 0f);
            GameObject prefab = AssetLoader.GetPrefab(ModConfig.MothershipPrefabName);
            if (prefab != null)
            {
                _gameObject = Object.Instantiate(prefab);
                _gameObject.transform.position = Position;
            }
        }

        public void SetAltitude(float altitudeAboveTarget)
        {
            Position = _targetGround + new Vector3(0f, altitudeAboveTarget, 0f);
            if (_gameObject != null) _gameObject.transform.position = Position;
        }

        public Vector3 SkyPointForBolt()
        {
            return Position;
        }

        public void Destroy()
        {
            if (_gameObject != null)
            {
                Object.Destroy(_gameObject);
                _gameObject = null;
            }
        }
    }
}
```

- [ ] **Step 2: implement InvasionManager.**

`src/AlienInvasion/Game/InvasionManager.cs`:
```csharp
using AlienInvasion.Core;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// Runs a single invasion from start to finish.
    /// UpdateVisual is main thread only: it touches GameObjects and is the sole writer of the
    /// phase timers and the state transitions.
    /// UpdateSimulation is simulation thread only: DisasterHelpers and writing the contamination.
    /// InvasionState and the phase timers are written from UpdateVisual alone, so there is one
    /// writer. UpdateSimulation only reads the state; it never writes it.
    /// </summary>
    public static class InvasionManager
    {
        private static InvasionState _state = InvasionState.Idle;
        private static Mothership _ship;
        private static Vector3 _target;
        private static float _phaseElapsed;
        private static float _strikeTimer;
        private static float _craterProgress; // 0..1
        private static bool _bombardResolved;  // whether the destruction and contamination at the end of Bombarding are done

        public static bool IsActive
        {
            get { return _state != InvasionState.Idle; }
        }

        public static void StartInvasion(Vector3 targetPosition)
        {
            if (_state != InvasionState.Idle) return;
            _target = targetPosition;
            _ship = new Mothership(targetPosition);
            _state = InvasionState.Descending;
            _phaseElapsed = 0f;
            _strikeTimer = 0f;
            _craterProgress = 0f;
            _bombardResolved = false;
            ModConfig.Log("Invasion started at " + targetPosition);
        }

        public static void UpdateVisual(float realTimeDelta)
        {
            if (_state == InvasionState.Idle) return;
            try
            {
                if (_state == InvasionState.Done)
                {
                    _state = InvasionState.Idle;
                    return;
                }

                _phaseElapsed += realTimeDelta;

                switch (_state)
                {
                    case InvasionState.Descending:
                        UpdateDescending();
                        break;
                    case InvasionState.Bombarding:
                        UpdateBombarding(realTimeDelta);
                        break;
                    case InvasionState.Ascending:
                        UpdateAscending();
                        break;
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("UpdateVisual error: " + e);
            }
        }

        private static void UpdateDescending()
        {
            float t = _phaseElapsed / ModConfig.DescendSeconds;
            float eased = MovementMath.EaseInOut(t);
            float altitude = MovementMath.Lerp(ModConfig.MothershipStartAltitude, ModConfig.MothershipHoverAltitude, eased);
            _ship.SetAltitude(altitude);
            if (t >= 1f)
            {
                _state = InvasionStateMachine.Next(_state);
                _phaseElapsed = 0f;
            }
        }

        private static void UpdateBombarding(float realTimeDelta)
        {
            _ship.SetAltitude(ModConfig.MothershipHoverAltitude);
            _strikeTimer += realTimeDelta;
            if (_strikeTimer >= ModConfig.StrikeIntervalSeconds)
            {
                _strikeTimer = 0f;
                Vector3 groundPoint = _target + new Vector3(
                    Random.Range(-ModConfig.StrikeScatterRadius, ModConfig.StrikeScatterRadius),
                    0f,
                    Random.Range(-ModConfig.StrikeScatterRadius, ModConfig.StrikeScatterRadius));
                Effects.PlayLightningStrike(groundPoint, _ship.SkyPointForBolt());
            }

            float t = _phaseElapsed / ModConfig.BombardSeconds;
            if (t > 1f) t = 1f;
            _craterProgress = t;

            if (_phaseElapsed >= ModConfig.BombardSeconds)
            {
                _state = InvasionStateMachine.Next(_state);
                _phaseElapsed = 0f;
            }
        }

        private static void UpdateAscending()
        {
            float t = _phaseElapsed / ModConfig.AscendSeconds;
            float eased = MovementMath.EaseInOut(t);
            float altitude = MovementMath.Lerp(ModConfig.MothershipHoverAltitude, ModConfig.MothershipStartAltitude, eased);
            _ship.SetAltitude(altitude);
            if (t >= 1f)
            {
                _ship.Destroy();
                _ship = null;
                _state = InvasionStateMachine.Next(_state); // -> Done
            }
        }

        /// <summary>Called every tick from the simulation thread. DisasterHelpers and the contamination writes happen here and nowhere else.</summary>
        public static void UpdateSimulation()
        {
            try
            {
                if (_state == InvasionState.Bombarding && _craterProgress > 0f)
                {
                    float radius = ModConfig.CraterRadiusMax * _craterProgress;
                    float depth = ModConfig.CraterDepthMax * _craterProgress;
                    DisasterHelpers.MakeCrater(new Vector2(_target.x, _target.z), radius, depth, true);
                }
                else if (_state == InvasionState.Ascending && !_bombardResolved)
                {
                    _bombardResolved = true;
                    ResolveBombardDamage();
                }
                else if (_state == InvasionState.Idle)
                {
                    _bombardResolved = false;
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("UpdateSimulation error: " + e);
            }
        }

        private static void ResolveBombardDamage()
        {
            int seed = (int)SimulationManager.instance.m_randomizer.Int32(1000000u);
            // preRadius must equal totalRadius - the known trap is that 0 destroys nothing at all.
            DisasterHelpers.DestroyStuff(seed, null, _target, ModConfig.DestructionRadius, ModConfig.DestructionRadius, 0f,
                ModConfig.DestructionRadius * 0.5f, ModConfig.DestructionRadius, ModConfig.DestructionRadius * 0.3f, ModConfig.DestructionRadius * 0.6f);

            long startTicks = SimulationManager.instance.m_currentGameTime.Ticks;
            var zone = new ContaminationZone(_target.x, _target.z, ModConfig.ContaminationRadius, startTicks);
            ContaminationManager.AddZone(zone);
            ModConfig.Log("Bombardment resolved: crater+destruction+contamination at " + _target);
        }
    }
}
```
Note that `UpdateVisual` makes the `Done` to `Idle` transition on a single `Idle` frame so `IsActive` returns to `false` correctly. The Next chain already runs `Done` to `Idle`, but the one step immediately after reaching Done has to go through this main-thread transition.

- [ ] **Step 3: verify the build.**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: the build succeeds.

- [ ] **Step 4: commit.**

```bash
git add src/AlienInvasion/Game/Mothership.cs src/AlienInvasion/Game/InvasionManager.cs
git commit -m "feat: add Mothership and InvasionManager, the state machine"
```

---

## Task 12: InvasionThreadingExtension (triggering, driving it each tick, and the contamination upkeep and expiry)

**Files:**
- Create: `src/AlienInvasion/Game/Simulation/InvasionThreadingExtension.cs`

**Interfaces:**
- Consumes `InvasionManager`, `ContaminationManager`, `RedContaminationVisual`, `ExpiryClock` from Core, and `ModConfig`
- Produces:
  - `class InvasionThreadingExtension : ThreadingExtensionBase`, found by the game on its own.
    - `OnUpdate(float realTimeDelta, float simulationTimeDelta)`, main thread: watches for the manual key and calls `InvasionManager.StartInvasion`, then `InvasionManager.UpdateVisual` and `RedContaminationVisual.Sync`.
    - `OnAfterSimulationTick()`, simulation thread: `InvasionManager.UpdateSimulation`, the random trigger roll, and the contamination zones' upkeep and expiry.

- [ ] **Step 1: implement.**

`src/AlienInvasion/Game/Simulation/InvasionThreadingExtension.cs`:
```csharp
using System.Collections.Generic;
using AlienInvasion.Core;
using ICities;
using UnityEngine;

namespace AlienInvasion.Game.Simulation
{
    /// <summary>
    /// Drives the invasion: triggering it, running it, and keeping the contamination in place.
    /// OnUpdate is the main thread, for GameObjects and input; OnAfterSimulationTick is the
    /// simulation thread, for DisasterHelpers and the contamination.
    /// </summary>
    public class InvasionThreadingExtension : ThreadingExtensionBase
    {
        private int _pollutionTickCounter;
        private int _randomCheckTickCounter;
        private const int PollutionProcessInterval = 16;

        public override void OnUpdate(float realTimeDelta, float simulationTimeDelta)
        {
            try
            {
                if (Input.GetKeyDown(ModConfig.ManualTriggerKey) && !InvasionManager.IsActive)
                {
                    Vector3 target = PickManualTargetPosition();
                    InvasionManager.StartInvasion(target);
                }

                InvasionManager.UpdateVisual(realTimeDelta);
                RedContaminationVisual.Sync(ContaminationManager.Zones);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("OnUpdate error: " + e);
            }
        }

        public override void OnAfterSimulationTick()
        {
            try
            {
                InvasionManager.UpdateSimulation();
                MaybeRollRandomInvasion();
                ProcessContaminationZones();
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("OnAfterSimulationTick error: " + e);
            }
        }

        private static Vector3 PickManualTargetPosition()
        {
            // A simple approach: aim at the ground position at the centre of the camera.
            Vector3 camPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            return new Vector3(camPos.x, 0f, camPos.z);
        }

        private void MaybeRollRandomInvasion()
        {
            if (InvasionManager.IsActive) return;
            _randomCheckTickCounter++;
            if (_randomCheckTickCounter < ModConfig.RandomCheckIntervalTicks) return;
            _randomCheckTickCounter = 0;

            int roll = (int)SimulationManager.instance.m_randomizer.Int32(10000u);
            if (roll >= ModConfig.RandomChancePer10000) return;

            const float half = 8500f; // roughly the extent of the map
            float x = (float)SimulationManager.instance.m_randomizer.Int32(0, (uint)(half * 2)) - half;
            float z = (float)SimulationManager.instance.m_randomizer.Int32(0, (uint)(half * 2)) - half;
            InvasionManager.StartInvasion(new Vector3(x, 0f, z));
            ModConfig.Log("Random invasion triggered at (" + x + ", " + z + ")");
        }

        private void ProcessContaminationZones()
        {
            if (++_pollutionTickCounter < PollutionProcessInterval) return;
            _pollutionTickCounter = 0;

            List<ContaminationZone> zones = ContaminationManager.Zones;
            if (zones.Count == 0) return;

            long nowTicks = SimulationManager.instance.m_currentGameTime.Ticks;
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                ContaminationZone zone = zones[i];
                if (ExpiryClock.HasExpired(zone.StartTicks, nowTicks, ModConfig.ExpiryYears))
                {
                    ContaminationManager.ClearZone(zone);
                    ContaminationManager.RemoveZoneAt(i);
                    ModConfig.Log("contamination zone expired (" + ModConfig.ExpiryYears + "y) and cleared");
                    continue;
                }
                ContaminationManager.ReassertZone(zone);
            }
        }
    }
}
```

- [ ] **Step 2: verify the build.**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: the build succeeds.

- [ ] **Step 3: commit.**

```bash
git add src/AlienInvasion/Game/Simulation/InvasionThreadingExtension.cs
git commit -m "feat: add InvasionThreadingExtension - triggering, the per-tick drive and the contamination upkeep"
```

---

## Task 13: InvasionDataExtension (persisting across save and load)

**Files:**
- Create: `src/AlienInvasion/Game/Serialization/InvasionDataExtension.cs`

**Interfaces:**
- Consumes `ContaminationManager` and `ZoneSerializer` from Core, plus `ModConfig`
- Produces:
  - `class InvasionDataExtension : SerializableDataExtensionBase` with `OnSaveData()` and `OnLoadData()`, under the data key `"AlienInvasion.Contamination.v1"`. Found by the game on its own.

- [ ] **Step 1: implement.**

`src/AlienInvasion/Game/Serialization/InvasionDataExtension.cs`:
```csharp
using System.Collections.Generic;
using AlienInvasion.Core;
using ICities;

namespace AlienInvasion.Game.Serialization
{
    /// <summary>Persists the contamination zone ledger into the save game. Discovered by the game.</summary>
    public class InvasionDataExtension : SerializableDataExtensionBase
    {
        private const string DataId = "AlienInvasion.Contamination.v1";

        public override void OnSaveData()
        {
            try
            {
                List<ContaminationZone> zones = ContaminationManager.Zones;
                byte[] bytes = ZoneSerializer.Serialize(zones);
                serializableDataManager.SaveData(DataId, bytes);
                ModConfig.Log("saved " + zones.Count + " zone(s)");
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("save error: " + e);
            }
        }

        public override void OnLoadData()
        {
            try
            {
                byte[] bytes = serializableDataManager.LoadData(DataId);
                List<ContaminationZone> zones = ZoneSerializer.Deserialize(bytes);
                ContaminationManager.ReplaceAll(zones);
                ModConfig.Log("loaded " + zones.Count + " zone(s)");
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("load error: " + e);
            }
        }
    }
}
```

- [ ] **Step 2: verify the build.**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: the build succeeds.

- [ ] **Step 3: commit.**

```bash
git add src/AlienInvasion/Game/Serialization/InvasionDataExtension.cs
git commit -m "feat: persist the contamination zones across save and load"
```

---

## Task 14: the AssetBundle build pipeline (a Unity Editor script) and the README

This task lays the groundwork for the user to actually build `alieninvasion.bundle` from `models/source/models.blend`. The Unity Editor cannot be run from here, so what this produces is **a written procedure plus the Editor script**.

**Files:**
- Create: `unity-project/Assets/Editor/BuildAssetBundles.cs`
- Create: `src/AlienInvasion/README.md`

**Interfaces:**
- Consumes: nothing
- Produces nothing but tooling and documentation

- [ ] **Step 1: create BuildAssetBundles.cs.**

`unity-project/Assets/Editor/BuildAssetBundles.cs`:
```csharp
using UnityEditor;
using System.IO;

public static class BuildAssetBundles
{
    [MenuItem("AlienInvasion/Build AssetBundle")]
    public static void Build()
    {
        string outDir = "AssetBundles";
        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
        BuildPipeline.BuildAssetBundles(outDir, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);
    }
}
```

- [ ] **Step 2: write the AssetBundle procedure into the README.**

`src/AlienInvasion/README.md`:
```markdown
# Alien Invasion (Cities: Skylines Mod)

A UFO mothership descends, hammers the ground with lightning to form a crater, destroys the buildings around it and leaves red radioactive contamination for an in-game year. Trigger it with **F7**, which `ManualTriggerKey` in `Game/ModConfig.cs` can change. It also happens at random, rarely.

## Building the AssetBundle (from the Blender model to a form the game can use)

1. Install **Unity Editor 5.6.6f2** from the [Unity Archive](https://unity3d.com/get-unity/download/archive). It is compatible with the Unity 5.6.7 engine Cities: Skylines runs on.
2. Open `models/source/models.blend` in Blender and **export each as FBX**: `MotherShip`, `TriPod` (unused in this phase) and the plane for the red decal.
   - `MotherShip`'s pivot is at its geometric centre; that has been checked and needs no change.
   - Put the decal object's pivot at its centre, on the face that meets the ground.
   - Apply each object's scale with `Ctrl+A` then Scale before exporting; an unapplied scale makes the behaviour after the FBX import hard to predict.
3. Open `unity-project` in Unity Editor 5.6.6f2.
4. Import the exported FBX files into `unity-project/Assets/` and set up the materials and textures: `_d` for colour, `_n` for the normal map, `_s` for specular, `_i` for emission and `_a` for transparency.
5. Turn each model into a prefab and name them **exactly** `Mothership` and `ContaminationDecal`, matching `MothershipPrefabName` and `RedDecalPrefabName` in `Game/ModConfig.cs`.
6. Set each prefab's **AssetBundle name** to `alieninvasion` in the Inspector - select the prefab, then use the AssetBundle dropdown at the bottom right.
7. Run **AlienInvasion -> Build AssetBundle** from the Unity menu.
8. That produces `unity-project/AssetBundles/alieninvasion` with no extension. Rename it to **`alieninvasion.bundle`** and put it at `src/AlienInvasion/Assets/alieninvasion.bundle`.
9. Run `build.ps1` again and it is deployed into the mod folder.

Without the AssetBundle the mod still builds and starts; only the mothership and decal visuals are skipped, and the log says `AssetBundle not found`.

## Building and deploying
```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```
It deploys to `%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\AlienInvasion\`.

## Verifying it in game
1. Enable "Alien Invasion" under Mods in the Content Manager.
2. Press **F7** in game, or wait and watch for the random trigger.
3. The mothership descends, the lightning hammers down and the crater forms, the buildings around it are destroyed, and it ascends and vanishes.
4. Confirm the red contamination is left behind. Without the AssetBundle in place, only the standard ground pollution shows.
5. Confirm the contamination lifts on its own after an in-game year.
6. Save and reload, and confirm the contamination is still there.

## Settings
The constants live in `Game/ModConfig.cs`: the trigger key, the probabilities, the radii, the durations and so on.

## Logs
Search for `[AlienInvasion]` in the output log under `%LOCALAPPDATA%\Colossal Order\Cities_Skylines\`.
```

- [ ] **Step 3: commit.**

```bash
git add unity-project/Assets/Editor/BuildAssetBundles.cs src/AlienInvasion/README.md
git commit -m "docs: add the AssetBundle build pipeline and the README"
```

---

## Task 15: the final build, the full test run, and asking for in-game verification

**Files:** none; this is verification only

- [ ] **Step 1: run every Core test.**

Run: `dotnet test tests/AlienInvasion.Core.Tests`
Expected: every test passes - roughly 28 across InvasionState, GridMath, ExpiryClock, ZoneSerializer and MovementMath.

- [ ] **Step 2: the final build and deployment of the mod.**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: the build succeeds and `AlienInvasion.dll` is deployed into the mod folder. Without the AssetBundle a warning is logged, which is normal; the user runs the Task 14 pipeline separately.

- [ ] **Step 3: ask the user to verify it in game.**

Ask the user to work through steps 1 to 6 of the README's in-game verification. Without the AssetBundle yet, pressing F7 and seeing whether the crater and the destruction happen is enough to confirm the logic; the mothership and the red decals can be checked once the AssetBundle is done.

- [ ] **Step 4: the final commit.**

```bash
git add -A
git commit -m "chore: final build and test verification"
```

---

## Self-Review

**1. Spec coverage, against the design document:**
- Triggering, both manual and random: Task 12, `InvasionThreadingExtension`, with the F7 key and the random roll ✅
- The mothership descending, the lightning, the crater forming and it ascending away: Task 11, `Mothership` and `InvasionManager` ✅
- The lightning effect, built from scratch because the game has none: Task 10, `Effects`, using a LineRenderer plus the borrowed meteor flash and thunder. Decompiling confirmed no such effect exists ✅
- Destroying buildings: Task 11, `ResolveBombardDamage`, using `DisasterHelpers.DestroyStuff` with the preRadius trap avoided ✅
- The red contamination on the ground and as an effect, lifting after a year, with no decontamination facility: Task 8 `ContaminationManager`, which only handles the year, plus Task 9 `RedContaminationVisual` for the decals ✅
- Shipping the AssetBundle: Task 7 `AssetLoader` plus the Task 14 pipeline ✅
- Tunable through ModConfig: every constant gathered into Task 6's `ModConfig` ✅
- Save and load: Task 13, `InvasionDataExtension` ✅
- Thread safety, with the main and simulation threads kept apart: stated in the global constraints and in each task ✅
- The tripods, Phase 2: out of scope here and covered by a separate plan, as the design document says ✅

**2. Placeholder scan:** no vague wording such as "TBD" or "later". Task 14 is something the user carries out, because the Unity Editor cannot be run from here and it is a written procedure by nature - that is the design, not something deferred, and not a placeholder.

**3. Type consistency:**
- `ContaminationZone(float,float,float,long)` - defined in Task 1 and used consistently in Tasks 4, 8, 9, 11, 12 and 13.
- `GridMath.CellsInRadius(float,float,float)` returning `List<int>` - defined in Task 2 and used consistently in Task 8.
- `ExpiryClock.HasExpired(long,long,int)` - defined in Task 3 and used consistently in Task 12.
- `ZoneSerializer.Serialize` and `Deserialize` - defined in Task 4 and used consistently in Task 13.
- `MovementMath.EaseInOut`, `Lerp` and `IsNear` - defined in Task 5, with `EaseInOut` and `Lerp` used in Task 11. `IsNear` is public API prepared for Phase 2 and is covered by tests, so it does not count as dead code.
- `InvasionManager.StartInvasion(Vector3)`, `IsActive`, `UpdateVisual(float)` and `UpdateSimulation()` - defined in Task 11 and used consistently in Task 12.
- `Mothership.SetAltitude(float)`, `SkyPointForBolt()`, `Destroy()` and `Position` - defined and used consistently within Task 11. The Step 2 revision changed the constructor to assign the position directly rather than calling `SetAltitude` from within it, and settled on the `Position` property alone for the arrival test.
- `AssetLoader.Initialize(string)`, `GetPrefab(string)` and `IsAvailable` - defined in Task 7, with `GetPrefab` used under the same name in Tasks 9 and 11. `IsAvailable` is public API kept for future branching.
</parameter>
