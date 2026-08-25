using System;

namespace Vivarium.Domain.Decisions
{
    /// <summary>
    /// A die expressing the weight of an influence: <c>Ambition d10</c>, <c>Better Pay d6</c> (§17).
    /// <para>
    /// Dice live on a fixed ladder so player interventions can step an influence up or down by one
    /// rung deterministically (§19), rather than nudging an arbitrary number.
    /// </para>
    /// </summary>
    public readonly struct Die : IEquatable<Die>, IComparable<Die>
    {
        /// <summary>The sanctioned ladder, ascending. Stepping moves between adjacent rungs.</summary>
        public static readonly int[] Ladder = { 2, 4, 6, 8, 10, 12, 20 };

        public static readonly Die None = new Die(0);

        public Die(int sides, int fixedResult = 0)
        {
            if (sides < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sides), "A die cannot have negative faces.");
            }

            if (fixedResult < 0 || (fixedResult > 0 && (sides == 0 || fixedResult > sides)))
            {
                throw new ArgumentOutOfRangeException(nameof(fixedResult), "A fixed result must be a face on the die.");
            }

            Sides = sides;
            FixedResult = fixedResult;
        }

        public int Sides { get; }

        /// <summary>Zero for an ordinary uniform die; otherwise this authored die always lands here.</summary>
        public int FixedResult { get; }

        public bool IsFixed => FixedResult > 0;

        public bool IsSet => Sides > 0;

        public static Die D4 => new Die(4);

        public static Die D6 => new Die(6);

        public static Die D8 => new Die(8);

        public static Die D10 => new Die(10);

        public static Die D12 => new Die(12);

        public static Die D20 => new Die(20);

        /// <summary>The next rung up, or this die if already at the top.</summary>
        public Die StepUp()
        {
            for (int i = 0; i < Ladder.Length; i++)
            {
                if (Ladder[i] > Sides)
                {
                    return new Die(Ladder[i]);
                }
            }

            return this;
        }

        /// <summary>The next rung down, or this die if already at the bottom.</summary>
        public Die StepDown()
        {
            for (int i = Ladder.Length - 1; i >= 0; i--)
            {
                if (Ladder[i] < Sides)
                {
                    return new Die(Ladder[i]);
                }
            }

            return this;
        }

        public bool Equals(Die other) => Sides == other.Sides && FixedResult == other.FixedResult;

        public override bool Equals(object obj) => obj is Die other && Equals(other);

        public override int GetHashCode() => (Sides * 397) ^ FixedResult;

        public int CompareTo(Die other)
        {
            int bySides = Sides.CompareTo(other.Sides);
            return bySides != 0 ? bySides : FixedResult.CompareTo(other.FixedResult);
        }

        public override string ToString() => IsSet ? (IsFixed ? $"d{Sides}={FixedResult}" : "d" + Sides) : "-";

        public static bool operator ==(Die a, Die b) => a.Equals(b);

        public static bool operator !=(Die a, Die b) => !a.Equals(b);
    }
}
