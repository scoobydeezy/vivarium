using Vivarium.Domain.Common;
using Vivarium.Domain.Events;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Social;

namespace Vivarium.Domain.Decisions
{
    /// <summary>Targets only open Decisions that declared a dependency on this observer→target belief.</summary>
    public sealed class SocialBeliefDecisionHandler : DomainEventHandler<SocialBeliefChangedEvent>
    {
        private readonly DecisionReevaluationService _reevaluation;

        public SocialBeliefDecisionHandler(DecisionReevaluationService reevaluation)
            : base(SocialBeliefChangedEvent.Type)
        {
            _reevaluation = reevaluation;
        }

        protected override void Handle(SocialBeliefChangedEvent domainEvent, WorldState world, SimulationContext context)
        {
            if (!domainEvent.Observer.IsCharacter)
            {
                return;
            }

            _reevaluation.ReevaluateDependents(
                context,
                new DecisionDependencyKey(
                    RevisionAspects.Scoped(
                        SocialDecisionDependencies.BeliefContext,
                        new AuthoredId("target." + domainEvent.TargetId.Value)),
                    domainEvent.Observer.CharacterId.ToRef()));
        }
    }
}
