# Alien Invasion - an options screen

- Date: 2026-08-12
- Status: approved, ready for implementation

## Goal

This mod is the only one of the author's five with no options screen at all - it implements
`Name`, `Description` and `OnEnabled` and nothing else, so the game never lists it on the Options
page. Give it one, and put the settings that players actually want behind it.

## Scope

**In scope**

| Group | Setting | Type | Default |
|---|---|---|---|
| Invasion | how to summon | help text | - |
| | Summon hotkey | dropdown | F7 |
| | Maximum invasions at once | slider 1-5 | 5 |
| Random invasions (DESTRUCTIVE - off by default) | Enable random invasions | checkbox | **off** |
| | Average in-game days between invasions | slider 5-365, step 5 | 60 |
| | what it does | help text | - |
| Aftermath | Leave red contamination behind | checkbox | on |
| Sound | UFO arrival sound | checkbox | on |
| | Tripod footstep sound | checkbox | on |
| | Sound volume | slider 0-100 | 100 |

**Out of scope** - the tripods' beam interval, range and active days stay constants. They are
balance tuning rather than preference, and every extra row makes the page harder to read.

## Random invasions: from real time to the game clock

Random invasions already work; the player has seen one. What does not work is the *rate*.

`InvasionThreadingExtension.MaybeRollRandomInvasion` rolls `1/10000` every
`RandomCheckIntervalTicks / 100 = 40.96` **real** seconds, so the mean interval is 409,600 s -
about 114 hours of play. That is why the feature has effectively never been seen, and why a plain
on/off switch would be a setting that does nothing.

Replace the schedule (not the firing path, which is proven) with one driven by the **game clock**:

- One check per in-game day, using the existing `ExpiryClock.HasElapsedDays` and
  `SimulationManager.instance.m_currentGameTime.Ticks`.
- Fire with probability `1 / averageDays`, so the mean interval is exactly the configured number
  of in-game days.
- It therefore stops while the game is paused and stretches with the game speed, matching how
  `TripodActiveDays` already measures the tripods' lifetime. The real-time version did neither.
- **The first check after a level load only establishes the baseline.** Loading a save whose game
  clock is far ahead of the last check must not roll a backlog of missed days.
- Still suppressed while any invasion is running, as now.

Why "average days between invasions" rather than a frequency multiplier: it names the quantity a
player actually wants to set, and it is honest about the model being a per-day coin flip rather
than a schedule.

### Core / Game split

`Core/RandomInvasionSchedule.cs` (no UnityEngine, unit-tested):

- `bool IsCheckDue(long lastCheckTicks, long nowTicks)` - whether an in-game day has passed.
- `bool ShouldFire(int averageDays, int roll)` - `roll` is 0-9999; true when
  `roll < 10000 / averageDays`. Guards `averageDays <= 0`.

The Game layer owns the clock, the RNG (`UnityEngine.Random`, main thread - never the simulation
thread's randomizer), and the priming flag.

## Settings storage

New `Game/ModSettings.cs`, following the sibling mods:

- File name **`AlienInvasionSettings`** - deliberately not `AlienInvasion`. A settings file named
  after the assembly collides with the mod's own registration key, which deletes the settings file
  on every launch and flags the mod errored.
- `SavedInt` per setting, `autoUpdate: true`, registered once behind an `Ensure()` latch.
- Keys: `randomEnabled` (0), `randomAverageDays` (60), `maxConcurrent` (5), `hotkey` (F7's
  `KeyCode` value), `contamination` (1), `ufoSound` (1), `tripodSound` (1), `soundVolume` (100).

These are new keys with no predecessors, so there is no migration and no provenance question.

## Behaviour of each setting

**Maximum invasions at once.** `InvasionManager._slots` is sized from
`ModConfig.MaxConcurrentInvasions` at static init and stays at 5. The setting caps how many slots
may be *used*: `CanStartMore` becomes `ActiveCount < clamp(setting, 1, _slots.Length)`. Lowering it
while invasions are running never invalidates them - they finish normally, and the next one is
refused.

**Hotkey.** A `KeyCode[]` of sensible choices with a dropdown, mirroring Missile Disaster. The
stored value is the `KeyCode`'s integer, not the dropdown position, so reordering the list later
cannot re-point an existing setting.

**Contamination.** Gates the two places a zone is created - the bombardment resolution in
`Invasion.cs` and the tripods' trail in `TripodGroup.cs`. Turning it off stops *new* contamination;
it deliberately does not erase zones already in the save, because silently changing the terrain the
moment a checkbox is ticked would be worse than the inconsistency.

**Sound.** The two checkboxes skip playback entirely; the volume slider scales the existing
`UfoSoundVolume` / `TripodSoundVolume` constants, which stay as the relative balance between them.

## Localization

All new strings go into `AlienStrings`; `ja.txt` is written by hand and `en.txt` regenerated from
the built assembly. Nothing may be copied into a `static readonly string[]` - the hotkey dropdown's
labels are `KeyCode.ToString()`, which is not localized, but the group and row labels are.

Adding `OnSettingsUI` also means the correction made to `LocaleLoader`'s comment - that this mod
has no options page - has to be reverted, and the options page now follows a language change
through `OptionsMainPanel.OnLocaleChanged`.

## Testing

- **Core, xunit:** `RandomInvasionSchedule` - a day has/has not passed; the boundary; the mean
  probability for representative day counts; `averageDays <= 0` never fires; a clock that moves
  backwards (a different save) does not fire.
- **Game:** not unit-testable. A clean `build.ps1` is the bar, as elsewhere in this project.
- In game: the options page appears and is in Japanese; turning random on with a short average
  produces invasions; lowering the cap refuses the next one; contamination off leaves no red weed.

## Done when

- `dotnet test` green including the new cases, `build.ps1` with no `error CS` or `warning CS`.
- `en.txt` and `ja.txt` carry every `AlienStrings` key, with no unknown or duplicated keys.
- The mod appears on the game's Options screen.
