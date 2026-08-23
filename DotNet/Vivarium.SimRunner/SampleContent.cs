using Vivarium.Domain.Content;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Evaluation;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Social;
using Vivarium.Domain.Time;

namespace Vivarium.SimRunner
{
    /// <summary>
    /// A minimal authored content set for the headless scenario.
    /// <para>
    /// In the real game this comes from Unity ScriptableObjects converted into the catalog (§41). Here it
    /// is written by hand, which is precisely the point: content is data, and the simulation cannot tell
    /// the difference between a designer's asset and this file.
    /// </para>
    /// <para>
    /// The prototype tests the architecture, not the setting (§55) — "bakery" and "home" are placeholders
    /// for an arbitrary containment hierarchy.
    /// </para>
    /// </summary>
    public static class SampleContent
    {
        public static readonly AuthoredId TraitAmbitious = new AuthoredId("trait.ambitious");
        public static readonly AuthoredId TraitEnjoysBaking = new AuthoredId("trait.enjoys_baking");
        public static readonly AuthoredId TraitHomebound = new AuthoredId("trait.homebound");

        public static readonly AuthoredId NeedHunger = new AuthoredId("need.hunger");
        public static readonly AuthoredId NeedSocial = new AuthoredId("need.social");

        public static readonly AuthoredId ActivityWorking = new AuthoredId("activity.working");
        public static readonly AuthoredId ActivitySleeping = new AuthoredId("activity.sleeping");
        public static readonly AuthoredId ActivityDining = new AuthoredId("activity.dining");
        public static readonly AuthoredId ActivityHelpingAtBakery = new AuthoredId("activity.helping_at_bakery");

        public static readonly AuthoredId LocationKindWorld = new AuthoredId("location_kind.world");
        public static readonly AuthoredId LocationKindTown = new AuthoredId("location_kind.town");
        public static readonly AuthoredId LocationKindBuilding = new AuthoredId("location_kind.building");

        public static readonly AuthoredId TravelModeWalking = new AuthoredId("travel_mode.walking");

        public static readonly AuthoredId CommitmentWorkShift = new AuthoredId("commitment.work_shift");
        public static readonly AuthoredId CommitmentDinnerWithGlen = new AuthoredId("commitment.dinner_with_glen");
        public static readonly AuthoredId CommitmentHelpDariusCloseBakery = new AuthoredId("commitment.help_darius_close_bakery");
        public static readonly AuthoredId TemplateBakeryShift = new AuthoredId("routine.bakery_shift");

        public static readonly AuthoredId DecisionJobOffer = new AuthoredId("decision.job_offer");
        public static readonly AuthoredId DecisionLeaveWork = new AuthoredId("decision.leave_work_early");
        public static readonly AuthoredId DecisionCommitmentConflict = new AuthoredId("decision.commitment_conflict");
        public static readonly AuthoredId OptionAccept = new AuthoredId("option.accept");
        public static readonly AuthoredId OptionStay = new AuthoredId("option.stay");
        public static readonly AuthoredId OptionLeave = new AuthoredId("option.leave");
        public static readonly AuthoredId OptionPreserveFirstCommitment = new AuthoredId("option.preserve_first_relinquish_second");
        public static readonly AuthoredId OptionPreserveSecondCommitment = new AuthoredId("option.preserve_second_relinquish_first");

        public static readonly AuthoredId ConflictScopeEmployment = new AuthoredId("conflict_scope.employment");

        public static readonly AuthoredId InterventionStepUp = new AuthoredId("intervention.encourage");
        public static readonly AuthoredId InterventionReroll = new AuthoredId("intervention.reconsider");

        public static readonly AuthoredId CategoryPersonalConcern = new AuthoredId("influence_category.personal_concern");
        public static readonly AuthoredId CategoryPractical = new AuthoredId("influence_category.practical");
        public static readonly AuthoredId CategorySocial = new AuthoredId("influence_category.social");

        public static readonly AuthoredId ContextHousingMarket = new AuthoredId("decision_context.local_opportunity");
        public static readonly AuthoredId ContextWorkPressure = new AuthoredId("decision_context.work_pressure");
        public static readonly AuthoredId ModifierDislikedColleague = new AuthoredId("activity_modifier.disliked_colleague_present");
        public static readonly AuthoredId InfluenceBadWorkContext = new AuthoredId("influence.bad_work_context");
        public static readonly AuthoredId LeaveOptionMarker = new AuthoredId("decision.option_marker.leave_work");
        public static readonly AuthoredId StayOptionMarker = new AuthoredId("decision.option_marker.finish_shift");
        public static readonly AuthoredId SocialCalibrationStandard = new AuthoredId("social.calibration.standard");
        public static readonly AuthoredId SocialPressureSeekCompany = new AuthoredId("social.pressure.seek_company");
        public static readonly AuthoredId DecisionSeekCompany = new AuthoredId("decision.seek_company");
        public static readonly AuthoredId OptionSeekCompany = new AuthoredId("option.seek_company");
        public static readonly AuthoredId OptionAvoidCompany = new AuthoredId("option.avoid_company");

        /// <summary>Home and the bakery are wired into the travel network by <see cref="SampleWorld"/>.</summary>
        public static DefinitionCatalog Build(int contentVersion = 1)
        {
            var builder = new DefinitionCatalog.Builder { ContentVersion = contentVersion };

            builder.Add(new TraitDefinition(
                TraitAmbitious,
                "Ambitious",
                new[]
                {
                    new DiscoveryChannel(DiscoveryChannels.Conversation),
                    new DiscoveryChannel(DiscoveryChannels.RepeatedObservation, 2500),
                    new DiscoveryChannel(DiscoveryChannels.Inspection),
                }));

            builder.Add(new TraitDefinition(
                TraitEnjoysBaking,
                "Enjoys baking",
                new[] { new DiscoveryChannel(DiscoveryChannels.DirectObservation) }));

            // Deliberately hard to learn: proves truth and knowledge can stay apart (§22).
            builder.Add(new TraitDefinition(
                TraitHomebound,
                "Homebound",
                new[] { new DiscoveryChannel(DiscoveryChannels.Conversation, 9000) }));

            builder.Add(new NeedDefinition(NeedHunger, "Hunger", 0, 10000, 12, 1, new long[] { 6000, 8000, 9500 }));
            builder.Add(new NeedDefinition(NeedSocial, "Social", 0, 10000, 4, 1, new long[] { 7000 }));

            builder.Add(new ActivityDefinition(ActivityWorking, "Working", SimDuration.FromHours(6), true, true));
            builder.Add(new ActivityDefinition(ActivitySleeping, "Sleeping", SimDuration.FromHours(8), false));
            builder.Add(new ActivityDefinition(ActivityDining, "Dining", SimDuration.FromMinutes(90), false));
            builder.Add(new ActivityDefinition(ActivityHelpingAtBakery, "Helping at the bakery", SimDuration.FromMinutes(90), false));
            builder.Add(new ActivityDefinition(WellKnownActivities.Waiting, "Waiting", SimDuration.FromHours(1), false));
            builder.Add(new ActivityDefinition(WellKnownActivities.Traveling, "Traveling", SimDuration.FromMinutes(10), false, false, true));

            builder.Add(new LocationKindDefinition(LocationKindWorld, "World"));
            builder.Add(new LocationKindDefinition(LocationKindTown, "Town"));
            builder.Add(new LocationKindDefinition(LocationKindBuilding, "Building"));

            builder.Add(new DecisionDefinition(
                DecisionJobOffer,
                new[]
                {
                    new DecisionOption(OptionAccept, "Take the job", 0),
                    new DecisionOption(OptionStay, "Stay where you are", 1),
                },
                SimDuration.FromHours(8),
                ConflictScopeEmployment,
                importance: 10,
                holdEligible: true,
                dependencyTemplates: new[] { new DecisionDependencyKey(ContextHousingMarket) }));

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
                trigger: new NeedThresholdDecisionTrigger(NeedHunger, 6000),
                activityOutcomes: new[]
                {
                    new DecisionActivityOutcome(OptionLeave, WellKnownActivities.Waiting, SimDuration.FromHours(1)),
                },
                reasoningProgram: LeaveWorkReasoningProgram()));

            builder.Add(new InterventionDefinition(InterventionStepUp, InterventionKind.StepDieUp, 1));
            builder.Add(new InterventionDefinition(InterventionReroll, InterventionKind.Reroll, 1));

            builder.Add(new AppraisalCalibrationProfile(
                SocialCalibrationStandard,
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
            builder.Add(new SocialPressureDefinition(SocialPressureSeekCompany, new SocialFactorRule[0]));
            builder.Add(new SocialPressureDefinition(
                new AuthoredId("social.pressure.interaction_relevance"),
                new SocialFactorRule[0]));
            builder.Add(new DecisionDefinition(
                DecisionSeekCompany,
                new[]
                {
                    new DecisionOption(OptionSeekCompany, "Seek their company", 0),
                    new DecisionOption(OptionAvoidCompany, "Keep distance", 1),
                },
                SimDuration.FromMinutes(10),
                new AuthoredId("conflict_scope.social_target"),
                importance: 12,
                socialTrigger: new SocialInteractionDecisionTrigger(
                    SocialPressureSeekCompany,
                    AppraisalLenses.Affiliation,
                    new SocialDecisionInfluenceSpec(
                        OptionSeekCompany,
                        OptionAvoidCompany,
                        CategorySocial,
                        new AuthoredId("influence.enjoys_company"),
                        new AuthoredId("influence.avoids_company"),
                        InfluenceVisibility.Full)),
                relationshipOutcomes: new[]
                {
                    new DecisionRelationshipOutcome(OptionSeekCompany, RelationshipChannels.Affection, 1000),
                    new DecisionRelationshipOutcome(OptionAvoidCompany, RelationshipChannels.Resentment, 500),
                }));

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
                    CategoryPractical,
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
                    CategoryPersonalConcern,
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
                    CategorySocial,
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
                    CategoryPractical,
                    new AuthoredId("influence.reliability"),
                    new AuthoredId("influence.unreliable"),
                    InfluenceVisibility.Full),
            });

        /// <summary>
        /// The bakery shift routine. Note what this is <i>not</i>: a calendar. It is a pattern the
        /// planner materializes across a bounded horizon on demand (§29.4).
        /// </summary>
        public static CommitmentTemplate BakeryShiftTemplate(LocationId bakery) => new CommitmentTemplate(
            TemplateBakeryShift,
            CommitmentWorkShift,
            cycleLengthDays: 7,
            activeDaysMask: 0b0011111,
            startMinuteOfDay: 9 * 60,
            duration: SimDuration.FromHours(6),
            locationId: bakery,
            priority: 100,
            activityDefinitionId: ActivityWorking,
            startWindow: SimDuration.FromMinutes(30));
    }
}
