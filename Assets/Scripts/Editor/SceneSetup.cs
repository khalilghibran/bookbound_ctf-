using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BookBound.EditorTools
{
    /// <summary>
    /// Builds the MainScene hierarchy from scratch: camera, the Screen Space - Overlay canvas holding
    /// EVERYTHING visual (background, bookcase, table, shelf slot grid, table anchors, InfoPanel, HUD),
    /// EventSystem, and Systems root.
    ///
    /// Background/bookcase/table are plain UI Images anchored fractionally within the Canvas, not
    /// world-space SpriteRenderers - an earlier version used SpriteRenderers + camera-projected
    /// world-to-canvas math (WorldAlignedRect) to line the shelf/table grids up with them, which
    /// proved fragile across window sizes/aspect ratios (misaligned or "jumbled" at some resolutions).
    /// Putting everything in the Canvas makes slot/anchor placement pure UI anchoring, which Unity
    /// gets right at any resolution with no custom projection math at all.
    ///
    /// Re-runnable: wipes and rebuilds the scene each time rather than accumulating duplicates.
    /// </summary>
    public static class SceneSetup
    {
        private const string ScenePath = "Assets/Scenes/MainScene.unity";

        // HUD overlay art - see SliceHudOverlaySprites for exactly which named sub-sprite comes from where.
        private const string HudIconsSheet = "Assets/UI/HUD/bookbound_ui_hud_icons_spritesheet.png";
        private const string PanelSheet = "Assets/UI/Panels/bookbound_ui_panels_spritesheet.png";
        private const string ModalSheet = "Assets/UI/Modals/bookbound_ui_modals_results_spritesheet.png";
        private const string DecorationsSheet = "Assets/UI/Decorations/bookbound_ui_decorations_spritesheet.png";
        private const string ControlsSheet = "Assets/UI/Controls/bookbound_ui_controls_spritesheet.png";
        private const string FontsSheet = "Assets/UI/Fonts/bookbound_ui_counter_glyphs_spritesheet.png";
        private const string SliderSheet = "Assets/UI/Controls/Slider2-BookBound.png";
        private const string HeartPath = "Assets/Art/Hearts_transparent.png";
        private const string PauseBackdropPath = "Assets/Art/PausedInterface-Background.png";
        private static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

        // Fractional rects within the full-screen background image, derived from the bookcase/table
        // art's measured proportions against the background (see plan.md section 0/1 asset mapping).
        private static readonly Vector2 BookcaseAnchorMin = new Vector2(0.2496f, 0.2939f);
        private static readonly Vector2 BookcaseAnchorMax = new Vector2(0.7648f, 0.9038f);
        private static readonly Vector2 TableAnchorMin = new Vector2(0.2530f, 0f);
        private static readonly Vector2 TableAnchorMax = new Vector2(0.7614f, 0.2438f);

        [MenuItem("BookBound/Setup/5. Build Main Scene")]
        public static void BuildMainScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCamera();

            // --- Canvas ---
            // Screen Space - Overlay: this project's URP setup silently fails to composite
            // Screen Space - Camera UI content (confirmed by toggling render mode live - Overlay fixed
            // it instantly). Overlay needs no camera reference and gives the same
            // EventSystem/GraphicRaycaster drag-drop behavior the plan wants.
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();

            // --- AspectLock: the ONE place aspect ratio is handled. Locks a 16:9 region to the
            // screen (letterboxed/pillarboxed as needed) via AspectRatioFitter, and every other
            // element below is anchored as a fraction of THIS container, not of the raw Canvas.
            // The Canvas's own pixel dimensions vary with the actual window's aspect ratio (it is
            // NOT always 1920x1080 even though that's the reference resolution) - fractional anchors
            // computed against the raw Canvas were only ever correct at exactly 16:9. Locking one
            // container up front makes every fractional rect below correct at any window size.
            var aspectLockGo = CreateUIObject(canvasRect, "AspectLock");
            RectTransform aspectLockRt = aspectLockGo.GetComponent<RectTransform>();
            aspectLockRt.anchorMin = Vector2.zero;
            aspectLockRt.anchorMax = Vector2.one;
            aspectLockRt.offsetMin = Vector2.zero;
            aspectLockRt.offsetMax = Vector2.zero;
            var aspectLockFitter = aspectLockGo.AddComponent<AspectRatioFitter>();
            aspectLockFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspectLockFitter.aspectRatio = ReferenceResolution.x / ReferenceResolution.y;
            RectTransform contentRoot = aspectLockRt;

            // --- Background, full-screen within the locked 16:9 area ---
            var backgroundGo = CreateUIObject(contentRoot, "Background");
            RectTransform backgroundRt = backgroundGo.GetComponent<RectTransform>();
            backgroundRt.anchorMin = Vector2.zero;
            backgroundRt.anchorMax = Vector2.one;
            backgroundRt.offsetMin = Vector2.zero;
            backgroundRt.offsetMax = Vector2.zero;
            var backgroundImg = backgroundGo.AddComponent<Image>();
            backgroundImg.sprite = LoadSprite("Assets/ChatGPT Image Aug 2, 2026, 12_00_45 PM.png");
            backgroundImg.raycastTarget = false;

            // --- BookcaseImage + ShelfRoot as its child (pure fractional UI anchoring, no camera math) ---
            var bookcaseGo = CreateUIObject(contentRoot, "BookcaseImage");
            RectTransform bookcaseRt = bookcaseGo.GetComponent<RectTransform>();
            bookcaseRt.anchorMin = BookcaseAnchorMin;
            bookcaseRt.anchorMax = BookcaseAnchorMax;
            bookcaseRt.offsetMin = Vector2.zero;
            bookcaseRt.offsetMax = Vector2.zero;
            var bookcaseImg = bookcaseGo.AddComponent<Image>();
            bookcaseImg.sprite = LoadSprite("Assets/Art/ChatGPT_Image_Aug_2__2026__11_42_42_AM-removebg-preview.png");
            bookcaseImg.raycastTarget = false;

            var shelfRootGo = CreateUIObject(bookcaseRt, "ShelfRoot");
            RectTransform shelfRoot = shelfRootGo.GetComponent<RectTransform>();
            shelfRoot.anchorMin = Vector2.zero;
            shelfRoot.anchorMax = Vector2.one;
            shelfRoot.offsetMin = Vector2.zero;
            shelfRoot.offsetMax = Vector2.zero;

            var geometry = AssetDatabase.LoadAssetAtPath<ShelfGeometry>("Assets/Data/Config/ShelfGeometry.asset");
            if (geometry == null)
                throw new InvalidOperationException("Run 'Create Shelf Geometry' (Setup step 4) before building the scene.");

            for (int r = 0; r < ShelfGeometry.RowCount; r++)
            {
                var rowRect = geometry.GetRowRect(r);
                var rowGo = CreateUIObject(shelfRoot, $"Row_{r}");
                RectTransform rowRt = rowGo.GetComponent<RectTransform>();
                rowRt.anchorMin = new Vector2(rowRect.x, rowRect.y);
                rowRt.anchorMax = new Vector2(rowRect.x + rowRect.width, rowRect.y + rowRect.height);
                rowRt.offsetMin = Vector2.zero;
                rowRt.offsetMax = Vector2.zero;

                var labelGo = CreateUIObject(rowRt, "GenreLabel");
                labelGo.SetActive(false); // labelSprite art not wired in yet - see plan.md section 3/15.3

                for (int s = 0; s < ShelfGeometry.SlotsPerRow; s++)
                {
                    var slotGo = CreateUIObject(rowRt, $"Slot_{s}");
                    RectTransform slotRt = slotGo.GetComponent<RectTransform>();
                    float x0 = s / (float)ShelfGeometry.SlotsPerRow;
                    float x1 = (s + 1) / (float)ShelfGeometry.SlotsPerRow;
                    slotRt.anchorMin = new Vector2(x0, 0f);
                    slotRt.anchorMax = new Vector2(x1, 1f);
                    slotRt.offsetMin = Vector2.zero;
                    slotRt.offsetMax = Vector2.zero;

                    // Debug-only alignment guide for Phase 0/1 verification. Kept invisible (alpha 0).
                    var img = slotGo.AddComponent<Image>();
                    img.color = new Color(0.2f, 1f, 0.4f, 0f);
                    img.raycastTarget = false;

                    CreateSlotHighlight(slotRt);
                    slotGo.AddComponent<ShelfSlot>().Configure(r, s);
                }
            }

            var shelfController = shelfRootGo.AddComponent<ShelfController>();
            var shelfControllerSo = new SerializedObject(shelfController);
            shelfControllerSo.FindProperty("shelfRoot").objectReferenceValue = shelfRoot;
            shelfControllerSo.ApplyModifiedPropertiesWithoutUndo();

            // --- TableImage + TableRoot as its child ---
            var tableGo = CreateUIObject(contentRoot, "TableImage");
            RectTransform tableImgRt = tableGo.GetComponent<RectTransform>();
            tableImgRt.anchorMin = TableAnchorMin;
            tableImgRt.anchorMax = TableAnchorMax;
            tableImgRt.offsetMin = Vector2.zero;
            tableImgRt.offsetMax = Vector2.zero;
            var tableImg = tableGo.AddComponent<Image>();
            tableImg.sprite = LoadSprite("Assets/Art/ChatGPT_Image_Aug_2__2026__12_15_06_PM-removebg-preview.png");
            tableImg.raycastTarget = false;

            var tableRootGo = CreateUIObject(tableImgRt, "TableRoot");
            RectTransform tableRoot = tableRootGo.GetComponent<RectTransform>();
            tableRoot.anchorMin = Vector2.zero;
            tableRoot.anchorMax = Vector2.one;
            tableRoot.offsetMin = Vector2.zero;
            tableRoot.offsetMax = Vector2.zero;

            // 5 anchors spaced fractionally across the table image so they stay visually on the table
            // at any window size.
            // The table art is a flat top surface (the top ~25% of the trimmed image, measured via a
            // pixel luminance scan - the lit top vs shadowed front face) sitting on a much thicker
            // front face/legs. Anchor books so their base sits well inside that surface band (not
            // hanging over the front edge) and smaller overall, per user feedback that books read as
            // oversized and too close to the table's front edge.
            var tableAnchors = new RectTransform[5];
            for (int i = 0; i < tableAnchors.Length; i++)
            {
                float x0 = 0.06f + i * 0.15f;
                var anchorGo = CreateUIObject(tableRoot, $"TableAnchor_{i}");
                RectTransform art = anchorGo.GetComponent<RectTransform>();
                art.anchorMin = new Vector2(x0, 0.76f);
                art.anchorMax = new Vector2(x0 + 0.115f, 1.10f);
                art.offsetMin = Vector2.zero;
                art.offsetMax = Vector2.zero;
                tableAnchors[i] = art;
            }

            // Only anchor 0 is a drop target (TableSlot), so only it needs a highlight.
            CreateSlotHighlight(tableAnchors[0]);

            // TableSpawnPoint: anchored to the Canvas corner (like InfoPanel) - just outside the
            // InfoPanel's left edge (panel left edge = 1920-40-460=1420) so new books slide in from
            // off-screen right without ever passing under/behind it.
            var spawnGo = CreateUIObject(contentRoot, "TableSpawnPoint");
            RectTransform spawnRt = spawnGo.GetComponent<RectTransform>();
            spawnRt.anchorMin = new Vector2(1f, 0f);
            spawnRt.anchorMax = new Vector2(1f, 0f);
            spawnRt.pivot = new Vector2(0.5f, 0.5f);
            spawnRt.anchoredPosition = new Vector2(-600f, 170f);
            spawnRt.sizeDelta = new Vector2(160f, 200f);

            var bookQueue = tableRootGo.AddComponent<BookQueue>();
            var bookQueueSo = new SerializedObject(bookQueue);
            var anchorsProp = bookQueueSo.FindProperty("anchors");
            anchorsProp.arraySize = tableAnchors.Length;
            for (int i = 0; i < tableAnchors.Length; i++)
                anchorsProp.GetArrayElementAtIndex(i).objectReferenceValue = tableAnchors[i];
            bookQueueSo.FindProperty("spawnPoint").objectReferenceValue = spawnRt;
            bookQueueSo.ApplyModifiedPropertiesWithoutUndo();

            // --- InfoPanel a.k.a. the Genre Guide (plan.md section 3) ---
            InfoPanelController infoPanel = BuildInfoPanel(contentRoot);

            // --- Controls hint (bottom-left): how to pick up / drop a book ---
            BuildControlsHint(contentRoot);

            // --- DragLayer - must stay the last child (within the locked content area) so dragged books render on top ---
            var dragLayerGo = CreateUIObject(contentRoot, "DragLayer");
            RectTransform dragLayer = dragLayerGo.GetComponent<RectTransform>();
            dragLayer.anchorMin = Vector2.zero;
            dragLayer.anchorMax = Vector2.one;
            dragLayer.offsetMin = Vector2.zero;
            dragLayer.offsetMax = Vector2.zero;
            dragLayerGo.AddComponent<DragLayerMarker>();

            // --- Systems root (created before HUD so ShelfTimer exists to wire into HUDController) ---
            var systemsGo = new GameObject("Systems");
            var gameManagerGo = new GameObject("GameManager");
            gameManagerGo.transform.SetParent(systemsGo.transform);
            BuildAudioManager(systemsGo.transform);
            gameManagerGo.AddComponent<BookBound.Debugging.AutoScreenshot>(); // dev-only, see its own doc comment
            var shelfTimer = gameManagerGo.AddComponent<ShelfTimer>();

            // --- HUD + shell (title/pause/game over/banner) ---
            HUDController hudController = BuildHud(contentRoot, shelfTimer, out ShellUI shellUI,
                out OptionsPanelController optionsPanel);

            // DragLayer must remain last among the content root's children - HUD overlays (pause/game
            // over) intentionally sit above gameplay but below a book actively being dragged.
            dragLayerGo.transform.SetAsLastSibling();

            // --- ScreenFader - above literally everything, including a book mid-drag, so a state
            // transition (title -> game, game over -> restart) can black out the whole screen ---
            ScreenFader screenFader = BuildScreenFader(contentRoot);

            // --- EventSystem (project uses the new Input System exclusively) ---
            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<InputSystemUIInputModule>();

            var bookPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Book.prefab");
            if (bookPrefab == null)
                throw new InvalidOperationException("Run 'Build Book Prefab' (Setup step 6) before building the scene.");

            // Row order top to bottom. One genre per row - the array length IS the row count.
            string[] genreIds = { "fantasy", "science", "history", "biography" };
            if (genreIds.Length != ShelfGeometry.RowCount)
                throw new InvalidOperationException($"{genreIds.Length} genres for {ShelfGeometry.RowCount} rows - they must match.");

            var genreAssets = new GenreDefinition[genreIds.Length];
            for (int i = 0; i < genreIds.Length; i++)
            {
                genreAssets[i] = AssetDatabase.LoadAssetAtPath<GenreDefinition>($"Assets/Data/Genres/{genreIds[i]}.asset");
                if (genreAssets[i] == null)
                    throw new InvalidOperationException($"Missing {genreIds[i]}.asset - run 'Create Genre Definitions' (Setup step 1) before building the scene.");
            }

            var difficultyCurve = AssetDatabase.LoadAssetAtPath<DifficultyCurve>("Assets/Data/Config/DifficultyCurve.asset");
            if (difficultyCurve == null)
                throw new InvalidOperationException("Run 'Create Difficulty Curve' (Setup step 3) before building the scene.");

            var gameManager = gameManagerGo.AddComponent<GameManager>();
            var gmSo = new SerializedObject(gameManager);
            var genresProp = gmSo.FindProperty("genres");
            genresProp.arraySize = genreAssets.Length;
            for (int i = 0; i < genreAssets.Length; i++)
                genresProp.GetArrayElementAtIndex(i).objectReferenceValue = genreAssets[i];
            gmSo.FindProperty("difficultyCurve").objectReferenceValue = difficultyCurve;
            gmSo.FindProperty("shelfController").objectReferenceValue = shelfController;
            gmSo.FindProperty("bookQueue").objectReferenceValue = bookQueue;
            gmSo.FindProperty("infoPanel").objectReferenceValue = infoPanel;
            gmSo.FindProperty("shelfTimer").objectReferenceValue = shelfTimer;
            gmSo.FindProperty("hudController").objectReferenceValue = hudController;
            gmSo.FindProperty("shellUI").objectReferenceValue = shellUI;
            gmSo.FindProperty("optionsPanel").objectReferenceValue = optionsPanel;
            gmSo.FindProperty("screenFader").objectReferenceValue = screenFader;
            gmSo.FindProperty("bookPrefab").objectReferenceValue = bookPrefab;
            gmSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[SceneSetup] Built and saved {ScenePath}");
        }

        private static void BuildCamera()
        {
            // Vestigial for now (Overlay canvas doesn't need it) - kept for AudioListener and any
            // future camera-shake/juice work.
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 4.705f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camGo.AddComponent<AudioListener>();
        }

        /// <summary>The "Genre Guide": a single parchment background with a ribbon title straddling
        /// its top edge and one plain text line per row ("ROW 1 - FANTASY"). No per-genre art - an
        /// earlier version used one genre_bar_* nameplate sprite per row, stretched across the whole
        /// row's width, which looked bad (a ~289px-wide plaque stretched that far softens badly and
        /// its baked-in corner icons drag apart from the border). Ribbon title math matches
        /// CreateFramedWindow's (see there for why the band isn't the sprite's vertical centre).</summary>
        private static InfoPanelController BuildInfoPanel(RectTransform parent)
        {
            Sprite parchment = LoadNamedSprite(ModalSheet, "modal_parchment");

            const float panelWidth = 340f;
            const float panelHeight = 300f;
            const float ribbonAspect = 509f / 171f;
            const float bandHeightRatio = 67f / 171f;
            const float bandOffsetRatio = -4.5f / 171f;
            const float ribbonWidth = 180f;
            const float ribbonHeight = ribbonWidth / ribbonAspect;

            var panelGo = CreateUIObject(parent, "InfoPanel");
            RectTransform panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(1f, 0f);
            panelRt.anchorMax = new Vector2(1f, 0f);
            panelRt.pivot = new Vector2(1f, 0f);
            panelRt.anchoredPosition = new Vector2(-40f, 40f);
            panelRt.sizeDelta = new Vector2(panelWidth, panelHeight);

            var bgGo = CreateUIObject(panelRt, "Background");
            RectTransform bgRt = bgGo.GetComponent<RectTransform>();
            Stretch(bgRt);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.sprite = parchment;
            bgImg.raycastTarget = false;

            // Ribbon rides the parchment's top edge, half above and half onto it.
            var ribbonGo = CreateUIObject(panelRt, "Ribbon");
            RectTransform ribbonRt = ribbonGo.GetComponent<RectTransform>();
            ribbonRt.anchorMin = ribbonRt.anchorMax = new Vector2(0.5f, 1f);
            ribbonRt.pivot = new Vector2(0.5f, 0.5f);
            ribbonRt.anchoredPosition = Vector2.zero;
            ribbonRt.sizeDelta = new Vector2(ribbonWidth, ribbonHeight);
            var ribbonImg = ribbonGo.AddComponent<Image>();
            ribbonImg.sprite = LoadNamedSprite(ModalSheet, "modal_banner");
            ribbonImg.preserveAspect = true;
            ribbonImg.raycastTarget = false;

            float bandHeight = ribbonHeight * bandHeightRatio;
            float titleWidth = ribbonWidth * 0.62f;
            float titleCapHeight = LoadSpriteFont().FitCapHeight("GENRE GUIDE", titleWidth, bandHeight * 0.66f);
            CreateSpriteLabel(ribbonRt, "Title", "GENRE GUIDE", titleCapHeight,
                new Vector2(0f, ribbonHeight * bandOffsetRatio), new Vector2(titleWidth, bandHeight));

            // Every row shares one left starting X rather than each line centering independently
            // (which put "ROW 1 FANTASY" and "ROW 4 BIOGRAPHY" at different left edges) - computed so
            // the WIDEST of the 16 row/genre combinations that can ever appear here lands with equal
            // margin on both sides, which reads as "block is centered, lines inside it are left-aligned."
            const float rowCapHeight = 17f;
            const float rowInset = 15f; // matches the old -30f (15 each side) symmetric inset
            float rowContentWidth = panelWidth - rowInset * 2f;
            string[] genreNames = { "FANTASY", "SCIENCE", "HISTORY", "BIOGRAPHY" };
            float maxLineWidth = 0f;
            for (int r = 1; r <= ShelfGeometry.RowCount; r++)
                foreach (string g in genreNames)
                    maxLineWidth = Mathf.Max(maxLineWidth, LoadSpriteFont().MeasureWidth($"ROW {r} {g}", rowCapHeight));
            float sideMargin = Mathf.Max(0f, (rowContentWidth - maxLineWidth) / 2f);

            var rowLabels = new SpriteText[ShelfGeometry.RowCount];
            float rowsTop = -(ribbonHeight * 0.5f) - 30f; // clear of the ribbon's visible bottom
            float rowStep = (panelHeight - (ribbonHeight * 0.5f) - 50f) / ShelfGeometry.RowCount;
            for (int i = 0; i < ShelfGeometry.RowCount; i++)
            {
                var rowGo = CreateUIObject(panelRt, $"Row_{i}");
                var text = rowGo.AddComponent<SpriteText>();
                text.Configure(LoadSpriteFont(), rowCapHeight, TextAnchor.MiddleLeft, ParchmentInk, string.Empty);

                // anchorMin/Max = (0,1)-(1,1): stretched horizontally, a single point at the parent's
                // top edge vertically. Setting offsetMin/Max directly for both axes (rather than mixing
                // in anchoredPosition/sizeDelta, which alias into these on a partially-stretched rect
                // and are easy to clobber with stale values) is the unambiguous way to place one.
                RectTransform rowRt = rowGo.GetComponent<RectTransform>();
                rowRt.anchorMin = new Vector2(0f, 1f);
                rowRt.anchorMax = new Vector2(1f, 1f);
                rowRt.pivot = new Vector2(0.5f, 1f);
                float rowTopY = rowsTop - i * rowStep;
                rowRt.offsetMin = new Vector2(rowInset + sideMargin, rowTopY - rowStep);
                rowRt.offsetMax = new Vector2(-rowInset, rowTopY);
                rowLabels[i] = text;
            }

            var controller = panelGo.AddComponent<InfoPanelController>();
            var so = new SerializedObject(controller);
            var labelsProp = so.FindProperty("rowLabels");
            labelsProp.arraySize = rowLabels.Length;
            for (int i = 0; i < rowLabels.Length; i++) labelsProp.GetArrayElementAtIndex(i).objectReferenceValue = rowLabels[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            return controller;
        }

        /// <summary>Static (no game state to drive) - just the mouse-icon + instruction pair the
        /// reference calls for. Bottom-left, mirroring the Genre Guide's bottom-right placement.</summary>
        private static void BuildControlsHint(RectTransform parent)
        {
            Sprite leftClick = LoadNamedSprite(ControlsSheet, "mouse_left_click");
            Sprite release = LoadNamedSprite(ControlsSheet, "mouse_release");
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var panelGo = CreateUIObject(parent, "ControlsHint");
            RectTransform panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0f, 0f);
            panelRt.pivot = new Vector2(0f, 0f);
            panelRt.anchoredPosition = new Vector2(40f, 40f);
            panelRt.sizeDelta = new Vector2(340f, 140f);
            var bg = panelGo.AddComponent<Image>();
            bg.sprite = uiSprite;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.08f, 0.07f, 0.1f, 0.82f);
            bg.raycastTarget = false;

            AddControlsRow(panelRt, leftClick, "LEFT CLICK\nTO DRAG", 0);
            AddControlsRow(panelRt, release, "RELEASE\nTO DROP", 1);
        }

        private static void AddControlsRow(RectTransform panelRt, Sprite icon, string label, int rowIndex)
        {
            const float rowHeight = 70f;
            float centerY = -rowHeight * (rowIndex + 0.5f);

            var iconGo = CreateUIObject(panelRt, "Icon");
            RectTransform iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 1f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = new Vector2(48f, centerY);
            iconRt.sizeDelta = new Vector2(52f, 52f);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            var textGo = CreateUIObject(panelRt, "Label");
            var text = textGo.AddComponent<SpriteText>();
            text.Configure(LoadSpriteFont(), 13f, TextAnchor.MiddleLeft,
                new Color(0.94f, 0.9f, 0.82f, 1f), label);
            RectTransform textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0f, 1f);
            textRt.anchorMax = new Vector2(0f, 1f);
            textRt.pivot = new Vector2(0f, 0.5f);
            textRt.anchoredPosition = new Vector2(90f, centerY);
            textRt.sizeDelta = new Vector2(220f, rowHeight - 8f);
        }

        private static Sprite[] LoadFontDigits()
        {
            var digits = new Sprite[10];
            for (int i = 0; i < digits.Length; i++)
                digits[i] = LoadNamedSprite(FontsSheet, $"font_{i}");
            return digits;
        }

        /// <summary>A row of Image "slots" driven by a DigitStrip - renders a fixed-width numeric/time
        /// string with the bitmap-numeral sprites instead of a font.</summary>
        private static DigitStrip CreateDigitStrip(RectTransform parent, string name, int slotCount,
            Vector2 slotSize, float spacing, Sprite[] digitSprites, Sprite colonSprite)
        {
            var go = CreateUIObject(parent, name);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            float totalWidth = slotCount * slotSize.x + (slotCount - 1) * spacing;
            rt.sizeDelta = new Vector2(totalWidth, slotSize.y);

            var slots = new Image[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                var slotGo = CreateUIObject(rt, $"Slot_{i}");
                RectTransform slotRt = slotGo.GetComponent<RectTransform>();
                slotRt.anchorMin = slotRt.anchorMax = new Vector2(0f, 0.5f);
                slotRt.pivot = new Vector2(0f, 0.5f);
                slotRt.anchoredPosition = new Vector2(i * (slotSize.x + spacing), 0f);
                slotRt.sizeDelta = slotSize;
                var img = slotGo.AddComponent<Image>();
                img.preserveAspect = true;
                img.raycastTarget = false;
                slots[i] = img;
            }

            var strip = go.AddComponent<DigitStrip>();
            var so = new SerializedObject(strip);
            SerializedProperty slotsProp = so.FindProperty("slots");
            slotsProp.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++) slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            SerializedProperty digitsProp = so.FindProperty("digitSprites");
            digitsProp.arraySize = digitSprites.Length;
            for (int i = 0; i < digitSprites.Length; i++) digitsProp.GetArrayElementAtIndex(i).objectReferenceValue = digitSprites[i];
            so.FindProperty("colonSprite").objectReferenceValue = colonSprite;
            so.ApplyModifiedPropertiesWithoutUndo();

            return strip;
        }

        /// <summary>One Image per starting heart, top-left. Uses the trimmed single-heart art; a lost
        /// heart is hidden outright rather than swapped for an empty-heart sprite, because no empty
        /// heart exists in the art set.</summary>
        private static Image[] BuildHearts(RectTransform hudRt)
        {
            Sprite heartSprite = LoadSprite(HeartPath);
            const float size = 46f;
            const float gap = 8f;

            var hearts = new Image[LivesService.StartingHearts];
            for (int i = 0; i < hearts.Length; i++)
            {
                var go = CreateUIObject(hudRt, $"Heart_{i}");
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(40f + i * (size + gap), -28f);
                rt.sizeDelta = new Vector2(size, size);

                var img = go.AddComponent<Image>();
                img.sprite = heartSprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                hearts[i] = img;
            }
            return hearts;
        }

        private static HUDController BuildHud(RectTransform parent, ShelfTimer shelfTimer,
            out ShellUI shellUI, out OptionsPanelController optionsPanel)
        {
            var hudGo = CreateUIObject(parent, "HUD");
            RectTransform hudRt = hudGo.GetComponent<RectTransform>();
            hudRt.anchorMin = Vector2.zero;
            hudRt.anchorMax = Vector2.one;
            hudRt.offsetMin = Vector2.zero;
            hudRt.offsetMax = Vector2.zero;

            Sprite[] fontDigits = LoadFontDigits();
            Sprite fontColon = LoadNamedSprite(FontsSheet, "font_colon");
            Sprite purpleBar = LoadNamedSprite(PanelSheet, "panel_purple_bar");
            Sprite decoStar = LoadNamedSprite(DecorationsSheet, "deco_star");
            Sprite clockIcon = LoadNamedSprite(HudIconsSheet, "hud_clock");
            Sprite pauseIcon = LoadNamedSprite(HudIconsSheet, "hud_pause");
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            Image[] heartImages = BuildHearts(hudRt);

            var bookcaseText = CreateSpriteLabel(hudRt, "BookcaseLabel", "BOOKCASE 1", 15f,
                Vector2.zero, new Vector2(300f, 30f), TextAnchor.MiddleLeft, Color.white);
            var bookcaseRt = (RectTransform)bookcaseText.transform;
            bookcaseRt.anchorMin = bookcaseRt.anchorMax = new Vector2(0f, 1f);
            bookcaseRt.pivot = new Vector2(0f, 1f);
            bookcaseRt.anchoredPosition = new Vector2(40f, -88f);

            // --- Score plaque: purple nameplate bar, "SCORE" label flanked by sparkles, digits below ---
            var scoreGo = CreateUIObject(hudRt, "ScorePlaque");
            RectTransform scoreRt = scoreGo.GetComponent<RectTransform>();
            scoreRt.anchorMin = scoreRt.anchorMax = new Vector2(0.5f, 1f);
            scoreRt.pivot = new Vector2(0.5f, 1f);
            scoreRt.anchoredPosition = new Vector2(0f, -18f);
            scoreRt.sizeDelta = new Vector2(320f, 96f);
            var scoreBg = scoreGo.AddComponent<Image>();
            scoreBg.sprite = purpleBar;
            scoreBg.raycastTarget = false;

            CreateSpriteLabel(scoreRt, "Label", "SCORE", 15f, new Vector2(0f, 20f), new Vector2(200f, 26f));

            foreach (float sign in new[] { -1f, 1f })
            {
                var starGo = CreateUIObject(scoreRt, "Sparkle");
                RectTransform starRt = starGo.GetComponent<RectTransform>();
                starRt.anchorMin = starRt.anchorMax = new Vector2(0.5f, 1f);
                starRt.pivot = new Vector2(0.5f, 0.5f);
                starRt.anchoredPosition = new Vector2(sign * 110f, -32f);
                starRt.sizeDelta = new Vector2(22f, 22f);
                var starImg = starGo.AddComponent<Image>();
                starImg.sprite = decoStar;
                starImg.preserveAspect = true;
                starImg.raycastTarget = false;
            }

            DigitStrip scoreDigits = CreateDigitStrip(scoreRt, "Digits", 5, new Vector2(28f, 40f), 2f, fontDigits, fontColon);
            RectTransform scoreDigitsRt = (RectTransform)scoreDigits.transform;
            scoreDigitsRt.anchorMin = scoreDigitsRt.anchorMax = new Vector2(0.5f, 1f);
            scoreDigitsRt.anchoredPosition = new Vector2(0f, -60f);

            // --- Timer: clock icon overlapping a dark pill, MM:SS digits inside it ---
            var timerGo = CreateUIObject(hudRt, "Timer");
            RectTransform timerRt = timerGo.GetComponent<RectTransform>();
            timerRt.anchorMin = timerRt.anchorMax = new Vector2(1f, 1f);
            timerRt.pivot = new Vector2(1f, 1f);
            timerRt.anchoredPosition = new Vector2(-160f, -30f);
            timerRt.sizeDelta = new Vector2(190f, 56f);

            var pillGo = CreateUIObject(timerRt, "Pill");
            RectTransform pillRt = pillGo.GetComponent<RectTransform>();
            pillRt.anchorMin = Vector2.zero;
            pillRt.anchorMax = Vector2.one;
            pillRt.offsetMin = new Vector2(28f, 0f);
            pillRt.offsetMax = Vector2.zero;
            var pillImg = pillGo.AddComponent<Image>();
            pillImg.sprite = uiSprite;
            pillImg.type = Image.Type.Sliced;
            pillImg.color = new Color(0.08f, 0.09f, 0.14f, 0.9f);
            pillImg.raycastTarget = false;

            var clockGo = CreateUIObject(timerRt, "ClockIcon");
            RectTransform clockRt = clockGo.GetComponent<RectTransform>();
            clockRt.anchorMin = clockRt.anchorMax = new Vector2(0f, 0.5f);
            clockRt.pivot = new Vector2(0.5f, 0.5f);
            clockRt.anchoredPosition = new Vector2(28f, 0f);
            clockRt.sizeDelta = new Vector2(56f, 56f);
            var clockImg = clockGo.AddComponent<Image>();
            clockImg.sprite = clockIcon;
            clockImg.preserveAspect = true;
            clockImg.raycastTarget = false;

            DigitStrip timerDigits = CreateDigitStrip(pillRt, "Digits", 5, new Vector2(18f, 32f), 1f, fontDigits, fontColon);
            RectTransform timerDigitsRt = (RectTransform)timerDigits.transform;
            timerDigitsRt.anchorMin = timerDigitsRt.anchorMax = new Vector2(0.5f, 0.5f);
            timerDigitsRt.anchoredPosition = new Vector2(6f, 0f);

            // Esc also pauses, but a visible button is the one that can't be missed (plan.md section 11).
            Button pauseButton = CreateArtButton(hudRt, "PauseButton", pauseIcon, null, Vector2.zero, new Vector2(56f, 56f), interactable: true);
            RectTransform pauseRt = pauseButton.GetComponent<RectTransform>();
            pauseRt.anchorMin = pauseRt.anchorMax = new Vector2(1f, 1f);
            pauseRt.pivot = new Vector2(1f, 1f);
            pauseRt.anchoredPosition = new Vector2(-40f, -30f);

            // Keep this above ControlsHint (which ends at y=180); Shell overlays block it off-state.
            Button shuffleButton = CreatePlateButton(hudRt, "ShuffleButton", "SHUFFLE TABLE",
                new Vector2(260f, 230f), 440f);
            RectTransform shuffleRt = shuffleButton.GetComponent<RectTransform>();
            shuffleRt.anchorMin = shuffleRt.anchorMax = Vector2.zero;
            shuffleRt.pivot = new Vector2(0.5f, 0.5f);
            shuffleRt.anchoredPosition = new Vector2(260f, 230f);

            shellUI = BuildShell(hudRt, pauseButton, shuffleButton, out optionsPanel);

            var hudController = hudGo.AddComponent<HUDController>();
            var hudSo = new SerializedObject(hudController);
            SerializedProperty heartsProp = hudSo.FindProperty("hearts");
            heartsProp.arraySize = heartImages.Length;
            for (int i = 0; i < heartImages.Length; i++)
                heartsProp.GetArrayElementAtIndex(i).objectReferenceValue = heartImages[i];
            hudSo.FindProperty("scoreDigits").objectReferenceValue = scoreDigits;
            hudSo.FindProperty("scorePlaque").objectReferenceValue = scoreRt;
            hudSo.FindProperty("timerDigits").objectReferenceValue = timerDigits;
            hudSo.FindProperty("timerGroup").objectReferenceValue = timerRt;
            hudSo.FindProperty("bookcaseText").objectReferenceValue = bookcaseText;
            hudSo.FindProperty("shelfTimer").objectReferenceValue = shelfTimer;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            return hudController;
        }

        /// <summary>
        /// Title / pause / game over / banner. Code-drawn from plain rectangles and text, matching the
        /// InfoPanel and HUD already in the scene: the UI spritesheets are auto-sliced into unnamed
        /// index-numbered sub-sprites, so wiring real panel art is a separate identify-and-map job.
        /// Every panel is a full-screen raycast blocker, which is also what stops drags off-state.
        /// </summary>
        private static ShellUI BuildShell(RectTransform hudRt, Button pauseButton, Button shuffleButton,
            out OptionsPanelController optionsPanel)
        {
            var shellGo = CreateUIObject(hudRt, "Shell");
            RectTransform shellRt = shellGo.GetComponent<RectTransform>();
            Stretch(shellRt);

            // --- Title --- reworked onto the real title-screen art: StartScreen-Background.png as the
            // backdrop, the BOOKBOUND logo lockup, and the button spritesheet's nameplate buttons (see
            // SliceStartScreenSprites for where the named sub-sprites come from). Three Start plates
            // choose difficulty; Options and Quit use their corresponding buttons.
            const string ButtonSheet = "Assets/Art/bookbound-spritesheet-purple-hover-transparent.png";
            Sprite startScreenBg = LoadSprite("Assets/Art/StartScreen-Background.png");
            Sprite logoNormal = LoadNamedSprite(ButtonSheet, "logo_normal");
            Sprite startNormal = LoadNamedSprite(ButtonSheet, "start_normal");
            Sprite startHover = LoadNamedSprite(ButtonSheet, "start_hover");
            Sprite optionsNormal = LoadNamedSprite(ButtonSheet, "options_selected");
            Sprite optionsHover = LoadNamedSprite(ButtonSheet, "options_hover");
            Sprite quitNormal = LoadNamedSprite(ButtonSheet, "quit_selected");
            Sprite quitHover = LoadNamedSprite(ButtonSheet, "quit_hover");

            RectTransform titleRt = CreateBlocker(shellRt, "TitlePanel", 1f, out AnimatedPanel titleAnim, out RectTransform titleContent, startScreenBg);

            // Keep the title controls on one centered axis. The source sheet contains art with
            // slightly different transparent margins, so preserving each sprite's aspect ratio would
            // make the controls appear to have inconsistent widths.
            var logoGo = CreateUIObject(titleContent, "Logo");
            RectTransform logoRt = logoGo.GetComponent<RectTransform>();
            logoRt.anchorMin = logoRt.anchorMax = new Vector2(0.5f, 0.5f);
            logoRt.pivot = new Vector2(0.5f, 0.5f);
            logoRt.anchoredPosition = new Vector2(0f, 300f);
            logoRt.sizeDelta = new Vector2(560f, 260f);
            var logoImg = logoGo.AddComponent<Image>();
            logoImg.sprite = logoNormal;
            logoImg.preserveAspect = true;
            logoImg.raycastTarget = false;
            logoGo.AddComponent<IdlePulse>(); // a hint of life while the title screen sits idle

            var titleBest = CreateSpriteLabel(titleContent, "BestLine", "", 19f,
                new Vector2(0f, 160f), new Vector2(800f, 90f));

            Vector2 buttonSize = new Vector2(460f, 72f);
            Button playEasyButton = CreateArtButton(titleContent, "PlayEasyButton", startNormal, startHover,
                new Vector2(0f, 90f), new Vector2(500f, 78f), interactable: true);
            Button playMediumButton = CreateArtButton(titleContent, "PlayMediumButton", startNormal, startHover,
                Vector2.zero, buttonSize, interactable: true);
            Button playHardButton = CreateArtButton(titleContent, "PlayHardButton", startNormal, startHover,
                new Vector2(0f, -90f), buttonSize, interactable: true);
            CreateSpriteLabel((RectTransform)playEasyButton.transform, "DifficultyLabel", "EASY", 24f,
                new Vector2(340f, 0f), new Vector2(180f, 44f));
            CreateSpriteLabel((RectTransform)playMediumButton.transform, "DifficultyLabel", "MEDIUM", 24f,
                new Vector2(340f, 0f), new Vector2(180f, 44f));
            CreateSpriteLabel((RectTransform)playHardButton.transform, "DifficultyLabel", "HARD", 24f,
                new Vector2(340f, 0f), new Vector2(180f, 44f));
            Button optionsButton = CreateArtButton(titleContent, "OptionsButton", optionsNormal, optionsHover,
                new Vector2(0f, -180f), buttonSize, interactable: true);
            Button quitButton = CreateArtButton(titleContent, "QuitButton", quitNormal, quitHover,
                new Vector2(0f, -270f), buttonSize, interactable: true);
            Button titleLeaderboardButton = CreatePlateButton(titleContent, "TitleLeaderboardButton",
                "LEADERBOARD", new Vector2(0f, -360f));
            ConfigurePanel(titleAnim, titleRt.GetComponent<Image>(), titleContent,
                logoImg, titleBest,
                playEasyButton.GetComponent<Image>(), playMediumButton.GetComponent<Image>(),
                playHardButton.GetComponent<Image>(),
                optionsButton.GetComponent<Image>(), quitButton.GetComponent<Image>(),
                titleLeaderboardButton.GetComponent<Image>());

            // --- Pause --- laid out from the supplied reference: the desk backdrop, a PAUSED ribbon
            // overlapping the top of an ornate frame, and stacked nameplate buttons separated by
            // divider ornaments. Resume sits above Options per an explicit instruction, and the
            // reference's "EXIT" is spelled out as EXIT TO MAIN MENU (it returns to the title, it
            // does not quit - the title screen's own Quit button is what closes the game).
            RectTransform pauseRt = CreateBlocker(shellRt, "PauseMenu", 0.7f, out AnimatedPanel pauseAnim,
                out RectTransform pauseContent, LoadSprite(PauseBackdropPath));

            // Frame height is DERIVED from its contents, not guessed: three plates, two dividers, the
            // gaps between them, a top inset that clears the PAUSED ribbon, and an equal-looking
            // bottom margin. Hand-picked positions in a hand-picked frame left the last button almost
            // touching the bottom border.
            const float plateHeight = 88f;
            const float plateWidth = 520f;
            const float dividerHeight = 26f;
            const float itemGap = 26f;
            const float ribbonInset = 86f; // the ribbon overlaps the top edge and eats this much
            const float bottomInset = 56f;

            float stackHeight = 3f * plateHeight + 2f * dividerHeight + 4f * itemGap;
            float frameHeight = ribbonInset + stackHeight + bottomInset;

            RectTransform pauseFrame = CreateFramedWindow(pauseContent, "Frame",
                new Vector2(700f, frameHeight), new Vector2(0f, -30f), "PAUSED", out SpriteText pauseHeading);

            // Pen walks down the content area; each call returns the next item's centre.
            float pen = frameHeight * 0.5f - ribbonInset;
            float NextY(float height)
            {
                float centre = pen - height * 0.5f;
                pen -= height + itemGap;
                return centre;
            }

            Button resumeButton = CreatePlateButton(pauseFrame, "ResumeButton", "RESUME",
                new Vector2(0f, NextY(plateHeight)), plateWidth);
            AddDivider(pauseFrame, new Vector2(0f, NextY(dividerHeight)));
            Button pauseOptionsButton = CreatePlateButton(pauseFrame, "OptionsButton", "OPTIONS",
                new Vector2(0f, NextY(plateHeight)), plateWidth);
            AddDivider(pauseFrame, new Vector2(0f, NextY(dividerHeight)));
            Button exitToMenuButton = CreatePlateButton(pauseFrame, "ExitToMenuButton", "EXIT TO MAIN MENU",
                new Vector2(0f, NextY(plateHeight)), plateWidth);

            ConfigurePanel(pauseAnim, pauseRt.GetComponent<Image>(), pauseContent, pauseHeading,
                resumeButton.GetComponent<Image>(), pauseOptionsButton.GetComponent<Image>(),
                exitToMenuButton.GetComponent<Image>());

            // --- Options --- same framed-window treatment, three volume rows, restore/back footer.
            optionsPanel = BuildOptionsPanel(shellRt);

            // --- Game over ---
            RectTransform gameOverRt = CreateBlocker(shellRt, "GameOverPanel", 0.85f, out AnimatedPanel gameOverAnim, out RectTransform gameOverContent);
            var gameOverText = CreateSpriteLabel(gameOverContent, "Results", "GAME OVER", 29f,
                new Vector2(0f, 110f), new Vector2(1000f, 340f));
            Button restartButton = CreatePlateButton(gameOverContent, "RestartButton", "PLAY AGAIN", new Vector2(0f, -160f));
            Button gameOverLeaderboardButton = CreatePlateButton(gameOverContent, "GameOverLeaderboardButton",
                "LEADERBOARD", new Vector2(-250f, -280f));
            Button gameOverMainMenuButton = CreatePlateButton(gameOverContent, "GameOverMainMenuButton",
                "MAIN MENU", new Vector2(250f, -280f));
            ConfigurePanel(gameOverAnim, gameOverRt.GetComponent<Image>(), gameOverContent, gameOverText,
                restartButton.GetComponent<Image>(), gameOverLeaderboardButton.GetComponent<Image>(),
                gameOverMainMenuButton.GetComponent<Image>());

            // --- Local leaderboard ---
            RectTransform leaderboardRt = CreateBlocker(shellRt, "LeaderboardPanel", 0.85f,
                out AnimatedPanel leaderboardAnim, out RectTransform leaderboardContent);
            SpriteText leaderboardRows = CreateSpriteLabel(leaderboardContent, "Rows", "LEADERBOARD", 23f,
                new Vector2(0f, 65f), new Vector2(1600f, 680f));
            Button leaderboardBackButton = CreatePlateButton(leaderboardContent, "LeaderboardBackButton",
                "BACK", new Vector2(0f, -350f));
            ConfigurePanel(leaderboardAnim, leaderboardRt.GetComponent<Image>(), leaderboardContent,
                leaderboardRows, leaderboardBackButton.GetComponent<Image>());

            // --- Transition banner. Also a blocker: the board is about to be torn down and rebuilt,
            // so dragging during the transition has nothing valid to act on.
            RectTransform bannerRt = CreateBlocker(shellRt, "Banner", 0.45f, out AnimatedPanel bannerAnim, out RectTransform bannerContent);
            var bannerText = CreateSpriteLabel(bannerContent, "Text", "", 46f, Vector2.zero, new Vector2(1400f, 240f));
            ConfigurePanel(bannerAnim, bannerRt.GetComponent<Image>(), bannerContent, bannerText);

            var shell = shellGo.AddComponent<ShellUI>();
            var so = new SerializedObject(shell);
            so.FindProperty("titlePanel").objectReferenceValue = titleAnim;
            so.FindProperty("pausePanel").objectReferenceValue = pauseAnim;
            so.FindProperty("gameOverPanel").objectReferenceValue = gameOverAnim;
            so.FindProperty("leaderboardPanel").objectReferenceValue = leaderboardAnim;
            so.FindProperty("banner").objectReferenceValue = bannerAnim;
            so.FindProperty("titleBestText").objectReferenceValue = titleBest;
            so.FindProperty("gameOverText").objectReferenceValue = gameOverText;
            so.FindProperty("leaderboardRowsText").objectReferenceValue = leaderboardRows;
            so.FindProperty("bannerText").objectReferenceValue = bannerText;
            so.FindProperty("playEasyButton").objectReferenceValue = playEasyButton;
            so.FindProperty("playMediumButton").objectReferenceValue = playMediumButton;
            so.FindProperty("playHardButton").objectReferenceValue = playHardButton;
            so.FindProperty("resumeButton").objectReferenceValue = resumeButton;
            so.FindProperty("restartButton").objectReferenceValue = restartButton;
            so.FindProperty("titleLeaderboardButton").objectReferenceValue = titleLeaderboardButton;
            so.FindProperty("gameOverLeaderboardButton").objectReferenceValue = gameOverLeaderboardButton;
            so.FindProperty("gameOverMainMenuButton").objectReferenceValue = gameOverMainMenuButton;
            so.FindProperty("leaderboardBackButton").objectReferenceValue = leaderboardBackButton;
            so.FindProperty("pauseButton").objectReferenceValue = pauseButton;
            so.FindProperty("shuffleButton").objectReferenceValue = shuffleButton;
            so.FindProperty("quitButton").objectReferenceValue = quitButton;
            so.FindProperty("titleOptionsButton").objectReferenceValue = optionsButton;
            so.FindProperty("pauseOptionsButton").objectReferenceValue = pauseOptionsButton;
            so.FindProperty("exitToMenuButton").objectReferenceValue = exitToMenuButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            return shell;
        }

        /// <summary>The ornate gold window used by both Pause and Options: a frame with a purple
        /// ribbon banner overlapping its top edge, title lettering on the ribbon. Returns the frame's
        /// rect so callers can parent content into it.</summary>
        private static RectTransform CreateFramedWindow(RectTransform parent, string name, Vector2 size,
            Vector2 anchoredPosition, string title, out SpriteText heading)
        {
            var frameGo = CreateUIObject(parent, name);
            RectTransform frameRt = frameGo.GetComponent<RectTransform>();
            frameRt.anchorMin = frameRt.anchorMax = new Vector2(0.5f, 0.5f);
            frameRt.pivot = new Vector2(0.5f, 0.5f);
            frameRt.anchoredPosition = anchoredPosition;
            frameRt.sizeDelta = size;
            var frameImg = frameGo.AddComponent<Image>();
            frameImg.sprite = LoadNamedSprite(ModalSheet, "modal_frame");
            frameImg.type = Image.Type.Sliced;
            frameImg.raycastTarget = false;

            // Ribbon rides the frame's top edge, half in and half out, as in the reference. Its rect
            // is sized to the sprite's own aspect so there's no letterboxing to reason about, and the
            // three ratios below are measured off the sprite rather than eyeballed: the purple band
            // is only the middle 39% of its height, and that band's centre sits slightly BELOW the
            // sprite's centre. Titles follow the band, not the rect - centring on the rect is what
            // left "PAUSED" riding high.
            const float ribbonAspect = 509f / 171f;
            const float bandHeightRatio = 67f / 171f;
            const float bandOffsetRatio = -4.5f / 171f;

            float ribbonWidth = size.x * 0.52f;
            float ribbonHeight = ribbonWidth / ribbonAspect;

            var ribbonGo = CreateUIObject(frameRt, "Ribbon");
            RectTransform ribbonRt = ribbonGo.GetComponent<RectTransform>();
            ribbonRt.anchorMin = ribbonRt.anchorMax = new Vector2(0.5f, 1f);
            ribbonRt.pivot = new Vector2(0.5f, 0.5f);
            ribbonRt.anchoredPosition = Vector2.zero;
            ribbonRt.sizeDelta = new Vector2(ribbonWidth, ribbonHeight);
            var ribbonImg = ribbonGo.AddComponent<Image>();
            ribbonImg.sprite = LoadNamedSprite(ModalSheet, "modal_banner");
            ribbonImg.preserveAspect = true;
            ribbonImg.raycastTarget = false;

            float bandHeight = ribbonHeight * bandHeightRatio;
            float titleWidth = ribbonWidth * 0.62f; // clears the ribbon's folded ends
            float capHeight = LoadSpriteFont().FitCapHeight(title, titleWidth, bandHeight * 0.66f);

            heading = CreateSpriteLabel(ribbonRt, "Title", title, capHeight,
                new Vector2(0f, ribbonHeight * bandOffsetRatio), new Vector2(titleWidth, bandHeight));

            return frameRt;
        }

        /// <summary>Purple nameplate button with sparkles either side of its label, matching the
        /// reference's OPTIONS / EXIT plates.</summary>
        private static Button CreatePlateButton(RectTransform parent, string name, string label,
            Vector2 anchoredPosition, float width = 440f)
        {
            const float height = 88f;

            var go = CreateUIObject(parent, name);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = new Vector2(width, height);

            var img = go.AddComponent<Image>();
            img.sprite = LoadNamedSprite(PanelSheet, "panel_purple_bar");
            img.type = Image.Type.Sliced;

            var button = go.AddComponent<Button>();
            button.targetGraphic = img;
            button.transition = Selectable.Transition.None;
            go.AddComponent<AnimatedButton>();

            // Long labels (EXIT TO MAIN MENU) shrink only as far as they actually need to, measured
            // against this plate's real width rather than guessed from a character count.
            float labelWidth = width - 130f; // clears the sparkles at both ends
            float capHeight = LoadSpriteFont().FitCapHeight(label, labelWidth, 24f);
            CreateSpriteLabel(rt, "Label", label, capHeight, Vector2.zero, new Vector2(labelWidth, height));

            foreach (float sign in new[] { -1f, 1f })
            {
                var starGo = CreateUIObject(rt, "Sparkle");
                RectTransform starRt = starGo.GetComponent<RectTransform>();
                starRt.anchorMin = starRt.anchorMax = new Vector2(0.5f, 0.5f);
                starRt.pivot = new Vector2(0.5f, 0.5f);
                starRt.anchoredPosition = new Vector2(sign * (width * 0.5f - 46f), 0f);
                starRt.sizeDelta = new Vector2(26f, 26f);
                var starImg = starGo.AddComponent<Image>();
                starImg.sprite = LoadNamedSprite(DecorationsSheet, "deco_star");
                starImg.preserveAspect = true;
                starImg.raycastTarget = false;
            }

            return button;
        }

        private static void AddDivider(RectTransform parent, Vector2 anchoredPosition)
        {
            var go = CreateUIObject(parent, "Divider");
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = new Vector2(300f, 26f);
            var img = go.AddComponent<Image>();
            img.sprite = LoadNamedSprite(DecorationsSheet, "deco_divider");
            img.preserveAspect = true;
            img.raycastTarget = false;
        }

        /// <summary>
        /// Options, from the supplied reference: framed window, OPTIONS ribbon, a close X, three
        /// volume rows (icon, label, slider, readout) separated by dividers, and a
        /// RESTORE DEFAULTS / BACK footer.
        ///
        /// Two deliberate departures from the reference, both because the art does not exist and
        /// plan.md section 0 forbids inventing it:
        ///   - all three rows use the speaker icon (there is no music-note icon in any sheet);
        ///   - readouts are bare numbers, no % sign (no % glyph in either font sheet).
        /// The slider rail/fill/handle sprites are left unassigned pending art - VolumeSlider renders
        /// nothing it lacks art for, so each row is an invisible but working drag region until then.
        /// </summary>
        private static OptionsPanelController BuildOptionsPanel(RectTransform shellRt)
        {
            RectTransform panelRt = CreateBlocker(shellRt, "OptionsPanel", 0.8f,
                out AnimatedPanel anim, out RectTransform content);

            RectTransform frame = CreateFramedWindow(content, "Frame", new Vector2(1020f, 620f),
                new Vector2(0f, -20f), "OPTIONS", out SpriteText heading);

            // Close X, inside the frame's top-right corner.
            var closeGo = CreateUIObject(frame, "CloseButton");
            RectTransform closeRt = closeGo.GetComponent<RectTransform>();
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-34f, -34f);
            closeRt.sizeDelta = new Vector2(56f, 56f);
            var closeImg = closeGo.AddComponent<Image>();
            closeImg.sprite = LoadNamedSprite(HudIconsSheet, "hud_close");
            closeImg.preserveAspect = true;
            var closeButton = closeGo.AddComponent<Button>();
            closeButton.targetGraphic = closeImg;
            closeButton.transition = Selectable.Transition.None;
            closeGo.AddComponent<AnimatedButton>();

            VolumeSlider master = BuildVolumeRow(frame, "Master", "VOLUME", 120f, out SpriteText masterReadout);
            AddDivider(frame, new Vector2(0f, 66f));
            VolumeSlider music = BuildVolumeRow(frame, "Music", "MUSIC VOLUME", 12f, out SpriteText musicReadout);
            AddDivider(frame, new Vector2(0f, -42f));
            VolumeSlider sfx = BuildVolumeRow(frame, "Sfx", "SFX VOLUME", -96f, out SpriteText sfxReadout);

            // No divider between these two - the pause menu stacks its buttons vertically and wants
            // separators; this footer is a side-by-side pair and reads cleaner without one.
            Button restoreButton = CreatePlateButton(frame, "RestoreDefaultsButton", "RESTORE DEFAULTS",
                new Vector2(-250f, -228f), 430f);
            Button backButton = CreatePlateButton(frame, "BackButton", "BACK", new Vector2(250f, -228f), 430f);

            ConfigurePanel(anim, panelRt.GetComponent<Image>(), content, heading,
                restoreButton.GetComponent<Image>(), backButton.GetComponent<Image>());

            // Controller lives on its own always-active object, NOT on the panel - the panel starts
            // inactive and Awake would never run there. See OptionsPanelController's own note.
            var controllerGo = CreateUIObject(shellRt, "OptionsController");
            var controller = controllerGo.AddComponent<OptionsPanelController>();
            var so = new SerializedObject(controller);
            so.FindProperty("panel").objectReferenceValue = anim;
            so.FindProperty("masterSlider").objectReferenceValue = master;
            so.FindProperty("musicSlider").objectReferenceValue = music;
            so.FindProperty("sfxSlider").objectReferenceValue = sfx;
            so.FindProperty("masterReadout").objectReferenceValue = masterReadout;
            so.FindProperty("musicReadout").objectReferenceValue = musicReadout;
            so.FindProperty("sfxReadout").objectReferenceValue = sfxReadout;
            so.FindProperty("restoreDefaultsButton").objectReferenceValue = restoreButton;
            so.FindProperty("backButton").objectReferenceValue = backButton;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            return controller;
        }

        private static VolumeSlider BuildVolumeRow(RectTransform frame, string name, string label,
            float y, out SpriteText readout)
        {
            var rowGo = CreateUIObject(frame, $"Row_{name}");
            RectTransform rowRt = rowGo.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 0.5f);
            rowRt.anchorMax = new Vector2(1f, 0.5f);
            rowRt.pivot = new Vector2(0.5f, 0.5f);
            rowRt.offsetMin = new Vector2(70f, 0f);
            rowRt.offsetMax = new Vector2(-70f, 0f);
            rowRt.sizeDelta = new Vector2(rowRt.sizeDelta.x, 80f);
            rowRt.anchoredPosition = new Vector2(0f, y);

            var iconGo = CreateUIObject(rowRt, "Icon");
            RectTransform iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.anchoredPosition = new Vector2(0f, 0f);
            iconRt.sizeDelta = new Vector2(56f, 56f);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite = LoadNamedSprite(HudIconsSheet, "hud_speaker");
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            var labelText = CreateSpriteLabel(rowRt, "Label", label, 17f, Vector2.zero, new Vector2(260f, 60f),
                TextAnchor.MiddleLeft);
            RectTransform labelRt = (RectTransform)labelText.transform;
            labelRt.anchorMin = labelRt.anchorMax = new Vector2(0f, 0.5f);
            labelRt.pivot = new Vector2(0f, 0.5f);
            labelRt.anchoredPosition = new Vector2(78f, 0f);

            VolumeSlider slider = BuildSlider(rowRt);

            readout = CreateSpriteLabel(rowRt, "Readout", "0", 17f, Vector2.zero, new Vector2(90f, 60f),
                TextAnchor.MiddleRight);
            RectTransform readoutRt = (RectTransform)readout.transform;
            readoutRt.anchorMin = readoutRt.anchorMax = new Vector2(1f, 0.5f);
            readoutRt.pivot = new Vector2(1f, 0.5f);
            readoutRt.anchoredPosition = new Vector2(0f, 0f);

            return slider;
        }

        /// <summary>
        /// Rail and fill are the same sprite stretched across the whole track; the fill is clipped by
        /// value (VolumeSlider) and the rail behind it is dimmed to read as the empty portion, since
        /// the art has no separate empty-track piece. Track height is derived from the rail sprite's
        /// own 10.56:1 aspect at the track's width, so it scales near-uniformly instead of squashing.
        /// </summary>
        private static VolumeSlider BuildSlider(RectTransform rowRt)
        {
            const float leftInset = 350f;
            const float rightInset = 120f;
            const float railAspect = 1309f / 124f;

            // The row stretches to the frame, so work out the track's real width to size the rail.
            float rowWidth = 1020f - 140f; // options frame width minus the row's own insets
            float trackWidth = rowWidth - leftInset - rightInset;
            float trackHeight = trackWidth / railAspect;

            var go = CreateUIObject(rowRt, "Slider");
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(leftInset, 0f);
            rt.offsetMax = new Vector2(-rightInset, 0f);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, trackHeight);

            Sprite railSprite = LoadNamedSprite(SliderSheet, "slider_rail");

            Image rail = AddSliderPart(rt, "Rail", stretch: true);
            rail.sprite = railSprite;
            rail.color = new Color(0.42f, 0.36f, 0.30f, 1f); // dimmed: this is the empty portion

            Image fill = AddSliderPart(rt, "Fill", stretch: true);
            fill.sprite = railSprite;

            Image handle = AddSliderPart(rt, "Handle", stretch: false);
            handle.sprite = LoadNamedSprite(SliderSheet, "slider_handle");

            // Sits last so it takes the pointer, and is the only part with raycastTarget on.
            var dragGo = CreateUIObject(rt, "DragRegion");
            Stretch(dragGo.GetComponent<RectTransform>());
            var drag = dragGo.AddComponent<Image>();
            drag.color = new Color(0f, 0f, 0f, 0f);
            drag.raycastTarget = true;

            var slider = go.AddComponent<VolumeSlider>();
            var so = new SerializedObject(slider);
            so.FindProperty("rail").objectReferenceValue = rail;
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("handle").objectReferenceValue = handle;
            so.FindProperty("dragRegion").objectReferenceValue = drag;
            so.ApplyModifiedPropertiesWithoutUndo();
            return slider;
        }

        private static Image AddSliderPart(RectTransform parent, string name, bool stretch)
        {
            var go = CreateUIObject(parent, name);
            RectTransform rt = go.GetComponent<RectTransform>();
            if (stretch)
            {
                Stretch(rt);
            }
            else
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = name == "Fill" ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(0f, 34f);
            }

            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = false;
            return img;
        }

        /// <summary>A full-screen blocker with an AnimatedPanel already attached and an empty "Content"
        /// child (also full-screen) ready to hold whatever text/buttons the panel needs - callers add
        /// those under <paramref name="content"/>, then finish wiring via <see cref="ConfigurePanel"/>.</summary>
        private static RectTransform CreateBlocker(RectTransform parent, string name, float alpha, out AnimatedPanel panel, out RectTransform content, Sprite backgroundSprite = null)
        {
            var go = CreateUIObject(parent, name);
            RectTransform rt = go.GetComponent<RectTransform>();
            Stretch(rt);
            var img = go.AddComponent<Image>();
            if (backgroundSprite != null)
            {
                // Opaque art backdrop (the title screen's wood-desk scene) instead of a translucent
                // tint over gameplay - AnimatedPanel still fades it in/out via its alpha the same way.
                img.sprite = backgroundSprite;
                img.preserveAspect = false; // art is ~1.777:1, effectively already 16:9 - negligible stretch
                img.color = Color.white;
            }
            else
            {
                img.color = new Color(0.03f, 0.02f, 0.05f, alpha);
            }
            img.raycastTarget = true;

            var contentGo = CreateUIObject(rt, "Content");
            content = contentGo.GetComponent<RectTransform>();
            Stretch(content);

            panel = go.AddComponent<AnimatedPanel>();
            go.SetActive(false);
            return rt;
        }

        /// <summary>A button built from pre-baked art (normal/hover sprites) rather than a flat tintable
        /// color - used for the title-screen menu. <paramref name="interactable"/> false makes it a
        /// visible-but-inert placeholder (dimmed via AnimatedButton's disabled state).</summary>
        private static Button CreateArtButton(RectTransform parent, string name, Sprite normalSprite, Sprite hoverSprite,
            Vector2 anchoredPosition, Vector2 size, bool interactable)
        {
            var go = CreateUIObject(parent, name);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.sprite = normalSprite;
            // Use the same rendered rectangle for normal and hover states so the menu never shifts
            // when the pointer moves over a button.
            img.preserveAspect = false;

            var button = go.AddComponent<Button>();
            button.targetGraphic = img;
            button.transition = Selectable.Transition.None;
            button.interactable = interactable;

            var animated = go.AddComponent<AnimatedButton>();
            var so = new SerializedObject(animated);
            so.FindProperty("targetGraphic").objectReferenceValue = img;
            so.FindProperty("hoverSprite").objectReferenceValue = hoverSprite;
            so.ApplyModifiedPropertiesWithoutUndo();

            return button;
        }

        /// <summary>Callers pass whatever component identifies the element (an Image, a SpriteText);
        /// each gets a CanvasGroup so AnimatedPanel can fade it as a unit regardless of how many
        /// Graphics it is actually made of.</summary>
        private static void ConfigurePanel(AnimatedPanel panel, Image backdrop, RectTransform content, params Component[] staggerChildren)
        {
            var so = new SerializedObject(panel);
            so.FindProperty("backdrop").objectReferenceValue = backdrop;
            so.FindProperty("content").objectReferenceValue = content;
            SerializedProperty arrayProp = so.FindProperty("staggerChildren");
            arrayProp.arraySize = staggerChildren.Length;
            for (int i = 0; i < staggerChildren.Length; i++)
                arrayProp.GetArrayElementAtIndex(i).objectReferenceValue = EnsureCanvasGroup(staggerChildren[i]);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static CanvasGroup EnsureCanvasGroup(Component component)
        {
            if (component == null) return null;
            return component.GetComponent<CanvasGroup>() ?? component.gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>Gold BookBound letterforms. Cap heights below are roughly the old TMP font sizes
        /// times 0.72, since TMP sized by em and this sizes by cap height.</summary>
        private static SpriteText CreateSpriteLabel(RectTransform parent, string name, string content,
            float capHeight, Vector2 anchoredPosition, Vector2 size,
            TextAnchor anchor = TextAnchor.MiddleCenter, Color? color = null)
        {
            var go = CreateUIObject(parent, name);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;

            var text = go.AddComponent<SpriteText>();
            text.Configure(LoadSpriteFont(), capHeight, anchor, color ?? DefaultInk, content);
            return text;
        }

        private static readonly Color DefaultInk = new Color(1f, 0.87f, 0.55f, 1f);
        // Gold ink reads fine on the dark plates everywhere else, but the Genre Guide sits on light
        // parchment now - needs a dark ink instead, same family as the parchment sign art elsewhere.
        private static readonly Color ParchmentInk = new Color(0.22f, 0.14f, 0.08f, 1f);

        private static SpriteFont _spriteFont;

        private static SpriteFont LoadSpriteFont()
        {
            if (_spriteFont != null) return _spriteFont;

            _spriteFont = AssetDatabase.LoadAssetAtPath<SpriteFont>("Assets/Data/Config/BookBoundFont.asset");
            if (_spriteFont == null)
                throw new InvalidOperationException(
                    "Missing BookBoundFont.asset - run 'BookBound > Setup > 9. Create Sprite Font' " +
                    "(or Run All Data Setup) before building the scene.");
            return _spriteFont;
        }

        private static ScreenFader BuildScreenFader(RectTransform parent)
        {
            var go = CreateUIObject(parent, "ScreenFader");
            RectTransform rt = go.GetComponent<RectTransform>();
            Stretch(rt);

            var img = go.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = false;

            var fader = go.AddComponent<ScreenFader>();
            var so = new SerializedObject(fader);
            so.FindProperty("image").objectReferenceValue = img;
            so.ApplyModifiedPropertiesWithoutUndo();

            return fader;
        }

        private static void BuildAudioManager(Transform systems)
        {
            var go = new GameObject("AudioManager");
            go.transform.SetParent(systems);

            var sfx = go.AddComponent<AudioSource>();
            sfx.playOnAwake = false;

            var music = go.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            music.volume = 0.4f;
            music.clip = LoadClip("Assets/Music/library_8bit_loop.wav");

            var manager = go.AddComponent<AudioManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("sfxSource").objectReferenceValue = sfx;
            so.FindProperty("musicSource").objectReferenceValue = music;
            so.FindProperty("pickup").objectReferenceValue = LoadClip("Assets/SFX/BookPickup-SFX.mp3");
            so.FindProperty("placeCorrect").objectReferenceValue = LoadClip("Assets/SFX/placement_correct.wav");
            so.FindProperty("placeNeutral").objectReferenceValue = LoadClip("Assets/SFX/Book Thuds/book_thud_01_balanced.wav");
            so.FindProperty("swap").objectReferenceValue = LoadClip("Assets/SFX/Book Thuds/book_thud_02_light_hardcover.wav");
            so.FindProperty("shelfClear").objectReferenceValue = LoadClip("Assets/SFX/point_gain.wav");
            so.FindProperty("heartLost").objectReferenceValue = LoadClip("Assets/SFX/point_loss.wav");
            so.FindProperty("timerTick").objectReferenceValue = LoadClip("Assets/SFX/ui_hover.wav");
            so.FindProperty("uiClick").objectReferenceValue = LoadClip("Assets/SFX/ui_click.wav");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AudioClip LoadClip(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) throw new InvalidOperationException($"Missing audio clip at '{path}'.");
            return clip;
        }

        private static void CreateSlotHighlight(RectTransform slot)
        {
            var go = CreateUIObject(slot, "Highlight");
            Stretch(go.GetComponent<RectTransform>());
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.enabled = false;
            go.AddComponent<SlotHighlight>();
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static GameObject CreateUIObject(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Sprite LoadSprite(string texturePath)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
            if (sprite == null)
                throw new InvalidOperationException($"Could not load a Sprite at '{texturePath}'. Check the TextureImporter's sprite mode.");
            return sprite;
        }

        /// <summary>Loads one named sub-sprite out of a Sprite Mode = Multiple sheet (e.g. the
        /// title-screen art sliced by SliceStartScreenSprites).</summary>
        private static Sprite LoadNamedSprite(string texturePath, string spriteName)
        {
            var sprite = AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>()
                .FirstOrDefault(s => s.name == spriteName);
            if (sprite == null)
                throw new InvalidOperationException(
                    $"Could not find sub-sprite '{spriteName}' in '{texturePath}'. Run 'BookBound > Setup > 6. Slice Start Screen Button Sprites' first.");
            return sprite;
        }
    }
}
