using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Relationships
{
    /// <summary>
    /// A standing relationship between two characters.
    /// <para>
    /// Endpoints are stored in canonical (low, high) id order so a pair maps to exactly one
    /// relationship regardless of which side asks. All durable social state is held by the two
    /// directional halves; the pair itself is identity, kind, lifecycle, and indexing only.
    /// </para>
    /// </summary>
    public sealed class Relationship
    {
        public Relationship(
            RelationshipId id,
            CharacterId a,
            CharacterId b,
            AuthoredId kind,
            AnalyticalProgression initialAffection,
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
            EstablishedAt = establishedAt;
            LowToHigh = new DirectionalRelationshipState(LowCharacterId, HighCharacterId, establishedAt);
            HighToLow = new DirectionalRelationshipState(HighCharacterId, LowCharacterId, establishedAt);
            LowToHigh.SetChannel(RelationshipChannels.Affection, initialAffection);
            HighToLow.SetChannel(RelationshipChannels.Affection, initialAffection);
            IsActive = true;
        }

        public RelationshipId Id { get; }

        public CharacterId LowCharacterId { get; }

        public CharacterId HighCharacterId { get; }

        /// <summary>Authored kind, e.g. <c>relationship.friend</c> or <c>relationship.spouse</c>.</summary>
        public AuthoredId Kind { get; private set; }

        public SimTime EstablishedAt { get; }

        public DirectionalRelationshipState LowToHigh { get; }

        public DirectionalRelationshipState HighToLow { get; }

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

        public DirectionalRelationshipState From(CharacterId observer)
        {
            if (observer == LowCharacterId)
            {
                return LowToHigh;
            }
            if (observer == HighCharacterId)
            {
                return HighToLow;
            }

            throw new ArgumentException($"{observer} is not part of relationship {Id}.", nameof(observer));
        }

        /// <summary>
        /// Applies a symmetric interaction outcome to both directional halves. Symmetric cause does
        /// not create an undirected score: later evidence and consequences may change either side.
        /// </summary>
        public void RecordInteraction(SimTime at, long affectionDelta, int familiarityDelta)
        {
            LastInteractionAt = at;
            LowToHigh.ApplyChannelDelta(RelationshipChannels.Affection, at, affectionDelta);
            HighToLow.ApplyChannelDelta(RelationshipChannels.Affection, at, affectionDelta);
            LowToHigh.RecordExposure(at, 1, familiarityDelta);
            HighToLow.RecordExposure(at, 1, familiarityDelta);
        }

        public void RecordDirectionalInteraction(
            CharacterId observer,
            SimTime at,
            IReadOnlyDictionary<AuthoredId, long> channelDeltas,
            long exposureMinutes,
            int familiarityDelta,
            RelationshipMemory memory = null)
        {
            DirectionalRelationshipState direction = From(observer);
            if (channelDeltas != null)
            {
                foreach (KeyValuePair<AuthoredId, long> delta in channelDeltas)
                {
                    direction.ApplyChannelDelta(delta.Key, at, delta.Value);
                }
            }
            direction.RecordExposure(at, exposureMinutes, familiarityDelta);
            if (memory != null)
            {
                direction.AddMemory(memory);
            }
            LastInteractionAt = at;
        }

        /// <summary>Changes affection drift in both directions for a genuinely symmetric cause.</summary>
        public void SetAffectionDrift(SimTime at, long ratePerMinuteNumerator, long ratePerMinuteDenominator = 1)
        {
            LowToHigh.SetChannelDrift(RelationshipChannels.Affection, at, ratePerMinuteNumerator, ratePerMinuteDenominator);
            HighToLow.SetChannelDrift(RelationshipChannels.Affection, at, ratePerMinuteNumerator, ratePerMinuteDenominator);
        }

        /// <summary>Restores saved state (§38).</summary>
        public void RestoreState(SimTime? lastInteractionAt, bool isActive)
        {
            LastInteractionAt = lastInteractionAt;
            IsActive = isActive;
        }

        public void Reclassify(AuthoredId kind) => Kind = kind;

        public void Dissolve() => IsActive = false;

        public override string ToString() => $"{Kind} {LowCharacterId}↔{HighCharacterId} ({Id})";
    }
}
