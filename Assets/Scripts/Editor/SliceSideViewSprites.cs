using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace BookBound.EditorTools
{
    /// <summary>
    /// Slices a genre's *_side_spritesheet.png into its 10 book cells. tools/prep_spines.py builds
    /// these as an exact uniform 10x1 grid (ten BOOK_W x BOOK_H cells, no padding, "ready for a 10x1
    /// grid slice in Unity" per its own docstring) - so unlike the title-screen art, this is pure
    /// arithmetic, not alpha-bbox measurement. Sub-sprites are named "{genre}_side_spritesheet_0..9"
    /// to match ProjectSetup.LoadOrderedSprites' expected _&lt;index&gt; suffix.
    ///
    /// Re-run this after any genre's side-view art is replaced - swapping in new bytes at the same
    /// path deletes the old .meta's slice data (and its GUIDs), so the file needs reslicing before
    /// ProjectSetup.CreateGenreDefinitions can pick the new sprites back up.
    /// </summary>
    public static class SliceSideViewSprites
    {
        private static readonly string[] Genres = { "fantasy", "science", "history", "biography" };

        [MenuItem("BookBound/Setup/7. Slice Side-View (Spine) Sprites")]
        public static void SliceAll()
        {
            int done = 0;
            foreach (string genre in Genres)
            {
                string path = $"Assets/Art/side-view/{genre}_side_spritesheet.png";
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                {
                    Debug.LogWarning($"[SliceSideViewSprites] Skipping {genre} - no file at {path}.");
                    continue;
                }

                // Already sliced into exactly 10 named cells (e.g. restored from git history with its
                // original slice metadata intact) - nothing to do.
                if (importer.spriteImportMode == SpriteImportMode.Multiple &&
                    AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count() == GenreDefinition.BooksPerGenre)
                {
                    continue;
                }

                Slice(importer, path, genre);
                done++;
            }

            Debug.Log($"[SliceSideViewSprites] (Re)sliced {done} side-view sheet(s); {Genres.Length - done} already had 10 cells.");
        }

        private static void Slice(TextureImporter importer, string path, string genre)
        {
            var size = GetSourceSize(path);
            int cellWidth = size.x / GenreDefinition.BooksPerGenre;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point; // this IS the project's pixel art, unlike the title screen
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            if (importer.maxTextureSize < 8192) importer.maxTextureSize = 8192;

            TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = false;
            importer.SetPlatformTextureSettings(standalone);

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();

            var rects = new SpriteRect[GenreDefinition.BooksPerGenre];
            for (int i = 0; i < rects.Length; i++)
            {
                rects[i] = new SpriteRect
                {
                    name = $"{genre}_side_spritesheet_{i}",
                    rect = new Rect(i * cellWidth, 0, cellWidth, size.y),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    border = Vector4.zero,
                    spriteID = GUID.Generate(),
                };
            }

            dataProvider.SetSpriteRects(rects);

            var nameFileIdProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            var pairs = rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToArray();
            nameFileIdProvider.SetNameFileIdPairs(pairs);

            dataProvider.Apply();
            importer.SaveAndReimport();
        }

        private static Vector2Int GetSourceSize(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.GetSourceTextureWidthAndHeight(out int w, out int h);
            return new Vector2Int(w, h);
        }
    }
}
