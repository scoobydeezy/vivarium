# Management Sim — Architecture Brief

**Status:** Architecture freeze
**Scope:** Core simulation, application architecture, Unity integration, persistence, determinism, content authoring, testing, and scalability
**Primary design principle:** **The simulation is the game. Unity hosts and presents it.**

---

# 1. Purpose

This game is a management simulation centered on autonomous characters whose lives, routines, relationships, knowledge, and circumstances generate meaningful decisions.

Characters are not directly commanded. When a meaningful choice occurs, competing influences form an RPG-like decision encounter. The character ultimately chooses through a dice-based resolution system. The player may observe, learn, alter circumstances, and occasionally influence the dice, but cannot directly dictate the outcome.

The architecture must therefore support five things exceptionally well:

1. Large numbers of autonomous characters.
2. Deterministic, debuggable simulation.
3. Persistent time, including fast-forward and offline progression.
4. Partial and potentially stale player knowledge about world truth.
5. A strict separation between simulation state and its Unity presentation.

Everything else—economy, construction, jobs, hobbies, relationships, towns, towers, ships, generations, etc.—must be able to grow around that core without requiring architectural rewrites.

---

# 2. Architectural Principles

## 2.1 The simulation does not depend on Unity

The authoritative game simulation is pure C#.

The Domain layer must not reference:

- `UnityEngine`
- `MonoBehaviour`
- `GameObject`
- `Transform`
- `Time`
- Unity serialization
- Unity random functions
- UI classes
- rendering assets

A complete simulated world must be able to run in a console application or automated test environment with no Unity process running.

Unity is responsible for:

- rendering
- animation
- audio
- input
- UI
- cameras
- asset loading
- platform integration
- bootstrapping

It is not responsible for deciding what is true in the world.

---

## 2.2 Commands are the external write boundary

External callers do not directly mutate authoritative simulation state.

Player/UI/platform requests enter through application commands such as:

```text
FollowCharacter
HoldDecision
ApplyDecisionInfluence
SubmitActivityPerformance
BuildLocation
ChangeBusinessHours
AdvanceSimulation
```

Internal simulation systems, Domain Event handlers, and scheduled-event handlers may mutate domain state as part of authoritative simulation execution.

The rule is therefore:

> **Commands are the only external write door.**

Not:

> Every internal state change must itself be represented as a command.

### 2.2.1 Deterministic command ingress

External commands are wrapped in a `CommandEnvelope` and accepted into a deterministic command queue owned by the Application/session layer:

```text
CommandEnvelope
├── CommandSequence
├── Command
└── optional diagnostic/input metadata
```

`CommandSequence` is monotonic within the session and is distinct from the scheduler's `EventSequence` (§11).

Commands execute one at a time at quiescent simulation boundaries:

```text
Command #501
      ↓
mutate authoritative state
      ↓
settle Domain Events and same-instant scheduled work
      ↓
QUIESCENT
      ↓
Command #502
```

A command never interleaves authoritative mutation with another external command. Save requests are serviced against a quiescent authoritative snapshot, never halfway through a settlement cascade.

---

## 2.3 Truth, Knowledge, and Presentation are distinct

Three separate concepts must never be collapsed:

```text
WORLD TRUTH
What is actually true.

PLAYER KNOWLEDGE
What the player has observed or learned.

PRESENTATION
What the game currently chooses to show the player.

```

For example:

```text
Truth:
Mina fears disappointing Glen.
Influence = d8

Knowledge:
Player knows Mina cares strongly about Glen,
but has not identified this particular fear.

Presentation:
"Personal concern d8"

```

Another influence might instead display:

```text
???

```

Unknown information does **not** automatically imply that the entire associated die is hidden.

Visibility is content-driven.

---

# 3. Core Dependency Map

```text
┌────────────────────────────────────────────┐
│              UNITY PRESENTATION            │
│                                            │
│ Rendering • UI • Input • Audio • Views    │
└──────────────────────┬─────────────────────┘
                       │
              Commands │ Queries
                       ▼
┌────────────────────────────────────────────┐
│              APPLICATION LAYER             │
│                                            │
│ Use Cases • Command Dispatch • Queries     │
│ Session • Simulation Runner • Projections │
└──────────────────────┬─────────────────────┘
                       │
                       ▼
┌────────────────────────────────────────────┐
│                DOMAIN CORE                 │
│                                            │
│ World State • Characters • Activities     │
│ Decisions • Knowledge • Time • Scheduling │
│ Relationships • Space • Commitments • Dice│
└────────────────────────────────────────────┘
             ▲                    ▲
             │                    │
┌────────────┴──────────┐ ┌───────┴───────────────┐
│    INFRASTRUCTURE     │ │      GAME CONTENT      │
│                       │ │                        │
│ Save • Storage • Log │ │ Definitions • Balance │
│ Serialization        │ │ Traits • Decisions    │
└───────────────────────┘ └────────────────────────┘

```

Dependencies point inward.

### Allowed dependencies

| Assembly May depend on |                                     |
| ---------------------- | ----------------------------------- |
| Domain                 | BCL only                            |
| Application            | Domain                              |
| Infrastructure         | Application, Domain                 |
| Unity Presentation     | Application, Domain                 |
| Unity Infrastructure   | Application, Domain, Unity          |
| Unity Authoring        | Domain, Unity                       |
| Bootstrap              | Everything required for composition |
| Headless SimRunner     | Application, Domain, Infrastructure |

Unity Assembly Definitions will enforce the Unity-side dependencies rather than relying purely on developer discipline. Unity's assembly-definition system exists specifically to establish explicit assembly boundaries and avoid the default all-code-can-see-all-code arrangement.

---

# 4. Recommended Repository Shape

The simulation source should be usable simultaneously by Unity and normal .NET tooling without maintaining two copies.

A recommended structure is:

```text
/ManagementSim
│
├── /Core
│   ├── /Runtime
│   │   ├── /Domain
│   │   ├── /Application
│   │   └── /Infrastructure
│   │
│   ├── package.json
│   └── *.asmdef
│
├── /DotNet
│   ├── ManagementSim.sln
│   ├── Domain.csproj
│   ├── Application.csproj
│   ├── Domain.Tests.csproj
│   ├── Application.Tests.csproj
│   └── SimRunner.csproj
│
├── /Unity
│   └── ManagementSim
│       ├── Assets
│       │   └── Game
│       │       ├── Presentation
│       │       ├── Infrastructure
│       │       ├── Authoring
│       │       ├── Bootstrap
│       │       └── Editor
│       └── Packages
│
└── /Docs
    └── Architecture.md

```

The Core directory can be consumed by Unity as a local package while normal `.csproj` projects compile the same source for `dotnet test` and headless tooling.

Unity currently supports .NET Standard 2.1 as its cross-platform API compatibility profile, so Core code should remain inside that common API surface unless there is a compelling reason not to.

This gives us:

```text
Unity Editor
      │
      └─────┐
            ▼
       SAME CORE SOURCE
            ▲
            │
dotnet test / SimRunner

```

No compiled-DLL-copy workflow should be required during normal development.

---

# 5. Organization Within a Layer

Avoid giant folders called:

```text
Models/
Services/
Managers/
Helpers/

```

Prefer **feature-oriented organization inside architectural layers**.

For example:

```text
Domain/
├── Characters/
├── Activities/
├── Decisions/
├── Knowledge/
├── Observation/
├── Attention/
├── Relationships/
├── Events/
├── Scheduling/
├── Spatial/
├── Groups/
├── Time/
├── Randomness/
└── Simulation/

```

Then:

```text
Domain/Decisions/Decision.cs
Domain/Decisions/DecisionOption.cs
Domain/Decisions/DecisionResolutionService.cs

```

rather than:

```text
Models/Decision.cs
Models/DecisionOption.cs
Services/DecisionService.cs

```

This keeps related concepts together as the project grows.

---

# 6. Domain Modeling Rules

The Domain layer should contain several kinds of object.

### Entities

Objects with stable runtime identity:

```text
Character
ActivityInstance
Commitment
Relationship
Decision
Location
Group
Household
Employment

```

### Value Objects

Small immutable concepts:

```text
CharacterId
ActivityInstanceId
CommitmentId
DecisionId
SimTime
SimDuration
AnalyticalProgression
Die
InfluenceMagnitude
LocationId
FactKey

```

### Definitions

Immutable descriptions of game content:

```text
TraitDefinition
NeedDefinition
ActivityDefinition
RoutinePattern / CommitmentTemplate
DecisionDefinition
HobbyDefinition
JobDefinition
LocationKindDefinition
InfluenceDefinition

```

### Domain Services

Rules requiring coordination across entities:

```text
DecisionResolutionService
ActivityResolutionService / Strategy
KnowledgeDiscoveryService
InteractionService
SchedulePlanner

```

Entities should not merely be unrestricted property bags. They may enforce their own local invariants.

Cross-entity rules belong in domain services or simulation systems.

Avoid generic `XManager` classes.

---

# 7. Stable Identity

Runtime identity is authoritative state.

Do not use randomly generated GUIDs inside the simulation.

There are two identity categories.

### Authored IDs

Stable human-readable content identifiers:

```text
trait.ambitious
need.social
decision.job_offer
activity.traveling
location_kind.building
rng.decision.influence_roll
```

These survive builds and patches.

### Runtime IDs

Deterministically allocated instance identifiers:

```text
CharacterId 42
DecisionId 1837
ActivityInstanceId 2911
RelationshipId 4112
ScheduledEventId 92811
```

Every runtime ID family uses a monotonic allocator:

```csharp
public interface IIdAllocator<TId>
{
    TId Next();
}
```

Allocator counters are persisted in the save.

Therefore identical initial state + identical ordered inputs + identical execution order produces identical IDs.

Never use:

```csharp
Guid.NewGuid()
```

for authoritative simulation identity.

### 7.1 Runtime identity is never reused

Retiring an entity from active simulation does not erase its referential identity.

Characters may die, employments may end, relationships may dissolve, and locations may be demolished while Knowledge, Legacy history, or other durable records still refer to them.

Therefore:

> **A runtime ID is never reassigned, and an entity being absent from an active repository does not mean that identity never existed.**

The eventual implementation may use tombstones, archive records, Legacy records, or an identity directory. That storage choice is deferred. The invariant is not.

A generic historical reference may eventually take a shape such as:

```text
EntityRef
├── EntityKind
└── RuntimeId
```

without requiring every retired entity to remain fully materialized forever.

---

# 8. World State

`WorldState` represents current authoritative truth.

Conceptually:

```text
WorldState
│
├── Clock
├── Characters
├── Activities
├── Commitments / Routine State
├── Relationships
├── SpatialHierarchy / TravelNetwork
├── Groups
├── Decisions
├── PlayerKnowledge
├── AttentionState
├── Scheduler
├── History
└── RuntimeIdState
```

It need not become one enormous mutable class.

`WorldState` is the conceptual aggregate of authoritative simulation repositories and indexes.

Ephemeral presentation state such as the current camera rectangle or pointer hover does not belong in `WorldState`. Durable player-attention settings may.

---

# 9. Simulation Time

Simulation time is independent of Unity frame time.

```text
Unity Time:
"16 milliseconds have passed."

Simulation Time:
"It is Tuesday, 3:42 PM."

```

Use explicit types:

```text
SimTime
SimDuration
SimDay

```

Authoritative time should use integral units.

For example:

```text
1 SimMinute = 1 integer unit

```

or a finer integer resolution if later required.

Never make game rules depend on `Time.deltaTime`.

---

# 10. Continuous Processes Should Not Tick

Not every value that changes over time should generate repeated scheduled events.

For values such as:

- hunger
- fatigue
- loneliness
- rent accumulation
- production progress
- recovery
- gradual opinion drift
- Activity progress
- travel progress

prefer a shared **analytical progression** model.

### 10.1 AnalyticalProgression is a reusable Domain primitive

Needs, timed Activities, travel, production, and other duration/rate-driven systems all share the same underlying shape:

```text
(start value / start time / rate-or-duration)
                 ↓
       value or progress at SimTime T
```

That math should be represented through a reusable Domain primitive/convention such as `AnalyticalProgression` or `TimedProgression`, rather than independently reimplemented by each subsystem.

Example:

```text
HungerState
ValueAtLastUpdate = 4100
LastUpdatedAt = 14:00
Rate = +12/minute
```

At 15:00:

```text
Current hunger =
4100 + (60 × 12)
```

No sixty hunger events were required.

A Traveling Activity works the same way:

```text
StartedAt = 14:05
CompletesAt = 14:17
```

At 14:11 its progress is analytically 50%; the Domain never performs per-frame position updates.

When an analytical rate or parameter changes:

1. Materialize the current value/progress.
2. Store the new reference timestamp.
3. Apply the new rate/parameters.
4. Increment the relevant aspect-scoped revision.
5. Recompute and reschedule the next behaviorally meaningful threshold/completion event.

Discrete events are for meaningful state transitions.

Continuous values are computed from time.

This is a major scalability mechanism.

### 10.2 Threshold-crossing scheduling is mandatory, not optional

If a decision, event precondition, Activity transition, or any other simulation behavior can be _gated_ by an analytical value crossing a threshold, nothing else in the system will necessarily notice that crossing on its own. The rule is therefore:

> **Any analytically progressing quantity whose threshold can change simulation behavior must schedule its next relevant threshold crossing as a real scheduled event.**

Not every numerical threshold imaginable — only behaviorally meaningful ones.

When the underlying rate or value changes:

```text
materialize current value
      ↓
invalidate the existing threshold/completion event
      ↓
increment the relevant aspect-scoped revision
      ↓
calculate the next crossing
      ↓
schedule it
```

This preserves the scaling benefit of analytical values without losing responsiveness to state changes that should matter immediately.

---

# 11. Discrete Event Scheduler

The scheduler is foundational simulation infrastructure.

A scheduled event is pure serializable data:

```text
ScheduledEvent
├── Id
├── DueAt
├── Phase
├── EventSequence
├── EventType
├── Payload
└── Dependencies[]

```

Deterministic ordering is:

```text
DueAt
→ Phase
→ EventSequence

```

`EventSequence` is monotonically allocated and persisted. It is a scheduler-local ordering counter and is not the same value as `CommandSequence`.

Two events can therefore never have undefined ordering.

---

## 11.1 Event invalidation

Future events can become obsolete.

Example:

```text
3:42 MinaLeavesWork

```

becomes invalid when Mina loses that job at 3:20.

The scheduler therefore supports:

```text
Schedule
Cancel
Reschedule
AdvanceUntil
PeekNext

```

and every scheduled event is revalidated when executed.

---

## 11.2 Revision dependencies

Events may record state revisions:

```text
Character Mina        revision 18
Employment #72         revision 4
Relationship #122      revision 9

```

These provide cheap stale-event detection.

Dependencies may include multiple entities.

A mismatch means an event is likely obsolete.

However:

> **Revision checks are an optimization. Semantic validation is authoritative.**

### 11.2.1 Revisions are aspect-scoped, never monolithic

A single per-entity revision counter is prohibited for normal event invalidation. If bumped on every change to a character, it invalidates every pending event for that character — including ones with no logical dependency on what actually changed — and turns routine state updates into invalidation storms.

Instead, revisions are scoped to the specific aspect an event actually depends on:

```text
Mina.ScheduleRevision
Mina.ActivityRevision
Mina.Needs.HungerRevision
Mina.Needs.SleepRevision

Employment #72 Revision
Relationship #122 Revision

```

or more generically, a `RevisionKey` of `(Entity/Aggregate, Aspect)`.

A scheduled event records only the revisions it actually depends upon:

```text
MinaLeavesWork

depends on:
Employment #72 rev 4
Schedule:Mina rev 18

```

This event does not care that Mina became slightly hungrier in the meantime.

> **Invariant: revision scopes must be as narrow as the dependency they protect. A monolithic per-entity revision is prohibited for normal event invalidation.**

---

## 11.3 Event handlers

Serialized payloads contain no behavior.

Handlers do.

Conceptually:

```csharp
public interface IScheduledEventHandler<TPayload>
{
    bool CanExecute(
        WorldState world,
        TPayload payload);

    void Execute(
        WorldState world,
        TPayload payload,
        SimulationContext context);
}

```

A handler registry maps stable event types to handlers.

```text
ScheduledEvent
      ↓
HandlerRegistry
      ↓
Revision check
      ↓
Semantic validation
      ↓
Execute / discard

```

---

## 11.4 Same-Instant Cascades and Quiescence

Handling one scheduled event may itself schedule another event at the _same_ due time. This must be handled explicitly rather than left implicit.

The rule is that same-instant work **settles** before the simulation instant is considered complete:

```text
Advance to 10:00
      ↓
process scheduled events
      ↓
new 10:00 events appear
      ↓
process those
      ↓
repeat
      ↓
no more work at 10:00
      ↓
QUIESCENT

```

### Ordering remains deterministic

Same-time work still obeys `DueAt → Phase → EventSequence`. Newly scheduled same-time events receive later `EventSequence` values than whatever caused them.

An event must **not** schedule same-time work into a phase earlier than the one currently executing. That would imply retroactively inserting work before its own cause.

### Runaway cascades fail loudly, not silently

A configurable `MaxSettlementWorkPerSimulationInstant` bounds total same-instant settlement work (scheduled events plus Domain Event reactions), set generously high. If exceeded, the scheduler raises `SimulationCascadeLimitExceeded`:

- In development/test builds: fail immediately with a diagnostic trace.
- In production: pause authoritative advancement, capture diagnostics, and surface/recover according to game-level error handling.

This is a deliberate choice: silently deferring the remainder of a cascade to the next scheduler pass would quietly change simulation semantics based on an arbitrary implementation limit. Discovering "Event A caused B caused A 10,000 times" as a loud failure is preferable to it silently becoming different gameplay.

The simulation only publishes read models after the complete settlement loop — scheduled events and Domain Event reactions — reaches quiescence (see §12.1 and §13.1).

---

# 12. Scheduled Events vs Domain Events

These must remain distinct.

### Scheduled Event

Something that may happen in the future.

> Mina's shift ends at 3:42.

Persistent and saved.

### Domain Event

Something that just happened.

> MinaQuitJob.

Used to trigger deterministic reactions in other Domain systems.

Transient by default; not itself save state unless promoted into History or another persistent entity.

### Presentation Notification

Something the player might care about.

> Mina left her job.

Derived from game state/domain events according to player Attention and presentation policy.

Never use one giant global event bus to represent all three.

## 12.1 Domain Event settlement is deterministic

Domain Events participate in the same authoritative settlement boundary as scheduled work.

```text
MinaQuitJob
      ↓
ordered Domain Event handlers
      ↓
handlers may mutate state, emit more Domain Events,
or schedule same-instant/future work
      ↓
reaction queues settle
      ↓
no remaining same-instant work
      ↓
QUIESCENT
```

For each Domain Event type, handler order is explicitly registered and stable. It must **not** depend on incidental subscription order, assembly load order, reflection enumeration, or content load order.

Handlers may emit further Domain Events. Those reactions are processed deterministically as part of the current settlement cycle.

The runaway guard in §11.4 covers the complete settlement workload — Scheduled Events **and** Domain Event reactions — so a loop such as `A → B → A` fails loudly rather than hanging indefinitely.

Domain Event dispatch remains an internal simulation mechanism, not an externally writable global bus.

---

# 13. Authoritative Concurrency Model

All authoritative world mutation has exactly **one owner**.

Initially:

> **Simulation execution is single-threaded.**

That does not necessarily mean it must permanently run on Unity's main thread.

It means no two threads may concurrently mutate `WorldState`.

Presentation reads projections or immutable snapshots.

If parallel optimization is introduced later, worker threads may perform pure calculations:

```text
Snapshot
   ↓
parallel calculation
   ↓
results
   ↓
deterministically ordered application
   ↓
Simulation owner

```

Worker jobs never freely mutate world entities.

## 13.1 Quiescent Read Boundaries

Presentation is guaranteed a consistent view of `WorldState` **only at quiescent simulation boundaries** — the points where same-instant cascades (§11.4) have fully settled.

```text
Command / Advance request
        ↓
Simulation mutation
        ↓
Scheduled consequences / Domain reactions
        ↓
Same-instant settlement reaches quiescence
        ↓
Indexes validated/updated
        ↓
QUIESCENT POINT
        ↓
Publish projections / snapshots

```

Unity cannot observe an in-progress mutation (e.g. "Mina quit her job, but employment membership hasn't been removed yet") — that intermediate state is simply not externally observable.

During long offline catch-up, progress may be published periodically, but only at safe quiescent boundaries, never mid-step.

---

# 14. Deterministic Randomness

Randomness is a core game mechanic and must be fully reproducible.

Do not use:

```text
UnityEngine.Random
System.Random scattered throughout systems
a single global RNG stream

```

Use a counter-based deterministic random oracle.

Conceptually:

```text
Random =
FixedHash(
    WorldSeed,
    ScopeType,
    ScopeId,
    Purpose,
    RollIndex
)

```

Example:

```text
WorldSeed:    827119
Scope:        Decision
ScopeId:      1837
Purpose:      option.accept/influence.ambition
RollIndex:    0

```

A reroll uses:

```text
RollIndex: 1

```

The result is independent of unrelated RNG activity elsewhere in the world.

`ScopeType` and `Purpose` must themselves be stable deterministic identifiers. In particular, `Purpose` should use authored IDs such as:

```text
rng.decision.influence_roll
rng.relationship.interaction
rng.character.initial_trait
```

rather than method names, arbitrary display strings, or other identifiers likely to change during refactoring.

The hashing/mixing implementation must itself be explicitly fixed and deterministic. Never use runtime-dependent functions such as:

```csharp
string.GetHashCode()

```

as an authoritative seed component.

Because RNG scoping is keyed by runtime IDs (`ScopeId`), those IDs must themselves be deterministically allocated (see §7) — otherwise two "identical" seeded runs could scope their RNG streams differently and diverge despite correct hashing.

---

# 15. Deterministic Ordering

Scoped randomness does not eliminate ordering requirements.

Any simulation operation whose ordering may affect state must use explicit ordering.

Never rely on iteration order from:

```text
Dictionary
HashSet
unordered queries

```

where outcomes depend on traversal order.

Use stable ordering such as:

```text
CharacterId
DecisionId
EventSequence
Definition order

```

where necessary.

Determinism means:

> **Same initial authoritative state + same ordered commands + same content version + same simulation-rules version + same random-algorithm version + same seed = same authoritative outcome.**

A different executable ruleset may intentionally produce a different future world from the same saved state. Version metadata exists to make that difference explicit and diagnosable, not to pretend rules never evolve.

---

# 16. Authoritative Numeric State

Prefer deterministic integer/fixed representations for state that can affect simulation branching.

Examples:

```text
Hunger:       0–10,000
Affinity:    -10,000–10,000
Probability:  basis points
Production:   integer units
Time:         integer simulation units

```

Rendering is free to convert these to floats.

Float math may still be used where its exact value cannot change authoritative branching.

---

# 17. Decisions

A Decision is a persistent runtime entity.

Conceptually:

```text
Decision
├── DecisionId
├── Character / Participants
├── DefinitionId
├── CreatedAt
├── ResolveAt
├── Status
├── Options[]
├── Influences[]
├── InfluenceRevision
├── InterventionState
├── AttentionState
└── Resolution

```

An option might have:

```text
TAKE JOB

Ambition               d10
Enjoys Baking            d8
Better Pay               d6
Fear of Stagnation       d6

```

Another:

```text
STAY

Family Routine           d8
Friendship               d6
Commute                  d6

```

The simulation constructs the **true influence set**.

The query/presentation layer determines how much the player sees.

### 17.1 A character may have multiple active Decisions

Forbidding concurrent decisions globally is artificial — Mina can plausibly be deciding whether to accept a promotion while also deciding whether to attend Glen's birthday, and those decisions don't logically compete.

> **A character may have multiple active Decisions.**

Decision Definitions may declare a **conflict scope**:

```text
Decision: AcceptBakeryJob
Conflict: Employment:Mina

```

While that decision is unresolved, no other mutually-exclusive decision within the same conflict scope is generated for Mina. Decisions in unrelated scopes (e.g. `Social:GlenBirthday`) coexist freely.

Held-decision capacity (§20) applies to the total active held decisions for a character across all of their concurrent Decisions, not per-decision.

### 17.2 Active Decisions are living runtime state

An open Decision is not a frozen snapshot of the world. Definition-derived semantics are snapshotted when the Decision is constructed (§42.1), but **world-derived influences may evolve while the Decision remains active**.

Examples:

```text
Mina is considering moving in with Darius.

Day 1:
Relationship        d10
Save Money           d6
Good Location        d6

Day 2:
A new apartment opens beside Mina's job.

Relationship        d10
Save Money           d6
Excellent Location  d10
```

Relevant world changes may therefore add an influence, remove one, change its magnitude, or alter its presentation before resolution. These updates happen through deterministic Domain reactions, not polling.

Active Decisions should record or expose the **dependency keys/contexts that can affect their world-derived influences** so relevant changes can target the Decisions that may need reevaluation. A `DecisionDependencyIndex` (or equivalent indexed registration) may map those dependency keys back to active Decisions. This prevents every unrelated world change from scanning every open Decision.

Conceptually:

```text
World change
    ↓
Domain Event
    ↓
Relevant active Decisions reevaluated
    ↓
Influence set changes
    ↓
Decision.InfluenceRevision increments
    ↓
Projection refreshes at quiescence
```

Each `DecisionInfluence` must have stable runtime identity within the Decision. Player interventions target that identity rather than a mutable array position, so changing or reordering the influence set cannot silently retarget an already-applied intervention.

The distinction is:

```text
Definition-derived decision rules
→ snapshotted at Decision construction

World-derived influences
→ reevaluated as relevant circumstances change
```

This allows decisions to develop over time without letting hot reload alter their underlying rules.

---

# 18. Decision Resolution

The broad pipeline is:

```text
World circumstances
        ↓
Decision generation
        ↓
True influences constructed
        ↓
Attention policy
        ↓
Player may ignore / inspect / intervene
        ↓
Deterministic dice resolution
        ↓
Degree of success / outcome
        ↓
Consequences
        ↓
World state changes

```

Decision generation and resolution must not depend on Unity.

---

# 19. Player Intervention

The player may influence choices but not directly choose outcomes.

Potential capabilities include:

```text
add die
remove die
step die up/down
reroll
replace die
apply temporary stat modifier
alter circumstances

```

These are game content, not architectural requirements.

The architectural requirement is that intervention occurs through validated commands.

Example:

```text
ApplyDecisionInterventionCommand
├── DecisionId
├── InterventionDefinitionId
└── TargetInfluenceId

```

Rules have one authority:

```text
DecisionInterventionRules.Evaluate(...)

```

The UI uses this evaluation to determine whether controls should appear enabled.

The command handler performs the same authoritative validation before mutating anything.

No duplicated UI validation logic.

---

# 20. Attention and Held Decisions

Attention is gameplay state.

A character or decision may have policies such as:

```text
Normal
Watch
Hold
Quiet
```

Exact names are not frozen.

The architecture must support:

- prioritizing decisions
- surfacing watched characters
- withholding auto-resolution
- player-configurable decision categories
- gating optional interactive Activity resolution
- bounded held-decision capacity

### 20.1 Observation and Attention share a canonical WatchState signal

Observation and Attention remain distinct systems because they answer different questions:

```text
Observation:
What can the player learn from watching this character?

Attention:
What should surface, pause for optional intervention,
or become interactively playable?
```

They must **not** independently track whether the player is watching Mina.

Presentation/Application produces one canonical semantic `WatchState`/watch-signal source. Both systems consume it.

Examples of semantic inputs include:

```text
Character became visible for meaningful observation
Character stopped being visible
Character selected / inspected
Character followed / unfollowed
```

Not every watch signal is necessarily persistent. For example, camera visibility may be ephemeral while a Follow setting is durable Attention state. The key architectural rule is that there is one canonical signal/source, not duplicated tracking in Observation and Attention.

Held decisions must never grow without bound.

A `DecisionHoldPolicy` controls:

```text
maximum global held decisions
maximum held decisions per character
priority
overflow resolution
```

Since a character may hold multiple concurrent Decisions (§17.1), the per-character cap governs the total across all of that character's active decisions, not one cap per decision.

Overflow behavior is deterministic.

Example ordering:

```text
lowest importance
→ oldest creation time
→ lowest DecisionId
```

When capacity is exceeded, an appropriate held decision auto-resolves and is reported in the recap/history.

---

# 21. Simulation Modes

Simulation execution receives an explicit context.

At minimum:

```text
Live
PlayerFastForward
OfflineCatchUp

```

Core physical/game rules remain the same.

Systems involving player availability may vary behavior by context.

Examples:

```text
Should notification be emitted immediately?
Can a decision remain held?
Should an offline recap entry be generated?
Should presentation animation be skipped?

```

Offline progression is therefore not merely:

> run Live mode extremely fast.

It is a formally represented simulation context.

The elapsed duration for `OfflineCatchUp` is calculated by the Application/Infrastructure layer from a persisted offline-progression anchor (§38) and `IRealWorldClock`; the Domain never reads wall-clock time directly.

---

# 22. Knowledge Architecture

Player knowledge is not a direct pointer to current truth.

A discoverable item is identified by a `FactKey`.

Example:

```text
FactKey
├── Kind: Relationship.Resentment
└── RelationshipId: 52

```

Truth might currently be:

```text
Strong

```

Player knowledge might contain:

```text
ObservedValue: Moderate
ObservedAt: Day 81, 14:20
Confidence: Known
DiscoverySource: Conversation

```

The relationship may since have changed.

Therefore:

```text
Current Truth ≠ Player Knowledge

```

Knowledge can naturally become stale.

This is intentional.

---

# 23. Knowledge Entries

A knowledge entry may eventually include:

```text
FactKey
ObservedValue
ObservedAt
Confidence
DiscoverySource

```

Potential future concepts include:

```text
Suspected
Confirmed
Stale
Contradicted

```

These are not required for the initial implementation.

The architecture merely preserves the possibility.

### 23.1 DiscoverySource is a weak reference

Knowledge must survive history pruning (§37). `DiscoverySource` is primarily durable descriptive/value data, not a live foreign key:

```text
Observed during conversation
Observed workplace behavior
Learned from Mina
Inferred from repeated visits

```

An optional `SourceEventId?` may be retained for recent/history navigation, but it is a **weak reference**. If the underlying history event is later pruned or compacted, the `KnowledgeEntry` remains completely valid — it does not depend on that event still existing.

---

# 24. Discoverability

Gameplay systems should not manually maintain a parallel Fact database.

Instead, domain aggregates expose discoverable truth systematically through providers.

Examples:

```text
CharacterFactProvider
RelationshipFactProvider
EmploymentFactProvider
HouseholdFactProvider

```

Conceptually:

```text
Truth
   ↓
Fact Providers
   ↓
Potential Discoverable Claims
   ↑
Observations
   ↓
Knowledge Discovery Rules
   ↓
Knowledge Entries

```

Definitions can configure how something may be discovered.

Example:

```text
Trait: Ambitious

Discoverable through:
- career decisions
- repeated work behavior
- conversation

```

Adding a new discoverable system must involve an explicit fact-provider path rather than scattered knowledge bookkeeping.

---

# 25. Observation

Observation is a first-class gameplay input and a consumer of the canonical WatchState described in §20.1.

Unity/Application can know semantic facts such as:

```text
Mina became meaningfully visible.
Mina is selected.
Mina's profile is open.
Player is following Mina.
```

These facts do not themselves automatically reveal Knowledge.

They become semantic observation inputs:

```text
Presentation / Application
      ↓
canonical WatchState / observation signals
      ↓
Observation System
      ↓
Knowledge Discovery
```

Do not emit observation commands every rendered frame.

Presentation should aggregate meaningful state transitions such as:

```text
BeginObservingCharacter
EndObservingCharacter
InspectCharacter
InspectRelationship
```

The Domain/Application layer decides what those observations can teach.

Attention consumes the same watch signal for surfacing/interactive-policy decisions, but does not own Knowledge discovery.

---

# 26. Influence Presentation

The true Decision and the player-facing Decision View are different models.

For each influence, presentation may independently expose:

```text
existence
category
label
magnitude / die size
specific explanation

```

Therefore an influence might appear as:

```text
Fear of disappointing Glen d8

```

or:

```text
Friendship concern d8

```

or:

```text
Personal concern d8

```

or:

```text
??? d8

```

or:

```text
???

```

or not be shown at all.

This policy is determined by content + player Knowledge.

The number of hidden influences is therefore not inherently exposed either.

---

# 27. Spatial Hierarchy

Containment hierarchy and navigation topology are two different things and must be modeled separately (see §28). This section covers containment only.

The world cannot assume a tower.

Use generic hierarchical containment.

Examples:

```text
World
└── Region
    └── Town
        └── District
            └── Building
                └── Floor
                    └── Room

```

or:

```text
World
└── Ship
    └── Deck
        └── Section
            └── Compartment
                └── Station

```

Underlying model:

```text
LocationNode
├── LocationId
├── ParentLocationId?
└── LocationKindId

```

`LocationKindId` is content-defined.

Containment answers **"what contains this place?"** It does not answer **"can a character get from here to there, and how long does that take?"** — that's the Travel Network.

---

# 28. Travel Network

Containment hierarchy does not encode navigability. A separate `TravelNetwork` answers:

> Can Mina walk from Room A to Room B, and how long does that take?

Conceptually:

```text
Locations = nodes / destinations

Travel connections:
A ↔ B
B ↔ C
C ↔ Elevator
Elevator ↔ Floor 12

```

A spaceship could have corridors and lifts. A town could have roads and paths. A country could have roads, trains, airports. A tower could have stairs and elevators.

The setting changes. The abstraction doesn't.

`TravelNetwork` conceptually includes:

```text
Connections
Travel costs
Travel modes
Route planning

```

---

# 29. Activities, Commitments, Routines, and Travel

The architecture needs an authoritative answer to four different questions:

```text
WHERE is Mina?
    Current Activity's SpatialContext

WHAT is Mina doing?
    Current ActivityInstance

WHAT is Mina committed/planning to do?
    Commitment / Routine planning

WHEN does something change?
    Scheduler
```

These concepts must remain distinct where they represent different semantics, while avoiding duplicate authoritative state.

## 29.1 A character has exactly one active primary Activity

Every active character has exactly one authoritative primary `ActivityInstance` at a time.

Conceptually:

```text
ActivityInstance
├── ActivityInstanceId
├── CharacterId
├── DefinitionId
├── StartedAt
├── AnalyticalProgression / completion parameters
├── SpatialContext
│   ├── Located(LocationId)
│   └── Traveling(TransitDetails)
├── SourceCommitmentId?
├── ResolutionStatus / accepted result
└── ActivityRevision
```

Examples of Activities:

```text
Working
Eating
Sleeping
Socializing
Studying
Recreation
Waiting
Traveling
```

This makes `ActivityInstance` the authoritative answer to **"what is this character doing?"** There is no separate mutable `SpatialPresence` field that can drift out of sync with Activity state.

If richer multitasking is introduced later (for example talking while walking), it should be modeled as a modifier, sub-behavior, interaction, or other explicitly subordinate concept rather than a second competing primary Activity.

## 29.2 Spatial presence is a projection of Activity SpatialContext

A non-travel Activity normally has:

```text
SpatialContext = Located(Bakery)
```

A travel Activity has:

```text
DefinitionId = activity.traveling
SpatialContext = Traveling(...)
```

`Traveling` is therefore a system-provided Activity kind whose route/timing parameters are supplied by the `TravelNetwork`, not a parallel Transit subsystem.

Conceptually:

```text
TransitDetails
├── OriginLocationId
├── DestinationLocationId
├── TravelPlanId / Route
├── DepartedAt
├── ArrivesAt
└── TravelModeId
```

When Mina leaves the café:

```text
Working @ Café
      ↓
Traveling(Café → Home)
      ↓
AtHome / next Activity @ Home
```

She is not simultaneously stationary at the origin and destination.

Travel progress is ordinary `AnalyticalProgression` (§10). At any simulation time the Domain can derive travel progress from committed parameters; Presentation may interpolate that progress visually at frame rate without authoritatively ticking position.

If meaningful route legs are later required, `TravelPlan` may contain them without changing the Activity abstraction.

## 29.3 Commitments are planning intent, not Scheduled Events

A `Commitment` represents something a character intends, is obliged, or has agreed to do:

```text
Commitment
├── CommitmentId
├── CharacterId / Participants
├── Kind
├── EarliestStart / ScheduledWindow
├── ExpectedDuration
├── Location / spatial requirement
├── Priority / obligation
└── Source (Employment, Household, Event, etc.)
```

Examples:

```text
WorkShift
BirthdayParty
DoctorAppointment
ClubMeeting
DinnerWithDarius
```

Commitments remain distinct from Scheduled Events.

> **Commitment is pre-execution planning intent. ScheduledEvent is concrete simulation execution.**

This separation allows the planner to detect conflicts such as overlapping work and social commitments before concrete Activity transitions are scheduled.

## 29.4 Routine planning is reactive with bounded look-ahead by default

Recurring routines/obligations should not eagerly materialize an infinite future calendar.

Default strategy:

> **Recurring patterns/templates are materialized into concrete Commitments reactively and only across the bounded planning horizon required by current simulation or a future-facing query.**

The `SchedulePlanner` may plan the next relevant Activity/transition on demand. It may materialize farther ahead when something explicitly needs future knowledge, such as:

```text
schedule-conflict UI
travel/departure planning
threshold behavior that depends on a future commitment
player inspection of tomorrow's schedule
```

The exact planning horizon is tuning/configuration, not architectural identity.

Changing a commitment or routine invalidates only the relevant planned transitions through aspect-scoped revisions.

## 29.5 Activity transitions use the Scheduler

The planner may transform a WorkShift commitment into concrete transitions such as:

```text
08:42 Begin Traveling(Home → Bakery)
08:57 Complete Traveling / arrive Bakery
09:00 Begin Working
15:00 Complete Working
```

The scheduler answers **when** these transitions are due; the Activities subsystem answers **what** state they create and **why**.

A scheduled event that requires a particular Activity/spatial context validates that precondition at execution time. If Mina is still `Traveling` when a Work Activity should begin, she is late rather than magically present.

## 29.6 Activity resolution supports automatic and optional interactive paths

Every Activity that can produce a gameplay performance/outcome must have an autonomous resolution path. This is non-optional for simulation scalability.

Conceptually:

```text
IActivityResolutionStrategy
├── ResolveAutomatic(WorldState, ActivityInstance) -> ActivityPerformanceResult
└── SupportsInteractiveResolution
```

The exact interface shape may evolve; the invariant is more important than the syntax.

> **Automatic resolution is always available. Interactive resolution is an optional alternate input path, never a requirement for simulation progress.**

If content permits interactive resolution and Attention/WatchState makes that Activity eligible, Unity may present a mini-game or richer interaction.

The mini-game never directly mutates Domain state. It submits a normalized result through a command such as:

```text
SubmitActivityPerformanceCommand
├── ActivityInstanceId
└── ActivityPerformanceResult
```

`ActivityPerformanceResult` is content-agnostic normalized outcome data (grade/magnitude/tier), not raw UI telemetry.

Both paths feed the **same Activity consequence pipeline**:

```text
Automatic dice/analytical resolution ─┐
                                      ├→ ActivityPerformanceResult
Player-provided interactive result ───┘
                                      ↓
                              Activity consequences
                                      ↓
                                  WorldState
```

Human-played results are external command input, analogous to a Decision intervention, and therefore do not threaten determinism. Diagnostics log them explicitly as player-provided outcomes rather than RNG results.

Decisions and Activities may share small resolution-source/consequence conventions where genuinely common (for example `Automatic` vs `PlayerProvided` provenance), but they remain separate Domain entities: a Decision is a branching choice, while an Activity is extended-duration doing. Do not merge them into a generic do-everything resolvable entity.

In-progress mini-game timing, animation, button state, and raw score telemetry are Presentation state, not authoritative save state. If play is interrupted before a normalized result is accepted, policy may resume, discard, or fall back to automatic resolution without requiring Domain mini-game state.

## 29.7 Time-varying context affects an Activity over the interval it was true

Activity resolution must not look only at the world snapshot that happens to exist when the Activity completes. Context may change during an extended Activity and must be accumulated for the portion of time in which it applied.

Example:

```text
09:00 Mina begins Working
11:20 hated boss enters the room
11:20 materialize accumulated work performance
11:20 apply boss-present modifier / new performance rate
11:40 boss leaves
11:40 materialize accumulated work performance
11:40 remove boss-present modifier / restore rate
15:00 Working completes
```

The boss's twenty-minute presence therefore matters for twenty minutes rather than being ignored because the boss was absent at 15:00.

This uses the same analytical-progression pattern as needs and travel: whenever a context change alters an Activity's authoritative progression rate or outcome parameters, materialize progress at the change time, bump the relevant Activity/aspect revision, apply the new parameters, and recompute any behaviorally meaningful completion/threshold schedule.

Short subordinate interactions may coexist with a primary Activity (for example, Mina can remain `Working` while conversing with Glen). Those interactions may modify the primary Activity's contextual performance without becoming a second competing primary Activity.

---

# 30. Spatial Indexes

A character's spatial presence is derived from the current Activity's `SpatialContext`.

The spatial service must efficiently answer:

```text
Where is Mina?
Who is in this room? (Located occupants only)
Who is in this building?
Who is in this district?
How many residents are in this settlement?
Who is currently Traveling?
```

**Direct occupancy excludes Traveling Activities** unless a specific travel context is itself modeled as an occupiable location (for example, an elevator car).

Indexes are maintained on Activity transitions rather than by scanning all current Activities on every query.

Implementation may maintain direct and ancestor occupancy indexes plus a Traveling-character index. When travel itself is an interaction context, active Traveling Activities may additionally be indexed by an interaction-relevant route/journey/segment key so two travelers can become candidates for interaction without scanning every traveler in the world. The exact route-segment model remains setting/pathfinding dependent.

The public API should not require scanning the entire character population, and locations must support efficient parent/ancestor traversal.

Where useful, bidirectional membership bookkeeping may reuse a generic `IndexedMembership<TContainer, TMember>` primitive, while spatial indexing may layer hierarchy-specific ancestor caches on top.

---

# 31. Non-Spatial Groups

Spatial hierarchy remains a tree.

Social and organizational membership is a separate system.

Examples:

```text
Household
Employer
Club
Friend Group
Team
School
Faction
Organization

```

A character may belong to many such groups simultaneously.

Use a `MembershipIndex` capable of efficient queries in both directions. Its maintain-on-write bookkeeping may share a generic bidirectional membership primitive with occupancy indexing, while group and spatial semantics remain separate:

```text
Character → Groups
Group → Members

```

Do not force non-spatial concepts into the location hierarchy.

---

# 32. Social Interaction Scaling

Never perform global pairwise character interaction scans.

Forbidden:

```text
for every Character A
    for every Character B
        CheckInteraction(A, B)

```

Interaction opportunities arise through shared context:

```text
same location
same workplace
same household
same activity
same event
same group
same journey

```

The relevant index produces **candidate participants**, not an instruction to enumerate every possible pair inside that context.

Large shared contexts can still be enormous — a concert, station, school, or city square may contain hundreds or thousands of characters. Interaction policies must therefore bound candidate selection using context-appropriate mechanisms such as:

```text
nearby / same subcontext
existing acquaintances
shared task or activity participants
relationship relevance
priority or affinity filters
bounded deterministic/random sample
```

A shared context creates a candidate pool; it does **not** justify an O(k²) pair scan within that pool. Any random sampling uses the deterministic random oracle and stable semantic scopes.

Relationship processing therefore scales with the number of interaction opportunities actually selected, not total population size or every possible pair within a large context.

---

## 32.1 Production Social and Relationship Model

Relationships use the matrix-first model specified by [`SocialModelBrief.md`](SocialModelBrief.md).
This product decision resolves the previously deferred NPC-belief and relationship-formula questions.

```text
true latent personality
→ observer-scoped uncertain belief
→ directional sparse appraisal field
→ calibrated appraisal lens
→ values/interests + directional history + familiarity + affect/context
→ explainable Decision/Activity/interaction pressure
```

Authoritative social math is deterministic fixed point. Named traits are projections of the latent
space and never a second scoring channel. Beliefs are sparse observer→target edges in the generalized
Knowledge model; unknown and neutral remain distinct. Relationship pairs retain stable runtime identity
and indexing, but durable channels and familiarity are directional. Affiliation, Respect, Comfort,
Attraction, and Reliance may disagree without being collapsed into one compatibility score.

Social evidence comes from bounded shared-context witnesses and updates beliefs jointly. It does not
directly rewrite personality truth. Reputation and perceived group norms are bounded Knowledge facts,
not omniscient truth or recursive unlimited theory of mind. Social evaluations may feed Decisions,
Activity modifiers, and interaction relevance, but never directly choose behavior.

---

# 33. Application Layer

The Application layer represents game use cases.

Examples:

```text
StartNewGame
LoadGame
AdvanceSimulation
ChangeGameSpeed

FollowCharacter
HoldDecision
ReleaseDecision
ApplyDecisionIntervention

InspectCharacter
InspectDecision

BuildLocation
ChangePolicy
ChangeOperatingHours

SaveGame

```

It coordinates Domain behavior but does not contain presentation logic.

---

# 34. Command Convention

Establish a lightweight convention:

```csharp
ICommand<TResult>
ICommandHandler<TCommand, TResult>
ICommandDispatcher
```

and a deterministic ingress envelope:

```text
CommandEnvelope
├── CommandSequence
├── Command
└── optional diagnostics/input metadata
```

Begin with a simple in-house dispatcher/queue.

`CommandSequence` orders external command ingress. `EventSequence` (§11) orders same-time scheduled events. They are deliberately separate counters with different scope and lifetime.

Do not install a mediator/DI framework merely because one may eventually be useful.

The convention is architectural.

The implementation can evolve.

---

# 35. Queries and Read Models

UI should not directly bind itself to mutable Domain entities.

Instead:

```text
Application Query
      ↓
Projection
      ↓
Read Model
      ↓
Unity UI

```

Examples:

```text
CharacterProfileView
DecisionView
LocationView
GroupView
ScheduleView
DecisionFeedView

```

This is particularly important because projections incorporate player Knowledge.

---

# 36. Query Strategy

Use two strategies.

### On-demand projections

Appropriate for focused views:

```text
GetCharacterProfile(Mina)
GetDecision(1837)

```

### Indexed/materialized projections

Appropriate for frequently repeated aggregate queries:

```text
all unemployed residents
all critical needs
all characters in location
all unresolved decisions
all watched characters

```

Do not materialize every conceivable view.

Index according to demonstrated access patterns.

Materialized read models and indexes should generally be reconstructible rather than treated as canonical save truth.

---

# 37. Domain History and Retention

Active Decisions do not remain forever.

Decision lifecycle:

```text
Active
   ↓
Resolved
   ↓
Recent History
   ↓
Summary / Memory / Statistics
   ↓
Pruned

```

Retention terminology:

```text
Ephemeral
Recent
Significant
Legacy

```

"Legacy" replaces the earlier notion of "Permanent" — **durability and storage granularity are separate concerns.** Long-lived historical significance does not mean unbounded raw history forever; it means the information survives while its representation compacts over time.

Example:

```text
Skipped bowling Tuesday
→ eventually discarded (Ephemeral)

Married Darius
→ Significant at the time, compacts to a Legacy summary decades later

```

A marriage may initially be a detailed Significant event. Two hundred simulated years later it may compact into: "Mina married Darius in Year 14." A whole deceased ancestor might eventually become a compact biographical record:

```text
Mina Cairn
12–87

Baker
Married Darius
Two children
Founded East Market Bakery

```

This is what makes generational-scale simulation ("generations" per §1) bounded rather than accumulating raw history indefinitely.

---

# 38. Persistence

Save data is explicitly versioned DTO data.

Do not serialize runtime Domain objects directly.

Conceptually:

```text
SaveGame
├── SchemaVersion
├── ContentVersion
├── SimulationRulesVersion
├── RandomAlgorithmVersion
├── WorldSeed
├── SimulationClock
├── OfflineProgressionAnchor
│   └── SavedAtRealTimeUtc (or equivalent infrastructure-owned value)
├── RuntimeIdCounters
├── WorldEntities
├── Relationships
├── Activities / Commitments / Routine State
├── SpatialHierarchy / mutable TravelNetwork state (if any)
├── GroupMembership
├── ActiveDecisions
├── Knowledge
├── Attention
├── Scheduler
│   ├── PendingEvents
│   └── NextEventSequence
└── SignificantHistory
```

The offline anchor allows Application/Infrastructure to calculate elapsed wall-clock time on load:

```text
IRealWorldClock.Now
      -
Saved offline anchor
      ↓
elapsed real duration
      ↓
offline progression policy / clamp
      ↓
AdvanceSimulation(duration, OfflineCatchUp)
```

The Domain never reads wall-clock time directly.

Active Traveling is represented by the current Traveling `ActivityInstance` and its snapshotted route/timing parameters; it must round-trip through save/load exactly.

Counter-based RNG requires little or no mutable RNG state beyond the world seed and meaningful roll indices already contained in relevant runtime state.

---

# 39. Save Migrations and Version Compatibility

Save compatibility is explicit.

```text
Save V1
   ↓ migration
Save V2
   ↓ migration
Save V3
```

Every save carries at least:

```text
SchemaVersion
ContentVersion
SimulationRulesVersion
RandomAlgorithmVersion
```

Definition references persist stable authored IDs such as:

```text
trait.ambitious
```

not Unity object references.

A strategy for removed/renamed definitions must accompany any patch that changes persistent content IDs.

### 39.1 Rules-version mismatch is not automatically a load blocker

`SchemaVersion` determines whether the persisted shape can be understood/migrated.

`ContentVersion`, `SimulationRulesVersion`, and `RandomAlgorithmVersion` are also compatibility and diagnostics metadata, but a mismatch does **not** by itself mean the save must be rejected.

Default policy:

- migrate/validate the save into a shape the current build understands;
- surface version mismatches through diagnostics/compatibility tooling;
- if the current build declares the migrated state supported, resume from that quiescent saved state under the current rules;
- if exact historical replay is required, reproduce using the matching rules/random/content versions or build.

This prevents normal rule/balance patches from implicitly invalidating all old saves while remaining honest that deterministic reproduction is version-scoped.

---

# 40. Derived Indexes and Saves

Indexes such as:

```text
Location → occupants
Character → memberships
Decision dependency/context → active Decisions
unemployed character index

```

should generally be rebuildable from canonical save state.

On load:

```text
deserialize canonical state
        ↓
rebuild indexes
        ↓
validate invariants
        ↓
resume simulation

```

This reduces save complexity and gives corrupted/inconsistent caches no authority.

The scheduler and active Activity/Commitment state are different: they are authoritative state and must be persisted, not merely rebuilt. Traveling route/timing parameters live in the active Traveling Activity.

---

# 41. Content Architecture

Runtime simulation consumes immutable domain definitions.

Unity ScriptableObjects are authoring tools, not authoritative Domain types.

Pipeline:

```text
ScriptableObject Authoring Assets
             ↓
Validation / Conversion
             ↓
Immutable Domain Definition Catalog
             ↓
Simulation

```

This preserves designer-friendly Inspector workflows while keeping Domain independent of Unity.

---

# 42. Content Hot Reload

The content pipeline should allow safe definition changes to be reapplied during Play Mode where practical.

Example:

```text
Ambitious career weight
1.20 → 1.35

```

should ideally not require restarting the game.

Structural changes may require restart or migration.

Definitions should distinguish between:

```text
hot-reload-safe balance changes
structural changes
save-affecting changes

```

Content validation should detect duplicate IDs, missing references, invalid ranges, and dependency errors before entering gameplay.

### 42.1 In-flight entities snapshot definition-derived values

Hot-reloading a definition must never mutate the semantics of already-constructed runtime entities.

> **In-flight runtime entities snapshot all definition-derived data necessary to complete their authoritative behavior.**

If Decision #1837 was constructed with `Ambition d10 / Baking d8 / Pay d6`, changing the `job_offer` definition mid-session must not transform that already-open decision's dice underneath the player. Hot reload affects **future** decisions, not already-constructed ones.

This applies broadly: a Traveling Activity already underway retains its committed route/departure/arrival parameters even if `WalkingSpeed` changes later; an active Work/Production Activity retains whatever outcome-affecting parameters were committed when it began.

This does not require snapshotting whole definition objects — only the outcome-affecting runtime values. Diagnostic traces (§53) should include `ContentVersion`/`ContentRevision` alongside the world seed, so a reproduction doesn't silently depend on content that has since changed.

---

# 43. Unity Presentation Architecture

Unity-side objects represent simulation state.

They do not own it.

Example:

```text
CharacterView
├── CharacterId
├── Sprite / Mesh
├── Animator
├── World Indicator
└── Interaction Target

```

`CharacterView` asks:

```text
Where is this character?
What are they currently doing?
What presentation state should represent that?

```

If a character is not currently visible, no Character GameObject needs to exist.

Hundreds or thousands of simulated characters do **not** imply hundreds or thousands of permanently active Unity objects.

Use pooling/virtualization where appropriate.

---

# 44. Rendering

Use Unity's Universal Render Pipeline.

URP is designed by Unity as a cross-platform rendering pipeline spanning mobile through higher-end consoles and PCs, which suits an initially modest management sim while preserving room for richer presentation later.

The architecture does **not** currently freeze whether the visible game becomes:

```text
2D
2.5D
stylized 3D

```

URP supports keeping that art-direction decision open.

---

# 45. User Interface

Prefer UI Toolkit for interface-heavy game UI.

This game is expected to contain substantial interface surfaces:

```text
character profiles
decision encounters
knowledge
relationships
schedules
filters
notifications
management screens
history
tooltips
settings

```

Unity positions UI Toolkit as its web-inspired runtime UI system with separated structure, styling, and behavior and cross-platform adaptability.

Unity UI should consume read models rather than inspect mutable Domain state directly.

---

# 46. Input

Use Unity's current Input System as the Unity-facing input abstraction.

Input is translated into semantic presentation/application actions:

```text
tap CharacterView
      ↓
InspectCharacter(CharacterId)

```

The Domain never understands:

```text
mouse buttons
screen coordinates
touch gestures
controllers

```

---

# 47. Bootstrap / Composition Root

There should be one obvious place where runtime dependencies are constructed.

Conceptually:

```text
GameBootstrapper
      ↓
Definition Catalog
      ↓
World Seed / Random Oracle
      ↓
Domain Services
      ↓
Scheduler / Event Registry
      ↓
Application Services
      ↓
Infrastructure Adapters
      ↓
Presentation

```

Manual constructor dependency injection is preferred initially.

A dependency-injection framework should only be introduced after there is demonstrated composition pain.

---

# 48. Infrastructure Ports

The Application layer exposes interfaces for external concerns.

Examples:

```text
ISaveGameStore
IRealWorldClock
ILogSink
IContentSource
IPlatformStorage

```

Infrastructure implements them.

Examples:

```text
JsonSaveGameStore
UnityPersistentDataStorage
HeadlessFileStorage

```

Domain code never accesses file paths or platform APIs directly. `IRealWorldClock` is consumed by Application/Infrastructure to compute offline elapsed duration; wall-clock timestamps are never queried by Domain rules.

---

# 49. Performance Contract

Do not prematurely rewrite the simulation as DOTS/ECS.

Instead, establish measurable simulation goals.

The architecture must be capable of testing worlds containing approximately:

```text
100
500
1,000
5,000
10,000

```

characters.

Benchmark:

```text
simulation time per in-game day
scheduled events processed/sec
decision generation throughput
activity transitions / analytical completions processed/sec
memory usage
GC allocations
offline catch-up speed
save size
save/load duration

```

Exact acceptable budgets should be established after the first functioning simulation exists.

Optimization occurs based on measurements.

---

# 50. Performance Principles

Regardless of implementation, preserve these rules:

```text
No per-character Unity Update()
No global relationship pair scans
No global scan of all active Decisions for every world change when dependency indexing can target reevaluation
No per-minute Need tick
No per-frame authoritative Activity/travel progress updates (progress is analytically derived)
No rendering object required for offscreen simulation
No O(n) population scan for common indexed queries
No unnecessary allocations in hot simulation paths

```

If OOP entity storage eventually becomes a bottleneck, internal Domain storage may evolve toward packed arrays/component-style storage without altering Application or Unity-facing contracts.

---

# 51. Testing Strategy

The Domain should receive the majority of automated tests.

### Unit tests

Individual rules:

```text
Decision influence construction
active Decision influence reevaluation + stable influence targeting
Decision dependency indexing / targeted reevaluation
Knowledge discovery
shared WatchState feeding Observation + Attention
Dice resolution
Event validation
Hold overflow
Conflict-scope enforcement across concurrent decisions
Activity automatic resolution and interactive-result command validation
time-weighted Activity context changes during analytical progression
Commitment conflict/planning behavior
bounded interaction-candidate selection in large shared contexts

```

### Determinism tests

Run identical simulations twice and compare authoritative output.

### Scheduler tests

Cover:

```text
tie ordering
cancel
reschedule
stale revision (aspect-scoped)
multi-party dependencies
same-instant cascade settlement
explicit Domain Event handler ordering
Domain Event reaction settlement
runaway settlement detection
save/load
invalid event discard

```

### Persistence tests

Cover:

```text
round-trip save/load
migration
removed definitions
scheduler reconstruction
active Activity/Traveling state round-trip
offline-progression anchor round-trip
rules/random version metadata
index rebuilding

```

### Simulation invariant tests

Assert:

```text
character has exactly one active primary Activity
Activity SpatialContext has exactly one valid shape (`Located` or `Traveling`)
occupancy indexes agree with current Activity SpatialContext
no raw/in-progress mini-game UI state exists in authoritative Activity state
relationship endpoints exist
held count never exceeds policy (across all of a character's active decisions)
no scheduled event executes before DueAt
no event observed by presentation mid-cascade (only at quiescence)
runtime IDs never repeat or get reused after retirement
commands execute in deterministic `CommandSequence` order
active Decision influences only change at deterministic Domain-reaction/quiescent boundaries
Decision interventions remain attached to stable `DecisionInfluence` identity across influence-set changes

```

### Hot-reload tests

Cover:

```text
in-flight decisions/Activities are immune to definition changes made after construction
newly generated content picks up reloaded definitions

```

### Performance tests

Run synthetic large populations headlessly.

---

# 52. Headless Simulation Runner

Create a small console application using the same Core.

Example capabilities later:

```text
generate 10,000 characters
run one simulated year
collect decision counts
measure event throughput
inspect population statistics
reproduce Decision #1837

```

This becomes useful for:

- performance testing
- balance tuning
- debugging
- automated regression testing
- economy simulation

The existence of the runner also continually verifies that Unity has not leaked into the simulation core.

---

# 53. Diagnostics

Debug builds should support an authoritative simulation trace.

Example:

```text
Day 14 10:32
CommandSequence: 501
Command: ApplyIntervention
Decision: 1837
Target: Influence 3

Day 14 10:33
DecisionResolved
ContentVersion: 4.2.0
SimulationRulesVersion: 12
RandomAlgorithmVersion: 2
Random Scope:
  WorldSeed 827119
  Decision 1837
  Purpose rng.decision.influence_roll
  RollIndex 0
Result: 7 / d10

Day 14 12:15
CommandSequence: 509
Command: SubmitActivityPerformance
ActivityInstance: 2911
Outcome Source: PlayerProvided
Normalized Performance: Excellent
```

Useful trace elements include:

```text
command sequence + commands
scheduled events + EventSequence
Domain Event dispatch/handler order
stale-event discards
settlement cascades
decision construction
decision resolution
activity transitions / resolutions
player-provided Activity outcomes
knowledge discoveries
random coordinates
content version
simulation-rules version
random-algorithm version
build version / commit hash (development)
```

Including content/rules/random versions matters because deterministic reproduction is explicitly version-scoped. A trace without them cannot reliably distinguish input divergence from an intentional rule or algorithm change.

Tracing should be optional so release simulation performance is unaffected.

---

# 54. Initial Architectural Modules

The first useful Core skeleton should contain approximately:

```text
Domain
├── Common
│   ├── IDs
│   ├── Result
│   ├── Revision
│   └── IndexedMembership<TContainer, TMember>   (generic bookkeeping primitive)
│
├── Time
│   ├── SimTime
│   ├── SimDuration
│   └── AnalyticalProgression
│
├── Randomness
│   ├── DeterministicRandomOracle
│   └── StableRandomPurposeId
│
├── Simulation
│   ├── WorldState
│   ├── SimulationContext
│   ├── SimulationRunner
│   └── SettlementLoop
│
├── Scheduling
│   ├── ScheduledEvent
│   ├── EventDependency
│   ├── Scheduler
│   ├── ScheduledEventHandlerRegistry
│   └── SettlementCascadeGuard
│
├── Events
│   ├── DomainEvent
│   ├── DomainEventQueue
│   └── OrderedDomainEventHandlerRegistry
│
├── Characters
│   └── Character
│
├── Activities
│   ├── ActivityInstance
│   ├── ActivityDefinition
│   ├── ActivitySpatialContext
│   ├── TransitDetails
│   ├── Commitment
│   ├── RoutinePattern / CommitmentTemplate
│   ├── SchedulePlanner
│   ├── ActivityPerformanceResult
│   ├── ActivityContextModifier / progression update seam
│   └── IActivityResolutionStrategy
│
├── Relationships
│   └── Relationship
│
├── Spatial
│   ├── LocationHierarchy
│   ├── TravelNetwork
│   ├── TravelPlan
│   └── SpatialIndexes
│
├── Groups
│   └── MembershipIndex
│
├── Decisions
│   ├── Decision
│   ├── DecisionOption
│   ├── DecisionInfluence
│   ├── DecisionInfluenceId / InfluenceRevision
│   ├── DecisionDependencyIndex
│   ├── Die
│   ├── DecisionResolution
│   ├── DecisionHoldPolicy
│   └── DecisionConflictScope
│
├── Attention
│   ├── AttentionState
│   └── WatchState
│
├── Observation
│   └── Observation
│
└── Knowledge
    ├── FactKey
    ├── KnowledgeEntry
    ├── FactProvider
    └── KnowledgeLedger
```

Application should additionally establish:

```text
Commands
├── CommandEnvelope (CommandSequence)
├── ICommand / ICommandHandler
└── CommandDispatcher / deterministic ingress queue
```

Not every type needs a full implementation immediately.

The skeleton establishes vocabulary and dependency direction.

---

# 55. First Vertical Slice

The first prototype should test the architecture, not the setting.

Create approximately 8–12 abstract characters.

Implement enough world state to support:

- characters
- simple locations + travel connection
- one basic recurring commitment/routine
- current Activities including `Traveling`
- one or two relationships
- several traits
- player knowledge
- observation
- one decision type
- true influences
- one world change that modifies an already-open Decision's influence set
- partially hidden influences
- deterministic dice
- one intervention
- degrees of success
- held vs automatic decisions
- automatic Activity resolution
- one time-varying Activity context modifier (e.g. another character entering/leaving the same location)
- one mocked `SubmitActivityPerformanceCommand` path (no real mini-game UI required)
- scheduled Activity/Decision resolution
- save/load

Example:

```text
Mina receives a job offer.

World truth produces four reasons to accept
and three reasons to stay.

The player knows five of those reasons.

The Decision projection shows an appropriate
mix of known, generalized, and hidden influences.

The player may:
- ignore it,
- inspect it,
- hold it,
- spend one intervention.

The result is deterministic from its random scope.

The result changes WorldState.

The game is saved.

Reloading produces precisely the same resulting world.

```

---

# 56. Vertical Slice Acceptance Criteria

Before adding major management systems, the prototype must demonstrate:

**Headless execution**
The scenario runs without Unity.

**Determinism**
Same state + ordered commands + content/rules/random versions + seed produces the same outcome.

**Persistence**
Saving before resolution and loading again preserves the outcome.

**Scheduling / settlement**
Changing relevant world state can correctly invalidate a queued event; Domain Events and same-instant Scheduled Events settle deterministically to quiescence.

**Knowledge separation**
Truth can differ from player knowledge.

**Presentation separation**
Two players with different Knowledge could receive different views of the same Decision.

**Observation**
Player observation can create new Knowledge.

**Intervention**
Player influence changes the odds without directly choosing the result.

**Living Decisions**
A relevant world-state change can add/remove/change an influence on an already-open Decision; the projection updates at quiescence and any existing intervention remains bound to the intended stable influence identity.

**Offline behavior**
Automatic and held decisions behave according to explicit policy during catch-up.

**Activities / routines / travel**
A character has one authoritative current Activity; Commitments can plan future Activity transitions; Traveling is represented as an Activity and drives occupancy correctly. A context change during an Activity affects only the interval for which that context was active.

**Interaction scaling**
Two characters sharing a relevant travel/location/activity context can interact, while a synthetic large shared context proves candidate selection remains bounded rather than pairwise.

**Hierarchy**
Activity spatial contexts can reference locations in an arbitrary containment hierarchy.

**Interactive Activity seam**
The same Activity consequence pipeline can accept either automatic resolution or a normalized player-provided performance result without requiring interactive play.

**Command ingress**
Multiple external commands are processed in deterministic `CommandSequence` order at quiescent boundaries.

**Offline anchor**
Save/load can calculate catch-up duration from persisted wall-clock metadata without Domain reading the wall clock.

**Scale sanity**
The same systems can run a synthetic population substantially larger than the visible prototype.

Only after these work should we start layering on substantial economy, construction, pathfinding, or presentation content.

---

# 57. Explicitly Deferred Decisions

The architecture intentionally does **not** freeze:

```text
final setting
tower vs town vs spaceship vs another hierarchy
2D vs 2.5D vs 3D
exact population target
character stats outside the provisional social latent space
exact needs
exact dice-resolution formula
exact degree-of-success system
intervention resource economy
knowledge confidence rules
pathfinding solution
economy model
save serialization format
Addressables adoption
networking
mod support
DOTS/ECS
multi-leg TravelPlan routing complexity
whether travel connections can themselves be occupiable spaces (e.g. elevator capacity)
exact Activity/Commitment planning horizon
secondary/multitasking Activity representation
actual mini-game framework, scoring, UI, and resume/discard policy
whether FactKey subjects need their own durability treatment when the entities they reference are compacted into Legacy history

```

The architecture preserves reasonable paths to these features without paying their complexity cost now.

---

# 58. Architectural Invariants

These are the rules future code must preserve.

1. The authoritative simulation is pure C# and has no Unity dependency.
2. Commands are the only external mutation boundary.
3. External commands enter through a deterministic `CommandSequence` and execute one at a time at quiescent boundaries.
4. Truth, Player Knowledge, and Presentation are separate models.
5. Knowledge records observations of truth and may become stale.
6. Discoverability is systematic through fact providers/definitions.
7. Observation is a first-class gameplay input.
8. Observation and Attention consume one canonical WatchState/watch-signal source rather than independently tracking whether a character is being watched.
9. Unknown information does not imply universally hidden dice.
10. Decision influence visibility is independently controllable by existence, label, magnitude, and explanation.
11. Simulation time is independent of render time.
12. Continuous/duration-based processes use shared analytical progression rather than periodic ticking where practical.
13. Behaviorally meaningful thresholds/completions on analytical state schedule their next crossing.
14. Changing an analytical rate/parameter invalidates and recomputes its relevant threshold/completion schedules.
15. Future simulation uses a persistent discrete-event scheduler.
16. Scheduled events have identity, ordering, cancellation, dependencies, and execution-time validation.
17. Scheduled-event data contains no behavior.
18. Scheduler tie ordering uses deterministic `EventSequence`, distinct from external `CommandSequence`.
19. Domain Events are transient internal reactions with explicitly ordered handlers; handler order never depends on incidental subscription/load order.
20. Scheduled Events and Domain Event reactions settle together to quiescence before authoritative state is externally readable.
21. Same-instant settlement has deterministic ordering and a hard runaway-work guard.
22. An event cannot retroactively schedule same-time work into an earlier execution phase.
23. Read models/snapshots are only guaranteed consistent at quiescent simulation boundaries.
24. Runtime IDs are deterministically allocated, persisted, never reused, and remain historically referential after active retirement.
25. Authoritative randomness is deterministic and counter-based.
26. Random results are keyed to semantic scope rather than incidental consumption order.
27. RNG scope/purpose identifiers are stable authored IDs, never runtime-dependent hashes or refactor-sensitive method/display names.
28. Authoritative order-sensitive collection processing is explicitly ordered.
29. Deterministic reproduction is scoped by content, simulation-rules, and random-algorithm versions in addition to state/commands/seed.
30. Authoritative branching math avoids floating-point dependence where practical.
31. Live, player fast-forward, and offline catch-up are explicit simulation modes.
32. Offline elapsed duration is computed outside Domain from a persisted wall-clock anchor; Domain never reads wall-clock time directly.
33. Held decisions are bounded and have deterministic overflow behavior.
34. Unresolved decisions do not freeze unrelated world simulation.
35. Multiple active Decisions per character are permitted, with definition-driven conflict scopes preventing logically incompatible concurrent choices.
36. Active Decisions are living runtime state: definition-derived semantics are snapshotted, while relevant world-derived influences may be deterministically reevaluated until resolution.
37. Decision influences have stable runtime identity; interventions bind to influence identity rather than collection position.
38. Dynamic Decision reevaluation is targeted through explicit dependency/context registration or equivalent indexing rather than global polling of all open Decisions.
39. Every active character has exactly one authoritative primary `ActivityInstance` at a time.
40. A character's spatial presence is derived from the current Activity's `SpatialContext`; there is no parallel mutable presence field.
41. Traveling is an Activity, not a peer Transit subsystem.
42. Travel/Activity progress is authoritative through deterministic analytical parameters; continuous visual progress is derived, never authoritatively ticked per frame.
43. Commitments represent planning intent and remain distinct from concrete Scheduled Events.
44. Recurring routines/commitments materialize reactively within a bounded required planning horizon rather than creating an unbounded future calendar.
45. Every outcome-producing Activity has an autonomous resolution path; optional interactive resolution can never be required for simulation progress.
46. Interactive Activity results enter through validated Commands as normalized performance input and feed the same consequence pipeline as automatic resolution.
47. Mini-game/UI session state is Presentation state; only an accepted normalized Activity result becomes authoritative.
48. Time-varying contextual effects on an extended Activity are accumulated only over the interval for which they apply; completion-time snapshots alone are insufficient.
49. Space is a generic hierarchical containment model.
50. Spatial containment and travel topology are separate models.
51. Direct occupancy is derived/indexed from Activity SpatialContext and excludes Traveling Activities unless their travel context is explicitly occupiable.
52. Non-spatial group membership is separate from spatial containment.
53. Common occupancy and membership queries are indexed; generic bidirectional membership bookkeeping may be reused beneath distinct domain semantics.
54. Social interactions arise through shared context and never global pair scanning.
55. Shared interaction contexts produce bounded candidate selections rather than exhaustive pair enumeration within large contexts.
56. UI reads projections/read models rather than mutable Domain entities.
57. Command eligibility and UI availability use the same authoritative validation rules.
58. Save data uses explicit versioned DTOs rather than serialized runtime objects.
59. Scheduler state, active Activities/Commitments, and offline-progression metadata required for continuation are save state.
60. Reconstructible indexes are rebuilt/validated after load.
61. Persistent content references use stable authored IDs.
62. Rules/content/random version mismatches are explicit compatibility metadata, not automatic load blockers; support/migration policy decides loadability.
63. Resolved transient simulation data has explicit retention/pruning policies.
64. Long-lived historical significance may compact into smaller Legacy representations; durable does not mean unbounded raw history.
65. Knowledge discovery provenance survives pruning; references to historical runtime entities are optional/weak.
66. Exactly one simulation owner performs authoritative mutation.
67. Parallel computation, if later introduced, returns results for deterministic application.
68. Visible Unity objects are representations, not authoritative entities.
69. Offscreen simulated characters require no Unity GameObject.
70. Performance is measured through headless benchmarks before architectural optimization.
71. Domain boundaries are mechanically enforced through project/assembly dependencies.
72. Core game definitions can be rebuilt/reloaded independently from runtime state where safe.
73. In-flight runtime entities snapshot definition-derived values required for future authoritative outcomes; hot reload affects newly created runtime state only.
74. Ground-truth personality is a compact latent vector; named social traits are projections and never duplicate authoritative scoring state.
75. Character social evaluation uses observer-scoped Knowledge/belief, never omniscient target truth.
76. Unknown personality and neutral personality are distinct because belief uncertainty is authoritative state.
77. Social beliefs and dyadic state are sparse observer→target edges; universal population pair tables are prohibited.
78. Appraisal fields are directional, sparse, fixed-point, deterministically ordered, and evaluated through one canonical pipeline.
79. Distinct appraisal lenses share calibration but may disagree; no universal compatibility scalar replaces them.
80. Values, interests, dyadic history, familiarity, current affect, and independent context remain distinct from latent personality appraisal.
81. Durable relationship channels and familiarity are directional even when the pair retains one stable relationship identity/index entry.
82. Social evidence updates an observer's joint belief distribution and does not directly rewrite personality truth.
83. Social witnesses come from bounded shared-context/index queries and never from global pair scans.
84. Social evaluation retains matrix-level and authored human-readable provenance sufficient to explain gameplay pressure.
85. Reputation and perceived group norms are bounded Knowledge facts, not omniscient truth or recursively unbounded theory of mind.
86. Social evaluation supplies calibrated pressures to Decisions, Activities, and interaction policy; it never directly chooses an outcome.
87. Social belief, appraisal-field, directional-history, values/interests, affect, and context revisions invalidate only dependent cached/active evaluations.
88. Persistent social truth, beliefs, fields, uncertainty, directional history, familiarity, affect, and relevant provenance round-trip through versioned save DTOs.
89. Social AppraisalFields and Decision Considerations remain distinct Domain concepts over one shared deterministic fixed-point SignalField evaluator.
90. SignalField evaluation preserves residual uncertainty through latent and bounded-output variance rather than collapsing uncertain inputs to point estimates.
91. Decision reasons are option-relative and carry explicit polarity; supporting Influence rolls add to their Option and opposing Influence rolls subtract from it through a replaceable Decision-resolution policy.
92. Player interventions change an Influence's die magnitude or roll state without changing its semantic polarity.
93. Resolved authoritative outcomes retain sufficient evaluation-time evidence to explain the reasons that existed when they resolved; later World-state drift must not rewrite that explanation, and retained evidence follows the outcome's retention/compaction lifecycle.
94. A Signal is explicitly known, uncertain, unknown, or not applicable; unknown and inapplicable inputs never enter numeric evaluation as neutral zeroes.
95. An in-flight Decision owns a deep snapshot of its compiled typed reasoning program and semantic context; save/load preserves that authority, while Candidate Reasons and routing indexes remain rebuildable projections.
96. Reevaluation reconciles a compiled reason by stable binding, option, and ReasonChannel identity; disappearance retracts rather than deletes it, reappearance reuses its Influence id, and authoritative interventions are replayed over the refreshed die magnitude without changing polarity.
97. Compiled Decision dependency routing is derived at `(DecisionId, BindingId, OptionId)` granularity, is never persisted, and is deterministically rebuilt from the snapshotted program and current Signal providers after load.
98. Each retained resolution roll freezes its semantic reason, signed evaluation result, uncertainty, Signal inputs, source revisions, and compact contribution evidence; explanation projections read only that snapshot, and it is pruned with the linked Decision history record.
99. Unity-authored Decision reasoning is converted into the same typed compiled program used headlessly and is linted before play for provider capability, parameter and Option compatibility, requested Signals, ReasonChannel/scale validity, and legacy-path conflicts; catalog construction repeats authoritative validation.
100. Commitment-conflict Decision Options carry canonical Preserve/Defer/Relinquish plans; individual Commitment IDs are not themselves the choice.
101. Commitment conflict is determined by genuine joint feasibility of the candidate set; pairwise overlap or pairwise compatibility is not sufficient.
102. Feasibility determines valid plans before Considerations rank them; impossibility never becomes an opposing Influence.
103. An open commitment-conflict Decision has a derived, revision-dependent hard deadline that Hold and offline catch-up cannot cross.
104. Resolving a commitment-conflict Decision mutates Commitment intent only; ordinary planner reactions determine subsequent Activity and Travel.
105. Relinquishing a Commitment under conflict is distinct authoritative history from generic cancellation.
106. If an open Decision's candidate set stops describing a real choice, it becomes Dissolved without executing resolution consequences; held capacity releases and spent interventions are unconditionally refundable.
107. Active conflict and dependency routing indexes are reconstructible projections; plan payloads, conflict episode identity, deadline state, interventions, and historical resolution evidence are authoritative save state.
108. Non-stacking ReasonChannel consolidation may merge repeated readings of the same bound subject, but never merges distinct targets or Commitments within one Option.

---

# 59. Architectural North Star

When deciding where new code belongs, ask:

> **Would this still need to exist if the entire game were represented as text in a console window?**

If yes, it probably belongs in Domain or Application.

If no—if it exists because the player needs to see, hear, click, drag, animate, or render something—it probably belongs in Unity Presentation.

And whenever a new feature is proposed, ask:

> **Does this create new truth, change existing truth, reveal truth, or merely present truth?**

Those four categories should resolve most architectural ambiguity.

The core relationship remains:

```text
WORLD
creates circumstances

ROUTINES / COMMITMENTS
create intended opportunities and obligations

ACTIVITIES
are what characters are actually doing

CHARACTERS
make choices

DICE
express uncertainty

PLAYER
observes and influences

KNOWLEDGE
changes what the player understands

CONSEQUENCES
change the world

```

The architecture exists to keep that loop deterministic, scalable, understandable, testable, and expandable.
