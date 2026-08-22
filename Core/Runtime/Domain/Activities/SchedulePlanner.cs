using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Activities
{
    /// <summary>
    /// Turns routine patterns into commitments, and commitments into concrete transitions (§29.4, §29.5).
    /// <para>
    /// <b>Reactive with bounded look-ahead.</b> Recurring patterns are materialized only across the
    /// horizon current simulation or a future-facing query actually needs — a schedule-conflict screen,
    /// departure planning, the player inspecting tomorrow. Nobody ever materializes an infinite future
    /// calendar (invariant 44).
    /// </para>
    /// <para>
    /// The planner answers <i>what</i> and <i>why</i>; the scheduler answers <i>when</i>.
    /// </para>
    /// </summary>
    public sealed class SchedulePlanner
    {
        /// <summary>Default look-ahead. Tuning, not architectural identity (§29.4).</summary>
        public static readonly SimDuration DefaultPlanningHorizon = SimDuration.FromDays(2);

        /// <summary>
        /// Materializes occurrences of <paramref name="templates"/> for a character across the horizon.
        /// Idempotent: an occurrence already planned is not planned twice.
        /// </summary>
        /// <returns>Newly created commitments, in chronological order.</returns>
        public IReadOnlyList<Commitment> MaterializeCommitments(
            SimulationContext context,
            CharacterId characterId,
            IReadOnlyList<CommitmentTemplate> templates,
            SimDuration horizon = default)
        {
            WorldState world = context.World;
            SimTime from = world.Clock.Now;
            SimTime until = from.Plus(horizon.IsZero ? DefaultPlanningHorizon : horizon);

            var created = new List<Commitment>();

            for (int t = 0; t < templates.Count; t++)
            {
                CommitmentTemplate template = templates[t];
                SimTime cursor = from;

                while (true)
                {
                    SimTime? occurrence = template.FirstOccurrenceAtOrAfter(cursor);
                    if (!occurrence.HasValue || occurrence.Value > until)
                    {
                        break;
                    }

                    if (!IsAlreadyPlanned(world, characterId, template.Id, occurrence.Value))
                    {
                        var commitment = new Commitment(
                            world.RuntimeIds.Commitments.Next(),
                            characterId,
                            template.CommitmentKind,
                            occurrence.Value,
                            occurrence.Value.Plus(template.StartWindow),
                            template.Duration,
                            template.LocationId,
                            template.Priority,
                            template.ActivityDefinitionId,
                            template.Source,
                            null,
                            template.Id);

                        world.Commitments.Add(commitment.Id, commitment);
                        created.Add(commitment);
                    }

                    cursor = occurrence.Value.Plus(SimDuration.FromMinutes(1));
                }
            }

            if (created.Count > 0)
            {
                // One bump for the batch: the character's planned schedule changed (§11.2.1).
                world.BumpRevision(new RevisionKey(characterId.ToRef(), RevisionAspects.Schedule));
            }

            return created;
        }

        /// <summary>
        /// Detects commitments contending for the same character's time (§29.3).
        /// <para>
        /// This is the payoff of keeping Commitments separate from ScheduledEvents: overlapping work and
        /// social obligations are visible <i>before</i> either becomes a concrete Activity transition.
        /// </para>
        /// </summary>
        public IReadOnlyList<Commitment> FindConflicts(WorldState world, CharacterId characterId, Commitment candidate)
        {
            var conflicts = new List<Commitment>();
            foreach (Commitment existing in world.Commitments.All)
            {
                if (existing.Id == candidate.Id ||
                    existing.CharacterId != characterId ||
                    existing.Status != CommitmentStatus.Planned)
                {
                    continue;
                }

                if (existing.OverlapsWindowOf(candidate))
                {
                    conflicts.Add(existing);
                }
            }

            return conflicts;
        }

        /// <summary>
        /// Schedules the transition that starts a commitment, leaving enough time to travel there.
        /// <para>
        /// Departure is <c>EarliestStart − estimated travel</c>. If travel then runs long, the start
        /// event's semantic validation finds her still <c>Traveling</c> and she is simply late — she is
        /// never magically present (§29.5).
        /// </para>
        /// </summary>
        public bool TryPlanCommitmentStart(SimulationContext context, Commitment commitment)
        {
            WorldState world = context.World;

            if (commitment.Status != CommitmentStatus.Planned)
            {
                return false;
            }

            SimDuration travel = SimDuration.Zero;
            if (world.TryGetSpatialContext(commitment.CharacterId, out ActivitySpatialContext current) &&
                current.IsLocated &&
                current.LocationId != commitment.LocationId &&
                world.TravelNetwork.TryPlanRoute(current.LocationId, commitment.LocationId, out TravelPlan plan))
            {
                travel = plan.TotalCost;
            }

            SimTime departAt = commitment.EarliestStart.Minus(travel);
            if (departAt < world.Clock.Now)
            {
                departAt = world.Clock.Now;
            }

            var dependencies = new[]
            {
                EventDependency.Capture(world.Revisions, new RevisionKey(commitment.CharacterId.ToRef(), RevisionAspects.Schedule)),
            };

            ScheduledEvent scheduled = world.Scheduler.Schedule(
                departAt,
                SchedulePhase.Activity,
                ScheduledEventTypes.ActivityStart,
                new ActivityStartPayload(
                    commitment.CharacterId,
                    commitment.Id,
                    commitment.ActivityDefinitionId,
                    commitment.LocationId),
                dependencies);

            if (context.Trace.IsEnabled)
            {
                context.Trace.Record(
                    "planner",
                    $"{world.Clock.Now} planned {commitment.Kind} for {commitment.CharacterId}: depart {departAt}, start {commitment.EarliestStart} (event {scheduled.Id})");
            }

            return true;
        }

        private static bool IsAlreadyPlanned(WorldState world, CharacterId characterId, AuthoredId templateId, SimTime start)
        {
            foreach (Commitment existing in world.Commitments.All)
            {
                if (existing.CharacterId == characterId &&
                    existing.SourceTemplateId == templateId &&
                    existing.EarliestStart == start)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
