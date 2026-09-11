using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HC.Core
{
    /// <summary>
    /// Quản lý âm thanh toàn game: 1 AudioSource cho nhạc nền (BGM, loop) và 1 cho hiệu ứng (SFX, one-shot).
    /// Singleton — truy cập qua <see cref="Instance"/>, tự tạo nếu chưa có trong scene, sống qua các scene.
    /// Trạng thái mute và volume lưu bằng PlayerPrefs, tự load lại khi mở game.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class AudioManager : MonoBehaviour
    {
        private const string PrefMuted = "audio.muted";
        private const string PrefMusicMuted = "audio.music.muted";
        private const string PrefSfxMuted = "audio.sfx.muted";
        private const string PrefMusicVolume = "audio.music.volume";
        private const string PrefSfxVolume = "audio.sfx.volume";

        [Header("Sources (để trống thì tự tạo lúc Awake)")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("Mặc định cho lần chạy đầu (chưa có PlayerPrefs)")]
        [Range(0f, 1f)][SerializeField] private float defaultMusicVolume = 0.6f;
        [Range(0f, 1f)][SerializeField] private float defaultSfxVolume = 1f;

        [Header("Tuỳ chọn")]
        [Tooltip("Thời gian fade mặc định khi đổi BGM (giây). 0 = đổi ngay.")]
        [Min(0f)][SerializeField] private float defaultBgmFade = 0.5f;
        [Tooltip("Cùng 1 clip gọi lại trong khoảng này sẽ bị bỏ qua, tránh chồng tiếng vỡ loa " +
                 "khi nhiều object cùng phát 1 SFX trong 1 frame. 0 = tắt.")]
        [Min(0f)][SerializeField] private float sfxRetriggerCooldown = 0.04f;

        /// <summary>Bắn ra mỗi lần mute/volume đổi, để UI settings tự đồng bộ lại.</summary>
        public event Action OnAudioSettingsChanged;

        private readonly Dictionary<AudioClip, float> _lastPlayedAt = new Dictionary<AudioClip, float>();
        private Coroutine _bgmRoutine;

        private bool _muted;
        private bool _musicMuted;
        private bool _sfxMuted;
        private float _musicVolume;
        private float _sfxVolume;

        private static AudioManager _instance;
        private static bool _isQuitting;

        public static AudioManager Instance
        {
            get
            {
                if (_isQuitting) return null;
                if (_instance != null) return _instance;

                _instance = FindFirstObjectByType<AudioManager>();
                if (_instance == null)
                {
                    var go = new GameObject("[AudioManager]");
                    _instance = go.AddComponent<AudioManager>();
                }
                return _instance;
            }
        }

        // Enter Play Mode không reload domain => static không tự reset. Dòng này lo việc đó.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _isQuitting = false;
        }

        /// <summary>Mute tổng (cả nhạc lẫn SFX). Đây là cái nút loa trên HUD.</summary>
        public bool IsMuted
        {
            get => _muted;
            set
            {
                if (_muted == value) return;
                _muted = value;
                PlayerPrefs.SetInt(PrefMuted, value ? 1 : 0);
                PlayerPrefs.Save();
                ApplySettings();
            }
        }

        public bool IsMusicMuted
        {
            get => _musicMuted;
            set
            {
                if (_musicMuted == value) return;
                _musicMuted = value;
                PlayerPrefs.SetInt(PrefMusicMuted, value ? 1 : 0);
                PlayerPrefs.Save();
                ApplySettings();
            }
        }

        public bool IsSfxMuted
        {
            get => _sfxMuted;
            set
            {
                if (_sfxMuted == value) return;
                _sfxMuted = value;
                PlayerPrefs.SetInt(PrefSfxMuted, value ? 1 : 0);
                PlayerPrefs.Save();
                ApplySettings();
            }
        }

        public float MusicVolume
        {
            get => _musicVolume;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(_musicVolume, value)) return;
                _musicVolume = value;
                PlayerPrefs.SetFloat(PrefMusicVolume, value);
                PlayerPrefs.Save();
                ApplySettings();
            }
        }

        public float SfxVolume
        {
            get => _sfxVolume;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(_sfxVolume, value)) return;
                _sfxVolume = value;
                PlayerPrefs.SetFloat(PrefSfxVolume, value);
                PlayerPrefs.Save();
                ApplySettings();
            }
        }

        public AudioClip CurrentBgm => bgmSource != null ? bgmSource.clip : null;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            EnsureSources();
            LoadSettings();
            ApplySettings();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        private void EnsureSources()
        {
            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.loop = true;
                bgmSource.playOnAwake = false;
            }
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.loop = false;
                sfxSource.playOnAwake = false;
            }

            // SFX/BGM là âm thanh 2D, không muốn bị attenuate theo khoảng cách tới listener.
            bgmSource.spatialBlend = 0f;
            sfxSource.spatialBlend = 0f;
        }

        private void LoadSettings()
        {
            _muted = PlayerPrefs.GetInt(PrefMuted, 0) == 1;
            _musicMuted = PlayerPrefs.GetInt(PrefMusicMuted, 0) == 1;
            _sfxMuted = PlayerPrefs.GetInt(PrefSfxMuted, 0) == 1;
            _musicVolume = PlayerPrefs.GetFloat(PrefMusicVolume, defaultMusicVolume);
            _sfxVolume = PlayerPrefs.GetFloat(PrefSfxVolume, defaultSfxVolume);
        }

        private void ApplySettings()
        {
            if (bgmSource != null)
            {
                bgmSource.mute = _muted || _musicMuted;
                // Chỉ set volume khi không đang fade, tránh giật cục giữa chừng bản fade.
                if (_bgmRoutine == null) bgmSource.volume = _musicVolume;
            }
            if (sfxSource != null)
            {
                sfxSource.mute = _muted || _sfxMuted;
                sfxSource.volume = _sfxVolume;
            }

            OnAudioSettingsChanged?.Invoke();
        }

        /// <summary>Bật/tắt tiếng toàn bộ. Trả về trạng thái mute sau khi đổi.</summary>
        public bool ToggleMute()
        {
            IsMuted = !_muted;
            return _muted;
        }

        public bool ToggleMusic()
        {
            IsMusicMuted = !_musicMuted;
            return _musicMuted;
        }

        public bool ToggleSfx()
        {
            IsSfxMuted = !_sfxMuted;
            return _sfxMuted;
        }

        /// <summary>Phát 1 hiệu ứng. Nhiều SFX chồng nhau được vì dùng PlayOneShot.</summary>
        public void PlaySFX(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;
            if (_muted || _sfxMuted) return;
            if (sfxSource == null) EnsureSources();

            // Cùng 1 clip phát dồn trong vài ms chỉ tạo ra tiếng vỡ + to gấp đôi, không nghe rõ hơn.
            if (sfxRetriggerCooldown > 0f)
            {
                float now = Time.unscaledTime;
                if (_lastPlayedAt.TryGetValue(clip, out var last) && now - last < sfxRetriggerCooldown) return;
                _lastPlayedAt[clip] = now;
            }

            sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }

        /// <summary>Phát nhạc nền (loop). Gọi lại với đúng clip đang chạy thì bỏ qua, không restart bài.</summary>
        public void PlayBGM(AudioClip clip)
        {
            PlayBGM(clip, defaultBgmFade);
        }

        /// <param name="fadeDuration">Thời gian fade out bài cũ / fade in bài mới. 0 = đổi ngay.</param>
        public void PlayBGM(AudioClip clip, float fadeDuration)
        {
            if (bgmSource == null) EnsureSources();
            if (clip == null)
            {
                StopBGM(fadeDuration);
                return;
            }

            // Đổi scene mà vẫn bài đó thì để nó chạy tiếp, đừng cắt ngang rồi phát lại từ đầu.
            if (bgmSource.clip == clip && bgmSource.isPlaying) return;

            if (_bgmRoutine != null) StopCoroutine(_bgmRoutine);

            if (fadeDuration <= 0f)
            {
                bgmSource.clip = clip;
                bgmSource.volume = _musicVolume;
                bgmSource.Play();
                _bgmRoutine = null;
                return;
            }

            _bgmRoutine = StartCoroutine(SwapBgmRoutine(clip, fadeDuration));
        }

        public void StopBGM(float fadeDuration = 0f)
        {
            if (bgmSource == null) return;
            if (_bgmRoutine != null) StopCoroutine(_bgmRoutine);

            if (fadeDuration <= 0f)
            {
                bgmSource.Stop();
                bgmSource.clip = null;
                _bgmRoutine = null;
                return;
            }

            _bgmRoutine = StartCoroutine(SwapBgmRoutine(null, fadeDuration));
        }

        public void PauseBGM()
        {
            if (bgmSource != null && bgmSource.isPlaying) bgmSource.Pause();
        }

        public void ResumeBGM()
        {
            if (bgmSource != null && bgmSource.clip != null && !bgmSource.isPlaying) bgmSource.UnPause();
        }

        private IEnumerator SwapBgmRoutine(AudioClip next, float fadeDuration)
        {
            // unscaledDeltaTime: lúc pause menu Time.timeScale = 0, dùng deltaTime thường sẽ đứng fade giữa chừng.
            if (bgmSource.isPlaying)
            {
                float from = bgmSource.volume;
                float t = 0f;
                while (t < fadeDuration)
                {
                    t += Time.unscaledDeltaTime;
                    bgmSource.volume = Mathf.Lerp(from, 0f, t / fadeDuration);
                    yield return null;
                }
                bgmSource.Stop();
            }

            bgmSource.clip = next;
            if (next == null)
            {
                bgmSource.volume = _musicVolume;
                _bgmRoutine = null;
                yield break;
            }

            bgmSource.volume = 0f;
            bgmSource.Play();

            float t2 = 0f;
            while (t2 < fadeDuration)
            {
                t2 += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(0f, _musicVolume, t2 / fadeDuration);
                yield return null;
            }

            bgmSource.volume = _musicVolume;
            _bgmRoutine = null;
        }
    }
}
