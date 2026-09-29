using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BookBound.EditorTools
{
    [InitializeOnLoad]
    public static class Phase5Diagnostic
    {
        private const string PendingKey = "BookBound.Phase5Diagnostic.Pending";
        private const string FrameCountKey = "BookBound.Phase5Diagnostic.FrameCount";
        private const int FramesToWait = 20;

        static Phase5Diagnostic()
        {
            EditorApplication.update += OnUpdate;
        }

        [MenuItem("BookBound/Setup/Debug/Phase 5 Diagnostic")]
        public static void Run()
        {
            SessionState.SetInt(FrameCountKey, 0);
            SessionState.SetBool(PendingKey, true);
            EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
            EditorApplication.isPlaying = true;
        }

        private static void OnUpdate()
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;

            int frame = SessionState.GetInt(FrameCountKey, 0) + 1;
            SessionState.SetInt(FrameCountKey, frame);
            if (frame < FramesToWait) return;
            if (frame == FramesToWait) { Dump(); return; }

            // One extra frame so Object.Destroy() calls issued inside Dump() actually flush before we
            // check the resulting count and exit.
            var allBooksNextFrame = Object.FindObjectsByType<BookView>(FindObjectsSortMode.None);
            Debug.Log($"[Phase5Diag] BookView count one frame later (post-Destroy-flush): {allBooksNextFrame.Length}");

            SessionState.SetBool(PendingKey, false);
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(0);
        }

        private static void Dump()
        {
            var gm = Object.FindFirstObjectByType<GameManager>();
            if (gm == null) { Debug.Log("[Phase5Diag] No GameManager found."); return; }

            var type = typeof(GameManager);
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            object Get(string name) => type.GetField(name, flags)?.GetValue(gm);

            Debug.Log($"[Phase5Diag] genres={((GenreDefinition[])Get("genres"))?.Length} " +
                      $"difficultyCurve={Get("difficultyCurve")} shelfController={Get("shelfController")} " +
                      $"bookQueue={Get("bookQueue")} infoPanel={Get("infoPanel")} " +
                      $"shelfTimer={Get("shelfTimer")} hudController={Get("hudController")} " +
                      $"bookPrefab={Get("bookPrefab")}");

            var score = (ScoreService)Get("_score");
            var lives = (LivesService)Get("_lives");
            Debug.Log($"[Phase5Diag] score={score?.Score} lives={lives?.Hearts}");

            var timer = Object.FindFirstObjectByType<ShelfTimer>();
            Debug.Log($"[Phase5Diag] timer duration={timer?.Duration} remaining={timer?.TimeRemaining} paused={timer?.IsPaused}");

            var shelfController = Object.FindFirstObjectByType<ShelfController>();
            int correctCount = 0, emptyCount = 0, occupiedCount = 0;
            var slotsField = typeof(ShelfController).GetField("_slots", flags);
            var slots = (ShelfSlot[,])slotsField.GetValue(shelfController);
            for (int r = 0; r < ShelfGeometry.RowCount; r++)
            for (int s = 0; s < ShelfGeometry.SlotsPerRow; s++)
            {
                var slot = slots[r, s];
                if (slot.Occupant == null) { emptyCount++; continue; }
                occupiedCount++;
                if (slot.IsCorrect(slot.Occupant.Data)) correctCount++;
            }
            Debug.Log($"[Phase5Diag] board: empty={emptyCount} occupied={occupiedCount} correct={correctCount}");

            // Find a slot whose CORRECT book is sitting somewhere else on the board (misplaced there),
            // and simulate the exact swap a successful player drag would perform, using the same
            // IDropTarget.Place() calls BookView.OnEndDrag uses - then check whether score/CheckState
            // actually react, exactly like a real move would trigger via OnAnyBookMoved.
            ShelfSlot targetSlot = null;
            ShelfSlot sourceSlot = null;
            for (int r = 0; r < ShelfGeometry.RowCount && targetSlot == null; r++)
            for (int s = 0; s < ShelfGeometry.SlotsPerRow && targetSlot == null; s++)
            {
                var slot = slots[r, s];
                if (slot.Occupant != null) continue; // want an EMPTY slot to fill correctly, simplest case
                var wanted = new BookData(slot.RowGenre, slot.ExpectedSequenceNumber);

                for (int r2 = 0; r2 < ShelfGeometry.RowCount; r2++)
                for (int s2 = 0; s2 < ShelfGeometry.SlotsPerRow; s2++)
                {
                    var candidate = slots[r2, s2];
                    if (candidate.Occupant != null && candidate.Occupant.Data.Equals(wanted))
                    {
                        targetSlot = slot;
                        sourceSlot = candidate;
                        break;
                    }
                }
            }

            if (targetSlot == null)
            {
                Debug.Log("[Phase5Diag] Could not find an empty slot whose correct book is misplaced elsewhere on the board to test with.");
            }
            else
            {
                int scoreBefore = score.Score;
                BookView book = sourceSlot.Occupant;
                Debug.Log($"[Phase5Diag] Simulating move: {book.Data} from a slot into its correct EMPTY slot (expects seq {targetSlot.ExpectedSequenceNumber} genre {targetSlot.RowGenre.Id}).");

                sourceSlot.Clear();
                targetSlot.Place(book);
                typeof(BookView).GetMethod("OnAnyBookMoved", BindingFlags.NonPublic | BindingFlags.Static);
                // Fire the same static event BookView.OnEndDrag fires, via reflection on its backing field.
                var eventField = typeof(BookView).GetField("OnAnyBookMoved", BindingFlags.NonPublic | BindingFlags.Static);
                var del = eventField?.GetValue(null) as System.Action;
                del?.Invoke();

                Debug.Log($"[Phase5Diag] score before={scoreBefore} after={score.Score} (expected +100)");
            }

            // Now simulate a timer expiry (heart lost -> bookcase respawns) and confirm no book
            // GameObjects leak. Object.Destroy() is deferred to end-of-frame, so record which
            // instances SHOULD be dead and re-check next editor tick rather than in the same call.
            var allBooksBefore = Object.FindObjectsByType<BookView>(FindObjectsSortMode.None);
            Debug.Log($"[Phase5Diag] BookView count before respawn: {allBooksBefore.Length}");

            var timerObj = Object.FindFirstObjectByType<ShelfTimer>();
            var expiredField = typeof(ShelfTimer).GetField("OnExpired", BindingFlags.NonPublic | BindingFlags.Instance);
            var expiredDel = expiredField?.GetValue(timerObj) as System.Action;
            expiredDel?.Invoke();

            var allBooksAfterSameFrame = Object.FindObjectsByType<BookView>(FindObjectsSortMode.None);
            Debug.Log($"[Phase5Diag] BookView count immediately after respawn (same frame, Destroy() not yet flushed): {allBooksAfterSameFrame.Length}");
            Debug.Log($"[Phase5Diag] lives after expiry: {lives.Hearts}");
            Debug.Log("[Phase5Diag] done with Dump(), checking post-flush count next frame.");
        }
    }
}
