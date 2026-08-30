# Rules of time travel

The mechanics of `GAME.md`, stated as invariants. Every example uses a loop of 0–400 (in
`LoopTime` raw units; the real loop is ~60 s = 600000).

Vocabulary: **loop time** is the position on the timeline being recorded — the only time coordinate
that exists. **Real time** is frames going past. A **body** is anything simulated. A **span** is one
continuous recording pass over one body. A body is **recording** (claimed, live-simulated) or
**playing back** (sampled from history).

---

## 0. Why any of this works: entropy, not prohibition

**The laws of physics are time-symmetric.** Reverse a trajectory and nothing breaks: `x(-t)` obeys the
same equations `x(t)` did. Under reversal, *odd* quantities flip sign (velocity, momentum, current) and
*even* ones do not (position, mass, acceleration, HP). Gravity needs no handling at all — a reversed
parabola is the same parabola.

**What is not symmetric is likelihood.** Gas fills a box because spread-out states are overwhelmingly
more numerous, not because a law forbids the reverse. A bullet leaving a wall and flying into a muzzle
breaks nothing; it just needs the wall's heat and the room's sound to conspire. It is *improbable*, and
it is *brittle* — perturb it anywhere and it collapses into ordinary forward behaviour.

**Recording is what buys us the conspiracy.** We do not model molecules, so we cannot generate the
fine-tuned reversed trajectory. We do not have to: we watched it happen forwards, so we can replay it
backwards exactly. Every body kept on playback is a butterfly effect we have refused to have — which is
why this ruleset is so protective of playback, and why claiming a body is always a cost.

Three consequences run through everything:

- **The line is contradiction, not entropy.** Improbable is allowed and is the entire aesthetic.
  Contradictory is not. A corpse standing up because its killer was erased is fine; a corpse standing up
  in zero time is not. A contradiction that cannot be unwound gets paid for forwards, which is what the
  ship exploding is for.
- **Matter is conserved.** A body that is not in the world is still around somewhere — in the gun, on the
  floor as a corpse, evaporated into the air (rule 7).
- **Perturbation must destroy the improbable trajectory.** Touch a backwards-running body and it should
  stop being backwards-running. It does: it diverges, is claimed, and records in the cursor's direction
  like anything else (rule 10).

Time direction is a property of a *span*, not of a body — of which pass laid it down. Since there is one
cursor (rule 1), two bodies are never simulated in opposite directions at once; "inverted" always means a
recording being played against live bodies. That is why conservation inside the live set is automatic,
and why every remaining problem in this document lives at the playback/live boundary.

## 1. There is exactly one cursor

Every body — recording or playing back — sits at the same loop time. There are no per-body clocks.

This is what makes interactions reproducible: they always happen between bodies at equal loop time, so
replaying the timeline reproduces them by construction.

## 2. Rate comes from the watched character

The cursor advances by `rate × dt` each physics step, where `rate` is the *watched* character's signed
timescale. The watched character need not be the recording one. Rate `0` freezes the timeline, and **a
frozen cursor records nothing**: no loop time passed, so there is nothing new to describe.

`rate` is also the conversion factor between the two time coordinates, which is what makes rule 5 work.

> **Bullet time is free.** A character at rate `0.2` takes five physics steps per unit of loop time that
> a rate-`1.0` character covers in one. Watched, it looks normal and the world crawls. Watched from
> someone else, its samples are five times denser, so it replays five times faster at full fidelity.
> Nobody wrote any code for that.

`|rate| > 1` costs history resolution, not correctness — a recording body covers more loop time per
physics step, so its samples land further apart. Physics always steps exactly once per frame; **rate is
never a physics property.** `Time.timeScale` and `fixedDeltaTime` are never touched.

## 3. Nothing is ever written behind the cursor

**The master rule.** Every span grows in the cursor's direction, one sample per physics step, keyed by
the loop time that step landed on. Nothing is ever written into loop time the cursor has already left,
and no span is ever created whole.

Most of the other rules are consequences of this one.

## 4. Playback is the default; recording happens only at the frontier

Moving through loop time a body already has history for is pure playback — no physics for that body. A
body records only when something claims it (rule 10), and the commonest trigger is having no history in
the direction of travel: the **frontier**.

The frontier is per body, not global. The first take establishes history for every body across the whole
loop, most of them recording NOOP, so afterwards the cursor almost never leaves known territory: **rewind
is pure playback and needs no physics at all.** This is also what makes direction changes cheap — flipping
to backwards does *not* re-record everything as inert, because everything with history below the flip
point simply plays back.

> A crate is claimed at 290 and shoved across the floor, recording its slide over `[290,300]`. Rewinding
> below 290 invents nothing — the crate's *older* span still covers that range, showing it standing where
> it always stood. The new span begins at 290 with an inherited sample (rule 5), so the slide starts
> exactly where the crate was sitting, caused by the shove.
>
> Without that older history the question has no good answer. Simulating a moving body while the cursor
> descends writes decreasing loop times, which reads forward as the crate sliding *back* into a shove
> that has not happened yet; freezing it reads as an uncaused stop. The first take is what makes the case
> unreachable.

## 5. A claim inherits its first sample, in loop-time units

When a body is claimed, the live body is seeded from whatever playback was already showing, and that seed
is written as the new span's first sample. So old and new authority **agree exactly at the instant they
meet**. Re-recording can never teleport a body; seam-freedom is structural, not policed.

> X is recorded walking through a doorway at 100. An inverted copy shuts that door, so X's position at
> 100 is now inside it. X is claimed at 100 and re-records backwards, getting pushed aside. At exactly
> 100 X is where it always was; below 100 it diverges; above 100 the original run stands.

**Every channel is stored as a derivative with respect to loop time, never real time.** A velocity sample
means `dx/dτ`, so seeding a rigidbody is `velocity = rate × sample` and reading one back is
`sample = velocity / rate`. Under a negative rate that negates automatically — rule 0's "reversal flips
the odd quantities", for free, with no flip logic anywhere.

Getting this wrong is an impulse from nowhere at every direction change, so keep the split explicit.
**Odd, must flip:** linear and angular velocity, momentum, animation playback rate, particle velocities,
conveyor and fan and current directions, audio playback direction. **Even, must not:** position,
orientation, mass, HP, acceleration, form. HP being even is why a backwards character *heals* as it walks
toward the shooter, which is the correct reading.

## 6. Where two spans overlap, the later recording wins

Each span carries a monotonically increasing `Seq`; lookup takes the highest `Seq` covering an instant.
That covers overwriting a previous take and re-recording the same stretch twice in one take.

**Interpolation never crosses a span boundary**, so boundaries are the only true discontinuities in the
game — and a discontinuity is impossible read in *either* direction, unlike a steep continuous ramp,
which is merely improbable in one (rule 0). Two consequences:

**A state change must happen inside a span, never on its boundary.** One recording step is enough;
nothing needs animating, since the world is only ever observed at sample resolution. This is the whole
difference between a corpse reassembling and a corpse blinking into existence.

**Both ends of a span must be accounted for.** Rule 5 handles the near end by inheriting. The far end —
where a re-recorded span stops and an older one resumes underneath — inherits nothing and cannot:
inheriting at both ends would make re-recording a two-point boundary value problem instead of an initial
value problem, which is the entire thing recording-based time travel exists to avoid. So the far end is
repaired rather than prevented — a body whose channels jump with no cause is exactly what rule 10 looks
for, so crossing such a seam claims the body and it re-simulates from there. That costs the player a
performance, but it is never a silent violation.

## 7. Matter is conserved; absence is a state, not a nonexistence

Nothing is created or destroyed. What changes is a body's **form**, one bit on every sample:

- **`Manifest`** — in the world, with its own channels and a `GameObject`. A corpse is `Manifest` with
  zero HP, not a separate form.
- **`Latent`** — around, but not present: still in the gun, absorbed, evaporated, dissipated. No
  channels, no `GameObject`, no position — nothing ever reads a latent body's location, because whatever
  releases it supplies the position at that moment (rule 8). One state covers everything from "not fired
  yet" to "gone into the air"; the difference is narrative, not mechanical.

**There are no void spans.** Absence is a channel value, so it inherits (rule 5), is overridden by `Seq`
(rule 6), and is claimed and re-recorded (rule 10) like anything else. Death was formerly a span-level
override with its own cause field, its own revival path and its own exception to rule 5; it is now a
transition on the HP and form channels.

**A body with no span covering an instant reads as `Latent`.** That default is what makes conservation
free: there is no roster to pre-populate and nothing is created in advance. Bodies enter the timeline the
first time something writes a span for them — which matters, because the turnstile can mint copies
without limit and no up-front set could hold them. Conservation is how the timeline *reads*, not an
allocation strategy.

> An inverted agent kills an enemy at 130 on its way down. The enemy is claimed at 130, takes the blow,
> and records backwards as a corpse over `[0,129]`. Read forwards, the corpse gets up at 130 — the
> intended inverted-causality signature. What makes it legal rather than merely striking is that the
> transition sits inside a span and interpolates (rule 6), so the corpse reassembles continuously and the
> blow retracts. Improbable, not impossible.

> A guard recorded alive over `[0,400]` fires a bullet at 150. You take over at 100, move forward, and
> kill the guard at 120. Nothing is voided: the guard becomes a corpse, the bullet stays `Latent` in his
> gun, and the release at 150 never happens — no separate rule for it, because `Latent → Manifest` is a
> state change that now has no cause (rule 8).

## 8. Causality lives on the thing it describes, not in an event log

There is no event log. Everything one would carry is single-valued, so it fits on what it describes:

- a body has exactly one **origin** — what released it into `Manifest`, and when (`Origin`, `OriginAt`).
  Since matter is conserved, this is not a claim about the body existing; it is a claim about the body
  being *let go*;
- a body records, each sample, **what pushed it** — its **contact set** (rule 10).

Both are cheap because both are small. A contact set is a handful of ids, taken from contact points the
engine already computes, and is usually empty or a single floor.

**Origin is read forwards, like everything else on the timeline.** An inverted shot read forwards is a
bullet emerging from a wall and flying into a gun, so its origin is the *wall*, not the muzzle — and it
is properly accounted for, because the bullet hole goes `Manifest → Latent` at that instant. The muzzle
is where that bullet *ends*.

So the general form is: **a transition between `Latent` and `Manifest` must be accounted for by whatever
is on the other side of it, in whichever direction it reads.** A gun firing spends a round; a gun
un-firing gains one and needs a bullet to arrive; a wall ejecting a bullet closes a hole.

When a recorded state has no such cause in the live world, the repair is always the same: **claim the
body** and let it re-simulate. There is no second repair path and no special case.

> X is shot dead at 150 and lies there for the rest of the loop. A later take removes the bullet. X's HP
> drop at 150 now has no damage dealer, so X diverges, is claimed with its pre-transition state, and never
> goes down.

> An inverted shooter's bullet is recorded over `[100,160]`: read forwards it leaves the wall at 100 and
> reaches the muzzle at 160. A forward character walks into its path at 130 and catches it. **The bullet
> needs no repair** — a bullet came out of a wall and hit somebody, which is improbable but perfectly
> legal (rule 0), and it simply stops at 130 and is `Latent` above.
>
> What breaks is the **gun**: its recording gains a round at 160 with nothing arriving to account for it.
> So the *shooter* diverges at 160 and loses everything above it — which, in their own backwards
> experience, is the run-up to the shot they now never un-take.

Anything genuinely per-instant — "this HP drop came from *that* bullet" — is still not recorded. The
component owning the channel notices a change it cannot account for and diverges.

A conflict discovered behind the cursor is not repaired there — rule 3 forbids it — so it is not repaired
until the cursor next passes through. **Nothing carries a note in the meantime;** the staleness follows
from rule 3 alone, and the contact set finds the conflict on the way back.

> A bullet fired at 100 and recorded flying to 160 is absorbed by an inverted character at 150. The
> backward take cannot fix the tail over `[151,160]`, because it is behind the cursor (rule 3). So the
> tail is left stale. On the next forward pass the bullet reaches 150,
> finds a live partner its contact set never recorded, diverges, hits the character for real, and records
> `Latent` from 151. The old tail is outranked and the history is right.

Staleness is rarer than it sounds, because a divergence fires *where the cursor is*, unlike a void, which
used to grow ahead of the cursor and invalidate things far away.

## 9. Presence is derived, never accumulated

Which bodies need instantiating at an instant is computed from the timeline every step and reconciled
against what exists — never accumulated from spawn/destroy events. `Manifest` bodies need a `GameObject`;
`Latent` ones do not.

This is why a body reappears by itself when the cursor descends past a transition again, and why
arbitrary cursor motion takes the same code path as a single step.

Pooling is a throughput optimisation for bullets, not part of the correctness story — but **every body
records its archetype**, because loading a save must be able to materialise a body from the file alone.

## 10. Claiming is one mechanism with several triggers

`Claim(body, direction, at)` is the whole of it. Triggers: the player touches the controls; the body is at
its frontier (rule 4); contact with something already recording; recorded state the live world cannot
account for.

A claimed body without a controller is **inert, not dead**. Inert means *no intent source* — not no
physics. It keeps its momentum, coasts, gets pushed, and dies. It stays a character and stays a switch
target; taking one over is just attaching an intent source to a body that was already claimed.

> Shove a crate across a doorway, then switch to another character and carry on. The crate is inert but
> must finish sliding, recording as it goes: the slide is a consequence the player caused, and the other
> characters have to be able to collide with the crate while it is still moving. A crate that froze the
> moment you looked away would be a different puzzle.

**Three predicates** detect divergence, each validating its own channel against the live world:

- **Contact set.** Compare the contacts recorded at this sample against the contacts the live world
  offers. A recorded partner that is no longer there, or a live partner that was not recorded, means the
  recorded motion is not the motion this body would now have.
- **Interpenetration.** Is my recorded position inside something solid now?
- **Channel accounting.** My recorded HP drops between these two samples — is there a live damage dealer
  for it? Same shape for ammo, charge, any consumable.

The contact set is deliberately not force accounting. Deciding whether a recorded velocity change is
"explained by gravity and friction" means re-deriving dynamics outside the physics engine, which is
brittle and is most of a second physics engine. Comparing *who touched me* needs no dynamics at all:
gravity is universal and identical across takes, and friction arrives through a contact already in the
set. If the same things touched me, the same forces acted.

> A guard shoves a lamp at 160; the lamp records the contact and topples over `[160,200]`. A later take
> kills the guard at 120, so at 160 the lamp's recorded contact partner is a corpse across the room. The
> lamp diverges, is claimed, and stands there. Without this check the lamp falls over on its own — an
> uncaused acceleration, which is a real violation of Newton's laws, unlike anything else in this game.

**Significance.** A contact is recorded, and checked, only if it would visibly move the body: accumulate
the unaccounted `Δv` it contributes and diverge when that crosses a threshold. This is what keeps the game
from being brittle — brushing past a recorded crate does not claim it; only doing something that would
actually have changed its recording does. Sub-threshold pushes into playback bodies are the one accepted
violation in the game, and they are accepted deliberately.

**The check runs in both directions**, on the live body as well as the playback one. Without that, a
playback body is an infinite-mass anchor: a live character could stand on a recorded crate or brace
against a recorded door, taking an equal and opposite reaction from nothing. Accumulating `Δv` handles
leaning correctly — a sustained push crosses the threshold on a light crate and never does on a heavy
one, which is right in both cases.

For channel accounting to be exact rather than a guess, **every damage source must be discoverable at the
instant it deals damage** — by overlap, by the raycast a laser already performs, by a registry of live
hazards. Force applied without contact (explosions, magnets, wind) registers in the contact set, because
from the channel's point of view it is a contact. Nothing is forced to grow a hitbox it would not
otherwise want, but **nothing may deal damage or apply force from nowhere.**

Divergence claims the **whole body**, not the offending channel. Damage usually transfers momentum, so HP
and transform move together, and splitting them would escalate to the whole body in most real cases
anyway. The cost is that a diverged character goes inert and loses the remainder of its performance,
which the player re-records by hand.

These checks run during **playback**, not only while something is recording: two bodies can both be on
playback and still conflict, because their recordings come from different takes and were never
simultaneously true.

## 10b. Every recorded state is applied, however fast the cursor moves

A check only runs where a state is applied, so a skipped instant is a skipped check — and unlike a dropped
frame, that leaves an impossible history behind. So the cursor does not jump to the frame's target
instant: it **walks every intervening state in order**, several per frame if the rate demands it. Playback
bodies run no physics, only queries, so this is affordable at sane cursor speeds.

The sequence is the **union across bodies**, not per-body. A check asks what is solid where a body is and
which bodies are touching it, which needs every *other* body positioned at the same instant; letting a
densely sampled bullet-time body sub-step alone would test it against everyone else's stale pose. Span
edges are stops too, since authority changing hands is a discontinuity even when no sample sits on it.

> Granularity still slips in one place: a recording body gets exactly one physics step per frame however
> far the cursor travels, so at `|rate| > 1` it moves in coarse jumps while playback bodies around it
> sub-step finely. Fast *recording*, not fast replay, is where tunnelling risk lives.

## 11. No jumps

The cursor moves continuously — simulate, fast-forward, rewind, or follow a character; switching
characters happens within a single loop-time instant. Fast-forward and rewind are not seeks: they are the
cursor moving quickly, applying every state and running every check on the way (rule 10b). This is what
removes dangling causes — you can never observe a body at loop time it has not reached, because getting
there requires traversing it, and traversing it runs the checks.

**The one exception is a full reset**: discard every layer and put the cursor at 0. That is not a jump
into unknown territory, it is throwing the timeline away — the escape hatch for a state the player cannot
undo their way out of.

Arbitrary seek is kept working as a development oracle rather than a feature: if a cold read at a random
instant is exact, every legal cursor motion is too. Its real value is enforcing that **anything
gameplay-relevant is a channel** — animator state, cooldowns, AI target, ammo, door state, form, contact
set. State kept in a `MonoBehaviour` field shows up immediately as a seek mismatch, where normal play
would hide the desync for hours.

That extends to everything the player can *see* has happened, because entropy is visible. Bullet holes,
scorch marks, cracked glass, blood, ragdoll rest poses, smoke, spilled liquid, audio tails — a backward
pass has to un-make all of them, and any one that is fire-and-forget puts a hole in the wall before the
bullet arrives. Marks are bodies: `Latent` before their cause, `Manifest` after, carrying a contact set
like anything else, so erasing the bullet diverges the hole and it reverts. This is the largest practical
cost of the design, and it is a presentation cost — which is where players actually catch fakery.

Anything genuinely random — particle seeds, procedural noise, AI tiebreaks — is standing in for the
molecular detail rule 0 refused to model, so it must be recorded or deterministically seeded. That is not
an engineering nicety; it is the thing carrying the entropy argument.

## 12. Undo is a layer, and only a takeover opens one

**Taking control of a character is the only undoable action**, because it is the only one that *erases*
anything: that character's performance from the previous loop is superseded from the takeover point
onward, and rewinding cannot bring it back — rewinding only moves the cursor. Push the wrong crate and you
rewind; going forward again re-records over it. That is destructive too, but incrementally and visibly, so
it needs no separate mechanism.

So: one layer per takeover, holding every claim and divergence that takeover cascaded. Popping it
truncates all of them at once and restores the prior history byte-for-byte. Layers pop LIFO, so a layer's
spans are always a suffix of the recording order — undo is a truncation, not a rewrite, which is why the
whole undo stack is affordable rather than the two copies originally planned.

Rewinding and replaying within one layer leaves superseded spans in place, outranked by `Seq`. Correct,
but it accumulates; if it ever matters, a compaction pass can drop any span wholly covered by a
higher-`Seq` span in the same layer.

`Seq` is deliberately *not* rewound: a later re-recording must never reuse a retired span's `Seq`, or a
stale event could resurrect by matching it.

---

## Worked example: the turnstile

A character walks forward to a machine at 200 and carries on past it. Using the machine emits an
**inverted copy** — a distinct body, id derived from `(machine, 200, 0)` — which records backwards from
200. Two instances of one character coexisting is not a paradox: the forward view reads as a single
worldline running backwards into the machine and forwards out of it.

So the flip has no mechanism of its own — **it is a form transition, exactly like firing a bullet.** At
the instant of use the machine writes the copy as `Manifest` from 200 in the direction the copy will
record; the other side is unwritten and reads `Latent` by default. In practice you use the machine *by*
becoming the copy, so the cursor flips at 200 and the backward pass begins immediately, and by rule 4
everything else with history below 200 just plays back. If nobody ever records the copy, it has no spans,
reads `Latent` everywhere, and the trip simply did not happen.

The machine can be used any number of times, and each use mints a body that did not exist before. That
costs nothing, because rule 7 has no roster: the copy enters the timeline when its first span is written,
and popping the layer that made it (rule 12) removes it again.

**The machine is the one conservation violation in the game.** Everywhere else matter is conserved; here
one worldline folds back and the same matter is in two places on the same time slice. That is what makes
it a machine rather than a mechanic, and why misusing it destroys the ship.

The *interesting* failure is the original surviving but never walking into the machine. That is not an
origin failure — machine and character both still exist at 200 — so only the machine can detect it, and by
then the copy has run its whole backward pass over `[0,200]`, entirely behind the cursor. Rule 3 forbids
unwinding it. **So this is the one conflict that cannot be repaired**, and that is what earns the
explosion: the paradox has to be paid for forwards, because forwards is the only direction left to write
in.

The predicate is **proximity**: was the original's *recorded* position within the machine's radius at the
instant the machine was used? And it is the **same radius that gates entry**, which makes it satisfiable
by construction. A tighter gate would explode takes that were legal; a looser one would let you step in
from outside the check's range and be doomed whatever you did. One radius asks exactly "did you still come
here?" — nothing more.

Note the division of labour. Killing the character before the trip explodes nothing: the corpse never
reaches the machine, the copy's release has no cause, and it diverges to `Latent` through rule 8 like any
other uncaused release. An unmade trip is not a paradox. The explosion is only for the case where the
character is alive and demonstrably somewhere else.

## What is impossible by construction

- A body teleporting when re-recorded (rule 5).
- A state change landing on a span boundary rather than inside a span (rule 6).
- A blended state that never existed (rule 6).
- Matter appearing or disappearing (rule 7) — the turnstile excepted, and priced.
- A `Latent`/`Manifest` transition that nothing on the other side of it accounts for (rules 6, 8).
- An interaction that cannot be reproduced on replay (rule 1).
- A force or a wound from nowhere (rule 10), above the significance threshold.

## Still open

**If sub-stepping (rule 10b) ever costs too much**, the optimisation is to check on *write* rather than on
read: only a re-recorded range can create a new conflict, so marking the written range dirty bounds
validation by what changed instead of by how far the cursor travelled. Even then the *repair* stays lazy —
rule 3 forbids writing behind the cursor — so it buys eager detection, not eager repair. It would also
make cursor jumps safe again, if a reason to want them ever appears.
