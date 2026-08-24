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

**Phase 0 is complete and Phase 1 is in progress.** The jointly locked
[`Minimum Playable Scenario`](MinimumPlayableScenario.md) and
[`Player Agency Brief`](PlayerAgencyBrief.md) define the small world, meaningful character Decisions,
supported player actions, and required Unity surfaces.

The immediate next slice is:

> **Employment v0 → workplace authority and recurring shift/closing-duty Commitments.**

Observable completion test:

> A character's authored Employment identity produces recurring workplace obligations with derived
> workplace authority and stakeholder roles; attendance, fulfillment, and conflict consequences arise
> through the existing Commitment lifecycle identically across uninterrupted and save/load runs.

The Energy/Sleep/Wake slice is complete: production content now supplies an analytically progressing
Energy Need and a content-backed rest routine that travels to a household location, sleeps, recovers,
wakes, and resumes fallback planning identically live, offline, and across save/load. Employment is now
the earliest missing MPS causal link and is required before the flagship Bakery obligations are
production-shaped rather than scenario-authored commitments.

## Ordered phases

| Phase | Outcome | Status |
| --- | --- | --- |
| 0. Lock playable intent | MPS and Player Agency briefs define one coherent playable world | Complete |
| 1. Close the daily routine loop | Energy/Sleep/Wake, Employment obligations, ordinary Eating, discretionary Recreation, and Socializing support indefinite lives | In progress |
| 2. Expand meaningful choice | Scenario-required branch points use compiled Considerations and production consequences | Pending |
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
2. Employment v0 → workplace authority and recurring shift/closing-duty Commitments. **Next.**
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
