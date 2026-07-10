using System.Collections;
using System.IO;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// Mod配置フォルダ/Sounds/*.wav を実行時にロードし再生する MonoBehaviour。
    /// WWW によるロードは1フレーム待ちが必要なためコルーチンで行う(SoundManager が生成・保持)。
    /// CSのUnity 5.6は実行時mp3デコード非対応(AudioType.MPEGがnull)のため、WAV(PCM)を読み込む。
    ///
    /// UFO飛来音: 使い捨ての2D AudioSourceで1回再生。
    /// トライポッド移動音: 単一の常設AudioSource(_tripodSource)で管理する。これにより
    ///   - 重ならない(前の再生が終わるまで次を鳴らさない)
    ///   - ゲーム一時停止中は Pause/UnPause で再生も止まる
    /// を満たす。再生は AudioSource を用いるため全てメインスレッド専用。
    /// </summary>
    public class SoundLoaderBehaviour : MonoBehaviour
    {
        private AudioClip _ufo;
        private AudioClip _tripod;
        private string _dir;

        // トライポッド移動音の常設ソース(重ねない・一時停止対応)
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
                ModConfig.LogError("SoundLoader: ファイルが見つかりません " + path);
                yield break;
            }

            string url = "file:///" + path.Replace("\\", "/");
            using (WWW www = new WWW(url))
            {
                yield return www;

                if (!string.IsNullOrEmpty(www.error))
                {
                    ModConfig.LogError("SoundLoader: WWWエラー " + fileName + " : " + www.error);
                    yield break;
                }

                AudioClip clip = www.GetAudioClip(false, false, AudioType.WAV);
                if (clip == null)
                {
                    ModConfig.LogError("SoundLoader: GetAudioClip が null " + fileName);
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
                    ModConfig.LogError("SoundLoader: ロード未完了 " + fileName + " state=" + clip.loadState);
                    yield break;
                }

                assign(clip);
                ModConfig.Log("SoundLoader: ロード完了 " + fileName + " (" + clip.length.ToString("0.0") + "s)");
            }
        }

        /// <summary>UFO飛来音を2D(常に一定音量)で1回再生する。メインスレッド専用。</summary>
        public void PlayUfoArrival()
        {
            if (_ufo == null) return;
            try
            {
                var go = new GameObject("AlienInvasion_SFX_UFO");
                var src = go.AddComponent<AudioSource>();
                src.clip = _ufo;
                src.volume = ModConfig.UfoSoundVolume;
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
        /// トライポッド移動音を毎フレーム駆動する。常設の単一ソースで再生するため重ならず、
        /// 一時停止中は再生も止まる(タイマーも進めない)。メインスレッド専用。
        /// </summary>
        public void UpdateTripodAmbience(bool hasTripod, Vector3 pos, bool paused, float dt)
        {
            if (_tripod == null) return;
            EnsureTripodSource();
            try
            {
                if (paused)
                {
                    // 時間停止中: 再生中なら一時停止し、タイマーも進めない
                    if (_tripodSource.isPlaying) { _tripodSource.Pause(); _tripodPausedMidClip = true; }
                    return;
                }

                // 停止解除: 一時停止していたクリップを再開
                if (_tripodPausedMidClip) { _tripodSource.UnPause(); _tripodPausedMidClip = false; }

                if (!hasTripod)
                {
                    if (_tripodSource.isPlaying) _tripodSource.Stop();
                    _tripodTimer = ModConfig.TripodStepIntervalSeconds; // 次の出現時に即発火できるよう満たしておく
                    return;
                }

                _tripodSource.transform.position = pos; // 代表トライポッドに追従(3D)

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
                        // まだ前回再生中(重ねない): 終わるまで待ち、終わり次第すぐ鳴らせるよう上限で保持
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
