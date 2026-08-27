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
- **Energy Rest/Continue branch** — authored ongoing Recreation and Social Activities may turn the
  Energy rest threshold into an immutable compiled preflight. Low-Importance fatigue rests directly;
  significant competing Interest adopts the exact reasons into a normal persistent, non-holdable
  Decision. Continue preserves the snapshotted Activity instance and rearms Energy through
  `NeedProgressionService` at a strictly lower declared threshold (`2000 → 1000 → 0` in sample
  content); exhausting that finite sequence rests automatically. Need and exact-Activity identity are
  dependency-indexed signals, so only affected living routes reevaluate with stable Influence identity.
  Option/context snapshots and existing Need persistence provide deterministic live, offline, and
  save/load continuation without timers, polling, or parallel routine state.
- **Ordinary Hunger / Eating routine** — an authored increasing-pressure Need may supply an ordinary
  satisfaction Activity, activation threshold, and instantaneous completion offset. Immutable locations
  own explicit Activity affordances indexed by Activity; the routine deterministically chooses the
  nearest reachable occupiable location, travels there when necessary, completes Eating, applies its
  snapshotted meal offset through `NeedProgressionService`, and returns to Waiting. It only starts from
  fallback Waiting, so uninterrupted Work is not silently treated as a meal break. Need-threshold
  Decision triggers may require an Activity context, keeping Mina's leave-work dilemma specific to
  Working while free characters eat without a Decision. In-flight Travel carries generic snapshotted
  continuation parameters through save/load and content drift.
- **Discretionary Recreation routine** — an authored increasing-pressure Need maps Decision Options to
  concrete Activities and Interests. At threshold, a free character filters candidates through the real
  nearest-reachable Activity-affordance index and evaluates the remaining Options with the compiled
  reasoning pipeline in an immutable preflight that allocates no identity, scheduler entry, or Domain
  Event. A low-Importance result deterministically starts its selected local Activity or Travel directly;
  a result at or above the catalog admission floor becomes a normal persistent Decision by adopting the
  exact preflight reasons and Importance without reevaluation. Destination, Activity duration, and Need
  satisfaction are snapshotted onto the selected Option/continuation, so completion and replanning remain
  identical live, offline, and after save/load. Production content authors Tabletop Games and Reading;
  focused tests cover ordinary selection, unavailable-affordance fallback, promoted admission, preflight
  purity, and persistence equivalence.
- **Ordinary Socializing routine** — an authored increasing-pressure Social Need may map to a concrete
  Socializing Activity, activation threshold, satisfying offset, and hard-bounded candidate count. It
  starts only for a free character at a location that explicitly affords Socializing and only after a
  real counterpart exists in the indexed direct-occupancy context. Threshold, Waiting, and bounded
  arrival reactions retry without polling or pair scanning. Candidate selection reuses directional
  social relevance and deterministic sampling; the chosen interaction uses the established relationship,
  familiarity, history, evidence, Knowledge, and optional social-Decision pipeline. Only the seeking
  character changes primary Activity, while the counterpart's Work, Recreation, or other Activity is
  left intact as a subordinate interaction. Target identity and Need satisfaction are snapshotted on the
  Activity, whose completion returns to Waiting identically live, offline, and after save/load.
- **Social invitation versus an existing plan** — the same bounded Social routine may author a compiled
  invitation Decision for a co-located counterpart already pursuing an explicitly listed discretionary
  Activity. The recipient reasons between Join and Keep Plan using their Knowledge-relative social
  appraisal, shared Activity context, and Interest in the current plan. Belief and Activity revisions
  target only the affected compiled routes while stable Influence identity preserves interventions and
  projection semantics. Acceptance abandons the snapshotted plan only through
  `ActivityTransitionService` and starts Socializing for the recipient; refusal or stale context leaves
  the plan untouched. Runtime Option/context snapshots, frozen resolution evidence, history, and
  save/load continuation require no parallel invitation state or new persistence schema.
- **Decisions** — living influence sets with stable influence identity, dependency-indexed
  reevaluation, deterministic dice resolution, bounded held decisions, one authority for intervention
  rules, one content-backed Need-threshold generation path with an Activity consequence, and targeted
  compiled Activity-context influence reevaluation (§17–§20). Influences now carry persisted option-relative
  polarity: the current replaceable resolution policy adds supporting rolls and subtracts opposing
  rolls, while interventions modify die magnitude without changing polarity. Production intervention
  eligibility now also evaluates its authored resource policy and cost against authoritative player
  state; command execution and per-action projection consume that same result.
- **Nudge economy** — new worlds begin at the locked three-Nudge cap. Successful Nudge-backed
  interventions spend their snapshotted cost; invalid/no-op commands spend nothing. A persistent
  Preparation-phase event regenerates one Nudge at each aligned eight-hour SimTime boundary without
  banking above the cap, including OfflineCatchUp. Dissolved Decisions refund snapshotted Nudge costs
  one intervention at a time with per-event clamping. Balance/revision, scheduled regeneration,
  applied resource provenance, migration from legacy free interventions, history, and balance plus
  eligibility/cost projections all round-trip through save schema v11 (and continue unchanged in v12).
- **Re-roll and die substitution** — an attended held Decision can produce a frozen pending roll set,
  expose accepted results, replace one result with the next deterministic per-Influence roll index, and
  commit explicitly or at a bounded expiry. Pre-roll substitution snapshots an authored effective die;
  the initial catalog includes a fixed-result loaded d20. Re-roll allowance and replacement-die
  holdings are separate authoritative resources with authored policy, shared command/projection
  eligibility, spend/refund behavior, scheduled refresh, and no Nudge coupling. Accepted and
  superseded evidence, fixed-die provenance, pending expiry work, and resource balances round-trip in
  save schema v12. Automatic and OfflineCatchUp resolution commit without waiting or spending.
- **Commons availability** — the sample Commons has persisted authoritative Open/Closed state and a
  validated one-Nudge `SetLocationAvailabilityCommand`. State-changing commands bump a location-scoped
  availability revision, publish retained history, and share eligibility/cost rules with the location
  projection; invalid, no-op, and unaffordable requests spend nothing. Closed locations are excluded
  from new discretionary affordance selection. Destination-indexed revalidation redirects only
  in-flight routine Travel that depended on the changed location, while already-running Activities and
  unrelated Commitment travel continue. Living Recreation Decisions register rebuildable location
  dependencies and dissolve/replan when an option set is invalidated. State, management capability,
  revisions, and Nudge balance round-trip through save schema v13; legacy locations migrate open.
- **Small-cast MPS foundation** — the shared production `MinimumPlayableWorld` now authors all ten locked MPS
  roles: Mina, Glen, Darius, Lena, Priya, Marcus, Tess, Owen, Jo, and Ravi. They share the three-location
  Residential/Bakery/Commons graph but begin in staggered authoritative Activities, including Eating,
  Working, Sleeping, and in-progress Travel. Shared households, six production Employments across two
  workplaces, distinct Need/Interest profiles, and the existing relationship/accountability paths give
  the cast intersecting rather than duplicated lives. A two-day deterministic acceptance asserts one
  active primary Activity per character, Sleep/wake completion for every character, and observed Eating,
  Work, Travel, Tabletop Games, Reading, and Socializing. The same full-cast fixture now compares Owen's
  Open-Commons Tabletop plan with a one-Nudge Closed-Commons branch that selects Reading from the
  remaining real affordance; the managed branch matches through save/reload and OfflineCatchUp. Sample
  content reactions use an explicit composition hook on both new and restored hosts. The fixture now
  also authors directional Mina→Glen, Mina→Darius, and Owen→Lena social edges with deliberately
  asymmetric familiarity/channels, positive and negative memories, and sparse observer-relative
  beliefs at different confidence levels. Owen's ordinary afternoon arrival at Lena's Commons shift
  revises his inaccurate first impression through interaction evidence; a later social Decision uses
  the changed evaluation, and the whole pre-evidence branch replays exactly after save/load. The
  full-cast Mina leave-work Decision now supplies paired Emphasize and Temper acceptance branches:
  each action is projected through the authoritative eligibility rules, spends one Nudge, leaves the
  Held Decision unresolved, persists with its stable Influence target, and reapplies over a later
  living-reason reevaluation identically after reload. Substitute now applies the separately resourced
  authored loaded d20 before rolls, persists it across load, produces its fixed 20, and still leaves
  winner selection to the normal option-total policy. Re-roll freezes Mina's production Decision,
  saves between the initial roll and player action, advances only the targeted scoped stream, retains
  the discarded roll as non-causal evidence, and commits identically after reload. Offline expiry also
  commits the frozen result without consuming Re-roll availability. All four intervention families are
  therefore integrated. The final Phase 4 Attention audit adds a living-Importance Decision feed,
  durable Normal/Auto-Hold/Quiet character policy, Follow prioritization, prospective Auto-Hold in
  player-present modes, and deterministic held-capacity overflow through the normal resolution path.
  Quiet suppresses surfacing without changing simulation or history, changing policy does not release
  an existing Hold, and durable policy/Hold state round-trips across save/load. The full-cast acceptance
  also closes and reopens the Commons during Owen's in-flight routine Travel and proves targeted
  redirection plus restored availability. Phase 4 is complete.
- **Phase 5 playable Unity surface** — Unity and the headless acceptance runner
  now call the same Infrastructure-owned ten-character world builder. BaseGame authoring contains the
  matching Bakery and Commons Employments, café-host Activity, hierarchy location kinds, and cast Trait
  ids. The Unity HUD projects clock/mode/speed/offline-return state; the complete roster projects
  observation-safe Activity/location, Follow, Attention policy, and surfaced/Held Decision state.
  Character profiles now combine observed Overview facts with the materialized Schedule,
  knowledge-filtered Relationships, active/recent Decisions, and retained character History. The
  legacy direct-Travel button remains serialized only for prefab compatibility and is always hidden,
  preserving the MVP rule that Travel is autonomous rather than a player command. Projection tests
  cover unobserved roster privacy and relationship Knowledge filtering; PlayMode coverage exercises
  the ten-character roster and all five profile sections. The Decision center now consumes the
  living-Importance inbox, preserves deliberate selection, and shows held capacity, countdowns,
  hard-deadline warnings, knowledge-filtered Options/reasons, pending and frozen rolls, applied
  interventions, resource balances, and authoritative availability/cost feedback for every authored
  action. BaseGame includes Nudge-backed Encourage and Temper plus the separately resourced Re-roll and
  loaded-d20 substitution actions; Unity sends the exact selected action and stable Influence target.
  Recent important resolutions remain in a bounded result feed, while below-threshold active Decisions
  remain available through the inspected character profile rather than leaking into the inbox. The
  character surface now has a dedicated chronological timeline mode over the materialized planning
  horizon. It shows start windows/deadlines, expected end, duration, location, lifecycle, recurring
  template provenance, participants, and conflicts. Conflict projection combines direct time overlap
  with active authoritative commitment-conflict episodes, so travel-induced infeasibility appears
  without Unity or the query layer duplicating feasibility rules. A dedicated Knowledge mode now renders
  only player-held personal and relationship evidence, including confidence, observation age, discovery
  channel/informant provenance, and staleness. Unknown relationships stay absent despite existing Domain
  truth, character-held belief distributions and latent relationship channels never leak, and evidence
  whose direction is not encoded says so explicitly instead of inventing a symmetric score. A selectable
  world-location panel now projects Home, Bakery, and Commons hierarchy and availability, authoritative
  Nudge cost/eligibility, recent location history, and only watched-character occupancy or inbound Travel.
  Its Open/Close control enqueues the existing location-availability Command, so Commons closure before
  Owen plans exposes the ordinary Reading fallback while closure during his trip exposes targeted Travel
  redirection without adding Unity-side simulation rules. The final bounded notification/recap panel
  derives meaningful events from retained causal History rather than subscribing to Domain Events.
  It applies the Decision Importance and Quiet policies for live surfacing, admits social consequences
  only when player Knowledge supports them, groups repeated event families under an eight-group bound,
  switches to one since-return recap during OfflineCatchUp, and retains navigation targets for Decisions,
  characters, and locations. Notification selection and toast/panel state remain ephemeral Presentation
  state. Phase 5 is complete.
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
  with the strongest persisted active reason magnitude. Structural generators remain admitted;
  discretionary Recreation is the first candidate generator to apply a catalog-owned admission floor
  while retaining a real ordinary fallback.
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
  `Dissolved`: its pending event is cancelled, held capacity is released, snapshotted Nudge spend is
  unconditionally refunded under the authoritative capped account, no resolution consequence runs,
  and an Ephemeral recap is recorded. A
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
- **Routine-produced commitment conflict** — a second authored Employment role supplies a recurring
  cafe-hosting obligation. When one character holds both the Bakery shift and Cafe role, each
  Employment materializes an independently feasible normal Commitment; their clock windows do not
  overlap, but Bakery-to-Cafe Travel makes the set jointly infeasible. Ordinary schedule-change events
  feed the same compiled Preserve/Relinquish generator without `CommitmentBecomesKnown` or a new
  conflict type. Resolution creates the canonical provenance-linked Relinquished outcome and leaves
  the preserved intent to routine planning, with identical deadline rolls, statuses, and pending work
  through offline catch-up and save/load.
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
  per-type values. Schema v13 adds persisted location availability and management capability; v12
  locations migrate open and unmanaged.
- **Scale regression gate** — the normal suite repeats a 250-character/six-hour workload and requires
  identical authoritative hashes and deterministic work counts under structural per-character ceilings.
  An opt-in 1,000-character/one-day tier enforces initial wall-clock and heap budgets while the CLI
  reports the same measurements and authoritative hash (§49–§52).

Intentionally thin, pending game-design decisions:

- **MVP agency presentation breadth.** Follow, Hold/Release, stable Influence intervention,
  knowledge-filtered Decision projection foundations, the Nudge economy, Re-roll/die-substitution,
  and the Commons availability Command and targeted reactions exist authoritatively. Normal/Auto-Hold/
  Quiet tuning is now authoritative. Phase 5 is complete: the Unity HUD projects current SimTime plus
  paused/live/fast-forward/offline-return status, and roster rows project observed Activity/location,
  Follow, Attention policy, and feed-qualified Decision attention without leaking unobserved live state;
  character profiles, the selectable Decision feed/detail surface, the dedicated materialized
  schedule/timeline, player-Knowledge relationship view, world/location management surface, and bounded
  live/offline notification recap are implemented. Phase 6 concrete save storage and restart/failure UX
  remain.
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
- **Needs → behaviour breadth.** Every locked MVP Need now has one production behavior: Energy drives
  Sleep/recovery, Hunger drives affordance-gated Eating, Recreation selects reachable Activities from
  Interests with per-instance Decision admission, and Social pressure starts bounded co-located
  Socializing. Competing routine priority and broader circumstance combinations remain intentionally
  thin.
- **Unity authoring/presentation.** The authored-content migration now covers Traits, Needs,
  Activities, Employment definitions, Commitment Accountability policies, and the singleton Decision
  Importance policy, plus Location Kinds, Appraisal Calibrations, Social Evidence, and Social
  Pressures, Decisions, and Interventions. BaseGame
  definitions, including explicit Energy, required Waiting/Traveling/Sleeping content, and Bakery
  Employment obligation patterns, are independent assets under a manifest-owned pack folder and enter
  builds through a deterministically sorted baked index. A stale
  index is a build error. Immutable `DefinitionSet` contributions and the Application resolver enforce
  same-pack uniqueness, declared full-record cross-pack replacement, load order, final catalog
  validation, engine-required Waiting/Traveling presence, and resolution provenance. The Unity
  BaseGame composition separately preflights its required Energy/Sleeping definitions. Direct tests
  cover builder/catalog snapshot isolation, missing override targets, and invalid override reordering.
  The BaseGame Commitment Accountability policy is also an
  independent indexed asset; Employment and Commitment Template references bind after overlay to the
  effective policy, including a declared cross-pack replacement. Synthetic headless tests exercise
  those semantics. `ContentPackAsset` has been removed: Unity Bootstrap consumes the baked index and
  its manifest directly, while final cross-reference validation remains post-resolution. Decision
  assets retain typed compiled reasoning, social triggers, and directional outcomes; Intervention
  assets retain their authored resource policies. Demo characters receive deterministic social
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
| HUD status projects pause, speed, SimTime, and offline return without owning simulation state | `CommandAndProjectionTests`, `VivariumPlayModeTests` |
| Roster combines observation-safe Activity/location, Attention, and authoritative Decision surfacing | `CommandAndProjectionTests`, `VivariumPlayModeTests` |
| UI availability and command validation share one authority | `CommandAndProjectionTests` |
| Nudge spend, insufficiency/no-op safety, and per-action eligibility/cost projection agree | `NudgeEconomyTests`, `CommandAndProjectionTests` |
| Eight-hour Nudge regeneration does not bank at cap and matches save/load/OfflineCatchUp | `NudgeEconomyTests`, `PersistenceTests` |
| Dissolved Decisions refund snapshotted Nudge spend under per-event clamping | `NudgeEconomyTests`, `CommitmentConflictDecisionTests` |
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
| Social pressure waits for real shared context, Socializes without displacing its counterpart, and completes identically after reload/offline | `SocializingRoutineTests`, `GoldenScenarioTests` |
| Social invitation reaches a character with an existing plan, reevaluates Knowledge/context reasons, and resolves with deterministic persistent Activity consequences | `SocialInvitationDecisionTests`, `GoldenScenarioTests` |
| Energy continuation admits only important Rest/Continue choices, rearms a strictly lower threshold, reevaluates Need/exact-Activity reasons, persists, and terminates without thrash | `NeedContinuationDecisionTests` |
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
| Two Employment routines naturally create a travel-only conflict and resolve authoritative intent identically offline and after reload | `RoutineCommitmentConflictTests` |
| Routine fulfillment changes Reliance evidence without Trust/memory mutation | `GoldenScenarioTests` |
| Breach attribution produces provenance-linked belief, memory, history, and channel effects once | `GoldenScenarioTests`, `VivariumPlayModeTests` |
| Same initial world yields a weaker later Reliance Influence after breach, exactly across pre-conflict reload | `GoldenScenarioTests` |
| Authored Unity Need crossing generates the projectable Decision | `VivariumPlayModeTests` |
| Decision history feed is causal, bounded, filtered, and newest-first | `CommandAndProjectionTests` |
| Unity Decision feed refreshes after intervention at quiescence | `VivariumPlayModeTests` |
| Unity Decision center selects surfaced Decisions and exposes resources plus every authored action without bypassing Attention floors | `VivariumPlayModeTests` |
| Low-Importance active Decisions remain inspectable through the character profile instead of leaking into the inbox | `VivariumPlayModeTests` |
| Materialized timeline orders commitments and finds every direct overlap, including non-adjacent intervals | `CommandAndProjectionTests` |
| Timeline conflict state consumes authoritative travel-feasibility conflict episodes and appears in Unity with windows, routines, and participants | `CommitmentConflictDecisionTests`, `VivariumPlayModeTests` |
| Knowledge view hides unknown relationship truth and exposes only observed confidence, age, provenance, and staleness without latent-channel leakage | `GoldenScenarioTests`, `VivariumPlayModeTests` |
| Location view exposes hierarchy, availability, bounded history, and only watched occupants/travelers; Commons closure surfaces both planning fallback and in-flight redirection | `RecreationRoutineTests`, `GoldenScenarioTests`, `VivariumPlayModeTests` |
| Notification/recap projection is bounded, groups repetition, suppresses proactive Quiet events without erasing recap history, filters social events through player Knowledge, and navigates retained targets in Unity | `RecreationRoutineTests`, `GoldenScenarioTests`, `VivariumPlayModeTests` |
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
