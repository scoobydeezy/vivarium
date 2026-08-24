# Vivarium Roadmap — Detailed Phase Reference

**Status:** Detailed phase requirements and acceptance reference  
**Last reconciled:** 2026-08-24 (MPS review + relationship-memory attrition roadmap incorporated)  
**Scope:** From the current proven Golden Scenario to a minimum viable playable simulation with approximately 8–12 characters (target: about 10) living complete routines, with all meaningful character Decisions and all supported player choices surfaced through playable presentation.

> Current priority and next-task authority live in [`Roadmap.md`](Roadmap.md). This detailed reference
> preserves the full phase rationale, inventories, completion tests, and post-MVP memory direction
> without forcing agents to load them for routine task selection.

## 1. Role of This Document

This document records the detailed rationale and completion requirements behind the ordered phases.
Use [`Roadmap.md`](Roadmap.md) to determine what should be built next. This reference does not replace
the frozen architecture or implementation rules.

Authority is split by purpose:

1. [`../../README.md`](../../README.md) and [`../Architecture/Reference.md`](../Architecture/Reference.md) — architectural truth and invariants. They always win on architecture.
2. [`../Architecture.md`](../Architecture.md) and [`../ImplementationStatus.md`](../ImplementationStatus.md) — repository realization and current implementation checkpoint. Code/tests remain the final evidence that a status claim is true.
3. Focused design briefs — locked system-specific product decisions.
4. [`Roadmap.md`](Roadmap.md) — authoritative product-development sequence and current next-task priority.
   This document supplies detailed phase requirements.
5. [`../IMPLEMENTATION_GUIDELINES.md`](../IMPLEMENTATION_GUIDELINES.md) — authoritative delivery discipline for *how* the selected roadmap slice is executed.

The former combined implementation checkpoint is archived under `Docs/History`.

If these phase requirements conflict with the architectural contract, the architecture wins. If
repository status changes, reconcile the active roadmap and this reference rather than preserving stale
checkmarks.

---

## 2. MVP Product Goal

The next major milestone is a **Minimum Playable Vivarium** centered on approximately 8–12 characters.

It must prove two things simultaneously:

### Goal A — Complete small-world simulation

Approximately 8–12 characters (target: about 10) can live repeating, understandable routines that collectively exercise:

- sleeping / recovery;
- eating / hunger management;
- work or another structured obligation;
- travel between meaningful locations;
- discretionary time;
- hobbies / recreation;
- social interaction;
- commitments and conflicts;
- needs that can alter behavior;
- meaningful Decisions;
- consequences that change later behavior and social state.

A “full routine” does **not** mean every character performs every activity category every day. It means the simulation can account for the complete day without relying on scenario-scripted Scheduled Events as a substitute for routine logic.

### Goal B — Playable agency and observability

Every meaningful character choice represented as a `Decision` must be inspectable through the normal projection path and playable under the same Hold / Knowledge / intervention rules.

Every player action included in the MVP must have:

- a visible Unity affordance;
- a corresponding validated Application command;
- authoritative Domain consequences where applicable;
- deterministic/save-load coverage when it changes authoritative state.

The player still does **not** directly choose character outcomes.

### Engineering constraint — no disposable prototype systems

Small scope is encouraged. Fake architecture is not.

The MVP may use ugly content, a tiny world, limited definitions, and narrow rules. It must not use a temporary subsystem that is expected to be replaced once the “real” version is built.

A valid v0 feature is therefore:

> **the smallest production-shaped version of a system that the scenario genuinely exercises.**

---

## 3. Current State Inventory

### 3.1 Implemented and trusted

These are not roadmap foundations to rebuild. Extend them only when a concrete slice exposes a missing capability.

- Deterministic simulation kernel, typed IDs, scoped randomness, ordered commands, scheduler, Domain Events, same-instant settlement, and quiescent projections.
- Analytical progression and behaviorally meaningful scheduled thresholds.
- Primary Activities, Traveling as an Activity, occupancy indexes, travel contexts, and interval-accurate Activity modifiers.
- Commitments and bounded routine planning primitives.
- Needs infrastructure with a real threshold-driven Decision path.
- Living Decisions, stable Influence identity, Hold/Attention, interventions, deterministic signed resolution, historical explanation snapshots, and targeted reevaluation.
- Compiled Decision reasoning: typed bindings, Signals, SignalFields, Considerations, ReasonChannels, calibration, uncertainty, and authoring validation.
- Knowledge/Observation with player- and character-scoped beliefs and canonical WatchState.
- Bounded shared-context interactions.
- Production matrix-first social model, directional beliefs/appraisals/history/familiarity, evidence, values/interests, affect, reputation, and social consequences.
- Commitment-conflict Decisions with true joint feasibility, plan-valued Options, hard deadlines, Dissolution, Preserve/Relinquish consequences, and save/load.
- Commitment lifecycle outcomes/accountability with stakeholder attribution, evidence, memory/history, and downstream Reliance effects.
- Versioned persistence mapping through schema v6, derived-index rebuilding, migration coverage, and offline-equivalence tests.
- Headless Golden Scenario, deterministic replay, save/load equivalence, and 250/1,000-character scale regression gates.
- Unity authored content path and a narrow playable Decision presentation loop using the same Domain/Application path as headless execution.

**Conclusion:** Vivarium has already proven its architecture. The next work should increase **life completeness and playability**, not add more generic infrastructure.

### 3.2 Defined but incomplete

The architecture or focused briefs already establish the shape, but product breadth is missing.

#### Routine / behavior breadth

- Need thresholds can generate one concrete Decision, but broader Need-to-behavior reactions are not implemented.
- Commitments and planning exist, but the current scenario does not constitute a complete repeating daily routine for a cast.
- Activity consequence paths exist, but the set of real Activities is narrow.
- Decision generation exists for Need pressure, social interaction, and commitment infeasibility; other meaningful circumstances remain absent.

#### Commitment breadth

- Preserve/Relinquish is implemented.
- Actual `Defer` behavior is not implemented.
- Broader n-way conflict clustering/tuning is deferred.
- Recurring-commitment semantics for Preserve/Defer/Relinquish are not locked.
- Changing-road revalidation and dissolve/regenerate thrash policy are deferred.

#### Social consequence breadth

- Character stakeholders work.
- Employment/institutional stakeholders are not implemented.
- Later attribution correction is structurally protected but not implemented.
- Global reputation propagation is intentionally absent.

#### Player interaction breadth

- Watch/Follow semantics, Hold/Release, Knowledge filtering, intervention commands, and an Activity-performance command seam exist.
- The intervention **resource economy** is not defined or implemented.
- No real interactive Activity/mini-game is implemented.
- The current Unity surface is a narrow smoke/playability UI, not the complete small-cast game interface.

#### Presentation breadth

- Content authoring and a playable Decision encounter are proven.
- General roster, character, schedule, location/travel, relationship, Knowledge, and event/history surfaces remain utilitarian or absent.
- UI Toolkit is architecturally preferred but not yet the general application UI layer.
- Art direction remains intentionally open.

#### Persistence breadth

- Save DTOs/mapping and in-memory save behavior are proven.
- A concrete save serialization/storage format remains unselected.

### 3.3 Conceptualized but not yet defined enough to implement cleanly

These areas exist in the architecture or product vocabulary, but a focused product decision is required before implementation.

- **The exact MVP Need set and semantics.** `README.md` intentionally defers exact Needs.
- **The complete routine model for the 10-character scenario.** We have primitives, not a locked daily-life content contract.
- **Employment v0.** `Employment` exists architecturally and Work Commitments exist in scenarios, but the production source of jobs/shifts/attendance/accountability has not been specified. The MPS now additionally requires Employment v0 to establish queryable workplace authority/hierarchy and to materialize more than one authored obligation pattern (for example, a regular shift and a distinct closing duty) through the same production mechanism.
- **Hobbies / discretionary activity v0.** Interests exist socially and Activities exist mechanically, but the durable relationship between a character, concrete recreational Activity affordances, recurring participation, and planner choice is not locked. The MPS should begin with a tiny concrete vocabulary rather than an abstract “hobby” placeholder.
- **Free-time arbitration.** It is not yet decided which low-stakes routine selections are planner behavior and which are meaningful Decisions worth surfacing.
- **Player intervention economy.** Refund rules exist, but currency/resource acquisition, cost, regeneration, caps, and spending strategy do not.
- **First real player world-management action.** The architecture allows the player to alter circumstances, but no minimum production management lever has been selected. The MPS reserves a location-availability/open-state seam so the Player Agency Brief can select a real lever without retrofitting the canvas.
- **MVP UX structure.** The needed information exists in pieces, but navigation, prioritization, and how the player follows approximately ten lives are not defined.
- **Concrete save format.** The port exists; the format/product requirements do not.
- **Interactive Activity product design.** The command/result seam exists; actual activity, scoring, and resume/fallback behavior do not.
- **Relationship-memory consolidation / attrition.** Salient `RelationshipMemory` already exists and the Social Model already anticipates lifecycle-managed `Active → Recent → Significant → Legacy / compacted` history, but the behavioral rule that decides which memories remain individually important versus which become fuzzy/background history is not yet implemented. The current product direction is:
  - individual memories carry a long-term **importance/reinforcement** measure separate from their current contextual relevance/accessibility;
  - memories become more important when they are retrieved into authoritative reasoning and materially contribute, with stronger contributions reinforcing them more; the exact reinforcement trigger (for example, only contribution to a winning/successful roll versus any materially expressed contribution) must be locked before implementation rather than guessed in code;
  - after sufficient age and/or memory-pressure threshold, low-importance memories consolidate into less-specific semantic/background relationship state instead of simply disappearing;
  - repeatedly important memories remain individually identifiable and individually weighted;
  - consolidation preserves the learned relationship effect while preventing the original detailed memory and its background summary from double-counting the same event.
  This should extend the existing Relationship/History retention model, not create a parallel generic “memory decay” subsystem.

### 3.4 Not yet meaningfully ideated in the source docs

These are genuine later product questions, not blockers for the 10-character MVP unless the MVP brief explicitly pulls one forward.

- Long-term progression / campaign structure.
- Win, loss, or scenario-completion conditions.
- Broad economy and resource production.
- Construction/building systems.
- Population growth, families/generations, or immigration.
- Deep job/career progression.
- Large-scale organizational/institutional simulation.
- Final pathfinding model and complex transportation.
- Final 2D/2.5D/3D art direction.
- Networking, mod support, DOTS/ECS, Addressables strategy.

These should remain deferred rather than being scaffolded “just in case.”

---

## 4. Critical Product Decisions Before More Breadth

The current roadmap reached a deliberate product gate. The next work should begin with two small design briefs rather than speculative code.

### Gate 1 — Minimum Playable Scenario Brief

Lock the actual 8–12-character test world (target: about 10; coverage is authoritative, not the exact headcount).

The brief must define:

- setting/container only as far as the test requires;
- approximately 10 named test characters, with explicit permission to tune within 8–12 when coverage requires splitting or merging roles;
- home/work/social/recreation locations and a small real TravelNetwork;
- authoritative Activity affordances per location (including where Eating is and is not available);
- which characters live/work/socialize together;
- the MVP Need set;
- the Activity set required for a complete day;
- recurring routine/Commitment sources;
- a smallest-real Employment/structured-obligation contract that produces workplace authority plus at least two obligation patterns when the scenario requires them (for example, regular shift and closing duty);
- at least one concrete hobby/discretionary pattern;
- at least one location availability/open-state reserved for the Player Agency brief;
- expected social opportunities;
- which circumstances are meaningful Decisions rather than automatic planner choices;
- a two-to-three simulated-day acceptance script;
- which existing Golden Scenario beats are retained as regression content, and which scenario-only injections must disappear once production sources replace them;
- at least one commitment conflict whose infeasibility is materially caused by travel/duration rather than only literal clock overlap;
- explicit control-character conditions that make an uneventful day likely without suppressing normal Decision generation.

**Recommended minimum Activity vocabulary:**

`Sleeping`, `Eating`, `Working`, `Traveling`, `Socializing`, `Recreation/Hobby`, `Waiting/Idle`.

This is a recommendation, not yet an architectural lock.

**Recommended minimum Need vocabulary:**

`Hunger`, `Energy`, `Social`, `Recreation`.

Again: product proposal, not current source truth. Add a fifth Need only if the scenario needs it to create a qualitatively different behavior.

### Gate 2 — Player Agency Brief

Lock what “playable” means for the MVP.

At minimum, decide whether the MVP player can:

- inspect/select a character;
- Follow/Watch/Quiet them;
- inspect current Activity, location, Needs, known relationships, schedule, history, and Knowledge;
- inspect any surfaced Decision;
- Hold/Release a Decision;
- spend an intervention resource on a Decision;
- optionally submit an interactive Activity result;
- alter at least one world circumstance through a genuine management command.

**Recommendation:** choose one small, production-shaped environmental lever rather than building an economy or construction system. The MPS reserves an authoritative location-availability/open-state seam (the Commons is the current leading candidate) because it can change schedules, travel, discretionary Activities, social opportunity, Needs, and Decisions without directly commanding a character.

The brief must also define the intervention resource economy if interventions are part of normal play rather than debug/demo behavior.

---

## 5. Ordered Development Roadmap

The rule for this roadmap is:

> **Build the earliest incomplete causal link required by the Minimum Playable Scenario whose prerequisites already exist.**

Do not start a later phase merely because its classes are easier to imagine.

### Phase 0 — Lock the Minimum Playable Scenario

**Status:** IN PROGRESS

[`MinimumPlayableScenario.md`](MinimumPlayableScenario.md) has a reviewed first draft. The remaining
Phase 0 deliverable is the Player Agency Brief, followed by a joint reconciliation pass so both briefs
define one playable world.

**Done when:** we can describe one complete simulated day for every test character, enumerate every meaningful character Decision that may arise, and enumerate every player action that must be exposed in Unity.

No Core implementation should be selected until this gate is sufficiently concrete to identify the earliest missing causal link.

---

### Phase 1 — Close the Daily Routine Loop Headlessly

**Goal:** a character can live indefinitely rather than only progress through authored scenario beats.

Implement as narrow vertical slices using existing production primitives.

#### 1A. Energy → Sleep → Wake

Add/author Energy as an analytical Need and Sleeping as a production Activity. A character becomes tired, plans/goes to an appropriate sleeping location, sleeps, recovers, wakes, and resumes planning.

**Completion test:** save/reload before sleep, during sleep, and after waking produces the same next-day world.

#### 1B. Real structured obligation source

Define and implement the smallest production-shaped Employment/structured-obligation model required by the scenario rather than continuing to inject work shifts or closing duties as scenario-only events.

Required v0 responsibilities:

- stable Employment identity;
- employer/role/work-location references;
- queryable workplace hierarchy/authority (for example, Darius is Mina's supervisor) exposed through the ordinary semantic Signal/fact path;
- authored obligation patterns that materialize concrete Commitments within the planning horizon;
- at minimum, the MPS must be able to distinguish a normal shift from a separate closing-duty obligation without inventing a one-off Commitment subsystem;
- stakeholder roles derived from the real obligation/Employment relationship for accountability;
- attendance/fulfillment flowing through existing `CommitmentLifecycleService`.

Wages/economy are **not** required unless the scenario brief makes them behaviorally necessary.

#### 1C. Eating as ordinary routine behavior

Generalize the existing Hunger path so characters can routinely obtain an Eating Activity rather than only demonstrate one leave-work Decision.

The implementation must respect explicit location/Activity affordances rather than assuming an unstated meal break or workplace interruption rule. For the first MPS, an ordinary control character may simply eat before and after an uninterrupted Work commitment; the scenario should not invent staffing-coverage or meal-break infrastructure merely to make a worked example convenient.

The slice must preserve the distinction between:

- low-stakes planner execution when Eating is genuinely available; and
- a meaningful choice when Hunger conflicts with another obligation or desirable Activity.

#### 1D. Discretionary time / hobby

Implement the smallest production-shaped relationship between Interests, available Activities/locations, and discretionary planning.

Avoid inventing a heavyweight Hobby runtime entity unless durable hobby-specific state actually requires one.

The first slice should prove:

`free time + available recreational Activity + character Interest → chosen Activity → possible shared context → later social/Need consequences`.

Begin with a tiny concrete vocabulary (the reviewed MPS currently proposes a social Tabletop Games Activity at the Commons and a solitary Reading Activity available at Home or Commons) rather than an abstract hobby flag.

#### 1E. Social discretionary behavior

Give characters a non-scripted route into Socializing when their state/context warrants it. Candidate selection must reuse bounded spatial/group/context indexes.

**Phase 1 done when:** the test cast can run for multiple days with no scenario script hand-scheduling their entire lives, and every moment has an authoritative Activity/location explanation.

---

### Phase 2 — Expand Meaningful Character Choice Breadth

**Goal:** the routine creates choices worth watching, not merely deterministic clockwork.

Add Decision generation only where the Minimum Playable Scenario identifies a genuine branch.

Priority candidates:

1. Need vs obligation.
2. Two desirable discretionary activities.
3. Social invitation/help vs existing plan.
4. Rest vs recreation/social continuation.
5. Commitment conflict beyond the existing authored case when naturally produced by the routine.

Use the implemented compiled Consideration pipeline. Do not add content-specific Influence factories.

Each new Decision must prove:

- circumstance-driven generation;
- multi-option reasoning if appropriate;
- actor-Knowledge-sensitive Signals where relevant;
- targeted live reevaluation;
- player-facing Knowledge filtering;
- deterministic resolution;
- consequences through authoritative services;
- historical explanation;
- save/load equivalence.

**Important rule:** not every planner selection is a Decision. Surface branch points only when uncertainty, values, social pressure, or competing goals create meaningful gameplay.

---

### Phase 3 — Complete Player Agency as a Real Game System

**Goal:** player influence is not a debug command set.

#### 3A. Intervention economy

Implement the product decision from the Player Agency Brief:

- authoritative resource state;
- gain/regeneration rules;
- cost validation;
- spend/refund lifecycle;
- Hold/Dissolution integration;
- save/load and offline behavior;
- projections explaining availability and cost.

The existing unconditional refund rule for Dissolved Decisions remains authoritative.

#### 3B. First environmental management action

Implement one real “alter circumstances” command selected by the brief.

It must cause ordinary simulation fallout rather than directly targeting a character outcome.

Example shape if business/location hours are selected:

`Player changes availability → schedule/location revision → planner/feasibility reevaluation → Activity/Commitment/Decision fallout → visible history/notification`.

#### 3C. Optional real interactive Activity

Only if the MVP includes one. Reuse the existing `SubmitActivityPerformanceCommand` seam and normal Activity consequence pipeline. Do not create a parallel mini-game simulation state in Domain.

**Phase 3 done when:** every player action counted as part of the MVP is a real command with real consequences and can be fully exercised headlessly before Unity presentation.

---

### Phase 4 — Build the Small-Cast Acceptance World

**Goal:** merge routine, social behavior, Decisions, commitments, accountability, and player agency into one durable scenario rather than separate feature demos.

Author approximately 8–12 characters (target: about 10) with deliberately intersecting lives:

- overlapping workplaces/obligations;
- different Need rates/routines;
- different Interests/hobbies;
- asymmetric social beliefs/history;
- a small set of shared locations/routes;
- enough routine overlap to generate interactions without forcing them;
- enough conflict to generate Decisions without scripting their outcomes.

The scenario should run at least **2–3 simulated days** in acceptance coverage.

### Required acceptance assertions

Across the scenario:

- every character always has exactly one primary Activity;
- every character sleeps and wakes through real Need/routine mechanics;
- work/structured obligations originate from their production source, including workplace authority and distinct authored obligation patterns where required;
- eating occurs through routine behavior only where the current location/Activity affords it;
- discretionary/hobby behavior occurs;
- social interactions occur through bounded context selection;
- at least three distinct Decision generators are exercised;
- at least one open Decision reevaluates because the world changes;
- at least one Commitment outcome changes later social reasoning;
- routine fulfilled Commitments contribute reliability evidence without accumulating direct Trust deltas or RelationshipMemory;
- stakeholder social consequences use KnownAttribution rather than leaking authoritative cause;
- at least one commitment conflict is caused materially by travel/duration even though the clock windows are not simply overlapping;
- at least one player environmental action changes later circumstances;
- Hold/intervention/resource spending works;
- save/reload at several checkpoints is equivalent;
- offline catch-up across active routine/Decisions is equivalent;
- repeated runs produce the same authoritative hash.

This becomes the new central Golden Scenario. Older narrow tests remain focused regressions.

---

### Phase 5 — Playable Unity Product Surface

**Goal:** make the headless small-cast world understandable and controllable without debug tooling.

Build the UI against projections/commands only.

Recommended surface order:

1. **Time controls / simulation status** — pause, speeds, current SimTime, offline-return state.
2. **Roster** — all test characters, current Activity/location, Attention state, urgent Decision indicator.
3. **Character view** — current Activity, location/travel, Needs, upcoming Commitments, known relationships, recent history.
4. **Decision center** — Options, visible reasons/dice, deadline, Hold/Release, intervention cost/action, historical result.
5. **Schedule/timeline** — routine and concrete Commitments with conflicts/deadlines.
6. **Knowledge/relationship view** — what the player knows rather than omniscient truth.
7. **World/location view** — enough spatial context to understand travel and the selected management action.
8. **History/notification feed** — causal events that explain why the world changed.
9. **Interactive Activity surface** — only if Phase 3C is in MVP scope.

Use UI Toolkit as the default unless a concrete Unity constraint justifies another route.

### Presentation completeness invariant

For the MVP:

> Every active Domain Decision is projectable; every player-facing MVP command has a UI path; no Unity control performs authoritative mutation directly.

---

### Phase 6 — Real Save/Continue Productization

**Goal:** persistence becomes player-facing rather than test-only.

Select the concrete format based on product/platform needs, then implement the existing persistence port.

Minimum behavior:

- create save;
- load save;
- continue after application restart;
- offline elapsed-time calculation;
- migration/version diagnostics;
- safe handling of incompatible/corrupt data;
- UI feedback for catch-up and restored state.

Do not redesign authoritative save DTOs merely to fit the serializer.

---

### Phase 7 — MVP Hardening Gate

Before adding economy, construction, procedural population breadth, advanced pathfinding, or content-scale polish:

- run the small-cast scenario repeatedly through several simulated days;
- test unattended/offline progression;
- test player intervention-heavy and intervention-free branches;
- inspect Decision/explanation quality;
- inspect whether routines feel legible rather than mechanical noise;
- verify there are no hidden scenario scripts masquerading as simulation;
- keep the existing 250/1,000-character scale gates green;
- add measured budgets only where the richer routine creates a demonstrated new bottleneck.

The gate question is:

> **Can the player follow these 10 people, understand what they are doing and why, notice meaningful choices, intervene through the intended rules, and see those choices change later life?**

If no, do not add management breadth yet.

**Memory-timescale note:** do not artificially accelerate relationship-memory attrition merely to make it visible inside the 2–3-day MPS. The MPS proves memories are created and later reasoning can use them; long-horizon retention/consolidation receives its own post-MVP slice below.

---

### Phase 8 — Relationship Memory Consolidation and Attrition

**Goal:** make long-running social history both behaviorally meaningful and bounded: characters retain defining memories as specific events while ordinary, low-importance memories gradually become fuzzy background history rather than accumulating forever or vanishing without consequence.

This is the first planned **post-MVP social-longevity slice**. It is not required to lock or complete the 48-hour MPS. Before implementation, draft a focused `MemoryAttritionBrief.md` that locks the unresolved scoring/threshold details.

#### 8A. One memory lifecycle, not a second system

Extend the existing `RelationshipMemory` / dyadic-history retention path. Do not create a separate generic memory store solely for attrition.

Conceptually:

```text
Individuated RelationshipMemory
        ↓
retrieved into relevant reasoning / behavior
        ↓
importance may be reinforced
        ↓
age / memory pressure reaches consolidation point
        ↓
    ┌───────────────────────┬────────────────────────┐
    │ important / defining  │ ordinary / low-value   │
    ↓                       ↓
remain individually         consolidate into fuzzy
weighted/addressable        semantic background state
```

Existing Evidence, beliefs, familiarity, and dyadic channels remain distinct. Routine fulfilled commitments already route through Evidence rather than spawning memories; attrition therefore must not become a second way of accumulating or decaying ordinary Dependability evidence.

#### 8B. Importance is not current relevance

A memory's **importance/reinforcement** answers:

> How strongly has this event proven itself to matter to this character over time?

Its **contextual relevance/accessibility** answers:

> Does this memory matter to the choice being considered right now?

These must remain separate. A defining betrayal can remain highly important for years without appearing in every Decision; a minor recent embarrassment can be highly relevant to one immediate choice without becoming a lifelong defining memory.

#### 8C. Reinforcement uses actual gameplay significance

The current product direction is that memories strengthen by being **used meaningfully**, not merely by existing. A memory that repeatedly contributes to Decision reasoning or another authoritative behavioral evaluation should become harder to consolidate away, and stronger contributions should reinforce it more.

The focused brief must lock the exact rule before code. In particular, resolve whether reinforcement requires the memory-backed Influence to contribute to the ultimately winning/successful Option/roll, or whether any materially expressed contribution reinforces the memory even when the final Option loses. Do not silently choose one during implementation.

Where Decision rolls drive reinforcement, use the existing frozen historical evaluation/roll evidence rather than recomputing past significance from current World state.

#### 8D. Consolidation preserves meaning while losing specificity

Low-importance memories should not simply disappear. Their detailed event identity may become unavailable/fuzzy while their accumulated lesson remains in production social state.

Illustrative semantic result:

```text
Before:
    "Mina abandoned dinner on Day 14"
    "Mina cancelled our outing on Day 27"
    "Mina failed to show on Day 41"

After consolidation:
    background semantic history:
    "Mina has let me down before"
```

The exact background representation remains for the brief to design. It may be channel/semantic-summary state, a compact Legacy memory, or another extension of dyadic history, but it must:

- continue to feed the existing Signal/Appraisal/Consideration pipeline;
- retain enough provenance/summary to remain explainable;
- avoid double-counting the same event after the detailed memory is consolidated;
- permit important memories to remain individually inspectable and weighted.

#### 8E. Consolidation is threshold/maintenance work, not per-minute ticking

Do not scan and decay every memory continuously. Use deterministic lifecycle/maintenance points based on age, retention pressure, bounded memory thresholds, or another production-shaped trigger locked by the brief.

If stochasticity is eventually used, it must use the deterministic random oracle. Prefer deterministic scoring/threshold rules unless randomness produces demonstrated gameplay value.

#### 8F. Persistence, replay, and scale are part of the slice

The implementation is incomplete until it proves:

- two identical runs reinforce and consolidate the same memories;
- save/load preserves memory importance, consolidation eligibility, and resulting background state;
- an individually important memory survives while a comparable low-use memory consolidates;
- stronger repeated meaningful contribution produces greater persistence than weak/unused contribution;
- a consolidated memory's learned effect still changes later reasoning through the normal social/Decision pipeline;
- detailed and consolidated representations never simultaneously double-count one source event;
- long-running relationship memory storage remains bounded under synthetic multi-year simulation.

**Phase 8 done when:** important experiences remain specific because they repeatedly matter, ordinary experiences become increasingly generalized without erasing their learned social effect, and the system remains deterministic, explainable, persistent, and population-scale safe.

---

## 6. Explicitly Deferred Until After the MVP Gate

Unless a minimum-scenario design proves one is necessary, do **not** implement:

- broad economy;
- construction;
- generalized organizations/institutional simulation;
- actual `Defer` semantics;
- broad n-way commitment clustering;
- global reputation propagation;
- relationship-memory consolidation/attrition (planned as Phase 8 immediately after the MVP hardening gate; do not pull it into the 48-hour MPS solely to demonstrate aging);
- advanced pathfinding/multi-leg transport;
- secondary primary Activities/multitasking (subordinate interactions during a primary Activity remain in scope and are already architecturally supported);
- procedural life-history/culture breadth beyond what current social generation already supports;
- large content libraries;
- final art direction/polish;
- networking;
- mod support;
- DOTS/ECS migration.

Deferred does not mean rejected. It means it is not currently on the causal path to the 10-character playable proof.

---

## 7. Rules for Choosing the Next Task

Whenever an agent asks “what should I do next?”, use this decision order:

1. **Is active work unfinished?** Finish it first unless explicitly redirected.
2. **Is Phase 0 locked?** If not, the next task is product definition, not code.
3. **Find the earliest incomplete roadmap item whose prerequisites are complete.**
4. **Choose one end-to-end behavior, not a subsystem skeleton.**
5. **State the observable completion test before coding.**
6. **Use existing production architecture.** Extend a primitive only when the behavior proves it is missing something.
7. **Do not add a new subsystem merely because a later feature might need it.**
8. **Persist new authoritative state immediately.** Add save/load, deterministic replay, revisions/index rebuilding, and event payload codecs as applicable in the same slice.
9. **Author through the real content path.** Scenario-specific C# construction is acceptable only in focused tests, never as the production route for content the Unity game will use.
10. **Expose through real projections/commands before calling a player-facing slice complete.**
11. **Update `Docs/ImplementationStatus.md` when capability evidence changes and this roadmap when
    priority changes. Update other documents only when the decisions they own change.**

### Definition of “complete” for a roadmap slice

A slice is complete only when all applicable statements are true:

- headless behavior exists;
- behavior is deterministic;
- save/load continuation is equivalent;
- offline behavior is defined if time can cross it;
- Domain Event/scheduled settlement reaches quiescence;
- content is production-authored where applicable;
- Unity consumes read models and commands rather than duplicating rules;
- acceptance tests prove the causal behavior, not just class APIs;
- documentation status is reconciled.

---

## 8. Immediate Next Task

**Next task: draft and lock `PlayerAgencyBrief.md`, then reconcile it jointly with the reviewed
[`MinimumPlayableScenario.md`](MinimumPlayableScenario.md).**

Do not start another generic Core subsystem while Phase 0 remains open.

The Player Agency brief must decide, concretely:

1. Which character/location inspection surfaces are part of MVP play?
2. How Follow/Watch/Quiet and Mina's automatic Hold policy are exposed to the player.
3. Whether intervention is normal MVP play, and if so the resource economy, costs, regeneration, cap, refund presentation, and save/offline behavior.
4. Which one environmental circumstance the player can alter through a real management command.
5. Whether the reserved location-availability/open-state seam is that lever (the Commons is the current leading candidate).
6. Which Unity surfaces are required to understand and use every supported player action.
7. Whether one interactive Activity is in MVP scope or remains deferred.
8. What happens when important Decisions occur off-screen, on unwatched characters, or during OfflineCatchUp.

After that brief is drafted, reconcile both Phase 0 documents so the MPS canvas actually exercises every selected player action and the Player Agency brief does not assume world state the MPS never defines. Only then select the earliest missing Phase 1 causal link.

---

## 9. Roadmap Snapshot

| Area | Status | MVP relevance | Next action |
|---|---|---:|---|
| Deterministic simulation kernel | Complete | Critical | Maintain |
| Activities / travel / occupancy | Complete foundation | Critical | Expand through routine slices |
| Needs infrastructure | Complete foundation, narrow behavior | Critical | Lock Need set; add full-routine behavior |
| Commitments / planning | Complete foundation | Critical | Add real recurring obligation source |
| Decision engine/reasoning | Complete foundation | Critical | Add only scenario-driven Decision types |
| Social model/interactions | Complete foundation | Critical | Use in full routine; avoid new social architecture |
| Relationship-memory attrition/consolidation | Concept locked at roadmap level; scoring/threshold details unresolved | Post-MVP longevity | Draft focused brief after MVP gate, then implement Phase 8 |
| Commitment conflict/accountability | Complete v0 | Important | Reuse; defer broader semantics |
| Player Knowledge/Attention | Complete foundation | Critical | Surface broadly in UI |
| Intervention mechanics | Mechanically proven | Critical | Define/implement resource economy |
| Player environmental management | Reserved seam, lever not selected | Critical | Player Agency Brief; Commons availability is leading candidate |
| Employment | Conceptual/architectural only | Critical for reviewed MPS | Lock/implement authority + obligation-pattern v0 |
| Hobbies/discretionary routine | Conceptual only | Critical | Start with concrete Tabletop Games + Reading Activities |
| Sleep/Energy loop | Missing from playable routine | Critical | First likely routine vertical slice |
| 8–12-character multi-day scenario | Missing | Critical | Build after routine links exist |
| Unity general UI | Narrow prototype only | Critical | Build after headless loop is trustworthy |
| Concrete save format | Deferred | Required for MVP completion | Select after world loop stabilizes |
| Interactive Activity | Seam only | Optional | Pull in only if Player Agency Brief requires it |
| Economy/construction | Unideated/deferred | Not required | Post-MVP |
| Advanced pathfinding | Deferred | Not required | Post-MVP unless scenario proves otherwise |

---

## 10. North Star for This Stage

The architecture has already demonstrated that Vivarium can simulate choices correctly.

The next milestone is to demonstrate that it can simulate **lives**.

The small-world MVP is successful when the player can look at any one of roughly 8–12 characters (target: about 10) and answer:

> Where are they? What are they doing? Why are they doing it? What are they planning next? Who matters to them? What do they believe? What choice are they facing? What can I do about it? What happened because of the last choice?

If those answers all come from the same deterministic, persistent, scalable systems that would support 1,000 characters, the MVP has done its job.
