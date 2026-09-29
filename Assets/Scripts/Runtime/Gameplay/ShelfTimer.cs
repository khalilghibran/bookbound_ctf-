using System;
using UnityEngine;

namespace BookBound
{
    /// <summary>
    /// Countdown for the current bookcase. Must not tick while paused (bookcase-clear transitions,
    /// heart-loss transitions, pause menu - plan.md section 8).
    /// </summary>
    public class ShelfTimer : MonoBehaviour
    {
        public float TimeRemaining { get; private set; }
        public float Duration { get; private set; }
        public bool IsPaused { get; private set; } = true;

        public event Action OnExpired;

        public void Reset(float duration)
        {
            Duration = duration;
            TimeRemaining = duration;
            IsPaused = false;
        }

        public void Pause() => IsPaused = true;
        public void Resume() => IsPaused = false;

        private void Update()
        {
            if (IsPaused || TimeRemaining <= 0f) return;

            TimeRemaining -= Time.deltaTime;
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                IsPaused = true;
                OnExpired?.Invoke();
            }
        }
    }
}
