using System.Collections.Generic;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Employment;
using Vivarium.Domain.Groups;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Social;
using Vivarium.Domain.Time;
using Vivarium.Infrastructure.Bootstrap;

namespace Vivarium.SimRunner
{
    /// <summary>Handles to the places the scenario cares about.</summary>
    public sealed class SampleWorldLayout
    {
        public LocationId World;
        public LocationId Town;
        public LocationId Home;
        public LocationId Bakery;
        public LocationId Cafe;

        public CharacterId Mina;
        public CharacterId Glen;
        public CharacterId Darius;
        public CharacterId Lena;
        public GroupId Employer;
        public EmploymentId MinaEmployment;
        public EmploymentId GlenEmployment;
    }

    /// <summary>
    /// Builds the small world the headless scenario runs in (§55).
    /// <para>
    /// Eight to twelve characters, a couple of locations with a travel connection, one recurring routine,
    /// a relationship, some traits, and one decision type — enough to exercise the architecture without
    /// committing to a setting.
    /// </para>
    /// </summary>
    public static class SampleWorld
    {
        public static SampleWorldLayout Populate(SimulationHost host, int extraPopulation = 0)
        {
            WorldState world = host.World;
            SimulationContext context = host.Simulation;
            var layout = new SampleWorldLayout();

            // --- containment hierarchy (§27) ---
            layout.World = AddLocation(world, LocationId.None, SampleContent.LocationKindWorld, "World");
            layout.Town = AddLocation(world, layout.World, SampleContent.LocationKindTown, "Eastmarket");
            layout.Home = AddLocation(
                world,
                layout.Town,
                SampleContent.LocationKindBuilding,
                "Mina's flat",
                new[] { WellKnownActivities.Eating, SampleContent.ActivityReading });
            layout.Bakery = AddLocation(world, layout.Town, SampleContent.LocationKindBuilding, "East Market Bakery");
            layout.Cafe = AddLocation(
                world,
                layout.Town,
                SampleContent.LocationKindBuilding,
                "Eastmarket Commons",
                new[]
                {
                    SampleContent.ActivityTabletopGames,
                    SampleContent.ActivityReading,
                    SampleContent.ActivitySocializing,
                },
                supportsPlayerManagedAvailability: true);

            // --- travel topology, separate from containment (§28) ---
            world.TravelNetwork.ConnectBidirectional(layout.Home, layout.Bakery, SimDuration.FromMinutes(12), SampleContent.TravelModeWalking);
            world.TravelNetwork.ConnectBidirectional(layout.Home, layout.Cafe, SimDuration.FromMinutes(5), SampleContent.TravelModeWalking);
            world.TravelNetwork.ConnectBidirectional(layout.Cafe, layout.Bakery, SimDuration.FromMinutes(9), SampleContent.TravelModeWalking);

            // --- characters ---
            layout.Mina = AddCharacter(host, "Mina Cairn", layout.Home, new[] { SampleContent.TraitAmbitious, SampleContent.TraitEnjoysBaking }, 2000);
            layout.Glen = AddCharacter(host, "Glen Ashby", layout.Home, new[] { SampleContent.TraitHomebound }, 2000);
            layout.Darius = AddCharacter(host, "Darius Vale", layout.Bakery, new[] { SampleContent.TraitAmbitious }, 1000);
            layout.Lena = AddCharacter(host, "Lena Marsh", layout.Cafe, new AuthoredId[0], 1500);
            AddRecreationRoutine(host, layout.Glen, 5990, tabletopInterest: 4500, readingInterest: 2500);
            AddSocialRoutine(host, layout.Lena, 7000);

            var employer = new Group(
                world.RuntimeIds.Groups.Next(),
                GroupKinds.Employer,
                "East Market Bakery",
                layout.Bakery);
            world.Groups.Add(employer.Id, employer);
            layout.Employer = employer.Id;
            Employment minaEmployment = host.Employments.Create(
                context,
                layout.Mina,
                employer.Id,
                SampleContent.EmploymentBakeryWorker,
                layout.Darius,
                new[] { SampleContent.TemplateBakeryShift, SampleContent.TemplateBakeryClosingDuty });
            Employment glenEmployment = host.Employments.Create(
                context,
                layout.Glen,
                employer.Id,
                SampleContent.EmploymentBakeryWorker,
                layout.Darius,
                new[] { SampleContent.TemplateBakeryShift });
            layout.MinaEmployment = minaEmployment.Id;
            layout.GlenEmployment = glenEmployment.Id;

            // A synthetic crowd, to prove the same systems run at a larger scale (§49, §56).
            for (int i = 0; i < extraPopulation; i++)
            {
                AddCharacter(host, "Resident " + (i + 1), layout.Town, new[] { SampleContent.TraitAmbitious });
            }

            // --- a relationship (§32) ---
            var relationship = new Relationship(
                world.RuntimeIds.Relationships.Next(),
                layout.Mina,
                layout.Glen,
                new AuthoredId("relationship.friend"),
                AnalyticalProgression.Linear(3200, world.Clock.Now, 0, 1, -10000, 10000),
                world.Clock.Now);

            world.Relationships.Add(relationship.Id, relationship);
            world.RelationshipIndex.Register(relationship);

            var dislikedBoss = new Relationship(
                world.RuntimeIds.Relationships.Next(),
                layout.Mina,
                layout.Darius,
                new AuthoredId("relationship.disliked_boss"),
                AnalyticalProgression.Constant(-5000, world.Clock.Now),
                world.Clock.Now);
            world.Relationships.Add(dislikedBoss.Id, dislikedBoss);
            world.RelationshipIndex.Register(dislikedBoss);

            var workPressure = new WorkContextPressureService(
                host.Transitions,
                host.DecisionReevaluation,
                SampleContent.ActivityWorking,
                SampleContent.ModifierDislikedColleague,
                SampleContent.ContextWorkPressure,
                affinityThreshold: -1000,
                pressuredRate: -2);
            host.DomainEventHandlers.Register(new WorkContextArrivalHandler(workPressure), 200);
            host.DomainEventHandlers.Register(new WorkContextDepartureHandler(workPressure), 100);

            // Employment is the production source of regular shifts and Mina's closing duty. Glen's
            // matching shift creates the same shared-route interaction without scenario injection.
            host.Employments.MaterializeCommitments(context, minaEmployment, SimDuration.FromDays(2));
            host.Employments.MaterializeCommitments(context, glenEmployment, SimDuration.FromDays(2));

            // After the established leave-work beat, Mina learns about dinner with Glen. Her closing
            // duty ends before dinner begins, but Bakery-to-Cafe travel makes the pair jointly
            // infeasible. Intent becomes authoritative only when it is known, so the conflict is not
            // visible before its final cause.
            SimTime revealAt = world.Clock.Now.Plus(SimDuration.FromHours(6));
            SimTime dinnerStart = world.Clock.Now
                .Plus(SimDuration.FromHours(8))
                .Plus(SimDuration.FromMinutes(35));
            ScheduleCommitmentReveal(world, revealAt, new CommitmentBecomesKnownPayload(
                layout.Mina,
                SampleContent.CommitmentDinnerWithGlen,
                dinnerStart,
                dinnerStart.Plus(SimDuration.FromMinutes(2)),
                SimDuration.FromMinutes(90),
                layout.Cafe,
                70,
                SampleContent.ActivityDining,
                new[] { layout.Glen },
                accountabilityPolicy: host.Catalog.CommitmentAccountabilityPolicies[SampleContent.AccountabilitySocialCommitment]));

            return layout;
        }

        private static void ScheduleCommitmentReveal(
            WorldState world,
            SimTime revealAt,
            CommitmentBecomesKnownPayload payload) =>
            world.Scheduler.Schedule(
                revealAt,
                SchedulePhase.Preparation,
                ScheduledEventTypes.CommitmentBecomesKnown,
                payload);

        private static LocationId AddLocation(
            WorldState world,
            LocationId parent,
            AuthoredId kind,
            string name,
            IReadOnlyList<AuthoredId> activityAffordances = null,
            bool supportsPlayerManagedAvailability = false)
        {
            var node = new LocationNode(
                world.RuntimeIds.Locations.Next(),
                parent,
                kind,
                name,
                activityAffordances: activityAffordances,
                supportsPlayerManagedAvailability: supportsPlayerManagedAvailability);
            world.Locations.Add(node);
            return node.Id;
        }

        private static CharacterId AddCharacter(
            SimulationHost host,
            string name,
            LocationId startingLocation,
            AuthoredId[] traits,
            long initialHunger = 2000)
        {
            WorldState world = host.World;

            var character = new Character(world.RuntimeIds.Characters.Next(), name, world.Clock.Now);
            for (int i = 0; i < traits.Length; i++)
            {
                character.AddTrait(traits[i]);
            }

            world.Characters.Add(character.Id, character);
            new SocialProfileGenerator(host.Simulation.Random).Generate(character, SampleContent.SocialCalibrationStandard);

            // Needs progress analytically and arm their own threshold events (§10.1, §10.2).
            NeedDefinition hunger = host.Catalog.Needs[SampleContent.NeedHunger];
            var hungerState = new NeedState(
                hunger.Id,
                AnalyticalProgression.Linear(initialHunger, world.Clock.Now, hunger.DefaultRateNumerator, hunger.DefaultRateDenominator, hunger.MinValue, hunger.MaxValue),
                hunger.BehaviouralThresholds[0]);

            character.SetNeed(hungerState);
            host.Needs.Rearm(host.Simulation, character, hungerState);

            NeedDefinition energy = host.Catalog.Needs[WellKnownNeeds.Energy];
            var energyState = new NeedState(
                energy.Id,
                AnalyticalProgression.Linear(9000, world.Clock.Now, energy.DefaultRateNumerator, energy.DefaultRateDenominator, energy.MinValue, energy.MaxValue),
                energy.RestRoutine.ActivationThreshold);
            character.SetNeed(energyState);
            host.Needs.Rearm(host.Simulation, character, energyState);

            var household = new Group(
                world.RuntimeIds.Groups.Next(),
                GroupKinds.Household,
                name + " household",
                startingLocation);
            world.Groups.Add(household.Id, household);
            world.Memberships.Join(household.Id, character.Id);

            // Every active character has exactly one primary Activity from the moment they exist (§29.1).
            host.Transitions.BeginActivity(
                host.Simulation,
                character.Id,
                WellKnownActivities.Waiting,
                startingLocation,
                SimDuration.FromHours(1));

            return character.Id;
        }

        private static void AddRecreationRoutine(
            SimulationHost host,
            CharacterId characterId,
            long initialRecreation,
            long tabletopInterest,
            long readingInterest)
        {
            WorldState world = host.World;
            Character character = world.Characters.Get(characterId);
            character.Interests.Set(SampleContent.InterestTabletopGames, tabletopInterest);
            character.Interests.Set(SampleContent.InterestReading, readingInterest);
            NeedDefinition recreation = host.Catalog.Needs[WellKnownNeeds.Recreation];
            var state = new NeedState(
                recreation.Id,
                AnalyticalProgression.Linear(
                    initialRecreation,
                    world.Clock.Now,
                    recreation.DefaultRateNumerator,
                    recreation.DefaultRateDenominator,
                    recreation.MinValue,
                    recreation.MaxValue),
                recreation.RecreationRoutine.ActivationThreshold);
            character.SetNeed(state);
            host.Needs.Rearm(host.Simulation, character, state);
        }

        private static void AddSocialRoutine(
            SimulationHost host,
            CharacterId characterId,
            long initialSocial)
        {
            WorldState world = host.World;
            Character character = world.Characters.Get(characterId);
            character.Interests.Set(SampleContent.InterestSocializing, 7000);
            NeedDefinition social = host.Catalog.Needs[WellKnownNeeds.Social];
            var state = new NeedState(
                social.Id,
                AnalyticalProgression.Linear(
                    initialSocial,
                    world.Clock.Now,
                    social.DefaultRateNumerator,
                    social.DefaultRateDenominator,
                    social.MinValue,
                    social.MaxValue),
                social.SocializingRoutine.ActivationThreshold);
            character.SetNeed(state);
            host.Needs.Rearm(host.Simulation, character, state);
        }
    }
}
