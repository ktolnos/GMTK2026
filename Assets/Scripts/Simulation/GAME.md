This is a puzzle game about re-recording timelines.
Think time travel + Outer Wilds + Superhot +  Hotline Miami + Tenet.
The time loop is small-ish (probably 1 minute).

The Simulation is recording everything that happens in the scene. 
It should be able to play back the events exactly from any point in time and at any timescale (including negative, 
e.g. for rewind).
The gameobjects in simulation can be either recording or already recorded. The recorded objects should behave exactly
as they behaved when they were recorded unless affected by the recording object.
The player controls multiple characters and can switch between them. By default (after the first recording) all
characters replay actions from the previous time loop until the player touches the controls. When they do,
the character becomes isRecording=true and records the history going forward.
Importantly, each character has their own timescale at which they move through simualtion, including a possibility of
a negative one. Also there If they affect other recorded objects, the objects also start to record with the same timescale.
E.g. if the player moves back in time and they shoot a bullet, the bullet is also going back in time, and if the bullet
hurts an enemy, the enemy will start moving back in time and record the fact that it has low HP. There is also support for
"time moves when you move" mechanic.
Switching between characters is allowed only within one time instant, so regardless of their timescale all other objects have 
something recorded (moving forward, inactive characters do NOOP actions).
Sometimes players overwrite a recording of a character (by touching the controls) when they didn't intend to. So we also
need to support CTRL+Z that rewinds the time to before the overwrite has happened and restores the recording.
This implies that we have to store at least 2 versions of the history (current and prev) and maybe more if we want to 
support multiple ctrl+z.

Architectural considerations:
Objects moving at different time scales may have different frequency of state saves (for smooth replay), 
so we need to store them in some kind of data structure that supports fast lookup of a state for a given frame.
The whole history (or even multiple histories) should be serializable and writable to disk.
Some objects start on the scene, some are instantiated at runtime. The simulation should support re-instantiating 
objects during recording (including the case where objects have to be instantiated at their destruction time for
negative time).