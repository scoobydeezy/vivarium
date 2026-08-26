# Vivarium Repository Architecture

The architectural contract is summarized in [`../README.md`](../README.md); its complete normative
form is [`Architecture/Reference.md`](Architecture/Reference.md). This document records how those rules
are realized by the current repository. It is intentionally compact. See
[`ImplementationStatus.md`](ImplementationStatus.md) for the detailed capability and test inventory.

## Repository shape

```text
/Core
  /Runtime
    /Domain                 authoritative simulation; BCL only
    /Application            commands, queries, session orchestration; depends on Domain
    /Infrastructure         save/storage adapters; depends on Application + Domain
  package.json              com.vivarium.core local UPM package

/DotNet
  Vivarium.slnx
  Vivarium.Domain/          netstandard2.1; compiles Core/Runtime/Domain
  Vivarium.Application/     netstandard2.1
  Vivarium.Infrastructure/  netstandard2.1
  *.Tests/                  net10.0 xUnit projects
  Vivarium.SimRunner/       net10.0 headless runner

/Assets/Game
  /Authoring                ScriptableObject → validated Domain definitions
  /Bootstrap                composition root
  /Infrastructure           Unity/platform adapters
  /Presentation             read-model-driven views and command input
  /Editor                   editor-only validation and tooling

/Docs
  /Architecture             complete normative reference
  /Design                   focused topic decisions
  /Product                  current roadmap and playable-scenario contract
  /History                  inactive checkpoints retained for provenance
```

`Packages/manifest.json` consumes `Core` as a local package, while the .NET projects compile the same
source. There is no compiled-DLL copy workflow.

## Dependency enforcement

| Assembly | Allowed dependencies |
| --- | --- |
| Domain | BCL only |
| Application | Domain |
| Infrastructure | Application, Domain |
| Unity Authoring | Domain, Unity |
| Unity Presentation | Application, Domain, Unity |
| Unity Infrastructure | Application, Domain, Unity |
| Bootstrap | Required composition dependencies |
| SimRunner | Application, Domain, Infrastructure |

Core asmdefs use `noEngineReferences: true`; project and assembly references mechanically enforce the
boundary. The headless runner is the second guard against Unity leakage.

`DefinitionCatalog` belongs to Domain because immutable definitions are Domain input and Unity
Authoring may depend on Domain without depending on Application.

An immutable `DefinitionSet` represents a possibly incomplete contribution before resolution. Unity
Authoring converts baked pack indexes to those sets; Application resolves configured pack order and
returns the validated `DefinitionCatalog` with immutable provenance. Unity Editor owns folder discovery,
deterministic baking, and stale-index build rejection. See
[`Design/AuthoredContent.md`](Design/AuthoredContent.md).

## Language contract

Core targets `netstandard2.1` with C# 9, matching the Unity compatibility surface. Nullable reference
annotations are currently disabled in both toolchains. Do not enable language/runtime features in only
one compilation path.

## Run and verify

```powershell
# Build all .NET projects
dotnet build DotNet/Vivarium.slnx

# Run all headless tests
dotnet test DotNet/Vivarium.slnx

# Run the playable scenario
DotNet/Vivarium.SimRunner/bin/Debug/net10.0/Vivarium.SimRunner.exe demo

# Compare identical seeded runs
DotNet/Vivarium.SimRunner/bin/Debug/net10.0/Vivarium.SimRunner.exe determinism

# Verify save-before-resolution continuation
DotNet/Vivarium.SimRunner/bin/Debug/net10.0/Vivarium.SimRunner.exe saveload

# Synthetic population measurement
DotNet/Vivarium.SimRunner/bin/Debug/net10.0/Vivarium.SimRunner.exe bench 1000 1

# Opt-in measured budget
$env:VIVARIUM_ENFORCE_PERFORMANCE_BUDGETS='1'
dotnet test DotNet/Vivarium.SimRunner.Tests --filter StandardMeasuredBudget
```

## Change routing

- Authoritative state or rules belong in Domain.
- External use cases, command ingress, projections, and session coordination belong in Application.
- Encoding, storage, clocks, and platform services implement Application ports in Infrastructure.
- Unity assets convert to immutable Domain definitions; Unity views consume projections and send
  Commands.
- A new scheduled event needs a data-only payload, handler, stable type, bootstrap registration,
  semantic validation, revision dependencies where applicable, and a payload codec.
- A new authoritative state family needs identity/versioning, persistence mapping, reconstruction of
  derived indexes, determinism coverage, and save/load coverage as applicable.
- A player action that physically prevents or overrides a character's intended execution must preserve
  the original choice/intent, record distinct causal provenance, and enter character Knowledge only
  through observable evidence and attribution.

For current implemented and intentionally thin capabilities, consult
[`ImplementationStatus.md`](ImplementationStatus.md). For delivery rules, consult
[`IMPLEMENTATION_GUIDELINES.md`](IMPLEMENTATION_GUIDELINES.md).
