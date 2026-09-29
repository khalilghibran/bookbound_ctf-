using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BookBound.EditorTools
{
    /// <summary>
    /// All book/UI art is hand-painted pixel art. Unity's default import (Bilinear filtering +
    /// Compressed) blurs and introduces compression artifacts on sharp pixel edges - most visibly on
    /// the baked-in spine numbers. Force Point filtering and Uncompressed (RGBA32) on every sprite
    /// sheet actually used by the game.
    /// </summary>
    public static class FixSpriteImportQuality
    {
        private static readonly HashSet<string> BilinearUiSheets = new(SliceHudOverlaySprites.Sheets);

        [MenuItem("BookBound/Setup/-2. Fix Sprite Import Quality (Point/Uncompressed)")]
        public static void Fix()
        {
            var paths = new List<string>();

            foreach (var view in new[] { "front-view", "side-view", "sleeping-view" })
            foreach (var genre in new[] { "fantasy", "science", "history", "biography" })
                paths.Add($"Assets/Art/{view}/{genre}_{(view == "front-view" ? "front" : view == "side-view" ? "side" : "sleeping")}_spritesheet.png");

            // Everything under Assets/UI EXCEPT the sheets SliceHudOverlaySprites owns. Those are the
            // same painterly AI art as the title screen (soft gradients, glow) and it sets them to
            // Bilinear on purpose - without this exclusion, RebuildAndBuild (which runs this pass and
            // then never re-runs the slicer) silently flipped the score plaque, clock, pause icon,
            // Genre Guide bars and controls icons back to Point on every build.
            paths.AddRange(AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/UI" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !BilinearUiSheets.Contains(p)));

            // Chunky pixel-art heart - wants Point like the book art, not the Bilinear default it was
            // imported with. PausedInterface-Background.png is deliberately absent: it's painterly.
            paths.Add("Assets/Art/Hearts_transparent.png");
            // Icon-BookBound*.png deliberately absent: SetPlayerIcon owns the square copy's import
            // settings (readable + mipmapped, which this pass would strip).
            paths.Add("Assets/Art/bookcase_5row.png");
            paths.Add("Assets/Art/ChatGPT_Image_Aug_2__2026__11_42_42_AM-removebg-preview.png");
            paths.Add("Assets/Art/ChatGPT_Image_Aug_2__2026__12_15_06_PM-removebg-preview.png");
            paths.Add("Assets/ChatGPT Image Aug 2, 2026, 12_00_45 PM.png");
            // StartScreen-Background.png and bookbound-spritesheet-purple-hover-transparent.png are
            // deliberately NOT here - they're painterly AI art (soft gradients/glow), not hard-edged
            // pixel art, so they want Bilinear filtering instead of the Point filter this pass forces.
            // See SliceStartScreenSprites, which owns their import settings.

            int fixedCount = 0;
            foreach (var path in paths.Distinct())
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool changed = false;
                if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
                if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
                if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
                // The genre spritesheets are 5120px wide natively; the default 2048 cap silently
                // downscaled them by more than half on import - real detail loss (illegible baked-in
                // numbers once magnified) that no filter/compression setting can undo. 8192 covers
                // every texture in this project with room to spare.
                if (importer.maxTextureSize < 8192) { importer.maxTextureSize = 8192; changed = true; }

                // The default max size above does NOT cover per-platform overrides - this project has
                // an explicit "Standalone" override separately pinned to 2048 (silently overriding the
                // default for exactly the Editor/Standalone context these are tested in), plus WebGL.
                // Disable the override so both fall back to the (now 8192) default.
                foreach (var platform in new[] { "Standalone", "WebGL", "iPhone", "Android" })
                {
                    var settings = importer.GetPlatformTextureSettings(platform);
                    if (settings.overridden)
                    {
                        settings.overridden = false;
                        importer.SetPlatformTextureSettings(settings);
                        changed = true;
                    }
                }

                if (changed)
                {
                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                    fixedCount++;
                }
            }

            Debug.Log($"[FixSpriteImportQuality] Reimported {fixedCount} texture(s) with Point filtering + Uncompressed.");
        }
    }
}
