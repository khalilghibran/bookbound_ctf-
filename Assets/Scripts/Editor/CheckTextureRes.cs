using UnityEditor;
using UnityEngine;

namespace BookBound.EditorTools
{
    public static class CheckTextureRes
    {
        [MenuItem("BookBound/Setup/Debug/Check Texture Resolution")]
        public static void Run()
        {
            string path = "Assets/Art/side-view/fantasy_side_spritesheet.png";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Debug.Log($"[CheckTextureRes] {path}: actual loaded size = {tex.width}x{tex.height}, filterMode={tex.filterMode}, format={tex.format}");
        }
    }
}
