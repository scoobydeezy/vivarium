using Vivarium.Domain.Common;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Decisions;

namespace Vivarium.Infrastructure.Bootstrap
{
    /// <summary>Stable authored ids required by the production-shaped minimum playable world.</summary>
    public static class MinimumPlayableContent
    {
        public static readonly AuthoredId TraitAmbitious = new AuthoredId("trait.ambitious");
        public static readonly AuthoredId TraitEnjoysBaking = new AuthoredId("trait.enjoys_baking");
        public static readonly AuthoredId TraitHomebound = new AuthoredId("trait.homebody");
        public static readonly AuthoredId NeedHunger = new AuthoredId("need.hunger");
        public static readonly AuthoredId ActivityWorking = new AuthoredId("activity.working");
        public static readonly AuthoredId ActivityDining = new AuthoredId("activity.dining");
        public static readonly AuthoredId ActivityTabletopGames = new AuthoredId("activity.tabletop_games");
        public static readonly AuthoredId ActivityReading = new AuthoredId("activity.reading");
        public static readonly AuthoredId ActivitySocializing = WellKnownActivities.Socializing;
        public static readonly AuthoredId InterestTabletopGames = new AuthoredId("interest.tabletop_games");
        public static readonly AuthoredId InterestReading = new AuthoredId("interest.reading");
        public static readonly AuthoredId InterestSocializing = new AuthoredId("interest.socializing");
        public static readonly AuthoredId LocationKindWorld = new AuthoredId("location_kind.world");
        public static readonly AuthoredId LocationKindTown = new AuthoredId("location_kind.town");
        public static readonly AuthoredId LocationKindBuilding = new AuthoredId("location_kind.building");
        public static readonly AuthoredId TravelModeWalking = new AuthoredId("travel_mode.walking");
        public static readonly AuthoredId CommitmentDinnerWithGlen = new AuthoredId("commitment.dinner_with_glen");
        public static readonly AuthoredId EmploymentBakeryWorker = new AuthoredId("employment.bakery_worker");
        public static readonly AuthoredId EmploymentCafeHost = new AuthoredId("employment.cafe_host");
        public static readonly AuthoredId TemplateBakeryShift = new AuthoredId("routine.bakery_shift");
        public static readonly AuthoredId TemplateBakeryClosingDuty = new AuthoredId("routine.bakery_closing_duty");
        public static readonly AuthoredId TemplateCafeHostingShift = new AuthoredId("routine.cafe_hosting_shift");
        public static readonly AuthoredId SocialCalibrationStandard = new AuthoredId("social.calibration.standard");
        public static readonly AuthoredId AccountabilitySocialCommitment = new AuthoredId("accountability.social_commitment");
        public static readonly AuthoredId ContextWorkPressure = new AuthoredId("decision_context.work_pressure");
        public static readonly AuthoredId ModifierDislikedColleague = new AuthoredId("activity_modifier.disliked_colleague_present");
    }
}
