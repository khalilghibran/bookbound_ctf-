using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BookBound.EditorTools
{
    /// <summary>
    /// One-off diagnostic (not part of the normal setup pipeline) that drives Play mode headlessly to
    /// exercise the new animation layer end to end: title screen idle, a Play click through the screen
    /// fade, a real drag-simulated book placement (via reflection on BookView's drag handlers, since
    /// there's no pointer device in batchmode), and a heart-loss/score-roll pass. Success is judged by
    /// grepping the resulting log for exceptions, same approach as Phase5Diagnostic.
    /// </summary>
    [InitializeOnLoad]
    public static class AnimSmokeTest
    {
        private const string PendingKey = "BookBound.AnimSmokeTest.Pending";
        private const string FrameKey = "BookBound.AnimSmokeTest.Frame";

        static AnimSmokeTest()
        {
            EditorApplication.update += OnUpdate;
        }

        [MenuItem("BookBound/Setup/Debug/Animation Smoke Test")]
        public static void Run()
        {
            SessionState.SetInt(FrameKey, 0);
            SessionState.SetBool(PendingKey, true);
            EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
            EditorApplication.isPlaying = true;
        }

        private static void OnUpdate()
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;

            int frame = SessionState.GetInt(FrameKey, 0) + 1;
            SessionState.SetInt(FrameKey, frame);

            switch (frame)
            {
                case 10:
                    Debug.Log("[AnimSmoke] Frame 10: clicking Play.");
                    ClickPlay();
                    break;
                case 60:
                    Debug.Log("[AnimSmoke] Frame 60: simulating a drag to the first empty correct slot.");
                    SimulateCorrectDrag();
                    break;
                case 90:
                    Debug.Log("[AnimSmoke] Frame 90: simulating a heart loss.");
                    SimulateHeartLoss();
                    break;
                case 150:
                    Debug.Log("[AnimSmoke] Frame 150: done, exiting play mode.");
                    SessionState.SetBool(PendingKey, false);
                    EditorApplication.isPlaying = false;
                    EditorApplication.Exit(0);
                    break;
            }
        }

        private static void ClickPlay()
        {
            var shellUI = Object.FindAnyObjectByType<ShellUI>();
            var playButtonField = typeof(ShellUI).GetField("playMediumButton", BindingFlags.NonPublic | BindingFlags.Instance);
            var button = (UnityEngine.UI.Button)playButtonField.GetValue(shellUI);
            button.onClick.Invoke();
        }

        private static void SimulateCorrectDrag()
        {
            var shelfController = Object.FindAnyObjectByType<ShelfController>();
            var slotsField = typeof(ShelfController).GetField("_slots", BindingFlags.NonPublic | BindingFlags.Instance);
            var slots = (ShelfSlot[,])slotsField.GetValue(shelfController);

            ShelfSlot targetSlot = null;
            BookView book = null;
            for (int r = 0; r < ShelfGeometry.RowCount && targetSlot == null; r++)
            for (int s = 0; s < ShelfGeometry.SlotsPerRow && targetSlot == null; s++)
            {
                ShelfSlot slot = slots[r, s];
                if (slot.Occupant != null) continue;
                var wanted = new BookData(slot.RowGenre, slot.ExpectedSequenceNumber);
                for (int r2 = 0; r2 < ShelfGeometry.RowCount; r2++)
                for (int s2 = 0; s2 < ShelfGeometry.SlotsPerRow; s2++)
                {
                    var candidate = slots[r2, s2];
                    if (candidate.Occupant != null && candidate.Occupant.Data.Equals(wanted))
                    {
                        targetSlot = slot;
                        book = candidate.Occupant;
                        break;
                    }
                }
            }

            if (targetSlot == null || book == null)
            {
                Debug.Log("[AnimSmoke] No misplaced-correct pair found on this board; skipping drag simulation.");
                return;
            }

            var ped = new PointerEventData(EventSystem.current) { position = ((RectTransform)book.transform).position };
            InvokeDragEvent(book, "OnBeginDrag", ped);
            ped.position = targetSlot.RectTransform.position;
            InvokeDragEvent(book, "OnDrag", ped);
            InvokeDragEvent(book, "OnEndDrag", ped);
            Debug.Log($"[AnimSmoke] Drag simulated: {book.Data} -> row {targetSlot.RowIndex} slot {targetSlot.SlotIndex}.");
        }

        private static void InvokeDragEvent(BookView book, string methodName, PointerEventData ped)
        {
            var method = typeof(BookView).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            method.Invoke(book, new object[] { ped });
        }

        private static void SimulateHeartLoss()
        {
            var timer = Object.FindAnyObjectByType<ShelfTimer>();
            var expiredField = typeof(ShelfTimer).GetField("OnExpired", BindingFlags.NonPublic | BindingFlags.Instance);
            var del = expiredField.GetValue(timer) as System.Action;
            del?.Invoke();
        }
    }
}
