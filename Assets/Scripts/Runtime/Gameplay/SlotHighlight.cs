using UnityEngine;
using UnityEngine.UI;

namespace BookBound
{
    /// <summary>
    /// The drop-target highlight tint, on a child Image of a slot so it can be raised above the slot's
    /// occupant - a swap highlight has to read on an already-full slot, and a child added before the
    /// book would otherwise render underneath it (plan.md section 13: swap clarity). Fades in/out and
    /// pulses via UITween so "empty slot" and "swap" read as visually distinct at a glance: swap uses a
    /// different tint and a faster, tighter pulse than a plain valid-drop highlight.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class SlotHighlight : MonoBehaviour
    {
        private static readonly Color ValidTint = new Color(0.35f, 1f, 0.5f, 0.35f);
        private static readonly Color SwapTint = new Color(1f, 0.62f, 0.12f, 0.5f);

        private Image _image;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _image.raycastTarget = false;
            _image.enabled = false;
        }

        public void Show(bool swap)
        {
            var s = AnimationSettings.Instance.highlight;
            Color target = swap ? SwapTint : ValidTint;

            _image.enabled = true;
            transform.SetAsLastSibling();

            Color start = target;
            start.a = 0f;
            _image.color = start;
            UITween.FadeTo(_image, target.a, s.fadeInDuration, EaseType.OutQuad);

            float peak = swap ? s.swapPulseScale : s.validPulseScale;
            float period = swap ? s.swapPulseDuration : s.validPulseDuration;
            UITween.PulseScaleLoop(transform, peak, period);
        }

        public void Hide()
        {
            var s = AnimationSettings.Instance.highlight;
            UITween.Kill(transform); // stops the pulse loop
            transform.localScale = Vector3.one;
            UITween.FadeTo(_image, 0f, s.fadeOutDuration, EaseType.OutQuad, () => _image.enabled = false);
        }
    }
}
