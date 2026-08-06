using System;

namespace AlienInvasion.Core
{
    /// <summary>Decides when a contamination zone has aged out, based on in-game time.</summary>
    public static class ExpiryClock
    {
        public static bool HasExpired(long startTicks, long nowTicks, int months)
        {
            DateTime start = new DateTime(startTicks);
            DateTime expiry = start.AddMonths(months);
            return nowTicks >= expiry.Ticks;
        }

        /// <summary>
        /// Whether days of in-game time have passed since start. Used for durations that
        /// should be measured in game days rather than real seconds - how long the tripods stay
        /// active, for instance. Such a duration stretches naturally with the game speed and
        /// stops advancing while paused, because the caller passes in the game clock.
        /// </summary>
        public static bool HasElapsedDays(long startTicks, long nowTicks, int days)
        {
            DateTime start = new DateTime(startTicks);
            DateTime expiry = start.AddDays(days);
            return nowTicks >= expiry.Ticks;
        }
    }
}
