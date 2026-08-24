# Vivarium Social Model Brief

**Status:** Accepted design reference; production foundation implemented  
**Project:** Vivarium  
**Purpose:** Define a coherent, explainable, scalable social model built around a shared latent personality space, observer-specific belief distributions, and directional matrix-based appraisal fields. Prove the production model incrementally rather than building a temporary social-scoring system that will later be replaced.

Current implementation evidence and remaining thin seams are tracked in
[`../ImplementationStatus.md`](../ImplementationStatus.md). The staged plan below is retained as design
rationale, not current roadmap priority.

---

# 1. Executive Summary

Vivarium should not model a relationship as one scalar such as:

```text
Friendship = 72
Compatibility = 81%
```

The social system exists to support more complicated truths:

- Mira can admire Darius while disliking him.
- She can be attracted to qualities she also finds exhausting.
- She can trust his judgment while distrusting his motives.
- She can enjoy him socially while hating working beneath him.
- She can publicly behave as though she hates him while privately caring deeply about him.
- She can react to the Darius she *believes* exists rather than the Darius represented by ground truth.
- Shared interests, values, history, hierarchy, familiarity, current mood, audience, and circumstance can all matter without collapsing into one undifferentiated score.
- A character can change slowly over years while a current mood changes in minutes and a relationship changes over weeks or months.

The central thesis is:

> **A character exists in a compact latent social space. Another character does not perceive that point perfectly; they maintain an uncertain belief about where the target lies in that space. The observer evaluates that belief through one or more directional preference/appraisal fields represented by sparse matrices and response functions.**

The matrix is not an optional future replacement for a simpler social system.

It **is the production concept**.

Early prototypes should be deliberately restricted versions of the same model:

```text
latent personality vector
        ↓
observer belief distribution
        ↓
sparse appraisal field
        ↓
expected appraisal
        ↓
history + familiarity + context
        ↓
decision influences / behavior
```

The project should grow this formulation gradually:

```text
diagonal coefficients
    ↓
sparse pairwise interactions
    ↓
observer-specific uncertainty
    ↓
multiple appraisal lenses
    ↓
context-conditioned terms
    ↓
only if useful: richer nonlinear behavior
```

Nothing in the early testbed should be designed with the expectation that it will later be thrown away when "the real matrix system" arrives.

---

# 2. Canonical Social Pipeline

There should be **one canonical personality-evaluation pipeline**.

```text
TRUE PERSONALITY
Character B's latent vector
        │
        │ behavior / actions
        ▼
OBSERVATION
A sees evidence about B
        │
        ▼
KNOWLEDGE / BELIEF
A's probability distribution over B's latent state
        │
        ▼
SOCIAL APPRAISAL FIELD
A evaluates possible personalities through a matrix/field
        │
        ▼
EXPECTED PERSONALITY APPRAISAL
How A currently evaluates the person they think B is
        │
        ├──────── Values / Interests
        ├──────── Dyadic history
        ├──────── Familiarity / exposure
        ├──────── Current affect
        └──────── Situation / social context
                     │
                     ▼
              CURRENT SOCIAL PRESSURES
                     │
                     ▼
                 DECISIONS
```

This pipeline prevents several accidental duplicate systems.

In particular:

- named traits do **not** independently add social points after matrix evaluation;
- Perception is not a parallel replacement for Vivarium's Knowledge system;
- dyadic history is not encoded by distorting personality coordinates;
- current anger does not rewrite durable affection;
- familiarity is not the same thing as liking;
- "reputation" is evidence, not omniscient truth.

---

# 3. Core Design Goals

## 3.1 Directionality

`Mira → Darius` and `Darius → Mira` are different.

Their:

- beliefs,
- preferences,
- history,
- attraction,
- trust,
- resentment,
- familiarity,
- social context,

may all be asymmetric.

## 3.2 Contradictory simultaneous appraisals

The model must support cases such as:

- affection without respect,
- respect without affection,
- attraction without comfort,
- trust in judgment without trust in motives,
- dependence without liking,
- admiration mixed with resentment,
- familiarity mixed with irritation.

No universal "relationship score" should erase these distinctions.

## 3.3 Compact latent personality space

The simulation should store a small number of underlying social dimensions rather than dozens of unrelated named personality stats.

Named human traits are primarily:

- projections,
- regions,
- interpretations,
- authoring vocabulary,
- explanation vocabulary,

over the shared latent space.

## 3.4 Perception instead of omniscience

Characters should react to what they currently believe about one another.

A character's belief may be:

- incomplete,
- uncertain,
- stale,
- biased,
- mistaken,
- based partly on reputation rather than direct observation.

## 3.5 Explainability

A designer should be able to ask:

> Why does Mira currently respond positively to Darius?

and receive a useful causal trace.

The answer should resemble:

```text
+ Mira values assertive composure.
+ She currently believes Darius is highly assertive and composed.
- She is sensitive to high Agency paired with low Attunement.
? Her estimate of his Warmth is still uncertain.
+ They share a strong niche interest.
+ He helped her during an emergency.
- She is currently angry about a recent public embarrassment.
```

rather than:

```text
Compatibility = 0.6371
```

## 3.6 Similarity, complementarity, monotonic preference, and ideal ranges

Different dimensions may behave differently.

Examples:

- Warmth may often reward more Warmth.
- Some values may reward similarity.
- Agency may sometimes reward complementarity.
- Reliability may be mostly monotonic: more is usually preferred.
- Novelty-seeking may have an observer-specific ideal range.
- High Agency may be desirable only when Attunement is also high.

There is no universal "distance = compatibility" rule.

## 3.7 The social model must feed gameplay

The purpose of this model is not personality simulation for its own sake.

It should ultimately produce meaningful pressures such as:

```text
ASK DARIUS FOR HELP?

He's capable under pressure      d10
He has helped me before           d8
He makes me uncomfortable         d6
I don't want to owe him           d8
```

---

# 4. Non-Goals

The social model does not initially attempt to provide:

- clinically accurate psychology,
- a simulation of every named personality theory,
- recursive unlimited theory-of-mind reasoning,
- natural-language reasoning,
- a handcrafted rule for every social trope,
- a perception record for every possible character pair,
- a giant fixed list of personality facets,
- one "AI agent" object responsible for all social behavior,
- stochastic black-box behavior that cannot be reproduced or explained,
- a matrix whose complexity exists only because the mathematics is interesting.

The model is successful when it creates believable pressures and histories that support emergent gameplay.

---

# 5. Shared Latent Personality Space

Begin with a provisional compact vector:

| Dimension | Low end | High end | Broad meaning |
|---|---|---|---|
| **Warmth** | Detached | Affectionate | tendency toward care, friendliness, positive regard |
| **Agency** | Yielding | Assertive | tendency to initiate, lead, push, impose direction |
| **Stability** | Reactive | Composed | emotional steadiness under stress |
| **Sociability** | Private | Gregarious | appetite for social engagement |
| **Openness** | Conventional | Novelty-seeking | comfort with novelty and experimentation |
| **Discipline** | Spontaneous | Structured | persistence, planning, reliability, constraint |
| **Attunement** | Oblivious | Perceptive | sensitivity to others' emotions and social signals |

Initial range:

```text
-1.0 ───────── 0.0 ───────── +1.0
```

Seven dimensions are a hypothesis, not doctrine.

The test corpus should reveal whether dimensions should be:

- renamed,
- merged,
- split,
- removed,
- replaced.

A new primitive should be added only when several meaningful failures indicate that the existing latent space cannot represent an important distinction.

---

# 6. Named Traits Are Views of the Space

Traits such as:

- Confident,
- Overbearing,
- Charismatic,
- Empathetic,
- Dependable,
- Rigid,
- Reckless,
- Shy,
- Aloof,
- Intimidating,

should **not** become a second personality state layered beside the primitive vector.

They are named ways of describing regions or projections of the same space.

A simple trait may begin as:

\[
T_k(x)=b_k+w_k^Tx
\]

A trait whose meaning genuinely depends on combinations may use:

\[
T_k(x)=b_k+w_k^Tx+x^TQ_kx
\]

Examples:

```text
Confident
    Agency          +
    Stability       +
    Agency×Stability +

Overbearing
    Agency          +
    Attunement      -
    Agency×Attunement interaction

Empathetic
    Warmth          +
    Attunement      +
```

The exact formulas remain experimental.

## 6.1 Traits serve three jobs

Named traits are useful for:

1. **Authoring** — designers can say "this behavior displays Confidence."
2. **Explanation** — UI/debug tools can say "Mira perceives Darius as Confident."
3. **Preference templates** — content may say "Mira tends to like Confident people."

But the third case must compile into the canonical appraisal field.

The evaluator must **not** do:

```text
Matrix appraisal
+ Confidence bonus
- Overbearing penalty
```

if Confidence and Overbearing already derive from the same primitive coordinates.

That would double-count the same information.

## 6.2 Trait-language compilation

A useful content workflow may be:

```text
Authoring:
Mira likes Confident: Strong
Mira dislikes Overbearing: Strong

        ↓ compile / expand

Underlying field coefficients:
Agency
Stability
Attunement
Agency×Stability
Agency×Attunement
...
```

The compiled matrix/field is authoritative for evaluation.

The named declarations remain available as provenance so the result is understandable.

---

# 7. Generalized Knowledge: Player and Character Belief Use the Same Primitive

Vivarium's core architecture already needs a Knowledge system representing:

> an observer's possibly stale, uncertain, or incorrect belief about truth.

Social perception is the same primitive.

The social model should therefore extend or reuse Knowledge rather than creating a separate `PerceptionProfile` subsystem.

Conceptually:

```text
KnowledgeLedger
├─ ObserverRef
│   ├─ Player
│   └─ CharacterId
└─ KnowledgeEntries[]
```

A personality belief may use facts such as:

```text
FactKey:
Character.Personality.Agency(Darius)
```

but a social observer needs more than a single confidence scalar if the matrix is going to reason about uncertainty correctly.

For the social latent vector, the useful conceptual model is:

```text
BeliefDistribution
├─ MeanVector μ
└─ Covariance Σ
```

The first testbed may simplify `Σ` to independent per-axis variances.

The production representation should remain capable of supporting covariance if correlated uncertainty proves useful.

## 7.1 Unknown is distinct from neutral

The model must distinguish:

```text
Mira believes Darius has average Warmth.
```

from:

```text
Mira has almost no evidence about Darius's Warmth.
```

These should not evaluate identically.

## 7.2 Sparse belief edges

Vivarium must not maintain a full `N × N` matrix of social beliefs for a population of thousands.

A character-to-character belief edge exists only when there is a reason for it:

- direct interaction,
- meaningful co-presence,
- shared work/group context,
- reputation/hearsay,
- existing relationship/history,
- current gameplay relevance.

Inactive or low-value beliefs can follow the broader Knowledge/history retention policy.

Historical identity remains referential even if the active belief edge is later compacted.

---

# 8. Observation and Evidence

An observed action should be treated as a **noisy measurement of latent personality**, not a direct affection adjustment.

Example:

```text
Darius calmly takes control during an equipment failure.
```

This observation might be authored using readable evidence labels:

```text
Displays:
    Confident       Strong
    Dependable      Moderate
```

Those labels correspond to likelihood information over latent space.

Conceptually:

\[
p(x \mid observation) \propto p(observation \mid x)\,p(x)
\]

The observer's belief cloud moves or narrows according to the evidence.

## 8.1 Avoid naive additive evidence

Do not independently apply:

```text
Agency += .2
Stability += .2
Discipline += .1
```

for every derived-trait observation if those measurements are correlated.

That risks repeatedly counting the same latent signal.

The preferred conceptual update is joint:

```text
prior latent belief
        +
measurement model
        ↓
posterior mean/covariance
```

An early implementation may use a small Gaussian/Kalman-style approximation.

Full Bayesian inference is not a requirement.

The important requirement is that evidence is interpreted as information about the **joint latent state**.

## 8.2 Witness detection reuses social/spatial context

`ObservedAction.Witnesses[]` should not be populated by a second global scanning system.

Witness candidates should come from the same bounded context/indexing machinery used by interactions:

- shared location,
- shared activity,
- travel context,
- group/event context,
- line-of-observation policy where relevant.

Observation itself may still depend on:

- attention,
- salience,
- distraction,
- relationship relevance,
- action visibility.

## 8.3 Interpretation biases may eventually be character-specific

Later, two observers may update differently from the same action because of:

- prior beliefs,
- stereotypes/reputation,
- first-impression bias,
- trust in the information source,
- attentiveness,
- emotional state.

The first testbed may use a shared measurement model.

---

# 9. Canonical Social Appraisal Field

The core matrix should be understood as a **bounded appraisal field over latent personality space**, not necessarily as a probability density.

For observer `A`, appraisal lens `L`, target personality point `x`, and current context `c`:

\[
U_{A,L}(x,c)
\]

means approximately:

> How positively or negatively does A evaluate a person at point x for appraisal L under context c?

A useful experimental family begins with an **unbounded latent score**:

\[
s_{A,L}(x,c)=
b(c)
+w(c)^Tx
+x^TQ(c)x
-\frac12(x-i(c))^TP(c)(x-i(c))
\]

and then applies one shared bounded response:

\[
U_{A,L}(x,c)=g(s_{A,L}(x,c))
\]

where:

- `b(c)` is the context-conditioned baseline bias;
- `w(c)` contains context-conditioned directional preferences;
- `Q(c)` contains context-conditioned signed pairwise interaction terms;
- `i(c)` is an optional context-conditioned ideal/reference point;
- `P(c) = L(c)L(c)^T` is positive-semidefinite and describes an ellipsoidal ideal/tolerance region;
- `g` is a bounded response function such as `tanh` or logistic.

`Q` should be treated as symmetric for evaluation; only its symmetric component contributes to `x^TQx`.

This is a **candidate formulation**, not a frozen equation.

Its job is to keep several useful mechanisms mathematically distinct while still composing them into one canonical field.

## 9.1 Two kinds of context

The model should not hide every situational effect inside one generic `C(c,x)` term.

There are two different mechanisms.

### Personality-conditioned context

Some contexts change **how personality is evaluated**.

Examples:

- high Sociability may be attractive at a party but exhausting in a quiet one-on-one setting;
- high Agency may feel reassuring during a crisis but intrusive during collaborative work;
- hierarchy may make low Attunement more damaging to Comfort when the target is the observer's boss.

These effects should modify the field itself:

```text
b(c)
w(c)
Q(c)
i(c)
P(c)
```

The exact implementation may use sparse coefficient deltas rather than materializing a separate complete field for every context.

### Independent situational pressure

Other effects matter socially but are not evaluations of the target's personality.

Examples:

- Mira is exhausted;
- Mira is already angry from an unrelated event;
- the decision is urgent;
- refusing help would create a scheduling problem.

These should remain **outside the personality appraisal field** as Current-Affect, Context, Activity, or Decision influences.

This separation preserves both mathematical meaning and explanation.

## 9.2 Why this resolves the previous inconsistency

The ideal-point term:

\[
-\frac12(x-i)^TP(x-i)
\]

uses a PSD matrix and therefore retains the clean ellipsoid/shape interpretation.

The signed interaction matrix:

\[
x^TQx
\]

is free to express:

```text
Agency high + Attunement high
    → positive

Agency high + Attunement low
    → negative
```

without pretending `Q` is a Gaussian precision matrix.

Both terms belong to the **same appraisal field**.

Named traits are projections/explanations of that field, not an additional score layered afterward.

---

# 10. Geometric Interpretation: Preference Shapes

The original design intuition remains central.

A character's appraisal can be visualized as a field or shape in personality space.

For an ideal-point component:

- the **center** represents a preferred region;
- the **width** along an axis represents tolerance or sensitivity;
- covariance/off-axis structure creates rotated ellipsoids;
- signed interaction terms bend or reshape the field beyond a simple ellipsoid;
- the bounded response function gives a stable output range.

In two or three dimensions this can be visualized directly.

In seven dimensions it is the same conceptual object.

Debug tooling should eventually expose 2D slices through this space.

---

# 11. Perceived Target as a Point or Uncertainty Cloud

Mira does not evaluate Darius's ground-truth vector directly.

She evaluates a probability distribution:

\[
p_A(B=x)
\]

Early in a relationship, Darius may occupy a broad uncertain region in Mira's model.

With repeated useful observations:

- the mean can move;
- uncertainty can narrow;
- contradictory evidence can broaden it;
- context-specific observations may leave some dimensions uncertain.

The mathematically exact target quantity would be:

\[
E[U_{A,L}(B)] =
E[g(s_{A,L}(B,c))]
\]

For a nonlinear bounded `g`, that expectation generally has no simple closed form.

The Stage-1 evaluator should therefore use a specific deterministic approximation rather than leaving integration strategy vague.

For:

\[
x \sim \mathcal N(\mu,\Sigma)
\]

the **pre-`g` score** has an exact closed-form expectation:

\[
E[s(x,c)] =
b(c)
+w(c)^T\mu
+\mu^TQ(c)\mu
+\operatorname{tr}(Q(c)\Sigma)
-\frac12
\left[
(\mu-i(c))^TP(c)(\mu-i(c))
+\operatorname{tr}(P(c)\Sigma)
\right]
\]

The initial bounded appraisal is then:

\[
\widehat{E[U]} =
g(E[s])
\]

This is a **plug-in approximation**:

```text
exact expected latent score
        ↓
apply bounded response once
        ↓
approximate expected appraisal
```

It is exact when `g` is linear and should be adequate for the first testbed when uncertainty is not extreme relative to field curvature.

Importantly, uncertainty still affects the result through the trace terms:

```text
tr(QΣ)
tr(PΣ)
```

so a broad belief cloud does not collapse to merely evaluating the target at its mean point.

If later testing shows that `g(E[s])` is materially inaccurate for highly uncertain or highly curved cases, a tighter deterministic approximation may use closed-form score variance and a second-order delta-method correction. That is explicitly deferred until the simpler approximation fails a meaningful case.

The design meaning remains:

```text
A's appraisal field
        evaluated against
A's uncertain belief about B
        ↓
expected current appraisal
```

Mira can remain interested in Darius partly because uncertainty about where he lies in social space changes the expected field evaluation.

---

# 12. Multiple Appraisal Lenses Over One Shared Belief

A single universal compatibility field is probably insufficient.

Otherwise the system risks recreating Friendship Points using expensive math.

Instead, the same perceived target distribution can be evaluated through multiple appraisal lenses.

Candidate lenses might include:

- **Affiliation** — do I enjoy / seek this person's company?
- **Respect** — do I admire or value their capability/character?
- **Comfort / Safety** — do I feel at ease or willing to be vulnerable?
- **Attraction** — am I personally/romantically drawn toward them?
- **Reliance** — do I want to depend on them in practical contexts?

This list is provisional.

The point is structural:

```text
one belief cloud about Darius
        ├─ evaluated through Affiliation field
        ├─ evaluated through Respect field
        ├─ evaluated through Comfort field
        └─ evaluated through Attraction field
```

This supports:

- liking without respect,
- attraction without comfort,
- respect without affection,
- reliance without friendship.

The test corpus should determine the smallest useful set of appraisal lenses.

## 12.1 Shared calibration across lenses

All appraisal lenses must share a **common output calibration contract** before their results become gameplay magnitudes.

A bounded field output is not automatically balanced merely because every lens returns a value in the same numeric range. Different coefficient distributions could make one lens saturate constantly while another rarely leaves the center.

Therefore:

1. every lens produces the same normalized appraisal range;
2. a shared calibration policy converts normalized magnitude into a gameplay-strength band;
3. Decision influence magnitude is derived from that shared band rather than from lens-specific arbitrary thresholds;
4. test tooling should report each lens's output distribution across a representative population so systematic "loud" or "quiet" lenses are visible.

Conceptually:

```text
raw lens field
    ↓
bounded score
    ↓
shared appraisal-strength calibration
    ↓
Minor / Moderate / Strong / Extreme
    ↓
Decision system maps strength to die/magnitude
```

The exact die mapping belongs to the Decision system and remains designable.

The social model's responsibility is to provide **comparable, calibrated strength**, plus provenance explaining which lens produced it.

---

# 13. Values and Interests Are Separate Spaces

Personality answers:

> How do you tend to behave?

Values answer:

> What matters to you?

Interests answer:

> What do you enjoy?

Do not distort the personality matrix to represent effects that belong elsewhere.

Examples:

```text
Antique-clock obsession
    → Interest

Strong family obligation
    → Value

Darius is my boss
    → Context / power relationship

Darius betrayed me
    → Dyadic history
```

Interests and values may eventually have their own similarity/difference calculations, but those should remain conceptually distinct from latent personality appraisal.

---

# 14. Dyadic Relationship State

Geometry describes baseline appraisal of the person someone believes the target to be.

It does not replace **what has happened between these two particular people**.

A directional dyadic state should eventually contain a deliberately small set of durable channels such as:

```text
Affection
Trust
Respect
Admiration
Comfort
Resentment
Obligation
Attraction
```

The exact set is not frozen.

The important requirement is that these channels remain capable of contradiction.

## 14.1 Salient memories

Meaningful events may add specific history effects:

```text
Darius defended Mira publicly.
Darius broke an important promise.
Mira embarrassed Darius.
They survived a crisis together.
```

These effects retain provenance.

## 14.2 Familiarity and accumulated exposure

Dyadic history also needs a channel for relationships built from thousands of small interactions with no memorable event.

Track something conceptually like:

```text
Exposure
Familiarity
```

fed by:

- repeated co-presence,
- repeated conversation,
- routine shared activity,
- frequent collaboration.

Familiarity is **not affection**.

Familiarity is also **not belief confidence**, although repeated interaction can increase both.

```text
Belief confidence / covariance
    → How certain am I about what this person is like?

Familiarity
    → How socially accustomed, practiced, and relationally embedded am I with this person?
```

A person can be highly familiar but still uncertain about a hidden aspect of someone's personality, or highly confident about a well-observed public trait while barely knowing the person socially.

Exposure events may feed both systems through separate effects; neither should be inferred mechanically from the other.

It may:

- reduce uncertainty,
- reduce social friction,
- create routine,
- increase salience,
- increase comfort,
- intensify irritation,
- make absence noticeable.

The relationship evaluator decides what familiarity means in a given appraisal/context.

---

# 15. Current Affect and Context

Durable relationship state must remain distinct from current emotional state.

A character can:

> love someone and be furious with them right now.

Current affect may include slowly/analytically evolving quantities such as:

- stress,
- arousal,
- irritation,
- fear,
- confidence,
- loneliness.

Where these behave like continuous processes, they should reuse Vivarium's general analytical-progression mechanics rather than inventing social-only ticking systems.

Context may include:

- current activity,
- workplace hierarchy,
- audience,
- group size,
- privacy,
- competition,
- recent argument,
- urgency,
- fatigue,
- location,
- current role,
- third-party presence.

Context may:

- **modify appraisal-field coefficients** when the context changes how personality is evaluated;
- alter which appraisal lens matters;
- add an **independent non-personality pressure** outside the field;
- change how a current Decision is constructed.

A context-dependent coefficient change should flow through the same closed-form latent-score expectation as the base field.

An independent pressure should not be smuggled into the field merely because it occurs at the same time.

Context should never rewrite the target's true personality.

---

# 16. Bounded Second-Order Social Belief

Some social cases require more than direct dyadic belief.

Examples:

- everyone dislikes the leader but assumes everyone else likes them;
- Mira likes a stranger because trusted Glen speaks highly of them;
- Darius has a strong reputation before Mira ever meets him.

Vivarium should support bounded social-knowledge concepts such as:

```text
Direct belief
Reported belief / reputation
Perceived group norm
```

These should use the generalized Knowledge system.

Example:

```text
Fact:
GroupOpinion(BakeryWorkers, Boss) = Positive

Observer:
Mira

Belief:
Strongly positive
```

while reality may be:

```text
Actual aggregate sentiment = Negative
```

This creates pluralistic ignorance without requiring recursive arbitrary structures such as:

```text
Mira believes Glen believes Priya believes...
```

Unlimited recursive theory of mind is explicitly out of scope unless gameplay later proves it necessary.

---

# 17. Personality and Preference Drift

Personality and preferences should be runtime state, not immutable authored facts.

However, they operate on different timescales.

Conceptually:

```text
True personality
    changes very slowly

Preference/appraisal fields
    change slowly to moderately

Dyadic relationship/history
    changes regularly

Current affect/context
    changes quickly
```

Possible long-term causes of personality/preference drift:

- age/life stage,
- repeated experiences,
- major events,
- cultural/group exposure,
- formative relationships,
- trauma or success,
- long-term roles and habits.

This mechanism should remain conservative.

Ordinary friendship drift should generally come from:

- reduced interaction,
- familiarity decay,
- changing routine,
- new social context,
- altered dyadic history,

rather than constantly mutating the underlying personality vector.

---

# 18. Population-Scale Preference Generation

The testbed should hand-author a few matrices because designers need to understand the behavior.

Production cannot hand-author appraisal fields for thousands of characters.

A later deterministic generator should create plausible fields from:

```text
Character personality
Values
Culture / social environment
Formative history
Role / life stage
Authored archetype tendencies
Deterministic individual variation
```

The generator should use psychologically informed priors where useful, but no universal rule such as:

> People always prefer people similar to themselves.

Different dimensions may use different priors:

- some similarity,
- some complementarity,
- some socially desirable high-end preference,
- some observer-specific ideals,
- some broad tolerance.

Generated fields should remain inspectable through the same explanation/provenance tools as hand-authored ones.

## 18.1 Culture is a named future dependency

`Culture / social environment` is intentionally not defined by this brief.

It is a future input to preference generation that may represent things such as:

- locally reinforced ideals,
- group norms,
- role expectations,
- learned social desirability,
- exposure-driven priors.

Before Stage 6 production generation is implemented, Culture should receive its own small design brief rather than becoming an unstructured bag of hidden modifiers inside the social generator.

The matrix testbed does not depend on a Culture system.

---

# 19. Scaling Rules

The social model must remain compatible with populations in the thousands.

## 19.1 No universal pair tables

Do not store social state for every possible `(A,B)` pair.

Create sparse edges only for socially meaningful pairs.

## 19.2 No global pair scanning

Interaction and witness candidates come from bounded shared contexts and indexes.

## 19.3 Belief retention is lifecycle-managed

Beliefs may transition conceptually through:

```text
Active
Recent
Significant
Legacy / compacted
```

using the same broader retention philosophy as Vivarium history.

## 19.4 Derived values should be computed or cached selectively

Do not materialize every named trait for every character toward every observer.

Compute/cache when:

- displayed,
- relevant to an active Decision,
- relevant to interaction candidate evaluation,
- needed for a test/debug trace.

## 19.5 Matrix storage should be sparse

A seven-dimensional quadratic interaction matrix is small already, but most meaningful character differences should not require every pairwise coefficient to be active.

Store/tune sparse interaction terms where possible.

---

# 20. Determinism and Reproducibility

The social model participates in Vivarium's existing determinism contract.

If any operation uses stochasticity, including:

- perception noise,
- action interpretation,
- procedural preference generation,
- rumor distortion,
- memory decay,
- observation selection,

it must use the shared deterministic RNG oracle with stable purpose IDs.

Do not use independent `System.Random` streams.

Given the same:

- authoritative state,
- observations,
- ordered commands/events,
- content version,
- simulation-rules version,
- seed,

the social model must produce the same resulting beliefs and evaluations.

Numerical evaluation policy is part of the determinism contract. The implementation should use a fixed matrix-operation order and a documented approximation strategy (`g(E[s])` in Stage 1) rather than switching opportunistically between sampling and analytical evaluation.

---

# 21. Matrix Testbed v0

The first executable testbed should use **the production concepts in deliberately constrained form**.

It should not implement a temporary friendship system.

## 21.1 v0 capabilities

### Latent personality

Seven provisional dimensions.

### Named trait projections

Approximately 20–30 trait definitions over the latent space.

Most can be linear at first.

A handful should deliberately include pairwise interactions.

### Sparse appraisal fields

At least two or three appraisal lenses should be tested.

Suggested starting lenses:

```text
Affiliation
Respect
Comfort
```

Attraction can be added if useful to the test cases.

Each field supports:

- linear coefficients,
- selected pairwise terms,
- optional ideal-point term;
- sparse context-conditioned coefficient deltas;
- exact Gaussian expectation of the pre-bounded latent score;
- the Stage-1 `g(E[s])` plug-in approximation for bounded appraisal.

### Belief distributions

Hand-authored observer→target mean vectors and uncertainty.

Begin with diagonal covariance if necessary.

### Evidence updates

10–20 observed actions with known measurement/evidence definitions.

### Interests and values

Simple tags/intensities.

### Dyadic history

A few durable channels plus explicit event modifiers.

### Familiarity

A simple exposure/familiarity quantity independent from affection.

### Explanation trace

Every result exposes meaningful contributing terms.

---

# 22. Hand-Authored Test Characters

These are intentionally exaggerated examples.

## Mira

```text
Warmth       +0.6
Agency       +0.2
Stability    +0.5
Sociability  +0.1
Openness     +0.4
Discipline   +0.8
Attunement   +0.7
```

Potential tendencies:

- appreciates composed assertiveness;
- strongly values reliability;
- dislikes Agency paired with low Attunement;
- values empathy;
- may prefer somewhat more Agency in others than she expresses herself.

## Darius

```text
Warmth       +0.2
Agency       +0.9
Stability    +0.8
Sociability  +0.5
Openness     +0.1
Discipline   +0.7
Attunement   -0.2
```

Likely human-readable projections:

```text
Confident       high
Dependable      high
Overbearing     high
Driven          high
Intimidating    moderate/high
```

Darius deliberately occupies a region Mira should evaluate differently across lenses.

She may:

- respect him,
- be drawn to his confidence,
- dislike being supervised by him,
- feel uncomfortable with his low Attunement.

## Glen

Suggested shape:

```text
Warmth       high
Agency       low
Attunement   high
Sociability  moderate
Discipline   low
```

Useful for testing:

- gentleness,
- emotional support,
- people-pleasing,
- unreliability,
- complementarity,
- practical distrust despite affection.

## Priya

Suggested shape:

```text
Warmth       low
Attunement   high
Discipline   high
Agency       moderate
Stability    high
```

Useful for testing:

- calculating perception,
- competence,
- reserve,
- professional respect,
- practical trust without emotional comfort.

## Optional stress characters

Add:

- high Openness / low Stability / low Discipline;
- low Sociability / high Warmth / high Attunement.

These stress distinctions such as:

- adventurous vs reckless;
- quiet vs cold;
- shy vs caring.

---

# 23. Suggested Observable Actions

Initial actions should describe behavior, not relationship effects.

1. Calmly takes charge during a crisis.
2. Interrupts a junior employee repeatedly.
3. Notices someone is upset and quietly checks on them.
4. Keeps an inconvenient promise.
5. Cancels plans at the last minute.
6. Takes an impulsive risk.
7. Defends someone unpopular.
8. Takes credit for another person's work.
9. Tries an unfamiliar activity enthusiastically.
10. Refuses to deviate from an established procedure.
11. Admits a mistake publicly.
12. Hides uncertainty and pretends to know the answer.
13. Makes a shy newcomer feel included.
14. Dominates a group conversation.
15. Works patiently through a frustrating problem.
16. Loses temper after a minor setback.
17. Remembers a small personal detail.
18. Ignores obvious emotional discomfort.
19. Volunteers for an unpleasant responsibility.
20. Avoids responsibility when failure becomes possible.

Each should define an evidence/likelihood model over the latent space.

No action should directly say:

```text
MiraLikesDarius += 2
```

unless the effect is explicitly a separate dyadic event consequence rather than personality inference.

---

# 24. Example Evaluation

Suppose Mira knows Darius imperfectly.

```text
Mira's belief about Darius:

Agency
    mean: +0.82
    uncertainty: low

Stability
    mean: +0.68
    uncertainty: medium

Attunement
    mean: -0.05
    uncertainty: high

Warmth
    mean: +0.20
    uncertainty: high
```

The evaluator might produce:

```text
AFFILIATION
+ Composed assertiveness fits Mira moderately well.
- High Agency combined with suspected low Attunement is aversive.
? Warmth remains highly uncertain.

RESPECT
+ Strong Agency.
+ Strong Stability.
+ Evidence of Discipline.
= High expected respect.

COMFORT
- Low/uncertain Attunement.
- Because Darius is currently Mira's supervisor, the Comfort field weights Agency×Attunement more strongly.
+ Familiarity is increasing.
= Low-to-moderate comfort.

OTHER FACTORS
+ Shared interest: antique clocks.
+ Darius helped Mira during an emergency.
- Darius embarrassed Mira publicly last week.
- Mira is currently stressed at work. [independent Current-Affect pressure; not part of the personality field]
```

A Decision may then select only relevant pressures:

```text
ASK DARIUS TO COVER MY SHIFT?

He is reliable under pressure        d10
He helped me before                   d8
I hate feeling managed by him         d8
I don't want to owe him               d6
```

The social system does not choose the Decision outcome.

It supplies explainable pressures.

---

# 25. Test Method

For every torture-test case:

1. Construct the minimum characters and circumstances required.
2. Identify which layer should create the behavior:
   - latent personality,
   - appraisal field,
   - uncertainty/perception,
   - values,
   - interests,
   - dyadic state,
   - familiarity,
   - current affect,
   - context,
   - reputation/group belief.
3. Ask whether the case emerges without a bespoke scenario rule.
4. Inspect the explanation trace.
5. Compare simpler and richer matrix configurations where useful.
6. Record the failure category.
7. Look for clusters of failures.
8. Add new latent dimensions only when multiple failures require a distinction the current space cannot represent.
9. Add new matrix interactions only when they solve a meaningful class of cases.
10. Do not distort the personality field to solve a problem that properly belongs to another layer.

The torture corpus is diagnostic.

v0 does **not** need to pass all 100 cases.

The failures are part of the experiment.

---

# 26. Social Relationship Torture Test — 100 Cases

These cases are intentionally broad, contradictory, asymmetric, and occasionally strange.

## A. Unexpected compatibility

1. Two people would normally dislike one another but share an obscure interest that keeps bringing them together.
2. Two people share almost every interest but have no interpersonal chemistry.
3. Opposite personalities complement one another unusually well.
4. Nearly identical personalities constantly clash.
5. Two people bond primarily because both feel like outsiders.
6. Two people become friends through a shared enemy.
7. Someone enjoys a hobby only because of the person they do it with.
8. A hobby friendship never becomes a friendship outside that activity.
9. Two people enjoy arguing because disagreement itself is stimulating.
10. Someone chooses an objectively worse routine because it creates more opportunities to encounter a particular person.

## B. Asymmetry

11. A considers B a best friend; B considers A a casual acquaintance.
12. A sees B as a rival; B barely notices A.
13. A hates B; B sincerely likes A.
14. A trusts B completely; B does not trust A.
15. A sees B as a mentor; B does not realize they are mentoring anyone.
16. A feels protective of B; B finds it patronizing.
17. A envies B while B independently envies A for something else.
18. A thinks they reconciled; B is still angry.
19. A assumes B dislikes them; B actually likes them.
20. A remembers an old insult that B has completely forgotten.

## C. Public vs private

21. Someone publicly hates another person but privately adores them.
22. Someone publicly praises a coworker they privately despise.
23. Two public rivals are privately close friends.
24. Two people present as close friends but are privately estranged.
25. Someone mocks an interest publicly but secretly shares it.
26. Someone is colder toward a friend when coworkers are present.
27. Someone attacks a person they privately like because their social group expects it.
28. Someone defends a disliked person publicly because the criticism is unfair.
29. Someone maintains an old rivalry because the surrounding group expects the rivalry to continue.
30. Someone publicly claims not to care whether another person leaves but privately dreads it.

## D. Attraction and affection

31. Someone is attracted to an overbearing boss.
32. Someone is attracted to a person they actively dislike.
33. Someone deeply loves a friend but feels no romantic attraction.
34. Attraction develops through admiration for competence.
35. Attraction develops through rivalry.
36. Attraction fades as friendship becomes deeper.
37. A long friendship unexpectedly develops attraction years later.
38. Someone is attracted to a person whose values they oppose.
39. Someone becomes more attracted after seeing how kindly a person treats someone with no status.
40. Someone loses attraction after seeing how a person treats subordinates.

## E. Respect, trust, and affection disagree

41. Someone likes a person personally but does not respect them.
42. Someone respects a person enormously but dislikes them.
43. Someone trusts a person's motives but distrusts their judgment.
44. Someone trusts a person's judgment but distrusts their motives.
45. Someone trusts another professionally but not emotionally.
46. Someone shares secrets with a person they would never trust with practical responsibility.
47. Someone admires another until they begin working closely together.
48. Someone dislikes a boss personally but respects their competence.
49. Someone loves a boss personally but thinks they are terrible at the job.
50. Someone loses respect after witnessing how a person behaves under pressure while affection remains intact.

## F. Misperception

51. Someone mistakes shyness for dislike.
52. Someone mistakes politeness for friendship.
53. Someone mistakes friendliness for attraction.
54. Someone mistakes concern for control.
55. Someone mistakes forgetfulness for disrespect.
56. Someone thinks a scheduling conflict is deliberate avoidance.
57. Someone thinks a person is laughing at them when they are laughing at something unrelated.
58. Someone believes a coworker stole their idea when both developed it independently.
59. Someone believes a person is highly reliable because they have only observed them in structured contexts.
60. Someone believes a disliked person is incompetent despite repeated contrary evidence.

## G. Context dependence

61. A person's presence causes someone else's work performance to improve.
62. A hated boss entering the room causes someone's work performance to decline.
63. Two people get along privately but clash whenever a particular third person is present.
64. Two people only manage to get along when a particular mediator is present.
65. Someone is charming at parties but exhausting one-on-one.
66. Someone is wonderful one-on-one but uncomfortable in groups.
67. Two enemies share a favorite quiet place and implicitly suspend their conflict there.
68. Two people who dislike one another become comfortable together during a repetitive commute.
69. Someone behaves confidently at work but becomes passive around family.
70. Someone who is normally calm becomes unusually hostile toward close friends while highly stressed.

## H. History and change

71. Childhood friends grow into incompatible adults.
72. Former enemies no longer remember what the original fight was about.
73. Someone forgave an offense but still behaves cautiously.
74. Someone claims to have forgiven an offense but remains deeply resentful.
75. A small act of kindness creates loyalty years later.
76. Two people remember the same important event very differently.
77. Returning to an old location revives dormant affection.
78. A friendship fades without any conflict.
79. A dormant friendship resumes immediately after years apart.
80. Someone misses who another person used to be rather than who they are now.

## I. Group and network dynamics

81. Someone likes every member of a group individually but dislikes the group collectively.
82. Someone dislikes several members individually but strongly values belonging to the group.
83. Everyone in a group privately dislikes the informal leader but assumes everyone else likes them.
84. A newcomer unexpectedly changes the chemistry between existing friends.
85. One person's departure reveals that they were the social glue holding a group together.
86. Someone likes a stranger initially because a trusted friend likes them.
87. Someone dislikes a stranger before meeting them because a trusted friend does.
88. A bad reputation makes an ordinary act of kindness disproportionately meaningful.
89. A good reputation causes harmful behavior to be excused.
90. Someone is broadly disliked but deeply loved by a small inner circle.

## J. Contradiction, dependency, and odd human behavior

91. Someone relies heavily on a person they do not particularly like.
92. Someone likes a person but refuses to rely on them.
93. A helper enjoys being needed until the dependence becomes burdensome.
94. Someone resents being helped because they want to prove independence.
95. Someone is fiercely protective of a rival when outsiders criticize them.
96. Someone automatically seeks out a person when upset without consciously considering them a close friend.
97. Someone realizes who matters most by noticing whom they want to tell good news first.
98. Two people have enormous affection but consistently poor communication.
99. Two people communicate exceptionally well but have little affection.
100. Two people who appear statistically unlikely to matter to one another become one another's most important relationship through years of tiny repeated interactions.

---

---

# 27. What Failure Clusters Mean

## "Likes X only when Y is present"

Likely requires a pairwise matrix interaction or contextual coefficient.

## Similar people behave differently toward the same target

Likely requires distinct observer appraisal fields or values/history differences.

## Character reacts to a false impression

Requires belief/perception, not a new personality axis.

## "Everyone thinks everyone else believes..."

Likely requires perceived group norm or bounded second-order Knowledge.

## Years of tiny contact matter without salient memories

Requires familiarity/exposure.

## Public behavior differs from private feelings

Usually requires context/audience and Decision pressures, not a second hidden relationship score.

## Attraction, respect, comfort, and affection disagree

Supports multiple appraisal lenses rather than a universal compatibility field.

## Behavior changes because a person matures

May require long-timescale personality or preference drift.

## Most new cases demand unique exceptions

The model is failing.

---

# 28. Development Stages

## Stage 0 — Mathematical sketch and visualization

No production integration.

Build a spreadsheet/notebook/debug prototype that can:

- represent seven-dimensional character vectors;
- represent sparse appraisal fields;
- evaluate linear terms;
- evaluate selected pairwise interactions;
- evaluate an optional PSD ideal-point term;
- visualize 2D slices of fields;
- place target points on those slices;
- show contribution traces.

Hand-author Mira, Darius, Glen, and Priya.

The purpose is to understand the geometry.

## Stage 1 — Pure C# sparse-field evaluator

Implement the actual production-shaped concepts:

```text
PersonalityVector
TraitProjection
AppraisalField
AppraisalLensId
BeliefDistribution
SocialEvaluationResult
MatrixContributionTrace
```

Support:

- deterministic evaluation;
- diagonal coefficients;
- sparse symmetric pairwise `Q` terms;
- PSD ideal/tolerance term `P = LLᵀ`;
- sparse context-conditioned coefficient deltas;
- exact closed-form `E[s]` for Gaussian belief distributions;
- bounded output via the explicit Stage-1 approximation `g(E[s])`;
- shared cross-lens calibration;
- provenance.

Run 20–30 torture cases.

Stage-1 tests should explicitly compare:

```text
point estimate:          g(s(μ))
uncertainty-aware:       g(E[s])
```

so the team can see when covariance materially changes appraisal.

## Stage 2 — Generalized Knowledge and evidence updates

Integrate/extend Vivarium Knowledge so `ObserverRef` can represent characters as well as the player.

Add:

- sparse observer→target belief edges;
- uncertainty;
- observed-action evidence;
- joint latent updates;
- repeated/contradictory observations;
- witness determination through existing context indexes.

Verify that beliefs move for understandable reasons.

## Stage 3 — Multiple appraisal lenses

Test the smallest useful set of lenses.

Begin with:

```text
Affiliation
Respect
Comfort
```

Add Attraction or other lenses only when cases require them.

Verify that contradictory social states emerge naturally.

## Stage 4 — Dyadic history, familiarity, and context

Add:

- durable pair-specific channels;
- salient relationship memories;
- analytical exposure/familiarity;
- current affect;
- context-conditioned evaluation.

Run the full 100-case suite.

## Stage 5 — Bounded network/reputation effects

Add only as justified:

- reported beliefs,
- reputation,
- perceived group norm,
- trusted-source propagation.

Do not introduce recursive unbounded theory of mind.

## Stage 6 — Drift and population generation

Add:

- slow personality drift,
- slow/moderate preference-field drift,
- deterministic generated appraisal fields,
- culture/value/life-history priors.

Stress-test populations.

## Stage 7 — Vivarium gameplay integration

Feed social evaluation into:

- Decision influence construction;
- Activity performance;
- interaction candidate weighting;
- observation/knowledge;
- social consequences;
- notification/read models.

The social engine supplies pressures and explanations.

It does not directly puppet characters.

---

# 29. Provisional Data Shapes

These are conceptual and should remain implementation-flexible.

```text
PersonalityVector
├─ CharacterId
└─ Dimensions[DimensionId] -> scalar


TraitProjection
├─ TraitId
├─ Bias
├─ LinearTerms[]
├─ PairwiseTerms[]
└─ ExplanationMetadata


AppraisalField
├─ CharacterId
├─ AppraisalLensId
├─ Bias
├─ LinearCoefficients[]
├─ PairwiseCoefficients[] // sparse symmetric Q terms
├─ IdealPoint?
├─ IdealFactorL?          // P = L L^T
├─ ContextModifiers[]     // sparse deltas to b, w, Q, i, and/or L
├─ ResponseFunction
├─ CalibrationProfileId
├─ AppraisalFieldRevision
└─ Provenance[]


AppraisalCalibrationProfile
├─ CalibrationProfileId
├─ NormalizedRange
├─ StrengthThresholds[]
└─ Version


BeliefDistribution
├─ ObserverRef
├─ TargetRef
├─ MeanVector
├─ Uncertainty
└─ EvidenceRevision


ObservedSocialEvidence
├─ ActorId
├─ ActionDefinitionId
├─ ObserverIds[]
├─ MeasurementModelId
├─ ObservedAt
└─ SourceContext


DyadicRelationshipState
├─ ObserverId
├─ TargetId
├─ DurableChannels[]
├─ Familiarity
├─ ExposureState
├─ SignificantHistory[]
└─ Revision


SocialEvaluationResult
├─ ObserverId
├─ TargetId
├─ AppraisalLensResults[]
├─ InterestEffects[]
├─ ValueEffects[]
├─ DyadicEffects[]
├─ FamiliarityEffects[]
├─ ContextEffects[]
└─ CandidateDecisionInfluences[]


MatrixContributionTrace
├─ LinearContributions[]
├─ PairwiseContributions[]
├─ IdealPointContribution
├─ UncertaintyEffect
├─ ContextualChanges[]
└─ HumanReadableExplanations[]
```

Reconstructible indexes should track only active/relevant belief and dyadic edges.

Any cached social appraisal must depend on the relevant aspect revisions, including at minimum:

```text
BeliefDistribution revision
AppraisalFieldRevision
DyadicRelationshipState revision
relevant Context/Affect revisions
relevant Values/Interests revisions
```

A drift in Mira's preference field must invalidate cached appraisals even if her belief about Darius has not changed.

---

# 30. Explainability and Debugging Requirements

Every important social change should retain enough provenance to answer:

```text
Why did Mira's evaluation of Darius change?
```

Example:

```text
Observed:
Darius defended a coworker.

Knowledge update:
Mira's estimated Warmth       +0.09
Mira's estimated Attunement   +0.06
Uncertainty in Warmth         decreased

Derived explanation:
Supportive projection         increased
Overbearing projection        decreased slightly

Appraisal change:
Affiliation                   +moderate
Comfort                       +small
Respect                       unchanged

Why:
Mira's Affiliation field strongly rewards Warmth.
Mira's Comfort field rewards Attunement.
Her Respect field is driven more by Discipline/Stability.
```

Debugging should be able to show both:

- matrix-level contribution terms;
- human-readable named-trait explanations.

This provenance is necessary for tuning and for deciding whether emergent behavior is meaningful rather than noise.

---

# 31. Success Criteria

The concept is working if:

1. Four to six hand-authored characters create recognizably different evaluations.
2. The same target is evaluated differently by different observers.
3. One observer can simultaneously respect, dislike, trust, fear, or be attracted to different aspects of the same target.
4. Observed behavior changes belief rather than directly changing personality truth.
5. Uncertainty materially affects evaluation.
6. Named traits explain the geometry without becoming duplicate scoring channels.
7. Pairwise matrix terms solve meaningful interaction cases such as Agency×Attunement.
8. Shared interests can matter without changing personality coordinates.
9. Dyadic history can overpower baseline personality fit without erasing it.
10. Familiarity produces meaningful effects distinct from affection.
11. Context can temporarily alter behavior without rewriting durable social state.
12. The evaluator produces useful Decision influences.
13. The system remains deterministic.
14. Social state remains sparse at population scale.
15. A substantial portion of the torture corpus emerges without bespoke trope rules.
16. Failures cluster around coherent missing concepts.
17. Context-conditioned personality evaluation and independent situational pressures remain distinguishable in traces.
18. Appraisal lenses produce comparable calibrated strength bands rather than accidental lens-specific loudness.
19. Field drift invalidates cached evaluations independently from belief updates.

---

# 32. Failure Criteria

Reconsider the design if:

- named traits and matrices both independently score the same facts;
- most behavior requires bespoke relationship rules;
- primitive dimensions proliferate to rescue isolated cases;
- every appraisal lens converges toward the same answer;
- uncertainty adds complexity but never affects gameplay;
- matrix terms become impossible to explain;
- pairwise coefficients proliferate without producing distinct behavior;
- social belief requires universal pairwise storage;
- observations repeatedly double-count correlated evidence;
- long-term history and current mood cannot be separated;
- designers cannot predict the broad effect of authored preferences;
- the system produces mathematically interesting outputs that cannot become understandable gameplay pressures;
- `E[g(s)]` is treated as though it were exactly equal to `g(E[s])` rather than an explicit approximation;
- personality-conditioned context and unrelated situational pressure become indistinguishable;
- one appraisal lens systematically dominates Decisions because calibration differs implicitly.

---

# 33. Guiding Principle

Vivarium's social model should not answer only:

> Are Mira and Darius compatible?

It should be able to answer:

> Why does Mira enjoy Darius's company?

> Why does she respect him during a crisis?

> Why does she hate working beneath him?

> Why does she remain uncertain whether he is kind?

> Why does she trust his competence but not his motives?

> Why does an interaction she observed this morning change what she believes about him?

> Why does years of mundane familiarity make his absence suddenly matter?

> Why does the same Darius look different through Affiliation, Respect, Comfort, or Attraction?

> Which of those pressures matter to the choice Mira is making right now?

The core social engine is:

> **A compact latent person, imperfectly known, evaluated through an observer's structured preference fields, then transformed by shared history and present circumstances into reasons for action.**

That is the system Vivarium should prove and build.
