using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace BookBound.EditorTools
{
    /// <summary>
    /// Slices the specific pieces of the HUD/Panels/Decorations/Modals/Controls/Fonts spritesheets that
    /// the in-game HUD overlay (score plaque, timer, pause button, genre guide, controls hint) actually
    /// uses. Like the title-screen art, these are painterly icon sheets with soft gradients/rounded
    /// edges, not flat pixel-block art, so they get Bilinear filtering.
    ///
    /// The Genre Guide used to use this sheet's genre_bar_* nameplates, one per row - dropped because
    /// stretching a ~289px-wide nameplate up to a full row's width looked bad. It's plain parchment
    /// (modal_parchment) and sprite-font text now.
    ///
    /// Rects are hand-measured off each sheet's alpha channel (per-row/per-column, since items on a
    /// given row aren't evenly gridded against the others) - there is no slice metadata in the source
    /// files. Re-run after any of these sheets are replaced.
    /// </summary>
    public static class SliceHudOverlaySprites
    {
        private const string HudIcons = "Assets/UI/HUD/bookbound_ui_hud_icons_spritesheet.png";
        private const string Panels = "Assets/UI/Panels/bookbound_ui_panels_spritesheet.png";
        private const string Decorations = "Assets/UI/Decorations/bookbound_ui_decorations_spritesheet.png";
        private const string Modals = "Assets/UI/Modals/bookbound_ui_modals_results_spritesheet.png";
        private const string Controls = "Assets/UI/Controls/bookbound_ui_controls_spritesheet.png";
        private const string Fonts = "Assets/UI/Fonts/bookbound_ui_counter_glyphs_spritesheet.png";
        private const string Letters = "Assets/UI/Fonts/BookBound-Fonts.png";
        private const string Slider = "Assets/UI/Controls/Slider2-BookBound.png";

        /// <summary>9-slice borders (left, bottom, right, top) for the sprites that get stretched to
        /// arbitrary sizes. Measured by scanning in from each edge to where the flat interior fill
        /// starts, then padded so the corner ornaments sit wholly inside the corner. Without these,
        /// Image.Type.Sliced degenerates to a plain stretch and the gold corners smear.</summary>
        private static readonly Dictionary<string, Vector4> Borders = new()
        {
            ["modal_frame"] = new Vector4(30, 33, 34, 14),
            ["panel_purple_bar"] = new Vector4(13, 11, 11, 12),
        };

        private static readonly (string sheet, string name, int x, int y, int w, int h)[] Sprites =
        {
            (HudIcons, "hud_clock", 88, 995, 182, 183),
            (HudIcons, "hud_pause", 97, 417, 156, 156),

            (Panels, "panel_purple_bar", 860, 582, 349, 88),

            (Decorations, "deco_star", 762, 736, 89, 93),

            (Modals, "modal_banner", 691, 127, 508, 171),
            (Modals, "modal_parchment", 856, 773, 348, 411),

            (Controls, "mouse_left_click", 114, 963, 113, 183),
            (Controls, "mouse_release", 694, 963, 113, 183),

            (Fonts, "font_0", 133, 978, 104, 167),
            (Fonts, "font_1", 440, 978, 85, 167),
            (Fonts, "font_2", 716, 978, 102, 167),
            (Fonts, "font_3", 1006, 978, 104, 167),
            (Fonts, "font_4", 133, 684, 110, 167),
            (Fonts, "font_5", 428, 684, 103, 167),
            (Fonts, "font_6", 715, 684, 103, 167),
            (Fonts, "font_7", 1006, 684, 104, 167),
            (Fonts, "font_8", 133, 392, 104, 169),
            (Fonts, "font_9", 424, 392, 104, 169),
            (Fonts, "font_colon", 744, 406, 43, 132),

            // Shell/menu art: ornate window frame, close button, volume + main-menu icons, dividers.
            (Modals, "modal_frame", 33, 785, 384, 415),
            (HudIcons, "hud_close", 989, 1010, 155, 156),
            (HudIcons, "hud_speaker", 383, 145, 173, 150),
            (HudIcons, "hud_library", 981, 407, 169, 187),
            (Decorations, "deco_divider", 85, 755, 185, 50),
            (Decorations, "deco_divider_long", 357, 759, 311, 43),

            // Volume slider. The sheet has one gold rail and one gem handle; the rail doubles as both
            // the track (dimmed) and the filled portion (bright, horizontally clipped) - there is no
            // separate empty-track art. Rects measured off the alpha at threshold 128, since the
            // source has 1px anti-aliasing specks that a naive bounding box picks up as extra sprites.
            (Slider, "slider_rail", 113, 434, 1309, 124),
            (Slider, "slider_handle", 1208, 172, 215, 184),

            // BookBound letterforms. Every rect below is the glyph's own alpha bounding box, measured
            // programmatically - the sheet's own auto-slice merged neighbours and split rows, and it
            // repeats I, J, R and 6, so the duplicates are deliberately not listed. Glyph tops align
            // within a row, so SpriteFont lays text out by top edge and lets J and Q hang naturally.
            (Letters, "letter_A", 281, 816, 85, 106),
            (Letters, "letter_B", 378, 817, 78, 104),
            (Letters, "letter_C", 470, 817, 74, 105),
            (Letters, "letter_D", 565, 817, 77, 104),
            (Letters, "letter_E", 661, 817, 70, 104),
            (Letters, "letter_F", 744, 817, 73, 104),
            (Letters, "letter_G", 832, 816, 86, 106),
            (Letters, "letter_H", 930, 817, 86, 104),
            (Letters, "letter_I", 1035, 817, 44, 105),
            (Letters, "letter_J", 1106, 804, 51, 117),
            (Letters, "letter_K", 381, 671, 85, 102),
            (Letters, "letter_L", 479, 671, 73, 102),
            (Letters, "letter_M", 565, 670, 104, 103),
            (Letters, "letter_N", 680, 671, 88, 102),
            (Letters, "letter_O", 774, 670, 86, 104),
            (Letters, "letter_P", 872, 671, 77, 102),
            (Letters, "letter_Q", 958, 670, 90, 104),
            (Letters, "letter_R", 1061, 671, 83, 102),
            (Letters, "letter_S", 339, 519, 69, 102),
            (Letters, "letter_T", 418, 520, 82, 101),
            (Letters, "letter_U", 512, 519, 89, 102),
            (Letters, "letter_V", 613, 520, 101, 101),
            (Letters, "letter_W", 725, 518, 132, 104),
            (Letters, "letter_X", 863, 519, 100, 102),
            (Letters, "letter_Y", 974, 519, 92, 102),
            (Letters, "letter_Z", 1072, 519, 93, 102),

            // Same sheet's digits. Smaller than its caps (81px vs 104px), so SpriteFont normalises
            // them separately - see NominalDigitHeight. The chunkier counter glyphs above stay in use
            // for the score plaque and timer readout; these are for digits inline in a sentence.
            (Letters, "digit_0", 410, 295, 58, 80),
            (Letters, "digit_1", 479, 294, 47, 81),
            (Letters, "digit_2", 534, 294, 54, 81),
            (Letters, "digit_3", 597, 294, 53, 80),
            (Letters, "digit_4", 653, 294, 60, 81),
            (Letters, "digit_5", 725, 294, 56, 80),
            (Letters, "digit_6", 795, 294, 59, 81),
            (Letters, "digit_7", 933, 294, 61, 80),
            (Letters, "digit_8", 1001, 293, 61, 82),
            (Letters, "digit_9", 1072, 294, 60, 81),
        };

        /// <summary>The sheets this script owns the import settings for - it sets them to Bilinear
        /// (see <see cref="SliceSheet"/>). Derived from the table above so the two can't drift apart.
        /// FixSpriteImportQuality reads this to skip them in its Point-filter sweep of Assets/UI.</summary>
        public static readonly string[] Sheets = Sprites.Select(s => s.sheet).Distinct().ToArray();

        [MenuItem("BookBound/Setup/8. Slice HUD Overlay Sprites")]
        public static void Slice()
        {
            int done = 0;
            foreach (var group in Sprites.GroupBy(s => s.sheet))
            {
                SliceSheet(group.Key, group.ToArray());
                done += group.Count();
            }
            Debug.Log($"[SliceHudOverlaySprites] Sliced {done} sprite(s) across {Sprites.Select(s => s.sheet).Distinct().Count()} sheet(s).");
        }

        private static void SliceSheet(string path, (string sheet, string name, int x, int y, int w, int h)[] sprites)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
                throw new System.InvalidOperationException($"{path} not found.");

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Bilinear; // painterly icon art, not flat pixel blocks
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            if (importer.maxTextureSize < 4096) importer.maxTextureSize = 4096;

            TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = false;
            importer.SetPlatformTextureSettings(standalone);

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();

            SpriteRect[] rects = sprites.Select(s => new SpriteRect
            {
                name = s.name,
                rect = new Rect(s.x, s.y, s.w, s.h),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                border = Borders.GetValueOrDefault(s.name, Vector4.zero),
                spriteID = GUID.Generate(),
            }).ToArray();

            dataProvider.SetSpriteRects(rects);

            var nameFileIdProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            var pairs = rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToArray();
            nameFileIdProvider.SetNameFileIdPairs(pairs);

            dataProvider.Apply();
            importer.SaveAndReimport();
        }
    }
}
