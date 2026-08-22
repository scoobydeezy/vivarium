using System.Collections.Generic;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Spatial;
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
            layout.Home = AddLocation(world, layout.Town, SampleContent.LocationKindBuilding, "Mina's flat");
            layout.Bakery = AddLocation(world, layout.Town, SampleContent.LocationKindBuilding, "East Market Bakery");
            layout.Cafe = AddLocation(world, layout.Town, SampleContent.LocationKindBuilding, "Corner cafe");

            // --- travel topology, separate from containment (§28) ---
            world.TravelNetwork.ConnectBidirectional(layout.Home, layout.Bakery, SimDuration.FromMinutes(12), SampleContent.TravelModeWalking);
            world.TravelNetwork.ConnectBidirectional(layout.Home, layout.Cafe, SimDuration.FromMinutes(5), SampleContent.TravelModeWalking);
            world.TravelNetwork.ConnectBidirectional(layout.Cafe, layout.Bakery, SimDuration.FromMinutes(9), SampleContent.TravelModeWalking);

            // --- characters ---
            layout.Mina = AddCharacter(host, "Mina Cairn", layout.Home, new[] { SampleContent.TraitAmbitious, SampleContent.TraitEnjoysBaking });
            layout.Glen = AddCharacter(host, "Glen Ashby", layout.Cafe, new[] { SampleContent.TraitHomebound });
            layout.Darius = AddCharacter(host, "Darius Vale", layout.Bakery, new[] { SampleContent.TraitAmbitious });

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

            // --- one recurring routine, materialized across a bounded horizon (§29.4) ---
            CommitmentTemplate shift = SampleContent.BakeryShiftTemplate(layout.Bakery);
            IReadOnlyList<Commitment> commitments = host.Planner.MaterializeCommitments(
                context,
                layout.Mina,
                new[] { shift },
                SimDuration.FromDays(2));

            for (int i = 0; i < commitments.Count; i++)
            {
                host.Planner.TryPlanCommitmentStart(context, commitments[i]);
            }

            return layout;
        }

        /// <summary>
        /// Creates a Decision with a true influence set, some of it hidden from the player (§17, §26).
        /// </summary>
        public static Decision CreateJobOfferDecision(SimulationHost host, CharacterId character, LocationId opportunityLocation)
        {
            WorldState world = host.World;
            DecisionDefinition definition = host.Catalog.Decisions[SampleContent.DecisionJobOffer];

            var decision = new Decision(
                world.RuntimeIds.Decisions.Next(),
                character,
                definition.Id,
                world.Clock.Now,
                world.Clock.Now.Plus(definition.TimeToResolve),
                definition.Options,
                new DecisionConflictScope(SampleContent.ConflictScopeEmployment, character.ToRef()),
                definition.Importance);

            // Definition-derived values are snapshotted now, so a later content reload cannot rewrite
            // this open decision underneath the player (§42.1).
            decision.SnapshotParameter(new AuthoredId("decision.param.time_to_resolve_minutes"), definition.TimeToResolve.TotalMinutes);

            // Four reasons to accept, three to stay — the player will not be shown all of them.
            decision.AddInfluence(SampleContent.OptionAccept, SampleContent.CategoryPersonalConcern, SampleContent.TraitAmbitious, Die.D10, InfluenceVisibility.Full, default, character.ToRef());
            decision.AddInfluence(SampleContent.OptionAccept, SampleContent.CategoryPersonalConcern, SampleContent.TraitEnjoysBaking, Die.D8, InfluenceVisibility.Existence | InfluenceVisibility.Category | InfluenceVisibility.Magnitude, default, character.ToRef());
            decision.AddInfluence(SampleContent.OptionAccept, SampleContent.CategoryPractical, new AuthoredId("influence.better_pay"), Die.D6, InfluenceVisibility.Full);
            decision.AddInfluence(
                SampleContent.OptionAccept,
                SampleContent.CategoryPractical,
                new AuthoredId("influence.good_location"),
                Die.D6,
                InfluenceVisibility.Full,
                new DecisionDependencyKey(SampleContent.ContextHousingMarket, opportunityLocation.ToRef()));

            decision.AddInfluence(SampleContent.OptionStay, SampleContent.CategorySocial, new AuthoredId("influence.family_routine"), Die.D8, InfluenceVisibility.Full);
            decision.AddInfluence(SampleContent.OptionStay, SampleContent.CategorySocial, new AuthoredId("influence.friendship"), Die.D6, InfluenceVisibility.Existence | InfluenceVisibility.Magnitude);

            // Entirely hidden: the player is not even told that a third reason to stay exists (§26).
            decision.AddInfluence(SampleContent.OptionStay, SampleContent.CategoryPractical, new AuthoredId("influence.commute"), Die.D6, InfluenceVisibility.Hidden);

            world.Decisions.Add(decision.Id, decision);
            world.DecisionDependencies.Register(decision);

            ScheduledEvent scheduled = world.Scheduler.Schedule(
                decision.ResolveAt,
                SchedulePhase.Decision,
                ScheduledEventTypes.DecisionResolve,
                new DecisionResolvePayload(decision.Id, character));

            decision.SetPendingResolveEvent(scheduled.Id);
            world.Publish(new DecisionCreatedEvent(decision.Id, character, definition.Id));

            return decision;
        }

        private static LocationId AddLocation(WorldState world, LocationId parent, AuthoredId kind, string name)
        {
            var node = new LocationNode(world.RuntimeIds.Locations.Next(), parent, kind, name);
            world.Locations.Add(node);
            return node.Id;
        }

        private static CharacterId AddCharacter(SimulationHost host, string name, LocationId startingLocation, AuthoredId[] traits)
        {
            WorldState world = host.World;

            var character = new Character(world.RuntimeIds.Characters.Next(), name, world.Clock.Now);
            for (int i = 0; i < traits.Length; i++)
            {
                character.AddTrait(traits[i]);
            }

            world.Characters.Add(character.Id, character);

            // Needs progress analytically and arm their own threshold events (§10.1, §10.2).
            NeedDefinition hunger = host.Catalog.Needs[SampleContent.NeedHunger];
            var hungerState = new NeedState(
                hunger.Id,
                AnalyticalProgression.Linear(2000, world.Clock.Now, hunger.DefaultRateNumerator, hunger.DefaultRateDenominator, hunger.MinValue, hunger.MaxValue),
                hunger.BehaviouralThresholds[0]);

            character.SetNeed(hungerState);
            host.Needs.Rearm(host.Simulation, character, hungerState);

            // Every active character has exactly one primary Activity from the moment they exist (§29.1).
            host.Transitions.BeginActivity(
                host.Simulation,
                character.Id,
                WellKnownActivities.Waiting,
                startingLocation,
                SimDuration.FromHours(1));

            return character.Id;
        }
    }
}
