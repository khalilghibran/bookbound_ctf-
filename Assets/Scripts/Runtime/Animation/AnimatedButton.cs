using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BookBound
{
    /// <summary>
    /// Attach to any button to get hover/press/release/disabled feedback (hover lift, press squash,
    /// overshoot on release, dim when disabled) with zero per-button hand animation. One component,
    /// reused everywhere a Button exists - purely visual, so it never delays or blocks the click:
    /// Unity's EventSystem fires Button.onClick on pointer-up independently of whatever this component
    /// is mid-tweening.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public class AnimatedButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        [Tooltip("Tinted/faded graphic. Defaults to the Selectable's own targetGraphic.")]
        [SerializeField] private Graphic targetGraphic;

        [Tooltip("If set, hover swaps targetGraphic's sprite to this instead of tinting its color - for " +
                 "buttons whose hover look is pre-baked art (e.g. the title-screen menu) rather than a flat " +
                 "tintable color. targetGraphic must be an Image.")]
        [SerializeField] private Sprite hoverSprite;

        private Selectable _selectable;
        private Image _image;
        private Sprite _normalSprite;
        private Color _baseColor;
        private bool _hovered;
        private bool _pressed;
        private bool _wasInteractable = true;

        private static AnimationSettings.ButtonSettings Settings => AnimationSettings.Instance.buttons;

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
            if (targetGraphic == null) targetGraphic = _selectable.targetGraphic != null ? _selectable.targetGraphic : GetComponent<Graphic>();
            if (targetGraphic != null) _baseColor = targetGraphic.color;

            _image = targetGraphic as Image;
            if (_image != null) _normalSprite = _image.sprite;
        }

        private void OnEnable()
        {
            transform.localScale = Vector3.one;
            _hovered = false;
            _pressed = false;
            _wasInteractable = IsInteractable();
            ApplyInteractable(instant: true);
        }

        private void OnDisable()
        {
            UITween.Kill(transform);
            if (targetGraphic != null) UITween.KillColor(targetGraphic);
        }

        // Selectable exposes no "interactable changed" event, so a cheap per-frame poll is the simplest
        // way to catch it toggling from elsewhere (e.g. a panel disabling a button while busy).
        private void Update()
        {
            bool interactable = IsInteractable();
            if (interactable != _wasInteractable)
            {
                _wasInteractable = interactable;
                ApplyInteractable(instant: false);
            }
        }

        private bool IsInteractable() => _selectable == null || _selectable.IsInteractable();

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            if (!IsInteractable()) return;
            UITween.ScaleTo(transform, Vector3.one * Settings.hoverScale, Settings.hoverDuration, EaseType.OutQuad);
            if (hoverSprite != null && _image != null) _image.sprite = hoverSprite;
            else TintTo(_baseColor * Settings.hoverTintBrighten);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            if (!IsInteractable() || _pressed) return;
            UITween.ScaleTo(transform, Vector3.one, Settings.hoverDuration, EaseType.OutQuad);
            if (hoverSprite != null && _image != null) _image.sprite = _normalSprite;
            else TintTo(_baseColor);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            if (!IsInteractable()) return;
            UITween.ScaleTo(transform, Vector3.one * Settings.pressScale, Settings.pressDuration, EaseType.OutQuad);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            if (!IsInteractable()) return;
            float target = _hovered ? Settings.releaseOvershootScale : 1f;
            UITween.ScaleTo(transform, Vector3.one * target, Settings.releaseDuration, EaseType.OutBack);
            if (!_hovered)
            {
                if (hoverSprite != null && _image != null) _image.sprite = _normalSprite;
                else TintTo(_baseColor);
            }
        }

        private void ApplyInteractable(bool instant)
        {
            bool interactable = IsInteractable();
            float alpha = interactable ? _baseColor.a : Settings.disabledAlpha;
            if (targetGraphic != null)
                UITween.FadeTo(targetGraphic, alpha, instant ? 0f : Settings.disabledDuration, EaseType.OutQuad);

            if (!interactable)
            {
                _hovered = false;
                _pressed = false;
                UITween.ScaleTo(transform, Vector3.one, instant ? 0f : Settings.hoverDuration, EaseType.OutQuad);
                if (hoverSprite != null && _image != null) _image.sprite = _normalSprite;
            }
        }

        private void TintTo(Color color)
        {
            if (targetGraphic == null) return;
            // Preserve whatever alpha FadeTo/ApplyInteractable currently has in flight - tint is a
            // color-channel effect only, disabled-dimming is an alpha effect only, and they must not
            // stomp each other since both animate targetGraphic.color.
            Color c = color;
            c.a = targetGraphic.color.a;
            UITween.ColorTo(targetGraphic, c, Settings.hoverDuration, EaseType.OutQuad);
        }
    }
}
