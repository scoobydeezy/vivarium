# Vivarium — Minimum Playable Scenario Brief

**Status:** Locked Phase 0 scenario contract
**Last reconciled:** 2026-08-24
**Purpose:** Define the smallest production-shaped Vivarium scenario that proves a small population can live complete autonomous lives, produce meaningful Decisions, react socially to consequences, continue off-screen, and expose the intended player-attention loop without relying on disposable scaffolding.

**Depends on:** [`../../README.md`](../../README.md), [`CoreIdentity.md`](CoreIdentity.md),
[`../Architecture.md`](../Architecture.md),
[`../IMPLEMENTATION_GUIDELINES.md`](../IMPLEMENTATION_GUIDELINES.md),
[`../Design/DecisionReasoning.md`](../Design/DecisionReasoning.md),
[`../Design/SocialModel.md`](../Design/SocialModel.md),
[`../Design/CommitmentConflict.md`](../Design/CommitmentConflict.md),
[`../Design/CommitmentAccountability.md`](../Design/CommitmentAccountability.md),
[`Roadmap.md`](Roadmap.md), and [`PlayerAgencyBrief.md`](PlayerAgencyBrief.md).

---

## 1. Scenario Goal

The Minimum Playable Scenario (MPS) should answer one product question:

> **Can Vivarium make approximately ten autonomous characters feel like they are living understandable lives that continue whether or not the player is watching them?**

The scenario must prove more than that the underlying systems function independently.

It should demonstrate a coherent loop:

```text
Character state
    ↓
Routine planning
    ↓
Activity + Travel
    ↓
Needs / social context / obligations change
    ↓
Meaningful Decision
    ↓
Player may notice / inspect / intervene
    ↓
Character resolves choice
    ↓
Activity / Commitment / social consequence
    ↓
Knowledge + history + relationship state change
    ↓
Future routine and Decisions differ
```

Vivarium already has production-shaped implementations of the major architectural pieces in this loop: Activities and Travel, living Decisions, Knowledge, social appraisal, commitment conflict, accountability, persistence, determinism, and Unity authoring/presentation seams.

The purpose of the MPS is to combine them into **complete lives**, rather than continue proving them as isolated vertical slices.

---

## 2. Design Principle: Author Causes, Not Outcomes

The scenario is deliberately constructed to make certain events **likely and testable**, but those events must arise through production simulation.

The scenario may author:

- personalities;
- beliefs;
- relationships;
- Needs;
- schedules;
- recurring obligation patterns;
- Interests;
- available Activities;
- location affordances;
- travel times;
- initial state.

It must not author narrative outcomes such as:

```text
07:12 Mina talks to Glen.
12:03 Mina becomes hungry.
17:00 Mina chooses Dinner.
18:00 Glen becomes disappointed.
```

Instead, it authors circumstances such that those results emerge naturally.

For example:

```text
Mina and Glen:
    leave nearby homes at similar times
    work at the same location
    share an existing relationship
    are eligible interaction candidates

Therefore:
    a shared-travel interaction is likely,
    but is still produced by the real interaction system.
```

This distinction is fundamental.

> **The MPS is a deterministic causal fixture, not a scripted story.**

If changing a character's schedule, Need state, relationship, location affordance, or travel time naturally changes an expected beat, the scenario is working correctly.

If the beat still occurs because the scenario explicitly schedules it regardless of those inputs, the scenario is hiding missing simulation.

### 2.1 Ownership rule for scenario prose

Whenever an expected causal beat contains a noun or rule that no current production system owns, that is not harmless flavor.

It must be one of two things:

1. an explicit MPS dependency to define and implement; or
2. removed/rephrased so the beat can arise from systems that really exist.

Examples include:

```text
Supervisor
Closing duty
Meal break
Staff coverage
Open/closed location
```

The brief may identify these requirements before the infrastructure exists. It may not pretend prose alone makes them authoritative state.

---

## 3. Design Principle: Complete Lives Continue Off-Screen

The scenario must prove that Unity presentation is not the owner of character existence.

A character's authoritative:

- current Activity;
- location;
- Travel;
- Needs;
- Commitments;
- Decisions;
- social interactions;
- Knowledge;
- relationship changes;

continue whether the character is visible or not.

The MPS therefore distinguishes three concepts.

### Simulation Presence

The character exists and continues to simulate.

### Spatial Presence

The character is physically located somewhere or is Traveling.

### Player Attention

The player is currently viewing, following, watching, or otherwise prioritizing that character.

These are intentionally orthogonal.

> **Leaving the visible canvas must never suspend or simplify a character's life.**

A headline acceptance case is:

> **A character leaves the player's current location, spends several simulated hours living entirely elsewhere, undergoes meaningful state changes, and later returns carrying the correct consequences of what happened off-screen.**

---

## 4. Minimum Canvas

The architectural minimum is two locations: one visible, one elsewhere.

The MPS will use **three primary locations** because three proves a network rather than a binary scene transition while remaining extremely small.

Each location also owns an explicit set of Activity affordances. A character may not perform an Activity merely because the scenario prose would find it convenient.

### 4.1 Residential Block

Represents the characters' homes or apartments.

Primary functions:

- Sleeping;
- waking;
- Eating;
- Reading;
- household interaction;
- morning/evening routine;
- departures and returns.

Multiple households may exist within this broad location for MPS purposes if finer room-scale space is not yet behaviorally necessary.

### 4.2 Bakery

The primary structured-work location.

Primary functions:

- Working;
- recurring Employment obligations;
- workplace hierarchy;
- coworker interaction;
- Work-context Activity pressure;
- attendance/accountability;
- distinct closing-duty obligations.

For the first MPS, the Bakery **does not automatically afford an ordinary Eating Activity during an uninterrupted Work commitment**.

That is a deliberate content rule for this scenario, not a claim that future Employment can never support breaks.

If realistic meal-break behavior later proves necessary, it should expose the real missing production feature—interruptible obligations / break windows / availability—not be smuggled into this fixture as an unstated exception.

The Bakery intentionally reuses the strongest existing Golden Scenario content rather than inventing an unrelated workplace.

### 4.3 Commons

A shared discretionary/recreation location.

Primary functions:

- Tabletop Games;
- Reading;
- Recreation;
- informal Socializing;
- non-work social networks;
- cross-group encounters.

The Commons exists specifically so that social life is not synonymous with workplace interaction.

The Commons owns authoritative **Open/Closed availability** and is the MVP's first environmental
management lever. The player may spend one Nudge to change that state under the rules locked in the
[`Player Agency Brief`](PlayerAgencyBrief.md). Closing changes real Activity availability and planning;
it never directly commands a character or ejects an Activity already underway.

---

## 5. Travel Network

The MPS should use a deliberately small but meaningful TravelNetwork.

Illustrative first topology:

```text
Residential Block ←→ Bakery
        ↕               ↕
        └────→ Commons ←┘
```

Provisional travel times:

```text
Residential → Bakery     12 minutes
Residential → Commons     8 minutes
Bakery → Commons          5 minutes
```

These values are tuning inputs, not architectural truth.

Their purpose is to make travel materially affect:

- departure timing;
- commitment feasibility;
- lateness;
- shared-route interaction;
- TravelBurden Considerations;
- whether two characters actually overlap at a destination.

### Design Intent

Travel should not merely animate movement between scenes.

Travel is part of character planning.

Changing travel time should be capable of changing a later outcome without special-case logic.

### 5.1 Flagship travel-feasibility requirement

At least one MPS commitment conflict must be **materially caused by duration + travel**, not merely by two clock windows visibly overlapping.

The intended Dinner-vs-Closing beat should be tuned so that a naive calendar appears close to feasible but actual route time makes the commitments jointly infeasible.

Illustrative shape only:

```text
Closing Duty
    Bakery
    runs late enough that Mina cannot depart early

Dinner with Glen
    Residential Block
    latest viable start shortly after Closing ends

Bakery → Residential
    12 minutes
```

#### Worked tuning example — not a locked schedule

One possible tuning pass could use:

```text
Closing Duty
    Bakery
    scheduled through 18:15

Dinner with Glen
    Residential Block
    latest viable start 18:20

Bakery → Residential
    12 minutes
```

On clock windows alone, those commitments appear compatible: Closing ends five minutes before Dinner's latest viable start. Once travel is included, Mina must leave the Bakery by **18:08** to preserve Dinner. The set is therefore jointly infeasible even though the commitment windows themselves do not overlap, and `LatestResolutionAt` becomes actionable before Closing's natural end.

These numbers are deliberately illustrative. The final schedule should be tuned during fixture construction so that travel meaningfully changes feasibility and `LatestResolutionAt`; the MPS does **not** lock 18:08, 18:15, or 18:20 as canonical times.

On a dumb calendar:

```text
Closing ends before Dinner's latest start.
```

In the actual world:

```text
Mina cannot teleport home.
```

This gives `CommitmentFeasibilityService`, Travel, and `LatestResolutionAt` a real reason to exist in the same scenario.

---

## 6. Visible Canvas

The initial playable presentation should show **one primary location at a time**.

The player may change which location they are viewing.

Characters at other locations continue normally.

This is a deliberate product constraint for the MPS because simultaneously displaying the entire world weakens the off-screen-life test.

While the player watches the Commons:

```text
Mina may be Working at the Bakery.
Jo may be Sleeping at Home.
Ravi may be Traveling.
Glen may make an autonomous Decision elsewhere.
```

The simulation remains identical except where player Observation or explicit player Commands legitimately alter later Knowledge or circumstances.

> **The camera is not an authoritative simulation input merely because it is looking somewhere.**

Observation and Attention may react to semantic WatchState, but they remain distinct from simulation truth.

The location surface exposes the Commons' Open/Closed state and its management Command. The MPS must
compare an open control branch with a managed branch so the player's circumstance change has an
observable downstream consequence.

---

## 7. Scenario Population

Target population:

> **Approximately 10 characters; expected tuning range 8–12.**

The exact headcount is not sacred.

Coverage is.

If one character is forced to carry incompatible test roles, split the role into an eleventh character. If two characters become redundant, nine is acceptable.

The population should remain small enough to understand individually while large enough to create overlapping routines and social networks.

The characters should not merely represent different personalities.

They form a **coverage matrix** for distinct life states, schedules, social structures, and player-attention conditions.

Every character should add at least one important configuration that the rest of the cast does not adequately cover.

At the same time, characters must overlap enough to resemble a community rather than isolated integration tests.

---

## 8. Provisional Cast

Names and detailed characterization remain editable. Their **scenario roles** are more important than final characterization at this stage.

| Character | Primary MPS role |
|---|---|
| **Mina** | Player focal character. Important Decisions default to Hold. Day-shift Bakery worker. Several meaningful social relationships. Existing Need-vs-Work and evening commitment-conflict content can center on her. |
| **Glen** | Autonomous contrast to Mina. Coworker, shared travel context, friend. Dinner stakeholder. Later Reliance Decision proves accountability feedback. |
| **Darius** | Later Bakery schedule and authoritative supervisor role. Competent but socially uncomfortable for Mina. Closing-duty obligation competes with Dinner. |
| **Priya** | Highly stable ordinary routine. Reliable, structured, relatively low-drama control character. |
| **Marcus** | Negative coworker context. His presence/absence changes Work pressure and can change a living Decision reason. |
| **Tess** | Home-centered/household relationship. Exercises sleep, Eating, household overlap, and life outside Bakery employment. |
| **Owen** | Recreation-heavy routine. Main social network centered on Commons rather than workplace. Strong Tabletop Games Interest. |
| **Lena** | Commons-based bridge character. Connects otherwise separate social clusters through legitimate interaction. |
| **Jo** | Different daily rhythm, potentially later work/sleep. Creates obligations and social life for Glen unrelated to Mina. |
| **Ravi** | Travel-timing connector. Begins or frequently remains in Transit at unusual times, misses otherwise plausible encounters, and proves off-screen Travel continuity. |

### 8.1 Ravi's role is timing, not a fake long commute

The three-location network is intentionally small.

Ravi should not be described as having a dramatically longer commute unless the network later gains a real longer route.

His distinguishing coverage is:

- unusual departure times;
- being mid-Travel at scenario start;
- moving against the dominant commute pattern;
- narrowly missing other characters because of timing;
- remaining off-screen while life continues.

---

## 9. Mina as the Attention / Hold Character

Mina is the MPS's primary interactive character.

Her Attention policy should cause qualifying important Decisions to enter **Held** state awaiting player attention.

For MVP content, qualifying means the Decision is `HoldEligible` and its derived Importance clears the
configured Auto-Hold floor ([`DecisionImportance.md`](../Design/DecisionImportance.md)). The numeric floor
remains intentionally tunable. Mina begins with the durable Auto-Hold policy selected. Follow remains an
independent player toggle, and the player may switch Mina among Normal, Auto-Hold, and Quiet.

The other characters do not use this automatic Hold behavior.

They resolve eligible Decisions autonomously.

### Design Intent

This creates a natural control comparison:

```text
Mina:
    important Decision appears
    ↓
    Hold gives player time to inspect/intervene
    ↓
    simulation continues

Others:
    same authoritative Decision architecture
    ↓
    autonomous resolution
    ↓
    simulation continues
```

Hold must not mean:

> stop Mina's entire life whenever something interesting happens.

Ordinary planner behavior remains autonomous.

Only real Decisions qualify.

Hard deadlines still outrank Hold. A commitment-conflict Decision cannot remain Held beyond its derived `LatestResolutionAt`.

During `OfflineCatchUp`, newly created Decisions do not enter Hold. They resolve under ordinary rules
and appear in the Knowledge-filtered recap after catch-up.

### Product Lesson

Mina should teach:

> **The player may pay special attention to a person without controlling them or stopping time for them.**

---

## 10. Character-State Coverage

The cast should deliberately cover different states across several dimensions.

### 10.1 Schedule Coverage

Across the cast:

- early start;
- normal daytime work;
- later shift;
- no work obligation during part of the scenario;
- evening obligation;
- discretionary morning;
- mismatched sleep/wake times.

### 10.2 Household Coverage

Include:

- at least one shared household;
- at least one character whose major social connection happens at home;
- at least one character spending substantial time alone.

### 10.3 Travel Coverage

Include:

- short trip;
- shared route;
- route with overlapping timing;
- route where timing narrowly prevents interaction;
- a character already Traveling when the scenario begins;
- at least one off-screen journey whose consequences are visible only later.

### 10.4 Need Coverage

Different characters should naturally pressure different Needs.

Locked MPS Need set:

```text
Hunger
Energy
Social
Recreation
```

#### Design Intent

Four Needs are enough to produce distinct daily pressures without prematurely creating a large Need taxonomy.

They roughly test:

```text
Hunger
    → Eating / obligation conflict

Energy
    → Sleep / staying out / work readiness

Social
    → seeking interaction / social opportunities

Recreation
    → hobby and discretionary-time behavior
```

A new Need should not enter the MPS unless it produces an important behavior these cannot express.

---

## 11. Minimum Activity Vocabulary

Locked minimum MPS primary Activities:

```text
Sleeping
Eating
Working
Traveling
Socializing
Recreation
Waiting / Idle
```

Concrete initial Recreation definitions:

```text
Tabletop Games
    location: Commons
    social recreation
    favored by characters with matching Interest

Reading
    location: Residential Block or Commons
    solitary recreation
    can satisfy Recreation without necessarily satisfying Social
```

### Design Intent

This set is intended to explain essentially the entire day for the test population.

The scenario should reveal when a genuinely missing Activity concept exists.

It should not add Activities merely to give every small behavior its own category.

### 11.1 Subordinate interaction is not a second primary Activity

The MPS relies on subordinate social interactions during Activities such as Traveling or Working.

For example:

```text
Primary Activity: Traveling
Subordinate occurrence: conversation with Glen
```

Mina is not simultaneously assigned two competing primary Activities.

Secondary primary Activities / general multitasking remain outside MVP scope.

---

## 12. Routine Diversity

The scenario should contain several routine sources.

### Structured Routine

Examples:

- Bakery shift;
- closing duty;
- household/social commitment.

These arise from Commitments and their real production sources.

### Need-Driven Routine

Examples:

- Hunger produces Eating;
- Energy produces Sleeping or rest-oriented planning.

### Discretionary Routine

Examples:

- Owen chooses Tabletop Games at Commons;
- another character chooses Reading;
- Tess remains Home;
- another character seeks Socializing.

### Design Intent

The scenario must prove characters can fill a day without every transition being authored as a Commitment.

Employment cannot be the universal answer to:

> What does a character do next?

Likewise, Need thresholds cannot become the universal answer.

A believable life combines:

```text
obligation
+
physical need
+
habit/routine
+
preference
+
social opportunity
+
meaningful choice
```

---

## 13. Employment v0 — Explicit MPS Dependency

The reviewed MPS requires a smallest-real Employment/structured-obligation system before its flagship work beats are production-shaped.

Employment v0 must own at least:

```text
Employment identity
Employer / workplace
Role
Work location
Workplace authority / hierarchy
Recurring obligation patterns
Stakeholder derivation
Attendance / fulfillment lifecycle
```

### 13.1 Workplace authority

The statement:

> Darius is Mina's supervisor.

must exist as authoritative, queryable simulation state.

It must be available to the ordinary semantic fact/Signal path so social appraisal and Decision Considerations may consume it without a scenario-specific lookup.

### 13.2 Obligation patterns

Employment v0 must materialize more than one kind of Commitment through the same production-shaped mechanism when the scenario requires it.

For Mina:

```text
Regular Shift
    recurring Employment obligation

Closing Duty
    distinct Employment obligation pattern
    separate window/duration
    real stakeholder/authority relationship
```

The MPS does **not** require a hard-coded `ClosingBakeryCommitment` subsystem.

The production abstraction is an authored Employment obligation pattern that materializes normal Commitments.

Future opening duty, staff meeting, training, or on-call patterns should be able to reuse the same mechanism.

### 13.3 Accountability role

Where Darius is the human supervisor/authority stakeholder, the resulting Commitment should use the real stakeholder-role machinery rather than a scenario-only social consequence.

Institutional stakeholders remain deferred; character-as-authority is sufficient for v0.

### 13.4 Explicitly not required

Employment v0 does not require:

- wages;
- payroll;
- business finances;
- promotion ladders;
- staffing simulation;
- break scheduling;
- institutional relationship state.

Those remain deferred unless the MPS later proves one is behaviorally necessary.

---

## 14. Social and Belief Coverage

The population should begin with deliberately varied social states.

The production Social Model is directional and belief-relative; the MPS should exercise that rather than reducing relationships to static authored flavor.

Required starting cases should include:

### Well-Known Relationship

Example:

```text
Mina → Glen
```

High familiarity and relatively confident beliefs.

### Partially Known Relationship

Example:

```text
Mina → Darius
```

Mina has strong evidence about some dimensions but meaningful uncertainty about another.

### Weak Acquaintance

Example:

```text
Owen → Lena
```

Sparse evidence and high uncertainty.

### Directional Asymmetry

At least one pair should evaluate each other differently.

### Existing Positive History

At least one relationship contains prior positive evidence or memory.

### Existing Negative History

At least one pair contains discomfort, resentment, or another directional negative condition.

### Incorrect or Weakly Supported Belief

At least one character should begin with a belief that later observed evidence meaningfully changes.

### Design Intent

The MPS should demonstrate:

> **Characters respond to the person they currently believe exists, not to hidden omniscient truth.**

---

## 15. The Ordinary Character

At least one character—provisionally Priya—should be authored so an uneventful day is likely.

Expected pattern:

```text
Wake
→ Eat
→ Travel
→ Work
→ Finish Work
→ Travel
→ Eat
→ Recreation or Socializing
→ Sleep
```

Priya may interact socially and accumulate ordinary evidence.

She should **not** be given a special rule preventing Decisions.

### Design Intent

This is a critical control case.

If Priya unexpectedly generates a meaningful Decision during tuning, that is legitimate simulation output.

The first response should be to inspect:

- Decision-generation thresholds;
- her authored Need state/rates;
- her obligations;
- the surrounding circumstances.

Do not quietly add a `NeverGenerateDecision` exception.

If every character generates dramatic Decisions several times per day, Vivarium risks becoming a crisis generator instead of a life simulation.

The MPS therefore tests:

> **Can an ordinary day remain interesting as observable life without constantly becoming a Decision encounter?**

---

## 16. Eating Affordances and Mina's Hunger Beat

Eating must be production behavior, not narrative shorthand.

For the first MPS:

- Residential Block affords Eating.
- Bakery does not provide an ordinary Eating Activity while a character remains inside an uninterrupted Work obligation.
- Commons may later afford Eating only if the Player Agency / location-content design explicitly adds it; it is not required by this brief.

Priya therefore eats before and after work in the control-day example.

Mina is deliberately authored so Hunger becomes behaviorally important **during Work**, creating real competition between Need satisfaction and her ongoing obligation.

### Design Intent

The scenario should not invent a meal-break system or staffing-coverage rule simply to explain one worked example.

If future testing shows that believable Employment requires meal breaks, then break windows / interruptible obligations become a real product requirement and should be designed as such.

---

## 17. Initial Scenario State

Do not initialize all characters identically.

At scenario start, characters should already occupy different life states.

Illustrative starting state:

```text
Mina
    waking at Home

Glen
    already Traveling toward Bakery

Priya
    Eating before work

Marcus
    already Working

Jo
    Sleeping

Lena
    beginning an opening routine at Commons

Ravi
    midway through Travel

Owen
    free until an afternoon discretionary plan

Tess
    at Home with rising Hunger

Darius
    off work until later in the day
```

### Design Intent

A staggered start immediately stresses:

- analytical progression;
- Activities;
- occupancy;
- Travel;
- Need state;
- schedule differences;
- off-screen continuity.

A synchronized “everyone begins at 06:00 with neutral Needs” fixture would exercise less and look less like an already-living world.

---

## 18. Expected Causal Beats

These are expected outcomes of the authored initial state.

They are not directly scheduled narrative events.

The exact timestamps and even some specific participants may change as the fixture is tuned.

---

## 19. Morning — Shared Travel Interaction

Expected conditions:

```text
Mina and Glen
    depart Residential near one another
    travel along compatible context
    have sufficient social relevance
```

Expected result:

```text
bounded interaction opportunity
→ interaction may occur
→ social evidence/history may update
```

A second pair should narrowly miss one another because of schedule timing.

### Design Intent

We need both positive and negative proofs:

> overlapping lives create opportunities;

and:

> merely sharing a route does not guarantee interaction.

---

## 20. Workday — Context Changes Behavior

Mina and Marcus overlap at the Bakery.

Their negative relationship produces Work-context pressure through the already-supported Activity/social context path.

Marcus later leaves.

Expected chain:

```text
Marcus present
→ Work Activity modifier active

Marcus leaves
→ modifier changes/removes
→ any dependent living Decision reason reevaluates
```

### Design Intent

This proves a character's current reasoning remains connected to the changing world rather than being frozen when a Decision is created.

Marcus leaving is **not** assumed to create a staffing-coverage obligation. Staffing levels are not part of Employment v0 unless a later design explicitly adds them.

---

## 21. Midday — Need vs Obligation

Mina's Hunger rises while she is Working.

Expected chain:

```text
Hunger becomes behaviorally important
→ meaningful Need-vs-Work Decision
→ Mina's Attention policy Holds it
→ player inspects the known reasons
→ player spends one Nudge on Emphasize or Temper
→ player Releases it, or lets its ordinary/hard deadline resolve it
→ world continues around her
```

The dilemma exists because Eating is not ordinarily available while she remains inside the uninterrupted Work obligation in this first MPS.

Other characters continue their lives while Mina's Decision remains Held.

### Design Intent

This is the first explicit demonstration of:

```text
character autonomy
+
player Attention
+
limited intervention
```

without direct player choice.

It also distinguishes Priya's ordinary control day from Mina's deliberately pressured state through authored circumstances rather than character-specific suppression.

---

## 22. Autonomous Decision Comparison

During the same general period, at least one non-Mina character should generate a meaningful Decision.

Their automatic Hold setting is off.

Expected chain:

```text
Decision generated
→ possibly surfaced to player according to Attention policy
→ no player intervention
→ deterministic autonomous resolution
→ consequences
```

### Design Intent

The player should understand that Mina is not running on a special Decision engine.

She is merely receiving different Attention treatment.

---

## 23. Evening — Travel-Caused Commitment Conflict

Mina has two individually plausible obligations:

```text
Dinner with Glen
    Residential Block

Closing Duty
    Bakery
    produced by Employment v0
```

Their windows should be authored so that they are **not merely obvious clock-overlap conflicts**.

Instead, duration + required travel makes the set jointly infeasible.

Existing commitment-conflict mechanics generate plan-valued Options:

```text
Preserve Dinner / Relinquish Closing

Preserve Closing / Relinquish Dinner
```

The Decision is Held because Mina is the focal character.

However, its feasibility deadline continues approaching.

If the player ignores it, it resolves autonomously at the real hard deadline.

### Design Intent

This is the strongest demonstration that:

> Hold means “await attention when possible,” not “suspend causality.”

It also proves that commitment feasibility is spatial/temporal planning rather than a simple overlap detector.

---

## 24. Evening Consequence — Accountability

Suppose Mina relinquishes Dinner.

Expected chain:

```text
CommitmentLifecycleService
→ Relinquished outcome
→ Glen observes outcome as stakeholder
→ stakeholder-facing KnownAttribution
→ social evidence / possible memory / channel change
→ Glen's later belief/appraisal changes
```

The consequence must use the production accountability pipeline, not a special Golden Scenario relationship modifier.

If Mina instead fulfills Dinner, ordinary fulfillment routes through Dependability-relevant Evidence.

### 24.1 Routine-fulfillment safeguard

Repeated ordinary fulfilled obligations must **not** create an ever-growing pile of direct Trust deltas or RelationshipMemory.

For routine fulfillment:

```text
Fulfilled
→ EvidenceContribution
→ belief update
→ live Reliance changes naturally
```

Direct Trust/Resentment channel changes and salient memories remain reserved for outcomes whose authored policy makes them meaningful.

### 24.2 Attribution safeguard

Glen must react only to what he actually knows about the outcome.

The social consequence path must not read authoritative causal truth directly and silently teach Glen information he never observed or learned.

For example, Glen may know:

```text
Mina relinquished Dinner.
```

He does not automatically know:

```text
She did it specifically because Darius required closing help.
```

unless that fact reaches him through an ordinary Knowledge/disclosure path.

---

## 25. Next Day — History Changes Choice

Glen later faces a Decision in which Mina is a relevant Option or subject.

The same Decision should produce measurably different reasoning depending on what happened the previous evening.

Expected comparison:

```text
Timeline A:
Mina kept Dinner
→ Glen has stronger Reliance reason

Timeline B:
Mina relinquished Dinner
→ Glen has weaker Reliance
   and/or a salient negative reason
```

### Design Intent

This is the MPS's most important long-loop proof:

> **Yesterday matters.**

Vivarium's social history must be causally load-bearing rather than decorative.

---

## 26. Secondary Independent Life Chain

The scenario must contain at least one significant causal chain that is not primarily about Mina.

Illustrative candidate:

```text
Tess's Energy rises
→ she changes or skips an evening Recreation opportunity

Owen reaches Commons under different social circumstances
→ Tess is absent

Owen instead interacts with Lena
→ evidence changes

Next day
→ Owen's discretionary/social planning differs
```

The exact participants remain tunable, but the locked agency comparison adds:

```text
Control branch:
    Commons remains Open
    → Owen's ordinary evening plan remains available

Managed branch:
    player spends one Nudge to close Commons before planning
    → Commons-dependent Travel / Recreation cannot begin
    → Owen selects from the remaining real affordances
    → later social opportunity differs
    → player reopens Commons and the affordance returns
```

The result must emerge from real planning, Need, availability, and interaction rules. Closing the
Commons must not directly assign Owen a replacement Activity.

### Design Intent

Ten characters cannot be:

> Mina + nine props.

At least one other person's routine must create emergent consequences that would still happen if Mina were removed from the world.

---

## 27. Off-Screen Acceptance Chain

One expected chain should happen entirely outside the player's viewed location.

Example:

```text
Player remains focused on Commons.

Meanwhile at Bakery / elsewhere:
    Glen finishes Work
    travels
    encounters another character
    resolves an autonomous choice
    arrives Home
    social state changes

Player later selects Glen.
```

The character view must correctly show:

- current Activity;
- current location;
- Need state;
- changed social state;
- relevant recent History;
- resolved Decision where player Knowledge permits.

### Design Intent

This is not merely a technical background-simulation test.

It proves the intended experience:

> **The world feels larger than the screen.**

---

## 28. Locked Player Agency Integration

The jointly reconciled [`Player Agency Brief`](PlayerAgencyBrief.md) locks the MPS player loop:

```text
view one location
→ inspect / Follow characters
→ set Normal, Auto-Hold, or Quiet attention
→ inspect a surfaced Decision
→ optionally Hold / Release
→ optionally spend a Nudge to Emphasize or Temper one known reason
→ optionally spend a Nudge to open or close Commons
→ review Knowledge-filtered off-screen / offline recap
```

The MPS begins with three Nudges and uses the same eight-hour regeneration boundaries as production.
The two-day acceptance run must exercise a Decision intervention and a Commons state change without
directly choosing a character outcome. Interactive Activities are explicitly deferred from MVP.

---

## 29. Attention Coverage Matrix

Run equivalent scenario branches under several Attention conditions.

| Condition | Authoritative simulation | Expected presentation effect |
|---|---|---|
| Viewing character's location | Normal | local observable events available |
| Viewing another location | Normal | character continues off-screen |
| Character Followed | Normal | relevant events receive greater Attention |
| Character unwatched | Normal | Observation/Knowledge may be reduced |
| Character Quiet | Normal | proactive notification suppressed; history remains |
| Mina Decision Held | World continues | Decision awaits player within Hold rules |
| Non-Mina Decision active | World continues | autonomous resolution |
| Hard deadline reached | World continues | Hold cannot prevent resolution |
| OfflineCatchUp | Same physical rules; no new Hold | bounded recap replaces live presentation |
| Commons Closed | Availability-dependent planning reacts | location state and causal consequence are explained |

### Design Intent

The MPS should prove that Attention changes **what the player is offered**, not the basic physical rules of the world.

---

## 30. 48-Hour Coverage Targets

Within the first approximately two simulated days, the fixture should naturally exercise all of the following.

### Routine

- at least one character wakes from Sleep;
- at least one remains asleep while others are active;
- ordinary Eating occurs without a Decision;
- ordinary Work occurs;
- at least one character performs Tabletop Games or another concrete Recreation Activity;
- at least one character uses Reading or another solitary Recreation Activity;
- at least one character Socializes outside work;
- at least one character has substantial discretionary time;
- at least one character completes an uneventful routine without suppression hacks.

### Travel / Space

- several successful trips;
- at least one character begins the test already Traveling;
- at least one shared-route interaction;
- at least one missed interaction due to timing;
- at least one meaningful event occurs off-screen;
- at least one character leaves the visible location and later returns with changed state;
- at least one commitment conflict is caused materially by travel/duration rather than simple time overlap.

### Social / Knowledge

- one well-known relationship;
- one uncertain relationship;
- one weak acquaintance;
- one asymmetric relationship;
- one belief changes through observed evidence;
- one social consequence affects later reasoning;
- routine fulfillment changes Reliance through Evidence without direct Trust/Memory accumulation;
- stakeholder attribution remains limited to what the stakeholder actually knows.

### Decisions

- one Need-driven Decision;
- one autonomous non-Mina Decision;
- one Mina Held Decision;
- one Decision reason changes while still active;
- one hard-deadline commitment conflict;
- one Decision occurs largely off-screen;
- historical explanation remains correct afterward.

### Commitments

- a normal fulfilled Commitment;
- compatible obligations that do not generate conflict;
- incompatible obligations that do;
- a Relinquished outcome;
- resulting stakeholder accountability;
- regular shift and closing duty originate from production-shaped Employment obligation patterns rather than scenario injection.

### Attention / Player Interaction

- player changes viewed location;
- player inspects Mina;
- player Follows and unfollows a character;
- player proves Quiet changes surfacing without changing simulation;
- Mina's important Decision becomes Held;
- player spends one Nudge to Emphasize or Temper a visible reason;
- player Releases Hold in one branch;
- ignoring the hard deadline still permits authoritative resolution;
- other characters continue autonomously throughout;
- player spends one Nudge to close the Commons and later reopens it;
- Commons availability changes a downstream plan/Activity/social opportunity without assigning an outcome;
- Nudge regeneration crosses at least one eight-hour boundary.

### Persistence

- save while characters occupy multiple different Activities;
- save during Travel;
- save while Mina has a Held Decision;
- save before commitment conflict;
- save with a non-full Nudge balance and the Commons Closed;
- reload and reproduce exact later outcomes;
- offline catch-up produces the same authoritative state, Nudge regeneration, and Commons-driven
  planning as uninterrupted simulation.

---

## 31. Existing Golden Scenario Integration Map

The MPS should absorb proven Golden Scenario beats into ordinary character life rather than retain them only as synthetic demonstrations.

| Existing proven beat | MPS production source |
|---|---|
| Mina/Glen shared-travel interaction | Their normal morning commute and bounded shared-travel candidate selection |
| Marcus Work pressure | Real coworker schedule overlap at Bakery |
| Hunger leave-work Decision | Mina's analytical Hunger progression during an uninterrupted Work obligation |
| Knowledge reveal / reason-label change | Real Observation/Knowledge path and living Decision reevaluation |
| Hold/intervention | Mina's Attention policy and normal Decision commands |
| Nudge economy | Authoritative balance, deterministic eight-hour regeneration, and refund path |
| Environmental management | Commons Open/Closed Command feeding real availability-dependent planning |
| Dinner vs Closing conflict | Social Commitment + Employment-produced Closing Duty + real Travel feasibility |
| Commitment hard deadline | Derived `LatestResolutionAt` from real windows/duration/travel |
| Accountability | Real `CommitmentLifecycleService` outcome and stakeholder consequence pipeline |
| Later Reliance Decision | Glen's later ordinary Decision consuming changed belief/appraisal |
| Save/reload equivalence | Same multi-day world, no scenario-specific alternate path |

### 31.1 Scenario-only injection retirement

The existing Golden Scenario currently uses authored scheduled input to make some obligations become known/materialize at a chosen instant.

That path remains a valid focused regression test.

It should **not** remain the production source of MPS Employment obligations once Employment v0 exists.

The MPS succeeds when regular shifts and Closing Duty originate from their real Employment/routine source.

---

## 32. Scenario Success Criteria

The MPS is successful when the following questions can be answered for every test character at any time.

> **Where are they?**

From their authoritative Activity SpatialContext.

> **What are they doing?**

From their primary Activity.

> **Why are they doing it?**

From routine planning, Need state, Commitments, preferences, social context, location affordances, or a resolved Decision.

> **What are they planning?**

From Commitments/routine state.

> **Who matters to them?**

From directional social state and history.

> **What do they believe?**

From observer-scoped Knowledge.

> **What meaningful choice are they facing?**

From a real living Decision where one exists.

> **What can the player do about it?**

From Attention and validated player Commands defined by the Player Agency Brief.

> **What happened because of the last choice?**

From authoritative consequences, history, Knowledge, and altered future reasoning.

---

## 33. Failure Criteria

The MPS is not ready if:

- characters stop or simplify when off-screen;
- Unity constructs or owns authoritative routine state;
- most daily transitions require hand-authored scheduled scenario events;
- all characters depend on Employment to know what to do;
- every Need threshold creates a dramatic Decision;
- every character constantly generates Decisions;
- Priya or another control character needs a special suppression rule to remain quiet;
- characters with no immediate Commitment become idle indefinitely;
- interactions require scripted pairs;
- social outcomes mutate generic relationship numbers without the Knowledge/Appraisal pipeline;
- routine fulfilled Commitments accumulate direct Trust deltas or RelationshipMemory instead of ordinary Dependability Evidence;
- stakeholder social reaction uses authoritative cause the stakeholder has not observed or learned;
- Mina behaves through different simulation rules from autonomous characters;
- Hold pauses the universe;
- travel is merely a presentation animation;
- commitment conflict is implemented as clock-overlap detection while ignoring required Travel;
- Employment prose such as “supervisor” or “closing duty” has no authoritative producer/query path;
- an unstated meal-break or staffing-coverage rule is introduced solely to rescue a scenario beat;
- location availability exists only as UI state rather than simulation truth;
- a Commons state change directly assigns a replacement Activity or character outcome;
- Nudge cost/refund/regeneration differs across live, save/load, or offline execution;
- Quiet suppresses simulation or prevents later history inspection;
- history changes but later reasoning does not;
- save/load changes an expected causal beat;
- changing a cause does not change its downstream outcome because the outcome was scripted;
- the player must watch a character for their life to continue.

---

## 34. Explicitly Out of Scope

The MPS should not require:

- broad economy;
- wages unless a later scenario revision proves them necessary;
- construction;
- generalized institutions;
- procedural population generation;
- advanced pathfinding;
- large transportation networks;
- full reputation propagation;
- actual `Defer` behavior;
- large-scale n-way commitment clustering;
- generations;
- final visual polish;
- DOTS/ECS migration;
- many hobbies;
- many job types;
- a large Need taxonomy;
- staffing coverage simulation;
- meal-break scheduling;
- secondary primary Activities / general multitasking;
- interactive Activities / mini-games;
- direct physical character interference, forced relocation, or action overriding;
- character beliefs about the Observer/AGI;
- AGI selection, Habitat progression, Culture, institutions, or multiple Habitats;

Scope should expand only when an observed MPS failure demonstrates a missing production capability.

---

## 35. Content Philosophy

The MPS may use exaggerated initial conditions.

That is acceptable.

Mina can begin with carefully tuned Hunger, Glen with specific beliefs, Marcus with a negative relationship, and several schedules can be selected specifically to create overlap.

The requirement is not realism of initial distribution.

The requirement is realism of causal execution.

> **Hand-author the loaded gun. Do not hand-author the bullet's path.**

---

## 36. Development Use

This brief and the Player Agency Brief are jointly locked. Implementation work should proceed by
building the **earliest expected MPS causal beat that the current simulation cannot yet produce through
production systems**.

That becomes the next vertical slice.

The slice is complete only when:

1. the circumstance exists in authoritative state;
2. production systems create the expected behavior;
3. the result is observable through the real query/presentation path;
4. deterministic replay matches;
5. applicable save/load and offline behavior match;
6. no temporary parallel system was introduced merely for the scenario.

---

## 37. Likely First Missing Links

Based on the current repository status and the joint Phase 0 review, the implementation order is:

1. **Energy → Sleeping → waking → replanning** — complete
2. **Employment v0 → workplace authority + recurring shift/closing-duty Commitments** — complete
3. **ordinary Hunger → Eating behavior governed by location/Activity affordances** — complete
4. **discretionary Recreation → Tabletop Games / Reading selection from Interests and availability** — complete
5. **ordinary Socializing behavior** — next
6. **Nudge economy + Commons availability management**
7. **Unity surfaces for following approximately ten simultaneous lives**

The immediate next slice is **ordinary Socializing behavior**. Revalidate later ordering after
each completed causal link; do not implement all routine links as one subsystem project.

---

## 38. Guiding Principle

The Minimum Playable Scenario is not successful merely because ten agents move around.

It is successful when approximately ten characters appear to have **lives**.

Those lives should intersect, separate, continue unseen, create obligations, accumulate history, produce choices, and occasionally become important enough that the player decides:

> **I want to pay attention to this person right now.**

The simulation continues either way.
