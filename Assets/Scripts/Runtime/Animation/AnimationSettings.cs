using UnityEngine;

namespace BookBound
{
    /// <summary>
    /// Every tunable duration/scale value used by the UI animation layer, in one Inspector-editable
    /// asset (Assets/Resources/AnimationSettings.asset) so it can be tuned by feel without recompiling.
    /// <see cref="enabled"/> is the single master switch: every animated component reads it and, when
    /// off, snaps straight to the end state instead of tweening - the game must stay fully playable
    /// and correct either way.
    /// </summary>
    [CreateAssetMenu(menuName = "BookBound/Animation Settings", fileName = "AnimationSettings")]
    public class AnimationSettings : ScriptableObject
    {
        [System.Serializable]
        public class ButtonSettings
        {
            public float hoverScale = 1.05f;
            public float hoverDuration = 0.12f;
            public float pressScale = 0.95f;
            public float pressDuration = 0.06f;
            public float releaseOvershootScale = 1.05f;
            public float releaseDuration = 0.10f;
            public float hoverTintBrighten = 1.15f;
            public float disabledAlpha = 0.5f;
            public float disabledDuration = 0.15f;
        }

        [System.Serializable]
        public class PanelSettings
        {
            public float openFromScale = 0.9f;
            public float openDuration = 0.25f;
            public float backdropAlpha = 0.6f;
            public float backdropDuration = 0.2f;
            public float closeToScale = 0.95f;
            public float closeDuration = 0.15f;
            public float childStaggerDelay = 0.04f;
            public float childFadeDuration = 0.15f;
        }

        [System.Serializable]
        public class ScoreSettings
        {
            public float rollDuration = 0.4f;
            public float punchScale = 1.15f;
            public float punchDuration = 0.25f;
            public float bonusRollDuration = 0.8f;
            public float bonusPunchScale = 1.3f;
            public Color bonusTintColor = new Color(1f, 0.84f, 0.2f, 1f);
        }

        [System.Serializable]
        public class TimerSettings
        {
            public float amberThresholdSeconds = 20f;
            public float redThresholdSeconds = 10f;
            public float doublePulseThresholdSeconds = 5f;
            public float colorTweenDuration = 0.5f;
            public float pulseScale = 1.03f;
            public float pulseDuration = 0.5f;
            public float fastPulseDuration = 0.25f;
            public float expiryFlashDuration = 0.15f;
            public Color calmColor = new Color(0.9f, 0.75f, 0.2f, 1f);
            public Color amberColor = new Color(0.95f, 0.55f, 0.15f, 1f);
            public Color redColor = new Color(0.9f, 0.2f, 0.15f, 1f);
        }

        [System.Serializable]
        public class HeartSettings
        {
            public float shakeAmplitude = 8f;
            public float shakeDuration = 0.3f;
            public float lossScale = 1.4f;
            public float lossFadeDuration = 0.25f;
            public float remainingPulseScale = 1.15f;
            public float remainingPulseDuration = 0.3f;
            public float remainingStagger = 0.06f;
            public float idleBreatheScale = 1.02f;
            public float idleBreatheDuration = 2.0f;
        }

        [System.Serializable]
        public class TableBookSettings
        {
            public float hoverLift = 6f;
            public float hoverScale = 1.04f;
            public float hoverDuration = 0.1f;
            public float feedInDuration = 0.35f;
            public float shiftDuration = 0.25f;
            public float shiftStagger = 0.03f;
        }

        [System.Serializable]
        public class DragSettings
        {
            public float pickupScale = 1.08f;
            public float pickupAlpha = 0.65f;
            public float pickupDuration = 0.08f;
            public float maxLeanDegrees = 5f;
            public float leanDamp = 10f;
            public float leanSensitivity = 0.6f;
            public float correctPunchScale = 1.2f;
            public float correctDuration = 0.3f;
            public Color correctFlashColor = new Color(1.3f, 1.05f, 0.6f, 1f);
            public float neutralPunchScale = 1.08f;
            public float neutralDuration = 0.15f;
            public float swapDuration = 0.2f;
            public float returnDuration = 0.25f;
        }

        [System.Serializable]
        public class HighlightSettings
        {
            public float fadeInDuration = 0.1f;
            public float fadeOutDuration = 0.1f;
            public float validPulseScale = 1.05f;
            public float validPulseDuration = 0.8f;
            public float swapPulseScale = 1.08f;
            public float swapPulseDuration = 0.3f;
        }

        [System.Serializable]
        public class BookcaseSettings
        {
            public float spawnSlideDistance = 40f;
            public float spawnDuration = 0.4f;
            public float prefillStagger = 0.02f;
        }

        [System.Serializable]
        public class ScreenTransitionSettings
        {
            public float fadeOutDuration = 0.3f;
            public float holdDuration = 0.1f;
            public float fadeInDuration = 0.3f;
        }

        [Header("Master switch - false leaves the game fully playable, everything snaps instantly")]
        public bool enabled = true;

        [Header("Buttons")] public ButtonSettings buttons = new ButtonSettings();
        [Header("Panels & Overlays")] public PanelSettings panels = new PanelSettings();
        [Header("Score")] public ScoreSettings score = new ScoreSettings();
        [Header("Timer Bar")] public TimerSettings timer = new TimerSettings();
        [Header("Hearts")] public HeartSettings hearts = new HeartSettings();
        [Header("Table Books")] public TableBookSettings tableBooks = new TableBookSettings();
        [Header("Drag & Placement")] public DragSettings drag = new DragSettings();
        [Header("Slot Highlight")] public HighlightSettings highlight = new HighlightSettings();
        [Header("Bookcase Transitions")] public BookcaseSettings bookcase = new BookcaseSettings();
        [Header("Screen Transitions")] public ScreenTransitionSettings screen = new ScreenTransitionSettings();

        private static AnimationSettings _instance;

        /// <summary>Loaded once from Resources and cached. Falls back to an in-memory default instance
        /// (animations on, stock values) if the asset is missing, so a deleted/misconfigured asset
        /// degrades to "animations work with default numbers" rather than null-refing everywhere.</summary>
        public static AnimationSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<AnimationSettings>("AnimationSettings");
                    if (_instance == null)
                        _instance = CreateInstance<AnimationSettings>();
                }
                return _instance;
            }
        }
    }
}
