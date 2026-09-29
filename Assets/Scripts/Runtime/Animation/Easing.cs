using UnityEngine;

namespace BookBound
{
    public enum EaseType { Linear, OutQuad, InQuad, OutBack, InOutSine, InOutQuad }

    /// <summary>Standard easing curves (t in 0..1 in, eased 0..1 out). See the easing vocabulary table
    /// in the UI animation spec for which case uses which curve.</summary>
    public static class Easing
    {
        private const float BackC1 = 1.70158f;
        private const float BackC3 = BackC1 + 1f;

        public static float Evaluate(EaseType ease, float t)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case EaseType.OutQuad: return 1f - (1f - t) * (1f - t);
                case EaseType.InQuad: return t * t;
                case EaseType.OutBack:
                    float x = t - 1f;
                    return 1f + BackC3 * x * x * x + BackC1 * x * x;
                case EaseType.InOutSine: return -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;
                case EaseType.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                default: return t;
            }
        }
    }
}
