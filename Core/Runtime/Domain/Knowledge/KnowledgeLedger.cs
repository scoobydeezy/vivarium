using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Social;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Knowledge
{
    /// <summary>
    /// Everything an observer (the player or a character) has observed or learned (§22, §23, §32.1).
    /// <para>
    /// World truth lives in domain entities, observer belief lives here, and presentation lives in read
    /// models. Character observers use the same fact/provenance rules without granting the player access.
    /// </para>
    /// <para>
    /// Authoritative save state (§38): it cannot be rebuilt from truth, because it is precisely a
    /// record of what truth <i>used to look like</i> from the player's side.
    /// </para>
    /// </summary>
    public sealed class KnowledgeLedger
    {
        private readonly SortedDictionary<ObserverFactKey, KnowledgeEntry> _entries =
            new SortedDictionary<ObserverFactKey, KnowledgeEntry>();
        private readonly SortedDictionary<SocialBeliefKey, BeliefDistribution> _socialBeliefs =
            new SortedDictionary<SocialBeliefKey, BeliefDistribution>();
        private readonly SortedDictionary<SocialBeliefKey, SocialBeliefMetadata> _socialBeliefMetadata =
            new SortedDictionary<SocialBeliefKey, SocialBeliefMetadata>();
        private int _playerEntryCount;

        /// <summary>Player fact count retained for existing player-facing queries.</summary>
        public int Count => _playerEntryCount;
        public int AllObserverCount => _entries.Count;

        /// <summary>Whether the player knows anything at all about this fact.</summary>
        public bool Knows(FactKey key) => Knows(ObserverRef.Player, key);

        public bool Knows(ObserverRef observer, FactKey key) => _entries.ContainsKey(new ObserverFactKey(observer, key));

        public bool TryGet(FactKey key, out KnowledgeEntry entry) => TryGet(ObserverRef.Player, key, out entry);

        public bool TryGet(ObserverRef observer, FactKey key, out KnowledgeEntry entry) =>
            _entries.TryGetValue(new ObserverFactKey(observer, key), out entry);

        /// <summary>
        /// Records an observation, replacing any earlier entry for the same fact. Overwriting is
        /// correct: the ledger holds what the player currently believes, and history of belief changes
        /// belongs to History (§37) if it is ever needed.
        /// </summary>
        public void Record(KnowledgeEntry entry)
        {
            var key = new ObserverFactKey(entry.Observer, entry.Key);
            if (entry.Observer.IsPlayer && !_entries.ContainsKey(key))
            {
                _playerEntryCount++;
            }
            _entries[key] = entry;
        }

        public bool Forget(FactKey key) => Forget(ObserverRef.Player, key);

        public bool Forget(ObserverRef observer, FactKey key)
        {
            bool removed = _entries.Remove(new ObserverFactKey(observer, key));
            if (removed && observer.IsPlayer)
            {
                _playerEntryCount--;
            }
            return removed;
        }

        /// <summary>All entries in deterministic fact-key order.</summary>
        public IEnumerable<KnowledgeEntry> All
        {
            get
            {
                foreach (KeyValuePair<ObserverFactKey, KnowledgeEntry> pair in _entries)
                {
                    if (pair.Key.Observer.IsPlayer)
                    {
                        yield return pair.Value;
                    }
                }
            }
        }

        public IEnumerable<KnowledgeEntry> AllObservers => _entries.Values;

        /// <summary>Entries about one subject — the backbone of knowledge-filtered projections (§35).</summary>
        public IEnumerable<KnowledgeEntry> About(EntityRef subject)
        {
            foreach (KeyValuePair<ObserverFactKey, KnowledgeEntry> pair in _entries)
            {
                if (pair.Key.Observer.IsPlayer && pair.Key.Fact.Subject.Equals(subject))
                {
                    yield return pair.Value;
                }
            }
        }

        public IEnumerable<KnowledgeEntry> About(ObserverRef observer, EntityRef subject)
        {
            foreach (KeyValuePair<ObserverFactKey, KnowledgeEntry> pair in _entries)
            {
                if (pair.Key.Observer.Equals(observer) && pair.Key.Fact.Subject.Equals(subject))
                {
                    yield return pair.Value;
                }
            }
        }

        public bool TryGetSocialBelief(ObserverRef observer, CharacterId target, out BeliefDistribution belief) =>
            _socialBeliefs.TryGetValue(new SocialBeliefKey(observer, target), out belief);

        public void SetSocialBelief(
            ObserverRef observer,
            CharacterId target,
            BeliefDistribution belief,
            SimTime updatedAt = default,
            SocialBeliefRetention retention = SocialBeliefRetention.Active)
        {
            if (observer.IsCharacter && observer.CharacterId == target)
            {
                throw new System.ArgumentException("A character does not need a sparse belief edge to itself.", nameof(target));
            }

            var key = new SocialBeliefKey(observer, target);
            _socialBeliefs[key] = belief ?? throw new System.ArgumentNullException(nameof(belief));
            _socialBeliefMetadata[key] = new SocialBeliefMetadata(retention, updatedAt);
        }

        public bool ForgetSocialBelief(ObserverRef observer, CharacterId target)
        {
            var key = new SocialBeliefKey(observer, target);
            _socialBeliefMetadata.Remove(key);
            return _socialBeliefs.Remove(key);
        }

        public void TouchSocialBelief(ObserverRef observer, CharacterId target, SimTime at)
        {
            var key = new SocialBeliefKey(observer, target);
            if (_socialBeliefs.ContainsKey(key))
            {
                _socialBeliefMetadata[key] = new SocialBeliefMetadata(SocialBeliefRetention.Active, at);
            }
        }

        public SocialBeliefMetadata SocialBeliefMetadataOf(SocialBeliefKey key) =>
            _socialBeliefMetadata.TryGetValue(key, out SocialBeliefMetadata metadata)
                ? metadata
                : new SocialBeliefMetadata(SocialBeliefRetention.Active, default);

        public int PruneSocialBeliefsOlderThan(SimTime cutoff)
        {
            var remove = new List<SocialBeliefKey>();
            foreach (KeyValuePair<SocialBeliefKey, SocialBeliefMetadata> pair in _socialBeliefMetadata)
            {
                if (pair.Value.Retention == SocialBeliefRetention.Recent && pair.Value.LastUpdatedAt < cutoff)
                {
                    remove.Add(pair.Key);
                }
            }
            for (int i = 0; i < remove.Count; i++)
            {
                _socialBeliefs.Remove(remove[i]);
                _socialBeliefMetadata.Remove(remove[i]);
            }
            return remove.Count;
        }

        public IEnumerable<KeyValuePair<SocialBeliefKey, BeliefDistribution>> SocialBeliefs => _socialBeliefs;
    }

    public enum SocialBeliefRetention
    {
        Active = 0,
        Recent = 1,
        Significant = 2,
        Legacy = 3,
    }

    public readonly struct SocialBeliefMetadata
    {
        public SocialBeliefMetadata(SocialBeliefRetention retention, SimTime lastUpdatedAt)
        {
            Retention = retention;
            LastUpdatedAt = lastUpdatedAt;
        }

        public SocialBeliefRetention Retention { get; }
        public SimTime LastUpdatedAt { get; }
    }

    public readonly struct SocialBeliefKey : System.IEquatable<SocialBeliefKey>, System.IComparable<SocialBeliefKey>
    {
        public SocialBeliefKey(ObserverRef observer, CharacterId target)
        {
            if (!target.IsSet)
            {
                throw new System.ArgumentException("A social belief needs a target.", nameof(target));
            }
            Observer = observer;
            Target = target;
        }

        public ObserverRef Observer { get; }
        public CharacterId Target { get; }
        public bool Equals(SocialBeliefKey other) => Observer.Equals(other.Observer) && Target == other.Target;
        public override bool Equals(object obj) => obj is SocialBeliefKey other && Equals(other);
        public override int GetHashCode() => (Observer.GetHashCode() * 397) ^ Target.GetHashCode();
        public int CompareTo(SocialBeliefKey other)
        {
            int observer = Observer.CompareTo(other.Observer);
            return observer != 0 ? observer : Target.CompareTo(other.Target);
        }
    }
}
