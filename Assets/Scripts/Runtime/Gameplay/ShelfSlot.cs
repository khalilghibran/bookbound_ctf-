using UnityEngine;

namespace BookBound
{
    /// <summary>
    /// One board position. (rowIndex, slotIndex) are baked in at scene-build time; rowGenre is set
    /// live per bookcase (shuffleRowOrder can change genre-per-row spawn to spawn). Always accepts a
    /// drop, swapping if occupied (plan.md section 6) - this is what guarantees the puzzle can never
    /// hard-deadlock.
    /// </summary>
    public class ShelfSlot : MonoBehaviour, IDropTarget
    {
        [SerializeField] private int rowIndex;
        [SerializeField] private int slotIndex;

        public int RowIndex => rowIndex;
        public int SlotIndex => slotIndex;
        public int ExpectedSequenceNumber => slotIndex + 1;
        public GenreDefinition RowGenre { get; private set; }
        public BookView Occupant { get; private set; }
        public bool HasScored { get; private set; }

        public RectTransform RectTransform => (RectTransform)transform;
        public bool AllowsSwap => true;

        private SlotHighlight _highlight;

        private void Awake() => _highlight = GetComponentInChildren<SlotHighlight>(true);

        private void OnEnable() => DropTargetRegistry.Register(this);
        private void OnDisable() => DropTargetRegistry.Unregister(this);

        public void SetHighlight(bool on)
        {
            if (_highlight == null) return;
            if (on) _highlight.Show(Occupant != null);
            else _highlight.Hide();
        }

        public void Configure(int row, int slot)
        {
            rowIndex = row;
            slotIndex = slot;
        }

        public void SetRowGenre(GenreDefinition genre)
        {
            RowGenre = genre;
        }

        public bool IsCorrect(BookData book)
        {
            return RowGenre != null && book.genre != null
                && book.genre.Id == RowGenre.Id
                && book.sequenceNumber == ExpectedSequenceNumber;
        }

        public void Place(BookView book)
        {
            Occupant = book;
            book.SetCurrentTarget(this);
            book.FillParent(RectTransform);
            book.ShowSpine();
            book.SetAlpha(1f);
        }

        public void Clear()
        {
            Occupant = null;
        }

        public void ResetForNewBookcase()
        {
            Occupant = null;
            HasScored = false;
        }

        public void MarkScored()
        {
            HasScored = true;
        }
    }
}
