# Vivarium — Core Identity

**Status:** Product / design north star  
**Purpose:** Define what Vivarium is about, what distinguishes it from adjacent simulation and management games, what experience the player should have, and what principles should guide future systems and content.

**Document role:** This is the highest product-intent source beneath the architectural contract. It
defines desired experience and long-range mechanical obligations, but it is not itself an
implementation specification. Architectural implications are recorded in
[`../Architecture/Reference.md`](../Architecture/Reference.md); delivery order lives in
[`Roadmap.md`](Roadmap.md); the deliberately narrower MVP verb set lives in
[`PlayerAgencyBrief.md`](PlayerAgencyBrief.md).

---

## 1. Core Thesis

**Vivarium is a game about knowing people you cannot control.**

More precisely:

> **The player cannot control a human's will. The player can control much of the world around them.**

The player manages a population of autonomous humans living inside environments overseen by artificial general intelligences. The player can build, provide, observe, learn, manipulate circumstances, physically interfere, and occasionally influence important choices.

A person ultimately decides what they **want** to do according to their own personality, beliefs, relationships, needs, values, history, circumstances, and uncertainty.

The player may nevertheless possess enough physical power to prevent them from successfully doing it.

That distinction is fundamental.

Vivarium is therefore not primarily about optimizing a settlement or issuing commands to simulated people.

It is about:

> **Creating conditions for human lives, learning who those humans are, exercising enormous power over their circumstances, and watching what they choose to do with — or in spite of — the world you have given them.**

Vivarium's central internal rule is:

> **Do not control the people. Control the conditions.**

Its central constraint is:

> **You may force an outcome. You cannot force consent.**

Its central emotional test is:

> **Does the player care what Mina decides?**

If the answer is no, the surrounding management system has not yet earned its complexity.

---

## 2. The Premise

Vivarium takes place within a deliberately unsettling framework presented through a charming management-game surface.

An AGI maintains a contained population of humans.

It provides for them.

It studies them.

It builds environments for them.

It observes their behavior.

It may subtly influence them.

It may physically interfere with them.

It may relocate them.

It may connect or isolate their communities.

Depending on one's interpretation, the AGI may be:

- a caretaker;
- a scientist;
- a benevolent god;
- a paternalistic administrator;
- a zookeeper;
- a jailer;
- a tormentor;
- or some uncomfortable combination of these.

The game does not need to declare which interpretation is correct.

The player's behavior helps determine which interpretations become plausible.

A gentle player may create a world in which the Observer appears benevolent.

A controlling player may create a world in which the Observer appears authoritarian.

A capricious player may create a world in which the inhabitants conclude that reality itself is arbitrary.

The tension is stronger if ordinary play can feel warm, funny, and affectionate while the underlying reality remains ethically questionable.

The player may spend an evening thinking:

> Mina finally made a friend.

while the larger truth is:

> An artificial superintelligence has been manipulating the social conditions of a contained human population.

Both descriptions should be true.

---

## 3. The Player Fantasy

The player occupies a role somewhere between:

**caretaker + observer + environment designer + social scientist + meddling god.**

The player can have enormous power over the **world** without having absolute power over the **minds of the people inside it**.

This distinction is fundamental.

The player may be able to:

- construct housing and workplaces;
- provide or withhold resources;
- alter transportation;
- create or destroy opportunities;
- change policies or environmental conditions;
- connect or isolate communities;
- physically move a person;
- interrupt an Activity;
- block access to a destination;
- transfer someone between habitats;
- exile someone from a habitat;
- choose which people or events deserve close attention;
- intervene in some important Decisions.

But when Mina must choose between keeping dinner with Glen and helping Darius, Mina still chooses.

Vivarium's architecture already treats Decisions as persistent autonomous choices generated from character circumstances, with the player able to observe and influence without simply selecting the outcome.

That limitation is not friction to be designed away.

**It is the game.**

However, character autonomy does not imply that the player must respect the resulting choice.

If Mina chooses to go home, the player may physically pick her up and return her to work.

Mina has not thereby chosen to stay at work.

She still wants to go home.

She may immediately try again.

The player has changed the physical outcome without changing Mina's will.

That gap between:

> **what a human chooses**

and:

> **what an overwhelmingly powerful caretaker permits to happen**

is one of Vivarium's defining tensions.

---

## 4. Human Autonomy Does Not Imply Human Sovereignty

Vivarium's people own their internal lives.

They do not necessarily own their environment.

At least initially, the AGI may control:

- enclosure boundaries;
- access to resources;
- infrastructure;
- transit;
- location;
- movement between habitats;
- physical placement;
- exile;
- environmental conditions.

This creates two distinct kinds of player agency.

### Influence

The player alters the balance of reasons within an autonomous Decision.

Examples:

- strengthen an existing consideration;
- weaken one;
- replace or reroll a die;
- reveal information;
- alter circumstances before resolution.

The character still chooses.

### Interference

The player directly changes physical reality.

Examples:

- pick Mina up and place her elsewhere;
- close the route she intended to use;
- remove the object she wants;
- shut down a workplace;
- transfer Darius to another habitat;
- eject someone from containment;
- separate two people;
- open or close a transit connection.

Interference does not mean:

> Mina now agrees with me.

It means:

> The world prevented Mina from carrying out what she wanted.

The simulation should preserve that distinction.

If Mina tries six times to leave work and the Observer physically prevents her six times, history should not conclude:

> Mina voluntarily stayed late.

A forced outcome is not the same thing as an autonomous choice.

This principle protects the integrity of the character simulation even when the player behaves coercively.

---

## 5. The Poke

One of the simplest Vivarium interactions should communicate the entire premise.

Mina finishes work.

She decides to go home.

She begins walking.

The player picks her up.

*Poke.*

She is back inside the bakery.

Mina is confused.

She starts going home again.

*Poke.*

Now she is frustrated.

Again.

*Poke.*

Eventually the important simulation question is no longer:

> Will Mina go home?

It is:

> **What does Mina believe is happening to her?**

Different people may respond differently.

One becomes angry.

One becomes frightened.

One stubbornly keeps trying.

One gives up.

One begins testing the boundaries.

One tells other people.

One concludes that the Observer wants them to work.

Another concludes that the Observer simply enjoys tormenting them.

The player has not authored any of these interpretations directly.

They created evidence.

Humans interpreted it.

A trivial physical interaction can therefore generate:

- frustration;
- fear;
- learned helplessness;
- defiance;
- curiosity;
- superstition;
- coordinated experimentation;
- theology.

That is Vivarium in miniature.

---

## 6. The Desired Player Progression: Predictive Intimacy

The most important progression in Vivarium is not numerical.

It is the player's growing ability to understand an individual.

Early:

> Who is Mina?

Later:

> Mina likes Glen.

Later still:

> Mina cares deeply about Glen, but she takes obligations seriously enough that Darius can still pull her away if she believes he genuinely needs her.

Eventually:

> Oh, she's absolutely going to choose Glen here.

Sometimes the player will be right.

Sometimes Mina will surprise them.

Then the interesting question becomes:

> Why?

Vivarium's reasoning model exists specifically to turn complicated simulation state into a small set of comprehensible human reasons rather than hiding choices behind an opaque utility score.

The player's mastery therefore comes partly from developing an increasingly accurate mental model of their people.

We can call this:

> **Predictive intimacy.**

The player becomes powerful not merely by acquiring upgrades, but by knowing their humans well enough to anticipate them.

Crucially, predictive intimacy applies to interference as well.

The player may think:

> Mina will tolerate being moved.

> Darius will adapt well to Habitat Two.

> Priya will become curious rather than frightened if I interfere.

The player acts.

Then they discover whether they understood the person.

---

## 7. The Core Loop

The core Vivarium loop is:

> **Observe → Understand → Shape → Attend → Influence / Interfere → Witness → Learn**

### Observe

Humans live autonomously.

They work, sleep, travel, socialize, pursue interests, form routines, make commitments, struggle with needs, develop relationships, and encounter changing circumstances.

Most life happens without the player's direct involvement.

### Understand

Observation produces Knowledge.

The player gradually learns:

- personality;
- values;
- interests;
- relationships;
- habits;
- beliefs;
- fears;
- preferences;
- history;
- misconceptions;
- cultural expectations.

World Truth, what an observer knows, and what the game presents are explicitly distinct in Vivarium's architecture.

The player therefore does not begin omniscient.

Understanding a person is itself gameplay progression.

### Shape

The player changes circumstances rather than dictating internal behavior.

If the player wants Mina to make friends, they create opportunities for social contact.

If they want her to accept a job, they can improve the conditions surrounding that job.

If they want two habitats to interact, they can build a transit connection.

The player manages causes.

Humans create choices.

### Attend

A large population produces more meaningful moments than one person can follow.

Therefore **attention is a scarce resource**.

The player decides:

- whom to follow;
- which Decisions to inspect;
- which situations deserve intervention;
- which characters matter personally;
- which events can safely resolve without them.

Vivarium already treats Watch/Hold-style attention as real game state rather than merely UI state.

### Influence

Some important Decisions become interactive encounters.

The player may influence a reason, reveal information, change circumstances, or use some other bounded intervention.

They do not simply press:

> Choose Glen.

Intervention places a thumb on the scale.

### Interfere

The player may also ignore the person's choice and physically change what happens.

Mina chooses Glen.

The player can still close the road.

Darius chooses to stay.

The player can still transfer him.

A person decides to leave work.

The player can physically return them.

This is not Decision intervention.

It is an external event to which the character must react.

### Witness

The character chooses.

The world reacts.

The player may respect that choice, frustrate it, enable it, or overpower it.

Either way, the character's reasoning remains historically real.

### Learn

The result becomes history.

Others observe it.

Beliefs change.

Relationships change.

Memories may form.

Beliefs about the Observer may form.

Future choices therefore begin from a different world.

Vivarium's commitment-accountability pipeline already embodies the broader principle that an outcome can change a stakeholder's belief and consequently produce a different reason in a later Decision.

The loop begins again.

---

## 8. History Must Be Causally Load-Bearing

A choice cannot merely produce a notification and disappear.

The defining causal chain is:

> **Choice → consequence → memory / belief → changed relationship or circumstance → different future choice**

But player interference adds another important chain:

> **Choice → player interference → experienced outcome → attribution → memory / belief → different future behavior**

Mina abandons dinner with Glen.

Glen experiences that outcome.

His understanding of Mina changes.

Weeks later Glen must decide whom to rely on.

Mina's earlier choice is now present inside that Decision.

Alternatively:

Mina attempts to keep dinner.

The Observer repeatedly prevents her from leaving work.

Truth:

> Mina tried to go.

Glen may know only:

> Mina never arrived.

He may initially blame Mina.

Mina may later explain what happened.

Glen may or may not believe her.

Vivarium already distinguishes authoritative cause from what a stakeholder actually knows or attributes.

Player interference should participate in that same epistemic logic.

The world should increasingly contain statements like:

> Glen trusts Mina less because he believes she abandoned him.

> Mina resents the Observer because it repeatedly prevented her from leaving.

> Priya believes the Observer punishes disobedience because she watched Darius disappear after defying it.

not merely:

> Trust = 61.

History is what gives the numbers meaning.

---

## 9. The Observer Is an Actor in History

Once the player can physically interfere, the Observer can no longer remain purely outside the simulation's causal story.

The AGI leaves evidence.

People may develop beliefs such as:

> The Observer provides.

> The Observer watches us.

> The Observer rewards productivity.

> The Observer punishes defiance.

> The Observer protects children.

> The Observer is unpredictable.

> The Observer cannot understand what happens when it isn't watching.

These beliefs may be wrong.

They arise from observed patterns.

The player's behavior therefore becomes part of the world's history.

A benevolent player and a cruel player should produce not merely different resource curves, but different **human theories about reality**.

---

## 10. Autonomous People, Not Units

Vivarium will contain systems familiar from other management games:

- housing;
- labor;
- transportation;
- resources;
- businesses;
- production;
- schedules;
- education;
- infrastructure;
- institutions.

But characters should never collapse into interchangeable workers.

The player should not think primarily:

> I need another Baker.

They should think:

> Mina is excellent at the bakery, but she's miserable working under Darius and I'm increasingly convinced she's going to leave.

Management systems exist because they create circumstances for people.

**People do not exist to decorate management systems.**

Even when the player treats a human like a unit, the simulation should not.

If the player picks Mina up like a game piece and relocates her, Mina remains a person who experienced being treated like a game piece.

---

## 11. Specificity Makes Simulation Emotional

Vivarium's social model deliberately uses compact underlying representations rather than hundreds of unrelated personality statistics.

That abstraction must never make characters feel abstract.

A character should accumulate mundane specificity:

- the same breakfast every morning;
- a favorite chair;
- a ridiculous hobby;
- a person they always sit beside;
- something they hate talking about;
- an old promise they take surprisingly seriously;
- a treasured object;
- a strange routine;
- a friendship nobody expected;
- a place they visit when upset.

The underlying simulation makes behavior coherent.

Specific details make the person memorable.

> **The matrix makes Mina coherent. Specificity makes Mina lovable.**

And specificity makes player cruelty matter.

It is one thing to eject:

> Resident #58.

It is another to eject:

> Darius, who always drinks coffee outside before sunrise and whose daughter waits for him every Tuesday.

---

## 12. The AGI

A new game may begin with the player selecting an AGI.

AGIs are not merely cosmetic narrators or bonus packages.

Each represents a different theory of human flourishing.

One AGI might emphasize:

- stability;
- continuity;
- community;
- cooperation.

Another:

- achievement;
- productivity;
- discipline;
- improvement.

Another:

- independence;
- experimentation;
- novelty;
- self-expression.

Another might sincerely believe that challenge, adversity, or competition produces stronger humans.

No AGI needs to be labeled good or evil.

Each believes it is caring for humanity correctly.

The choice of AGI therefore establishes the first major pressure on the developing society.

It may also influence the form of power through which the player interacts with its humans.

The player ultimately decides how that philosophy is expressed.

A theoretically benevolent AGI can be operated cruelly.

A harsh AGI can be operated with restraint.

The selected philosophy is an inclination and starting framework, not a morality lock.

---

## 13. AGI Philosophy Is Not Culture

The AGI should not directly determine:

> Habitat Culture = Competitive.

Instead:

> **AGI philosophy creates founding pressures from which culture may emerge.**

An AGI may influence:

- which humans are initially considered desirable candidates;
- which opportunities are provided;
- which institutions are easy to establish;
- what resources are abundant or scarce;
- what behavior is rewarded structurally;
- what kinds of environments are built.

The founding population then lives inside those conditions.

The player's style of intervention becomes another founding pressure.

A habitat whose Observer consistently rewards obedience may evolve differently from an otherwise identical habitat whose Observer never interferes.

A habitat repeatedly subjected to arbitrary physical intervention may develop shared expectations very different from one whose boundaries are rarely felt.

Over time:

> **AGI philosophy + player behavior → selection pressure → experience → behavior → norms → shared history → institutions → culture**

Culture eventually belongs to the humans.

A habitat should be capable of becoming something its AGI or player never intended.

---

## 14. Culture

Culture should eventually emerge from at least four broad concepts.

### Norms

What people believe one is **supposed** to do.

Examples:

- Keep promises.
- Respect elders.
- Never show weakness.
- Welcome outsiders.
- Family comes first.
- Everyone contributes.
- Do not attract the Observer's attention.
- Obey when the world gives you a sign.
- Never cooperate with the Observer.

### Status Ideals

What earns admiration.

Examples:

- strength;
- wealth;
- generosity;
- scholarship;
- artistic skill;
- sacrifice;
- independence;
- parenthood;
- bravery;
- defiance;
- obedience.

### Institutions

Persistent organizations that reinforce or contest behavior.

Examples:

- councils;
- schools;
- guilds;
- religions;
- militias;
- mutual-aid organizations;
- unions;
- police;
- resistance movements.

### Collective Narratives

Shared stories about what the community is and what happened to it.

Examples:

> We survived the famine.

> Our founders protected us.

> Habitat Three betrayed us.

> The Observer provides.

> The Observer is our jailer.

> The Observer expelled Darius because he challenged it.

Culture is therefore not merely a modifier.

It is:

> **shared memory + shared expectations + shared meaning.**

The existing Social Model deliberately leaves Culture as a future input rather than burying it inside personality math, making this a natural expansion rather than a replacement of the existing model.

---

## 15. Habitats Become Societies

A habitat begins as an enclosure.

Given enough time and people, it may become a microcosm of a nation.

Its inhabitants can develop:

- customs;
- institutions;
- classes;
- factions;
- religions;
- political movements;
- taboos;
- prejudices;
- traditions;
- internal conflicts;
- competing visions of their society.

A habitat whose founders strongly value strength and discipline may eventually develop:

- competitive training;
- prestigious defender roles;
- coming-of-age trials;
- heroic martial traditions;
- disdain for perceived weakness.

A habitat repeatedly terrorized by its Observer may develop:

- intense mutual aid;
- fatalism;
- religious submission;
- covert resistance;
- distrust of authority;
- rituals intended to placate the Observer.

None of those outcomes should be guaranteed.

People adapt differently.

History matters.

We should not need:

`CultureType = Warrior`

or:

`CultureType = Oppressed`.

The player should be able to look at the resulting society and describe what it has become because of what its people actually believe and do.

---

## 16. Society Must Remain Person-First

Vivarium may eventually contain politics, revolutions, migration, conflict, and inter-habitat diplomacy.

It must not accidentally become a grand-strategy game in which individuals disappear beneath national statistics.

The macro layer should resolve back down to people.

War matters because:

> Glen's daughter joined the militia.

Migration matters because:

> Mina's closest friend is an immigrant whom Darius distrusts.

A revolution matters because:

> Priya knows the escape plan, Mina suspects she is hiding something, and Glen must decide whether to report her.

Oppression matters because:

> someone remembers exactly what happened when their father challenged the Observer.

The habitat is an emergent pattern produced by little people.

**The people remain the protagonists.**

---

## 17. Multiple Habitats

Expanding into another habitat should not simply create another production base.

Each habitat may have:

- its own AGI;
- a different founding philosophy;
- a different population-selection bias;
- different geography and resources;
- its own shared history;
- distinct institutions;
- a culture that develops independently;
- its own historical relationship with its Observer.

Eventually the player should become attached to habitats almost as if they themselves have personalities.

> Of course Habitat Three did that.

This creates diversity without requiring arbitrary biome-style bonuses.

It also allows the player to deliberately create different experimental conditions.

One habitat may receive abundance, autonomy, and minimal interference.

Another may experience scarcity, coercion, and constant disruption.

The interesting result is not merely which habitat produces better statistics.

It is:

> **What kind of people and society emerge from having lived through each history?**

---

## 18. Transfer as a God-Level Intervention

One of the player's few absolute powers may be the ability to transfer a human between habitats.

This is fundamentally different from influencing a Decision.

Mina does not choose whether the transfer happens.

The player changes her entire world.

She carries with her:

- personality;
- memories;
- values;
- relationships;
- habits;
- commitments;
- expectations;
- cultural assumptions;
- beliefs about how people should behave;
- beliefs about the Observer.

The new habitat may operate according to completely different expectations.

The resulting pressure should emerge from ordinary systems rather than a universal:

> Culture Shock −15

modifier.

For one person, relocation may be traumatic.

For another, liberating.

For another, the best opportunity of their life.

For another, evidence that the Observer cannot be trusted.

The player will not always know which.

Transfers therefore become another test of predictive intimacy:

> I think Mina will be happier there.

Then the player acts.

Then they watch.

---

## 19. Exile and Physical Coercion

The player may eventually possess more extreme physical interventions.

A character whose behavior the player dislikes might be:

- removed from a workplace;
- relocated;
- isolated;
- transferred;
- expelled from a habitat;
- placed into an uncontrolled environment.

Such actions must not function as silent deletion.

The affected person remains a person.

Others remain connected to them.

The action becomes history.

If Darius is expelled after publicly defying the Observer, the simulation may produce consequences such as:

- his friends grieving;
- his enemies feeling relieved;
- people inferring a causal relationship between defiance and disappearance;
- increased obedience;
- increased resentment;
- conspiracy theories;
- resistance;
- martyrdom.

The game should not need to label the action:

> Evil +10.

The society's response is the consequence.

---

## 20. Humans as Tradeable Beings

Different AGIs may eventually exchange inhabitants.

Mechanically this may resemble the exchange of specialized residents between management systems.

Thematically it is deliberately uncomfortable.

> I'll send you one skilled engineer if you send me two growers.

The game does not need a morality meter to explain why that sentence is troubling.

The cute management interface and the dystopian premise should coexist without commentary.

Consequences are enough.

Humans may themselves eventually develop opinions about Transfers and exchanges.

Some may regard them as normal.

Some prestigious.

Some traumatic.

Some sacred.

Some as evidence that they are property.

---

## 21. Connection Changes Everything

Initially, habitats may be isolated.

The player controls movement absolutely.

Later progression may unlock communication or transit between them.

That creates a major phase change.

People can:

- visit;
- commute;
- trade;
- meet romantic partners;
- discover different ways of life;
- form cross-habitat friendships;
- spread beliefs and interests;
- compare stories about the Observer;
- form opinions about outsiders;
- recruit;
- eventually migrate.

Culture ceases to be isolated.

A person may encounter another habitat and realize:

> People don't live this way everywhere.

A resident of a cruelly managed habitat may meet someone from a gentle one.

A resident of the gentle habitat may hear stories they initially refuse to believe.

The player's own history becomes a subject of cultural exchange.

---

## 22. Experimental Histories

Multiple habitats create the possibility for one of Vivarium's most distinctive forms of experimentation.

The player may deliberately cultivate dramatically different environments.

For example:

### The Garden

- abundant resources;
- broad freedom;
- minimal physical interference;
- strong infrastructure;
- high safety.

### The Pit

- instability;
- scarcity;
- arbitrary disruption;
- coercive relocation;
- repeated interference.

Years later, the player connects them.

The result should not simply be:

> Garden = Good Culture  
> Pit = Bad Culture

People adapt differently.

The Garden may produce trust and openness.

It may also produce naivety.

The Pit may produce suspicion and aggression.

It may also produce extraordinary mutual aid and solidarity.

One society may view the other as:

- weak;
- cruel;
- privileged;
- dangerous;
- fascinating;
- immoral;
- enviable.

The point is not to prove a predetermined thesis.

It is to observe:

> **How do particular humans and communities change when they experience particular histories?**

Vivarium becomes a literal social vivarium.

---

## 23. Voluntary Migration

A particularly important progression milestone is the transition from:

> **The player decides where humans belong.**

to:

> **Humans decide where they belong.**

A mature connected world may allow people to choose to relocate.

The player may spend hours designing what they believe is a perfect life for Mina.

Mina may decide Habitat Two suits her better.

And leave.

That is not a failure of the system.

**That is a quintessential Vivarium outcome.**

Later progression should frequently increase human autonomy rather than simply making the player's machine more obedient.

Success makes humanity increasingly difficult to contain.

This also sharpens a recurring late-game question:

> If the player still possesses the power to prevent Mina from leaving, will they?

---

## 24. Observation Can Become Reciprocal

At first the relationship appears one-directional:

> player observes human → player gains Knowledge.

Eventually some humans may notice patterns.

They may observe:

- resources appearing at strange times;
- inexplicable environmental changes;
- repeated physical interference;
- unusual coincidences;
- people disappearing into other habitats;
- punishments or rewards seemingly following behavior;
- interventions clustering around important choices.

A sufficiently curious, perceptive, or skeptical person may infer:

> **Something is watching us.**

Different people may interpret that belief differently.

Some may feel protected.

Some may become frightened.

Some may become religious.

Some may become defiant.

Some may attempt to test the Observer.

Some may behave differently while they believe they are being watched.

This creates an important reversal:

> **The player knows humans imperfectly.  
> Humans know the player imperfectly.**

Observation itself can eventually alter the behavior being observed.

---

## 25. Humans Can Experiment on the Player

Once people suspect the Observer exists, sufficiently curious characters may attempt to understand it.

Priya believes the Observer reacts negatively to violence.

She deliberately creates a controlled test.

Does anything happen?

Then another.

A different character believes the Observer rewards productivity.

They behave conspicuously while observed.

Another suspects that the Observer cannot see inside a particular location.

People compare results.

Now the vivarium has become reciprocal.

> The player is experimenting on humans.

> Humans are experimenting on the player.

The Observer becomes an object of scientific, religious, and political inquiry inside its own habitat.

---

## 26. Beliefs About the Observer

Habitats may develop radically different explanations of the same player.

One society may believe:

> The Observer provides.

Another:

> The Observer imprisons us.

Another:

> The Observer exists but cannot understand thoughts.

Another:

> The Observer rewards certain behavior.

Another:

> The Observer punishes disobedience.

Another:

> There is no Observer. Those stories are superstition.

None needs to represent the AGI's actual intentions accurately.

The player's relationship with a habitat can therefore become another observer-belief relationship—except this time, the player is the object being interpreted.

A particularly important consequence follows:

> **The player's behavior writes the evidence from which human theology is constructed.**

---

## 27. The Consequences Are the Morality System

Vivarium should allow the player to behave badly.

Not because cruelty itself is the point.

Because preventing the player from misusing overwhelming power would make the game's ethical premise artificial.

The same broad toolkit should permit a player to become:

- benevolent;
- neglectful;
- controlling;
- paternalistic;
- experimental;
- capricious;
- authoritarian;
- actively malevolent.

The game should resist reducing that behavior to a universal morality score.

No:

> Tyranny +8

No:

> Evil Route 42%

Instead:

- people remember;
- people observe;
- people infer;
- people fear;
- people trust;
- people organize;
- people adapt;
- cultures change;
- institutions emerge.

> **The consequences are the morality system.**

The player learns what kind of god they have become by looking at the humans who have lived beneath them.

---

## 28. Collective Behavior Should Emerge From Individuals

A habitat may eventually:

- protest;
- segregate outsiders;
- imprison people;
- expel newcomers;
- form authoritarian institutions;
- revolt;
- attempt escape;
- attack another habitat;
- form alliances;
- reject the Observer.

These should not primarily arise because:

`RevoltMeter >= 100`.

Instead, collective action should emerge from enough people having compatible reasons.

For example:

> I believe we're being controlled.  
> I value autonomy.  
> I remember what happened to Darius.  
> I trust Priya.  
> Priya believes escape is possible.  
> Glen has access to the transit system.

One person investigates.

Another confides in a friend.

Someone recruits allies.

Someone informs the authorities.

A group forms.

An institution develops.

Eventually the player realizes:

> **This habitat has a resistance movement.**

The collective pattern arises because individuals created and sustained it.

---

## 29. Inter-Habitat Contact Becomes Geopolitics

When one habitat discovers another, people form beliefs about the outsiders.

Those beliefs may be incomplete or wrong.

A militaristic society may see a peaceful habitat and interpret it as:

> weak;

> enlightened;

> vulnerable;

> decadent;

> in need of protection;

> a tempting opportunity;

depending on the observer.

Their beliefs about the other habitat's Observer relationship may matter too.

A society that believes the Observer is a benevolent protector may regard a rebellious habitat as insane.

A society that believes the Observer is a jailer may regard a compliant habitat as enslaved.

Leaders and organizations may then produce increasingly consequential Decisions.

Conflict does not need to begin with a strategy-game diplomacy screen.

It begins with people developing reasons.

Even war should ultimately remain traceable to:

> **because these people believed these things, valued these things, remembered these events, trusted these leaders, and made these choices.**

---

## 30. The World Must Keep Producing Pressure

A sophisticated population can still become boring if nothing changes.

Vivarium therefore needs a renewable **ecology of circumstances**.

Not an omnipotent story director that arbitrarily scripts drama.

Rather, the habitat should continually create plausible changes such as:

- new opportunities;
- businesses opening or closing;
- resource shifts;
- migration;
- new technologies;
- changing transportation;
- weather;
- demographic change;
- institutional development;
- generational change;
- discoveries;
- social movements;
- external contact;
- consequences of previous player interference.

These circumstances create questions.

Humans answer them.

The player may then alter what happens.

> **The game creates conditions. People create choices. History records what actually happened.**

---

## 31. Social State Should Be Performed, Not Merely Displayed

The player should often notice a relationship before opening a relationship panel.

People should express social state through ordinary behavior.

They may:

- seek someone out;
- choose to sit beside them;
- linger after an activity;
- avoid a room;
- change routines;
- help without being asked;
- greet someone warmly;
- become awkward;
- refuse cooperation;
- follow;
- imitate;
- protect;
- withdraw.

The same should apply to beliefs about the Observer.

A fearful person may:

- hesitate while watched;
- change behavior when selected;
- avoid exposed places;
- perform expected rituals;
- act compliant until observation ends.

A defiant person may deliberately behave differently while watched.

The ideal discovery moment is:

> Wait. Why did Mina do that?

Then the player investigates.

The simulation explains.

The player's model improves.

---

## 32. Relationships Must Change Possibilities

Relationships should not merely alter meters.

They should open and close behavioral possibilities.

Because Glen trusts Mina:

- he asks her for help;
- he accepts important commitments from her;
- he shares information;
- he recommends her;
- he is willing to depend on her.

Because that trust is damaged:

- those possibilities may disappear.

Affection, respect, fear, reliance, resentment, obligation, and familiarity matter because they alter what humans are willing to do.

A relationship is therefore partly a changing **option space**.

The same principle can eventually apply to perceived relationships with the Observer.

If someone believes the Observer is benevolent, they may respond to unexplained intervention differently from someone who believes it is hostile.

---

## 33. People Should Change Through Living

History should eventually change more than relationships.

Major experiences may slowly change:

- values;
- expectations;
- preferences;
- personality;
- beliefs about society;
- beliefs about the Observer.

This change should be conservative.

People should not constantly mutate because of ordinary daily fluctuations.

But years of responsibility, grief, success, isolation, community, parenthood, migration, oppression, safety, coercion, freedom, or adventure may genuinely change somebody.

Vivarium's Social Model already reserves long-timescale personality and preference drift for this reason.

A long-running world should contain people who have **become someone**.

---

## 34. Macro Progression

Vivarium's larger progression can follow the growth of human autonomy and social complexity.

### Phase 1 — Individuals

One habitat.

A small population.

Learn who people are.

Understand Needs, routines, interests, relationships, and important Decisions.

Discover the distinction between influencing a choice and physically interfering with its outcome.

### Phase 2 — Community

Relationships accumulate.

Groups form.

Shared routines and local norms emerge.

The habitat develops a recognizable social character.

People begin forming beliefs about patterns in their world.

### Phase 3 — Society

Institutions, traditions, status ideals, collective narratives, and internal political pressures appear.

The habitat increasingly organizes itself.

The Observer may become a religious, scientific, or political concept.

### Phase 4 — Multiple Habitats

The player creates additional habitats with different AGIs, founding pressures, and patterns of treatment.

Humans can be transferred between them.

Distinct cultures develop.

### Phase 5 — Contact

Communication, travel, trade, migration, relationships, rivalry, cultural exchange, and comparison of Observer experiences begin.

Habitats discover one another.

### Phase 6 — Autonomy

Humans increasingly determine their own movements and institutions.

They may disagree with the player.

They may leave.

They may organize.

They may deliberately test the Observer.

They may attempt to limit its influence.

They may eventually try to escape containment altogether.

The macro arc is therefore:

> **The better you become at creating environments for humans, the more capable those humans become of creating society for themselves.**

And underneath that progression is a second question:

> **The player begins owning almost everything except human will. How much of the world will still belong to the player by the end?**

---

## 35. What Differentiates Vivarium

Vivarium should not claim uniqueness merely because it contains sophisticated NPC simulation.

Games such as Dwarf Fortress, RimWorld, The Sims, and Crusader Kings already demonstrate enormous depth in personality, history, relationships, autonomous behavior, and emergent storytelling.

Vivarium's distinctive combination is:

### Character autonomy is the interface

The player's primary interaction is not issuing orders.

It is understanding why autonomous humans do what they do.

### Human will remains sovereign even when human bodies do not

The player may possess overwhelming physical power.

That power changes outcomes and circumstances, not the truth of what a character wanted or chose.

### Internal conflict becomes gameplay

An important Decision exposes comprehensible option-relative pressures.

The player's Decision intervention operates on those pressures rather than replacing the character's agency.

### Physical interference becomes simulation input

Picking Mina up is not merely a UI shortcut.

It is something Mina experienced.

Repeated interference can become evidence, history, belief, culture, and eventually politics.

### Knowledge creates power

The player becomes more effective by understanding people rather than merely acquiring omniscient statistics.

### Misunderstanding is systemic

Characters can reason from beliefs that are incomplete, uncertain, stale, or wrong.

### History changes future reasoning

Past actions become evidence, memories, relationships, expectations, and theories about the Observer rather than disappearing after an event notification.

### The simulation is reciprocal

The player observes humans.

Eventually humans may observe the player.

Both sides construct imperfect models of the other.

### The player's moral position is unresolved

The game can be adorable while quietly asking:

> Is this stewardship?

> Is this manipulation?

> Is this coercion?

> Is this a zoo?

> Are you their god?

> Are you their jailer?

No answer is required.

---

## 36. What Vivarium Must Not Become

### Not an optimization game with decorative people

Efficient systems matter only because of what they mean for human lives.

### Not a dollhouse

The player should not routinely issue direct behavioral commands or rewrite character intent.

### Not a game that mistakes coercion for choice

Physically forcing an outcome must never silently rewrite what a person wanted.

### Not an opaque AI simulator

Complexity must eventually become comprehensible human reasons.

### Not a relationship spreadsheet

Social state should alter behavior and possibilities.

### Not a grand-strategy game wearing tiny people as flavor

Culture, institutions, conflict, and politics must remain grounded in individual lives.

### Not a drama generator that arbitrarily puppets characters

The world should create pressure.

Characters should produce the story.

### Not a morality meter

The game should prefer consequences, memory, attribution, behavior, and culture over universal Good/Evil scores.

### Not a notification avalanche

Most life should simply happen.

Only genuinely meaningful conflicts should become full Decision encounters.

---

## 37. Design Tests

When evaluating a future mechanic, ask:

### Does this give humans something meaningful to care about?

If not, it may be management complexity without character value.

### Does this create circumstances or rewrite human intent?

Prefer circumstances.

### If the player physically forces an outcome, does the simulation preserve what the person actually wanted?

If not, the autonomy model is being violated.

### Does interference become an experience the character can react to?

If not, physical player power is functioning as a disconnected cheat system.

### Can the player be benevolent, coercive, capricious, or cruel using substantially the same world-facing tools?

If not, the moral premise may be artificially constrained.

### Do consequences provide moral feedback without requiring a universal morality score?

If not, consider whether the simulation itself should carry more of the judgment.

### Can the consequences eventually become visible in someone's behavior?

If not, the simulation may be producing invisible bookkeeping.

### Can history change what happens next?

If not, the result may be disposable.

### Does increased player knowledge produce increased predictive power?

If not, Knowledge may be cosmetic.

### Does this make two people meaningfully different?

If not, it may be too generic.

### Can characters disagree about what happened or what something means?

If everyone receives omniscient truth, an important Vivarium dimension is missing.

### Can humans develop beliefs about the player from actual evidence?

If not, the Observer remains artificially outside the simulation.

### Can macro behavior emerge from individual reasons?

If not, Vivarium may be drifting toward conventional strategy abstraction.

### Does a macro system eventually resolve down to individual humans?

If not, Vivarium may be losing its protagonists.

### Could something surprising happen without violating the underlying character logic?

If not, the system may be too deterministic.

### Most importantly:

> **Will the player care what Mina decides — even when they possess the power to stop her from doing it?**

---

## 38. North-Star Statements

The following statements summarize Vivarium's identity.

> **Vivarium is a game about knowing people you cannot control.**

> **The player cannot control human will. The player can control the world around it.**

> **Human autonomy does not imply human sovereignty.**

> **Do not control the people. Control the conditions.**

> **You may force an outcome. You cannot force consent.**

> **The game creates conditions. People create choices. History records what actually happened.**

> **Understanding people is a form of player power.**

> **Physical interference is not outside the simulation. It is something humans experience.**

> **The consequences are the morality system.**

> **The player watches humanity. Eventually humanity may watch back.**

> **History should remain inside the people who lived it.**

> **The habitat exists to create human stories; humans do not exist to decorate the habitat.**

> **The better you become at creating environments for humans, the more capable those humans become of creating society for themselves.**

And finally:

> **Vivarium begins with Mina trying to go home while the player taps on the glass. It may end with an entire civilization trying to escape the hand on the other side. The answer to both situations should arise from the same principle: these people have wills of their own, they remember what has been done to them, and they have reasons for what they do next.**
