# Vivarium — Commitment-Conflict Decision Brief

**Status:** Addendum — locks the design for the second vertical-slice Decision
**Depends on:** Management-Sim-Architecture-Brief.md (core simulation), Vivarium Social Model Brief (Knowledge/Appraisal), Considerations/Decision-Reasoning discussion (SignalField, parameter-bound Considerations)
**Scope:** This document does not re-derive the core architecture. It records what's been decided specifically for the commitment-conflict Decision, including two items resolved in this round: joint (not pairwise) feasibility, and Decision Dissolution as a formal outcome.

---

## 1. Decision Goal

Not:

> "When two commitments become incompatible, decide which commitment to honor."

Instead:

> **"When the current set of commitments becomes jointly infeasible, generate a Decision among valid commitment-resolution plans. Resolution changes commitment intent — preserve, defer, or relinquish — not commitment fulfillment. Normal planning then determines Activities and Travel."**

Choosing a plan is not honoring a commitment. A preserved commitment remains `Planned` until the character actually fulfills it through ordinary Activity execution.

---

## 2. Commitment Resolution Plans

Options are resolution plans, not individual Commitment IDs:

```text
CommitmentResolutionPlan
├─ PlanId
├─ Preserve[CommitmentId]
├─ Defer[CommitmentId]
└─ Relinquish[CommitmentId]
```

The first slice generates exactly two plans for a two-commitment conflict (Preserve A / Relinquish B, and the reverse), with an empty `Defer` set. The type is shaped for `Preserve {A, B} vs. Preserve {C}` from day one — that costs nothing now and avoids a painful refactor once three-way conflicts appear.

---

## 3. Joint Feasibility, Not Pairwise Union

`CommitmentFeasibilityService` evaluates whether a *set* of commitments is jointly satisfiable given windows, location, duration, and travel — not whether every pair in the set is individually compatible.

This distinction is load-bearing: three commitments can each be pairwise compatible (A fits with B, B fits with C, A fits with C in isolation) while the full set of three is infeasible together. A pairwise-then-cluster approach cannot detect this class of conflict at all. The service must test genuine combinations, and the resolution plans it returns must reflect real joint validity, not an artifact of pairwise unioning.

**Scale rule:** joint feasibility is not recomputed by scanning a character's entire commitment set on every planner pass. Re-evaluation is triggered incrementally by aspect-scoped revision changes on the specific commitments, windows, or travel-network state a character's active set actually depends on (per the core architecture's revision-dependency discipline). Clustering groups commitments transitively through actual joint-infeasibility relationships, not proximity or naive pairwise adjacency.

---

## 4. Feasibility vs. Reasoning Stay Separate

- **Feasibility** (`CommitmentFeasibilityService`) determines which plans remain valid, and produces `LatestResolutionAt`.
- **Considerations** rank the valid plans.

Travel duration can inform both — a `TravelBurden` Consideration can oppose a plan while impossibility of arriving in time removes the Option entirely. Impossibility never becomes an opposing die; it removes the Option before reasoning ever sees it.

---

## 5. Conflict Identity

```text
ConflictKey
├─ CharacterId
├─ ParticipatingCommitmentIds[]     // canonical sorted order
└─ ConflictInstanceRevision         // the feasibility/schedule revision that produced this instance
```

A derived active-conflict index, keyed by `ConflictKey`, prevents repeated planner passes from spawning duplicate Decisions and is rebuilt after load like other reconstructible indexes. If the same commitments conflict again later after being rescheduled, that is a **new** conflict instance (new `ConflictInstanceRevision`), not a reopening of the historical Decision.

---

## 6. Resolution Deadline

`LatestResolutionAt` is derived, not fixed at generation. It changes when its inputs change (travel time increases, a window shifts), the same way any analytically-derived, revision-dependent quantity does elsewhere in the architecture.

- A real scheduled event (`AutoResolveConflictDecision`) is registered against the current deadline, carrying revision dependencies on the inputs that produced it.
- When those inputs change, the existing cancel/reschedule machinery moves the deadline correctly rather than firing stale or silently going unrecalculated.
- A player Hold cannot cross `LatestResolutionAt`. Offline catch-up that spans the deadline resolves the Decision autonomously at the correct simulation instant, then continues — never retroactively decided using later world state.

**Recommendation, not required for v0:** deadline proximity should eventually feed into held-decision priority/importance rather than sitting beside it as an unrelated backstop, so a conflict doesn't sit at low visible priority for most of its life and then jump straight to forced auto-resolution with no warning.

---

## 7. Consequence Flow

Resolution does not directly call planner/Activity code:

```text
Decision resolves
     ↓
Preserved commitments: remain Planned
Sacrificed commitments: marked Relinquished (distinct from generic cancellation)
     ↓
Commitment domain events published
     ↓
Routine Planner reacts
     ↓
chooses next Activity (Travel becomes primary Activity if needed)
```

The Decision states what the character chose. The planner determines how that choice gets executed. `Relinquished` is a distinct commitment status from ordinary cancellation, since it carries different narrative/history meaning (a commitment given up under conflict, not one that simply ended).

---

## 8. Multi-Target Consideration Binding Within a Plan

A single Option (plan) can involve more than one relevant target — Glen via the preserved commitment, Priya via the relinquished one. The parameter-binding schema from the Considerations design must support a Consideration firing once per relevant commitment/target inside a plan, not once against a single bound target:

```text
GO TO DINNER (Preserve Dinner, Relinquish Rehearsal)

  SocialCostOfBreakingCommitment(target: Glen)      → "I promised Glen I'd be there"      d8
  SocialCostOfBreakingCommitment(target: Priya)     → "I'll let Priya down at rehearsal"   d8
  TravelBurden(plan: this)                          → "It's a long trip"                   d4
```

ReasonChannel consolidation operates within a single `(Consideration, bound target)` evaluation — it must not merge distinct targets/commitments in the same plan into one misleading line, even when thematically similar (both above are "social cost," but Glen and Priya are separate people and separate dice).

---

## 9. Historical Integrity

If a resolved Decision's chosen plan later becomes impossible (a road closes after Dinner was chosen), the original Decision is never rewritten. The character genuinely chose Dinner given the world that existed then. The later change produces new planner fallout — a failure, or a fresh Decision — never a retroactive edit of the historical explanation.

---

## 10. Decision Dissolution

### 10.1 The general pattern

This is the same principle as scheduled-event invalidation (§11.1 of the core brief), applied one level up. A `ScheduledEvent` is revalidated at execution and discarded if the world no longer supports it. A Decision, similarly, can have its Option set invalidated by a feasibility change while it's still open — and needs the same kind of formal "this is no longer a real choice" outcome, rather than being silently mutated or left to resolve against a stale Option set.

**Recommendation:** model this as a general Decision-lifecycle capability (an optional validity/dissolution dependency on any Decision, analogous to a `ScheduledEvent`'s `Dependencies[]`), with commitment-conflict as the first concrete Decision type that populates it. This costs little now and avoids rediscovering the same need when a future Decision type (e.g. a job offer withdrawn before the character responds) needs it too.

### 10.2 Trigger

When a joint-feasibility re-evaluation (§3) determines that an open (`Active` or `Held`) commitment-conflict Decision's `CandidatePlans` no longer reflect reality — a plan became infeasible, the conflict's participating commitments changed, or the conflict resolved itself outside the Decision (e.g. one commitment was cancelled for an unrelated reason) — the Decision **dissolves** rather than being mutated in place or left stale.

### 10.3 Effects

- **Terminal status:** `Dissolved`, distinct from `Resolved`. No consequence pipeline executes. No commitment state changes as a result of the dissolution itself.
- **Intervention refund:** any resource already spent on the dissolved Decision is unconditionally returned to the player. The world changing out from under a reasonable choice is not the player's fault.
- **Held-capacity release:** if the Decision was Held, its capacity slot releases immediately (§20 of the core brief).
- **Domain event:** `DecisionDissolved` (`DecisionId`, `Reason`, `InterventionsToRefund[]`, `DissolvedAt`) settles through the standard deterministic domain-event cascade (§11.4). If the underlying commitments still form some conflict, a fresh Decision (new `ConflictKey`, per §5) may generate as part of the **same** same-instant settlement, not a later tick. If no conflict remains, the character simply resumes normal planning.
- **Notification:** surfaced through the existing Attention/Notification path, not silently dropped — a Held or Watched decision going away should produce a recap entry the same way an auto-resolved one does.
- **Retention:** dissolved Decisions default to `Ephemeral` retention (§37 of the core brief) — no world state changed as a result, so there's little reason to keep a heavyweight historical record, though the diagnostic trace still captures the dissolution event normally.
- **Command race, already handled:** a command referencing a Decision that dissolved between UI display and command execution fails the existing "does this Decision exist / is it unresolved" validation — no new mechanism required.

### 10.4 Open question, not blocking

Repeated dissolve/regenerate cycles for the same underlying conflict (e.g. rapidly oscillating travel conditions) aren't guarded against by anything specific — the same-instant cascade guard (§11.4) covers runaway behavior *within* one instant, not thrashing *across* several. Worth a cooldown/escalation policy if it ever shows up in practice; unlikely to matter for the v0 slice and not worth solving speculatively.

---

## 11. Vertical Slice Scope

**In scope for v0:**

- Exactly two commitments, two candidate plans (Preserve A/Relinquish B and its reverse), empty `Defer` set.
- `CommitmentFeasibilityService` with a real (if simple, given only two commitments) joint-feasibility check — the *interface* must be shaped for n-way evaluation even though v0 never exercises more than two.
- `ConflictKey` deduplication and index rebuild after load.
- `LatestResolutionAt` as a real scheduled, revision-dependent deadline.
- Dissolution, including intervention refund and notification — this is a plausible outcome even in a two-commitment slice (e.g. one commitment gets independently cancelled while the Decision is Held) and isn't worth deferring.

**Explicitly deferred:**

- Actual `Defer` behavior and changing-road revalidation scenarios.
- Full n-way clustering algorithm tuning.
- Intervention resource economy mechanics (the refund *rule* is locked; the resource *system* it refunds into is not).
- Dissolve/regenerate thrash protection.
- Generalizing dissolution triggers to Decision types beyond commitment-conflict (the shape is recommended now; only this one type uses it).
- Recurring-commitment semantics for Preserve/Defer/Relinquish (which occurrence vs. the recurring commitment itself).

---

## 12. Diagnostics

Add Decision dissolution as a trace element alongside stale-event discards (§53 of the core brief) — it's architecturally the same kind of thing, one level up:

```text
Day 14 15:58
DecisionDissolved
Decision: 2214
Reason: OptionSetInvalidated
Refunded: 1 intervention
Regenerated: Decision 2231 (new ConflictKey)
```

---

## 13. Invariants (this brief)

1. `CommitmentResolutionPlan` is the Option payload for commitment-conflict Decisions; individual Commitment IDs are never used as Options directly.
2. `CommitmentFeasibilityService` evaluates true joint feasibility over candidate commitment sets; pairwise-only evaluation is insufficient and prohibited as the sole mechanism.
3. Feasibility re-evaluation is triggered by aspect-scoped revision changes, not full-set rescans every planner pass.
4. Feasibility (which plans are valid) and reasoning (which valid plan is preferred) remain separate; infeasibility never appears as an opposing die.
5. `ConflictKey` prevents duplicate Decision generation across planner passes and is rebuilt after load.
6. `LatestResolutionAt` is derived and revision-dependent; a Hold cannot cross it; offline catch-up resolves at the correct simulation instant.
7. Decision consequences mutate Commitments (`Planned` / `Relinquished`) via domain events; the planner reacts to schedule Activities/Travel. The Decision handler never calls planning/Activity logic directly.
8. A Consideration may bind to and evaluate multiple targets within a single Option/plan; ReasonChannel consolidation never merges across distinct targets.
9. Resolved Decisions are never rewritten by later world changes; later infeasibility produces new planner fallout or a new conflict instance.
10. A Decision whose Option set is invalidated by a feasibility change transitions to a distinct `Dissolved` terminal status — never silently mutated, never left to resolve against a stale Option set.
11. Dissolution is not resolution: no consequence pipeline runs, and any spent intervention is unconditionally refunded.
12. Dissolution releases held-capacity immediately and is surfaced through the standard Notification path.
13. Dissolution is a domain event settling through the standard same-instant cascade; a resulting fresh Decision, if any, settles within the same instant.
14. Dissolved Decisions default to Ephemeral retention.

---

## 14. Guiding Note

This Decision is a good second vertical slice precisely because it stresses the architecture differently than the hunger/social examples: generation from schedule/commitment infeasibility rather than a Need threshold or social trigger, genuinely multi-option reasoning over plans rather than binary favor/oppose, a real deadline with offline implications, and now a formal "this choice stopped being real" outcome. That last piece — Dissolution — is worth keeping as a general capability rather than a one-off, since autonomous people's plans colliding with a changing world is exactly the kind of thing that will recur well beyond commitments.
