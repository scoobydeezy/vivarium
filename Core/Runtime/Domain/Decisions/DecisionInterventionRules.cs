using System;
using Vivarium.Domain.Common;
using Vivarium.Domain.PlayerAgency;

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

    /// <summary>Which authoritative availability policy pays for an intervention.</summary>
    public enum InterventionResourceKind
    {
        Nudge = 0,
        None = 1,
        ReRoll = 2,
        ReplacementDie = 3,
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
            Die replacementDie = default,
            InterventionResourceKind resourceKind = InterventionResourceKind.Nudge,
            InterventionResourcePolicy resourcePolicy = default)
        {
            if (!id.IsSet)
            {
                throw new ArgumentException("Definitions need a stable authored id (§7).", nameof(id));
            }

            if (cost < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cost));
            }

            if (resourceKind == InterventionResourceKind.None && cost != 0)
            {
                throw new ArgumentException("An intervention with no resource policy must have zero cost.", nameof(cost));
            }

            Id = id;
            Kind = kind;
            Cost = cost;
            RequiresTargetInfluence = requiresTargetInfluence;
            RepeatableOnSameInfluence = repeatableOnSameInfluence;
            ReplacementDie = replacementDie;
            ResourceKind = resourceKind;
            ResourcePolicy = resourcePolicy;

            if (kind == InterventionKind.ReplaceDie && !replacementDie.IsSet)
                throw new ArgumentException("A die substitution needs an authored replacement die.", nameof(replacementDie));
            if ((resourceKind == InterventionResourceKind.ReRoll || resourceKind == InterventionResourceKind.ReplacementDie) &&
                resourcePolicy.Cap < 1)
                throw new ArgumentException("A non-Nudge resource needs an authored availability policy.", nameof(resourcePolicy));
        }

        public AuthoredId Id { get; }

        public InterventionKind Kind { get; }

        /// <summary>Cost under <see cref="ResourceKind"/>.</summary>
        public int Cost { get; }

        public InterventionResourceKind ResourceKind { get; }

        public InterventionResourcePolicy ResourcePolicy { get; }

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
        public static readonly AuthoredId ReasonInsufficientNudges = new AuthoredId("decision.intervention.insufficient_nudges");
        public static readonly AuthoredId ReasonUnsupportedResource = new AuthoredId("decision.intervention.resource_not_available");
        public static readonly AuthoredId ReasonRollsNotProduced = new AuthoredId("decision.intervention.rolls_not_produced");
        public static readonly AuthoredId ReasonRollsAlreadyProduced = new AuthoredId("decision.intervention.rolls_already_produced");
        public static readonly AuthoredId ReasonInfluenceHidden = new AuthoredId("decision.intervention.influence_hidden");

        /// <summary>
        /// Evaluates eligibility without mutating anything.
        /// </summary>
        /// <summary>Eligibility including the intervention's authoritative resource policy.</summary>
        public static Result Evaluate(
            Decision decision,
            InterventionDefinition intervention,
            DecisionInfluenceId targetInfluenceId,
            NudgeAccount nudges,
            DecisionInterventionResources resources)
        {
            Result mechanics = EvaluateMechanics(decision, intervention, targetInfluenceId);
            if (mechanics.IsFailure)
            {
                return mechanics;
            }

            switch (intervention.ResourceKind)
            {
                case InterventionResourceKind.None:
                    return Result.Ok();
                case InterventionResourceKind.Nudge:
                    return nudges.CanSpend(intervention.Cost)
                        ? Result.Ok()
                        : Result.Fail(ReasonInsufficientNudges, $"Need {intervention.Cost}; have {nudges.Balance}.");
                case InterventionResourceKind.ReRoll:
                case InterventionResourceKind.ReplacementDie:
                    return resources != null && resources.CanSpend(intervention.ResourceKind, intervention.Cost)
                        ? Result.Ok()
                        : Result.Fail(ReasonUnsupportedResource, intervention.ResourceKind.ToString());
                default: return Result.Fail(ReasonUnsupportedResource, intervention.ResourceKind.ToString());
            }
        }

        private static Result EvaluateMechanics(Decision decision, InterventionDefinition intervention, DecisionInfluenceId targetInfluenceId)
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

            if (intervention.Kind == InterventionKind.Reroll && !decision.IsAwaitingCommit)
                return Result.Fail(ReasonRollsNotProduced, "Re-roll is available only after the initial roll is known.");
            if (intervention.Kind != InterventionKind.Reroll && decision.IsAwaitingCommit)
                return Result.Fail(ReasonRollsAlreadyProduced, "Pre-roll interventions cannot alter frozen rolls.");

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

            if ((influence.DefaultVisibility & InfluenceVisibility.Existence) == 0)
                return Result.Fail(ReasonInfluenceHidden, targetInfluenceId.ToString());

            if (intervention.Kind == InterventionKind.Reroll &&
                !decision.PendingResolution.TryGetAccepted(targetInfluenceId, out InfluenceRoll _))
                return Result.Fail(ReasonInfluenceUnknown, "The target did not participate in the frozen roll set.");

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
                        // The resolution service advances the scoped stream only after the frozen
                        // target and resource spend have both been validated.
                        break;

                    case InterventionKind.AddDie:
                        // Adding a die is a new influence; content supplies its label and category, so
                        // the command handler creates it and this records only the spend.
                        break;
                }
            }

            decision.RecordIntervention(new AppliedIntervention(
                intervention.Id,
                targetInfluenceId,
                commandSequence,
                intervention.Kind,
                intervention.ReplacementDie,
                intervention.ResourceKind,
                intervention.Cost));
        }
    }
}
