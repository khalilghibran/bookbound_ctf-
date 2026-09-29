using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BookBound
{
    /// <summary>
    /// A horizontal 0..1 slider drawn from BookBound art. Unity's built-in Slider is not used because
    /// it wants a fixed handle/fill rig with its own RectTransform contract; this drives three plain
    /// Images off one drag region, which is less to fight when the art is a rail plus a gem.
    ///
    /// Deliberately renders nothing it does not have art for: the rail, fill and handle Images stay
    /// disabled until their sprites are assigned, so before the art lands the row is an invisible but
    /// fully working drag region rather than a placeholder white bar. Wiring the art later is three
    /// sprite assignments in SceneSetup and no code change here.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class VolumeSlider : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        [Tooltip("Full-width track background.")]
        [SerializeField] private Image rail;

        [Tooltip("Filled portion, left-anchored, width driven by value.")]
        [SerializeField] private Image fill;

        [Tooltip("Knob, centred on the value position.")]
        [SerializeField] private Image handle;

        [Tooltip("Invisible full-rect raycast target that actually receives the drag.")]
        [SerializeField] private Image dragRegion;

        [SerializeField] private float handleSize = 34f;

        [Range(0f, 1f)]
        [SerializeField] private float value = 1f;

        /// <summary>Fires on user input only, never on <see cref="SetValueWithoutNotify"/>, so a
        /// panel can push settings in without echoing them straight back out.</summary>
        public event Action<float> OnValueChanged;

        public float Value => value;

        private RectTransform _rt;
        private RectTransform Rt => _rt != null ? _rt : _rt = (RectTransform)transform;

        public void SetValueWithoutNotify(float v)
        {
            value = Mathf.Clamp01(v);
            Apply();
        }

        public void OnPointerDown(PointerEventData eventData) => DragTo(eventData);

        public void OnDrag(PointerEventData eventData) => DragTo(eventData);

        private void OnEnable() => Apply();

        private void DragTo(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    Rt, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;

            // Local x runs from -width/2 to +width/2 at a centred pivot; Rt.rect.xMin is correct for
            // any pivot. Track is inset by half a handle at each end so the knob never overhangs.
            float inset = handleSize * 0.5f;
            float min = Rt.rect.xMin + inset;
            float max = Rt.rect.xMax - inset;
            if (max <= min) return;

            float next = Mathf.Clamp01((local.x - min) / (max - min));
            if (Mathf.Approximately(next, value)) return;

            value = next;
            Apply();
            OnValueChanged?.Invoke(value);
        }

        private void Apply()
        {
            float inset = handleSize * 0.5f;
            float width = Rt.rect.width;
            float usable = Mathf.Max(0f, width - handleSize);
            float handleX = inset + usable * value;

            if (rail != null) rail.enabled = rail.sprite != null;
            if (dragRegion != null)
            {
                // Must stay enabled to receive raycasts, but must never paint - a sprite-less Image
                // with alpha 0 is an invisible hit area, not a white box.
                dragRegion.color = new Color(0f, 0f, 0f, 0f);
                dragRegion.raycastTarget = true;
            }

            if (fill != null)
            {
                // The fill is the SAME full-width rail sprite, clipped horizontally rather than
                // squashed - there is no separate filled-bar art, and scaling the rail down to the
                // value would drag its rounded end cap inward with it. Filled needs a sprite to do
                // anything at all (see HANDOFF), hence the enable check.
                fill.enabled = fill.sprite != null;
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                fill.fillAmount = width > 0f ? Mathf.Clamp01(handleX / width) : 0f;
            }

            if (handle != null)
            {
                handle.enabled = handle.sprite != null;
                var handleRt = (RectTransform)handle.transform;
                // handleSize is the HEIGHT; width follows the sprite so the gem never squashes.
                float aspect = handle.sprite != null
                    ? handle.sprite.rect.width / handle.sprite.rect.height
                    : 1f;
                handleRt.sizeDelta = new Vector2(handleSize * aspect, handleSize);
                handleRt.anchoredPosition = new Vector2(handleX, 0f);
            }
        }
    }
}
