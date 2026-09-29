using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BookBound.EditorTools
{
    public static class BuildPrefabs
    {
        private const string BookPrefabPath = "Assets/Prefabs/Book.prefab";

        [MenuItem("BookBound/Setup/6. Build Book Prefab")]
        public static void CreateBookPrefab()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            var go = new GameObject("Book", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(100f, 100f);

            var image = go.AddComponent<Image>();
            image.raycastTarget = true;
            image.preserveAspect = true;

            var bookView = go.AddComponent<BookView>();
            var so = new SerializedObject(bookView);
            so.FindProperty("image").objectReferenceValue = image;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(go, BookPrefabPath);
            Object.DestroyImmediate(go);

            Debug.Log($"[BuildPrefabs] Saved {BookPrefabPath}");
        }
    }
}
