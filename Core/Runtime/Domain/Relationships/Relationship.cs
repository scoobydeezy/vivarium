using System;
using Vivarium.Domain.Common;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Relationships
{
    /// <summary>
    /// A standing relationship between two characters.
    /// <para>
    /// Endpoints are stored in canonical (low, high) id order so a pair maps to exactly one
    /// relationship regardless of which side asks. Relationship formulas themselves are deferred
    /// content (§57); what is fixed here is integral state (§16), analytical drift instead of ticking
    /// (§10), and aspect-scoped revisions (§11.2.1).
    /// </para>
    /// </summary>
    public sealed class Relationship
    {
        public Relationship(
            RelationshipId id,
            CharacterId a,
            CharacterId b,
            AuthoredId kind,
            AnalyticalProgression affinity,
            SimTime establishedAt)
        {
            if (!id.IsSet)
            {
                throw new ArgumentException("A relationship needs an allocated runtime id (§7).", nameof(id));
            }

            if (a == b)
            {
                throw new ArgumentException("A relationship needs two distinct characters.", nameof(b));
            }

            Id = id;
            LowCharacterId = a.Value < b.Value ? a : b;
            HighCharacterId = a.Value < b.Value ? b : a;
            Kind = kind;
            Affinity = affinity;
            EstablishedAt = establishedAt;
            IsActive = true;
        }

        public RelationshipId Id { get; }

        public CharacterId LowCharacterId { get; }

        public CharacterId HighCharacterId { get; }

        /// <summary>Authored kind, e.g. <c>relationship.friend</c> or <c>relationship.spouse</c>.</summary>
        public AuthoredId Kind { get; private set; }

        /// <summary>
        /// Affinity in the range −10,000..10,000 (§16), progressing analytically so gradual opinion
        /// drift costs no events (§10.1).
        /// </summary>
        public AnalyticalProgression Affinity { get; private set; }

        /// <summary>How well they know each other, 0–10,000.</summary>
        public int Familiarity { get; private set; }

        public SimTime EstablishedAt { get; }

        public SimTime? LastInteractionAt { get; private set; }

        /// <summary>
        /// False once dissolved. The id stays valid: Knowledge and Legacy history may still refer to
        /// this relationship long after it ends (§7.1).
        /// </summary>
        public bool IsActive { get; private set; }

        public RevisionKey RevisionKey => new RevisionKey(Id.ToRef(), RevisionAspects.Relationship);

        public bool Involves(CharacterId character) => LowCharacterId == character || HighCharacterId == character;

        /// <summary>The other party, given one of them.</summary>
        public CharacterId Other(CharacterId character)
        {
            if (character == LowCharacterId)
            {
                return HighCharacterId;
            }

            if (character == HighCharacterId)
            {
                return LowCharacterId;
            }

            throw new ArgumentException($"{character} is not part of relationship {Id}.", nameof(character));
        }

        public long AffinityAt(SimTime at) => Affinity.ValueAt(at);

        /// <summary>
        /// Applies an interaction outcome. Materializes affinity at <paramref name="at"/> before
        /// changing it, per the analytical-progression update sequence (§10.1).
        /// </summary>
        public void RecordInteraction(SimTime at, long affinityDelta, int familiarityDelta)
        {
            Affinity = Affinity.WithOffset(at, affinityDelta);
            Familiarity = IntegerMath.Clamp(Familiarity + familiarityDelta, 0, 10000);
            LastInteractionAt = at;
        }

        /// <summary>Changes the ongoing drift rate — for example after moving in together or falling out.</summary>
        public void SetAffinityDrift(SimTime at, long ratePerMinuteNumerator, long ratePerMinuteDenominator = 1)
        {
            Affinity = Affinity.WithRate(at, ratePerMinuteNumerator, ratePerMinuteDenominator);
        }

        /// <summary>Restores saved state (§38).</summary>
        public void RestoreState(int familiarity, SimTime? lastInteractionAt, bool isActive)
        {
            Familiarity = familiarity;
            LastInteractionAt = lastInteractionAt;
            IsActive = isActive;
        }

        public void Reclassify(AuthoredId kind) => Kind = kind;

        public void Dissolve() => IsActive = false;

        public override string ToString() => $"{Kind} {LowCharacterId}↔{HighCharacterId} ({Id})";
    }
}
