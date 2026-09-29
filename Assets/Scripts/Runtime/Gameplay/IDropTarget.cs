using UnityEngine;

namespace BookBound
{
    /// <summary>
    /// A place a BookView can be dropped: a shelf slot or a table position. ShelfSlot always accepts
    /// (swapping if occupied); TableSlot only accepts an empty position (plan.md section 6: dropping
    /// on an occupied table position does nothing, unlike a shelf slot).
    /// </summary>
    public interface IDropTarget
    {
        RectTransform RectTransform { get; }
        BookView Occupant { get; }
        bool AllowsSwap { get; }

        /// <summary>Places the book here and updates its visuals. Caller is responsible for swap/origin bookkeeping.</summary>
        void Place(BookView book);

        /// <summary>Marks this target empty without placing anything (used when its occupant left for a non-swap move).</summary>
        void Clear();

        /// <summary>Tints this target while a dragged book hovers it. The target decides whether that
        /// reads as "place here" or "swap with what's here".</summary>
        void SetHighlight(bool on);
    }
}
