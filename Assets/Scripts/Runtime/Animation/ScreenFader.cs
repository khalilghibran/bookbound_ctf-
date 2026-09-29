using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BookBound
{
    /// <summary>
    /// Full-screen fade to black / hold / fade in, used around state changes that tear down and rebuild
    /// the board (title -> game, game over -> restart - plan.md section 12). A plain wash, not an
    /// "entering element", so it eases out with InQuad and back in with OutQuad rather than the
    /// overshoot used for popping UI elements in.
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        [SerializeField] private Image image;

        /// <summary>Fades to black, invokes <paramref name="onBlack"/> while the screen is fully
        /// covered (the moment to swap game state with nothing visible), holds briefly, then fades back
        /// in. Non-blocking - returns immediately; the work happens over subsequent frames.</summary>
        public void FadeOutIn(Action onBlack)
        {
            StartCoroutine(Routine(onBlack));
        }

        private IEnumerator Routine(Action onBlack)
        {
            var settings = AnimationSettings.Instance;
            AnimationSettings.ScreenTransitionSettings s = settings.screen;
            image.raycastTarget = true;

            if (settings.enabled)
            {
                UITween.FadeTo(image, 1f, s.fadeOutDuration, EaseType.InQuad);
                yield return new WaitForSecondsRealtime(s.fadeOutDuration);
            }
            else
            {
                SetAlpha(1f);
            }

            onBlack?.Invoke();

            if (settings.enabled)
            {
                yield return new WaitForSecondsRealtime(s.holdDuration);
                UITween.FadeTo(image, 0f, s.fadeInDuration, EaseType.OutQuad);
                yield return new WaitForSecondsRealtime(s.fadeInDuration);
            }
            else
            {
                SetAlpha(0f);
            }

            image.raycastTarget = false;
        }

        private void SetAlpha(float a)
        {
            Color c = image.color;
            c.a = a;
            image.color = c;
        }
    }
}
