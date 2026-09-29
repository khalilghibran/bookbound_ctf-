using UnityEditor;
using UnityEngine;

namespace BookBound.EditorTools
{
    /// <summary>
    /// The URP 2D Renderer ships with Transparency Sort Mode = Custom Axis, Axis = (0,1,0) - a
    /// top-down-game default where a sprite's Y position (not sortingOrder) decides draw order.
    /// BookBound is a flat diorama, not Y-sorted, and that default was silently overriding our
    /// sortingOrder (the bookcase, at a higher world Y than the wall, was drawing BEHIND it despite
    /// a higher sortingOrder). Switch the sort axis to Z so world-space Z / sortingOrder behaves as
    /// expected everywhere in the scene.
    /// </summary>
    public static class FixRenderer2DSorting
    {
        private const string RendererDataPath = "Assets/Settings/Renderer2D.asset";

        [MenuItem("BookBound/Setup/-1. Fix Renderer2D Transparency Sort Axis")]
        public static void Fix()
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(RendererDataPath);
            if (asset == null)
            {
                Debug.LogError($"[FixRenderer2DSorting] No asset at {RendererDataPath}");
                return;
            }

            var so = new SerializedObject(asset);
            var axisProp = so.FindProperty("m_TransparencySortAxis");
            axisProp.vector3Value = new Vector3(0f, 0f, 1f);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log("[FixRenderer2DSorting] Transparency sort axis set to (0,0,1).");
        }
    }
}
