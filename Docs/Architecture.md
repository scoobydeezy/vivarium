# Vivarium — Architecture

The authoritative architecture brief lives in [`/README.md`](../README.md). It is the source of truth
for principles, invariants, and deferred decisions. This document records only how that brief is
*realised in this repository*, and where the implementation deliberately differs.

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
- **Decisions** — living influence sets with stable influence identity, dependency-indexed
  reevaluation, deterministic dice resolution, bounded held decisions, one authority for intervention
  rules, one content-backed Need-threshold generation path with an Activity consequence, and targeted
  Activity-context influence reevaluation (§17–§20).
- **Knowledge** — fact providers, a knowledge ledger that goes stale by design, discovery driven by
  observation through one canonical `WatchState` (§20.1, §22–§25).
- **Interactions** — location-arrival and indexed shared-travel-segment opportunities use bounded
  deterministic candidate selection, leave primary Activities intact, update Relationships, and create
  observation-driven Knowledge only through canonical `WatchState` (§25, §32).
- **Commands and queries** — deterministic ingress queue, dispatcher, projections published only at
  quiescent boundaries, knowledge-filtered decision views (§2.2.1, §26, §35).
- **Persistence** — versioned DTOs, explicit payload codecs, revision persistence, index rebuilding on
  load, migration chain with version-drift reporting (§38–§40).

Intentionally thin, pending game-design decisions:

- **Decision generation breadth.** One Need-threshold trigger now generates a content-backed Decision;
  other circumstances and targeted live influence construction remain to be added as concrete content
  requires them. All headless runner execution paths now use the generated leave-work choice.
- **Consequences breadth.** A resolved option can now change the primary Activity through the common
  transition service. Employment, relationship, and Commitment consequences remain unimplemented.
- **Save serialization format.** Explicitly deferred (§57). `ISaveGameSerializer` is defined;
  `InMemorySaveGameStore` exercises mapping without committing to an encoding.
- **Needs → behaviour breadth.** Threshold crossings can generate one Decision type; direct routine,
  Activity-priority, and other behavioral reactions remain unimplemented.
- **Unity authoring/presentation.** `ContentPackAsset` converts authored Needs, Activities, Decisions
  (including threshold triggers, initial influences, and Activity outcomes), and interventions into the
  validated Domain catalog. The smoke scene schedules two shared work Commitments: Mina and Glen
  interact while travelling, Mina arrives beside a disliked working colleague and gains Work pressure,
  then a real hunger crossing generates the leave-work Decision. `WorldPresenter` surfaces the
  resulting knowledge-filtered projection and sends
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
| Held decisions bounded, overflow deterministic | `SimulationInvariantTests`, `CommandAndProjectionTests` |
| Commands execute in ingress order at quiescent boundaries | `CommandAndProjectionTests` |
| UI availability and command validation share one authority | `CommandAndProjectionTests` |
| Hidden influence count not exposed | `CommandAndProjectionTests` |
| Different knowledge yields different views of one decision | `CommandAndProjectionTests` |
| Save/load round-trip, including active travel and revisions | `PersistenceTests` |
| Need pressure generates a Decision and Activity consequence | `PersistenceTests` |
| Generated Decision resolves identically after save/reload | `PersistenceTests` |
| Work pressure counts only while context is present | `WorkContextTests` |
| Living influence reevaluates with stable identity and reloads | `WorkContextTests` |
| Observation reveals a generalized live influence | `WorkContextTests` |
| Shared-context interaction leaves Activities intact | `InteractionTests` |
| Shared travel segment interaction survives index rebuild/load | `InteractionTests` |
| Watched interaction creates Knowledge; unwatched does not | `InteractionTests` |
| Large shared context produces a bounded interaction outcome | `SimulationInvariantTests` |
| Full Golden Scenario causal chain | `GoldenScenarioTests` |
| Held generated Decision resolves identically offline after reload | `GoldenScenarioTests` |
| Mixed travel/Decision/Need/Commitment offline checkpoint is equivalent | `GoldenScenarioTests` |
| Authored Unity Need crossing generates the projectable Decision | `VivariumPlayModeTests` |
| Decision history feed is causal, bounded, filtered, and newest-first | `CommandAndProjectionTests` |
| Unity Decision feed refreshes after intervention at quiescence | `VivariumPlayModeTests` |
| Unity demo progresses through shared travel, Work pressure, and generated Decision | `VivariumPlayModeTests` |
| Version drift diagnosed, not automatically blocking | `PersistenceTests` |
| Offline duration computed outside Domain | `PersistenceTests` |

## Adding to this codebase

Two questions from §59, worth asking before writing anything:

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
