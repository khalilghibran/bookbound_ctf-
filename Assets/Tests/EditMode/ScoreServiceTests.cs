using NUnit.Framework;

namespace BookBound.Tests
{
    public class ScoreServiceTests
    {
        [Test]
        public void AddSlotFirstCorrect_AddsHundredPoints()
        {
            var score = new ScoreService();
            score.AddSlotFirstCorrect();
            Assert.AreEqual(100, score.Score);
        }

        [Test]
        public void AddTimeBonus_MatchesFormula()
        {
            var score = new ScoreService();
            // 500 * 0.8 * (1 + 5*0.1) = 500 * 0.8 * 1.5 = 600
            int bonus = score.AddTimeBonus(timeRemaining: 40f, shelfDuration: 50f, shelfIndex: 5);
            Assert.AreEqual(600, bonus);
            Assert.AreEqual(600, score.Score);
        }

        [Test]
        public void Score_NeverDecreases_AcrossManyEvents()
        {
            var score = new ScoreService();
            int last = 0;
            for (int i = 0; i < 50; i++)
            {
                if (i % 5 == 0)
                    score.AddTimeBonus(timeRemaining: i, shelfDuration: 50f, shelfIndex: i);
                else
                    score.AddSlotFirstCorrect();

                Assert.GreaterOrEqual(score.Score, last);
                last = score.Score;
            }
        }

        [Test]
        public void OnScoreChanged_FiresWithNewScore()
        {
            var score = new ScoreService();
            int? observed = null;
            score.OnScoreChanged += s => observed = s;

            score.AddSlotFirstCorrect();

            Assert.AreEqual(100, observed);
        }

        [Test]
        public void AddTimeBonus_ZeroDuration_Throws()
        {
            var score = new ScoreService();
            Assert.Throws<System.ArgumentOutOfRangeException>(() => score.AddTimeBonus(10f, 0f, 0));
        }
    }
}
