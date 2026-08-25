# Vivarium — Implementation Status

The concise architectural contract lives in [`../README.md`](../README.md), and the complete normative
reference lives in [`Architecture/Reference.md`](Architecture/Reference.md). This document records what
the repository currently implements, what remains intentionally thin, and where tests provide evidence.
It is a checkpoint, not a roadmap or a substitute for inspecting code.

Section references such as “§41” refer to the complete architecture reference.

## Repository shape

The brief (§4) assumes a repo where the Unity project sits under `/Unity/ManagementSim`. This
repository already had the Unity project at the root, so the layout is adapted rather than followed
literally:

```
/Core                       Unity-independent simulation, consumed as a local UPM package
  /Runtime
    /Domain                 Vivarium.Domain.asmdef      (BCL only)
    /Application            Vivarium.Application.asmdef (Domain)
    /Infrastructure         Vivarium.Infrastructure.asmdef (Application, Domain)
  package.json              com.vivarium.core

/DotNet                     Same source, normal .NET tooling
  Vivarium.slnx
  Vivarium.Domain/          netstandard2.1, compiles ../Core/Runtime/Domain/**
  Vivarium.Application/     netstandard2.1
  Vivarium.Infrastructure/  netstandard2.1
  Vivarium.Domain.Tests/    net10.0, xunit
  Vivarium.Application.Tests/
  Vivarium.SimRunner/       net10.0 console app
  Vivarium.SimRunner.Tests/ net10.0 Golden Scenario acceptance tests

/Assets/Game                Unity-side code, one asmdef per layer
  /Presentation             Vivarium.Unity.Presentation   (Domain, Application, Unity)
  /Infrastructure           Vivarium.Unity.Infrastructure (Domain, Application, Unity)
  /Authoring                Vivarium.Unity.Authoring      (Domain, Unity)
  /Bootstrap                Vivarium.Unity.Bootstrap      (everything)
  /Editor                   Vivarium.Unity.Editor         (Domain, Authoring, Editor-only)

/Docs                       This file
```

`Packages/manifest.json` references `"com.vivarium.core": "file:../Core"`, so Unity and `dotnet build`
compile **the same files**. There is no DLL-copy step.

### Why the catalog lives in Domain

`DefinitionCatalog` sits in `Vivarium.Domain.Content`, not in Application. §41 calls it an "immutable
Domain definition catalog", and the dependency table (§3) allows Unity Authoring to reference Domain
but not Application. Putting the catalog anywhere else would force the authoring assembly to reference
Application just to convert a ScriptableObject.

## Dependency enforcement

Boundaries are enforced mechanically, not by discipline (invariant 71):

| Assembly | Enforcement |
| --- | --- |
| `Vivarium.Domain` | `noEngineReferences: true` in the asmdef; the csproj has zero references |
| `Vivarium.Application` | asmdef references Domain only; `noEngineReferences: true` |
| `Vivarium.Infrastructure` | asmdef references Application + Domain; `noEngineReferences: true` |
| Unity assemblies | asmdefs list their allowed references explicitly |

`noEngineReferences` is the load-bearing setting: it makes `UnityEngine` invisible to Core, so a stray
`using UnityEngine;` fails to compile rather than quietly coupling the simulation to the engine.

The `Vivarium.SimRunner` console app is the second enforcement mechanism (§52). It compiles the same
Core with no Unity present, so engine leakage breaks the build immediately.

## Running things

```bash
# Build everything on the .NET side
dotnet build DotNet/Vivarium.slnx

# All tests
dotnet test DotNet/Vivarium.slnx

# The vertical-slice scenario, printed to the console
DotNet/Vivarium.SimRunner/bin/Debug/net10.0/Vivarium.SimRunner.exe demo

# Same seed twice, compare authoritative state
Vivarium.SimRunner.exe determinism

# Save before a decision resolves, reload, confirm the same outcome
Vivarium.SimRunner.exe saveload

# Synthetic population benchmark
Vivarium.SimRunner.exe bench 1000 1

# Enforce the measured 1,000-character/one-day budget (PowerShell)
$env:VIVARIUM_ENFORCE_PERFORMANCE_BUDGETS='1'
dotnet test DotNet/Vivarium.SimRunner.Tests --filter StandardMeasuredBudget
```

## Language level

Core targets `netstandard2.1` with `LangVersion 9.0`, matching Unity's cross-platform profile (§4).

Nullable reference annotations are **off** on purpose. Unity's asmdef compilation does not enable the
nullable context, and Core must compile identically in both toolchains, so nullability is documented in
XML comments rather than enforced by the compiler. Turning it on means turning it on in both places at
once — and checking that `IsExternalInit` is available before using `init` accessors or records.

## What exists today

Working implementations:

- **Time** — `SimTime`, `SimDuration`, `SimClock`, `AnalyticalProgression` with exact integer
  threshold-crossing arithmetic (§9, §10).
- **Identity** — typed runtime ids, monotonic allocators, persisted counters, `EntityRef` for
  historical references (§7).
- **Randomness** — counter-based `DeterministicRandomOracle` over a fixed FNV-1a/SplitMix64 mixer, with
  authored scope and purpose ids (§14).
- **Scheduling** — `Scheduler` ordered by `DueAt → Phase → EventSequence`, cancellation, rescheduling,
  aspect-scoped revision dependencies, and the same-instant phase guard (§11).
- **Settlement** — `SettlementLoop` drains scheduled events and Domain Event reactions together to
  quiescence, with the runaway guard raising `SimulationCascadeLimitExceeded` (§11.4, §12.1).
- **Activities** — one authoritative primary Activity per character, travel as an Activity, occupancy
  indexes maintained on transition, time-weighted context modifiers, and a content-configurable
  disliked-colleague Work pressure reaction (§29, §30).
- **Energy rest routine** — a content-backed reserve Need can react directly to its low threshold,
  travel to the character's household location, enter Sleeping, recover analytically to a distinct
  threshold, wake into fallback planning, and continue identically through save/load and
  `OfflineCatchUp`. Optional travel continuation intent is snapshotted in the scheduled arrival payload;
  schema v7 preserves older arrivals as having no invented continuation.
- **Ordinary Hunger / Eating routine** — an authored increasing-pressure Need may supply an ordinary
  satisfaction Activity, activation threshold, and instantaneous completion offset. Immutable locations
  own explicit Activity affordances indexed by Activity; the routine deterministically chooses the
  nearest reachable occupiable location, travels there when necessary, completes Eating, applies its
  snapshotted meal offset through `NeedProgressionService`, and returns to Waiting. It only starts from
  fallback Waiting, so uninterrupted Work is not silently treated as a meal break. Need-threshold
  Decision triggers may require an Activity context, keeping Mina's leave-work dilemma specific to
  Working while free characters eat without a Decision. In-flight Travel carries generic snapshotted
  continuation parameters through save/load and content drift.
- **Decisions** — living influence sets with stable influence identity, dependency-indexed
  reevaluation, deterministic dice resolution, bounded held decisions, one authority for intervention
  rules, one content-backed Need-threshold generation path with an Activity consequence, and targeted
  compiled Activity-context influence reevaluation (§17–§20). Influences now carry persisted option-relative
  polarity: the current replaceable resolution policy adds supporting rolls and subtracts opposing
  rolls, while interventions modify die magnitude without changing polarity.
- **Knowledge** — player- and character-scoped fact providers, sparse social belief distributions,
  lifecycle/retention metadata, and discovery driven by observation through one canonical `WatchState`
  (§20.1, §22–§25).
- **Interactions** — location-arrival and indexed shared-travel-segment opportunities use bounded
  deterministic candidate selection, leave primary Activities intact, update Relationships, and create
  observation-driven Knowledge only through canonical `WatchState` (§25, §32).
- **Matrix-first social model** — seven-dimensional fixed-point latent personality, named-trait
  projections, sparse directional appraisal fields with pairwise/context/PSD ideal terms, covariance-aware
  observer beliefs, shared lens calibration, directional dyadic channels, analytical familiarity,
  salient memories, values/interests, affect, bounded reputation, deterministic generation/drift, and
  full contribution traces (§32.1). Shared-context interactions create bounded character-held evidence;
  one concrete social interaction Decision consumes the appraisal and applies an asymmetric relationship
  consequence through the normal living-Decision pipeline. Its appraisal math now runs through the
  generic `Evaluation/SignalField` primitive and exposes deterministic latent/output variance in addition
  to its existing expectation, calibration, and contribution trace.
- **Decision reasoning checkpoint A** — the social interaction Decision now follows
  `SignalField → InterpersonalComfort Consideration → non-stacking ReasonChannel → DecisionInfluence`.
  The former direct `SocialDecisionInfluenceFactory` path was retained as a parity oracle while the new
  route was proven, then removed.
- **Decision reasoning checkpoint B** — Decisions and Options now carry small
  typed semantic contexts, and an in-flight Decision deep-snapshots a compiled reasoning program made
  of validated parameter schemas/bindings, Signal requests, fixed-point fields, ReasonChannels, and die
  scales. Minimal capability providers cover Decision context, character Values, target availability,
  directional relationship channels, travel burden, and authored modifiers on the actor's current
  Activity. One evaluator handles any number of target,
  self, and wait Options, distinguishes unknown/not-applicable Signals from neutral values, and produces
  consolidated signed reasons through the existing Influence policy. Save schema v4 round-trips the
  authoritative typed contexts and complete compiled program; Candidate Reasons and dependency indexes
  remain absent from the save and are deterministically rebuilt. Compiled reasons reconcile by stable
  binding/Option/ReasonChannel identity: reevaluation updates or retracts the same Influence id, and
  snapshotted intervention mechanics are replayed over its refreshed base magnitude. Derived dependency
  routes now address `(DecisionId, BindingId, OptionId)`, support partial evaluation/reconciliation, and
  rebuild after load by evaluating the persisted program through the composed provider capabilities.
  `CompiledDecisionGenerationService` takes runtime-bound target/self Options through creation, initial
  reasoning, route registration, scheduling, creation events, and the normal signed resolution service.
  Unity authoring serializes the complete typed program, including Option context, parameter schemas and
  sources, provider requests, linear/pairwise/ideal Signal fields, ReasonChannels, scales, labels, and
  visibility. Pre-play lint rejects unknown providers, invalid or unbound parameters, impossible Option
  bindings, unrequested Signals, unsupported dice, and incompatible legacy/social reasoning paths; the
  Domain catalog repeats the authoritative validation at construction. The playable leave-work Decision
  is the first Unity-authored production consumer: hunger urgency supports leaving, reliability supports
  staying, and disliked-colleague Activity context reevaluates the same work-context reason from d10 to
  d6. Its old direct templates and content-specific reevaluator have been removed.
- **Derived Decision Importance foundation** — each admitted Decision now derives one bounded living
  Importance from the maximum absolute `ExpectedScore` among its active consolidated reasons. Static
  per-Decision-type importance has been removed from Domain definitions, generator requests, headless
  content, and Unity authoring. Full and targeted reason reconciliation recompute the value from the
  complete current reason set; intervention-modified dice do not affect it, and resolved Decisions retain
  their last value. The exact value round-trips in saves, while schema v10 replaces legacy authored values
  with the strongest persisted active reason magnitude. All current generators remain admitted; the
  Recreation slice will first apply an admission floor to a candidate with a real ordinary fallback.
- **Frozen Decision explanations** — live compiled Influences retain their latest compact evaluation
  snapshot (signed expectation, output variance, Signal means/variance/applicability/source revisions,
  and contribution amounts). Resolution deep-copies that evidence plus semantic label/category/channel,
  subject, polarity, die, and roll into each retained `InfluenceRoll`. `DecisionProjector` constructs the
  explanation lazily from this historical evidence and never re-queries current World state. Resolution
  history is Significant, linked back to its Decision, persisted, and pruned together with the resolved
  Decision/evidence by `DecisionHistoryRetentionService`.
- **Commitment-conflict Decision** — aspect-scoped commitment schedule changes invoke a
  `CommitmentFeasibilityService` that searches complete deterministic orderings of the active set; it
  does not infer joint feasibility from pairwise overlaps. The v0 content slice generates two
  plan-valued Options (`Preserve A / Relinquish B` and the reverse), while the authoritative
  `CommitmentResolutionPlan` already carries canonical Preserve/Defer/Relinquish sets. Feasibility
  removes invalid Options before compiled Considerations rank the remaining plans. Each plan evaluates
  its preserved and relinquished Commitment as distinct bound subjects, so non-stacking ReasonChannels
  merge duplicate readings of one subject without merging different people or obligations.
  `CommitmentConflictKey` retains an episode revision while a rebuildable active-conflict index prevents
  duplicate generation. A revision-dependent hard deadline auto-resolves even a Held Decision at the
  correct simulation instant. If the candidate set stops describing reality first, the Decision becomes
  `Dissolved`: its pending event is cancelled, held capacity is released, interventions are enumerated
  for unconditional refund, no resolution consequence runs, and an Ephemeral recap is recorded. A
  resolved plan marks sacrificed intent `Relinquished`; a separate routine-planner reaction schedules
  Activity/Travel for preserved intent. Save schema v5 persists plans, conflict identity, deadline,
  interventions, and the deadline event while rebuilding only indexes/routes.
- **Commitment outcomes and accountability** — `CommitmentLifecycleService` is the sole runtime
  authority for Commitment status transitions. Each terminal transition validates the locked
  Outcome/Cause pairing, allocates one immutable `CommitmentOutcomeId`, records an Ephemeral outcome,
  and publishes one canonical event. Materialized Commitments snapshot role-bearing `StakeholderRef`s
  and a most-specific-wins `CommitmentAccountabilityPolicy`; both the Commitment and a pending
  `CommitmentBecomesKnownPayload` persist that snapshot across content hot reload and save/load.
  `CommitmentAttributionMapper` is the only boundary that reads authoritative cause. The ordered
  consequence handler sees only stakeholder-facing attribution, records character-scoped Knowledge,
  routes authored evidence through the existing covariance-aware social belief updater, and optionally
  creates one directional memory/history record plus salient channel deltas. Every durable artifact
  carries a weak `SourceOutcomeId` and a denormalized explanation. Routine fulfillment contributes only
  Dependability-relevant evidence; it does not mutate Trust or create memory. External cancellation is
  observed without blaming the actor. Accountability settles at handler order 100, before schedule and
  social-belief-dependent Decision reactions. Ephemeral outcomes prune on the same history-retention
  cutoff while their Significant/Legacy artifacts remain meaningful.
- **Employment v0** — an authoritative `Employment` identity snapshots employee, Employer group,
  role, workplace, character supervisor, and assigned recurring obligation patterns. The
  `EmploymentService` derives Employer membership and materializes bounded regular-shift and
  closing-duty patterns as ordinary Commitments sourced to that Employment, with the supervisor as an
  Authority stakeholder. Employer, role, and supervisor facts flow through the ordinary fact-provider
  and inspection path. Employment identity, allocator state, and obligation snapshots persist in save
  schema v8; rebuildable employee/employer indexes are restored after load. Focused tests cover
  materialization, attendance/fulfillment, semantic facts, and future behavior across save/load.
- **Golden Scenario commitment conflict** — `CommitmentBecomesKnownPayload` is persisted scheduled
  scenario input for dinner: at its authored reveal instant it materializes that authoritative
  Commitment and publishes the normal schedule-change event. Mina's closing duty already exists from
  her Employment, and duration plus Bakery-to-Cafe travel—not simple clock overlap—makes the pair
  jointly infeasible. The detector deterministically selects the first
  individually feasible but jointly infeasible pair by Commitment ID, so unrelated future routine
  occurrences can coexist without suppressing the v0 two-plan encounter. After the existing
  leave-work beat, Mina learns that dinner with Glen conflicts with helping Darius close the bakery.
  The same content shape runs in the headless and Unity compositions. `DecisionProjector` translates
  the runtime plan into concrete Keep/Give-up text and identifies its feasibility cutoff as a hard
  deadline; Unity deterministically presents the highest-importance active Decision.
- **Commands and queries** — deterministic ingress queue, dispatcher, projections published only at
  quiescent boundaries, knowledge-filtered decision views (§2.2.1, §26, §35).
- **Persistence** — versioned DTOs, explicit payload codecs, revision persistence, index rebuilding on
  load, migration chain with version-drift reporting (§38–§40). Schema v4 additionally persists typed
  Decision/Option context and snapshotted compiled reasoning programs. Schema-v3 direct-influence
  Decisions migrate without inventing a program; schema-v2 Influences still migrate as supporting
  legacy reasons. Schema v5 adds authoritative commitment-conflict plans, instance identity, and hard
  deadlines without persisting the active-conflict index.
  Schema v6 adds the CommitmentOutcome allocator plus Commitment stakeholder/accountability snapshots
  and weak outcome provenance on Knowledge, RelationshipMemory, and History. Outcome ledgers and
  idempotency indexes are not save caches: settled durable consequences are authoritative, while pending
  policy snapshots are carried by their scheduled payload. Schema v7 adds optional travel-arrival
  continuation fields; v6 payloads migrate with the prior no-continuation behavior. Schema v8 adds
  Employment identities, their snapshotted obligation patterns, and the Employment allocator. Schema
  v9 adds authoritative location Activity affordances; optional continuation parameters reuse the
  existing scheduled-payload collections and older Travel arrivals decode with none. Schema v10 derives
  saved Decision Importance from active persisted reason evaluations rather than retaining legacy authored
  per-type values.
- **Scale regression gate** — the normal suite repeats a 250-character/six-hour workload and requires
  identical authoritative hashes and deterministic work counts under structural per-character ceilings.
  An opt-in 1,000-character/one-day tier enforces initial wall-clock and heap budgets while the CLI
  reports the same measurements and authoritative hash (§49–§52).

Intentionally thin, pending game-design decisions:

- **MVP agency contract.** Follow, Hold/Release, stable Influence intervention, and knowledge-filtered
  Decision projection foundations exist. The locked Nudge balance/regeneration/refund economy,
  Normal/Auto-Hold/Quiet policy semantics, Commons availability Command, targeted availability
  reactions, bounded recap, and complete MVP Unity surfaces are not implemented.
- **Intent versus forced outcome.** Decisions retain historical reasoning and Commitments distinguish
  planning intent from Activity execution, but there is no general action-attempt provenance or player
  physical-interference path. The simulation cannot yet record “Mina chose and attempted to leave, but
  the Observer returned her to work” as distinct causal facts.
- **Beliefs about the Observer.** `ObserverRef` currently identifies the player or a Character as a
  Knowledge holder; the Observer/AGI is not yet a durable fact subject or social actor. Player actions do
  not produce character-scoped evidence, attribution, belief, or later Decision pressure about the
  Observer.
- **Product-identity macro systems.** AGI philosophy, Habitat identity, Culture, norms, status ideals,
  institutions, collective narratives, forced transfer, voluntary migration, inter-habitat contact,
  and person-grounded collective action are product-directed but unimplemented. They remain post-MVP
  roadmap phases and require focused briefs before code.
- **Decision generation breadth.** Need-threshold, social-interaction, and joint commitment-infeasibility
  triggers now generate content-backed Decisions. Other circumstances remain content-driven additions.
- **Consequences breadth.** Resolved options can change the primary Activity, a directional relationship
  channel, or Commitment intent through Preserve/Relinquish. Commitment outcomes now feed stakeholder
  attribution, memories, directional channels, and live social belief/Reliance. Institutional
  stakeholders, wages/payroll, promotion/staffing, later attribution correction, and actual Defer
  behavior remain unimplemented.
- **Save serialization format.** Explicitly deferred (§57). `ISaveGameSerializer` is defined;
  `InMemorySaveGameStore` exercises mapping without committing to an encoding.
- **Needs → behaviour breadth.** Threshold crossings can generate one Decision type, Energy drives a
  Sleep/recovery routine, and Hunger drives ordinary affordance-gated Eating. Competing routine
  priority, Recreation/Social satisfaction, and other behavioral reactions remain unimplemented.
- **Unity authoring/presentation.** `ContentPackAsset` converts authored Needs (including rest and
  satisfaction routines), Activities, Employment definitions and obligation patterns, Decisions
  (including typed compiled reasoning, social triggers, and directional outcomes), appraisal calibration,
  social evidence/pressure, and interventions into the validated Domain catalog. Demo characters receive deterministic social
  profiles. The smoke scene creates Mina and Glen's Bakery Employments, whose shared shift Commitments
  interact while travelling, Mina arrives beside a disliked working colleague and gains Work pressure,
  then a real hunger crossing generates the compiled, explainable leave-work Decision. Leaving Work now
  naturally travels toward the Room's Eating affordance; the Workshop has none. After that beat, dinner
  becomes known and conflicts with Mina's Employment-derived closing duty.
  `WorldPresenter` surfaces the resulting knowledge-filtered projection and sends
  Hold, Release, and intervention Commands. A bounded newest-first Decision history projection promotes
  appearance, successful intervention, and resolution events into explanatory recent History and is
  rendered at quiescence alongside the encounter. Character/roster/travel surfaces remain deliberately
  utilitarian, with no general-purpose event browser, UI Toolkit layer, or art direction (§44, §45
  remain open).

## Invariants with test coverage

The test suite is organised around the §58 invariants rather than around classes:

| Invariant | Where |
| --- | --- |
| Deterministic outcomes from seed + ordered commands | `DeterminismTests`, `SimRunner determinism` |
| Aspect-scoped revisions, never monolithic | `SchedulerAndSettlementTests` |
| Same-instant settlement reaches quiescence; runaway fails loudly | `SchedulerAndSettlementTests` |
| Domain Event handler order is explicit | `SchedulerAndSettlementTests` |
| Exactly one primary Activity; occupancy agrees with it | `SimulationInvariantTests` |
| Travel excluded from direct occupancy | `SimulationInvariantTests`, `PersistenceTests` |
| Time-varying context counts only for its interval | `SimulationInvariantTests` |
| Interventions bound to stable influence identity | `SimulationInvariantTests`, `PersistenceTests` |
| Signed option-relative resolution and intervention-preserved polarity | `SimulationInvariantTests`, `PersistenceTests` |
| Generic fixed-point SignalField expectation and variance | `SignalFieldTests`, `SocialModelTests` |
| Social Consideration path preserves former observable influence behavior | `DecisionReasoningTests`, `SocialDecisionTests` |
| ReasonChannels are non-stacking by default | `DecisionReasoningTests` |
| Typed target/self/wait binding and explicit Signal applicability | `DecisionReasoningTests`, `SignalFieldTests` |
| In-flight reasoning snapshots resist source mutation and round-trip | `PersistenceTests` |
| Reevaluation preserves semantic Influence and intervention identity | `DecisionReasoningTests`, `PersistenceTests` |
| Binding/Option routes target reevaluation and rebuild after load | `DecisionReasoningTests`, `PersistenceTests` |
| Implemented-state multi-option Decision generates, schedules, and resolves | `DecisionReasoningTests` |
| Resolved explanations freeze evaluation evidence across drift and reload | `DecisionReasoningTests`, `PersistenceTests` |
| Resolution evidence prunes with linked Decision history | `DecisionReasoningTests`, `PersistenceTests` |
| Authored reasoning programs reject invalid providers, bindings, Signals, and scales | `DecisionReasoningTests`, `VivariumPlayModeTests` |
| Playable leave-work content runs entirely through authored compiled reasons | `WorkContextTests`, `GoldenScenarioTests`, `VivariumPlayModeTests` |
| Held decisions bounded, overflow deterministic | `SimulationInvariantTests`, `CommandAndProjectionTests` |
| Commands execute in ingress order at quiescent boundaries | `CommandAndProjectionTests` |
| UI availability and command validation share one authority | `CommandAndProjectionTests` |
| Hidden influence count not exposed | `CommandAndProjectionTests` |
| Different knowledge yields different views of one decision | `CommandAndProjectionTests` |
| Save/load round-trip, including active travel and revisions | `PersistenceTests` |
| Need pressure generates a Decision and Activity consequence | `PersistenceTests` |
| Energy travels home, sleeps, wakes, and replans identically live/offline/after reload | `SleepRoutineTests` |
| Hunger selects only an explicit reachable Eating affordance, consumes, and replans identically live/offline/after reload and content drift | `EatingRoutineTests`, `GoldenScenarioTests`, `VivariumPlayModeTests` |
| Generated Decision resolves identically after save/reload | `PersistenceTests` |
| Work pressure counts only while context is present | `WorkContextTests` |
| Living influence reevaluates with stable identity and reloads | `WorkContextTests` |
| Observation reveals a generalized live influence | `WorkContextTests` |
| Shared-context interaction leaves Activities intact | `InteractionTests` |
| Shared travel segment interaction survives index rebuild/load | `InteractionTests` |
| Watched interaction creates Knowledge; unwatched does not | `InteractionTests` |
| Large shared context produces a bounded interaction outcome | `SimulationInvariantTests` |
| Latent appraisal math, uncertainty, context, provenance, and contradictory lenses | `SocialModelTests` |
| Observer beliefs update jointly and remain sparse | `SocialModelTests`, `InteractionTests`, `SocialScaleTests` |
| Social truth, fields, beliefs, directional history, and uncertainty round-trip | `SocialPersistenceTests` |
| Schema-v1 affinity migrates into two directional states | `SocialPersistenceTests` |
| Social interaction creates a living Decision and asymmetric consequence | `SocialDecisionTests` |
| Calibrated social pressure changes Activity performance only while context exists | `SocialActivityPressureTests` |
| Torture corpus has an executable layer/mechanism routing audit | `SocialTortureCorpusTests` |
| Full Golden Scenario causal chain | `GoldenScenarioTests` |
| Held generated Decision resolves identically offline after reload | `GoldenScenarioTests` |
| Mixed travel/Decision/Need/Commitment offline checkpoint is equivalent | `GoldenScenarioTests` |
| Scheduled obligations generate a concrete plan Decision identically across reload | `GoldenScenarioTests`, `VivariumPlayModeTests` |
| Terminal Commitment transitions validate, mint one outcome, and survive expiration reload | `CommitmentOutcomeTests`, `CommitmentOutcomePersistenceTests` |
| Accountability policy/stakeholder snapshots round-trip without persisting derived routing | `CommitmentOutcomePersistenceTests`, `GoldenScenarioTests` |
| Employment derives workplace obligations, authority facts, attendance, and identical future behavior after reload | `EmploymentTests`, `GoldenScenarioTests`, `VivariumPlayModeTests` |
| Routine fulfillment changes Reliance evidence without Trust/memory mutation | `GoldenScenarioTests` |
| Breach attribution produces provenance-linked belief, memory, history, and channel effects once | `GoldenScenarioTests`, `VivariumPlayModeTests` |
| Same initial world yields a weaker later Reliance Influence after breach, exactly across pre-conflict reload | `GoldenScenarioTests` |
| Authored Unity Need crossing generates the projectable Decision | `VivariumPlayModeTests` |
| Decision history feed is causal, bounded, filtered, and newest-first | `CommandAndProjectionTests` |
| Unity Decision feed refreshes after intervention at quiescence | `VivariumPlayModeTests` |
| Unity demo progresses through shared travel, Work pressure, and generated Decision | `VivariumPlayModeTests` |
| Version drift diagnosed, not automatically blocking | `PersistenceTests` |
| Schema-v6 travel arrivals migrate without invented continuation intent | `PersistenceTests` |
| Offline duration computed outside Domain | `PersistenceTests` |
| Fixed population workload has identical hash and bounded structural work | `ScaleBenchmarkTests` |
| Opt-in 1,000-character measured budget | `ScaleBenchmarkTests.StandardMeasuredBudgetIsOptIn` |

## Adding to this codebase

Two questions from §60, worth asking before writing anything:

1. *Would this still need to exist if the whole game were text in a console?* If yes, it belongs in
   Domain or Application. If it exists because the player needs to see, hear, click, or animate
   something, it belongs in Unity Presentation.
2. *Does this create new truth, change existing truth, reveal truth, or merely present truth?* Those
   four map onto Domain systems, commands, Knowledge, and projections respectively.

Practical conventions that follow from the invariants:

- New scheduled event type → payload (data only) + handler + registry entry in the bootstrapper +
  **a payload codec**, or it silently vanishes from saves.
- New continuous value → `AnalyticalProgression`, plus a scheduled crossing if any threshold can change
  behaviour, plus revision bump + reschedule wherever the rate changes.
- New random draw → authored purpose id in `RandomPurposes`, never a method name or display string.
- New collection iteration that can affect state → sort it explicitly, or use the sorted
  repositories/indexes that already exist.
- New index → make it rebuildable in `WorldState.RebuildDerivedIndexes` rather than persisted.
