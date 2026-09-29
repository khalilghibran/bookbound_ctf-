using System;
using UnityEngine;
using UnityEngine.UI;

namespace BookBound
{
    /// <summary>
    /// The Options screen: master / music / SFX volume, restore defaults, and a way back.
    ///
    /// Owns no audio state - it reads and writes <see cref="SettingsService"/>, and AudioManager
    /// reacts to that on its own. Reachable from both the title screen and the pause menu, so it
    /// raises <see cref="OnClosed"/> rather than deciding where "back" goes; GameManager knows which
    /// screen it came from.
    ///
    /// This component deliberately does NOT live on the panel it drives. The panel starts inactive,
    /// and a component on an inactive object never gets Awake, so its button listeners would only
    /// hook up on first open - after the open had already been requested.
    /// </summary>
    public class OptionsPanelController : MonoBehaviour
    {
        [SerializeField] private AnimatedPanel panel;

        [SerializeField] private VolumeSlider masterSlider;
        [SerializeField] private VolumeSlider musicSlider;
        [SerializeField] private VolumeSlider sfxSlider;

        [SerializeField] private SpriteText masterReadout;
        [SerializeField] private SpriteText musicReadout;
        [SerializeField] private SpriteText sfxReadout;

        [SerializeField] private Button restoreDefaultsButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button closeButton;

        public event Action OnClosed;

        public bool IsOpen => panel != null && panel.gameObject.activeSelf;

        private void Awake()
        {
            masterSlider.OnValueChanged += v => { SettingsService.Master = v; RefreshReadouts(); };
            musicSlider.OnValueChanged += v => { SettingsService.Music = v; RefreshReadouts(); };
            sfxSlider.OnValueChanged += v => { SettingsService.Sfx = v; RefreshReadouts(); };

            restoreDefaultsButton.onClick.AddListener(() =>
            {
                AudioManager.PlayUiClick();
                SettingsService.RestoreDefaults();
                PullFromSettings();
            });

            backButton.onClick.AddListener(Close);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        public void Show()
        {
            PullFromSettings();
            panel.Show();
        }

        public void Hide() => panel.Hide();

        private void Close()
        {
            AudioManager.PlayUiClick();
            panel.Hide();
            OnClosed?.Invoke();
        }

        /// <summary>Sliders are pushed without notify so restoring defaults doesn't loop back through
        /// OnValueChanged and rewrite what it just set.</summary>
        private void PullFromSettings()
        {
            masterSlider.SetValueWithoutNotify(SettingsService.Master);
            musicSlider.SetValueWithoutNotify(SettingsService.Music);
            sfxSlider.SetValueWithoutNotify(SettingsService.Sfx);
            RefreshReadouts();
        }

        private void RefreshReadouts()
        {
            // Bare numbers - the sheet has no % glyph, and at the end of a slider row the number
            // reads as a percentage without one.
            Set(masterReadout, SettingsService.Master);
            Set(musicReadout, SettingsService.Music);
            Set(sfxReadout, SettingsService.Sfx);
        }

        private static void Set(SpriteText readout, float value01)
        {
            if (readout != null) readout.SetText(Mathf.RoundToInt(value01 * 100f).ToString());
        }
    }
}
