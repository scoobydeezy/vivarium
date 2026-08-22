using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Decisions
{
    /// <summary>Lifecycle of a Decision (§37).</summary>
    public enum DecisionStatus
    {
        Active = 0,
        Resolved = 1,

        /// <summary>Its window passed without resolution.</summary>
        Expired = 2,

        /// <summary>Made irrelevant by world events before resolving.</summary>
        Superseded = 3,
    }

    /// <summary>
    /// A meaningful choice a character is facing — a persistent runtime entity, not a UI popup (§17).
    /// <para>
    /// <b>Living runtime state (§17.2).</b> Definition-derived semantics are snapshotted at
    /// construction so hot reload cannot rewrite an open decision's dice underneath the player (§42.1),
    /// while world-derived influences may be added, removed, or re-weighted right up until resolution.
    /// Those updates arrive through targeted Domain reactions found via
    /// <see cref="DecisionDependencyIndex"/> — never by polling every open Decision.
    /// </para>
    /// <para>
    /// A character may hold several of these at once, constrained only by
    /// <see cref="ConflictScope"/> (§17.1).
    /// </para>
    /// </summary>
    public sealed class Decision
    {
        private static readonly CharacterId[] NoParticipants = new CharacterId[0];

        private readonly List<DecisionInfluence> _influences = new List<DecisionInfluence>();
        private readonly List<AppliedIntervention> _interventions = new List<AppliedIntervention>();
        private readonly SortedSet<DecisionDependencyKey> _dependencyKeys = new SortedSet<DecisionDependencyKey>();
        private readonly SortedDictionary<AuthoredId, long> _snapshottedParameters = new SortedDictionary<AuthoredId, long>();
        private readonly DecisionOption[] _options;

        private int _nextInfluenceId;

        public Decision(
            DecisionId id,
            CharacterId characterId,
            AuthoredId definitionId,
            SimTime createdAt,
            SimTime resolveAt,
            IReadOnlyList<DecisionOption> options,
            DecisionConflictScope conflictScope = default,
            int importance = 0,
            IReadOnlyList<CharacterId> additionalParticipants = null)
        {
            if (!id.IsSet)
            {
                throw new ArgumentException("A Decision needs an allocated runtime id (§7).", nameof(id));
            }

            if (options == null || options.Count < 2)
            {
                throw new ArgumentException("A Decision needs at least two options to be a choice.", nameof(options));
            }

            Id = id;
            CharacterId = characterId;
            DefinitionId = definitionId;
            CreatedAt = createdAt;
            ResolveAt = resolveAt;
            ConflictScope = conflictScope;
            Importance = importance;
            AdditionalParticipants = additionalParticipants ?? NoParticipants;
            Status = DecisionStatus.Active;

            _options = new DecisionOption[options.Count];
            for (int i = 0; i < options.Count; i++)
            {
                _options[i] = options[i];
            }

            Array.Sort(_options, (a, b) => a.OrderIndex.CompareTo(b.OrderIndex));
        }

        public DecisionId Id { get; }

        /// <summary>The character whose choice this is.</summary>
        public CharacterId CharacterId { get; }

        public IReadOnlyList<CharacterId> AdditionalParticipants { get; }

        /// <summary>Authored definition id, e.g. <c>decision.job_offer</c>.</summary>
        public AuthoredId DefinitionId { get; }

        public SimTime CreatedAt { get; }

        /// <summary>When this resolves if nothing intervenes.</summary>
        public SimTime ResolveAt { get; private set; }

        public DecisionStatus Status { get; private set; }

        public IReadOnlyList<DecisionOption> Options => _options;

        /// <summary>
        /// The <b>true</b> influence set (§17). How much of it any given player sees is decided by the
        /// projection layer, from content policy plus Knowledge (§26).
        /// </summary>
        public IReadOnlyList<DecisionInfluence> Influences => _influences;

        /// <summary>
        /// Bumped whenever the influence set changes, so projections know to refresh at the next
        /// quiescent boundary (§17.2, §13.1).
        /// </summary>
        public int InfluenceRevision { get; private set; }

        public IReadOnlyList<AppliedIntervention> Interventions => _interventions;

        /// <summary>
        /// Exclusivity scope. While this Decision is unresolved, no mutually exclusive decision in the
        /// same scope is generated for this character (§17.1).
        /// </summary>
        public DecisionConflictScope ConflictScope { get; }

        /// <summary>Weight used by hold-overflow ordering (§20).</summary>
        public int Importance { get; }

        /// <summary>
        /// World contexts whose changes can alter this Decision's influences (§17.2). Registered in
        /// <see cref="DecisionDependencyIndex"/> so reevaluation is targeted, not global.
        /// </summary>
        public IReadOnlyCollection<DecisionDependencyKey> DependencyKeys => _dependencyKeys;

        /// <summary>
        /// Definition-derived values captured at construction (§42.1). Only outcome-affecting values
        /// need snapshotting — not whole definition objects.
        /// </summary>
        public IReadOnlyDictionary<AuthoredId, long> SnapshottedParameters => _snapshottedParameters;

        public DecisionResolution Resolution { get; private set; }

        /// <summary>The scheduled resolution event, retained so it can be cancelled or moved (§11.1).</summary>
        public ScheduledEventId PendingResolveEventId { get; private set; }

        public bool IsActive => Status == DecisionStatus.Active;

        /// <summary>Revision key protecting this Decision's influence set (§11.2.1).</summary>
        public RevisionKey InfluenceRevisionKey => new RevisionKey(Id.ToRef(), RevisionAspects.DecisionInfluence);

        public void SnapshotParameter(AuthoredId key, long value) => _snapshottedParameters[key] = value;

        public long SnapshottedParameterOr(AuthoredId key, long fallback) =>
            _snapshottedParameters.TryGetValue(key, out long value) ? value : fallback;

        public void RegisterDependency(DecisionDependencyKey key)
        {
            if (key.IsSet)
            {
                _dependencyKeys.Add(key);
            }
        }

        /// <summary>
        /// Adds a world-derived influence, allocating a stable id within this Decision (§17.2).
        /// Legal while active — a new apartment opening beside Mina's job should be able to change an
        /// open decision.
        /// </summary>
        public DecisionInfluence AddInfluence(
            AuthoredId optionId,
            AuthoredId category,
            AuthoredId labelId,
            Die die,
            InfluenceVisibility defaultVisibility,
            DecisionDependencyKey dependencyKey = default,
            EntityRef subject = default)
        {
            RequireActive();

            var influence = new DecisionInfluence(
                new DecisionInfluenceId(++_nextInfluenceId),
                optionId,
                category,
                labelId,
                die,
                defaultVisibility,
                dependencyKey,
                subject);

            _influences.Add(influence);
            RegisterDependency(dependencyKey);
            InfluenceRevision++;
            return influence;
        }

        public bool TryGetInfluence(DecisionInfluenceId id, out DecisionInfluence influence)
        {
            for (int i = 0; i < _influences.Count; i++)
            {
                if (_influences[i].Id == id)
                {
                    influence = _influences[i];
                    return true;
                }
            }

            influence = null;
            return false;
        }

        /// <summary>Applies a world-driven change to an influence and bumps the revision.</summary>
        public bool ChangeInfluenceDie(DecisionInfluenceId id, Die die)
        {
            if (!TryGetInfluence(id, out DecisionInfluence influence))
            {
                return false;
            }

            influence.SetDie(die);
            InfluenceRevision++;
            return true;
        }

        /// <summary>
        /// Retracts an influence the world no longer supports. Retained rather than deleted so an
        /// intervention already bound to its id stays explicable (§17.2).
        /// </summary>
        public bool RetractInfluence(DecisionInfluenceId id)
        {
            if (!TryGetInfluence(id, out DecisionInfluence influence) || influence.IsRetracted)
            {
                return false;
            }

            influence.Retract();
            InfluenceRevision++;
            return true;
        }

        /// <summary>
        /// Records a player intervention. Validation is <see cref="DecisionInterventionRules"/>'s job —
        /// the same evaluation the UI uses to decide whether the control is even enabled (§19).
        /// </summary>
        public void RecordIntervention(AppliedIntervention intervention)
        {
            RequireActive();
            _interventions.Add(intervention);
            InfluenceRevision++;
        }

        public bool HasInterventionTargeting(DecisionInfluenceId influenceId, AuthoredId interventionDefinitionId)
        {
            for (int i = 0; i < _interventions.Count; i++)
            {
                if (_interventions[i].TargetInfluenceId == influenceId &&
                    _interventions[i].InterventionDefinitionId == interventionDefinitionId)
                {
                    return true;
                }
            }

            return false;
        }

        public void SetPendingResolveEvent(ScheduledEventId eventId) => PendingResolveEventId = eventId;

        /// <summary>
        /// Reinstates an influence exactly as saved, including its stable id (§38).
        /// <para>
        /// Restoration must never re-run generation logic: an intervention recorded against
        /// <c>Influence#3</c> has to find the same <c>Influence#3</c> after a reload (invariant 37).
        /// </para>
        /// </summary>
        public DecisionInfluence RestoreInfluence(
            DecisionInfluenceId id,
            AuthoredId optionId,
            AuthoredId category,
            AuthoredId labelId,
            Die baseDie,
            Die currentDie,
            InfluenceVisibility visibility,
            int rollIndex,
            bool isRetracted,
            DecisionDependencyKey dependencyKey = default,
            EntityRef subject = default)
        {
            var influence = new DecisionInfluence(id, optionId, category, labelId, baseDie, visibility, dependencyKey, subject);
            influence.SetDie(currentDie);

            for (int i = 0; i < rollIndex; i++)
            {
                influence.Reroll();
            }

            if (isRetracted)
            {
                influence.Retract();
            }

            _influences.Add(influence);
            RegisterDependency(dependencyKey);

            if (id.Value > _nextInfluenceId)
            {
                _nextInfluenceId = id.Value;
            }

            return influence;
        }

        /// <summary>Restores the saved influence revision, rather than counting restore operations (§38).</summary>
        public void RestoreInfluenceRevision(int revision) => InfluenceRevision = revision;

        /// <summary>Restores a previously applied intervention without re-validating it (§38).</summary>
        public void RestoreIntervention(AppliedIntervention intervention) => _interventions.Add(intervention);

        /// <summary>Restores lifecycle state, including a resolution for an already-decided Decision.</summary>
        public void RestoreStatus(DecisionStatus status, DecisionResolution resolution = null)
        {
            Status = status;
            Resolution = resolution;
        }

        public void Defer(SimTime newResolveAt)
        {
            RequireActive();
            ResolveAt = newResolveAt;
        }

        public void Resolve(DecisionResolution resolution)
        {
            RequireActive();
            Resolution = resolution ?? throw new ArgumentNullException(nameof(resolution));
            Status = DecisionStatus.Resolved;
        }

        public void Expire() => Status = DecisionStatus.Expired;

        public void Supersede() => Status = DecisionStatus.Superseded;

        public override string ToString() => $"{DefinitionId} for {CharacterId} ({Status}, {_influences.Count} influences)";

        private void RequireActive()
        {
            if (Status != DecisionStatus.Active)
            {
                throw new InvalidOperationException($"Decision {Id} is {Status} and can no longer change.");
            }
        }
    }
}
