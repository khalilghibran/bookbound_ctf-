using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BookBound
{
    /// <summary>
    /// Reusable open/close for a full-screen panel (title, pause, game over, transition banner):
    /// backdrop fades in on its own timing while a separate "content" group (the panel's text/buttons)
    /// pops in with a slight overshoot, then its direct children fade in one at a time. Close is the
    /// mirror, faster and without the overshoot so dismissal feels immediate. Replaces bare
    /// GameObject.SetActive calls so ShellUI doesn't have to know any of this.
    /// </summary>
    public class AnimatedPanel : MonoBehaviour
    {
        [Tooltip("The full-screen tint Image behind the content - usually this panel's own Image.")]
        [SerializeField] private Graphic backdrop;

        [Tooltip("Child that scales/fades in as a whole (holds everything except the backdrop).")]
        [SerializeField] private RectTransform content;

        // CanvasGroups, not Graphics: a SpriteText label is a pool of child Images with no single
        // Graphic to fade, and a CanvasGroup fades everything under it in one go.
        [Tooltip("Direct children of content, in the order they should stagger in.")]
        [SerializeField] private CanvasGroup[] staggerChildren;

        private float _backdropTargetAlpha;
        private float[] _childTargetAlphas;

        private void Awake()
        {
            if (backdrop != null) _backdropTargetAlpha = backdrop.color.a;

            _childTargetAlphas = new float[staggerChildren?.Length ?? 0];
            for (int i = 0; i < _childTargetAlphas.Length; i++)
                _childTargetAlphas[i] = staggerChildren[i] != null ? staggerChildren[i].alpha : 1f;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            StopAllCoroutines();
            var settings = AnimationSettings.Instance;
            AnimationSettings.PanelSettings s = settings.panels;

            if (!settings.enabled)
            {
                if (content != null) content.localScale = Vector3.one;
                SetAlphaInstant(backdrop, _backdropTargetAlpha);
                for (int i = 0; i < _childTargetAlphas.Length; i++) SetAlphaInstant(staggerChildren[i], _childTargetAlphas[i]);
                return;
            }

            SetAlphaInstant(backdrop, 0f);
            if (backdrop != null) UITween.FadeTo(backdrop, _backdropTargetAlpha, s.backdropDuration, EaseType.OutQuad);

            if (content != null)
            {
                content.localScale = Vector3.one * s.openFromScale;
                UITween.ScaleTo(content, Vector3.one, s.openDuration, EaseType.OutBack);
            }

            for (int i = 0; i < _childTargetAlphas.Length; i++)
            {
                CanvasGroup g = staggerChildren[i];
                if (g == null) continue;
                SetAlphaInstant(g, 0f);
                StartCoroutine(DelayedFade(g, _childTargetAlphas[i], i * s.childStaggerDelay, s.childFadeDuration));
            }
        }

        public void Hide()
        {
            if (!gameObject.activeSelf) return;
            StopAllCoroutines();
            var settings = AnimationSettings.Instance;
            AnimationSettings.PanelSettings s = settings.panels;

            if (!settings.enabled)
            {
                gameObject.SetActive(false);
                return;
            }

            if (content != null) UITween.ScaleTo(content, Vector3.one * s.closeToScale, s.closeDuration, EaseType.InQuad);
            if (backdrop != null) UITween.FadeTo(backdrop, 0f, s.closeDuration, EaseType.InQuad);
            StartCoroutine(DeactivateAfter(s.closeDuration));
        }

        private IEnumerator DelayedFade(CanvasGroup g, float targetAlpha, float delay, float duration)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            UITween.FadeTo(g, targetAlpha, duration, EaseType.OutQuad);
        }

        private IEnumerator DeactivateAfter(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            gameObject.SetActive(false);
        }

        private static void SetAlphaInstant(Graphic g, float alpha)
        {
            if (g == null) return;
            Color c = g.color;
            c.a = alpha;
            g.color = c;
        }

        private static void SetAlphaInstant(CanvasGroup g, float alpha)
        {
            if (g != null) g.alpha = alpha;
        }
    }
}
