using System.Collections.Generic;
using UnityEngine;

namespace BookBound
{
    /// <summary>
    /// The "Genre Guide": a single parchment background with one text line per row - "ROW 1 - FANTASY"
    /// and so on. No per-genre art at all, so there's nothing to look up by genre id; each row's line
    /// just reflects whatever genre <see cref="SetRowGenres"/> says currently occupies it, which is
    /// already resolved correctly upstream (accounting for shuffleRowOrder) by the caller.
    /// </summary>
    public class InfoPanelController : MonoBehaviour
    {
        [SerializeField] private SpriteText[] rowLabels = new SpriteText[ShelfGeometry.RowCount];

        public void SetRowGenres(IReadOnlyList<GenreDefinition> rowGenres)
        {
            for (int i = 0; i < rowLabels.Length; i++)
            {
                if (rowLabels[i] == null) continue;

                GenreDefinition genre = (rowGenres != null && i < rowGenres.Count) ? rowGenres[i] : null;
                rowLabels[i].SetText(genre != null ? $"ROW {i + 1} - {genre.DisplayName}" : $"ROW {i + 1}");
            }
        }
    }
}
