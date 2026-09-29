using NUnit.Framework;

namespace BookBound.Tests
{
    public class LivesServiceTests
    {
        [Test]
        public void StartsAtThreeHearts()
        {
            var lives = new LivesService();
            Assert.AreEqual(3, lives.Hearts);
            Assert.IsFalse(lives.IsGameOver);
        }

        [Test]
        public void LoseHeart_Decrements_AndFiresEvent()
        {
            var lives = new LivesService();
            int? observed = null;
            lives.OnHeartLost += h => observed = h;

            lives.LoseHeart();

            Assert.AreEqual(2, lives.Hearts);
            Assert.AreEqual(2, observed);
        }

        [Test]
        public void LoseHeart_ToZero_FiresGameOver()
        {
            var lives = new LivesService(1);
            bool gameOver = false;
            lives.OnGameOver += () => gameOver = true;

            lives.LoseHeart();

            Assert.AreEqual(0, lives.Hearts);
            Assert.IsTrue(lives.IsGameOver);
            Assert.IsTrue(gameOver);
        }

        [Test]
        public void LoseHeart_AfterGameOver_IsNoOp()
        {
            var lives = new LivesService(1);
            lives.LoseHeart();

            int gameOverCount = 0;
            lives.OnGameOver += () => gameOverCount++;

            lives.LoseHeart();

            Assert.AreEqual(0, lives.Hearts);
            Assert.AreEqual(0, gameOverCount);
        }

        [Test]
        public void Reset_RestoresStartingHearts()
        {
            var lives = new LivesService(3);
            lives.LoseHeart();
            lives.LoseHeart();

            lives.Reset(3);

            Assert.AreEqual(3, lives.Hearts);
            Assert.IsFalse(lives.IsGameOver);
        }
    }
}
