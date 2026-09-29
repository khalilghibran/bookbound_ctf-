using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BookBound
{
    /// <summary>
    /// Owns the table as a single-file conveyor: anchors[0] is the only active (draggable) position,
    /// nearest the shelf; anchors[1..] hold non-interactive "waiting their turn" books. Clearing the
    /// active book shifts every waiting book one step left and slides the next backlog book in from
    /// off-screen right.
    /// </summary>
    public class BookQueue : MonoBehaviour
    {
        [SerializeField] private RectTransform[] anchors;
        [SerializeField] private RectTransform spawnPoint;

        private readonly List<BookData> _backlog = new List<BookData>();
        private BookView[] _queue;
        private GameObject _bookPrefab;
        private TableSlot _activeSlot;
        private bool _isShifting;

        /// <summary>A shuffle is available only while the table is settled and at least two books
        /// remain. The active book must be included so the button can change the next choice.</summary>
        public bool CanShuffle
        {
            get
            {
                if (_queue == null || _isShifting || BookView.AnyDragging) return false;
                SyncActiveBook();
                if (_queue[0] == null) return false;

                int remaining = _backlog.Count;
                foreach (BookView book in _queue)
                    if (book != null) remaining++;
                return remaining > 1;
            }
        }

        public void Populate(IReadOnlyList<BookData> dealtBooks, GameObject bookPrefab)
        {
            StopAllCoroutines(); // abandon any in-flight shift animation from the previous bookcase
            _isShifting = false;
            SyncActiveBook();

            if (_queue != null)
            {
                foreach (BookView book in _queue)
                {
                    if (book == null) continue;
                    UITween.Kill(book.transform); // tweens outlive BookQueue coroutines
                    Object.Destroy(book.gameObject);
                }
            }

            _bookPrefab = bookPrefab;
            _backlog.Clear();
            _backlog.AddRange(dealtBooks);
            _queue = new BookView[anchors.Length];

            _activeSlot = anchors[0].GetComponent<TableSlot>();
            if (_activeSlot == null) _activeSlot = anchors[0].gameObject.AddComponent<TableSlot>();
            _activeSlot.OnEmptied -= HandleActiveEmptied;
            _activeSlot.OnEmptied += HandleActiveEmptied;
            _activeSlot.AssignOccupant(null);

            int count = Mathf.Min(anchors.Length, _backlog.Count);
            for (int i = 0; i < count; i++)
            {
                BookView view = CreateBook(_backlog[0]);
                _backlog.RemoveAt(0);
                _queue[i] = view;
                view.FillParent(anchors[i]);
                view.SetInteractable(i == 0);
            }

            if (_queue[0] != null)
                _activeSlot.AssignOccupant(_queue[0]);
        }

        /// <summary>Permutes the active, visible waiting, and hidden backlog books without moving
        /// anything already on the shelf. Rebinding the settled table views preserves their target
        /// ownership and avoids destroying/recreating books during a live run.</summary>
        public bool ShuffleRemaining(int seed)
        {
            if (!CanShuffle) return false;

            var remaining = new List<BookData>(_queue.Length + _backlog.Count);
            foreach (BookView book in _queue)
                if (book != null) remaining.Add(book.Data);
            remaining.AddRange(_backlog);

            BookData previousActive = _queue[0].Data;
            var rng = new System.Random(seed);
            for (int i = remaining.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (remaining[i], remaining[j]) = (remaining[j], remaining[i]);
            }

            // A button press must visibly change the playable book when a choice exists.
            if (remaining[0].Equals(previousActive))
            {
                int j = 1 + rng.Next(remaining.Count - 1);
                (remaining[0], remaining[j]) = (remaining[j], remaining[0]);
            }

            int index = 0;
            foreach (BookView book in _queue)
            {
                if (book == null) continue;
                book.Bind(remaining[index++]);
                book.ShowCover();
            }

            _backlog.Clear();
            while (index < remaining.Count) _backlog.Add(remaining[index++]);
            return true;
        }

        /// <summary>Table/shelf swaps replace the active TableSlot occupant without emptying it.
        /// Read that slot before using the cached queue array so a later shuffle or reset acts on
        /// the book actually sitting on the table.</summary>
        private void SyncActiveBook()
        {
            if (_queue != null && _queue.Length > 0 && _activeSlot != null)
                _queue[0] = _activeSlot.Occupant;
        }

        private BookView CreateBook(BookData data)
        {
            var bookGo = Object.Instantiate(_bookPrefab);
            var view = bookGo.GetComponent<BookView>();
            view.Bind(data);
            view.ShowCover();
            return view;
        }

        private void HandleActiveEmptied(TableSlot slot)
        {
            if (_isShifting) return;
            _isShifting = true;
            StartCoroutine(ShiftAndFeedRoutine());
        }

        private IEnumerator ShiftAndFeedRoutine()
        {
            _queue[0] = null;
            var s = AnimationSettings.Instance.tableBooks;
            Vector2 bookSize = anchors[0].rect.size;
            var moving = new List<(BookView view, RectTransform target, float delay, float duration, EaseType ease)>();

            // Detach every currently-waiting book from its stretch-fit anchor into a free-floating
            // fixed-size transform (world position preserved) so it can be lerped smoothly.
            for (int i = 1; i < _queue.Length; i++)
            {
                if (_queue[i] == null) continue;
                DetachForAnimation(_queue[i], bookSize);
                moving.Add((_queue[i], anchors[i - 1], (i - 1) * s.shiftStagger, s.shiftDuration, EaseType.OutQuad));
            }

            for (int i = 0; i < _queue.Length - 1; i++)
                _queue[i] = _queue[i + 1];
            _queue[_queue.Length - 1] = null;

            if (_backlog.Count > 0)
            {
                BookView newBook = CreateBook(_backlog[0]);
                _backlog.RemoveAt(0);
                RectTransform rt = DetachForAnimation(newBook, bookSize);
                rt.position = spawnPoint.position;
                moving.Add((newBook, anchors[anchors.Length - 1], 0f, s.feedInDuration, EaseType.OutBack));
                _queue[anchors.Length - 1] = newBook;
            }

            // Set logical occupancy before moving visuals. A shelf book cannot be dropped into a
            // transiently empty active slot while the next table book is sliding into it.
            _activeSlot.AssignOccupant(_queue[0]);
            foreach (BookView book in _queue) book?.SetInteractable(false);

            foreach (var (view, target, delay, duration, ease) in moving)
                StartCoroutine(MoveBookRoutine(view, target, delay, duration, ease));

            float maxWait = 0f;
            if (AnimationSettings.Instance.enabled)
                foreach (var m in moving) maxWait = Mathf.Max(maxWait, m.delay + m.duration);
            if (maxWait > 0f) yield return new WaitForSecondsRealtime(maxWait);

            foreach (var (view, target, _, _, _) in moving)
                view.FillParent(target);

            for (int i = 0; i < _queue.Length; i++)
                _queue[i]?.SetInteractable(i == 0);
            _isShifting = false;
        }

        private static IEnumerator MoveBookRoutine(BookView view, RectTransform target, float delay, float duration, EaseType ease)
        {
            if (AnimationSettings.Instance.enabled && delay > 0f)
                yield return new WaitForSecondsRealtime(delay);
            UITween.MoveTo((RectTransform)view.transform, target.position, duration, ease);
        }

        private RectTransform DetachForAnimation(BookView view, Vector2 size)
        {
            var rt = (RectTransform)view.transform;
            Vector3 worldPos = rt.position;
            rt.SetParent(DragLayerMarker.Instance, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.position = worldPos;
            return rt;
        }
    }
}
