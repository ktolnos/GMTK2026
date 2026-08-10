namespace Chronomancers.Sim
{
    /// <summary>
    /// The small amount of geometry the rules themselves need. Plain floats rather than a vector type,
    /// because the core deliberately has no engine reference — and because this way it is testable
    /// headlessly, which matters for a predicate that decides whether history gets erased.
    /// </summary>
    public static class Geometry
    {
        /// <summary>
        /// Whether segment <c>p1..p2</c> properly crosses segment <c>q1..q2</c>.
        /// <para>
        /// This is the <i>swept</i> form of a passage test: give it the two endpoints of one step of recorded
        /// motion and a gate line, and it answers "did this go through" without any dependence on how far
        /// apart the samples are. A sampled test cannot do that — a body recorded at <c>|rate| &gt; 1</c> takes
        /// long strides, and a point test simply misses the ones that straddle a narrow gap.
        /// </para>
        /// <para>
        /// Touching and collinear cases count as <b>no</b> crossing, on purpose. Sliding along a gate, or
        /// stopping exactly on it, is not passing through it — and since a false positive here claims a body
        /// and overwrites its history, the strict reading is the safe one.
        /// </para>
        /// </summary>
        /// <summary>
        /// How far off a line a point has to be to count as off it at all. Anything closer is treated as
        /// touching, which reports no crossing — the direction that does not erase history.
        /// </summary>
        const float OnTheLine = 1e-6f;

        public static bool SegmentsCross(
            float p1X, float p1Y, float p2X, float p2Y,
            float q1X, float q1Y, float q2X, float q2Y)
        {
            var s1 = Side(Cross(q2X - q1X, q2Y - q1Y, p1X - q1X, p1Y - q1Y));
            var s2 = Side(Cross(q2X - q1X, q2Y - q1Y, p2X - q1X, p2Y - q1Y));
            var s3 = Side(Cross(p2X - p1X, p2Y - p1Y, q1X - p1X, q1Y - p1Y));
            var s4 = Side(Cross(p2X - p1X, p2Y - p1Y, q2X - p1X, q2Y - p1Y));

            // Each segment must have the other's endpoints *strictly* on opposite sides.
            //
            // Zero has to be excluded explicitly rather than folded in with one sign: an endpoint sitting
            // exactly on the other line means the segments touch, and comparing `d > 0` booleans would sort
            // that zero onto the negative side and report a crossing. A body that walks up to a door and stops
            // dead on the gate would then be claimed for going through it.
            //
            // Requiring it of *both* segments is separately what stops a body passing through the wall beside a
            // door being blamed on the door: it crosses the gate's infinite line, but not the gate.
            if (s1 == 0 || s2 == 0 || s3 == 0 || s4 == 0) return false;

            return s1 != s2 && s3 != s4;
        }

        static int Side(float d) => d > OnTheLine ? 1 : d < -OnTheLine ? -1 : 0;

        static float Cross(float aX, float aY, float bX, float bY) => aX * bY - aY * bX;
    }
}
