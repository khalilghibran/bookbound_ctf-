# BookBound — Handoff Notes

This picks up from `plan.md` (the original spec — read that first for the full design). This document covers what's actually been built, key decisions/gotchas discovered along the way that aren't in the plan, and what's left.

## The sprite-font / Options session (2026-08-04) — READ THIS FIRST

The commit before this one (`70e6177 "feat : lmao"`) ran out of budget midway through *"put every
interface letter in the BookBound font, and build an Options screen"*, leaving the font sheet and
three art files on disk but unwired. **That task is now done**, plus the hearts art and the pause
screen. Verified in a real standalone build, screenshotted.

What landed:

1. **A sprite font.** `SpriteFont` (ScriptableObject glyph table) + `SpriteText` (MonoBehaviour that
   lays out pooled glyph Images). **Every alphabetic string in the game now renders as BookBound
   letterforms** — HUD, Genre Guide, controls hints, title, pause, options, game over, banners.
   TMP is gone from the scene entirely. See "The sprite font" below for the details that matter.
2. **Options screen**, from the supplied reference: framed window, OPTIONS ribbon, close ✕, three
   volume rows, RESTORE DEFAULTS / BACK. Reachable from the title *and* the pause menu. Backed by
   `SettingsService` (PlayerPrefs) which `AudioManager` reacts to.
3. **Pause screen** rebuilt from the supplied reference: desk backdrop, PAUSED ribbon over an ornate
   frame, and RESUME / OPTIONS / EXIT TO MAIN MENU plates with divider ornaments.
4. **Hearts are sprites**, not `Lives: N` text. Three of them; a lost heart shakes, swells, fades and
   is gone.
5. `RebuildAndBuild` now runs the slicers (see the fixed gotcha below), and the HUD-art filtering bug
   (old point 20) is fixed.

6. **Volume sliders use the supplied art** (`Assets/UI/Controls/Slider2-BookBound.png`). The sheet
   has one gold rail and one gem handle, and **no empty-track piece**, so the rail is drawn twice:
   once full width dimmed (the empty portion) and once bright on top, clipped by value with
   `Image.Type.Filled` rather than scaled — scaling would drag the rail's rounded end cap inward.
   The handle takes its width from its sprite's aspect so the gem never squashes.
7. **The app icon is set** on all 8 standalone slots by `SetPlayerIcon`, which runs inside
   `RebuildAndBuild`.

Two deliberate departures from the Options reference, both because the art does not exist and
`plan.md` §0 forbids inventing it — confirmed with the user before building:
- **All three rows use the speaker icon.** There is no music-note icon in any of the 8 UI sheets.
- **Readouts are bare numbers, no `%`.** Neither font sheet has a `%` glyph.

## Current state

Phases 0–8 of the plan are complete and confirmed working in a standalone Windows build (rebuilt and
screenshotted 2026-08-04, including the pause and options screens).
- Asset pipeline (all book art, UI art wired up, texture import fixed for crispness)
- Full data layer (`GenreDefinition`, `ShelfGenerator`, `DifficultyCurve`, etc.) — 37 EditMode tests passing
- Scene built entirely via an Editor script (`SceneSetup.cs`), not hand-assembled in the Inspector
- Drag/drop/swap between shelf and table, fully working
- Table is a single-file conveyor (only the leftmost book is draggable; clearing it slides the queue over)
- Score, countdown timer, hearts all live and wired to the HUD
- Run loop: clear a bookcase → time-bonus banner → next bookcase at the next difficulty band; timer
  expiry → heart-loss banner → same bookcase respawns; 0 hearts → game over
- Title / pause / game over shell, restart with no scene reload, PlayerPrefs high score + best bookcase
- Feel pass: hover lift, drop-target highlight (green = place, orange = swap), pickup/place/swap/clear/
  heart-loss SFX, looping music with ducking, timer goes red and ticks under 10s

Landed after the above and **not** part of any numbered plan phase:

- **A full UI animation layer.** `UITween` (the tween runner), `Easing`, `AnimatedButton`
  (hover/press/release scale + sprite swap + disabled dim), `AnimatedPanel` (backdrop fade, pop-in,
  staggered children), `IdlePulse`, `ScreenFader` (state-transition blackout, sits above even a book
  mid-drag). Every value lives on `Assets/Resources/AnimationSettings.asset`;
  `AnimationSettings.enabled = false` snaps everything to its end state, and the game must stay fully
  playable that way. `BookBound > Setup > Debug > Animation Smoke Test` drives it headlessly.
- **Real HUD art, replacing the code-drawn HUD.** Score plaque (purple nameplate + sparkles), clock
  icon + dark pill timer, pause icon — all sliced by `SliceHudOverlaySprites`.
- **`DigitStrip`.** Score and countdown render as sliced bitmap numerals from
  `bookbound_ui_counter_glyphs_spritesheet`, not a font. This also replaced the old timer *fill bar*
  with an `MM:SS` readout (so point 8 below is now history, not current behaviour).
- **The InfoPanel is now the "Genre Guide" and uses real art** — one `genre_bar_*` nameplate per row
  off the genre-signs spritesheet, under a `modal_banner` header. It is no longer the plain
  semi-transparent rectangle `plan.md` §3 describes. Bars are looked up **by genre id, not row
  index**, so `shuffleRowOrder` stays correct.
- **The title screen uses real art**: `StartScreen-Background.png` backdrop, the BOOKBOUND logo lockup
  with an idle pulse, and nameplate buttons from the purple-hover spritesheet
  (`SliceStartScreenSprites`).
- **Controls hint panel** (bottom-left): mouse icons + "LEFT CLICK TO DRAG" / "RELEASE TO DROP".
- **Sprite font, Options, Pause, hearts** — see the section at the top of this file.
- **Spine art was regenerated at the correct aspect ratio** — all four `*_side_spritesheet.png` are now
  2560×474, i.e. ten 256×474 cells at 1 : 1.85, matching the slot ratio. `SliceSideViewSprites` cuts
  them. The old "spines are stretched" problem is **solved**.

- **Four genres, four rows, 40 slots.** A fifth genre (`mystery`) was briefly added and then removed
  again the same day - back to the original four-genre board. See `plan.md` §3's amendment note.
  Mystery's art (cover/spine/side) and `Assets/Art/bookcase_5row.png` are still on disk, unused, in
  case mystery comes back later - nothing in code references them.

**Not built yet:** Phase 9 (balance). `START_SPM`/`DECAY`/`FLOOR_SPM` and the difficulty bands are still
the plan's first-guess numbers — they need real playtests, which is a human job. The bands are
*fractions* of the board, so the 50-slot experiment and the revert back to 40 both passed through them
without edits; at 40 slots a bookcase is back to ~18 required moves at index 0.

## How the scene gets built

**Important:** the scene (`Assets/Scenes/MainScene.unity`) is *generated*, not hand-edited. All layout, wiring, and object creation lives in `Assets/Scripts/Editor/SceneSetup.cs`. If you need to change anchor positions, wiring, or hierarchy, edit that script and re-run it — don't hand-edit the scene in the Inspector, because the next `SceneSetup.BuildMainScene` run will wipe and rebuild it from scratch.

Useful menu items (all under **BookBound > Setup** in the Unity menu bar):
- `1-4`: create/refresh the data assets (genres, layout configs, difficulty curve, shelf geometry)
- `5. Create Animation Settings` — regenerates `Assets/Resources/AnimationSettings.asset`
- `9. Create Sprite Font` — rebuilds `Assets/Data/Config/BookBoundFont.asset` from the sliced letter
  sheet. Must run **after** the slicer; `Run All Data Setup` already orders them correctly.
- `10. Set Player Icon` — points the standalone icon slots at `Assets/Art/Icon-BookBound-Square.png`
- `5. Build Main Scene` — rebuilds `MainScene.unity` from scratch. **Run this after any change to `SceneSetup.cs`, `GameManager.cs`, or any other script that gets wired into the scene.**
- `6. Build Book Prefab` — regenerates `Assets/Prefabs/Book.prefab`
- `6. Slice Start Screen Button Sprites` / `7. Slice Side-View (Spine) Sprites` /
  `8. Slice HUD Overlay Sprites` — re-cut the named sub-sprites out of each spritesheet
- `-1`, `-2`, `-3`: one-time environment fixes (Renderer2D sort axis, sprite import quality, TMP essential resources) — already applied, shouldn't need to run again unless you reimport assets from scratch
- `Debug > ...`: various diagnostic tools used to debug issues during development (screenshot capture, layout dumps, animation smoke test). Safe to ignore/delete once things are stable.
- `BookBound > Rebuild Scene and Build Player` — runs the whole chain in order: fix import settings, regenerate the data assets, rebuild the scene, build the player. Each step is idempotent. **This is the only thing you need to run**, and the entry point the headless build uses; the steps above are just it broken apart for when you want one piece.

One gotcha left in that list:

- **The numbers collide.** `5` and `6` each name two different menu items now (Build Main Scene vs
  Create Animation Settings; Build Book Prefab vs Slice Start Screen Button Sprites). Unity shows
  both, so nothing breaks, but the numbering no longer implies an order. Renumber if you touch them.

*(Previously listed here and now fixed: `RebuildAndBuild` didn't run the slicers, so replacing a
spritesheet's bytes — which wipes its slice data — broke the next build with "could not find
sub-sprite". The chain now runs all three slicers between the import pass and the data pass.)*

## The sprite font

Every letter in the game comes from `Assets/UI/Fonts/BookBound-Fonts.png` via two scripts:

- **`SpriteFont`** (ScriptableObject at `Assets/Data/Config/BookBoundFont.asset`) — a char → sprite
  table plus metrics. Metrics are stored as **multiples of cap height**, not pixels, so one asset
  drives a 12px row label and a 46px banner with no per-size tuning.
- **`SpriteText`** (MonoBehaviour) — pools child Images and lays glyphs out by hand. Explicit `\n`
  works; **there is no word wrap** (every string in this game has authored line breaks).

Things that will bite you:

1. **The sheet is UPPERCASE with no punctuation at all.** `SpriteText` uppercases its input, and any
   character with no glyph advances as a blank. Two strings were reworded to fit rather than render
   with holes: the clear banner says `BONUS n` not `+n`, and the title's best line is two lines
   instead of `BEST n · BOOKCASE n`. **If you add UI copy, it can only use A–Z, 0–9 and spaces.**
2. **The sheet repeats glyphs.** It contains a second I, J, R and 6. The rect table in
   `SliceHudOverlaySprites` deliberately lists only one of each — do not "fix" the missing indices.
3. **Its digits are smaller than its caps** (81px vs 104px), so `ProjectSetup` normalises the two
   classes against different nominals. Without that, digits inline in a sentence come out
   three-quarters the height of the letters beside them.
4. **Vertical alignment measures ink, not line boxes.** A line box is `lineHeight` (1.4) cap heights
   tall but glyphs only fill the cap height at its TOP, so centring the boxes puts every label ~0.2
   cap heights high inside its plate — subtle alone, obvious across a menu. `SpriteFont.Measure`
   returns ink height for the same reason. Two tests pin this.
5. **Plate and ribbon labels auto-fit** via `SpriteFont.FitCapHeight`, which shrinks a label only as
   far as its actual measured width requires. Replaced a `label.Length > 12 ? 18f : 24f` guess.
6. **`SpriteText.text` must stay `[SerializeField]`.** Labels authored once at scene-build time
   (SCORE, GENRE GUIDE, the controls hints) have no runtime `SetText` caller, so a plain private
   field renders them blank **in a player only** — the editor and the tests both looked fine.
   `SpriteTextTests.ConfiguredTextSurvivesSerialization` exists to catch exactly that.
7. **The counter glyphs are a different sheet and still in use.** Score and timer render through
   `DigitStrip` off `bookbound_ui_counter_glyphs_spritesheet` (chunky numerals), by choice. The
   letter sheet's own digits are for numbers inline in a sentence.
8. TMP is no longer used anywhere in the scene. `ImportTmpEssentials` and the TMP package are still
   present but nothing depends on them.

## Building without opening Unity

The whole loop (regenerate scene → compile → build a Windows player) runs headlessly. Unity must not be
open on this project at the same time — it holds a lock on `Library/`.

```bash
"C:/Program Files/Unity/Hub/Editor/6000.4.4f1/Editor/Unity.exe" -batchmode -quit -projectPath "C:/Users/berdz/Documents/Github/IGI_GAMEJAM" -buildTarget Win64 -executeMethod BookBound.EditorTools.BuildStandalone.RebuildAndBuild -logFile build.log
```

Output lands at `Build/BookBound.exe` (~198 MB, gitignored). Tests run the same way:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.4.4f1/Editor/Unity.exe" -batchmode -projectPath "C:/Users/berdz/Documents/Github/IGI_GAMEJAM" -runTests -testPlatform EditMode -testResults results.xml -logFile tests.log
```

The player remembers its window mode between runs (Unity stores it in the registry), so if it opens
fullscreen and you want a window, launch with `-screen-fullscreen 0 -screen-width 1280 -screen-height 720`.

## Art pipeline (`tools/`)

Two standalone Python scripts (PIL + numpy, both already installed). Neither is part of the Unity
build; they produce assets that then get imported normally.

- **`tools/build_bookcase.py`** — regenerates `Assets/Art/bookcase_5row.png` from the original 4-row
  sprite by re-stacking its own slices: cornice, then five copies of (opening + board), then the base.
  Image generators cannot reliably count shelf compartments — the 5-row bookcase was attempted several
  times and kept coming back with six. A bookcase is a repeating structure, so this sidesteps the model
  entirely, guarantees the art style matches because it *is* the same pixels, and prints the exact
  `ShelfGeometry` row rects instead of anyone measuring a PNG by eye. It asserts the five compartments
  are identical rather than trusting its own loop. Keep the original 4-row sprite — it's the input.
- **`tools/stage_new_spines.py`** — turns `side-view/non-ready-assets/<Genre>/<uuid>.png` into
  game-ready sheets in `side-view/ready-assets/`. The generator names its output with UUIDs, so file
  order says nothing about which book is which — the sequence number exists only as pixels on the
  spine's number plate. The `PLATE_ORDER` table in that file was read off the rendered plates by eye
  and is the whole reason the script exists; if a book is regenerated, re-check its entry.

- **`tools/prep_spines.py`** — key, trim, normalise and montage for regenerated book spines.
  Run `python tools/prep_spines.py <folder> <genre>`. It reports each book's measured aspect,
  refuses to rescue one that came out the wrong shape (regenerate it instead of squashing it),
  normalises every book to exactly 256 x 474, and writes a 2560 x 474 ten-cell spritesheet.
  Input that already has an alpha channel is passed through untouched, so it doesn't matter whether
  the generator gives you chroma or transparency.

  **It keys by connectivity, not by hue, and that is not a style preference.** Measuring all five
  genres' art against six candidate key colours, every colour has subject pixels leaning toward it:
  fantasy has pixels 176/255 toward green, science leans magenta, biography leans cyan, history and
  mystery lean green. There is no safe key colour, so per-genre key colours don't solve anything.
  Only background reachable from the image border is removed, which means a green pixel inside a
  book survives a green key. `--selftest` proves it by round-trip: it flattens the real art onto
  deliberately hostile backgrounds, keys it back, and asserts the interior comes back bit-identical.

  The match tolerance is 20, swept rather than guessed. At 20 or below, nothing is erased and
  nothing is left behind, against both a perfectly flat background and one with +-3 noise. At 30 or
  above, stray edge pixels that happen to match the key get eaten. The background only needs to be
  *flat* — if it isn't (a vignette or gradient), the flood stops early and the tool says so, rather
  than letting it surface later as a baffling aspect-ratio complaint.

  This deliberately does NOT live in ChatGPT. Its image tool and its Python sandbox don't reliably
  share a filesystem, so "generate then key then zip" fails in ways that are hard to notice. ChatGPT
  only generates; everything after that happens here where it's deterministic and checkable.

## Architecture decisions worth knowing (not in plan.md)

1. **Canvas is Screen Space - Overlay, not Screen Space - Camera.** The plan originally specified Camera mode. This project's URP setup silently fails to render Screen Space - Camera UI content at all (no errors, just invisible) — confirmed by testing. Overlay works fine and doesn't need a camera reference. If you ever see UI just not rendering with no console errors, check this first.

2. **Everything visual lives in the Canvas, including the background/bookcase/table.** The plan's original architecture kept those as world-space `SpriteRenderer`s behind the canvas, using a runtime component to project world positions into canvas space so the shelf/table UI grids would line up. That approach was fragile across window sizes and got scrapped. Now the bookcase and table are plain UI `Image`s, and the shelf/table grids are children of those images using pure percentage-based anchors — no camera math involved anywhere.

3. **`AspectLock`** (a child of Canvas, first thing under it) locks a true 16:9 region to the screen via `AspectRatioFitter`, and *everything* else is anchored as a fraction of that, not of the raw Canvas. The Canvas's own pixel dimensions vary with actual window aspect ratio, so anything anchored directly to Canvas only lined up correctly at exactly 16:9. This was the fix for shelf/table misalignment at different window sizes.

4. **Book prefab image settings differ by context.** Cover art (table) uses `preserveAspect = true`. Spine art (shelved) uses `preserveAspect = false` — it stretches to exactly fill its slot so adjacent books touch with no gap (shelved books should look packed, not like separated icons). The spine art was regenerated to match: every `*_side_spritesheet.png` is 2560×474, ten 256×474 cells at 1 : 1.85, against slots averaging 1 : 1.89. The stretch is now negligible. If spine art is ever redrawn, keep targeting that ratio — see the table below.

   | Row | Genre | Slot size @ 1920×1080 | Aspect (w:h) |
   |---|---|---|---|
   | 1 | Fantasy | 61 × 110 px | 1 : 1.80 |
   | 2 | Science | 61 × 118 px | 1 : 1.93 |
   | 3 | History | 61 × 120 px | 1 : 1.96 |
   | 4 | Biography | 61 × 115 px | 1 : 1.88 |

   Designing to the 1:1.89 average and letting Unity scale it is fine — don't need 4 separate ratios.

5. **Texture import settings matter a lot here.** All book/UI art is pixel art; Unity's defaults (Bilinear filtering, Compressed, and — critically — a 2048px max size cap) were silently blurring and downscaling it. The genre spritesheets are 5120px wide natively. Fixed via `FixSpriteImportQuality.cs` (Point filter, Uncompressed, 8192 max size, and clearing a per-platform "Standalone" override that separately pinned 2048). If new art gets added, either run that tool again or make sure new imports match: **Point filter, Uncompressed, no per-platform max-size override below 8192.**

6. **If a standalone build fails with "script class layout is incompatible between the editor and the player,"** don't waste time on targeted cache clearing (`Library/Bee`, `Library/PlayerDataCache`) — it doesn't help. Delete the entire `Library/` folder and let Unity do a full clean reimport (~1 min), then rebuild. This happened once after changing a MonoBehaviour's serialized fields and only a full wipe fixed it.

7. **Hearts are three sprite Images** built by `SceneSetup.BuildHearts`, driven by `HUDController`.
   `Assets/Art/Hearts_transparent.png` (a single 368x304 keyed heart) is the only heart art there is -
   **there is no empty-heart sprite**, so a lost heart shakes, swells, fades and is hidden outright
   rather than dimmed in place. `LivesService.StartingHearts` decides how many get built.

8. **`Image.Type.Filled` silently does nothing without a sprite.** *(Historical — the timer is an
   `MM:SS` `DigitStrip` now, not a bar. Kept because the trap is general.)* The old timer bar was a
   sprite-less colored `Image` with `type = Filled`, and Unity's `Image.OnPopulateMesh` bails out to a
   plain full-rect quad when `sprite == null` — so `fillAmount` was ignored and the bar always read as
   100% full. The fix was assigning the builtin `UI/Skin/UISprite.psd`. Same trap applies to any
   future radial/filled bar.

9. **Phase 7's `JuiceController` was deliberately not created, and still doesn't exist.** The plan
   scopes it to screen shake, particles, and hit-stop, none of which this build does. What it actually
   needs — hover lift, drop highlight, timer pulse, SFX — is four small pieces that each live where
   they belong (`BookView`, `SlotHighlight`, `HUDController`, `AudioManager`). The animation layer that
   landed later (point 17) is a *tween runner plus a settings asset*, not a juice controller: nothing
   routes feedback through a central object. If real screen shake/particles get added, that's the point
   to introduce one.

10. **Real UI art gets wired by hand-measuring rects, not by auto-slicing.** Unity's automatic slicer
    produces unnamed, index-numbered sub-sprites that merge neighbours and split single elements
    (`BookBound-Fonts.png`'s 37 junk rects are the current example of that going wrong). Every sheet
    that is actually wired in got there via a hand-measured `(name, x, y, w, h)` table in an editor
    script — `SliceStartScreenSprites`, `SliceHudOverlaySprites`, `SliceSideViewSprites`. **Copy that
    pattern for any new sheet.** Note those three use `FilterMode.Bilinear`, not the Point filtering
    `FixSpriteImportQuality` applies to the book art: the UI sheets are painterly AI-rendered art with
    soft gradients, and Point filtering makes them look chunky rather than crisp.

    Status per surface: **title, HUD, Genre Guide, pause and options all use real art.** Only the
    game-over panel and the transition banner are still a plain dark rectangle behind sprite-font
    text. `CreateFramedWindow` + `CreatePlateButton` in `SceneSetup` are the reusable pieces if you
    want to give those two the same treatment.

11. **`AudioManager` and `SaveService` are reached statically.** Books are `Instantiate`d at runtime from
    a prefab, so they can't be Inspector-wired to a scene object; `AudioManager.Instance` follows the
    `DragLayerMarker.Instance` pattern already in the codebase. Every audio call is null-safe, so
    deleting the AudioManager leaves the game fully playable in silence.

12. **`GameManager.seed` defaults to 0, which means "reseed randomly each run."** Set it to any nonzero
    value in the Inspector to make a whole run reproducible for debugging.

13. **There is a visible PAUSE button, not just Esc.** Esc is wired through
    `Keyboard.current.escapeKey` and is in the build, but it could not be confirmed through scripted
    input injection during development — the button is the path that's actually verified. If you test
    Esc by hand and it works, the button is still worth keeping (plan.md section 11 asks for both).

14. **Row count lives in exactly one constant.** `ShelfGeometry.RowCount` drives the board, the
    generator, the InfoPanel line count, the genre array length and the EditMode tests. Adding or
    removing a genre means changing that number, adding the id to `SceneSetup.genreIds` and to
    `ProjectSetup.Genres`, and adding row rects — nothing else. The tests derive their genre count
    from it too, so they don't need rewriting.

15. **The scene uses the original 4-row bookcase sprite again.** `bookcase_5row.png` (built by
    `tools/build_bookcase.py` for the now-reverted 5-genre experiment) is still in the repo but
    unreferenced - kept in case a 5th genre comes back later.

16. **The game doesn't use `System.DateTime`-based RNG or Unity's global `Random`** — `ShelfGenerator` takes an explicit `int` seed and uses `System.Random` internally, fully deterministic and unit-testable. `GameManager` currently reseeds each new bookcase from a `System.Random` seeded once at `Start()`.

17. **The animation layer has one master switch and it must keep working.**
    `AnimationSettings.Instance.enabled == false` has to leave the game fully playable — every animated
    component checks it and snaps to the end state rather than tweening (`HUDController.UpdateScore`
    is the clearest example). The asset lives at `Assets/Resources/AnimationSettings.asset` and is
    loaded by path, so it must stay under a `Resources/` folder. Tweens run on
    `Time.unscaledDeltaTime` throughout, so they keep working while the game is paused.

18. **Genre Guide bars are looked up by genre id, never by row index.** `shuffleRowOrder` (bookcase
    14+) reassigns genres to rows every bookcase, so `InfoPanelController.SetRowGenres` re-resolves
    each row's bar sprite through its `genreBars` id→sprite table. Indexing by row would silently make
    the guide lie, and per `plan.md` §3 a lying guide makes the game unwinnable. Same reasoning as the
    original code-drawn panel — the art swap didn't change the rule.

19. **Point vs Bilinear is decided per sheet, and the two rules fight if you're careless.**
    `FixSpriteImportQuality` forces Point + Uncompressed on the pixel-art sheets. The painterly
    AI-rendered sheets (title screen, and everything `SliceHudOverlaySprites` owns, including the
    letter sheet) want Bilinear and are **excluded from that sweep** - the exclusion list is derived
    from `SliceHudOverlaySprites.Sheets`, so adding a sheet there is enough and the two can't drift.

    *This used to be a live bug:* `RebuildAndBuild` ran the Point sweep over all of `Assets/UI` and
    never re-ran the slicer, so every standard build shipped a chunky-looking score plaque, clock,
    pause icon and Genre Guide. Fixed 2026-08-04.

    `Hearts_transparent.png` is chunky pixel art and is in the Point list. `PausedInterface-
    Background.png` is painterly and is deliberately not.

20. **Framed windows size themselves to their contents.** `SceneSetup`'s pause menu derives its
    frame height from plate heights + divider heights + gaps + a top inset that clears the ribbon +
    a bottom margin, rather than hard-coding a frame size and hand-placing items in it. The first
    version did the latter and left EXIT TO MAIN MENU 16px off the bottom border. `CreateFramedWindow`
    likewise derives the ribbon size from the sprite's aspect and puts the title on the ribbon's
    purple BAND, which is only the middle 39% of the sprite's height and sits slightly below its
    centre - centring on the rect leaves the title riding high.

21. **The app icon is a pre-squared copy, not the source PNG.** Unity scales whatever it is given
    into square icon slots, so `Icon-BookBound.png` (1536x1024, art floating inside it) would come out
    squashed and ringed with dead space. `Icon-BookBound-Square.png` is that file trimmed to its alpha
    bounding box and padded to 737x737 - one-off asset prep in the spirit of `tools/prep_spines.py`.
    **Keep the original**; it is the input if the crop needs redoing. Note the source has stray 1px
    anti-aliasing specks near the canvas edges, so a naive `Image.getbbox()` returns almost the whole
    canvas - the crop thresholds alpha at 128 and ignores rows/columns with fewer than 8 opaque
    pixels. The same specks are why the slider rects were measured that way.

22. **Two sprites carry 9-slice borders** (`modal_frame`, `panel_purple_bar`), set from the `Borders`
    table in `SliceHudOverlaySprites`. They get stretched to arbitrary window and button sizes, and
    `Image.Type.Sliced` silently degrades to a plain stretch when the border is zero - which smears
    the gold corner ornaments. Borders were measured by scanning in from each edge to the flat
    interior fill. Any new sprite you intend to stretch needs an entry there too.

## Known open items / not-yet-decided things

- **Genre label art is now wired in — as a corner panel, not as row labels.** The Genre Guide uses the
  real `genre_bar_*` art from `UI/Genres/bookbound_ui_genre_signs_spritesheet.png`, so the "code-drawn
  placeholder" `plan.md` §3 describes is gone. But `plan.md` §15.3's actual complaint stands: the
  labels still sit in the bottom-right corner, forcing a glance away from the board on every uncertain
  book, when they should sit under each row. The `GenreLabel` node per row is still created and left
  inactive in `SceneSetup`, and `GenreDefinition.labelSprite` is still null, so that move is still cheap.
- **`front-view/` art is still unused.** A "front-view" (flat frontal, non-angled) cover style exists alongside the sleeping-view covers the game actually uses. Left wired out on purpose — not a bug.
- **`mystery`'s art (cover/spine/side) is unused again** after the 5-genre experiment was reverted the same day it landed. Left on disk on purpose, not a bug - see `plan.md` §3.
- **Table book size/position and shelf row boundaries were tuned by eye against user feedback**, not derived from a fresh pixel measurement of the final art. If the art changes, these will likely need re-tuning (`BookcaseAnchorMin/Max`, `TableAnchorMin/Max`, and the per-row rects in `ShelfGeometry.asset` in `SceneSetup.cs`/`ProjectSetup.cs`).
- **No mipmaps, no compression** on any game texture (see point 5) — fine for this project's texture budget, but worth knowing if art assets grow substantially.

## What's left

**Phase 9 — Balance.** Tune `START_SPM`/`DECAY`/`FLOOR_SPM` (on `Assets/Data/Config/DifficultyCurve.asset`)
and the already-correct/misplaced bands (`Band_*.asset`, generated by `ProjectSetup.CreateShelfLayoutConfigs`)
against real playtests. Target per plan.md §12: an average run ends around bookcase 8–12, a strong player
reaches 18+. `FLOOR_SPM = 1.4s` is still an unverified guess — if runs die well before bookcase 14, raise
that first. **This is the only remaining plan phase, and it is a human job.**

**Nice-to-haves, not blockers:**
- Game over and the transition banner are still a plain dark rectangle behind sprite-font text, while
  everything else is on real art. `CreateFramedWindow` + `CreatePlateButton` are the reusable pieces.
- Genre labels still sit in the bottom-right corner rather than under each row (plan.md §15.3).
- No bookcase dissipate animation — the board is swapped instantly under the clear banner.
- `Assets/Art/Hearts.png` (the full 1536×1024 sheet) is still unused; the trimmed
  `Hearts_transparent.png` is what the HUD uses.
- No empty-heart art exists, which is why a lost heart vanishes rather than greying out.

## Testing workflow

The Editor's Game view can't be trusted for this project — Screen Space Overlay UI doesn't render
correctly in scripted Game-view capture, and its preview scaling has produced false "blurry sprite"
reports during development. Trust a real standalone build over anything seen in the Editor.

**A real standalone build CAN be screenshotted, though**, which turned out to be the missing piece for
verifying layout/art changes without a human looking at a window: `Assets/Scripts/Runtime/Debugging/
AutoScreenshot.cs` is a MonoBehaviour that, only when the player is launched with a `-bbscreenshot`
command-line argument, waits for the title screen, calls `ScreenCapture.CaptureScreenshot`, clicks Play
via reflection on `ShellUI`, waits again, and screenshots gameplay, then quits. It's harmless to leave in
- normal launches without the flag do nothing. One catch: `ScreenCapture.CaptureScreenshot`'s relative
path resolves against `Contents/Resources/Data/` inside the `.app`, not the process's working directory
- look for the PNGs there.

The built player can be driven from a script for smoke tests: `PrintWindow` with `PW_RENDERFULLCONTENT`
captures the window even when it isn't frontmost, and `AttachThreadInput` + `SetForegroundWindow` is what
it takes to get focus (plain `SetForegroundWindow` is refused). Without focus the player accepts no input.
Click positions should be computed as fractions of `GetClientRect`, not fixed pixels — the player restores
whatever window mode it was last in.
