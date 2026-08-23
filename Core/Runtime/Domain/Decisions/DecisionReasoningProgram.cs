using System;
using System.Collections.Generic;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Evaluation;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Social;
using Vivarium.Domain.Spatial;

namespace Vivarium.Domain.Decisions
{
    public enum DecisionParameterKind
    {
        Integer = 0,
        AuthoredId = 1,
        Entity = 2,
    }

    /// <summary>A small typed semantic value; bindings never use unrestricted object property bags.</summary>
    public readonly struct DecisionParameterValue : IEquatable<DecisionParameterValue>
    {
        private DecisionParameterValue(
            DecisionParameterKind kind,
            long integer,
            AuthoredId authoredId,
            EntityRef entity)
        {
            Kind = kind;
            Integer = integer;
            AuthoredId = authoredId;
            Entity = entity;
        }

        public DecisionParameterKind Kind { get; }
        public long Integer { get; }
        public AuthoredId AuthoredId { get; }
        public EntityRef Entity { get; }

        public static DecisionParameterValue FromInteger(long value) =>
            new DecisionParameterValue(DecisionParameterKind.Integer, value, default, default);
        public static DecisionParameterValue FromAuthoredId(AuthoredId value) =>
            new DecisionParameterValue(DecisionParameterKind.AuthoredId, 0, value, default);
        public static DecisionParameterValue FromEntity(EntityRef value) =>
            new DecisionParameterValue(DecisionParameterKind.Entity, 0, default, value);

        public bool Equals(DecisionParameterValue other) =>
            Kind == other.Kind && Integer == other.Integer && AuthoredId == other.AuthoredId && Entity.Equals(other.Entity);
        public override bool Equals(object obj) => obj is DecisionParameterValue other && Equals(other);
        public override int GetHashCode() => (((int)Kind * 397) ^ Integer.GetHashCode()) ^ AuthoredId.GetHashCode() ^ Entity.GetHashCode();
    }

    public static class DecisionReasoningParameters
    {
        public static readonly AuthoredId Actor = new AuthoredId("decision.parameter.actor");
        public static readonly AuthoredId Target = new AuthoredId("decision.parameter.target");
        public static readonly AuthoredId ValueId = new AuthoredId("decision.parameter.value_id");
        public static readonly AuthoredId RelationshipChannelId = new AuthoredId("decision.parameter.relationship_channel_id");
        public static readonly AuthoredId Urgency = new AuthoredId("decision.parameter.urgency");
        public static readonly AuthoredId SelfOption = new AuthoredId("decision.parameter.self_option");
        public static readonly AuthoredId WaitOption = new AuthoredId("decision.parameter.wait_option");
        public static readonly AuthoredId ActivityModifierId = new AuthoredId("decision.parameter.activity_modifier_id");
    }

    public enum ParameterBindingSource
    {
        DecisionActor = 0,
        DecisionContext = 1,
        OptionContext = 2,
        Literal = 3,
    }

    public readonly struct ConsiderationParameter
    {
        public ConsiderationParameter(AuthoredId id, DecisionParameterKind kind, bool required = true)
        {
            Id = id;
            Kind = kind;
            Required = required;
        }

        public AuthoredId Id { get; }
        public DecisionParameterKind Kind { get; }
        public bool Required { get; }
    }

    public readonly struct CompiledParameterBinding
    {
        public CompiledParameterBinding(
            AuthoredId parameterId,
            ParameterBindingSource source,
            AuthoredId sourceParameterId = default,
            DecisionParameterValue literal = default)
        {
            ParameterId = parameterId;
            Source = source;
            SourceParameterId = sourceParameterId;
            Literal = literal;
        }

        public AuthoredId ParameterId { get; }
        public ParameterBindingSource Source { get; }
        public AuthoredId SourceParameterId { get; }
        public DecisionParameterValue Literal { get; }
    }

    public readonly struct DecisionSignalRequest
    {
        public DecisionSignalRequest(AuthoredId signalId, AuthoredId providerId)
        {
            SignalId = signalId;
            ProviderId = providerId;
        }

        public AuthoredId SignalId { get; }
        public AuthoredId ProviderId { get; }
    }

    public readonly struct ReasonDieThreshold
    {
        public ReasonDieThreshold(long minimumMagnitude, Die die)
        {
            MinimumMagnitude = Math.Max(0, minimumMagnitude);
            Die = die;
        }

        public long MinimumMagnitude { get; }
        public Die Die { get; }
    }

    public sealed class ReasonScaleProfile
    {
        private readonly ReasonDieThreshold[] _thresholds;

        public ReasonScaleProfile(AuthoredId id, IReadOnlyList<ReasonDieThreshold> thresholds)
        {
            Id = id;
            _thresholds = new ReasonDieThreshold[thresholds?.Count ?? 0];
            for (int i = 0; i < _thresholds.Length; i++) _thresholds[i] = thresholds[i];
            Array.Sort(_thresholds, (a, b) => a.MinimumMagnitude.CompareTo(b.MinimumMagnitude));
        }

        public AuthoredId Id { get; }
        public IReadOnlyList<ReasonDieThreshold> Thresholds => _thresholds;

        public Die Map(long signedScore)
        {
            long magnitude = Math.Abs(signedScore);
            Die result = Die.None;
            for (int i = 0; i < _thresholds.Length && magnitude >= _thresholds[i].MinimumMagnitude; i++)
            {
                result = _thresholds[i].Die;
            }
            return result;
        }

        public static ReasonScaleProfile Standard() => new ReasonScaleProfile(
            new AuthoredId("decision.reason_scale.standard"),
            new[]
            {
                new ReasonDieThreshold(1000, Die.D4),
                new ReasonDieThreshold(2500, Die.D6),
                new ReasonDieThreshold(4500, Die.D8),
                new ReasonDieThreshold(6500, Die.D10),
                new ReasonDieThreshold(8500, Die.D12),
            });
    }

    /// <summary>A validated, definition-derived binding safe to snapshot onto an in-flight Decision.</summary>
    public sealed class CompiledConsiderationBinding
    {
        public CompiledConsiderationBinding(
            AuthoredId bindingId,
            AuthoredId considerationId,
            int definitionVersion,
            IReadOnlyList<ConsiderationParameter> parameterSchema,
            IReadOnlyList<CompiledParameterBinding> parameterBindings,
            IReadOnlyList<DecisionSignalRequest> signals,
            SignalFieldDefinition field,
            ReasonChannelDefinition reasonChannel,
            ReasonScaleProfile scale,
            AuthoredId categoryId,
            AuthoredId positiveLabelId,
            AuthoredId negativeLabelId,
            InfluenceVisibility visibility)
        {
            BindingId = bindingId;
            ConsiderationId = considerationId;
            DefinitionVersion = definitionVersion;
            ParameterSchema = parameterSchema ?? new ConsiderationParameter[0];
            ParameterBindings = parameterBindings ?? new CompiledParameterBinding[0];
            Signals = signals ?? new DecisionSignalRequest[0];
            Field = field ?? throw new ArgumentNullException(nameof(field));
            ReasonChannel = reasonChannel ?? throw new ArgumentNullException(nameof(reasonChannel));
            Scale = scale ?? throw new ArgumentNullException(nameof(scale));
            CategoryId = categoryId;
            PositiveLabelId = positiveLabelId;
            NegativeLabelId = negativeLabelId;
            Visibility = visibility;
        }

        public AuthoredId BindingId { get; }
        public AuthoredId ConsiderationId { get; }
        public int DefinitionVersion { get; }
        public IReadOnlyList<ConsiderationParameter> ParameterSchema { get; }
        public IReadOnlyList<CompiledParameterBinding> ParameterBindings { get; }
        public IReadOnlyList<DecisionSignalRequest> Signals { get; }
        public SignalFieldDefinition Field { get; }
        public ReasonChannelDefinition ReasonChannel { get; }
        public ReasonScaleProfile Scale { get; }
        public AuthoredId CategoryId { get; }
        public AuthoredId PositiveLabelId { get; }
        public AuthoredId NegativeLabelId { get; }
        public InfluenceVisibility Visibility { get; }
    }

    public sealed class DecisionReasoningProgram
    {
        private readonly CompiledConsiderationBinding[] _bindings;

        public DecisionReasoningProgram(IReadOnlyList<CompiledConsiderationBinding> bindings)
        {
            _bindings = new CompiledConsiderationBinding[bindings?.Count ?? 0];
            for (int i = 0; i < _bindings.Length; i++) _bindings[i] = Snapshot(bindings[i]);
            Array.Sort(_bindings, (a, b) => a.BindingId.CompareTo(b.BindingId));
        }

        public IReadOnlyList<CompiledConsiderationBinding> Bindings => _bindings;

        private static CompiledConsiderationBinding Snapshot(CompiledConsiderationBinding source)
        {
            var schema = new ConsiderationParameter[source.ParameterSchema.Count];
            for (int i = 0; i < schema.Length; i++) schema[i] = source.ParameterSchema[i];
            var parameterBindings = new CompiledParameterBinding[source.ParameterBindings.Count];
            for (int i = 0; i < parameterBindings.Length; i++) parameterBindings[i] = source.ParameterBindings[i];
            var signals = new DecisionSignalRequest[source.Signals.Count];
            for (int i = 0; i < signals.Length; i++) signals[i] = source.Signals[i];
            var linear = new SignalLinearTerm[source.Field.LinearTerms.Count];
            for (int i = 0; i < linear.Length; i++) linear[i] = source.Field.LinearTerms[i];
            var pairwise = new SignalPairwiseTerm[source.Field.PairwiseTerms.Count];
            for (int i = 0; i < pairwise.Length; i++) pairwise[i] = source.Field.PairwiseTerms[i];
            var ideal = new SortedDictionary<AuthoredId, long>();
            foreach (KeyValuePair<AuthoredId, long> pair in source.Field.IdealPoint) ideal[pair.Key] = pair.Value;
            var factors = new SignalIdealFactor[source.Field.IdealFactors.Count];
            for (int i = 0; i < factors.Length; i++)
            {
                SignalIdealFactor factor = source.Field.IdealFactors[i];
                var coefficients = new SignalLinearTerm[factor.Coefficients.Count];
                for (int c = 0; c < coefficients.Length; c++) coefficients[c] = factor.Coefficients[c];
                factors[i] = new SignalIdealFactor(factor.Id, coefficients, factor.Provenance);
            }
            var thresholds = new ReasonDieThreshold[source.Scale.Thresholds.Count];
            for (int i = 0; i < thresholds.Length; i++) thresholds[i] = source.Scale.Thresholds[i];
            return new CompiledConsiderationBinding(
                source.BindingId, source.ConsiderationId, source.DefinitionVersion,
                schema, parameterBindings, signals,
                new SignalFieldDefinition(
                    source.Field.Id, source.Field.Bias, linear, pairwise, ideal, factors, source.Field.Revision),
                new ReasonChannelDefinition(source.ReasonChannel.Id, source.ReasonChannel.ConsolidationPolicy),
                new ReasonScaleProfile(source.Scale.Id, thresholds), source.CategoryId,
                source.PositiveLabelId, source.NegativeLabelId, source.Visibility);
        }
    }

    public sealed class BoundConsiderationParameters
    {
        private readonly SortedDictionary<AuthoredId, DecisionParameterValue> _values =
            new SortedDictionary<AuthoredId, DecisionParameterValue>();

        public IReadOnlyDictionary<AuthoredId, DecisionParameterValue> Values => _values;
        public void Set(AuthoredId id, DecisionParameterValue value) => _values[id] = value;
        public bool TryGet(AuthoredId id, out DecisionParameterValue value) => _values.TryGetValue(id, out value);
    }

    public static class DecisionParameterBinder
    {
        public static bool TryBind(
            Decision decision,
            DecisionOption option,
            CompiledConsiderationBinding consideration,
            out BoundConsiderationParameters parameters)
        {
            parameters = new BoundConsiderationParameters();
            for (int i = 0; i < consideration.ParameterBindings.Count; i++)
            {
                CompiledParameterBinding binding = consideration.ParameterBindings[i];
                if (TryResolve(decision, option, binding, out DecisionParameterValue value))
                {
                    parameters.Set(binding.ParameterId, value);
                }
            }

            for (int i = 0; i < consideration.ParameterSchema.Count; i++)
            {
                ConsiderationParameter declared = consideration.ParameterSchema[i];
                if (!parameters.TryGet(declared.Id, out DecisionParameterValue value))
                {
                    if (declared.Required) return false;
                    continue;
                }
                if (value.Kind != declared.Kind)
                {
                    throw new InvalidOperationException(
                        $"Binding {consideration.BindingId} resolved {declared.Id} as {value.Kind}, expected {declared.Kind}.");
                }
            }
            return true;
        }

        private static bool TryResolve(
            Decision decision,
            DecisionOption option,
            CompiledParameterBinding binding,
            out DecisionParameterValue value)
        {
            switch (binding.Source)
            {
                case ParameterBindingSource.DecisionActor:
                    value = DecisionParameterValue.FromEntity(decision.CharacterId.ToRef());
                    return true;
                case ParameterBindingSource.DecisionContext:
                    return decision.TryGetContextParameter(binding.SourceParameterId, out value);
                case ParameterBindingSource.OptionContext:
                    return option.TryGetContext(binding.SourceParameterId, out value);
                case ParameterBindingSource.Literal:
                    value = binding.Literal;
                    return true;
                default:
                    value = default;
                    return false;
            }
        }
    }

    public sealed class ResolvedDecisionSignal
    {
        public ResolvedDecisionSignal(SignalValue value, IReadOnlyList<DecisionDependencyKey> dependencies = null)
        {
            Value = value;
            Dependencies = dependencies ?? new DecisionDependencyKey[0];
        }

        public SignalValue Value { get; }
        public IReadOnlyList<DecisionDependencyKey> Dependencies { get; }
    }

    public interface IDecisionSignalProvider
    {
        AuthoredId Id { get; }
        ResolvedDecisionSignal Resolve(
            WorldState world,
            Decision decision,
            DecisionOption option,
            DecisionSignalRequest request,
            BoundConsiderationParameters parameters);
    }

    /// <summary>Small capability registry shared only by the providers real Considerations require.</summary>
    public sealed class DecisionSignalProviderRegistry
    {
        private readonly Dictionary<AuthoredId, IDecisionSignalProvider> _providers =
            new Dictionary<AuthoredId, IDecisionSignalProvider>();

        public void Register(IDecisionSignalProvider provider)
        {
            if (_providers.ContainsKey(provider.Id)) throw new InvalidOperationException($"Duplicate Signal provider {provider.Id}.");
            _providers.Add(provider.Id, provider);
        }

        public ResolvedDecisionSignal Resolve(
            WorldState world,
            Decision decision,
            DecisionOption option,
            DecisionSignalRequest request,
            BoundConsiderationParameters parameters)
        {
            if (!_providers.TryGetValue(request.ProviderId, out IDecisionSignalProvider provider))
            {
                throw new InvalidOperationException($"No Signal provider is registered for {request.ProviderId}.");
            }
            return provider.Resolve(world, decision, option, request, parameters);
        }

        public static DecisionSignalProviderRegistry WithBuiltIns()
        {
            var registry = new DecisionSignalProviderRegistry();
            registry.Register(new DecisionContextSignalProvider());
            registry.Register(new ActorValueSignalProvider());
            registry.Register(new TargetAvailabilitySignalProvider());
            registry.Register(new RelationshipChannelSignalProvider());
            registry.Register(new TravelBurdenSignalProvider());
            registry.Register(new ActivityModifierSignalProvider());
            return registry;
        }
    }

    public static class DecisionSignalProviderIds
    {
        public static readonly AuthoredId DecisionContext = new AuthoredId("decision.signal_provider.context");
        public static readonly AuthoredId ActorValue = new AuthoredId("decision.signal_provider.actor_value");
        public static readonly AuthoredId TargetAvailability = new AuthoredId("decision.signal_provider.target_availability");
        public static readonly AuthoredId RelationshipChannel = new AuthoredId("decision.signal_provider.relationship_channel");
        public static readonly AuthoredId TravelBurden = new AuthoredId("decision.signal_provider.travel_burden");
        public static readonly AuthoredId ActivityModifier = new AuthoredId("decision.signal_provider.activity_modifier");
        public static readonly AuthoredId[] BuiltIns =
        {
            DecisionContext, ActorValue, TargetAvailability, RelationshipChannel, TravelBurden, ActivityModifier,
        };
    }

    internal static class DecisionSignalParameters
    {
        public static bool TryTarget(BoundConsiderationParameters parameters, out CharacterId target)
        {
            target = default;
            if (!parameters.TryGet(DecisionReasoningParameters.Target, out DecisionParameterValue value) ||
                value.Kind != DecisionParameterKind.Entity ||
                value.Entity.Kind != EntityKind.Character)
            {
                return false;
            }
            target = new CharacterId(value.Entity.RuntimeId);
            return true;
        }
    }

    public sealed class DecisionContextSignalProvider : IDecisionSignalProvider
    {
        public AuthoredId Id => DecisionSignalProviderIds.DecisionContext;

        public ResolvedDecisionSignal Resolve(
            WorldState world, Decision decision, DecisionOption option, DecisionSignalRequest request,
            BoundConsiderationParameters parameters)
        {
            return parameters.TryGet(request.SignalId, out DecisionParameterValue value) &&
                   value.Kind == DecisionParameterKind.Integer
                ? new ResolvedDecisionSignal(
                    new SignalValue(request.SignalId, value.Integer, 0, SignalApplicability.Known),
                    new[]
                    {
                        new DecisionDependencyKey(
                            RevisionAspects.Scoped(RevisionAspects.DecisionContext, request.SignalId),
                            decision.Id.ToRef()),
                    })
                : new ResolvedDecisionSignal(new SignalValue(request.SignalId, 0, 0, SignalApplicability.Unknown));
        }
    }

    /// <summary>Exposes whether the deciding character's current Activity carries one authored modifier.</summary>
    public sealed class ActivityModifierSignalProvider : IDecisionSignalProvider
    {
        public AuthoredId Id => DecisionSignalProviderIds.ActivityModifier;

        public ResolvedDecisionSignal Resolve(
            WorldState world, Decision decision, DecisionOption option, DecisionSignalRequest request,
            BoundConsiderationParameters parameters)
        {
            var dependency = new DecisionDependencyKey(request.SignalId, decision.CharacterId.ToRef());
            var revisionKey = new RevisionKey(decision.CharacterId.ToRef(), RevisionAspects.Activity);
            if (!parameters.TryGet(DecisionReasoningParameters.ActivityModifierId, out DecisionParameterValue modifier) ||
                modifier.Kind != DecisionParameterKind.AuthoredId || !modifier.AuthoredId.IsSet)
            {
                return new ResolvedDecisionSignal(
                    new SignalValue(
                        request.SignalId, 0, 0, SignalApplicability.Unknown,
                        world.Revisions.Get(revisionKey)),
                    new[] { dependency });
            }

            bool present = world.TryGetCurrentActivity(decision.CharacterId, out ActivityInstance activity) &&
                activity.HasModifier(modifier.AuthoredId);
            return new ResolvedDecisionSignal(
                new SignalValue(
                    request.SignalId,
                    present ? SignalNumeric.Scale : 0,
                    0,
                    SignalApplicability.Known,
                    world.Revisions.Get(revisionKey)),
                new[] { dependency });
        }
    }

    public sealed class ActorValueSignalProvider : IDecisionSignalProvider
    {
        public AuthoredId Id => DecisionSignalProviderIds.ActorValue;

        public ResolvedDecisionSignal Resolve(
            WorldState world, Decision decision, DecisionOption option, DecisionSignalRequest request,
            BoundConsiderationParameters parameters)
        {
            if (!parameters.TryGet(DecisionReasoningParameters.ValueId, out DecisionParameterValue tag) ||
                tag.Kind != DecisionParameterKind.AuthoredId)
            {
                return new ResolvedDecisionSignal(new SignalValue(request.SignalId, 0, 0, SignalApplicability.Unknown));
            }
            CharacterId actor = decision.CharacterId;
            Character character = world.Characters.Get(actor);
            var dependency = new DecisionDependencyKey(
                RevisionAspects.Scoped(RevisionAspects.CharacterValue, tag.AuthoredId),
                actor.ToRef());
            return new ResolvedDecisionSignal(
                new SignalValue(request.SignalId, character.Values.Intensity(tag.AuthoredId), 0, SignalApplicability.Known, character.Values.Revision),
                new[] { dependency });
        }
    }

    public sealed class TargetAvailabilitySignalProvider : IDecisionSignalProvider
    {
        public AuthoredId Id => DecisionSignalProviderIds.TargetAvailability;

        public ResolvedDecisionSignal Resolve(
            WorldState world, Decision decision, DecisionOption option, DecisionSignalRequest request,
            BoundConsiderationParameters parameters)
        {
            if (!DecisionSignalParameters.TryTarget(parameters, out CharacterId target))
            {
                return new ResolvedDecisionSignal(new SignalValue(request.SignalId, 0, 0, SignalApplicability.NotApplicable));
            }
            bool active = world.Characters.TryGet(target, out Character character) && character.IsActive;
            bool traveling = world.TryGetCurrentActivity(target, out ActivityInstance activity) && activity.SpatialContext.IsTraveling;
            long value = active && !traveling ? SignalNumeric.Scale : -SignalNumeric.Scale;
            return new ResolvedDecisionSignal(
                new SignalValue(request.SignalId, value, 0, SignalApplicability.Known),
                new[]
                {
                    new DecisionDependencyKey(RevisionAspects.Activity, target.ToRef()),
                    new DecisionDependencyKey(RevisionAspects.CharacterLifecycle, target.ToRef()),
                });
        }
    }

    public sealed class RelationshipChannelSignalProvider : IDecisionSignalProvider
    {
        public AuthoredId Id => DecisionSignalProviderIds.RelationshipChannel;

        public ResolvedDecisionSignal Resolve(
            WorldState world, Decision decision, DecisionOption option, DecisionSignalRequest request,
            BoundConsiderationParameters parameters)
        {
            if (!DecisionSignalParameters.TryTarget(parameters, out CharacterId target) ||
                !parameters.TryGet(DecisionReasoningParameters.RelationshipChannelId, out DecisionParameterValue channel) ||
                channel.Kind != DecisionParameterKind.AuthoredId)
            {
                return new ResolvedDecisionSignal(new SignalValue(request.SignalId, 0, 0, SignalApplicability.NotApplicable));
            }
            long value = 0;
            var dependencies = new List<DecisionDependencyKey>();
            if (world.RelationshipIndex.TryGetBetween(decision.CharacterId, target, out RelationshipId relationshipId))
            {
                Relationship relationship = world.Relationships.Get(relationshipId);
                value = relationship.From(decision.CharacterId).ChannelAt(channel.AuthoredId, world.Clock.Now);
                dependencies.Add(new DecisionDependencyKey(
                    RevisionAspects.Scoped(SocialDecisionDependencies.RelationshipContext, new AuthoredId("target." + target.Value)),
                    decision.CharacterId.ToRef()));
            }
            return new ResolvedDecisionSignal(
                new SignalValue(request.SignalId, value, 0, SignalApplicability.Known),
                dependencies);
        }
    }

    public sealed class TravelBurdenSignalProvider : IDecisionSignalProvider
    {
        public AuthoredId Id => DecisionSignalProviderIds.TravelBurden;

        public ResolvedDecisionSignal Resolve(
            WorldState world, Decision decision, DecisionOption option, DecisionSignalRequest request,
            BoundConsiderationParameters parameters)
        {
            if (!DecisionSignalParameters.TryTarget(parameters, out CharacterId target) ||
                !world.TryGetSpatialContext(decision.CharacterId, out ActivitySpatialContext actorContext) ||
                !world.TryGetSpatialContext(target, out ActivitySpatialContext targetContext) ||
                !actorContext.IsLocated || !targetContext.IsLocated)
            {
                return new ResolvedDecisionSignal(new SignalValue(request.SignalId, 0, 0, SignalApplicability.NotApplicable));
            }

            if (!world.TravelNetwork.TryPlanRoute(actorContext.LocationId, targetContext.LocationId, out TravelPlan plan))
            {
                return new ResolvedDecisionSignal(new SignalValue(request.SignalId, SignalNumeric.Scale, 0, SignalApplicability.Known));
            }
            long burden = IntegerMath.Clamp(plan.TotalCost.TotalMinutes * 500, 0, SignalNumeric.Scale);
            return new ResolvedDecisionSignal(
                new SignalValue(request.SignalId, burden, 0, SignalApplicability.Known),
                new[]
                {
                    new DecisionDependencyKey(RevisionAspects.Activity, decision.CharacterId.ToRef()),
                    new DecisionDependencyKey(RevisionAspects.Activity, target.ToRef()),
                });
        }
    }

    public sealed class DecisionReasoningEvaluation
    {
        public DecisionReasoningEvaluation(
            IReadOnlyList<CandidateReason> reasons,
            IReadOnlyList<DecisionReasoningDependencyRoute> dependencyRoutes)
        {
            Reasons = reasons;
            DependencyRoutes = dependencyRoutes;
        }

        public IReadOnlyList<CandidateReason> Reasons { get; }
        public IReadOnlyList<DecisionReasoningDependencyRoute> DependencyRoutes { get; }
    }

    public sealed class CompiledDecisionReasoningEvaluator
    {
        private readonly SignalFieldEvaluator _fields = new SignalFieldEvaluator();
        private readonly ReasonConsolidator _consolidator = new ReasonConsolidator();

        public IReadOnlyList<CandidateReason> Evaluate(
            WorldState world,
            Decision decision,
            DecisionSignalProviderRegistry providers) => EvaluateDetailed(world, decision, providers).Reasons;

        public DecisionReasoningEvaluation EvaluateDetailed(
            WorldState world,
            Decision decision,
            DecisionSignalProviderRegistry providers,
            IReadOnlyCollection<DecisionReasoningRoute> selectedRoutes = null)
        {
            if (decision.ReasoningProgram == null)
            {
                return new DecisionReasoningEvaluation(
                    new CandidateReason[0], new DecisionReasoningDependencyRoute[0]);
            }
            var candidates = new List<CandidateReason>();
            var routes = new List<DecisionReasoningDependencyRoute>();
            SortedSet<DecisionReasoningRoute> selected = selectedRoutes == null
                ? null
                : new SortedSet<DecisionReasoningRoute>(selectedRoutes);
            for (int b = 0; b < decision.ReasoningProgram.Bindings.Count; b++)
            {
                CompiledConsiderationBinding binding = decision.ReasoningProgram.Bindings[b];
                for (int o = 0; o < decision.Options.Count; o++)
                {
                    DecisionOption option = decision.Options[o];
                    var route = new DecisionReasoningRoute(decision.Id, binding.BindingId, option.Id);
                    if (selected != null && !selected.Contains(route)) continue;
                    if (!DecisionParameterBinder.TryBind(decision, option, binding, out BoundConsiderationParameters parameters))
                    {
                        continue;
                    }

                    var vector = new SignalVector();
                    var dependencies = new SortedSet<DecisionDependencyKey>();
                    var signalEvidence = new List<DecisionSignalEvidence>();
                    bool applicable = true;
                    for (int s = 0; s < binding.Signals.Count; s++)
                    {
                        ResolvedDecisionSignal resolved = providers.Resolve(
                            world, decision, option, binding.Signals[s], parameters);
                        signalEvidence.Add(new DecisionSignalEvidence(
                            resolved.Value.SignalId, resolved.Value.Mean, resolved.Value.Variance,
                            resolved.Value.Applicability, resolved.Value.SourceRevision));
                        if (!resolved.Value.CanEvaluate)
                        {
                            applicable = false;
                            break;
                        }
                        vector.Set(resolved.Value);
                        for (int d = 0; d < resolved.Dependencies.Count; d++) dependencies.Add(resolved.Dependencies[d]);
                    }
                    foreach (DecisionDependencyKey dependency in dependencies)
                    {
                        routes.Add(new DecisionReasoningDependencyRoute(
                            dependency,
                            route));
                    }
                    if (!applicable) continue;

                    SignalFieldEvaluation result = _fields.Evaluate(vector, binding.Field);
                    Die die = binding.Scale.Map(result.ExpectedBoundedScore);
                    if (!die.IsSet) continue;
                    InfluencePolarity polarity = result.ExpectedBoundedScore >= 0
                        ? InfluencePolarity.Supporting
                        : InfluencePolarity.Opposing;
                    var orderedDependencies = new List<DecisionDependencyKey>(dependencies);
                    DecisionDependencyKey primary = orderedDependencies.Count > 0 ? orderedDependencies[0] : default;
                    if (orderedDependencies.Count > 0) orderedDependencies.RemoveAt(0);
                    EntityRef subject = default;
                    if (parameters.TryGet(DecisionReasoningParameters.Target, out DecisionParameterValue target) &&
                        target.Kind == DecisionParameterKind.Entity)
                    {
                        subject = target.Entity;
                    }
                    else if (parameters.TryGet(DecisionReasoningParameters.Actor, out DecisionParameterValue actor) &&
                        actor.Kind == DecisionParameterKind.Entity)
                    {
                        subject = actor.Entity;
                    }
                    var contributionEvidence = new DecisionContributionEvidence[result.Contributions.Count];
                    for (int c = 0; c < contributionEvidence.Length; c++)
                    {
                        contributionEvidence[c] = new DecisionContributionEvidence(
                            (int)result.Contributions[c].Kind,
                            result.Contributions[c].SourceId,
                            result.Contributions[c].Amount);
                    }
                    candidates.Add(new CandidateReason(
                        option.Id,
                        binding.ConsiderationId,
                        binding.ReasonChannel,
                        result.ExpectedBoundedScore,
                        result.BoundedVariance,
                        die,
                        polarity,
                        binding.CategoryId,
                        polarity == InfluencePolarity.Supporting ? binding.PositiveLabelId : binding.NegativeLabelId,
                        binding.Visibility,
                        primary,
                        subject,
                        additionalDependencies: orderedDependencies,
                        bindingId: binding.BindingId,
                        evaluation: new DecisionReasonEvaluation(
                            result.ExpectedBoundedScore, result.BoundedVariance,
                            signalEvidence, contributionEvidence)));
                }
            }
            return new DecisionReasoningEvaluation(_consolidator.Consolidate(candidates), routes);
        }
    }

    public sealed class CompiledDecisionReasoningService
    {
        private readonly CompiledDecisionReasoningEvaluator _evaluator = new CompiledDecisionReasoningEvaluator();
        private readonly DecisionReasonReconciler _reconciler = new DecisionReasonReconciler();

        public int EvaluateAndReconcile(
            WorldState world,
            Decision decision,
            DecisionSignalProviderRegistry providers,
            IReadOnlyCollection<DecisionReasoningRoute> selectedRoutes = null)
        {
            DecisionReasoningEvaluation evaluation = _evaluator.EvaluateDetailed(
                world, decision, providers, selectedRoutes);
            if (selectedRoutes == null)
            {
                world.DecisionDependencies.ReplaceReasoningRoutes(decision, evaluation.DependencyRoutes);
            }
            else
            {
                world.DecisionDependencies.ReplaceReasoningRoutes(
                    decision, evaluation.DependencyRoutes, selectedRoutes);
            }
            return _reconciler.Reconcile(decision, evaluation.Reasons, selectedRoutes);
        }

        public int RebuildRoutes(
            WorldState world,
            Decision decision,
            DecisionSignalProviderRegistry providers)
        {
            DecisionReasoningEvaluation evaluation = _evaluator.EvaluateDetailed(world, decision, providers);
            world.DecisionDependencies.ReplaceReasoningRoutes(decision, evaluation.DependencyRoutes);
            return evaluation.DependencyRoutes.Count;
        }
    }

    public sealed class CompiledDecisionInfluenceReevaluator : IDecisionInfluenceReevaluator
    {
        private readonly DecisionSignalProviderRegistry _providers;
        private readonly CompiledDecisionReasoningService _reasoning = new CompiledDecisionReasoningService();

        public CompiledDecisionInfluenceReevaluator(
            AuthoredId decisionDefinitionId,
            DecisionSignalProviderRegistry providers)
        {
            DecisionDefinitionId = decisionDefinitionId;
            _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        }

        public AuthoredId DecisionDefinitionId { get; }

        public void Reevaluate(
            WorldState world,
            Decision decision,
            DecisionDependencyKey changedKey,
            SimulationContext context)
        {
            IReadOnlyCollection<DecisionReasoningRoute> indexed =
                world.DecisionDependencies.ReasoningRoutesDependingOn(changedKey);
            var selected = new List<DecisionReasoningRoute>();
            foreach (DecisionReasoningRoute route in indexed)
            {
                if (route.DecisionId == decision.Id) selected.Add(route);
            }
            if (selected.Count > 0) _reasoning.EvaluateAndReconcile(world, decision, _providers, selected);
        }
    }

    /// <summary>Reconciles a complete compiled evaluation without reallocating semantic reasons.</summary>
    public sealed class DecisionReasonReconciler
    {
        private readonly DecisionReasoningInfluenceFactory _factory = new DecisionReasoningInfluenceFactory();

        public int Reconcile(
            Decision decision,
            IReadOnlyList<CandidateReason> reasons,
            IReadOnlyCollection<DecisionReasoningRoute> selectedRoutes = null)
        {
            if (decision == null || reasons == null) throw new ArgumentNullException("Decision and reasons are required.");
            var desired = new SortedSet<string>(StringComparer.Ordinal);
            int changed = 0;
            for (int i = 0; i < reasons.Count; i++)
            {
                CandidateReason reason = reasons[i];
                string key = Key(reason.BindingId, reason.OptionId, reason.Channel.Id);
                desired.Add(key);
                DecisionInfluence existing = decision.FindReasonInfluence(
                    reason.BindingId, reason.OptionId, reason.Channel.Id);
                if (existing == null)
                {
                    _factory.Add(decision, reason);
                    changed++;
                }
                else if (decision.UpdateReasonInfluence(existing.Id, reason))
                {
                    changed++;
                }
            }

            if (decision.ReasoningProgram == null) return changed;
            SortedSet<DecisionReasoningRoute> selected = selectedRoutes == null
                ? null
                : new SortedSet<DecisionReasoningRoute>(selectedRoutes);
            var programBindings = new SortedSet<AuthoredId>();
            for (int i = 0; i < decision.ReasoningProgram.Bindings.Count; i++)
            {
                programBindings.Add(decision.ReasoningProgram.Bindings[i].BindingId);
            }
            for (int i = 0; i < decision.Influences.Count; i++)
            {
                DecisionInfluence influence = decision.Influences[i];
                if (programBindings.Contains(influence.ReasonBindingId) &&
                    (selected == null || selected.Contains(new DecisionReasoningRoute(
                        decision.Id, influence.ReasonBindingId, influence.OptionId))) &&
                    !desired.Contains(Key(influence.ReasonBindingId, influence.OptionId, influence.ReasonChannelId)) &&
                    decision.RetractInfluence(influence.Id))
                {
                    changed++;
                }
            }
            return changed;
        }

        private static string Key(AuthoredId binding, AuthoredId option, AuthoredId channel) =>
            binding.Value + "\n" + option.Value + "\n" + channel.Value;
    }

}
