using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BookBound.EditorTools
{
    /// <summary>
    /// Enters Play mode, waits a few frames so Start() has run, captures the main camera to PNG, then
    /// exits. Entering Play mode triggers a C# domain reload, which wipes plain static fields and
    /// event subscriptions - state has to survive that via SessionState, and the update callback has
    /// to be re-registered unconditionally via [InitializeOnLoad] rather than only inside Run().
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeScreenshot
    {
        private const string PendingKey = "BookBound.PlayModeScreenshot.Pending";
        private const string OutputPathKey = "BookBound.PlayModeScreenshot.OutputPath";
        private const string FrameCountKey = "BookBound.PlayModeScreenshot.FrameCount";
        private const int FramesToWait = 20;

        static PlayModeScreenshot()
        {
            EditorApplication.update += OnUpdate;
        }

        public static void RunDefault()
        {
            Run("/private/tmp/claude-501/-Users-fahd-Documents-Game-Development-Unity-FirstGame-Compfest-IGI-GAMEJAM/7b5b9f36-76c6-423f-aa6b-fe34b9c80b0d/scratchpad/playmode_screenshot.png");
        }

        public static void Run(string outputPath)
        {
            SessionState.SetString(OutputPathKey, outputPath);
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

            SessionState.SetBool(PendingKey, false);
            Capture(SessionState.GetString(OutputPathKey, ""));
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(0);
        }

        private static void Capture(string outputPath)
        {
            var books = Object.FindObjectsByType<BookView>(FindObjectsSortMode.None);
            Debug.Log($"[PlayModeScreenshot] BookView count in scene: {books.Length}");
            if (books.Length > 0)
            {
                var b = books[0];
                var img = b.GetComponent<UnityEngine.UI.Image>();
                var bookRt = (RectTransform)b.transform;
                Canvas canvasParent = b.GetComponentInParent<Canvas>();
                Debug.Log($"[PlayModeScreenshot] sample book: activeInHierarchy={b.gameObject.activeInHierarchy} " +
                          $"imgEnabled={img.enabled} color={img.color} sprite={img.sprite} " +
                          $"localRect={bookRt.rect} localScale={bookRt.localScale} lossyScale={bookRt.lossyScale} " +
                          $"canvasParent={(canvasParent ? canvasParent.name : "NULL")} canvasEnabled={(canvasParent ? canvasParent.enabled.ToString() : "-")} " +
                          $"parentChain={DescribeParentChain(bookRt)}");
            }

            // Same batch-mode-only workaround as the SpriteRenderer case: force unique material
            // instances so a manual off-cycle Camera.Render() doesn't sample the wrong texture from
            // a shared material.
            foreach (var g in Object.FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsSortMode.None))
                g.material = new Material(g.material);

            Canvas.ForceUpdateCanvases();

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[PlayModeScreenshot] No Main Camera found.");
                return;
            }

            int width = 1920, height = 1080;
            var rt = new RenderTexture(width, height, 24);
            cam.aspect = width / (float)height;
            cam.targetTexture = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            System.IO.File.WriteAllBytes(outputPath, tex.EncodeToPNG());
            Debug.Log($"[PlayModeScreenshot] wrote {outputPath}");

            cam.targetTexture = null;
            RenderTexture.active = null;
            cam.ResetAspect();
            rt.Release();
        }

        private static string DescribeParentChain(Transform t)
        {
            var names = new System.Collections.Generic.List<string>();
            while (t != null)
            {
                names.Add($"{t.name}(active={t.gameObject.activeSelf},scale={t.localScale})");
                t = t.parent;
            }
            names.Reverse();
            return string.Join(" > ", names);
        }
    }
}
