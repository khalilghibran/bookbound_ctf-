using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BookBound.EditorTools
{
    public static class SceneDebugDump
    {
        [MenuItem("BookBound/Setup/Debug/Dump Scene Info")]
        public static void Dump()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");

            foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                var sprite = sr.sprite;
                Debug.Log($"[Dump] SpriteRenderer '{sr.gameObject.name}': sprite={(sprite ? sprite.name : "NULL")} " +
                          $"spriteRect={(sprite ? sprite.rect.ToString() : "-")} " +
                          $"pivot={(sprite ? sprite.pivot.ToString() : "-")} " +
                          $"pos={sr.transform.position} scale={sr.transform.localScale} " +
                          $"bounds={sr.bounds} sortingLayer={sr.sortingLayerName} order={sr.sortingOrder} " +
                          $"mat={(sr.sharedMaterial ? sr.sharedMaterial.name : "NULL")}({(sr.sharedMaterial ? sr.sharedMaterial.GetInstanceID().ToString() : "-")}) " +
                          $"matMainTex={(sr.sharedMaterial && sr.sharedMaterial.mainTexture ? sr.sharedMaterial.mainTexture.name : "NULL")} " +
                          $"spriteTex={(sprite && sprite.texture ? sprite.texture.name : "NULL")} " +
                          $"enabled={sr.enabled} color={sr.color}");
            }

            var cam = Camera.main;
            Debug.Log($"[Dump] Camera pos={cam.transform.position} orthoSize={cam.orthographicSize} aspect={cam.aspect} " +
                      $"viewportOfOrigin={cam.WorldToViewportPoint(Vector3.zero)}");

            var shelfRoot = GameObject.Find("Canvas/ShelfRoot");
            if (shelfRoot != null)
            {
                var rt = shelfRoot.GetComponent<RectTransform>();
                Debug.Log($"[Dump] ShelfRoot anchoredPosition={rt.anchoredPosition} sizeDelta={rt.sizeDelta} " +
                          $"worldCorners=[{string.Join(", ", GetWorldCorners(rt))}]");
            }
        }

        private static Vector3[] GetWorldCorners(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return corners;
        }
    }
}
