using System;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Social
{
    /// <summary>
    /// Deterministic fixed-point arithmetic for authoritative social evaluation. One whole unit is
    /// represented by 10,000, matching the rest of Vivarium's integral simulation state (§16).
    /// </summary>
    public static class SocialNumeric
    {
        public const long Scale = 10000;
        public const long MinCoordinate = -Scale;
        public const long MaxCoordinate = Scale;
        public const long MaxVariance = Scale * Scale;

        public static long Multiply(long left, long right) =>
            DivideRounded(left * right, Scale);

        /// <summary>Multiplies a fixed-point coefficient by a covariance value (whose scale is Scale²).</summary>
        public static long MultiplyCovariance(long coefficient, long covariance) =>
            DivideRounded(coefficient * covariance, Scale * Scale);

        public static long Square(long value) => Multiply(value, value);

        /// <summary>
        /// A deterministic bounded response in [-1, 1]: x / (1 + |x|). It avoids platform-sensitive
        /// floating point while retaining a smooth monotonic saturation curve.
        /// </summary>
        public static long BoundedResponse(long score)
        {
            if (score == 0)
            {
                return 0;
            }

            long magnitude = score == long.MinValue ? long.MaxValue : Math.Abs(score);
            long denominator = checked(Scale + magnitude);
            long bounded = DivideRounded(checked(score * Scale), denominator);
            return IntegerMath.Clamp(bounded, -Scale, Scale);
        }

        public static long DivideRounded(long dividend, long divisor)
        {
            if (divisor <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(divisor), "The divisor must be positive.");
            }

            if (dividend >= 0)
            {
                return (dividend + (divisor / 2)) / divisor;
            }

            return -((-dividend + (divisor / 2)) / divisor);
        }

        public static long Coordinate(long value) => IntegerMath.Clamp(value, MinCoordinate, MaxCoordinate);

        public static long Variance(long value) => IntegerMath.Clamp(value, 0, MaxVariance);
    }
}
