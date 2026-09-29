using System;
using System.Collections.Generic;
using UnityEngine;

namespace BookBound
{
    /// <summary>
    /// The BookBound letterforms as a lookup table, so every <see cref="SpriteText"/> in the scene
    /// shares one glyph set instead of serializing 36 sprite references each.
    ///
    /// Metrics are stored as multiples of cap height rather than pixels: a SpriteText picks a cap
    /// height and every glyph scales off it, so the same asset drives a 20px hint and a 72px heading
    /// with no per-size tuning. The sheet's digits are drawn smaller than its caps (81px vs 104px), so
    /// the two are normalised against different nominals at build time - see ProjectSetup.
    ///
    /// The sheet is UPPERCASE ONLY and has no punctuation, so <see cref="Lookup"/> uppercases its
    /// input and anything with no glyph advances as a blank. Strings that need a character this font
    /// does not have must be reworded, not rendered - see SceneSetup/ShellUI for the two that were.
    /// </summary>
    [CreateAssetMenu(menuName = "BookBound/Sprite Font", fileName = "BookBoundFont")]
    public class SpriteFont : ScriptableObject
    {
        [Serializable]
        public struct Glyph
        {
            public char character;
            public Sprite sprite;

            [Tooltip("Glyph width as a multiple of cap height.")]
            public float widthRatio;

            [Tooltip("Glyph height as a multiple of cap height. Over 1 for descenders (J, Q).")]
            public float heightRatio;
        }

        [SerializeField] private Glyph[] glyphs = Array.Empty<Glyph>();

        [Tooltip("Gap between glyphs, as a multiple of cap height.")]
        [SerializeField] private float tracking = 0.05f;

        [Tooltip("Width of a space, as a multiple of cap height.")]
        [SerializeField] private float spaceWidth = 0.28f;

        [Tooltip("Baseline-to-baseline distance, as a multiple of cap height.")]
        [SerializeField] private float lineHeight = 1.4f;

        public float Tracking => tracking;
        public float SpaceWidth => spaceWidth;
        public float LineHeight => lineHeight;

        private Dictionary<char, Glyph> _map;

        /// <summary>True and fills <paramref name="glyph"/> if this character has art. Uppercases
        /// first, so callers do not have to.</summary>
        public bool Lookup(char c, out Glyph glyph)
        {
            if (_map == null)
            {
                _map = new Dictionary<char, Glyph>(glyphs.Length);
                foreach (Glyph g in glyphs)
                    if (g.sprite != null) _map[g.character] = g;
            }

            return _map.TryGetValue(char.ToUpperInvariant(c), out glyph);
        }

        /// <summary>Width of one line at the given cap height. Pure - no GameObjects, so scene-build
        /// code can size a plate around a label before the label exists.</summary>
        public float MeasureWidth(string line, float capHeight)
        {
            if (string.IsNullOrEmpty(line)) return 0f;

            float width = 0f;
            foreach (char c in line)
                width += Lookup(c, out Glyph g) ? g.widthRatio * capHeight : spaceWidth * capHeight;
            return width + (line.Length - 1) * tracking * capHeight;
        }

        /// <summary>Ink size of a whole (possibly multi-line) string - the height measures glyph ink,
        /// not line boxes, matching how SpriteText aligns.</summary>
        public Vector2 Measure(string text, float capHeight)
        {
            if (string.IsNullOrEmpty(text)) return Vector2.zero;

            string[] lines = text.Split('\n');
            float widest = 0f;
            foreach (string line in lines) widest = Mathf.Max(widest, MeasureWidth(line, capHeight));
            return new Vector2(widest, (lines.Length - 1) * lineHeight * capHeight + capHeight);
        }

        /// <summary>Largest cap height at which <paramref name="text"/> still fits <paramref name="maxWidth"/>,
        /// never above <paramref name="preferred"/>. Lets long labels shrink to fit their plate instead
        /// of overflowing it or every caller hard-coding a size per string length.</summary>
        public float FitCapHeight(string text, float maxWidth, float preferred)
        {
            float width = Measure(text, preferred).x;
            return width <= maxWidth || width <= 0f ? preferred : preferred * maxWidth / width;
        }

#if UNITY_EDITOR
        public void SetGlyphs(Glyph[] value)
        {
            glyphs = value;
            _map = null;
        }
#endif
    }
}
