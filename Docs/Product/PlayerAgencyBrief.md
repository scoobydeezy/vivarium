# Vivarium — MVP Player Agency Brief

**Status:** Locked MVP product contract  
**Last reconciled:** 2026-08-24  
**Purpose:** Define what the player can understand and do in the Minimum Playable Scenario without
directly choosing character outcomes.

This brief locks the deliberately small MVP subset of the broader influence/interference progression
defined by Core Identity. It does not implement physical coercion, transfer, Observer belief, culture,
or multi-habitat power.

**Depends on:** [`../../README.md`](../../README.md), [`CoreIdentity.md`](CoreIdentity.md),
[`../Architecture.md`](../Architecture.md), [`../Architecture/Reference.md`](../Architecture/Reference.md),
[`../IMPLEMENTATION_GUIDELINES.md`](../IMPLEMENTATION_GUIDELINES.md),
[`MinimumPlayableScenario.md`](MinimumPlayableScenario.md), and [`Roadmap.md`](Roadmap.md).

---

## 1. Player promise

The MVP player is an attentive steward of a small autonomous community.

The player may:

- choose where to look;
- inspect what is knowable about a character, location, plan, or Decision;
- decide whose life deserves proactive attention;
- buy time to consider a qualifying Decision;
- spend a scarce **Nudge** to change the weight of one known reason;
- open or close the Commons, changing a real circumstance to which characters react.

The player may not select a Decision Option, assign a character an Activity, teleport a character,
edit a Need, or directly change a Relationship. Characters remain the owners of their choices and
routines.

The MVP loop is:

```text
look → learn → follow → notice → inspect → optionally hold/nudge
     → character resolves → consequences continue → recap → look again
```

---

## 2. Supported player actions

| Player action | MVP behavior | Authority boundary |
|---|---|---|
| View location | Show one of Residential, Bakery, or Commons | Presentation selection; emits semantic observation transitions |
| Select/inspect character | Open the character profile and observe discoverable facts | `InspectCharacter`/canonical `WatchState`; never reveals raw truth directly |
| Follow/unfollow character | Persistently prioritize a character and include them in the canonical watch signal | Validated Command; saved |
| Set Normal / Auto-Hold / Quiet | Choose proactive Decision handling for a character | Validated Command; saved |
| Inspect Decision | Show known Options, reasons, resolve time, and prior changes | Knowledge-filtered read model |
| Hold/release Decision | Defer eligible auto-resolution within bounded capacity; release restores scheduling | Validated Commands; saved; an immovable ceiling (§4) still wins where one exists |
| Emphasize/Temper reason | Spend one Nudge to step one visible reason up/down | Validated Command; never selects the winning Option |
| Open/close Commons | Spend one Nudge to change authoritative Commons availability | Validated Command; saved; causes targeted replanning |
| Review recap/history | Explain important off-screen/offline changes the player is allowed to know | Knowledge-filtered projections |

`TravelCharacterCommand`, `BuildLocationCommand`, raw time advancement, and scenario/debug controls are
not player-facing MVP verbs.

---

## 3. Inspection surfaces

### 3.1 World and location

The world surface shows simulation time, current Nudge balance, current viewed location, and the three
location selectors. The selected location shows:

- display name and open/closed state;
- current visible occupants and Activities;
- Activity affordances available there;
- imminent public arrivals/departures where player Knowledge permits;
- the Commons management control and its cost/rule explanation.

Changing the viewed location changes observation, not simulation truth.

### 3.2 Roster and character profile

The always-available roster shows every MPS character, current known Activity/location summary,
Follow state, Attention policy, and whether an important Decision needs attention.

The character profile contains:

- current Activity and spatial context, including Travel progress;
- known Needs with observation age/confidence;
- materialized schedule and conflicts;
- known relationships and relevant directional history;
- player Knowledge, including uncertainty and staleness;
- active and recently resolved Decisions;
- recent character history/recap entries.

Unknown facts remain unknown. Inspection may trigger ordinary discovery, but the profile never binds to
mutable Domain state or bypasses Knowledge filtering.

### 3.3 Decision feed and detail

The Decision feed is the ongoing inbox for unresolved and recent important Decisions. It shows the
character, importance, Hold state, time remaining until its current `ResolveAt`, and a ceiling warning
when that Decision has one.

Decision detail shows only player-visible Options and reasons. Each visible reason independently shows
the most specific label/explanation and die magnitude the player's Knowledge permits. It also shows:

- whether Hold is legal and remaining global/per-character capacity;
- whether this Decision's `ResolveAt` has an immovable ceiling Hold cannot push past (every Decision has
  a `ResolveAt`; most MVP types do not yet define a ceiling — see §4);
- Emphasize/Temper eligibility and cost using the authoritative rule result;
- applied interventions and any refund;
- frozen post-resolution reasoning and outcome provenance.

The UI never exposes a hidden-reason count or duplicates eligibility logic.

---

## 4. Follow, Watch, Auto-Hold, and Quiet

These terms have distinct meanings.

### Watch

**Watching** is a derived, ephemeral status. A character is watched while meaningfully visible,
selected, inspected, or Followed. Presentation emits state transitions rather than per-frame Commands.
Ephemeral visibility/selection/profile state is cleared on load.

### Follow

**Follow** is a durable per-character toggle. It keeps a character in the player's priority roster,
increases surfacing priority, and participates in the canonical observation signal. It does not move
the camera, pause simulation, or automatically Hold Decisions.

### Auto-Hold

**Auto-Hold** is a durable per-character Attention policy. In Live mode **and in player fast-forward**
(`Architecture/Reference.md` §21.1 — the player is still present in both, only offline is genuine
absence), a newly created Decision is automatically Held only when it is `HoldEligible` and its
`Importance` clears the Auto-Hold cutoff. `Importance` is derived from the Decision's own evaluated
Signal magnitude, not an authored constant (`Architecture/Reference.md` §18.2, `DecisionReasoning.md`
§39.2); the earlier placeholder "20 or greater" described a raw designer-set scale that no longer applies
now that the mechanism is magnitude-derived, and is retired rather than corrected, since the real cutoff
has to be set against that new scale, not the old one (open MVP parameter — see §14). The player can
still manually Hold any eligible lower-importance Decision they inspect.

Mina begins the MPS with Auto-Hold enabled. Every other character begins in Normal. Auto-Hold uses the
same bounded global/per-character capacity and deterministic overflow rules as manual Hold (the actual
capacity numbers are also an open MVP parameter — see §14). "Auto-Hold" means the Decision's `ResolveAt`
(`Architecture/Reference.md` §18.1) is deferred, not paused outright; for the MVP's commitment-conflict
Decisions that deferral still cannot cross the derived hard feasibility deadline. Other MVP Decision
types do not currently define an equivalent ceiling, so their `ResolveAt` can be deferred by repeated
Holds up to held-decision capacity, same as manual Hold.

### Quiet

**Quiet** is a durable per-character Attention policy. It suppresses proactive live interruption and
notification, but not simulation, history, discoverable Knowledge, or later inspection. Quiet never
changes Decision timing or outcome. Opening a Quiet character's profile still allows manual Hold where
legal.

Normal, Auto-Hold, and Quiet are mutually exclusive policies. Follow is an independent toggle. The UI
labels the derived Watch state rather than presenting it as a fourth durable policy.

### Changing Attention policy does not touch an already-Held Decision

Attention policy governs whether a *newly created* Decision gets proactively Held, and whether
notifications fire for this character going forward. It does not reach back and change a Decision that
is already Held. If Mina (Auto-Hold) has a Decision Held and the player switches her to Quiet, that
Decision stays Held exactly as it was — Quiet only suppresses notification for anything from that point
on. Outside ordinary resolution and deterministic held-capacity overflow, the only way to give up an
existing Hold is the Release action (§2). Importance reevaluation may change which Held Decision loses a
slot when later overflow occurs, but crossing the Auto-Hold floor alone never retroactively Holds or
releases anything. This mirrors the general architecture's separation between a character-level Attention
policy and per-Decision Hold state (`Architecture/Reference.md` §20): they are different axes, and one does
not implicitly reset the other.

### Pausing is a time control, not a fourth Attention policy

The MVP's world HUD and time controls (§11) include a genuine simulation pause: SimTime stops advancing
entirely. That is the mechanism for "stop everything so I can think about just this Decision" — the
player pauses, inspects and intervenes at leisure, then resumes. It is deliberately **not** modeled as a
fourth Attention-policy value, because a Decision-scoped policy that froze the rest of the simulation
would contradict the general rule that unresolved Decisions never freeze unrelated world simulation
(`Architecture/Reference.md` invariant 34). Pausing already gets the same practical effect for free: while
SimTime is stopped, no `ResolveAt` can be reached and no Need threshold can cross, for anyone, without
inventing any new mechanism.

---

## 5. Decision surfacing and recap

Rows below apply identically in Live and player fast-forward unless stated otherwise — both are modes
where the player is present (`Architecture/Reference.md` §21.1). OfflineCatchUp is the only mode where
the player is genuinely absent.

| Context | Live/fast-forward presentation | Authoritative behavior |
|---|---|---|
| Viewed/inspected character | Immediate feed update and local notification | Normal simulation; Auto-Hold may apply |
| Followed but off-screen | Immediate feed update and notification | Normal simulation; Auto-Hold may apply |
| Unwatched Normal character | Important Decision enters inbox without camera interruption | Resolves at its `ResolveAt` unless manually Held in time |
| Quiet character | No proactive notification | Resolves at its `ResolveAt`; eligible history remains available |
| Decision's `ResolveAt` reached with no ceiling | Urgent resolution/recap entry | Resolves even if Held, unless deferred again in time |
| Decision's ceiling reached (commitment-conflict only, §4) | Urgent resolution/recap entry | Resolves even if Held — this deadline cannot be deferred further |
| OfflineCatchUp | No live notifications and no new Hold | All Decisions resolve under ordinary physical rules; summary appears afterward |

Fast-forward notification delivery may be paced/coalesced for presentation reasons without changing which
authoritative events occur (`Architecture/Reference.md` §21.1).

After OfflineCatchUp, one grouped recap reports material Activity/location changes, Need crossings,
created/resolved Decisions, Commitment outcomes, social consequences, and Commons availability changes,
filtered through player Knowledge. Routine repetition is summarized rather than emitted as an unbounded
event list. Selecting a recap entry opens the relevant character, Decision, or location when retained.

---

## 6. Nudge economy

Intervention is normal MVP play through a single deterministic resource named **Nudges**.

### Balance and regeneration

- A new MPS begins with **3 Nudges**.
- The cap is **3**.
- One Nudge regenerates at each eight-hour world-time boundary: `00:00`, `08:00`, and `16:00`.
- Regeneration uses simulation time and applies identically during Live, fast-forward, and
  OfflineCatchUp.
- Current balance is authoritative save state. The next boundary is derived from simulation time.
- Time spent while already at cap is not banked.

### Costs and intervention content

The MVP exposes exactly two Decision interventions:

- **Emphasize** — cost 1; step one visible active reason's die up once.
- **Temper** — cost 1; step one visible active reason's die down once.

Each intervention may target a given Influence at most once. The existing stable Influence identity and
authoritative `DecisionInterventionRules` remain the legality authority. Resource sufficiency becomes
part of that same evaluation path so projections and command execution agree.

Opening or closing the Commons costs **1 Nudge** when the command actually changes its state. An invalid
or no-op Command costs nothing.

### Refunds

- A failed Command never spends a Nudge.
- A Decision intervention is refunded if the Decision dissolves without resolution because its Option
  set becomes invalid.
- Resolution, manual Release, a retracted reason, or player regret does not refund it.
- An accepted Commons state change is never refunded.
- Refunds are authoritative, capped at 3, persisted through the resulting balance, and shown in
  history/recap.
- The cap is enforced by clamping balance after **every** individual balance-affecting event (a spend, a
  refund, or a regeneration tick) — not only checked at regeneration boundaries. A refund and a
  regeneration boundary can coincide at the same simulation instant (a Decision dissolving exactly at
  `00:00`/`08:00`/`16:00`); per-event clamping means the final balance is the same regardless of which one
  the general same-instant settlement order (`Architecture/Reference.md` §11.4) happens to apply first, so
  this case needs no special-cased tie-break of its own.

---

## 7. First environmental management lever: Commons availability

The Commons has authoritative `Open`/`Closed` availability. The player may issue a real management
Command to change it for one Nudge.

The rule is deliberately narrow:

- Closed prevents new Travel/plans whose required discretionary Activity depends on the Commons and
  prevents beginning those Activities there.
- Closing does not teleport occupants or cancel unrelated Commitments.
- Closing does not abort an Activity already actively under way at the Commons; that Activity keeps its
  committed snapshot and finishes normally — the affordance change reaches it only as "this cannot be
  started/renewed again while Closed," not as an interruption of something already happening.
- A character already Traveling **toward** the Commons for an Activity that depends on it is a different
  case: that Travel is targeted for revalidation the same way an active Decision would be
  (`Architecture/Reference.md` §29.8, generalized beyond this one lever). Ordinary planner reaction may
  redirect them before arrival rather than letting them complete a now-pointless trip and discover the
  precondition failed on arrival. This is the general in-progress-Activity revalidation mechanism, not a
  Commons-specific special case — the same mechanism governs any Activity, travel or otherwise, whose
  in-flight assumptions a world change invalidates.
- Reopening restores affordances and invalidates only availability-dependent plans/reasons.
- The state change bumps an aspect-scoped availability revision and triggers targeted replanning or
  living-Decision reevaluation; it does not globally scan every relationship or Decision.
- A visible location badge, disabled-state explanation, history entry, and recap entry expose the
  consequence.

This lever proves “alter circumstances, not outcomes”: closing the Commons can redirect Recreation,
Travel, social opportunity, and later Decisions, but the player never tells Owen where to go or whom to
meet.

The MPS acceptance fixture requires two branches beyond the open control: one where the player closes the
Commons before Owen's evening planning window (no in-flight Travel to react to) and one where the player
closes it **while Owen is already Traveling toward the Commons** for a Commons-dependent Activity, proving
the reactive-revalidation path specifically rather than only the plan-formation-time path. At least one
downstream Activity or social outcome must differ for a causal reason in each, and reopening restores the
affordance in both.

---

## 8. Meaningful MPS Decisions

The jointly locked MPS expects these Decision families and no additional dramatic choice solely to
make the feed busy:

| Decision | Typical attention behavior |
|---|---|
| Hunger vs uninterrupted Work | Mina Auto-Holds; an equivalent non-Mina case resolves autonomously |
| Dinner vs Closing Duty commitment plan | Mina Auto-Holds; hard deadline still resolves it |
| Social approach/avoid interaction | Usually autonomous; surfaced when watched/followed |
| Later Reliance choice | Autonomous comparison showing prior accountability changed reasoning |

Sleeping/waking, ordinary Eating, routine Work, and ordinary Recreation/Socializing remain planner or
Activity behavior unless their circumstances create a genuinely meaningful branch. They must not
generate Decisions merely for presentation coverage.

---

## 9. Interactive Activity decision

No interactive Activity or mini-game is in MVP scope.

Every Activity resolves autonomously, including while watched or offline. The existing normalized
`SubmitActivityPerformanceCommand` seam remains architectural proof for later content, but no MVP UI
surface exposes it. This avoids building a second interaction mode before the daily-life loop is
playable and legible.

---

## 10. Persistence and determinism contract

The save must preserve:

- Follow and Normal/Auto-Hold/Quiet policies;
- Held Decisions and bounded-capacity state already required for continuation;
- Nudge balance;
- Commons availability and its revision;
- intervention applications/refunds and causal history.

Ephemeral viewed location, visible/selected/profile-open flags, open panels, animation, and notification
toast state are Presentation state and are not authoritative. On load, the UI chooses a default viewed
location and rebuilds projections from the restored world.

Uninterrupted simulation, save/load continuation, and OfflineCatchUp must produce the same authoritative
world for identical Commands and time. OfflineCatchUp may summarize presentation differently but may not
grant free Holds, skip Nudge regeneration boundaries, or simplify physical behavior.

---

## 11. Required MVP Unity surfaces

The minimum playable UI consists of:

1. world HUD and time controls;
2. one-location-at-a-time world view with location selector;
3. virtualized character roster;
4. character profile with Overview, Schedule, Social/Knowledge, Decisions, and History sections;
5. Decision feed and Decision detail/encounter panel;
6. Nudge balance and authoritative eligibility/cost feedback;
7. Commons open/close management control;
8. live notifications and grouped offline/off-screen recap.

These surfaces consume read models and send semantic Commands. They do not inspect or mutate Domain
entities directly.

---

## 12. Acceptance contract

The MVP agency loop is complete when a deterministic two-day MPS run proves all of the following:

1. The player changes viewed locations while every character continues to simulate.
2. Inspecting Mina reveals only player-known state and updates the canonical watch signal.
3. Follow persists across save/load; ephemeral Watch inputs do not.
4. Mina's qualifying Decision Auto-Holds identically in Live and in fast-forward; an equivalent
   non-Mina Decision resolves autonomously.
5. The player can Release a Held Decision. A Decision's `ResolveAt` still forces resolution once no
   further deferral applies, and a commitment-conflict Decision's ceiling forces resolution even if still
   Held.
6. Emphasize or Temper spends one Nudge, changes a stable reason, and can change—but cannot select—the
   deterministic outcome.
7. Nudge regeneration and dissolution refund behavior match across uninterrupted, save/load, and
   offline runs, including when a refund and a regeneration boundary coincide.
8. Closing the Commons spends one Nudge, changes authoritative availability, and causally changes a
   downstream plan/Activity/social opportunity both for a plan formed after closure and for a character
   already Traveling toward the Commons when it closes; reopening restores the affordance.
9. Quiet changes surfacing only, never simulation or outcome.
10. Off-screen and offline important events appear in a bounded, Knowledge-filtered recap.

---

## 13. Explicitly deferred

The MVP does not include direct character orders, physical Interference in any form — picking a
character up, redirecting or isolating them, or transferring them between habitats (CoreIdentity.md
§4–5, §18–19) — construction, a broad economy, purchasable Nudge upgrades, observation-driven Nudge
farming, queued location schedules, forced Activity cancellation, multiple managed locations,
player-authored Commitments, interactive Activities, or final notification tuning.

Everything in this brief is Core Identity's **Influence** category only: the player changes the weight
of a reason inside a Decision the character still resolves. **Interference** — the player physically
overriding what happens regardless of that resolution — is entirely out of MVP scope and remains
post-MVP Roadmap Phase 9 ("The Poke").

Expand these only when a later playable failure proves they are necessary.

---

## 14. Open MVP parameters

Numeric parameters this brief depends on are named here but not yet assigned values anywhere in the
documentation, and must be locked before Phase 3 (Roadmap.md) implementation can complete:

- **The Decision importance scale.** `Architecture/Reference.md` §18.2 and `DecisionReasoning.md` §39.2
  (2026-08-24) now fix the *mechanism*: `Importance` is derived from a Decision's own evaluated Signal
  magnitude, recomputed on reevaluation, not an authored constant. What they deliberately do not fix is
  the actual cutoff number(s) — the Auto-Hold threshold (§4) and the overflow-ordering comparison
  (`Architecture/Reference.md` §20) both read from this same derived value, and both need a real number
  set against the magnitude scale `Evaluation/SignalField` actually produces. The retired "20 or greater"
  placeholder was written against a raw authored-integer scale that no longer exists, so it is not a
  starting estimate for the new cutoff — treat this as unset, not as "was 20, now needs adjusting."
  **Current state (2026-08-24):** none of the three shipped Decision generators — Need-threshold
  (`DecisionGeneration.cs`), social-interaction (`SocialDecisionGeneration.cs`), or commitment-conflict
  (`CommitmentConflictDecision.cs`) — derive this yet. Each just forwards its `DecisionDefinition`'s
  static, authored `Importance` (default `0`) straight into the generated `Decision`, unchanged by
  reevaluation. That's not a bug today — Auto-Hold and overflow eviction, the only consumers of
  `Importance`, are both still unimplemented (`ImplementationStatus.md`'s "MVP agency contract" bullet
  names Normal/Auto-Hold/Quiet policy semantics as not yet built) — but it means the derivation has no
  code yet and is a real dependency of Phase 3, not a small tune-up of something already working.
- **The feed thresholds.** [`DecisionImportance.md`](../Design/DecisionImportance.md) defines separate
  Normal and prioritized feed floors on the same derived scale. Watched/Followed Decisions may qualify at
  the no-higher prioritized floor; Quiet suppresses proactive surfacing regardless of magnitude. Both
  numeric values remain unset until representative Decision distributions can be measured.
- **The Decision admission floor.** New alongside the above (`Architecture/Reference.md` §18.2,
  `DecisionReasoning.md` §39.2, corrected 2026-08-24): whether a candidate choice is promoted into a full
  reasoning Decision is gated by the same per-instance evaluated Signal magnitude `Importance` uses —
  checked cheaply during a routine's ordinary candidate scoring, per character, per circumstance — not by
  a static per-Decision-type count. (An earlier version of this parameter gated admission on the number
  of `SignalRequirements` a Decision *type* declares, checked once regardless of character; that was
  wrong, because it meant an entire category of choice — a board game night, an outfit — could never
  become a Decision for anyone, when in practice it should be able to for the right character in the
  right circumstance.) Below the floor, a choice resolves through the ordinary routine path
  (`Architecture/Reference.md` §29) instead of generating a Decision — this is what keeps population-scale
  ordinary choices off the Decision pipeline by default without permanently exiling any category of choice
  from ever mattering. The floor itself is unset; it should be picked from real playtesting evidence
  (what evaluated magnitude the leave-work Decision actually produces is a natural first data point)
  rather than guessed in the abstract. This floor applies only when a generator has a truthful automatic
  fallback. Structural generators such as commitment conflict always admit their Decision once the
  conflict exists; derived Importance still controls their surfacing, Auto-Hold, and overflow priority.
- **Held-decision capacity numbers.** `Architecture/Reference.md` §20 defines `DecisionHoldPolicy` with a
  `maximum global held decisions` and `maximum held decisions per character` field, explicitly left
  unassigned ("exact names are not frozen"). §4's Auto-Hold description leans on "the same bounded
  global/per-character capacity... as manual Hold" as a known quantity; it is actually an open product
  decision, not a locked one.

None of these parameters change this brief's mechanics—only their numbers—so later tuning should be a
small, self-contained follow-up rather than a redesign. The admission, prioritized-feed, normal-feed, and
Auto-Hold floors should be calibrated together because they read the same evaluated-magnitude scale at
different moments. Their required ordering and initial derivation are owned by
[`DecisionImportance.md`](../Design/DecisionImportance.md).
