using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Evaluation;
using Vivarium.Domain.Events;
using Vivarium.Domain.History;
using Vivarium.Domain.Randomness;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Social;
using Vivarium.Domain.Time;
using Xunit;

namespace Vivarium.Domain.Tests
{
    public sealed class DecisionReasoningTests
    {
        private static readonly CharacterId Actor = new CharacterId(1);
        private static readonly CharacterId Target = new CharacterId(2);
        private static readonly AuthoredId Seek = new AuthoredId("option.seek");
        private static readonly AuthoredId Avoid = new AuthoredId("option.avoid");

        [Theory]
        [InlineData(7000, "option.seek", "influence.enjoys")]
        [InlineData(-7000, "option.avoid", "influence.avoids")]
        public void ConsiderationPathPreservesFormerSocialInfluenceObservableResult(
            long normalized,
            string expectedOption,
            string expectedLabel)
        {
            CompositeSocialEvaluationResult evaluation = Evaluation(normalized, AppraisalStrength.Strong);
            SocialDecisionInfluenceSpec spec = Spec();
            Decision reasoningDecision = Decision(2);

            CandidateReason candidate = new InterpersonalComfortConsideration().Evaluate(
                reasoningDecision, Target, evaluation, spec);
            IReadOnlyList<CandidateReason> consolidated = new ReasonConsolidator().Consolidate(
                new[] { candidate });
            DecisionInfluence reasoning = new DecisionReasoningInfluenceFactory().Add(
                reasoningDecision, consolidated[0]);

            Assert.Equal(new AuthoredId(expectedOption), reasoning.OptionId);
            Assert.Equal(new AuthoredId(expectedLabel), reasoning.LabelId);
            Assert.Equal(new AuthoredId("cat.social"), reasoning.Category);
            Assert.Equal(Die.D8, reasoning.CurrentDie);
            Assert.Equal(InfluenceVisibility.Full, reasoning.DefaultVisibility);
            Assert.Equal(
                new DecisionDependencyKey(
                    RevisionAspects.Scoped(
                        SocialDecisionDependencies.BeliefContext,
                        new AuthoredId("target.2")),
                    Actor.ToRef()),
                reasoning.DependencyKey);
            Assert.Equal(Target.ToRef(), reasoning.Subject);
            Assert.Equal(InfluencePolarity.Supporting, reasoning.Polarity);
            Assert.Equal(ReasonChannelIds.InterpersonalComfort, reasoning.ReasonChannelId);
            Assert.Equal(3, reasoningDecision.DependencyKeys.Count);
        }

        [Fact]
        public void DefaultReasonChannelDoesNotStackCorrelatedCandidates()
        {
            Decision decision = Decision(1);
            SocialDecisionInfluenceSpec spec = Spec();
            CandidateReason mild = new InterpersonalComfortConsideration().Evaluate(
                decision, Target, Evaluation(2500, AppraisalStrength.Minor), spec);
            CandidateReason strong = new InterpersonalComfortConsideration().Evaluate(
                decision, Target, Evaluation(7000, AppraisalStrength.Strong), spec);

            IReadOnlyList<CandidateReason> result = new ReasonConsolidator().Consolidate(new[] { mild, strong });

            Assert.Single(result);
            Assert.Equal(Die.D8, result[0].GameplayDie);
        }

        [Fact]
        public void DefaultReasonChannelKeepsDistinctBoundTargetsSeparate()
        {
            Decision decision = Decision(1);
            SocialDecisionInfluenceSpec spec = Spec();
            CandidateReason first = new InterpersonalComfortConsideration().Evaluate(
                decision, Target, Evaluation(2500, AppraisalStrength.Minor), spec);
            CandidateReason second = new InterpersonalComfortConsideration().Evaluate(
                decision, new CharacterId(Target.Value + 1), Evaluation(2500, AppraisalStrength.Minor), spec);

            IReadOnlyList<CandidateReason> result = new ReasonConsolidator().Consolidate(new[] { first, second });

            Assert.Equal(2, result.Count);
            Assert.NotEqual(result[0].Subject, result[1].Subject);
        }

        [Fact]
        public void CompiledProgramEvaluatesTargetAndTargetlessOptionsInOneDecision()
        {
            var world = new WorldState(41, SimTime.Epoch);
            var mira = AddCharacter(world, "Mira");
            var darius = AddCharacter(world, "Darius");
            var glen = AddCharacter(world, "Glen");
            var priya = AddCharacter(world, "Priya");
            priya.Retire(world.Clock.Now);
            var independence = new AuthoredId("value.independence");
            mira.Values.Set(independence, 7000);

            DecisionOption[] options =
            {
                TargetOption("option.ask_darius", 0, darius.Id),
                TargetOption("option.ask_glen", 1, glen.Id),
                TargetOption("option.ask_priya", 2, priya.Id),
                MarkerOption("option.go_alone", 3, DecisionReasoningParameters.SelfOption),
                MarkerOption("option.wait", 4, DecisionReasoningParameters.WaitOption),
            };
            var decision = new Decision(
                new DecisionId(1), mira.Id, new AuthoredId("decision.generator_repair"),
                world.Clock.Now, world.Clock.Now.Plus(SimDuration.FromMinutes(10)), options);
            decision.SetContextParameter(
                DecisionReasoningParameters.Urgency,
                DecisionParameterValue.FromInteger(8000));
            decision.SnapshotReasoningProgram(Program(independence));
            world.Decisions.Add(decision.Id, decision);

            var providers = new DecisionSignalProviderRegistry();
            providers.Register(new TargetAvailabilitySignalProvider());
            providers.Register(new ActorValueSignalProvider());
            providers.Register(new DecisionContextSignalProvider());
            DecisionReasoningEvaluation evaluation = new CompiledDecisionReasoningEvaluator().EvaluateDetailed(
                world, decision, providers);
            IReadOnlyList<CandidateReason> reasons = evaluation.Reasons;

            Assert.Equal(5, reasons.Count);
            AssertReason(reasons, "option.ask_darius", InfluencePolarity.Supporting);
            AssertReason(reasons, "option.ask_glen", InfluencePolarity.Supporting);
            AssertReason(reasons, "option.ask_priya", InfluencePolarity.Opposing);
            AssertReason(reasons, "option.go_alone", InfluencePolarity.Supporting);
            AssertReason(reasons, "option.wait", InfluencePolarity.Opposing);

            Assert.Equal(5, new CompiledDecisionReasoningService().EvaluateAndReconcile(world, decision, providers));
            Assert.Equal(5, decision.Influences.Count);
            Assert.DoesNotContain(decision.DependencyKeys, dependency =>
                dependency.ContextKind.Value.Contains("social_appraisal"));

            IReadOnlyCollection<DecisionReasoningRoute> valueRoutes = world.DecisionDependencies.ReasoningRoutesDependingOn(
                new DecisionDependencyKey(
                    RevisionAspects.Scoped(RevisionAspects.CharacterValue, independence), mira.Id.ToRef()));
            DecisionReasoningRoute valueRoute = Assert.Single(valueRoutes);
            Assert.Equal(new AuthoredId("binding.self_reliance"), valueRoute.BindingId);
            Assert.Equal(new AuthoredId("option.go_alone"), valueRoute.OptionId);

            IReadOnlyCollection<DecisionReasoningRoute> urgencyRoutes = world.DecisionDependencies.ReasoningRoutesDependingOn(
                new DecisionDependencyKey(
                    RevisionAspects.Scoped(RevisionAspects.DecisionContext, DecisionReasoningParameters.Urgency),
                    decision.Id.ToRef()));
            Assert.Equal(new AuthoredId("option.wait"), Assert.Single(urgencyRoutes).OptionId);

            DecisionInfluence selfInfluence = null;
            for (int i = 0; i < decision.Influences.Count; i++)
            {
                if (decision.Influences[i].OptionId == new AuthoredId("option.go_alone"))
                {
                    selfInfluence = decision.Influences[i];
                    break;
                }
            }
            Assert.NotNull(selfInfluence);
            int before = decision.InfluenceRevision;
            mira.Values.Set(independence, 10000);
            var reevaluation = new DecisionReevaluationService();
            reevaluation.Register(new CompiledDecisionInfluenceReevaluator(decision.DefinitionId, providers));
            var simulation = new SimulationContext(
                world, new DeterministicRandomOracle(world.WorldSeed), SimulationMode.Live, 1, 1);
            Assert.Equal(1, reevaluation.ReevaluateDependents(
                simulation,
                new DecisionDependencyKey(
                    RevisionAspects.Scoped(RevisionAspects.CharacterValue, independence), mira.Id.ToRef())));
            Assert.Equal(Die.D8, selfInfluence.BaseDie);
            Assert.Equal(before + 1, decision.InfluenceRevision);
            Assert.Single(world.DecisionDependencies.ReasoningRoutesDependingOn(
                new DecisionDependencyKey(
                    RevisionAspects.Scoped(RevisionAspects.DecisionContext, DecisionReasoningParameters.Urgency),
                    decision.Id.ToRef())));
        }

        [Fact]
        public void ReevaluationPreservesSemanticInfluenceAndReplaysMagnitudeIntervention()
        {
            var world = new WorldState(42, SimTime.Epoch);
            Character mira = AddCharacter(world, "Mira");
            var independence = new AuthoredId("value.independence");
            mira.Values.Set(independence, 7000);
            var decision = new Decision(
                new DecisionId(1), mira.Id, new AuthoredId("decision.generator_repair"),
                world.Clock.Now, world.Clock.Now.Plus(SimDuration.FromMinutes(10)),
                new[]
                {
                    MarkerOption("option.go_alone", 0, DecisionReasoningParameters.SelfOption),
                    new DecisionOption(new AuthoredId("option.wait"), new AuthoredId("option.wait.label"), 1),
                });
            decision.SnapshotReasoningProgram(Program(independence));
            var providers = new DecisionSignalProviderRegistry();
            providers.Register(new ActorValueSignalProvider());
            var evaluator = new CompiledDecisionReasoningEvaluator();
            var reconciler = new DecisionReasonReconciler();

            Assert.Equal(1, reconciler.Reconcile(decision, evaluator.Evaluate(world, decision, providers)));
            DecisionInfluence influence = Assert.Single(decision.Influences);
            DecisionInfluenceId stableId = influence.Id;
            Assert.Equal(Die.D6, influence.BaseDie);

            var stepUp = new InterventionDefinition(
                new AuthoredId("intervention.encourage"), InterventionKind.StepDieUp, 0);
            DecisionInterventionRules.Apply(decision, stepUp, stableId, 17);
            Assert.Equal(Die.D8, influence.CurrentDie);

            mira.Values.Set(independence, 10000);
            Assert.Equal(1, reconciler.Reconcile(decision, evaluator.Evaluate(world, decision, providers)));
            Assert.Equal(stableId, influence.Id);
            Assert.Equal(Die.D8, influence.BaseDie);
            Assert.Equal(Die.D10, influence.CurrentDie);
            Assert.Equal(stableId, Assert.Single(decision.Interventions).TargetInfluenceId);

            mira.Values.Set(independence, 0);
            Assert.Equal(1, reconciler.Reconcile(decision, evaluator.Evaluate(world, decision, providers)));
            Assert.True(influence.IsRetracted);

            mira.Values.Set(independence, -7000);
            Assert.Equal(1, reconciler.Reconcile(decision, evaluator.Evaluate(world, decision, providers)));
            Assert.False(influence.IsRetracted);
            Assert.Equal(stableId, influence.Id);
            Assert.Equal(InfluencePolarity.Opposing, influence.Polarity);
            Assert.Equal(Die.D6, influence.BaseDie);
            Assert.Equal(Die.D8, influence.CurrentDie);
            Assert.Single(decision.Influences);
        }

        [Fact]
        public void ImplementedStateScenarioGeneratesSchedulesAndResolvesThroughNormalServices()
        {
            var world = new WorldState(9173, SimTime.Epoch);
            Character mira = AddCharacter(world, "Mira");
            Character darius = AddCharacter(world, "Darius");
            Character glen = AddCharacter(world, "Glen");
            Character priya = AddCharacter(world, "Priya");
            priya.Retire(world.Clock.Now);
            var independence = new AuthoredId("value.independence");
            mira.Values.Set(independence, 7000);
            var providers = new DecisionSignalProviderRegistry();
            providers.Register(new TargetAvailabilitySignalProvider());
            providers.Register(new ActorValueSignalProvider());
            providers.Register(new DecisionContextSignalProvider());
            var context = new SortedDictionary<AuthoredId, DecisionParameterValue>
            {
                [DecisionReasoningParameters.Urgency] = DecisionParameterValue.FromInteger(8000),
            };
            var request = new CompiledDecisionGenerationRequest(
                mira.Id,
                new AuthoredId("decision.generator_repair"),
                SimDuration.FromMinutes(10),
                new[]
                {
                    TargetOption("option.ask_darius", 0, darius.Id),
                    TargetOption("option.ask_glen", 1, glen.Id),
                    TargetOption("option.ask_priya", 2, priya.Id),
                    MarkerOption("option.go_alone", 3, DecisionReasoningParameters.SelfOption),
                    MarkerOption("option.wait", 4, DecisionReasoningParameters.WaitOption),
                },
                Program(independence),
                context);

            Decision decision = new CompiledDecisionGenerationService(providers).Generate(world, request);

            Assert.NotNull(decision);
            Assert.Same(decision, world.Decisions.Get(decision.Id));
            Assert.True(decision.PendingResolveEventId.IsSet);
            Assert.Equal(5, decision.Influences.Count);
            Assert.True(world.DomainEvents.TryDequeue(out IDomainEvent created));
            Assert.IsType<DecisionCreatedEvent>(created);

            var simulation = new SimulationContext(
                world, new DeterministicRandomOracle(world.WorldSeed), SimulationMode.Live, 1, 1);
            DecisionResolution resolution = new DecisionResolutionService().Resolve(decision, simulation);
            Assert.Equal(5, resolution.OptionTotals.Count);
            Assert.Equal(5, resolution.Rolls.Count);
            Assert.Contains(resolution.OptionTotals, total =>
                total.OptionId == new AuthoredId("option.ask_priya") && total.Total < 0);

            InfluenceRoll selfRoll = default;
            for (int i = 0; i < resolution.Rolls.Count; i++)
            {
                if (resolution.Rolls[i].OptionId == new AuthoredId("option.go_alone")) selfRoll = resolution.Rolls[i];
            }
            Assert.NotNull(selfRoll.Reason);
            Assert.Equal(7000, Assert.Single(selfRoll.Reason.Evaluation.Signals).Mean);
            decision.Resolve(resolution);
            mira.Values.Set(independence, -10000);
            Assert.Equal(7000, Assert.Single(selfRoll.Reason.Evaluation.Signals).Mean);
        }

        [Fact]
        public void FrozenEvidencePrunesWithItsLinkedDecisionHistory()
        {
            var world = new WorldState(3, SimTime.Epoch);
            Character actor = AddCharacter(world, "Mira");
            Decision decision = new Decision(
                world.RuntimeIds.Decisions.Next(), actor.Id, new AuthoredId("decision.retained"),
                world.Clock.Now, world.Clock.Now,
                new[]
                {
                    new DecisionOption(new AuthoredId("option.a"), new AuthoredId("label.a"), 0),
                    new DecisionOption(new AuthoredId("option.b"), new AuthoredId("label.b"), 1),
                });
            decision.Resolve(new DecisionResolution(
                new AuthoredId("option.a"), DegreeOfSuccess.Marginal, world.Clock.Now,
                new OptionTotal[0], new InfluenceRoll[0], Activities.OutcomeSource.Automatic));
            HistoryEntry history = world.HistoryLedger.Record(
                new AuthoredId("history.decision_resolved"), world.Clock.Now, RetentionTier.Significant, "resolved");
            decision.LinkResolutionHistory(history.Id);
            world.Decisions.Add(decision.Id, decision);

            int removed = new DecisionHistoryRetentionService().Prune(
                world, world.Clock.Now.Plus(SimDuration.FromMinutes(1)), RetentionTier.Significant);

            Assert.Equal(1, removed);
            Assert.False(world.Decisions.Contains(decision.Id));
            Assert.False(world.HistoryLedger.TryGet(history.Id, out _));
        }

        [Fact]
        public void ReasoningLintRejectsUnknownProvidersUnrequestedSignalsAndImpossibleOptionBindings()
        {
            var target = DecisionReasoningParameters.Target;
            var requested = new AuthoredId("signal.requested");
            var unrequested = new AuthoredId("signal.unrequested");
            var binding = new CompiledConsiderationBinding(
                new AuthoredId("binding.bad"), new AuthoredId("consideration.bad"), 1,
                new[] { new ConsiderationParameter(target, DecisionParameterKind.Entity) },
                new[] { new CompiledParameterBinding(target, ParameterBindingSource.OptionContext, target) },
                new[] { new DecisionSignalRequest(requested, new AuthoredId("provider.missing")) },
                new SignalFieldDefinition(
                    new AuthoredId("field.bad"), 0,
                    new[] { new SignalLinearTerm(unrequested, SignalNumeric.Scale) }, null, null, null),
                new ReasonChannelDefinition(new AuthoredId("channel.bad")),
                new ReasonScaleProfile(
                    new AuthoredId("scale.bad"), new[] { new ReasonDieThreshold(1000, new Die(7)) }),
                new AuthoredId("category.bad"), new AuthoredId("label.yes"),
                new AuthoredId("label.no"), InfluenceVisibility.Full);
            var options = new[]
            {
                new DecisionOption(new AuthoredId("option.wait"), new AuthoredId("label.wait"), 0),
                new DecisionOption(new AuthoredId("option.self"), new AuthoredId("label.self"), 1),
            };

            IReadOnlyList<string> errors = DecisionReasoningProgramValidator.Validate(
                new DecisionReasoningProgram(new[] { binding }), options, DecisionSignalProviderIds.BuiltIns);

            Assert.Contains(errors, error => error.Contains("unknown Signal provider"));
            Assert.Contains(errors, error => error.Contains("unrequested Signal"));
            Assert.Contains(errors, error => error.Contains("unsupported d7"));
            Assert.Contains(errors, error => error.Contains("cannot satisfy"));
        }

        private static Character AddCharacter(WorldState world, string name)
        {
            var character = new Character(world.RuntimeIds.Characters.Next(), name, world.Clock.Now);
            world.Characters.Add(character.Id, character);
            return character;
        }

        private static DecisionOption TargetOption(string id, int order, CharacterId target)
        {
            var option = new DecisionOption(new AuthoredId(id), new AuthoredId(id + ".label"), order);
            option.SetContext(DecisionReasoningParameters.Target, DecisionParameterValue.FromEntity(target.ToRef()));
            return option;
        }

        private static DecisionOption MarkerOption(string id, int order, AuthoredId marker)
        {
            var option = new DecisionOption(new AuthoredId(id), new AuthoredId(id + ".label"), order);
            option.SetContext(marker, DecisionParameterValue.FromInteger(1));
            return option;
        }

        private static DecisionReasoningProgram Program(AuthoredId independence)
        {
            var availability = new AuthoredId("signal.availability");
            var selfReliance = new AuthoredId("signal.self_reliance");
            AuthoredId urgency = DecisionReasoningParameters.Urgency;
            return new DecisionReasoningProgram(new[]
            {
                Binding(
                    "binding.availability", "consideration.availability", availability,
                    DecisionSignalProviderIds.TargetAvailability,
                    new[] { new ConsiderationParameter(DecisionReasoningParameters.Target, DecisionParameterKind.Entity) },
                    new[] { new CompiledParameterBinding(DecisionReasoningParameters.Target, ParameterBindingSource.OptionContext, DecisionReasoningParameters.Target) },
                    SignalNumeric.Scale, "channel.availability"),
                Binding(
                    "binding.self_reliance", "consideration.self_reliance", selfReliance,
                    DecisionSignalProviderIds.ActorValue,
                    new[]
                    {
                        new ConsiderationParameter(DecisionReasoningParameters.SelfOption, DecisionParameterKind.Integer),
                        new ConsiderationParameter(DecisionReasoningParameters.ValueId, DecisionParameterKind.AuthoredId),
                    },
                    new[]
                    {
                        new CompiledParameterBinding(DecisionReasoningParameters.SelfOption, ParameterBindingSource.OptionContext, DecisionReasoningParameters.SelfOption),
                        new CompiledParameterBinding(DecisionReasoningParameters.ValueId, ParameterBindingSource.Literal, literal: DecisionParameterValue.FromAuthoredId(independence)),
                    },
                    SignalNumeric.Scale, "channel.self_reliance"),
                Binding(
                    "binding.urgency", "consideration.delay_cost", urgency,
                    DecisionSignalProviderIds.DecisionContext,
                    new[]
                    {
                        new ConsiderationParameter(DecisionReasoningParameters.WaitOption, DecisionParameterKind.Integer),
                        new ConsiderationParameter(urgency, DecisionParameterKind.Integer),
                    },
                    new[]
                    {
                        new CompiledParameterBinding(DecisionReasoningParameters.WaitOption, ParameterBindingSource.OptionContext, DecisionReasoningParameters.WaitOption),
                        new CompiledParameterBinding(urgency, ParameterBindingSource.DecisionContext, urgency),
                    },
                    -SignalNumeric.Scale, "channel.delay_cost"),
            });
        }

        private static CompiledConsiderationBinding Binding(
            string bindingId,
            string considerationId,
            AuthoredId signal,
            AuthoredId provider,
            IReadOnlyList<ConsiderationParameter> schema,
            IReadOnlyList<CompiledParameterBinding> parameters,
            long coefficient,
            string channel)
        {
            return new CompiledConsiderationBinding(
                new AuthoredId(bindingId), new AuthoredId(considerationId), 1,
                schema, parameters, new[] { new DecisionSignalRequest(signal, provider) },
                new SignalFieldDefinition(
                    new AuthoredId(bindingId + ".field"), 0,
                    new[] { new SignalLinearTerm(signal, coefficient) }, null, null, null),
                new ReasonChannelDefinition(new AuthoredId(channel)), ReasonScaleProfile.Standard(),
                new AuthoredId("category.reason"), new AuthoredId("label.supporting"),
                new AuthoredId("label.opposing"), InfluenceVisibility.Full);
        }

        private static void AssertReason(
            IReadOnlyList<CandidateReason> reasons,
            string option,
            InfluencePolarity polarity)
        {
            Assert.Contains(reasons, reason => reason.OptionId == new AuthoredId(option) && reason.Polarity == polarity);
        }

        private static Decision Decision(int id) => new Decision(
            new DecisionId(id),
            Actor,
            new AuthoredId("decision.seek_company"),
            SimTime.Epoch,
            SimTime.Epoch.Plus(SimDuration.FromMinutes(10)),
            new[]
            {
                new DecisionOption(Seek, "Seek", 0),
                new DecisionOption(Avoid, "Avoid", 1),
            });

        private static SocialDecisionInfluenceSpec Spec() => new SocialDecisionInfluenceSpec(
            Seek,
            Avoid,
            new AuthoredId("cat.social"),
            new AuthoredId("influence.enjoys"),
            new AuthoredId("influence.avoids"),
            InfluenceVisibility.Full);

        private static CompositeSocialEvaluationResult Evaluation(long normalized, AppraisalStrength strength)
        {
            var personality = new SocialEvaluationResult(
                Actor,
                Target,
                AppraisalLenses.Affiliation,
                normalized,
                normalized,
                0,
                normalized,
                0,
                strength,
                new SocialContribution[0]);
            return new CompositeSocialEvaluationResult(
                personality,
                normalized,
                normalized,
                strength,
                new SocialContribution[0]);
        }
    }
}
