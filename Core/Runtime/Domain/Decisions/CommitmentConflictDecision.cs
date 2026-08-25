using System;
using System.Collections.Generic;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;
using Vivarium.Domain.Events;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Simulation;

namespace Vivarium.Domain.Decisions
{
    public sealed class CommitmentConflictDecisionTrigger { }

    /// <summary>Re-evaluates feasibility only when authoritative commitment intent changes.</summary>
    public sealed class CommitmentConflictDecisionGenerationHandler : DomainEventHandler<CommitmentScheduleChangedEvent>
    {
        public static readonly AuthoredId DissolutionReasonOptionSetInvalidated =
            new AuthoredId("decision.dissolution.option_set_invalidated");
        private readonly List<DecisionDefinition> _definitions = new List<DecisionDefinition>();
        private readonly CompiledDecisionGenerationService _generation;
        private readonly CommitmentFeasibilityService _feasibility;
        private readonly DecisionSignalProviderRegistry _providers;
        private readonly CompiledDecisionReasoningService _reasoning = new CompiledDecisionReasoningService();

        public CommitmentConflictDecisionGenerationHandler(Content.DefinitionCatalog catalog,
            DecisionSignalProviderRegistry providers, CommitmentFeasibilityService feasibility = null)
            : base(ActivityDomainEventTypes.CommitmentScheduleChanged)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            _providers = providers ?? throw new ArgumentNullException(nameof(providers));
            _generation = new CompiledDecisionGenerationService(_providers);
            _feasibility = feasibility ?? new CommitmentFeasibilityService();
            foreach (KeyValuePair<AuthoredId, DecisionDefinition> pair in catalog.Decisions)
                if (pair.Value.CommitmentConflictTrigger != null) _definitions.Add(pair.Value);
            _definitions.Sort((a, b) => a.Id.CompareTo(b.Id));
        }

        protected override void Handle(CommitmentScheduleChangedEvent e, WorldState world, SimulationContext context)
        {
            var planned = new List<Commitment>();
            foreach (Commitment c in world.Commitments.All)
                if (c.CharacterId == e.CharacterId && c.Status == CommitmentStatus.Planned) planned.Add(c);
            planned.Sort((a, b) => a.Id.CompareTo(b.Id));
            Commitment first = null;
            Commitment second = null;
            CommitmentFeasibilityResult joint = null;
            for (int i = 0; i < planned.Count && first == null; i++)
                for (int j = i + 1; j < planned.Count; j++)
                {
                    CommitmentFeasibilityResult candidate = _feasibility.Evaluate(
                        world, e.CharacterId, new[] { planned[i], planned[j] });
                    if (candidate.IsJointlyFeasible) continue;
                    if (!_feasibility.Evaluate(world, e.CharacterId, new[] { planned[i] }).IsJointlyFeasible ||
                        !_feasibility.Evaluate(world, e.CharacterId, new[] { planned[j] }).IsJointlyFeasible) continue;
                    first = planned[i];
                    second = planned[j];
                    joint = candidate;
                    break;
                }
            var ids = first == null
                ? new CommitmentId[0]
                : new[] { first.Id, second.Id };

            var stale = new List<Decision>();
            foreach (Decision d in world.Decisions.All)
                if (d.IsActive && d.CommitmentConflictKey != null && d.CharacterId == e.CharacterId &&
                    !d.CommitmentConflictKey.HasSameParticipants(e.CharacterId, ids)) stale.Add(d);
            for (int i = 0; i < stale.Count; i++) Dissolve(world, context, stale[i]);

            if (first == null) return;
            Decision existing = null;
            bool haveExisting = world.CommitmentConflicts.TryFindByParticipants(e.CharacterId, ids, out DecisionId existingId) &&
                world.Decisions.TryGet(existingId, out existing) && existing.IsActive;
            if (joint.IsJointlyFeasible)
            {
                if (haveExisting) Dissolve(world, context, existing);
                return;
            }

            if (haveExisting)
            {
                RescheduleDeadline(world, existing, joint.LatestResolutionAt, e.CharacterId);
                if (_reasoning.EvaluateAndReconcile(world, existing, _providers) > 0)
                {
                    world.BumpRevision(existing.InfluenceRevisionKey);
                    world.Publish(new DecisionInfluencesChangedEvent(existing.Id, existing.InfluenceRevision));
                }
                return;
            }

            for (int i = 0; i < _definitions.Count; i++)
            {
                DecisionDefinition definition = _definitions[i];
                if (definition.ReasoningProgram == null || definition.Options.Count != 2) continue;
                var options = new[]
                {
                    BoundPlanOption(definition.Options[0], first, second),
                    BoundPlanOption(definition.Options[1], second, first),
                };
                var key = new CommitmentConflictKey(e.CharacterId, ids, e.ScheduleRevision);
                var dependencies = new[] { EventDependency.Capture(world.Revisions,
                    new RevisionKey(e.CharacterId.ToRef(), RevisionAspects.Schedule)) };
                var scope = definition.ConflictScopeKind.IsSet
                    ? new DecisionConflictScope(definition.ConflictScopeKind, e.CharacterId.ToRef())
                    : DecisionConflictScope.None;
                Decision generated = _generation.Generate(world, new CompiledDecisionGenerationRequest(
                    e.CharacterId, definition.Id, definition.TimeToResolve, options, definition.ReasoningProgram,
                    conflictScope: scope,
                    absoluteResolveAt: joint.LatestResolutionAt,
                    resolveEventType: ScheduledEventTypes.AutoResolveCommitmentConflict,
                    resolveDependencies: dependencies, commitmentConflictKey: key));
                if (generated != null) world.CommitmentConflicts.Register(generated);
            }
        }

        private static DecisionOption BoundPlanOption(DecisionOption template, Commitment preserve, Commitment relinquish)
        {
            var context = new SortedDictionary<AuthoredId, DecisionParameterValue>();
            foreach (KeyValuePair<AuthoredId, DecisionParameterValue> pair in template.Context) context[pair.Key] = pair.Value;
            context[DecisionReasoningParameters.Commitment] = DecisionParameterValue.FromEntity(preserve.Id.ToRef());
            context[DecisionReasoningParameters.PreservedCommitment] = DecisionParameterValue.FromEntity(preserve.Id.ToRef());
            context[DecisionReasoningParameters.RelinquishedCommitment] = DecisionParameterValue.FromEntity(relinquish.Id.ToRef());
            var plan = new CommitmentResolutionPlan(template.Id, new[] { preserve.Id },
                Array.Empty<CommitmentId>(), new[] { relinquish.Id });
            return new DecisionOption(template.Id, template.LabelId, template.OrderIndex, context, plan);
        }

        private static void RescheduleDeadline(WorldState world, Decision decision,
            Domain.Time.SimTime deadline, CharacterId actor)
        {
            if (decision.PendingResolveEventId.IsSet) world.Scheduler.Cancel(decision.PendingResolveEventId);
            if (decision.ResolveAt != deadline) decision.UpdateLatestResolutionAt(deadline);
            ScheduledEvent scheduled = world.Scheduler.Schedule(deadline, SchedulePhase.Decision,
                ScheduledEventTypes.AutoResolveCommitmentConflict,
                new DecisionResolvePayload(decision.Id, actor),
                new[] { EventDependency.Capture(world.Revisions, new RevisionKey(actor.ToRef(), RevisionAspects.Schedule)) });
            decision.SetPendingResolveEvent(scheduled.Id);
        }

        private static void Dissolve(WorldState world, SimulationContext context, Decision decision)
        {
            if (!decision.IsActive) return;
            if (decision.PendingResolveEventId.IsSet) world.Scheduler.Cancel(decision.PendingResolveEventId);
            decision.Dissolve();
            world.Attention.Release(decision.Id);
            world.Attention.SetPolicy(decision.Id, Vivarium.Domain.Attention.AttentionPolicy.Normal);
            world.DecisionDependencies.Unregister(decision.Id);
            world.CommitmentConflicts.Unregister(decision.Id);
            if (context.Trace.IsEnabled)
                context.Trace.Record("decision-dissolved", $"{world.Clock.Now} dissolved {decision.Id}; refunded {decision.Interventions.Count} intervention(s)");
            world.Publish(new DecisionDissolvedEvent(decision.Id, decision.CharacterId,
                DissolutionReasonOptionSetInvalidated, decision.Interventions, world.Clock.Now));
        }
    }

    /// <summary>Changes commitment intent only. It never invokes Activity or planner code.</summary>
    public sealed class CommitmentConflictDecisionOutcomeHandler : DomainEventHandler<DecisionResolvedEvent>
    {
        private readonly CommitmentLifecycleService _commitments;
        public CommitmentConflictDecisionOutcomeHandler(CommitmentLifecycleService commitments)
            : base(DecisionDomainEventTypes.DecisionResolved) => _commitments = commitments;
        protected override void Handle(DecisionResolvedEvent e, WorldState world, SimulationContext context)
        {
            Decision decision = world.Decisions.Get(e.DecisionId);
            if (decision.CommitmentConflictKey == null) return;
            DecisionOption chosen = null;
            for (int i = 0; i < decision.Options.Count; i++)
                if (decision.Options[i].Id == e.Resolution.ChosenOptionId) chosen = decision.Options[i];
            CommitmentResolutionPlan plan = chosen?.CommitmentResolutionPlan;
            if (plan == null) return;
            var changed = new List<CommitmentId>();
            for (int i = 0; i < plan.Relinquish.Count; i++)
                if (world.Commitments.TryGet(plan.Relinquish[i], out Commitment c) && c.Status == CommitmentStatus.Planned)
                {
                    _commitments.Relinquish(world, c, decision.Id);
                    changed.Add(c.Id);
                }
            if (changed.Count == 0) return;
        }
    }

    /// <summary>Routine planner reaction to the commitment-domain consequence.</summary>
    public sealed class CommitmentIntentPlanningHandler : DomainEventHandler<CommitmentOutcomeOccurredEvent>
    {
        private readonly SchedulePlanner _planner;
        public CommitmentIntentPlanningHandler(SchedulePlanner planner)
            : base(ActivityDomainEventTypes.CommitmentOutcomeOccurred) => _planner = planner;
        protected override void Handle(CommitmentOutcomeOccurredEvent e, WorldState world, SimulationContext context)
        {
            if (e.Outcome.Outcome != CommitmentOutcomeKind.Relinquished) return;
            foreach (Commitment c in world.Commitments.All)
                if (c.CharacterId == e.CharacterId && c.Status == CommitmentStatus.Planned)
                    _planner.TryPlanCommitmentStart(context, c);
        }
    }

    public sealed class ActivityStartedDecisionReevaluationHandler : DomainEventHandler<ActivityStartedEvent>
    {
        private readonly DecisionReevaluationService _reevaluation;
        public ActivityStartedDecisionReevaluationHandler(DecisionReevaluationService reevaluation)
            : base(ActivityDomainEventTypes.ActivityStarted) => _reevaluation = reevaluation;
        protected override void Handle(ActivityStartedEvent e, WorldState world, SimulationContext context) =>
            _reevaluation.ReevaluateDependents(context,
                new DecisionDependencyKey(RevisionAspects.Activity, e.CharacterId.ToRef()));
    }
}
