using System;

namespace BookBound
{
    /// <summary>A single book: which genre, which sequence number (1..10). Value equality on (genre.id, sequenceNumber).</summary>
    [Serializable]
    public struct BookData : IEquatable<BookData>
    {
        public GenreDefinition genre;
        public int sequenceNumber;

        public BookData(GenreDefinition genre, int sequenceNumber)
        {
            this.genre = genre;
            this.sequenceNumber = sequenceNumber;
        }

        public bool Equals(BookData other)
        {
            if (sequenceNumber != other.sequenceNumber) return false;
            if (ReferenceEquals(genre, other.genre)) return true;
            if (genre == null || other.genre == null) return false;
            return genre.Id == other.genre.Id;
        }

        public override bool Equals(object obj) => obj is BookData other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = sequenceNumber;
                hash = (hash * 397) ^ (genre != null ? genre.Id?.GetHashCode() ?? 0 : 0);
                return hash;
            }
        }

        public static bool operator ==(BookData a, BookData b) => a.Equals(b);
        public static bool operator !=(BookData a, BookData b) => !a.Equals(b);

        public override string ToString() => genre != null ? $"{genre.Id}#{sequenceNumber:00}" : $"<null>#{sequenceNumber:00}";
    }
}
