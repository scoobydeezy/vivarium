using System;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;
using Vivarium.Domain.Simulation;

namespace Vivarium.Domain.Decisions
{
    /// <summary>Content-configured reevaluation of one influence from an active Activity modifier.</summary>
    public sealed class ActivityContextInfluenceReevaluator : IDecisionInfluenceReevaluator
    {
        private readonly AuthoredId _contextKind;
        private readonly AuthoredId _modifierId;
        private readonly AuthoredId _influenceLabelId;
        private readonly Die _presentDie;
        private readonly Die _absentDie;

        public ActivityContextInfluenceReevaluator(
            AuthoredId decisionDefinitionId,
            AuthoredId contextKind,
            AuthoredId modifierId,
            AuthoredId influenceLabelId,
            Die presentDie,
            Die absentDie)
        {
            DecisionDefinitionId = decisionDefinitionId;
            _contextKind = contextKind;
            _modifierId = modifierId;
            _influenceLabelId = influenceLabelId;
            _presentDie = presentDie;
            _absentDie = absentDie;
        }

        public AuthoredId DecisionDefinitionId { get; }

        public void Reevaluate(WorldState world, Decision decision, DecisionDependencyKey changedKey, SimulationContext context)
        {
            if (changedKey.ContextKind != _contextKind || changedKey.Subject != decision.CharacterId.ToRef())
            {
                return;
            }

            bool present = world.TryGetCurrentActivity(decision.CharacterId, out ActivityInstance activity) &&
                activity.HasModifier(_modifierId);
            Die target = present ? _presentDie : _absentDie;

            for (int i = 0; i < decision.Influences.Count; i++)
            {
                DecisionInfluence influence = decision.Influences[i];
                if (!influence.IsRetracted && influence.LabelId == _influenceLabelId && influence.CurrentDie != target)
                {
                    decision.ChangeInfluenceDie(influence.Id, target);
                }
            }
        }
    }
}
