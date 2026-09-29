using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace BookBound.Tests
{
    /// <summary>
    /// Guards the hand-laid glyph layout - the part of the sprite-font path with real arithmetic in
    /// it. A font asset is built in-memory from stub sprites with known metrics, so these assert
    /// layout, not art.
    /// </summary>
    public class SpriteTextTests
    {
        private const float Cap = 100f;

        // Deliberately uneven: a 0.5-wide A and a 1.0-wide B make a wrong advance obvious.
        private static SpriteFont MakeFont()
        {
            var font = ScriptableObject.CreateInstance<SpriteFont>();
            font.SetGlyphs(new[]
            {
                Glyph('A', 0.5f, 1f),
                Glyph('B', 1f, 1f),
                Glyph('J', 0.5f, 1.2f), // descender: taller than cap height
            });
            return font;
        }

        private static SpriteFont.Glyph Glyph(char c, float w, float h)
        {
            var texture = new Texture2D(4, 4);
            return new SpriteFont.Glyph
            {
                character = c,
                sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f)),
                widthRatio = w,
                heightRatio = h,
            };
        }

        private static SpriteText MakeText(TextAnchor anchor, Vector2 size)
        {
            var go = new GameObject("SpriteText", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            var text = go.AddComponent<SpriteText>();
            text.Configure(MakeFont(), Cap, anchor, Color.white, string.Empty);
            return text;
        }

        private static Image[] Glyphs(SpriteText text) =>
            text.GetComponentsInChildren<Image>(includeInactive: true).Where(i => i.enabled).ToArray();

        [Test]
        public void RendersOneImagePerKnownCharacter()
        {
            SpriteText text = MakeText(TextAnchor.MiddleCenter, new Vector2(1000f, 200f));
            text.SetText("AB");

            Assert.AreEqual(2, Glyphs(text).Length);
        }

        [Test]
        public void UppercasesInput()
        {
            SpriteText text = MakeText(TextAnchor.MiddleCenter, new Vector2(1000f, 200f));
            text.SetText("ab");

            Assert.AreEqual(2, Glyphs(text).Length, "lowercase must map onto the uppercase-only sheet");
        }

        [Test]
        public void UnknownCharacterAdvancesButDrawsNothing()
        {
            SpriteText text = MakeText(TextAnchor.MiddleCenter, new Vector2(1000f, 200f));
            text.SetText("A?B");

            Assert.AreEqual(2, Glyphs(text).Length, "'?' has no glyph, so only A and B draw");
        }

        [Test]
        public void GlyphAdvanceUsesPerGlyphWidth()
        {
            SpriteText text = MakeText(TextAnchor.UpperLeft, new Vector2(1000f, 200f));
            text.SetText("AB");

            Image[] glyphs = Glyphs(text);
            float aX = ((RectTransform)glyphs[0].transform).anchoredPosition.x;
            float bX = ((RectTransform)glyphs[1].transform).anchoredPosition.x;

            // A is 0.5 cap wide, plus one tracking step.
            Assert.AreEqual(0f, aX, 0.01f);
            Assert.AreEqual((0.5f + 0.05f) * Cap, bX - aX, 0.01f);
        }

        [Test]
        public void DescenderIsTallerButSharesTheLineTop()
        {
            SpriteText text = MakeText(TextAnchor.UpperLeft, new Vector2(1000f, 200f));
            text.SetText("AJ");

            Image[] glyphs = Glyphs(text);
            var a = (RectTransform)glyphs[0].transform;
            var j = (RectTransform)glyphs[1].transform;

            Assert.AreEqual(a.anchoredPosition.y, j.anchoredPosition.y, 0.01f, "tops must align");
            Assert.Greater(j.sizeDelta.y, a.sizeDelta.y, "J hangs below the baseline");
        }

        [Test]
        public void SecondLineSitsBelowTheFirst()
        {
            SpriteText text = MakeText(TextAnchor.UpperLeft, new Vector2(1000f, 400f));
            text.SetText("A\nB");

            Image[] glyphs = Glyphs(text);
            float firstY = ((RectTransform)glyphs[0].transform).anchoredPosition.y;
            float secondY = ((RectTransform)glyphs[1].transform).anchoredPosition.y;

            Assert.AreEqual(-1.4f * Cap, secondY - firstY, 0.01f);
        }

        [Test]
        public void CentredTextIsCentredOnTheRect()
        {
            const float width = 1000f;
            SpriteText text = MakeText(TextAnchor.MiddleCenter, new Vector2(width, 200f));
            text.SetText("A");

            float x = ((RectTransform)Glyphs(text)[0].transform).anchoredPosition.x;
            Assert.AreEqual((width - 0.5f * Cap) * 0.5f, x, 0.01f);
        }

        /// <summary>Regression: the authored text used to live in a plain private field, so labels
        /// written once at scene-build time (SCORE, GENRE GUIDE, the controls hints) came back empty
        /// in a player while runtime-driven labels looked fine. It has to survive serialization.</summary>
        [Test]
        public void ConfiguredTextSurvivesSerialization()
        {
            SpriteText text = MakeText(TextAnchor.MiddleCenter, new Vector2(1000f, 200f));
            text.Configure(MakeFont(), Cap, TextAnchor.MiddleCenter, Color.white, "AB");

            FieldInfo field = typeof(SpriteText)
                .GetField("text", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(field, "the text field must exist");
            Assert.IsNotNull(field.GetCustomAttribute<SerializeField>(),
                "text must be [SerializeField] or build-time labels render blank in a player");
            Assert.AreEqual("AB", field.GetValue(text));
        }

        /// <summary>Regression: vertical alignment used to centre the LINE BOXES (lineHeight = 1.4 cap
        /// heights) rather than the ink, so every label sat ~0.2 cap heights high inside its plate.
        /// Most visible on the pause menu's buttons and the PAUSED ribbon.</summary>
        [Test]
        public void SingleLineIsVerticallyCentredOnItsInk()
        {
            const float height = 300f;
            SpriteText text = MakeText(TextAnchor.MiddleCenter, new Vector2(1000f, height));
            text.SetText("A");

            var rt = (RectTransform)Glyphs(text)[0].transform;
            float gapAbove = -rt.anchoredPosition.y;               // glyph top is below the rect top
            float gapBelow = height - (gapAbove + rt.sizeDelta.y); // ink bottom to rect bottom

            Assert.AreEqual(gapAbove, gapBelow, 0.01f, "ink must sit equally between top and bottom");
        }

        [Test]
        public void MultiLineIsVerticallyCentredOnItsInk()
        {
            const float height = 400f;
            SpriteText text = MakeText(TextAnchor.MiddleCenter, new Vector2(1000f, height));
            text.SetText("A\nB");

            Image[] glyphs = Glyphs(text);
            var first = (RectTransform)glyphs[0].transform;
            var last = (RectTransform)glyphs[1].transform;

            float gapAbove = -first.anchoredPosition.y;
            float inkBottom = -last.anchoredPosition.y + last.sizeDelta.y;

            Assert.AreEqual(gapAbove, height - inkBottom, 0.01f);
        }

        [Test]
        public void FitCapHeightShrinksOnlyWhenTooWide()
        {
            SpriteFont font = MakeFont();

            // "A" is 0.5 cap wide, so at cap 100 it is 50 wide - well inside 500.
            Assert.AreEqual(100f, font.FitCapHeight("A", 500f, 100f), 0.01f, "must not upscale to fill");

            // "BB" is 1+1 wide plus one tracking step = 2.05 cap. At cap 100 that's 205, over 100.
            float fitted = font.FitCapHeight("BB", 100f, 100f);
            Assert.Less(fitted, 100f);
            Assert.AreEqual(100f, font.Measure("BB", fitted).x, 0.01f, "shrunk text must exactly fit");
        }

        [Test]
        public void PoolIsReusedNotRegrown()
        {
            SpriteText text = MakeText(TextAnchor.MiddleCenter, new Vector2(1000f, 200f));
            text.SetText("ABAB");
            int afterLong = text.GetComponentsInChildren<Image>(includeInactive: true).Length;

            text.SetText("A");
            int afterShort = text.GetComponentsInChildren<Image>(includeInactive: true).Length;

            Assert.AreEqual(afterLong, afterShort, "shrinking text must not destroy pooled glyphs");
            Assert.AreEqual(1, Glyphs(text).Length, "but only one may still be drawing");
        }
    }
}
