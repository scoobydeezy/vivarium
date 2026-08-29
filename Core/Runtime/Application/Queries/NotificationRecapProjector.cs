using System;
using System.Collections.Generic;
using Vivarium.Domain.Attention;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.History;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.PlayerAgency;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Time;

namespace Vivarium.Application.Queries
{
    public sealed class NotificationEntryView
    {
        public NotificationEntryView(
            int historyEntryId,
            string occurredAtLabel,
            string category,
            string message,
            int occurrenceCount,
            int characterId,
            int decisionId,
            int locationId)
        {
            HistoryEntryId = historyEntryId;
            OccurredAtLabel = occurredAtLabel;
            Category = category;
            Message = message;
            OccurrenceCount = occurrenceCount;
            CharacterId = characterId;
            DecisionId = decisionId;
            LocationId = locationId;
        }

        public int HistoryEntryId { get; }
        public string OccurredAtLabel { get; }
        public string Category { get; }
        public string Message { get; }
        public int OccurrenceCount { get; }
        public int CharacterId { get; }
        public int DecisionId { get; }
        public int LocationId { get; }
    }

    public sealed class NotificationRecapView
    {
        public NotificationRecapView(
            bool isOfflineRecap,
            IReadOnlyList<NotificationEntryView> entries,
            int includedEventCount,
            int omittedGroupCount)
        {
            IsOfflineRecap = isOfflineRecap;
            Entries = entries;
            IncludedEventCount = includedEventCount;
            OmittedGroupCount = omittedGroupCount;
        }

        public bool IsOfflineRecap { get; }
        public IReadOnlyList<NotificationEntryView> Entries { get; }
        public int IncludedEventCount { get; }
        public int OmittedGroupCount { get; }
    }

    /// <summary>
    /// Projects retained causal history into a bounded, Knowledge- and Attention-filtered presentation feed.
    /// It creates no notification truth: grouping, cursor time, and live/offline layout remain Presentation state.
    /// </summary>
    public sealed class NotificationRecapProjector
    {
        private static readonly AuthoredId DecisionResolved = new AuthoredId("history.decision_resolved");
        private static readonly AuthoredId Interaction = new AuthoredId("history.interaction");
        private static readonly AuthoredId CommitmentOutcome = new AuthoredId("history.commitment_outcome");
        private readonly DecisionImportancePolicyDefinition _importance;

        public NotificationRecapProjector(DecisionImportancePolicyDefinition importance) =>
            _importance = importance ?? throw new ArgumentNullException(nameof(importance));

        public NotificationRecapView Project(
            WorldState world,
            SimulationMode mode,
            SimTime? since = null,
            int maximumGroups = 8)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (maximumGroups < 0) throw new ArgumentOutOfRangeException(nameof(maximumGroups));

            bool offline = mode == SimulationMode.OfflineCatchUp;
            var groups = new List<Group>();
            var groupsByKey = new Dictionary<string, Group>();
            int included = 0;
            IReadOnlyList<HistoryEntry> history = world.HistoryLedger.Entries;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                HistoryEntry entry = history[i];
                if (since.HasValue && entry.OccurredAt < since.Value) break;
                if (!TryDescribe(world, entry, offline, out Description description)) continue;
                included++;

                string key = description.Category + ":" + description.CharacterId + ":" +
                    description.DecisionId + ":" + description.LocationId;
                if (groupsByKey.TryGetValue(key, out Group existing))
                {
                    existing.Count++;
                    continue;
                }

                var group = new Group(key, entry, description);
                groups.Add(group);
                groupsByKey.Add(key, group);
            }

            // Select material groups before routine social repetition can consume the bounded recap.
            // Display order remains newest-first after selection, so priority affects inclusion rather
            // than turning the recap into a category-sorted event browser.
            groups.Sort((left, right) =>
            {
                int byPriority = Priority(right.Description.Category).CompareTo(
                    Priority(left.Description.Category));
                if (byPriority != 0) return byPriority;
                int byTime = right.Entry.OccurredAt.CompareTo(left.Entry.OccurredAt);
                return byTime != 0 ? byTime : right.Entry.Id.CompareTo(left.Entry.Id);
            });
            int selectedCount = Math.Min(maximumGroups, groups.Count);
            int omittedGroups = groups.Count - selectedCount;
            if (groups.Count > selectedCount)
                groups.RemoveRange(selectedCount, groups.Count - selectedCount);
            groups.Sort((left, right) =>
            {
                int byTime = right.Entry.OccurredAt.CompareTo(left.Entry.OccurredAt);
                return byTime != 0 ? byTime : right.Entry.Id.CompareTo(left.Entry.Id);
            });

            var entries = new NotificationEntryView[groups.Count];
            for (int i = 0; i < groups.Count; i++)
            {
                Group group = groups[i];
                entries[i] = new NotificationEntryView(
                    group.Entry.Id.Value,
                    group.Entry.OccurredAt.ToString(),
                    group.Description.Category,
                    group.Description.Message,
                    group.Count,
                    group.Description.CharacterId,
                    group.Description.DecisionId,
                    group.Description.LocationId);
            }
            return new NotificationRecapView(offline, entries, included, omittedGroups);
        }

        private static int Priority(string category)
        {
            switch (category)
            {
                case "Intervention":
                case "World":
                    return 4;
                case "Decision":
                case "Commitment":
                    return 3;
                case "Resources":
                    return 2;
                default:
                    return 1;
            }
        }

        private bool TryDescribe(
            WorldState world,
            HistoryEntry entry,
            bool offline,
            out Description description)
        {
            description = default;
            int characterId = SubjectId(entry.Subjects, EntityKind.Character);
            int decisionId = SubjectId(entry.Subjects, EntityKind.Decision);
            int locationId = SubjectId(entry.Subjects, EntityKind.Location);
            bool playerCaused = entry.Kind == DecisionInterventionHistoryHandler.HistoryKind ||
                entry.Kind == LocationAvailabilityHistoryHandler.HistoryKind;

            if (!playerCaused && characterId > 0 && !offline &&
                world.Attention.PolicyFor(new CharacterId(characterId)) == AttentionPolicy.Quiet)
                return false;

            if (IsDecision(entry.Kind))
            {
                if (decisionId <= 0 || !world.Decisions.TryGet(new DecisionId(decisionId), out Decision decision))
                    return false;
                bool watched = world.Attention.WatchStateOf(decision.CharacterId).IsWatched;
                int floor = watched ? _importance.PrioritizedFeedFloor : _importance.NormalFeedFloor;
                if (!world.Attention.IsHeld(decision.Id) && decision.Importance < floor) return false;
                string character = CharacterName(world, decision.CharacterId.Value);
                string verb = entry.Kind == DecisionCreatedHistoryHandler.HistoryKind
                    ? "faces"
                    : entry.Kind == DecisionDissolvedHistoryHandler.HistoryKind ? "lost" : "resolved";
                description = new Description(
                    "Decision",
                    $"{character} {verb} {entry.Summary}",
                    decision.CharacterId.Value,
                    decision.Id.Value,
                    locationId);
                return true;
            }

            if (entry.Kind == LocationAvailabilityHistoryHandler.HistoryKind)
            {
                description = new Description("World", entry.Summary, 0, 0, locationId);
                return true;
            }

            if (entry.Kind == DecisionInterventionHistoryHandler.HistoryKind)
            {
                description = new Description(
                    "Intervention",
                    $"You influenced {CharacterName(world, characterId)}: {entry.Summary}",
                    characterId,
                    decisionId,
                    locationId);
                return true;
            }

            if (entry.Kind == NudgeBalanceHistoryHandler.HistoryKind)
            {
                if (entry.Summary.StartsWith("Spent", StringComparison.Ordinal)) return false;
                description = new Description("Resources", "Nudges — " + entry.Summary, characterId, decisionId, locationId);
                return true;
            }

            if (entry.Kind == Interaction || entry.Kind == CommitmentOutcome)
            {
                if (!IsKnown(world, entry)) return false;
                string message = entry.Kind == Interaction ? InteractionMessage(world, entry.Subjects) : entry.Summary;
                description = new Description(
                    entry.Kind == Interaction ? "Social" : "Commitment",
                    message,
                    characterId,
                    decisionId,
                    locationId);
                return true;
            }

            return false;
        }

        private static bool IsDecision(AuthoredId kind) =>
            kind == DecisionCreatedHistoryHandler.HistoryKind ||
            kind == DecisionResolved ||
            kind == DecisionDissolvedHistoryHandler.HistoryKind;

        private static bool IsKnown(WorldState world, HistoryEntry history)
        {
            foreach (KnowledgeEntry knowledge in world.Knowledge.All)
            {
                if (knowledge.ObservedAt < history.OccurredAt) continue;
                for (int i = 0; i < history.Subjects.Count; i++)
                    if (knowledge.Key.Subject == history.Subjects[i]) return true;
            }
            return false;
        }

        private static string InteractionMessage(WorldState world, IReadOnlyList<EntityRef> subjects)
        {
            int first = 0;
            int second = 0;
            int location = 0;
            for (int i = 0; i < subjects.Count; i++)
            {
                if (subjects[i].Kind == EntityKind.Character)
                {
                    if (first == 0) first = subjects[i].RuntimeId;
                    else if (second == 0) second = subjects[i].RuntimeId;
                }
                else if (subjects[i].Kind == EntityKind.Location) location = subjects[i].RuntimeId;
            }
            string message = CharacterName(world, first) + " interacted with " + CharacterName(world, second);
            if (location > 0 && world.Locations.TryGet(new LocationId(location), out Domain.Spatial.LocationNode node))
                message += " at " + node.DisplayName;
            return message;
        }

        private static string CharacterName(WorldState world, int characterId) =>
            characterId > 0 && world.Characters.TryGet(new CharacterId(characterId), out Domain.Characters.Character character)
                ? character.DisplayName
                : "A character";

        private static int SubjectId(IReadOnlyList<EntityRef> subjects, EntityKind kind)
        {
            for (int i = 0; i < subjects.Count; i++)
                if (subjects[i].Kind == kind) return subjects[i].RuntimeId;
            return 0;
        }

        private readonly struct Description
        {
            public Description(string category, string message, int characterId, int decisionId, int locationId)
            {
                Category = category;
                Message = message;
                CharacterId = characterId;
                DecisionId = decisionId;
                LocationId = locationId;
            }
            public string Category { get; }
            public string Message { get; }
            public int CharacterId { get; }
            public int DecisionId { get; }
            public int LocationId { get; }
        }

        private sealed class Group
        {
            public Group(string key, HistoryEntry entry, Description description)
            {
                Key = key;
                Entry = entry;
                Description = description;
                Count = 1;
            }
            public string Key { get; }
            public HistoryEntry Entry { get; }
            public Description Description { get; }
            public int Count { get; set; }
        }
    }
}
