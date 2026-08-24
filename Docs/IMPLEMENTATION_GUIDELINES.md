# Vivarium Implementation Guidelines

This document owns delivery discipline: how an already-selected product slice is implemented. Current
priority belongs only to [`Product/Roadmap.md`](Product/Roadmap.md); architecture belongs to
[`../README.md`](../README.md) and [`Architecture/Reference.md`](Architecture/Reference.md).

## Delivery principle

> Build the smallest production-shaped behavior whose simulated cause produces an observable,
> deterministic, persistent consequence.

Do not build a broad subsystem as disconnected scaffolding, and do not keep extending foundations once
they support the next causal link.

## Before implementation

1. Inspect `git status` and preserve unrelated work.
2. Read [`Architecture.md`](Architecture.md), the relevant sections of the full architecture reference,
   and the focused design/scenario brief for the subsystem in scope.
3. Check [`ImplementationStatus.md`](ImplementationStatus.md), then verify its claims in production code
   and tests.
4. Confirm the selected work is the active roadmap item or an explicit user request.
5. Define one completion statement in causal form: “when X changes in authoritative state, Y follows
   and appears in Z projection/history.”
6. List the architectural invariants and cross-cutting obligations the slice can affect.

## Cross-cutting checklist

Apply only the relevant items, but check each deliberately.

### Domain and Application

- Is this truth, a truth mutation, observer Knowledge, or Presentation?
- Does external mutation enter through a validated Command in `CommandSequence` order?
- Are runtime IDs typed, monotonic, persisted, and never reused?
- Is order-sensitive iteration explicit and deterministic?
- Is authoritative branching math integral/fixed-point where practical?
- Does continuous state use analytical progression and schedule behaviorally meaningful crossings?
- Are revisions aspect-scoped and backed by semantic validation?
- Do active Decision dependencies reevaluate through targeted routes rather than polling?
- Does same-instant work have explicit phase/handler order and settle to quiescence?

### Scheduling and persistence

For every new scheduled event, provide a data-only payload, handler, stable event type, composition
registration, execution-time validation, dependencies, and payload codec. Persist authoritative state,
allocator counters, revisions, and scheduled work required for identical continuation. Rebuild derived
indexes through the established load-time reconstruction path; do not persist reconstructible caches.

### Content and Unity

Use stable authored IDs for definitions and random purposes. Validate authored content before play.
Snapshot definition-derived values required by in-flight behavior. Unity objects remain
representational: they translate input into Commands and render read models.

### Verification

During iteration, run the narrowest affected tests. Before completing Core behavior, run:

```powershell
dotnet test DotNet/Vivarium.slnx
```

For scenario-affecting work, also run the applicable SimRunner modes from
[`Architecture.md`](Architecture.md). New authoritative state is incomplete until persistence and
determinism obligations are tested or explicitly shown not to apply.

## Definition of done

A slice is complete when all applicable statements are true:

- the causal behavior runs headlessly;
- deterministic replay matches;
- save/load continuation and offline crossing behavior match;
- scheduled and Domain Event work settles to quiescence;
- production content uses the validated authoring path;
- Unity consumes Commands and read models without duplicating authority;
- acceptance tests prove behavior rather than only APIs;
- documentation ownership is reconciled.

Update [`ImplementationStatus.md`](ImplementationStatus.md) when capability evidence changes. Update
[`Product/Roadmap.md`](Product/Roadmap.md) when priority changes. Update a focused design brief or the
architecture only when the decisions owned by that source change.
