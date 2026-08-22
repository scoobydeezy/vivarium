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

        public Die(int sides)
        {
            if (sides < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sides), "A die cannot have negative faces.");
            }

            Sides = sides;
        }

        public int Sides { get; }

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

        public bool Equals(Die other) => Sides == other.Sides;

        public override bool Equals(object obj) => obj is Die other && Equals(other);

        public override int GetHashCode() => Sides;

        public int CompareTo(Die other) => Sides.CompareTo(other.Sides);

        public override string ToString() => IsSet ? "d" + Sides : "-";

        public static bool operator ==(Die a, Die b) => a.Sides == b.Sides;

        public static bool operator !=(Die a, Die b) => a.Sides != b.Sides;
    }
}
