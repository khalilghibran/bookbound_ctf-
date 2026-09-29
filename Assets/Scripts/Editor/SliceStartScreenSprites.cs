using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace BookBound.EditorTools
{
    /// <summary>
    /// Import fix + slice for the title-screen art: Assets/Art/StartScreen-Background.png and the
    /// logo/button spritesheet. Both are painterly AI-rendered art (soft gradients, glow, anti-aliased
    /// edges) rather than the project's hard-edged pixel art, so unlike FixSpriteImportQuality they get
    /// Bilinear filtering, not Point - Point filtering on art like this looks chunky/low-quality instead
    /// of crisp, since there are no flat pixel blocks to preserve.
    ///
    /// The button/logo sheet actually contains 3 near-duplicate AI-generated columns of the same
    /// content; rects below all come from the rightmost column, which measured slightly higher-res than
    /// the other two (e.g. the logo is 406px wide here vs 358-384px in the other columns). Hand-measured
    /// off the source PNG's alpha channel - there is no slice metadata baked into the source file.
    /// </summary>
    public static class SliceStartScreenSprites
    {
        private const string ButtonSheetPath = "Assets/Art/bookbound-spritesheet-purple-hover-transparent.png";
        private const string BackgroundPath = "Assets/Art/StartScreen-Background.png";

        // Widened a few px on every edge from the first pass: the tight alpha-bbox measurement clipped
        // the small pink corner-gem accents on every plaque (most visible on options_hover/quit_hover's
        // bright sparkle bursts, which is what actually prompted re-measuring these).
        private static readonly (string name, int x, int y, int w, int h)[] Sprites =
        {
            ("logo_normal", 1011, 853, 394, 162),
            ("logo_hover", 1011, 687, 394, 161),
            ("logo_selected", 1005, 517, 406, 162),
            ("start_normal", 1028, 455, 364, 53),
            ("start_hover", 1028, 397, 364, 52),
            ("options_hover", 1018, 340, 387, 51),
            ("options_selected", 1028, 284, 364, 50),
            ("collection_normal", 1028, 226, 364, 52),
            ("collection_hover", 1028, 170, 364, 50),
            ("quit_hover", 1016, 113, 389, 50),
            ("quit_selected", 1028, 55, 364, 51),
        };

        [MenuItem("BookBound/Setup/6. Slice Start Screen Button Sprites")]
        public static void Slice()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(ButtonSheetPath);
            if (importer == null)
                throw new System.InvalidOperationException($"{ButtonSheetPath} not found.");

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 4096;

            TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = false;
            importer.SetPlatformTextureSettings(standalone);

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();

            SpriteRect[] rects = Sprites.Select(s => new SpriteRect
            {
                name = s.name,
                rect = new Rect(s.x, s.y, s.w, s.h),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                border = Vector4.zero,
                spriteID = GUID.Generate(),
            }).ToArray();

            dataProvider.SetSpriteRects(rects);

            var nameFileIdProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            var pairs = rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToArray();
            nameFileIdProvider.SetNameFileIdPairs(pairs);

            dataProvider.Apply();
            importer.SaveAndReimport();

            FixBackgroundImport();

            Debug.Log($"[SliceStartScreenSprites] Sliced {rects.Length} sprites from {ButtonSheetPath} and fixed {BackgroundPath}.");
        }

        private static void FixBackgroundImport()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(BackgroundPath);
            if (importer == null)
                throw new System.InvalidOperationException($"{BackgroundPath} not found.");

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 4096;

            TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = false;
            importer.SetPlatformTextureSettings(standalone);

            importer.SaveAndReimport();
        }
    }
}
