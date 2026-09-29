using UnityEngine;

namespace BookBound
{
    /// <summary>Marks the DragLayer RectTransform so BookView can reparent onto it without a scene search.</summary>
    public class DragLayerMarker : MonoBehaviour
    {
        public static RectTransform Instance { get; private set; }

        private void Awake() => Instance = (RectTransform)transform;

        private void OnDestroy()
        {
            if (Instance == (RectTransform)transform) Instance = null;
        }
    }
}
