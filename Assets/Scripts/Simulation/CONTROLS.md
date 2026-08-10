# Controls

Top-down, Hotline Miami handling. Everything below is read straight from the keyboard and mouse by
`PlayerIntentSource`, so the playtest scene needs no input wiring.

Build the scene with **Chronomancers > Build Playtest Scene**, then press play.

## Acting

| Input | Does |
|---|---|
| `W` `A` `S` `D` (or arrows) | Move, both axes. There is no jump. |
| Mouse | Aim. Independent of movement — you can walk one way and cover another. |
| Left mouse, or `J` | Fire |
| `E` (or `K`) | Interact: open or shut a door, use the reversal machine |

Only the character you **control** acts. Everything else that is recording is *inert*: no intent, but
still physics. It keeps its momentum, gets pushed, and can still be shot.

## Moving through the loop

| Input | Does |
|---|---|
| `1`–`9` | Take control of that character. **This is the only undoable action.** |
| `Tab` | Watch the next character without taking control |
| `Space` | Pause. A frozen cursor records nothing at all. |
| `R` (hold) | Run loop time *against* the way the watched character experiences it |
| `Left Shift` (hold) | Move through loop time faster, whichever way it is going |
| `Ctrl+Z` | Undo the last takeover, and everything it cascaded |
| `F5` / `F9` | Save / load the timeline |

**Watching is free; controlling is not.** Watching only changes the cursor's rate, which is why `Tab`
costs nothing. Taking control re-records that character from this instant onward, which erases what
they did in the previous take — so it is the one action that opens an undo layer.

**Moving takes control.** You do not have to press a number key to resume acting. Pressing a movement,
fire or interact key claims the character you already control, which matters after a rewind: reversing
closes every open span, so the body you were driving goes back to being played back and ignores input
until you take it again. Aim alone does not count — a mouse always has a position, so if it did, the
world could never stand still.

**`R` is relative, not absolute.** It negates the watched character's own rate rather than forcing the
cursor backwards. Watching an ordinary character it rewinds; watching an inverted copy, whose rate is
already negative, it runs the loop *forwards* again. `Left Shift` only scales magnitude, so it speeds
up a backward cursor instead of flipping it.

**Rate comes from whoever you are watching, not whoever you control.** Bob's rate is `0.35`, so
watching Bob makes the whole world crawl while he moves normally; watching Alice puts it back to
`1.0`. Nothing about that is special-cased — it is one field on one component.

## Superhot mode — time moves when you move

Per character, on `SimRate`. Off by default so it does not change the baseline feel.

| Field | Meaning |
|---|---|
| `timeMovesWhenYouMove` | Enable it |
| `rate` | Rate while acting, as usual |
| `idleRate` | Rate while standing still. Signed off `rate`, so an inverted character still creeps backwards. |
| `activeHoldSeconds` | How long after a keypress you still count as moving. Smooths the stop-start. |

It applies only while you are **both driving and watching** that character. Watching someone else must
not freeze the world because *you* are standing still — a body on playback is always moving in the only
sense that matters, in that its recording is running.

`idleRate = 0` is legal and freezes the cursor completely, but a frozen cursor records nothing at all
(rule 2), so a motionless character accrues no history for anyone to replay later. A small non-zero
value keeps the world creeping and history accumulating, which is why the default is `0.05`.

## Reading the HUD

Top line is the cursor: loop position in seconds, raw fixed-point units, the current rate, and the
direction.

Second line is the state: who you control, who you watch, how many bodies are live, how many are
recording, how many exist in the timeline at all, the current `Seq`, and the undo depth.

Below that is the **span strip**, one row per body. This is the real debugging tool — most things that
can go wrong here are a span with the wrong range, direction or precedence, and none of those are
visible by watching the game.

| Colour | Means |
|---|---|
| Green bar | Recorded forwards |
| Blue bar | Recorded backwards |
| Red bar | Void — the body explicitly does not exist over that range |
| Faint track | No span at all, which also means non-existent |
| White line | The cursor |
| Yellow name | That body is recording right now |

Spans are drawn oldest-first, so a later recording paints over an earlier one — which is exactly the
precedence rule, and means what you see is what a lookup would return.

## Things worth trying

- **Bullet time.** `Tab` to Bob and move. His samples are five times denser than Alice's, so when you
  switch back and watch Alice, Bob replays fast at full fidelity.
- **Shut a door on your own past.** Walk Alice through the doorway, rewind, take control of Bob, shut
  the door with `E`, then go forward. Alice's recorded path now runs through a shut door, so she is
  claimed and goes inert from that instant — watch her row in the strip gain a new span.
- **Shove the crate and look away.** Push it, then `1`/`2` to another character. The crate keeps
  sliding and keeps recording, because the slide is a consequence you caused.
- **Kill the shooter after the shot.** Have one character fire, rewind, then kill them before they
  fired. The bullet's origin no longer exists, so it is voided — it never appears at all.
- **The turnstile.** Walk into the machine and press `E`. It emits an inverted copy, hands you
  control, and time immediately runs backwards because the copy's rate is `-1`.
- **Break the turnstile.** After using it, `Ctrl+Z` and re-record so that character never goes to the
  machine. The copy has already played a whole backward pass, which is behind the cursor and cannot be
  unwritten — so the ship goes up instead. That is the one paradox with no quiet repair.

## Notes

The cursor clamps at both ends of the loop rather than wrapping, so at the end of the loop nothing
advances until you hold `R`.

`logRepairs` on `SimRunner` is on by default and logs every claim and void with the reason it
happened. A claim you did not expect is the main symptom of a rule going wrong, and it is invisible
otherwise.
