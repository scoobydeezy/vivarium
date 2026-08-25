# Vivarium Product Roadmap

**Status:** Current product sequence and next-task authority  
**Last reconciled:** 2026-08-24

This document answers: **given the implemented simulation, what should be built next?** The product
north star is [`CoreIdentity.md`](CoreIdentity.md). Detailed phase
requirements and acceptance assertions live in [`RoadmapPhases.md`](RoadmapPhases.md). Capability
evidence lives in [`../ImplementationStatus.md`](../ImplementationStatus.md). Neither replaces the
architectural contract.

## Product goal

Build a minimum playable Vivarium in which approximately 8–12 characters live complete,
understandable routines across multiple days. Their Activities, Travel, Needs, obligations, social
context, Knowledge, and history create meaningful Decisions. The player can follow those lives,
inspect what they know, and alter circumstances without directly choosing character outcomes.

All behavior must use production-shaped, deterministic, persistent systems shared by the headless and
Unity paths. Small content is encouraged; disposable parallel architecture is not.

The MVP proves the individual-scale foundation of Core Identity: knowing autonomous people, shaping
circumstances, applying bounded influence, and seeing history change later reasons. Later phases expand
that same foundation into physical interference, human beliefs about the Observer, emergent society,
multiple habitats, and reciprocal autonomy.

## Current gate

**Phases 0–2 are complete; Phase 3 is next.** The jointly locked
[`Minimum Playable Scenario`](MinimumPlayableScenario.md) and
[`Player Agency Brief`](PlayerAgencyBrief.md) define the small world, meaningful character Decisions,
supported player actions, and required Unity surfaces.

The complete locked Need set now drives ordinary life through production routines: Energy rests,
Hunger eats, Recreation selects available hobbies, and Social pressure selects bounded shared-context
company, and the five required circumstance branches now use production-shaped Decisions. The
immediate next work begins player agency as a real game system:

> **The authoritative Nudge economy.**

Observable completion test:

> A world begins with three Nudges, spends them only through valid Emphasize/Temper interventions,
> regenerates one at each eight-hour world-time boundary without banking above the cap, refunds a
> dissolved Decision's interventions, and projects the same eligibility/cost state across live,
> offline, and save/load continuation.

The Ordinary Hunger slice is complete: an authored satisfaction routine now turns analytical Hunger
into Eating only at an explicit, reachable location Activity affordance. It carries snapshotted meal
effects through Travel, applies consumption on completion, and resumes fallback planning identically
live, offline, and across save/load. Work-context eligibility preserves Mina's Need-vs-obligation
Decision rather than inventing a Bakery meal break. The focused
[`Decision Importance brief`](../Design/DecisionImportance.md) places one small derived-importance
foundation ahead of Recreation so discretionary alternatives do not introduce a second, routine-only
choice model. Recreation now proves that architecture end to end: a runtime-ID-free preflight scores
reachable Tabletop Games and Reading affordances from Interests, ordinary instances execute directly,
and unusually important instances adopt the exact preflight reasons into the compiled Decision pipeline.
Ordinary Socializing now completes that loop: active Social pressure waits for an actual co-located
counterpart at a Socializing affordance, selects through the bounded interaction-candidate path, leaves
the counterpart's primary Activity untouched, and reuses ordinary interaction and Need consequences.
The first Phase 2 social branch is also complete: that bounded Social routine can invite a co-located
character who is already pursuing an explicitly authored discretionary plan. The recipient's compiled
Join/Keep Decision combines belief-relative interpersonal appraisal, shared-context availability, and
the value of the existing plan; targeted belief or Activity changes reevaluate stable reasons.
Acceptance interrupts the snapshotted plan through the authoritative Activity transition path, while
refusal or invalidated context preserves it, with frozen explanation and save/load equivalence.
The Energy continuation branch is complete as well: authored Recreation and Social Activities may
surface a preflighted Rest/Continue choice at low Energy. Ordinary fatigue rests without allocating a
Decision, while meaningful competing Interest adopts the exact compiled reasons. Continue preserves
the exact Activity and rearms Energy only at the next strictly lower authored threshold; the finite
threshold sequence ends in automatic rest, preventing same-threshold retrigger or unwatched reserve
state. Need and Activity revisions reevaluate only their dependent living reasons, and the choice and
continuation remain equivalent across save/load.
The final Phase 2 branch is complete without a second conflict implementation: two independently
feasible Employment patterns at different workplaces can materialize ordinary Commitments whose
non-overlapping windows become jointly infeasible only after real Travel is included. Their normal
schedule revisions generate the existing compiled Preserve/Relinquish Decision—without a
`CommitmentBecomesKnown` scenario event—and resolution records a provenance-linked Relinquished
outcome identically offline and across save/load.

## Ordered phases

| Phase | Outcome | Status |
| --- | --- | --- |
| 0. Lock playable intent | MPS and Player Agency briefs define one coherent playable world | Complete |
| 1. Close the daily routine loop | Energy/Sleep/Wake, Employment obligations, ordinary Eating, discretionary Recreation, and Socializing support indefinite lives | Complete |
| 2. Expand meaningful choice | Scenario-required branch points use compiled Considerations and production consequences | Complete |
| 3. Complete player agency | Nudge economy and Commons availability become real Commands; interactive Activity stays deferred | Pending |
| 4. Build the small-cast world | 8–12 characters run 2–3 days through one durable headless acceptance scenario | Pending |
| 5. Build playable Unity surfaces | Roster, character, Decision, schedule, Knowledge, world, and history views consume projections/Commands | Pending |
| 6. Productize save/continue | Concrete storage, restart continuation, offline catch-up, migration diagnostics, and failure UX | Pending |
| 7. MVP hardening gate | The small cast is legible, causal, replayable, persistent, and remains within scale gates | Pending |
| 8. Relationship-memory longevity | Post-MVP consolidation retains defining memories and compacts ordinary history deterministically | Pending, post-MVP |
| 9. The Poke | Preserve intent versus forced outcome; physical interference becomes observable history and Observer evidence | Pending, post-MVP |
| 10. Performed personhood | Specific habits and relationships visibly change routine, opportunity, and option space | Pending, post-MVP |
| 11. Habitat and AGI foundation | One Habitat gains durable identity; AGI philosophy creates founding pressures rather than Culture presets | Pending, post-MVP |
| 12. Community into society | Norms, status ideals, institutions, and narratives emerge from individual state and group behavior | Pending, post-MVP |
| 13. Multiple habitats | Forced transfer and voluntary migration remain distinct while characters carry identity and history | Pending, post-MVP |
| 14. Contact and reciprocal autonomy | Inter-habitat contact, Observer inquiry, collective action, and renewable pressures remain person-first | Pending, post-MVP |

See [`RoadmapPhases.md`](RoadmapPhases.md) for phase-level completion tests and the complete acceptance
matrix.

Phases 9–14 require focused briefs before implementation. Core Identity locks their experiential and
architectural obligations, not their exact data models or tuning.

## Earliest routine links

The locked Phase 0 review establishes this order, subject to revalidation against code after each
slice:

1. Energy → Sleeping → waking → replanning. **Complete.**
2. Employment v0 → workplace authority and recurring shift/closing-duty Commitments. **Complete.**
3. Ordinary Hunger → Eating governed by location and Activity affordances. **Complete.**
4. Derived Decision Importance foundation. **Complete.**
5. Recreation admission and automatic Tabletop Games / Reading selection from Interests and
   availability. **Complete.**
6. Ordinary Socializing through bounded shared-context selection. **Complete.**

All five Phase 2 branch candidates now exist: Need-vs-obligation, two-desirable-Recreation,
social-invitation-versus-plan, rest-versus-continuation, and a second commitment conflict produced by
routine Employment rather than scenario input. Task selection therefore advances to Phase 3A's
authoritative Nudge economy.

This is sequencing guidance, not permission to implement all five as one subsystem project.

## Task selection rules

1. Finish active work first unless explicitly redirected.
2. If Phase 0 is not locked, perform product definition rather than speculative implementation.
3. Choose the earliest incomplete causal link whose prerequisites exist.
4. State one observable completion test before coding.
5. Extend existing production primitives only when the behavior demonstrates a missing capability.
6. Persist new authoritative state and add deterministic/save-load coverage in the same slice.
7. Author through the real content path; scenario-only construction stays in focused tests.
8. Expose player-facing behavior through real projections and Commands.
9. Update [`../ImplementationStatus.md`](../ImplementationStatus.md) when evidence changes and this
   roadmap when priority changes.

Delivery mechanics and the full definition of done are in
[`../IMPLEMENTATION_GUIDELINES.md`](../IMPLEMENTATION_GUIDELINES.md).

## Explicitly deferred beyond the MVP gate

Unless the MPS demonstrates a direct need, defer broad economy, construction, actual commitment
`Defer`, broad n-way conflict clustering, global reputation, relationship-memory attrition, advanced
pathfinding, secondary primary Activities, large content libraries, final art direction, networking,
mod support, and DOTS/ECS migration. Core Identity mechanics in phases 9–14 are planned post-MVP work,
not permission to scaffold them during the current routine slices.

## Stage north star

The architecture has proven it can simulate choices. The next milestone must prove it can simulate
**lives**. The longer arc must preserve Core Identity's distinction between knowing, influencing, and
physically overruling people whose wills remain their own.

For any one of the small cast, the player should be able to answer:

> Where are they? What are they doing and why? What are they planning? Who matters to them? What do
> they believe? What choice are they facing? What can I do about it? What changed because of the last
> choice?
