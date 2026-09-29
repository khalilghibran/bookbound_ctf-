using System;
using UnityEngine;

namespace BookBound
{
    /// <summary>
    /// Pure C# score tracking. No negative scoring anywhere: score is monotonically non-decreasing.
    /// </summary>
    public class ScoreService
    {
        public const int SlotFirstCorrectPoints = 100;
        public const float TimeBonusBase = 500f;
        public const float TimeBonusShelfIndexWeight = 0.1f;

        public int Score { get; private set; }

        public event Action<int> OnScoreChanged;

        public void AddSlotFirstCorrect()
        {
            AddScore(SlotFirstCorrectPoints);
        }

        /// <summary>Awarded once per cleared bookcase. remaining/duration in seconds.</summary>
        public int AddTimeBonus(float timeRemaining, float shelfDuration, int shelfIndex)
        {
            if (shelfDuration <= 0f) throw new ArgumentOutOfRangeException(nameof(shelfDuration));

            float fraction = Mathf.Clamp01(timeRemaining / shelfDuration);
            int bonus = Mathf.RoundToInt(TimeBonusBase * fraction * (1f + shelfIndex * TimeBonusShelfIndexWeight));
            AddScore(bonus);
            return bonus;
        }

        private void AddScore(int delta)
        {
            if (delta < 0)
                throw new InvalidOperationException("ScoreService: score must never decrease.");

            Score += delta;
            OnScoreChanged?.Invoke(Score);
        }
    }
}
