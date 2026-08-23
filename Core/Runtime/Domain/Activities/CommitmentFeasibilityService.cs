using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Activities
{
    /// <summary>The authoritative intent carried by one commitment-conflict Decision Option.</summary>
    public sealed class CommitmentResolutionPlan
    {
        private readonly CommitmentId[] _preserve;
        private readonly CommitmentId[] _defer;
        private readonly CommitmentId[] _relinquish;

        public CommitmentResolutionPlan(
            AuthoredId planId,
            IReadOnlyList<CommitmentId> preserve,
            IReadOnlyList<CommitmentId> defer,
            IReadOnlyList<CommitmentId> relinquish)
        {
            if (!planId.IsSet) throw new ArgumentException("A resolution plan needs a stable id.", nameof(planId));
            PlanId = planId;
            _preserve = CanonicalCopy(preserve);
            _defer = CanonicalCopy(defer);
            _relinquish = CanonicalCopy(relinquish);
            ValidateDisjoint(_preserve, _defer, _relinquish);
        }

        public AuthoredId PlanId { get; }
        public IReadOnlyList<CommitmentId> Preserve => _preserve;
        public IReadOnlyList<CommitmentId> Defer => _defer;
        public IReadOnlyList<CommitmentId> Relinquish => _relinquish;

        public CommitmentResolutionPlan Copy() =>
            new CommitmentResolutionPlan(PlanId, _preserve, _defer, _relinquish);

        private static CommitmentId[] CanonicalCopy(IReadOnlyList<CommitmentId> source)
        {
            var copy = new CommitmentId[source?.Count ?? 0];
            for (int i = 0; i < copy.Length; i++)
            {
                if (!source[i].IsSet) throw new ArgumentException("Resolution plans cannot contain an unset CommitmentId.");
                copy[i] = source[i];
            }
            Array.Sort(copy);
            for (int i = 1; i < copy.Length; i++)
            {
                if (copy[i] == copy[i - 1]) throw new ArgumentException("A resolution plan cannot contain a Commitment twice.");
            }
            return copy;
        }

        private static void ValidateDisjoint(params CommitmentId[][] sets)
        {
            var seen = new SortedSet<CommitmentId>();
            for (int s = 0; s < sets.Length; s++)
            for (int i = 0; i < sets[s].Length; i++)
            {
                if (!seen.Add(sets[s][i])) throw new ArgumentException("Preserve, Defer, and Relinquish must be disjoint.");
            }
        }
    }

    /// <summary>Canonical identity for one episode of joint commitment infeasibility.</summary>
    public sealed class CommitmentConflictKey : IEquatable<CommitmentConflictKey>, IComparable<CommitmentConflictKey>
    {
        private readonly CommitmentId[] _participants;

        public CommitmentConflictKey(
            CharacterId characterId,
            IReadOnlyList<CommitmentId> participatingCommitments,
            int conflictInstanceRevision)
        {
            if (!characterId.IsSet) throw new ArgumentException("A conflict needs a character.", nameof(characterId));
            if (participatingCommitments == null || participatingCommitments.Count < 2)
                throw new ArgumentException("A conflict needs at least two commitments.", nameof(participatingCommitments));
            CharacterId = characterId;
            ConflictInstanceRevision = conflictInstanceRevision;
            _participants = new CommitmentId[participatingCommitments.Count];
            for (int i = 0; i < _participants.Length; i++)
            {
                if (!participatingCommitments[i].IsSet) throw new ArgumentException("A conflict cannot contain an unset CommitmentId.");
                _participants[i] = participatingCommitments[i];
            }
            Array.Sort(_participants);
            for (int i = 1; i < _participants.Length; i++)
                if (_participants[i] == _participants[i - 1]) throw new ArgumentException("A conflict cannot contain a Commitment twice.");
        }

        public CharacterId CharacterId { get; }
        public IReadOnlyList<CommitmentId> ParticipatingCommitmentIds => _participants;
        public int ConflictInstanceRevision { get; }

        public bool HasSameParticipants(CharacterId characterId, IReadOnlyList<CommitmentId> participants)
        {
            if (CharacterId != characterId || participants == null || participants.Count != _participants.Length) return false;
            var copy = new CommitmentId[participants.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = participants[i];
            Array.Sort(copy);
            for (int i = 0; i < copy.Length; i++) if (copy[i] != _participants[i]) return false;
            return true;
        }

        public bool Equals(CommitmentConflictKey other) => other != null && CompareTo(other) == 0;
        public override bool Equals(object obj) => Equals(obj as CommitmentConflictKey);
        public override int GetHashCode()
        {
            int hash = (CharacterId.GetHashCode() * 397) ^ ConflictInstanceRevision;
            for (int i = 0; i < _participants.Length; i++) hash = (hash * 397) ^ _participants[i].GetHashCode();
            return hash;
        }
        public int CompareTo(CommitmentConflictKey other)
        {
            if (other == null) return 1;
            int actor = CharacterId.CompareTo(other.CharacterId);
            if (actor != 0) return actor;
            int count = _participants.Length.CompareTo(other._participants.Length);
            if (count != 0) return count;
            for (int i = 0; i < _participants.Length; i++)
            {
                int id = _participants[i].CompareTo(other._participants[i]);
                if (id != 0) return id;
            }
            return ConflictInstanceRevision.CompareTo(other.ConflictInstanceRevision);
        }
    }

    public sealed class CommitmentFeasibilityResult
    {
        public CommitmentFeasibilityResult(bool jointlyFeasible, SimTime latestResolutionAt)
        {
            IsJointlyFeasible = jointlyFeasible;
            LatestResolutionAt = latestResolutionAt;
        }
        public bool IsJointlyFeasible { get; }
        public SimTime LatestResolutionAt { get; }
    }

    /// <summary>
    /// Evaluates a set as a set. The bounded search considers complete deterministic orderings, so
    /// pairwise compatibility is never mistaken for genuine joint feasibility.
    /// </summary>
    public sealed class CommitmentFeasibilityService
    {
        public CommitmentFeasibilityResult Evaluate(
            WorldState world,
            CharacterId characterId,
            IReadOnlyList<Commitment> commitments)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (commitments == null || commitments.Count == 0)
                return new CommitmentFeasibilityResult(true, world.Clock.Now);

            var candidates = new List<Commitment>(commitments.Count);
            for (int i = 0; i < commitments.Count; i++)
            {
                Commitment commitment = commitments[i];
                if (commitment.CharacterId != characterId || commitment.Status != CommitmentStatus.Planned)
                    return new CommitmentFeasibilityResult(false, world.Clock.Now);
                candidates.Add(commitment);
            }
            candidates.Sort((a, b) => a.Id.CompareTo(b.Id));

            LocationId origin = default;
            SimTime availableAt = world.Clock.Now;
            if (world.TryGetSpatialContext(characterId, out ActivitySpatialContext spatial))
            {
                if (spatial.IsLocated) origin = spatial.LocationId;
                else if (spatial.IsTraveling)
                {
                    origin = spatial.Transit.DestinationLocationId;
                    availableAt = spatial.Transit.ArrivesAt > availableAt ? spatial.Transit.ArrivesAt : availableAt;
                }
            }

            bool feasible = Search(world, candidates, new bool[candidates.Count], availableAt, origin, 0);
            SimTime deadline = LatestResolutionAt(world, candidates, origin);
            return new CommitmentFeasibilityResult(feasible, deadline);
        }

        private static bool Search(
            WorldState world,
            IReadOnlyList<Commitment> commitments,
            bool[] used,
            SimTime availableAt,
            LocationId location,
            int depth)
        {
            if (depth == commitments.Count) return true;
            for (int i = 0; i < commitments.Count; i++)
            {
                if (used[i]) continue;
                Commitment next = commitments[i];
                if (!TryTravel(world, location, next.LocationId, out SimDuration travel)) continue;
                SimTime arrival = availableAt.Plus(travel);
                SimTime start = arrival < next.EarliestStart ? next.EarliestStart : arrival;
                if (start > next.LatestStart) continue;
                used[i] = true;
                if (Search(world, commitments, used, start.Plus(next.ExpectedDuration), next.LocationId, depth + 1)) return true;
                used[i] = false;
            }
            return false;
        }

        private static SimTime LatestResolutionAt(WorldState world, IReadOnlyList<Commitment> commitments, LocationId origin)
        {
            SimTime deadline = commitments[0].LatestStart;
            for (int i = 0; i < commitments.Count; i++)
            {
                if (!TryTravel(world, origin, commitments[i].LocationId, out SimDuration travel)) return world.Clock.Now;
                SimTime latestDeparture = commitments[i].LatestStart.Minus(travel);
                if (latestDeparture < deadline) deadline = latestDeparture;
            }
            return deadline < world.Clock.Now ? world.Clock.Now : deadline;
        }

        private static bool TryTravel(WorldState world, LocationId from, LocationId to, out SimDuration duration)
        {
            if (!from.IsSet || from == to)
            {
                duration = SimDuration.Zero;
                return true;
            }
            if (world.TravelNetwork.TryPlanRoute(from, to, out TravelPlan plan))
            {
                duration = plan.TotalCost;
                return true;
            }
            duration = default;
            return false;
        }
    }
}
