using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BookBound
{
    /// <summary>
    /// Subscribes to service events and updates hearts, score, timer. Read-only - never mutates game
    /// state (plan.md section 9). Score rolls and punches rather than snapping, the timer eases through
    /// calm/amber/red zones and pulses under threat, and heart loss gets a shake+flash - all through
    /// UITween so AnimationSettings.enabled can flatten everything to instant snaps. Score/timer render
    /// through DigitStrip (sliced bitmap-numeral sprites), not a font, to match the HUD art style.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        private enum TimerZone { Calm, Amber, Red, RedFast }

        [Tooltip("One Image per starting heart, left to right.")]
        [SerializeField] private Image[] hearts;

        [SerializeField] private DigitStrip scoreDigits;
        [SerializeField] private RectTransform scorePlaque;
        [SerializeField] private DigitStrip timerDigits;
        [SerializeField] private RectTransform timerGroup;
        [SerializeField] private SpriteText bookcaseText;
        [SerializeField] private ShelfTimer shelfTimer;

        private int _lastWholeSecond = -1;
        private int _displayedScore;
        private bool _nextRollIsBonus;
        private Coroutine _scoreRollCoroutine;
        private TimerZone _timerZone = TimerZone.Calm;

        public void BindScore(ScoreService score)
        {
            score.OnScoreChanged += UpdateScore;
            _displayedScore = score.Score;
            scoreDigits.SetText(score.Score.ToString("00000"));
        }

        public void BindLives(LivesService lives)
        {
            lives.OnHeartLost += UpdateHearts;

            // A restart re-binds against a fresh LivesService, so every heart has to come back -
            // Show() undoes whatever the previous run's losses did to scale and alpha.
            for (int i = 0; i < hearts.Length; i++) Show(hearts[i], i < lives.Hearts);

            var s = AnimationSettings.Instance.hearts;
            if (!AnimationSettings.Instance.enabled) return;
            foreach (Image heart in hearts)
                UITween.PulseScaleLoop((RectTransform)heart.transform, s.idleBreatheScale, s.idleBreatheDuration);
        }

        public void SetBookcaseNumber(int number, ShelfType shelfType = ShelfType.Standard)
        {
            if (bookcaseText == null) return;
            string typeName = shelfType switch
            {
                ShelfType.Delivery => " DELIVERY",
                ShelfType.Repair => " REPAIR",
                _ => string.Empty,
            };
            bookcaseText.SetText($"BOOKCASE {number}{typeName}");
        }

        private static void Show(Image heart, bool visible)
        {
            if (heart == null) return;
            UITween.Kill((RectTransform)heart.transform);
            heart.transform.localScale = Vector3.one;
            heart.enabled = visible;
            heart.color = Color.white;
        }

        /// <summary>Call right before awarding a score gain that should read as a big deal (the
        /// bookcase-clear time bonus): the next score roll uses the bonus duration/punch/gold tint
        /// instead of the everyday per-slot roll.</summary>
        public void AnnounceBonusIncoming() => _nextRollIsBonus = true;

        private void Awake()
        {
            if (shelfTimer != null) shelfTimer.OnExpired += HandleExpired;
        }

        private void OnDestroy()
        {
            if (shelfTimer != null) shelfTimer.OnExpired -= HandleExpired;
        }

        private void Update()
        {
            if (shelfTimer == null || shelfTimer.Duration <= 0f) return;

            float remaining = Mathf.Max(0f, shelfTimer.TimeRemaining);
            int totalSeconds = Mathf.CeilToInt(remaining);
            timerDigits.SetText($"{totalSeconds / 60:00}:{totalSeconds % 60:00}");

            var s = AnimationSettings.Instance.timer;
            TimerZone zone =
                remaining <= s.doublePulseThresholdSeconds ? TimerZone.RedFast :
                remaining <= s.redThresholdSeconds ? TimerZone.Red :
                remaining <= s.amberThresholdSeconds ? TimerZone.Amber :
                TimerZone.Calm;

            if (!shelfTimer.IsPaused && zone != _timerZone)
            {
                _timerZone = zone;
                ApplyTimerZone(zone);
            }

            bool warning = zone != TimerZone.Calm && !shelfTimer.IsPaused;
            if (!warning)
            {
                _lastWholeSecond = -1;
                return;
            }

            if (totalSeconds != _lastWholeSecond)
            {
                _lastWholeSecond = totalSeconds;
                AudioManager.PlayTimerTick();
            }
        }

        private void ApplyTimerZone(TimerZone zone)
        {
            var s = AnimationSettings.Instance.timer;
            Color color = zone switch
            {
                TimerZone.Amber => s.amberColor,
                TimerZone.Red => s.redColor,
                TimerZone.RedFast => s.redColor,
                _ => s.calmColor,
            };
            timerDigits.SetColor(color);

            if (timerGroup == null) return;
            if (zone == TimerZone.Red) UITween.PulseScaleLoop(timerGroup, s.pulseScale, s.pulseDuration);
            else if (zone == TimerZone.RedFast) UITween.PulseScaleLoop(timerGroup, s.pulseScale, s.fastPulseDuration);
            else
            {
                UITween.Kill(timerGroup);
                timerGroup.localScale = Vector3.one;
            }
        }

        /// <summary>Flash on expiry, ahead of GameManager's heart-loss sequence.</summary>
        private void HandleExpired()
        {
            var s = AnimationSettings.Instance.timer;
            if (timerGroup != null)
            {
                UITween.Kill(timerGroup);
                timerGroup.localScale = Vector3.one;
            }
            timerDigits.SetColor(Color.white);
            StartCoroutine(FlashBackToCalm(s.expiryFlashDuration));
            _timerZone = TimerZone.Calm; // re-evaluated fresh once the next ShelfTimer.Reset() runs
        }

        private IEnumerator FlashBackToCalm(float duration)
        {
            yield return new WaitForSecondsRealtime(duration);
            timerDigits.SetColor(AnimationSettings.Instance.timer.calmColor);
        }

        private void UpdateScore(int score)
        {
            bool bonus = _nextRollIsBonus;
            _nextRollIsBonus = false;

            if (!AnimationSettings.Instance.enabled)
            {
                if (_scoreRollCoroutine != null) StopCoroutine(_scoreRollCoroutine);
                _displayedScore = score;
                scoreDigits.SetText(score.ToString("00000"));
                return;
            }

            if (_scoreRollCoroutine != null) StopCoroutine(_scoreRollCoroutine);
            _scoreRollCoroutine = StartCoroutine(RollScore(_displayedScore, score, bonus));
        }

        private IEnumerator RollScore(int from, int to, bool bonus)
        {
            var s = AnimationSettings.Instance.score;
            float duration = bonus ? s.bonusRollDuration : s.rollDuration;
            float peak = bonus ? s.bonusPunchScale : s.punchScale;
            float punchDuration = bonus ? duration : s.punchDuration;

            UITween.PunchScale(scorePlaque, peak, punchDuration, Vector3.one, bonus ? EaseType.OutBack : EaseType.OutQuad);

            if (bonus) scoreDigits.SetColor(s.bonusTintColor);

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                int value = Mathf.RoundToInt(Mathf.LerpUnclamped(from, to, Easing.Evaluate(EaseType.OutQuad, t / duration)));
                scoreDigits.SetText(value.ToString("00000"));
                yield return null;
            }
            scoreDigits.SetText(to.ToString("00000"));
            _displayedScore = to;

            if (bonus) scoreDigits.SetColor(Color.white);
            _scoreRollCoroutine = null;
        }

        /// <summary>
        /// Hearts are real sprites now, so plan.md section 5's per-heart feedback attaches to the one
        /// that was actually lost: the rightmost still-visible heart shakes, then swells and fades out
        /// and is gone (the alternative - dimming it in place - was considered and not chosen). The
        /// survivors pulse once to draw the eye to what's left.
        /// </summary>
        private void UpdateHearts(int remaining)
        {
            int lostIndex = Mathf.Clamp(remaining, 0, hearts.Length - 1);

            // Anything below the lost heart is already gone; make sure state matches even if a
            // previous animation was interrupted by a restart mid-flight.
            for (int i = remaining; i < hearts.Length; i++)
                if (i != lostIndex) Show(hearts[i], false);

            if (!AnimationSettings.Instance.enabled)
            {
                Show(hearts[lostIndex], false);
                return;
            }

            StartCoroutine(HeartLossFeedback(lostIndex, remaining));
        }

        private IEnumerator HeartLossFeedback(int lostIndex, int remaining)
        {
            var s = AnimationSettings.Instance.hearts;
            Image lost = hearts[lostIndex];
            var rt = (RectTransform)lost.transform;
            UITween.Kill(rt);
            Vector2 basePos = rt.anchoredPosition;

            float t = 0f;
            while (t < s.shakeDuration)
            {
                t += Time.unscaledDeltaTime;
                float damp = 1f - t / s.shakeDuration;
                rt.anchoredPosition = basePos + new Vector2(Mathf.Sin(t * 40f) * s.shakeAmplitude * damp, 0f);
                yield return null;
            }
            rt.anchoredPosition = basePos;

            // Swell and fade, then it's gone for good.
            UITween.ScaleTo(rt, Vector3.one * s.lossScale, s.lossFadeDuration, EaseType.OutQuad);
            UITween.FadeTo(lost, 0f, s.lossFadeDuration, EaseType.OutQuad, () => Show(lost, false));

            // Survivors pulse in sequence, drawing the eye across what's left.
            for (int i = 0; i < remaining; i++)
            {
                var survivor = (RectTransform)hearts[i].transform;
                yield return new WaitForSecondsRealtime(s.remainingStagger);
                UITween.PunchScale(survivor, s.remainingPulseScale, s.remainingPulseDuration, Vector3.one,
                    EaseType.OutBack,
                    () => UITween.PulseScaleLoop(survivor, s.idleBreatheScale, s.idleBreatheDuration));
            }
        }
    }
}
