using System;
using System.Collections.Generic;
using System.IO;
using Chronomancers.Sim;
using NUnit.Framework;

namespace Chronomancers.Sim.Tests
{
    [TestFixture]
    public class TimelineTests
    {
        const int Pose = 1;

        /// <summary>Stand-in for a real component's state: some continuous fields, one discrete.</summary>
        struct P
        {
            public float X;
            public int Hp;
        }

        static readonly SimId A = new(0x0A);
        static readonly SimId B = new(0x0B);

        static LoopTime T(int raw) => LoopTime.FromRaw(raw);

        static Timeline NewTimeline()
        {
            var timeline = new Timeline();
            timeline.Registry.Register<P>(Pose);
            timeline.OpenLayer();
            return timeline;
        }

        /// <summary>Records one span over the given instants, in the order given.</summary>
        static void Record(Timeline timeline, SimId id, int dir, int[] raws, Func<int, P> value)
        {
            timeline.Claim(id, dir, T(raws[0]));
            var body = timeline.Body(id);
            var channel = body.Channel<P>(Pose);
            foreach (var raw in raws)
            {
                var index = body.WriteSample(T(raw));
                channel.Set(index, value(raw));
            }
            timeline.EndSpan(id);
        }

        /// <summary>What a component would actually display: the endpoints blended by T.</summary>
        static float Lerp(in Sampled<P> sampled) =>
            sampled.A.X + (sampled.B.X - sampled.A.X) * sampled.T;

        static byte[] Snapshot(Timeline timeline)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            timeline.WriteTo(writer);
            writer.Flush();
            return stream.ToArray();
        }

        // ---------------------------------------------------------------- LoopTime

        [Test]
        public void LoopTime_RawValuesStayHumanReadable()
        {
            Assert.AreEqual(12345, LoopTime.FromSeconds(1.2345).Raw);
            Assert.AreEqual(600000, LoopTime.FromSeconds(60).Raw);
            Assert.AreEqual(1.2345, LoopTime.FromRaw(12345).Seconds, 1e-9);
        }

        [Test]
        public void LoopTime_InverseLerp_IsZeroAtStart()
        {
            // The original PlaybackData.t computed (next - now) / (next - prev), which returns 1
            // when now == prev — inverted. This is the regression guard for that.
            Assert.AreEqual(0f, LoopTime.InverseLerp(T(100), T(200), T(100)), 1e-6f);
            Assert.AreEqual(0.25f, LoopTime.InverseLerp(T(100), T(200), T(125)), 1e-6f);
            Assert.AreEqual(1f, LoopTime.InverseLerp(T(100), T(200), T(200)), 1e-6f);
        }

        [Test]
        public void LoopTime_InverseLerp_SurvivesDegenerateInterval()
        {
            // A single-sample span, or a clamped endpoint, would divide by zero.
            Assert.AreEqual(0f, LoopTime.InverseLerp(T(100), T(100), T(100)), 1e-6f);
        }

        // ---------------------------------------------------------------- SimClock

        [Test]
        public void Clock_AdvanceIsDriftFreeAcrossAWholeLoop()
        {
            // 1/60 s is not representable in a power-of-ten fixed point. Quantising the step itself
            // would give 167 units per step -> 601200 after 3600 steps, 0.12 s fast. Folding the
            // step into a sub-unit accumulator keeps the error bounded at a single unit instead.
            var clock = new SimClock(LoopTime.FromSeconds(60)) { Rate = 1f };
            for (var i = 0; i < 3600; i++) clock.Advance(1d / 60d);

            Assert.AreEqual(600000, clock.Cursor.Raw);
            Assert.AreNotEqual(3600 * LoopTime.FromSeconds(1d / 60d).Raw, clock.Cursor.Raw);
        }

        [Test]
        public void Clock_FrozenRateRecordsNothing()
        {
            var clock = new SimClock(LoopTime.FromSeconds(60)) { Rate = 0f };
            clock.Seek(T(1234));

            Assert.AreEqual(0, clock.Advance(1d / 60d).Raw);
            Assert.AreEqual(1234, clock.Cursor.Raw);
            Assert.IsTrue(clock.Frozen);
            Assert.AreEqual(0, clock.Dir);
        }

        [Test]
        public void Clock_BulletTimeIsDenserInLoopTimeThanNormalRate()
        {
            // The whole point of rate: at 0.2 the character takes five physics steps per unit of
            // loop time that a rate-1.0 character covers in one, so it stores five times the
            // samples over the same range and replays at full fidelity from any other viewpoint.
            var slow = new SimClock(LoopTime.FromSeconds(60)) { Rate = 0.2f };
            var normal = new SimClock(LoopTime.FromSeconds(60)) { Rate = 1f };
            for (var i = 0; i < 300; i++)
            {
                slow.Advance(1d / 60d);
                normal.Advance(1d / 60d);
            }

            // Within a millisecond; the two accumulators truncate independently.
            Assert.AreEqual(normal.Cursor.Raw, slow.Cursor.Raw * 5, 10d);
        }

        [Test]
        public void Clock_NegativeRateClampsAtLoopStart()
        {
            var clock = new SimClock(LoopTime.FromSeconds(60)) { Rate = -1f };
            clock.Seek(LoopTime.FromSeconds(0.5));
            for (var i = 0; i < 120; i++) clock.Advance(1d / 60d);

            Assert.AreEqual(0, clock.Cursor.Raw);
            Assert.IsTrue(clock.AtStart);
            Assert.AreEqual(0, clock.Advance(1d / 60d).Raw, "must not move once clamped");
        }

        // ---------------------------------------------------------------- sampling

        [Test]
        public void Sample_ReturnsRecordedValuesExactlyAtTheirOwnInstants()
        {
            var timeline = NewTimeline();
            Record(timeline, A, +1, new[] { 0, 100, 200 }, raw => new P { X = raw, Hp = 3 });
            var body = timeline.Body(A);

            foreach (var raw in new[] { 0, 100, 200 })
            {
                var sampled = body.Sample<P>(Pose, T(raw));
                Assert.IsTrue(sampled.Exists);
                Assert.AreEqual(raw, sampled.A.X, 1e-6f, $"at raw {raw}");
            }
        }

        [Test]
        public void Sample_InterpolatesWithinASpan()
        {
            var timeline = NewTimeline();
            Record(timeline, A, +1, new[] { 100, 200 }, raw => new P { X = raw, Hp = 3 });

            var sampled = timeline.Body(A).Sample<P>(Pose, T(125));
            Assert.IsTrue(sampled.Exists);
            Assert.AreEqual(100f, sampled.A.X, 1e-6f);
            Assert.AreEqual(200f, sampled.B.X, 1e-6f);
            Assert.AreEqual(0.25f, sampled.T, 1e-6f);
        }

        [Test]
        public void Sample_OutsideEverySpanDoesNotExist()
        {
            var timeline = NewTimeline();
            Record(timeline, A, +1, new[] { 100, 200 }, raw => new P { X = raw, Hp = 3 });

            Assert.IsFalse(timeline.Body(A).Sample<P>(Pose, T(50)).Exists);
            Assert.IsFalse(timeline.Body(A).Sample<P>(Pose, T(250)).Exists);
            Assert.IsFalse(timeline.Exists(A, T(250)));
        }

        [Test]
        public void Sample_BackwardSpanReadsInAscendingTimeOrder()
        {
            // Recorded descending (300, 200, 100) because the cursor was travelling backwards.
            // Readers must still see it as an ascending series.
            var timeline = NewTimeline();
            Record(timeline, A, -1, new[] { 300, 200, 100 }, raw => new P { X = raw, Hp = 3 });
            var body = timeline.Body(A);

            var span = body.GetSpan(0);
            Assert.AreEqual(100, span.Min.Raw);
            Assert.AreEqual(300, span.Max.Raw);
            Assert.AreEqual(-1, span.Dir);

            var sampled = body.Sample<P>(Pose, T(150));
            Assert.IsTrue(sampled.Exists);
            Assert.AreEqual(100f, sampled.A.X, 1e-6f);
            Assert.AreEqual(200f, sampled.B.X, 1e-6f);
            Assert.AreEqual(0.5f, sampled.T, 1e-6f);
        }

        [Test]
        public void WriteSample_RejectsANonAdvancingInstant()
        {
            // Encodes "a frozen clock records nothing": the sim loop must not call WriteSample when
            // loop time did not move, and a repeated instant is surfaced rather than swallowed.
            var timeline = NewTimeline();
            timeline.Claim(A, +1, T(0));
            var body = timeline.Body(A);
            body.WriteSample(T(0));

            Assert.Throws<ArgumentException>(() => body.WriteSample(T(0)));
            Assert.Throws<ArgumentException>(() => body.WriteSample(T(-1)));
        }

        // ---------------------------------------------------------------- overlap and seams

        [Test]
        public void HigherSeqSpanWinsWhereverTwoOverlap()
        {
            var timeline = NewTimeline();
            Record(timeline, A, +1, new[] { 0, 100, 200, 300 }, raw => new P { X = raw, Hp = 10 });

            // A later backward pass re-records the middle: an inverted character shooting an enemy
            // that was originally recorded moving forward.
            timeline.OpenLayer();
            Record(timeline, A, -1, new[] { 200, 150, 100 }, raw => new P { X = raw, Hp = 1 });

            var body = timeline.Body(A);
            Assert.AreEqual(1, body.Sample<P>(Pose, T(150)).A.Hp, "inside the newer span");
            Assert.AreEqual(10, body.Sample<P>(Pose, T(250)).A.Hp, "after it, the original stands");
            Assert.AreEqual(10, body.Sample<P>(Pose, T(50)).A.Hp, "before it, likewise");
        }

        [Test]
        public void InterpolationNeverCrossesASpanSeam()
        {
            // The join between a forward pass and a backward pass over it is a real discontinuity —
            // full HP on one side, nearly dead on the other. Blending across it would invent a state
            // that never existed.
            var timeline = NewTimeline();
            Record(timeline, A, +1, new[] { 0, 100, 200, 300 }, raw => new P { X = raw, Hp = 10 });
            timeline.OpenLayer();
            Record(timeline, A, -1, new[] { 200, 150, 100 }, raw => new P { X = raw, Hp = 1 });

            var body = timeline.Body(A);
            Assert.AreEqual(1, body.Sample<P>(Pose, T(200)).A.Hp, "the newer span owns the boundary");

            var justAfter = body.Sample<P>(Pose, T(201));
            Assert.AreEqual(10, justAfter.A.Hp, "one unit later, unblended");
            Assert.AreEqual(10, justAfter.B.Hp, "and its far endpoint is in the same span");
        }

        [Test]
        public void VoidSpanOverridesAnEarlierRecording()
        {
            var timeline = NewTimeline();
            Record(timeline, A, +1, new[] { 0, 100, 200, 300 }, raw => new P { X = raw, Hp = 10 });
            Assert.IsTrue(timeline.Exists(A, T(150)));

            // Killed at 200 by something travelling backwards: non-existence propagates to every
            // earlier instant the inverted character still has to travel through.
            timeline.OpenLayer();
            timeline.Void(A, -1, T(200));
            timeline.Body(A).Extend(T(100));
            timeline.EndSpan(A);

            Assert.IsFalse(timeline.Exists(A, T(150)), "voided range");
            Assert.IsFalse(timeline.Body(A).Sample<P>(Pose, T(150)).Exists);
            Assert.IsTrue(timeline.Exists(A, T(250)), "outside it the recording stands");
        }

        // ---------------------------------------------------------------- passage

        // A door 0.6 wide filling a gap in a wall, its gate line running from (2,-1) to (2,1).
        const float GateX = 2f;
        const float GateLow = -1f;
        const float GateHigh = 1f;

        static bool Crosses(float fromX, float fromY, float toX, float toY) =>
            Geometry.SegmentsCross(fromX, fromY, toX, toY, GateX, GateLow, GateX, GateHigh);

        [Test]
        public void WalkingThroughADoorwayCrossesTheGate()
        {
            Assert.IsTrue(Crosses(1.5f, 0f, 2.5f, 0f), "straight through");
            Assert.IsTrue(Crosses(1.6f, -0.5f, 2.4f, 0.6f), "and at an angle");
        }

        [Test]
        public void StandingAgainstAShutDoorDoesNotCrossIt()
        {
            // The false positive that broke rewinding: physics reports flush contact as overlap, so an
            // overlap test claimed anyone leaning on the door. A crossing test cannot.
            Assert.IsFalse(Crosses(1.25f, -0.4f, 1.25f, 0.4f), "pacing alongside the door");
            Assert.IsFalse(Crosses(1.4f, 0f, 1.6f, 0f), "walking up to it and stopping");
            Assert.IsFalse(Crosses(2f, -0.5f, 2f, 0.5f), "sliding along the gate itself is not through it");
        }

        [Test]
        public void ALongStrideOverTheDoorwayStillCrossesIt()
        {
            // Why this is swept rather than sampled. At |rate| > 1 a recorded body takes strides longer than
            // the door is wide, so no *sample* ever lands inside the gap — but the step between two samples
            // plainly goes through it.
            Assert.IsTrue(Crosses(0.5f, 0f, 4.5f, 0f), "a stride four times the door's width");
            Assert.IsFalse(Crosses(0.5f, 3f, 4.5f, 3f), "the same stride, well above the gate");
        }

        [Test]
        public void GoingThroughTheWallBesideADoorIsNotTheDoorsDoing()
        {
            // The gate is a segment, not an infinite line. A body passing through the wall above the doorway
            // crosses the gate's extension but not the gate, and blaming the door for it would claim bodies
            // for the wrong reason — and in the wrong place.
            Assert.IsFalse(Crosses(1.5f, 2.5f, 2.5f, 2.5f), "above the gate");
            Assert.IsFalse(Crosses(1.5f, -2.5f, 2.5f, -2.5f), "and below it");
        }

        [Test]
        public void TouchingTheGateWithoutPassingThroughDoesNotCount()
        {
            // Strict on purpose: a false positive erases history, a missed one does not.
            Assert.IsFalse(Crosses(1.5f, 0f, 2f, 0f), "stopping exactly on the gate");
            Assert.IsFalse(Crosses(1.5f, 1f, 2.5f, 1f), "grazing its endpoint");
        }

        // ---------------------------------------------------------------- frontier

        [Test]
        public void PastTheEndOfATakeIsTheFrontier()
        {
            // The bug this exists for: play to 300, rewind, come forward again. Beyond 300 nothing covers
            // the cursor, and without recognising that as the frontier every body is released as
            // non-existent — the whole world vanishes the moment you catch up to where you rewound from.
            var timeline = NewTimeline();
            Record(timeline, A, +1, new[] { 0, 100, 200, 300 }, raw => new P { X = raw, Hp = 5 });

            Assert.IsFalse(timeline.Body(A).AtFrontier(T(200), +1), "inside its history: pure playback");
            Assert.IsTrue(timeline.Body(A).AtFrontier(T(301), +1), "past the take: recording must resume");
        }

        [Test]
        public void AnAuthoredBodyHasAFrontierInBothDirections()
        {
            // A take that began at 100 leaves everything below it unrecorded, and rewinding into that gap is
            // a frontier like any other: the body records standing still. An authored body has no origin, so
            // rule 8's wall does not apply to it — unlike a bullet, it was always there.
            var timeline = NewTimeline();
            Record(timeline, A, +1, new[] { 100, 200, 300 }, raw => new P { X = raw, Hp = 5 });

            Assert.IsTrue(timeline.Body(A).AtFrontier(T(99), -1), "rewinding below the take records NOOP");
            Assert.IsTrue(timeline.Body(A).AtFrontier(T(301), +1), "and so does running past the end of it");
            Assert.IsFalse(timeline.Body(A).AtFrontier(T(150), -1), "but not where history already exists");
        }

        [Test]
        public void PastTheEndOfAVoidIsNotTheFrontier()
        {
            // A void only covers loop time the cursor actually traversed, so replaying past its far end
            // finds no span either. Treating that as a frontier would resurrect everything that ever died.
            var timeline = NewTimeline();
            Record(timeline, A, +1, new[] { 0, 400 }, raw => new P { X = raw, Hp = 3 }); // the killer
            Record(timeline, B, +1, new[] { 0, 100, 200 }, raw => new P { X = raw, Hp = 5 });

            timeline.Void(B, +1, T(201), causedBy: A, causedAt: T(200));
            timeline.Body(B).Extend(T(300));
            timeline.EndSpan(B);

            Assert.IsFalse(timeline.Exists(B, T(250)), "dead inside the void");
            Assert.IsFalse(timeline.Body(B).AtFrontier(T(301), +1), "and still dead past the end of it");
        }

        [Test]
        public void ABulletIsNotAtAFrontierBelowItsMuzzle()
        {
            // Rule 8: a span may only grow away from where the body came into being. Rewinding below a
            // bullet's muzzle must release it, not claim it and record it backwards out of the gun.
            var timeline = NewTimeline();
            Record(timeline, A, +1, new[] { 0, 400 }, raw => new P { X = raw, Hp = 3 });

            var bullet = SimId.Spawn(A, T(290), 0);
            timeline.DeclareOrigin(bullet, A, T(290));
            Record(timeline, bullet, +1, new[] { 290, 350 }, raw => new P { X = raw, Hp = 1 });

            Assert.IsFalse(timeline.Body(bullet).AtFrontier(T(289), -1),
                "below the muzzle it simply does not exist");
            Assert.IsTrue(timeline.Body(bullet).AtFrontier(T(351), +1),
                "but past where it had got to, it carries on flying");
        }

        [Test]
        public void AnInvertedBulletsFrontierRunsTheOtherWay()
        {
            var timeline = NewTimeline();
            Record(timeline, A, +1, new[] { 0, 400 }, raw => new P { X = raw, Hp = 3 });

            // Recorded backwards from its muzzle at 350 down to 290.
            var bullet = SimId.Spawn(A, T(350), 0);
            timeline.DeclareOrigin(bullet, A, T(350));
            Record(timeline, bullet, -1, new[] { 350, 290 }, raw => new P { X = raw, Hp = 1 });

            Assert.IsTrue(timeline.Body(bullet).AtFrontier(T(289), -1), "carries on away from the muzzle");
            Assert.IsFalse(timeline.Body(bullet).AtFrontier(T(351), +1), "never back across it");
        }

        [Test]
        public void AFreshlyEmittedCopyMayGrowEitherWayFromItsMachine()
        {
            // The turnstile. The machine emits the copy while the cursor is still running forward, so its one
            // sample is recorded forward — and then you take it over and it records backwards. Rule 8 must not
            // refuse that: nothing has crossed the muzzle yet, because the muzzle is all the history there is.
            //
            // Getting this wrong released the copy the instant time reversed, so the clone appeared and
            // evaporated in the same breath.
            var timeline = NewTimeline();
            Record(timeline, A, +1, new[] { 0, 400 }, raw => new P { X = raw, Hp = 3 }); // the original

            var copy = SimId.Spawn(A, T(200), 0);
            timeline.DeclareOrigin(copy, A, T(200));
            Record(timeline, copy, +1, new[] { 200 }, raw => new P { X = raw, Hp = 3 });

            Assert.IsTrue(timeline.Body(copy).AtFrontier(T(199), -1), "backwards, which is the whole point");
            Assert.IsTrue(timeline.Body(copy).AtFrontier(T(201), +1), "forwards is legal too, just not intended");

            // Once it has actually run backwards, the muzzle becomes a wall like anyone else's.
            Record(timeline, copy, -1, new[] { 199, 100 }, raw => new P { X = raw, Hp = 3 });
            Assert.IsFalse(timeline.Body(copy).AtFrontier(T(201), +1),
                "now it has history below the machine, it may not grow back across it");
        }

        // ---------------------------------------------------------------- causality

        [Test]
        public void SpawnedBodyLosesItsFootingWhenItsOriginIsGone()
        {
            // The enemy fires at 100; the bullet's origin is the enemy at that instant.
            var timeline = NewTimeline();
            Record(timeline, A, +1, new[] { 0, 400 }, raw => new P { X = raw, Hp = 3 }); // the killer
            Record(timeline, B, +1, new[] { 0, 100, 200, 300, 400 }, raw => new P { X = raw, Hp = 5 });

            var bullet = SimId.Spawn(B, T(100), 0);
            timeline.Declare(bullet, 4);
            timeline.DeclareOrigin(bullet, B, T(100));
            Record(timeline, bullet, +1, new[] { 100, 150 }, raw => new P { X = raw, Hp = 1 });

            Assert.IsTrue(timeline.CausalityHolds(bullet, T(120)));

            // A later take kills the enemy at 80, before it ever fired.
            timeline.OpenLayer();
            timeline.Void(B, +1, T(81), causedBy: A, causedAt: T(80));
            timeline.Body(B).Extend(T(400));
            timeline.EndSpan(B);

            Assert.IsFalse(timeline.CausalityHolds(bullet, T(120)),
                "nothing fired it, so the bullet cannot exist");

            var broken = new List<BrokenCausality>();
            timeline.CollectBrokenCausality(T(120), broken);
            Assert.AreEqual(1, broken.Count);
            Assert.AreEqual(bullet, broken[0].Id);
            Assert.AreEqual(CausalityBreak.OriginGone, broken[0].Reason,
                "void it — there is nothing to re-simulate about a shot that was never fired");
        }

        [Test]
        public void BrokenOriginAndBrokenCauseAskForOppositeRepairs()
        {
            // Both failures at once, on two different bodies. A fires a bullet AND is the recorded
            // killer of B, so erasing A breaks a spawn and a destruction in the same stroke.
            var timeline = NewTimeline();
            Record(timeline, A, +1, new[] { 0, 400 }, raw => new P { X = raw, Hp = 3 });
            Record(timeline, B, +1, new[] { 0, 100 }, raw => new P { X = raw, Hp = 5 });
            timeline.Void(B, +1, T(101), causedBy: A, causedAt: T(100));
            timeline.Body(B).Extend(T(400));
            timeline.EndSpan(B);

            var bullet = SimId.Spawn(A, T(100), 0);
            timeline.DeclareOrigin(bullet, A, T(100));
            Record(timeline, bullet, +1, new[] { 100, 150 }, raw => new P { X = raw, Hp = 1 });

            var broken = new List<BrokenCausality>();
            timeline.CollectBrokenCausality(T(120), broken);
            Assert.AreEqual(0, broken.Count, "nothing is wrong yet");

            // Now A never existed at all.
            timeline.OpenLayer();
            timeline.Void(A, +1, T(0));
            timeline.Body(A).Extend(T(400));
            timeline.EndSpan(A);

            timeline.CollectBrokenCausality(T(120), broken);
            Assert.AreEqual(2, broken.Count);

            var forB = broken.Find(b => b.Id.Equals(B));
            var forBullet = broken.Find(b => b.Id.Equals(bullet));
            Assert.AreEqual(CausalityBreak.CauseGone, forB.Reason, "B revives: it has a loop left to live");
            Assert.AreEqual(CausalityBreak.OriginGone, forBullet.Reason, "the bullet is simply voided");
        }

        [Test]
        public void ADeathSpawnNamesTheKillerSoErasingItUndoesBoth()
        {
            // The polarity trap: an item dropped on death must NOT take the corpse as its origin, or
            // the drop would be flagged exactly while it is legitimate. It takes the killer instead,
            // which is the same SimId the corpse's void span already carries.
            var timeline = NewTimeline();
            Record(timeline, A, +1, new[] { 0, 400 }, raw => new P { X = raw, Hp = 3 }); // the killer
            Record(timeline, B, +1, new[] { 0, 200 }, raw => new P { X = raw, Hp = 5 }); // dies at 200
            timeline.Void(B, +1, T(201), causedBy: A, causedAt: T(200));
            timeline.Body(B).Extend(T(400));
            timeline.EndSpan(B);

            var loot = SimId.Spawn(B, T(200), 0);
            timeline.DeclareOrigin(loot, A, T(200)); // the killer, not the corpse
            Record(timeline, loot, +1, new[] { 200, 400 }, _ => new P { X = 50, Hp = 0 });

            Assert.IsTrue(timeline.CausalityHolds(loot, T(300)), "B is dead and the loot is on the floor");

            // Erase the killer. The death and the drop have to fall together.
            timeline.OpenLayer();
            timeline.Void(A, +1, T(0));
            timeline.Body(A).Extend(T(400));
            timeline.EndSpan(A);

            var broken = new List<BrokenCausality>();
            timeline.CollectBrokenCausality(T(300), broken);
            Assert.AreEqual(2, broken.Count);
            Assert.AreEqual(CausalityBreak.CauseGone, broken.Find(b => b.Id.Equals(B)).Reason);
            Assert.AreEqual(CausalityBreak.OriginGone, broken.Find(b => b.Id.Equals(loot)).Reason);
        }

        [Test]
        public void DestroyedBodyRevivesWhenItsKillerIsGone()
        {
            // The case that actually breaks puzzles. X is shot dead at 150 and voided for the rest of
            // the loop. A later take removes the bullet, so nothing killed X — and without a cause on
            // the void span, X would simply stay dead forever with no killer, silently deleting
            // everything X was meant to do over [151,400].
            const int bulletPrefab = 4;
            var bullet = SimId.Spawn(B, T(100), 0);

            var timeline = NewTimeline();
            timeline.Declare(bullet, bulletPrefab);
            Record(timeline, bullet, +1, new[] { 100, 150 }, raw => new P { X = raw, Hp = 1 });
            Record(timeline, A, +1, new[] { 0, 100, 150 }, raw => new P { X = raw, Hp = 3 });

            timeline.Void(A, +1, T(151), causedBy: bullet, causedAt: T(150));
            timeline.Body(A).Extend(T(400));
            timeline.EndSpan(A);

            Assert.IsFalse(timeline.Exists(A, T(300)), "dead for the rest of the loop");
            Assert.IsTrue(timeline.CausalityHolds(A, T(300)), "and legitimately so: the bullet is there");

            // Now the bullet never happens.
            timeline.OpenLayer();
            timeline.Void(bullet, +1, T(100), causedBy: SimId.None, causedAt: T(100));
            timeline.Body(bullet).Extend(T(400));
            timeline.EndSpan(bullet);

            Assert.IsFalse(timeline.CausalityHolds(A, T(300)), "X was killed by something that is gone");
            Assert.IsTrue(timeline.CausalityHolds(A, T(100)), "but its earlier history is untouched");

            // Reviving it: a claim in the cursor's direction, seeded from the state X had when it died
            // — there is no playback to inherit from inside a void.
            var reviveSeed = timeline.Body(A).Sample<P>(Pose, T(150));
            Assert.IsTrue(reviveSeed.Exists, "the instant before the void still holds X's last state");

            timeline.Claim(A, +1, T(151));
            var body = timeline.Body(A);
            var channel = body.Channel<P>(Pose);
            channel.Set(body.WriteSample(T(151)), reviveSeed.A);
            channel.Set(body.WriteSample(T(400)), reviveSeed.A);
            timeline.EndSpan(A);

            Assert.IsTrue(timeline.Exists(A, T(300)), "alive again");
            Assert.AreEqual(150f, timeline.Body(A).Sample<P>(Pose, T(151)).A.X, 1e-6f,
                "resuming from where it was hit");
            Assert.IsTrue(timeline.CausalityHolds(A, T(300)));
        }

        [Test]
        public void UncausedDestructionNeedsNoJustification()
        {
            // Fell out of the world, expired, scripted: nothing to check.
            var timeline = NewTimeline();
            Record(timeline, A, +1, new[] { 0, 100 }, raw => new P { X = raw, Hp = 1 });
            timeline.Void(A, +1, T(101));
            timeline.Body(A).Extend(T(400));
            timeline.EndSpan(A);

            Assert.IsFalse(timeline.Exists(A, T(200)));
            Assert.IsTrue(timeline.CausalityHolds(A, T(200)));
        }

        // ---------------------------------------------------------------- undo

        [Test]
        public void PoppingALayerRestoresTheHistoryByteForByte()
        {
            var timeline = NewTimeline();
            Record(timeline, A, +1, new[] { 0, 100, 200, 300 }, raw => new P { X = raw, Hp = 10 });
            Record(timeline, B, +1, new[] { 0, 150, 300 }, raw => new P { X = raw, Hp = 5 });
            var before = Snapshot(timeline);

            // A player action plus the whole divergence cascade it triggers: A re-recorded backwards,
            // B voided, an event logged. One layer covers all of it.
            var layer = timeline.OpenLayer();
            Record(timeline, A, -1, new[] { 250, 200, 150 }, raw => new P { X = raw, Hp = 1 });
            timeline.Claim(A, -1, T(140));
            timeline.Body(A).WriteSample(T(140)); // a claim always writes its inherited seed first
            timeline.EndSpan(A);
            timeline.Void(B, -1, T(200));
            timeline.Body(B).Extend(T(50));
            timeline.EndSpan(B);
            Assert.AreNotEqual(before.Length, Snapshot(timeline).Length, "the layer really changed things");

            timeline.PopLayer(layer);
            var after = Snapshot(timeline);

            // Compared from offset 4: the leading int is the Seq counter, which is deliberately not
            // rewound so a later re-recording can never reuse a retired span's Seq and let a stale
            // event resurrect by matching it.
            Assert.AreEqual(before.Length, after.Length, "history size restored");
            for (var i = 4; i < before.Length; i++)
                Assert.AreEqual(before[i], after[i], $"byte {i} differs after undo");
        }

        [Test]
        public void PoppingALayerForgetsBodiesFirstTouchedByIt()
        {
            var timeline = NewTimeline();
            Record(timeline, A, +1, new[] { 0, 100 }, raw => new P { X = raw, Hp = 10 });
            Assert.AreEqual(1, timeline.BodyCount);

            var layer = timeline.OpenLayer();
            Record(timeline, B, +1, new[] { 0, 100 }, raw => new P { X = raw, Hp = 5 });
            Assert.AreEqual(2, timeline.BodyCount);

            timeline.PopLayer(layer);
            Assert.AreEqual(1, timeline.BodyCount);
            Assert.IsFalse(timeline.TryGetBody(B, out _));
        }

        [Test]
        public void UndoStackIsUnbounded()
        {
            var timeline = NewTimeline();
            Record(timeline, A, +1, new[] { 0, 400 }, raw => new P { X = raw, Hp = 10 });

            var snapshots = new List<byte[]> { Snapshot(timeline) };
            var layers = new List<int>();
            for (var i = 0; i < 8; i++)
            {
                layers.Add(timeline.OpenLayer());
                Record(timeline, A, +1, new[] { 10 + i * 10, 300 - i * 10 }, raw => new P { X = raw, Hp = i });
                snapshots.Add(Snapshot(timeline));
            }

            for (var i = layers.Count - 1; i >= 0; i--)
            {
                timeline.PopLayer(layers[i]);
                var expected = snapshots[i];
                var actual = Snapshot(timeline);
                Assert.AreEqual(expected.Length, actual.Length, $"undo step {i}");
                for (var b = 4; b < expected.Length; b++)
                    Assert.AreEqual(expected[b], actual[b], $"undo step {i}, byte {b}");
            }
        }

        // ---------------------------------------------------------------- identity, persistence

        [Test]
        public void SpawnIdsAreDeterministic()
        {
            var first = SimId.Spawn(A, T(1234), 0);
            Assert.AreEqual(first, SimId.Spawn(A, T(1234), 0), "replay must reproduce the same id");
            Assert.AreNotEqual(first, SimId.Spawn(A, T(1234), 1), "shotgun pellets differ");
            Assert.AreNotEqual(first, SimId.Spawn(A, T(1235), 0), "so do different instants");
            Assert.AreNotEqual(first, SimId.Spawn(B, T(1234), 0), "and different spawners");
            Assert.IsTrue(first.IsValid);
        }

        [Test]
        public void TimelineRoundTripsThroughDisk()
        {
            var timeline = NewTimeline();
            Record(timeline, A, +1, new[] { 0, 100, 200, 300, 400 }, raw => new P { X = raw, Hp = 10 });
            Record(timeline, B, -1, new[] { 300, 200, 100 }, raw => new P { X = raw, Hp = 5 });

            var spawned = SimId.Spawn(A, T(320), 0);
            timeline.Declare(spawned, 7);
            timeline.DeclareOrigin(spawned, A, T(320));
            Record(timeline, spawned, +1, new[] { 320, 360 }, raw => new P { X = raw, Hp = 1 });
            timeline.Void(spawned, +1, T(361), causedBy: A, causedAt: T(360));
            timeline.Body(spawned).Extend(T(400));
            timeline.EndSpan(spawned);

            var saved = Snapshot(timeline);

            var loaded = new Timeline();
            loaded.Registry.Register<P>(Pose);
            using (var stream = new MemoryStream(saved))
            using (var reader = new BinaryReader(stream))
                loaded.ReadFrom(reader);

            var resaved = Snapshot(loaded);
            Assert.AreEqual(saved.Length, resaved.Length);
            for (var i = 0; i < saved.Length; i++)
                Assert.AreEqual(saved[i], resaved[i], $"byte {i}");

            // And it is still queryable, not merely byte-equal.
            Assert.AreEqual(10, loaded.Body(A).Sample<P>(Pose, T(150)).A.Hp);
            Assert.AreEqual(5, loaded.Body(B).Sample<P>(Pose, T(150)).A.Hp);

            // Origin and void cause survive, so causality is still checkable from the file alone.
            Assert.AreEqual(A, loaded.Body(spawned).Origin);
            Assert.AreEqual(T(320), loaded.Body(spawned).OriginAt);
            Assert.IsFalse(loaded.Exists(spawned, T(380)));
            Assert.IsTrue(loaded.CausalityHolds(spawned, T(380)));
        }

        // ---------------------------------------------------------------- materialization

        [Test]
        public void ExistingSetIsDerivedFromTheTimelineNotAccumulatedFromEvents()
        {
            const int bulletPrefab = 4;

            var timeline = NewTimeline();
            timeline.Declare(A, 0); // authored scene body: no prefab, deactivated not destroyed
            Record(timeline, A, +1, new[] { 0, 100, 200, 300 }, raw => new P { X = raw, Hp = 10 });

            timeline.Declare(B, bulletPrefab);
            Record(timeline, B, +1, new[] { 100, 200 }, raw => new P { X = raw, Hp = 1 });

            var live = new List<SimId>();

            timeline.CollectExisting(T(50), live);
            Assert.AreEqual(1, live.Count, "B has not been spawned yet at 50");
            Assert.AreEqual(A, live[0]);

            timeline.CollectExisting(T(150), live);
            Assert.AreEqual(2, live.Count, "both alive at 150");

            timeline.CollectExisting(T(250), live);
            Assert.AreEqual(1, live.Count, "B is gone again after 200");
            Assert.AreEqual(A, live[0]);

            // Arriving at 150 from above rather than below yields the same set, with no unwinding:
            // the query is stateless, which is what makes scrub, undo and load one code path.
            timeline.CollectExisting(T(150), live);
            Assert.AreEqual(2, live.Count, "and the same set however the cursor got there");
        }

        [Test]
        public void VoidedBodyDropsOutOfTheExistingSet()
        {
            var timeline = NewTimeline();
            Record(timeline, A, +1, new[] { 0, 300 }, raw => new P { X = raw, Hp = 10 });
            Record(timeline, B, +1, new[] { 0, 300 }, raw => new P { X = raw, Hp = 10 });

            var live = new List<SimId>();
            timeline.CollectExisting(T(150), live);
            Assert.AreEqual(2, live.Count);

            timeline.OpenLayer();
            timeline.Void(B, -1, T(200));
            timeline.Body(B).Extend(T(100));
            timeline.EndSpan(B);

            timeline.CollectExisting(T(150), live);
            Assert.AreEqual(1, live.Count, "B must be released over the voided range");
            Assert.AreEqual(A, live[0]);

            timeline.CollectExisting(T(250), live);
            Assert.AreEqual(2, live.Count, "and materialized again outside it");
        }

        [Test]
        public void EveryBodyInALoadedHistoryKnowsWhatToInstantiate()
        {
            const int bulletPrefab = 4;
            const int enemyPrefab = 9;

            var timeline = NewTimeline();
            timeline.Declare(A, enemyPrefab);
            Record(timeline, A, +1, new[] { 0, 300 }, raw => new P { X = raw, Hp = 10 });

            var spawned = SimId.Spawn(A, T(120), 0);
            timeline.Declare(spawned, bulletPrefab);
            Record(timeline, spawned, +1, new[] { 120, 240 }, raw => new P { X = raw, Hp = 1 });

            var loaded = new Timeline();
            loaded.Registry.Register<P>(Pose);
            using (var stream = new MemoryStream(Snapshot(timeline)))
            using (var reader = new BinaryReader(stream))
                loaded.ReadFrom(reader);

            // Nothing survives from the run that recorded this, so the file alone has to say what to
            // instantiate for every body it mentions.
            Assert.AreEqual(enemyPrefab, loaded.Body(A).Archetype);
            Assert.AreEqual(bulletPrefab, loaded.Body(spawned).Archetype);

            var live = new List<SimId>();
            loaded.CollectExisting(T(180), live);
            Assert.AreEqual(2, live.Count);
        }

        // ---------------------------------------------------------------- the turnstile

        [Test]
        public void TurnstileIsAPlainSpawn_TheOriginalIsNotVoided()
        {
            const int characterPrefab = 9;
            var machine = new SimId(0xCC);
            var flip = T(200);

            // The forward character walks to the machine and carries on past it.
            var timeline = NewTimeline();
            timeline.Declare(A, characterPrefab);
            Record(timeline, A, +1, new[] { 0, 100, 200, 300, 400 }, raw => new P { X = raw, Hp = 10 });

            // Using the machine emits an inverted copy. Nothing is voided: two instances of the same
            // character coexisting is not a paradox, because the forward view reads as one
            // continuous worldline that runs backwards into the machine and forwards out of it. So
            // the flip needs no mechanism of its own — it is a spawn, exactly like firing a bullet.
            var layer = timeline.OpenLayer();
            var inverted = SimId.Spawn(machine, flip, 0);
            timeline.Declare(inverted, characterPrefab);
            timeline.DeclareOrigin(inverted, A, flip); // emitted by A, at the machine
            timeline.Claim(inverted, -1, flip);
            var invertedBody = timeline.Body(inverted);
            var channel = invertedBody.Channel<P>(Pose);
            foreach (var raw in new[] { 200, 100, 0 })
                channel.Set(invertedBody.WriteSample(T(raw)), new P { X = raw, Hp = 10 });
            timeline.EndSpan(inverted);

            Assert.IsTrue(timeline.Exists(A, T(150)), "the original reaches the machine");
            Assert.IsTrue(timeline.Exists(A, T(250)), "and keeps going past it");
            Assert.IsTrue(timeline.Exists(A, T(400)), "all the way to the end of the loop");
            Assert.IsTrue(timeline.Exists(inverted, T(150)), "the copy occupies the range behind it");
            Assert.IsFalse(timeline.Exists(inverted, T(250)), "but not ahead of the machine");
            Assert.AreEqual(-1, invertedBody.GetSpan(0).Dir);

            var live = new List<SimId>();
            timeline.CollectExisting(T(150), live);
            Assert.AreEqual(2, live.Count, "both instances coexist before the machine");
            timeline.CollectExisting(T(250), live);
            Assert.AreEqual(1, live.Count, "only the original after it");
            Assert.AreEqual(A, live[0]);

            // The copy's origin is A at the machine, so "did A get there?" needs no separate record —
            // it is the origin check.
            Assert.IsTrue(timeline.CausalityHolds(inverted, T(150)));

            timeline.OpenLayer();
            timeline.Void(A, +1, T(150)); // A killed on its way to the machine in a later take
            timeline.Body(A).Extend(T(400));
            timeline.EndSpan(A);

            Assert.IsFalse(timeline.CausalityHolds(inverted, T(150)),
                "A is gone by the flip, so the copy has no origin");
            Assert.IsTrue(timeline.Exists(inverted, T(150)),
                "core reports it; reacting to it is the sim loop's job");

            // Undoing the machine restores the original history untouched and forgets the copy.
            timeline.PopLayer(layer);
            Assert.IsTrue(timeline.Exists(A, T(250)));
            Assert.IsFalse(timeline.TryGetBody(inverted, out _));
        }

        [Test]
        public void InvertedKillVoidsOnlyTheTraversedRange()
        {
            // An inverted agent kills an enemy at 130 while travelling 200 -> 0. The void covers the
            // range the agent goes on to traverse; the enemy's own forward history is untouched.
            var timeline = NewTimeline();
            Record(timeline, B, +1, new[] { 0, 100, 200, 300, 400 }, raw => new P { X = raw, Hp = 10 });

            timeline.OpenLayer();
            timeline.Void(B, -1, T(129)); // 129, not 130: a body exists at the instant it is killed,
            timeline.Body(B).Extend(T(0)); // so any event caused by it there still has its cause
            timeline.EndSpan(B);

            // Anything simulated inside the voided range sees no enemy, and must not fall back to
            // the older recording that still covers those instants.
            Assert.IsFalse(timeline.Exists(B, T(0)));
            Assert.IsFalse(timeline.Exists(B, T(80)));
            Assert.IsFalse(timeline.Exists(B, T(129)));
            Assert.IsFalse(timeline.Body(B).Sample<P>(Pose, T(80)).Exists);

            // Past the kill the enemy is alive, at its original recorded state. Read forwards that
            // is a resurrection at 130 — which is the intended inverted-causality signature, not a
            // gap to paper over.
            Assert.IsTrue(timeline.Exists(B, T(130)), "alive at the instant of the kill");
            Assert.IsTrue(timeline.Exists(B, T(131)));
            Assert.AreEqual(10, timeline.Body(B).Sample<P>(Pose, T(200)).A.Hp);
        }

        [Test]
        public void InvertedBulletIsSeenToLeaveTheWallAndEnterTheMuzzle()
        {
            // Fired by an inverted agent at 150, stopping in a wall at 120.
            const int bulletPrefab = 4;
            var muzzle = T(150);
            var impact = T(120);

            var timeline = NewTimeline();
            var bullet = SimId.Spawn(A, muzzle, 0);
            timeline.Declare(bullet, bulletPrefab);
            Record(timeline, bullet, -1, new[] { 150, 140, 130, 120 }, raw => new P { X = raw, Hp = 1 });

            var body = timeline.Body(bullet);
            var span = body.GetSpan(0);

            Assert.AreEqual(-1, span.Dir);
            Assert.AreEqual(muzzle.Raw, span.Max.Raw, "causal birth is at the muzzle, the Max end");
            Assert.AreEqual(impact.Raw, span.Min.Raw, "and its death is at the wall, the Min end");

            Assert.IsFalse(timeline.Exists(bullet, T(119)));
            Assert.IsTrue(timeline.Exists(bullet, impact));
            Assert.IsTrue(timeline.Exists(bullet, muzzle));
            Assert.IsFalse(timeline.Exists(bullet, T(151)));

            // A character taking over at 80 and playing forward meets this body at its Min end
            // first. So the effect played there is the impact, running in reverse — the bullet
            // leaves the wall at 120 and travels to the muzzle at 150, where it vanishes. Keying
            // effects off the span end alone would muzzle-flash out of the wall.
            var atImpact = body.Sample<P>(Pose, impact);
            var atMuzzle = body.Sample<P>(Pose, muzzle);
            Assert.IsTrue(atImpact.Exists);
            Assert.IsTrue(atMuzzle.Exists);
            Assert.AreEqual(120f, atImpact.A.X, 1e-6f);
            Assert.AreEqual(150f, atMuzzle.A.X, 1e-6f);
        }

        [Test]
        public void AbsorbedProjectileIsRetiredOnTheNextForwardPass()
        {
            // A bullet fired at 100 and recorded flying to 160 is absorbed by an inverted character
            // at 150, while the cursor descends.
            //
            // Neither side may be written during that take. Behind the cursor is forbidden outright;
            // voiding the low side is equally wrong, because it would erase the muzzle end and leave
            // the bullet apparently emitted by the character. A projectile's span must always have
            // its muzzle at one end — Min for a forward shot, Max for an inverted one.
            //
            // So the backward take records only the collision and leaves the bullet whole. The tail
            // is retired on the next forward pass, when the cursor crosses 150 travelling in the
            // direction the consequence actually points.
            const int flagChannel = 3;
            var inverted = new SimId(0x11);

            var timeline = NewTimeline();
            timeline.Registry.Register<Flag>(flagChannel);
            timeline.Declare(B, 4);
            Record(timeline, B, +1, new[] { 100, 120, 140, 160 }, raw => new P { X = raw, Hp = 1 });
            Record(timeline, inverted, -1, new[] { 200, 150, 100 }, raw => new P { X = raw, Hp = 10 });

            // The backward take claims the bullet just long enough to record that it was absorbed —
            // one sample, at the instant of contact. No event entity: the flag is ordinary recorded
            // state, so overwriting that span retires the flag with it, for free.
            var layer = timeline.OpenLayer();
            timeline.Claim(B, -1, T(150));
            var body = timeline.Body(B);
            var pose = body.Channel<P>(Pose);
            var flags = body.Channel<Flag>(flagChannel);
            var index = body.WriteSample(T(150));
            pose.Set(index, new P { X = 150, Hp = 1 });
            flags.Set(index, new Flag { Absorbed = 1 });
            timeline.EndSpan(B);

            Assert.IsTrue(timeline.Exists(B, T(100)), "muzzle end intact through the backward take");
            Assert.IsTrue(timeline.Exists(B, T(160)), "tail not retired yet");
            Assert.AreEqual(1, body.Sample<Flag>(flagChannel, T(150)).Snap.Absorbed);

            // A later forward pass samples the flag at 150 and retires the tail as the cursor moves.
            // The guard against doing it twice is just whether the bullet still exists past the hit,
            // so the operation is idempotent and needs no once-only delivery.
            Assert.IsTrue(timeline.Exists(B, T(151)), "not retired yet, so the retirement must run");
            timeline.OpenLayer();
            timeline.Void(B, +1, T(151), causedBy: inverted, causedAt: T(150));
            foreach (var raw in new[] { 155, 158, 160 }) timeline.Body(B).Extend(T(raw));
            timeline.EndSpan(B);

            Assert.IsTrue(timeline.Exists(B, T(100)), "still fired from its muzzle");
            Assert.IsTrue(timeline.Exists(B, T(150)), "and exists where it was absorbed");
            Assert.IsFalse(timeline.Exists(B, T(151)), "but travels no further");
            Assert.IsFalse(timeline.Exists(B, T(160)));
            Assert.IsTrue(timeline.CausalityHolds(B, T(155)), "retired by a body that is still there");

            // Undoing the collision restores the original flight whole, muzzle to wall.
            timeline.PopLayer(layer);
            Assert.IsTrue(timeline.Exists(B, T(100)));
            Assert.IsTrue(timeline.Exists(B, T(160)));
        }

        struct DoorState
        {
            public int Open;
        }

        struct Flag
        {
            public byte Absorbed;
        }

        [Test]
        public void InvertedCopyClosingADoorOnItsPastSelfJustClaimsIt()
        {
            const int doorChannel = 2;
            var door = new SimId(0xD0);
            var machine = new SimId(0xCC);

            var timeline = NewTimeline();
            timeline.Registry.Register<DoorState>(doorChannel);

            // The door starts open for the whole loop.
            timeline.Claim(door, +1, T(0));
            var doorBody = timeline.Body(door);
            var doorOpen = doorBody.Channel<DoorState>(doorChannel);
            foreach (var raw in new[] { 0, 400 })
                doorOpen.Set(doorBody.WriteSample(T(raw)), new DoorState { Open = 1 });
            timeline.EndSpan(door);

            // X walks forward, through the doorway at 100, on to the machine at 200 and past it.
            Record(timeline, A, +1, new[] { 0, 100, 200, 300, 400 }, raw => new P { X = raw, Hp = 10 });

            // The machine emits an inverted copy, which travels 200 -> 0 and shuts the door at 120.
            // Being converted to the copy's direction, the door records shut over [0,120] and stays
            // open above it.
            var layer = timeline.OpenLayer();
            var inverted = SimId.Spawn(machine, T(200), 0);
            timeline.DeclareOrigin(inverted, A, T(200));
            timeline.Claim(inverted, -1, T(200));
            var invertedBody = timeline.Body(inverted);
            var invertedPose = invertedBody.Channel<P>(Pose);
            foreach (var raw in new[] { 200, 120, 0 })
                invertedPose.Set(invertedBody.WriteSample(T(raw)), new P { X = raw, Hp = 10 });
            timeline.EndSpan(inverted);

            timeline.Claim(door, -1, T(120));
            foreach (var raw in new[] { 120, 60, 0 })
                doorOpen.Set(doorBody.WriteSample(T(raw)), new DoorState { Open = 0 });
            timeline.EndSpan(door);

            Assert.AreEqual(0, doorBody.Sample<DoorState>(doorChannel, T(60)).Snap.Open, "shut below 120");
            Assert.AreEqual(1, doorBody.Sample<DoorState>(doorChannel, T(200)).Snap.Open, "still open above");

            // X's recorded crossing at 100 is now inside a shut door. That conflict lies below the
            // cursor — where it is heading — so it is an ordinary claim in the cursor's direction,
            // not a special case. X records backwards to 50, inert: pushed aside, not killed.
            var xBody = timeline.Body(A);
            var seed = xBody.Sample<P>(Pose, T(100)); // what playback was already showing
            timeline.Claim(A, -1, T(100));
            var xPose = xBody.Channel<P>(Pose);
            xPose.Set(xBody.WriteSample(T(100)), seed.A); // inherited, so the boundary is continuous
            xPose.Set(xBody.WriteSample(T(75)), new P { X = -1f, Hp = 10 });
            xPose.Set(xBody.WriteSample(T(50)), new P { X = -1f, Hp = 10 });
            timeline.EndSpan(A);

            Assert.AreEqual(seed.A.X, xBody.Sample<P>(Pose, T(100)).A.X, 1e-6f, "no jump at the claim");
            Assert.AreEqual(101f, Lerp(xBody.Sample<P>(Pose, T(101))), 1e-3f, "old run resumes smoothly");
            Assert.AreEqual(-1f, xBody.Sample<P>(Pose, T(60)).A.X, 1e-6f, "displaced below the conflict");
            Assert.AreEqual(200f, xBody.Sample<P>(Pose, T(200)).A.X, 1e-6f, "original run intact above it");
            Assert.IsTrue(timeline.Exists(A, T(50)), "X is inert, not destroyed");
            Assert.IsTrue(timeline.Exists(A, T(300)), "and still exists for the whole loop");
            Assert.IsTrue(timeline.CausalityHolds(inverted, T(150)),
                "X still reaches the machine, so the copy still has an origin");

            // X remains a character and so remains a switch target: the player can take it over and
            // re-record its run forwards. Taking over is just attaching an intent source to a body
            // that was already claimed.
            timeline.OpenLayer();
            Record(timeline, A, +1, new[] { 100, 200, 300, 400 }, raw => new P { X = raw * 2, Hp = 10 });

            Assert.AreEqual(400f, xBody.Sample<P>(Pose, T(200)).A.X, 1e-6f, "re-recorded run");
            Assert.AreEqual(-1f, xBody.Sample<P>(Pose, T(50)).A.X, 1e-6f, "below the takeover, unchanged");
            Assert.IsTrue(timeline.CausalityHolds(inverted, T(150)));

            // And the whole machine episode still undoes in one step.
            timeline.PopLayer(layer);
            Assert.AreEqual(1, doorBody.Sample<DoorState>(doorChannel, T(60)).Snap.Open, "door open again");
            Assert.AreEqual(100f, xBody.Sample<P>(Pose, T(100)).A.X, 1e-6f, "X's original run restored");
            Assert.IsFalse(timeline.TryGetBody(inverted, out _));
        }

        [Test]
        public void ClaimIsContinuousWithThePlaybackItReplaces()
        {
            // The invariant that makes seams impossible at a claim boundary: the live body is seeded
            // from whatever playback was already showing, and that seed is written as the span's
            // first sample. Old and new authority therefore agree exactly at the instant they meet,
            // by construction rather than by luck — so re-recording can never teleport a body.
            var timeline = NewTimeline();
            Record(timeline, A, +1, new[] { 0, 100, 200 }, raw => new P { X = raw, Hp = 10 });

            var body = timeline.Body(A);
            var seed = body.Sample<P>(Pose, T(100));

            timeline.OpenLayer();
            timeline.Claim(A, -1, T(100));
            var channel = body.Channel<P>(Pose);
            channel.Set(body.WriteSample(T(100)), seed.A); // inherit, do not invent
            channel.Set(body.WriteSample(T(60)), new P { X = -5f, Hp = 10 });
            timeline.EndSpan(A);

            Assert.AreEqual(100f, body.Sample<P>(Pose, T(100)).A.X, 1e-6f, "continuous at the boundary");
            Assert.AreEqual(101f, Lerp(body.Sample<P>(Pose, T(101))), 1e-3f, "old authority above it");
            Assert.AreEqual(-5f, Lerp(body.Sample<P>(Pose, T(60))), 1e-6f, "diverges below it");
        }

        // ---------------------------------------------------------------- sub-stepping

        [Test]
        public void SubSteppingVisitsEveryAppliedStateInOrder()
        {
            // A fast cursor must not skip states. Divergence checks only run where a state is applied,
            // so a skipped instant is a skipped check — which leaves an impossible history behind
            // rather than merely dropping a frame.
            var timeline = NewTimeline();
            Record(timeline, A, +1, new[] { 0, 100, 200, 300, 400 }, raw => new P { X = raw, Hp = 1 });
            Record(timeline, B, +1, new[] { 0, 50, 150, 250, 400 }, raw => new P { X = raw, Hp = 1 });

            // One frame carrying the cursor 0 -> 400 still visits the union of both bodies' instants.
            var visited = new List<int>();
            var at = T(0);
            while (timeline.TryNextChange(at, +1, T(400), out var next))
            {
                visited.Add(next.Raw);
                at = next;
            }

            foreach (var expected in new[] { 50, 100, 150, 200, 250, 300, 400 })
                Assert.IsTrue(visited.Contains(expected), $"skipped {expected}");
            for (var i = 1; i < visited.Count; i++)
                Assert.IsTrue(visited[i] > visited[i - 1], "must advance strictly, never repeat");
        }

        [Test]
        public void SubSteppingWorksBackwardsToo()
        {
            var timeline = NewTimeline();
            Record(timeline, A, +1, new[] { 0, 100, 200, 300, 400 }, raw => new P { X = raw, Hp = 1 });
            Record(timeline, B, -1, new[] { 400, 250, 150, 50 }, raw => new P { X = raw, Hp = 1 });

            var visited = new List<int>();
            var at = T(400);
            while (timeline.TryNextChange(at, -1, T(0), out var next))
            {
                visited.Add(next.Raw);
                at = next;
            }

            foreach (var expected in new[] { 300, 250, 200, 150, 100, 50, 0 })
                Assert.IsTrue(visited.Contains(expected), $"skipped {expected}");
            for (var i = 1; i < visited.Count; i++)
                Assert.IsTrue(visited[i] < visited[i - 1], "must descend strictly, never repeat");
        }

        [Test]
        public void SubSteppingStopsAtSpanEdgesWhereAuthorityChanges()
        {
            // A seam is a discontinuity, so it has to be a stop even when no sample sits on it.
            var timeline = NewTimeline();
            Record(timeline, A, +1, new[] { 0, 400 }, raw => new P { X = raw, Hp = 10 });
            timeline.OpenLayer();
            Record(timeline, A, -1, new[] { 220, 180 }, raw => new P { X = raw, Hp = 1 });

            var visited = new List<int>();
            var at = T(0);
            while (timeline.TryNextChange(at, +1, T(400), out var next))
            {
                visited.Add(next.Raw);
                at = next;
            }

            Assert.IsTrue(visited.Contains(180), "the newer span takes over here");
            Assert.IsTrue(visited.Contains(220), "and hands back here");
            Assert.AreEqual(1, timeline.Body(A).Sample<P>(Pose, T(200)).A.Hp, "as the samples confirm");
            Assert.AreEqual(10, timeline.Body(A).Sample<P>(Pose, T(221)).A.Hp);
        }

        [Test]
        public void SamplingDoesNotDependOnHowTheCursorGotThere()
        {
            // Seeking to an arbitrary instant is no longer a gameplay requirement, but keeping it
            // exact is a cheap oracle: if a cold read at a random instant is right, then record,
            // rewind, fast-forward and character switching are all right too, and any state the
            // timeline failed to capture shows up immediately.
            var timeline = NewTimeline();
            Record(timeline, A, +1, new[] { 0, 100, 200, 300, 400 }, raw => new P { X = raw, Hp = 10 });
            timeline.OpenLayer();
            Record(timeline, A, -1, new[] { 260, 180, 120 }, raw => new P { X = -raw, Hp = 1 });
            timeline.OpenLayer();
            timeline.Void(A, -1, T(60));
            timeline.Body(A).Extend(T(20));
            timeline.EndSpan(A);

            var body = timeline.Body(A);
            var ascending = new Dictionary<int, string>();
            for (var raw = 0; raw <= 400; raw += 7)
            {
                var sampled = body.Sample<P>(Pose, T(raw));
                ascending[raw] = $"{sampled.Exists}|{sampled.A.X}|{sampled.A.Hp}|{sampled.B.X}|{sampled.T:0.######}";
            }

            // Revisit in a scrambled, deterministic order.
            var scrambled = 0;
            for (var i = 0; i < ascending.Count; i++)
            {
                scrambled = (scrambled * 37 + 101) % 401;
                var raw = scrambled - scrambled % 7;
                var sampled = body.Sample<P>(Pose, T(raw));
                var actual = $"{sampled.Exists}|{sampled.A.X}|{sampled.A.Hp}|{sampled.B.X}|{sampled.T:0.######}";
                Assert.AreEqual(ascending[raw], actual, $"at raw {raw}");
            }
        }

        [Test]
        public void ConflictingArchetypeDeclarationThrows()
        {
            var timeline = NewTimeline();
            timeline.Declare(A, 4);
            timeline.Declare(A, 4); // idempotent
            Assert.Throws<InvalidOperationException>(() => timeline.Declare(A, 9));
        }

        [Test]
        public void LoadingAnUnregisteredChannelFailsLoudly()
        {
            var timeline = NewTimeline();
            Record(timeline, A, +1, new[] { 0, 100 }, raw => new P { X = raw, Hp = 1 });
            var saved = Snapshot(timeline);

            var loaded = new Timeline(); // Pose deliberately not registered
            using var stream = new MemoryStream(saved);
            using var reader = new BinaryReader(stream);
            Assert.Throws<InvalidOperationException>(() => loaded.ReadFrom(reader));
        }
    }
}
