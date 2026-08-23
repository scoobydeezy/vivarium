using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Social
{
    /// <summary>
    /// An observer's uncertain belief over a target's latent personality. Covariance is sparse and
    /// symmetric; missing diagonal entries mean maximally unknown, while missing off-diagonal entries
    /// mean no represented correlation.
    /// </summary>
    public sealed class BeliefDistribution
    {
        private readonly SortedDictionary<SocialDimensionPair, long> _covariance =
            new SortedDictionary<SocialDimensionPair, long>();

        public BeliefDistribution(SocialVector mean, int evidenceRevision = 0)
        {
            Mean = mean?.Copy() ?? throw new ArgumentNullException(nameof(mean));
            EvidenceRevision = evidenceRevision;
        }

        public SocialVector Mean { get; }
        public int EvidenceRevision { get; private set; }
        public IEnumerable<KeyValuePair<SocialDimensionPair, long>> CovarianceTerms => _covariance;

        public long Covariance(AuthoredId first, AuthoredId second)
        {
            var pair = new SocialDimensionPair(first, second);
            if (_covariance.TryGetValue(pair, out long value))
            {
                return value;
            }

            return pair.IsDiagonal ? SocialNumeric.MaxVariance : 0;
        }

        public void SetCovariance(AuthoredId first, AuthoredId second, long value)
        {
            var pair = new SocialDimensionPair(first, second);
            long bounded = pair.IsDiagonal
                ? SocialNumeric.Variance(value)
                : IntegerMath.Clamp(value, -SocialNumeric.MaxVariance, SocialNumeric.MaxVariance);
            _covariance[pair] = bounded;
        }

        public void MarkEvidenceApplied() => EvidenceRevision++;

        public BeliefDistribution Copy()
        {
            var copy = new BeliefDistribution(Mean, EvidenceRevision);
            foreach (KeyValuePair<SocialDimensionPair, long> pair in _covariance)
            {
                copy._covariance.Add(pair.Key, pair.Value);
            }

            return copy;
        }
    }
}
