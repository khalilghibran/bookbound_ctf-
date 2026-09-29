using UnityEngine;
using UnityEngine.UI;

namespace BookBound
{
    /// <summary>
    /// Renders a fixed-width numeric/time string (score, countdown) as a row of pre-sliced digit
    /// sprites instead of a font, matching the bitmap-numeral art style. Slots are fixed at scene-build
    /// time; <see cref="SetText"/> just re-points each slot's sprite, so this never allocates or
    /// resizes at runtime.
    /// </summary>
    public class DigitStrip : MonoBehaviour
    {
        [Tooltip("One Image per character position, left to right.")]
        [SerializeField] private Image[] slots;

        [Tooltip("Index 0..9 -> digit sprites '0'..'9'.")]
        [SerializeField] private Sprite[] digitSprites = new Sprite[10];

        [SerializeField] private Sprite colonSprite;

        /// <summary>Characters beyond this get '0'-'9' or ':' rendered; anything else (including a
        /// short string's implicit blanks) just disables that slot.</summary>
        public void SetText(string text)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                char c = i < text.Length ? text[i] : ' ';
                Sprite sprite = c switch
                {
                    >= '0' and <= '9' => digitSprites[c - '0'],
                    ':' => colonSprite,
                    _ => null,
                };
                slots[i].sprite = sprite;
                slots[i].enabled = sprite != null;
            }
        }

        public void SetColor(Color color)
        {
            foreach (Image slot in slots) slot.color = color;
        }
    }
}
