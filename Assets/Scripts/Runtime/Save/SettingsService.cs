using System;
using UnityEngine;

namespace BookBound
{
    /// <summary>
    /// Player-facing audio settings, persisted in PlayerPrefs alongside <see cref="SaveService"/>.
    ///
    /// Master multiplies into both channels, matching the Options screen's three rows. Values are
    /// stored 0..1 and clamped on the way in, so a hand-edited pref can't push a source past 1 or
    /// negative. <see cref="OnChanged"/> is what AudioManager listens to - nothing else pokes at
    /// AudioSource volumes directly.
    /// </summary>
    public static class SettingsService
    {
        private const string MasterKey = "volMaster";
        private const string MusicKey = "volMusic";
        private const string SfxKey = "volSfx";

        public const float DefaultMaster = 0.7f;
        public const float DefaultMusic = 0.6f;
        public const float DefaultSfx = 0.8f;

        /// <summary>Raised whenever any volume changes, including a defaults restore.</summary>
        public static event Action OnChanged;

        public static float Master
        {
            get => PlayerPrefs.GetFloat(MasterKey, DefaultMaster);
            set => Set(MasterKey, value);
        }

        public static float Music
        {
            get => PlayerPrefs.GetFloat(MusicKey, DefaultMusic);
            set => Set(MusicKey, value);
        }

        public static float Sfx
        {
            get => PlayerPrefs.GetFloat(SfxKey, DefaultSfx);
            set => Set(SfxKey, value);
        }

        /// <summary>Final multiplier for the music source.</summary>
        public static float EffectiveMusic => Master * Music;

        /// <summary>Final multiplier for one-shot SFX.</summary>
        public static float EffectiveSfx => Master * Sfx;

        public static void RestoreDefaults()
        {
            PlayerPrefs.SetFloat(MasterKey, DefaultMaster);
            PlayerPrefs.SetFloat(MusicKey, DefaultMusic);
            PlayerPrefs.SetFloat(SfxKey, DefaultSfx);
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }

        private static void Set(string key, float value)
        {
            PlayerPrefs.SetFloat(key, Mathf.Clamp01(value));
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }
    }
}
