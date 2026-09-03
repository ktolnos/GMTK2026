This is a puzzle game about re-recording timelines.
Think time travel + Outer Wilds + Superhot + Hotline Miami + Tenet.
The time loop is small-ish (probably 1 minute).

`RULES.md` is the normative document — it states all of this as invariants and works through the
consequences. This file is the pitch and the reasoning behind it.

## The premise

The laws of physics are time-symmetric. What is not symmetric is likelihood: a bullet flying out of a
wall and into a muzzle breaks no law, it just needs the wall's heat and the room's sound to conspire.
So the game never violates physics locally — it only ever shows you the improbable branch. Every
backwards thing you see is legal and merely absurdly lucky.

Recording is what buys the luck. We can't simulate molecules, so we can't generate a fine-tuned reversed
trajectory — but we already watched it happen forwards, so we can replay it backwards exactly. Every
object we keep on playback is a butterfly effect we've refused to have.

That gives one line, and it is not entropy: **improbable is allowed and is the whole aesthetic;
contradictory is not.** A corpse standing up because you erased its killer is fine. A corpse standing up
in zero time is not. Where a contradiction can't be unwound, it gets paid for — that's what the ship
exploding is for.

Two things follow that shape the whole codebase:

- **Matter is conserved.** Nothing is created or destroyed. Instantiate/Destroy are presence
  optimisations; an object that isn't in the world is *latent* rather than absent — still in the gun,
  lying on the floor as a corpse, evaporated into the air. There is no "this object doesn't exist here"
  concept, and no roster to pre-populate either: latent is just what the timeline reads when nothing
  has been recorded, so objects still enter it the first time something writes them.
- **Everything the player can see happened has to be recorded and un-makeable.** Entropy is visible:
  bullet holes, scorch marks, blood, ragdoll rest poses, smoke, audio tails. A backward pass has to
  un-make all of them, so none of them can be fire-and-forget. This is the biggest practical cost of the
  design and it is where players will catch fakery if we get it wrong.

## The mechanics

The Simulation records everything in the scene and can play it back exactly, from any point in time, at
any timescale including negative.

There is **one cursor** — a single loop-time position shared by everything. Timescale comes from the
character you are currently watching, and it drives that shared cursor, so a character with a negative
timescale rewinds the world rather than running privately backwards. This is what makes interactions
reproducible: they always happen between objects at equal loop time. "Time moves when you move" is just
a rate of 0 when you hold still.

Objects are either recording or playing back. Playback is the default and is what almost everything is
doing almost all the time; an object only records when something claims it. The player touching the
controls claims a character. Contact with something already recording claims the thing it touched. And
an object whose recorded history stops making sense in the live world claims itself and re-simulates
from there.

That last one is the interesting one, because it's also the correct physics: perturbing an improbable
trajectory should destroy it. Touch a backwards-running enemy and it stops being backwards-running —
it goes inert, records forward like everything else, and loses the rest of its performance. So there is
a real cost to interfering, and it is legible.

Objects don't have an inherent time direction. Direction is a property of a *recording*, of which pass
laid it down. So "inverted" always means "recorded during a backward pass, now being played back", and
there is never a moment where two things are being simulated in opposite directions at once.

Example: you move backwards through time and shoot. The bullet is recorded during a backward pass, so
played forwards it flies out of the wall into your gun. It hits an enemy on the way, which claims the
enemy — it records backwards too, so forwards it reads as an enemy that was wounded and gets up healed
as the bullet leaves it.

Switching between characters happens within one time instant, so everything always has something
recorded at every instant — inactive characters record NOOP.

Players sometimes overwrite a recording by touching the controls when they didn't mean to. CTRL+Z rewinds
to before the overwrite and restores what was there. Taking over a character is the only action that
*erases* anything, so it is the only thing that needs undo — everything else you fix by rewinding and
re-recording. There is a full reset too, for states nobody can undo their way out of.

## Architectural considerations

There is one time coordinate — the **tick**, an int, 50 to the second — and everything records one state
per tick. That is what removes the sample-density problem: nothing needs a search, a lookup is an array
index, and interpolation is between two adjacent ticks. A character in bullet time does not sample
faster; the world moves slower around them.

Interpolation must never cross a recording boundary — those joins are real discontinuities.

Undo is a stack of takes, not a pair of history copies. A take opens every time the player takes control
of a character, so taking control is exactly the undoable act and nothing else opens one — everything
else that records joins the take it happens inside, and is undone with it. Every
recording made under it goes into a layer of its own rather than overwriting what was there. So undo
moves no data: how far up the stack is live is the only thing that changes, and the layer underneath
becomes visible again by itself. That is cheap enough that the whole undo stack is affordable rather
than just current + prev.

Takes are anonymous, and reading a tick is nothing more than the newest layer that has it. Nothing
tries to work out from the stack whose fault a recording was — a stack can tell you a character was
re-recorded, but not whether they still shove the crate they shoved last time. Repairing a consequence
whose cause has been re-recorded away is a job for causality checking, not for bookkeeping.

Every gameplay-relevant piece of state has to live in the timeline rather than in a MonoBehaviour field —
animator state, cooldowns, AI target, ammo, door state, form, and the set of things currently touching an
object. Anything genuinely random has to be recorded or deterministically seeded; randomness is standing
in for the molecular detail we refused to model, and it's exactly the part that makes reversal fragile.

Velocity is the exception, and deliberately: it is never recorded and never restored either. Playback is
not teleported onto its recorded poses, it is *driven* at the velocity that lands it on the next one, so
a body is always genuinely moving and a claim simply stops choosing its velocity for it. Under a
descending cursor that drive aims at decreasing ticks, so the body is already travelling backwards along
its own path — the entire reversal mechanism, with no code anywhere flipping a sign.

Nothing is ever kinematic, because kinematic is infinite mass: playback would be scenery you could lean
on forever. Staying dynamic is what makes it interactable. Whether a recording still holds is a separate
question, asked before each state is applied and answered by who is touching the body rather than by
where it is: a partner the recording expects and cannot find, or one it never recorded that is touching
now, means the world would not let that recording happen, and it ends there.

The whole history should be serializable to disk, and every object records its archetype so a save can be
materialised from the file alone. Bodies carry a stable id for the same reason, so the undo stack still
means something after a round trip, and so a body pooled out and back in is the same character.

Objects come and go from the scene for performance, including at their destruction time when running
backwards, but that is instantiation only — the timeline itself never gains or loses anyone.
