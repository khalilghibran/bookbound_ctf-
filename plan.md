# BookBound — Full Specification

Engine: **Unity 2D · C#** · Mouse + touch
Genre: timed sorting puzzle / arcade high-score chase
Session length: 3–10 minutes per run

Use **BookBound** as the title everywhere it appears: window title, `PlayerSettings.productName`, title screen, and any UI text.

---

## 0. Instructions for the Implementer

**All art, audio, and UI assets already exist in the project.** Do not generate, draw, synthesize, or download any sprites, textures, fonts, icons, or sound files. Do not create placeholder art. Do not substitute Unity primitives or solid-color `Image` components for missing art.

**There is exactly one exception: the Information panel (§3).** It is explicitly a temporary code-drawn placeholder, built from a plain rectangle and text, and will be replaced with real art later. Build it as specified and do not extend this exception to anything else.

Before writing any gameplay code:

1. Inventory `Assets/` — list every sprite, sprite sheet, font, and audio clip with its path.
2. Map what exists onto the asset contract in §10. Report the mapping.
3. Measure the shelf sprite to derive row and slot geometry (§3).
4. **If a required asset has no clear match, stop and ask.** Do not invent a substitute.

Everything in this document is code, data, and wiring. Art decisions are already made.

---

## 1. Concept

You are a librarian at closing time. A bookcase sits in front of you, already partly stocked — and some of those books are shelved **wrong**. More returned books arrive on your desk below.

Your job: get every book into its correct slot before the clock runs out. That means shelving new books from the table *and* fixing the mess already on the shelf. Clear the whole bookcase and a fresh one takes its place, with less time on the clock. Survive on 3 hearts.

**Core loop:**

```
Bookcase spawns — some slots prefilled (some correct, some misplaced),
remaining books deal onto the table
  → player drags books: table → slot, slot → slot, slot → table
  → each slot that becomes correct for the first time scores
  → every slot simultaneously correct
      → timer stops, time bonus awarded
      → bookcase dissipates, new one appears, timer resets shorter
  → repeat

Timer reaches 0 → lose a heart → bookcase resets, run continues
0 hearts → game over → show score + best
```

**The central design point:** there is **no penalty for putting a book in the wrong place.** Rearranging is how you solve the puzzle, not a mistake. The only pressure is the clock.

---

## 2. Screen Layout

Fixed camera. Target 16:9, 1920×1080 reference resolution. No portrait support.

```
┌────────────────────────────────────────────────────────┐
│  HUD:  ♥♥♥          SCORE 12,450          [▓▓▓▓░░░]    │
├────────────────────────────────────────────────────────┤
│ side   ┌──────────────────────────────────────┐  side  │
│ shelf  │ [1][ ][3][█][ ][█][7][ ][ ][█]       │  shelf │
│(decor) │  ──────────── FANTASY ────────────   │ (decor)│
│        │ [█][2][ ][ ][█][ ][ ][8][9][ ]       │        │
│        │  ──────────── SCIENCE ────────────   │        │  ← 4 rows
│        │ [ ][█][ ][4][ ][6][ ][ ][█][10]      │        │    × 10 slots
│        │  ──────────── HISTORY ────────────   │        │    = 40 books
│        │ [█][ ][ ][█][5][ ][█][ ][ ][ ]       │        │
│        │  ─────────── BIOGRAPHY ───────────   │        │
│        └──────────────────────────────────────┘        │
├────────────────────────────────────────────────────────┤
│ ▓▓ TABLE ▓▓  [bk][bk][bk][bk][bk] ←┐   ┌──────────────┐│
│                                     │   │ INFORMATION  ││
│                          books enter┘   │ Row 1: Fantasy│
│                                         │ Row 2: Science│
│                                         │ Row 3: History│
│                                         │ Row 4: Biogr. ││
└─────────────────────────────────────────└──────────────┘┘

  [n] = correctly shelved   [█] = misplaced   [ ] = empty
```

**Bookcase** — one fixed sprite, center. Genre label sits **below** each row. Slots are invisible rect regions laid out over the sprite's shelf boards.

**Table** — bottom strip. Books lie flat, showing cover art. Up to `maxOnTable` (default 5) at fixed anchor positions. New books slide in from off-screen right when a position frees up.

**Information panel** — bottom-right corner. Temporary placeholder UI, see §3.

**Side shelves, ivy, wall, paneling** — decoration only. No colliders, never raycast targets.

> **Layout conflict to resolve in Phase 1:** the Information panel occupies the bottom-right, which is also where table books feed in from off-screen right. Shift the table anchors left so the rightmost book never sits under or behind the panel, and move the off-screen spawn point to just outside the panel's left edge. Books must remain fully visible as they slide in. If the two cannot coexist cleanly at 16:9, park the panel just above the table on the right instead and report the change.

---

## 3. Bookcase Geometry

> **AMENDMENT (2026-08-04, reverted 2026-08-04):** a fifth genre, `mystery`, was briefly added
> (5 rows × 10 slots = 50 books) and then removed again the same day - the game is staying at four
> genres. `Assets/Art/bookcase_5row.png` and mystery's cover/spine/side art are left on disk, unused,
> in case they're wanted again later, but nothing in code references them.

**The board is fixed at one row per genre × 10 slots.**

This is not a measured value or a tunable — it is a hard structural fact of the game:

- **One row per genre**, so the row count and the genre count are the same number by construction.
- **10 slots per row** because each genre has exactly 10 numbered books.

Therefore **every row always holds sequence numbers 1 through 10, in order, left to right.** Slot index `j` (0-based) always expects sequence number `j + 1`. There are no partial rows, no sequence windows, and no variable board sizes. A completed bookcase contains every book in the game exactly once.

Phase 0 measures the bookcase sprite only to derive **pixel geometry**, not counts:

```
Rect[]  rowRects     // 4 entries, sprite-local coordinates
Vector2 slotSize     // rowRect.width / 10, by rowRect.height
```

Slot rects are generated by dividing each row rect into 10 equal columns. If the sprite does not have 4 usable shelf boards, or 10 books cannot fit legibly across one board, **stop and report** rather than adjusting the counts.

Genre labels render below each row.

**Row order is fixed by default:** Fantasy, Science, History, Biography, top to bottom. `shuffleRowOrder` can randomize the genre→row assignment per bookcase as a late-game option.

### Information Panel — TEMPORARY PLACEHOLDER

Proper genre label art does not exist yet and will be added later. Until it does, the player learns which row is which from a small panel in the **bottom-right corner**:

```
┌─────────────────────┐
│    INFORMATION      │
│                     │
│  Row 1 : Fantasy    │
│  Row 2 : Science    │
│  Row 3 : History    │
│  Row 4 : Biography  │
└─────────────────────┘
```

Build it as a `RectTransform` with a plain semi-transparent dark rectangle (`Image`, no sprite) and text in the project's existing font. Header reads `INFORMATION`. Four lines below it, `Row N : Genre`, top row first.

Requirements:

- **Rows are numbered 1–4 top to bottom**, matching the bookcase visually. Row 1 is the top board.
- **The panel is generated from live game state, never hardcoded.** When `shuffleRowOrder` is enabled (bookcase 14+), the genre→row assignment changes every bookcase, and the panel must update to match. A stale panel here makes the game unwinnable.
- Rebuild the panel contents whenever a new bookcase spawns.
- **Optional but cheap:** tint each genre's name with that genre's `accentColor`, so the panel doubles as a color key.
- Never a raycast target. It must never intercept a drag. Set `raycastTarget = false` on every element.

**Isolate it.** Put it in its own `InfoPanel` prefab driven by a single `InfoPanelController` script, with no other system reaching into it. When the real art arrives, swapping it out should mean replacing one prefab and nothing else.

---

## 4. Data Model

### GenreDefinition (ScriptableObject) — exactly four assets

```
string    id             // "fantasy" | "science" | "history" | "biography"
string    displayName    // "Fantasy" — shown in the Information panel
Color     accentColor    // read from existing art; highlights + Information panel tint
Sprite    labelSprite    // OPTIONAL, leave null — row label art does not exist yet
Sprite[]  coverSprites   // index 0..9 → sequence numbers 1..10, flat/table view
Sprite[]  spineSprites   // index 0..9 → sequence numbers 1..10, shelved view
```

`labelSprite` is reserved for when real row-label art is made. Until then it stays null and the Information panel (§3) does the job. Code must handle a null `labelSprite` without erroring.

**Genre roster — fixed at four. There are no unlockable genres.**

| id | Display name | Leather |
|---|---|---|
| `fantasy` | Fantasy | plum |
| `science` | Science | forest green |
| `history` | History | navy |
| `biography` | Biography | rust |

Each genre has **10 numbered books** (sequence 1–10). Numbers are **baked into the sprite art**, so sequence numbers are hard-capped at 1–10 and must never be rendered as runtime text.

### BookData (serializable class)

```
GenreDefinition genre
int             sequenceNumber   // 1..10
```

Value equality on `(genre.id, sequenceNumber)`. Sprite lookup: `genre.coverSprites[sequenceNumber - 1]`.

### ShelfLayoutConfig (ScriptableObject)

The board is always 40 slots, so difficulty is expressed as **how much of it is already solved at spawn.**

```
float alreadyCorrectMin   // fraction of the 40 slots that spawn correctly filled
float alreadyCorrectMax
float misplacedMin        // fraction of slots holding a WRONG book at spawn
float misplacedMax
bool  shuffleRowOrder
```

The remainder — `1 − alreadyCorrect − misplaced` — spawns as empty slots, and exactly that many books are dealt to the table.

**Required moves** is the real difficulty measure and drives the timer (§8):

```
requiredMoves = totalSlots − (slots that spawn already correct)     // totalSlots = 40
```

Both a misplaced book and an empty slot cost roughly one move, so this is a fair proxy for workload. Note that a *higher* misplaced ratio means *more* work, not less — misplaced books must be pulled out as well as re-shelved.

### DifficultyCurve (ScriptableObject)

```
ShelfLayoutConfig GetLayoutConfig(int shelfIndex)
float             GetSecondsPerMove(int shelfIndex)
float             GetShelfDuration(int shelfIndex, int requiredMoves)
```

Duration depends on the generated board, not just the index — see §8. Pure, deterministic, no Unity API calls beyond being a `ScriptableObject`.

---

## 5. Book Visual States

Every book has three states. All three use existing art.

| State | Visual |
|---|---|
| **On the table** | Flat/laying **cover sprite**. Genre color and sequence number both clearly readable *before* the player picks it up. |
| **Being dragged** | Same cover sprite at reduced alpha (default `0.65`) with a slight scale-up, so it reads as lifted and lets the player see the slot underneath. |
| **In a shelf slot** | **Spine sprite**, upright. Swap happens the instant it lands in a slot. |

Dragging a book *out of* a slot flips it back to the cover sprite immediately on grab.

---

## 6. Placement Rules

A book is **correct** in a slot iff both hold:

1. `slot.rowGenre.id == book.genre.id`
2. `slot.expectedSequenceNumber == book.sequenceNumber`

### Every drop is legal. There are no penalties.

| Drop target | Result |
|---|---|
| Empty slot | Book is placed. If it's correct and that slot hasn't scored yet this bookcase, award points. |
| **Occupied slot** | **Swap.** The two books exchange places. Both are re-evaluated for correctness. |
| Empty table position | Book returns to the table as a cover sprite. |
| Occupied table position, decor, HUD, off-screen | Book returns to where it came from. Nothing happens. |

**All books are always draggable**, including correctly placed ones. Nothing locks. This is required — swapping a correct book out of the way may be the only route to solving a tight bookcase.

**Swap-on-occupied guarantees the puzzle is always solvable**, even when the bookcase is completely full with no empty slot and no table space. Do not implement occupied slots as rejected drops; that can hard-deadlock a run.

Books may move freely in all directions: table→slot, slot→slot, slot→table.

### Win condition

The bookcase completes the moment **every slot holds its correct book, simultaneously**. Check after every placement and every swap. The table will necessarily be empty at that point, since the number of books dealt exactly equals the number of slots.

---

## 7. Scoring

There is no negative scoring anywhere in the game. Score never decreases.

| Event | Score |
|---|---|
| A slot becomes correct **for the first time this bookcase** | `+100` |
| A slot becomes correct again after being disturbed | `0` |
| Any wrong placement, swap, or removal | `0` |
| Bookcase cleared | `+ timeBonus` |

**Why the first-time-only rule:** with no penalties and free dragging, a player could otherwise pull a correct book out and re-place it forever to farm points. Each `ShelfSlot` carries a `hasScored` bool, set true on its first correct fill and reset only when a new bookcase spawns.

**Time bonus** — awarded once per cleared bookcase, and the main source of score:

```
timeBonus = round( 500 × (timeRemaining / shelfDuration) × (1 + shelfIndex × 0.1) )
```

Examples: clearing bookcase 5 with 80% of the clock left → `500 × 0.8 × 1.5 = 600`. With 5% left → `~38`. The spread is deliberate: speed is what the player is chasing, not mere completion.

---

## 8. Timer, Hearts, Difficulty

### Shelf duration — budgeted per move, not per bookcase

A flat countdown does not work on a 40-slot board. A bookcase that spawns 50% solved and one that spawns 5% solved are wildly different amounts of work, and giving them the same clock makes difficulty lurch unpredictably.

So the timer is derived from the actual workload:

```
secondsPerMove(n) = max(FLOOR_SPM, START_SPM × pow(DECAY, n))
shelfDuration(n)  = ceil(requiredMoves × secondsPerMove(n))

START_SPM = 3.0f     // seconds allowed per required move, bookcase 0
DECAY     = 0.93f
FLOOR_SPM = 1.4f     // hard floor — below this the game is not humanly playable
```

`secondsPerMove` is the single knob that controls pressure. The board can grow more scrambled *and* the clock can tighten, without the two compounding into impossibility.

| Bookcase | Already correct | Required moves | s/move | Duration |
|---|---|---|---|---|
| 0 | 55% | 18 | 3.00 | 54s |
| 3 | 45% | 22 | 2.42 | 54s |
| 6 | 35% | 26 | 1.95 | 51s |
| 10 | 25% | 30 | 1.45 | 44s |
| 14 | 15% | 34 | 1.40 (floor) | 48s |
| 18+ | 10% | 36 | 1.40 (floor) | 51s |

Duration stays in a fairly narrow band while the work per second climbs steadily — which is the sensation you want. The player doesn't notice the clock shrinking; they notice the board getting worse and their hands needing to move faster.

All constants live on `DifficultyCurve`, Inspector-tunable without recompiling.

> **Balance warning:** 1.4 s/move is aggressive for a drag-and-drop game. If playtesting shows players topping out well before bookcase 14, raise `FLOOR_SPM` before touching anything else.

The timer **pauses** during bookcase-clear transitions, heart-loss transitions, and the pause menu. It never runs while the player cannot act.

### Hearts

Start at 3. Timer reaching 0 costs **one heart** — the only way to lose one.

On heart loss: play the feedback, clear the bookcase out, spawn a new bookcase at the **same** `shelfIndex` (no advance, no rollback) with a fresh full timer. Score is retained.

At 0 hearts: Game Over.

### Difficulty ramp

Board size (4×10) and genre count (4) are fixed, so difficulty scales through **how solved the board starts, how scrambled the rest is, and seconds per move.**

| Bookcase | Already correct | Misplaced | Empty | Other |
|---|---|---|---|---|
| 0–2 | 50–60% | 10–20% | remainder | gentle intro |
| 3–5 | 40–50% | 20–30% | remainder | — |
| 6–9 | 30–40% | 30–45% | remainder | — |
| 10–13 | 20–30% | 40–55% | remainder | — |
| 14+ | 10–20% | 55–70% | remainder | `shuffleRowOrder = true` |

Within each band the exact values are **rolled randomly per bookcase**, so no two feel identical.

Note the shift in character across the ramp: early bookcases are mostly *filling empty slots* from the table, late bookcases are mostly *untangling a scrambled shelf*. The second is harder per move — it requires reading the board rather than reading one book — which is a difficulty ramp on top of the numbers.

Books dealt to the table are always randomized — never pre-sorted, never grouped by genre.

### Generation must guarantee

- The multiset of all 40 books (prefilled + dealt) is **exactly** one of each `(genre, 1..10)` pair. Perfect bijection with the board, always.
- Every "misplaced" prefilled book sits in a slot that is not its correct one.
- `dealtBookCount == 40 − prefilledCount`, and dealt books are exactly those not prefilled.
- At least one slot is empty **or** the swap rule guarantees solvability (§6). Both hold, so no deadlock is possible in any generated state.
- Generation is seeded and reproducible for debugging.

---

## 9. Technical Architecture

### Rendering approach

**Build the interactive layer in Unity UI (`Canvas`, render mode `Screen Space - Camera`).**

Rationale: drag-and-drop is the entire game. Unity's `EventSystem` gives pointer capture, correct z-ordering during drag, raycast-based drop target resolution, and mouse/touch parity for free. Doing this with `Collider2D` + `Physics2D.OverlapPoint` means hand-rolling all of it.

The bookcase sprite, background wall, paneling, side shelves, ivy, and table surface stay as `SpriteRenderer`s behind the canvas. Only books, slot regions, labels, and HUD live on the canvas.

`CanvasScaler`: **Scale With Screen Size**, reference 1920×1080, match 0.5.

### Scene hierarchy

```
MainScene
├── Main Camera                     (orthographic)
├── Background                      (SpriteRenderers, "Background" sorting layer)
│   └── Wall, Paneling, SideShelf_L, SideShelf_R, Ivy, TableSurface, BookcaseSprite
├── Canvas  [Screen Space - Camera]
│   ├── ShelfRoot
│   │   └── ShelfInstance           (prefab, spawned & destroyed per bookcase)
│   │       ├── Row_0..3
│   │       │   ├── GenreLabel      (inactive — labelSprite art not made yet)
│   │       │   └── Slot_0..9       (ShelfSlot — invisible RectTransform + highlight)
│   ├── TableRoot
│   │   └── TableAnchor_0..4        (shifted left to clear the InfoPanel)
│   ├── InfoPanel                   (TEMPORARY placeholder, bottom-right, §3)
│   ├── DragLayer                   (empty, LAST sibling)
│   └── HUD
│       ├── HeartsGroup, ScoreLabel, TimerBar
│       └── Overlays: ShelfClearBanner, HeartLostBanner, PauseMenu,
│                    GameOverPanel, TitlePanel
├── EventSystem
└── Systems
    └── GameManager, AudioManager, JuiceController, SaveService
```

`DragLayer` must be the canvas's last child so the dragged book renders above everything.

### Scripts

**Services — pure C#, no `MonoBehaviour`, no Unity API beyond `ScriptableObject`:**

| Script | Responsibility |
|---|---|
| `DifficultyCurve` (SO) | `GetShelfDuration(n)`, `GetLayoutConfig(n)`. Deterministic, side-effect free. |
| `ShelfGeometry` (SO) | 4 row rects measured from the bookcase sprite in Phase 0, each divided into 10 slot rects. |
| `ShelfGenerator` | From a `ShelfLayoutConfig` + seed, produce: the 4×10 slot grid with genre/sequence assignments, the prefilled placements (correct and misplaced), and the shuffled list of `BookData` to deal to the table. Also reports `requiredMoves` so `DifficultyCurve` can budget the timer. **Enforces the §8 generation guarantees.** |
| `ScoreService` | `AddSlotFirstCorrect()`, `AddTimeBonus(remaining, duration, shelfIndex)`. Event `OnScoreChanged`. Score is monotonically non-decreasing — assert this. |
| `LivesService` | Heart count, `LoseHeart()`. Events `OnHeartLost`, `OnGameOver`. |

**MonoBehaviours:**

| Script | Responsibility |
|---|---|
| `GameManager` | Owns the state machine and run state (`shelfIndex`, hearts, active bookcase). **The only script that drives state transitions.** States: `Title`, `Playing`, `ShelfClearing`, `HeartLost`, `Paused`, `GameOver`. |
| `ShelfController` | Instantiates the bookcase from a generated layout, wires rows/labels/slots, applies prefill, evaluates the win condition after every board change, raises `OnShelfComplete`. Runs dissipate and spawn animations. |
| `ShelfSlot` | `IDropHandler`. Holds `(rowGenre, expectedSequenceNumber, occupant, hasScored)`. Resolves drops: place into empty, or **swap** with occupant. Reports board changes to `ShelfController`. |
| `BookView` | `IBeginDragHandler`, `IDragHandler`, `IEndDragHandler`, `IPointerEnterHandler`, `IPointerExitHandler`. Binds `BookData`, switches between cover and spine sprite per §5, handles drag alpha, reparents to `DragLayer`, tweens home on invalid drops. |
| `BookQueue` | Owns the table. Maintains up to `maxOnTable` books at anchors, feeds the next in from off-screen right when a position frees, shifts the rest. Accepts books dragged back down from the shelf. |
| `ShelfTimer` | Countdown, `Pause()`, `Resume()`, `Reset(duration)`, event `OnExpired`. Must not tick outside the `Playing` state. |
| `HUDController` | Subscribes to service events, updates hearts, score, timer bar. **Read-only — never mutates game state.** |
| `InfoPanelController` | Builds the temporary Information panel (§3) from the active bookcase's genre→row assignment. Rebuilds on every bookcase spawn. Self-contained — no other script reaches into it, so the whole prefab can be swapped when real label art arrives. |
| `AudioManager` | Pooled SFX + music, ducking on bookcase clear. Existing clips only. |
| `JuiceController` | Screen shake, particles, hit-stop, tweens. **The game must be fully playable and correct with this disabled.** |
| `SaveService` | High score and best bookcase reached via `PlayerPrefs`. Keys: `hiscore`, `bestShelf`. |

### Architectural rules

- Services never reference views. Views subscribe to service events.
- Views never call other views directly — everything routes through `GameManager` or `ShelfController`.
- No `FindObjectOfType` in gameplay code. Wire via Inspector or a single composition root on `GameManager`.
- `ShelfGenerator`, `ScoreService`, `LivesService`, and `DifficultyCurve` must be testable in EditMode tests without entering Play mode. Write those tests.
- All randomness takes an explicit seed.

---

## 10. Asset Contract

Every entry refers to an asset **that already exists**. Locate and wire it; do not create it.

| Slot in the game | What's needed | Count |
|---|---|---|
| Book covers (table view) | Flat/laying sprite showing genre color + number | 4 genres × 10 = 40 |
| Book spines (shelved view) | Upright spine sprite showing genre color + number | 4 genres × 10 = 40 |
| Genre labels | **Does not exist yet.** Replaced for now by the code-drawn Information panel (§3). Leave `labelSprite` null. | 0 |
| Bookcase | The single main bookcase sprite | 1 |
| Slot highlight | Highlight state for a valid drop target | 1 |
| Background | Wall, paneling, side shelves, ivy, table surface | — |
| Hearts | Full and empty heart sprites | 2 |
| Timer bar | Bar fill + frame (+ danger state if it exists) | — |
| UI panels | Pause, game over, title, banner backgrounds | — |
| SFX | Pickup, place-correct, place-neutral, swap, bookcase clear, heart loss, timer warning, button click | — |
| Music | Gameplay loop, menu loop | — |

**Legibility requirement — verify this in Phase 0 before anything else.** Ten books across one board means each spine is about a tenth of the shelf width. The baked-in sequence number must be readable at that size on a 1280×720 window, in **both** cover and spine views. Never re-render numbers as text; they are part of the art. If they're illegible, report it and stop — the fix is a spec change (wider bookcase, tighter camera), not a code workaround.

---

## 11. Input & Interaction

- **Drag follow:** zero smoothing. The book tracks the pointer exactly. Any interpolation reads as broken input.
- **Grab offset:** preserve the pointer↔book offset at grab time. Never snap the book's center to the cursor.
- **Snap magnetism:** on release, if the pointer is within `snapRadius` (default 60px at reference resolution) of a slot center, treat it as a drop on that slot. Generous by design.
- **Hover:** hovering a table book lifts it slightly. While dragging, the slot under the pointer highlights — with a distinct highlight for "this slot is occupied, dropping here will swap."
- **Touch parity:** single-touch drag behaves identically to mouse. Multi-touch ignored — first touch wins until release.
- **Pause:** `Esc` or button. Pauses the timer, blocks all drag input.
- **No input lockouts anywhere.** There are no penalties, so there is nothing to lock the player out for.

---

## 12. Build Phases

Each phase ends committable and verifiable. Do not start a phase before the previous phase's DoD is met.

### Phase 0 — Asset inventory & geometry
Enumerate `Assets/`. Map to §10. Measure the bookcase sprite and author `ShelfGeometry` — 4 row rects, each divided into 10 equal slots.
**Critical check:** render one book spine at `rowWidth / 10` and confirm its baked-in number is legible at 1280×720. Ten books across one board is tight; this is the single most likely thing to break.
**DoD:** asset path → contract mapping written, `ShelfGeometry` created, legibility verified with a screenshot. **Stop for confirmation if anything is missing or illegible.**

### Phase 1 — Scene assembly
Build the §9 hierarchy from existing art. Camera framing, canvas scaler, background layering, table anchors, slot rects overlaid on the bookcase. Build the temporary `InfoPanel` prefab and resolve the panel-vs-table-entry layout conflict noted in §2. Set the product name and window title to **BookBound**.
**DoD:** scene opens at 16:9 and matches §2. Slot rects visibly align with the bookcase art in a debug gizmo view. Table books slide in fully visible without passing under the InfoPanel. Nothing interactive.

### Phase 2 — Data layer
`GenreDefinition` ×4 wired to all 80 sprites, `BookData`, `ShelfLayoutConfig`, `DifficultyCurve`, `ShelfGenerator`.
**DoD:** EditMode tests pass — book multiset exactly fills the board, `dealtCount == slots − prefill`, every "misplaced" book really is in a wrong slot, generation is deterministic per seed.

### Phase 3 — Static board
Bookcase populates from a generated layout: prefilled books render as spines in their slots, dealt books render as covers on the table. No interaction.
**DoD:** Play mode shows a correct, legible, partly-scrambled bookcase and a full table.

### Phase 4 — Drag, drop, swap *(vertical slice)*
`BookView` drag with grab offset, alpha, and cover↔spine switching. `ShelfSlot` place-and-swap. Table→slot, slot→slot, slot→table all working. `BookQueue` shifting and feed-in. Win condition detection.
**DoD:** an entire scrambled bookcase can be solved by hand from start to finish. **Stop and play it.** If the drag and swap don't feel right, nothing downstream will fix it.

### Phase 5 — Score, timer, hearts
`ScoreService` with the first-time-only slot rule, `ShelfTimer`, `LivesService`, `HUDController`.
**DoD:** score never decreases under any input; re-placing a book in an already-scored slot awards nothing; timer expiry costs a heart and resets the bookcase; 0 hearts reaches Game Over.

### Phase 6 — The run loop
Bookcase complete → timer stops → time bonus banner → dissipate → new bookcase at reduced duration. Full §8 difficulty ramp.
**DoD:** a run reaches bookcase 10 and is measurably tighter than bookcase 1. `shelfIndex`, hearts, and score persist correctly across transitions.

### Phase 7 — Feel & feedback
Hover lift, slot highlight (empty vs swap), placement thud, correct-placement chime, swap sound, bookcase-clear sequence, timer pulse under 10s. All through `JuiceController` using existing SFX.
**DoD:** a 30-second clip with sound feels good. Disabling `JuiceController` leaves the game fully playable.

### Phase 8 — Shell & persistence
Title, pause, game over with score and best. Restart with no scene reload. `SaveService`.
**DoD:** start → lose → restart leaks no state. High score survives app restart.

### Phase 9 — Balance
Tune `START_SPM`, `DECAY`, `FLOOR_SPM`, the already-correct and misplaced bands, `maxOnTable`, `snapRadius`. Measure real seconds-per-move from playtests first — `FLOOR_SPM` is currently a guess.
**Target:** an average player's run ends around bookcase 8–12; a strong player reaches 18+.
**DoD:** constants locked, 5+ recorded full playtests.

---

## 13. Feel Targets

Requirements, not polish.

- **Drag latency:** zero. Non-negotiable.
- **Snap magnetism:** generous. The player should feel accurate, not fiddly.
- **Swap clarity:** when the pointer is over an occupied slot, it must be visually obvious a swap will happen. Ambiguity here will make the whole board feel unpredictable.
- **Cover↔spine transition:** instant, not animated. Any delay makes the board state feel uncertain during fast play.
- **Bookcase clear:** 1.2–1.8s. Long enough to land as a reward, short enough that the player is impatient for the next one.
- **Timer under 10s:** color shift plus audible pulse. This is the emotional peak of every bookcase — it must be unmistakable.
- **Book feed-in:** new table books slide in visibly from the right. The player should be able to read a book before it settles.

---

## 14. Out of Scope (v1)

Multiplayer · level editor · story mode · online leaderboards · mid-run saves · portrait layout · localization · any asset creation.

---

## 15. Open Questions

1. **Spine legibility at 10-across** — the biggest open risk. Ten books per board makes each spine roughly a tenth of the shelf width, and the sequence number is baked into the art at a fixed resolution. Phase 0 verifies this before any gameplay code is written. If it fails, the options are a wider bookcase, a taller camera crop, or fewer slots — all of which change the spec.

2. **Streak multiplier** — removed entirely. With no penalties there's no clean way to break a streak that doesn't punish the rearranging the game requires. If more scoring texture is wanted later, a speed bonus (N correct placements within a time window) fits the design better than a streak.

3. **Genre label art is deferred** — the Information panel (§3) is a stopgap, not the intended design. When the real label art is made, it should sit under each row where the player's eyes already are; the panel forces a glance to the corner and back on every uncertain book, which costs time on a clock-driven game. Worth prioritizing sooner rather than later. Keep `labelSprite` and the inactive `GenreLabel` node in place so the swap is trivial.

4. **40 books may be a long bookcase** — a full board is every book in the game, and even a well-solved spawn is ~18 drags. If playtesting shows bookcases dragging on, the fix is raising `alreadyCorrect` across the board rather than shrinking the shelf.

5. **`FLOOR_SPM = 1.4s` is unverified** — it's a guess at the fastest a human can reliably drag and drop. Confirm it in Phase 9 before treating the late-game ramp as real.

6. **Table size vs. board size** — `maxOnTable = 5` against a 40-slot board means the table is a narrow window onto a long queue. If players feel they can't plan ahead, raising it to 6–7 is the cheapest fix.
