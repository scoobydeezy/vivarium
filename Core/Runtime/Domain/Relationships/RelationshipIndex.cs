using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Relationships
{
    /// <summary>Canonical (low, high) character pair used to look a relationship up in one step.</summary>
    public readonly struct RelationshipPairKey : IEquatable<RelationshipPairKey>, IComparable<RelationshipPairKey>
    {
        public RelationshipPairKey(CharacterId a, CharacterId b)
        {
            Low = a.Value < b.Value ? a : b;
            High = a.Value < b.Value ? b : a;
        }

        public CharacterId Low { get; }

        public CharacterId High { get; }

        public bool Equals(RelationshipPairKey other) => Low == other.Low && High == other.High;

        public override bool Equals(object obj) => obj is RelationshipPairKey other && Equals(other);

        public override int GetHashCode() => (Low.Value * 397) ^ High.Value;

        public int CompareTo(RelationshipPairKey other)
        {
            int byLow = Low.CompareTo(other.Low);
            return byLow != 0 ? byLow : High.CompareTo(other.High);
        }

        public override string ToString() => $"{Low}↔{High}";
    }

    /// <summary>
    /// Lookup structures over relationships (§40 — rebuildable, not canonical).
    /// <para>
    /// Exists so relationship work scales with interactions actually selected rather than population
    /// size (§32): "do these two know each other?" is a dictionary hit, and "who does Mina know?" is
    /// O(her relationships).
    /// </para>
    /// </summary>
    public sealed class RelationshipIndex
    {
        private readonly Dictionary<RelationshipPairKey, RelationshipId> _byPair = new Dictionary<RelationshipPairKey, RelationshipId>();
        private readonly IndexedMembership<CharacterId, RelationshipId> _byCharacter = new IndexedMembership<CharacterId, RelationshipId>();
        private readonly IndexedMembership<CharacterId, CharacterId> _knownCharacters = new IndexedMembership<CharacterId, CharacterId>();

        public void Register(Relationship relationship)
        {
            if (relationship == null)
            {
                throw new ArgumentNullException(nameof(relationship));
            }

            var key = new RelationshipPairKey(relationship.LowCharacterId, relationship.HighCharacterId);
            _byPair[key] = relationship.Id;
            _byCharacter.Add(relationship.LowCharacterId, relationship.Id);
            _byCharacter.Add(relationship.HighCharacterId, relationship.Id);
            _knownCharacters.Add(relationship.LowCharacterId, relationship.HighCharacterId);
            _knownCharacters.Add(relationship.HighCharacterId, relationship.LowCharacterId);
        }

        public void Unregister(Relationship relationship)
        {
            _byPair.Remove(new RelationshipPairKey(relationship.LowCharacterId, relationship.HighCharacterId));
            _byCharacter.Remove(relationship.LowCharacterId, relationship.Id);
            _byCharacter.Remove(relationship.HighCharacterId, relationship.Id);
            _knownCharacters.Remove(relationship.LowCharacterId, relationship.HighCharacterId);
            _knownCharacters.Remove(relationship.HighCharacterId, relationship.LowCharacterId);
        }

        public bool TryGetBetween(CharacterId a, CharacterId b, out RelationshipId id) =>
            _byPair.TryGetValue(new RelationshipPairKey(a, b), out id);

        /// <summary>Relationships involving a character, ascending by relationship id.</summary>
        public IReadOnlyCollection<RelationshipId> Of(CharacterId character) => _byCharacter.MembersOf(character);

        /// <summary>Characters with a registered relationship to this character, ascending.</summary>
        public IReadOnlyCollection<CharacterId> KnownCharactersOf(CharacterId character) =>
            _knownCharacters.MembersOf(character);

        public void Clear()
        {
            _byPair.Clear();
            _byCharacter.Clear();
            _knownCharacters.Clear();
        }
    }
}
