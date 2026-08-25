# Vivarium — Desire, Inhibition & Condition Brief

**Status:** Draft design reference — revised across three rounds of adversarial review, two of which
checked its specific code citations line-by-line against the live repo. Reliable as vocabulary and as an
honest account of what does and doesn't already exist; not yet attempted as an actual implementation, so
sequencing and effort estimates should still be treated as provisional
**Project:** Vivarium
**Purpose:** Give Desire, Inhibition, Addiction, and Condition (status effects) a single mechanically precise
place in the existing architecture — specifically, to close the gap between the Regulation & Development
Model Brief's durable regulatory vocabulary and `DecisionReasoning.md`'s existing pressure-to-reason
pipeline, using existing primitives everywhere one already fits.

**Depends on:** [`DecisionReasoning.md`](DecisionReasoning.md), [`SocialModel.md`](SocialModel.md),
[`../Architecture/Reference.md`](../Architecture/Reference.md), [`../Product/CoreIdentity.md`](../Product/CoreIdentity.md)
(including its §4 "Interference is not exclusively a player action," added alongside this revision),
and the *Regulation & Development Model Brief* (Jason's 2026-08-25 draft; not yet a committed repo path).

**Relationship to those documents:** This brief does not introduce a new pipeline. `DecisionReasoning.md`
already owns "how does pressure become a reason" (§2's canonical pipeline); this brief adds vocabulary and
exactly one new primitive to that existing pipeline, and states precisely how each maps onto code that
already exists. Where the Regulation brief's durable-character-space material (RegulatoryProfile,
Development, Culture) is concerned, this brief stays silent — that scope remains owned there and is
explicitly out of scope here (§9). Where `SocialModel.md`'s belief/observation model is concerned, this
brief complies with it rather than extending it — see §5.5, corrected in this revision.

**What changed in this revision:** An earlier draft of this brief was reviewed adversarially. Four of that
review's corrections exposed genuine internal inconsistencies rather than stylistic preferences, and are
load-bearing changes in this version: (1) Desire is redefined as the pre-arbitration candidate, not the
post-arbitration winner — the original definition made Inhibition logically incoherent; (2) Social Appraisal
was corrected to read an observer's *belief*, mediated by observed behavior, rather than a directly
modified personality value — the original design silently bypassed `SocialModel.md`'s belief pipeline;
(3) the claim that no acquired pressure may ever compel a physical outcome was too broad — Jason's own
read is that this was a design oversight, not a deliberate omission, and `CoreIdentity.md` has been amended
alongside this brief to say so explicitly; (4) several places conflated "same storage shape" with "same
semantic meaning" (Addiction inside Values, an `inhibition.*` channel-naming convention) and have been
un-conflated. A fifth claim — that several concurrently-pressing Needs get reconciled together into one
arbitration pass — was checked against the actual routine code for this revision and found to be false of
the system as it exists today; that finding is now recorded explicitly rather than assumed either way (§9).

**What changed in the second revision:** A further review checked this brief's specific code citations
line-by-line against the live repo rather than trusting them by inference, and found five more corrections,
all now folded in: (1) `SocializingRoutineService.TryStart` does **not** build Desire candidates and run
arbitration the way `RecreationRoutineService.TryStart` does — verified it picks and starts the first
workable candidate directly, with no Considerations weighed against alternatives at all; only the
*recipient's* later Accept/Decline of the resulting invitation goes through a Decision, and that path skips
preflight entirely rather than being admission-gated (§2.1); (2) the composed signal-provider roster is
twelve providers, not nine, and `AffectState` is not "completely invisible" to Considerations — it already
reaches `SocialAppraisalSignalProvider` through an `ObserverAffect`-sourced rule, though only as a colorant
of appraising *another* character, never as a standalone signal an actor's own Desire can read (§6);
(3) `AffectState.Set`/`ApplyDelta` bump only a local per-kind counter — they never call `world.BumpRevision`,
publish anything, or schedule a crossing, so giving Craving a real rate needs an actual new
`AffectProgressionService`, not a two-line change to an existing call (§4.2–§4.3, §7); (4) "Condition is one
new primitive" is true of the concept but was understating the build — authoritative instance state,
content definitions and validation, scheduled expiry payloads/handlers/codecs, composition, save migration,
Knowledge/discovery wiring, signal dependencies, projections, and history provenance are all real surface
area even when the underlying math is reused (§5.1); (5) neither `NeedProgressionService` nor
`ActivityTransitionService` currently composes multiple simultaneous rate contributors at all — both are
verified last-write absolute-rate setters where the *caller* supplies the already-composed number — so
Condition's additive composition rule is a real redesign of both target services, not incremental wiring
onto composition that already exists (§5.4).

**What changed in the third revision:** A further pass found the five corrections above well-integrated,
but flagged that the document still contradicted its own findings in several places, plus one new
structural gap: (1) this Status line still claimed no live-code check had happened, after two rounds that
did exactly that — reworded to say what actually happened; (2) the Executive Summary still said "almost no
new Domain machinery" and "not new engine machinery" after the body had gone on to document a new
`AffectProgressionService`, a redesign of two target services' rate composition, new persistence, and new
Knowledge wiring — softened to match; (3) §4.2 said to build Craving "now," which is this brief overstepping
its own scope — it doesn't get to set production sequencing, and §10 already disclaims that; reworded to
"when a slice actually needs it"; (4) §7 claimed the future `ConditionInstance` list "already round-trips"
through save/load — it doesn't exist yet, so nothing about it already does anything; corrected to say it
needs the same kind of explicit codec work `Character.Traits` already went through, not automatic
serialization; (5) a genuine new finding: `SocialAppraisalSignalProvider.Resolve` (verified at
`DecisionReasoningProgram.cs`) registers exactly one dependency — a `BeliefContext` key scoped to the target
— even when its computed value depends on `ObserverAffect`. An `AffectProgressionService` that only bumps a
revision key `SocialAppraisalSignalProvider` never declared a dependency on would leave active social
Decisions holding stale appraisal reasons after Affect changes — a real staleness bug this brief would
introduce if built exactly as previously written. §6 and §7 now call this out as required work, not optional
polish. §10 was also restructured around vertical slices rather than horizontal infrastructure, per this
round's feedback, and now lists the previously-implied-but-unlisted work (AcquiredDrives, ActorConditionSignalProvider,
Knowledge/discovery, projections, save migration) explicitly rather than leaving it folded into prose
elsewhere in the document.

---

# 1. Executive Summary

Four ideas — Desire, Inhibition, Addiction/Craving, and Condition (including involuntary physical
consequences as a Condition-adjacent case) — turn out to need almost no new Domain *concepts*. That claim
should not be read as "almost no new work": getting Craving and Condition actually running still means a
new `AffectProgressionService`, a redesign of how two existing target services compose simultaneous rate
contributors, new persistence, and new Knowledge wiring — all real, all detailed in §4–§7. What's true is
narrower and still worth stating plainly, because it's what keeps that work from sprawling into a second
parallel reasoning system:

> **Desire is not a new entity. Inhibition is not a new entity. Only Condition is a new kind of state — and
> even Condition is exactly one new primitive, not several. Everything Condition needs beyond that primitive
> is new plumbing for existing machinery, not a new machine: existing services gain new callers and, in two
> places, a capability they're missing today (§5.4); Decision Reasoning gains new Signal sources through its
> existing provider pattern; Social Appraisal gains nothing new to read. The one place this brief asks for
> an explicit product-level acknowledgment rather than just new plumbing is `CoreIdentity.md`, now amended,
> that a body can override a mind the same way the Observer already can.**

Concretely:

```text
Desire
    → an ephemeral candidate action a routine proposes in response to current pressure — already built,
      today, as one of the DecisionOptions RecreationRoutineService.TryStart assembles before arbitration.
      No new state. Not the arbitration winner — the proposal arbitration is run ON. Verified NOT yet
      uniform across routines: SocializingRoutineService.TryStart currently picks and starts its first
      workable candidate directly, with no arbitration step at all (§2.1).

Inhibition
    → the existing negative-signed Consideration / CandidateReason, evaluated against a specific Desire's
      Option during that same arbitration, given a name and an authoring convention so it presents and
      explains distinctly. No new state, no new field.

Addiction
    → durable acquired state: an entry in a WeightedTagSet-shaped container of its own (not Values —
      see §4.1). Reuses the primitive, not the container.

Craving
    → an AffectState entry, actually given a nonzero rate (the storage primitive already supports this;
      nothing currently uses it). No new *type* — but a real new *service* is needed to drive it, since
      today's only mutator bumps a local counter and nothing else (§4.2–§4.3).

Condition (status effects, including withdrawal/intoxication/exhaustion)
    → ONE new conceptual primitive: a small, bounded, time-scheduled list of active modifier instances
      per Character, applied through existing services wherever a target already has one
      (NeedProgressionService, ActivityContextModifier), through one new signal provider where it
      doesn't (Decision reasoning, which today reaches Affect only indirectly through Social Appraisal —
      §6), through observed *behavior* rather than a direct personality read where Social Appraisal is
      concerned (§5.5), and — for a narrow, physically severe subset — through a genuinely involuntary
      consequence the character's own Decision Reasoning never chose (§5.7, CoreIdentity's new companion
      principle). "One new primitive" describes the concept, not the build: every target it touches needs
      real new plumbing, and two of them (Need and Activity rate composition, §5.4) need work that has no
      existing precedent to extend at all.
```

The rest of this brief specifies exactly how.

---

# 2. Desire: an ephemeral proposal, evaluated by arbitration it is not the same thing as

## 2.1 What a Desire actually is

A Desire is not the answer to *"what did this character just decide to do."* It is the answer to *"what is
one thing current pressure is nudging this character toward, before anything gets to argue with it."*

Verified against `RecreationRoutineService.TryStart`: the routine first builds a list of candidate
`DecisionOption`s from the Need's authored `RecreationRoutineDefinition.Candidates` — each one **is** a
Desire, a proposal grounded in one pressure source, evaluated for nothing yet. Only after that list exists
does the routine call `DecisionReasoningPreflightService.Evaluate`, which scores every Considered Option
and returns whichever scored highest. That result is not the Desire — it is the **Preference**: what
arbitration settled on once every Desire (and everything opposing each one) had its say.

This proposal-then-arbitration shape is confirmed for `RecreationRoutineService` specifically — it is not
yet a uniform pattern across every routine, and this brief should not imply otherwise.
`SocializingRoutineService.TryStart` was checked for this revision and does something meaningfully
different: it iterates nearby candidates and calls `_transitions.BeginActivity` on the **first** one
`_interactions.TryInteractAtLocation` accepts — no `DecisionOption` list is built, no Considerations are
weighed against alternatives, and there is no arbitration step at all for "who does Mina approach." A
Decision only enters the picture afterward and on the *other* character's side: the recipient's
Accept/Decline of the resulting invitation goes through `CompiledDecisionGenerationService.Generate`
directly — not gated through `DecisionReasoningPreflightService.Evaluate`/`AdmissionFloor` the way
Recreation's routine is. So today there are at least three different shapes already in production:
Recreation's propose-then-arbitrate-then-maybe-promote; Socializing's requester-side pick-the-first-
workable-candidate with no arbitration; and Socializing's recipient-side unconditional Decision. Bringing
Socializing's requester side in line with the Desire/Inhibition vocabulary this brief defines — so a
character could actually decline to approach someone, not just have nothing else nearby to approach instead
— is real, currently-unbuilt work, not a naming exercise over an existing arbitration step.

```text
Need / Craving / Affect pressure exists (durable, continuous)
        ↓
a trigger event fires (threshold crossing, arrival, activity started, decision resolved)
        ↓
a routine builds one or more candidate Options from its authored content — each candidate IS a Desire
        ↓
Considerations are evaluated for every candidate — supporting (§Considerations) and inhibitory (§3)
        ↓
arbitration picks the highest-scoring candidate — the PREFERENCE, not the Desire
        ↓
Importance < AdmissionFloor  →  silently acted on (ordinary routine behavior)
Importance >= AdmissionFloor →  promoted into a compiled Decision; every Desire is now one of its Options
```

Getting this ordering right matters beyond pedantry: it's the only ordering under which Inhibition (§3) is
coherent. Inhibition opposes a Desire *during* arbitration. If Desire were defined as arbitration's own
output, there would be nothing left standing for Inhibition to oppose by the time it existed.

## 2.2 Why this is deliberate, not an oversight

`AnalyticalProgression`'s whole reason for existing is that continuous pressure is computed on demand, not
ticked (`AnalyticalProgression.cs`: "Sixty hunger events per hour is exactly what this exists to
prevent"). A persistent "Desire" entity that has to be created, revision-tracked, dependency-indexed, and
kept in sync with the Need/Craving/Affect state that produced it would reintroduce exactly the kind of
standing per-character bookkeeping that principle exists to avoid — for state that is cheap to recompute
fresh every time a real trigger event actually fires. The durable thing is the pressure (`NeedState`,
`AffectState`); a Desire is a candidate built fresh from a read of it, discarded whether or not it wins.

This also matches the existing guardrail already established for the Rest-vs-Recreation slice (see the
project's `rest-recreation-reevaluation-guardrails-audit.md`): reevaluation happens at scheduled trigger
events, never per tick, and a routine's candidate-building-then-arbitration step already is the mechanism,
not a new one layered on top of it.

## 2.3 Desire is the ephemeral member of a durable family — not the only kind of "want"

Not everything that reads in English as "Mina wants X" belongs in this brief's Desire vocabulary. Some
wants are genuinely long-lived, and modeling them as a Desire — or, worse, forcing them into a
Craving-shaped accumulating pressure just because that's the closest existing shape — would misrepresent
what they are:

```text
Goal
    a durable desired future state ("become a doctor," "leave the Bakery")
    — owned elsewhere; this brief does not define it, only leaves room for it.

Commitment
    a durable intended/obligated action, already a first-class Domain concept (`CommitmentConflict.md`).

Regulatory pressure (Need / Craving / Affect)
    a durable, continuously changing internal condition — what generates Desires, not a Desire itself.

Immediate Desire
    the ephemeral "what would I do about this right now" proposal this brief defines in §2.1 — ordinary
    Considerations resolve most; a real conflict promotes one into a compiled Decision.
```

A want that must *accumulate* across many separate evaluations without a single persistent trigger —
"Mina has wanted to confront Darius for three days and it's getting harder to ignore" — is not itself a
stored Desire; if it needs durable state at all, that state is the *pressure* (an `AffectState` entry, per
§4.2, or occasionally a Need), read fresh into a new Desire each time it's evaluated. But "Mina wants to
become a doctor" is not pressure-shaped at all — it has no decay curve and doesn't get "satisfied" by a
single Activity. That one is a Goal, full stop, and belongs wherever Goals already live or eventually will.
This brief's Desire vocabulary should never be stretched to cover it.

---

# 3. Inhibition: a named category of existing Considerations, not a new Domain concept

## 3.1 Definition

An Inhibition is a `CandidateReason`/`DecisionInfluence` that opposes a specific Desire's Option during the
same arbitration pass that Desire is being evaluated in. Mechanically it is nothing but a negative-signed
Consideration (`DecisionReasoning.md` §21: "positive → supports this Option, negative → opposes this
Option"). What Inhibition adds is not a mechanism — it's a **name for a specific semantic role** a
Consideration can play, so authoring and presentation can treat "why does she hesitate" as a distinguishable
category from "why is this option bad."

```text
NEED/CRAVING/AFFECT
    ↓
one or more DESIRES (ephemeral candidate proposals, §2.1)
    ↓
CONSIDERATIONS evaluated for each Desire's Option
    │
    ├─ supporting (positive) — ordinary Considerations
    └─ inhibitory (negative) — Considerations specifically opposing acting on that Desire
        ↓
arbitration scores every candidate → PREFERENCE (§2.1)
    │
    ├─ a Desire clearly wins        → ordinary routine behavior (that Desire acted on)
    ├─ a Desire is clearly suppressed → a different candidate wins instead — but only if one was offered (§3.2)
    └─ genuine conflict             → Importance clears AdmissionFloor → compiled Decision
```

This is, term for term, the same computation `DecisionReasoning.md` §39.2 and `DecisionImportance.md`
already specify: Importance is derived from the same per-instance evaluated Signal magnitude arbitration
already produces. "Clearly wins / clearly suppressed / genuine conflict" is a plain-language restatement of
"stays under AdmissionFloor and resolves silently toward whichever candidate scored highest."

## 3.2 The one thing this requires that isn't automatic: an explicit decline candidate — and it's load-bearing, not merely tidy

Verified directly in `DecisionReasoningPreflightResult`'s constructor: selection is a strict argmax over
`context.Options` — it starts from `Options[0]`, keeps whichever candidate scores higher, and always
returns *some* option's id. There is no floor beneath which the whole candidate set can be declined. That
means if a routine hands arbitration exactly one candidate — say, a single Recreation option — that
candidate wins by construction regardless of how negative its Considerations score. Its Inhibitions can
dilute its Importance below `AdmissionFloor` (which only controls whether the outcome gets promoted into an
explainable Decision), but they cannot currently make the character *not do it*, because there was never a
second candidate available to lose to.

This means the earlier draft's assumption that routines already have an "implicit do-nothing" path was
wrong for at least this call site: `RecreationRoutineService.TryStart` only produces zero candidates when no
affordance exists at all (need below `ActivationThreshold`, no reachable location) — never when candidates
exist but Inhibition-laden Considerations argue against every one of them. For Inhibition to be able to
*win* against a live Desire, a routine's candidate set has to include something to inhibit toward: "keep
doing what I'm currently doing," "stay home," "say nothing." A `NeedContinuationRoutineDefinition`-style
generator (already present in the catalog for the Rest-vs-Recreation slice) is the concrete pattern to
follow: "Continue" is itself a real Option with its own Considerations (current activity's enjoyment,
remaining duration, momentum), not merely the absence of a winning alternative.

The rule this brief recommends is narrower than "every Desire must have a decline option," because that's
not always true either — *which exit do I take during a fire* has several live candidates and no sensible
"do nothing." The rule is: **when non-enactment is a meaningful and physically available outcome, the
candidate set must represent it explicitly enough for arbitration to actually be able to choose it** —
otherwise Inhibition has nothing legible to win toward, and nothing legible to point at in the explanation
trace either.

## 3.3 Authoring convention, not a new field — and the channel name should not carry the role

Rather than add an `IsInhibitory` bool to `ConsiderationDefinition` (which would require every existing and
future Consideration to answer a question that only matters relative to a specific Desire, not as a
property of the Consideration itself — the same Consideration can support one Option and inhibit another in
the same Decision), the recommended convention is presentation-layer: a `CandidateReason`/
`DecisionInfluence` is inhibitory *for a given Desire* exactly when its sign opposes that Desire's Option.
This can be computed from data already present (`OptionId`, `SignedExpectedScore`, and which Option the
Desire being evaluated corresponds to) — no new persisted field, no new Domain type.

An earlier draft of this brief then undercut its own argument by proposing an `inhibition.*` prefix for
`ReasonChannelId` (`inhibition.social_anxiety`, `inhibition.self_doubt`). That's the same category error
§3.3's opening paragraph just ruled out, applied to the channel's name instead of a boolean field: whether
`social_anxiety` is functioning as an inhibition is relative to which Option it's being weighed against in
a given Decision, not an intrinsic property of the reason. `ReasonChannelId`s should name the reason itself
— `social_anxiety`, `duty`, `risk`, `embarrassment` — and let the current Decision's option-relative sign
determine whether it's presented as support or inhibition. The same channel can be a Consideration that
supports staying home tonight and inhibits going out tomorrow; giving it a permanent `inhibition.*` name
would quietly deny that.

---

# 4. Addiction & Craving: acquired pressure on existing primitives

## 4.1 Three-part split — reusing primitives, not containers

```text
Addiction   (durable, acquired, slow-changing)   → WeightedTagSet entry, in its own container
Craving     (current, continuously rising)        → AffectState entry, WITH a real nonzero rate
Withdrawal / intoxication / satiation (temporary)  → Condition (§5)
```

Addiction does not need a new *class*. `Character.Values` and `Character.Interests` are already
`WeightedTagSet` — a stable authored id with a deterministic clamped integral intensity
(`[-10000, 10000]`), exactly the shape an acquired dependency's *strength* needs. But `Values` and
`Interests` are deliberately kept as two separate `WeightedTagSet` instances despite sharing an identical
implementation, precisely because "what matters to me" and "what do I enjoy" are different facts about a
person even though they're stored the same way. Addiction deserves the same discipline, not a shortcut:
reuse the primitive, but give it its own container — an `AcquiredDrives`-shaped `WeightedTagSet` distinct
from `Values`, or, once it exists, a natural home inside the Regulation brief's `RegulatoryProfile` (durable
acquired dependency strength is squarely regulatory-domain state, not a value). What must not happen is
`addiction.substance_x = 7000` living in the same dictionary as `values.family = 7000`, with the reader
expected to infer the difference from the tag prefix. Same primitive implementation, deliberately different
semantic container — this is not inventing a new mechanism, it's applying the exact discipline `Values`
versus `Interests` already models.

## 4.2 Craving reuses `AffectState`'s shape — how confident this brief is that it's also the right *semantic home*

`AffectState` (`Social/CharacterSocialState.cs`) is an open-vocabulary
`SortedDictionary<AuthoredId, AnalyticalProgression>` on every Character, already seeded with
`Stress`/`Arousal`/`Irritation`/`Fear`/`Confidence`/`Loneliness`. Nothing structurally prevents adding
`affect.craving.substance_x` as a seventh kind — the dictionary is open, and `AffectState.Set` already
accepts a full `AnalyticalProgression`, meaning it can already carry a real rate. The gap is bigger than
"the only mutator called anywhere today, `ApplyDelta`, always constructs a rate-zero
`AnalyticalProgression.Constant`." Checked directly against `CharacterSocialState.cs`: `AffectState.Set`
takes no `WorldState`/`SimulationContext` at all — it bumps only a local `_revisions[kind]` counter private
to the `AffectState` instance. It never calls `world.BumpRevision`, never publishes a domain event, and has
no scheduling companion whatsoever. `NeedProgressionService.Rearm` is not something Craving can "go
through," because nothing analogous exists yet for `AffectState` to go through. Craving genuinely needs a
new `AffectProgressionService` — structurally parallel to `NeedProgressionService`, but not a call site
reusing it — that (a) sets a real-rate `AnalyticalProgression` the way `NeedProgressionService.SetRate`
does, (b) bumps a real `world`-scoped `RevisionKey` so `DecisionDependencyIndex` can see the change, and
(c), per §4.3, schedules the crossing that lets Craving actually gate behavior. This is small in scope —
one new service, one new revision key, mirroring a pattern that already exists once for Needs — but it is
new infrastructure, not a parameter change to an existing call.

That much is a confident claim: the storage shape fits. Whether `AffectState` is also the correct
*semantic owner* of Craving is a weaker claim, and this brief should not overstate it. `SocialModel.md`
describes Affect as current emotional state — though it's worth noting `Loneliness` is already seeded
there, and Loneliness already reads more like motivational/regulatory pressure than pure emotion, so the
boundary isn't as clean as "Affect = feeling, never motivation" would suggest. Still, if Connection
regulation, boredom/stimulation, and autonomy frustration eventually all become experience-driven analytical
states too, stuffing all of them into `AffectState` risks quietly renaming it `DynamicCharacterState` while
still calling it Affect. The recommendation: **when a slice actually needs Craving, build it as an
`AffectState` entry** — it is by far the smallest way to prove the acquired-pressure shape on one concrete
case, not a claim that Craving belongs ahead of other work on anyone's roadmap; this brief sets vocabulary
and shape, not sequencing (see §10) — **but treat `AffectState` as "the currently-available
analytical-state container," not as Craving's permanent conceptual home.** If a
lower-level `AnalyticalStateSet<AuthoredId>` primitive is ever factored out (the same way `SignalField` is
already shared by multiple Domain concepts without those concepts becoming identical), Craving is a natural
candidate to move onto it without changing any of the behavior described in this brief.

```text
addiction.substance_x intensity (durable acquired-drive container, §4.1)
        ↓ determines
affect.craving.substance_x rate (AffectState, continuous)
        ↓ crosses an authored threshold (see §4.3 on scheduling)
        ↓
one or more DESIRES: "seek X" (§2 — ephemeral candidate proposals)
        ↓
INHIBITIONS (§3): obligations, values, discipline, relationships — ordinary Considerations,
    reading the same signal sources every other Decision reads
        ↓
resolves like any other arbitration: a Desire clearly wins / is suppressed / genuine conflict → Decision
        ↓
consumption → satisfying offset applied to affect.craving.substance_x (ApplyDelta, same as a meal
    applies an offset to Hunger) → cycle resumes
```

## 4.3 Craving needs the same threshold-scheduling AffectState currently lacks

`AffectState` has no `NeedState`-style `BehaviouralThreshold`/`PendingThresholdEventId`/`Rearm` companion.
If Craving is meant to *gate behavior* (produce a trigger event the way `NeedThresholdReachedEvent` does),
it needs that scheduling machinery extended to (at minimum) the craving kind specifically — this is new
work, structurally identical to `NeedProgressionService.Rearm`, not a conceptual gap. This brief recommends
treating it as scoped, targeted new work rather than a general `AffectState`-wide threshold system: only
Affect kinds meant to gate behavior (Craving being the first) need scheduled crossings.

## 4.4 The non-negotiable design constraint — and exactly where its boundary sits

`CoreIdentity.md` §4 makes the constraint this brief needs unambiguous, and — as of this revision —
explicit about its own boundary too. Influence ("the player alters the balance of reasons... the character
still chooses") is the only category **Craving** is allowed to occupy. An addiction that deterministically
forces consumption once Craving crosses some value would be Interference-shaped state wearing a Need-shaped
costume — `CoreIdentity.md` treats conflating those as a serious category error ("A forced outcome is not
the same thing as an autonomous choice"). Mechanically this is automatically satisfied as long as Craving
only ever contributes a Desire and Considerations (§2–§3) and never bypasses Decision Reasoning to directly
call `ActivityTransitionService.BeginActivity` unconditionally. Two characters with identical Craving
intensity resolving differently because their Values/Discipline/Relationships/existing Considerations
differ is not an edge case to handle — it is the architecture working as designed.

That constraint is specifically about *motivational* pressure compelling a *chosen* action — it is not, and
was never meant to be, a claim that Conditions can never produce a physically forced outcome at all.
`CoreIdentity.md`'s new companion section (added alongside this revision) draws that line explicitly: a
craving may never choose for a character, but a severe enough physical Condition may still end an Activity,
block a transition, or force sleep, the same way player Interference already can — see §5.7.

---

# 5. Condition: one new primitive

## 5.1 What's genuinely new — and what "one primitive" does and doesn't mean

Every modifier target the original discussion proposed already has an existing mechanism to extend
*except one*. Checked against real code:

| Target | Existing mechanism | New work needed |
|---|---|---|
| Need progression (rate/threshold) | `NeedProgressionService.SetRateAndThreshold` | A composition rule owned by the target, not by Condition (§5.4) |
| Activity performance | `ActivityContextModifier` via `ActivityTransitionService.ApplyContextModifier` | None — apply/remove through the same path |
| Decision Signals | `IDecisionSignalProvider` / `DecisionSignalProviderRegistry` | One new provider (§6) |
| Perception/observation evidence | `SocialModel.md` §8 evidence/likelihood model | Not verified this pass; likely a new evidence label, not new machinery |
| Behavioral expression, as read by other characters | `SocialModel.md`'s belief/observation pipeline | A modifier on *expressed behavior*, feeding belief the normal way — not a shortcut around it (§5.5) |
| Severe, physically forcing outcomes | `CoreIdentity.md`'s Interference concept, now extended | A narrow, explicitly-flagged involuntary path, causally recorded the same way Interference already is (§5.7) |

"One new primitive" is a claim about *concept count*, not about implementation size, and this brief should
not let the phrase imply otherwise. Even with every column on the right reusing existing math or an existing
pattern, actually shipping Condition still means: authoritative per-Character instance state and its content
definitions; content validation for those definitions; scheduled expiry payloads, handlers, and save codecs;
the composition logic in §5.4 (which, per that section, does not currently exist at either target site);
save/migration for the new Character field; `DiscoveryChannel` wiring for Knowledge-gated visibility; the
two new `IDecisionSignalProvider`s in §6 and their dependency routes; any Trait-projection interactions; and
history/provenance for §5.7's involuntary path. None of that is a hidden new *concept* — Condition really is
one new kind of thing, not several — but it is genuine surface area, and §10's sequencing reflects that
rather than treating Condition as a quick follow-on to the Desire/Inhibition vocabulary work.

## 5.2 `ConditionInstance`: storage shape

Mirrors `ActivityInstance._activeModifiers` structurally (a bounded list of time-bounded, cause-attributed,
add/remove-by-id modifiers) but lives on `Character`, not on a single Activity, because a Condition like
"Drunk" must survive an Activity transition (walking home while still drunk) that `ActivityContextModifier`
by design does not.

```text
ConditionInstance
├─ ConditionDefinitionId
├─ AppliedAt
├─ ExpiryKind             // Fixed | AnalyticallyDerived — these are not the same mechanism (see below)
├─ ExpiresAt              // set when ExpiryKind = Fixed
├─ DerivedExpiryTarget?     // set when ExpiryKind = AnalyticallyDerived, e.g. (NeedId, threshold)
├─ Cause                  // EntityRef — usually an Activity, another Character, or an Addiction tag
├─ PendingExpiryEventId    // scheduled, mirrors NeedState.PendingThresholdEventId — never polled
└─ StackKey                // for StackingPolicy (§5.6)
```

A **Fixed** expiry ("Drunk, until 23:15") is an ordinary one-shot scheduled event, exactly like
`ActivityInstance`'s completion event — nothing more to say about it.

A **derived** expiry ("Exhausted, until Energy recovers above 6000") is not the same mechanism wearing a
different label. It depends on an analytical value whose *rate* can itself change while the Condition is
active — someone who was Exhausted might then also start Resting, changing Energy's recovery rate, which
changes when the derived crossing actually happens. That means a derived `ConditionInstance` needs the same
revision-aware rescheduling discipline any other derived future event already needs:

```text
the target Need's AnalyticalProgression revision changes (rate or offset changed)
        ↓
the Condition's previously-scheduled derived-expiry crossing is stale
        ↓
recompute TryTimeOfCrossing against the new progression
        ↓
cancel the stale ScheduledEventId, schedule the new one
```

This is not a second Condition system — it's `NeedProgressionService.Rearm`'s existing discipline, applied
once more, this time to a Condition's own expiry rather than a Need's behavioral threshold.

`Character` gains one new field: a small `SortedDictionary`/list of active `ConditionInstance`s, the same
shape as `_traits`.

## 5.3 `ConditionDefinition`: content shape

```text
ConditionDefinition
├─ ConditionId
├─ DisplayName
├─ DefaultDuration?                 // fixed, when not analytically derived
├─ StackingPolicy                    // Replace | Extend | Stack | Ignore-if-present
├─ NeedRateDeltas[]                   // (NeedId, rateDelta) — contributed to, not owned by, §5.4
├─ ActivityPerformanceRateDelta?      // applied via existing ActivityContextModifier at Activity-begin
├─ AffectDeltas[]                      // (AffectKind, delta or rate) — e.g. Craving satisfied on consumption
├─ BehavioralExpressionModifier[]       // (SocialVector dimension, bounded delta) — see §5.5. Renamed from
│                                         an earlier draft's PersonalityOverlay; the rename is deliberate.
├─ DecisionSignalContribution?          // exposed through the new provider, §6
├─ ObservationEvidenceLabel?             // fed into SocialModel.md §8's evidence pipeline while active
├─ InvoluntaryConsequence?               // optional; a physically forced outcome — see §5.7. Absent for
│                                          the overwhelming majority of Conditions.
└─ DiscoverableThrough[]                  // same DiscoveryChannel pattern as TraitDefinition — knowing
                                            someone is drunk is itself Knowledge, not a free omniscient flag
```

Not every Condition uses every field — "Exhausted" might only touch `NeedRateDeltas`,
`ActivityPerformanceRateDelta`, and (severe cases only) `InvoluntaryConsequence`; "Drunk" touches
`BehavioralExpressionModifier`, `ActivityPerformanceRateDelta`, and `ObservationEvidenceLabel`; "Withdrawal"
touches `AffectDeltas` and `DecisionSignalContribution`.

## 5.4 Composition rule for simultaneous Conditions — a real redesign of both target services, verified

Verified against `ActivityInstance`: today, `_activeModifiers` is bookkeeping/provenance only —
`ApplyContextChange` sets whatever single rate its caller computes; the list does not auto-sum. An earlier
draft of this section treated that as the *only* gap and proposed routing Condition's contribution through
"the rearm/reschedule step the target already owns," implying `NeedProgressionService` and
`ActivityTransitionService` already know how to compose more than one simultaneous contributor and Condition
would simply be handed that composition. Checked directly against both services for this revision, and
that's not true of either one:

- `ActivityTransitionService.ApplyContextModifier` calls `activity.ApplyContextChange(now,
  modifier.PerformanceRateNumerator, modifier.PerformanceRateDenominator)` — it applies the incoming
  modifier's rate as the new absolute rate, full stop. `RemoveContextModifier` requires the *caller* to
  already know and pass in `restoredRateNumerator`/`restoredRateDenominator` — the service has no way to
  recompute what the rate should revert to on its own, because it never tracked what combination of
  contributors produced the current rate in the first place.
- `NeedProgressionService.SetRate`/`SetRateAndThreshold` are exactly the same shape: each call replaces
  `need.Progression`'s rate outright via `WithRate(...)`. Nothing sums a base rate against a list of active
  modifiers; whichever call happened most recently wins.

So two simultaneous Conditions touching the same Need or the same Activity's performance rate do not
"combine deterministically" through any existing mechanism today — they would silently clobber each other,
last write winning, exactly the failure mode this brief set out to prevent. Building the additive-delta
composition rule below is not wiring Condition into an existing target-owned capability; it is building that
capability at both target sites for the first time, and Condition is simply the first client of it:

> **A target (a Need, an Activity's performance rate) should own its own effective-rate composition, once
> that capability is built. Conditions are one *contributor* among possibly several — Activity context and,
> eventually, environment or age/biology are equally plausible contributors — offering an additive delta
> over the target's authored base value, never an absolute override. Whenever any contributor's active set
> changes, the target recomputes its effective rate from scratch as `base + Σ(all active contributions)`,
> clamped to its authored bounds, and runs its existing rearm/reschedule step
> (`NeedProgressionService.Rearm`, and a to-be-built equivalent replacement for
> `ActivityTransitionService.ApplyContextModifier`'s current last-write behavior) once against the new
> total.**

This keeps Condition from becoming a de facto central "modifier manager" for every system it touches — but
it means `ActivityTransitionService` in particular needs new state (something that remembers which
contributors are currently active and what each one asked for) that it does not have today, not just a new
caller. It's the same shape `SignalField`'s linear terms already use (sum of weighted contributions, then
one bounded/clamped result) and the same shape `WeightedTagSet`/`AffectState` already enforce
(`IntegerMath.Clamp(..., -10000, 10000)`) — the *math* is not new, but the place it needs to live, on both
target services, currently does not exist.

## 5.5 Behavioral expression: how a Condition reaches Social Appraisal without bypassing belief

An earlier draft of this section proposed an `EffectivePersonalityAt` overlay, read directly wherever
`Personality` is currently read "for appraisal purposes." That was checked against `SocialModel.md` for
this revision and does not hold: Social Appraisal in this architecture never reads a target's `Personality`
directly at all — not the ground-truth value and not an adjusted one. It reads the *observer's belief*
about the target, and that belief only moves in response to *observed behavior*
(`SocialModel.md`: "observed behavior changes belief rather than directly changing personality truth"; the
whole model is explicitly `TRUE PERSONALITY → OBSERVATION → OBSERVER'S BELIEF → APPRAISAL`, not `TRUE
PERSONALITY (+ modifiers) → APPRAISAL`). An `EffectivePersonalityAt` read at appraisal time would have
quietly given every observer — informed or not — omniscient access to Darius's current physical state,
which is exactly the kind of shortcut `SocialModel.md` §15 ("context should never rewrite the target's true
personality") was written to prevent, just approached from the appraisal side instead of the storage side.

The corrected mechanism keeps the same intent — a Condition should be able to make someone act differently
without touching who they actually are — but routes it through the belief pipeline that already exists,
rather than around it:

```text
TRUE Personality (untouched — never mutated by a Condition, never overlaid at read time)
        +
CURRENT active Condition's BehavioralExpressionModifier
        ↓
behavioral expression / Activity performance / observable Signal — WHAT THE CHARACTER ACTUALLY DOES
        ↓
OBSERVATION (SocialModel.md §8 — a noisy measurement of the behavior, not of the Condition)
        ↓
the observer's BELIEF about the target updates, or doesn't, exactly as it already would for any other
    misleading or informative behavior
        ↓
APPRAISAL reads the observer's belief, as it already does today — unmodified
```

Concretely: `Character.SetPersonality` remains the sole authorized mutation path for ground-truth
`Personality`, and Conditions still never call it — that constraint from the earlier draft was correct and
is unchanged. What's new is that `BehavioralExpressionModifier` deltas apply only at the point where a
Condition's effect becomes *observable behavior* — Activity performance, Decision Signals available to
other characters' appraisal-adjacent Considerations, and `ObservationEvidenceLabel`-tagged evidence fed into
§8's belief-update math — never as a direct substitute read at Social Appraisal's own evaluation site.
Appraisal itself changes nothing about how it works; it keeps reading `BeliefDistribution`, same as before
this brief existed.

This does cost something the earlier design didn't: an observer who already knows Darius is drunk (via
`DiscoverableThrough`, §5.3) doesn't get to discount the *appraisal* directly — they get to discount the
*evidence*, the same way `SocialModel.md` already lets any observer treat a piece of evidence as less
diagnostic when they have a reason to (§8's noisy-measurement framing). That is a strictly better fit for
the architecture than a direct appraisal override would have been, not a compromise.

## 5.6 Stacking and expiry

`StackingPolicy` (`Replace | Extend | Stack | Ignore-if-present`) is a per-`ConditionDefinition` authoring
choice, mirroring `DecisionReasoning.md` §17's `MutuallyExclusive | Supersedes | Merge | AllowStacking`
relation vocabulary for Considerations — the same design question ("do two instances of basically the same
pressure count twice?") recurring at a different layer, deliberately reusing the same small vocabulary
rather than inventing a second one.

## 5.7 Involuntary physical consequences — a narrow, explicitly product-sanctioned exception

`CoreIdentity.md` has been amended alongside this brief (§4, "Interference is not exclusively a player
action") to say plainly what an earlier draft of this brief treated as out of scope by default: a Need or
Condition severe enough can end an Activity, block a transition, or put a character to sleep without that
ever passing through Decision Reasoning — the same way the player's own physical Interference already can.
This was judged a design oversight, not a deliberate omission, once raised: consequences are a real and
intended part of how Needs and Conditions matter, and pretending every physical limit routes through a
chosen Option would understate what's actually at stake for a character running on empty.

This is a narrow exception, not a loophole, and it applies to a genuinely different thing than §4.4's
constraint:

```text
MOTIVATIONAL pressure (a Craving, an ordinary Need, most Conditions)
    → may only ever contribute a Desire and Considerations (§2–§4)
    → NEVER selects a voluntary action directly — CoreIdentity.md §4 (Influence), unchanged, absolute

PHYSICAL Condition consequence (severity crosses an authored, narrow line — collapse, forced sleep,
    an Activity that cannot continue)
    → MAY end an Activity, block a transition, or force sleep directly, exactly the way player
      Interference already can — CoreIdentity.md §4 (Interference), now explicitly extended
    → but MUST NEVER be recorded as a chosen outcome, and MUST carry the same causal honesty player
      Interference already requires: what the character was actually doing, and that it ended because
      it was imposed rather than decided
```

`ConditionDefinition.InvoluntaryConsequence` (§5.3) is deliberately its own field, separate from
`NeedRateDeltas`/`ActivityPerformanceRateDelta`/`DecisionSignalContribution` — the overwhelming majority of
authored Conditions should never set it. It exists for the small set of cases where a physical limit is
being crossed, not a motivational one being felt strongly.

---

# 6. Wiring Craving and Condition into Decision Reasoning

`DecisionSignalProviderRegistry` (`DecisionReasoningProgram.cs`) has twelve registered `IDecisionSignalProvider`
implementations — the full roster is enumerated at `DecisionSignalProviderIds.BuiltIns`:
`DecisionContextSignalProvider`, `ActorValueSignalProvider`, `ActorInterestSignalProvider`,
`TargetAvailabilitySignalProvider`, `RelationshipChannelSignalProvider`, `TravelBurdenSignalProvider`,
`ActivityModifierSignalProvider`, `CommitmentSignalProvider`, `SocialAppraisalSignalProvider`,
`SharedActivityContextSignalProvider`, `ActorNeedSignalProvider`, `CurrentActivityIdentitySignalProvider`.
(The parameterless `DecisionSignalProviderRegistry.WithBuiltIns()` convenience factory only registers ten of
these — `SocialAppraisalSignalProvider` and `ActorNeedSignalProvider` need catalog-scoped construction and
are registered by whatever composition root builds the production registry instead.)

An earlier draft of this brief claimed `AffectState` is "completely invisible to every Consideration in the
game." Checked directly against `SocialPressureEvaluation.cs`, that overstates it: `SocialFactorSourceKind`
already includes `ObserverAffect`, and `SocialPressureEvaluator.SourceValue` reads
`observer.Affect.ValueAt(rule.SourceId, world.Clock.Now)` directly, composing it into
`CompositeSocialEvaluationResult.NormalizedAppraisal` — a value `SocialAppraisalSignalProvider.Resolve`
returns as a live `SignalValue`. So `AffectState` already reaches at least one Consideration today, provided
content authors a `SocialFactorRule` with `SourceKind = ObserverAffect` into some `SocialPressureDefinition`.

What that path does *not* give a character is a standalone read of their own current Affect for a
Consideration unrelated to appraising a specific other person. `SocialAppraisalSignalProvider.Resolve`
requires a `target` character and a `SocialPressureId`/`AppraisalLensId` pair — it exists specifically to
compute "how do I feel about *them*, adjusted for my current state," not "what is my own Stress/Craving
right now." A Craving-driven Desire like "seek X" has no target character to appraise, so this existing path
cannot serve it. That's still a real gap, just a narrower one than "completely invisible" claimed — this
brief's single highest-leverage concrete recommendation is still a thirteenth provider, of a different shape
than the existing appraisal-mediated path:

```text
ActorAffectSignalProvider : IDecisionSignalProvider
    exposes AffectState entries (Stress, Fear, Loneliness, ... and new Craving kinds)
    as named Signals, following the exact shape ActorNeedSignalProvider already establishes
    for NeedState entries.

ActorConditionSignalProvider : IDecisionSignalProvider
    exposes active ConditionInstance.DecisionSignalContribution values while a Condition is active
    (e.g. "currently intoxicated" as a Signal a Consideration can read directly, distinct from its
    downstream behavioral-expression/Activity effects).
```

Once these exist, Considerations can be authored the same way every existing one already is — a
`social.obligation_aversion`-shaped binding, just reading a new signal source — with no change to
`ConsiderationDefinition`, `SignalField`, reason consolidation, or dice mapping. A worked example:

```text
DESIRE: "seek X" (a proposal, per §2, generated from rising Craving pressure)

Craving Signal (affect.craving.substance_x, rising)
    → Consideration: "SubstanceCravingPull" → candidate reason favoring Seek-X over Go-Home

Obligation Consideration (existing ObligationAversion, reading Values/Relationships as it does today)
    → candidate reason favoring Go-Home over Seek-X (an Inhibition, per §3, relative to the Seek-X Desire)

Condition: Withdrawal (if craving has gone unaddressed long enough — an authored escalation,
    itself a Condition applied by the same craving-threshold crossing §4.3 schedules)
    → ActorConditionSignalProvider exposes "currently in withdrawal" → strengthens the craving
      Consideration further
```

Every step above is an existing Consideration/Signal-provider pattern, just fed by two new sources — with
one exception verified this round, and it's a real one, not a formality.

## 6.1 `AffectProgressionService` must also repair `SocialAppraisalSignalProvider`'s dependency, or appraisal goes stale

`SocialAppraisalSignalProvider.Resolve` (§6, `DecisionReasoningProgram.cs`) already reads an observer's own
Affect today, through `SocialPressureEvaluator`'s `ObserverAffect` source (§6, above) — but checked directly,
the `ResolvedDecisionSignal` it returns registers exactly one `DecisionDependencyKey`: a `BeliefContext` key
scoped to the *target* character. Nothing in that method registers a dependency on the *observer's own*
Affect revision, even when the pressure definition being evaluated has an `ObserverAffect`-sourced rule
whose value the returned Signal actually depends on.

That's a latent gap today only because nothing currently changes `AffectState` with enough ceremony to
matter — `ApplyDelta`'s silent local-counter bump never triggered a reevaluation anyone was relying on. Once
`AffectProgressionService` (§4.2) exists and starts bumping a real `world`-scoped `RevisionKey` when Affect
changes, that gap becomes an active bug: a Decision holding a `SocialAppraisalSignalProvider`-sourced reason
would not reevaluate when the observer's own Stress, Fear, or Craving changed, even though the appraisal it
already computed depends on exactly that value. Fixing this is not optional cleanup — it's a required part
of shipping `AffectProgressionService`, not a separate follow-on: `SocialAppraisalSignalProvider.Resolve`
needs to additionally register a dependency on the observer's Affect `RevisionKey` whenever
`pressureDefinition.Rules` includes an `ObserverAffect`-sourced rule for the lens being evaluated. This is a
small, targeted change to one existing method, not new Decision-side machinery in the sense the rest of this
section claims — but it is a real code change this brief's Craving work requires, and it belongs in the same
slice as `AffectProgressionService` itself, not discovered later as a stale-appraisal bug report.

---

# 7. Determinism, save/load, and explainability

- `ConditionInstance` expiry is a scheduled event (`world.Scheduler`), never a per-tick scan — same
  discipline as `NeedState` and `ActivityInstance`. Derived expiries additionally need the
  revision-invalidate-and-reschedule discipline described in §5.2 whenever their underlying analytical
  value's rate changes.
- `AffectState.Set` on its own does **not** provide this today — verified it bumps only a local per-kind
  counter, with no `world`-scoped `RevisionKey` and no `world.BumpRevision` call anywhere in
  `CharacterSocialState.cs`. The new `AffectProgressionService` (§4.2) must bump a real `RevisionKey` itself
  so a Decision that depends on a Craving-derived Signal actually reevaluates through the existing
  `DecisionReevaluationService`/`DecisionDependencyIndex` path (dependency-indexed, never globally polled —
  invariant 38 applies unchanged) — this is a requirement on the new service, not a discipline the existing
  call already enforces. That reevaluation path only reaches Decisions that actually declared a dependency
  on the changed revision: see §6.1 for a specific, verified case (`SocialAppraisalSignalProvider`) where
  the dependency also needs to be added, not just relied upon.
- `BehavioralExpressionModifier` (§5.5) is pure and applied only where behavior/performance is computed —
  it needs no persistence of its own beyond the `ConditionInstance` list itself. That list does not
  "already round-trip" anything — it doesn't exist yet. What's true is narrower: it needs the same *kind*
  of explicit save/load work `Character.Traits` already went through (a DTO shape, a schema entry, a
  restore path), not automatic serialization by virtue of resembling an existing field. It never touches
  `Character.Personality` or `PersonalityRevision`, and Social Appraisal's own read path is unmodified by
  this brief.
- A resolved Decision's historical explanation snapshot (`DecisionReasoning.md` §33–§34) must capture
  whatever Craving/Condition Signal values were live at evaluation time, the same way it already captures
  every other Signal — no special case, since the new providers plug into the same
  `ConsiderationEvaluationSnapshot` pipeline everything else uses.
- An involuntary physical consequence (§5.7) must be recorded with the same causal honesty
  `CoreIdentity.md` already requires of player Interference: what the character was actually doing when it
  happened, and that it ended the way it did because it was imposed, not chosen. This is a save/history
  requirement, not a Decision Reasoning one — it never touches a Decision or its explanation trace, because
  it never went through one.
- Discovery: whether the player (or another character) *knows* someone is drunk, addicted, or in
  withdrawal is a Knowledge fact, not a free read of `ConditionInstance`/acquired-drive state — reuse
  `DiscoveryChannel` (`TraitDefinition.cs` already has this exact pattern for Traits) rather than exposing
  Condition state omnisciently.

---

# 8. Worked torture cases

1. **Two characters, identical Craving intensity, different outcomes.** Mina and Darius both have the same
   acquired-drive intensity for `substance_x`, so `affect.craving.substance_x` rises at the same rate for
   both. Mina's `ObligationAversion` and `values.family` Considerations are strong; Darius's are weak. Same
   Desire, same Craving Signal, different Inhibitions, different resolutions — no bespoke rule; the
   existing Consideration/Signal machinery produces the divergence for free.
2. **Drunk in public, correctly.** Darius is intoxicated at a gathering. His `BehavioralExpressionModifier`
   makes him visibly less attentive and more impulsive in his actual behavior — nothing about his stored
   `Personality` changes. An observer who witnesses that behavior forms or updates a belief about him the
   normal way (`SocialModel.md` §8): if she already knows he's drunk (Knowledge/Discovery), she can treat
   the behavior as weaker evidence and largely preserve her prior belief; if she doesn't know, her belief
   about his *true* dispositions shifts incorrectly, exactly the epistemic consequence the belief model
   already produces for any other misleading evidence — unmodified, because appraisal never touched
   Darius's Condition or Personality directly. Nothing here required an appraisal-time overlay.
3. **Mina hesitates to approach Glen, and the hesitation can actually win.** Connection pressure proposes
   the Desire "approach Glen." Considerations run: wanting company (+), Glen usually being comfortable (+),
   fear of embarrassing herself (–, an Inhibition relative to this Desire). Because the routine also offers
   an explicit "keep reading" candidate (§3.2), Mina's Inhibition can genuinely win arbitration rather than
   being structurally unable to stop a single-candidate Desire from winning by default — this is precisely
   the gap §3.2 identifies in the current single-candidate call sites.
4. **Exhausted suppresses Recreation motivationally — and, past a line, physically.** "Exhausted" applies a
   `NeedRateDelta` further depressing Energy recovery and an `ActivityPerformanceRateDelta` on any active
   Recreation Activity — both ordinary motivational effects, reachable through existing mechanisms
   (`NeedProgressionService`, `ActivityContextModifier`), composed per §5.4. If Energy falls far enough
   past that, the same Condition's `InvoluntaryConsequence` (§5.7) may end the current Activity and force
   sleep — a physical outcome, never a Decision, and recorded with the same causal honesty player
   Interference already requires: *Mina collapsed mid-shift*, not *Mina chose to stop working*.
5. **Withdrawal escalates without ever forcing a choice.** Craving crosses an authored threshold
   unaddressed; a scheduled crossing (§4.3) applies a `Withdrawal` Condition, which strengthens the Craving
   Consideration (§6) and adds a `NeedRateDelta` (harder to concentrate → Competence-adjacent Considerations
   weaken). The character still chooses at every subsequent Desire/Decision — Withdrawal never calls
   `ActivityTransitionService.BeginActivity` directly, because Craving and Withdrawal-as-motivational-state
   are still governed by §4.4's absolute rule, unchanged by §5.7's narrower physical exception.
6. **Two identical "Drunk" instances don't double-stack accidentally.** `StackingPolicy.Replace` (or
   `Extend`, authoring's choice) on the `Drunk` `ConditionDefinition` means a second drink extends duration
   rather than doubling the `BehavioralExpressionModifier` delta — the same "don't silently double-count
   correlated evidence" discipline `DecisionReasoning.md` §16 already requires of Considerations, applied to
   Condition stacking instead.

---

# 9. Explicit non-goals

This brief does **not**:

- define the Regulation & Development Model Brief's `RegulatoryProfile` (durable per-domain sensitivity) —
  that remains genuinely new state with no existing precedent (Regulation Brief Finding 3) and is out of
  scope here, though §4.1 notes Addiction's acquired-drive container would fit naturally inside it once it
  exists;
- resolve whether `need.social`/Connection should migrate off homeostatic `NeedState` (Regulation Brief
  Finding 1) — Craving's use of `AffectState` (§4.2) is a second, independent proof that
  experience-accumulating-shaped pressure belongs on an analytical-progression container rather than
  `NeedState`, which is evidence toward that migration but does not decide it;
- specify Culture, Development/drift, or population generation — unrelated layers;
- invent a general `AffectState`-wide threshold-scheduling system — only Craving (and any future Affect
  kind explicitly meant to gate behavior) gets one, per §4.3's narrower recommendation;
- change `ConsiderationDefinition`, `SignalField`, reason consolidation, dice mapping, or any existing
  Decision Reasoning type. Every new capability in this brief is a new *content* shape
  (`ConditionDefinition`, two new `IDecisionSignalProvider`s) consumed by unmodified existing machinery;
- change how Social Appraisal reads its inputs — §5.5 was corrected specifically to comply with
  `SocialModel.md`'s existing belief-mediated pipeline rather than add a second read path around it;
- **claim that several concurrently-pressing Needs get reconciled into one arbitration pass today.** This
  was checked directly against `RecreationRoutineService`/`SocializingRoutineService`/
  `DecisionReasoningPreflightResult` for this revision: each routine builds and arbitrates only its own
  Need's candidates; a `NeedThresholdReachedEvent` for Hunger and one for Connection are each handled by
  their own routine service independently, and the only existing tie-break for the shared "character is
  currently Waiting" slot is causal ordering (whichever scheduled event fires first) plus a sorted-by-id
  precedence loop in `RecreationRoutineService.TryStartDeferred` — not a joint evaluation of both Needs'
  Desires together. If Vivarium eventually wants several simultaneous Desires from different Needs weighed
  against each other in one pass, that is unbuilt routine-dispatch work with no existing precedent to
  extend, and this brief's vocabulary changes (Desire, Inhibition) do not by themselves create it. Worth a
  deliberate design pass of its own rather than being assumed into existence here;
- **claim that every routine already follows the propose-then-arbitrate shape §2 defines.** Verified only
  for `RecreationRoutineService`. `SocializingRoutineService`'s requester-side selection currently has no
  arbitration step at all (§2.1) — bringing it in line with this brief's vocabulary is unbuilt work, not a
  documentation update;
- **claim `AffectState` is unreachable by Decision Reasoning today.** It already reaches
  `SocialAppraisalSignalProvider` through an `ObserverAffect`-sourced rule (§6) — this brief's contribution
  is a direct, non-appraisal-mediated read of an actor's own Affect, not the first path in, and its
  `AffectProgressionService` recommendation (§4.2) is what's actually new, not the signal exposure alone.

---

# 10. Future implementation decomposition

This is a decomposition of *this brief's* scope into shippable slices — not a claim about where it sits in
the overall production schedule; that call belongs to whoever owns the roadmap, not this document. An
earlier revision described the Rest-vs-Recreation slice as "already in flight," citing only that
`NeedContinuationRoutineDefinition` exists in the content catalog. That evidence shows the *content schema*
for continuation routines exists; it does not show a consuming service has shipped, and this brief has no
independent way to confirm the slice's actual completion status from the code alone. Treat the project's own
tracking as authoritative on that, not this brief.

The earlier version of this decomposition was organized around capability layers (all signal-provider work,
then all progression work, then all Condition work) — infrastructure-first, in an order that leaves several
early steps unused until later ones land. A vertical-slice ordering serves the codebase's own discipline
better: each slice should be one concrete, observable behavior change, built end-to-end, adding only the
infrastructure that specific behavior actually demonstrates.

```text
1. One concrete Affect-driven choice, end-to-end:
     - ActorAffectSignalProvider (§6) — direct actor-scoped read.
     - AffectProgressionService (§4.2) — real-rate AffectState entry, world-scoped RevisionKey, Scheduler
       wiring, narrow threshold crossing (§4.3) for this one Affect kind.
     - the SocialAppraisalSignalProvider dependency fix (§6.1) — required in this slice, not deferred,
       because AffectProgressionService is what makes the staleness it fixes observable in the first place.
     - one authored Consideration reading the new signal, and one routine's Desire that it actually changes
       the outcome of.
   No new persistence work needed here: AffectState.Restore already round-trips any AuthoredId kind, so a
   new Affect kind is a content addition, not a schema change — unlike Condition (step 3).
2. Inhibition naming/authoring convention (§3.3) + explicit decline candidates where non-enactment is
   meaningful (§3.2) — mostly documentation/content; §3.2's one real code implication
   (RecreationRoutineService-style single-candidate call sites can't currently let Inhibition win at all)
   can ship independently of step 1.
3. One concrete, non-forcing Condition on ONE target only — recommend Need rate over Activity performance
   rate as the first target, since NeedProgressionService's rate-setting is simpler and less entangled with
   travel/transit than ActivityTransitionService's. This slice adds only what that one target demonstrates:
     - ConditionInstance storage (§5.2) — new persistence, unlike step 1's Affect kind: a real DTO shape,
       schema entry, and restore path, the same kind of work Character.Traits already has, not automatic.
     - one ConditionDefinition (§5.3), fixed-duration expiry only — defer analytically-derived expiry's
       extra revision-invalidate-and-reschedule discipline until a Condition actually needs it.
     - effective-rate composition (§5.4) for Need rate specifically, not both targets — build the
       capability the one target needs, not a general system speculatively covering the other one too.
     - DiscoverableThrough/DiscoveryChannel wiring (§5.3, §7) — even one Condition needs this to be
       meaningful; it's cheap and reuses TraitDefinition's existing pattern exactly.
4. Expand Condition to a second target only once a real behavior needs it — likely Activity performance
   rate, which is where effective-rate composition (§5.4) has to be built a second time (it is not
   automatically reusable across NeedProgressionService and ActivityTransitionService, which are separate
   services with separate rate-setting methods today). This is also the natural point to add:
     - BehavioralExpressionModifier (§5.5) — once a Condition exists that should be externally visible.
     - ActorConditionSignalProvider (§6) — once a Condition should contribute a Decision Signal directly.
     - Trait-projection interaction, if any Condition's authored effect needs to influence a projected
       Trait rather than only raw personality dimensions (§5.1's table flags this as unverified this pass).
5. AcquiredDrives (§4.1) — Addiction's own WeightedTagSet-shaped container, separate from Values/Interests.
   Independent of steps 1–4: no progression, no scheduling, just a new durable container. Slot it in
   whenever content first needs an authored Addiction, not necessarily in this numeric order.
6. InvoluntaryConsequence (§5.7) — deferred until the project's own intent/outcome provenance foundation
   exists (per this round's review, that foundation is separate, later work — "Phase 9" in that reviewer's
   own terms, which this brief has no independent visibility into and is relaying rather than verifying).
   It's the one capability here that can end an Activity without a Decision ever running, and
   `CoreIdentity.md`'s companion principle should be read alongside whatever authors the first Condition
   that uses it.
```

Steps 1–2 and step 5 are independently shippable in any order relative to each other. Steps 3–4 are
sequential — step 4 depends on step 3's `ConditionInstance` existing — and step 4 should not start until a
specific authored Condition actually needs a second target, not on the assumption that it eventually will.
Step 6 is intentionally last and gated on work this brief does not own.

---

# 11. Architectural invariants (local to this brief)

1. A Desire is an ephemeral candidate proposal built before arbitration, not arbitration's output. It is
   never persisted as its own entity.
2. Arbitration's output is the Preference, not the Desire — the two must not be named or defined the same
   way, or Inhibition becomes incoherent.
3. An Inhibition is a negative-signed Consideration relative to a specific Desire's Option, not a distinct
   Domain type, persisted field, or dedicated `ReasonChannelId` namespace.
4. When non-enactment is a meaningful and physically available outcome, a routine's candidate set must
   represent it explicitly — arbitration has no implicit "decline everything" floor (verified:
   `DecisionReasoningPreflightResult` always selects the max of whatever options it was given).
5. Addiction is durable acquired state, stored in a `WeightedTagSet`-shaped container of its own — never
   inside `Character.Values` or `Character.Interests`. Craving is continuous acquired pressure
   (`AffectState`-shaped, with a real rate, semantic ownership treated as provisional per §4.2). Neither is
   a `Character.Needs` entry.
6. Motivational pressure (Craving, an ordinary Need, most Conditions) may never bypass Decision Reasoning
   to directly compel a chosen Activity transition; it may only contribute Desires and Considerations
   (`CoreIdentity.md` §4, Influence).
7. A narrow, explicitly-flagged subset of severe physical Conditions may force an involuntary physical
   outcome — ending an Activity, blocking a transition, forcing sleep — the same way player Interference
   already can (`CoreIdentity.md` §4, Interference, extended). This is never the same pathway as invariant
   6, and the two must never be conflated in either authoring or code.
8. Any involuntary physical outcome must be recorded with the same causal honesty player Interference
   already requires: what the character was doing, and that the outcome was imposed rather than chosen.
9. A Condition never mutates `Character.Personality` or `PersonalityRevision`. Its effect on how other
   characters perceive the affected character is expressed only as observable behavior, fed into
   `SocialModel.md`'s existing belief/observation pipeline — never as a direct read at Social Appraisal's
   own evaluation site.
10. Simultaneous contributors (Conditions, Activity context, and any future source) affecting the same
    target must compose as additive deltas over the target's authored base value, recomputed in full by the
    target itself on every change to the active contributor set — never as last-write-wins overrides, and
    never owned by the Condition side of the relationship. Verified this capability does not exist at
    either target service today (`NeedProgressionService`, `ActivityTransitionService` are both currently
    last-write absolute-rate setters, §5.4) — this invariant governs what must be built, not what already
    holds.
11. Condition expiry is a scheduled event, never a per-tick or per-poll check. A derived expiry additionally
    requires the same revision-invalidate-and-reschedule discipline as any other derived future event.
12. Whether an observer knows about an active Condition is a Knowledge/Discovery fact, not a free read of
    authoritative Character state.
13. New Craving/Condition signal sources integrate through `IDecisionSignalProvider`, consumed by unmodified
    `ConsiderationDefinition`/`SignalField`/reason-consolidation machinery — no Decision Reasoning type
    changes to accommodate them.
14. Multiple concurrently-pressing Needs are not assumed to be reconciled into one arbitration pass unless
    and until that dispatch mechanism is separately designed and built — this brief's vocabulary does not
    itself create that capability (verified absent from the routine code as of this revision).
15. This brief's Desire/Inhibition vocabulary (§2–§3) is verified against `RecreationRoutineService` only.
    A routine that does not yet build multiple candidates and arbitrate them (`SocializingRoutineService`'s
    requester-side selection, as of this revision) does not automatically gain Inhibition-can-win behavior
    just because this brief exists — each such routine needs its own pass to adopt the pattern.
16. Any new source of a `RevisionKey` bump that an existing `IDecisionSignalProvider` already reads through
    indirectly (verified case: `SocialAppraisalSignalProvider` reading `ObserverAffect` without depending on
    it, §6.1) must have that provider's dependency declaration extended to match, in the same slice that
    introduces the new revision source — never assumed to already be covered by an existing dependency that
    was scoped for a different reason.

---

# 12. Guiding principle

> **A person does not have a want and a resistance as two different kinds of thing. They have one mind
> producing reasons that point in different directions — some from pressure that has been building for
> years, some from a value they've held since childhood, some from exhaustion that will be gone by morning.
> Vivarium already has the machinery that turns many reasons into one legible choice. Desire, Inhibition,
> and Condition are not new minds bolted onto that machinery. They are what it looks like when a person
> wants something they also have reason not to take — and, sometimes, when the body they live in stops
> asking their mind's permission at all. Both are true of real people. The simulation should be able to
> tell the difference between them, and never pretend the second one was the first.**
