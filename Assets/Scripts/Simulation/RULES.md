# Rules of time travel

The mechanics of `GAME.md`, stated as invariants. Every example uses a loop of 0–400 ticks; the real
loop is ~60 s, so ~3000.

Vocabulary: a **tick** is the one time coordinate — an int, possibly negative, 50 to the second. A
**body** is anything simulated. A body is **recording** (claimed, live-simulated) or **playing back**
(read from what was recorded). A **take** is one stretch of the world re-run over ground that has
already been run; every recording belongs to one, and a body keeps a **layer** per take it recorded in.

**Rules 0–6 and 12 are built and describe the code. Rules 7–11 and the turnstile are design** —
nothing in the project does any of it yet, and their vocabulary still needs bringing into line as each
one lands.

What exists, and where the rules live in it:

| | |
|---|---|
| `Core/History<T>` | one thing's recording within one take: a state per tick, growable both ways |
| `Core/Layers<T>` | that recording as a layer per take, and which layer answers for a tick |
| `Core/Takes` | the stack of takes and the undo cursor into it |
| `Core/SimStep` | what a component is told about a step |
| `Runtime/Sim` | the cursor, the stepping loop, undo and redo |
| `Runtime/SimBody` | record-or-replay, and which layer a recording goes into |
| `Runtime/SimComponent<T>` | one recordable aspect of a body |
| `Runtime/SimRigidbody` | position and rotation; the only component that touches physics |
| `Runtime/SimCharacter` | intent, and the rate the cursor runs at |
| `Runtime/CharacterSwitcher` | every character, and which of them the player is |
| `Runtime/Controls` | the keyboard |

---

## 0. Why any of this works: entropy, not prohibition

**The laws of physics are time-symmetric.** Reverse a trajectory and nothing breaks: `x(-t)` obeys the
same equations `x(t)` did. Gravity needs no handling at all — a reversed parabola is the same parabola.

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

Time direction is a property of a *take*, not of a body — of which pass laid it down. Since there is one
cursor (rule 1), two bodies are never simulated in opposite directions at once; "inverted" always means a
recording being played against live bodies. That is why conservation inside the live set is automatic,
and why every remaining problem in this document lives at the playback/live boundary.

## 1. There is exactly one cursor

Every body — recording or playing back — sits on the same tick. There are no per-body clocks.

This is what makes interactions reproducible: they always happen between bodies on the same tick, so
replaying the timeline reproduces them by construction.

## 2. Rate comes from the watched character

`Rate` is ticks of loop time per second of real time, signed, and it comes from the character being
watched. `1` is ordinary, `-1` runs the loop backwards, `0` freezes it, and **a frozen cursor records
nothing** — no loop time passed, so there is nothing new to describe. That is the whole of "time moves
when you move": a character with the superhot rule set drops to `idleRate` when the player is not
acting, and the world crawls with them.

The player's scrub folds in on top rather than replacing it. Scrubbing *with* a character adds to what
they were doing; scrubbing *against* them contributes their own rate nothing at all, because they are
not acting — their recording is playing:

```
own  = scrubbing against ? 0 : OwnRate
Rate = (own + scrub * |rate|) * (fast-forward ? 4 : 1)
```

Scaling the scrub by the character's own rate rather than making it an absolute number of ticks means
one notch is always "one more character's worth of speed", whoever you are.

**The cursor is two numbers.** `SimulatedTick` is the last tick actually simulated; `TargetTick` is
where the cursor wants to be, and moves by a fraction of a tick per frame. Each frame we step one tick
at a time until they are within one of each other — so a rate above 1 takes several steps in one frame,
and a rate below 1 takes none for a while.

That is why physics is driven from `Update` with `Physics2D.Simulate` called by hand, rather than from
`FixedUpdate`: Unity's clock will not run several fixed steps on request. `fixedDeltaTime` is set once
to match the tick and `simulationMode` is `Script`. **Rate is never a physics property** — a step is
always exactly one tick of exactly `SecondsPerTick`, whatever the cursor is doing.

A consequence worth remembering: **no physics runs between steps**, which is what makes it safe to move
transforms about for drawing.

> **Bullet time costs input resolution, not correctness.** Input is read once a frame, so a
> fast-forwarding character reads the same keys for every step inside that frame. The player really did
> hold one key for that whole frame, so the recording is honest — it is just coarser than acting at
> rate 1.

## 3. Nothing is ever written behind the cursor

**The master rule.** A recording grows in the cursor's direction, one state per tick, keyed by the tick
the step landed on. Nothing is ever written into a tick the cursor has already left, and no recording
is ever created whole.

Most of the other rules are consequences of this one.

## 4. Playback is the default; recording happens only where nothing answers

Moving through ticks a body already has a recording for is playback: the state is put back on the body
and the solver carries it there. A body records only when something claims it (rule 10), or when nothing
answers for the tick — the **frontier**.

The frontier is per body, not global. The first pass establishes a recording for every body across the
whole loop, most of them standing still, so afterwards the cursor almost never leaves known territory
and **rewind is pure playback**.

**A claim ends when the cursor turns against it.** A claim is the statement that this body writes
history while the cursor travels one particular way — that is what `RecordDir` is — so the moment the
cursor turns the other way, the body goes back to playback and taking it again means acting again.
That is one test at the top of every step, and it is deliberately a property of the cursor rather than
of the controls: it covers a backward seek, a character who lives the loop the other way round becoming
the one who drives it, and any body the reversal machine claims later, all as the same thing. Watching
somebody else is *not* one of them — who the player is looking at has nothing to do with who is writing
history, so switching character releases nobody.

**Playback still runs physics, and nothing is ever kinematic.** Every body is dynamic all the time. A
recording body is moved by the solver and whatever it does is the recording. A replaying body is handed
the velocity that lands it on its recorded pose — `(pose - where it actually is) / dt` — and the solver
moves it from there. Nobody writes collision response, and the difference between the two modes is one
line in `SimRigidbody`: who chooses the velocity.

**Kinematic is infinite mass**, which is why nothing may be it: a kinematic body wins every contact and
takes no reaction, so a crate could not be pushed by another crate and you could lean on your own past
self forever. Playback you cannot push is scenery, and nothing here is meant to be scenery.

Driving by velocity rather than by teleport is **self-correcting** — the aim is taken from where the body
really is, so a knock is undone by the next step's aim and playback holds its path without ever being
placed on it. `MovePosition` does the moving, since it skips gravity and damping and so lands an
unobstructed body exactly while still colliding, and the velocity is written alongside it because
`MovePosition` leaves none behind — and a body's velocity is what it keeps if rule 10 claims it.

> A crate is claimed at 290 and shoved across the floor, recording its slide over `[290,300]`.
> Rewinding below 290 invents nothing — the crate's *older* recording still covers that range, showing
> it standing where it always stood.
>
> Without that older recording the question has no good answer. Simulating a moving body while the
> cursor descends writes decreasing ticks, which reads forward as the crate sliding *back* into a shove
> that has not happened yet; freezing it reads as an uncaused stop. The first pass is what makes the
> case unreachable — and where it is reachable anyway, off the ends of recorded time, the body does
> record backwards. That is the one known hole in "seeking never records".

## 5. Velocity is never recorded, and never has to be restored

A body's recording is its **pose**: position and rotation, absolute, one per tick. No velocity, no
momentum, no derivatives with respect to anything.

Nothing seeds a velocity at a claim, because there is nothing to seed. A replaying body is dynamic and
driven at `(pose[tick] - where it actually is) / dt` every step (rule 4), so **it is already moving at
the right speed in the right direction at every instant**, claimed or not. Claiming stops choosing its
velocity for it; it does not give it one.

Under a descending cursor that drive aims at *decreasing* ticks, so the body is physically travelling
backwards along its own recorded path, and a claim inherits that. **That is the whole of reversal.** No
code anywhere flips a sign. The odd/even taxonomy — which quantities reverse under time reversal and
which do not — is a fact about physics this design never has to encode, because it stores none of the
odd ones.

A claim inherits the pose it was already showing, so **re-recording can never teleport a body**.
Seam-freedom at the near end is structural, not policed.

## 6. The newest layer wins, and takes are anonymous

A recording is never overwritten. It is written into a layer belonging to the take that made it, on top
of whatever was there. Reading a tick means walking down from the newest take on the stack and taking
the first layer that wrote it — **and nothing else**. No layer is ever held to be superseded; a take is
in force by being on the stack and out of it by being popped. That is what makes undo cost nothing to
store (rule 12), and it is why re-recording the same stretch a dozen times costs a dozen layers rather
than losing eleven recordings.

**Nothing records who caused a take**, because for the body that re-recorded, nothing needs to: it has
a newer layer over that stretch and wins by itself.

What that leaves is the body that did *not* re-record. A crate shoved in an earlier take goes on
replaying the slide after the shove has been re-recorded away, because its old layer is still the newest
thing it has. Naming a claimant on each take and voiding that claimant's takes from the re-recording
point was an attempt to repair this from the stack alone, and it cannot: the stack knows a character was
re-recorded, not whether they still shove the crate, so it deletes shoves that still happen exactly as
readily as ones that no longer do. **The repair belongs to rule 10.** A crate accelerating with nothing
touching it is a divergence, and re-simulating it from there is the only answer that stays true whether
the shove survived the re-recording or not. That check now exists, so this is repaired rather than
merely accounted for.

**A take opens every time the player takes a character** — see rule 12 for the whole of it. Two things
follow, and both are the point:

- **Only the player opens a take.** Not a shove: a crate claimed by divergence joins the take the shove
  belongs to, because the shove required someone to be acting.
- **A take is one performance**, which is precisely the thing undo has to undo.

**Interpolation never crosses a take boundary**, so boundaries are the only true discontinuities in the
game — and a discontinuity is impossible read in *either* direction, unlike a steep continuous ramp,
which is merely improbable in one (rule 0). Two consequences:

**A state change must happen inside a take, never on its boundary.** One recorded tick is enough;
nothing needs animating, since the world is only ever observed at tick resolution. This is the whole
difference between a corpse reassembling and a corpse blinking into existence.

**Both ends of a recording must be accounted for.** Rule 5 handles the near end by inheriting. The far
end — where a re-recorded stretch stops and the body's own older layer resurfaces underneath — inherits nothing and
cannot: inheriting at both ends would make re-recording a two-point boundary value problem instead of
an initial value problem, which is the entire thing recording-based time travel exists to avoid. So the
far end is repaired rather than prevented — a body whose state jumps with no cause is exactly what rule
10 looks for, so crossing such a seam claims the body and it re-simulates from there. That costs the
player a performance, but it is never a silent violation.

## 6b. Drawing

Not a rule about time, but it constrains one. The display runs **one tick stale**, interpolating across
the last step taken: from the tick the cursor left, to the tick it is on. Both are certainly simulated,
so there is no frontier case, no missing future state and no fallback, and a recording body is drawn
exactly like a playback one.

The price is 20 ms of lag on everything including your own character, and a bounded sub-tick wobble
when the cursor reverses, because the pair being interpolated only swaps once the next step happens.

This is why `SimRigidbody` draws to a separate `view` transform rather than the rigidbody's own: a
recording body's drawn pose is *behind* its solver pose, and writing that back would feed a stale
position into the next step.

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

> **This rule now carries a load nothing else can.** Takes are anonymous (rule 6), so a body replaying a
> consequence whose cause has been re-recorded away — the crate still sliding from a shove that no longer
> happens — is repaired here or not at all. It is a divergence like any other: something is moving with
> nothing to move it. The bookkeeping alternative was tried and thrown out, because a take stack can tell
> you a character was re-recorded but not whether they still shove the crate.

`Claim(body, direction, at)` is the whole of it. Triggers: the player touches the controls; the body is at
its frontier (rule 4); contact with something already recording; recorded state the live world cannot
account for.

**The contact set is the whole test.** Contact is a fact about the world rather than about this body's
recording, and the two questions it answers are the only two ways a recording can be wrong. A partner
the recording names that is no longer near means the cause of what is about to be replayed is gone. A
partner the recording does not name that is touching now means something new is interfering.

**Each direction asks whatever is trustworthy for it**, and neither has to look past the bodies actually
involved. Absence must be *measured*: the solver's manifold records how the bodies came together, and
since contact resolution only ever separates — the velocity pass stops approach, the position pass pushes
penetrating bodies apart over several steps — a replayed overlap ends each step wider than it was
recorded, so the manifold is dropped, the drive re-aims into it, and it is dropped again. Presence
flickers in the *middle* of a replayed contact, where no boundary tolerance can rescue it. Distance
between shapes carries no such history. The partners to measure to are the handful the recording names.

Presence, on the other hand, is exactly what the manifold is good for. It can only ever under-report a
replayed contact, never invent one, so a flicker costs at most a tick's delay in noticing an intruder.
And it needs no tolerance at all: the set it gives before the step is the set the previous tick recorded
from it, nothing having moved in between, so the two agree exactly rather than approximately.

**Do not measure the pose,** and do not measure velocity. Penetration depth fails to reproduce for the
same reason presence did: nothing ever pushes two bodies together to restore an overlap that was
recorded, so a pose tolerance tight enough to be useful fires on contacts that were replayed faithfully.
Velocity answers the opposite of the question, because the solver stops a body in proportion to how hard
it was pushing — so a body being shoved reads as motionless.

**The decision belongs before the solver, not after it.** A body claimed after being driven has already
been given its recorded pose's velocity, and a frictionless world keeps it: the crate whose shove was
re-recorded away coasts off across the room anyway. Asked first, the body is never driven at all, and the
velocity it inherits is the one it legitimately had.

Running first is also what puts a tolerance on the measured side. The world stands a step short of the
configuration being replayed, so a partner the recording expects has only to be **nearby** rather than
touching — it may still be closing. Nothing corresponding is needed on the manifold side, which compares
two readings of the same query at the same instant. A partner that was touching on the tick behind is a
contact ending rather than a new one, and is let alone.

A claimed body without a controller is **inert, not dead**. Inert means *no intent source* — not no
physics. It keeps its momentum, coasts, gets pushed, and dies. It stays a character and stays a switch
target; taking one over is just attaching an intent source to a body that was already claimed.

> Shove a crate across a doorway, then switch to another character and carry on. The crate is inert but
> must finish sliding, recording as it goes: the slide is a consequence the player caused, and the other
> characters have to be able to collide with the crate while it is still moving. A crate that froze the
> moment you looked away would be a different puzzle.

**Two predicates** detect divergence, each validating its own channel against the live world:

- **Contact set.** Compare the contacts recorded at this sample against the contacts the live world
  offers. A recorded partner that is no longer there, or a live partner that was not recorded, means the
  recorded motion is not the motion this body would now have. Something solid where the recording is
  about to go is the second case, so interpenetration needs no predicate of its own.
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

> **Built so far:** asked before the state is applied. Shape-to-shape distance to each partner the
> recording names, against a single `causeRange`; and the solver's contacts for anything touching that
> neither this tick nor the one behind recorded. Neither walks the world — one iterates a handful of
> recorded ids, the other what is already touching — so the cost is in the contacts, not the body count.
> Static geometry is left out, since it cannot change and touching it is never evidence that history has.
> A body standing in a pose no recording covers — the first tick of a rewind into recorded ground — is
> asked only about the partners its own recording names, since its contacts contradict nothing. Not yet
> an accumulator, so a sub-threshold lean never claims however long it goes on.

**The check runs in both directions**, on the live body as well as the playback one — though rule 4 now
does most of that work by itself. Playback is dynamic and takes a real reaction, so a character bracing
against a recorded door is pushed back by it with the door's own mass and nobody has to arrange it.
What the check still owes is the *other* half: noticing that the door was moved, and ending its
recording rather than letting it be dragged off its path indefinitely. Accumulating `Δv` handles
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

One comparison covers both. Two recordings that were never simultaneously true put a body next to a
partner its own recording does not name, which is the second question; a **cause that is gone** leaves a
named partner nowhere near, which is the first. Nothing has to work out whose fault it was, and no take
numbers are involved — which is why the bookkeeping alternative was not needed after all.

The second question is the one a top-down game with no gravity barely needs, and the first is the one it
lives on: nothing falls, nothing topples, and a body only moves while something is pushing it.

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

## 12. Undo is a take, and a take is one re-recording

**Re-recording is the only undoable action**, because it is the only one that *erases* anything: a
stretch that had a recording now has a newer one over it (rule 6), and rewinding cannot bring the old
one back — rewinding only moves the cursor. Everything else is additive. Driving a character across
ground nobody has covered destroys nothing, so there is nothing there to take back; if you dislike what
you did, you rewind and re-record it, and *that* is the undoable step.

**A take opens every time the player takes a character.** Nothing else opens one, and there is no test:
taking control *is* the undoable act, so the player always knows exactly what one press of undo costs.
Testing whether the ground was already recorded is tempting and wrong — it leaves a performance over
fresh ground with no take to lift, and that first pass is then the one thing you can never take back.

**Nothing a body does to another body opens a take.** A crate shoved into recording joins the take the
shove belongs to, since the shove required the player to be acting, and it needs no layer of its own to
be undone: lifting that take takes the shove with it.

Opening a take gives every body an empty layer in it, which is also what makes a claim structurally
safe. A claimed body writes into the live take and can never meet or hole its own layer there, because
it has none yet.

**Which take a write goes into follows the same distinction.** Replacing something goes into the live
take, because the live take is what undo lifts. Ground nobody has covered continues whichever layer
already ends next to it, however far down the stack that is — a layer is one unbroken stretch, and
starting the same body higher up would leave a hole in the one below and put untouched ground on the
undo stack, where there is nothing to undo.

So: one take per re-recording, holding every layer started while it ran. **Undo moves no data.**
The recording is still sitting in its layers; `Live` — how far up the stack is live — is the only
thing that changes, and everything underneath becomes visible again on its own.

That is why the whole undo stack is affordable rather than the two copies originally planned, and it is
why nothing needs restoring. Both directions are one integer:

| | |
|---|---|
| **Undo** | wind the cursor back to where the take opened, *then* take it off the stack |
| **Redo** | put it back on, *then* wind forward to the far end of what it ran |

The order is opposite on purpose. The recording being wound through has to be on the stack for the wind
to show anything — undo the other way round and the bodies sit frozen while the cursor slides backwards.

**A wind is not a performance.** Nothing is claimed while one runs, nothing records, and input is
ignored, so it cannot write over the very stretch it is winding through. After either, the player is
watching rather than driving, and re-recording opens a new take — which is exactly where the redo branch
should die, and does: opening a take drops every undone take above it, and that is the only place a
recording is ever actually thrown away.

Rewinding and re-recording leaves superseded ticks in place, outranked by the newer layer. Correct, but
it accumulates; if it ever matters, a compaction pass can drop any layer wholly covered by a newer one.

Take numbers are indices into the stack, so they are reused after an undone branch is dropped. That is
safe only because the layers filed under those numbers are dropped at the same moment.
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
