using System.Linq;
using Vivarium.Application.Persistence;
using Vivarium.Application.Queries;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Evaluation;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Social;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Time;
using Vivarium.Infrastructure.Bootstrap;
using Vivarium.Infrastructure.Persistence;
using Xunit;

namespace Vivarium.Application.Tests
{
    public sealed class SocialInvitationDecisionTests
    {
        private static readonly AuthoredId Building = new AuthoredId("location_kind.building");
        private static readonly AuthoredId Reading = new AuthoredId("activity.reading");
        private static readonly AuthoredId ReadingInterest = new AuthoredId("interest.reading");
        private static readonly AuthoredId InvitationDecision = new AuthoredId("decision.social_invitation");
        private static readonly AuthoredId Join = new AuthoredId("option.social_invitation.join");
        private static readonly AuthoredId KeepPlan = new AuthoredId("option.social_invitation.keep_plan");
        private static readonly AuthoredId Pressure = new AuthoredId("social.pressure.invitation");
        private static readonly AuthoredId Calibration = new AuthoredId("social.calibration.standard");
        private static readonly AuthoredId ComfortLabel = new AuthoredId("influence.enjoys_inviter");
        private static readonly AuthoredId ComfortBinding = new AuthoredId("binding.social_invitation.comfort");
        private static readonly AuthoredId AvailabilityBinding = new AuthoredId("binding.social_invitation.shared_context");

        [Fact]
        public void InvitationUsesRecipientKnowledgeAndReevaluatesOnlyItsLiveCompiledReasons()
        {
            Fixture fixture = Create(readingInterest: 8000, warmthBelief: 9000);
            Decision decision = fixture.Invitation();
            DecisionInfluence comfort = FindInfluence(decision, ComfortBinding);
            DecisionInfluence availability = FindInfluence(decision, AvailabilityBinding);
            Assert.Equal(fixture.Recipient, decision.CharacterId);
            Assert.Contains(decision.Influences, i => i.OptionId == KeepPlan);
            Assert.True(comfort.Evaluation.Signals[0].Mean > 0);

            DecisionView hidden = new DecisionProjector().Project(fixture.Host.World, decision);
            Assert.Null(FindInfluence(hidden, comfort.Id).Label);
            fixture.Host.World.Knowledge.Record(new KnowledgeEntry(
                new FactKey(FactKinds.DecisionInfluence, fixture.Requester.ToRef(), comfort.LabelId),
                ObservedValue.Of(comfort.LabelId),
                fixture.Host.World.Clock.Now,
                KnowledgeConfidence.Known,
                DiscoverySource.Channel(DiscoveryChannels.Conversation)));
            Assert.Equal(ComfortLabel.Value,
                FindInfluence(new DecisionProjector().Project(fixture.Host.World, decision), comfort.Id).Label);

            BeliefDistribution negative = Belief(-9000);
            fixture.Host.World.Knowledge.SetSocialBelief(
                ObserverRef.Character(fixture.Recipient), fixture.Requester, negative, fixture.Host.World.Clock.Now);
            fixture.Host.World.Publish(new SocialBeliefChangedEvent(
                ObserverRef.Character(fixture.Recipient), fixture.Requester, negative.EvidenceRevision));
            fixture.Host.Session.Advance(SimDuration.Zero);

            DecisionInfluence changedComfort = FindInfluence(decision, ComfortBinding);
            Assert.Equal(comfort.Id, changedComfort.Id);
            Assert.True(changedComfort.Evaluation.Signals[0].Mean < 0);

            fixture.Host.Transitions.BeginActivity(
                fixture.Host.Simulation,
                fixture.Recipient,
                WellKnownActivities.Waiting,
                fixture.Location,
                SimDuration.FromHours(1));
            fixture.Host.Session.Advance(SimDuration.Zero);

            DecisionInfluence changedAvailability = FindInfluence(decision, AvailabilityBinding);
            Assert.Equal(availability.Id, changedAvailability.Id);
            Assert.Equal(InfluencePolarity.Opposing, changedAvailability.Polarity);
            Assert.True(decision.IsActive);
        }

        [Fact]
        public void AcceptedInvitationChangesBothActivitiesAndFreezesExplanationAcrossReload()
        {
            Fixture fixture = Create(readingInterest: 0, warmthBelief: 9000);
            Decision decision = fixture.Invitation();
            long evaluatedComfort = FindInfluence(decision, ComfortBinding).Evaluation.Signals[0].Mean;

            SaveGameData save = fixture.Host.Session.Save("social-invitation");
            WorldState restoredWorld = fixture.Host.SaveMapper.Restore(save);
            SimulationHost restored = SimulationBootstrapper.CreateFromRestoredWorld(
                restoredWorld,
                BuildCatalog(),
                save.LastCommandSequence);

            fixture.Host.Session.Advance(SimDuration.FromMinutes(10));
            restored.Session.Advance(SimDuration.FromMinutes(10));

            Decision expected = fixture.Host.World.Decisions.Get(decision.Id);
            Decision actual = restored.World.Decisions.Get(decision.Id);
            Assert.Equal(Join, expected.Resolution.ChosenOptionId);
            Assert.Equal(expected.Resolution.ChosenOptionId, actual.Resolution.ChosenOptionId);
            Assert.Equal(WellKnownActivities.Socializing, Current(fixture.Host, fixture.Recipient).DefinitionId);
            Assert.Equal(Current(fixture.Host, fixture.Recipient).DefinitionId,
                Current(restored, fixture.Recipient).DefinitionId);
            Assert.Equal(WellKnownActivities.Socializing, Current(fixture.Host, fixture.Requester).DefinitionId);
            Assert.Contains(fixture.Host.World.HistoryLedger.Entries,
                h => h.Subjects.Contains(decision.Id.ToRef()) && h.Kind.Value == "history.decision_resolved");

            InfluenceRoll frozen = expected.Resolution.Rolls.Single(
                roll => roll.Reason.BindingId == ComfortBinding);
            Assert.Equal(evaluatedComfort, frozen.Reason.Evaluation.Signals[0].Mean);
            Assert.Equal(
                frozen.Reason.Evaluation.Signals[0].Mean,
                actual.Resolution.Rolls.Single(roll => roll.Reason.BindingId == ComfortBinding)
                    .Reason.Evaluation.Signals[0].Mean);
        }

        [Fact]
        public void DeclinedInvitationPreservesTheSnapshottedExistingPlan()
        {
            Fixture fixture = Create(readingInterest: 9000, warmthBelief: -9000);
            Decision decision = fixture.Invitation();
            ActivityInstance plan = Current(fixture.Host, fixture.Recipient);

            fixture.Host.Session.Advance(SimDuration.FromMinutes(10));

            Assert.Equal(KeepPlan, decision.Resolution.ChosenOptionId);
            Assert.Equal(plan.Id, Current(fixture.Host, fixture.Recipient).Id);
            Assert.Equal(ActivityStatus.Active, plan.Status);
        }

        private static Fixture Create(long readingInterest, long warmthBelief)
        {
            DefinitionCatalog catalog = BuildCatalog();
            SimulationHost host = SimulationBootstrapper.CreateNewWorld(
                77191,
                SimTime.FromClockTime(0, 18, 0),
                catalog,
                saveStore: new InMemorySaveGameStore());
            WorldState world = host.World;
            var commons = new LocationNode(
                world.RuntimeIds.Locations.Next(),
                LocationId.None,
                Building,
                "Commons",
                activityAffordances: new[] { WellKnownActivities.Socializing, Reading });
            world.Locations.Add(commons);

            var requester = new Character(world.RuntimeIds.Characters.Next(), "Lena", world.Clock.Now);
            world.Characters.Add(requester.Id, requester);
            NeedDefinition social = catalog.Needs[WellKnownNeeds.Social];
            var need = new NeedState(
                social.Id,
                AnalyticalProgression.Linear(7000, world.Clock.Now, 4, 1, 0, 10000),
                social.SocializingRoutine.ActivationThreshold);
            requester.SetNeed(need);
            host.Needs.Rearm(host.Simulation, requester, need);
            host.Transitions.BeginActivity(
                host.Simulation, requester.Id, WellKnownActivities.Waiting, commons.Id, SimDuration.FromHours(1));

            var recipient = new Character(world.RuntimeIds.Characters.Next(), "Glen", world.Clock.Now);
            recipient.Interests.Set(ReadingInterest, readingInterest);
            recipient.SetAppraisalField(new AppraisalField(
                recipient.Id,
                AppraisalLenses.Affiliation,
                0,
                new[] { new SocialLinearTerm(SocialDimensions.Warmth, 10000) },
                null, null, null, null,
                Calibration));
            world.Characters.Add(recipient.Id, recipient);
            world.Knowledge.SetSocialBelief(
                ObserverRef.Character(recipient.Id), requester.Id, Belief(warmthBelief), world.Clock.Now);
            host.Transitions.BeginActivity(
                host.Simulation, recipient.Id, Reading, commons.Id, SimDuration.FromHours(1));

            host.Session.Advance(SimDuration.Zero);
            return new Fixture(host, requester.Id, recipient.Id, commons.Id);
        }

        private static DefinitionCatalog BuildCatalog()
        {
            var builder = new DefinitionCatalog.Builder();
            builder.Add(new ActivityDefinition(WellKnownActivities.Waiting, "Waiting", SimDuration.FromHours(1), false));
            builder.Add(new ActivityDefinition(WellKnownActivities.Traveling, "Traveling", SimDuration.FromMinutes(10), false, false, true));
            builder.Add(new ActivityDefinition(WellKnownActivities.Socializing, "Socializing", SimDuration.FromMinutes(30), false));
            builder.Add(new ActivityDefinition(Reading, "Reading", SimDuration.FromHours(1), false));
            builder.Add(new LocationKindDefinition(Building, "Building"));
            builder.Add(new AppraisalCalibrationProfile(
                Calibration,
                new[]
                {
                    new AppraisalStrengthThreshold(1000, AppraisalStrength.Minor),
                    new AppraisalStrengthThreshold(2500, AppraisalStrength.Moderate),
                    new AppraisalStrengthThreshold(5000, AppraisalStrength.Strong),
                    new AppraisalStrengthThreshold(7500, AppraisalStrength.Extreme),
                },
                1));
            builder.Add(new SocialPressureDefinition(Pressure, new SocialFactorRule[0]));
            builder.Add(new DecisionDefinition(
                InvitationDecision,
                new[]
                {
                    InvitationJoinOption(),
                    InvitationKeepOption(),
                },
                SimDuration.FromMinutes(10),
                new AuthoredId("conflict_scope.social_invitation"),
                reasoningProgram: ReasoningProgram()));
            builder.Add(new NeedDefinition(
                WellKnownNeeds.Social,
                "Social",
                0,
                10000,
                4,
                1,
                new long[] { 7000 },
                socializingRoutine: new SocializingRoutineDefinition(
                    WellKnownActivities.Socializing,
                    7000,
                    -5000,
                    4,
                    new SocialInvitationRoutineDefinition(
                        InvitationDecision,
                        Join,
                        new[] { new SocialInvitationPlanDefinition(Reading, ReadingInterest) }))));
            return builder.Build();
        }

        private static DecisionReasoningProgram ReasoningProgram()
        {
            var comfort = new AuthoredId("decision.signal.social_invitation.comfort");
            var available = new AuthoredId("decision.signal.social_invitation.shared_context");
            var interest = new AuthoredId("decision.signal.social_invitation.plan_interest");
            return new DecisionReasoningProgram(new[]
            {
                new CompiledConsiderationBinding(
                    ComfortBinding,
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
                            literal: DecisionParameterValue.FromAuthoredId(Pressure)),
                    },
                    new[] { new DecisionSignalRequest(comfort, DecisionSignalProviderIds.SocialAppraisal) },
                    new SignalFieldDefinition(
                        new AuthoredId("field.social_invitation.comfort"), 0,
                        new[] { new SignalLinearTerm(comfort, 10000, new AuthoredId("reason.social_comfort")) },
                        null, null, null),
                    new ReasonChannelDefinition(ReasonChannelIds.InterpersonalComfort),
                    ReasonScaleProfile.Standard(),
                    new AuthoredId("cat.social"),
                    ComfortLabel,
                    new AuthoredId("influence.uncomfortable_with_inviter"),
                    InfluenceVisibility.Existence | InfluenceVisibility.Category | InfluenceVisibility.Magnitude),
                new CompiledConsiderationBinding(
                    AvailabilityBinding,
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
                            literal: DecisionParameterValue.FromAuthoredId(WellKnownActivities.Socializing)),
                    },
                    new[] { new DecisionSignalRequest(available, DecisionSignalProviderIds.SharedActivityContext) },
                    new SignalFieldDefinition(
                        new AuthoredId("field.social_invitation.shared_context"), 0,
                        new[] { new SignalLinearTerm(available, 8000, new AuthoredId("reason.shared_context")) },
                        null, null, null),
                    new ReasonChannelDefinition(new AuthoredId("reason_channel.social_invitation.shared_context")),
                    ReasonScaleProfile.Standard(),
                    new AuthoredId("cat.practical"),
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
                    new[] { new DecisionSignalRequest(interest, DecisionSignalProviderIds.ActorInterest) },
                    new SignalFieldDefinition(
                        new AuthoredId("field.social_invitation.existing_plan"), 0,
                        new[] { new SignalLinearTerm(interest, 12000, new AuthoredId("reason.existing_plan")) },
                        null, null, null),
                    new ReasonChannelDefinition(new AuthoredId("reason_channel.social_invitation.existing_plan")),
                    ReasonScaleProfile.Standard(),
                    new AuthoredId("cat.personal"),
                    new AuthoredId("influence.values_existing_plan"),
                    new AuthoredId("influence.dislikes_existing_plan"),
                    InfluenceVisibility.Full),
            });
        }

        private static BeliefDistribution Belief(long warmth)
        {
            BeliefDistribution belief = SocialBeliefUpdateService.BroadPrior();
            belief.Mean.Set(SocialDimensions.Warmth, warmth);
            for (int i = 0; i < SocialDimensions.Provisional.Count; i++)
                belief.SetCovariance(SocialDimensions.Provisional[i], SocialDimensions.Provisional[i], 0);
            return belief;
        }

        private static DecisionOption InvitationJoinOption()
        {
            var option = new DecisionOption(Join, "Join them", 0);
            option.SetContext(
                DecisionReasoningParameters.Target,
                DecisionParameterValue.FromEntity(new EntityRef(EntityKind.Character, 0)));
            return option;
        }

        private static DecisionOption InvitationKeepOption()
        {
            var option = new DecisionOption(KeepPlan, "Keep my plan", 1);
            option.SetContext(
                DecisionReasoningParameters.InterestId,
                DecisionParameterValue.FromAuthoredId(ReadingInterest));
            return option;
        }

        private static DecisionInfluence FindInfluence(Decision decision, AuthoredId bindingId) =>
            decision.Influences.Single(i => !i.IsRetracted && i.ReasonBindingId == bindingId);

        private static InfluenceView FindInfluence(DecisionView view, DecisionInfluenceId id) =>
            view.Options.SelectMany(option => option.Influences).Single(influence => influence.InfluenceId == id.Value);

        private static ActivityInstance Current(SimulationHost host, CharacterId character) =>
            host.World.Activities.Get(host.World.Characters.Get(character).CurrentActivityId);

        private sealed class Fixture
        {
            public Fixture(SimulationHost host, CharacterId requester, CharacterId recipient, LocationId location)
            {
                Host = host;
                Requester = requester;
                Recipient = recipient;
                Location = location;
            }

            public SimulationHost Host { get; }
            public CharacterId Requester { get; }
            public CharacterId Recipient { get; }
            public LocationId Location { get; }
            public Decision Invitation() => Host.World.Decisions.All.Single(d => d.DefinitionId == InvitationDecision);
        }
    }
}
