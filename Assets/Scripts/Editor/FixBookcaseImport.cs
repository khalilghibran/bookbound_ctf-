using UnityEditor;
using UnityEngine;

namespace BookBound.EditorTools
{
    /// <summary>
    /// The bookcase source PNG imported with Sprite Mode = Multiple and a single auto-detected slice
    /// that's tighter than the full artwork (it drops the crown molding and base trim, leaving just
    /// the repeating shelf-board pattern). We need the complete bookcase - switch it to Single/full-rect.
    /// </summary>
    public static class FixBookcaseImport
    {
        private const string BookcasePath = "Assets/Art/ChatGPT_Image_Aug_2__2026__11_42_42_AM-removebg-preview.png";

        [MenuItem("BookBound/Setup/0. Fix Bookcase Sprite Import (full rect)")]
        public static void Fix()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(BookcasePath);
            if (importer == null)
            {
                Debug.LogError($"[FixBookcaseImport] No importer found at {BookcasePath}");
                return;
            }

            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            importer.SetTextureSettings(settings);

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            Debug.Log("[FixBookcaseImport] Reimported as Single/full-rect.");
        }
    }
}
