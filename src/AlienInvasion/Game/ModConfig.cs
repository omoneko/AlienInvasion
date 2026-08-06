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
        public const string TripodPrefabName = "Tripod";

        // --- Loading OBJ at runtime, the fallback for when the AssetBundle cannot be used ---
        // <name>.obj and <name>.mtl go in the Models subfolder of the mod directory.
        public const string ModelsFolderName = "Models";
        public const float ObjMetallic = 0.7f;     // the Standard shader's metallic parameter
        public const float ObjGlossiness = 0.6f;   // the Standard shader's smoothness parameter
        public static readonly Color ObjFallbackColor = new Color(0.2f, 0.2f, 0.2f, 1f); // fallback when the MTL carries none: metallic grey

        // --- Glowing at night ---
        // The material with this name - the metallic grey base - never glows. Every other
        // coloured material glows in its own colour at night. This must match a newmtl name
        // in the MTL.
        public const string BaseMaterialName = "MetallicGray";
        public const float NightEmissionIntensity = 2.2f;  // emission strength at night; above 1 it picks up bloom readily
        public const float EmissionFadePerSecond = 1.5f;   // how fast the glow fades in and out at dawn and dusk (0..1 per second)
        // Highlight colour of the contamination decal, crimson through orange-red. The alpha
        // is the peak opacity at the centre, fading radially outwards. It colours the dense
        // parts of the red-weed texture.
        public static readonly Color RedDecalColor = new Color(1f, 0.22f, 0.05f, 0.5f);

        // --- The mothership's flight ---
        public const float MothershipStartAltitude = 800f;   // spawn altitude, relative to the ground
        public const float MothershipHoverAltitude = 220f;   // hovering altitude after descending, during the bombardment
        public const float MothershipLingerAltitude = 300f;  // altitude it loiters at after the bombardment, while the tripods are active
        public const float DescendSeconds = 6f;
        public const float BombardSeconds = 10f;
        public const float StrikeIntervalSeconds = 0.6f;
        public const float AscendSeconds = 5f;                // time to climb to the loitering altitude after the bombardment
        public const float DepartSeconds = 6f;                // time to leave once the tripods are gone: it climbs to the spawn altitude and disappears
        public const float MothershipSpinDegPerSec = 60f;  // how fast the mothership spins about its vertical axis (degrees/s)
        public const float MothershipScale = 1f;           // scale applied when the prefab is created, calibrated in-game

        // --- Sinkholes and destruction ---
        // Equivalent to a vanilla sinkhole at disaster scale 5.5 (internally intensity 55).
        // SinkholeAI computes:
        //   width = m_holeWidth(50) * (55*0.01) + 16 = 43.5, depth = m_holeDepth(50) * (55*0.01) + 16 = 43.5
        //   MakeCrater(pos, radius = width*0.5 = 21.75, depth = 43.5, raiseEdges:false).
        // MakeCrater digs down relative to the current ground height and clamps at an absolute
        // 0 m. Calling it every tick accumulates and eventually digs to the terrain floor,
        // which looks absurd, so it is applied exactly once when the bombardment ends - the
        // same single sinkhole a vanilla level 5.5 produces.
        public const float SinkholeRadius = 21.75f;
        public const float SinkholeDepth = 43.5f;
        public const float StrikeScatterRadius = 15f;   // how far each strike is scattered from the centre
        public const float DestructionRadius = 70f;     // radius in which buildings are destroyed when the bombardment ends

        // --- Tripods: deployment and free movement ---
        public const int TripodCount = 3;
        public const float TripodSpeed = 45f;                // horizontal speed (units/s)
        public const int TripodActiveDays = 14;              // how long they stay from deployment to disappearing,
                                                             // measured in in-game days rather than real seconds,
                                                             // so it stretches with the game speed and stops while paused
        public const float TripodTurnIntervalSeconds = 2.5f; // how often they change direction
        public const float TripodTurnMaxDeg = 60f;           // largest turn in one change of direction, either way
        public const float TripodScale = 1f;
        public const float TripodSpawnScatter = 40f;         // radius they are scattered over around the crater centre
        public const float TripodMapHalfExtent = 8500f;      // movement bounds, roughly the map radius
        // Facing: LookRotation(heading) points the model's front - Blender's -Y side - along
        // the heading. This yaw in degrees compensates for whichever Unity local axis the
        // model's front actually ended up on; adjust it if the facing looks wrong in-game.
        public const float TripodYawOffsetDeg = 0f;
        // A vertical bob that sells the hovering. Visual only - the logical Position stays on the ground.
        public const float TripodBobAmplitude = 2.5f;        // amplitude of the bob (m)
        public const float TripodBobFreqHz = 1.1f;           // frequency of the bob (Hz)

        // --- Tripods: the laser and the contaminated trail ---
        public const float BeamIntervalSeconds = 1.5f;             // how often the beam fires, which is also how often it destroys
        public const float BeamDestroyRadius = 25f;                // radius destroyed by one beam impact
        public const float TripodTrailContamRadius = 30f;          // radius of the contamination left in their trail
        public const float TripodTrailContamIntervalSeconds = 3f;  // how often the trail is stamped down
        public const float TripodHeadHeight = 60f;                 // height of the head the beam fires from, above its footing
        public const float BeamMinAngleDeg = 20f;                  // smallest depression angle of the beam, measured down from horizontal
        public const float BeamMaxAngleDeg = 60f;                  // largest depression angle of the beam
        public const float BeamMaxRange = 180f;                    // furthest horizontal distance the beam can reach

        // --- Toppling from a direct nuclear hit: a hidden feature tied to the Missile
        //     Disaster mod ---
        // A tripod within this radius of a Nuclear warhead's impact falls over and disappears.
        // The test is for a genuine direct hit, on the scale of the crater. Nothing happens at
        // all without the Missile mod: the two are only loosely coupled, through reflection.
        public const float NuclearToppleRadius = 150f;          // horizontal radius counted as a direct hit (m)
        public const float TripodToppleFallAngleDeg = 88f;      // angle it falls through; 90 would be flat on its side
        public const float TripodToppleDurationSeconds = 1.4f;  // time the fall takes (seconds)
        public const float TripodToppleDwellSeconds = 2.0f;     // time it lies there after falling, before disappearing (seconds)
        public const float TripodToppleSink = 4f;               // how far it sinks into the ground as it falls (m), which hides the legs not meeting the terrain

        // --- Effect colours: the lightning and the laser both glow blue-white ---
        public static readonly Color BoltColor = new Color(0.55f, 0.8f, 1f);  // the mothership's lightning
        public static readonly Color BeamColor = new Color(0.6f, 0.85f, 1f);  // the tripods' laser

        // --- Impact explosion, shared by the mothership's lightning and the tripods' laser ---
        // It uses the game's own medium explosion (m_mediumExplosion); the magnitude below
        // scales it.
        public const float ImpactEffectMagnitude = 0.7f;

        // --- Contamination (the red weed) ---
        public const int ExpiryMonths = 2;              // in-game months before the contamination lifts
        public const float ContaminationRadius = 90f;   // radius of the contamination left around a sinkhole
        public const byte MaxPollution = 255;
        public const float RedDecalYOffset = 0.3f;

        // --- Sounds (WAV, in the Sounds subfolder of the mod directory) ---
        // The originals are mp3, but CS runs Unity 5.6, which cannot decode mp3 at runtime -
        // WWW with AudioType.MPEG simply returns null. They are therefore converted to WAV
        // (PCM) with Blender's aud module and loaded from that.
        public const string SoundsFolderName = "Sounds";
        public const string UfoSoundFile = "UFOSound.wav";       // played once when the mothership arrives
        public const string TripodSoundFile = "TriPodSound.wav"; // played at intervals while the tripods move
        public const float UfoSoundVolume = 1f;
        public const float TripodSoundVolume = 0.9f;
        // Interval between tripod movement sounds, in seconds. They cannot overlap because a
        // single long-lived AudioSource plays them, so the next one waits for the previous to
        // finish. Playback pauses with the game.
        public const float TripodStepIntervalSeconds = 10f;

        // --- Triggering ---
        public const int MaxConcurrentInvasions = 5;   // how many invasions can run at once
        public const KeyCode ManualTriggerKey = KeyCode.F7;
        public const int RandomCheckIntervalTicks = 4096;
        public const int RandomChancePer10000 = 1;

        // --- The summon button, attached beside the disasters panel ---
        // The button is added as a direct child of the DisastersPanel root, so it follows that
        // panel's visibility automatically. The exact placement can only really be judged
        // in-game, so it lives here as constants: an offset in the panel's own coordinates,
        // where OffsetX and OffsetY are measured from the panel's top-left corner. The default
        // sits towards the top right.
        // The panel size is written to the log in-game, so adjust these if it sits wrong.
        public const float SummonButtonWidth = 46f;  // the mothership icon button
        public const float SummonButtonHeight = 36f;
        public const float SummonButtonOffsetX = 8f;    // inner margin from the panel's right edge, used to right-align it
        public const float SummonButtonOffsetY = -40f;  // Y relative to the panel's top edge; negative lifts it above the disaster icon row
        // Grace period, in frames, before falling back to a permanent button in the top-left
        // corner because the disasters panel never appeared - without the Natural Disasters
        // DLC, for instance.
        public const int SummonButtonFallbackFrames = 600;

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
