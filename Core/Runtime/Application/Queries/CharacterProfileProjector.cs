using System.Collections.Generic;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Attention;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.History;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Time;

namespace Vivarium.Application.Queries
{
    public sealed class CharacterRosterProjector
    {
        private readonly DecisionFeedProjector _decisionFeed;

        public CharacterRosterProjector()
        {
        }

        public CharacterRosterProjector(
            DecisionImportancePolicyDefinition importance,
            DecisionHoldPolicy holds)
        {
            _decisionFeed = new DecisionFeedProjector(importance, holds);
        }

        public IReadOnlyList<CharacterRosterEntryView> Project(WorldState world)
        {
            var entries = new List<CharacterRosterEntryView>();
            var surfacedDecisions = new Dictionary<int, DecisionFeedEntryView>();
            if (_decisionFeed != null)
            {
                DecisionFeedView feed = _decisionFeed.Project(world);
                for (int i = 0; i < feed.Entries.Count; i++)
                {
                    DecisionFeedEntryView candidate = feed.Entries[i];
                    if (!surfacedDecisions.ContainsKey(candidate.CharacterId))
                    {
                        surfacedDecisions.Add(candidate.CharacterId, candidate);
                    }
                }
            }

            foreach (Character character in world.Characters.All)
            {
                WatchState watch = world.Attention.WatchStateOf(character.Id);
                string activityLabel = "Unknown";
                string locationLabel = "Not currently observed";
                if (watch.SupportsObservation && world.TryGetCurrentActivity(character.Id, out ActivityInstance activity))
                {
                    activityLabel = activity.DefinitionId.Value;
                    locationLabel = activity.SpatialContext.IsTraveling
                        ? LocationName(world, activity.SpatialContext.Transit.DestinationLocationId) + " (en route)"
                        : LocationName(world, activity.SpatialContext.LocationId);
                }

                bool needsAttention = surfacedDecisions.TryGetValue(character.Id.Value, out DecisionFeedEntryView decision);
                entries.Add(new CharacterRosterEntryView(
                    character.Id.Value,
                    character.DisplayName,
                    watch.IsFollowed,
                    activityLabel,
                    locationLabel,
                    world.Attention.PolicyFor(character.Id).ToString(),
                    needsAttention,
                    needsAttention && decision.IsHeld));
            }

            return entries;
        }

        private static string LocationName(WorldState world, LocationId locationId) =>
            world.Locations.TryGet(locationId, out LocationNode node) ? node.DisplayName : locationId.ToString();
    }

    /// <summary>
    /// Projects a character profile from truth filtered through player knowledge (§35, §36).
    /// <para>
    /// An on-demand projection: focused views like this are computed when asked for, rather than
    /// materialized for every character in the world (§36).
    /// </para>
    /// <para>
    /// Note the asymmetry — the character's <i>current activity and location</i> come from truth
    /// because the player can watch them happen, while traits and needs come only from the knowledge
    /// ledger. Whether that stays the right line is a content decision, but it is a decision made here
    /// rather than one the Domain leaks.
    /// </para>
    /// </summary>
    public sealed class CharacterProfileProjector
    {
        /// <summary>How stale an observation must be before the view flags it as possibly outdated.</summary>
        private static readonly SimDuration StalenessWindow = SimDuration.FromDays(1);
        private readonly ScheduleProjector _schedules = new ScheduleProjector();

        public bool TryProject(WorldState world, CharacterId characterId, out CharacterProfileView view)
        {
            view = null;

            if (!world.Characters.TryGet(characterId, out Character character))
            {
                return false;
            }

            string activityLabel = "unknown";
            string locationLabel = "unknown";
            bool isTraveling = false;
            string travelOriginLabel = null;
            int travelProgressBasisPoints = 0;

            if (world.TryGetCurrentActivity(characterId, out ActivityInstance activity))
            {
                activityLabel = activity.DefinitionId.Value;
                isTraveling = activity.SpatialContext.IsTraveling;

                if (isTraveling)
                {
                    travelOriginLabel = LocationName(world, activity.SpatialContext.Transit.OriginLocationId);
                    locationLabel = LocationName(world, activity.SpatialContext.Transit.DestinationLocationId) + " (en route)";
                    long totalMinutes = activity.SpatialContext.Transit.ArrivesAt
                        .Since(activity.SpatialContext.Transit.DepartedAt).TotalMinutes;
                    long elapsedMinutes = world.Clock.Now
                        .Since(activity.SpatialContext.Transit.DepartedAt).TotalMinutes;
                    travelProgressBasisPoints = totalMinutes <= 0
                        ? 10000
                        : (int)System.Math.Max(0, System.Math.Min(10000, elapsedMinutes * 10000 / totalMinutes));
                }
                else
                {
                    locationLabel = LocationName(world, activity.SpatialContext.LocationId);
                }
            }

            var traits = new List<KnownFactView>();
            var needs = new List<KnownFactView>();
            EntityRef subject = characterId.ToRef();

            foreach (KnowledgeEntry entry in world.Knowledge.About(subject))
            {
                var factView = new KnownFactView(
                    entry.Key.Qualifier.IsSet ? entry.Key.Qualifier.Value : entry.Key.Kind.Value,
                    entry.ObservedValue.ToString(),
                    entry.ObservedAt.ToString(),
                    entry.Confidence.ToString(),
                    world.Clock.Now.Since(entry.ObservedAt) > StalenessWindow);

                if (entry.Key.Kind == FactKinds.CharacterTrait)
                {
                    traits.Add(factView);
                }
                else if (entry.Key.Kind == FactKinds.CharacterNeed)
                {
                    needs.Add(factView);
                }
            }

            var relationships = new List<KnownRelationshipView>();
            foreach (Relationship relationship in world.Relationships.All)
            {
                if (!relationship.IsActive || !relationship.Involves(characterId)) continue;

                var knownFacts = new List<KnownFactView>();
                foreach (KnowledgeEntry entry in world.Knowledge.About(relationship.Id.ToRef()))
                {
                    if (entry.Key.Kind != FactKinds.RelationshipStanding &&
                        entry.Key.Kind != FactKinds.RelationshipResentment) continue;
                    knownFacts.Add(ToKnownFact(world, entry));
                }
                if (knownFacts.Count == 0) continue;

                CharacterId otherId = relationship.Other(characterId);
                string otherName = world.Characters.TryGet(otherId, out Character other)
                    ? other.DisplayName
                    : otherId.ToString();
                relationships.Add(new KnownRelationshipView(
                    relationship.Id.Value,
                    otherId.Value,
                    otherName,
                    knownFacts));
            }

            var decisions = new List<CharacterDecisionSummaryView>();
            foreach (Decision decision in world.Decisions.All)
            {
                if (decision.CharacterId != characterId) continue;
                string timeLabel = decision.IsActive
                    ? "resolves " + decision.ResolveAt
                    : decision.Resolution == null ? decision.CreatedAt.ToString() : "resolved " + decision.Resolution.ResolvedAt;
                decisions.Add(new CharacterDecisionSummaryView(
                    decision.Id.Value,
                    decision.DefinitionId.Value,
                    decision.Status.ToString(),
                    timeLabel));
            }

            var history = new List<CharacterHistoryEntryView>();
            IReadOnlyList<HistoryEntry> allHistory = world.HistoryLedger.Entries;
            for (int i = allHistory.Count - 1; i >= 0 && history.Count < 5; i--)
            {
                HistoryEntry entry = allHistory[i];
                if (!HasSubject(entry.Subjects, subject)) continue;
                history.Add(new CharacterHistoryEntryView(entry.Id.Value, entry.OccurredAt.ToString(), entry.Summary));
            }

            view = new CharacterProfileView(
                characterId.Value,
                character.DisplayName,
                activityLabel,
                locationLabel,
                isTraveling,
                travelOriginLabel,
                travelProgressBasisPoints,
                world.Attention.WatchStateOf(characterId).IsFollowed,
                traits,
                needs,
                _schedules.Project(world, characterId),
                relationships,
                decisions,
                history);

            return true;
        }

        private static string LocationName(WorldState world, LocationId locationId) =>
            world.Locations.TryGet(locationId, out LocationNode node) ? node.DisplayName : locationId.ToString();

        private static KnownFactView ToKnownFact(WorldState world, KnowledgeEntry entry) => new KnownFactView(
            entry.Key.Qualifier.IsSet ? entry.Key.Qualifier.Value : entry.Key.Kind.Value,
            entry.ObservedValue.ToString(),
            entry.ObservedAt.ToString(),
            entry.Confidence.ToString(),
            world.Clock.Now.Since(entry.ObservedAt) > StalenessWindow);

        private static bool HasSubject(IReadOnlyList<EntityRef> subjects, EntityRef subject)
        {
            for (int i = 0; i < subjects.Count; i++)
                if (subjects[i] == subject) return true;
            return false;
        }
    }

    /// <summary>Projects a location and its occupancy counts from the indexes (§30, §35).</summary>
    public sealed class LocationProjector
    {
        public bool TryProject(WorldState world, LocationId locationId, out LocationView view)
        {
            view = null;

            if (!world.Locations.TryGet(locationId, out LocationNode node))
            {
                return false;
            }

            var children = new List<int>();
            foreach (LocationId child in world.Locations.ChildrenOf(locationId))
            {
                children.Add(child.Value);
            }

            // Counts come from maintained indexes, never from scanning the population (§50).
            bool requestedState = !node.IsOpen;
            Result availability = LocationAvailabilityRules.Evaluate(world, node.Id, requestedState);
            view = new LocationView(
                node.Id.Value,
                node.DisplayName,
                node.LocationKindId.Value,
                world.Spatial.CountDirectlyIn(locationId),
                world.Spatial.CountWithin(locationId),
                children,
                node.IsOpen,
                availability.IsSuccess,
                availability.IsFailure ? availability.Reason.Value : null);

            return true;
        }
    }

    /// <summary>
    /// Projects a character's materialized schedule, flagging overlaps (§29.3, §29.4).
    /// </summary>
    public sealed class ScheduleProjector
    {
        public ScheduleView Project(WorldState world, CharacterId characterId)
        {
            var commitments = new List<Commitment>();
            foreach (Commitment commitment in world.Commitments.All)
            {
                if (commitment.CharacterId == characterId && commitment.Status != CommitmentStatus.Cancelled)
                {
                    commitments.Add(commitment);
                }
            }

            commitments.Sort((a, b) =>
            {
                int byStart = a.EarliestStart.CompareTo(b.EarliestStart);
                return byStart != 0 ? byStart : a.Id.Value.CompareTo(b.Id.Value);
            });

            var entries = new List<ScheduleEntryView>(commitments.Count);
            for (int i = 0; i < commitments.Count; i++)
            {
                Commitment commitment = commitments[i];
                bool conflicts = (i > 0 && commitments[i - 1].OverlapsWindowOf(commitment))
                    || (i + 1 < commitments.Count && commitments[i + 1].OverlapsWindowOf(commitment));

                entries.Add(new ScheduleEntryView(
                    commitment.Id.Value,
                    commitment.Kind.Value,
                    commitment.EarliestStart.ToString(),
                    commitment.ExpectedDuration.ToString(),
                    world.Locations.TryGet(commitment.LocationId, out LocationNode node) ? node.DisplayName : commitment.LocationId.ToString(),
                    commitment.Status.ToString(),
                    conflicts));
            }

            return new ScheduleView(characterId.Value, entries);
        }
    }
}
