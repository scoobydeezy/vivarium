using System;
using System.Collections.Generic;
using Vivarium.Domain.Attention;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Simulation;

namespace Vivarium.Application.Queries
{
    /// <summary>Builds the bounded-attention Decision inbox from living Importance and watch policy.</summary>
    public sealed class DecisionFeedProjector
    {
        private readonly DecisionImportancePolicyDefinition _importance;
        private readonly DecisionHoldPolicy _holds;

        public DecisionFeedProjector(
            DecisionImportancePolicyDefinition importance,
            DecisionHoldPolicy holds)
        {
            _importance = importance ?? throw new ArgumentNullException(nameof(importance));
            _holds = holds ?? throw new ArgumentNullException(nameof(holds));
        }

        public DecisionFeedView Project(WorldState world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var candidates = new List<Candidate>();
            foreach (Decision decision in world.Decisions.All)
            {
                if (!decision.IsActive ||
                    !world.Characters.TryGet(decision.CharacterId, out Domain.Characters.Character character))
                {
                    continue;
                }

                bool held = world.Attention.IsHeld(decision.Id);
                AttentionPolicy policy = world.Attention.PolicyFor(decision.CharacterId);
                bool prioritized = world.Attention.WatchStateOf(decision.CharacterId).IsWatched;
                int floor = prioritized ? _importance.PrioritizedFeedFloor : _importance.NormalFeedFloor;
                if (!held && (policy == AttentionPolicy.Quiet || decision.Importance < floor))
                {
                    continue;
                }

                candidates.Add(new Candidate(decision, character.DisplayName, held, prioritized));
            }

            candidates.Sort(Compare);
            var entries = new DecisionFeedEntryView[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                Candidate item = candidates[i];
                entries[i] = new DecisionFeedEntryView(
                    item.Decision.Id.Value,
                    item.Decision.CharacterId.Value,
                    item.CharacterName,
                    item.Decision.DefinitionId.Value,
                    item.Decision.ResolveAt.ToString(),
                    item.Held,
                    item.Decision.Importance);
            }

            return new DecisionFeedView(entries, world.Attention.HeldCount, _holds.MaxGlobalHeld);
        }

        private static int Compare(Candidate left, Candidate right)
        {
            int held = right.Held.CompareTo(left.Held);
            if (held != 0) return held;
            int prioritized = right.Prioritized.CompareTo(left.Prioritized);
            if (prioritized != 0) return prioritized;
            int importance = right.Decision.Importance.CompareTo(left.Decision.Importance);
            if (importance != 0) return importance;
            int resolveAt = left.Decision.ResolveAt.CompareTo(right.Decision.ResolveAt);
            return resolveAt != 0 ? resolveAt : left.Decision.Id.CompareTo(right.Decision.Id);
        }

        private readonly struct Candidate
        {
            public Candidate(Decision decision, string characterName, bool held, bool prioritized)
            {
                Decision = decision;
                CharacterName = characterName;
                Held = held;
                Prioritized = prioritized;
            }

            public Decision Decision { get; }
            public string CharacterName { get; }
            public bool Held { get; }
            public bool Prioritized { get; }
        }
    }
}
