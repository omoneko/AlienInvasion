namespace AlienInvasion.Core
{
    /// <summary>
    /// Decides whether a nuclear warhead's impact counts as a direct hit on a tripod (pure,
    /// no Unity dependency). Only the horizontal distance matters - a tripod is on the ground,
    /// so height is ignored.
    /// </summary>
    public static class NuclearHitTest
    {
        /// <summary>
        /// True if the horizontal offset (dx, dz) from the impact is within radius.
        /// A negative radius counts as no hit.
        /// </summary>
        public static bool IsDirectHit(float dx, float dz, float radius)
        {
            if (radius < 0f) return false;
            return dx * dx + dz * dz <= radius * radius;
        }
    }
}
