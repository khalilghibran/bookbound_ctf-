using System;
using UnityEngine;

namespace BookBound
{
    /// <summary>
    /// Pure, deterministic difficulty ramp. Duration is budgeted per required move, not per bookcase.
    /// No Unity API calls beyond being a ScriptableObject.
    /// </summary>
    [CreateAssetMenu(menuName = "BookBound/Difficulty Curve", fileName = "DifficultyCurve")]
    public class DifficultyCurve : ScriptableObject
    {
        [Header("Seconds allowed per required move")]
        [SerializeField] private float startSecondsPerMove = 3.0f;
        [SerializeField] private float decayPerShelf = 0.93f;
        [SerializeField] private float floorSecondsPerMove = 1.4f;

        [Serializable]
        public struct DifficultyBand
        {
            [Tooltip("This band applies from this shelfIndex (inclusive) until the next band's minShelfIndex.")]
            public int minShelfIndex;
            public ShelfLayoutConfig layoutConfig;
        }

        [Tooltip("Sorted ascending by minShelfIndex. The last band whose minShelfIndex <= shelfIndex applies.")]
        [SerializeField] private DifficultyBand[] bands = Array.Empty<DifficultyBand>();

        public float GetSecondsPerMove(int shelfIndex)
        {
            float raw = startSecondsPerMove * Mathf.Pow(decayPerShelf, shelfIndex);
            return Mathf.Max(floorSecondsPerMove, raw);
        }

        public float GetShelfDuration(int shelfIndex, int requiredMoves)
        {
            return Mathf.Ceil(requiredMoves * GetSecondsPerMove(shelfIndex));
        }

        public ShelfLayoutConfig GetLayoutConfig(int shelfIndex)
        {
            if (bands.Length == 0)
                throw new InvalidOperationException($"{name} has no difficulty bands configured.");

            ShelfLayoutConfig result = bands[0].layoutConfig;
            for (int i = 0; i < bands.Length; i++)
            {
                if (bands[i].minShelfIndex <= shelfIndex)
                    result = bands[i].layoutConfig;
                else
                    break;
            }
            return result;
        }
    }
}
