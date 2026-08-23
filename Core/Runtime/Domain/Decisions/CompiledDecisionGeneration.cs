using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Decisions
{
    /// <summary>Runtime-bound input for a compiled Decision whose Options may refer to world entities.</summary>
    public sealed class CompiledDecisionGenerationRequest
    {
        public CompiledDecisionGenerationRequest(
            CharacterId actor,
            AuthoredId definitionId,
            SimDuration timeToResolve,
            IReadOnlyList<DecisionOption> options,
            DecisionReasoningProgram reasoningProgram,
            IReadOnlyDictionary<AuthoredId, DecisionParameterValue> context = null,
            DecisionConflictScope conflictScope = default,
            int importance = 0)
        {
            Actor = actor;
            DefinitionId = definitionId;
            TimeToResolve = timeToResolve;
            Options = options ?? throw new ArgumentNullException(nameof(options));
            ReasoningProgram = reasoningProgram ?? throw new ArgumentNullException(nameof(reasoningProgram));
            Context = context ?? new SortedDictionary<AuthoredId, DecisionParameterValue>();
            ConflictScope = conflictScope;
            Importance = importance;
        }

        public CharacterId Actor { get; }
        public AuthoredId DefinitionId { get; }
        public SimDuration TimeToResolve { get; }
        public IReadOnlyList<DecisionOption> Options { get; }
        public DecisionReasoningProgram ReasoningProgram { get; }
        public IReadOnlyDictionary<AuthoredId, DecisionParameterValue> Context { get; }
        public DecisionConflictScope ConflictScope { get; }
        public int Importance { get; }
    }

    /// <summary>Creates, reasons about, indexes, schedules, and announces one runtime-bound Decision.</summary>
    public sealed class CompiledDecisionGenerationService
    {
        private readonly DecisionSignalProviderRegistry _providers;
        private readonly CompiledDecisionReasoningService _reasoning = new CompiledDecisionReasoningService();

        public CompiledDecisionGenerationService(DecisionSignalProviderRegistry providers)
        {
            _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        }

        public Decision Generate(WorldState world, CompiledDecisionGenerationRequest request)
        {
            foreach (Decision existing in world.Decisions.All)
            {
                if (existing.IsActive && existing.CharacterId == request.Actor &&
                    (existing.DefinitionId == request.DefinitionId ||
                     (request.ConflictScope.IsSet && existing.ConflictScope.Equals(request.ConflictScope))))
                {
                    return null;
                }
            }

            var decision = new Decision(
                world.RuntimeIds.Decisions.Next(), request.Actor, request.DefinitionId,
                world.Clock.Now, world.Clock.Now.Plus(request.TimeToResolve), request.Options,
                request.ConflictScope, request.Importance);
            foreach (KeyValuePair<AuthoredId, DecisionParameterValue> parameter in request.Context)
            {
                decision.SetContextParameter(parameter.Key, parameter.Value);
            }
            decision.SnapshotReasoningProgram(request.ReasoningProgram);
            world.Decisions.Add(decision.Id, decision);
            _reasoning.EvaluateAndReconcile(world, decision, _providers);

            ScheduledEvent scheduled = world.Scheduler.Schedule(
                decision.ResolveAt,
                SchedulePhase.Decision,
                Activities.ScheduledEventTypes.DecisionResolve,
                new DecisionResolvePayload(decision.Id, request.Actor));
            decision.SetPendingResolveEvent(scheduled.Id);
            world.Publish(new DecisionCreatedEvent(decision.Id, request.Actor, request.DefinitionId));
            return decision;
        }
    }
}
