using System;
using Vivarium.Domain.Common;
using Vivarium.Domain.Evaluation;

namespace Vivarium.Domain.Social
{
    /// <summary>
    /// Deterministic fixed-point arithmetic for authoritative social evaluation. One whole unit is
    /// represented by 10,000, matching the rest of Vivarium's integral simulation state (§16).
    /// </summary>
    public static class SocialNumeric
    {
        public const long Scale = SignalNumeric.Scale;
        public const long MinCoordinate = -Scale;
        public const long MaxCoordinate = Scale;
        public const long MaxVariance = SignalNumeric.MaxVariance;

        public static long Multiply(long left, long right) => SignalNumeric.Multiply(left, right);

        /// <summary>Multiplies a fixed-point coefficient by a covariance value (whose scale is Scale²).</summary>
        public static long MultiplyCovariance(long coefficient, long covariance) =>
            SignalNumeric.MultiplyCovariance(coefficient, covariance);

        public static long Square(long value) => SignalNumeric.Square(value);

        /// <summary>
        /// A deterministic bounded response in [-1, 1]: x / (1 + |x|). It avoids platform-sensitive
        /// floating point while retaining a smooth monotonic saturation curve.
        /// </summary>
        public static long BoundedResponse(long score) => SignalNumeric.BoundedResponse(score);

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
