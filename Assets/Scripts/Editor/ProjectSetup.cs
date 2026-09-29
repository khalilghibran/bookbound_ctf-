using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BookBound.EditorTools
{
    /// <summary>
    /// One-time / re-runnable data-asset setup. Wires existing art into the four GenreDefinition
    /// assets, builds the difficulty-band ShelfLayoutConfigs, the DifficultyCurve, and ShelfGeometry.
    /// Safe to re-run: it overwrites the same asset paths each time rather than duplicating them.
    /// </summary>
    public static class ProjectSetup
    {
        private const string GenreFolder = "Assets/Data/Genres";
        private const string ConfigFolder = "Assets/Data/Config";
        private const string ResourcesFolder = "Assets/Resources";

        private struct GenreSpec
        {
            public string Id;
            public string DisplayName;
            public Color Accent;
            public string SleepingSheet; // table/cover view
            public string SideSheet;     // shelved/spine view
        }

        private static readonly GenreSpec[] Genres =
        {
            new GenreSpec
            {
                Id = "fantasy", DisplayName = "Fantasy", Accent = new Color(0.227f, 0.129f, 0.298f),
                SleepingSheet = "Assets/Art/sleeping-view/fantasy_sleeping_spritesheet.png",
                SideSheet = "Assets/Art/side-view/fantasy_side_spritesheet.png",
            },
            new GenreSpec
            {
                Id = "science", DisplayName = "Science", Accent = new Color(0.031f, 0.286f, 0.125f),
                SleepingSheet = "Assets/Art/sleeping-view/science_sleeping_spritesheet.png",
                SideSheet = "Assets/Art/side-view/science_side_spritesheet.png",
            },
            new GenreSpec
            {
                Id = "history", DisplayName = "History", Accent = new Color(0.043f, 0.129f, 0.239f),
                SleepingSheet = "Assets/Art/sleeping-view/history_sleeping_spritesheet.png",
                SideSheet = "Assets/Art/side-view/history_side_spritesheet.png",
            },
            new GenreSpec
            {
                Id = "biography", DisplayName = "Biography", Accent = new Color(0.451f, 0.133f, 0.055f),
                SleepingSheet = "Assets/Art/sleeping-view/biography_sleeping_spritesheet.png",
                SideSheet = "Assets/Art/side-view/biography_side_spritesheet.png",
            },
        };

        [MenuItem("BookBound/Setup/Run All Data Setup")]
        public static void RunAll()
        {
            EnsureFolder(GenreFolder);
            EnsureFolder(ConfigFolder);

            CreateGenreDefinitions();
            CreateShelfLayoutConfigs();
            CreateDifficultyCurve();
            CreateShelfGeometry();
            CreateAnimationSettings();
            CreateSpriteFont();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ProjectSetup] Data asset setup complete.");
        }

        [MenuItem("BookBound/Setup/1. Create Genre Definitions")]
        public static void CreateGenreDefinitions()
        {
            EnsureFolder(GenreFolder);

            foreach (var spec in Genres)
            {
                Sprite[] covers = LoadOrderedSprites(spec.SleepingSheet, GenreDefinition.BooksPerGenre);
                Sprite[] spines = LoadOrderedSprites(spec.SideSheet, GenreDefinition.BooksPerGenre);

                string path = $"{GenreFolder}/{spec.Id}.asset";
                var genre = AssetDatabase.LoadAssetAtPath<GenreDefinition>(path);
                bool isNew = genre == null;
                if (isNew)
                {
                    genre = ScriptableObject.CreateInstance<GenreDefinition>();
                }

                var so = new SerializedObject(genre);
                so.FindProperty("id").stringValue = spec.Id;
                so.FindProperty("displayName").stringValue = spec.DisplayName;
                so.FindProperty("accentColor").colorValue = spec.Accent;
                // labelSprite intentionally left null - real genre-sign art exists (UI/Genres) but
                // plan.md section 3 specifies the temporary Information panel for now; wire it later.

                var coverProp = so.FindProperty("coverSprites");
                coverProp.arraySize = covers.Length;
                for (int i = 0; i < covers.Length; i++)
                    coverProp.GetArrayElementAtIndex(i).objectReferenceValue = covers[i];

                var spineProp = so.FindProperty("spineSprites");
                spineProp.arraySize = spines.Length;
                for (int i = 0; i < spines.Length; i++)
                    spineProp.GetArrayElementAtIndex(i).objectReferenceValue = spines[i];

                so.ApplyModifiedPropertiesWithoutUndo();

                if (isNew)
                    AssetDatabase.CreateAsset(genre, path);
                else
                    EditorUtility.SetDirty(genre);
            }
        }

        [MenuItem("BookBound/Setup/2. Create Shelf Layout Configs")]
        public static void CreateShelfLayoutConfigs()
        {
            EnsureFolder(ConfigFolder);

            CreateOrUpdateLayoutConfig("Band_0_2", 0.50f, 0.60f, 0.10f, 0.20f, false);
            CreateOrUpdateLayoutConfig("Band_3_5", 0.40f, 0.50f, 0.20f, 0.30f, false);
            CreateOrUpdateLayoutConfig("Band_6_9", 0.30f, 0.40f, 0.30f, 0.45f, false);
            CreateOrUpdateLayoutConfig("Band_10_13", 0.20f, 0.30f, 0.40f, 0.55f, false);
            CreateOrUpdateLayoutConfig("Band_14_Plus", 0.10f, 0.20f, 0.55f, 0.70f, true);
        }

        private static void CreateOrUpdateLayoutConfig(string assetName, float correctMin, float correctMax, float misMin, float misMax, bool shuffle)
        {
            string path = $"{ConfigFolder}/{assetName}.asset";
            var config = AssetDatabase.LoadAssetAtPath<ShelfLayoutConfig>(path);
            bool isNew = config == null;
            if (isNew) config = ScriptableObject.CreateInstance<ShelfLayoutConfig>();

            config.alreadyCorrectMin = correctMin;
            config.alreadyCorrectMax = correctMax;
            config.misplacedMin = misMin;
            config.misplacedMax = misMax;
            config.shuffleRowOrder = shuffle;

            if (isNew) AssetDatabase.CreateAsset(config, path);
            else EditorUtility.SetDirty(config);
        }

        [MenuItem("BookBound/Setup/3. Create Difficulty Curve")]
        public static void CreateDifficultyCurve()
        {
            EnsureFolder(ConfigFolder);

            string path = $"{ConfigFolder}/DifficultyCurve.asset";
            var curve = AssetDatabase.LoadAssetAtPath<DifficultyCurve>(path);
            bool isNew = curve == null;
            if (isNew) curve = ScriptableObject.CreateInstance<DifficultyCurve>();

            var bands = new (int min, string configName)[]
            {
                (0, "Band_0_2"),
                (3, "Band_3_5"),
                (6, "Band_6_9"),
                (10, "Band_10_13"),
                (14, "Band_14_Plus"),
            };

            var so = new SerializedObject(curve);
            var bandsProp = so.FindProperty("bands");
            bandsProp.arraySize = bands.Length;
            for (int i = 0; i < bands.Length; i++)
            {
                var config = AssetDatabase.LoadAssetAtPath<ShelfLayoutConfig>($"{ConfigFolder}/{bands[i].configName}.asset");
                if (config == null)
                    throw new InvalidOperationException($"Run 'Create Shelf Layout Configs' before 'Create Difficulty Curve' - missing {bands[i].configName}.");

                var element = bandsProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("minShelfIndex").intValue = bands[i].min;
                element.FindPropertyRelative("layoutConfig").objectReferenceValue = config;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            if (isNew) AssetDatabase.CreateAsset(curve, path);
            else EditorUtility.SetDirty(curve);
        }

        [MenuItem("BookBound/Setup/4. Create Shelf Geometry")]
        public static void CreateShelfGeometry()
        {
            EnsureFolder(ConfigFolder);

            // Measured from Assets/Art/ChatGPT_Image_Aug_2__2026__11_42_42_AM-removebg-preview.png
            // (612x408 source, 4 shelf boards detected at y ~ 108/181/255/326, interior x ~ 117-495).
            // Normalized to the full texture, bottom-left origin (Unity RectTransform convention).
            var rows = new[]
            {
                new ShelfGeometry.NormalizedRect(0.1912f, 0.7353f, 0.6176f, 0.1667f), // Row 0 (top) - Fantasy
                new ShelfGeometry.NormalizedRect(0.1912f, 0.5564f, 0.6176f, 0.1789f), // Row 1 - Science
                new ShelfGeometry.NormalizedRect(0.1912f, 0.3750f, 0.6176f, 0.1814f), // Row 2 - History
                new ShelfGeometry.NormalizedRect(0.1912f, 0.2010f, 0.6176f, 0.1740f), // Row 3 (bottom) - Biography
            };

            string path = $"{ConfigFolder}/ShelfGeometry.asset";
            var geometry = AssetDatabase.LoadAssetAtPath<ShelfGeometry>(path);
            bool isNew = geometry == null;
            if (isNew) geometry = ScriptableObject.CreateInstance<ShelfGeometry>();

            geometry.SetRowRects(rows);

            if (isNew) AssetDatabase.CreateAsset(geometry, path);
            else EditorUtility.SetDirty(geometry);
        }

        [MenuItem("BookBound/Setup/5. Create Animation Settings")]
        public static void CreateAnimationSettings()
        {
            EnsureFolder(ResourcesFolder);

            // Must live at exactly this Resources path - AnimationSettings.Instance loads it by name.
            string path = $"{ResourcesFolder}/AnimationSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<AnimationSettings>(path);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<AnimationSettings>();
                AssetDatabase.CreateAsset(settings, path);
            }
            // Never overwritten once it exists - it's the one asset in this pipeline meant for hand-tuning.
        }

        private const string LetterSheet = "Assets/UI/Fonts/BookBound-Fonts.png";

        // The sheet draws its caps at ~104px and its digits at ~81px. Normalising each class against
        // its own nominal is what makes "BOOKCASE 1" come out with the 1 the same height as the B,
        // instead of a digit three-quarters the size of the letters beside it.
        private const float NominalCapHeight = 104f;
        private const float NominalDigitHeight = 81f;

        [MenuItem("BookBound/Setup/9. Create Sprite Font")]
        public static void CreateSpriteFont()
        {
            EnsureFolder(ConfigFolder);

            var sprites = AssetDatabase.LoadAllAssetsAtPath(LetterSheet).OfType<Sprite>()
                .ToDictionary(s => s.name, s => s);
            if (sprites.Count == 0)
            {
                throw new InvalidOperationException(
                    $"{LetterSheet} has no sliced sprites. Run 'BookBound > Setup > 8. Slice HUD " +
                    "Overlay Sprites' first - it owns this sheet's rects.");
            }

            var glyphs = new List<SpriteFont.Glyph>();
            for (char c = 'A'; c <= 'Z'; c++) glyphs.Add(MakeGlyph(sprites, $"letter_{c}", c, NominalCapHeight));
            for (char c = '0'; c <= '9'; c++) glyphs.Add(MakeGlyph(sprites, $"digit_{c}", c, NominalDigitHeight));

            string path = $"{ConfigFolder}/BookBoundFont.asset";
            var font = AssetDatabase.LoadAssetAtPath<SpriteFont>(path);
            bool isNew = font == null;
            if (isNew) font = ScriptableObject.CreateInstance<SpriteFont>();

            font.SetGlyphs(glyphs.ToArray());
            EditorUtility.SetDirty(font);
            if (isNew) AssetDatabase.CreateAsset(font, path);

            Debug.Log($"[ProjectSetup] BookBoundFont: {glyphs.Count} glyphs from {LetterSheet}.");
        }

        private static SpriteFont.Glyph MakeGlyph(
            IReadOnlyDictionary<string, Sprite> sprites, string spriteName, char character, float nominal)
        {
            if (!sprites.TryGetValue(spriteName, out Sprite sprite))
                throw new InvalidOperationException($"{LetterSheet} is missing sub-sprite '{spriteName}'.");

            Rect r = sprite.rect;
            return new SpriteFont.Glyph
            {
                character = character,
                sprite = sprite,
                widthRatio = r.width / nominal,
                heightRatio = r.height / nominal,
            };
        }

        private static Sprite[] LoadOrderedSprites(string texturePath, int expectedCount)
        {
            var objects = AssetDatabase.LoadAllAssetsAtPath(texturePath);
            var sprites = objects.OfType<Sprite>()
                .OrderBy(s => ExtractTrailingIndex(s.name))
                .ToArray();

            if (sprites.Length != expectedCount)
            {
                throw new InvalidOperationException(
                    $"{texturePath}: expected {expectedCount} sliced sprites, found {sprites.Length}. " +
                    "Check the TextureImporter sprite mode / slicing.");
            }
            return sprites;
        }

        private static int ExtractTrailingIndex(string spriteName)
        {
            int underscore = spriteName.LastIndexOf('_');
            if (underscore < 0 || !int.TryParse(spriteName.Substring(underscore + 1), out int index))
                throw new InvalidOperationException($"Sprite name '{spriteName}' doesn't end in _<index>.");
            return index;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
