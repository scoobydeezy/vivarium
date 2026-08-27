using System;
using System.Collections.Generic;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.PlayerAgency;

namespace Vivarium.Application.Queries
{
    /// <summary>
    /// Projects a true Decision into the player-facing view (§26).
    /// <para>
    /// This is where truth, knowledge, and presentation stay separated (§2.3). The Domain constructed
    /// the <b>true</b> influence set; this decides how much of it the player sees, from content's
    /// visibility policy widened by what the player has actually learned.
    /// </para>
    /// <para>
    /// Truth: Mina fears disappointing Glen, d8. Knowledge: the player knows she cares about Glen but
    /// has not identified this fear. Presentation: "Personal concern d8".
    /// </para>
    /// </summary>
    public sealed class DecisionProjector
    {
        private readonly IReadOnlyDictionary<AuthoredId, InterventionDefinition> _interventions;
        private readonly DecisionHoldPolicy _holds;

        /// <param name="interventions">
        /// Used to answer "should this control be enabled?" with the same rules the command handler
        /// enforces (§19). Pass an empty dictionary to project without intervention affordances.
        /// </param>
        public DecisionProjector(
            IReadOnlyDictionary<AuthoredId, InterventionDefinition> interventions = null,
            DecisionHoldPolicy holds = null)
        {
            _interventions = interventions ?? new Dictionary<AuthoredId, InterventionDefinition>();
            _holds = holds;
        }

        public DecisionView Project(WorldState world, Decision decision)
        {
            if (decision == null)
            {
                throw new ArgumentNullException(nameof(decision));
            }

            string characterName = world.Characters.TryGet(decision.CharacterId, out Character character)
                ? character.DisplayName
                : decision.CharacterId.ToString();

            var options = new List<DecisionOptionView>(decision.Options.Count);

            for (int o = 0; o < decision.Options.Count; o++)
            {
                DecisionOption option = decision.Options[o];
                var influences = new List<InfluenceView>();

                for (int i = 0; i < decision.Influences.Count; i++)
                {
                    DecisionInfluence influence = decision.Influences[i];
                    if (influence.OptionId != option.Id || influence.IsRetracted)
                    {
                        continue;
                    }

                    InfluenceView view = ProjectInfluence(world, decision, influence);
                    if (view != null)
                    {
                        influences.Add(view);
                    }
                }

                string intent = ProjectCommitmentIntent(world, option);
                options.Add(new DecisionOptionView(
                    option.Id.Value,
                    intent ?? option.LabelId.Value,
                    influences,
                    intent));
            }

            DecisionResolutionView resolution = decision.Resolution == null
                ? null
                : new DecisionResolutionView(
                    decision.Resolution.ChosenOptionId.Value,
                    decision.Resolution.Degree.ToString(),
                    decision.Resolution.ResolvedAt.ToString(),
                    decision.Resolution.Source.ToString(),
                    ProjectResolvedReasons(decision.Resolution),
                    ProjectRolls(decision.Resolution.SupersededRolls));

            bool isHeld = world.Attention.IsHeld(decision.Id);
            int globalRemaining = _holds == null
                ? 0
                : Math.Max(0, _holds.MaxGlobalHeld - world.Attention.HeldCount);
            int heldForCharacter = HeldForCharacter(world, decision.CharacterId);
            int characterRemaining = _holds == null
                ? 0
                : Math.Max(0, _holds.MaxHeldPerCharacter - heldForCharacter);
            bool canBeHeld = decision.IsActive && !decision.IsAwaitingCommit && !isHeld &&
                (_holds == null || (globalRemaining > 0 && characterRemaining > 0));

            return new DecisionView(
                decision.Id.Value,
                decision.CharacterId.Value,
                characterName,
                decision.DefinitionId.Value,
                decision.Status.ToString(),
                decision.ResolveAt.ToString(),
                decision.InfluenceRevision,
                isHeld,
                canBeHeld,
                options,
                resolution,
                decision.CommitmentConflictKey != null,
                ProjectPending(decision.PendingResolution),
                decision.Importance,
                globalRemaining,
                characterRemaining,
                HoldUnavailableReason(decision, isHeld, globalRemaining, characterRemaining),
                ProjectAppliedInterventions(decision));
        }

        private int HeldForCharacter(WorldState world, CharacterId characterId)
        {
            if (_holds == null) return 0;
            int count = 0;
            foreach (DecisionId heldId in world.Attention.HeldDecisions)
                if (world.Decisions.TryGet(heldId, out Decision held) &&
                    held.IsActive && held.CharacterId == characterId)
                    count++;
            return count;
        }

        private string HoldUnavailableReason(
            Decision decision,
            bool isHeld,
            int globalRemaining,
            int characterRemaining)
        {
            if (isHeld) return "decision.hold.already_held";
            if (!decision.IsActive) return "decision.hold.not_active";
            if (decision.IsAwaitingCommit) return "decision.hold.rolls_pending";
            if (_holds != null && globalRemaining == 0) return "decision.hold.global_capacity";
            if (_holds != null && characterRemaining == 0) return "decision.hold.character_capacity";
            return null;
        }

        private static IReadOnlyList<AppliedInterventionView> ProjectAppliedInterventions(Decision decision)
        {
            var views = new List<AppliedInterventionView>(decision.Interventions.Count);
            for (int i = 0; i < decision.Interventions.Count; i++)
            {
                AppliedIntervention intervention = decision.Interventions[i];
                views.Add(new AppliedInterventionView(
                    intervention.InterventionDefinitionId.Value,
                    intervention.TargetInfluenceId.Value,
                    intervention.Kind.ToString(),
                    intervention.ResourceKind.ToString(),
                    intervention.ResourceCost,
                    intervention.CommandSequence));
            }
            return views;
        }

        private static PendingDecisionResolutionView ProjectPending(PendingDecisionResolution pending)
        {
            if (pending == null) return null;
            var accepted = new List<PendingInfluenceRollView>(pending.AcceptedRolls.Count);
            var superseded = new List<PendingInfluenceRollView>(pending.SupersededRolls.Count);
            for (int i = 0; i < pending.AcceptedRolls.Count; i++) accepted.Add(ProjectPendingRoll(pending.AcceptedRolls[i]));
            for (int i = 0; i < pending.SupersededRolls.Count; i++) superseded.Add(ProjectPendingRoll(pending.SupersededRolls[i]));
            return new PendingDecisionResolutionView(pending.ExpiresAt.ToString(), accepted, superseded);
        }

        private static PendingInfluenceRollView ProjectPendingRoll(InfluenceRoll roll) =>
            new PendingInfluenceRollView(roll.InfluenceId.Value, roll.Die.Sides, roll.Rolled, roll.RollIndex, roll.Die.IsFixed);

        private static IReadOnlyList<PendingInfluenceRollView> ProjectRolls(IReadOnlyList<InfluenceRoll> rolls)
        {
            var result = new List<PendingInfluenceRollView>(rolls.Count);
            for (int i = 0; i < rolls.Count; i++) result.Add(ProjectPendingRoll(rolls[i]));
            return result;
        }

        private static string ProjectCommitmentIntent(WorldState world, DecisionOption option)
        {
            CommitmentResolutionPlan plan = option.CommitmentResolutionPlan;
            if (plan == null || plan.Preserve.Count == 0 || plan.Relinquish.Count == 0) return null;
            return "Keep " + CommitmentLabel(world, plan.Preserve[0]) +
                "; give up " + CommitmentLabel(world, plan.Relinquish[0]);
        }

        private static string CommitmentLabel(WorldState world, CommitmentId id)
        {
            if (!world.Commitments.TryGet(id, out Commitment commitment)) return id.ToString();
            string value = commitment.Kind.Value;
            int separator = value.LastIndexOf('.');
            if (separator >= 0) value = value.Substring(separator + 1);
            string[] words = value.Split('_');
            for (int i = 0; i < words.Length; i++)
                if (words[i].Length > 0)
                    words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
            return string.Join(" ", words);
        }

        private static IReadOnlyList<DecisionReasonExplanationView> ProjectResolvedReasons(DecisionResolution resolution)
        {
            var views = new List<DecisionReasonExplanationView>();
            for (int i = 0; i < resolution.Rolls.Count; i++)
            {
                InfluenceRoll roll = resolution.Rolls[i];
                FrozenDecisionReason reason = roll.Reason;
                if (reason == null || (reason.Visibility & InfluenceVisibility.Existence) == 0) continue;
                var inputs = new List<string>(reason.Evaluation.Signals.Count);
                for (int s = 0; s < reason.Evaluation.Signals.Count; s++)
                {
                    DecisionSignalEvidence signal = reason.Evaluation.Signals[s];
                    inputs.Add($"{signal.SignalId.Value}={signal.Mean}, variance={signal.Variance} ({signal.Applicability})");
                }
                var contributions = new List<string>(reason.Evaluation.Contributions.Count);
                for (int c = 0; c < reason.Evaluation.Contributions.Count; c++)
                {
                    DecisionContributionEvidence contribution = reason.Evaluation.Contributions[c];
                    contributions.Add($"{contribution.SourceId.Value}:{contribution.Amount}");
                }
                views.Add(new DecisionReasonExplanationView(
                    roll.InfluenceId.Value,
                    roll.OptionId.Value,
                    (reason.Visibility & InfluenceVisibility.Label) != 0 ? reason.LabelId.Value : null,
                    (reason.Visibility & InfluenceVisibility.Category) != 0 ? reason.CategoryId.Value : null,
                    (reason.Visibility & InfluenceVisibility.Magnitude) != 0 ? roll.Die.Sides : 0,
                    roll.Rolled,
                    roll.Polarity.ToString(),
                    reason.Evaluation.ExpectedScore,
                    reason.Evaluation.OutputVariance,
                    inputs,
                    contributions));
            }
            return views;
        }

        /// <summary>
        /// Applies visibility policy to one influence.
        /// <para>
        /// Returns <c>null</c> when the influence should not be shown at all — and the caller must not
        /// substitute a placeholder, because the <i>number</i> of hidden influences is not exposed
        /// either (§26).
        /// </para>
        /// </summary>
        private InfluenceView ProjectInfluence(WorldState world, Decision decision, DecisionInfluence influence)
        {
            InfluenceVisibility visibility = EffectiveVisibility(world, influence);

            if ((visibility & InfluenceVisibility.Existence) == 0)
            {
                return null;
            }

            string label = (visibility & InfluenceVisibility.Label) != 0 ? influence.LabelId.Value : null;
            string category = (visibility & InfluenceVisibility.Category) != 0 ? influence.Category.Value : null;
            int? dieSides = (visibility & InfluenceVisibility.Magnitude) != 0 ? influence.CurrentDie.Sides : (int?)null;
            string explanation = (visibility & InfluenceVisibility.Explanation) != 0 ? influence.LabelId.Value : null;

            return new InfluenceView(
                influence.Id.Value,
                label,
                category,
                dieSides,
                explanation,
                AnyInterventionAvailable(world, decision, influence.Id),
                ProjectInterventions(world, decision, influence.Id));
        }

        /// <summary>
        /// Content's default visibility, widened by what the player knows.
        /// <para>
        /// Knowledge can only ever <i>reveal</i> here. It never hides something content chose to show,
        /// which keeps "why can I see this?" answerable.
        /// </para>
        /// </summary>
        private static InfluenceVisibility EffectiveVisibility(WorldState world, DecisionInfluence influence)
        {
            InfluenceVisibility visibility = influence.DefaultVisibility;

            if (!influence.Subject.IsSet)
            {
                return visibility;
            }

            // Knowing the underlying fact promotes a generalized influence to a specific one.
            var influenceFact = new FactKey(FactKinds.DecisionInfluence, influence.Subject, influence.LabelId);
            var legacyTraitFact = new FactKey(FactKinds.CharacterTrait, influence.Subject, influence.LabelId);
            if (world.Knowledge.Knows(influenceFact) || world.Knowledge.Knows(legacyTraitFact))
            {
                visibility |= InfluenceVisibility.Label | InfluenceVisibility.Explanation;
            }

            return visibility;
        }

        private bool AnyInterventionAvailable(WorldState world, Decision decision, DecisionInfluenceId influenceId)
        {
            foreach (KeyValuePair<AuthoredId, InterventionDefinition> pair in _interventions)
            {
                if (DecisionInterventionRules.Evaluate(decision, pair.Value, influenceId, world.Nudges, world.InterventionResources).IsSuccess)
                {
                    return true;
                }
            }

            return false;
        }

        private IReadOnlyList<InterventionAvailabilityView> ProjectInterventions(
            WorldState world,
            Decision decision,
            DecisionInfluenceId influenceId)
        {
            var views = new List<InterventionAvailabilityView>(_interventions.Count);
            foreach (KeyValuePair<AuthoredId, InterventionDefinition> pair in _interventions)
            {
                Result eligibility = DecisionInterventionRules.Evaluate(decision, pair.Value, influenceId, world.Nudges, world.InterventionResources);
                views.Add(new InterventionAvailabilityView(
                    pair.Key.Value,
                    pair.Value.ResourceKind.ToString(),
                    pair.Value.Cost,
                    eligibility.IsSuccess,
                    eligibility.IsFailure ? eligibility.Reason.Value : null));
            }
            views.Sort((left, right) => string.CompareOrdinal(
                left.InterventionDefinitionId,
                right.InterventionDefinitionId));
            return views;
        }
    }

    public sealed class NudgeEconomyProjector
    {
        public NudgeEconomyView Project(WorldState world) => new NudgeEconomyView(
            world.Nudges.Balance,
            world.Nudges.Cap,
            world.Nudges.Revision,
            NudgeRegenerationSchedule.NextBoundaryAfter(world.Clock.Now).ToString());
    }


    public sealed class DecisionInterventionResourceProjector
    {
        public IReadOnlyList<InterventionResourceView> Project(WorldState world)
        {
            var result = new List<InterventionResourceView>();
            foreach (KeyValuePair<InterventionResourceKind, ResourceState> pair in world.InterventionResources.All)
            {
                ResourceState state = pair.Value;
                result.Add(new InterventionResourceView(pair.Key.ToString(), state.Balance, state.Cap, state.Revision,
                    state.Refreshes ? state.NextRefreshAt.ToString() : null));
            }
            return result;
        }
    }
}
