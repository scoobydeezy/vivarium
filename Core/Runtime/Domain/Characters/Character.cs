using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Characters
{
    /// <summary>
    /// An autonomous simulated person — the entity the whole game exists to make interesting (§1).
    /// <para>
    /// Not a property bag: a character enforces its own local invariants (§6). Cross-entity rules
    /// (who they can interact with, what they should do next, how a decision resolves) belong to
    /// domain services and simulation systems, not here.
    /// </para>
    /// <para>
    /// Note what is deliberately absent: no position, no spatial presence field. Where a character is
    /// derives from their current Activity's <c>SpatialContext</c> (§29.2, invariant 40).
    /// </para>
    /// </summary>
    public sealed class Character
    {
        private readonly SortedSet<AuthoredId> _traits = new SortedSet<AuthoredId>();
        private readonly SortedDictionary<AuthoredId, NeedState> _needs = new SortedDictionary<AuthoredId, NeedState>();

        public Character(CharacterId id, string displayName, SimTime createdAt)
        {
            if (!id.IsSet)
            {
                throw new ArgumentException("A character needs an allocated runtime id (§7).", nameof(id));
            }

            Id = id;
            DisplayName = displayName;
            CreatedAt = createdAt;
            IsActive = true;
        }

        public CharacterId Id { get; }

        public string DisplayName { get; }

        public SimTime CreatedAt { get; }

        /// <summary>
        /// False once the character leaves active simulation (death, departure). Their runtime id stays
        /// spent and remains referable from Knowledge and Legacy history (§7.1).
        /// </summary>
        public bool IsActive { get; private set; }

        public SimTime? RetiredAt { get; private set; }

        /// <summary>
        /// The character's single authoritative primary Activity (§29.1, invariant 39).
        /// <see cref="ActivityInstanceId.None"/> only during construction, before the first Activity
        /// is assigned.
        /// </summary>
        public ActivityInstanceId CurrentActivityId { get; private set; }

        /// <summary>Authored trait ids, ascending. Traits are content (§6, §41).</summary>
        public IReadOnlyCollection<AuthoredId> Traits => _traits;

        /// <summary>Need states keyed by authored need id, ascending.</summary>
        public IReadOnlyDictionary<AuthoredId, NeedState> Needs => _needs;

        public bool HasTrait(AuthoredId traitId) => _traits.Contains(traitId);

        public bool AddTrait(AuthoredId traitId)
        {
            if (!traitId.IsSet)
            {
                throw new ArgumentException("Traits are referenced by stable authored id (§39).", nameof(traitId));
            }

            return _traits.Add(traitId);
        }

        public bool RemoveTrait(AuthoredId traitId) => _traits.Remove(traitId);

        public void SetNeed(NeedState need) => _needs[need.NeedId] = need;

        public bool TryGetNeed(AuthoredId needId, out NeedState need) => _needs.TryGetValue(needId, out need);

        /// <summary>
        /// Points the character at their new primary Activity. Callers are responsible for the rest of
        /// the transition — retiring the previous instance, updating occupancy indexes, bumping the
        /// activity revision — which is why this stays package-visible to the Activities systems.
        /// </summary>
        public void SetCurrentActivity(ActivityInstanceId activityInstanceId)
        {
            if (!IsActive)
            {
                throw new InvalidOperationException($"{Id} is retired and cannot take on a new Activity.");
            }

            CurrentActivityId = activityInstanceId;
        }

        /// <summary>
        /// Restores saved lifecycle state (§38). Unlike <see cref="Retire"/> this keeps the recorded
        /// current Activity, and unlike <see cref="SetCurrentActivity"/> it works for retired characters
        /// — whose identity remains referable from Knowledge and Legacy history (§7.1).
        /// </summary>
        public void RestoreLifecycle(bool isActive, SimTime? retiredAt, ActivityInstanceId currentActivityId)
        {
            IsActive = isActive;
            RetiredAt = retiredAt;
            CurrentActivityId = currentActivityId;
        }

        public void Retire(SimTime at)
        {
            IsActive = false;
            RetiredAt = at;
            CurrentActivityId = ActivityInstanceId.None;
        }

        public override string ToString() => $"{DisplayName} ({Id})";
    }
}
