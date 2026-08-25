# Vivarium Decision Interventions

**Status:** Locked mechanic shape; availability and balance are authored tuning  
**Last reconciled:** 2026-08-25  
**Scope:** Player actions that alter a Decision's dice without selecting its Option

This brief distinguishes the intervention families Phase 3 must support. It supplements the MVP
economy in [`../Product/PlayerAgencyBrief.md`](../Product/PlayerAgencyBrief.md); it does not turn every
intervention into a Nudge cost or introduce physical Interference.

## 1. Shared rule

All Decision interventions are validated Commands against a live Decision and, where applicable, a
stable `DecisionInfluenceId`. One authoritative rules evaluation owns timing, target eligibility,
availability/cost, stacking, and the reason a command is unavailable. Projections display that same
result.

An intervention may make one die deterministic or extremely strong. It still changes a die rather
than naming the winning Option: the normal signed resolution policy combines every participating roll
and owns the outcome.

## 2. Intervention families

| Family | Timing | Effect |
|---|---|---|
| Emphasize | Before rolls | Step one visible active Influence die up once |
| Temper | Before rolls | Step one visible active Influence die down once |
| Re-roll | After the target roll is known, before outcome commitment | Discard that result and roll the same effective die again using its next deterministic roll index |
| Substitute | Before the target roll | Replace one Influence's effective die with an authored die variant for this Decision resolution |

Substitution is replacement, not another step on the ordinary die ladder. A variant may change size,
face distribution, or be deterministic—for example, replacing a `d4` with a `d12`, or using a loaded
die whose only result is `20`. Variant definitions and their provenance are authoritative content;
the command never accepts an arbitrary client-supplied face table or result.

## 3. Timing and resolution lifecycle

Emphasize, Temper, and Substitute apply while the Decision is still open and before its dice have been
rolled. They remain attached to stable Influence identity across living reevaluation under the
existing reconciliation rules. A replacement controls the effective die at resolution; it does not
rewrite the world-derived base reason.

A meaningful Re-roll requires the first result to be known. Resolution therefore needs an
authoritative, bounded pre-commit state for player-attended Decisions:

```text
Open → RollsProducedAwaitingCommit → Committed/Resolved
                         └─ Re-roll target → replace result → AwaitingCommit
```

Entering this state freezes the resolution input snapshot and produced rolls. World drift cannot
rewrite them while the player considers a re-roll. Unrelated world simulation is not implicitly
paused; ordinary simulation pause remains a separate time control. A deadline or other authored
expiry must be able to commit the pending result without player input so unattended Decisions never
block the world.

The implementation may choose semantic commands such as `BeginDecisionResolution`,
`ApplyDecisionReroll`, and `CommitDecisionResolution`; exact API names are not locked here. It may not
implement a re-roll by resolving, undoing consequences, and resolving again.

## 4. Determinism, persistence, and history

- Every produced roll records its stable random scope, purpose, and roll index.
- A re-roll of one Influence advances only that Influence's roll stream; unrelated rolls do not move.
- Save/load preserves a pending resolution snapshot, all produced and superseded roll evidence, and
  applied pre-roll interventions.
- Historical explanation identifies the effective die, the accepted result, and any superseded
  re-roll result without presenting the discarded result as causal.
- Dissolution before outcome commitment refunds any refundable spend/charge under its owning resource
  policy. Committed or resolved interventions are not refundable.
- Offline catch-up cannot wait for a player re-roll. It commits through the ordinary unattended path
  and does not consume player-only availability.

## 5. Resource policy is separate from effect

`Nudge` is one resource policy, not the type system for all interventions. Phase 3 must allow an
intervention definition to declare its own availability policy and cost so the rules engine does not
hard-code every action as `Nudge cost = 1`.

The MVP already locks one-Nudge costs for Emphasize and Temper. The additional values below are
authored tuning knobs. Phase 3B needs explicit initial values so behavior is deterministic and
testable, but those values are expected to change through playtesting without changing the mechanic:

- the Re-roll refresh boundary (once per world day or another deterministic period);
- whether unused Re-rolls bank, and the cap;
- how replacement dice are granted, consumed, retained, and refunded;
- the initial authored replacement-die catalog;
- whether one Decision may receive both a substitution and a re-roll, and the per-Influence/per-Decision
  stacking limits;
- the bounded duration/expiry of `RollsProducedAwaitingCommit`.

The temporary player-facing name is **Re-roll**. Do not introduce a second branded currency such as
Hero Point, Inspiration, or Fortune unless later product direction calls for one. Tuning belongs in
validated content/configuration rather than hard-coded branching, while authoritative current
availability and any consumed holdings remain save state.

### Initial Phase 3B tuning

The first implementation uses these deliberately revisable authored/configured values:

- Re-roll starts at one charge, caps at one, and refreshes by one after each world-day period; unused
  availability does not bank.
- The pending-roll window lasts 15 world minutes.
- Replacement dice start with one persistent charge and do not automatically refresh. The first
  catalog entry is a loaded d20 whose fixed result is 20.
- Each intervention definition may target an Influence once. Substitute and Re-roll may affect the
  same Influence; accepted order is Substitute before rolls, then Re-roll after rolls.

These are testable defaults, not newly locked balance decisions.

## 6. Required proofs

1. A pre-roll substitution targets a stable Influence, survives save/load, and uses the authored
   variant rather than its base die at resolution.
2. A known initial roll can be re-rolled before commitment; the accepted result uses the next scoped
   roll index and replays exactly.
3. Save/load between the initial roll and re-roll produces the same accepted result and history.
4. Expiry and OfflineCatchUp commit without consuming a Re-roll and without blocking unrelated lives.
5. Invalid timing, hidden/ineligible targets, insufficient availability, duplicate use, and stale
   commands mutate and spend nothing.
6. Even a deterministic replacement die changes only its roll; the normal resolution policy still
   owns the winning Option.
