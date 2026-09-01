# Controls

Top-down, Hotline Miami handling. Read straight off the project-wide input actions asset by
`Controls`, which is static and pollable at any moment — input updates once at the top of the frame,
so nothing needs an execution order.

Actions used: `Move`, `Attack`, `Interact`, `Next`, `Previous`, `Seek`, `FastForward`, `Undo`, `Redo`.
All but `Seek`, `FastForward`, `Undo` and `Redo` come with the default asset.

## Acting

| Input | Does |
|---|---|
| `W` `A` `S` `D` | Move, both axes. There is no jump. |
| Left mouse | Fire — bound, but nothing fires yet |
| `E` | Interact — bound, but nothing interacts yet |

Only the character you **control** acts. Everything else that is recording is *inert*: no intent, but
still physics. It keeps its momentum, gets pushed, and can still be shot.

Movement sets velocity outright rather than accelerating toward it, so there is **no inertia** — let go
and you stop that tick. Being shoved still works, because it comes out of contact resolution during
the step rather than out of momentum carried between steps: a body leaning on you displaces you, it
just cannot send you flying.

## Moving through the loop

| Input | Does |
|---|---|
| `←` `→` | Seek: run loop time against or with the way the watched character experiences it |
| `Shift` (hold) | Move through loop time faster, whichever way it is going |
| `Z` | Undo the last re-recording |
| `Shift+Z` | Redo it |

## Switching character

| Input | Does |
|---|---|
| `Tab` or `2` | Become the next character |
| `1` | Become the previous one |

**Switching is not a claim, and not a release.** Who you are looking at has nothing to do with who is
writing history: a character you walk away from mid-performance carries on doing what they were doing,
they just stop hearing you. What ends a claim is the cursor turning against it — seek backwards, and
whoever was recording goes back to playback.

A character with nobody driving them has no intent, so they stop dead rather than skate on. That is the
same statement as the driven character with no keys held, and they can still be shoved.

**Watching is free; controlling is not.** Taking control re-records that character from this instant
onward, superseding what they did in the previous take — so it is the one action that opens a take,
and the one thing undo has to undo.

**Moving takes control.** You do not press anything special to resume acting. A movement, fire or
interact key claims the character you already control, which matters after seeking backwards: that
drops the claim, so the body goes back to being played back and ignores input until you take it again.
Aim alone does not count — a mouse always has a position, so if it did, the world could never stand
still.

**Seek is signed and relative, not absolute.** It is a 1D axis: negative winds the loop back, positive
winds it on. It is read against the watched character's own direction rather than the world's, so for
a character the reversal machine has turned round, it is seeking *forwards* that fights them. Releasing
it returns the cursor to that character's own rate.

Seeking against a character drops its claim, and their own rate stops contributing — they are not
acting, their recording is playing, so the cursor runs at the scrub speed alone. Seeking *with* them
they are still acting, so the scrub adds on top: an ordinary character at rate 1 seeks forward at 2.

**Rate comes from whoever you are watching, not whoever you control.** A character whose `rate` is
`0.35` makes the whole world crawl while they move normally. Nothing about that is special-cased — it
is one field on one component.

## Superhot mode — time moves when you move

Per character, on `SimCharacter`. Off by default so it does not change the baseline feel.

| Field | Meaning |
|---|---|
| `timeMovesWhenYouMove` | Enable it |
| `rate` | Ticks of loop time per second while acting. Negative for a character who experiences the loop backwards. |
| `idleRate` | Magnitude of the rate while standing still. Takes its sign from `rate`, so an inverted character still creeps backwards rather than turning round. |

Idleness is read live off the controls, so it only tells the truth for the character being driven. The
moment you can watch someone other than yourself, it has to come out of the recording instead — which
is the first thing that will make `SimCharacter` record anything.

## Not built yet

Pause, save and load. Firing and interacting are bound but do
nothing. Nothing yet reads `Attack` or `Interact` except the "is the player acting?" test that claims
a character and drives the superhot rate.
