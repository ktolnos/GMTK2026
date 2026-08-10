# Rules of time travel

The mechanics of `GAME.md`, stated as invariants. Every example uses a loop of 0–400 (in
`LoopTime` raw units; the real loop is ~60 s = 600000).

Vocabulary: **loop time** is the position on the timeline being recorded — the only time coordinate
that exists. **Real time** is frames going past. A **body** is anything simulated. A **span** is one
continuous recording pass over one body. A body is **recording** (claimed, live-simulated) or
**playing back** (sampled from history).

---

## 1. There is exactly one cursor

Every body — recording or playing back — sits at the same loop time. There are no per-body clocks.

This is what makes interactions reproducible: they always happen between bodies at equal loop time,
so replaying the timeline reproduces them by construction.

## 2. Rate comes from the watched character

The cursor advances by `rate × dt` each physics step, where `rate` is the *watched* character's
signed timescale. The watched character need not be the recording one — you can watch a body that is
playing back.

Rate `0` freezes the timeline, and **a frozen cursor records nothing**: no loop time passed, so
there is no new loop time to describe.

> **Bullet time is free.** A character at rate `0.2` takes five physics steps per unit of loop time
> that a rate-`1.0` character covers in one. Watched, it looks normal and the world crawls. Watched
> from someone else, its samples are five times denser, so it replays five times faster at full
> fidelity. Nobody wrote any code for that.

`|rate| > 1` costs history resolution, not correctness — a recording body covers more loop time per
physics step, so its samples land further apart. Physics always steps exactly once per frame; **rate is
never a physics property.** `Time.timeScale` and `fixedDeltaTime` are never touched.

## 3. Nothing is ever written behind the cursor

**The master rule.** Every span grows in the cursor's direction, one sample per physics step, keyed
by the loop time that step landed on. Nothing is ever written into loop time the cursor has already
left, and no span is ever created whole.

Most of the other rules are consequences of this one.

## 4. Recording happens at the frontier, and the frontier is rarely reached

Moving through loop time that is already recorded is pure playback — no physics. Moving *beyond* the
recorded frontier records. There is no separate "replay mode"; there is only whether the cursor is
inside known territory.

The first take establishes history for every body across the whole loop, most of them recording NOOP.
So afterwards the cursor almost never leaves known territory: **rewind is pure playback and needs no
physics at all.**

> A crate is claimed at 290 and shoved across the floor, recording its slide over `[290,300]`. Rewinding
> below 290 invents nothing — the crate's *older* span still covers that range, showing it standing
> where it always stood. The new span begins at 290 with an inherited sample (rule 5), so the slide
> starts exactly where the crate was sitting, caused by the shove.
>
> Without that older history the question would have no good answer. Continuing to simulate a moving
> body while the cursor descends writes decreasing loop times, which reads forward as the crate sliding
> *back* into the shove that has not happened yet; freezing it reads as an uncaused stop. Both are
> seams. The first take is what makes the case unreachable.
>
> A body with no history below its origin — a bullet spawned at 290 — simply does not exist there
> (rule 10) and is released.

## 5. A claim inherits its first sample

When a body is claimed, the live body is seeded from whatever playback was already showing, and that
seed is written as the new span's first sample.

So old and new authority **agree exactly at the instant they meet**. Re-recording can never teleport
a body. Seam-freedom is structural, not something to police.

> X is recorded walking through a doorway at 100. An inverted copy shuts that door, so X's position
> at 100 is now inside it. X is claimed at 100 and re-records backwards, getting pushed aside. At
> exactly 100 X is where it always was; below 100 it diverges; above 100 the original run stands.

The one case with nothing to inherit is a body revived because its void stopped holding (rule 9):
there is no playback inside a void. It inherits the state at the void's start instead — what it had
when it died, which is the sensible reading of being un-killed.

## 6. Where two spans overlap, the later recording wins

Each span carries a monotonically increasing `Seq`. Lookup takes the highest `Seq` covering an
instant. That one rule covers overwriting a previous take, re-recording the same stretch twice in
one take (the cursor doubling back), and destroying a body.

**Interpolation never crosses a span boundary.** The join between a forward pass and a backward pass
over it is a real discontinuity — full HP one side, dead the other — and must snap.

## 7. Void means destroyed, and grows with the cursor

A **void span** says "this body does not exist over this range". It outranks older recordings by
`Seq`, which is how a death propagates into loop time a body used to occupy.

Void is *only* for destruction, and like every span it grows with the cursor. Absence of any span
also means non-existence; the difference is that a void span overrides.

> An inverted agent kills an enemy at 130 on its way down. The enemy is voided over `[0,129]` and
> keeps its original recording over `[130,400]`. Read forwards, that is a resurrection at 130 — which
> is the intended inverted-causality signature, not a gap to paper over.

A body exists at the instant it is destroyed; the void begins one unit beyond, in the cursor's
direction. Otherwise events caused by it at that instant would lose their cause (rule 9).

## 8. A body keeps its origin

Truncation may only ever remove the end of a span *away* from where the body came into being.

> A bullet's span must have its muzzle at one end — `Min` for a forward shot, `Max` for an inverted
> one. A bullet may never appear to be emitted by something that is not a gun.

## 9. Causality lives on the thing it describes, not in an event log

There is no event log. Everything one would have carried is single-valued, so it fits on what it
describes:

- a body has exactly one **origin** — what spawned it, and when (`Origin`, `OriginAt`);
- a destruction has exactly one **cause** — what did it, and when (`Span.CausedBy`, `CausedAt`).

Each holds only while the other party still exists at that instant. When one stops holding the recorded
outcome could not have happened — but the two failures have opposite repairs:

- **origin gone → void the body.** Something with no cause never happened, so there is nothing to
  re-simulate. This is an ordinary void span growing with the cursor, in the current layer, so undo
  restores it for free. Not a paradox: just a later recording outranking an earlier one (rule 6).
- **cause gone → claim the body.** Being un-killed leaves a body that has to live on from there, so it
  revives inert and re-simulates (rule 5).

> A guard recorded alive over `[0,400]` fires a bullet at 150, so the bullet's origin is `(guard, 150)`.
> You take over at 100, move forward, and kill the guard at 120. The void grows `[120,400]`, covering
> 150, so the bullet's origin no longer exists — and the bullet is voided from 120 onward, which is its
> entire existence. It simply never appears.

That repair never writes behind the cursor, in either direction. Forwards, a newly broken origin is
necessarily ahead. Backwards, a void covers only the traversed range (rule 7), so an origin instant is
either still ahead of the cursor or outside the void and not broken at all.

**Origin existence is necessary, never sufficient.** It is only the part answerable from the timeline
alone; whether the spawn *actually happened* is spatial, and belongs to the spawner (rule 11).

Spawns triggered by a *death* invert the condition, so they name the killer rather than the corpse: a
dropped item's origin is the enemy's `CausedBy`, at its `CausedAt`. Erasing the killer then revives the
enemy and voids the drop in one motion. Naming the corpse gets the polarity backwards — the drop would
be flagged precisely when it is legitimate.

> X is shot dead at 150 and voided for the rest of the loop. A later take removes the bullet. Without
> a cause on the void span, X stays dead forever with no killer, silently deleting everything X was
> meant to do over `[151,400]`. With one, the void stops holding and X is revived.

Anything genuinely per-instant — "this HP drop came from *that* bullet" — is not recorded at all. The
component owning the channel notices a change it cannot account for and diverges, which is the same
mechanism as a body finding its recorded position inside a door that is now shut (rule 11).

Consequences pointing behind the cursor are deferred rather than written:

> A bullet fired at 100 and recorded flying to 160 is absorbed by an inverted character at 150.
> Voiding the low side would erase the muzzle (rule 8); writing the high side is forbidden (rule 3).
> So the backward take claims the bullet for a single sample to record an `Absorbed` flag — ordinary
> state, which dies with its span. On a later forward pass that flag is sampled at 150 and the tail is
> retired from 151 as the cursor moves. Final span `[100,150]`: fired from its muzzle, vanishing into
> the character. The repair is idempotent — the guard is just whether the bullet still exists past the
> hit — so it needs no once-only delivery.

The timeline is therefore transiently stale between the two passes. That is accepted: it records what
has actually been simulated, and corrects when simulation next reaches the region.

## 10. Existence is derived, never accumulated

The set of bodies alive at an instant is computed from the timeline every step and reconciled against
what is instantiated. It is never accumulated from spawn/destroy events.

This is why a body destroyed at one instant reappears by itself when the cursor descends past that
instant again, and why arbitrary cursor motion takes the same code path as a single step.

Pooling is a throughput optimisation for bullets. It is not part of the correctness story — but
**every body records its archetype**, because loading a save must be able to materialise a body from
the file alone.

## 11. Claiming is one mechanism with several triggers

`Claim(body, direction, at)` is the whole of it. Triggers: the player touches the controls; contact
with something already recording; an event's precondition fails; recorded state became impossible.

A claimed body without a controller is **inert, not dead**. Inert means *no intent source* — not no
physics. It keeps its momentum, keeps coasting, gets pushed, and dies. It keeps existing, stays a
character, and stays a switch target. Taking one over is just attaching an intent source to a body
that was already claimed. This is also what the first take is — every body claimed, almost all of them
doing nothing.

> Shove a crate across a doorway, then switch to another character and carry on. The crate is inert but
> must finish sliding, recording as it goes: the slide is a consequence the player caused, and the other
> characters have to be able to collide with the crate while it is still moving. A crate that froze the
> moment you looked away would be a different puzzle.

Because a claimed body records in the *cursor's* direction, changing the watched character's
direction closes and reopens every open span, leaving a seam at that instant (rule 6).

A component detects divergence by validating **its own channel** against the live world:

- a transform asks "is my recorded position inside something solid now?";
- an HP channel asks "my recorded HP drops between these two samples — is there a live damage dealer
  that accounts for it?"

For the second to be exact rather than a guess, **every damage source must be discoverable at the
instant it deals damage** — by overlap, by the raycast a laser already performs, by a registry of live
hazards, whatever suits it. Nothing is forced to grow a hitbox it would not otherwise want, but nothing
may deal damage from nowhere.

Divergence claims the **whole body**, not the offending channel. Damage usually transfers momentum, so
HP and transform move together; splitting them would escalate to the whole body in most real cases
anyway. The cost is that a diverged character goes inert and loses the remainder of its performance,
which the player re-records by hand.

These checks run during **playback**, not only while something is recording. Two bodies can both be on
playback and still conflict, because their recordings come from different takes and were never
simultaneously true.

## 11b. Every recorded state is applied, however fast the cursor moves

A check only runs where a state is applied, so a skipped instant is a skipped check — and unlike a
dropped frame, that leaves an impossible history behind. So the cursor does not jump to the frame's
target instant: it **walks every intervening state in order**, several per frame if the rate demands
it. Playback bodies run no physics, only queries, so this is affordable at sane cursor speeds.

The sequence is the **union across bodies**, not per-body. A check asks what is solid where a body is,
which needs every *other* body positioned at the same instant; letting a densely sampled bullet-time
body sub-step alone would test it against everyone else's stale pose. Span edges are stops too, since
authority changing hands is a discontinuity even when no sample sits on it.

> The one place granularity still slips is recording at `|rate| > 1`. A recording body gets exactly one
> physics step per frame however far the cursor travels, so it moves in coarse jumps while playback
> bodies around it sub-step finely — fast *recording*, not fast replay, is where tunnelling risk lives.

## 12. No jumps

The cursor moves continuously — simulate, rewind, or follow a character; switching characters happens
within a single loop-time instant. It never teleports.

This is what removes dangling origins. You can never observe a body at loop time it has not reached,
because getting there requires traversing it, and traversing it while the body is claimed records it
(rule 4).

Arbitrary seek is kept working anyway, as an oracle rather than a feature: if a cold read at a random
instant is exact, every legal cursor motion is too. Its real value is enforcing that **anything
gameplay-relevant is a channel** — animator state, cooldowns, AI target, ammo, door state. State kept
in a MonoBehaviour field instead of the timeline shows up immediately as a seek mismatch, where normal
play would hide the desync for hours.

## 13. Undo is a layer, and only a takeover opens one

**Taking control of a character is the only undoable action**, because it is the only one that
*erases* anything: that character's performance from the previous loop is superseded from the takeover
point onward, and rewinding cannot bring it back — rewinding only moves the cursor.

Nothing else needs undo. Push the wrong crate and you rewind; going forward again re-records over it.
That is destructive too, but incrementally and visibly, so it needs no separate mechanism.

So: one layer per takeover, holding every claim, void and divergence that takeover cascaded. Popping
it truncates all of them at once and restores the prior history byte-for-byte. Layers pop LIFO, so a
layer's spans are always a suffix of the recording order — undo is a truncation, not a rewrite, which
is why the whole undo stack is affordable rather than the two copies originally planned.

Rewinding and replaying within one layer leaves the superseded spans in place, outranked by `Seq`.
Correct, but it accumulates across many rewinds. If it ever matters, a compaction pass can drop any
span wholly covered by a higher-`Seq` span in the same layer.

`Seq` is deliberately *not* rewound: a later re-recording must never reuse a retired span's `Seq`, or
a stale event could resurrect by matching it.

---

## Worked example: the turnstile

A character walks forward to a machine at 200 and carries on past it. Using the machine emits an
**inverted copy** — a distinct body, id derived from `(machine, 200, 0)` — which records backwards
from 200.

Nothing is voided. Two instances of one character coexisting is not a paradox: the forward view reads
as a single worldline running backwards into the machine and forwards out of it. So the flip has no
mechanism of its own — **it is a spawn, exactly like firing a bullet.**

The copy's origin is the original, so a later take killing the original at 150 voids the copy by rule 9
like any other uncaused spawn. The copy's existence lies ahead of the cursor, so the void reaches it.

The *interesting* failure is a different one: the original survives and simply never walks into the
machine. That is not an origin failure — the machine and the character both still exist at 200 — so
only the machine can detect it. And by the time it does, the copy has already run its whole backward
pass over `[0,200]`, which is entirely *behind* the cursor. Rule 3 forbids voiding it.

**So this is the one conflict that cannot be repaired by unwinding**, and that is what earns the ship
exploding. The paradox has to be paid for forwards, because forwards is the only direction left to
write in.

The predicate is **proximity**: was the original's *recorded* position within the machine's radius at the
instant the machine was used? And it is the **same radius that gates entry**, which is what makes it
satisfiable by construction. A gate tighter than the check would explode takes that were legal; a looser
one would let you step in from outside the check's range and be doomed whatever you did. One radius asks
exactly "did you still come here?" — nothing more.

Note the division of labour. The copy's *origin* is the character, not the machine, so a take that kills
the character before the trip voids the copy for free through rule 9's cheap existence check, and nothing
explodes — an unmade trip is not a paradox. The explosion is only for the case where the character is
alive and demonstrably somewhere else.

## What is impossible by construction

- A body teleporting when re-recorded (rule 5).
- A body appearing out of nothing (rules 8, 12).
- A blended state that never existed (rule 6).
- An interaction that cannot be reproduced on replay (rule 1).

## Still open

- **What the copy does above the machine's instant.** The intended turnstile shape has it existing only
  *below* the instant it was emitted, but a spawn is claimed in the cursor's direction like everything
  else, so a copy nobody switches to records forward as an inert body instead. Handing control over
  immediately produces the right shape; leaving it alone produces a body with history on both sides. Not
  paradoxical, just not the intended figure.
- **If sub-stepping (rule 11b) ever costs too much**, the optimisation is to check on *write* rather
  than on read: only a re-recorded range can create a new conflict, so marking the written range dirty
  bounds validation by what changed instead of by how far the cursor travelled. Note that even then
  the *repair* stays lazy — rule 3 forbids writing behind the cursor — so it buys eager detection, not
  eager repair.
