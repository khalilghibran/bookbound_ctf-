using UnityEngine;

namespace BookBound
{
    /// <summary>
    /// All SFX and music. One AudioSource for one-shots (PlayOneShot already mixes overlapping clips,
    /// so no pool is needed) plus a looping music source that ducks during the bookcase-clear fanfare.
    ///
    /// Exposed as a static Instance, matching DragLayerMarker: books are Instantiated at runtime from a
    /// prefab, so they can't be Inspector-wired to a scene object and need some static handle. Every
    /// call site is null-safe, so deleting the AudioManager leaves the game fully playable in silence.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource musicSource;

        [Header("SFX")]
        [SerializeField] private AudioClip pickup;
        [SerializeField] private AudioClip placeCorrect;
        [SerializeField] private AudioClip placeNeutral;
        [SerializeField] private AudioClip swap;
        [SerializeField] private AudioClip shelfClear;
        [SerializeField] private AudioClip heartLost;
        [SerializeField] private AudioClip timerTick;
        [SerializeField] private AudioClip uiClick;

        [Tooltip("Music level at 100% volume. SettingsService scales this, it does not replace it.")]
        [SerializeField] private float musicBaseVolume = 0.45f;

        private bool _ducking;

        private void Awake()
        {
            Instance = this;
            SettingsService.OnChanged += ApplyVolumes;
            if (musicSource != null)
            {
                musicSource.loop = true;
                musicSource.Play();
            }
            ApplyVolumes();
        }

        private void OnDestroy()
        {
            SettingsService.OnChanged -= ApplyVolumes;
            if (Instance == this) Instance = null;
        }

        /// <summary>Single point where settings reach the mixer. Called on start, on every settings
        /// change, and at both ends of a duck, so a volume slider moved mid-duck still lands right.</summary>
        private void ApplyVolumes()
        {
            if (musicSource == null) return;
            musicSource.volume = musicBaseVolume * SettingsService.EffectiveMusic * (_ducking ? 0.35f : 1f);
        }

        public static void PlayPickup() => Play(Instance?.pickup);
        public static void PlayPlaceCorrect() => Play(Instance?.placeCorrect);
        public static void PlayPlaceNeutral() => Play(Instance?.placeNeutral);
        public static void PlaySwap() => Play(Instance?.swap);
        public static void PlayHeartLost() => Play(Instance?.heartLost);
        public static void PlayTimerTick() => Play(Instance?.timerTick, 0.5f);
        public static void PlayUiClick() => Play(Instance?.uiClick);

        /// <summary>Ducks the music under the clear fanfare, then restores it (plan.md section 9).</summary>
        public static void PlayShelfClear()
        {
            Play(Instance?.shelfClear);
            if (Instance != null) Instance.DuckMusic();
        }

        private static void Play(AudioClip clip, float volume = 1f)
        {
            if (Instance == null || Instance.sfxSource == null || clip == null) return;
            Instance.sfxSource.PlayOneShot(clip, volume * SettingsService.EffectiveSfx);
        }

        private void DuckMusic()
        {
            if (musicSource == null) return;
            CancelInvoke(nameof(RestoreMusic));
            _ducking = true;
            ApplyVolumes();
            Invoke(nameof(RestoreMusic), 1.5f);
        }

        private void RestoreMusic()
        {
            _ducking = false;
            ApplyVolumes();
        }
    }
}
