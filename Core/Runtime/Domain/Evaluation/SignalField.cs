using System;
using System.Collections.Generic;
using System.Numerics;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Evaluation
{
    public enum SignalApplicability
    {
        Known = 0,
        Uncertain = 1,
        Unknown = 2,
        NotApplicable = 3,
    }

    /// <summary>One semantic evaluator input; ignorance is never represented as a neutral zero.</summary>
    public readonly struct SignalValue
    {
        public SignalValue(
            AuthoredId signalId,
            long mean,
            long variance,
            SignalApplicability applicability,
            int sourceRevision = 0)
        {
            if (!signalId.IsSet) throw new ArgumentException("A signal value needs a stable id.", nameof(signalId));
            SignalId = signalId;
            Mean = mean;
            Variance = Math.Max(0, variance);
            Applicability = applicability;
            SourceRevision = sourceRevision;
        }

        public AuthoredId SignalId { get; }
        public long Mean { get; }
        public long Variance { get; }
        public SignalApplicability Applicability { get; }
        public int SourceRevision { get; }
        public bool CanEvaluate => Applicability == SignalApplicability.Known || Applicability == SignalApplicability.Uncertain;
    }

    /// <summary>Deterministic fixed-point arithmetic shared by semantic field evaluators.</summary>
    public static class SignalNumeric
    {
        public const long Scale = 10000;
        public const long MaxVariance = Scale * Scale;

        public static long Multiply(long left, long right) => DivideRounded((BigInteger)left * right, Scale);

        public static long MultiplyCovariance(long coefficient, long covariance) =>
            DivideRounded((BigInteger)coefficient * covariance, (BigInteger)Scale * Scale);

        public static long Square(long value) => Multiply(value, value);

        /// <summary>A deterministic bounded response in [-1, 1]: x / (1 + |x|).</summary>
        public static long BoundedResponse(long score)
        {
            if (score == 0)
            {
                return 0;
            }

            BigInteger magnitude = BigInteger.Abs(new BigInteger(score));
            return ClampToLong(DivideRounded((BigInteger)score * Scale, Scale + magnitude), -Scale, Scale);
        }

        /// <summary>
        /// First derivative of <see cref="BoundedResponse"/>, represented in the same fixed-point scale.
        /// For g(x)=x/(1+|x|), g'(x)=1/(1+|x|)^2.
        /// </summary>
        public static long BoundedResponseDerivative(long score)
        {
            BigInteger denominator = Scale + BigInteger.Abs(new BigInteger(score));
            return ClampToLong(
                DivideRounded(BigInteger.Pow(Scale, 3), denominator * denominator),
                0,
                Scale);
        }

        public static long ApplyDerivativeToVariance(long variance, long derivative)
        {
            if (variance <= 0 || derivative <= 0)
            {
                return 0;
            }

            BigInteger scaled = (BigInteger)variance * derivative * derivative;
            return ClampToLong(DivideRounded(scaled, (BigInteger)Scale * Scale), 0, long.MaxValue);
        }

        public static long DivideRounded(BigInteger dividend, BigInteger divisor)
        {
            if (divisor <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(divisor), "The divisor must be positive.");
            }

            BigInteger quotient = dividend >= 0
                ? (dividend + (divisor / 2)) / divisor
                : -((-dividend + (divisor / 2)) / divisor);
            return ClampToLong(quotient, long.MinValue, long.MaxValue);
        }

        private static long ClampToLong(BigInteger value, long minimum, long maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return (long)value;
        }
    }

    public readonly struct SignalPair : IEquatable<SignalPair>, IComparable<SignalPair>
    {
        public SignalPair(AuthoredId first, AuthoredId second)
        {
            if (!first.IsSet || !second.IsSet)
            {
                throw new ArgumentException("Signal pairs require two stable signal ids.");
            }

            if (first.CompareTo(second) <= 0)
            {
                First = first;
                Second = second;
            }
            else
            {
                First = second;
                Second = first;
            }
        }

        public AuthoredId First { get; }
        public AuthoredId Second { get; }
        public bool IsDiagonal => First == Second;

        public bool Equals(SignalPair other) => First == other.First && Second == other.Second;
        public override bool Equals(object obj) => obj is SignalPair other && Equals(other);
        public override int GetHashCode() => (First.GetHashCode() * 397) ^ Second.GetHashCode();
        public int CompareTo(SignalPair other)
        {
            int first = First.CompareTo(other.First);
            return first != 0 ? first : Second.CompareTo(other.Second);
        }
    }

    /// <summary>Ordered signal means plus a sparse covariance matrix.</summary>
    public sealed class SignalVector
    {
        private readonly SortedDictionary<AuthoredId, long> _means = new SortedDictionary<AuthoredId, long>();
        private readonly SortedDictionary<SignalPair, long> _covariance = new SortedDictionary<SignalPair, long>();

        public IEnumerable<KeyValuePair<AuthoredId, long>> Means => _means;
        public IEnumerable<KeyValuePair<SignalPair, long>> CovarianceTerms => _covariance;

        public long Mean(AuthoredId signal) => _means.TryGetValue(signal, out long value) ? value : 0;
        public long Covariance(AuthoredId first, AuthoredId second) =>
            _covariance.TryGetValue(new SignalPair(first, second), out long value) ? value : 0;

        public void SetMean(AuthoredId signal, long value)
        {
            if (!signal.IsSet) throw new ArgumentException("A signal mean needs a stable id.", nameof(signal));
            _means[signal] = value;
        }

        public void SetCovariance(AuthoredId first, AuthoredId second, long value) =>
            _covariance[new SignalPair(first, second)] = value;

        public void Set(SignalValue value)
        {
            if (!value.CanEvaluate)
            {
                throw new InvalidOperationException($"{value.SignalId} is {value.Applicability} and cannot enter a numeric field.");
            }
            SetMean(value.SignalId, value.Mean);
            SetCovariance(value.SignalId, value.SignalId, value.Variance);
        }
    }

    public readonly struct SignalLinearTerm
    {
        public SignalLinearTerm(AuthoredId signal, long coefficient, AuthoredId provenance = default)
        {
            Signal = signal;
            Coefficient = coefficient;
            Provenance = provenance;
        }

        public AuthoredId Signal { get; }
        public long Coefficient { get; }
        public AuthoredId Provenance { get; }
    }

    /// <summary>One complete sparse contribution; off-diagonal terms are not mirrored by content.</summary>
    public readonly struct SignalPairwiseTerm
    {
        public SignalPairwiseTerm(AuthoredId first, AuthoredId second, long coefficient, AuthoredId provenance = default)
        {
            Pair = new SignalPair(first, second);
            Coefficient = coefficient;
            Provenance = provenance;
        }

        public SignalPair Pair { get; }
        public long Coefficient { get; }
        public AuthoredId Provenance { get; }
    }

    public sealed class SignalIdealFactor
    {
        public SignalIdealFactor(AuthoredId id, IReadOnlyList<SignalLinearTerm> coefficients, AuthoredId provenance = default)
        {
            Id = id;
            Coefficients = coefficients ?? new SignalLinearTerm[0];
            Provenance = provenance;
        }

        public AuthoredId Id { get; }
        public IReadOnlyList<SignalLinearTerm> Coefficients { get; }
        public AuthoredId Provenance { get; }
    }

    /// <summary>Generic uncertain quadratic field. It knows nothing about Social or Decisions.</summary>
    public sealed class SignalFieldDefinition
    {
        public SignalFieldDefinition(
            AuthoredId id,
            long bias,
            IReadOnlyList<SignalLinearTerm> linearTerms,
            IReadOnlyList<SignalPairwiseTerm> pairwiseTerms,
            IReadOnlyDictionary<AuthoredId, long> idealPoint,
            IReadOnlyList<SignalIdealFactor> idealFactors,
            int revision = 0)
        {
            if (!id.IsSet) throw new ArgumentException("A signal field needs a stable id.", nameof(id));
            Id = id;
            Bias = bias;
            LinearTerms = linearTerms ?? new SignalLinearTerm[0];
            PairwiseTerms = pairwiseTerms ?? new SignalPairwiseTerm[0];
            IdealPoint = idealPoint ?? new SortedDictionary<AuthoredId, long>();
            IdealFactors = idealFactors ?? new SignalIdealFactor[0];
            Revision = revision;
        }

        public AuthoredId Id { get; }
        public long Bias { get; }
        public IReadOnlyList<SignalLinearTerm> LinearTerms { get; }
        public IReadOnlyList<SignalPairwiseTerm> PairwiseTerms { get; }
        public IReadOnlyDictionary<AuthoredId, long> IdealPoint { get; }
        public IReadOnlyList<SignalIdealFactor> IdealFactors { get; }
        public int Revision { get; }
    }

    public enum SignalContributionKind
    {
        Bias = 0,
        Linear = 1,
        Pairwise = 2,
        IdealTolerance = 3,
    }

    public readonly struct SignalContribution
    {
        public SignalContribution(SignalContributionKind kind, AuthoredId sourceId, long amount, string explanation)
        {
            Kind = kind;
            SourceId = sourceId;
            Amount = amount;
            Explanation = explanation;
        }

        public SignalContributionKind Kind { get; }
        public AuthoredId SourceId { get; }
        public long Amount { get; }
        public string Explanation { get; }
    }

    public sealed class SignalFieldEvaluation
    {
        public SignalFieldEvaluation(
            long pointLatentScore,
            long expectedLatentScore,
            long latentVariance,
            long expectedBoundedScore,
            long boundedVariance,
            IReadOnlyList<SignalContribution> contributions)
        {
            PointLatentScore = pointLatentScore;
            ExpectedLatentScore = expectedLatentScore;
            LatentVariance = latentVariance;
            ExpectedBoundedScore = expectedBoundedScore;
            BoundedVariance = boundedVariance;
            Contributions = contributions;
        }

        public long PointLatentScore { get; }
        public long ExpectedLatentScore { get; }
        public long LatentVariance { get; }
        public long ExpectedBoundedScore { get; }
        public long BoundedVariance { get; }
        public IReadOnlyList<SignalContribution> Contributions { get; }
    }

    /// <summary>Canonical deterministic evaluator for uncertain fixed-point signal fields.</summary>
    public sealed class SignalFieldEvaluator
    {
        public SignalFieldEvaluation Evaluate(SignalVector signals, SignalFieldDefinition definition)
        {
            if (signals == null || definition == null)
            {
                throw new ArgumentNullException("Signals and field definition are required.");
            }

            MergedField field = Merge(definition);
            var contributions = new List<SignalContribution>();
            long point = field.Bias;
            long expected = field.Bias;
            contributions.Add(new SignalContribution(SignalContributionKind.Bias, definition.Id, field.Bias, "authored baseline"));

            foreach (KeyValuePair<AuthoredId, long> term in field.Linear)
            {
                long amount = SignalNumeric.Multiply(term.Value, signals.Mean(term.Key));
                point = checked(point + amount);
                expected = checked(expected + amount);
                contributions.Add(new SignalContribution(
                    SignalContributionKind.Linear,
                    SourceId(field.LinearProvenance[term.Key], term.Key),
                    amount,
                    "linear " + term.Key.Value + ProvenanceText(field.LinearProvenance[term.Key])));
            }

            foreach (KeyValuePair<SignalPair, long> term in field.Pairwise)
            {
                long meanProduct = SignalNumeric.Multiply(signals.Mean(term.Key.First), signals.Mean(term.Key.Second));
                long pointAmount = SignalNumeric.Multiply(term.Value, meanProduct);
                long uncertaintyAmount = SignalNumeric.MultiplyCovariance(
                    term.Value,
                    signals.Covariance(term.Key.First, term.Key.Second));
                point = checked(point + pointAmount);
                expected = checked(expected + pointAmount + uncertaintyAmount);
                contributions.Add(new SignalContribution(
                    SignalContributionKind.Pairwise,
                    SourceId(field.PairwiseProvenance[term.Key], definition.Id),
                    pointAmount + uncertaintyAmount,
                    "pairwise " + term.Key.First.Value + "/" + term.Key.Second.Value +
                    ProvenanceText(field.PairwiseProvenance[term.Key])));
            }

            foreach (KeyValuePair<AuthoredId, SortedDictionary<AuthoredId, long>> factor in field.IdealFactors)
            {
                long projectedMean = 0;
                foreach (KeyValuePair<AuthoredId, long> coefficient in factor.Value)
                {
                    long centered = signals.Mean(coefficient.Key) - ValueOrZero(field.IdealPoint, coefficient.Key);
                    projectedMean = checked(projectedMean + SignalNumeric.Multiply(coefficient.Value, centered));
                }

                long pointPenalty = -(SignalNumeric.Square(projectedMean) / 2);
                long projectedVariance = 0;
                foreach (KeyValuePair<AuthoredId, long> left in factor.Value)
                {
                    foreach (KeyValuePair<AuthoredId, long> right in factor.Value)
                    {
                        long coefficientProduct = SignalNumeric.Multiply(left.Value, right.Value);
                        projectedVariance = checked(projectedVariance + SignalNumeric.MultiplyCovariance(
                            coefficientProduct,
                            signals.Covariance(left.Key, right.Key)));
                    }
                }

                long expectedPenalty = pointPenalty - (projectedVariance / 2);
                point = checked(point + pointPenalty);
                expected = checked(expected + expectedPenalty);
                contributions.Add(new SignalContribution(
                    SignalContributionKind.IdealTolerance,
                    SourceId(field.FactorProvenance[factor.Key], factor.Key),
                    expectedPenalty,
                    "distance from preferred region" + ProvenanceText(field.FactorProvenance[factor.Key])));
            }

            long latentVariance = CalculateLatentVariance(signals, field);
            long bounded = SignalNumeric.BoundedResponse(expected);
            long derivative = SignalNumeric.BoundedResponseDerivative(expected);
            long boundedVariance = SignalNumeric.ApplyDerivativeToVariance(latentVariance, derivative);
            return new SignalFieldEvaluation(point, expected, latentVariance, bounded, boundedVariance, contributions);
        }

        private static long CalculateLatentVariance(SignalVector signals, MergedField field)
        {
            var keys = new SortedSet<AuthoredId>();
            foreach (KeyValuePair<AuthoredId, long> term in field.Linear) keys.Add(term.Key);
            foreach (KeyValuePair<SignalPair, long> term in field.Pairwise)
            {
                keys.Add(term.Key.First);
                keys.Add(term.Key.Second);
            }
            foreach (KeyValuePair<AuthoredId, SortedDictionary<AuthoredId, long>> factor in field.IdealFactors)
            {
                foreach (KeyValuePair<AuthoredId, long> coefficient in factor.Value) keys.Add(coefficient.Key);
            }

            var ordered = new List<AuthoredId>(keys);
            int count = ordered.Count;
            if (count == 0) return 0;

            var index = new Dictionary<AuthoredId, int>();
            for (int i = 0; i < count; i++) index.Add(ordered[i], i);

            var p = new long[count, count];
            foreach (KeyValuePair<AuthoredId, SortedDictionary<AuthoredId, long>> factor in field.IdealFactors)
            {
                foreach (KeyValuePair<AuthoredId, long> left in factor.Value)
                {
                    foreach (KeyValuePair<AuthoredId, long> right in factor.Value)
                    {
                        int l = index[left.Key];
                        int r = index[right.Key];
                        p[l, r] = checked(p[l, r] + SignalNumeric.Multiply(left.Value, right.Value));
                    }
                }
            }

            // B = 2A = 2Q - P. Off-diagonal authored pair coefficients already represent the
            // complete x_i*x_j contribution, so B_ij receives that coefficient in both directions.
            var b = new long[count, count];
            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count; j++) b[i, j] = -p[i, j];
            }
            foreach (KeyValuePair<SignalPair, long> term in field.Pairwise)
            {
                int first = index[term.Key.First];
                int second = index[term.Key.Second];
                if (first == second)
                {
                    b[first, first] = checked(b[first, first] + (2 * term.Value));
                }
                else
                {
                    b[first, second] = checked(b[first, second] + term.Value);
                    b[second, first] = checked(b[second, first] + term.Value);
                }
            }

            var gradient = new long[count];
            for (int i = 0; i < count; i++)
            {
                long value = field.Linear.TryGetValue(ordered[i], out long linear) ? linear : 0;
                for (int j = 0; j < count; j++)
                {
                    long ideal = ValueOrZero(field.IdealPoint, ordered[j]);
                    value = checked(value + SignalNumeric.Multiply(p[i, j], ideal));
                    value = checked(value + SignalNumeric.Multiply(b[i, j], signals.Mean(ordered[j])));
                }
                gradient[i] = value;
            }

            BigInteger gradientVariance = 0;
            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count; j++)
                {
                    gradientVariance += (BigInteger)gradient[i] * signals.Covariance(ordered[i], ordered[j]) * gradient[j];
                }
            }
            long gradientTerm = SignalNumeric.DivideRounded(
                gradientVariance,
                (BigInteger)SignalNumeric.Scale * SignalNumeric.Scale);

            var bSigma = new BigInteger[count, count];
            for (int i = 0; i < count; i++)
            {
                for (int k = 0; k < count; k++)
                {
                    BigInteger value = 0;
                    for (int j = 0; j < count; j++)
                    {
                        value += (BigInteger)b[i, j] * signals.Covariance(ordered[j], ordered[k]);
                    }
                    bSigma[i, k] = value;
                }
            }

            BigInteger trace = 0;
            for (int i = 0; i < count; i++)
            {
                for (int k = 0; k < count; k++) trace += bSigma[i, k] * bSigma[k, i];
            }
            BigInteger traceDivisor = 2 * BigInteger.Pow(SignalNumeric.Scale, 4);
            long quadraticTerm = SignalNumeric.DivideRounded(trace, traceDivisor);
            return Math.Max(0, checked(gradientTerm + quadraticTerm));
        }

        private static MergedField Merge(SignalFieldDefinition definition)
        {
            var field = new MergedField { Bias = definition.Bias };
            foreach (KeyValuePair<AuthoredId, long> ideal in definition.IdealPoint) field.IdealPoint[ideal.Key] = ideal.Value;
            for (int i = 0; i < definition.LinearTerms.Count; i++)
            {
                SignalLinearTerm term = definition.LinearTerms[i];
                field.Linear[term.Signal] = field.Linear.TryGetValue(term.Signal, out long current)
                    ? checked(current + term.Coefficient)
                    : term.Coefficient;
                AddProvenance(field.LinearProvenance, term.Signal, term.Provenance);
            }
            for (int i = 0; i < definition.PairwiseTerms.Count; i++)
            {
                SignalPairwiseTerm term = definition.PairwiseTerms[i];
                field.Pairwise[term.Pair] = field.Pairwise.TryGetValue(term.Pair, out long current)
                    ? checked(current + term.Coefficient)
                    : term.Coefficient;
                AddProvenance(field.PairwiseProvenance, term.Pair, term.Provenance);
            }
            for (int i = 0; i < definition.IdealFactors.Count; i++)
            {
                SignalIdealFactor factor = definition.IdealFactors[i];
                if (!field.IdealFactors.TryGetValue(factor.Id, out SortedDictionary<AuthoredId, long> coefficients))
                {
                    coefficients = new SortedDictionary<AuthoredId, long>();
                    field.IdealFactors.Add(factor.Id, coefficients);
                }
                AddProvenance(field.FactorProvenance, factor.Id, factor.Provenance);
                for (int c = 0; c < factor.Coefficients.Count; c++)
                {
                    SignalLinearTerm coefficient = factor.Coefficients[c];
                    coefficients[coefficient.Signal] = coefficients.TryGetValue(coefficient.Signal, out long current)
                        ? checked(current + coefficient.Coefficient)
                        : coefficient.Coefficient;
                }
            }
            return field;
        }

        private static long ValueOrZero(IReadOnlyDictionary<AuthoredId, long> values, AuthoredId key) =>
            values.TryGetValue(key, out long value) ? value : 0;

        private static void AddProvenance<TKey>(SortedDictionary<TKey, SortedSet<AuthoredId>> target, TKey key, AuthoredId source)
        {
            if (!target.TryGetValue(key, out SortedSet<AuthoredId> sources))
            {
                sources = new SortedSet<AuthoredId>();
                target.Add(key, sources);
            }
            if (source.IsSet) sources.Add(source);
        }

        private static AuthoredId SourceId(SortedSet<AuthoredId> sources, AuthoredId fallback)
        {
            foreach (AuthoredId source in sources) return source;
            return fallback;
        }

        private static string ProvenanceText(SortedSet<AuthoredId> sources) =>
            sources.Count == 0 ? string.Empty : " [" + string.Join(", ", sources) + "]";

        private sealed class MergedField
        {
            public long Bias;
            public readonly SortedDictionary<AuthoredId, long> Linear = new SortedDictionary<AuthoredId, long>();
            public readonly SortedDictionary<AuthoredId, SortedSet<AuthoredId>> LinearProvenance = new SortedDictionary<AuthoredId, SortedSet<AuthoredId>>();
            public readonly SortedDictionary<SignalPair, long> Pairwise = new SortedDictionary<SignalPair, long>();
            public readonly SortedDictionary<SignalPair, SortedSet<AuthoredId>> PairwiseProvenance = new SortedDictionary<SignalPair, SortedSet<AuthoredId>>();
            public readonly SortedDictionary<AuthoredId, long> IdealPoint = new SortedDictionary<AuthoredId, long>();
            public readonly SortedDictionary<AuthoredId, SortedDictionary<AuthoredId, long>> IdealFactors = new SortedDictionary<AuthoredId, SortedDictionary<AuthoredId, long>>();
            public readonly SortedDictionary<AuthoredId, SortedSet<AuthoredId>> FactorProvenance = new SortedDictionary<AuthoredId, SortedSet<AuthoredId>>();
        }
    }
}
