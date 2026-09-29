using System;
using UnityEngine;

namespace BookBound
{
    /// <summary>
    /// Populates the existing Row_0..3/Slot_0..9 hierarchy from a generated layout and reports whether
    /// every slot currently holds its correct book (plan.md section 6: checked after every placement/swap).
    /// </summary>
    public class ShelfController : MonoBehaviour
    {
        [SerializeField] private Transform shelfRoot;

        private readonly ShelfSlot[,] _slots = new ShelfSlot[ShelfGeometry.RowCount, ShelfGeometry.SlotsPerRow];

        public event Action OnSlotBecameCorrect;

        private void Awake()
        {
            for (int r = 0; r < ShelfGeometry.RowCount; r++)
            {
                Transform row = shelfRoot.Find($"Row_{r}");
                for (int s = 0; s < ShelfGeometry.SlotsPerRow; s++)
                {
                    Transform slotTransform = row.Find($"Slot_{s}");
                    _slots[r, s] = slotTransform.GetComponent<ShelfSlot>();
                }
            }
        }

        public void Populate(ShelfLayoutResult layout, GameObject bookPrefab)
        {
            AnimationSettings.BookcaseSettings anim = AnimationSettings.Instance.bookcase;

            for (int r = 0; r < ShelfGeometry.RowCount; r++)
            {
                for (int s = 0; s < ShelfGeometry.SlotsPerRow; s++)
                {
                    ShelfSlot slot = _slots[r, s];
                    if (slot.Occupant != null)
                        UnityEngine.Object.Destroy(slot.Occupant.gameObject);
                    slot.ResetForNewBookcase();
                    slot.SetRowGenre(layout.RowGenres[r]);

                    BookData? occupant = layout.SlotOccupants[r, s];
                    if (occupant == null) continue;

                    var bookGo = UnityEngine.Object.Instantiate(bookPrefab);
                    var view = bookGo.GetComponent<BookView>();
                    view.Bind(occupant.Value);
                    slot.Place(view);
                    if (slot.IsCorrect(occupant.Value))
                        slot.MarkScored();

                    // Sweeps top-left to bottom-right (row-major = row 0 is the top row) so the board
                    // visibly assembles instead of popping in all at once (plan.md section 13).
                    int sweepIndex = r * ShelfGeometry.SlotsPerRow + s;
                    view.PlaySpawnIn(sweepIndex * anim.prefillStagger, anim.spawnSlideDistance, anim.spawnDuration);
                }
            }
        }

        /// <summary>Call after any placement/swap resolves. Marks newly-correct slots scored and
        /// reports whether the whole bookcase is now solved.</summary>
        public bool CheckState()
        {
            bool allCorrect = true;
            for (int r = 0; r < ShelfGeometry.RowCount; r++)
            {
                for (int s = 0; s < ShelfGeometry.SlotsPerRow; s++)
                {
                    ShelfSlot slot = _slots[r, s];
                    BookView occupant = slot.Occupant;
                    bool correct = occupant != null && slot.IsCorrect(occupant.Data);

                    if (correct && !slot.HasScored)
                    {
                        slot.MarkScored();
                        OnSlotBecameCorrect?.Invoke();
                    }

                    if (!correct) allCorrect = false;
                }
            }
            return allCorrect;
        }
    }
}
