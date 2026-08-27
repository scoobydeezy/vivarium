using System;
using System.Collections.Generic;
using Vivarium.Domain.Attention;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;

namespace Vivarium.Application.Queries
{
    /// <summary>Builds the bounded-attention Decision inbox from living Importance and watch policy.</summary>
    public sealed class DecisionFeedProjector
    {
        private readonly DecisionImportancePolicyDefinition _importance;
        private readonly DecisionHoldPolicy _holds;
        private readonly int _recentResolutionLimit;

        public DecisionFeedProjector(
            DecisionImportancePolicyDefinition importance,
            DecisionHoldPolicy holds,
            int recentResolutionLimit = 5)
        {
            _importance = importance ?? throw new ArgumentNullException(nameof(importance));
            _holds = holds ?? throw new ArgumentNullException(nameof(holds));
            if (recentResolutionLimit < 0) throw new ArgumentOutOfRangeException(nameof(recentResolutionLimit));
            _recentResolutionLimit = recentResolutionLimit;
        }

        public DecisionFeedView Project(WorldState world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var candidates = new List<Candidate>();
            var recent = new List<Candidate>();
            foreach (Decision decision in world.Decisions.All)
            {
                if (!world.Characters.TryGet(decision.CharacterId, out Domain.Characters.Character character))
                {
                    continue;
                }

                if (!decision.IsActive)
                {
                    if (decision.Status == DecisionStatus.Resolved &&
                        decision.Resolution != null &&
                        decision.Importance >= _importance.NormalFeedFloor)
                    {
                        recent.Add(new Candidate(
                            decision,
                            character.DisplayName,
                            held: false,
                            prioritized: false,
                            isRecentResolution: true));
                    }
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

                candidates.Add(new Candidate(decision, character.DisplayName, held, prioritized, false));
            }

            recent.Sort(Compare);
            int recentCount = Math.Min(_recentResolutionLimit, recent.Count);
            for (int i = 0; i < recentCount; i++) candidates.Add(recent[i]);
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
                    item.Decision.Importance,
                    item.Decision.Status.ToString(),
                    item.IsRecentResolution ? null : RemainingLabel(item.Decision.ResolveAt, world.Clock.Now),
                    item.Decision.CommitmentConflictKey != null,
                    item.IsRecentResolution);
            }

            return new DecisionFeedView(
                entries,
                world.Attention.HeldCount,
                _holds.MaxGlobalHeld,
                _holds.MaxHeldPerCharacter);
        }

        private static string RemainingLabel(SimTime resolveAt, SimTime now)
        {
            SimDuration remaining = resolveAt - now;
            return remaining.IsNegative ? SimDuration.Zero.ToString() : remaining.ToString();
        }

        private static int Compare(Candidate left, Candidate right)
        {
            int recent = left.IsRecentResolution.CompareTo(right.IsRecentResolution);
            if (recent != 0) return recent;
            if (left.IsRecentResolution)
            {
                int resolvedAt = right.Decision.Resolution.ResolvedAt.CompareTo(
                    left.Decision.Resolution.ResolvedAt);
                return resolvedAt != 0 ? resolvedAt : right.Decision.Id.CompareTo(left.Decision.Id);
            }
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
            public Candidate(
                Decision decision,
                string characterName,
                bool held,
                bool prioritized,
                bool isRecentResolution)
            {
                Decision = decision;
                CharacterName = characterName;
                Held = held;
                Prioritized = prioritized;
                IsRecentResolution = isRecentResolution;
            }

            public Decision Decision { get; }
            public string CharacterName { get; }
            public bool Held { get; }
            public bool Prioritized { get; }
            public bool IsRecentResolution { get; }
        }
    }
}
