using System;
using UnityEngine;

namespace BookBound
{
    /// <summary>
    /// One row rect per genre, measured from the bookcase sprite, each divided into 10 equal slot
    /// rects. Rects are normalized (0..1) against the bookcase image's own RectTransform, bottom-left
    /// origin, so they stay correct regardless of how large the bookcase is drawn on screen.
    /// </summary>
    [CreateAssetMenu(menuName = "BookBound/Shelf Geometry", fileName = "ShelfGeometry")]
    public class ShelfGeometry : ScriptableObject
    {
        /// <summary>One row per genre - the board has exactly as many rows as there are genres.</summary>
        public const int RowCount = 4;
        public const int SlotsPerRow = 10;

        [Serializable]
        public struct NormalizedRect
        {
            [Range(0f, 1f)] public float x;
            [Range(0f, 1f)] public float y;
            [Range(0f, 1f)] public float width;
            [Range(0f, 1f)] public float height;

            public NormalizedRect(float x, float y, float width, float height)
            {
                this.x = x;
                this.y = y;
                this.width = width;
                this.height = height;
            }
        }

        [Tooltip("One entry per row, top row (index 0) first. Normalized to the bookcase image rect, bottom-left origin.")]
        [SerializeField] private NormalizedRect[] rowRects = new NormalizedRect[RowCount];

        public NormalizedRect GetRowRect(int rowIndex) => rowRects[rowIndex];

        public NormalizedRect GetSlotRect(int rowIndex, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotsPerRow)
                throw new ArgumentOutOfRangeException(nameof(slotIndex));

            NormalizedRect row = rowRects[rowIndex];
            float slotWidth = row.width / SlotsPerRow;
            return new NormalizedRect(row.x + slotIndex * slotWidth, row.y, slotWidth, row.height);
        }

        public void SetRowRects(NormalizedRect[] rects)
        {
            if (rects == null || rects.Length != RowCount)
                throw new ArgumentException($"Expected exactly {RowCount} row rects.");
            rowRects = rects;
        }
    }
}
