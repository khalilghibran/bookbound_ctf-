using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BookBound
{
    /// <summary>
    /// Coroutine-based tween utility for UI elements: position, scale, rotation, color/alpha, plus a
    /// punch-scale shorthand. Chosen over DOTween so the animation layer has zero external package
    /// dependency - this project's whole build pipeline is driven headlessly via Editor scripts with no
    /// Package Manager / Asset Store access in that loop, and a hand-rolled utility keeps it that way.
    ///
    /// Every tween is keyed by (target instance ID, property). Starting a new tween on the same key
    /// kills whatever was already running there first, so overlapping tweens on one property can never
    /// fight (the "interruptible" requirement). All tweens run on a single hidden, persistent runner
    /// object using unscaled time, so menus keep animating even if gameplay time is ever scaled/paused,
    /// and nothing here ever blocks the caller - starting a tween returns immediately.
    /// </summary>
    public static class UITween
    {
        private enum Prop { Position, AnchoredPosition, Scale, Rotation, Color, Alpha }

        private static readonly Dictionary<(int id, Prop prop), Coroutine> Running = new Dictionary<(int, Prop), Coroutine>();

        private class Runner : MonoBehaviour { }

        private static Runner _runner;

        private static Runner GetRunner()
        {
            if (_runner != null) return _runner;
            var go = new GameObject("~UITweenRunner") { hideFlags = HideFlags.HideInHierarchy };
            UnityEngine.Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<Runner>();
            return _runner;
        }

        private static bool AnimationsEnabled => AnimationSettings.Instance.enabled;

        // ---- Kill ----

        public static void Kill(Transform target, bool includeScale = true, bool includeRotation = true, bool includePosition = true)
        {
            if (target == null) return;
            int id = target.GetInstanceID();
            if (includePosition)
            {
                KillKey((id, Prop.Position));
                KillKey((id, Prop.AnchoredPosition));
            }
            if (includeScale) KillKey((id, Prop.Scale));
            if (includeRotation) KillKey((id, Prop.Rotation));
        }

        public static void KillColor(Graphic target)
        {
            if (target == null) return;
            int id = target.GetInstanceID();
            KillKey((id, Prop.Color));
            KillKey((id, Prop.Alpha));
        }

        private static void KillKey((int, Prop) key)
        {
            if (Running.TryGetValue(key, out Coroutine c) && c != null && _runner != null)
                _runner.StopCoroutine(c);
            Running.Remove(key);
        }

        private static void Start((int, Prop) key, IEnumerator routine)
        {
            KillKey(key);
            Coroutine c = GetRunner().StartCoroutine(routine);
            Running[key] = c;
        }

        // ---- Public tween API ----

        public static void MoveTo(RectTransform target, Vector2 anchoredPosition, float duration, EaseType ease, Action onComplete = null)
        {
            if (target == null) return;
            var key = (target.GetInstanceID(), Prop.AnchoredPosition);
            if (!AnimationsEnabled || duration <= 0f)
            {
                KillKey(key);
                target.anchoredPosition = anchoredPosition;
                onComplete?.Invoke();
                return;
            }
            Start(key, TweenVector(v => target.anchoredPosition = v, target.anchoredPosition, anchoredPosition, duration, ease, onComplete));
        }

        public static void MoveTo(RectTransform target, Vector3 worldPosition, float duration, EaseType ease, Action onComplete = null)
        {
            if (target == null) return;
            var key = (target.GetInstanceID(), Prop.Position);
            if (!AnimationsEnabled || duration <= 0f)
            {
                KillKey(key);
                target.position = worldPosition;
                onComplete?.Invoke();
                return;
            }
            Start(key, TweenVector(v => target.position = v, target.position, worldPosition, duration, ease, onComplete));
        }

        public static void ScaleTo(Transform target, Vector3 scale, float duration, EaseType ease, Action onComplete = null)
        {
            if (target == null) return;
            var key = (target.GetInstanceID(), Prop.Scale);
            if (!AnimationsEnabled || duration <= 0f)
            {
                KillKey(key);
                target.localScale = scale;
                onComplete?.Invoke();
                return;
            }
            Start(key, TweenVector(v => target.localScale = v, target.localScale, scale, duration, ease, onComplete));
        }

        /// <summary>Eases the local Z rotation toward the given degrees (shortest signed delta).</summary>
        public static void RotateTo(Transform target, float zDegrees, float duration, EaseType ease, Action onComplete = null)
        {
            if (target == null) return;
            var key = (target.GetInstanceID(), Prop.Rotation);
            if (!AnimationsEnabled || duration <= 0f)
            {
                KillKey(key);
                target.localRotation = Quaternion.Euler(0f, 0f, zDegrees);
                onComplete?.Invoke();
                return;
            }
            float from = target.localEulerAngles.z;
            if (from > 180f) from -= 360f;
            Start(key, TweenFloat(v => target.localRotation = Quaternion.Euler(0f, 0f, v), from, zDegrees, duration, ease, onComplete));
        }

        public static void FadeTo(Graphic target, float alpha, float duration, EaseType ease, Action onComplete = null)
        {
            if (target == null) return;
            var key = (target.GetInstanceID(), Prop.Alpha);
            if (!AnimationsEnabled || duration <= 0f)
            {
                KillKey(key);
                SetAlpha(target, alpha);
                onComplete?.Invoke();
                return;
            }
            Start(key, TweenFloat(v => SetAlpha(target, v), target.color.a, alpha, duration, ease, onComplete));
        }

        public static void FadeTo(CanvasGroup target, float alpha, float duration, EaseType ease, Action onComplete = null)
        {
            if (target == null) return;
            var key = (target.GetInstanceID(), Prop.Alpha);
            if (!AnimationsEnabled || duration <= 0f)
            {
                KillKey(key);
                target.alpha = alpha;
                onComplete?.Invoke();
                return;
            }
            Start(key, TweenFloat(v => target.alpha = v, target.alpha, alpha, duration, ease, onComplete));
        }

        public static void ColorTo(Graphic target, Color color, float duration, EaseType ease, Action onComplete = null)
        {
            if (target == null) return;
            var key = (target.GetInstanceID(), Prop.Color);
            if (!AnimationsEnabled || duration <= 0f)
            {
                KillKey(key);
                target.color = color;
                onComplete?.Invoke();
                return;
            }
            Color from = target.color;
            Start(key, TweenGeneric(t =>
            {
                target.color = Color.Lerp(from, color, t);
            }, duration, ease, onComplete));
        }

        /// <summary>Scales up to <paramref name="baseScale"/> * peak then eases back down to baseScale -
        /// the "punch" used for score gains, correct placements, heart pulses, etc.</summary>
        public static void PunchScale(Transform target, float peak, float duration, Vector3? baseScale = null, EaseType settleEase = EaseType.OutQuad, Action onComplete = null)
        {
            if (target == null) return;
            var key = (target.GetInstanceID(), Prop.Scale);
            Vector3 baseS = baseScale ?? Vector3.one;
            if (!AnimationsEnabled || duration <= 0f)
            {
                KillKey(key);
                target.localScale = baseS;
                onComplete?.Invoke();
                return;
            }
            Start(key, PunchRoutine(target, baseS, peak, duration, settleEase, onComplete));
        }

        // ---- Coroutines ----

        private static IEnumerator PunchRoutine(Transform target, Vector3 baseScale, float peak, float duration, EaseType settleEase, Action onComplete)
        {
            float upDuration = duration * 0.3f;
            float downDuration = duration - upDuration;
            Vector3 peakScale = baseScale * peak;

            float t = 0f;
            while (t < upDuration)
            {
                t += Time.unscaledDeltaTime;
                target.localScale = Vector3.LerpUnclamped(baseScale, peakScale, Easing.Evaluate(EaseType.OutQuad, t / upDuration));
                yield return null;
            }
            target.localScale = peakScale;

            t = 0f;
            while (t < downDuration)
            {
                t += Time.unscaledDeltaTime;
                target.localScale = Vector3.LerpUnclamped(peakScale, baseScale, Easing.Evaluate(settleEase, t / downDuration));
                yield return null;
            }
            target.localScale = baseScale;
            onComplete?.Invoke();
        }

        private static IEnumerator TweenVector(Action<Vector3> apply, Vector3 from, Vector3 to, float duration, EaseType ease, Action onComplete)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                apply(Vector3.LerpUnclamped(from, to, Easing.Evaluate(ease, t / duration)));
                yield return null;
            }
            apply(to);
            onComplete?.Invoke();
        }

        private static IEnumerator TweenFloat(Action<float> apply, float from, float to, float duration, EaseType ease, Action onComplete)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                apply(Mathf.LerpUnclamped(from, to, Easing.Evaluate(ease, t / duration)));
                yield return null;
            }
            apply(to);
            onComplete?.Invoke();
        }

        /// <summary>Drives a raw 0..1 eased fraction into a callback each frame - for tweens (like Color)
        /// that don't reduce to a single float/vector lerp target.</summary>
        private static IEnumerator TweenGeneric(Action<float> applyEasedFraction, float duration, EaseType ease, Action onComplete)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                applyEasedFraction(Easing.Evaluate(ease, t / duration));
                yield return null;
            }
            applyEasedFraction(1f);
            onComplete?.Invoke();
        }

        private static void SetAlpha(Graphic g, float a)
        {
            Color c = g.color;
            c.a = a;
            g.color = c;
        }

        /// <summary>Endless scale pulse (idle heart breathing, slot-highlight pulse, low-timer pulse) -
        /// registered under the same key as <see cref="ScaleTo"/>/<see cref="PunchScale"/>, so starting
        /// any other scale tween on this target (or calling <see cref="Kill"/>) stops the loop cleanly
        /// instead of fighting it. Runs until killed; there is no natural end.</summary>
        public static void PulseScaleLoop(Transform target, float peakScale, float period, Vector3? baseScale = null)
        {
            if (target == null) return;
            Vector3 baseS = baseScale ?? Vector3.one;
            var key = (target.GetInstanceID(), Prop.Scale);
            if (!AnimationsEnabled)
            {
                KillKey(key);
                target.localScale = baseS;
                return;
            }
            Start(key, PulseLoopRoutine(target, baseS, peakScale, period));
        }

        private static IEnumerator PulseLoopRoutine(Transform target, Vector3 baseScale, float peak, float period)
        {
            Vector3 peakVec = baseScale * peak;
            float half = Mathf.Max(0.01f, period) / 2f;
            while (true)
            {
                float t = 0f;
                while (t < half)
                {
                    t += Time.unscaledDeltaTime;
                    target.localScale = Vector3.LerpUnclamped(baseScale, peakVec, Easing.Evaluate(EaseType.InOutSine, t / half));
                    yield return null;
                }
                t = 0f;
                while (t < half)
                {
                    t += Time.unscaledDeltaTime;
                    target.localScale = Vector3.LerpUnclamped(peakVec, baseScale, Easing.Evaluate(EaseType.InOutSine, t / half));
                    yield return null;
                }
            }
        }
    }
}
