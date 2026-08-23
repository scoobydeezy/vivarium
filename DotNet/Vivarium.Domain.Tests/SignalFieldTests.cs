using System.Collections.Generic;
using System;
using Vivarium.Domain.Common;
using Vivarium.Domain.Evaluation;
using Xunit;

namespace Vivarium.Domain.Tests
{
    public sealed class SignalFieldTests
    {
        private static readonly AuthoredId FieldId = new AuthoredId("field.test");
        private static readonly AuthoredId A = new AuthoredId("signal.a");
        private static readonly AuthoredId B = new AuthoredId("signal.b");

        [Fact]
        public void LinearFieldPreservesMeanAndResidualVarianceInFixedPoint()
        {
            var signals = new SignalVector();
            signals.SetMean(A, 4000);
            signals.SetCovariance(A, A, 25000000);
            var field = new SignalFieldDefinition(
                FieldId,
                0,
                new[] { new SignalLinearTerm(A, 5000) },
                null,
                null,
                null);

            SignalFieldEvaluation result = new SignalFieldEvaluator().Evaluate(signals, field);

            Assert.Equal(2000, result.PointLatentScore);
            Assert.Equal(2000, result.ExpectedLatentScore);
            Assert.Equal(6250000, result.LatentVariance);
            Assert.Equal(SignalNumeric.BoundedResponse(2000), result.ExpectedBoundedScore);
            Assert.InRange(result.BoundedVariance, 1, result.LatentVariance - 1);
        }

        [Fact]
        public void PairwiseCovarianceChangesExpectationWithoutChangingPointEstimate()
        {
            SignalVector independent = Signals((A, 8000), (B, -1000));
            SignalVector correlated = Signals((A, 8000), (B, -1000));
            correlated.SetCovariance(A, B, 20000000);
            var field = new SignalFieldDefinition(
                FieldId,
                0,
                null,
                new[] { new SignalPairwiseTerm(A, B, -5000) },
                null,
                null);
            var evaluator = new SignalFieldEvaluator();

            SignalFieldEvaluation first = evaluator.Evaluate(independent, field);
            SignalFieldEvaluation second = evaluator.Evaluate(correlated, field);

            Assert.Equal(first.PointLatentScore, second.PointLatentScore);
            Assert.Equal(-1000, second.ExpectedLatentScore - first.ExpectedLatentScore);
            Assert.True(second.LatentVariance > 0);
        }

        [Fact]
        public void IdealFactorIncludesUncertaintyPenaltyAndVariance()
        {
            var signals = new SignalVector();
            signals.SetMean(A, 0);
            signals.SetCovariance(A, A, 25000000);
            var field = new SignalFieldDefinition(
                FieldId,
                0,
                null,
                null,
                new SortedDictionary<AuthoredId, long>(),
                new[]
                {
                    new SignalIdealFactor(
                        new AuthoredId("factor.a"),
                        new[] { new SignalLinearTerm(A, SignalNumeric.Scale) }),
                });

            SignalFieldEvaluation result = new SignalFieldEvaluator().Evaluate(signals, field);

            Assert.Equal(0, result.PointLatentScore);
            Assert.Equal(-1250, result.ExpectedLatentScore);
            Assert.True(result.LatentVariance > 0);
        }

        [Fact]
        public void BoundedDerivativeIsDeterministicAndDecreasesIntoSaturation()
        {
            Assert.Equal(SignalNumeric.Scale, SignalNumeric.BoundedResponseDerivative(0));
            Assert.True(SignalNumeric.BoundedResponseDerivative(5000) > SignalNumeric.BoundedResponseDerivative(20000));
            Assert.Equal(
                SignalNumeric.BoundedResponseDerivative(5000),
                SignalNumeric.BoundedResponseDerivative(-5000));
        }

        [Theory]
        [InlineData(SignalApplicability.Unknown)]
        [InlineData(SignalApplicability.NotApplicable)]
        public void MissingSemanticSignalCannotMasqueradeAsNeutral(SignalApplicability applicability)
        {
            var signals = new SignalVector();
            var value = new SignalValue(A, 0, 0, applicability);

            Assert.False(value.CanEvaluate);
            Assert.Throws<InvalidOperationException>(() => signals.Set(value));
        }

        private static SignalVector Signals(params (AuthoredId id, long mean)[] values)
        {
            var signals = new SignalVector();
            for (int i = 0; i < values.Length; i++) signals.SetMean(values[i].id, values[i].mean);
            return signals;
        }
    }
}
