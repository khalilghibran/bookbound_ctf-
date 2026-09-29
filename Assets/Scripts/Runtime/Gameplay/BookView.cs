using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BookBound
{
    /// <summary>
    /// Renders one BookData as either its cover (table, flat/laying) or spine (shelved, upright), and
    /// handles drag/drop between IDropTargets (plan.md sections 6 and 11): zero-smoothing drag with a
    /// preserved grab offset, generous snap-radius drop resolution, and instant cover/spine switching.
    /// All motion beyond the raw drag-follow (pickup, hover, drag lean, and every placement outcome) is
    /// layered on through UITween so it can be tuned/disabled centrally via AnimationSettings.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class BookView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        private const float SnapRadiusAtReferenceRes = 60f;
        private const float ReferenceHeight = 1080f;

        [SerializeField] private Image image;

        [Tooltip("Shown instead of the real cover while Blind Sort mode is active. Assign one shared " +
                 "'mystery book' sprite on the prefab (plain color / question-mark design).")]
        [SerializeField] private Sprite mysteryCoverSprite;

        /// <summary>
        /// Blind Sort mode toggle (plan.md new-mode addendum). When true, ShowCover renders
        /// <see cref="mysteryCoverSprite"/> instead of the book's real cover art, so genre and sequence
        /// number stay hidden until the book is actually placed (ShowSpine always reveals the truth -
        /// placement resolution and IsCorrect checks are completely unaffected by this flag).
        /// Set by GameManager/mode-select UI before a run starts; not meant to change mid-shelf.
        /// </summary>
        public static bool BlindSortModeActive { get; set; }

        private IDropTarget _currentTarget;
        private IDropTarget _hoverTarget;
        private Vector2 _grabOffset;
        private bool _dragging;
        private static int _activeDragCount;
        private float _dragTargetAngle;
        private Vector2 _lastDragScreenPos;

        public BookData Data { get; private set; }

        public static bool AnyDragging => _activeDragCount > 0;

        /// <summary>Fires after any drag resolves (successful move, swap, or bounce-back) so GameManager can re-check win state.</summary>
        public static event System.Action OnAnyBookMoved;

        public void Bind(BookData data)
        {
            Data = data;
        }

        public void SetCurrentTarget(IDropTarget target)
        {
            _currentTarget = target;
        }

        public void ShowCover()
        {
            image.sprite = BlindSortModeActive && mysteryCoverSprite != null
                ? mysteryCoverSprite
                : Data.genre.GetCoverSprite(Data.sequenceNumber);
            image.preserveAspect = true;
            SetAlpha(1f);
        }

        public void ShowSpine()
        {
            image.sprite = Data.genre.GetSpineSprite(Data.sequenceNumber);
            // Stretch to fill the slot exactly, not preserveAspect: shelved books sit flush against
            // their neighbors with no gap. preserveAspect would letterbox the spine within its slot,
            // leaving visible margin on either side even when every slot in a row is filled.
            image.preserveAspect = false;
            SetAlpha(1f);
        }

        public void SetAlpha(float alpha)
        {
            Color c = image.color;
            c.a = alpha;
            image.color = c;
        }

        /// <summary>Waiting-queue books (BookQueue-managed, non-active table positions) aren't draggable.
        /// All-books-always-draggable (plan.md section 6) still holds for shelved and active-slot books.</summary>
        public void SetInteractable(bool interactable)
        {
            image.raycastTarget = interactable;
        }

        /// <summary>Reparents into the given container and stretches to fill it.</summary>
        public void FillParent(RectTransform parent)
        {
            var rt = (RectTransform)transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Plays a new-bookcase spawn-in: slides down from <paramref name="slideDistance"/>px
        /// above rest while fading in, after an optional stagger delay. Assumes it has already been
        /// placed at its final anchored rect (FillParent + zero offsets) by the caller.</summary>
        public void PlaySpawnIn(float delay, float slideDistance, float duration)
        {
            var rt = (RectTransform)transform;
            if (!AnimationSettings.Instance.enabled)
            {
                SetAlpha(1f);
                return;
            }

            Vector2 restPos = rt.anchoredPosition;
            rt.anchoredPosition = restPos + new Vector2(0f, slideDistance);
            SetAlpha(0f);
            StartCoroutine(SpawnInRoutine(delay, restPos, duration));
        }

        private IEnumerator SpawnInRoutine(float delay, Vector2 restPos, float duration)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            var rt = (RectTransform)transform;
            UITween.MoveTo(rt, restPos, duration, EaseType.OutBack);
            UITween.FadeTo(image, 1f, duration, EaseType.OutQuad);
        }

        /// <summary>Hover lift, table books only (plan.md section 11) - shelved books sit packed against
        /// their neighbors, where a lift reads as the row twitching rather than as feedback. No shadow:
        /// the art set has no drop-shadow sprite, and a faked one isn't worth the extra draw call.</summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_dragging || !(_currentTarget is TableSlot)) return;
            var s = AnimationSettings.Instance.tableBooks;
            var rt = (RectTransform)transform;
            UITween.ScaleTo(rt, Vector3.one * s.hoverScale, s.hoverDuration, EaseType.OutQuad);
            UITween.MoveTo(rt, new Vector2(0f, s.hoverLift), s.hoverDuration, EaseType.OutQuad);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_dragging) return;
            var s = AnimationSettings.Instance.tableBooks;
            var rt = (RectTransform)transform;
            UITween.ScaleTo(rt, Vector3.one, s.hoverDuration, EaseType.OutQuad);
            UITween.MoveTo(rt, Vector2.zero, s.hoverDuration, EaseType.OutQuad);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            var rt = (RectTransform)transform;
            UITween.Kill(rt); // cancel any in-flight hover lift/scale before the manual reparent below
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            // Capture current visual bounds in screen space before reparenting - stretch anchors
            // (0,0)-(1,1) would otherwise be reinterpreted against DragLayer's very different size.
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector2 size = new Vector2(corners[2].x - corners[0].x, corners[2].y - corners[0].y);
            Vector3 center = (corners[0] + corners[2]) / 2f;

            rt.SetParent(DragLayerMarker.Instance, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.position = center;

            _grabOffset = (Vector2)rt.position - eventData.position;
            _lastDragScreenPos = eventData.position;
            _dragTargetAngle = 0f;

            // Dragging a book out of a slot flips it back to the cover sprite immediately (plan.md section 5).
            ShowCover();
            var s = AnimationSettings.Instance.drag;
            UITween.FadeTo(image, s.pickupAlpha, s.pickupDuration, EaseType.OutQuad);
            UITween.ScaleTo(rt, Vector3.one * s.pickupScale, s.pickupDuration, EaseType.OutQuad);
            image.raycastTarget = false;
            _dragging = true;
            _activeDragCount++;
            AudioManager.PlayPickup();
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Zero smoothing: the book tracks the pointer exactly, preserving the grab-time offset.
            ((RectTransform)transform).position = eventData.position + _grabOffset;

            var s = AnimationSettings.Instance.drag;
            float deltaX = eventData.position.x - _lastDragScreenPos.x;
            _lastDragScreenPos = eventData.position;
            _dragTargetAngle = Mathf.Clamp(-deltaX * s.leanSensitivity, -s.maxLeanDegrees, s.maxLeanDegrees);

            IDropTarget hovered = FindBestTarget(eventData.position);
            if (hovered == _currentTarget) hovered = null; // dropping back home isn't a move
            if (hovered == _hoverTarget) return;

            _hoverTarget?.SetHighlight(false);
            _hoverTarget = hovered;
            _hoverTarget?.SetHighlight(true);
        }

        /// <summary>Drives the drag-lean rotation every frame while dragging - a per-frame damped chase
        /// toward <see cref="_dragTargetAngle"/> rather than a fire-and-forget tween, since the target
        /// keeps changing every time the pointer moves.</summary>
        private void Update()
        {
            if (!_dragging) return;
            if (!AnimationSettings.Instance.enabled)
            {
                transform.localRotation = Quaternion.identity;
                return;
            }

            var s = AnimationSettings.Instance.drag;
            float current = transform.localEulerAngles.z;
            if (current > 180f) current -= 360f;
            float damp = 1f - Mathf.Exp(-s.leanDamp * Time.unscaledDeltaTime);
            float next = Mathf.LerpAngle(current, _dragTargetAngle, damp);
            transform.localRotation = Quaternion.Euler(0f, 0f, next);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            image.raycastTarget = true;
            EndDragTracking();
            var s = AnimationSettings.Instance.drag;
            UITween.RotateTo(transform, 0f, s.pickupDuration, EaseType.OutQuad);
            _hoverTarget?.SetHighlight(false);
            _hoverTarget = null;

            IDropTarget origin = _currentTarget;
            IDropTarget best = FindBestTarget(eventData.position);

            if (best == null || best == origin || best.Occupant == this)
            {
                ReturnToOrigin(origin, s.returnDuration);
                AudioManager.PlayPlaceNeutral();
            }
            else if (best.Occupant == null)
            {
                origin.Clear();
                bool correct = best is ShelfSlot slot && slot.IsCorrect(Data);
                SnapWithPunch(best, correct);
                PlayLandingSound(best, swapped: false);
            }
            else if (best.AllowsSwap)
            {
                BookView displaced = best.Occupant;
                SwapAnimated(best, origin, displaced);
                PlayLandingSound(best, swapped: true);
            }
            else
            {
                // Occupied, no-swap target (a filled table position) - every OTHER drop is legal, but
                // this one bounces back (plan.md section 6).
                ReturnToOrigin(origin, s.returnDuration);
                AudioManager.PlayPlaceNeutral();
            }

            OnAnyBookMoved?.Invoke();
        }

        private void OnDisable() => EndDragTracking();

        private void EndDragTracking()
        {
            if (!_dragging) return;
            _dragging = false;
            _activeDragCount = Mathf.Max(0, _activeDragCount - 1);
        }

        /// <summary>Invalid drop / dropping back where it came from: tweens back rather than snapping,
        /// so the player can see where the book actually went.</summary>
        private void ReturnToOrigin(IDropTarget origin, float duration)
        {
            var rt = (RectTransform)transform;
            Vector3 fromWorld = rt.position;
            origin.Place(this); // snaps anchors/visuals instantly; we animate the visual position over it
            Vector3 toWorld = rt.position;
            rt.position = fromWorld;

            UITween.ScaleTo(rt, Vector3.one, duration, EaseType.OutQuad);
            UITween.MoveTo(rt, toWorld, duration, EaseType.OutQuad);
        }

        /// <summary>Successful drop into an empty target: the book snaps to its slot immediately, then
        /// punches - big and warm for a correct placement, small and plain otherwise.</summary>
        private void SnapWithPunch(IDropTarget target, bool correct)
        {
            target.Place(this);
            PlayLandingFeedback(correct);
        }

        /// <summary>Two books trade places, crossing visibly rather than teleporting (plan.md section 7).</summary>
        private void SwapAnimated(IDropTarget best, IDropTarget origin, BookView displaced)
        {
            var s = AnimationSettings.Instance.drag;
            var thisRt = (RectTransform)transform;
            var otherRt = (RectTransform)displaced.transform;

            Vector3 thisFrom = thisRt.position;
            Vector3 otherFrom = otherRt.position;

            bool correctForThis = best is ShelfSlot bs && bs.IsCorrect(Data);
            bool correctForOther = origin is ShelfSlot os && os.IsCorrect(displaced.Data);

            best.Place(this);
            origin.Place(displaced);

            Vector3 thisTo = thisRt.position;
            Vector3 otherTo = otherRt.position;
            thisRt.position = thisFrom;
            otherRt.position = otherFrom;

            UITween.MoveTo(thisRt, thisTo, s.swapDuration, EaseType.InOutQuad, () => PlayLandingFeedback(correctForThis));
            UITween.MoveTo(otherRt, otherTo, s.swapDuration, EaseType.InOutQuad, () => displaced.PlayLandingFeedback(correctForOther));
        }

        /// <summary>The punch (+ warm flash if correct) that caps off any successful placement, whether
        /// it landed in an empty slot or arrived via a swap.</summary>
        private void PlayLandingFeedback(bool correct)
        {
            var s = AnimationSettings.Instance.drag;
            var rt = (RectTransform)transform;
            rt.localScale = Vector3.one;

            if (correct)
            {
                UITween.PunchScale(rt, s.correctPunchScale, s.correctDuration, Vector3.one, EaseType.OutBack);
                FlashWarm(s.correctDuration, s.correctFlashColor);
            }
            else
            {
                UITween.PunchScale(rt, s.neutralPunchScale, s.neutralDuration, Vector3.one, EaseType.OutQuad);
            }
        }

        private void FlashWarm(float duration, Color flashColor)
        {
            UITween.ColorTo(image, flashColor, duration * 0.3f, EaseType.OutQuad,
                () => UITween.ColorTo(image, Color.white, duration * 0.7f, EaseType.OutQuad));
        }

        private void PlayLandingSound(IDropTarget landed, bool swapped)
        {
            if (landed is ShelfSlot slot && slot.IsCorrect(Data)) AudioManager.PlayPlaceCorrect();
            else if (swapped) AudioManager.PlaySwap();
            else AudioManager.PlayPlaceNeutral();
        }

        private static IDropTarget FindBestTarget(Vector2 screenPoint)
        {
            float snapRadius = SnapRadiusAtReferenceRes * (Screen.height / ReferenceHeight);
            IDropTarget best = null;
            float bestDist = snapRadius;

            foreach (var target in DropTargetRegistry.All)
            {
                Vector2 center = target.RectTransform.position;
                float dist = Vector2.Distance(center, screenPoint);
                if (dist <= bestDist)
                {
                    bestDist = dist;
                    best = target;
                }
            }
            return best;
        }
    }
}
