using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BookBound.EditorTools
{
    public static class SceneScreenshot
    {
        [MenuItem("BookBound/Setup/Debug/Capture Main Scene Screenshot")]
        public static void Capture()
        {
            CaptureInternal(showSlotDebugGuides: false);
        }

        [MenuItem("BookBound/Setup/Debug/Capture Main Scene Screenshot (Slot Guides Visible)")]
        public static void CaptureWithGuides()
        {
            CaptureInternal(showSlotDebugGuides: true);
        }

        private static void CaptureInternal(bool showSlotDebugGuides)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");

            // Workaround for a batch-mode-only rendering artifact: multiple SpriteRenderers sharing
            // Unity's default Sprite-Lit-Default material instance can sample the wrong texture when
            // rendered back-to-back via repeated Camera.Render() calls outside a normal Play-mode
            // frame loop (confirmed via a minimal 2-sprite repro). Does not affect the saved scene -
            // real Play mode uses the shared material normally, which is standard and fine.
            foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                sr.sharedMaterial = new Material(sr.sharedMaterial);

            if (showSlotDebugGuides)
            {
                foreach (var img in Object.FindObjectsByType<Image>(FindObjectsSortMode.None))
                {
                    if (img.gameObject.name.StartsWith("Slot_"))
                    {
                        var c = img.color;
                        c.a = 0.5f;
                        img.color = c;
                    }
                }
            }

            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[SceneScreenshot] No Main Camera found.");
                return;
            }

            int width = 1920, height = 1080;
            var rt = new RenderTexture(width, height, 24);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            float prevAspect = cam.aspect;

            cam.aspect = width / (float)height;
            cam.targetTexture = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            string outPath = "/private/tmp/claude-501/-Users-fahd-Documents-Game-Development-Unity-FirstGame-Compfest-IGI-GAMEJAM/7b5b9f36-76c6-423f-aa6b-fe34b9c80b0d/scratchpad/scene_screenshot.png";
            System.IO.File.WriteAllBytes(outPath, png);

            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            cam.ResetAspect();
            rt.Release();

            Debug.Log($"[SceneScreenshot] wrote {outPath}, {png.Length} bytes");
        }
    }
}
