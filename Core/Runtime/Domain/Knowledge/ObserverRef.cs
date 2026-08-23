using System;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Knowledge
{
    public enum ObserverKind
    {
        Player = 0,
        Character = 1,
    }

    /// <summary>Identifies either the player or a character as the holder of Knowledge.</summary>
    public readonly struct ObserverRef : IEquatable<ObserverRef>, IComparable<ObserverRef>
    {
        public static readonly ObserverRef Player = new ObserverRef(ObserverKind.Player, CharacterId.None);

        public ObserverRef(ObserverKind kind, CharacterId characterId)
        {
            if (kind == ObserverKind.Character && !characterId.IsSet)
            {
                throw new ArgumentException("A character observer needs a character id.", nameof(characterId));
            }
            if (kind == ObserverKind.Player && characterId.IsSet)
            {
                throw new ArgumentException("The player observer does not carry a character id.", nameof(characterId));
            }

            Kind = kind;
            CharacterId = characterId;
        }

        public ObserverKind Kind { get; }
        public CharacterId CharacterId { get; }
        public bool IsPlayer => Kind == ObserverKind.Player;
        public bool IsCharacter => Kind == ObserverKind.Character;

        public static ObserverRef Character(CharacterId id) => new ObserverRef(ObserverKind.Character, id);

        public bool Equals(ObserverRef other) => Kind == other.Kind && CharacterId == other.CharacterId;
        public override bool Equals(object obj) => obj is ObserverRef other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ CharacterId.GetHashCode();
        public int CompareTo(ObserverRef other)
        {
            int kind = Kind.CompareTo(other.Kind);
            return kind != 0 ? kind : CharacterId.CompareTo(other.CharacterId);
        }

        public override string ToString() => IsPlayer ? "Player" : CharacterId.ToString();
    }

    public readonly struct ObserverFactKey : IEquatable<ObserverFactKey>, IComparable<ObserverFactKey>
    {
        public ObserverFactKey(ObserverRef observer, FactKey fact)
        {
            Observer = observer;
            Fact = fact;
        }

        public ObserverRef Observer { get; }
        public FactKey Fact { get; }
        public bool Equals(ObserverFactKey other) => Observer.Equals(other.Observer) && Fact.Equals(other.Fact);
        public override bool Equals(object obj) => obj is ObserverFactKey other && Equals(other);
        public override int GetHashCode() => (Observer.GetHashCode() * 397) ^ Fact.GetHashCode();
        public int CompareTo(ObserverFactKey other)
        {
            int observer = Observer.CompareTo(other.Observer);
            return observer != 0 ? observer : Fact.CompareTo(other.Fact);
        }
    }
}
