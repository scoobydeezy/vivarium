namespace Vivarium.Domain.Common
{
    /// <summary>
    /// Sign-correct integer division helpers. Authoritative numeric state is integral (§16), and
    /// C#'s <c>/</c> truncates toward zero, which silently breaks monotonicity for negative values —
    /// so anything that can branch the simulation uses these instead.
    /// </summary>
    public static class IntegerMath
    {
        /// <summary>Division rounding toward negative infinity.</summary>
        public static long FloorDiv(long dividend, long divisor)
        {
            long quotient = dividend / divisor;
            long remainder = dividend % divisor;
            if (remainder != 0 && ((remainder < 0) != (divisor < 0)))
            {
                quotient--;
            }

            return quotient;
        }

        /// <summary>Division rounding toward positive infinity.</summary>
        public static long CeilDiv(long dividend, long divisor)
        {
            long quotient = dividend / divisor;
            long remainder = dividend % divisor;
            if (remainder != 0 && ((remainder < 0) == (divisor < 0)))
            {
                quotient++;
            }

            return quotient;
        }

        public static long Clamp(long value, long min, long max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
