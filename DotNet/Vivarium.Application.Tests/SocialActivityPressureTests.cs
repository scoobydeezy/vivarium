using Vivarium.Domain.Activities;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Social;
using Vivarium.Domain.Time;
using Xunit;

namespace Vivarium.Application.Tests
{
    public sealed class SocialActivityPressureTests
    {
        [Fact]
        public void CalibratedSocialPressureChangesActivityOnlyForTheSharedContextInterval()
        {
            TestWorld fixture = TestWorld.Create(includeSocialDecision: true);
            var darius = new Character(fixture.Host.World.RuntimeIds.Characters.Next(), "Darius", fixture.Host.World.Clock.Now);
            fixture.Host.World.Characters.Add(darius.Id, darius);
            fixture.Host.Transitions.BeginActivity(
                fixture.Host.Simulation,
                darius.Id,
                WellKnownActivities.Waiting,
                fixture.Bakery,
                SimDuration.FromHours(1));

            Character mina = fixture.Host.World.Characters.Get(fixture.Mina);
            mina.SetAppraisalField(new AppraisalField(
                mina.Id,
                AppraisalLenses.Comfort,
                0,
                new[] { new SocialLinearTerm(SocialDimensions.Warmth, -10000) },
                null,
                null,
                null,
                null,
                new AuthoredId("social.calibration.standard")));
            BeliefDistribution belief = SocialBeliefUpdateService.BroadPrior();
            belief.Mean.Set(SocialDimensions.Warmth, 9000);
            for (int i = 0; i < SocialDimensions.Provisional.Count; i++)
            {
                belief.SetCovariance(SocialDimensions.Provisional[i], SocialDimensions.Provisional[i], 0);
            }
            fixture.Host.World.Knowledge.SetSocialBelief(
                ObserverRef.Character(mina.Id),
                darius.Id,
                belief,
                fixture.Host.World.Clock.Now);

            var modifierId = new AuthoredId("activity_modifier.social_discomfort");
            SocialPressureDefinition pressure = fixture.Catalog.SocialPressures[new AuthoredId("social.pressure.seek_company")];
            var service = new SocialActivityPressureService(
                fixture.Host.Transitions,
                fixture.Catalog,
                pressure,
                AppraisalLenses.Comfort,
                TestWorld.ActivityWorking,
                modifierId,
                pressuredRate: 2,
                minimumStrength: AppraisalStrength.Minor);
            fixture.Host.DomainEventHandlers.Register(new SocialActivityArrivalHandler(service), 250);
            fixture.Host.DomainEventHandlers.Register(new SocialActivityDepartureHandler(service), 250);

            ActivityInstance work = fixture.Host.Transitions.BeginActivity(
                fixture.Host.Simulation,
                mina.Id,
                TestWorld.ActivityWorking,
                fixture.Bakery,
                SimDuration.FromHours(1),
                performanceRatePerMinute: 10);
            fixture.Host.Session.Advance(SimDuration.Zero);
            Assert.True(work.HasModifier(modifierId));

            fixture.Host.Session.Advance(SimDuration.FromMinutes(10));
            fixture.Host.Transitions.BeginActivity(
                fixture.Host.Simulation,
                darius.Id,
                WellKnownActivities.Waiting,
                fixture.Home,
                SimDuration.FromHours(1));
            fixture.Host.Session.Advance(SimDuration.Zero);
            Assert.False(work.HasModifier(modifierId));

            fixture.Host.Session.Advance(SimDuration.FromMinutes(10));
            Assert.Equal((10 * 2) + (10 * 10), work.Performance.ValueAt(fixture.Host.World.Clock.Now));
        }
    }
}
