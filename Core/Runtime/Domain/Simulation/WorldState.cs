using Vivarium.Domain.Activities;
using Vivarium.Domain.Attention;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Events;
using Vivarium.Domain.Employment;
using Vivarium.Domain.Groups;
using Vivarium.Domain.History;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Simulation
{
    /// <summary>
    /// Current authoritative truth (§8).
    /// <para>
    /// Deliberately <b>not</b> one enormous mutable class: it is the conceptual aggregate of the
    /// authoritative repositories and indexes, each of which owns its own invariants.
    /// </para>
    /// <para>
    /// Ephemeral presentation state — camera rectangle, pointer hover, mini-game timing — does not
    /// belong here (§8, §29.6). Durable player-attention settings do.
    /// </para>
    /// <para>
    /// Exactly one owner mutates this, and only at the boundaries described by
    /// <see cref="SettlementLoop"/> (§13). Presentation reads projections published at quiescence,
    /// never this object mid-cascade (§13.1).
    /// </para>
    /// </summary>
    public sealed class WorldState
    {
        public WorldState(long worldSeed, SimTime startTime, RuntimeIdCounters restoredCounters = default)
        {
            WorldSeed = worldSeed;
            Clock = new SimClock(startTime);
            RuntimeIds = new RuntimeIdState(restoredCounters);
            Revisions = new RevisionRegistry();
            Scheduler = new Scheduler(RuntimeIds.ScheduledEvents, RuntimeIds.EventSequence);
            DomainEvents = new DomainEventQueue();

            Characters = new EntityRepository<CharacterId, Character>("Character");
            Activities = new EntityRepository<ActivityInstanceId, ActivityInstance>("ActivityInstance");
            Commitments = new EntityRepository<CommitmentId, Commitment>("Commitment");
            Relationships = new EntityRepository<RelationshipId, Relationship>("Relationship");
            Decisions = new EntityRepository<DecisionId, Decision>("Decision");
            Groups = new EntityRepository<GroupId, Group>("Group");
            Employments = new EntityRepository<EmploymentId, Employment.Employment>("Employment");

            Locations = new LocationHierarchy();
            TravelNetwork = new TravelNetwork();
            Spatial = new SpatialIndexes(Locations);
            Memberships = new MembershipIndex();
            EmploymentIndex = new EmploymentIndex();
            RelationshipIndex = new RelationshipIndex();
            DecisionDependencies = new DecisionDependencyIndex();
            CommitmentConflicts = new CommitmentConflictIndex();
            CommitmentOutcomes = new CommitmentOutcomeLedger();

            Knowledge = new KnowledgeLedger();
            Attention = new AttentionState();
            HistoryLedger = new HistoryLedger(RuntimeIds.HistoryEntries);
        }

        /// <summary>The seed every random stream is derived from (§14). Save state (§38).</summary>
        public long WorldSeed { get; }

        public SimClock Clock { get; }

        public RuntimeIdState RuntimeIds { get; }

        public RevisionRegistry Revisions { get; }

        public Scheduler Scheduler { get; }

        public DomainEventQueue DomainEvents { get; }

        public EntityRepository<CharacterId, Character> Characters { get; }

        public EntityRepository<ActivityInstanceId, ActivityInstance> Activities { get; }

        public EntityRepository<CommitmentId, Commitment> Commitments { get; }

        public EntityRepository<RelationshipId, Relationship> Relationships { get; }

        public EntityRepository<DecisionId, Decision> Decisions { get; }

        public EntityRepository<GroupId, Group> Groups { get; }

        public EntityRepository<EmploymentId, Employment.Employment> Employments { get; }

        /// <summary>Containment hierarchy (§27).</summary>
        public LocationHierarchy Locations { get; }

        /// <summary>Navigability, separate from containment (§28).</summary>
        public TravelNetwork TravelNetwork { get; }

        /// <summary>Occupancy indexes derived from Activity spatial contexts. Rebuildable (§30, §40).</summary>
        public SpatialIndexes Spatial { get; }

        /// <summary>Non-spatial group membership (§31). Rebuildable.</summary>
        public MembershipIndex Memberships { get; }

        public EmploymentIndex EmploymentIndex { get; }

        /// <summary>Relationship lookups. Rebuildable.</summary>
        public RelationshipIndex RelationshipIndex { get; }

        /// <summary>Targeted decision reevaluation (§17.2). Rebuildable.</summary>
        public DecisionDependencyIndex DecisionDependencies { get; }

        /// <summary>Active commitment-conflict identity projection. Rebuilt from Decisions after load.</summary>
        public CommitmentConflictIndex CommitmentConflicts { get; }

        public CommitmentOutcomeLedger CommitmentOutcomes { get; }

        /// <summary>What the player knows — not a view of truth (§22).</summary>
        public KnowledgeLedger Knowledge { get; }

        /// <summary>Attention and the canonical watch signal (§20).</summary>
        public AttentionState Attention { get; }

        /// <summary>Retained history with explicit retention tiers (§37).</summary>
        public HistoryLedger HistoryLedger { get; }

        /// <summary>The character's current primary Activity (§29.1).</summary>
        public bool TryGetCurrentActivity(CharacterId characterId, out ActivityInstance activity)
        {
            activity = null;
            return Characters.TryGet(characterId, out Character character)
                && character.CurrentActivityId.IsSet
                && Activities.TryGet(character.CurrentActivityId, out activity);
        }

        /// <summary>
        /// Where the character is, derived from their Activity — the only authoritative answer (§29.2).
        /// </summary>
        public bool TryGetSpatialContext(CharacterId characterId, out ActivitySpatialContext context)
        {
            if (TryGetCurrentActivity(characterId, out ActivityInstance activity))
            {
                context = activity.SpatialContext;
                return true;
            }

            context = default;
            return false;
        }

        /// <summary>Publishes a Domain Event into the current settlement cycle (§12.1).</summary>
        public void Publish(IDomainEvent domainEvent) => DomainEvents.Publish(domainEvent);

        /// <summary>Bumps an aspect-scoped revision (§11.2.1).</summary>
        public int BumpRevision(RevisionKey key) => Revisions.Bump(key);

        /// <summary>
        /// Rebuilds every derived index from canonical state. Run after load, before resuming (§40).
        /// </summary>
        public void RebuildDerivedIndexes()
        {
            Locations.RebuildCaches();
            Spatial.Clear();
            RelationshipIndex.Clear();
            DecisionDependencies.Clear();
            CommitmentConflicts.Clear();
            EmploymentIndex.Clear();

            foreach (Character character in Characters.All)
            {
                if (!character.IsActive || !character.CurrentActivityId.IsSet)
                {
                    continue;
                }

                if (Activities.TryGet(character.CurrentActivityId, out ActivityInstance activity))
                {
                    Spatial.ApplyTransition(character.Id, null, activity.SpatialContext);
                }
            }

            foreach (Relationship relationship in Relationships.All)
            {
                if (relationship.IsActive)
                {
                    RelationshipIndex.Register(relationship);
                }
            }

            foreach (Employment.Employment employment in Employments.All)
            {
                EmploymentIndex.Register(employment);
            }

            foreach (Decision decision in Decisions.All)
            {
                if (decision.IsActive)
                {
                    DecisionDependencies.Register(decision);
                    CommitmentConflicts.Register(decision);
                }
            }

            Attention.ClearEphemeralWatchState();
        }
    }
}
