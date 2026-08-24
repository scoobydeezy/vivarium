# Vivarium

Vivarium is a deterministic management simulation about knowing autonomous people the player cannot
control. Their routines, relationships, Knowledge, history, and circumstances generate meaningful
choices. The player controls much of the world around them and may influence—or physically frustrate—
an outcome without rewriting what a person wanted.

> **The simulation is the game. Unity hosts and presents it.**

## Architectural contract

These rules are the highest-level source of truth for the repository:

1. The authoritative simulation is pure C# and has no Unity dependency.
2. Commands are the only external mutation boundary. They execute in deterministic sequence at
   quiescent simulation boundaries.
3. World truth, observer Knowledge, and Presentation are separate models.
4. Simulation time is independent of render time. Continuous state progresses analytically and
   schedules only behaviorally meaningful crossings or completions.
5. Scheduled Events, ordered Domain Event reactions, and same-instant work settle deterministically
   to quiescence before state is externally readable.
6. Runtime identity, authoritative ordering, numeric state, and randomness are deterministic and
   persist when continuation depends on them.
7. Every active character has exactly one primary Activity. Traveling is an Activity; spatial presence
   is derived from its `SpatialContext`.
8. Decisions are living, persistent runtime state. Their reasons can reevaluate through targeted
   dependencies while stable semantic identity protects interventions and explanations.
9. Save data uses explicit versioned DTOs. Authoritative continuation state is persisted; derived
   indexes are rebuilt and validated.
10. Simulation behavior remains runnable and testable headlessly. Unity sends Commands and renders
    read models; it never becomes a second simulation owner.
11. Population-scale work is bounded by indexes, shared context, analytical progression, and measured
    headless regression gates—not global pair scans or per-character/per-frame ticking.
12. New behavior is delivered as the smallest end-to-end slice that advances the playable scenario,
    including determinism and persistence coverage where applicable.
13. Autonomous intent and physical outcome are distinct. Player interference may change what happens,
    but it never retroactively changes what a person chose or believed; the interference remains part
    of observable, attributable history.

The complete contract, including all 120 invariants, acceptance criteria, and explicitly
deferred decisions, is preserved in
[`Docs/Architecture/Reference.md`](Docs/Architecture/Reference.md). That reference is normative. This
README is its routing layer and concise summary; it does not narrow or replace the detailed rules.

If this summary and the reference ever appear to disagree, the detailed reference governs and the
summary must be corrected.

## Documentation map

Read only as far as the task requires:

| Need | Source |
| --- | --- |
| Mandatory agent workflow and task routing | [`AGENTS.md`](AGENTS.md) |
| Repository boundaries, build commands, and implementation shape | [`Docs/Architecture.md`](Docs/Architecture.md) |
| Detailed current capability and test evidence | [`Docs/ImplementationStatus.md`](Docs/ImplementationStatus.md) |
| How to select and deliver a vertical slice | [`Docs/IMPLEMENTATION_GUIDELINES.md`](Docs/IMPLEMENTATION_GUIDELINES.md) |
| Current product priority and ordered roadmap | [`Docs/Product/Roadmap.md`](Docs/Product/Roadmap.md) |
| Product identity and long-range mechanical north star | [`Docs/Product/CoreIdentity.md`](Docs/Product/CoreIdentity.md) |
| The 8–12-character acceptance world | [`Docs/Product/MinimumPlayableScenario.md`](Docs/Product/MinimumPlayableScenario.md) |
| Topic-specific locked design decisions | [`Docs/README.md`](Docs/README.md) |

## Source precedence

1. This architectural contract and its detailed normative reference.
2. Core Identity for product intent, provided it remains architecture-compliant.
3. Focused design briefs for their declared topic.
4. The current product roadmap for sequencing and priority.
5. Implementation guidelines for delivery discipline.
6. Status documentation as a checkpoint; code and tests remain the evidence of what exists.

An explicitly deferred decision requires product direction. Do not silently turn a proposal, example,
or roadmap recommendation into architectural truth.

## Repository shape

```text
Core/Runtime/           Unity-independent Domain, Application, and Infrastructure source
DotNet/                 .NET projects, tests, and headless SimRunner over the same Core source
Assets/Game/            Unity Authoring, Bootstrap, Infrastructure, Presentation, and Editor layers
Docs/                   Architecture, product, design, status, and historical documentation
```

Start with [`Docs/Architecture.md`](Docs/Architecture.md) for the concrete dependency map and commands.

## Architectural north star

When deciding where code belongs, ask:

> Would this still need to exist if the entire game were represented as text in a console window?

Then ask:

> Does this create truth, change truth, reveal truth, or merely present truth?

For player power, ask one more:

> Did the player change a person's reasons, change the world, or physically prevent an intended
> action—and does history preserve the difference?

Those questions should resolve most boundary decisions without loading the full reference.
