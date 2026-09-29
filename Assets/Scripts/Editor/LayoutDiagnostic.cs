using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BookBound.EditorTools
{
    /// <summary>
    /// Enters Play mode at a specific simulated resolution and dumps RectTransform world corners for
    /// the key layout objects, so misalignment can be diagnosed numerically without a human screenshot.
    /// </summary>
    [InitializeOnLoad]
    public static class LayoutDiagnostic
    {
        private const string PendingKey = "BookBound.LayoutDiagnostic.Pending";
        private const string FrameCountKey = "BookBound.LayoutDiagnostic.FrameCount";
        private const string WidthKey = "BookBound.LayoutDiagnostic.Width";
        private const string HeightKey = "BookBound.LayoutDiagnostic.Height";
        private const int FramesToWait = 20;

        static LayoutDiagnostic()
        {
            EditorApplication.update += OnUpdate;
        }

        public static void RunAtUserResolution()
        {
            Run(3024, 1964);
        }

        public static void RunAt1920x1080()
        {
            Run(1920, 1080);
        }

        private static void Run(int width, int height)
        {
            SessionState.SetInt(WidthKey, width);
            SessionState.SetInt(HeightKey, height);
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

            if (frame == 2)
            {
                int w = SessionState.GetInt(WidthKey, 1920);
                int h = SessionState.GetInt(HeightKey, 1080);
                Screen.SetResolution(w, h, false);
                Debug.Log($"[LayoutDiagnostic] Forced resolution to {w}x{h}");
                return;
            }

            if (frame < FramesToWait) return;

            SessionState.SetBool(PendingKey, false);
            Dump();
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(0);
        }

        private static void Dump()
        {
            Debug.Log($"[LayoutDiagnostic] Actual Screen.width={Screen.width} Screen.height={Screen.height}");

            DumpRect("AspectLock", GameObject.Find("Canvas/AspectLock"));
            DumpRect("Background", GameObject.Find("Canvas/AspectLock/Background"));
            DumpRect("BookcaseImage", GameObject.Find("Canvas/AspectLock/BookcaseImage"));
            DumpRect("ShelfRoot", GameObject.Find("Canvas/AspectLock/BookcaseImage/ShelfRoot"));
            DumpRect("Row_0", GameObject.Find("Canvas/AspectLock/BookcaseImage/ShelfRoot/Row_0"));
            DumpRect("Row_0/Slot_0", GameObject.Find("Canvas/AspectLock/BookcaseImage/ShelfRoot/Row_0/Slot_0"));
            DumpRect("Row_1/Slot_0", GameObject.Find("Canvas/AspectLock/BookcaseImage/ShelfRoot/Row_1/Slot_0"));
            DumpRect("TableImage", GameObject.Find("Canvas/AspectLock/TableImage"));
            DumpRect("TableRoot", GameObject.Find("Canvas/AspectLock/TableImage/TableRoot"));
            DumpRect("TableAnchor_0", GameObject.Find("Canvas/AspectLock/TableImage/TableRoot/TableAnchor_0"));

            // Also find one actual Book(Clone) inside the shelf and one on the table, and report their
            // world rects directly - this is what genuinely determines what's on screen.
            var books = Object.FindObjectsByType<BookView>(FindObjectsSortMode.None);
            Debug.Log($"[LayoutDiagnostic] BookView count: {books.Length}");
            foreach (var b in books)
            {
                string path = GetPath(b.transform);
                if (path.Contains("Slot_0") || path.Contains("TableAnchor_0"))
                    DumpRect($"Book in {path}", b.gameObject);
            }
        }

        private static void DumpRect(string label, GameObject go)
        {
            if (go == null)
            {
                Debug.Log($"[LayoutDiagnostic] {label}: NOT FOUND");
                return;
            }
            var rt = (RectTransform)go.transform;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Debug.Log($"[LayoutDiagnostic] {label}: bottomLeft={corners[0]} topRight={corners[2]} " +
                      $"anchoredPos={rt.anchoredPosition} sizeDelta={rt.sizeDelta} anchorMin={rt.anchorMin} anchorMax={rt.anchorMax}");
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
