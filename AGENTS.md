# Vivarium Agent Instructions

Before planning or changing this repository, read these sources in order:

1. [`README.md`](README.md) — frozen architectural truth. Its principles, boundaries, invariants,
   acceptance criteria, and explicitly deferred decisions are authoritative.
2. [`Docs/Architecture.md`](Docs/Architecture.md) — how that architecture is realised by the current
   repository and which capabilities are implemented or intentionally thin.
3. [`Docs/IMPLEMENTATION_GUIDELINES.md`](Docs/IMPLEMENTATION_GUIDELINES.md) — delivery order,
   selection rules for the next vertical slice, and the current roadmap.

If these documents disagree, `README.md` wins. Do not silently resolve a genuine contradiction:
record it in the implementation plan and either choose the architecture-compliant interpretation or
ask for a product decision when the README explicitly defers it.

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
- Update `Docs/Architecture.md` and the roadmap checkpoint in
  `Docs/IMPLEMENTATION_GUIDELINES.md` when a slice materially changes what exists or what is next.

