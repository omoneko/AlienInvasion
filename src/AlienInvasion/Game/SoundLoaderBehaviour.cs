using System.Collections;
using System.IO;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// MonoBehaviour that loads and plays Sounds/*.wav from the mod folder at runtime.
    /// Loading through WWW needs a frame to complete, so it runs as a coroutine; SoundManager
    /// creates and holds this behaviour. CS runs Unity 5.6, which cannot decode mp3 at runtime
    /// - AudioType.MPEG simply yields null - so these are WAV (PCM).
    ///
    /// The arrival sound gets a throwaway 2D AudioSource and plays once.
    /// The tripod movement sound is managed through a single long-lived AudioSource
    /// (_tripodSource), which gives us two things:
    ///   - it never overlaps, since the next one waits for the previous to finish
    ///   - Pause and UnPause stop it with the game
    /// Playback uses AudioSource, so all of this is main thread only.
    /// </summary>
    public class SoundLoaderBehaviour : MonoBehaviour
    {
        private AudioClip _ufo;
        private AudioClip _tripod;
        private string _dir;

        // The long-lived source for the tripod movement sound: never overlapping, and pausable
        private AudioSource _tripodSource;
        private float _tripodTimer;
        private bool _tripodPausedMidClip;

        public void BeginLoad(string soundsDir)
        {
            _dir = soundsDir;
            StartCoroutine(LoadAll());
        }

        private IEnumerator LoadAll()
        {
            yield return StartCoroutine(LoadOne(ModConfig.UfoSoundFile, c => _ufo = c));
            yield return StartCoroutine(LoadOne(ModConfig.TripodSoundFile, c => _tripod = c));
            ModConfig.Log("SoundLoader: done (ufo=" + (_ufo != null) + ", tripod=" + (_tripod != null) + ")");
        }

        private IEnumerator LoadOne(string fileName, System.Action<AudioClip> assign)
        {
            string path = Path.Combine(_dir, fileName);
            if (!File.Exists(path))
            {
                ModConfig.LogError("SoundLoader: file not found " + path);
                yield break;
            }

            string url = "file:///" + path.Replace("\\", "/");
            using (WWW www = new WWW(url))
            {
                yield return www;

                if (!string.IsNullOrEmpty(www.error))
                {
                    ModConfig.LogError("SoundLoader: WWW error " + fileName + " : " + www.error);
                    yield break;
                }

                AudioClip clip = www.GetAudioClip(false, false, AudioType.WAV);
                if (clip == null)
                {
                    ModConfig.LogError("SoundLoader: GetAudioClip returned null " + fileName);
                    yield break;
                }

                float waited = 0f;
                while (clip.loadState == AudioDataLoadState.Loading && waited < 10f)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }

                if (clip.loadState != AudioDataLoadState.Loaded)
                {
                    ModConfig.LogError("SoundLoader: not loaded " + fileName + " state=" + clip.loadState);
                    yield break;
                }

                assign(clip);
                ModConfig.Log("SoundLoader: loaded " + fileName + " (" + clip.length.ToString("0.0") + "s)");
            }
        }

        /// <summary>Plays the arrival sound once in 2D, at a constant volume. Main thread only.</summary>
        public void PlayUfoArrival()
        {
            if (_ufo == null) return;
            if (!ModSettings.UfoSoundEnabled) return;
            try
            {
                var go = new GameObject("AlienInvasion_SFX_UFO");
                var src = go.AddComponent<AudioSource>();
                src.clip = _ufo;
                // The constant stays the balance between the two sounds; the setting scales both.
                src.volume = ModConfig.UfoSoundVolume * ModSettings.SoundVolume;
                src.spatialBlend = 0f; // 2D
                src.Play();
                Object.Destroy(go, _ufo.length + 0.2f);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("SoundLoader.PlayUfoArrival error: " + e);
            }
        }

        /// <summary>
        /// Drives the tripod movement sound every frame. One long-lived source plays it, so it
        /// never overlaps, and while the game is paused both the playback and the timer stop.
        /// Main thread only.
        /// </summary>
        public void UpdateTripodAmbience(bool hasTripod, Vector3 pos, bool paused, float dt)
        {
            if (_tripod == null) return;
            EnsureTripodSource();
            try
            {
                if (!ModSettings.TripodSoundEnabled)
                {
                    // Switched off mid-clip: stop what is playing rather than letting it finish.
                    if (_tripodSource.isPlaying) _tripodSource.Stop();
                    _tripodPausedMidClip = false;
                    _tripodTimer = ModConfig.TripodStepIntervalSeconds;
                    return;
                }

                // Followed live, so the slider takes effect without waiting for the next clip.
                _tripodSource.volume = ModConfig.TripodSoundVolume * ModSettings.SoundVolume;

                if (paused)
                {
                    // While paused: pause anything playing and leave the timer alone.
                    if (_tripodSource.isPlaying) { _tripodSource.Pause(); _tripodPausedMidClip = true; }
                    return;
                }

                // On resuming: continue the clip that was paused.
                if (_tripodPausedMidClip) { _tripodSource.UnPause(); _tripodPausedMidClip = false; }

                if (!hasTripod)
                {
                    if (_tripodSource.isPlaying) _tripodSource.Stop();
                    _tripodTimer = ModConfig.TripodStepIntervalSeconds; // left full so the next appearance fires at once
                    return;
                }

                _tripodSource.transform.position = pos; // follows the representative tripod, in 3D

                _tripodTimer += dt;
                if (_tripodTimer >= ModConfig.TripodStepIntervalSeconds)
                {
                    if (!_tripodSource.isPlaying)
                    {
                        _tripodTimer = 0f;
                        _tripodSource.clip = _tripod;
                        _tripodSource.Play();
                    }
                    else
                    {
                        // The previous one is still playing and they must not overlap, so wait
                        // for it and hold the timer at its maximum to fire the moment it ends.
                        _tripodTimer = ModConfig.TripodStepIntervalSeconds;
                    }
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("SoundLoader.UpdateTripodAmbience error: " + e);
            }
        }

        private void EnsureTripodSource()
        {
            if (_tripodSource != null) return;
            var go = new GameObject("AlienInvasion_TripodAmbience");
            go.transform.parent = transform;
            _tripodSource = go.AddComponent<AudioSource>();
            _tripodSource.spatialBlend = 1f; // 3D
            _tripodSource.rolloffMode = AudioRolloffMode.Linear;
            _tripodSource.minDistance = 200f;
            _tripodSource.maxDistance = 4000f;
            _tripodSource.volume = ModConfig.TripodSoundVolume;
            _tripodSource.loop = false;
        }
    }
}
