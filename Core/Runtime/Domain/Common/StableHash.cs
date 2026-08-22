namespace Vivarium.Domain.Common
{
    /// <summary>
    /// Explicitly fixed, deterministic hashing and mixing primitives (Architecture §14).
    /// <para>
    /// Nothing in this class may ever change behaviour without bumping
    /// <see cref="Vivarium.Domain.Randomness.RandomAlgorithmVersion"/>, because saved worlds
    /// reproduce their futures through these functions.
    /// </para>
    /// </summary>
    public static class StableHash
    {
        private const ulong FnvOffsetBasis = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        /// <summary>Golden-ratio odd constant used by the SplitMix64 finaliser.</summary>
        public const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;

        /// <summary>
        /// FNV-1a over UTF-16 code units. Chosen for being trivially reimplementable and
        /// independent of framework version, culture, and randomised string hashing.
        /// </summary>
        public static ulong OfString(string value)
        {
            if (value == null)
            {
                return FnvOffsetBasis;
            }

            ulong hash = FnvOffsetBasis;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                hash ^= (byte)(c & 0xFF);
                hash *= FnvPrime;
                hash ^= (byte)((c >> 8) & 0xFF);
                hash *= FnvPrime;
            }

            return hash;
        }

        /// <summary>Mixes a 64-bit value into an accumulator. Order-sensitive by design.</summary>
        public static ulong Combine(ulong accumulator, ulong value)
        {
            return Avalanche(accumulator ^ (value + GoldenGamma + (accumulator << 6) + (accumulator >> 2)));
        }

        public static ulong Combine(ulong accumulator, long value) => Combine(accumulator, unchecked((ulong)value));

        public static ulong Combine(ulong accumulator, int value) => Combine(accumulator, unchecked((ulong)(long)value));

        public static ulong Combine(ulong accumulator, string value) => Combine(accumulator, OfString(value));

        public static ulong Combine(ulong accumulator, AuthoredId value) => Combine(accumulator, value.StableHashCode);

        /// <summary>The SplitMix64 finaliser: a fixed, well-distributed 64-bit bit mixer.</summary>
        public static ulong Avalanche(ulong x)
        {
            unchecked
            {
                x ^= x >> 30;
                x *= 0xBF58476D1CE4E5B9UL;
                x ^= x >> 27;
                x *= 0x94D049BB133111EBUL;
                x ^= x >> 31;
                return x;
            }
        }
    }
}
