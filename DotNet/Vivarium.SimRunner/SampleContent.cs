using Vivarium.Domain.Content;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Spatial;
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
        public static readonly AuthoredId OptionAccept = new AuthoredId("option.accept");
        public static readonly AuthoredId OptionStay = new AuthoredId("option.stay");

        public static readonly AuthoredId ConflictScopeEmployment = new AuthoredId("conflict_scope.employment");

        public static readonly AuthoredId InterventionStepUp = new AuthoredId("intervention.encourage");
        public static readonly AuthoredId InterventionReroll = new AuthoredId("intervention.reconsider");

        public static readonly AuthoredId CategoryPersonalConcern = new AuthoredId("influence_category.personal_concern");
        public static readonly AuthoredId CategoryPractical = new AuthoredId("influence_category.practical");
        public static readonly AuthoredId CategorySocial = new AuthoredId("influence_category.social");

        public static readonly AuthoredId ContextHousingMarket = new AuthoredId("decision_context.local_opportunity");

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

            builder.Add(new InterventionDefinition(InterventionStepUp, InterventionKind.StepDieUp, 1));
            builder.Add(new InterventionDefinition(InterventionReroll, InterventionKind.Reroll, 1));

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
