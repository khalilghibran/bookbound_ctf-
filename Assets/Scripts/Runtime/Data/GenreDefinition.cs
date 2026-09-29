using UnityEngine;

namespace BookBound
{
    /// <summary>One of the four fixed genres. Exactly four assets exist; no unlockables.</summary>
    [CreateAssetMenu(menuName = "BookBound/Genre Definition", fileName = "GenreDefinition")]
    public class GenreDefinition : ScriptableObject
    {
        public const int BooksPerGenre = 10;

        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private Color accentColor = Color.white;

        [Tooltip("Reserved for future row-label art. Leave null until real label art is wired in.")]
        [SerializeField] private Sprite labelSprite;

        [Tooltip("Index 0..9 -> sequence numbers 1..10. Flat/laying, table view.")]
        [SerializeField] private Sprite[] coverSprites = new Sprite[BooksPerGenre];

        [Tooltip("Index 0..9 -> sequence numbers 1..10. Upright, shelved view.")]
        [SerializeField] private Sprite[] spineSprites = new Sprite[BooksPerGenre];

        public string Id => id;
        public string DisplayName => displayName;
        public Color AccentColor => accentColor;
        public Sprite LabelSprite => labelSprite;

        public Sprite GetCoverSprite(int sequenceNumber) => coverSprites[SequenceToIndex(sequenceNumber)];
        public Sprite GetSpineSprite(int sequenceNumber) => spineSprites[SequenceToIndex(sequenceNumber)];

        private static int SequenceToIndex(int sequenceNumber)
        {
            if (sequenceNumber < 1 || sequenceNumber > BooksPerGenre)
            {
                throw new System.ArgumentOutOfRangeException(nameof(sequenceNumber), sequenceNumber,
                    $"Sequence number must be 1..{BooksPerGenre}.");
            }
            return sequenceNumber - 1;
        }
    }
}
