# Vivarium Decision Importance Brief

**Status:** Draft for implementation; numeric thresholds intentionally open for tuning  
**Scope:** Deriving per-instance Decision Importance, admitting meaningful candidate choices into the
Decision pipeline, and using that value for player-facing Attention policy.  
**Related documents:** [`DecisionReasoning.md`](DecisionReasoning.md),
[`../Product/PlayerAgencyBrief.md`](../Product/PlayerAgencyBrief.md), and
[`../Architecture/Reference.md`](../Architecture/Reference.md).

---

## 1. Product intent

Characters make many choices that should simply happen in the background. A smaller number deserve a
persistent, explainable Decision and an even smaller number deserve proactive player attention.

The category of a choice does not decide which group it belongs to. Choosing clothing, Recreation, or
whether to accept a date may be ordinary for one character and personally significant for another.

> Importance is derived for the specific character, choice, and circumstance from evaluated reasoning;
> it is never a fixed importance assigned to a Decision type.

---

## 2. One scale, several gates

All importance consumers use the same deterministic fixed-point scale as evaluated Decision reasons:

```text
0                                  SignalNumeric.Scale (10,000)
no evaluated magnitude             maximum evaluated magnitude
```

They answer different questions and therefore use independently tunable thresholds:

| Gate | Question | Effect |
| --- | --- | --- |
| Admission floor | Should this candidate become a full Decision? | Below it, choose and act through the ordinary routine path. At or above it, create a persistent Decision. |
| Normal feed floor | Should an admitted Decision proactively enter the ordinary player feed? | Below it, the Decision remains inspectable from its character and history but does not demand attention. |
| Prioritized feed floor | Should a watched or Followed character's Decision enter the feed? | Uses the same Importance value with a separately tunable, no-higher threshold. |
| Auto-Hold floor | Should an eligible new Decision engage Auto-Hold? | Applies only when the Decision is `HoldEligible` and the character's Attention policy and simulation mode allow it. |

Quiet suppresses proactive surfacing regardless of Importance. It does not alter the Decision, its
resolution, history, or later inspection. Offline catch-up creates no immediate notification or new Hold;
recap policy remains a separate presentation concern.

The initial ordering constraint is:

```text
AdmissionFloor <= PrioritizedFeedFloor <= NormalFeedFloor <= AutoHoldFloor
```

The actual values remain unset until representative choices can be measured. They are validated content
parameters, not constants scattered through generators or Unity views.

---

## 3. Initial derivation

Compiled reasoning already produces a consolidated `CandidateReason` for each surviving
Option/ReasonChannel pair. Each carries `DecisionReasonEvaluation.ExpectedScore` on the shared
`-10,000...10,000` bounded scale.

For the initial implementation:

```text
ReasonMagnitude = abs(ExpectedScore)
DecisionImportance = max(ReasonMagnitude for every active consolidated reason)
```

If no evaluated reason survives consolidation, Importance is `0`.

This maximum rule is deliberately not a count or sum. One strong reason must be able to make a choice
important; adding many trivial Options or correlated reasons must not inflate it past a meaningful one.
Reason consolidation happens before Importance derivation, so correlated raw Signals cannot gain weight
by being repeated.

`OutputVariance` is retained as reasoning evidence but is not independently added to Importance.
Uncertainty has no universal behavioral meaning (`DecisionReasoning.md` §8); a Consideration that treats
uncertainty as important must express that through its evaluated score or a distinct consolidated reason.

Importance uses the unmodified world-derived evaluation. Player intervention may change a die used for
resolution, but it does not retroactively make the underlying choice more or less important.

---

## 4. Candidate admission without a disposable Decision

Routine planning needs to evaluate a candidate before allocating persistent runtime state:

```text
available authored candidates
        ↓
bind actor, Options, and circumstance
        ↓
evaluate and consolidate reasons
        ↓
derive Importance
        ├─ below AdmissionFloor → choose through ordinary routine scoring
        └─ clears AdmissionFloor → allocate and publish a full Decision
```

The preflight path must not allocate a `DecisionId`, schedule an Event, publish a Domain Event, or mutate
the World. Rejected candidates must be invisible to authoritative identity and ordering.

The evaluator should therefore consume a reusable immutable reasoning context—actor, Options, bound
parameters, and snapshotted reasoning program—rather than require an already-added `Decision`. An admitted
Decision adopts the exact evaluated reasons and Importance from that preflight result; it does not perform
a second potentially divergent evaluation at the same instant.

Automatic routine selection and Decision admission share evaluation evidence but remain distinct outputs:

- routine scoring answers which available action the character prefers;
- Importance answers whether the magnitude warrants the full Decision lifecycle.

Admission gating applies only when the generator has a valid automatic fallback for the same choice. Some
Decision generators represent a structural dilemma that cannot truthfully disappear below a magnitude
floor. A real commitment conflict, for example, must select which incompatible plan to preserve; there is
no ordinary routine outcome that avoids making that choice. Such mandatory generators always create a
Decision once their own generation predicate is satisfied. They still derive Importance for feed ordering,
Auto-Hold, and held-capacity overflow, but they do not consult `AdmissionFloor`.

---

## 5. Living Decisions

Importance is authoritative mutable state on an active Decision. Whenever targeted reevaluation changes
its consolidated reasons, Importance is recomputed from the complete current reason set—not only the
routes touched by that event.

An Importance change:

- participates in the same deterministic revision/update boundary as the reason change;
- refreshes feed ordering and eligibility at quiescence;
- does not re-run the admission decision for an already-created Decision;
- does not newly Auto-Hold or release a Decision merely because its Importance crossed a threshold;
- is persisted so save/load resumes from the exact authoritative value.

This prospective Auto-Hold rule does not freeze held-capacity priority. Live Importance participates in
the existing deterministic overflow ordering whenever a new Hold would exceed capacity. An already-Held
Decision may therefore be selected for overflow resolution after relative Importance changes; that is
capacity enforcement, not retroactive threshold-based Auto-Hold or release. A Decision that grows more
important may also newly qualify for the feed at quiescence even though it is not automatically Held.

Resolved historical Decisions retain the Importance that existed at resolution. Later World changes do
not rewrite it.

---

## 6. Authoring and migration rules

`DecisionDefinition.Importance` and generator request importance arguments are transitional static inputs
and must be removed once all production generators derive Importance.

Every reason allowed to affect Importance must carry evaluation evidence. Production definitions that
still rely solely on legacy influence templates must either migrate those reasons to compiled
Considerations or fail validation when importance-derived behavior is enabled. A static fallback would
reintroduce two incompatible importance systems and is not allowed.

One catalog-owned policy definition supplies the named thresholds. Unity authoring converts it into the
same immutable Domain definition used by headless content. Threshold values may be tuned later without
changing the derivation algorithm.

---

## 7. Implementation sequence

### Slice A — derived Importance for existing Decisions

1. Add a pure `DecisionImportanceEvaluator` over consolidated reason evaluations.
2. Make active `Decision.Importance` mutable only through reason reconciliation; persist and restore it.
3. Recompute it after full or targeted reevaluation and publish one coherent change at quiescence.
4. Remove static importance from the three production generation paths and migrate production reasoning
   that lacks evaluation evidence.
5. Keep all existing Decisions admitted during this compatibility slice. Later admission gating applies
   only to candidate generators with an ordinary fallback; mark structural generators such as
   commitment-conflict as permanently mandatory rather than awaiting migration.

### Slice B — Recreation proves candidate admission

1. Introduce the immutable preflight reasoning context/result without allocating runtime identity.
2. Score available Tabletop Games / Reading candidates from Interests and availability.
3. Resolve ordinary low-Importance Recreation automatically.
4. Promote a high-Importance instance into the same compiled Decision pipeline.
5. Prove deterministic selection, admission, Travel, completion, save/load, and offline equivalence.

### Slice C — Attention consumers

1. Add the catalog-owned threshold policy and validation.
2. Build the feed query from all admitted Decisions using Importance plus Normal/Follow/Watch/Quiet policy.
3. Implement Auto-Hold against `HoldEligible`, the same Importance value, and its own threshold.
4. Add bounded ordering and overflow tests using live Importance, resolve time, then Decision ID, including
   an existing Hold whose relative eviction priority changed after reevaluation.

Threshold calibration may proceed after Slice B produces real distributions; it must not block the core
derivation or candidate-admission architecture.

---

## 8. Calibration evidence

Tests and diagnostic traces should record the derived Importance for representative cases without
asserting premature final cutoffs:

- ordinary clothing choice;
- ordinary Tabletop Games versus Reading;
- Recreation carrying an unusually strong Interest or social reason;
- accepting or declining a date;
- Hunger versus uninterrupted Work;
- travel-caused commitment conflict.

Initial tests assert ordering and invariants—for example, a strong identity-linked Recreation reason
outranks several trivial clothing reasons—while threshold-specific acceptance tests are added only when
the product values are tuned and locked.

---

## 9. Required invariants

1. Importance is derived per instance from consolidated evaluated reasons, never authored per Decision type.
2. The same derivation is used at applicable candidate admission, active reevaluation, feed ordering,
   Auto-Hold, and held-capacity overflow.
3. No runtime identity or Event is allocated for a fallback-capable candidate rejected by the admission floor.
4. A mandatory structural Decision is never suppressed by the admission floor; its derived Importance
   governs Attention and capacity policy after generation.
5. Ordinary automatic choices remain deterministic, explainable in diagnostic tracing, and available
   without player presence.
6. Importance does not leak hidden reasons through player-facing detail; the scalar may be shown only when
   the relevant projection contract permits it.
7. Attention changes surfacing and Hold behavior, never the character's evaluated reasons or autonomous
   outcome rules.
8. Save/load, offline catch-up, and deterministic replay preserve admission and resolution equivalence.

---

## 10. Completion statement

> When a character evaluates an authored candidate choice with a valid ordinary fallback, the magnitude
> of its consolidated Signals deterministically decides whether it resolves as background routine behavior
> or becomes a persistent Decision; mandatory structural Decisions remain admitted, and that same living
> Importance then orders and gates player Attention without changing the character's reasons or outcome.
