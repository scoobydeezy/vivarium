using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Social
{
    /// <summary>
    /// One joint linear measurement of latent personality: y = hᵀx + noise. A behavioral action may
    /// carry several such measurements when the author intends them to be conditionally independent.
    /// </summary>
    public sealed class SocialEvidenceMeasurement
    {
        public SocialEvidenceMeasurement(
            AuthoredId id,
            IReadOnlyList<SocialLinearTerm> projection,
            long observedValue,
            long noiseVariance)
        {
            if (!id.IsSet)
            {
                throw new ArgumentException("An evidence measurement needs a stable id.", nameof(id));
            }
            if (noiseVariance <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(noiseVariance), "Evidence noise must be positive.");
            }

            Id = id;
            Projection = projection ?? throw new ArgumentNullException(nameof(projection));
            ObservedValue = SocialNumeric.Coordinate(observedValue);
            NoiseVariance = SocialNumeric.Variance(noiseVariance);
        }

        public AuthoredId Id { get; }
        public IReadOnlyList<SocialLinearTerm> Projection { get; }
        public long ObservedValue { get; }
        public long NoiseVariance { get; }
    }

    public sealed class SocialEvidenceDefinition
    {
        public SocialEvidenceDefinition(
            AuthoredId actionDefinitionId,
            IReadOnlyList<SocialEvidenceMeasurement> measurements,
            AuthoredId explanationId)
        {
            if (!actionDefinitionId.IsSet)
            {
                throw new ArgumentException("Social evidence needs a stable action id.", nameof(actionDefinitionId));
            }

            ActionDefinitionId = actionDefinitionId;
            Measurements = measurements ?? new SocialEvidenceMeasurement[0];
            ExplanationId = explanationId;
        }

        public AuthoredId ActionDefinitionId { get; }
        public IReadOnlyList<SocialEvidenceMeasurement> Measurements { get; }
        public AuthoredId ExplanationId { get; }
    }

    public readonly struct ObservedSocialEvidence
    {
        public ObservedSocialEvidence(
            CharacterId actorId,
            ObserverRef observer,
            AuthoredId actionDefinitionId,
            SimTime observedAt,
            AuthoredId sourceContext)
        {
            ActorId = actorId;
            Observer = observer;
            ActionDefinitionId = actionDefinitionId;
            ObservedAt = observedAt;
            SourceContext = sourceContext;
        }

        public CharacterId ActorId { get; }
        public ObserverRef Observer { get; }
        public AuthoredId ActionDefinitionId { get; }
        public SimTime ObservedAt { get; }
        public AuthoredId SourceContext { get; }
    }

    /// <summary>Deterministic scalar Kalman updates over the joint latent vector.</summary>
    public sealed class SocialBeliefUpdateService
    {
        public BeliefDistribution Apply(
            WorldState world,
            ObservedSocialEvidence evidence,
            SocialEvidenceDefinition definition)
        {
            if (world == null || definition == null)
            {
                throw new ArgumentNullException("World and evidence definition are required.");
            }
            if (evidence.ActionDefinitionId != definition.ActionDefinitionId)
            {
                throw new InvalidOperationException(
                    $"Evidence {evidence.ActionDefinitionId} cannot use definition {definition.ActionDefinitionId}.");
            }
            if (!world.Characters.TryGet(evidence.ActorId, out Characters.Character target) || !target.IsActive)
            {
                throw new InvalidOperationException($"Social evidence target {evidence.ActorId} is not active.");
            }
            if (evidence.Observer.IsCharacter &&
                (!world.Characters.TryGet(evidence.Observer.CharacterId, out Characters.Character observer) || !observer.IsActive))
            {
                throw new InvalidOperationException($"Social observer {evidence.Observer} is not active.");
            }

            if (!world.Knowledge.TryGetSocialBelief(evidence.Observer, evidence.ActorId, out BeliefDistribution belief))
            {
                belief = BroadPrior();
                world.Knowledge.SetSocialBelief(evidence.Observer, evidence.ActorId, belief, evidence.ObservedAt);
            }

            for (int i = 0; i < definition.Measurements.Count; i++)
            {
                ApplyMeasurement(belief, definition.Measurements[i]);
            }

            belief.MarkEvidenceApplied();
            world.Knowledge.TouchSocialBelief(evidence.Observer, evidence.ActorId, evidence.ObservedAt);
            EntityRef revisionSubject = evidence.Observer.IsCharacter
                ? evidence.Observer.CharacterId.ToRef()
                : evidence.ActorId.ToRef();
            world.BumpRevision(new RevisionKey(
                revisionSubject,
                RevisionAspects.Scoped(RevisionAspects.SocialBelief, new AuthoredId("target." + evidence.ActorId.Value))));
            world.Publish(new SocialBeliefChangedEvent(evidence.Observer, evidence.ActorId, belief.EvidenceRevision));
            return belief;
        }

        public static BeliefDistribution BroadPrior()
        {
            var belief = new BeliefDistribution(new SocialVector());
            for (int i = 0; i < SocialDimensions.Provisional.Count; i++)
            {
                AuthoredId dimension = SocialDimensions.Provisional[i];
                belief.SetCovariance(dimension, dimension, SocialNumeric.MaxVariance);
            }
            return belief;
        }

        private static void ApplyMeasurement(BeliefDistribution belief, SocialEvidenceMeasurement measurement)
        {
            var h = new SortedDictionary<AuthoredId, long>();
            for (int i = 0; i < measurement.Projection.Count; i++)
            {
                SocialLinearTerm term = measurement.Projection[i];
                h[term.Dimension] = h.TryGetValue(term.Dimension, out long current)
                    ? checked(current + term.Coefficient)
                    : term.Coefficient;
            }

            long projectedMean = 0;
            foreach (KeyValuePair<AuthoredId, long> coefficient in h)
            {
                projectedMean = checked(projectedMean + SocialNumeric.Multiply(
                    coefficient.Value,
                    belief.Mean[coefficient.Key]));
            }

            var dimensions = new List<AuthoredId>(SocialDimensions.Provisional);
            foreach (AuthoredId dimension in h.Keys)
            {
                if (!dimensions.Contains(dimension))
                {
                    dimensions.Add(dimension);
                }
            }
            dimensions.Sort();

            var crossCovariance = new SortedDictionary<AuthoredId, long>();
            for (int i = 0; i < dimensions.Count; i++)
            {
                long cross = 0;
                foreach (KeyValuePair<AuthoredId, long> coefficient in h)
                {
                    cross = checked(cross + SocialNumeric.DivideRounded(
                        checked(belief.Covariance(dimensions[i], coefficient.Key) * coefficient.Value),
                        SocialNumeric.Scale));
                }
                crossCovariance.Add(dimensions[i], cross);
            }

            long innovationVariance = measurement.NoiseVariance;
            foreach (KeyValuePair<AuthoredId, long> coefficient in h)
            {
                innovationVariance = checked(innovationVariance + SocialNumeric.DivideRounded(
                    checked(crossCovariance[coefficient.Key] * coefficient.Value),
                    SocialNumeric.Scale));
            }
            if (innovationVariance <= 0)
            {
                throw new InvalidOperationException($"Evidence measurement {measurement.Id} produced non-positive innovation variance.");
            }

            long innovation = measurement.ObservedValue - projectedMean;
            var gains = new SortedDictionary<AuthoredId, long>();
            for (int i = 0; i < dimensions.Count; i++)
            {
                AuthoredId dimension = dimensions[i];
                long gain = SocialNumeric.DivideRounded(
                    checked(crossCovariance[dimension] * SocialNumeric.Scale),
                    innovationVariance);
                gains.Add(dimension, gain);
                belief.Mean.Set(
                    dimension,
                    belief.Mean[dimension] + SocialNumeric.Multiply(gain, innovation));
            }

            // P' = P - K(HP), applied from the captured pre-update cross covariance.
            for (int i = 0; i < dimensions.Count; i++)
            {
                for (int j = i; j < dimensions.Count; j++)
                {
                    AuthoredId left = dimensions[i];
                    AuthoredId right = dimensions[j];
                    long reduction = SocialNumeric.DivideRounded(
                        checked(gains[left] * crossCovariance[right]),
                        SocialNumeric.Scale);
                    belief.SetCovariance(left, right, belief.Covariance(left, right) - reduction);
                }
            }

            // A large contradiction is information that the current model was overconfident, not a
            // reason to become even more certain. Inflate measured diagonals deterministically when
            // the residual exceeds two innovation standard deviations (innovation² > 4S).
            long squaredInnovation = checked(innovation * innovation);
            long surpriseThreshold = checked(innovationVariance * 4);
            if (squaredInnovation > surpriseThreshold)
            {
                long baseInflation = Math.Min(
                    SocialNumeric.MaxVariance / 4,
                    (squaredInnovation - surpriseThreshold) / 8);
                foreach (KeyValuePair<AuthoredId, long> coefficient in h)
                {
                    long weightedInflation = SocialNumeric.Multiply(
                        baseInflation,
                        Math.Abs(coefficient.Value));
                    belief.SetCovariance(
                        coefficient.Key,
                        coefficient.Key,
                        belief.Covariance(coefficient.Key, coefficient.Key) + weightedInflation);
                }
            }
        }
    }
}
