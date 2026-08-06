namespace AlienInvasion.Core
{
    /// <summary>
    /// Pure progression maths for the tripod toppling animation (no Unity dependency), used
    /// for the sequence a direct nuclear hit sets off: it falls, lies there, then disappears.
    /// </summary>
    public static class ToppleAnimation
    {
        /// <summary>
        /// How far the fall has progressed, 0 to 1. It eases in as t squared - slow at first,
        /// then accelerating - which is what gives it the weight of toppling from the base.
        /// A duration of zero or less returns 1 immediately, which is the safe direction.
        /// </summary>
        public static float FallFraction(float elapsed, float duration)
        {
            if (duration <= 0f) return 1f;
            float t = elapsed / duration;
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return t * t;
        }

        /// <summary>
        /// Whether the fall (duration) and the time spent lying there (dwell) are both over,
        /// so it can disappear. This single function is what enforces the order: it falls
        /// first, and only then vanishes.
        /// </summary>
        public static bool IsFinished(float elapsed, float duration, float dwell)
        {
            return elapsed >= duration + dwell;
        }
    }
}
