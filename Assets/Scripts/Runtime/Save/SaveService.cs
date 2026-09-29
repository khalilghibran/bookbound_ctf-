using System;
using System.Collections.Generic;
using UnityEngine;

namespace BookBound
{
    /// <summary>A single local leaderboard result. BookcasesCleared is zero when unknown.</summary>
    [Serializable]
    public sealed class LeaderboardEntry
    {
        public string Initials;
        public int Score;
        public int BookcasesCleared;
        public string Difficulty;
        public int SubmissionOrder;
    }

    /// <summary>Local high score, best bookcase reached, and the ten best run results.</summary>
    public static class SaveService
    {
        private const string HighScoreKey = "hiscore";
        private const string BestShelfKey = "bestShelf";
        private const string LeaderboardKey = "leaderboard_v1";
        private const string LastInitialsKey = "leaderboard_initials";
        private const int LeaderboardLimit = 10;

        [Serializable]
        private sealed class LeaderboardData
        {
            public int Version = 1;
            public int NextSubmissionOrder;
            public List<LeaderboardEntry> Entries = new List<LeaderboardEntry>();
        }

        public static int HighScore => PlayerPrefs.GetInt(HighScoreKey, 0);

        /// <summary>Best bookcase NUMBER reached (1-based, as shown in the HUD), not shelfIndex.</summary>
        public static int BestShelf => PlayerPrefs.GetInt(BestShelfKey, 0);

        public static string LastInitials => NormalizeInitials(PlayerPrefs.GetString(LastInitialsKey, "YOU"));

        public static void SetLastInitials(string initials)
        {
            PlayerPrefs.SetString(LastInitialsKey, NormalizeInitials(initials));
            PlayerPrefs.Save();
        }

        /// <summary>Keep the historical progress keys current, including during an unfinished run.</summary>
        public static void Submit(int score, int bookcaseNumber)
        {
            // Snapshot a pre-leaderboard high score before this run can replace it. This also marks
            // a fresh installation as migrated, so its first run is not duplicated as OLD.
            LoadLeaderboard();

            if (score > HighScore) PlayerPrefs.SetInt(HighScoreKey, score);
            if (bookcaseNumber > BestShelf) PlayerPrefs.SetInt(BestShelfKey, bookcaseNumber);
            PlayerPrefs.Save();
        }

        /// <summary>Returns a snapshot sorted by score, then bookcases cleared, then earliest run.</summary>
        public static IReadOnlyList<LeaderboardEntry> GetLeaderboard()
        {
            LeaderboardData data = LoadLeaderboard();
            var snapshot = new LeaderboardEntry[data.Entries.Count];
            for (int i = 0; i < snapshot.Length; i++) snapshot[i] = Copy(data.Entries[i]);
            return Array.AsReadOnly(snapshot);
        }

        /// <summary>Submit once when a run ends. Returns whether it appears in the top ten.</summary>
        public static bool SubmitLeaderboardRun(int score, int bookcasesCleared, string difficulty, string initials)
        {
            LeaderboardData data = LoadLeaderboard();
            if (score <= 0) return false;

            string normalizedInitials = NormalizeInitials(initials);
            var entry = new LeaderboardEntry
            {
                Initials = normalizedInitials,
                Score = score,
                BookcasesCleared = Math.Max(0, bookcasesCleared),
                Difficulty = NormalizeDifficulty(difficulty),
                SubmissionOrder = data.NextSubmissionOrder++,
            };

            data.Entries.Add(entry);
            data.Entries.Sort(CompareEntries);
            if (data.Entries.Count > LeaderboardLimit)
                data.Entries.RemoveRange(LeaderboardLimit, data.Entries.Count - LeaderboardLimit);

            PlayerPrefs.SetString(LastInitialsKey, normalizedInitials);
            if (score > HighScore) PlayerPrefs.SetInt(HighScoreKey, score);
            SaveLeaderboard(data);
            return data.Entries.Contains(entry);
        }

        private static LeaderboardData LoadLeaderboard()
        {
            if (!PlayerPrefs.HasKey(LeaderboardKey)) return MigrateLegacyHighScore();

            string json = PlayerPrefs.GetString(LeaderboardKey);
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{") ||
                !json.TrimEnd().EndsWith("}"))
                return MigrateLegacyHighScore();

            LeaderboardData data;
            try
            {
                data = JsonUtility.FromJson<LeaderboardData>(json);
            }
            catch (Exception)
            {
                data = null;
            }

            if (data == null || data.Version != 1 || data.Entries == null)
                return MigrateLegacyHighScore();

            NormalizeEntries(data);
            return data;
        }

        private static LeaderboardData MigrateLegacyHighScore()
        {
            var data = new LeaderboardData();
            int oldHighScore = HighScore;
            if (oldHighScore > 0)
            {
                // bestShelf is a separate maximum and might belong to another run. Never attach
                // it to this imported score or imply a difficulty we did not record.
                data.Entries.Add(new LeaderboardEntry
                {
                    Initials = "OLD",
                    Score = oldHighScore,
                    BookcasesCleared = 0,
                    Difficulty = "UNKNOWN",
                    SubmissionOrder = 0,
                });
                data.NextSubmissionOrder = 1;
            }
            SaveLeaderboard(data);
            return data;
        }

        private static void NormalizeEntries(LeaderboardData data)
        {
            var seenOrders = new HashSet<int>();
            int next = Math.Max(0, data.NextSubmissionOrder);
            for (int i = data.Entries.Count - 1; i >= 0; i--)
            {
                LeaderboardEntry entry = data.Entries[i];
                if (entry == null || entry.Score <= 0)
                {
                    data.Entries.RemoveAt(i);
                    continue;
                }

                entry.Initials = NormalizeInitials(entry.Initials);
                entry.BookcasesCleared = Math.Max(0, entry.BookcasesCleared);
                entry.Difficulty = NormalizeDifficulty(entry.Difficulty);
                if (entry.SubmissionOrder < 0 || !seenOrders.Add(entry.SubmissionOrder))
                {
                    entry.SubmissionOrder = next++;
                    seenOrders.Add(entry.SubmissionOrder);
                }
                else if (entry.SubmissionOrder >= next)
                {
                    next = entry.SubmissionOrder + 1;
                }
            }

            data.NextSubmissionOrder = next;
            data.Entries.Sort(CompareEntries);
            if (data.Entries.Count > LeaderboardLimit)
                data.Entries.RemoveRange(LeaderboardLimit, data.Entries.Count - LeaderboardLimit);
        }

        private static int CompareEntries(LeaderboardEntry left, LeaderboardEntry right)
        {
            int score = right.Score.CompareTo(left.Score);
            if (score != 0) return score;
            int cleared = right.BookcasesCleared.CompareTo(left.BookcasesCleared);
            return cleared != 0 ? cleared : left.SubmissionOrder.CompareTo(right.SubmissionOrder);
        }

        private static string NormalizeInitials(string initials)
        {
            if (string.IsNullOrEmpty(initials)) return "YOU";
            var chars = new char[3];
            int count = 0;
            foreach (char raw in initials)
            {
                char c = char.ToUpperInvariant(raw);
                if ((c < 'A' || c > 'Z') && (c < '0' || c > '9')) continue;
                chars[count++] = c;
                if (count == chars.Length) break;
            }
            return count == 0 ? "YOU" : new string(chars, 0, count);
        }

        private static string NormalizeDifficulty(string difficulty)
        {
            string value = difficulty?.ToUpperInvariant();
            return value == "EASY" || value == "MEDIUM" || value == "HARD" ? value : "UNKNOWN";
        }

        private static LeaderboardEntry Copy(LeaderboardEntry entry) => new LeaderboardEntry
        {
            Initials = entry.Initials,
            Score = entry.Score,
            BookcasesCleared = entry.BookcasesCleared,
            Difficulty = entry.Difficulty,
            SubmissionOrder = entry.SubmissionOrder,
        };

        private static void SaveLeaderboard(LeaderboardData data)
        {
            PlayerPrefs.SetString(LeaderboardKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
    }
}
