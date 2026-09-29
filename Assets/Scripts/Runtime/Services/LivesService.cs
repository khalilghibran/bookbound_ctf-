using System;

namespace BookBound
{
    /// <summary>Pure C# heart/lives tracking. Timer expiry is the only way to lose a heart.</summary>
    public class LivesService
    {
        public const int StartingHearts = 3;

        public int Hearts { get; private set; }
        public bool IsGameOver => Hearts <= 0;

        public event Action<int> OnHeartLost;
        public event Action OnGameOver;

        public LivesService(int startingHearts = StartingHearts)
        {
            Hearts = startingHearts;
        }

        public void LoseHeart()
        {
            if (IsGameOver) return;

            Hearts--;
            OnHeartLost?.Invoke(Hearts);

            if (Hearts <= 0)
                OnGameOver?.Invoke();
        }

        public void Reset(int startingHearts = StartingHearts)
        {
            Hearts = startingHearts;
        }
    }
}
