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
using Vivarium.Domain.Employment;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Social;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Time;
using Vivarium.Unity.Authoring;
using Vivarium.Unity.Bootstrap;
using Vivarium.Unity.Presentation;

namespace Vivarium.Unity.Tests
{
    public sealed class VivariumPlayModeTests
    {
        private static readonly AuthoredId DemoDecisionId = new AuthoredId("decision.leave_work_early");
        private static readonly AuthoredId CommitmentConflictDecisionId = new AuthoredId("decision.commitment_conflict");
        private static readonly AuthoredId HungerNeedId = new AuthoredId("need.hunger");
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

        [Test]
        public void Authored_decision_reasoning_converts_and_passes_lint()
        {
            var authored = new DecisionReasoningProgramEntry
            {
                bindings = new[]
                {
                    new CompiledConsiderationBindingEntry
                    {
                        bindingId = "binding.urgency",
                        considerationId = "consideration.urgency",
                        definitionVersion = 1,
                        signals = new[]
                        {
                            new DecisionSignalRequestEntry
                            {
                                signalId = "decision.parameter.urgency",
                                providerId = "decision.signal_provider.context",
                            },
                        },
                        field = new SignalFieldEntry
                        {
                            authoredId = "field.urgency",
                            linearTerms = new[]
                            {
                                new SignalLinearTermEntry
                                {
                                    signalId = "decision.parameter.urgency",
                                    coefficient = 10000,
                                    provenanceId = "reason.urgency",
                                },
                            },
                        },
                        reasonChannelId = "channel.urgency",
                        scaleId = "scale.urgency",
                        scaleThresholds = new[]
                        {
                            new ReasonDieThresholdEntry { minimumMagnitude = 1000, dieSides = 4 },
                        },
                        categoryId = "category.urgency",
                        positiveLabelId = "reason.urgent",
                        negativeLabelId = "reason.not_urgent",
                        visibility = InfluenceVisibility.Full,
                    },
                },
            };
            DecisionReasoningProgram program = authored.ToDefinition();
            var options = new[]
            {
                new DecisionOption(new AuthoredId("option.wait"), new AuthoredId("label.wait"), 0),
            };

            IReadOnlyList<string> errors = DecisionReasoningProgramValidator.Validate(
                program, options, DecisionSignalProviderIds.BuiltIns);

            Assert.That(errors, Is.Empty);
            Assert.That(program.Bindings[0].Field.LinearTerms[0].Coefficient, Is.EqualTo(10000));
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
            Assert.That(first.KnownNeeds.Count, Is.EqualTo(character.Needs.Count));
            long firstHunger = long.Parse(KnownNeed(first, HungerNeedId).ValueLabel);

            _bootstrapper.Host.Session.Advance(SimDuration.FromMinutes(60));
            _bootstrapper.Host.Session.Execute(new InspectCharacterCommand(id, false));
            _bootstrapper.Host.Session.Execute(new InspectCharacterCommand(id, true));

            Assert.That(projector.TryProject(_bootstrapper.Host.World, id, out CharacterProfileView refreshed), Is.True);
            long refreshedHunger = long.Parse(KnownNeed(refreshed, HungerNeedId).ValueLabel);
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
            TravelConnection connection = LongestConnectionFrom(initial.LocationId);
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
        public IEnumerator Authored_need_crossing_generates_a_projectable_decision()
        {
            AdvanceToDemoDecision();
            Decision decision = DemoDecision();
            DecisionDefinition definition = _bootstrapper.Host.Catalog.Decisions[decision.DefinitionId];
            var projector = new DecisionProjector(_bootstrapper.Host.Catalog.Interventions);
            DecisionView view = projector.Project(_bootstrapper.Host.World, decision);

            Assert.That(decision.DefinitionId, Is.EqualTo(new AuthoredId("decision.leave_work_early")));
            Assert.That(definition.Trigger, Is.Not.Null);
            Assert.That(definition.Trigger.NeedId, Is.EqualTo(new AuthoredId("need.hunger")));
            Assert.That(definition.DependencyTemplates, Is.Empty);
            Assert.That(definition.InfluenceTemplates, Is.Empty);
            Assert.That(definition.ReasoningProgram, Is.Not.Null);
            Assert.That(definition.ReasoningProgram.Bindings.Count, Is.EqualTo(3));
            Assert.That(decision.ReasoningProgram, Is.Not.Null);
            Assert.That(decision.TryGetContextParameter(
                DecisionReasoningParameters.Urgency,
                out DecisionParameterValue urgency), Is.True);
            Assert.That(urgency.Integer, Is.EqualTo(6000));
            Assert.That(definition.ActivityOutcomes.Count, Is.EqualTo(1));
            Assert.That(view.Options.Count, Is.EqualTo(2));
            Assert.That(view.Options[0].Influences.Count, Is.GreaterThan(0));
            Assert.That(view.Options[1].Influences.Count, Is.GreaterThan(0));
            DecisionInfluence workContext = InfluenceWithLabel(
                decision, new AuthoredId("Difficult work context"));
            Assert.That(workContext.CurrentDie, Is.EqualTo(Die.D10));
            Assert.That(workContext.ReasonChannelId,
                Is.EqualTo(new AuthoredId("reason_channel.work_context")));
            Assert.That(workContext.Evaluation.Signals[0].Mean, Is.EqualTo(10000));
            Assert.That(view.Resolution, Is.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Decision_can_be_held_released_and_intervened_on()
        {
            AdvanceToDemoDecision();
            Decision decision = DemoDecision();
            DecisionInfluence influence = FirstIntervenableInfluence(decision);
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

        [UnityTest]
        public IEnumerator Decision_panel_feed_refreshes_after_intervention_at_quiescence()
        {
            AdvanceToDemoDecision();
            DecisionPanel panel = Object.FindAnyObjectByType<DecisionPanel>();
            Decision decision = DemoDecision();
            DecisionInfluence influence = FirstIntervenableInfluence(decision);
            Assert.That(panel.DisplayedText, Does.Contain("Recent events"));
            Assert.That(panel.DisplayedText, Does.Contain("faces decision.leave_work_early"));

            Result result = _bootstrapper.Host.Session.Execute(new ApplyDecisionInterventionCommand(
                decision.Id,
                new AuthoredId("intervention.encourage"),
                influence.Id));
            Assert.That(result.IsSuccess, Is.True);
            yield return null;

            Assert.That(panel.DisplayedText, Does.Contain("You influenced Mina Test"));
        }

        [UnityTest]
        public IEnumerator Demo_progresses_through_shared_travel_work_pressure_and_need_decision()
        {
            Character mina = CharacterNamed("Mina Test");
            Character glen = CharacterNamed("Glen Test");
            Assert.That(TryFindDecision(DemoDecisionId, out Decision _), Is.False);

            _bootstrapper.Host.Session.Advance(SimDuration.FromMinutes(2));
            Assert.That(_bootstrapper.Host.World.TryGetSpatialContext(mina.Id, out ActivitySpatialContext minaTravel), Is.True);
            Assert.That(_bootstrapper.Host.World.TryGetSpatialContext(glen.Id, out ActivitySpatialContext glenTravel), Is.True);
            Assert.That(minaTravel.IsTraveling, Is.True);
            Assert.That(glenTravel.IsTraveling, Is.True);
            Assert.That(_bootstrapper.Host.World.RelationshipIndex.TryGetBetween(mina.Id, glen.Id, out RelationshipId sharedTravelRelationship), Is.True);
            Assert.That(_bootstrapper.Host.World.Relationships.Get(sharedTravelRelationship).LastInteractionAt.HasValue, Is.True);

            _bootstrapper.Host.Session.Advance(SimDuration.FromMinutes(30));
            ActivityInstance work = _bootstrapper.Host.World.Activities.Get(mina.CurrentActivityId);
            Assert.That(work.DefinitionId, Is.EqualTo(new AuthoredId("activity.working")));
            Assert.That(work.HasModifier(new AuthoredId("activity_modifier.disliked_colleague_present")), Is.True);
            Assert.That(TryFindDecision(DemoDecisionId, out Decision _), Is.False);

            _bootstrapper.Host.Session.Advance(SimDuration.FromMinutes(2));
            Assert.That(DemoDecision().DefinitionId, Is.EqualTo(DemoDecisionId));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Golden_scenario_surfaces_concrete_commitment_conflict_after_leave_work()
        {
            Assert.That(_bootstrapper.Host.Catalog.Decisions.ContainsKey(CommitmentConflictDecisionId), Is.True);
            Assert.That(_bootstrapper.Host.Catalog.CommitmentAccountabilityPolicies.ContainsKey(
                new AuthoredId("accountability.social_commitment")), Is.True);
            Assert.That(_bootstrapper.Host.Catalog.SocialEvidence.ContainsKey(
                new AuthoredId("social.action.commitment_breach")), Is.True);
            Assert.That(_bootstrapper.Host.Catalog.EmploymentDefinitions.ContainsKey(
                new AuthoredId("employment.bakery_worker")), Is.True);

            Character mina = CharacterNamed("Mina Test");
            Employment employment = null;
            foreach (Employment candidate in _bootstrapper.Host.World.Employments.All)
                if (candidate.EmployeeId == mina.Id) employment = candidate;
            Assert.That(employment, Is.Not.Null);
            Assert.That(employment.SupervisorId, Is.EqualTo(CharacterNamed("Darius Test").Id));

            Commitment closing = null;
            foreach (Commitment candidate in _bootstrapper.Host.World.Commitments.All)
                if (candidate.CharacterId == mina.Id && candidate.Kind == new AuthoredId("commitment.help_darius_close_bakery"))
                    closing = candidate;
            Assert.That(closing, Is.Not.Null);
            Assert.That(closing.Source, Is.EqualTo(employment.Id.ToRef()));
            Assert.That(closing.Stakeholders[0].Role, Is.EqualTo(StakeholderRole.Authority));
            Assert.That(TryFindDecision(CommitmentConflictDecisionId, out Decision _), Is.False);

            _bootstrapper.Host.Session.Advance(SimDuration.FromHours(1));
            Assert.That(TryFindDecision(CommitmentConflictDecisionId, out Decision conflict), Is.True);
            Assert.That(conflict.IsActive, Is.True);
            Assert.That(conflict.CommitmentConflictKey, Is.Not.Null);
            Assert.That(conflict.CommitmentConflictKey.ParticipatingCommitmentIds.Count, Is.EqualTo(2));

            DecisionView view = new DecisionProjector(_bootstrapper.Host.Catalog.Interventions)
                .Project(_bootstrapper.Host.World, conflict);
            Assert.That(view.HasHardDeadline, Is.True);
            DecisionOptionView keepDinner = null;
            for (int i = 0; i < view.Options.Count; i++)
                if (view.Options[i].IntentSummary.Contains("Keep Dinner With Glen")) keepDinner = view.Options[i];
            Assert.That(keepDinner, Is.Not.Null);
            Assert.That(keepDinner.IntentSummary, Does.Contain("give up Help Darius Close Bakery"));

            yield return null;
            DecisionPanel panel = Object.FindAnyObjectByType<DecisionPanel>();
            Assert.That(panel.DisplayedText, Does.Contain("hard deadline"));
            Assert.That(panel.DisplayedText, Does.Contain("Dinner With Glen"));

            _bootstrapper.Host.Session.Advance(conflict.ResolveAt - _bootstrapper.Host.World.Clock.Now);
            Assert.That(conflict.Status, Is.EqualTo(DecisionStatus.Resolved));
            Commitment relinquished = null;
            foreach (Commitment commitment in _bootstrapper.Host.World.Commitments.All)
                if (commitment.Status == CommitmentStatus.Relinquished)
                {
                    relinquished = commitment;
                    break;
                }
            Assert.That(relinquished, Is.Not.Null);
            Assert.That(relinquished.AccountabilityPolicy.Id,
                Is.EqualTo(new AuthoredId("accountability.social_commitment")));
            StakeholderRef stakeholder = relinquished.Stakeholders[0];
            var observer = new CharacterId(stakeholder.Entity.RuntimeId);
            Assert.That(_bootstrapper.Host.World.Knowledge.TryGetSocialBelief(
                ObserverRef.Character(observer), relinquished.CharacterId, out BeliefDistribution _), Is.True);
            Assert.That(_bootstrapper.Host.World.RelationshipIndex.TryGetBetween(
                observer, relinquished.CharacterId, out RelationshipId accountabilityRelationship), Is.True);
            Assert.That(_bootstrapper.Host.World.Relationships.Get(accountabilityRelationship)
                .From(observer).Memories.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Ordinary_hunger_travels_to_authored_eating_affordance_and_replans()
        {
            var eatingId = WellKnownActivities.Eating;
            Assert.That(_bootstrapper.Host.Catalog.Activities.ContainsKey(eatingId), Is.True);
            NeedDefinition hungerDefinition = _bootstrapper.Host.Catalog.Needs[HungerNeedId];
            Assert.That(hungerDefinition.SatisfactionRoutine, Is.Not.Null);

            LocationNode room = null;
            LocationNode workshop = null;
            foreach (LocationNode location in _bootstrapper.Host.World.Locations.Nodes.All)
            {
                if (location.DisplayName == "Demo Room") room = location;
                if (location.DisplayName == "Demo Workshop") workshop = location;
            }
            Assert.That(room, Is.Not.Null);
            Assert.That(workshop, Is.Not.Null);
            Assert.That(room.Affords(eatingId), Is.True);
            Assert.That(workshop.Affords(eatingId), Is.False);

            var priya = new Character(
                _bootstrapper.Host.World.RuntimeIds.Characters.Next(),
                "Priya Eating Test",
                _bootstrapper.Host.World.Clock.Now);
            _bootstrapper.Host.World.Characters.Add(priya.Id, priya);
            var hunger = new NeedState(
                HungerNeedId,
                AnalyticalProgression.Linear(
                    5990,
                    _bootstrapper.Host.World.Clock.Now,
                    hungerDefinition.DefaultRateNumerator,
                    hungerDefinition.DefaultRateDenominator,
                    hungerDefinition.MinValue,
                    hungerDefinition.MaxValue),
                hungerDefinition.SatisfactionRoutine.ActivationThreshold);
            priya.SetNeed(hunger);
            _bootstrapper.Host.Needs.Rearm(_bootstrapper.Host.Simulation, priya, hunger);
            _bootstrapper.Host.Transitions.BeginActivity(
                _bootstrapper.Host.Simulation,
                priya.Id,
                WellKnownActivities.Waiting,
                workshop.Id,
                SimDuration.FromHours(1));
            _bootstrapper.Host.Session.Advance(SimDuration.Zero);

            _bootstrapper.Host.Session.Advance(SimDuration.FromMinutes(1));
            ActivityInstance travel = _bootstrapper.Host.World.Activities.Get(priya.CurrentActivityId);
            Assert.That(travel.DefinitionId, Is.EqualTo(WellKnownActivities.Traveling));
            Assert.That(travel.SpatialContext.Transit.DestinationLocationId, Is.EqualTo(room.Id));

            _bootstrapper.Host.Session.Advance(SimDuration.FromMinutes(60));
            ActivityInstance replanned = _bootstrapper.Host.World.Activities.Get(priya.CurrentActivityId);
            Assert.That(replanned.DefinitionId, Is.EqualTo(WellKnownActivities.Waiting));
            Assert.That(replanned.SpatialContext.LocationId, Is.EqualTo(room.Id));
            Assert.That(priya.TryGetNeed(HungerNeedId, out NeedState satisfied), Is.True);
            Assert.That(satisfied.ValueAt(_bootstrapper.Host.World.Clock.Now),
                Is.LessThan(hungerDefinition.SatisfactionRoutine.ActivationThreshold));
            foreach (Decision decision in _bootstrapper.Host.World.Decisions.All)
                Assert.That(decision.CharacterId == priya.Id && decision.DefinitionId == DemoDecisionId, Is.False);

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

        private Character CharacterNamed(string displayName)
        {
            foreach (Character character in _bootstrapper.Host.World.Characters.All)
            {
                if (character.DisplayName == displayName)
                {
                    return character;
                }
            }

            Assert.Fail($"The demo world did not seed '{displayName}'.");
            return null;
        }

        private void AdvanceToDemoDecision()
        {
            if (!TryFindDecision(DemoDecisionId, out Decision _))
            {
                _bootstrapper.Host.Session.Advance(SimDuration.FromMinutes(34));
            }
        }

        private DecisionInfluence FirstIntervenableInfluence(Decision decision)
        {
            InterventionDefinition intervention =
                _bootstrapper.Host.Catalog.Interventions[new AuthoredId("intervention.encourage")];
            for (int i = 0; i < decision.Influences.Count; i++)
            {
                DecisionInfluence influence = decision.Influences[i];
                if (DecisionInterventionRules.Evaluate(decision, intervention, influence.Id).IsSuccess)
                {
                    return influence;
                }
            }

            Assert.Fail("The authored demo Decision has no influence eligible for encouragement.");
            return null;
        }

        private static DecisionInfluence InfluenceWithLabel(Decision decision, AuthoredId label)
        {
            for (int i = 0; i < decision.Influences.Count; i++)
            {
                if (!decision.Influences[i].IsRetracted && decision.Influences[i].LabelId == label)
                {
                    return decision.Influences[i];
                }
            }

            Assert.Fail($"The Decision has no active influence labelled '{label}'.");
            return null;
        }

        private TravelConnection LongestConnectionFrom(LocationId locationId)
        {
            IReadOnlyList<TravelConnection> connections =
                _bootstrapper.Host.World.TravelNetwork.ConnectionsFrom(locationId);
            Assert.That(connections, Is.Not.Empty);

            TravelConnection longest = connections[0];
            for (int i = 1; i < connections.Count; i++)
            {
                if (connections[i].Cost > longest.Cost)
                {
                    longest = connections[i];
                }
            }

            return longest;
        }

        private static KnownFactView KnownNeed(CharacterProfileView profile, AuthoredId needId)
        {
            for (int i = 0; i < profile.KnownNeeds.Count; i++)
            {
                if (profile.KnownNeeds[i].Label == needId.Value)
                {
                    return profile.KnownNeeds[i];
                }
            }

            Assert.Fail($"The projected profile has no known Need labelled '{needId}'.");
            return null;
        }

        private Decision DemoDecision()
        {
            if (TryFindDecision(DemoDecisionId, out Decision decision))
            {
                return decision;
            }

            Assert.Fail("The demo world did not generate the authored Need Decision.");
            return null;
        }

        private bool TryFindDecision(AuthoredId definitionId, out Decision found)
        {
            foreach (Decision decision in _bootstrapper.Host.World.Decisions.All)
            {
                if (decision.DefinitionId == definitionId)
                {
                    found = decision;
                    return true;
                }
            }

            found = null;
            return false;
        }
    }
}
