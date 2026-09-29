using UnityEditor;
using UnityEngine;

namespace BookBound.EditorTools
{
    public static class ImportTmpEssentials
    {
        [MenuItem("BookBound/Setup/-3. Import TMP Essential Resources")]
        public static void Run()
        {
            AssetDatabase.importPackageCompleted += OnCompleted;
            AssetDatabase.importPackageFailed += OnFailed;
            AssetDatabase.importPackageCancelled += OnCancelled;

            string packagePath =
                "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
            AssetDatabase.ImportPackage(packagePath, false);
            Debug.Log("[ImportTmpEssentials] Requested import of TMP Essential Resources.");
        }

        private static void OnCompleted(string packageName)
        {
            Debug.Log($"[ImportTmpEssentials] Completed: {packageName}");
            EditorApplication.Exit(0);
        }

        private static void OnFailed(string packageName, string errorMessage)
        {
            Debug.LogError($"[ImportTmpEssentials] Failed: {packageName}: {errorMessage}");
            EditorApplication.Exit(1);
        }

        private static void OnCancelled(string packageName)
        {
            Debug.LogWarning($"[ImportTmpEssentials] Cancelled: {packageName}");
            EditorApplication.Exit(1);
        }
    }
}
