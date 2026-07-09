# Alien Invasion Mod — Phase 1 (Mothership) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cities: Skylines（初代）に、手動（キーバインド）またはランダムで発動するUFO母船襲来（降下→雷連打→クレーター形成＋周辺建物破壊→上昇消滅→赤い放射能汚染をゲーム内1年残す）を実装する。

**Architecture:** Unity/ゲーム型に依存しない純粋ロジック（状態遷移・座標補間・汚染セル計算・期限判定・直列化）を `Core/` に分離してxUnitで実TDD。ゲーム統合層（AssetBundleロード・母船GameObject制御・エフェクト・汚染書込・Harmony不要＝Mod主導）は薄く保つ。**スレッド分離が最重要**: GameObject/Transform/LineRenderer等のUnity Object操作は必ず `OnUpdate`（メイン/描画スレッド）で行い、`DisasterHelpers`/`NaturalResourceManager`（クレーター・建物破壊・汚染書込）は必ず `OnBeforeSimulationTick`/`OnAfterSimulationTick`（シミュレーションスレッド）で行う。この2つを跨ぐ状態は単純な値型フィールド（enum/float/Vector3）とし、各フィールドは常に単一スレッドのみが書き込む（single-writer原則）。

**Tech Stack:** C# / .NET Framework 3.5（Mod本体, MSBuildビルド）, `ICities`/`Assembly-CSharp`/`UnityEngine`/`ColossalManaged` 参照。テストは .NET 8 + xUnit（Coreソースをリンク参照）。3Dアセットは Unity 5.6.6f2 で AssetBundle 化し同梱。

## Global Constraints

- 対象FW（Mod本体）: **.NET Framework 3.5**。`Core/` は net35 と net8 の両方でコンパイルされるため ValueTuple 等の net35 非対応機能を使わない。
- ゲームDLL参照元: `C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed\`（`ICities.dll`, `Assembly-CSharp.dll`, `UnityEngine.dll`, `ColossalManaged.dll`）。参照は `Private=False`。
- **Harmonyは本Modでは不要**（既存メソッドへのパッチが無いため）。ただし `ICities` インターフェース（`IUserMod`, `ThreadingExtensionBase`, `SerializableDataExtensionBase`）はゲームが自動検出する。
- デプロイ先: `%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\AlienInvasion\`。AssetBundle は `Assets\alieninvasion.bundle` として同梱。
- ログは `UnityEngine.Debug.Log` に接頭辞 `"[AlienInvasion] "` を付けてのみ出力。
- 全 tick / GameObject生成 / エフェクト / 直列化処理は try/catch で保護し、例外をゲーム本体へ伝播させない。
- **AssetBundle/prefab が読み込めない場合はログを出して該当演出をスキップ**（ゲームを巻き込まない。テスト時にまだ `.bundle` が無くてもビルド・起動は成功する）。
- 検証済み実API（逆コンパイルで確認済み。以降のタスクで正確に使用する）:
  - `UnityEngine.AssetBundle.LoadFromFile(string path)` → `AssetBundle`（static）
  - `AssetBundle.LoadAsset<T>(string name)` where `T : UnityEngine.Object`
  - `UnityEngine.LineRenderer`: `startWidth`/`endWidth`/`positionCount`/`useWorldSpace`（すべて `extern` プロパティ）, `SetPosition(int, Vector3)`, `SetPositions(Vector3[])`。基底 `Renderer` から `material` プロパティを継承。
  - `UnityEngine.Shader.Find(string name)`（static）。ビルトインシェーダー名 `"Particles/Additive"` を使用。
  - `UnityEngine.Object.FindObjectOfType<T>()`（static）
  - `RainProperties`（`MonoBehaviour`）: `public AudioInfo m_ThunderSound;` フィールド。再生は `Singleton<AudioManager>.instance.AddEvent(Singleton<AudioManager>.instance.AmbientGroup, audioInfo, position, Vector3.zero, 200f, 1f, 1f)`。
  - `DisasterHelpers.MakeCrater(Vector2 position, float radius, float depth, bool raiseEdges)`（static）
  - `DisasterHelpers.DestroyStuff(int seed, InstanceManager.Group group, Vector3 position, float totalRadius, float preRadius, float removeRadius, float destructionRadiusMin, float destructionRadiusMax, float burnRadiusMin, float burnRadiusMax)`（static）。**重要な既知の罠**: `preRadius` は「衝撃波が到達した外周半径」として機能する門番値であり、`preRadius=0` を渡すと**内部の距離判定が常に偽になり何も破壊されない**（Nuclear Meltdown Modで実際に踏んだバグ）。必ず `preRadius = totalRadius` を渡すこと。
  - `NaturalResourceManager.instance.m_naturalResources[index].m_pollution`（public byte、構造体配列でインプレース代入可）、`NaturalResourceManager.instance.AreaModifiedB(minX,minZ,maxX,maxZ)`。グリッド定数: `CellSize=33.75f`, `Resolution=512`, `cell=Clamp((int)(world/33.75f+256f),0,511)`, `index=cellZ*512+cellX`。
  - `MeteorAI.m_impactEffect`（public `EffectInfo`）を `PrefabCollection<VehicleInfo>` 経由で取得可能（`VehicleInfo.m_vehicleAI as MeteorAI`）。爆発の閃光演出に流用。
  - `Singleton<EffectManager>.instance.DispatchEffect(EffectInfo, InstanceID, EffectInfo.SpawnArea, Vector3, float, float, AudioGroup)`。
  - `ThreadingExtensionBase`（namespace `ICities`）: `OnUpdate(float realTimeDelta, float simulationTimeDelta)`（**メイン/描画スレッド**）、`OnBeforeSimulationTick()`/`OnAfterSimulationTick()`（**シミュレーションスレッド**）。
  - `SerializableDataExtensionBase`: `OnSaveData()`/`OnLoadData()`、`serializableDataManager.SaveData(id, byte[])`/`LoadData(id)`。
- Unity バージョン: **5.6.6f2**（`Cities.exe` 実体は5.6.7ビルド、コミュニティ標準の5.6.6と相互互換）でAssetBundleをビルドする。
- 汚染半径・時間・確率・キー等はすべて `ModConfig` の定数として一箇所にまとめ、調整可能にする（ユーザー要件）。
- **3Dモデル確認済み事項**（Blender MCP接続で直接検証）: `MotherShip` は幾何中心ピボットで正しい。`TriPod` は当初ピボットが頭部付近にあり接地点になっていなかったため、原点を脚の最下点(バウンディングボックス最下部の中心)へ移動済み（メッシュ自体は移動していない）。両モデルとも現時点でマテリアル未割当（0個）。`MotherShip` はZ軸スケール×0.1、`TriPod` はY軸スケール×1.5が未適用のまま残っている（FBXエクスポート前に適用推奨）。これらはAssetBundle制作(Task 14)側の作業であり、本プランのC#実装をブロックしない。

---

## File Structure

```
エイリアン襲来プロジェクト/
├─ AlienInvasion.sln
├─ build.ps1
├─ models/source/                         # Blenderソース(MotherShip.stl, TriPod.stl, models.blend)
├─ unity-project/                          # AssetBundleビルド用Unityプロジェクト(Task 14で作成)
│  └─ Assets/Editor/BuildAssetBundles.cs
├─ src/AlienInvasion/
│  ├─ AlienInvasion.csproj
│  ├─ Properties/AssemblyInfo.cs
│  ├─ Assets/alieninvasion.bundle          # (ユーザーがUnityでビルドして配置)
│  ├─ Core/                                 # Unity非依存・テスト対象
│  │   ├─ InvasionState.cs                  # 状態enum＋遷移判定
│  │   ├─ ContaminationZone.cs              # struct(中心/半径/開始時刻)
│  │   ├─ GridMath.cs                       # 座標変換＋半径セル列挙
│  │   ├─ ExpiryClock.cs                    # N年経過判定
│  │   ├─ ZoneSerializer.cs                 # ゾーン台帳の直列化
│  │   └─ MovementMath.cs                   # 高度補間・イージング
│  ├─ Game/
│  │   ├─ Mod.cs                            # IUserMod
│  │   ├─ ModConfig.cs                      # 全定数
│  │   ├─ AssetLoader.cs                    # AssetBundleロード
│  │   ├─ PollutionField.cs                 # NaturalResourceManager書込
│  │   ├─ ContaminationManager.cs           # 汚染ゾーン台帳
│  │   ├─ RedContaminationVisual.cs         # 赤デカール配置/撤去
│  │   ├─ Effects.cs                        # 雷ボルト・閃光・雷鳴
│  │   ├─ InvasionManager.cs                # 状態機械の統括
│  │   ├─ Mothership.cs                     # 母船GameObject制御
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

**依存の向き:** `Game/* → Core/*`（一方向）。`Core/*` は他に依存しない。

---

## Task 1: Core — InvasionState と ContaminationZone

**Files:**
- Create: `src/AlienInvasion/Core/InvasionState.cs`
- Create: `src/AlienInvasion/Core/ContaminationZone.cs`
- Create: `tests/AlienInvasion.Core.Tests/AlienInvasion.Core.Tests.csproj`
- Create: `tests/AlienInvasion.Core.Tests/InvasionStateTests.cs`

**Interfaces:**
- Consumes: なし
- Produces:
  - `enum InvasionState { Idle, Descending, Bombarding, Ascending, Done }`（namespace `AlienInvasion.Core`）
  - `static class InvasionStateMachine { static bool CanTransition(InvasionState from, InvasionState to); static InvasionState Next(InvasionState current); }` — 許可される遷移は `Idle→Descending→Bombarding→Ascending→Done→Idle` の一方向のみ。
  - `struct ContaminationZone { public float CenterX; public float CenterZ; public float Radius; public long StartTicks; public ContaminationZone(float centerX, float centerZ, float radius, long startTicks); }`

- [ ] **Step 1: テストプロジェクトを作成**

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

- [ ] **Step 2: 失敗するテストを書く**

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

- [ ] **Step 3: テスト実行して失敗を確認**

Run: `dotnet test tests/AlienInvasion.Core.Tests`
Expected: FAIL（`InvasionState`/`InvasionStateMachine`/`ContaminationZone` が未定義でコンパイルエラー）

- [ ] **Step 4: 実装**

`src/AlienInvasion/Core/InvasionState.cs`:
```csharp
namespace AlienInvasion.Core
{
    /// <summary>1回の襲来イベントの進行状態。Idle→Descending→Bombarding→Ascending→Done→Idle の一方向循環。</summary>
    public enum InvasionState
    {
        Idle,
        Descending,
        Bombarding,
        Ascending,
        Done
    }

    /// <summary>InvasionState の許可された遷移のみを通す状態機械ロジック。</summary>
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
    /// <summary>ワールド座標中心・半径(m)・発生ゲーム内時刻(DateTime.Ticks)の汚染ゾーン。</summary>
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

- [ ] **Step 5: テスト実行して成功を確認**

Run: `dotnet test tests/AlienInvasion.Core.Tests`
Expected: PASS（全件）

- [ ] **Step 6: コミット**

```bash
git add src/AlienInvasion/Core/InvasionState.cs src/AlienInvasion/Core/ContaminationZone.cs tests/AlienInvasion.Core.Tests
git commit -m "feat: InvasionState状態機械とContaminationZoneを追加"
```

---

## Task 2: Core — GridMath（座標変換と半径セル列挙）

**Files:**
- Create: `src/AlienInvasion/Core/GridMath.cs`
- Test: `tests/AlienInvasion.Core.Tests/GridMathTests.cs`

**Interfaces:**
- Consumes: なし
- Produces（`static class GridMath`, namespace `AlienInvasion.Core`）:
  - `const float CellSize = 33.75f;`
  - `const int Resolution = 512;`
  - `int WorldToCell(float world)` → `Clamp((int)(world/33.75f+256f), 0, 511)`
  - `int CellIndex(int cellX, int cellZ)` → `cellZ*512+cellX`
  - `System.Collections.Generic.List<int> CellsInRadius(float centerX, float centerZ, float radiusMeters)` — 半径内の全セルindexを重複なく列挙（円判定はセル中心のワールド距離）。

- [ ] **Step 1: 失敗するテストを書く**

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

- [ ] **Step 2: テスト実行して失敗を確認**

Run: `dotnet test tests/AlienInvasion.Core.Tests`
Expected: FAIL（`GridMath` 未定義）

- [ ] **Step 3: 実装**

`src/AlienInvasion/Core/GridMath.cs`:
```csharp
using System.Collections.Generic;

namespace AlienInvasion.Core
{
    /// <summary>NaturalResourceManager の汚染グリッド(512x512, セル33.75m)に対する座標計算。</summary>
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

- [ ] **Step 4: テスト実行して成功を確認**

Run: `dotnet test tests/AlienInvasion.Core.Tests`
Expected: PASS（全件）

- [ ] **Step 5: コミット**

```bash
git add src/AlienInvasion/Core/GridMath.cs tests/AlienInvasion.Core.Tests/GridMathTests.cs
git commit -m "feat: GridMath 座標変換と半径セル列挙を追加"
```

---

## Task 3: Core — ExpiryClock（N年経過判定）

**Files:**
- Create: `src/AlienInvasion/Core/ExpiryClock.cs`
- Test: `tests/AlienInvasion.Core.Tests/ExpiryClockTests.cs`

**Interfaces:**
- Consumes: なし
- Produces（`static class ExpiryClock`, namespace `AlienInvasion.Core`）:
  - `bool HasExpired(long startTicks, long nowTicks, int years)` — `now >= start.AddYears(years)`。

- [ ] **Step 1: 失敗するテストを書く**

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

- [ ] **Step 2: テスト実行して失敗を確認**

Run: `dotnet test tests/AlienInvasion.Core.Tests`
Expected: FAIL（`ExpiryClock` 未定義）

- [ ] **Step 3: 実装**

`src/AlienInvasion/Core/ExpiryClock.cs`:
```csharp
using System;

namespace AlienInvasion.Core
{
    /// <summary>汚染ゾーンの時間経過による消滅判定（ゲーム内時刻ベース）。</summary>
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

- [ ] **Step 4: テスト実行して成功を確認**

Run: `dotnet test tests/AlienInvasion.Core.Tests`
Expected: PASS（全件）

- [ ] **Step 5: コミット**

```bash
git add src/AlienInvasion/Core/ExpiryClock.cs tests/AlienInvasion.Core.Tests/ExpiryClockTests.cs
git commit -m "feat: ExpiryClock N年経過判定を追加"
```

---

## Task 4: Core — ZoneSerializer（ゾーン台帳の直列化）

**Files:**
- Create: `src/AlienInvasion/Core/ZoneSerializer.cs`
- Test: `tests/AlienInvasion.Core.Tests/ZoneSerializerTests.cs`

**Interfaces:**
- Consumes: `ContaminationZone`（Task 1）
- Produces（`static class ZoneSerializer`, namespace `AlienInvasion.Core`）:
  - `const byte Version = 1;`
  - `byte[] Serialize(List<ContaminationZone> zones)`
  - `List<ContaminationZone> Deserialize(byte[] data)` — null/短すぎる/未知バージョン/破損時は空リストを返し、例外を投げない。

- [ ] **Step 1: 失敗するテストを書く**

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

- [ ] **Step 2: テスト実行して失敗を確認**

Run: `dotnet test tests/AlienInvasion.Core.Tests`
Expected: FAIL（`ZoneSerializer` 未定義）

- [ ] **Step 3: 実装**

`src/AlienInvasion/Core/ZoneSerializer.cs`:
```csharp
using System.Collections.Generic;
using System.IO;

namespace AlienInvasion.Core
{
    /// <summary>汚染ゾーン台帳を byte[] に直列化/復元（セーブデータ保存用）。</summary>
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

- [ ] **Step 4: テスト実行して成功を確認**

Run: `dotnet test tests/AlienInvasion.Core.Tests`
Expected: PASS（全件）

- [ ] **Step 5: コミット**

```bash
git add src/AlienInvasion/Core/ZoneSerializer.cs tests/AlienInvasion.Core.Tests/ZoneSerializerTests.cs
git commit -m "feat: ZoneSerializer ゾーン台帳の直列化/復元を追加"
```

---

## Task 5: Core — MovementMath（高度補間・イージング）

**Files:**
- Create: `src/AlienInvasion/Core/MovementMath.cs`
- Test: `tests/AlienInvasion.Core.Tests/MovementMathTests.cs`

**Interfaces:**
- Consumes: なし
- Produces（`static class MovementMath`, namespace `AlienInvasion.Core`）:
  - `float EaseInOut(float t)` — `t`(0-1)を滑らかな加減速カーブに変換（`3t²-2t³`のスムーズステップ）。
  - `float Lerp(float a, float b, float t)` — `t`を0-1にクランプして線形補間。
  - `bool IsNear(float a, float b, float epsilon)` — 差が`epsilon`以下か（フェーズ2のトライポッド移動判定用に用意。本プランでは未使用だが公開APIとしてテストする）。

- [ ] **Step 1: 失敗するテストを書く**

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

- [ ] **Step 2: テスト実行して失敗を確認**

Run: `dotnet test tests/AlienInvasion.Core.Tests`
Expected: FAIL（`MovementMath` 未定義）

- [ ] **Step 3: 実装**

`src/AlienInvasion/Core/MovementMath.cs`:
```csharp
namespace AlienInvasion.Core
{
    /// <summary>母船/演出の座標補間に使う純粋な数学関数。</summary>
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

- [ ] **Step 4: テスト実行して成功を確認**

Run: `dotnet test tests/AlienInvasion.Core.Tests`
Expected: PASS（全件）

- [ ] **Step 5: コミット**

```bash
git add src/AlienInvasion/Core/MovementMath.cs tests/AlienInvasion.Core.Tests/MovementMathTests.cs
git commit -m "feat: MovementMath 高度補間/イージングを追加"
```

---

## Task 6: Mod本体プロジェクト（csproj/AssemblyInfo/ModConfig/Mod）とビルド検証

**Files:**
- Create: `src/AlienInvasion/AlienInvasion.csproj`
- Create: `src/AlienInvasion/Properties/AssemblyInfo.cs`
- Create: `src/AlienInvasion/Game/ModConfig.cs`
- Create: `src/AlienInvasion/Game/Mod.cs`
- Create: `AlienInvasion.sln`
- Create: `build.ps1`

**Interfaces:**
- Consumes: なし
- Produces:
  - `static class ModConfig`（namespace `AlienInvasion.Game`）: 全定数（下記Step2で定義）+ `static void Log(string)` / `static void LogError(string)`。
  - `class Mod : IUserMod`: `Name`, `Description`（get-only プロパティ）。

- [ ] **Step 1: csproj を作成**

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

- [ ] **Step 2: AssemblyInfo と ModConfig を作成**

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
    /// <summary>Mod全体の定数と共通ログ。</summary>
    public static class ModConfig
    {
        public const string LogPrefix = "[AlienInvasion] ";

        // --- AssetBundle ---
        public const string AssetBundleFileName = "alieninvasion.bundle";
        public const string MothershipPrefabName = "Mothership";
        public const string RedDecalPrefabName = "ContaminationDecal";

        // --- 母船の飛行 ---
        public const float MothershipStartAltitude = 800f;   // 出現高度(地表からの相対高さ)
        public const float MothershipHoverAltitude = 220f;   // 降下後のホバリング高度
        public const float DescendSeconds = 6f;
        public const float BombardSeconds = 10f;
        public const float StrikeIntervalSeconds = 0.6f;
        public const float AscendSeconds = 5f;

        // --- クレーター/破壊(累積値。Bombarding中に徐々に成長し、終了時に確定) ---
        public const float CraterRadiusMax = 90f;
        public const float CraterDepthMax = 22f;
        public const float StrikeScatterRadius = 15f;   // 落雷点を中心からランダムにずらす範囲
        public const float DestructionRadius = 70f;     // Bombarding終了時に建物破壊する半径

        // --- 汚染(赤) ---
        public const int ExpiryYears = 1;
        public const float ContaminationRadius = 90f;   // クレーター跡の汚染半径
        public const byte MaxPollution = 255;
        public const float RedDecalYOffset = 0.3f;

        // --- 発動 ---
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

- [ ] **Step 3: Mod.cs を作成**

`src/AlienInvasion/Game/Mod.cs`:
```csharp
using ICities;

namespace AlienInvasion.Game
{
    public class Mod : IUserMod
    {
        public string Name => "Alien Invasion";
        public string Description => "UFO母船が飛来し、雷とクレーターで街を破壊、放射能汚染を残します。手動発動キー: F7";
    }
}
```

- [ ] **Step 4: ソリューションファイルを作成**

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

- [ ] **Step 5: build.ps1 を作成（UTF-8 BOM 必須。日本語文字列を含むため）**

`build.ps1`:
```powershell
$ErrorActionPreference = "Stop"
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
if (-not $msbuild) { throw "MSBuild が見つかりません" }

& $msbuild "src\AlienInvasion\AlienInvasion.csproj" /t:Restore,Build /p:Configuration=Release /v:minimal
if ($LASTEXITCODE -ne 0) { throw "ビルド失敗" }

$dll = "src\AlienInvasion\bin\Release\AlienInvasion.dll"
$modDir = Join-Path $env:LOCALAPPDATA "Colossal Order\Cities_Skylines\Addons\Mods\AlienInvasion"
New-Item -ItemType Directory -Force -Path $modDir | Out-Null
Copy-Item $dll $modDir -Force

$bundleDir = Join-Path $modDir "Assets"
New-Item -ItemType Directory -Force -Path $bundleDir | Out-Null
$bundleSrc = "src\AlienInvasion\Assets\alieninvasion.bundle"
if (Test-Path $bundleSrc) {
    Copy-Item $bundleSrc $bundleDir -Force
    Write-Host "AssetBundle を配置しました"
} else {
    Write-Host "警告: $bundleSrc が見つかりません。ビジュアル(母船/赤デカール)は起動時スキップされます。"
}
Write-Host "配置完了: $modDir"
```
このファイルは**必ずUTF-8 BOM付きで保存**すること（PowerShell 5.1が日本語文字列リテラルを正しく解釈するため。Nuclear Meltdown Modで実際に踏んだ問題）。

- [ ] **Step 6: `src/AlienInvasion/Assets/` ディレクトリを作成（AssetBundle配置場所のプレースホルダ）**

```bash
mkdir -p src/AlienInvasion/Assets
touch src/AlienInvasion/Assets/.gitkeep
```

- [ ] **Step 7: ビルド検証**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: ビルド成功。`AlienInvasion.dll` が生成され Modフォルダへコピーされる。`alieninvasion.bundle` はまだ存在しないため警告が出るが、これは想定どおり（Task 14で用意）。

- [ ] **Step 8: コミット**

```bash
git add src/AlienInvasion/AlienInvasion.csproj src/AlienInvasion/Properties src/AlienInvasion/Game/ModConfig.cs src/AlienInvasion/Game/Mod.cs src/AlienInvasion/Assets/.gitkeep AlienInvasion.sln build.ps1
git commit -m "feat: Mod本体プロジェクト骨組みとビルド/配置スクリプトを追加"
```

---

## Task 7: AssetLoader（AssetBundleロード）

**Files:**
- Create: `src/AlienInvasion/Game/AssetLoader.cs`

**Interfaces:**
- Consumes: `ModConfig`（Task 6）
- Produces（`static class AssetLoader`, namespace `AlienInvasion.Game`）:
  - `void Initialize(string modAssemblyDirectory)` — `.bundle` をロード（見つからなければログのみで継続）。
  - `GameObject GetPrefab(string name)` — ロード済みprefabを名前で取得。無ければ `null`。
  - `bool IsAvailable { get; }` — AssetBundleが正常にロードされたか。

- [ ] **Step 1: 実装**

`src/AlienInvasion/Game/AssetLoader.cs`:
```csharp
using System.IO;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>Mod同梱の AssetBundle から prefab をロードする。見つからない場合は静かにスキップ。</summary>
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

- [ ] **Step 2: Mod.cs から Initialize を呼ぶよう更新**

`src/AlienInvasion/Game/Mod.cs` を以下に置換:
```csharp
using System.IO;
using System.Reflection;
using ICities;

namespace AlienInvasion.Game
{
    public class Mod : IUserMod
    {
        public string Name => "Alien Invasion";
        public string Description => "UFO母船が飛来し、雷とクレーターで街を破壊、放射能汚染を残します。手動発動キー: F7";

        public void OnEnabled()
        {
            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            AssetLoader.Initialize(dir);
        }
    }
}
```

- [ ] **Step 3: ビルド検証**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: ビルド成功。

- [ ] **Step 4: コミット**

```bash
git add src/AlienInvasion/Game/AssetLoader.cs src/AlienInvasion/Game/Mod.cs
git commit -m "feat: AssetLoader(AssetBundleロード)を追加"
```

---

## Task 8: PollutionField と ContaminationManager（汚染読み書き＋ゾーン台帳）

**Files:**
- Create: `src/AlienInvasion/Game/PollutionField.cs`
- Create: `src/AlienInvasion/Game/ContaminationManager.cs`

**Interfaces:**
- Consumes: `GridMath`, `ContaminationZone`（Core）, `ModConfig`
- Produces:
  - `static class PollutionField`:
    - `void ApplyMax(int cellIndex, byte intensity)` — セルの `m_pollution` を `Max(current, intensity)` に上げる。
    - `void ClearCell(int cellIndex)` — `m_pollution = 0`。
    - `void Refresh(int minX, int minZ, int maxX, int maxZ)` — `AreaModifiedB` 呼び出し。
  - `static class ContaminationManager`:
    - `List<ContaminationZone> Zones { get; }`（スナップショットのコピーを返す）
    - `void ReplaceAll(List<ContaminationZone> zones)`（ロード復元用）
    - `void AddZone(ContaminationZone zone)` — 台帳へ追加し初回汚染を適用。
    - `void RemoveZoneAt(int index)`
    - `void ReassertZone(ContaminationZone zone)` — 半径内セルへ再度 `ApplyMax`（自然減衰対策）。
    - `void ClearZone(ContaminationZone zone)` — 半径内セルを0にしてRefresh。

- [ ] **Step 1: PollutionField を実装**

`src/AlienInvasion/Game/PollutionField.cs`:
```csharp
namespace AlienInvasion.Game
{
    /// <summary>NaturalResourceManager の土壌汚染セルへの読み書きラッパ。</summary>
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

- [ ] **Step 2: ContaminationManager を実装**

`src/AlienInvasion/Game/ContaminationManager.cs`:
```csharp
using System.Collections.Generic;
using AlienInvasion.Core;

namespace AlienInvasion.Game
{
    /// <summary>汚染ゾーン台帳と、グリッドへの適用/維持/除去。</summary>
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

- [ ] **Step 3: ビルド検証**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: ビルド成功。

- [ ] **Step 4: コミット**

```bash
git add src/AlienInvasion/Game/PollutionField.cs src/AlienInvasion/Game/ContaminationManager.cs
git commit -m "feat: 汚染グリッド書込(PollutionField)とゾーン台帳(ContaminationManager)を追加"
```

---

## Task 9: RedContaminationVisual（赤デカールの配置/撤去）

**Files:**
- Create: `src/AlienInvasion/Game/RedContaminationVisual.cs`

**Interfaces:**
- Consumes: `AssetLoader`, `ContaminationZone`（Core）, `ModConfig`
- Produces（`static class RedContaminationVisual`）:
  - `void Sync(List<ContaminationZone> activeZones)` — 現在のゾーン一覧に合わせてデカールGameObjectを生成/破棄。ゾーンごとに1つのデカールを`(CenterX, TerrainHeight, CenterZ)`に配置し、`Radius*2`にスケール。**メインスレッド専用**（GameObject操作のため）。
  - `void Clear()` — 全デカールを破棄（Mod無効化/エラー時のクリーンアップ用）。

- [ ] **Step 1: 実装**

`src/AlienInvasion/Game/RedContaminationVisual.cs`:
```csharp
using System.Collections.Generic;
using AlienInvasion.Core;
using ColossalFramework;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// 汚染ゾーンに対応する赤いデカールGameObjectを配置/撤去する。
    /// GameObjectを直接操作するため、必ずメインスレッド(OnUpdate)から呼ぶこと。
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
注: `TerrainManager.SampleDetailHeight` は地形の高さを取得する標準API。ゾーンの一意キーは `(CenterX, CenterZ, StartTicks)` のハッシュとする（同一ゾーンが `ReplaceAll` 等でリスト内位置を変えても同じキーを保つため、インデックスではなく値ベース）。

- [ ] **Step 2: ビルド検証**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: ビルド成功。

- [ ] **Step 3: コミット**

```bash
git add src/AlienInvasion/Game/RedContaminationVisual.cs
git commit -m "feat: RedContaminationVisual 赤デカールの配置/撤去を追加"
```

---

## Task 10: Effects（雷ボルト・閃光・雷鳴）

**Files:**
- Create: `src/AlienInvasion/Game/Effects.cs`

**Interfaces:**
- Consumes: `ModConfig`
- Produces（`static class Effects`）:
  - `void PlayLightningStrike(Vector3 groundPoint, Vector3 skyPoint)` — **メインスレッド専用**。`groundPoint`と`skyPoint`を結ぶジグザグの`LineRenderer`ボルトを一瞬表示し、着弾点に隕石衝撃エフェクト（流用）を再生、`RainProperties.m_ThunderSound`を再生。
  - 内部で生成した一時GameObject（ボルト）は再生後 `Object.Destroy(go, lifetime)` で自動破棄。

- [ ] **Step 1: 実装**

`src/AlienInvasion/Game/Effects.cs`:
```csharp
using ColossalFramework;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>雷ボルト・着弾閃光・雷鳴の再生。全てメインスレッド(OnUpdate)から呼ぶこと。</summary>
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

- [ ] **Step 2: ビルド検証**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: ビルド成功。

- [ ] **Step 3: コミット**

```bash
git add src/AlienInvasion/Game/Effects.cs
git commit -m "feat: Effects 雷ボルト/閃光/雷鳴の再生を追加"
```

---

## Task 11: Mothership と InvasionManager（状態機械の統括）

**Files:**
- Create: `src/AlienInvasion/Game/Mothership.cs`
- Create: `src/AlienInvasion/Game/InvasionManager.cs`

**Interfaces:**
- Consumes: `AssetLoader`, `Effects`, `ContaminationManager`, `InvasionState`/`MovementMath`/`ContaminationZone`（Core）, `ModConfig`
- Produces:
  - `class Mothership`: `Mothership(Vector3 targetPosition)` コンストラクタ、`void SetAltitude(float altitudeAboveTarget)`（メインスレッド：GameObject位置更新）、`Vector3 SkyPointForBolt()`、`void Destroy()`。プロパティ: `Vector3 Position`。
  - `static class InvasionManager`:
    - `bool IsActive { get; }`
    - `void StartInvasion(Vector3 targetPosition)` — アイドル時のみ受理。
    - `void UpdateVisual(float realTimeDelta)` — **メインスレッド専用**。母船の位置補間・フェーズ内タイマー進行・フェーズ遷移判定（Descending→Bombarding→Ascending→Done）を行う。
    - `void UpdateSimulation()` — **シミュレーションスレッド専用**。現在`Bombarding`ならクレーター成長を行い、`Bombarding→Ascending`遷移の**直後1回だけ**建物破壊＋汚染ゾーン登録を行う。

- [ ] **Step 1: Mothership を実装**

`src/AlienInvasion/Game/Mothership.cs`:
```csharp
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>母船のGameObjectと位置。GameObject操作は全てメインスレッド(OnUpdate)から呼ぶこと。</summary>
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

- [ ] **Step 2: InvasionManager を実装**

`src/AlienInvasion/Game/InvasionManager.cs`:
```csharp
using AlienInvasion.Core;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// 1回の襲来イベントの統括。
    /// UpdateVisual: メインスレッド専用(GameObject操作・フェーズタイマー・状態遷移の書き込み元)。
    /// UpdateSimulation: シミュレーションスレッド専用(DisasterHelpers/汚染書込)。
    /// InvasionState/フェーズタイマーは UpdateVisual からのみ書き込む(single-writer)。
    /// UpdateSimulation は状態を読むのみで書き込まない。
    /// </summary>
    public static class InvasionManager
    {
        private static InvasionState _state = InvasionState.Idle;
        private static Mothership _ship;
        private static Vector3 _target;
        private static float _phaseElapsed;
        private static float _strikeTimer;
        private static float _craterProgress; // 0..1
        private static bool _bombardResolved;  // Bombarding終了時の建物破壊/汚染登録が完了したか

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

        /// <summary>シミュレーションスレッドから毎tick呼ぶ。DisasterHelpers/汚染書込はここでのみ行う。</summary>
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
            // preRadius は totalRadius と同じ値にする(0だと何も破壊されないという既知の罠を回避)
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
注: `UpdateVisual` を `Idle` 状態の1フレームだけ `Done→Idle` に遷移させ、`IsActive` が正しく `false` に戻るようにしている（元のNextチェーンは `Done→Idle` だが、Doneに到達した直後の1回はメインスレッドのこの遷移を経由する必要がある）。

- [ ] **Step 3: ビルド検証**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: ビルド成功。

- [ ] **Step 4: コミット**

```bash
git add src/AlienInvasion/Game/Mothership.cs src/AlienInvasion/Game/InvasionManager.cs
git commit -m "feat: Mothership と InvasionManager(状態機械)を追加"
```

---

## Task 12: InvasionThreadingExtension（発動・毎tick駆動・汚染維持/期限）

**Files:**
- Create: `src/AlienInvasion/Game/Simulation/InvasionThreadingExtension.cs`

**Interfaces:**
- Consumes: `InvasionManager`, `ContaminationManager`, `RedContaminationVisual`, `ExpiryClock`（Core）, `ModConfig`
- Produces:
  - `class InvasionThreadingExtension : ThreadingExtensionBase` — ゲームが自動検出。
    - `OnUpdate(float realTimeDelta, float simulationTimeDelta)`（メインスレッド）: 手動キー検知 → `InvasionManager.StartInvasion`、`InvasionManager.UpdateVisual`、`RedContaminationVisual.Sync`。
    - `OnAfterSimulationTick()`（シミュレーションスレッド）: `InvasionManager.UpdateSimulation`、ランダム発生抽選、汚染ゾーンの維持/期限処理。

- [ ] **Step 1: 実装**

`src/AlienInvasion/Game/Simulation/InvasionThreadingExtension.cs`:
```csharp
using System.Collections.Generic;
using AlienInvasion.Core;
using ICities;
using UnityEngine;

namespace AlienInvasion.Game.Simulation
{
    /// <summary>
    /// 襲来の発動・進行・汚染維持を駆動する。
    /// OnUpdate=メインスレッド(GameObject/入力)、OnAfterSimulationTick=シミュレーションスレッド(DisasterHelpers/汚染)。
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
            // カメラ中心の地表位置を狙う簡易実装
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

            const float half = 8500f; // マップ範囲の目安
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

- [ ] **Step 2: ビルド検証**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: ビルド成功。

- [ ] **Step 3: コミット**

```bash
git add src/AlienInvasion/Game/Simulation/InvasionThreadingExtension.cs
git commit -m "feat: InvasionThreadingExtension 発動/毎tick駆動/汚染維持を追加"
```

---

## Task 13: InvasionDataExtension（セーブ/ロード永続化）

**Files:**
- Create: `src/AlienInvasion/Game/Serialization/InvasionDataExtension.cs`

**Interfaces:**
- Consumes: `ContaminationManager`, `ZoneSerializer`（Core）, `ModConfig`
- Produces:
  - `class InvasionDataExtension : SerializableDataExtensionBase` — `OnSaveData()`/`OnLoadData()`。データキー `"AlienInvasion.Contamination.v1"`。ゲームが自動検出。

- [ ] **Step 1: 実装**

`src/AlienInvasion/Game/Serialization/InvasionDataExtension.cs`:
```csharp
using System.Collections.Generic;
using AlienInvasion.Core;
using ICities;

namespace AlienInvasion.Game.Serialization
{
    /// <summary>汚染ゾーン台帳をセーブデータへ永続化する。ゲームが自動検出。</summary>
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

- [ ] **Step 2: ビルド検証**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: ビルド成功。

- [ ] **Step 3: コミット**

```bash
git add src/AlienInvasion/Game/Serialization/InvasionDataExtension.cs
git commit -m "feat: 汚染ゾーンのセーブ/ロード永続化を追加"
```

---

## Task 14: AssetBundleビルドパイプライン（Unity Editorスクリプト）とREADME

このタスクはユーザーが `models/source/models.blend` から `alieninvasion.bundle` を実際に作るための土台を用意する。Claudeは Unity Editor を実行できないため、**手順書＋Editorスクリプト**を成果物とする。

**Files:**
- Create: `unity-project/Assets/Editor/BuildAssetBundles.cs`
- Create: `src/AlienInvasion/README.md`

**Interfaces:**
- Consumes: なし
- Produces: なし（ツール/ドキュメント）

- [ ] **Step 1: BuildAssetBundles.cs を作成**

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

- [ ] **Step 2: README にAssetBundle制作手順を記載**

`src/AlienInvasion/README.md`:
```markdown
# Alien Invasion (Cities: Skylines Mod)

UFO母船が飛来し、雷を連打して地面にクレーターを形成、周辺の建物を破壊、放射能汚染(赤)をゲーム内1年残す。手動発動キー: **F7**（`Game/ModConfig.cs`の`ManualTriggerKey`で変更可）。ランダムでも低確率発生する。

## AssetBundle の作り方（Blenderモデル→ゲームで使える形式）

1. **Unity Editor 5.6.6f2** をインストール（[Unity Archive](https://unity3d.com/get-unity/download/archive) から取得。Cities: Skylines のエンジン(Unity 5.6.7)と互換のバージョン）。
2. `models/source/models.blend` を Blender で開き、`MotherShip` と `TriPod`（本フェーズでは未使用）、赤いデカール用の平面オブジェクトをそれぞれ **FBX でエクスポート**。
   - `MotherShip` のピボットは幾何中心（確認済み・修正不要）。
   - デカール用オブジェクトは中心/接地面にピボットを置く。
   - エクスポート前に `Ctrl+A → Scale` で各オブジェクトのスケールを適用しておく（未適用スケールが残ったままだとFBXインポート後の挙動が予測しにくくなるため）。
3. `unity-project` を Unity Editor 5.6.6f2 で開く。
4. エクスポートしたFBXを `unity-project/Assets/` にインポートし、マテリアル/テクスチャ（`_d`色/`_n`ノーマル/`_s`スペキュラ/`_i`自発光/`_a`透明）を設定。
5. 各モデルを Prefab 化し、**Prefab名を正確に** `Mothership` / `ContaminationDecal` にする（`Game/ModConfig.cs` の `MothershipPrefabName`/`RedDecalPrefabName` と一致させる）。
6. 各Prefabの Inspector で **AssetBundle名** を `alieninvasion` に設定（Prefab選択 → Inspector右下の AssetBundle ドロップダウン）。
7. Unity メニュー **AlienInvasion → Build AssetBundle** を実行。
8. `unity-project/AssetBundles/alieninvasion` というファイル（拡張子なし）が生成されるので、**`alieninvasion.bundle`** にリネームして `src/AlienInvasion/Assets/alieninvasion.bundle` に配置。
9. `build.ps1` を再実行するとModフォルダへ自動配置される。

AssetBundleが無い状態でもModはビルド・起動でき、母船/デカールの視覚演出のみスキップされる（ログに `AssetBundle not found` と出る）。

## ビルドと配置
```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```
`%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\AlienInvasion\` に配置される。

## ゲーム内動作確認手順
1. Content Manager → Mods で "Alien Invasion" を有効化。
2. ゲーム内で **F7** を押す（または待ってランダム発生を確認）。
3. 母船が降下 → 雷を連打しながらクレーターが形成される → 周辺建物が破壊される → 上昇して消える。
4. 跡地に赤い汚染が残ることを確認（AssetBundle未配置の場合は標準の土壌汚染のみ）。
5. ゲーム内1年経過で汚染が自動消滅することを確認。
6. セーブ→ロードで汚染が維持されることを確認。

## 設定
定数は `Game/ModConfig.cs`（発動キー・確率・半径・時間等）。

## ログ
`%LOCALAPPDATA%\Colossal Order\Cities_Skylines\` の output_log で `[AlienInvasion]` を検索。
```

- [ ] **Step 3: コミット**

```bash
git add unity-project/Assets/Editor/BuildAssetBundles.cs src/AlienInvasion/README.md
git commit -m "docs: AssetBundleビルドパイプラインとREADMEを追加"
```

---

## Task 15: 最終ビルド・全テスト・ゲーム内検証依頼

**Files:** なし（検証のみ）

- [ ] **Step 1: Coreの全テスト実行**

Run: `dotnet test tests/AlienInvasion.Core.Tests`
Expected: 全テストPASS（InvasionState/GridMath/ExpiryClock/ZoneSerializer/MovementMath 合計約28件）。

- [ ] **Step 2: Mod本体の最終ビルド・配置**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: ビルド成功、Modフォルダへ `AlienInvasion.dll` が配置。AssetBundle未配置なら警告ログが出るが正常（Task 14のパイプラインは別途ユーザーが実施）。

- [ ] **Step 3: ゲーム内検証をユーザーへ依頼**

README の「ゲーム内動作確認手順」1-6 をユーザーに実施依頼。AssetBundleがまだ無い場合は「F7で発動→クレーターと建物破壊が起きるか」だけでもロジックの正しさを確認できる（母船/赤デカールの見た目は後日AssetBundle完成後に確認）。

- [ ] **Step 4: 最終コミット**

```bash
git add -A
git commit -m "chore: 最終ビルド・テスト確認"
```

---

## Self-Review

**1. Spec coverage（設計書との対応）:**
- 発動(手動＋ランダム) → Task 12 `InvasionThreadingExtension`（F7キー＋ランダム抽選） ✅
- 母船降下→雷連打→クレーター形成→上昇消滅 → Task 11 `Mothership`/`InvasionManager` ✅
- 雷エフェクト(ゲームに存在しないため自作) → Task 10 `Effects`（LineRenderer+隕石閃光流用+雷鳴流用、逆コンパイルで実在しないことを確認済み） ✅
- 建物破壊 → Task 11 `ResolveBombardDamage`（`DisasterHelpers.DestroyStuff`、preRadius罠を回避） ✅
- 赤い汚染(地面+エフェクト)、1年で消滅、除染施設なし → Task 8 `ContaminationManager`(1年のみ) + Task 9 `RedContaminationVisual`(赤デカール) ✅
- AssetBundle同梱 → Task 7 `AssetLoader` + Task 14 パイプライン ✅
- ModConfigで調整可能 → 全定数をTask 6 `ModConfig`に集約 ✅
- セーブ/ロード → Task 13 `InvasionDataExtension` ✅
- スレッド安全性(メイン/シミュレーション分離) → Global Constraints + 各タスクで明示 ✅
- トライポッド(フェーズ2) → 本プランのスコープ外（別プランで実施、設計書どおり） ✅

**2. Placeholder scan:** "TBD"/"後で"等の曖昧語なし。Task 14はUnity Editorを私が実行できないため手順書という性質上「ユーザーが行う」ものだが、これは仕様であり後回しではない（プレースホルダではない）。

**3. Type consistency:**
- `ContaminationZone(float,float,float,long)` — Task1定義、Task4/8/9/11/12/13利用で一致。
- `GridMath.CellsInRadius(float,float,float)→List<int>` — Task2定義、Task8利用で一致。
- `ExpiryClock.HasExpired(long,long,int)` — Task3定義、Task12利用で一致。
- `ZoneSerializer.Serialize/Deserialize` — Task4定義、Task13利用で一致。
- `MovementMath.EaseInOut/Lerp/IsNear` — Task5定義、Task11で`EaseInOut`/`Lerp`を利用。`IsNear`はフェーズ2向けに用意した公開APIで、テストで検証済みのため未使用コード扱いにはならない。
- `InvasionManager.StartInvasion(Vector3)`/`IsActive`/`UpdateVisual(float)`/`UpdateSimulation()` — Task11定義、Task12利用で一致。
- `Mothership.SetAltitude(float)`/`SkyPointForBolt()`/`Destroy()`/`Position` — Task11内で定義・利用で一致（Step2修正時に`UpdateVisual`のコンストラクタ引数呼び出しをコンストラクタ内`SetAltitude`ではなく直接位置代入に整理し、`Position`プロパティのみで到達判定を行う設計に統一）。
- `AssetLoader.Initialize(string)`/`GetPrefab(string)`/`IsAvailable` — Task7定義。Task9/11で`GetPrefab`利用、名前一致。`IsAvailable`は将来の分岐用に公開APIとして用意。
</parameter>
