using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BookBound.Tests
{
    public class ShelfGeneratorTests
    {
        private static GenreDefinition CreateGenre(string id)
        {
            var genre = ScriptableObject.CreateInstance<GenreDefinition>();
            var so = new SerializedObject(genre);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            return genre;
        }

        /// <summary>One genre per row, sized off RowCount so these tests don't have to be rewritten
        /// every time a genre is added or removed.</summary>
        private static GenreDefinition[] CreateGenres()
        {
            string[] ids = { "fantasy", "science", "history", "biography" };
            Assert.GreaterOrEqual(ids.Length, ShelfGeometry.RowCount, "add an id here for the new row");

            var genres = new GenreDefinition[ShelfGeometry.RowCount];
            for (int i = 0; i < genres.Length; i++) genres[i] = CreateGenre(ids[i]);
            return genres;
        }

        private static ShelfLayoutConfig CreateConfig(float correctMin, float correctMax, float misMin, float misMax, bool shuffle = false)
        {
            var config = ScriptableObject.CreateInstance<ShelfLayoutConfig>();
            config.alreadyCorrectMin = correctMin;
            config.alreadyCorrectMax = correctMax;
            config.misplacedMin = misMin;
            config.misplacedMax = misMax;
            config.shuffleRowOrder = shuffle;
            return config;
        }

        private static IEnumerable<(int row, int slot)> AllPositions()
        {
            for (int r = 0; r < ShelfGeometry.RowCount; r++)
                for (int s = 0; s < ShelfGeometry.SlotsPerRow; s++)
                    yield return (r, s);
        }

        [Test]
        public void Generate_ProducesExactBijectionOfAllBooks()
        {
            var genres = CreateGenres();
            var config = CreateConfig(0.4f, 0.6f, 0.1f, 0.3f);

            var result = ShelfGenerator.Generate(config, genres, seed: 12345);

            var seen = new HashSet<BookData>();
            foreach (var (row, slot) in AllPositions())
            {
                var occupant = result.SlotOccupants[row, slot];
                if (occupant != null)
                    Assert.IsTrue(seen.Add(occupant.Value), $"Duplicate book on board: {occupant.Value}");
            }
            foreach (var book in result.DealtBooks)
                Assert.IsTrue(seen.Add(book), $"Duplicate book in dealt pile / on board: {book}");

            Assert.AreEqual(ShelfGenerator.TotalSlots, seen.Count, "Every (genre,1..10) pair must appear exactly once.");
            foreach (var genre in genres)
                for (int n = 1; n <= GenreDefinition.BooksPerGenre; n++)
                    Assert.IsTrue(seen.Contains(new BookData(genre, n)), $"Missing {genre.Id}#{n:00}");
        }

        [Test]
        public void Generate_DealtCountEqualsSlotsMinusPrefill()
        {
            var genres = CreateGenres();
            var config = CreateConfig(0.3f, 0.5f, 0.2f, 0.4f);

            var result = ShelfGenerator.Generate(config, genres, seed: 777);

            int prefilled = 0;
            foreach (var (row, slot) in AllPositions())
                if (result.SlotOccupants[row, slot] != null) prefilled++;

            Assert.AreEqual(ShelfGenerator.TotalSlots - prefilled, result.DealtBooks.Count);
        }

        [Test]
        public void Generate_EveryMisplacedBookIsActuallyWrong()
        {
            var genres = CreateGenres();
            var config = CreateConfig(0.2f, 0.3f, 0.5f, 0.6f);

            var result = ShelfGenerator.Generate(config, genres, seed: 99);

            foreach (var (row, slot) in AllPositions())
            {
                var occupant = result.SlotOccupants[row, slot];
                if (occupant == null) continue;

                bool isCorrect = result.IsCorrectAtSpawn(row, slot);
                var expected = new BookData(result.RowGenres[row], slot + 1);
                bool actuallyMatches = occupant.Value.Equals(expected);

                Assert.AreEqual(actuallyMatches, isCorrect);
                // If it's not correct, it must genuinely not equal the expected book.
                if (!isCorrect)
                    Assert.AreNotEqual(expected, occupant.Value);
            }
        }

        [Test]
        public void Generate_IsDeterministicForSameSeed()
        {
            var genresA = CreateGenres();
            var genresB = CreateGenres();
            var config = CreateConfig(0.35f, 0.55f, 0.2f, 0.4f);

            var resultA = ShelfGenerator.Generate(config, genresA, seed: 555);
            var resultB = ShelfGenerator.Generate(config, genresB, seed: 555);

            for (int r = 0; r < ShelfGeometry.RowCount; r++)
            {
                Assert.AreEqual(resultA.RowGenres[r].Id, resultB.RowGenres[r].Id);
                for (int s = 0; s < ShelfGeometry.SlotsPerRow; s++)
                {
                    var a = resultA.SlotOccupants[r, s];
                    var b = resultB.SlotOccupants[r, s];
                    Assert.AreEqual(a == null, b == null);
                    if (a != null) Assert.AreEqual(a.Value, b.Value);
                }
            }

            Assert.AreEqual(resultA.DealtBooks.Count, resultB.DealtBooks.Count);
            for (int i = 0; i < resultA.DealtBooks.Count; i++)
                Assert.AreEqual(resultA.DealtBooks[i], resultB.DealtBooks[i]);

            Assert.AreEqual(resultA.RequiredMoves, resultB.RequiredMoves);
        }

        [Test]
        public void Generate_RequiredMovesEqualsSlotsMinusCorrect()
        {
            var genres = CreateGenres();
            var config = CreateConfig(0.5f, 0.6f, 0.1f, 0.2f);

            var result = ShelfGenerator.Generate(config, genres, seed: 42);

            int correct = 0;
            foreach (var (row, slot) in AllPositions())
                if (result.IsCorrectAtSpawn(row, slot)) correct++;

            Assert.AreEqual(ShelfGenerator.TotalSlots - correct, result.RequiredMoves);
        }

        [Test]
        public void Generate_ShuffleRowOrder_StillCoversEveryGenre()
        {
            var genres = CreateGenres();
            var config = CreateConfig(0.4f, 0.5f, 0.2f, 0.3f, shuffle: true);

            var result = ShelfGenerator.Generate(config, genres, seed: 2024);

            var rowGenreIds = new HashSet<string>();
            foreach (var g in result.RowGenres) rowGenreIds.Add(g.Id);
            Assert.AreEqual(ShelfGeometry.RowCount, rowGenreIds.Count);
        }

        [Test]
        public void Generate_ExtremeAllCorrect_ProducesNoDealtBooks()
        {
            var genres = CreateGenres();
            var config = CreateConfig(1f, 1f, 0f, 0f);

            var result = ShelfGenerator.Generate(config, genres, seed: 1);

            Assert.AreEqual(0, result.DealtBooks.Count);
            Assert.AreEqual(0, result.RequiredMoves);
            foreach (var (row, slot) in AllPositions())
                Assert.IsTrue(result.IsCorrectAtSpawn(row, slot));
        }

        [Test]
        public void Generate_ExtremeAllEmpty_DealsEveryBook()
        {
            var genres = CreateGenres();
            var config = CreateConfig(0f, 0f, 0f, 0f);

            var result = ShelfGenerator.Generate(config, genres, seed: 1);

            Assert.AreEqual(ShelfGenerator.TotalSlots, result.DealtBooks.Count);
            Assert.AreEqual(ShelfGenerator.TotalSlots, result.RequiredMoves);
        }

        [Test]
        public void DeliveryShelves_DealEveryUnsolvedBookWithoutMisplacedPrefill()
        {
            var genres = CreateGenres();
            var config = CreateConfig(0.4f, 0.5f, 0.2f, 0.4f);

            for (int seed = 0; seed < 32; seed++)
            {
                var result = ShelfGenerator.Generate(config, genres, seed, ShelfType.Delivery);
                var seen = new HashSet<BookData>();
                int occupied = 0;
                foreach (var (row, slot) in AllPositions())
                {
                    var book = result.SlotOccupants[row, slot];
                    if (book == null) continue;
                    occupied++;
                    Assert.IsTrue(result.IsCorrectAtSpawn(row, slot));
                    Assert.IsTrue(seen.Add(book.Value));
                }
                foreach (BookData book in result.DealtBooks) Assert.IsTrue(seen.Add(book));

                Assert.AreEqual(ShelfGenerator.TotalSlots, seen.Count, $"seed {seed}");
                Assert.AreEqual(ShelfGenerator.TotalSlots - occupied, result.DealtBooks.Count, $"seed {seed}");
            }
        }

        [Test]
        public void RepairShelves_StartFullWithEveryBookExactlyOnce()
        {
            var genres = CreateGenres();
            var config = CreateConfig(0.4f, 0.5f, 0.2f, 0.4f);

            for (int seed = 0; seed < 32; seed++)
            {
                var result = ShelfGenerator.Generate(config, genres, seed, ShelfType.Repair);
                var seen = new HashSet<BookData>();
                int wrong = 0;
                foreach (var (row, slot) in AllPositions())
                {
                    var book = result.SlotOccupants[row, slot];
                    Assert.IsTrue(book.HasValue, $"empty slot at seed {seed}");
                    Assert.IsTrue(seen.Add(book.Value), $"duplicate book at seed {seed}");
                    if (!result.IsCorrectAtSpawn(row, slot)) wrong++;
                }

                Assert.IsEmpty(result.DealtBooks, $"seed {seed}");
                Assert.AreEqual(ShelfGenerator.TotalSlots, seen.Count, $"seed {seed}");
                Assert.Greater(wrong, 0, $"seed {seed}");
            }
        }

        [Test]
        public void SpecialShelves_RemainPlayableWhenLayoutRequestsAllCorrect()
        {
            var genres = CreateGenres();
            var config = CreateConfig(1f, 1f, 0f, 0f);

            var delivery = ShelfGenerator.Generate(config, genres, 17, ShelfType.Delivery);
            Assert.AreEqual(1, delivery.DealtBooks.Count);
            Assert.AreEqual(1, delivery.RequiredMoves);

            var repair = ShelfGenerator.Generate(config, genres, 17, ShelfType.Repair);
            Assert.IsEmpty(repair.DealtBooks);
            Assert.AreEqual(2, repair.RequiredMoves);
            int wrong = 0;
            foreach (var (row, slot) in AllPositions())
                if (!repair.IsCorrectAtSpawn(row, slot)) wrong++;
            Assert.AreEqual(2, wrong);
        }

        [TestCase(0, ShelfType.Standard)]
        [TestCase(1, ShelfType.Standard)]
        [TestCase(2, ShelfType.Delivery)]
        [TestCase(3, ShelfType.Standard)]
        [TestCase(4, ShelfType.Repair)]
        [TestCase(5, ShelfType.Standard)]
        [TestCase(6, ShelfType.Delivery)]
        [TestCase(8, ShelfType.Repair)]
        public void ShelfTypeSchedule_RepeatsWithAStandardShelfBetweenSpecials(int index, ShelfType expected)
        {
            Assert.AreEqual(expected, ShelfTypeSchedule.ForShelfIndex(index));
        }
    }
}
