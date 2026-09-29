using System;
using System.Collections.Generic;
using UnityEngine;

namespace BookBound
{
    public enum ShelfType { Standard, Delivery, Repair }

    /// <summary>Introduce a special shelf every other bookcase after the first two. Alternating
    /// Delivery and Repair leaves a standard shelf between them as the difficulty bands advance.</summary>
    public static class ShelfTypeSchedule
    {
        public static ShelfType ForShelfIndex(int shelfIndex)
        {
            if (shelfIndex < 0) throw new ArgumentOutOfRangeException(nameof(shelfIndex));
            if (shelfIndex < 2) return ShelfType.Standard;
            // Parentheses make the modulo expression the switch input. Without them, the compiler
            // parses `shelfIndex % 4 switch` as an attempt to apply `%` to an int and ShelfType.
            return (shelfIndex % 4) switch
            {
                2 => ShelfType.Delivery,
                0 => ShelfType.Repair,
                _ => ShelfType.Standard,
            };
        }
    }

    /// <summary>Result of one generated bookcase layout.</summary>
    public sealed class ShelfLayoutResult
    {
        /// <summary>Length 4. Row index -> genre assigned to that row for this bookcase.</summary>
        public GenreDefinition[] RowGenres;

        /// <summary>[row, slotIndex] -> occupant, or null if the slot spawns empty.</summary>
        public BookData?[,] SlotOccupants;

        /// <summary>Books dealt to the table, already shuffled. Count == number of empty slots.</summary>
        public List<BookData> DealtBooks;

        /// <summary>40 - (slots that spawn already correct).</summary>
        public int RequiredMoves;

        public bool IsCorrectAtSpawn(int row, int slotIndex)
        {
            BookData? occupant = SlotOccupants[row, slotIndex];
            if (occupant == null) return false;
            return occupant.Value.Equals(new BookData(RowGenres[row], slotIndex + 1));
        }
    }

    /// <summary>
    /// From a ShelfLayoutConfig + seed, produces the 4x10 slot grid with genre/sequence assignments,
    /// prefilled placements (correct and misplaced), and the shuffled list of books to deal to the table.
    /// Pure C#, no MonoBehaviour, fully deterministic per seed.
    /// </summary>
    public static class ShelfGenerator
    {
        public const int TotalSlots = ShelfGeometry.RowCount * ShelfGeometry.SlotsPerRow;

        public static ShelfLayoutResult Generate(ShelfLayoutConfig config, IReadOnlyList<GenreDefinition> genres,
            int seed, ShelfType shelfType = ShelfType.Standard)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (genres == null || genres.Count != ShelfGeometry.RowCount)
                throw new ArgumentException($"Expected exactly {ShelfGeometry.RowCount} genres.", nameof(genres));
            if (!Enum.IsDefined(typeof(ShelfType), shelfType))
                throw new ArgumentOutOfRangeException(nameof(shelfType));
            config.Validate();

            var rng = new System.Random(seed);

            var rowGenres = new GenreDefinition[genres.Count];
            for (int i = 0; i < genres.Count; i++) rowGenres[i] = genres[i];
            if (config.shuffleRowOrder) Shuffle(rowGenres, rng);

            float alreadyCorrectFrac = Mathf.Lerp(config.alreadyCorrectMin, config.alreadyCorrectMax, (float)rng.NextDouble());
            float misplacedFrac = Mathf.Lerp(config.misplacedMin, config.misplacedMax, (float)rng.NextDouble());

            int correctCount = Mathf.Clamp(Mathf.RoundToInt(alreadyCorrectFrac * TotalSlots), 0, TotalSlots);
            // Special shelves always need an action. A full Repair shelf also needs at least two
            // wrong books: one isolated wrong slot cannot form a valid permutation.
            if (shelfType == ShelfType.Delivery) correctCount = Math.Min(correctCount, TotalSlots - 1);
            if (shelfType == ShelfType.Repair) correctCount = Math.Min(correctCount, TotalSlots - 2);
            int misplacedCount = shelfType switch
            {
                ShelfType.Delivery => 0,
                ShelfType.Repair => TotalSlots - correctCount,
                _ => Mathf.Clamp(Mathf.RoundToInt(misplacedFrac * TotalSlots), 0, TotalSlots - correctCount),
            };
            int emptyCount = TotalSlots - correctCount - misplacedCount;

            if (misplacedCount == 1 && emptyCount == 0)
            {
                // A single misplaced slot with no empty slot has nowhere for its rightful book to
                // come from except itself - a derangement of size 1 is mathematically impossible.
                throw new InvalidOperationException(
                    $"{config.name}: rolled misplacedCount=1 with emptyCount=0 for seed {seed} - unsolvable. " +
                    "Widen the layout config's ranges so this combination can't occur.");
            }

            var allPositions = new List<(int row, int slot)>(TotalSlots);
            for (int r = 0; r < ShelfGeometry.RowCount; r++)
                for (int s = 0; s < ShelfGeometry.SlotsPerRow; s++)
                    allPositions.Add((r, s));
            Shuffle(allPositions, rng);

            var correctPositions = allPositions.GetRange(0, correctCount);
            var misplacedPositions = allPositions.GetRange(correctCount, misplacedCount);
            var emptyPositions = allPositions.GetRange(correctCount + misplacedCount, emptyCount);

            var occupants = new BookData?[ShelfGeometry.RowCount, ShelfGeometry.SlotsPerRow];
            foreach (var pos in correctPositions)
                occupants[pos.row, pos.slot] = new BookData(rowGenres[pos.row], pos.slot + 1);

            // Homeless books: the identity books for every slot that does NOT spawn correct.
            // They must land either on a misplaced board slot (never their own home) or the table.
            var homeless = new List<BookData>(misplacedCount + emptyCount);
            foreach (var pos in misplacedPositions) homeless.Add(new BookData(rowGenres[pos.row], pos.slot + 1));
            foreach (var pos in emptyPositions) homeless.Add(new BookData(rowGenres[pos.row], pos.slot + 1));
            Shuffle(homeless, rng);

            // destination[i] >= 0 -> index into misplacedPositions (a board slot with a "not this book" constraint)
            // destination[i] == -1 -> the table (no constraint)
            var destinations = new List<int>(misplacedCount + emptyCount);
            for (int i = 0; i < misplacedCount; i++) destinations.Add(i);
            for (int i = 0; i < emptyCount; i++) destinations.Add(-1);
            Shuffle(destinations, rng);

            BookData HomeOf(int misplacedIndex)
            {
                var p = misplacedPositions[misplacedIndex];
                return new BookData(rowGenres[p.row], p.slot + 1);
            }

            bool ConflictAt(int i)
            {
                int d = destinations[i];
                return d >= 0 && homeless[i].Equals(HomeOf(d));
            }

            int FindConflict()
            {
                for (int i = 0; i < homeless.Count; i++)
                    if (ConflictAt(i)) return i;
                return -1;
            }

            int guard = 0;
            int conflict = FindConflict();
            while (conflict != -1)
            {
                if (++guard > 100000)
                    throw new InvalidOperationException("ShelfGenerator: could not resolve a valid misplaced-book arrangement within the iteration budget.");

                int j;
                do { j = rng.Next(homeless.Count); } while (j == conflict);
                (homeless[conflict], homeless[j]) = (homeless[j], homeless[conflict]);
                conflict = FindConflict();
            }

            var dealt = new List<BookData>(emptyCount);
            for (int i = 0; i < homeless.Count; i++)
            {
                int d = destinations[i];
                if (d < 0)
                {
                    dealt.Add(homeless[i]);
                }
                else
                {
                    var p = misplacedPositions[d];
                    occupants[p.row, p.slot] = homeless[i];
                }
            }
            Shuffle(dealt, rng);

            return new ShelfLayoutResult
            {
                RowGenres = rowGenres,
                SlotOccupants = occupants,
                DealtBooks = dealt,
                RequiredMoves = TotalSlots - correctCount
            };
        }

        private static void Shuffle<T>(IList<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
