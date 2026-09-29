using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BookBound
{
    /// <summary>
    /// Renders a string as hand-laid glyph sprites from <see cref="SpriteFont"/> instead of a font
    /// asset - the same approach <see cref="DigitStrip"/> takes for the score and timer, generalised
    /// to letters and to strings whose length is not known at build time.
    ///
    /// Glyph Images are pooled: <see cref="SetText"/> re-points and repositions existing children and
    /// only ever grows the pool, so a label that changes every frame (the bookcase counter) allocates
    /// nothing after its first call. Layout is single-pass - measure each line, then place - and
    /// supports explicit newlines but NOT automatic word wrap: every string in this game is short and
    /// its line breaks are authored, so wrapping would be machinery with no caller.
    /// </summary>
    public class SpriteText : MonoBehaviour
    {
        [SerializeField] private SpriteFont font;

        [Tooltip("Cap height in reference pixels. Every glyph scales off this.")]
        [SerializeField] private float capHeight = 32f;

        [SerializeField] private TextAnchor alignment = TextAnchor.MiddleCenter;
        [SerializeField] private Color color = Color.white;

        // MUST be serialized. Labels authored once at scene-build time (SCORE, GENRE GUIDE, the
        // controls hints) have no runtime SetText caller, so a plain private field came back empty
        // in the player while runtime-driven labels looked fine - a bug that only showed in a build.
        [SerializeField] [TextArea] private string text = string.Empty;

        private readonly List<Image> _pool = new();
        private RectTransform _rt;

        public string Text => text;

        public float CapHeight
        {
            get => capHeight;
            set { capHeight = value; Rebuild(); }
        }

        private RectTransform Rt => _rt != null ? _rt : _rt = (RectTransform)transform;

        public void SetText(string value)
        {
            value ??= string.Empty;
            if (value == text) return;
            text = value;
            Rebuild();
        }

        public void SetColor(Color value)
        {
            color = value;
            foreach (Image image in _pool) image.color = value;
        }

        /// <summary>Laid-out ink size of the current text, for callers that size a plate around a label.</summary>
        public Vector2 PreferredSize() =>
            font == null ? Vector2.zero : font.Measure(text, capHeight);

        private void OnEnable() => Rebuild();

        /// <summary>A stretched rect has no usable size until the parent resolves, and left/right
        /// alignment needs one - re-lay out when it changes rather than reading a stale zero.</summary>
        protected void OnRectTransformDimensionsChange() => Rebuild();

        private void Rebuild()
        {
            if (font == null) return;

            string[] lines = text.Split('\n');
            float lineStep = font.LineHeight * capHeight;
            Rect rect = Rt.rect;

            // Vertical alignment measures the INK, not the line boxes. A line box is lineHeight (1.4)
            // tall, but glyphs only fill the cap height at the TOP of it - so centring line boxes left
            // every label sitting ~0.2 cap heights high inside its plate. Visible on every button.
            float inkHeight = (lines.Length - 1) * lineStep + capHeight;

            // Everything below is measured from the rect's top-left corner, and glyphs anchor there
            // too (see Take), so this is independent of whatever pivot the rect happens to carry.
            float blockTop = alignment switch
            {
                TextAnchor.UpperLeft or TextAnchor.UpperCenter or TextAnchor.UpperRight
                    => 0f,
                TextAnchor.LowerLeft or TextAnchor.LowerCenter or TextAnchor.LowerRight
                    => -(rect.height - inkHeight),
                _ => -(rect.height - inkHeight) * 0.5f,
            };

            int used = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                float lineWidth = font.MeasureWidth(line, capHeight);
                float x = alignment switch
                {
                    TextAnchor.UpperLeft or TextAnchor.MiddleLeft or TextAnchor.LowerLeft
                        => 0f,
                    TextAnchor.UpperRight or TextAnchor.MiddleRight or TextAnchor.LowerRight
                        => rect.width - lineWidth,
                    _ => (rect.width - lineWidth) * 0.5f,
                };

                // Glyph tops align with the line top; descenders (J, Q) are taller and hang below,
                // which is exactly how they sit on the source sheet.
                float top = blockTop - i * lineStep;

                foreach (char c in line)
                {
                    if (!font.Lookup(c, out SpriteFont.Glyph g))
                    {
                        x += (font.SpaceWidth + font.Tracking) * capHeight;
                        continue;
                    }

                    Image image = Take(used++);
                    image.sprite = g.sprite;
                    image.color = color;

                    var glyphRt = (RectTransform)image.transform;
                    float w = g.widthRatio * capHeight;
                    float h = g.heightRatio * capHeight;
                    glyphRt.sizeDelta = new Vector2(w, h);
                    glyphRt.anchoredPosition = new Vector2(x, top);

                    x += (g.widthRatio + font.Tracking) * capHeight;
                }
            }

            for (int i = used; i < _pool.Count; i++) _pool[i].enabled = false;
        }

        private Image Take(int index)
        {
            while (_pool.Count <= index)
            {
                var go = new GameObject("Glyph", typeof(RectTransform));
                go.transform.SetParent(Rt, false);
                var glyphRt = (RectTransform)go.transform;
                // Anchored AND pivoted to the parent's top-left, so a glyph's anchoredPosition is
                // literally its pen position - no centre offsets, no pivot arithmetic.
                glyphRt.anchorMin = glyphRt.anchorMax = new Vector2(0f, 1f);
                glyphRt.pivot = new Vector2(0f, 1f);
                var image = go.AddComponent<Image>();
                image.raycastTarget = false;
                _pool.Add(image);
            }

            Image taken = _pool[index];
            taken.enabled = true;
            return taken;
        }

#if UNITY_EDITOR
        /// <summary>Editor-time wiring for SceneSetup, which builds the whole scene from script.</summary>
        public void Configure(SpriteFont spriteFont, float height, TextAnchor anchor, Color tint, string content)
        {
            font = spriteFont;
            capHeight = height;
            alignment = anchor;
            color = tint;
            text = content ?? string.Empty;
        }
#endif
    }
}
