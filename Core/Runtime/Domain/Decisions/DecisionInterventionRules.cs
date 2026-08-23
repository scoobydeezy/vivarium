using System;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Decisions
{
    /// <summary>What an intervention does to a decision (§19). These are content, not architecture.</summary>
    public enum InterventionKind
    {
        Unknown = -1,
        AddDie = 0,
        RemoveDie = 1,
        StepDieUp = 2,
        StepDieDown = 3,
        Reroll = 4,
        ReplaceDie = 5,
    }

    /// <summary>Immutable content description of a player intervention (§19).</summary>
    public sealed class InterventionDefinition
    {
        public InterventionDefinition(
            AuthoredId id,
            InterventionKind kind,
            int cost,
            bool requiresTargetInfluence = true,
            bool repeatableOnSameInfluence = false,
            Die replacementDie = default)
        {
            if (!id.IsSet)
            {
                throw new ArgumentException("Definitions need a stable authored id (§7).", nameof(id));
            }

            Id = id;
            Kind = kind;
            Cost = cost;
            RequiresTargetInfluence = requiresTargetInfluence;
            RepeatableOnSameInfluence = repeatableOnSameInfluence;
            ReplacementDie = replacementDie;
        }

        public AuthoredId Id { get; }

        public InterventionKind Kind { get; }

        /// <summary>Resource cost. The intervention economy itself is deferred (§57).</summary>
        public int Cost { get; }

        public bool RequiresTargetInfluence { get; }

        public bool RepeatableOnSameInfluence { get; }

        /// <summary>Used by <see cref="InterventionKind.ReplaceDie"/>.</summary>
        public Die ReplacementDie { get; }

        public override string ToString() => $"{Id} ({Kind})";
    }

    /// <summary>
    /// The single authority on whether an intervention is legal (§19).
    /// <para>
    /// The UI calls <see cref="Evaluate"/> to decide whether a control appears enabled; the command
    /// handler calls the same method before mutating anything. <b>No duplicated UI validation logic</b>
    /// (invariant 57) — if these ever disagree, the player is being lied to by one of them.
    /// </para>
    /// </summary>
    public static class DecisionInterventionRules
    {
        public static readonly AuthoredId ReasonDecisionNotActive = new AuthoredId("decision.intervention.decision_not_active");
        public static readonly AuthoredId ReasonInfluenceUnknown = new AuthoredId("decision.intervention.influence_unknown");
        public static readonly AuthoredId ReasonInfluenceRetracted = new AuthoredId("decision.intervention.influence_retracted");
        public static readonly AuthoredId ReasonTargetRequired = new AuthoredId("decision.intervention.target_required");
        public static readonly AuthoredId ReasonAlreadyApplied = new AuthoredId("decision.intervention.already_applied");
        public static readonly AuthoredId ReasonDieAtLadderTop = new AuthoredId("decision.intervention.die_at_ladder_top");
        public static readonly AuthoredId ReasonDieAtLadderBottom = new AuthoredId("decision.intervention.die_at_ladder_bottom");

        /// <summary>
        /// Evaluates eligibility without mutating anything.
        /// </summary>
        public static Result Evaluate(Decision decision, InterventionDefinition intervention, DecisionInfluenceId targetInfluenceId)
        {
            if (decision == null)
            {
                throw new ArgumentNullException(nameof(decision));
            }

            if (intervention == null)
            {
                throw new ArgumentNullException(nameof(intervention));
            }

            if (!decision.IsActive)
            {
                return Result.Fail(ReasonDecisionNotActive, $"Decision {decision.Id} is {decision.Status}.");
            }

            if (!intervention.RequiresTargetInfluence)
            {
                return Result.Ok();
            }

            if (!targetInfluenceId.IsSet)
            {
                return Result.Fail(ReasonTargetRequired, $"{intervention.Id} must target an influence.");
            }

            if (!decision.TryGetInfluence(targetInfluenceId, out DecisionInfluence influence))
            {
                return Result.Fail(ReasonInfluenceUnknown, $"{targetInfluenceId} is not part of decision {decision.Id}.");
            }

            if (influence.IsRetracted)
            {
                return Result.Fail(ReasonInfluenceRetracted, $"{targetInfluenceId} no longer applies.");
            }

            if (!intervention.RepeatableOnSameInfluence && decision.HasInterventionTargeting(targetInfluenceId, intervention.Id))
            {
                return Result.Fail(ReasonAlreadyApplied, $"{intervention.Id} was already spent on {targetInfluenceId}.");
            }

            switch (intervention.Kind)
            {
                case InterventionKind.StepDieUp when influence.CurrentDie.StepUp() == influence.CurrentDie:
                    return Result.Fail(ReasonDieAtLadderTop, $"{influence.CurrentDie} is already the largest die.");

                case InterventionKind.StepDieDown when influence.CurrentDie.StepDown() == influence.CurrentDie:
                    return Result.Fail(ReasonDieAtLadderBottom, $"{influence.CurrentDie} is already the smallest die.");

                default:
                    return Result.Ok();
            }
        }

        /// <summary>
        /// Applies an intervention that <see cref="Evaluate"/> has already approved.
        /// <para>
        /// Callers must evaluate first — this method assumes eligibility rather than re-deriving it, so
        /// that the rule lives in exactly one place.
        /// </para>
        /// </summary>
        public static void Apply(
            Decision decision,
            InterventionDefinition intervention,
            DecisionInfluenceId targetInfluenceId,
            long commandSequence)
        {
            if (intervention.RequiresTargetInfluence && decision.TryGetInfluence(targetInfluenceId, out DecisionInfluence influence))
            {
                switch (intervention.Kind)
                {
                    case InterventionKind.StepDieUp:
                        influence.SetDie(influence.CurrentDie.StepUp());
                        break;

                    case InterventionKind.StepDieDown:
                        influence.SetDie(influence.CurrentDie.StepDown());
                        break;

                    case InterventionKind.ReplaceDie:
                        influence.SetDie(intervention.ReplacementDie);
                        break;

                    case InterventionKind.RemoveDie:
                        influence.Retract();
                        break;

                    case InterventionKind.Reroll:
                        influence.Reroll();
                        break;

                    case InterventionKind.AddDie:
                        // Adding a die is a new influence; content supplies its label and category, so
                        // the command handler creates it and this records only the spend.
                        break;
                }
            }

            decision.RecordIntervention(new AppliedIntervention(
                intervention.Id, targetInfluenceId, commandSequence, intervention.Kind, intervention.ReplacementDie));
        }
    }
}
