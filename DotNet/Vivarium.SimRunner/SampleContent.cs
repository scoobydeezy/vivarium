using Vivarium.Domain.Content;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
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

        public static readonly AuthoredId LocationKindWorld = new AuthoredId("location_kind.world");
        public static readonly AuthoredId LocationKindTown = new AuthoredId("location_kind.town");
        public static readonly AuthoredId LocationKindBuilding = new AuthoredId("location_kind.building");

        public static readonly AuthoredId TravelModeWalking = new AuthoredId("travel_mode.walking");

        public static readonly AuthoredId CommitmentWorkShift = new AuthoredId("commitment.work_shift");
        public static readonly AuthoredId TemplateBakeryShift = new AuthoredId("routine.bakery_shift");

        public static readonly AuthoredId DecisionJobOffer = new AuthoredId("decision.job_offer");
        public static readonly AuthoredId DecisionLeaveWork = new AuthoredId("decision.leave_work_early");
        public static readonly AuthoredId OptionAccept = new AuthoredId("option.accept");
        public static readonly AuthoredId OptionStay = new AuthoredId("option.stay");
        public static readonly AuthoredId OptionLeave = new AuthoredId("option.leave");

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
                DecisionLeaveWork,
                new[]
                {
                    new DecisionOption(OptionLeave, "Leave work early", 0),
                    new DecisionOption(OptionStay, "Finish the shift", 1),
                },
                SimDuration.FromMinutes(10),
                new AuthoredId("conflict_scope.current_activity"),
                importance: 20,
                trigger: new NeedThresholdDecisionTrigger(NeedHunger, 6000),
                influenceTemplates: new[]
                {
                    new DecisionInfluenceTemplate(
                        OptionLeave,
                        CategoryPersonalConcern,
                        new AuthoredId("influence.hunger"),
                        Die.D20,
                        InfluenceVisibility.Full,
                        subjectIsCharacter: true),
                    new DecisionInfluenceTemplate(
                        OptionLeave,
                        CategorySocial,
                        InfluenceBadWorkContext,
                        Die.D10,
                        InfluenceVisibility.Existence | InfluenceVisibility.Category | InfluenceVisibility.Magnitude,
                        subjectIsCharacter: true),
                    new DecisionInfluenceTemplate(
                        OptionStay,
                        CategoryPractical,
                        new AuthoredId("influence.reliability"),
                        Die.D6,
                        InfluenceVisibility.Full),
                },
                activityOutcomes: new[]
                {
                    new DecisionActivityOutcome(OptionLeave, WellKnownActivities.Waiting, SimDuration.FromHours(1)),
                },
                dependencyTemplates: new[] { new DecisionDependencyKey(ContextWorkPressure) }));

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
