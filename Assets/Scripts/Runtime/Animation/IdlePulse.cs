using UnityEngine;

namespace BookBound
{
    /// <summary>
    /// A small, purely decorative continuous scale breathing loop - drop onto anything that should
    /// read as "alive" while sitting idle (the title-screen logo, say) without needing a bespoke
    /// animation. Respects AnimationSettings.enabled the same way every other UITween call does.
    /// </summary>
    public class IdlePulse : MonoBehaviour
    {
        [SerializeField] private float peakScale = 1.015f;
        [SerializeField] private float period = 3f;

        private void OnEnable()
        {
            UITween.PulseScaleLoop(transform, peakScale, period);
        }

        private void OnDisable()
        {
            UITween.Kill(transform);
            transform.localScale = Vector3.one;
        }
    }
}
