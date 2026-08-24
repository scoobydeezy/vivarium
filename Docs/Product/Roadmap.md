# Vivarium Product Roadmap

**Status:** Current product sequence and next-task authority  
**Last reconciled:** 2026-08-24

This document answers: **given the implemented simulation, what should be built next?** Detailed phase
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

## Current gate

**Phase 0 is in progress.** The reviewed
[`Minimum Playable Scenario`](MinimumPlayableScenario.md) defines the small world, cast, routine
coverage, and causal acceptance beats.

The immediate next task is:

> **Draft and lock `PlayerAgencyBrief.md`, then reconcile it with the Minimum Playable Scenario.**

Do not select another generic Core subsystem before this gate is resolved unless explicitly directed.
The joint briefs must identify every meaningful character Decision, every supported player action, and
the Unity surface required to understand and perform those actions.

The Player Agency brief must decide:

1. Character and location inspection surfaces included in MVP play.
2. How Follow, Watch, Quiet, and Mina's automatic Hold policy are exposed.
3. Whether intervention is normal play and, if so, its resource costs, regeneration, cap, refunds,
   persistence, and offline behavior.
4. The first environmental management command. Commons availability/open-state is the leading reserved
   seam, but remains a product decision.
5. Whether one interactive Activity is in MVP scope.
6. Presentation and recap behavior for off-screen, unwatched, and offline Decisions.

After reconciliation, select the earliest missing Phase 1 causal link proven necessary by the scenario.

## Ordered phases

| Phase | Outcome | Status |
| --- | --- | --- |
| 0. Lock playable intent | MPS and Player Agency briefs define one coherent playable world | In progress |
| 1. Close the daily routine loop | Energy/Sleep/Wake, Employment obligations, ordinary Eating, discretionary Recreation, and Socializing support indefinite lives | Pending |
| 2. Expand meaningful choice | Scenario-required branch points use compiled Considerations and production consequences | Pending |
| 3. Complete player agency | Intervention economy, one environmental lever, and optional interactive Activity become real Commands | Pending |
| 4. Build the small-cast world | 8–12 characters run 2–3 days through one durable headless acceptance scenario | Pending |
| 5. Build playable Unity surfaces | Roster, character, Decision, schedule, Knowledge, world, and history views consume projections/Commands | Pending |
| 6. Productize save/continue | Concrete storage, restart continuation, offline catch-up, migration diagnostics, and failure UX | Pending |
| 7. MVP hardening gate | The small cast is legible, causal, replayable, persistent, and remains within scale gates | Pending |
| 8. Relationship-memory longevity | Post-MVP consolidation retains defining memories and compacts ordinary history deterministically | Pending, post-MVP |

See [`RoadmapPhases.md`](RoadmapPhases.md) for phase-level completion tests and the complete acceptance
matrix.

## Earliest likely routine links

Once Phase 0 is locked, current analysis suggests this order, subject to revalidation against code and
the reconciled briefs:

1. Energy → Sleeping → waking → replanning.
2. Employment v0 → workplace authority and recurring shift/closing-duty Commitments.
3. Ordinary Hunger → Eating governed by location and Activity affordances.
4. Discretionary Recreation → Tabletop Games / Reading selected from Interests and availability.
5. Ordinary Socializing through bounded shared-context selection.

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

Unless the MPS demonstrates a direct need, defer broad economy, construction, generalized
institutions, actual commitment `Defer`, broad n-way conflict clustering, global reputation,
relationship-memory attrition, advanced pathfinding, secondary primary Activities, large content
libraries, final art direction, networking, mod support, and DOTS/ECS migration.

## Stage north star

The architecture has proven it can simulate choices. The next milestone must prove it can simulate
**lives**.

For any one of the small cast, the player should be able to answer:

> Where are they? What are they doing and why? What are they planning? Who matters to them? What do
> they believe? What choice are they facing? What can I do about it? What changed because of the last
> choice?
