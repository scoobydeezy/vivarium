# Vivarium Decision Reasoning & Considerations Brief

**Status:** Accepted design reference; implementation checkpoints complete  
**Project:** Vivarium  
**Scope:** Decision influence construction — the deterministic bridge from simulation truth, Knowledge, social appraisal, needs, values, history, context, and practical facts into a small set of explainable option-relative reasons and dice.  
**Related documents:** [`../Architecture/Reference.md`](../Architecture/Reference.md) and
[`SocialModel.md`](SocialModel.md).

Current implementation evidence and remaining thin seams are tracked in
[`../ImplementationStatus.md`](../ImplementationStatus.md). The staged plan below is retained as design
rationale, not current roadmap priority.

---

# 1. Executive Summary

Vivarium's Decisions should not be authored as exhaustive tables such as:

```text
IF Mira asks Darius for help:
    Capability        d10
    History           d8
    Discomfort        d6
    Obligation        d8
```

That approach cannot scale across:

- thousands of characters,
- many Decision types,
- arbitrary option targets,
- changing world state,
- incomplete Knowledge,
- asymmetric relationships,
- new content.

Instead, Decisions should use a reusable middle layer called **Considerations**.

A Consideration answers a semantic question such as:

```text
How capable do I believe this option is?
How comfortable am I with this person?
How much obligation would this create?
How risky is this?
How expensive is this?
How well does this align with my values?
```

The canonical pipeline is:

```text
WORLD / KNOWLEDGE / SOCIAL STATE
            │
            ▼
      NAMED SIGNALS
            │
            ▼
   CONSIDERATION FIELDS
            │
            ▼
     CANDIDATE REASONS
            │
            ▼
   REASON CONSOLIDATION
            │
            ▼
   DECISION INFLUENCES
            │
            ▼
           DICE
```

Most Considerations are reusable across unrelated Decisions.

A Decision Definition binds those Considerations to the actor, each option, and the Decision's specific context.

The same underlying mathematical primitive used by Social Appraisal should power Considerations:

> **A deterministic field evaluator combines named signals, uncertainty, linear weights, pairwise interactions, optional ideal-point terms, and a bounded response into a calibrated result.**

However:

> **An AppraisalField is not a Consideration, and a Consideration is not an AppraisalField.**

They are separate Domain concepts that use the same lower-level **Signal Field** primitive.

The design goal is:

> **Detailed simulation truth becomes a small number of deterministic, comprehensible reasons without an N×M lookup table and without an opaque AI black box.**

---

# 2. Relationship to the Existing Decision Architecture

The frozen Vivarium architecture already establishes that a Decision:

- is a persistent runtime entity;
- may contain multiple Options;
- contains a true Influence set;
- can coexist with other Decisions subject to conflict scopes;
- remains live while world-derived Influences change;
- uses stable Influence identity;
- targets reevaluation through dependency indexes;
- can be Held or resolve autonomously;
- permits player intervention without direct outcome selection;
- separates true Influences from player-facing presentation.

This brief does **not** replace that architecture.

It specifies the missing construction step:

```text
World circumstances
        ↓
Decision generated
        ↓
Options instantiated
        ↓
Considerations evaluated per option
        ↓
Candidate reasons consolidated
        ↓
True DecisionInfluences created/updated
        ↓
Attention / presentation / intervention
        ↓
Dice resolution
```

The exact final dice-resolution formula and degree-of-success rules remain outside this brief.

---

# 3. Core Principle: Reasons Are Semantic Compression

The world may contain dozens of facts relevant to Mira's discomfort around Darius:

```text
Darius has high Agency.
Mira believes Darius has low Attunement.
Darius is Mira's supervisor.
Darius embarrassed Mira publicly.
Mira dislikes overbearing behavior.
Mira is uncertain whether Darius is warm.
```

The player should not automatically receive six correlated dice for those six facts.

For the Decision:

> Should Mira ask Darius for help?

those facts may all contribute to one human-scale reason:

```text
He makes me uncomfortable     d8
```

Therefore:

> **The detailed systems produce truth. Considerations compress relevant truth into candidate reasons. Reason consolidation prevents correlated candidate reasons from becoming duplicate dice.**

Every final Influence should answer:

```text
Why are you here?
```

and preserve a route back to the underlying state that produced it.

---

# 4. Shared Mathematical Primitive: Signal Fields

Social Appraisal and Decision Considerations both need the same general operation:

> Combine a named uncertain signal vector into one deterministic bounded evaluation.

Extract that operation into a lower-level primitive.

Conceptually:

```text
SignalFieldDefinition
├─ Bias
├─ LinearTerms[]
├─ PairwiseTerms[]
├─ IdealPoint?
├─ IdealFactorL?
├─ ResponseFunction
├─ CalibrationProfileId
├─ FieldRevision
└─ ProvenanceMetadata
```

and:

```text
SignalVector
├─ SignalKeys[]
├─ MeanVector
└─ Covariance
```

The input dimensions are not fixed globally.

For a Social Appraisal field, signals may be:

```text
Warmth
Agency
Stability
Attunement
...
```

For a Decision Consideration, signals may be:

```text
Comfort
PriorReciprocity
IndependenceValue
ExistingObligation
PowerImbalance
...
```

The evaluator is generic.

The Domain concepts remain distinct.

---

# 5. Signal Field Evaluation

For input signal vector `z`, define a pre-bounded score:

\[
s(z)=
b
+w^Tz
+z^TQz
-\frac12(z-i)^TP(z-i)
\]

where:

- `b` is the bias;
- `w` contains linear coefficients;
- `Q` is a sparse symmetric signed interaction matrix;
- `i` is an optional ideal point;
- `P = LL^T` is optional and positive-semidefinite;
- the ideal-point term supports "best around this amount" relationships.

The bounded result is:

\[
U(z)=g(s(z))
\]

where `g` is a bounded response function such as `tanh` or logistic.

This gives Considerations the same expressive tools as the Social Model.

Examples:

```text
More capability is better
    → positive linear coefficient

Very high obligation is bad
    → negative linear coefficient

Some familiarity is ideal, but extreme dependence is not
    → ideal-point term

Embarrassment matters more when target has authority
    → Embarrassment × PowerImbalance pairwise term
```

Not every Consideration should use all of these features.

Most should remain sparse and simple.

---

# 6. Uncertainty Is Part of a Signal

A Consideration must not receive only point estimates if the upstream system actually knows that the estimate is uncertain.

A signal is conceptually:

```text
SignalValue
├─ Mean
├─ Variance
├─ Applicability
└─ SourceRevision
```

Multiple correlated signals may additionally provide covariance.

For:

\[
z \sim \mathcal N(\mu,\Sigma)
\]

the SignalField evaluator should compute the exact expected **pre-bounded** score.

Let:

\[
A = Q-\frac12P
\]

and:

\[
r = w + Pi
\]

with constants folded into the bias.

Then:

\[
E[s(z)] =
b
+w^T\mu
+\mu^TQ\mu
+\operatorname{tr}(Q\Sigma)
-\frac12
\left[
(\mu-i)^TP(\mu-i)
+\operatorname{tr}(P\Sigma)
\right]
\]

The initial bounded estimate is:

\[
\widehat{E[U]} = g(E[s])
\]

This is the same explicit plug-in approximation used by the Social Model.

## 6.1 Residual uncertainty

Considerations also need a numerical uncertainty value.

For the expanded quadratic score:

\[
s(z)=k+r^Tz+z^TAz
\]

with symmetric `A`, the pre-bounded variance is:

\[
Var[s] =
2\,tr(A\Sigma A\Sigma)
+
(r+2A\mu)^T
\Sigma
(r+2A\mu)
\]

The first-order approximation in bounded-output units is:

\[
Var[U] \approx
g'(E[s])^2\,Var[s]
\]

The evaluator should therefore return both:

```text
ExpectedScore
OutputVariance
```

The exact statistical model can evolve, but **uncertainty must not disappear between layers**.

---

# 7. Social Appraisal Output Contract

Because Considerations may consume Social Appraisal results such as Comfort or Respect, the Social Model must expose residual uncertainty.

Conceptually:

```text
AppraisalLensResult
├─ AppraisalLensId
├─ ExpectedScore
├─ LatentScoreVariance
├─ OutputVariance
├─ CalibratedStrength
├─ FieldRevision
├─ BeliefRevision
└─ ProvenanceHandle
```

Therefore:

```text
Mira → Darius Comfort
```

can mean:

```text
ExpectedScore:  -0.42
Uncertainty:     high
```

rather than arriving at the Decision system as a falsely precise `-0.42`.

This is an integration contract between the Social Model and Decision Reasoning.

---

# 8. Uncertainty Does Not Have One Universal Meaning

Low certainty should **not** automatically damp every reason.

The psychological meaning of uncertainty depends on the Decision.

Examples:

### Dangerous reactor repair

```text
I think Darius may be capable.
But I'm very uncertain.
```

The uncertainty itself can strongly oppose choosing him.

### Moving a couch

The same uncertainty may be nearly irrelevant.

### Asking about his expertise

Uncertainty can favor the action:

```text
I don't know whether he can do this.
I should ask.
```

Therefore a Consideration may choose among several uncertainty policies.

Conceptually:

```text
UncertaintyPolicy
├─ IncorporatedIntoField
├─ PresentationOnly
├─ EmitDistinctCandidateReason
└─ IgnoreWhenNotMaterial
```

These are semantic policies, not required literal enum values.

If uncertainty becomes a distinct Candidate Reason, correlated-reason safeguards in §15 still apply.

---

# 9. ConsiderationDefinition

A Consideration is a reusable semantic reason generator.

Conceptually:

```text
ConsiderationDefinition
├─ ConsiderationId
├─ ParameterSchema
├─ SignalRequirements[]
├─ SignalFieldDefinition
├─ UncertaintyPolicy
├─ ReasonChannelId
├─ PresentationProfile
├─ ApplicabilityRules
├─ DependencyDescriptor
├─ Version
└─ ReusabilityScope
```

Example:

```text
social.obligation_aversion
```

might use signals:

```text
PersonalIndependence
ExpectedReciprocity
ExistingObligation
PowerImbalance
RelationshipComfort
```

and emit a reason in:

```text
ReasonChannel:
social.obligation
```

---

# 10. Reusable by Default, Local When Truly Local

The default question is:

> **Would this reason plausibly matter in several unrelated Decisions?**

If yes, prefer a reusable Consideration.

Examples:

```text
PerceivedCapability
InterpersonalComfort
PriorReciprocity
ObligationAversion
Risk
Cost
TravelTime
Duty
ValueAlignment
ReputationConcern
Loyalty
Fear
Curiosity
```

But architectural purity is not a goal.

A Decision Definition may contain a local Consideration when the reason is genuinely specific.

Example:

```text
Decision:
Break the Pilgrimage Oath

Local Consideration:
MeaningOfTheOathToThisCharacter
```

Do not create absurdly generic abstractions merely to avoid one local rule.

Local Considerations still use the same SignalField evaluator, calibration, provenance, and determinism rules.

---

# 11. Decisions Are Multi-Option by Default

No Consideration should assume Decisions are binary.

A Decision may be:

```text
WHO SHOULD I ASK TO REPAIR THE GENERATOR?

Ask Darius
Ask Glen
Ask Priya
Do It Myself
Wait
```

Considerations are evaluated **relative to each Option**.

The binary case is merely a degenerate multi-option Decision.

---

# 12. Parameter-Bound Consideration Evaluation

A Decision Definition does not simply list:

```text
perceived_capability
```

It binds the Consideration's declared parameters to Decision/Option context.

Conceptually:

```text
ConsiderationBinding
├─ BindingId
├─ ConsiderationDefinitionId
└─ ParameterBindings[]
```

Example:

```text
consideration: perceived_capability

bind:
    actor  = Decision.Actor
    target = Option.TargetCharacter
    task   = Decision.Context.RequestedTask
```

Parameter bindings should use stable typed/semantic parameter definitions rather than unrestricted string property bags.

---

# 13. Parameters Are Declared Per Consideration

Not every Option has another character as a target.

This must be structural.

Example:

```text
DO IT MYSELF
```

has no `Option.TargetCharacter`.

A Consideration declares only the parameters it needs.

Examples:

### PerceivedCapability

```text
required:
    actor
    task

optional:
    target
```

If `target` is absent:

```text
target = actor
```

may be an explicit binding policy for this Consideration.

### Cost

```text
required:
    actor
    optionCost
```

No target character exists.

### TravelBurden

```text
required:
    actor
    destination
```

### InterpersonalComfort

```text
required:
    actor
    target
```

For `Do It Myself`, this Consideration is simply Not Applicable.

The evaluator should never manufacture a fake target to satisfy a schema.

---

# 14. Generic Parameter Sources

Bindings may resolve from semantically typed sources such as:

```text
Decision.Actor
Decision.Participant(role)
Decision.Context.Task
Decision.Context.Location
Decision.Context.Amount
Decision.Context.Subject

Option.TargetCharacter
Option.Destination
Option.Item
Option.Job
Option.Cost
Option.Duration
Option.RiskContext
Option.Subject

Actor.Self
```

The exact implementation may use typed unions, generic semantic values, or generated binding code.

The requirement is:

> **Bindings are explicit, validated, deterministic, and not target-character-specific.**

Invalid required bindings should fail content validation before gameplay where possible.

---

# 15. Candidate Reasons and Reason Channels

A Consideration does **not** immediately create a die.

It emits a Candidate Reason.

Conceptually:

```text
CandidateReason
├─ OptionId
├─ ConsiderationBindingId
├─ ReasonChannelId
├─ SignedExpectedScore
├─ Uncertainty
├─ CalibratedStrength
├─ PresentationSemantic
├─ SourceSignalKeys[]
├─ RelationMetadata
└─ ProvenanceHandle
```

`ReasonChannelId` identifies the human-scale meaning represented by the Candidate Reason.

Examples:

```text
capability
comfort
obligation
reciprocity
risk
cost
loyalty
value_alignment
time
status
```

This exists to prevent multiple mathematically distinct calculations from accidentally creating multiple dice for one underlying human reason.

---

# 16. Correlated-Reason Safeguard

The Considerations layer was introduced partly to avoid:

```text
He's insensitive       d4
He's pushy             d6
He embarrassed me      d4
He's my boss           d4
I hate pushy people    d6
```

when the actual Decision-level reason is:

```text
He makes me uncomfortable    d8
```

The same problem can recur between Considerations.

Example:

```text
PerceivedCapability
UncertaintyAboutCapability
```

may both read the same upstream capability belief.

If both independently become dice, the system can double-count one signal.

Therefore:

> **By default, one Option should produce at most one final Influence per ReasonChannel.**

Candidate Reasons in the same channel must be consolidated.

---

# 17. Consideration Relations

Some Candidate Reasons legitimately interact.

Definitions/bindings should be able to declare relationships such as:

```text
MutuallyExclusive
Supersedes
Merge
AllowStacking
```

These are conceptual policies.

Examples:

### Supersedes

```text
Severe safety uncertainty
supersedes
generic capability confidence
```

### Merge

```text
Several sources of interpersonal discomfort
merge into
social.comfort
```

### AllowStacking

Rarely:

```text
He is incapable of the task       d10
Trying could kill someone         d12
```

Both may derive partly from the same underlying technical facts, but they are genuinely distinct reasons.

Explicit stacking should be exceptional and author-visible.

---

# 18. Structural Authoring Validation

Content validation should warn when:

- two Consideration bindings emit the same ReasonChannel without a merge/supersede policy;
- multiple final channels read substantially the same declared SourceSignalKeys with no explicit stacking relationship;
- a required parameter cannot resolve for an eligible Option kind;
- a target-required Consideration is bound to a targetless Option;
- a Decision-local Consideration duplicates an existing reusable definition without justification;
- a Consideration's output can never pass the minimum Influence threshold;
- incompatible calibration profiles are mixed accidentally.

This does not require automatically proving statistical correlation.

Lightweight semantic metadata is sufficient to catch the common failure modes.

---

# 19. Reason Consolidation

After all eligible Considerations evaluate:

```text
Candidate Reasons
        ↓
group by Option + ReasonChannel
        ↓
apply relation policy
        ↓
consolidated reason
        ↓
Influence threshold/calibration
        ↓
DecisionInfluence
```

The consolidation step is deterministic.

Possible channel-level strategies may include:

```text
strongest applicable
field-based merge
superseding priority
explicit weighted merge
```

Do not silently sum correlated magnitudes.

The strategy belongs to the ReasonChannel/Decision definition and must be explainable.

---

# 20. Shared Calibration and Dice Mapping

A SignalField returns a bounded normalized result.

A Consideration then receives a shared calibrated strength.

Conceptually:

```text
bounded result
    ↓
Field Calibration
    ↓
Normalized Reason Strength
    ↓
Decision Influence Scale
    ↓
Die / magnitude
```

The Social Model and Considerations may reuse generic field-calibration mechanics.

The Decision system owns the mapping from a normalized Reason Strength to its gameplay representation.

Illustrative only:

| Absolute strength | Influence |
|---:|---|
| below threshold | omit |
| mild | d4 |
| moderate | d6 |
| strong | d8 |
| very strong | d10 |
| extreme | d12 |

The exact thresholds are a game-balance decision.

The important rule is:

> **Die size comes from a deterministic shared scale, not from a bespoke lookup for each Consideration.**

---

# 21. Sign and Option Semantics

A Consideration result is evaluated **for an Option**.

Therefore it does not need:

```text
favors: Ask
```

A signed result means:

```text
positive
    → supports this Option

negative
    → opposes this Option
```

The Decision renderer/resolution rules determine how supporting and opposing Influences participate in the eventual dice mechanic.

For targetless or abstention Options:

```text
Do It Myself
Wait
Decline All
```

the exact same rule applies.

---

# 22. Worked Example: Choosing Whom to Ask

Decision:

```text
WHO SHOULD MIRA ASK TO REPAIR THE GENERATOR?

A. Ask Darius
B. Ask Glen
C. Ask Priya
D. Do It Myself
```

Bindings:

```text
PerceivedCapability
    actor  = Mira
    target = Option.TargetCharacter? ?? Mira
    task   = GeneratorRepair

InterpersonalComfort
    actor  = Mira
    target = Option.TargetCharacter
    only when target exists

PriorReciprocity
    actor  = Mira
    target = Option.TargetCharacter
    only when target exists

ObligationAversion
    actor  = Mira
    target = Option.TargetCharacter
    only when target exists

SelfReliance
    actor  = Mira
    active for DoItMyself
```

Possible Candidate Reasons:

```text
ASK DARIUS
capability       +0.82
comfort          -0.41
reciprocity      +0.55
obligation       -0.62

ASK GLEN
capability       +0.18, high uncertainty
comfort          +0.71
reciprocity      +0.39
obligation       -0.12

ASK PRIYA
capability       +0.64
comfort          -0.08
reciprocity      +0.02

DO IT MYSELF
capability       +0.32
self_reliance    +0.69
risk             -0.48
```

After calibration/consolidation:

```text
ASK DARIUS

He's excellent at this             d10
He has helped me before             d8
He makes me uncomfortable           d6
I don't want to owe him             d8


ASK GLEN

I trust him                         d8
I'm not sure he can handle this     d6


ASK PRIYA

She's very capable                  d8
We've barely worked together        d4


DO IT MYSELF

I hate asking for help              d8
I might be able to manage it        d6
This could go badly                 d6
```

No character-pair-specific lookup table was authored.

---

# 23. Where Signals Come From

Considerations can consume signals from many authoritative systems.

Examples:

```text
Knowledge
Social Appraisals
Dyadic Relationship State
Needs / Affect
Values
Interests
Skills / Capability
Activities
Commitments
Employment
Economy
Location / Travel
History
Groups
Current Decision Context
Option Properties
```

The Decision system should not hard-wire directly to every source system.

It asks for named semantic signals.

---

# 24. Signal Providers and Fact Providers

Vivarium already has the `FactProvider` pattern for systematically exposing discoverable truth to Knowledge.

The Decision system should **not** create a completely separate provider-registration ecosystem that independently walks all Domain aggregates.

However, Facts and Signals are not identical:

```text
Fact
    → a discoverable claim about truth

Signal
    → a normalized/uncertain input used by an evaluator
```

Some Facts can directly become Signals.

Some Signals are derived from multiple Facts or from non-discoverable runtime state.

Therefore the recommended architecture is:

```text
Shared Semantic Source Registry
        ├─ FactProvider capability
        └─ SignalProvider capability
```

or an equivalent shared provider family.

The important invariants are:

1. Domain ownership/access paths are registered once.
2. Fact discovery and Signal resolution reuse the same indexing/lifecycle infrastructure where practical.
3. Adding a new system should not require two unrelated global provider registries that can drift apart.
4. A provider does not need to expose both capabilities.

The exact interface names remain implementation details.

---

# 25. Knowledge-Sensitive Signals

A character Decision must generally use the **actor's beliefs**, not omniscient World Truth.

Example:

```text
PerceivedCapability(Mira, Darius, GeneratorRepair)
```

queries what Mira knows/believes about:

- Darius's relevant skill;
- Darius's observed task performance;
- potentially relevant Social Appraisal signals such as Stability/Respect.

It should not silently use Darius's true Skill value if Mira has no way to know it.

A Signal Provider must therefore declare whether a signal is:

```text
Truth-relative
Observer-belief-relative
Option-intrinsic
Actor-internal
Derived appraisal
```

The ConsiderationBinding determines the relevant observer.

This preserves Vivarium's Truth / Knowledge separation.

---

# 26. Unknown and Uncertain Are Gameplay Inputs

A missing/uncertain signal must not automatically become:

```text
0.0
```

because zero may mean actual neutrality.

A signal requirement can return:

```text
Known
Uncertain
Unknown
NotApplicable
```

Conceptually.

A Consideration decides how those states matter.

Examples:

```text
Unknown capability
    → meaningful safety concern

Unknown hobby preference
    → perhaps no Influence

NotApplicable target comfort
    → no Candidate Reason for DoItMyself
```

This prevents ignorance from being conflated with a neutral fact.

---

# 27. Presentation Semantics

Consideration computation and prose presentation are separate.

A Candidate/Consolidated Reason carries semantic presentation information such as:

```text
ReasonSemanticId
Sign
StrengthBand
CertaintyBand
TargetRef?
ContextTags[]
```

Presentation templates may distinguish:

```text
Positive / Mild
Positive / Strong
Negative / Mild
Negative / Strong
Uncertain / Mild
Uncertain / Strong
```

Example:

```text
Mild negative comfort:
"He's a little awkward to ask."

Strong negative comfort:
"He makes me uncomfortable."

High capability / high uncertainty:
"I think he can do this."

Moderate capability / very high uncertainty:
"I'm not sure he can handle this."
```

The exact player-facing wording may be generalized or hidden according to Player Knowledge.

The semantic Influence remains stable underneath.

---

# 28. True Influence vs Player-Facing Influence

The Decision's true Influence set is constructed from the actor's state and Knowledge.

The player's Knowledge controls what the player can see about that true reason.

Example true Influence:

```text
Reason:
social.obligation
Strength:
Strong
Underlying explanation:
Mira values independence + Darius is supervisor + existing debt
```

Possible player-facing views:

```text
I don't want to owe him      d8

Social concern               d8

Personal concern             d8

???                          d8

???

[hidden entirely]
```

This preserves the frozen architecture's Influence Presentation rules.

---

# 29. Dynamic Reevaluation

Active Decisions are living state.

If relevant world/belief conditions change, affected Considerations reevaluate.

Example:

```text
Mira considers asking Darius for help.

10:00:
Comfort -0.55

10:05:
Darius apologizes sincerely.

Knowledge + dyadic history change.

Relevant dependencies invalidate.

10:05:
Comfort -0.31
```

The pipeline is:

```text
Dependency changes
      ↓
DecisionDependencyIndex identifies affected Decisions
      ↓
affected Consideration bindings reevaluate
      ↓
Candidate Reasons update
      ↓
Reason consolidation updates
      ↓
DecisionInfluence set/magnitudes update
      ↓
InfluenceRevision increments
```

Unrelated Considerations should not be recomputed.

---

# 30. Consideration Dependencies

Each Consideration binding should expose/register the semantic dependencies that can change its result.

Examples:

```text
PerceivedCapability
    Knowledge(Mira, Darius, skill.repair)
    Knowledge(Mira, Darius, social.stability)
    Task(GeneratorRepair)

InterpersonalComfort
    SocialAppraisal(Mira, Darius, comfort)
    DyadicHistory(Mira, Darius)
    Hierarchy(Mira, Darius)

TravelBurden
    TravelNetworkRevision
    ActorActivity/Location
    OptionDestination
```

Dependencies should use existing aspect-scoped revision/indexing rules.

Do not poll all active Decisions on every world change.

---

# 31. Stable Influence Identity Across Reevaluation

Player interventions must remain bound to the intended reason even when magnitude changes.

A useful semantic key is conceptually:

```text
InfluenceSemanticKey
├─ OptionId
├─ ReasonChannelId
└─ optional BindingIdentity
```

When the same consolidated reason persists through reevaluation:

```text
He makes me uncomfortable d8
        ↓
He makes me uncomfortable d6
```

its runtime `DecisionInfluenceId` should remain stable.

If the reason disappears and later genuinely reappears, exact identity policy may depend on Decision lifecycle rules, but collection position must never determine identity.

---

# 32. Player Intervention Applies After Reason Construction

The player may:

- add/remove/step a die;
- reroll;
- replace;
- apply another content-defined intervention.

Those operations act on the resulting stable `DecisionInfluenceId`.

The player does not directly edit:

```text
Mira's Comfort field
Mira's Knowledge
ObligationAversion coefficients
```

unless an intervention explicitly represents an actual world-changing action.

This keeps:

```text
changing circumstances
```

distinct from:

```text
temporarily influencing the Decision mechanic
```

---

# 33. Provenance Has Two Tiers

Full explanation chains are valuable but potentially expensive.

Do not eagerly materialize a deep provenance tree for every autonomous Decision across thousands of characters.

Use two tiers.

## 33.1 Runtime compact result

During routine simulation, retain only what authoritative continuation needs.

Conceptually:

```text
DecisionInfluence
├─ DecisionInfluenceId
├─ OptionId
├─ ReasonChannelId
├─ ConsiderationId
├─ SignedMagnitude
├─ Die / gameplay magnitude
├─ PresentationSemantic
├─ EvaluationRevision
└─ ProvenanceHandle
```

## 33.2 Lazy explanation

Detailed explanation is built when:

- player inspects the Decision;
- developer tracing is enabled;
- a Decision becomes Significant;
- a history view requires explanation.

The evaluator can then render:

```text
"I don't want to owe him" d8
        ↓
ObligationAversion = .67
        ↓
Independence = .84
ExpectedReciprocity = .61
ExistingObligation = .32
PowerImbalance = .55
        ↓
source Knowledge / relationship / context
```

---

# 34. Historical Explanation Must Describe the Historical Evaluation

Lazy provenance must **not** recompute an old Decision from the current world.

If Mira resolved the Decision at 10:00 and someone inspects it at 17:00, the explanation must describe 10:00.

Therefore a resolved Decision needs a compact evaluation snapshot.

Conceptually:

```text
ConsiderationEvaluationSnapshot
├─ ConsiderationDefinitionId
├─ DefinitionVersion
├─ BindingId
├─ BoundParameterRefs
├─ InputSignalMeans
├─ InputSignalUncertainty
├─ Relevant SourceRevisions
├─ ExpectedScore
├─ OutputVariance
├─ StrengthBand
├─ ReasonChannelId
└─ ContributionSummary
```

This is the **world-drift sibling** of the architecture's existing rule that in-flight entities snapshot definition-derived values against content hot reload.

The principle is:

> **Time moving forward must not rewrite why a past authoritative outcome happened.**

---

# 35. Snapshot Retention Follows Decision Retention

Evaluation snapshots must not create an unbounded hidden history.

They inherit the lifecycle of the Decision/history record they explain.

Conceptually:

```text
Ephemeral Decision
    → snapshot prunes with Decision

Recent Decision
    → compact explanation retained

Significant Decision
    → richer explanation may remain

Legacy Decision/Event
    → explanation compacts to durable summary
```

There is no independent forever-growing Consideration snapshot collection.

---

# 36. Core Architecture Invariant to Add

The main Architecture Brief should eventually gain a sibling invariant to its in-flight definition snapshot rule:

> **Resolved authoritative outcomes retain sufficient historical evaluation data to explain themselves from the state/reasons that existed when they resolved; explanation must not be reconstructed from later mutable World state. This retained evaluation data follows the same retention/compaction lifecycle as the outcome it explains.**

This principle likely applies beyond Decisions.

Potential future consumers include:

- Activity outcomes;
- social events;
- important production outcomes;
- other resolved autonomous choices.

The Decision system is simply the first concrete use.

---

# 37. Consideration Library — Initial Categories

A first reusable library might include:

## Social

```text
PerceivedCapability
InterpersonalComfort
PriorReciprocity
Trust
Respect
Affection
Attraction
Fear
Resentment
Loyalty
ObligationAversion
StatusConcern
ReputationConcern
SharedIdentity
ValueAlignment
```

## Practical

```text
Cost
Reward
Risk
TravelTime
Convenience
Urgency
Availability
CommitmentConflict
ExpectedDuration
ResourceNeed
```

## Personal

```text
Duty
Independence
Pride
Ambition
Curiosity
Habit
Aversion
GoalAlignment
MoralAlignment
NeedSatisfaction
```

This is a design vocabulary, not a requirement to implement all of them immediately.

---

# 38. Considerations Are Not Necessarily Social

The abstraction must work for Decisions such as:

```text
WHICH APARTMENT SHOULD I RENT?

Rent cost
Commute
Space
Neighborhood familiarity
Distance from friends
Status
Risk
```

or:

```text
SHOULD I GO TO BED?

Fatigue
Current activity enjoyment
Morning commitment
Social pressure
Habit
```

The same reasoning pipeline should transform arbitrary simulation signals into compact option-relative reasons.

That is why the generic layer is `SignalField`, not `RelationshipConsideration`.

---

# 39. Decision Generation vs Decision Reasoning

These are distinct responsibilities.

## Decision Generation

Answers:

```text
Should a Decision exist?
What is the Decision about?
Which Options exist?
When does it resolve?
What conflict scope does it occupy?
```

## Decision Reasoning

Answers:

```text
For each Option, what reasons currently support or oppose it?
How strong are those reasons?
How uncertain are they?
Which correlated reasons should be consolidated?
What true Influences/dice result?
```

Do not combine these into one giant Decision service.

---

# 40. Decision Consequences Are Separate Again

Considerations explain why an Option is attractive or aversive.

They do not define what happens after that Option wins.

Conceptually:

```text
Generation
    ↓
Reasoning
    ↓
Resolution
    ↓
Consequence
```

A Decision Definition may reference all four pieces, but each responsibility remains distinct.

---

# 41. Suggested Data Shapes

Illustrative only:

```text
SignalKey
├─ SignalId
└─ ValueKind


SignalValue
├─ Mean
├─ Variance
├─ Applicability
├─ SourceRevision
└─ ProvenanceHandle?


SignalVector
├─ OrderedSignalKeys[]
├─ MeanVector
└─ Covariance


SignalFieldDefinition
├─ SignalFieldId
├─ Bias
├─ LinearTerms[]
├─ PairwiseTerms[]
├─ IdealPoint?
├─ IdealFactorL?
├─ ResponseFunction
├─ CalibrationProfileId
├─ FieldRevision
└─ ProvenanceMetadata


SignalFieldEvaluation
├─ ExpectedLatentScore
├─ LatentVariance
├─ ExpectedBoundedScore
├─ BoundedVariance
├─ StrengthBand
└─ ContributionHandle


ConsiderationDefinition
├─ ConsiderationId
├─ ParameterSchema
├─ SignalRequirements[]
├─ SignalFieldDefinitionId
├─ UncertaintyPolicy
├─ ReasonChannelId
├─ PresentationProfileId
├─ ApplicabilityRules
├─ DependencyDescriptor
├─ ReusabilityScope
└─ Version


ConsiderationBinding
├─ BindingId
├─ ConsiderationDefinitionId
├─ ParameterBindings[]
├─ OptionFilter?
└─ RelationOverrides?


CandidateReason
├─ OptionId
├─ BindingId
├─ ReasonChannelId
├─ ExpectedScore
├─ Uncertainty
├─ StrengthBand
├─ SourceSignalKeys[]
├─ PresentationSemantic
└─ ProvenanceHandle


ReasonChannelDefinition
├─ ReasonChannelId
├─ ConsolidationPolicy
├─ DefaultRelationRules
├─ InfluenceScaleProfileId
└─ PresentationCategory


DecisionInfluence
├─ DecisionInfluenceId
├─ OptionId
├─ ReasonChannelId
├─ SignedMagnitude
├─ GameplayMagnitude
├─ PresentationSemantic
├─ EvaluationRevision
└─ ProvenanceHandle


ConsiderationEvaluationSnapshot
├─ DecisionId
├─ OptionId
├─ BindingId
├─ DefinitionVersion
├─ BoundParameterRefs
├─ InputSignalSnapshot
├─ EvaluationResult
├─ ContributionSummary
└─ RetentionClass
```

These shapes should remain content- and implementation-flexible.

---

# 42. Module Boundaries

A likely Domain structure:

```text
Domain/
├─ Evaluation/
│   ├─ Signals/
│   ├─ SignalFields/
│   ├─ Calibration/
│   └─ ContributionTracing/
│
├─ Decisions/
│   ├─ Definitions/
│   ├─ Generation/
│   ├─ Considerations/
│   ├─ ReasonConsolidation/
│   ├─ Influences/
│   ├─ Resolution/
│   └─ Consequences/
│
├─ Knowledge/
├─ Relationships/
├─ Characters/
├─ Activities/
└─ ...
```

The exact folders are not architectural requirements.

The dependency direction is.

`Evaluation/SignalFields` should know nothing about Decisions or Relationships.

Decisions may consume generic evaluation results.

Relationships/Social Appraisals may consume the same generic evaluator.

---

# 43. Authoring Example

```text
DecisionDefinition:
social.request_help

Context schema:
    RequestedTask

Options:
    CandidatePeople
    Self

Bindings:

- perceived_capability
    actor  = Decision.Actor
    target = Option.TargetCharacter? ?? Decision.Actor
    task   = Decision.Context.RequestedTask

- interpersonal_comfort
    actor  = Decision.Actor
    target = Option.TargetCharacter
    when   = Option.HasTargetCharacter

- prior_reciprocity
    actor  = Decision.Actor
    target = Option.TargetCharacter
    when   = Option.HasTargetCharacter

- obligation_aversion
    actor  = Decision.Actor
    target = Option.TargetCharacter
    when   = Option.HasTargetCharacter

- self_reliance
    actor  = Decision.Actor
    when   = Option.Kind == Self
```

The actual authoring format might be ScriptableObjects, JSON-like definitions, generated C#, or another content layer.

The semantic structure should remain equivalent.

---

# 44. Explainability Example

Player inspects:

```text
I don't want to owe him     d8
```

Top-line explanation:

```text
Asking Darius would create an obligation Mira strongly dislikes.
```

Expanded explanation:

```text
ObligationAversion
    Expected score: Strong negative

Major contributors:
    Mira strongly values independence.
    Mira already feels somewhat indebted to Darius.
    Darius is her supervisor.
    Mira believes Darius tends to expect reciprocity.

Interaction:
    Independence × PowerImbalance increased the pressure.

Uncertainty:
    Mira is moderately uncertain about Darius's reciprocity expectations.
```

Deep debug trace may additionally show:

```text
signal means
signal covariance
linear terms
pairwise terms
latent expected score
latent variance
bounded score
bounded variance
calibration thresholds
definition versions
source revisions
```

The player should not need to see mathematical internals unless a debug mode exposes them.

---

# 45. Testing Strategy

## 45.1 SignalField unit tests

Verify:

- linear evaluation;
- pairwise terms;
- ideal-point terms;
- covariance effects;
- exact pre-bounded expectation;
- variance calculation;
- bounded approximation;
- deterministic ordering.

## 45.2 Parameter binding tests

Verify:

- target-relative Options;
- targetless Options;
- self Options;
- task-specific bindings;
- missing required parameters;
- optional parameters;
- invalid content caught before runtime.

## 45.3 Consolidation tests

Verify:

- two Comfort candidates merge;
- superseding uncertainty prevents duplicate capability dice;
- explicit stacking works;
- same-channel duplicate warnings occur;
- ordering cannot change outcome.

## 45.4 Knowledge tests

Verify:

- omniscient truth is not used accidentally;
- unknown differs from neutral;
- uncertainty survives Social Appraisal → Consideration;
- belief update targets relevant active Decisions.

## 45.5 Dynamic Decision tests

Verify:

- source world change updates only affected Considerations;
- magnitude changes preserve Influence identity;
- disappearing/reappearing reasons follow identity policy;
- interventions remain attached correctly.

## 45.6 Historical explanation tests

Verify:

- inspect at 17:00 explains the 10:00 evaluation;
- content hot reload does not rewrite old explanation;
- later Knowledge changes do not rewrite old explanation;
- pruning/compaction removes snapshots with their Decisions.

---

# 46. Golden Test Scenario

Use one scenario to exercise the whole reasoning pipeline.

Mira must decide who should repair a failed generator.

Candidates:

```text
Darius
Glen
Priya
Mira herself
```

World state contains:

- different repair skills;
- incomplete Knowledge of those skills;
- Social Appraisal results with uncertainty;
- different relationship histories;
- Mira's strong Independence value;
- Darius's supervisor relationship;
- current urgency/risk;
- travel/time differences.

The Decision should produce distinct reason sets for each Option.

Then:

1. Darius apologizes for a prior embarrassment.
2. Mira observes Glen successfully fix related equipment.
3. Priya becomes unavailable.
4. urgency increases.

Only the relevant Considerations should reevaluate.

The true Influence set should update deterministically.

Player-facing Knowledge may reveal only part of the reasoning.

Save/reload should preserve the same state and eventual resolution.

After resolution, later world changes must not alter the historical explanation.

---

# 47. Implementation Stages

## Stage 0 — Generic SignalField evaluator

Implement independently of Decisions.

Support:

- named ordered Signals;
- means/covariance;
- linear/pairwise/ideal terms;
- expected latent score;
- latent variance;
- bounded approximation;
- calibration;
- contribution tracing.

Use synthetic vectors.

## Stage 1 — Consideration evaluation

Implement:

```text
ConsiderationDefinition
ParameterSchema
ConsiderationBinding
Signal resolution
CandidateReason
```

Test target and targetless Options.

No real dice needed yet.

## Stage 2 — Reason consolidation

Implement:

```text
ReasonChannel
Merge / Supersede / Exclusive / Stack
Influence semantic identity
```

Generate clean final reason sets.

## Stage 3 — Decision Influence integration

Map calibrated reasons into actual `DecisionInfluence` objects and the game's initial die scale.

Integrate dynamic reevaluation and stable Influence identity.

## Stage 4 — Social uncertainty integration

Extend Social Appraisal output to include variance.

Feed Comfort/Respect/etc. into Considerations with uncertainty intact.

## Stage 5 — Knowledge/provider integration

Generalize/reuse semantic provider infrastructure.

Ensure observer-relative Signals use actor Knowledge.

## Stage 6 — Lazy historical provenance

Add compact evaluation snapshots and retention lifecycle.

Verify old decisions remain explainable.

## Stage 7 — Content authoring and linting

Add:

- binding validation;
- ReasonChannel collision warnings;
- correlated-source warnings;
- reusable/local Consideration tooling;
- contribution/debug visualizations.

---

# 48. Success Criteria

The Decision Reasoning model is successful when:

1. One reusable Consideration can operate across many unrelated Decision Definitions.
2. A Decision can contain any number of Options without binary-specific logic.
3. Targetless and self-referential Options work without fake characters.
4. Considerations consume actor Knowledge rather than omniscient truth where appropriate.
5. Upstream uncertainty survives into Consideration evaluation.
6. Uncertainty can matter differently in different Decisions.
7. Correlated reasons consolidate rather than automatically creating duplicate dice.
8. Explicitly distinct reasons may still stack when content says they should.
9. Final Influence magnitudes come from shared calibration rather than bespoke per-character tables.
10. Dynamic world changes reevaluate only affected Considerations.
11. Influence identity remains stable through magnitude changes.
12. Player interventions remain attached to the intended Influence.
13. Full provenance is lazy during ordinary simulation.
14. Resolved Decisions remain historically explainable after the world changes.
15. Evaluation snapshots prune/compact with Decision history.
16. Signal and Fact source infrastructure does not become two unrelated provider ecosystems.
17. The entire pipeline is deterministic and headless.
18. A designer can explain why each final die exists.

---

# 49. Failure Criteria

Reconsider the design if:

- most Decisions require bespoke character-pair rules;
- Considerations become giant procedural scripts rather than small semantic evaluators;
- every Decision invents new signals rather than reusing semantic ones;
- Social Appraisal and Considerations duplicate the same field math;
- uncertainty is routinely collapsed or ignored;
- the same upstream reason commonly produces several dice accidentally;
- ReasonChannels require arbitrary hand-tuning to suppress symptoms of bad modeling;
- multi-option Decisions require special-case branching;
- targetless Options become awkward;
- historical explanations change when current world state changes;
- provenance storage grows without retention policy;
- the Decision engine directly reaches into every Domain aggregate instead of resolving semantic signals;
- designers cannot predict or explain which state produced an Influence.

---

# 50. Architectural Invariants

1. A Decision reason is produced through a Consideration, not an exhaustive character-pair lookup table.
2. AppraisalFields and Considerations are distinct Domain concepts over one shared SignalField evaluation primitive.
3. SignalField evaluation is deterministic and preserves uncertainty.
4. Social Appraisal outputs consumed by Decisions expose both expected score and residual uncertainty.
5. Considerations are evaluated relative to Options, not hard-coded to favor named binary choices.
6. Considerations explicitly declare required/optional parameters.
7. Options may be target-relative, targetless, self-referential, practical, spatial, or otherwise non-social.
8. Missing/unknown Signals are distinct from neutral values and from Not Applicable.
9. Reusable Considerations are preferred, but Decision-local Considerations are allowed when reasoning genuinely does not generalize.
10. Considerations emit Candidate Reasons; they do not directly create dice.
11. Candidate Reasons carry a semantic ReasonChannel.
12. By default, an Option produces at most one final Influence per ReasonChannel.
13. Stacking correlated reasons requires explicit authoring intent.
14. Reason consolidation is deterministic and explainable.
15. Final Influence magnitude is produced through shared calibration/Decision scale rules, not bespoke pair-specific tables.
16. Actor-belief-relative reasoning uses the actor's Knowledge rather than omniscient Truth.
17. Fact and Signal resolution reuse a shared semantic-source/provider infrastructure where practical rather than independent global registries.
18. Dynamic Decision reevaluation is dependency-targeted, not globally polled.
19. Stable Influence identity survives magnitude/presentation changes while the semantic reason persists.
20. Player interventions target stable Influence identity after reason construction.
21. Full provenance is lazy by default.
22. Historical explanations use evaluation-time snapshots rather than later mutable world state.
23. Evaluation snapshots inherit the Decision's retention/compaction lifecycle.
24. Content hot reload cannot retroactively change in-flight or historical reasoning semantics.
25. The Decision Reasoning system remains pure C#, headless, scalable, and independent of Unity presentation.

---

# 51. Guiding Principle

Vivarium should not need to know:

> "When Mira asks Darius for help, add these four specific dice."

It should know reusable things:

```text
Mira values independence.
Mira believes Darius is capable.
Mira is uncomfortable around Darius.
Darius has helped Mira before.
Mira expects asking him to create obligation.
The task is urgent.
```

The Decision asks reusable semantic questions:

```text
Can this option do what I need?
How comfortable am I choosing it?
What do I owe?
What has happened before?
What does it cost?
What could go wrong?
```

The evaluator determines how strongly each reason applies.

The consolidator turns detailed truth into a small number of distinct human reasons.

The Decision system turns those reasons into uncertain gameplay.

The final design principle is:

> **World state provides evidence. Signals make it comparable. Considerations make it meaningful. Reason Channels keep it non-redundant. Influences make it playable. Dice keep the choice uncertain.**
