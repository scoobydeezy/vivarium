using Vivarium.Domain.Content;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Evaluation;
using Vivarium.Domain.Employment;
using Vivarium.Domain.Groups;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Social;
using Vivarium.Domain.Time;
using Vivarium.Infrastructure.Bootstrap;

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
        public static readonly AuthoredId TraitHomebound = MinimumPlayableContent.TraitHomebound;

        public static readonly AuthoredId NeedHunger = new AuthoredId("need.hunger");
        public static readonly AuthoredId NeedSocial = WellKnownNeeds.Social;
        public static readonly AuthoredId NeedRecreation = WellKnownNeeds.Recreation;

        public static readonly AuthoredId ActivityWorking = new AuthoredId("activity.working");
        public static readonly AuthoredId ActivitySleeping = WellKnownActivities.Sleeping;
        public static readonly AuthoredId ActivityDining = new AuthoredId("activity.dining");
        public static readonly AuthoredId ActivityHelpingAtBakery = new AuthoredId("activity.helping_at_bakery");
        public static readonly AuthoredId ActivityTabletopGames = new AuthoredId("activity.tabletop_games");
        public static readonly AuthoredId ActivityReading = new AuthoredId("activity.reading");
        public static readonly AuthoredId ActivitySocializing = WellKnownActivities.Socializing;
        public static readonly AuthoredId ActivityCafeHosting = new AuthoredId("activity.cafe_hosting");
        public static readonly AuthoredId InterestTabletopGames = new AuthoredId("interest.tabletop_games");
        public static readonly AuthoredId InterestReading = new AuthoredId("interest.reading");
        public static readonly AuthoredId InterestSocializing = new AuthoredId("interest.socializing");

        public static readonly AuthoredId LocationKindWorld = new AuthoredId("location_kind.world");
        public static readonly AuthoredId LocationKindTown = new AuthoredId("location_kind.town");
        public static readonly AuthoredId LocationKindBuilding = new AuthoredId("location_kind.building");

        public static readonly AuthoredId TravelModeWalking = new AuthoredId("travel_mode.walking");

        public static readonly AuthoredId CommitmentWorkShift = new AuthoredId("commitment.work_shift");
        public static readonly AuthoredId CommitmentDinnerWithGlen = new AuthoredId("commitment.dinner_with_glen");
        public static readonly AuthoredId CommitmentHelpDariusCloseBakery = new AuthoredId("commitment.help_darius_close_bakery");
        public static readonly AuthoredId CommitmentCafeHostingShift = new AuthoredId("commitment.cafe_hosting_shift");
        public static readonly AuthoredId TemplateBakeryShift = new AuthoredId("routine.bakery_shift");
        public static readonly AuthoredId TemplateBakeryClosingDuty = new AuthoredId("routine.bakery_closing_duty");
        public static readonly AuthoredId EmploymentBakeryWorker = new AuthoredId("employment.bakery_worker");
        public static readonly AuthoredId EmploymentRoleBaker = new AuthoredId("employment.role.baker");
        public static readonly AuthoredId TemplateCafeHostingShift = new AuthoredId("routine.cafe_hosting_shift");
        public static readonly AuthoredId EmploymentCafeHost = new AuthoredId("employment.cafe_host");
        public static readonly AuthoredId EmploymentRoleCafeHost = new AuthoredId("employment.role.cafe_host");

        public static readonly AuthoredId DecisionJobOffer = new AuthoredId("decision.job_offer");
        public static readonly AuthoredId DecisionLeaveWork = new AuthoredId("decision.leave_work_early");
        public static readonly AuthoredId DecisionCommitmentConflict = new AuthoredId("decision.commitment_conflict");
        public static readonly AuthoredId DecisionChooseRecreation = new AuthoredId("decision.choose_recreation");
        public static readonly AuthoredId DecisionSocialInvitation = new AuthoredId("decision.social_invitation");
        public static readonly AuthoredId DecisionRestOrContinue = new AuthoredId("decision.rest_or_continue");
        public static readonly AuthoredId OptionAccept = new AuthoredId("option.accept");
        public static readonly AuthoredId OptionStay = new AuthoredId("option.stay");
        public static readonly AuthoredId OptionLeave = new AuthoredId("option.leave");
        public static readonly AuthoredId OptionPreserveFirstCommitment = new AuthoredId("option.preserve_first_relinquish_second");
        public static readonly AuthoredId OptionPreserveSecondCommitment = new AuthoredId("option.preserve_second_relinquish_first");
        public static readonly AuthoredId OptionTabletopGames = new AuthoredId("option.recreation.tabletop_games");
        public static readonly AuthoredId OptionReading = new AuthoredId("option.recreation.reading");
        public static readonly AuthoredId OptionJoinInvitation = new AuthoredId("option.social_invitation.join");
        public static readonly AuthoredId OptionKeepPlan = new AuthoredId("option.social_invitation.keep_plan");
        public static readonly AuthoredId OptionRest = new AuthoredId("option.rest");
        public static readonly AuthoredId OptionContinue = new AuthoredId("option.continue");

        public static readonly AuthoredId ConflictScopeEmployment = new AuthoredId("conflict_scope.employment");

        public static readonly AuthoredId InterventionStepUp = new AuthoredId("intervention.encourage");
        public static readonly AuthoredId InterventionTemper = new AuthoredId("intervention.temper");
        public static readonly AuthoredId InterventionReroll = new AuthoredId("intervention.reconsider");
        public static readonly AuthoredId InterventionLoadedTwenty = new AuthoredId("intervention.loaded_twenty");

        public static readonly AuthoredId CategoryPersonalConcern = new AuthoredId("influence_category.personal_concern");
        public static readonly AuthoredId CategoryPractical = new AuthoredId("influence_category.practical");
        public static readonly AuthoredId CategorySocial = new AuthoredId("influence_category.social");

        public static readonly AuthoredId ContextHousingMarket = new AuthoredId("decision_context.local_opportunity");
        public static readonly AuthoredId ContextWorkPressure = new AuthoredId("decision_context.work_pressure");
        public static readonly AuthoredId ModifierDislikedColleague = new AuthoredId("activity_modifier.disliked_colleague_present");
        public static readonly AuthoredId InfluenceBadWorkContext = new AuthoredId("influence.bad_work_context");
        public static readonly AuthoredId LeaveOptionMarker = new AuthoredId("decision.option_marker.leave_work");
        public static readonly AuthoredId StayOptionMarker = new AuthoredId("decision.option_marker.finish_shift");
        public static readonly AuthoredId RestOptionMarker = new AuthoredId("decision.option_marker.rest");
        public static readonly AuthoredId SocialCalibrationStandard = new AuthoredId("social.calibration.standard");
        public static readonly AuthoredId SocialPressureSeekCompany = new AuthoredId("social.pressure.seek_company");
        public static readonly AuthoredId SocialEvidenceCommitmentFulfilled = new AuthoredId("social.action.commitment_fulfilled");
        public static readonly AuthoredId SocialEvidenceCommitmentBreach = new AuthoredId("social.action.commitment_breach");
        public static readonly AuthoredId AccountabilitySocialCommitment = new AuthoredId("accountability.social_commitment");
        public static readonly AuthoredId DecisionSeekCompany = new AuthoredId("decision.seek_company");
        public static readonly AuthoredId DecisionRelyOnPerson = new AuthoredId("decision.rely_on_person");
        public static readonly AuthoredId OptionSeekCompany = new AuthoredId("option.seek_company");
        public static readonly AuthoredId OptionAvoidCompany = new AuthoredId("option.avoid_company");
        public static readonly AuthoredId OptionRely = new AuthoredId("option.rely_on_person");
        public static readonly AuthoredId OptionDoItSelf = new AuthoredId("option.do_it_self");
        public static readonly AuthoredId SocialPressureReliance = new AuthoredId("social.pressure.reliance");

        /// <summary>Home and the bakery are wired into the travel network by <see cref="MinimumPlayableWorld"/>.</summary>
        public static DefinitionCatalog Build(int contentVersion = 1)
        {
            var builder = new DefinitionCatalog.Builder { ContentVersion = contentVersion };
            builder.SetDecisionImportancePolicy(new DecisionImportancePolicyDefinition(
                admissionFloor: 6500,
                prioritizedFeedFloor: 6500,
                normalFeedFloor: 7000,
                autoHoldFloor: 7000));

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

            builder.Add(new NeedDefinition(
                NeedHunger,
                "Hunger",
                0,
                10000,
                12,
                1,
                new long[] { 6000, 8000, 9500 },
                satisfactionRoutine: new NeedSatisfactionRoutineDefinition(
                    WellKnownActivities.Eating,
                    6000,
                    -5000)));
            builder.Add(new NeedDefinition(
                NeedSocial,
                "Social",
                0,
                10000,
                4,
                1,
                new long[] { 7000 },
                socializingRoutine: new SocializingRoutineDefinition(
                    ActivitySocializing,
                    7000,
                    -5000,
                    4,
                    new SocialInvitationRoutineDefinition(
                        DecisionSocialInvitation,
                        OptionJoinInvitation,
                        new[]
                        {
                            new SocialInvitationPlanDefinition(ActivityTabletopGames, InterestTabletopGames),
                            new SocialInvitationPlanDefinition(ActivityReading, InterestReading),
                        }))));
            builder.Add(new NeedDefinition(
                NeedRecreation,
                "Recreation",
                0,
                10000,
                2,
                1,
                new long[] { 6000 },
                recreationRoutine: new RecreationRoutineDefinition(
                    DecisionChooseRecreation,
                    6000,
                    -5000,
                    new[]
                    {
                        new RecreationCandidateDefinition(OptionTabletopGames, ActivityTabletopGames, InterestTabletopGames),
                        new RecreationCandidateDefinition(OptionReading, ActivityReading, InterestReading),
                    })));
            builder.Add(new NeedDefinition(
                WellKnownNeeds.Energy,
                "Energy",
                0,
                10000,
                -10,
                1,
                new long[] { 0, 1000, 2000, 8000 },
                new NeedRestRoutineDefinition(
                    WellKnownActivities.Sleeping,
                    GroupKinds.Household,
                    2000,
                    8000,
                    20),
                continuationRoutine: new NeedContinuationRoutineDefinition(
                    DecisionRestOrContinue,
                    OptionRest,
                    OptionContinue,
                    2000,
                    1000,
                    new[]
                    {
                        new NeedContinuationCandidateDefinition(ActivityTabletopGames, InterestTabletopGames),
                        new NeedContinuationCandidateDefinition(ActivityReading, InterestReading),
                        new NeedContinuationCandidateDefinition(ActivitySocializing, InterestSocializing),
                    })));

            builder.Add(new ActivityDefinition(ActivityWorking, "Working", SimDuration.FromHours(6), true, true));
            builder.Add(new ActivityDefinition(ActivitySleeping, "Sleeping", SimDuration.FromHours(8), false));
            builder.Add(new ActivityDefinition(ActivityDining, "Dining", SimDuration.FromMinutes(90), false));
            builder.Add(new ActivityDefinition(ActivityHelpingAtBakery, "Helping at the bakery", SimDuration.FromMinutes(90), false));
            builder.Add(new ActivityDefinition(WellKnownActivities.Eating, "Eating", SimDuration.FromMinutes(30), false));
            builder.Add(new ActivityDefinition(WellKnownActivities.Waiting, "Waiting", SimDuration.FromHours(1), false));
            builder.Add(new ActivityDefinition(WellKnownActivities.Traveling, "Traveling", SimDuration.FromMinutes(10), false, false, true));
            builder.Add(new ActivityDefinition(ActivityTabletopGames, "Tabletop Games", SimDuration.FromMinutes(90), false));
            builder.Add(new ActivityDefinition(ActivityReading, "Reading", SimDuration.FromMinutes(60), false));
            builder.Add(new ActivityDefinition(ActivitySocializing, "Socializing", SimDuration.FromMinutes(30), false));
            builder.Add(new ActivityDefinition(ActivityCafeHosting, "Hosting at the cafe", SimDuration.FromMinutes(90), false));

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
                trigger: new NeedThresholdDecisionTrigger(NeedHunger, 6000, ActivityWorking),
                activityOutcomes: new[]
                {
                    new DecisionActivityOutcome(OptionLeave, WellKnownActivities.Waiting, SimDuration.FromHours(1)),
                },
                reasoningProgram: LeaveWorkReasoningProgram()));

            builder.Add(new DecisionDefinition(
                DecisionChooseRecreation,
                new[]
                {
                    RecreationOption(OptionTabletopGames, "Play Tabletop Games", 0, InterestTabletopGames),
                    RecreationOption(OptionReading, "Read", 1, InterestReading),
                },
                SimDuration.FromMinutes(10),
                new AuthoredId("conflict_scope.recreation"),
                reasoningProgram: RecreationReasoningProgram()));

            builder.Add(new DecisionDefinition(
                DecisionSocialInvitation,
                new[]
                {
                    SocialInvitationJoinOption(),
                    SocialInvitationKeepOption(),
                },
                SimDuration.FromMinutes(10),
                new AuthoredId("conflict_scope.social_invitation"),
                reasoningProgram: SocialInvitationReasoningProgram()));

            builder.Add(new DecisionDefinition(
                DecisionRestOrContinue,
                new[]
                {
                    MarkedOption(OptionRest, "Stop and rest", 0, RestOptionMarker),
                    RecreationOption(OptionContinue, "Keep going", 1, InterestTabletopGames),
                },
                SimDuration.FromMinutes(10),
                new AuthoredId("conflict_scope.current_activity"),
                holdEligible: false,
                reasoningProgram: RestOrContinueReasoningProgram()));

            builder.Add(new InterventionDefinition(InterventionStepUp, InterventionKind.StepDieUp, 1));
            builder.Add(new InterventionDefinition(InterventionTemper, InterventionKind.StepDieDown, 1));
            builder.Add(new InterventionDefinition(
                InterventionReroll,
                InterventionKind.Reroll,
                1,
                resourceKind: InterventionResourceKind.ReRoll,
                resourcePolicy: new Vivarium.Domain.PlayerAgency.InterventionResourcePolicy(
                    1, 1, 1, SimDuration.FromDays(1))));
            builder.Add(new InterventionDefinition(
                InterventionLoadedTwenty,
                InterventionKind.ReplaceDie,
                1,
                replacementDie: new Die(20, 20),
                resourceKind: InterventionResourceKind.ReplacementDie,
                resourcePolicy: new Vivarium.Domain.PlayerAgency.InterventionResourcePolicy(1, 1)));

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
            builder.Add(CommitmentEvidence(
                SocialEvidenceCommitmentFulfilled,
                new AuthoredId("social.measurement.commitment_fulfilled"),
                4500,
                new AuthoredId("social.explanation.commitment_fulfilled")));
            builder.Add(CommitmentEvidence(
                SocialEvidenceCommitmentBreach,
                new AuthoredId("social.measurement.commitment_breach"),
                -6000,
                new AuthoredId("social.explanation.commitment_breach")));
            CommitmentAccountabilityPolicy socialCommitmentPolicy = SocialCommitmentAccountabilityPolicy();
            builder.Add(socialCommitmentPolicy);
            builder.Add(new EmploymentDefinition(
                EmploymentBakeryWorker,
                EmploymentRoleBaker,
                new[]
                {
                    new EmploymentObligationPattern(
                        TemplateBakeryShift,
                        CommitmentWorkShift,
                        cycleLengthDays: 7,
                        activeDaysMask: 0b0011111,
                        startMinuteOfDay: 9 * 60,
                        duration: SimDuration.FromHours(5),
                        priority: 100,
                        activityDefinitionId: ActivityWorking,
                        startWindow: SimDuration.FromMinutes(30),
                        accountabilityPolicy: socialCommitmentPolicy),
                    new EmploymentObligationPattern(
                        TemplateBakeryClosingDuty,
                        CommitmentHelpDariusCloseBakery,
                        cycleLengthDays: 7,
                        activeDaysMask: 0b0000001,
                        startMinuteOfDay: 14 * 60,
                        duration: SimDuration.FromMinutes(90),
                        priority: 90,
                        activityDefinitionId: ActivityHelpingAtBakery,
                        startWindow: SimDuration.FromMinutes(10),
                        accountabilityPolicy: socialCommitmentPolicy),
                }));
            builder.Add(new EmploymentDefinition(
                EmploymentCafeHost,
                EmploymentRoleCafeHost,
                new[]
                {
                    new EmploymentObligationPattern(
                        TemplateCafeHostingShift,
                        CommitmentCafeHostingShift,
                        cycleLengthDays: 7,
                        activeDaysMask: 0b0011111,
                        startMinuteOfDay: 14 * 60,
                        duration: SimDuration.FromMinutes(90),
                        priority: 80,
                        activityDefinitionId: ActivityCafeHosting,
                        startWindow: SimDuration.FromMinutes(5),
                        accountabilityPolicy: socialCommitmentPolicy),
                }));
            builder.Add(new SocialPressureDefinition(SocialPressureSeekCompany, new SocialFactorRule[0]));
            builder.Add(new SocialPressureDefinition(SocialPressureReliance, new SocialFactorRule[0]));
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
            builder.Add(new DecisionDefinition(
                DecisionRelyOnPerson,
                new[]
                {
                    new DecisionOption(OptionRely, "Rely on them", 0),
                    new DecisionOption(OptionDoItSelf, "Do it myself", 1),
                },
                SimDuration.FromMinutes(10),
                new AuthoredId("conflict_scope.reliance_target"),
                socialTrigger: new SocialInteractionDecisionTrigger(
                    SocialPressureReliance,
                    AppraisalLenses.Reliance,
                    new SocialDecisionInfluenceSpec(
                        OptionRely,
                        OptionDoItSelf,
                        CategorySocial,
                        new AuthoredId("influence.person_seems_reliable"),
                        new AuthoredId("influence.person_seems_unreliable"),
                        InfluenceVisibility.Full))));

            return builder.Build();
        }

        private static SocialEvidenceDefinition CommitmentEvidence(
            AuthoredId actionId,
            AuthoredId measurementId,
            long observedValue,
            AuthoredId explanationId) =>
            new SocialEvidenceDefinition(
                actionId,
                new[]
                {
                    new SocialEvidenceMeasurement(
                        measurementId,
                        new[]
                        {
                            new SocialLinearTerm(SocialDimensions.Discipline, 7000),
                            new SocialLinearTerm(SocialDimensions.Stability, 3000),
                        },
                        observedValue,
                        30000000),
                },
                explanationId);

        private static CommitmentAccountabilityPolicy SocialCommitmentAccountabilityPolicy()
        {
            var breachDeltas = new System.Collections.Generic.SortedDictionary<AuthoredId, long>
            {
                [RelationshipChannels.TrustJudgment] = -1200,
                [RelationshipChannels.Resentment] = 900,
            };
            var breach = new CommitmentConsequenceSet(
                new CommitmentMemoryConsequence(
                    new AuthoredId("relationship.memory.commitment_breach"),
                    new AuthoredId("relationship.explanation.commitment_breach")),
                SocialEvidenceCommitmentBreach,
                breachDeltas);
            return new CommitmentAccountabilityPolicy(
                byOutcome: new System.Collections.Generic.SortedDictionary<CommitmentOutcomeKind, CommitmentConsequenceSet>
                {
                    [CommitmentOutcomeKind.Fulfilled] = new CommitmentConsequenceSet(
                        evidenceActionId: SocialEvidenceCommitmentFulfilled),
                    [CommitmentOutcomeKind.Relinquished] = breach,
                    [CommitmentOutcomeKind.Missed] = breach,
                    [CommitmentOutcomeKind.Cancelled] = breach,
                },
                id: AccountabilitySocialCommitment);
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

        private static DecisionOption RecreationOption(
            AuthoredId optionId,
            AuthoredId labelId,
            int order,
            AuthoredId interestId)
        {
            var option = new DecisionOption(optionId, labelId, order);
            option.SetContext(
                DecisionReasoningParameters.InterestId,
                DecisionParameterValue.FromAuthoredId(interestId));
            return option;
        }

        private static DecisionReasoningProgram RecreationReasoningProgram()
        {
            var signal = new AuthoredId("decision.signal.recreation.interest");
            return new DecisionReasoningProgram(new[]
            {
                new CompiledConsiderationBinding(
                    new AuthoredId("binding.recreation.interest"),
                    new AuthoredId("consideration.recreation.interest"),
                    1,
                    new[]
                    {
                        new ConsiderationParameter(DecisionReasoningParameters.Actor, DecisionParameterKind.Entity),
                        new ConsiderationParameter(DecisionReasoningParameters.InterestId, DecisionParameterKind.AuthoredId),
                    },
                    new[]
                    {
                        new CompiledParameterBinding(DecisionReasoningParameters.Actor, ParameterBindingSource.DecisionActor),
                        new CompiledParameterBinding(
                            DecisionReasoningParameters.InterestId,
                            ParameterBindingSource.OptionContext,
                            DecisionReasoningParameters.InterestId),
                    },
                    new[] { new DecisionSignalRequest(signal, DecisionSignalProviderIds.ActorInterest) },
                    new SignalFieldDefinition(
                        new AuthoredId("field.recreation.interest"),
                        0,
                        new[] { new SignalLinearTerm(signal, 30000, new AuthoredId("reason.recreation.interest")) },
                        null,
                        null,
                        null),
                    new ReasonChannelDefinition(new AuthoredId("reason_channel.recreation.interest")),
                    ReasonScaleProfile.Standard(),
                    CategoryPersonalConcern,
                    new AuthoredId("influence.recreation.interest"),
                    new AuthoredId("influence.recreation.disinterest"),
                    InfluenceVisibility.Full),
            });
        }

        private static DecisionReasoningProgram RestOrContinueReasoningProgram()
        {
            var energy = new AuthoredId("decision.signal.rest_or_continue.energy");
            var interest = new AuthoredId("decision.signal.rest_or_continue.interest");
            var stillActive = new AuthoredId("decision.signal.rest_or_continue.current_activity");
            return new DecisionReasoningProgram(new[]
            {
                new CompiledConsiderationBinding(
                    new AuthoredId("binding.rest_or_continue.fatigue"),
                    new AuthoredId("consideration.rest_or_continue.fatigue"),
                    1,
                    new[]
                    {
                        new ConsiderationParameter(RestOptionMarker, DecisionParameterKind.Integer),
                        new ConsiderationParameter(DecisionReasoningParameters.NeedId, DecisionParameterKind.AuthoredId),
                    },
                    new[]
                    {
                        new CompiledParameterBinding(RestOptionMarker, ParameterBindingSource.OptionContext, RestOptionMarker),
                        new CompiledParameterBinding(DecisionReasoningParameters.NeedId,
                            ParameterBindingSource.DecisionContext, DecisionReasoningParameters.NeedId),
                    },
                    new[] { new DecisionSignalRequest(energy, DecisionSignalProviderIds.ActorNeed) },
                    new SignalFieldDefinition(
                        new AuthoredId("field.rest_or_continue.fatigue"),
                        8000,
                        new[] { new SignalLinearTerm(energy, -8000, new AuthoredId("reason.fatigue")) },
                        null, null, null),
                    new ReasonChannelDefinition(new AuthoredId("reason_channel.rest_or_continue.fatigue")),
                    ReasonScaleProfile.Standard(),
                    CategoryPersonalConcern,
                    new AuthoredId("influence.needs_rest"),
                    new AuthoredId("influence.feels_restored"),
                    InfluenceVisibility.Full),
                new CompiledConsiderationBinding(
                    new AuthoredId("binding.rest_or_continue.interest"),
                    new AuthoredId("consideration.rest_or_continue.interest"),
                    1,
                    new[]
                    {
                        new ConsiderationParameter(DecisionReasoningParameters.Actor, DecisionParameterKind.Entity),
                        new ConsiderationParameter(DecisionReasoningParameters.InterestId, DecisionParameterKind.AuthoredId),
                    },
                    new[]
                    {
                        new CompiledParameterBinding(DecisionReasoningParameters.Actor, ParameterBindingSource.DecisionActor),
                        new CompiledParameterBinding(DecisionReasoningParameters.InterestId,
                            ParameterBindingSource.OptionContext, DecisionReasoningParameters.InterestId),
                    },
                    new[] { new DecisionSignalRequest(interest, DecisionSignalProviderIds.ActorInterest) },
                    new SignalFieldDefinition(
                        new AuthoredId("field.rest_or_continue.interest"),
                        0,
                        new[] { new SignalLinearTerm(interest, 24000, new AuthoredId("reason.engrossed")) },
                        null, null, null),
                    new ReasonChannelDefinition(new AuthoredId("reason_channel.rest_or_continue.interest")),
                    ReasonScaleProfile.Standard(),
                    CategoryPersonalConcern,
                    new AuthoredId("influence.wants_to_continue"),
                    new AuthoredId("influence.ready_to_stop"),
                    InfluenceVisibility.Full),
                new CompiledConsiderationBinding(
                    new AuthoredId("binding.rest_or_continue.current_activity"),
                    new AuthoredId("consideration.rest_or_continue.current_activity"),
                    1,
                    new[]
                    {
                        new ConsiderationParameter(DecisionReasoningParameters.InterestId, DecisionParameterKind.AuthoredId),
                        new ConsiderationParameter(DecisionReasoningParameters.PlannedActivity, DecisionParameterKind.Entity),
                    },
                    new[]
                    {
                        new CompiledParameterBinding(DecisionReasoningParameters.InterestId,
                            ParameterBindingSource.OptionContext, DecisionReasoningParameters.InterestId),
                        new CompiledParameterBinding(DecisionReasoningParameters.PlannedActivity,
                            ParameterBindingSource.DecisionContext, DecisionReasoningParameters.PlannedActivity),
                    },
                    new[] { new DecisionSignalRequest(stillActive, DecisionSignalProviderIds.CurrentActivityIdentity) },
                    new SignalFieldDefinition(
                        new AuthoredId("field.rest_or_continue.current_activity"),
                        0,
                        new[] { new SignalLinearTerm(stillActive, 2000, new AuthoredId("reason.current_activity")) },
                        null, null, null),
                    new ReasonChannelDefinition(new AuthoredId("reason_channel.rest_or_continue.current_activity")),
                    ReasonScaleProfile.Standard(),
                    CategoryPractical,
                    new AuthoredId("influence.already_engaged"),
                    new AuthoredId("influence.activity_ended"),
                    InfluenceVisibility.Full),
            });
        }

        private static DecisionReasoningProgram SocialInvitationReasoningProgram()
        {
            var comfort = new AuthoredId("decision.signal.social_invitation.comfort");
            var available = new AuthoredId("decision.signal.social_invitation.shared_context");
            var planInterest = new AuthoredId("decision.signal.social_invitation.plan_interest");
            return new DecisionReasoningProgram(new[]
            {
                new CompiledConsiderationBinding(
                    new AuthoredId("binding.social_invitation.comfort"),
                    ConsiderationIds.InterpersonalComfort,
                    1,
                    new[]
                    {
                        new ConsiderationParameter(DecisionReasoningParameters.Actor, DecisionParameterKind.Entity),
                        new ConsiderationParameter(DecisionReasoningParameters.Target, DecisionParameterKind.Entity),
                        new ConsiderationParameter(DecisionReasoningParameters.AppraisalLensId, DecisionParameterKind.AuthoredId),
                        new ConsiderationParameter(DecisionReasoningParameters.SocialPressureId, DecisionParameterKind.AuthoredId),
                    },
                    new[]
                    {
                        new CompiledParameterBinding(DecisionReasoningParameters.Actor, ParameterBindingSource.DecisionActor),
                        new CompiledParameterBinding(DecisionReasoningParameters.Target, ParameterBindingSource.OptionContext,
                            DecisionReasoningParameters.Target),
                        new CompiledParameterBinding(DecisionReasoningParameters.AppraisalLensId, ParameterBindingSource.Literal,
                            literal: DecisionParameterValue.FromAuthoredId(AppraisalLenses.Affiliation)),
                        new CompiledParameterBinding(DecisionReasoningParameters.SocialPressureId, ParameterBindingSource.Literal,
                            literal: DecisionParameterValue.FromAuthoredId(SocialPressureSeekCompany)),
                    },
                    new[] { new DecisionSignalRequest(comfort, DecisionSignalProviderIds.SocialAppraisal) },
                    new SignalFieldDefinition(
                        new AuthoredId("field.social_invitation.comfort"),
                        0,
                        new[] { new SignalLinearTerm(comfort, 10000, new AuthoredId("reason.social_comfort")) },
                        null, null, null),
                    new ReasonChannelDefinition(ReasonChannelIds.InterpersonalComfort),
                    ReasonScaleProfile.Standard(),
                    CategorySocial,
                    new AuthoredId("influence.enjoys_inviter"),
                    new AuthoredId("influence.uncomfortable_with_inviter"),
                    InfluenceVisibility.Existence | InfluenceVisibility.Category | InfluenceVisibility.Magnitude),
                new CompiledConsiderationBinding(
                    new AuthoredId("binding.social_invitation.shared_context"),
                    new AuthoredId("consideration.social_invitation.shared_context"),
                    1,
                    new[]
                    {
                        new ConsiderationParameter(DecisionReasoningParameters.Target, DecisionParameterKind.Entity),
                        new ConsiderationParameter(DecisionReasoningParameters.PlannedActivity, DecisionParameterKind.Entity),
                        new ConsiderationParameter(DecisionReasoningParameters.ActivityDefinitionId, DecisionParameterKind.AuthoredId),
                    },
                    new[]
                    {
                        new CompiledParameterBinding(DecisionReasoningParameters.Target, ParameterBindingSource.OptionContext,
                            DecisionReasoningParameters.Target),
                        new CompiledParameterBinding(DecisionReasoningParameters.PlannedActivity,
                            ParameterBindingSource.DecisionContext, DecisionReasoningParameters.PlannedActivity),
                        new CompiledParameterBinding(DecisionReasoningParameters.ActivityDefinitionId,
                            ParameterBindingSource.Literal,
                            literal: DecisionParameterValue.FromAuthoredId(ActivitySocializing)),
                    },
                    new[] { new DecisionSignalRequest(available, DecisionSignalProviderIds.SharedActivityContext) },
                    new SignalFieldDefinition(
                        new AuthoredId("field.social_invitation.shared_context"),
                        0,
                        new[] { new SignalLinearTerm(available, 8000, new AuthoredId("reason.shared_context")) },
                        null, null, null),
                    new ReasonChannelDefinition(new AuthoredId("reason_channel.social_invitation.shared_context")),
                    ReasonScaleProfile.Standard(),
                    CategoryPractical,
                    new AuthoredId("influence.inviter_is_here"),
                    new AuthoredId("influence.inviter_is_gone"),
                    InfluenceVisibility.Full),
                new CompiledConsiderationBinding(
                    new AuthoredId("binding.social_invitation.existing_plan"),
                    new AuthoredId("consideration.social_invitation.existing_plan"),
                    1,
                    new[]
                    {
                        new ConsiderationParameter(DecisionReasoningParameters.Actor, DecisionParameterKind.Entity),
                        new ConsiderationParameter(DecisionReasoningParameters.InterestId, DecisionParameterKind.AuthoredId),
                    },
                    new[]
                    {
                        new CompiledParameterBinding(DecisionReasoningParameters.Actor, ParameterBindingSource.DecisionActor),
                        new CompiledParameterBinding(DecisionReasoningParameters.InterestId, ParameterBindingSource.OptionContext,
                            DecisionReasoningParameters.InterestId),
                    },
                    new[] { new DecisionSignalRequest(planInterest, DecisionSignalProviderIds.ActorInterest) },
                    new SignalFieldDefinition(
                        new AuthoredId("field.social_invitation.existing_plan"),
                        0,
                        new[] { new SignalLinearTerm(planInterest, 12000, new AuthoredId("reason.existing_plan")) },
                        null, null, null),
                    new ReasonChannelDefinition(new AuthoredId("reason_channel.social_invitation.existing_plan")),
                    ReasonScaleProfile.Standard(),
                    CategoryPersonalConcern,
                    new AuthoredId("influence.values_existing_plan"),
                    new AuthoredId("influence.dislikes_existing_plan"),
                    InfluenceVisibility.Full),
            });
        }

        private static DecisionOption SocialInvitationJoinOption()
        {
            var option = new DecisionOption(OptionJoinInvitation, "Join them", 0);
            option.SetContext(
                DecisionReasoningParameters.Target,
                DecisionParameterValue.FromEntity(new EntityRef(EntityKind.Character, 0)));
            return option;
        }

        private static DecisionOption SocialInvitationKeepOption()
        {
            var option = new DecisionOption(OptionKeepPlan, "Keep my plan", 1);
            option.SetContext(
                DecisionReasoningParameters.InterestId,
                DecisionParameterValue.FromAuthoredId(InterestReading));
            return option;
        }

    }
}
