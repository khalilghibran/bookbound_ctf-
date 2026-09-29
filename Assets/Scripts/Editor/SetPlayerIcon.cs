using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace BookBound.EditorTools
{
    /// <summary>
    /// Points the standalone player's icon at the BookBound crest.
    ///
    /// Uses the pre-squared copy, not the source PNG: Unity scales whatever it's given into square
    /// icon slots, so a 1536x1024 source with the art floating in it would come out both squashed and
    /// surrounded by dead space. `Icon-BookBound-Square.png` is the source trimmed to its alpha
    /// bounding box and padded to a square - same kind of one-off asset prep as tools/prep_spines.py.
    /// Keep the original around; it's the input if the crop ever needs redoing.
    /// </summary>
    public static class SetPlayerIcon
    {
        private const string IconPath = "Assets/Art/Icon-BookBound-Square.png";

        [MenuItem("BookBound/Setup/10. Set Player Icon")]
        public static void Apply()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            if (importer == null)
                throw new System.InvalidOperationException($"{IconPath} not found.");

            // An icon source has to be readable and uncompressed for Unity to resample it cleanly.
            importer.textureType = TextureImporterType.Default;
            importer.spriteImportMode = SpriteImportMode.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = true; // icons get downscaled hard; mips avoid aliasing
            importer.isReadable = true;
            importer.SaveAndReimport();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (texture == null)
                throw new System.InvalidOperationException($"Could not load a Texture2D at {IconPath}.");

            var target = NamedBuildTarget.Standalone;
            int[] sizes = PlayerSettings.GetIconSizes(target, IconKind.Any);
            var icons = new Texture2D[sizes.Length];
            for (int i = 0; i < icons.Length; i++) icons[i] = texture;

            PlayerSettings.SetIcons(target, icons, IconKind.Any);
            AssetDatabase.SaveAssets();

            Debug.Log($"[SetPlayerIcon] Set {icons.Length} standalone icon slot(s) from {IconPath}.");
        }
    }
}
