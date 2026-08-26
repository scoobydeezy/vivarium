# Authored Content Architecture

**Status:** Active technical design  
**Locked:** 2026-08-26  
**Scope:** Unity authoring assets, content packs, deterministic pack resolution, and staged migration
from the inline `ContentPackAsset`.

## Purpose

Vivarium content must remain cheap to create, review, merge, replace, and extend as the number of
authored definitions grows. Runtime authority remains unchanged: the simulation consumes immutable,
Unity-free Domain definitions. This design owns how production authoring sources are collected and
resolved into that catalog.

The migration is intentionally incremental. The first production slice moves Activities to independent
assets and establishes the durable pack seams. It does not ship a player-facing mod platform.

## Locked model

```text
per-entity Unity assets
        ↓ deterministic editor bake
baked pack index + pack manifest
        ↓ conversion
immutable pack contribution
        ↓ ordered resolution
validated DefinitionCatalog + immutable provenance
        ↓
simulation
```

### Entity assets

Each independently authored definition has its own `ScriptableObject` and `.asset` file. The asset owns
Inspector data, local validation, and conversion to its Unity-free Domain definition. Stable
`AuthoredId` values—not Unity references—continue to identify definitions in runtime state and saves.

Family-owned authoring entries live beside their asset wrapper rather than as nested types inside a
pack class. This prevents future assets from retaining a source dependency on the monolith being
removed.

### Pack identity and membership

A production pack is a folder containing:

- one `ContentPackManifestAsset` with stable pack id, display name, and monotonic version;
- one machine-maintained `ContentPackIndexAsset`;
- per-family subfolders containing entity assets.

Folder discovery is editor-only. Runtime and player builds load the baked index, never scan the
filesystem or use `AssetDatabase`.

The manifest is identity and metadata, not a content container. The index is build inclusion and a
deterministic inventory, not a hand-authored surface.

### Bake invariant

The baker discovers supported assets beneath a pack folder, rejects duplicate ids, sorts each family by
`AuthoredId` using ordinal comparison, and writes the index. A build validator rescans the folder and
fails the build if the discovered inventory differs from the index. A stale index must never silently
omit content from a player build.

The initial bake fingerprint covers inventory identity. A later canonical definition digest may extend
it without changing the pack or resolver boundaries.

### Contribution and catalog semantics

A pack converts to an immutable, possibly incomplete contribution. A contribution is not a valid
`DefinitionCatalog`: it may reference definitions supplied by an earlier pack.

Resolution folds contributions in configured load order. Later packs may replace a complete definition;
field-level merging is forbidden. Same-pack duplicate ids are always errors. Cross-pack replacement is
legal only when the later pack explicitly declares the family, id, and expected source pack. An
undeclared collision, missing override target, or unexpected source is an error.

Catalog-wide `ContentValidator` validation runs after resolution so cross-pack references are checked
against the effective catalog. Resolution returns the catalog together with immutable pack/order and
override provenance; it does not mutate a catalog after construction.

New ids should be pack-namespaced. Intentional overrides reuse the target id and are declared explicitly.

### Ownership boundaries

- Domain owns immutable definitions, contribution data, and final catalog validation.
- Application owns pack descriptors, ordered resolution policy, provenance, and future save-content
  compatibility analysis.
- Unity Authoring owns `ScriptableObject` conversion and baked indexes and continues to depend only on
  Domain.
- Unity Editor owns discovery, baking, and build-time freshness enforcement.
- Unity Bootstrap converts configured manifest/index assets into Application inputs.

Pack resolution remains independent of Unity so future JSON, generated, DLC, or bundle-backed sources
can produce the same contribution shape.

## Complete input inventory

Migration work must account for every current catalog input, not only the obvious arrays:

- Traits;
- Needs;
- Activities;
- Decisions;
- Interventions;
- Location Kinds;
- Commitment Templates;
- Appraisal Calibration profiles;
- Social Evidence;
- Commitment Accountability policies;
- Social Pressure definitions;
- Employment definitions;
- the singleton Decision Importance policy;
- required well-known definitions historically supplied implicitly by the legacy pack builder.

Required well-known content belongs to an explicit built-in/BaseGame contribution or explicit assets;
independent pack builders must not each inject fallback copies.

## Staged delivery

### Migrated foundation

- `ActivityDefinitionAsset` owns BaseGame Activities, including explicit Waiting, Traveling, and
  Sleeping definitions.
- Existing Trait assets, `NeedDefinitionAsset` instances, the singleton
  `DecisionImportancePolicyAsset`, and `EmploymentDefinitionAsset` instances live under the BaseGame
  pack and enter the catalog through the same baked index. Energy is explicit content rather than a
  builder fallback.
- `CommitmentAccountabilityPolicyAsset` owns the BaseGame social-commitment consequence policy.
  Employment obligations and Commitment Templates retain accountability-policy identity through
  contribution conversion and bind to the effective policy only after pack overlay. This permits
  references to an earlier pack and makes later declared policy overrides apply consistently. BaseGame
  currently has no Unity-authored Commitment Template records to migrate.
- `LocationKindDefinitionAsset`, `AppraisalCalibrationAsset`, `SocialEvidenceAsset`, and
  `SocialPressureAsset` own the BaseGame spatial-kind and social-model catalog inputs. Each social
  action/evidence model and pressure definition is independently replaceable by stable id.
- `DecisionDefinitionAsset` owns each complete Decision record, including its Options, generation
  trigger, outcomes, and compiled reasoning program. `InterventionDefinitionAsset` owns each effect and
  availability-policy record.
- The BaseGame manifest/index, deterministic bake, stale-index build validation, immutable
  contribution/resolution seams, and synthetic multi-pack resolver tests are active.
- Simulation-ready resolution rejects catalogs missing the engine-addressed Waiting or Traveling
  Activities. Composition-owned requirements remain separate: the current Unity BaseGame bootstrap
  additionally requires explicit Energy and Sleeping definitions before it creates the world.
- The legacy `ContentPackAsset` has been removed. Unity Bootstrap references the baked index directly;
  its manifest supplies pack identity/version. All populated catalog families are independently
  authored and baked; BaseGame currently contributes no Commitment Templates.

Pack-local validation constructs an immutable contribution and checks each asset. Catalog-wide
cross-reference validation remains exclusively post-resolution because one pack may intentionally
reference definitions supplied by another.

### Designed now, implemented when consumed

- canonical per-pack and resolved-catalog content digests;
- persisted resolved manifests and the corresponding save-schema migration;
- structured save/content compatibility analysis;
- pack dependency/version constraints;
- external pack discovery and loading;
- player-facing mod ordering and compatibility UI.

The resolver result carries provenance from the first slice so these additions do not require another
authoring-layout migration.

## Persistence constraints

Until multiple production packs are loadable, existing `ContentVersion` behavior remains valid. When a
resolved manifest is persisted, a manifest mismatch alone remains diagnostic rather than an automatic
load blocker, consistent with architecture §39.1. A separate Application compatibility analyzer will
distinguish missing continuation-critical definitions from harmless or reproduction-only drift.

## Pilot acceptance

The migrated foundation remains complete when:

1. Migrated families are independently editable assets and absent from legacy inline arrays.
2. The baked index is deterministically ordered across every migrated family and a stale index blocks a
   build.
3. Pack contributions cannot be mutated after construction.
4. Same-pack duplicates and undeclared cross-pack collisions fail clearly.
5. Declared full-record replacement resolves deterministically in load order.
6. The production catalog contains equivalent definitions for every migrated family and passes
   catalog-wide validation.
7. Headless tests, the full .NET suite, and the relevant Unity smoke tests remain green.
