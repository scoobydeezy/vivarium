# Vivarium Implementation Guidelines

This document turns the frozen architecture and the milestone proposal into an execution policy. It
answers: **given the code that exists now, what should be implemented next?**

It is not a second architecture specification. [`../README.md`](../README.md) remains the authority
for system design and invariants; [`Architecture.md`](Architecture.md) records the repository-specific
realisation.

## Source precedence

Use the sources this way:

| Source | Role | Authority |
| --- | --- | --- |
| `README.md` | Required boundaries, invariants, acceptance criteria, deferred decisions | Highest |
| `Docs/Architecture.md` | Current repository shape, implemented capabilities, known thin seams | Must remain consistent with README |
| Milestone proposal | Dependency order, integration-test cadence, Golden Scenario | Planning input; never overrides README |
| Code and tests | Evidence of what actually exists | Verify before selecting work |

The proposal describes the desired dependency order, not the present repository state. Do not infer
that Vivarium is at Milestone 0 merely because the proposal begins there.

## Delivery principle

> Do not build a system until the system beneath it can create a reason for it to exist.

Also do not keep building foundations after they already support the next behavior. Prefer a narrow
vertical slice that begins with a simulated circumstance and ends with an observable, deterministic,
persistent consequence.

Every completed slice should leave all of these true where relevant:

1. It runs without Unity.
2. The same initial state, versions, seed, and ordered commands produce the same result.
3. Scheduled and Domain Event work settles to quiescence before projections are published.
4. Save/reload preserves all authoritative state needed to continue identically.
5. Truth, player Knowledge, and Presentation remain distinct.
6. Unity consumes commands and read models; it does not become an alternate simulation owner.
7. The behavior has acceptance-level coverage, not only isolated class tests.

## How to select the next implementation step

At the start of a task:

1. **Respect active work.** Inspect `git status` and current task context. Finish the explicitly
   requested or already-active slice before taking an unrelated roadmap item. Never overwrite another
   change merely because the roadmap ranks something else higher.
2. **Re-establish the baseline.** Read the status section in `Docs/Architecture.md`, inspect the
   relevant production code and tests, and run the narrow relevant test project when practical.
3. **Find the earliest missing Golden Scenario link.** Choose the first missing causal link whose
   prerequisites already exist. A thin implementation does not count as complete if no simulated
   circumstance exercises it.
4. **Define one observable outcome.** Express the slice as “when X happens in authoritative state, Y
   follows and appears in Z projection/history,” not “add services for Y.”
5. **List affected invariants.** Use README §58 and the acceptance criteria in §56. If the slice
   pressures an invariant without coverage, add that coverage.
6. **Trace cross-cutting obligations.** New state may require typed identity, revisions, scheduler
   payloads and handlers, payload codecs, save DTOs/mapping, index rebuilding, projections, content
   validation, bootstrap registration, and diagnostics. Include only applicable pieces, but check all
   of them deliberately.
7. **Set a stop condition.** The slice is done when its scenario passes headlessly and the appropriate
   determinism/save-load checks pass. Avoid opportunistic expansion into later milestones.

If several items are equally ready, prefer the one that closes more of README §56, exercises more
existing seams, and makes the Golden Scenario more playable. Prefer product behavior over another
generic abstraction.

## Current roadmap checkpoint

Last reconciled against the repository on **2026-08-22**. Reverify this section against code and tests
before acting on it.

### Foundation already present

The repository is beyond the proposal's Milestone 0. It already contains the simulation kernel,
analytical progression, Activities/travel/occupancy, commitments and bounded planning, Needs and
relationships, living Decisions and deterministic resolution, Knowledge/observation, command/query
boundaries, versioned persistence mapping, Unity authoring/presentation seams, and a headless
SimRunner. `Docs/Architecture.md` contains the precise supported list and invariant-test map.

Do not rebuild those systems from scratch. Extend them only when the selected behavior exposes a
specific missing capability.

### Priority 1 — make circumstances generate and complete a real Decision

This is the next unclaimed Core vertical slice after any active working-tree task is finished.

Implement one content-backed choice arising from existing simulation pressure—for example, a Need
threshold or adverse Work context causing Mina to consider leaving early. The slice should:

- generate the Decision through an explicitly ordered Domain Event reaction, rather than direct runner
  construction;
- build true influences from current authoritative state and register targeted dependencies;
- preserve existing Attention/Hold, Knowledge filtering, intervention, and deterministic resolution;
- apply at least one real consequence through the common consequence pipeline, changing current
  Activity and/or a future Commitment;
- publish the resulting projection/history only after quiescence; and
- prove deterministic replay plus save-before-resolution/reload equivalence.

Keep this to one Decision definition and the minimum supporting content. Do not build a generalized
content language until a second concrete decision demonstrates the shared shape.

### Priority 2 — interaction as a subordinate occurrence

The bounded candidate selector exists, but selection alone is not the interaction feature. Add one
shared-context interaction that leaves the primary Activity intact, changes a relationship or Need,
and can emit an Observation when watched. Cover a normal location/travel context and a synthetic large
context proving bounded selection rather than pairwise population scanning.

### Priority 3 — expand the Golden Scenario end to end

Connect routine planning, shared travel, Work context, Needs, the generated Decision, Knowledge reveal,
intervention, consequences, travel, save, and reload into one growing headless scenario. Maintain it as
an acceptance test or SimRunner scenario; do not rely on a prose demo alone.

### Priority 4 — persistence and offline hardening

Extend serialization coverage as each earlier slice adds state. Once the Golden Scenario is connected,
stress offline catch-up with active travel, held and automatic Decisions, scheduled Need thresholds,
Activities, Commitments, and interactions. Choose a concrete save encoding only when product/platform
requirements resolve the format that README §57 deliberately defers.

### Priority 5 — authoring and playable presentation

Author real definitions through the validated ScriptableObject-to-Domain catalog path and expose the
Golden Scenario through character, Activity, Decision, Watch/Hold, intervention, Knowledge, and event
feed surfaces. Presentation work may proceed earlier when explicitly requested, but it must consume
the same commands, validation rules, and quiescent read models as the headless path.

### Priority 6 — scale gate before management breadth

Turn the SimRunner's synthetic population mode into repeatable performance and determinism regression
coverage. Establish measured budgets before architectural optimization. Substantial economy,
construction, advanced pathfinding, procedural populations, polished animation, and large content
libraries wait until the Golden Scenario works and the scale gate passes.

## Implementation checklist

Use the applicable checks, not boilerplate for its own sake.

### Domain and Application

- Does this create truth, change truth, reveal truth, or merely present it?
- Is external mutation represented by a Command and processed in `CommandSequence` order?
- Are runtime IDs typed, monotonic, persisted, and never reused?
- Is order-sensitive iteration explicit and deterministic?
- Is authoritative math integral/fixed-point where practical?
- Does continuous state use `AnalyticalProgression` with meaningful crossings scheduled?
- Are revision dependencies aspect-scoped and backed by semantic validation?
- Are active Decision dependencies registered for targeted reevaluation rather than globally polled?
- Do same-instant reactions have explicit handler/phase order and settle to quiescence?

### Scheduling and persistence

For every new scheduled event, provide data-only payload, handler, stable event type, bootstrap
registration, execution-time validation, and a payload codec. Persist required allocator counters,
revisions, active runtime state, and scheduler state. Rebuild derived indexes in
`WorldState.RebuildDerivedIndexes`; do not persist reconstructible indexes.

### Content and Unity

Use stable authored IDs for content and RNG purpose/scope identifiers. Validate content before play.
Snapshot definition-derived values needed by in-flight entities. Keep Unity objects representational,
translate input into Commands, and render read models instead of mutable Domain entities.

### Verification

During implementation, run the narrow affected project/tests. Before completing Core changes, run:

```text
dotnet test DotNet/Vivarium.slnx
```

For scenario-affecting changes, also run the relevant SimRunner modes documented in
`Docs/Architecture.md`. A feature that adds authoritative state is incomplete until its persistence and
determinism obligations are either tested or explicitly shown not to apply.

## Golden Scenario

The roadmap converges on this causal chain:

```text
Commitment → planned Travel → shared-context Interaction → Work
→ contextual pressure + Need threshold → living Decision
→ Watch/Hold → Knowledge discovery → intervention → deterministic resolution
→ consequence changes Activity/Commitment → further Travel → save/reload equivalence
```

Use ugly, small content and roughly 8–12 characters until this chain is interesting and trustworthy.
Only then broaden the management game. The product question at the gate is not whether another system
can be scaffolded; it is whether the player cares what Mina decides.

