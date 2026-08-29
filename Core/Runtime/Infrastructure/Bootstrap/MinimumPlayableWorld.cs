using System.Collections.Generic;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Attention;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Employment;
using Vivarium.Domain.Groups;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Social;
using Vivarium.Domain.Time;
using Vivarium.Infrastructure.Bootstrap;

namespace Vivarium.Infrastructure.Bootstrap
{
    /// <summary>Handles to the places the scenario cares about.</summary>
    public sealed class MinimumPlayableWorldLayout
    {
        public LocationId World;
        public LocationId Town;
        public LocationId Home;
        public LocationId Bakery;
        public LocationId Cafe;
        public LocationId Commons;

        public CharacterId Mina;
        public CharacterId Glen;
        public CharacterId Darius;
        public CharacterId Lena;
        public CharacterId Priya;
        public CharacterId Marcus;
        public CharacterId Tess;
        public CharacterId Owen;
        public CharacterId Jo;
        public CharacterId Ravi;
        public GroupId Employer;
        public GroupId CommonsEmployer;
        public EmploymentId MinaEmployment;
        public EmploymentId GlenEmployment;
        public EmploymentId PriyaEmployment;
        public EmploymentId MarcusEmployment;
        public EmploymentId JoEmployment;
        public EmploymentId LenaEmployment;
    }

    /// <summary>
    /// Builds the minimum playable world hosted by both Unity and the headless acceptance runner (§55).
    /// <para>
    /// Eight to twelve characters, a couple of locations with a travel connection, one recurring routine,
    /// a relationship, some traits, and one decision type — enough to exercise the architecture without
    /// committing to a setting.
    /// </para>
    /// </summary>
    public static class MinimumPlayableWorld
    {
        public static MinimumPlayableWorldLayout Populate(SimulationHost host, int extraPopulation = 0)
        {
            WorldState world = host.World;
            SimulationContext context = host.Simulation;
            var layout = new MinimumPlayableWorldLayout();

            // --- containment hierarchy (§27) ---
            layout.World = AddLocation(world, LocationId.None, MinimumPlayableContent.LocationKindWorld, "World");
            layout.Town = AddLocation(world, layout.World, MinimumPlayableContent.LocationKindTown, "Eastmarket");
            layout.Home = AddLocation(
                world,
                layout.Town,
                MinimumPlayableContent.LocationKindBuilding,
                "Mina's flat",
                new[] { WellKnownActivities.Eating, MinimumPlayableContent.ActivityReading });
            layout.Bakery = AddLocation(world, layout.Town, MinimumPlayableContent.LocationKindBuilding, "East Market Bakery");
            layout.Cafe = AddLocation(
                world,
                layout.Town,
                MinimumPlayableContent.LocationKindBuilding,
                "Eastmarket Commons",
                new[]
                {
                    MinimumPlayableContent.ActivityTabletopGames,
                    MinimumPlayableContent.ActivityReading,
                    MinimumPlayableContent.ActivitySocializing,
                },
                supportsPlayerManagedAvailability: true);
            layout.Commons = layout.Cafe;

            // --- travel topology, separate from containment (§28) ---
            world.TravelNetwork.ConnectBidirectional(layout.Home, layout.Bakery, SimDuration.FromMinutes(12), MinimumPlayableContent.TravelModeWalking);
            world.TravelNetwork.ConnectBidirectional(layout.Home, layout.Cafe, SimDuration.FromMinutes(5), MinimumPlayableContent.TravelModeWalking);
            world.TravelNetwork.ConnectBidirectional(layout.Cafe, layout.Bakery, SimDuration.FromMinutes(9), MinimumPlayableContent.TravelModeWalking);

            // --- characters: the locked MPS cast begins in deliberately staggered life states ---
            GroupId cairnHousehold = AddHousehold(world, "Cairn household", layout.Home);
            GroupId ashbyHousehold = AddHousehold(world, "Ashby household", layout.Home);
            layout.Mina = AddCharacter(host, "Mina Cairn", layout.Home, new[] { MinimumPlayableContent.TraitAmbitious, MinimumPlayableContent.TraitEnjoysBaking }, 2000, householdId: cairnHousehold);
            layout.Glen = AddCharacter(host, "Glen Ashby", layout.Home, new[] { MinimumPlayableContent.TraitHomebound }, 2000, householdId: ashbyHousehold);
            layout.Darius = AddCharacter(host, "Darius Vale", layout.Bakery, new[] { MinimumPlayableContent.TraitAmbitious }, 1000);
            layout.Lena = AddCharacter(host, "Lena Marsh", layout.Cafe, new AuthoredId[0], 1500);
            layout.Priya = AddCharacter(host, "Priya Nair", layout.Home, new AuthoredId[0], 6200);
            layout.Marcus = AddCharacter(host, "Marcus Reed", layout.Bakery, new[] { MinimumPlayableContent.TraitAmbitious }, 2500);
            layout.Tess = AddCharacter(host, "Tess Cairn", layout.Home, new[] { MinimumPlayableContent.TraitHomebound }, 5900, householdId: cairnHousehold);
            layout.Owen = AddCharacter(host, "Owen Hart", layout.Home, new AuthoredId[0], 1800);
            layout.Jo = AddCharacter(host, "Jo Ashby", layout.Home, new AuthoredId[0], 2200, initialEnergy: 1000, householdId: ashbyHousehold);
            layout.Ravi = AddCharacter(host, "Ravi Shah", layout.Cafe, new AuthoredId[0], 3000);
            world.Attention.SetPolicy(layout.Mina, AttentionPolicy.AutoHold);
            AddRecreationRoutine(host, layout.Glen, 5990, tabletopInterest: 4500, readingInterest: 2500);
            AddSocialRoutine(host, layout.Lena, 7000);
            AddRecreationRoutine(host, layout.Owen, 5020, tabletopInterest: 8500, readingInterest: 0);
            AddRecreationRoutine(host, layout.Priya, 1000, tabletopInterest: 1000, readingInterest: 3500);
            AddSocialRoutine(host, layout.Tess, 6500);

            host.Transitions.BeginActivity(
                context,
                layout.Priya,
                WellKnownActivities.Eating,
                layout.Home,
                host.Catalog.Activities[WellKnownActivities.Eating].DefaultDuration,
                committedParameters: new SortedDictionary<AuthoredId, long>
                {
                    [ActivityNeedParameters.SatisfactionOffset(MinimumPlayableContent.NeedHunger)] = -5000,
                });
            host.Transitions.BeginActivity(
                context,
                layout.Marcus,
                MinimumPlayableContent.ActivityWorking,
                layout.Bakery,
                SimDuration.FromHours(3));
            host.Transitions.BeginActivity(
                context,
                layout.Jo,
                WellKnownActivities.Sleeping,
                layout.Home,
                host.Catalog.Activities[WellKnownActivities.Sleeping].DefaultDuration);
            if (!host.Transitions.TryBeginTravel(context, layout.Ravi, layout.Bakery, out ActivityInstance _))
                throw new System.InvalidOperationException("The authored MPS route must let Ravi begin in Transit.");

            var employer = new Group(
                world.RuntimeIds.Groups.Next(),
                GroupKinds.Employer,
                "East Market Bakery",
                layout.Bakery);
            world.Groups.Add(employer.Id, employer);
            layout.Employer = employer.Id;
            var commonsEmployer = new Group(
                world.RuntimeIds.Groups.Next(),
                GroupKinds.Employer,
                "Eastmarket Commons",
                layout.Commons);
            world.Groups.Add(commonsEmployer.Id, commonsEmployer);
            layout.CommonsEmployer = commonsEmployer.Id;
            Employment minaEmployment = host.Employments.Create(
                context,
                layout.Mina,
                employer.Id,
                MinimumPlayableContent.EmploymentBakeryWorker,
                layout.Darius,
                new[] { MinimumPlayableContent.TemplateBakeryShift, MinimumPlayableContent.TemplateBakeryClosingDuty });
            Employment glenEmployment = host.Employments.Create(
                context,
                layout.Glen,
                employer.Id,
                MinimumPlayableContent.EmploymentBakeryWorker,
                layout.Darius,
                new[] { MinimumPlayableContent.TemplateBakeryShift });
            Employment priyaEmployment = host.Employments.Create(
                context,
                layout.Priya,
                employer.Id,
                MinimumPlayableContent.EmploymentBakeryWorker,
                layout.Darius,
                new[] { MinimumPlayableContent.TemplateBakeryShift });
            Employment marcusEmployment = host.Employments.Create(
                context,
                layout.Marcus,
                employer.Id,
                MinimumPlayableContent.EmploymentBakeryWorker,
                layout.Darius,
                new[] { MinimumPlayableContent.TemplateBakeryShift });
            Employment joEmployment = host.Employments.Create(
                context,
                layout.Jo,
                commonsEmployer.Id,
                MinimumPlayableContent.EmploymentCafeHost,
                layout.Lena,
                new[] { MinimumPlayableContent.TemplateCafeHostingShift });
            Employment lenaEmployment = host.Employments.Create(
                context,
                layout.Lena,
                commonsEmployer.Id,
                MinimumPlayableContent.EmploymentCafeHost,
                supervisorId: default,
                new[] { MinimumPlayableContent.TemplateCafeHostingShift });
            layout.MinaEmployment = minaEmployment.Id;
            layout.GlenEmployment = glenEmployment.Id;
            layout.PriyaEmployment = priyaEmployment.Id;
            layout.MarcusEmployment = marcusEmployment.Id;
            layout.JoEmployment = joEmployment.Id;
            layout.LenaEmployment = lenaEmployment.Id;

            // A synthetic crowd, to prove the same systems run at a larger scale (§49, §56).
            for (int i = 0; i < extraPopulation; i++)
            {
                AddCharacter(host, "Resident " + (i + 1), layout.Town, new[] { MinimumPlayableContent.TraitAmbitious });
            }

            SeedSocialTopology(world, layout);

            ConfigureScenarioServices(host);

            // Employment is the production source of regular shifts and Mina's closing duty. Glen's
            // matching shift creates the same shared-route interaction without scenario injection.
            host.Employments.MaterializeCommitments(context, minaEmployment, SimDuration.FromDays(2));
            host.Employments.MaterializeCommitments(context, glenEmployment, SimDuration.FromDays(2));
            host.Employments.MaterializeCommitments(context, priyaEmployment, SimDuration.FromDays(2));
            host.Employments.MaterializeCommitments(context, marcusEmployment, SimDuration.FromDays(2));
            host.Employments.MaterializeCommitments(context, joEmployment, SimDuration.FromDays(2));
            host.Employments.MaterializeCommitments(context, lenaEmployment, SimDuration.FromDays(2));

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
                MinimumPlayableContent.CommitmentDinnerWithGlen,
                dinnerStart,
                dinnerStart.Plus(SimDuration.FromMinutes(2)),
                SimDuration.FromMinutes(90),
                layout.Cafe,
                70,
                MinimumPlayableContent.ActivityDining,
                new[] { layout.Glen },
                accountabilityPolicy: host.Catalog.CommitmentAccountabilityPolicies[MinimumPlayableContent.AccountabilitySocialCommitment]));

            return layout;
        }

        /// <summary>Installs scenario content reactions for both new and restored MPS hosts.</summary>
        public static void ConfigureScenarioServices(SimulationHost host)
        {
            var workPressure = new WorkContextPressureService(
                host.Transitions,
                host.DecisionReevaluation,
                MinimumPlayableContent.ActivityWorking,
                MinimumPlayableContent.ModifierDislikedColleague,
                MinimumPlayableContent.ContextWorkPressure,
                affinityThreshold: -1000,
                pressuredRate: -2);
            host.DomainEventHandlers.Register(new WorkContextArrivalHandler(workPressure), 200);
            host.DomainEventHandlers.Register(new WorkContextDepartureHandler(workPressure), 100);
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

        private static void SeedSocialTopology(WorldState world, MinimumPlayableWorldLayout layout)
        {
            SimTime now = world.Clock.Now;

            Relationship friends = AddRelationship(
                world, layout.Mina, layout.Glen, "relationship.friend", 3200);
            friends.From(layout.Mina).SetChannel(
                RelationshipChannels.TrustMotives, AnalyticalProgression.Constant(4200, now));
            friends.From(layout.Mina).RecordExposure(now, 480, 6500);
            friends.From(layout.Mina).AddMemory(new RelationshipMemory(
                new AuthoredId("relationship.memory.glen_showed_up"),
                now.Plus(SimDuration.FromDays(-30)),
                new AuthoredId("relationship.explanation.glen_showed_up"),
                new SortedDictionary<AuthoredId, long>
                {
                    [RelationshipChannels.Affection] = 1200,
                    [RelationshipChannels.TrustMotives] = 900,
                }));
            friends.From(layout.Glen).SetChannel(
                RelationshipChannels.Affection, AnalyticalProgression.Constant(2100, now));
            friends.From(layout.Glen).SetChannel(
                RelationshipChannels.Resentment, AnalyticalProgression.Constant(700, now));
            friends.From(layout.Glen).RecordExposure(now, 360, 5200);
            friends.From(layout.Glen).AddMemory(new RelationshipMemory(
                new AuthoredId("relationship.memory.mina_missed_supper"),
                now.Plus(SimDuration.FromDays(-12)),
                new AuthoredId("relationship.explanation.mina_missed_supper"),
                new SortedDictionary<AuthoredId, long>
                {
                    [RelationshipChannels.Affection] = -500,
                    [RelationshipChannels.Resentment] = 700,
                }));

            Relationship boss = AddRelationship(
                world, layout.Mina, layout.Darius, "relationship.disliked_boss", -5000);
            boss.From(layout.Mina).SetChannel(
                RelationshipChannels.Respect, AnalyticalProgression.Constant(2400, now));
            boss.From(layout.Mina).SetChannel(
                RelationshipChannels.Resentment, AnalyticalProgression.Constant(4200, now));
            boss.From(layout.Mina).RecordExposure(now, 90, 2100);
            boss.From(layout.Darius).SetChannel(
                RelationshipChannels.Affection, AnalyticalProgression.Constant(-600, now));
            boss.From(layout.Darius).SetChannel(
                RelationshipChannels.Respect, AnalyticalProgression.Constant(1700, now));
            boss.From(layout.Darius).RecordExposure(now, 35, 850);

            Relationship acquaintances = AddRelationship(
                world, layout.Owen, layout.Lena, "relationship.acquaintance", 200);
            acquaintances.From(layout.Owen).SetChannel(
                RelationshipChannels.Affection, AnalyticalProgression.Constant(500, now));
            acquaintances.From(layout.Owen).RecordExposure(now, 12, 600);
            acquaintances.From(layout.Lena).SetChannel(
                RelationshipChannels.Affection, AnalyticalProgression.Constant(-200, now));
            acquaintances.From(layout.Lena).RecordExposure(now, 5, 250);

            SetBelief(world, layout.Mina, layout.Glen, 5200, 3100, 8000000);
            SetBelief(world, layout.Glen, layout.Mina, 2600, 900, 12000000);
            SetBelief(world, layout.Mina, layout.Darius, -2800, 600, 45000000);

            // Owen's sparse first impression is confidently rosy enough to be wrong, but uncertain
            // enough that ordinary shared-context evidence can still revise it materially.
            SetBelief(world, layout.Owen, layout.Lena, 9000, 8500, 65000000);
        }

        private static Relationship AddRelationship(
            WorldState world,
            CharacterId first,
            CharacterId second,
            string kind,
            long initialAffection)
        {
            var relationship = new Relationship(
                world.RuntimeIds.Relationships.Next(),
                first,
                second,
                new AuthoredId(kind),
                AnalyticalProgression.Constant(initialAffection, world.Clock.Now),
                world.Clock.Now.Plus(SimDuration.FromDays(-30)));
            world.Relationships.Add(relationship.Id, relationship);
            world.RelationshipIndex.Register(relationship);
            return relationship;
        }

        private static void SetBelief(
            WorldState world,
            CharacterId observer,
            CharacterId target,
            long warmth,
            long sociability,
            long variance)
        {
            BeliefDistribution belief = SocialBeliefUpdateService.BroadPrior();
            belief.Mean.Set(SocialDimensions.Warmth, warmth);
            belief.Mean.Set(SocialDimensions.Sociability, sociability);
            belief.SetCovariance(SocialDimensions.Warmth, SocialDimensions.Warmth, variance);
            belief.SetCovariance(SocialDimensions.Sociability, SocialDimensions.Sociability, variance);
            world.Knowledge.SetSocialBelief(
                ObserverRef.Character(observer), target, belief, world.Clock.Now);
        }

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
            long initialHunger = 2000,
            long initialEnergy = 9000,
            GroupId householdId = default)
        {
            WorldState world = host.World;

            var character = new Character(world.RuntimeIds.Characters.Next(), name, world.Clock.Now);
            for (int i = 0; i < traits.Length; i++)
            {
                character.AddTrait(traits[i]);
            }

            world.Characters.Add(character.Id, character);
            new SocialProfileGenerator(host.Simulation.Random).Generate(character, MinimumPlayableContent.SocialCalibrationStandard);

            // Needs progress analytically and arm their own threshold events (§10.1, §10.2).
            NeedDefinition hunger = host.Catalog.Needs[MinimumPlayableContent.NeedHunger];
            var hungerState = new NeedState(
                hunger.Id,
                AnalyticalProgression.Linear(initialHunger, world.Clock.Now, hunger.DefaultRateNumerator, hunger.DefaultRateDenominator, hunger.MinValue, hunger.MaxValue),
                hunger.BehaviouralThresholds[0]);

            character.SetNeed(hungerState);
            host.Needs.Rearm(host.Simulation, character, hungerState);

            NeedDefinition energy = host.Catalog.Needs[WellKnownNeeds.Energy];
            var energyState = new NeedState(
                energy.Id,
                AnalyticalProgression.Linear(initialEnergy, world.Clock.Now, energy.DefaultRateNumerator, energy.DefaultRateDenominator, energy.MinValue, energy.MaxValue),
                energy.RestRoutine.ActivationThreshold);
            character.SetNeed(energyState);
            host.Needs.Rearm(host.Simulation, character, energyState);

            if (!householdId.IsSet)
                householdId = AddHousehold(world, name + " household", startingLocation);
            world.Memberships.Join(householdId, character.Id);

            // Every active character has exactly one primary Activity from the moment they exist (§29.1).
            host.Transitions.BeginActivity(
                host.Simulation,
                character.Id,
                WellKnownActivities.Waiting,
                startingLocation,
                SimDuration.FromHours(1));

            return character.Id;
        }

        private static GroupId AddHousehold(WorldState world, string name, LocationId home)
        {
            var household = new Group(
                world.RuntimeIds.Groups.Next(),
                GroupKinds.Household,
                name,
                home);
            world.Groups.Add(household.Id, household);
            return household.Id;
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
            character.Interests.Set(MinimumPlayableContent.InterestTabletopGames, tabletopInterest);
            character.Interests.Set(MinimumPlayableContent.InterestReading, readingInterest);
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
            character.Interests.Set(MinimumPlayableContent.InterestSocializing, 7000);
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
