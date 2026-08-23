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

### Completed checkpoint — circumstances generate and complete a real Decision

Completed on **2026-08-22** for one deliberately narrow content path: a Need threshold publishes a
Domain Event, content generates a living Decision, deterministic resolution selects an option, and an
Activity outcome runs through the authoritative transition service. Save-before-resolution/reload
equivalence is covered.

Future Decision content should preserve the established shape:

- generate the Decision through an explicitly ordered Domain Event reaction, rather than direct runner
  construction;
- build true influences from current authoritative state and register targeted dependencies;
- preserve existing Attention/Hold, Knowledge filtering, intervention, and deterministic resolution;
- apply consequences through authoritative services rather than mutating repositories ad hoc;
- publish the resulting projection/history only after quiescence; and
- prove deterministic replay plus save-before-resolution/reload equivalence.

Do not generalize the trigger/outcome model further until a second concrete Decision demonstrates the
shared shape.

### Completed checkpoint — interaction as a subordinate occurrence

Completed on **2026-08-22** for location-arrival and indexed directed travel-segment contexts: bounded deterministic selection produces at
most one interaction, leaves both primary Activities intact, changes a Relationship, records recent
history, and creates observation-driven Knowledge only for characters supported by canonical
`WatchState`. A 2,000-character shared-context test covers the bounded outcome, and save/reload rebuilds
travel candidate indexes from active Traveling Activities.

### Priority 1 — expand the Golden Scenario end to end

Connect routine planning, shared travel, Work context, Needs, the generated Decision, Knowledge reveal,
intervention, consequences, travel, save, and reload into one growing headless scenario. Maintain it as
an acceptance test or SimRunner scenario; do not rely on a prose demo alone.

Work-context pressure is now connected: a negatively related colleague's presence applies an
interval-accurate Activity modifier, and departure reevaluates the generated Decision through its
targeted dependency while preserving influence identity across save/load.

The headless demo, determinism check, save/load check, and dedicated `Vivarium.SimRunner.Tests`
acceptance project now use the generated leave-work choice. The
scenario exercises routine travel, shared-segment interaction, Work pressure, Watch/Hold, a
Knowledge-driven label reveal, targeted reevaluation, stable-identity intervention, deterministic
resolution, an Activity consequence, and reload equivalence without runner-side Decision construction.

Checkpointed assertions cover the entire causal chain. Offline coverage now proves a held generated
Decision resolves under `OfflineCatchUp`, durable Follow and ephemeral visibility restore according to
policy, and the resulting Decision and Activity match the uninterrupted branch.

### Completed checkpoint — persistence and offline hardening

Extend serialization coverage as each earlier slice adds state. Once the Golden Scenario is connected,
stress offline catch-up with active travel, held and automatic Decisions, scheduled Need thresholds,
Activities, Commitments, and interactions. Choose a concrete save encoding only when product/platform
requirements resolve the format that README §57 deliberately defers.

Completed on **2026-08-22** with both focused held-Decision coverage and a mixed-state checkpoint
containing active travel, held and automatic generated Decisions, multiple pending Need crossings, and
active Commitments. Offline catch-up compares allocator counters, scheduler order, analytical Needs,
Activities, Commitments, Decisions and rolls, Relationships, and rebuilt spatial indexes between
uninterrupted and restored branches. Concrete save encoding remains explicitly deferred by README §57.

### Priority 2 — authoring and playable presentation

Author real definitions through the validated ScriptableObject-to-Domain catalog path and expose the
Golden Scenario through character, Activity, Decision, Watch/Hold, intervention, Knowledge, and event
feed surfaces. Presentation work may proceed earlier when explicitly requested, but it must consume
the same commands, validation rules, and quiescent read models as the headless path.

The first playable authoring slice was completed on **2026-08-22**. The Unity content pack now authors
the leave-work Decision's Need trigger, options, initial influence visibility, Activity outcome, and
intervention. The smoke bootstrap no longer constructs a Decision directly: it settles Mina's authored
hunger crossing and presents the generated Decision through the existing knowledge-filtered read model
and Hold, Release, and intervention Commands. Unity assemblies and the PlayMode test assembly compile;
the batch PlayMode suite passes all 10 tests after its assertions were made explicit about selecting the
Need Decision in a world that can also contain social Decisions.

The causal event-feed slice was completed on **2026-08-22**. Decision appearance and successful player
intervention are promoted from ordered Domain Events into bounded recent History; resolution already
used that ledger. A knowledge-safe Application projector filters those records, returns at most five in
newest-first order, and Unity refreshes the feed beside the Decision encounter only at quiescent
boundaries. Rejected commands do not create history and presentation never reads the transient event
queue.

The playable progression slice was completed on **2026-08-22**. The Unity smoke world begins without a
Decision. At 07:02 Mina and Glen follow scheduled Work Commitments onto the same directed travel
segment and interact; at 07:32 Mina arrives beside a negatively related working colleague and receives
the interval-accurate Work-pressure modifier; at 07:34 her authored hunger threshold generates the
leave-work Decision. The Decision's Work-pressure dependency is now authored, and the content-specific
reevaluator and handlers are restored whenever Unity recomposes a loaded host. The pre-Decision panel
shows a clean inactive state. PlayMode coverage asserts each causal checkpoint.

Priority 2's minimum playable loop is now connected. The next implementation gate is Priority 3:
convert the SimRunner's existing synthetic population mode into a repeatable scale and determinism
regression with explicit measured budgets before adding management breadth.

### Priority 3 — scale gate before management breadth

Turn the SimRunner's synthetic population mode into repeatable performance and determinism regression
coverage. Establish measured budgets before architectural optimization. Substantial economy,
construction, advanced pathfinding, procedural populations, polished animation, and large content
libraries wait until the Golden Scenario works and the scale gate passes.

Completed on **2026-08-22**. The always-on regression tier runs 250 characters for six simulated hours
twice, requires the same authoritative hash and exact work/Activity/event counts, and enforces ceilings
of 100 work items, 8 Activities, and 3 pending events per character. This tier deliberately does not
assert wall time or heap usage.

The measured tier runs 1,000 characters for one simulated day and is enforced when
`VIVARIUM_ENFORCE_PERFORMANCE_BUDGETS=1`. Its initial budgets are 2 seconds to build, 15 seconds to run,
128 MB managed heap, 320 work items, 30 Activities, and 2 pending events per character. The pre-social
baseline on the development machine was 97 ms build, 6,938 ms run, 28 MB, 141,894 work items, 24,994
Activities, 1,002 pending events, and authoritative hash `A332FAE357E085CD`. The CLI reports these
metrics for arbitrary population/day inputs and only enforces measured limits for the standard tier
when the environment flag is enabled.

The production social pass adds participant evidence plus at most two indexed witnesses per interaction.
Its 1,000-character/one-day Release measurement on the same development machine is 189 ms build,
10,669 ms run, 82 MB, 286,595 work items, 24,994 Activities, 1,014 pending events, and hash
`DA36FBBC20D6042E`. The structural work ceiling was revised to describe this deliberately bounded new
pipeline; the original wall-clock and heap gates still pass.

The architecture gate has passed for beginning one deliberately narrow management vertical slice.
Select that slice from concrete product intent; do not infer a broad economy, construction, or job
model merely because performance coverage now exists.

### Active checkpoint — production relationship model

Product direction on **2026-08-22** selected `SocialModelBrief.md` as the production relationship
model and explicitly un-deferred NPC-held beliefs and relationship formulas. The former undirected
affinity testbed is being replaced by observer-scoped uncertain belief, sparse directional appraisal
fields, multiple calibrated lenses, directional history/familiarity, and explainable social pressure.

The brief's production foundation is implemented end to end. Indexed interactions create bounded
character-held evidence; covariance-aware sparse appraisal combines distinct lenses, directional history,
familiarity, values/interests, affect, and independent context with a causal trace. Calibrated results feed
interaction relevance, Decisions, and Activity pressure. Belief changes target living influence
reevaluation, and resolution changes only the deciding character's directional channel. Save schema v2
persists personality, fields, beliefs/covariance, values/interests, affect, directional channels,
familiarity, and memories; v1 affinity migrates into two initially equal directions. Runtime no longer
stores a universal relationship score. Reputation remains bounded observer Knowledge, and culture remains
the explicitly named future dependency from the brief.

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
