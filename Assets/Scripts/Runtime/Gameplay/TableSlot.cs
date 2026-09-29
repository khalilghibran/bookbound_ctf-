using System;
using UnityEngine;

namespace BookBound
{
    /// <summary>
    /// The single active (draggable) table position - the leftmost slot, nearest the shelf. Unlike
    /// ShelfSlot, does NOT swap - dropping onto it while occupied does nothing (plan.md section 6).
    /// The other 4 table anchors are BookQueue-managed "waiting" positions, not IDropTargets: only the
    /// active slot is ever a valid drop destination. Fires OnEmptied so BookQueue can shift the queue
    /// and feed the next backlog book in when this position frees up.
    /// </summary>
    public class TableSlot : MonoBehaviour, IDropTarget
    {
        public BookView Occupant { get; private set; }
        public RectTransform RectTransform => (RectTransform)transform;
        public bool AllowsSwap => false;

        public event Action<TableSlot> OnEmptied;

        private SlotHighlight _highlight;

        private void Awake() => _highlight = GetComponentInChildren<SlotHighlight>(true);

        private void OnEnable() => DropTargetRegistry.Register(this);
        private void OnDisable() => DropTargetRegistry.Unregister(this);

        /// <summary>Only highlights when empty - dropping onto an occupied table position does nothing,
        /// so promising a drop there would be a lie.</summary>
        public void SetHighlight(bool on)
        {
            if (_highlight == null) return;
            if (on && Occupant == null) _highlight.Show(false);
            else _highlight.Hide();
        }

        public void Place(BookView book)
        {
            Occupant = book;
            book.SetCurrentTarget(this);
            book.FillParent(RectTransform);
            book.ShowCover();
            book.SetAlpha(1f);
            book.SetInteractable(true);
        }

        /// <summary>Assigns bookkeeping only - used by BookQueue's slide animation, which has already
        /// positioned/parented the book itself.</summary>
        public void AssignOccupant(BookView book)
        {
            Occupant = book;
            if (book == null) return;
            book.SetCurrentTarget(this);
            book.SetInteractable(true);
        }

        public void Clear()
        {
            Occupant = null;
            OnEmptied?.Invoke(this);
        }
    }
}
