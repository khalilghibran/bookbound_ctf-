using System.IO;
using UnityEditor;
using UnityEngine;

namespace BookBound.EditorTools
{
    /// <summary>
    /// Builds a playable player into &lt;project&gt;/Build/. Callable from the menu or headlessly:
    ///   Unity.exe -batchmode -quit -projectPath &lt;proj&gt; -executeMethod BookBound.EditorTools.BuildStandalone.Build
    /// Build/ is gitignored, so the output never pollutes the repo.
    /// </summary>
    public static class BuildStandalone
    {
        /// <summary>
        /// Import settings -> slices -> data assets -> scene -> player, in that order. The scene is
        /// generated and the data assets are derived from code (see HANDOFF.md), so running the whole
        /// chain is the only way a headless build can't end up with a scene wired to stale
        /// ScriptableObjects. Each step is idempotent and skips work it doesn't need to redo.
        ///
        /// The slicers have to sit between the import pass and the data pass: they own the named
        /// sub-sprites that ProjectSetup (SpriteFont) and SceneSetup (every LoadNamedSprite call)
        /// read, and they set Bilinear on the painterly sheets, which the import pass deliberately
        /// skips. Leaving them out of this chain meant replacing a spritesheet's bytes silently broke
        /// the next build with "could not find sub-sprite".
        /// </summary>
        [MenuItem("BookBound/Rebuild Scene and Build Player")]
        public static void RebuildAndBuild()
        {
            FixSpriteImportQuality.Fix();
            SliceStartScreenSprites.Slice();
            SliceSideViewSprites.SliceAll();
            SliceHudOverlaySprites.Slice();
            SetPlayerIcon.Apply();
            ProjectSetup.RunAll();
            SceneSetup.BuildMainScene();
            Build();
        }

        [MenuItem("BookBound/Build Player")]
        public static void Build()
        {
            BuildTo("BookBound.exe", BuildTarget.StandaloneWindows64);
        }

        /// <summary>Local Mac test build - the committed "Build Player" targets Windows, which isn't
        /// runnable on this machine. Keeps the .app out of source control the same way (see .gitignore).</summary>
        [MenuItem("BookBound/Build Player (Mac, local test)")]
        public static void BuildMac()
        {
            BuildTo("BookBound.app", BuildTarget.StandaloneOSX);
        }

        private static void BuildTo(string fileName, BuildTarget target)
        {
            string root = Path.Combine(Directory.GetCurrentDirectory(), "Build");
            string outputPath = Path.Combine(root, fileName);
            Directory.CreateDirectory(root);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/MainScene.unity" },
                locationPathName = outputPath,
                target = target,
                options = BuildOptions.None
            });

            Debug.Log($"[BuildStandalone] Result: {report.summary.result}, size={report.summary.totalSize}, path={outputPath}");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
