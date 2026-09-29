using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace BookBound.Debugging
{
    /// <summary>
    /// Dev-only visual smoke check for a real (non-headless) standalone build: screenshots the title
    /// screen, clicks Play via reflection (same trick as the editor's AnimSmokeTest), waits for the
    /// screen-fade + bookcase spawn, then screenshots gameplay. Only runs when launched with
    /// -bbscreenshot, so it's a no-op for a normal launch. Not wired to anything else - safe to delete
    /// once the HUD overlay art is confirmed to look right.
    /// </summary>
    public class AutoScreenshot : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log($"[AutoScreenshot] args: {string.Join(" ", System.Environment.GetCommandLineArgs())}");
            if (System.Environment.GetCommandLineArgs().Contains("-bbscreenshot"))
                StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            Debug.Log("[AutoScreenshot] starting capture sequence");
            yield return new WaitForSecondsRealtime(1.2f);
            Debug.Log($"[AutoScreenshot] capturing title, screen={Screen.width}x{Screen.height}");
            ScreenCapture.CaptureScreenshot("bb_screenshot_title.png");
            yield return new WaitForSecondsRealtime(0.5f);
            Debug.Log("[AutoScreenshot] clicking play");

            var shellUI = FindFirstObjectByType<ShellUI>();
            Click(shellUI, "playMediumButton");

            yield return new WaitForSecondsRealtime(1.5f);
            Debug.Log("[AutoScreenshot] capturing gameplay");
            ScreenCapture.CaptureScreenshot("bb_screenshot_gameplay.png");
            yield return new WaitForSecondsRealtime(0.8f);

            // Pause, then Options: the only way to eyeball those two screens without a human at the
            // window, and the reason this tool earns its keep past the HUD art pass.
            Click(shellUI, "pauseButton");
            yield return new WaitForSecondsRealtime(1.2f);
            Debug.Log("[AutoScreenshot] capturing pause");
            ScreenCapture.CaptureScreenshot("bb_screenshot_pause.png");
            yield return new WaitForSecondsRealtime(0.8f);

            Click(shellUI, "pauseOptionsButton");
            yield return new WaitForSecondsRealtime(1.2f);
            Debug.Log("[AutoScreenshot] capturing options");
            ScreenCapture.CaptureScreenshot("bb_screenshot_options.png");
            yield return new WaitForSecondsRealtime(0.8f);

            Debug.Log($"[AutoScreenshot] done, data path: {Application.persistentDataPath}");
            Application.Quit();
        }

        /// <summary>Fires a ShellUI button by field name. Reflection because there's no pointer device
        /// to click with here, and ShellUI's buttons are private by design.</summary>
        private static void Click(ShellUI shellUI, string fieldName)
        {
            FieldInfo field = typeof(ShellUI).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field?.GetValue(shellUI) is Button button) button.onClick.Invoke();
            else Debug.LogWarning($"[AutoScreenshot] no button field '{fieldName}'");
        }
    }
}
