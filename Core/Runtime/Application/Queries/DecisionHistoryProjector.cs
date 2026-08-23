using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.History;
using Vivarium.Domain.Simulation;

namespace Vivarium.Application.Queries
{
    /// <summary>One knowledge-safe explanation of a retained Decision event.</summary>
    public sealed class DecisionHistoryEntryView
    {
        public DecisionHistoryEntryView(int historyEntryId, string occurredAtLabel, string message)
        {
            HistoryEntryId = historyEntryId;
            OccurredAtLabel = occurredAtLabel;
            Message = message;
        }

        public int HistoryEntryId { get; }
        public string OccurredAtLabel { get; }
        public string Message { get; }
    }

    /// <summary>A newest-first, bounded causal feed suitable for a small presentation surface.</summary>
    public sealed class DecisionHistoryView
    {
        public DecisionHistoryView(IReadOnlyList<DecisionHistoryEntryView> entries) => Entries = entries;

        public IReadOnlyList<DecisionHistoryEntryView> Entries { get; }
    }

    public sealed class DecisionHistoryProjector
    {
        public DecisionHistoryView Project(WorldState world, int maximumEntries = 5)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (maximumEntries < 0) throw new ArgumentOutOfRangeException(nameof(maximumEntries));

            var entries = new List<DecisionHistoryEntryView>();
            IReadOnlyList<HistoryEntry> history = world.HistoryLedger.Entries;
            for (int i = history.Count - 1; i >= 0 && entries.Count < maximumEntries; i--)
            {
                HistoryEntry entry = history[i];
                if (!IsDecisionHistory(entry.Kind))
                {
                    continue;
                }

                entries.Add(new DecisionHistoryEntryView(
                    entry.Id.Value,
                    entry.OccurredAt.ToString(),
                    FormatMessage(world, entry)));
            }

            return new DecisionHistoryView(entries);
        }

        private static bool IsDecisionHistory(AuthoredId kind) =>
            kind == DecisionCreatedHistoryHandler.HistoryKind ||
            kind == DecisionInterventionHistoryHandler.HistoryKind ||
            kind == new AuthoredId("history.decision_resolved");

        private static string FormatMessage(WorldState world, HistoryEntry entry)
        {
            string characterName = CharacterName(world, entry.Subjects);
            if (entry.Kind == DecisionCreatedHistoryHandler.HistoryKind)
            {
                return $"{characterName} faces {entry.Summary}";
            }

            if (entry.Kind == DecisionInterventionHistoryHandler.HistoryKind)
            {
                return $"You influenced {characterName}: {entry.Summary}";
            }

            return $"{characterName} resolved {entry.Summary}";
        }

        private static string CharacterName(WorldState world, IReadOnlyList<EntityRef> subjects)
        {
            for (int i = 0; i < subjects.Count; i++)
            {
                EntityRef subject = subjects[i];
                if (subject.Kind == EntityKind.Character &&
                    world.Characters.TryGet(new CharacterId(subject.RuntimeId), out Domain.Characters.Character character))
                {
                    return character.DisplayName;
                }
            }

            return "A character";
        }
    }
}
