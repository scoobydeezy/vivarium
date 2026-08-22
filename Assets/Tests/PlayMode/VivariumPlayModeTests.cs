using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Vivarium.Application.Commands;
using Vivarium.Application.Queries;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Time;
using Vivarium.Unity.Bootstrap;
using Vivarium.Unity.Presentation;

namespace Vivarium.Unity.Tests
{
    public sealed class VivariumPlayModeTests
    {
        private GameBootstrapper _bootstrapper;
        private WorldPresenter _presenter;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("TestScene", LoadSceneMode.Single);
            yield return null;

            _bootstrapper = Object.FindAnyObjectByType<GameBootstrapper>();
            _presenter = Object.FindAnyObjectByType<WorldPresenter>();

            Assert.That(_bootstrapper, Is.Not.Null);
            Assert.That(_presenter, Is.Not.Null);
            Assert.That(_bootstrapper.Host, Is.Not.Null);

            _bootstrapper.SetSpeedMultiplier(0f);
        }

        [UnityTest]
        public IEnumerator Paused_clock_stays_still_and_direct_advance_is_exact()
        {
            long before = _bootstrapper.Host.World.Clock.Now.TotalMinutes;
            yield return null;
            yield return null;
            Assert.That(_bootstrapper.Host.World.Clock.Now.TotalMinutes, Is.EqualTo(before));

            _bootstrapper.Host.Session.Advance(SimDuration.FromMinutes(60));
            Assert.That(_bootstrapper.Host.World.Clock.Now.TotalMinutes, Is.EqualTo(before + 60));
        }

        [UnityTest]
        public IEnumerator Demo_world_projects_three_character_views()
        {
            yield return null;
            Assert.That(_bootstrapper.Host.World.Characters.Count, Is.EqualTo(3));
            Assert.That(_presenter.ActiveViewCount, Is.EqualTo(3));
            Assert.That(_presenter.PooledViewCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator Reinspection_refreshes_analytical_hunger_knowledge()
        {
            Character character = FirstCharacter();
            CharacterId id = character.Id;
            var projector = new CharacterProfileProjector();

            Result opened = _bootstrapper.Host.Session.Execute(new InspectCharacterCommand(id));
            Assert.That(opened.IsSuccess, Is.True);
            Assert.That(projector.TryProject(_bootstrapper.Host.World, id, out CharacterProfileView first), Is.True);
            Assert.That(first.KnownNeeds.Count, Is.EqualTo(1));
            long firstHunger = long.Parse(first.KnownNeeds[0].ValueLabel);

            _bootstrapper.Host.Session.Advance(SimDuration.FromMinutes(60));
            _bootstrapper.Host.Session.Execute(new InspectCharacterCommand(id, false));
            _bootstrapper.Host.Session.Execute(new InspectCharacterCommand(id, true));

            Assert.That(projector.TryProject(_bootstrapper.Host.World, id, out CharacterProfileView refreshed), Is.True);
            long refreshedHunger = long.Parse(refreshed.KnownNeeds[0].ValueLabel);
            Assert.That(refreshedHunger, Is.GreaterThan(firstHunger));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Travel_command_arrives_at_committed_destination()
        {
            CharacterId characterId = FirstCharacter().Id;
            Assert.That(_bootstrapper.Host.World.TryGetSpatialContext(characterId, out ActivitySpatialContext initial), Is.True);
            TravelConnection connection = _bootstrapper.Host.World.TravelNetwork.ConnectionsFrom(initial.LocationId)[0];

            Result result = _bootstrapper.Host.Session.Execute(
                new TravelCharacterCommand(characterId, connection.To));
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(_bootstrapper.Host.World.TryGetSpatialContext(characterId, out ActivitySpatialContext traveling), Is.True);
            Assert.That(traveling.IsTraveling, Is.True);

            _bootstrapper.Host.Session.Advance(connection.Cost);
            Assert.That(_bootstrapper.Host.World.TryGetSpatialContext(characterId, out ActivitySpatialContext arrived), Is.True);
            Assert.That(arrived.IsLocated, Is.True);
            Assert.That(arrived.LocationId, Is.EqualTo(connection.To));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Save_load_restores_mid_travel_and_pending_arrival()
        {
            CharacterId characterId = FirstCharacter().Id;
            _bootstrapper.Host.World.TryGetSpatialContext(characterId, out ActivitySpatialContext initial);
            TravelConnection connection = _bootstrapper.Host.World.TravelNetwork.ConnectionsFrom(initial.LocationId)[0];
            _bootstrapper.Host.Session.Execute(new TravelCharacterCommand(characterId, connection.To));
            _bootstrapper.Host.Session.Advance(SimDuration.FromMinutes(10));

            long savedMinute = _bootstrapper.Host.World.Clock.Now.TotalMinutes;
            _bootstrapper.SaveRuntimeSmokeTest();
            _bootstrapper.Host.Session.Advance(SimDuration.FromMinutes(30));

            Assert.That(_bootstrapper.LoadRuntimeSmokeTest(), Is.True);
            Assert.That(_bootstrapper.Host.World.Clock.Now.TotalMinutes, Is.EqualTo(savedMinute));
            Assert.That(_bootstrapper.Host.World.TryGetSpatialContext(characterId, out ActivitySpatialContext restored), Is.True);
            Assert.That(restored.IsTraveling, Is.True);

            _bootstrapper.Host.Session.Advance(SimDuration.FromMinutes(20));
            Assert.That(_bootstrapper.Host.World.TryGetSpatialContext(characterId, out ActivitySpatialContext arrived), Is.True);
            Assert.That(arrived.IsLocated, Is.True);
            Assert.That(arrived.LocationId, Is.EqualTo(connection.To));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Unfollow_releases_and_refollow_reuses_a_bound_view()
        {
            CharacterId characterId = FirstCharacter().Id;
            Result hidden = _bootstrapper.Host.Session.Execute(new FollowCharacterCommand(characterId, false));
            Assert.That(hidden.IsSuccess, Is.True);
            yield return null;

            Assert.That(_presenter.ActiveViewCount, Is.EqualTo(2));
            Assert.That(_presenter.PooledViewCount, Is.EqualTo(1));
            Assert.That(_presenter.HasActiveView(characterId), Is.False);

            Result shown = _bootstrapper.Host.Session.Execute(new FollowCharacterCommand(characterId, true));
            Assert.That(shown.IsSuccess, Is.True);
            yield return null;

            Assert.That(_presenter.ActiveViewCount, Is.EqualTo(3));
            Assert.That(_presenter.PooledViewCount, Is.EqualTo(0));
            Assert.That(_presenter.HasActiveView(characterId), Is.True);
        }

        [UnityTest]
        public IEnumerator Demo_decision_projects_visible_options_and_influences()
        {
            Decision decision = FirstDecision();
            var projector = new DecisionProjector(_bootstrapper.Host.Catalog.Interventions);
            DecisionView view = projector.Project(_bootstrapper.Host.World, decision);

            Assert.That(view.Options.Count, Is.EqualTo(2));
            Assert.That(view.Options[0].Influences.Count, Is.GreaterThan(0));
            Assert.That(view.Options[1].Influences.Count, Is.GreaterThan(0));
            Assert.That(view.Resolution, Is.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Decision_can_be_held_released_and_intervened_on()
        {
            Decision decision = FirstDecision();
            DecisionInfluence influence = decision.Influences[0];
            int originalSides = influence.CurrentDie.Sides;

            Result held = _bootstrapper.Host.Session.Execute(new HoldDecisionCommand(decision.Id));
            Assert.That(held.IsSuccess, Is.True);
            Assert.That(_bootstrapper.Host.World.Attention.IsHeld(decision.Id), Is.True);

            Result released = _bootstrapper.Host.Session.Execute(new ReleaseDecisionCommand(decision.Id));
            Assert.That(released.IsSuccess, Is.True);
            Assert.That(_bootstrapper.Host.World.Attention.IsHeld(decision.Id), Is.False);

            Result intervened = _bootstrapper.Host.Session.Execute(
                new ApplyDecisionInterventionCommand(
                    decision.Id,
                    new AuthoredId("intervention.encourage"),
                    influence.Id));
            Assert.That(intervened.IsSuccess, Is.True);
            Assert.That(influence.CurrentDie.Sides, Is.GreaterThan(originalSides));
            yield return null;
        }

        private Character FirstCharacter()
        {
            foreach (Character character in _bootstrapper.Host.World.Characters.All)
            {
                return character;
            }

            Assert.Fail("The demo world did not seed any characters.");
            return null;
        }

        private Decision FirstDecision()
        {
            foreach (Decision decision in _bootstrapper.Host.World.Decisions.All)
            {
                return decision;
            }

            Assert.Fail("The demo world did not seed a decision.");
            return null;
        }
    }
}
