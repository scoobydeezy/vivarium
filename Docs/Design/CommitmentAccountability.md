# Vivarium — Commitment Outcomes / Accountability Brief

**Status:** Accepted design reference; v0 vertical slice implemented
**Depends on:** [`../Architecture/Reference.md`](../Architecture/Reference.md),
[`SocialModel.md`](SocialModel.md), [`CommitmentConflict.md`](CommitmentConflict.md), and
[`DecisionReasoning.md`](DecisionReasoning.md)
**Scope:** This is not a new Decision. It's the feedback mechanism that makes commitment outcomes causally matter — the thing that turns "Mina made a choice" into "the world remembers she made it." This document records what's been locked across three rounds of review; it does not re-derive dependencies.

Current evidence and explicitly thin behavior are tracked in
[`../ImplementationStatus.md`](../ImplementationStatus.md).

---

## 1. Slice Goal

> When a Commitment transitions lifecycle status, apply directional social consequences to the stakeholders it's actually owed to — through the existing Knowledge/Appraisal machinery, not a parallel one — such that a later Decision involving the responsible character produces a measurably different Influence.

Not every failure implies blame. The system exists to distinguish outcome, cause, and stakeholder-perceived attribution as three separate things, and to prove that the resulting belief change actually reaches downstream reasoning — not just that a number moved somewhere.

---

## 2. Canonical Outcome Event and Lifecycle Authority

All status mutation moves behind one authority:

```text
CommitmentLifecycleService.Transition(...)
```

It validates the transition, updates Commitment state exactly once, allocates a stable `CommitmentOutcomeId`, and emits one canonical Domain Event. It knows nothing about Trust, Resentment, or Evidence — social accountability is purely a reaction to the event it emits.

```text
CommitmentOutcome                    // immutable once created — a historical fact, not mutable state
├─ CommitmentOutcomeId               // stable runtime ID, allocated here
├─ CommitmentId
├─ PreviousStatus
├─ NewStatus
├─ OccurredAt
├─ AuthoritativeCause
├─ ResponsibleActor?
├─ SourceDecisionId?
```

Every artifact this outcome produces carries the same ID for provenance and idempotency:

```text
RelationshipMemory.SourceOutcomeId
ObservedSocialEvidence.SourceOutcomeId
HistoryEntry.SourceOutcomeId
```

**Idempotency is a real rule, not just a debugging convenience:** any handler reacting to the lifecycle event (evidence ingestion, memory creation, history entry) checks-and-sets against `CommitmentOutcomeId` before applying its effect. Two reaction paths processing the same outcome must be recognizable as the same causal event, not silently double-applied.

---

## 3. Authoritative Cause ≠ Known Attribution

This is the central lock. `AuthoritativeCause` on `CommitmentOutcome` is simulation truth. It must never be read directly by anything computing a stakeholder's social reaction — that would grant the stakeholder omniscient access to causal facts they have no way of actually knowing, which is exactly what the Truth/Knowledge separation exists to prevent.

Instead:

```text
AUTHORITATIVE OUTCOME (simulation truth)
        ↓
STAKEHOLDER OBSERVATION (what the stakeholder actually perceives)
        ↓
KNOWN ATTRIBUTION (what the stakeholder currently believes caused it)
        ↓
SOCIAL CONSEQUENCE
```

```text
KnownAttribution
├─ ObservedOutcome        // Fulfilled | Relinquished | Missed | Cancelled
├─ PerceivedCause         // RelinquishedByActor | MissedWindowExpired | Unknown
├─ ObservedAt
└─ SourceOutcomeId        // weak reference — see §11
```

This deliberately protects a future feature without building it now: if Glen later learns the true cause (a road closure, not indifference), that's a belief update through the ordinary Knowledge pipeline against an *already-formed* attribution — not a retroactive rewrite of the original `CommitmentOutcome`. The event doesn't change; its meaning to Glen can. Not built in this slice. Not foreclosed by it.

---

## 4. V0 Outcome / Cause Model

Four outcomes, four causes, with an explicit valid-pairing table — cross-checked against the conflict-decision brief, which already defines `Relinquished` specifically as a conflict-resolution outcome distinct from generic cancellation:

| Outcome | Valid Cause(s) |
|---|---|
| `Fulfilled` | — (success case; no cause needed) |
| `Relinquished` | `ConflictResolution` only |
| `Missed` | `WindowExpired` only |
| `Cancelled` | `ExternalCancellation` or `ExplicitCancellation` |

And the `AuthoritativeCause → KnownAttribution` mapping, stated explicitly rather than left inferable:

| AuthoritativeCause | PerceivedCause (KnownAttribution) | Notes |
|---|---|---|
| `ConflictResolution` | `RelinquishedByActor` | `ResponsibleActor` known; the competing reason stays unknown to the stakeholder unless separately disclosed |
| `ExplicitCancellation` | `RelinquishedByActor` | Deliberately collapses to the same stakeholder-facing signal as above — v0 does not distinguish "chose something else" from "just decided not to" from the stakeholder's perspective. Stated choice, not an accident. |
| `ExternalCancellation` | *(no negative attribution toward the actor)* | Counterparty- or world-initiated; the commitment's other party isn't at fault |
| `WindowExpired` | `MissedWindowExpired` | `PerceivedCause` starts `Unknown` to the stakeholder until/unless the reason is later disclosed |

`InitiatingActor`, `ResponsibleActor`, and `SourceDecisionId` remain part of `AuthoritativeCause`'s supporting data, not part of `KnownAttribution` — they inform the accountability policy's resolution logic (§7) but are never exposed to the stakeholder's belief state directly.

### 4.1 Interference is a named future dependency

The locked four-cause model above does not yet have a slot for the Observer physically preventing a
character from fulfilling or attending a Commitment.
[`../Product/CoreIdentity.md`](../Product/CoreIdentity.md) §4–5 and §8 define that case (`Interference`)
as distinct from ordinary cancellation, and [`../Architecture/Reference.md`](../Architecture/Reference.md)
§59 requires the distinction between chosen intent, attempted execution, and forced outcome to survive
into later Knowledge and attribution. This brief's "cause taxonomy beyond the locked four values" (§14)
is where that distinction will land: most likely a fifth `AuthoritativeCause` (for example
`ExternalInterference`), with its own `KnownAttribution` mapping that can differ by witness — Core
Identity's own example has Priya seeing the interference while Glen only knows Mina never arrived, which
is a materially different Knowledge case from today's single `ExternalCancellation` "no negative
attribution" mapping.

Not built in this slice. Scope it explicitly into the Phase 9 `InterferenceAndObserverBrief.md`
(see [`../Product/RoadmapPhases.md`](../Product/RoadmapPhases.md)) rather than rediscovering it mid-implementation.

---

## 5. Stakeholders

```text
StakeholderRef
├─ EntityRef      // {EntityKind, RuntimeId} — reuses the entity-lifecycle primitive
│                 // already locked in the core architecture brief
└─ Role           // Counterparty | Beneficiary | Authority | Participant
```

Reusing `EntityRef` rather than a bare `CharacterId` costs nothing now and avoids painting the Commitment structure into a corner before Employment introduces non-Character stakeholders (a manager, eventually an Organization). The v0 accountability handler simply states: *directional social consequences apply only when a stakeholder resolves to a Character* — no institutional-relationship subsystem required yet.

**Default:** for a simple two-party social commitment, the other participant defaults to `Counterparty` unless the `CommitmentDefinition` explicitly overrides it. This mirrors the same "cheap procedural default, authored exceptions where they matter" pattern already used for preference-matrix seeding and reusable-vs-local Considerations elsewhere in this project.

---

## 6. Accountability Policy Authoring

Consequences are authored as a matching/fallback policy, not an exhaustive `Outcome × Cause × Role` table:

```text
CommitmentAccountabilityPolicy
├─ Default:            ConsequenceSet
├─ ByOutcome:           { Outcome → ConsequenceSet }
├─ ByRole:              { Role → ConsequenceSet }
└─ SpecificOverrides:   { (Outcome, Role[, Cause]) → ConsequenceSet }
```

**Broad default first; narrow authored exceptions second.** The most specific applicable rule wins; unspecified fields fall back toward `Default`. This is the same discipline already used for Considerations (reusable by default, decision-local where the reasoning genuinely doesn't generalize) — worth naming here as a recurring authoring pattern across the project, not a one-off invention.

The policy is snapshotted onto the materialized Commitment at authoring time (protected against hot reload, per the core architecture's in-flight-entity rule) — but the *evaluation* of that policy against current relationship/evidence state happens at transition time, against then-current runtime circumstances, not frozen values.

---

## 7. Consequence Types — Three Distinct, One Canonical Application

```text
ConsequenceSet
├─ Memory?                 // "what happened between us" — template + retention tier
├─ EvidenceContribution?   // "what I infer about what kind of person you are" —
│                          //   feeds the standard ObservedSocialEvidence pathway,
│                          //   including its joint/correlated-evidence update discipline
└─ ChannelDelta?           // "how our relationship changed" — a direct dyadic-history
                           //   channel modifier (Trust, Resentment), reserved for
                           //   salient events (see §8)
```

A single `CommitmentOutcomeConsequenceHandler` reacts to the lifecycle event, resolves the applicable `ConsequenceSet` via §6's policy, and applies all three effects exactly once — checking `CommitmentOutcomeId` for idempotency before applying (§2). No separate handler independently reinterprets the same memory into a second channel mutation.

`EvidenceContribution` is not a parallel mechanism invented for this slice — it *is* an `ObservedSocialEvidence` record, ingested through the existing evidence pathway built specifically to avoid naively double-counting correlated evidence across shared belief dimensions.

---

## 8. Routine Fulfillment Routes Through Evidence, Not Direct Channel Mutation

Locked correction from round three: ordinary reliability is not primarily a `Trust`-accumulation problem. "Mina reliably shows up" is evidence about her Dependability — and the belief-update machinery already gives this the right shape for free.

- **Ordinary `Fulfilled` outcomes:** `EvidenceContribution` only (small magnitude, Dependability-relevant). No `Memory`. No `ChannelDelta`. The Reliance appraisal lens reflects the accumulating evidence *live*, computed fresh from the current belief distribution each time it's evaluated — not from a separate accumulator. The underlying belief update's diminishing-returns shape (repeated identical evidence narrows an already-narrow belief less than it narrows an uncertain one) gives the right saturation behavior with zero extra mechanism.
- **Salient breaches** (`Relinquished`, `Missed`, `Cancelled` — particularly against a `Counterparty` on a meaningful commitment): may carry `Memory` and a direct `ChannelDelta` in addition to `EvidenceContribution`. This is where Trust/Resentment as discrete dyadic-history modifiers belong.

This keeps Trust from becoming an unbounded meter incremented daily by routine success, and correctly separates two things that share a word: dyadic-history *Trust* (a discrete modifier from named events) and live-computed *Reliance* (a continuous appraisal lens reflecting accumulated belief). They are not the same field.

---

## 9. Observation Timing

For this slice, lock a simple explicit rule rather than building disclosure/notification machinery:

> **Stakeholder standing (§5) is itself sufficient observation status for a commitment's own outcome.** Direct accountability stakeholders observe explicit `Fulfilled`/`Relinquished`/`Cancelled` outcomes immediately — no spatial/social witness detection required, since being owed the obligation is what makes the stakeholder aware. `Missed` becomes observable when the fulfillment window closes.

Evidence and memory production route through the existing observation/Knowledge machinery from that point forward — the stakeholder isn't granted omniscient access, just immediate awareness of the outcome they were specifically owed.

---

## 10. Retention and Provenance

`CommitmentOutcome` defaults to Ephemeral retention, same as other lifecycle events. But durable artifacts it produces (a Significant/Legacy-tier `RelationshipMemory` like "abandoned my wedding") may outlive it. `SourceOutcomeId` gets the same weak-reference treatment already established for `DiscoverySource`: durable artifacts must carry enough denormalized descriptive data to remain meaningful independent of whether the outcome record itself has been pruned. The provenance chain ("why does Glen distrust Mina → outcome X → Decision Y") should degrade gracefully, not break, when the outcome record ages out.

---

## 11. Event Settlement Ordering

Accountability consequences must settle before anything that could evaluate the *social/belief state they affect* — but the rule is scoped to that dependency, not to "everything downstream of this event":

> No Decision generation whose Considerations could bind to social or belief state affected by this outcome may evaluate before that state's settlement completes.

This is deliberately narrower than "nothing downstream proceeds until accountability settles." Mina's own Activity/Travel replanning (freeing the time slot the relinquished commitment occupied) has no dependency on Glen's trust in her and shouldn't be forced to wait behind accountability settlement just because both trace back to the same lifecycle event.

This is a genuine integration test for the same-instant settlement architecture: `lifecycle transition → accountability consequences → resulting reactions → replanning/choice generation that actually depends on the changed state → quiescence`.

---

## 12. No Global Reputation

This outcome affects Glen because Glen was involved — not the settlement at large. If Priya later learns Mina bailed on Glen, that happens because Glen tells her, or she observes something, or a later reputation-propagation system (deliberately out of scope here) carries it — never because a global reputation scalar updated automatically. Consistent with the bounded second-order-belief design already locked in the Social Model brief.

---

## 13. Completion Test

Not "a number changed" — a proven counterfactual, with save/load integrity on top:

```text
SAME INITIAL SNAPSHOT
       │
       ├── Timeline A: Mina keeps dinner
       │        → later Glen Decision involving Mina → Influences = X
       │
       └── Timeline B: Mina relinquishes dinner (conflict resolution)
                → Glen observes outcome (§9)
                → accountability applies (§6–8)
                → Glen's belief/Reliance updates
                → later, same Glen Decision → Influences = Y

Assert: X != Y, for the specific expected semantic reason
        (e.g. a "she's been reliable" influence weakens or a
        new "she let me down before" influence appears)

Within Timeline B:
    save before the original conflict
    reload
    replay
    → exact Y again, including the later Influence
```

---

## 14. Vertical Slice Scope

**In scope:**

- `CommitmentLifecycleService` as sole mutation authority, emitting `CommitmentOutcome` with the locked 4-outcome/4-cause model and pairing table (§4).
- `AuthoritativeCause → KnownAttribution` mapping (§4), enforced — no handler reads `AuthoritativeCause` for social consequence.
- `StakeholderRef{EntityRef, Role}` with the two-party default (§5).
- `CommitmentAccountabilityPolicy` with the Default → ByOutcome → ByRole → SpecificOverrides fallback (§6).
- Three-way consequence split with one canonical, idempotent handler (§7).
- Routine-fulfillment-via-evidence, not channel mutation (§8).
- Stakeholder-standing observation timing (§9).
- Scoped settlement ordering (§11).
- Full counterfactual + save/load completion test (§13).

**Explicitly deferred:**

- Economy, Employment, and institutional stakeholders beyond the `EntityRef`/`Character`-only v0 handler.
- The Glen-learns-the-truth-later reassessment loop (protected against by §3, not built).
- Generic/templated effects authoring language.
- `Defer` semantics (still empty per the conflict-decision brief).
- Global reputation propagation.
- Broader n-way conflict planning.
- A cause taxonomy beyond the locked four values.

---

## 15. Invariants (this brief)

1. All Commitment lifecycle mutation goes through `CommitmentLifecycleService`; no direct status-setter calls elsewhere.
2. Every lifecycle transition allocates one `CommitmentOutcomeId`; all resulting artifacts (`Memory`, `Evidence`, `HistoryEntry`) reference it, and handlers check-and-set against it before applying effects.
3. `AuthoritativeCause` is never read directly by social-consequence logic. Stakeholder-facing reasoning uses only `KnownAttribution`, derived through the locked mapping.
4. `Outcome × Cause` pairings are restricted to the table in §4; `Relinquished` pairs only with `ConflictResolution`.
5. Stakeholder roles default sensibly for simple commitments (two-party → `Counterparty`) without requiring per-commitment authoring.
6. Accountability policy resolution follows Default → ByOutcome → ByRole → SpecificOverrides, most-specific-wins.
7. The accountability policy is snapshotted at materialization (hot-reload safe); its evaluation against relationship/evidence state happens at transition time against current runtime values.
8. Memory, Evidence, and Channel effects are conceptually distinct and applied through exactly one canonical handler per outcome.
9. Ordinary `Fulfilled` outcomes produce `EvidenceContribution` only; direct `ChannelDelta` (Trust/Resentment) is reserved for salient outcomes.
10. Reliance is computed live from belief state, never a separately maintained accumulator duplicating what evidence-driven belief update already provides.
11. Stakeholder standing alone is sufficient observation status for a commitment's own outcome; `Missed` becomes observable at window expiry.
12. Decision generation whose Considerations bind to affected social/belief state waits for accountability settlement; unrelated replanning does not.
13. No character outside direct stakeholder/observation status changes state as a result of an outcome it wasn't party to.
14. Durable artifacts referencing a `CommitmentOutcome` carry enough denormalized data to remain meaningful if the outcome record itself is later pruned.
15. `CommitmentOutcome` records are immutable once created; later world changes never rewrite them, only produce new outcomes or belief updates.

---

## 16. Guiding Note

This is the slice where history starts being causally load-bearing rather than cosmetic. Mina stops being a bundle of current stats and becomes someone Glen specifically remembers being let down by — with the exact right amount of epistemic humility built in, since Glen's version of events and the simulation's version are deliberately allowed to diverge and only reconcile through the same Knowledge machinery everything else in this project already goes through.

The protected-but-undbuilt reassessment loop (§3) is worth keeping in mind while implementing, not because it needs to work now, but because it's the clearest sign this slice is wired correctly: if "Glen later learns the road closed" turns out to be a small, natural addition once the rest is in place, the Truth/Attribution separation held. If it turns out to require touching `CommitmentOutcome` itself, something upstream leaked authoritative cause into stakeholder belief after all.

And once this lands, Employment stops being a scheduler curiosity — showing up to work becomes an obligation someone can come to trust you to fulfill, with all of this machinery already built to receive it.
