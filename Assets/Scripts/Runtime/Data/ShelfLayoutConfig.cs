using UnityEngine;

namespace BookBound
{
    /// <summary>
    /// Describes how much of the fixed 40-slot board spawns already solved vs. misplaced.
    /// The remainder spawns empty and is dealt to the table.
    /// </summary>
    [CreateAssetMenu(menuName = "BookBound/Shelf Layout Config", fileName = "ShelfLayoutConfig")]
    public class ShelfLayoutConfig : ScriptableObject
    {
        [Range(0f, 1f)] public float alreadyCorrectMin = 0.5f;
        [Range(0f, 1f)] public float alreadyCorrectMax = 0.6f;
        [Range(0f, 1f)] public float misplacedMin = 0.1f;
        [Range(0f, 1f)] public float misplacedMax = 0.2f;
        public bool shuffleRowOrder;

        public void Validate()
        {
            if (alreadyCorrectMax < alreadyCorrectMin)
                throw new System.InvalidOperationException($"{name}: alreadyCorrectMax < alreadyCorrectMin.");
            if (misplacedMax < misplacedMin)
                throw new System.InvalidOperationException($"{name}: misplacedMax < misplacedMin.");
            if (alreadyCorrectMin + misplacedMin > 1f)
                throw new System.InvalidOperationException($"{name}: alreadyCorrectMin + misplacedMin exceeds 1.");
        }
    }
}
