# Vivarium Agent Instructions

## Required context

Before planning or changing the repository, read in order:

1. [`README.md`](README.md) — concise architectural contract and source precedence.
2. [`Docs/Architecture.md`](Docs/Architecture.md) — repository boundaries, build commands, and
   implementation shape.
3. The task-specific sources selected below.

Use progressive disclosure; do not preload every brief.

| Task | Additional required source |
| --- | --- |
| Selecting or assessing next product work | [`Docs/Product/Roadmap.md`](Docs/Product/Roadmap.md) |
| Implementing an authoritative vertical slice | [`Docs/IMPLEMENTATION_GUIDELINES.md`](Docs/IMPLEMENTATION_GUIDELINES.md) and the relevant sections of [`Docs/Architecture/Reference.md`](Docs/Architecture/Reference.md) |
| Verifying whether something exists | [`Docs/ImplementationStatus.md`](Docs/ImplementationStatus.md), then production code and tests |
| Changing the small-world/MVP scenario | [`Docs/Product/MinimumPlayableScenario.md`](Docs/Product/MinimumPlayableScenario.md) |
| Changing social modeling | [`Docs/Design/SocialModel.md`](Docs/Design/SocialModel.md) |
| Changing Decision reasoning | [`Docs/Design/DecisionReasoning.md`](Docs/Design/DecisionReasoning.md) |
| Changing commitment conflict or accountability | The applicable brief under [`Docs/Design/`](Docs/Design/) |

The full documentation map is [`Docs/README.md`](Docs/README.md).

If sources disagree, follow the precedence in `README.md`. Do not silently resolve a genuine
contradiction: record it in the implementation plan and choose the architecture-compliant
interpretation, or request a product decision when the architecture explicitly defers it.

## Working rules

- Inspect the implementation and tests before trusting a status checklist; status documentation is a
  checkpoint, not a substitute for the code.
- Preserve unrelated work in the working tree. Do not edit, discard, or reformat it as part of a new
  slice.
- Deliver the smallest end-to-end behavior that advances the Golden Scenario. Do not build a broad
  subsystem as disconnected scaffolding.
- Keep authoritative simulation behavior Unity-independent and reachable from the headless runner.
- Add or extend invariant-focused tests for every authoritative behavior change. For deterministic or
  persistent state, include replay and save/load coverage as applicable.
- Run the narrowest relevant tests while iterating, then run `dotnet test DotNet/Vivarium.slnx` before
  considering Core work complete.
- When a slice materially changes what exists, update `Docs/ImplementationStatus.md`. When it changes
  what is next, update `Docs/Product/Roadmap.md`. Update architecture or focused design sources only
  when their owned decisions change.
