using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace BookBound.Tests
{
    public class SaveServiceTests
    {
        private const string HighScoreKey = "hiscore";
        private const string BestShelfKey = "bestShelf";
        private const string LeaderboardKey = "leaderboard_v1";
        private const string LastInitialsKey = "leaderboard_initials";

        private bool _hadHighScore;
        private int _oldHighScore;
        private bool _hadBestShelf;
        private int _oldBestShelf;
        private bool _hadLeaderboard;
        private string _oldLeaderboard;
        private bool _hadInitials;
        private string _oldInitials;

        [SetUp]
        public void SetUp()
        {
            // Preserve the editor's real save. These tests must not erase anyone's records.
            _hadHighScore = PlayerPrefs.HasKey(HighScoreKey);
            _oldHighScore = PlayerPrefs.GetInt(HighScoreKey);
            _hadBestShelf = PlayerPrefs.HasKey(BestShelfKey);
            _oldBestShelf = PlayerPrefs.GetInt(BestShelfKey);
            _hadLeaderboard = PlayerPrefs.HasKey(LeaderboardKey);
            _oldLeaderboard = PlayerPrefs.GetString(LeaderboardKey);
            _hadInitials = PlayerPrefs.HasKey(LastInitialsKey);
            _oldInitials = PlayerPrefs.GetString(LastInitialsKey);

            PlayerPrefs.DeleteKey(HighScoreKey);
            PlayerPrefs.DeleteKey(BestShelfKey);
            PlayerPrefs.DeleteKey(LeaderboardKey);
            PlayerPrefs.DeleteKey(LastInitialsKey);
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadHighScore) PlayerPrefs.SetInt(HighScoreKey, _oldHighScore);
            else PlayerPrefs.DeleteKey(HighScoreKey);
            if (_hadBestShelf) PlayerPrefs.SetInt(BestShelfKey, _oldBestShelf);
            else PlayerPrefs.DeleteKey(BestShelfKey);
            if (_hadLeaderboard) PlayerPrefs.SetString(LeaderboardKey, _oldLeaderboard);
            else PlayerPrefs.DeleteKey(LeaderboardKey);
            if (_hadInitials) PlayerPrefs.SetString(LastInitialsKey, _oldInitials);
            else PlayerPrefs.DeleteKey(LastInitialsKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void RankingUsesScoreThenClearedThenEarlierSubmission_AndPersists()
        {
            SaveService.SubmitLeaderboardRun(1000, 2, "easy", "aaa");
            SaveService.SubmitLeaderboardRun(1500, 1, "HARD", "bbb");
            SaveService.SubmitLeaderboardRun(1000, 4, "MEDIUM", "ccc");
            SaveService.SubmitLeaderboardRun(1000, 2, "EASY", "ddd");

            var entries = SaveService.GetLeaderboard();
            Assert.AreEqual(4, entries.Count);
            Assert.AreEqual("BBB", entries[0].Initials);
            Assert.AreEqual("CCC", entries[1].Initials);
            Assert.AreEqual("AAA", entries[2].Initials);
            Assert.AreEqual("DDD", entries[3].Initials);
            Assert.AreEqual("EASY", entries[2].Difficulty);
            Assert.Less(entries[2].SubmissionOrder, entries[3].SubmissionOrder);

            // Each query reads persisted JSON, rather than an in-memory leaderboard cache.
            Assert.AreEqual("BBB", SaveService.GetLeaderboard()[0].Initials);
        }

        [Test]
        public void KeepsTenBestRuns_AndIgnoresNonpositiveScores()
        {
            Assert.IsFalse(SaveService.SubmitLeaderboardRun(0, 0, "EASY", "BAD"));
            Assert.IsFalse(SaveService.SubmitLeaderboardRun(-10, 0, "EASY", "BAD"));
            for (int i = 0; i < 12; i++)
                Assert.IsTrue(SaveService.SubmitLeaderboardRun(100 + i, i, "HARD", "A"));

            Assert.IsFalse(SaveService.SubmitLeaderboardRun(99, 50, "HARD", "LOW"));
            var entries = SaveService.GetLeaderboard();
            Assert.AreEqual(10, entries.Count);
            Assert.AreEqual(111, entries[0].Score);
            Assert.AreEqual(102, entries[9].Score);
        }

        [Test]
        public void MigratesOldHighScoreOnce_WithoutClaimingBestShelf()
        {
            PlayerPrefs.SetInt(HighScoreKey, 1500);
            PlayerPrefs.SetInt(BestShelfKey, 7);

            var entries = SaveService.GetLeaderboard();
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("OLD", entries[0].Initials);
            Assert.AreEqual(1500, entries[0].Score);
            Assert.AreEqual(0, entries[0].BookcasesCleared);
            Assert.AreEqual("UNKNOWN", entries[0].Difficulty);
            Assert.AreEqual(7, SaveService.BestShelf);

            SaveService.Submit(2000, 8);
            entries = SaveService.GetLeaderboard();
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(1500, entries[0].Score);
            Assert.AreEqual(2000, SaveService.HighScore);
            Assert.AreEqual(8, SaveService.BestShelf);
        }

        [Test]
        public void SubmitSnapshotsLegacyScoreBeforeUpdatingIt()
        {
            PlayerPrefs.SetInt(HighScoreKey, 900);

            SaveService.Submit(1200, 4);

            Assert.AreEqual(900, SaveService.GetLeaderboard()[0].Score);
            Assert.AreEqual(1200, SaveService.HighScore);
        }

        [Test]
        public void MalformedStorageFallsBackToOldHighScore()
        {
            PlayerPrefs.SetInt(HighScoreKey, 700);
            PlayerPrefs.SetString(LeaderboardKey, "{not valid json");

            var entries = SaveService.GetLeaderboard();
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("OLD", entries[0].Initials);
            Assert.AreEqual(700, entries[0].Score);
            Assert.AreEqual(1, SaveService.GetLeaderboard().Count);
        }

        [Test]
        public void InitialsAreLimitedToThreeLettersOrDigits()
        {
            SaveService.SetLastInitials(" a-1b9 ");
            Assert.AreEqual("A1B", SaveService.LastInitials);

            SaveService.SubmitLeaderboardRun(100, 1, "MEDIUM", "*!?");
            Assert.AreEqual("YOU", SaveService.LastInitials);
            Assert.AreEqual("YOU", SaveService.GetLeaderboard()[0].Initials);
        }

        [Test]
        public void GameManagerSubmitsOneEntryWhenTheFinishedRunIsSubmittedAgain()
        {
            var go = new GameObject("Leaderboard submission test");
            try
            {
                var manager = go.AddComponent<GameManager>();
                var score = new ScoreService();
                score.AddSlotFirstCorrect();
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(GameManager).GetField("_score", fields).SetValue(manager, score);
                typeof(GameManager).GetField("_runStarted", fields).SetValue(manager, true);
                typeof(GameManager).GetField("_shelfIndex", fields).SetValue(manager, 2);
                typeof(GameManager).GetField("_playerInitials", fields).SetValue(manager, "ABC");
                MethodInfo submit = typeof(GameManager).GetMethod("SubmitRunToLeaderboard", fields);

                // Game over followed by Main Menu must not create two ranked copies of one run.
                submit.Invoke(manager, null);
                submit.Invoke(manager, null);

                var entries = SaveService.GetLeaderboard();
                Assert.AreEqual(1, entries.Count);
                Assert.AreEqual("ABC", entries[0].Initials);
                Assert.AreEqual(100, entries[0].Score);
                Assert.AreEqual(2, entries[0].BookcasesCleared);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
