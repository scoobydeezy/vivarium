using Vivarium.Domain.Content;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Evaluation;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Time;
using Vivarium.Domain.Social;
using Vivarium.Domain.Relationships;
using Vivarium.Infrastructure.Bootstrap;
using Vivarium.Infrastructure.Clock;
using Vivarium.Infrastructure.Persistence;

namespace Vivarium.Application.Tests
{
    /// <summary>Shared fixture: a two-location world with one character and one decision type.</summary>
    public sealed class TestWorld
    {
        public SimulationHost Host;
        public InMemorySaveGameStore Store;
        public FixedRealWorldClock Clock;
        public DefinitionCatalog Catalog;
        public LocationId Town;
        public LocationId Home;
        public LocationId Bakery;
        public CharacterId Mina;

        public static readonly AuthoredId TraitAmbitious = new AuthoredId("trait.ambitious");
        public static readonly AuthoredId NeedHunger = new AuthoredId("need.hunger");
        public static readonly AuthoredId ActivityWorking = new AuthoredId("activity.working");
        public static readonly AuthoredId DecisionJobOffer = new AuthoredId("decision.job_offer");
        public static readonly AuthoredId DecisionLeaveWork = new AuthoredId("decision.leave_work_early");
        public static readonly AuthoredId DecisionCommitmentConflict = new AuthoredId("decision.commitment_conflict");
        public static readonly AuthoredId OptionAccept = new AuthoredId("option.accept");
        public static readonly AuthoredId OptionStay = new AuthoredId("option.stay");
        public static readonly AuthoredId OptionLeave = new AuthoredId("option.leave");
        public static readonly AuthoredId OptionPreserveFirstCommitment = new AuthoredId("option.preserve_first_relinquish_second");
        public static readonly AuthoredId OptionPreserveSecondCommitment = new AuthoredId("option.preserve_second_relinquish_first");
        public static readonly AuthoredId InterventionStepUp = new AuthoredId("intervention.encourage");
        public static readonly AuthoredId Walking = new AuthoredId("travel_mode.walking");
        public static readonly AuthoredId KindBuilding = new AuthoredId("location_kind.building");
        public static readonly AuthoredId ContextWorkPressure = new AuthoredId("decision_context.work_pressure");
        public static readonly AuthoredId ModifierDislikedColleague = new AuthoredId("activity_modifier.disliked_colleague_present");
        public static readonly AuthoredId InfluenceBadWorkContext = new AuthoredId("influence.bad_work_context");
        public static readonly AuthoredId LeaveOptionMarker = new AuthoredId("decision.option_marker.leave_work");
        public static readonly AuthoredId StayOptionMarker = new AuthoredId("decision.option_marker.finish_shift");

        public static DefinitionCatalog BuildCatalog(int contentVersion = 1, bool includeSocialDecision = false)
        {
            var builder = new DefinitionCatalog.Builder { ContentVersion = contentVersion };

            builder.Add(new TraitDefinition(
                TraitAmbitious,
                "Ambitious",
                new[]
                {
                    new DiscoveryChannel(DiscoveryChannels.Inspection),
                    new DiscoveryChannel(DiscoveryChannels.DirectObservation),
                    new DiscoveryChannel(DiscoveryChannels.Conversation),
                }));

            builder.Add(new NeedDefinition(NeedHunger, "Hunger", 0, 10000, 12, 1, new long[] { 8000 }));
            builder.Add(new ActivityDefinition(ActivityWorking, "Working", SimDuration.FromHours(6), true, true));
            builder.Add(new ActivityDefinition(WellKnownActivities.Waiting, "Waiting", SimDuration.FromHours(1), false));
            builder.Add(new ActivityDefinition(WellKnownActivities.Traveling, "Traveling", SimDuration.FromMinutes(10), false, false, true));
            builder.Add(new LocationKindDefinition(KindBuilding, "Building"));

            builder.Add(new DecisionDefinition(
                DecisionJobOffer,
                new[]
                {
                    new DecisionOption(OptionAccept, "Take the job", 0),
                    new DecisionOption(OptionStay, "Stay", 1),
                },
                SimDuration.FromHours(8),
                new AuthoredId("conflict_scope.employment"),
                importance: 10));

            builder.Add(new DecisionDefinition(
                DecisionCommitmentConflict,
                new[]
                {
                    CommitmentOption(OptionPreserveFirstCommitment, "Preserve first / relinquish second", 0),
                    CommitmentOption(OptionPreserveSecondCommitment, "Preserve second / relinquish first", 1),
                },
                SimDuration.FromMinutes(10),
                new AuthoredId("conflict_scope.schedule"),
                importance: 30,
                reasoningProgram: CommitmentConflictReasoningProgram(),
                commitmentConflictTrigger: new CommitmentConflictDecisionTrigger()));

            builder.Add(new DecisionDefinition(
                DecisionLeaveWork,
                new[]
                {
                    MarkedOption(OptionLeave, "Leave work early", 0, LeaveOptionMarker),
                    MarkedOption(OptionStay, "Finish the shift", 1, StayOptionMarker),
                },
                SimDuration.FromMinutes(10),
                new AuthoredId("conflict_scope.current_activity"),
                importance: 20,
                trigger: new NeedThresholdDecisionTrigger(NeedHunger, 8000),
                activityOutcomes: new[]
                {
                    new DecisionActivityOutcome(OptionLeave, WellKnownActivities.Waiting, SimDuration.FromHours(1)),
                },
                reasoningProgram: LeaveWorkReasoningProgram()));

            builder.Add(new InterventionDefinition(InterventionStepUp, InterventionKind.StepDieUp, 1));

            builder.Add(new AppraisalCalibrationProfile(
                new AuthoredId("social.calibration.standard"),
                new[]
                {
                    new AppraisalStrengthThreshold(1000, AppraisalStrength.Minor),
                    new AppraisalStrengthThreshold(2500, AppraisalStrength.Moderate),
                    new AppraisalStrengthThreshold(5000, AppraisalStrength.Strong),
                    new AppraisalStrengthThreshold(7500, AppraisalStrength.Extreme),
                },
                1));
            builder.Add(new SocialEvidenceDefinition(
                new AuthoredId("social.action.interaction"),
                new[]
                {
                    new SocialEvidenceMeasurement(
                        new AuthoredId("social.measurement.friendly_interaction"),
                        new[]
                        {
                            new SocialLinearTerm(SocialDimensions.Warmth, 7000),
                            new SocialLinearTerm(SocialDimensions.Sociability, 3000),
                        },
                        4000,
                        30000000),
                },
                new AuthoredId("social.explanation.friendly_interaction")));

            if (includeSocialDecision)
            {
                var socialPressureId = new AuthoredId("social.pressure.seek_company");
                var optionSeek = new AuthoredId("option.seek_company");
                var optionAvoid = new AuthoredId("option.avoid_company");
                builder.Add(new SocialPressureDefinition(socialPressureId, new SocialFactorRule[0]));
                builder.Add(new DecisionDefinition(
                    new AuthoredId("decision.seek_company"),
                    new[]
                    {
                        new DecisionOption(optionSeek, "Seek their company", 0),
                        new DecisionOption(optionAvoid, "Keep distance", 1),
                    },
                    SimDuration.FromMinutes(10),
                    new AuthoredId("conflict_scope.social_target"),
                    importance: 12,
                    socialTrigger: new SocialInteractionDecisionTrigger(
                        socialPressureId,
                        AppraisalLenses.Affiliation,
                        new SocialDecisionInfluenceSpec(
                            optionSeek,
                            optionAvoid,
                            new AuthoredId("cat.social"),
                            new AuthoredId("influence.enjoys_company"),
                            new AuthoredId("influence.avoids_company"),
                            InfluenceVisibility.Full)),
                    relationshipOutcomes: new[]
                    {
                        new DecisionRelationshipOutcome(optionSeek, RelationshipChannels.Affection, 1000),
                        new DecisionRelationshipOutcome(optionAvoid, RelationshipChannels.Resentment, 500),
                    }));
            }

            return builder.Build();
        }

        private static DecisionOption MarkedOption(AuthoredId id, AuthoredId label, int order, AuthoredId marker)
        {
            var option = new DecisionOption(id, label, order);
            option.SetContext(marker, DecisionParameterValue.FromInteger(1));
            return option;
        }

        private static DecisionOption CommitmentOption(AuthoredId id, AuthoredId label, int order)
        {
            var option = new DecisionOption(id, label, order);
            option.SetContext(
                DecisionReasoningParameters.Commitment,
                DecisionParameterValue.FromEntity(new EntityRef(EntityKind.Commitment, 0)));
            option.SetContext(DecisionReasoningParameters.PreservedCommitment,
                DecisionParameterValue.FromEntity(new EntityRef(EntityKind.Commitment, 0)));
            option.SetContext(DecisionReasoningParameters.RelinquishedCommitment,
                DecisionParameterValue.FromEntity(new EntityRef(EntityKind.Commitment, 0)));
            return option;
        }

        private static DecisionReasoningProgram CommitmentConflictReasoningProgram() =>
            new DecisionReasoningProgram(new[]
            {
                CommitmentConflictBinding("preserved", DecisionReasoningParameters.PreservedCommitment, 1),
                CommitmentConflictBinding("relinquished", DecisionReasoningParameters.RelinquishedCommitment, -1),
            });

        private static CompiledConsiderationBinding CommitmentConflictBinding(
            string instance, AuthoredId optionParameter, int polarity) =>
                new CompiledConsiderationBinding(
                    new AuthoredId("binding.commitment_conflict.honorability." + instance),
                    new AuthoredId("consideration.commitment_honorability"),
                    1,
                    new[]
                    {
                        new ConsiderationParameter(DecisionReasoningParameters.Actor, DecisionParameterKind.Entity),
                        new ConsiderationParameter(DecisionReasoningParameters.Commitment, DecisionParameterKind.Entity),
                        new ConsiderationParameter(DecisionReasoningParameters.Target, DecisionParameterKind.Entity),
                    },
                    new[]
                    {
                        new CompiledParameterBinding(DecisionReasoningParameters.Actor, ParameterBindingSource.DecisionActor),
                        new CompiledParameterBinding(
                            DecisionReasoningParameters.Commitment,
                            ParameterBindingSource.OptionContext,
                            optionParameter),
                        new CompiledParameterBinding(DecisionReasoningParameters.Target,
                            ParameterBindingSource.OptionContext, optionParameter),
                    },
                    new[]
                    {
                        new DecisionSignalRequest(CommitmentDecisionSignals.Priority, DecisionSignalProviderIds.Commitment),
                        new DecisionSignalRequest(CommitmentDecisionSignals.Urgency, DecisionSignalProviderIds.Commitment),
                        new DecisionSignalRequest(CommitmentDecisionSignals.TravelBurden, DecisionSignalProviderIds.Commitment),
                    },
                    new SignalFieldDefinition(
                        new AuthoredId("field.commitment_conflict.honorability"),
                        0,
                        new[]
                        {
                            new SignalLinearTerm(CommitmentDecisionSignals.Priority, polarity * 6000, new AuthoredId("reason.commitment.priority")),
                            new SignalLinearTerm(CommitmentDecisionSignals.Urgency, polarity * 4000, new AuthoredId("reason.commitment.urgency")),
                            new SignalLinearTerm(CommitmentDecisionSignals.TravelBurden, polarity * -4000, new AuthoredId("reason.commitment.travel")),
                        }, null, null, null),
                    new ReasonChannelDefinition(new AuthoredId("reason_channel.commitment_honorability")),
                    new ReasonScaleProfile(
                        new AuthoredId("reason_scale.commitment_honorability"),
                        new[]
                        {
                            new ReasonDieThreshold(0, Die.D4),
                            new ReasonDieThreshold(2000, Die.D6),
                            new ReasonDieThreshold(4000, Die.D8),
                            new ReasonDieThreshold(6000, Die.D10),
                        }),
                    new AuthoredId("cat.commitment"),
                    new AuthoredId("influence.commitment_worth_honoring"),
                    new AuthoredId("influence.commitment_costly_to_honor"),
                    InfluenceVisibility.Full);

        private static DecisionReasoningProgram LeaveWorkReasoningProgram() => new DecisionReasoningProgram(
            new[]
            {
                new CompiledConsiderationBinding(
                    new AuthoredId("binding.leave_work.hunger"),
                    new AuthoredId("consideration.need_urgency"),
                    1,
                    new[]
                    {
                        new ConsiderationParameter(LeaveOptionMarker, DecisionParameterKind.Integer),
                        new ConsiderationParameter(DecisionReasoningParameters.Actor, DecisionParameterKind.Entity),
                        new ConsiderationParameter(DecisionReasoningParameters.Urgency, DecisionParameterKind.Integer),
                    },
                    new[]
                    {
                        new CompiledParameterBinding(LeaveOptionMarker, ParameterBindingSource.OptionContext, LeaveOptionMarker),
                        new CompiledParameterBinding(DecisionReasoningParameters.Actor, ParameterBindingSource.DecisionActor),
                        new CompiledParameterBinding(
                            DecisionReasoningParameters.Urgency,
                            ParameterBindingSource.DecisionContext,
                            DecisionReasoningParameters.Urgency),
                    },
                    new[]
                    {
                        new DecisionSignalRequest(
                            DecisionReasoningParameters.Urgency,
                            DecisionSignalProviderIds.DecisionContext),
                    },
                    new SignalFieldDefinition(
                        new AuthoredId("field.leave_work.hunger"),
                        0,
                        new[]
                        {
                            new SignalLinearTerm(
                                DecisionReasoningParameters.Urgency,
                                SignalNumeric.Scale,
                                new AuthoredId("reason.hunger")),
                        }, null, null, null),
                    new ReasonChannelDefinition(new AuthoredId("reason_channel.physical_urgency")),
                    new ReasonScaleProfile(
                        new AuthoredId("reason_scale.hunger"),
                        new[] { new ReasonDieThreshold(0, Die.D20) }),
                    new AuthoredId("cat.physical"),
                    new AuthoredId("influence.hunger"),
                    new AuthoredId("influence.not_hungry"),
                    InfluenceVisibility.Full),
                new CompiledConsiderationBinding(
                    new AuthoredId("binding.leave_work.work_context"),
                    new AuthoredId("consideration.activity_context"),
                    1,
                    new[]
                    {
                        new ConsiderationParameter(LeaveOptionMarker, DecisionParameterKind.Integer),
                        new ConsiderationParameter(DecisionReasoningParameters.Actor, DecisionParameterKind.Entity),
                        new ConsiderationParameter(DecisionReasoningParameters.ActivityModifierId, DecisionParameterKind.AuthoredId),
                    },
                    new[]
                    {
                        new CompiledParameterBinding(LeaveOptionMarker, ParameterBindingSource.OptionContext, LeaveOptionMarker),
                        new CompiledParameterBinding(DecisionReasoningParameters.Actor, ParameterBindingSource.DecisionActor),
                        new CompiledParameterBinding(
                            DecisionReasoningParameters.ActivityModifierId,
                            ParameterBindingSource.Literal,
                            literal: DecisionParameterValue.FromAuthoredId(ModifierDislikedColleague)),
                    },
                    new[]
                    {
                        new DecisionSignalRequest(ContextWorkPressure, DecisionSignalProviderIds.ActivityModifier),
                    },
                    new SignalFieldDefinition(
                        new AuthoredId("field.leave_work.work_context"),
                        0,
                        new[]
                        {
                            new SignalLinearTerm(
                                ContextWorkPressure,
                                SignalNumeric.Scale,
                                new AuthoredId("reason.difficult_work_context")),
                        }, null, null, null),
                    new ReasonChannelDefinition(new AuthoredId("reason_channel.work_context")),
                    new ReasonScaleProfile(
                        new AuthoredId("reason_scale.work_context"),
                        new[]
                        {
                            new ReasonDieThreshold(0, Die.D6),
                            new ReasonDieThreshold(4000, Die.D10),
                        }),
                    new AuthoredId("cat.social"),
                    InfluenceBadWorkContext,
                    new AuthoredId("influence.supportive_work_context"),
                    InfluenceVisibility.Existence | InfluenceVisibility.Category | InfluenceVisibility.Magnitude),
                new CompiledConsiderationBinding(
                    new AuthoredId("binding.leave_work.reliability"),
                    new AuthoredId("consideration.reliability"),
                    1,
                    new[] { new ConsiderationParameter(StayOptionMarker, DecisionParameterKind.Integer) },
                    new[]
                    {
                        new CompiledParameterBinding(StayOptionMarker, ParameterBindingSource.OptionContext, StayOptionMarker),
                    },
                    new DecisionSignalRequest[0],
                    new SignalFieldDefinition(new AuthoredId("field.leave_work.reliability"), 1000, null, null, null, null),
                    new ReasonChannelDefinition(new AuthoredId("reason_channel.reliability")),
                    new ReasonScaleProfile(
                        new AuthoredId("reason_scale.reliability"),
                        new[] { new ReasonDieThreshold(0, Die.D6) }),
                    new AuthoredId("cat.practical"),
                    new AuthoredId("influence.reliability"),
                    new AuthoredId("influence.unreliable"),
                    InfluenceVisibility.Full),
            });

        public static TestWorld Create(long seed = 827119, int contentVersion = 1, bool includeSocialDecision = false)
        {
            var fixture = new TestWorld
            {
                Store = new InMemorySaveGameStore(),
                Clock = new FixedRealWorldClock(1000000000000L),
                Catalog = BuildCatalog(contentVersion, includeSocialDecision),
            };

            fixture.Host = SimulationBootstrapper.CreateNewWorld(
                seed,
                SimTime.FromClockTime(0, 8, 0),
                fixture.Catalog,
                simulationRulesVersion: 1,
                trace: null,
                saveStore: fixture.Store,
                realWorldClock: fixture.Clock);

            WorldState world = fixture.Host.World;

            fixture.Town = AddLocation(world, LocationId.None, "Town");
            fixture.Home = AddLocation(world, fixture.Town, "Home");
            fixture.Bakery = AddLocation(world, fixture.Town, "Bakery");
            world.TravelNetwork.ConnectBidirectional(fixture.Home, fixture.Bakery, SimDuration.FromMinutes(12), Walking);

            var mina = new Character(world.RuntimeIds.Characters.Next(), "Mina Cairn", world.Clock.Now);
            mina.AddTrait(TraitAmbitious);
            world.Characters.Add(mina.Id, mina);
            fixture.Mina = mina.Id;

            NeedDefinition hunger = fixture.Catalog.Needs[NeedHunger];
            var hungerState = new NeedState(
                hunger.Id,
                AnalyticalProgression.Linear(2000, world.Clock.Now, hunger.DefaultRateNumerator, hunger.DefaultRateDenominator, hunger.MinValue, hunger.MaxValue),
                hunger.BehaviouralThresholds[0]);

            mina.SetNeed(hungerState);
            fixture.Host.Needs.Rearm(fixture.Host.Simulation, mina, hungerState);

            fixture.Host.Transitions.BeginActivity(
                fixture.Host.Simulation, mina.Id, WellKnownActivities.Waiting, fixture.Home, SimDuration.FromHours(1));

            return fixture;
        }

        /// <summary>Creates a job-offer decision with a mix of visible, generalized, and hidden influences.</summary>
        public Decision CreateDecision()
        {
            WorldState world = Host.World;
            DecisionDefinition definition = Catalog.Decisions[DecisionJobOffer];

            var decision = new Decision(
                world.RuntimeIds.Decisions.Next(),
                Mina,
                definition.Id,
                world.Clock.Now,
                world.Clock.Now.Plus(definition.TimeToResolve),
                definition.Options,
                new DecisionConflictScope(definition.ConflictScopeKind, Mina.ToRef()),
                definition.Importance);

            decision.AddInfluence(OptionAccept, new AuthoredId("cat.personal"), TraitAmbitious, Die.D10, InfluenceVisibility.Existence | InfluenceVisibility.Category | InfluenceVisibility.Magnitude, default, Mina.ToRef());
            decision.AddInfluence(OptionAccept, new AuthoredId("cat.practical"), new AuthoredId("influence.better_pay"), Die.D6, InfluenceVisibility.Full);
            decision.AddInfluence(OptionStay, new AuthoredId("cat.social"), new AuthoredId("influence.family"), Die.D8, InfluenceVisibility.Full);
            decision.AddInfluence(OptionStay, new AuthoredId("cat.practical"), new AuthoredId("influence.commute"), Die.D6, InfluenceVisibility.Hidden);

            world.Decisions.Add(decision.Id, decision);
            world.DecisionDependencies.Register(decision);

            ScheduledEvent scheduled = world.Scheduler.Schedule(
                decision.ResolveAt,
                SchedulePhase.Decision,
                ScheduledEventTypes.DecisionResolve,
                new DecisionResolvePayload(decision.Id, Mina));

            decision.SetPendingResolveEvent(scheduled.Id);
            return decision;
        }

        private static LocationId AddLocation(WorldState world, LocationId parent, string name)
        {
            var node = new LocationNode(world.RuntimeIds.Locations.Next(), parent, KindBuilding, name);
            world.Locations.Add(node);
            return node.Id;
        }
    }
}
