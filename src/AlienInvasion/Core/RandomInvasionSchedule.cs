namespace AlienInvasion.Core
{
    /// <summary>
    /// Decides when to roll for a random invasion, and whether that roll fires (no UnityEngine
    /// dependency).
    /// <para>
    /// The schedule runs on the <b>game</b> clock, not real time: one check per in-game day, each
    /// firing with probability 1/averageDays, so the mean interval is exactly the number of days
    /// the player configured. Driving it from the game clock means it stops while the game is
    /// paused and stretches with the game speed, which is also how TripodActiveDays measures how
    /// long the tripods stay - the two now agree.
    /// </para>
    /// The Game layer owns the clock, the RNG and the priming flag; everything decidable without
    /// them is here, where it can be tested.
    /// </summary>
    public static class RandomInvasionSchedule
    {
        /// <summary>The roll is drawn from [0, RollRange).</summary>
        public const int RollRange = 10000;

        /// <summary>Bounds of the "average days between invasions" setting, and its default.</summary>
        public const int MinAverageDays = 5;
        public const int MaxAverageDays = 365;
        public const int DefaultAverageDays = 60;

        /// <summary>
        /// Whether at least one in-game day has passed since the last check.
        /// <para>
        /// A clock that has moved backwards - which is what loading a different save looks like -
        /// counts as not due. The caller re-primes on level load anyway; this is the second line
        /// of defence, and it keeps the function total rather than surprising.
        /// </para>
        /// </summary>
        public static bool IsCheckDue(long lastCheckTicks, long nowTicks)
        {
            if (nowTicks < lastCheckTicks) return false;
            return ExpiryClock.HasElapsedDays(lastCheckTicks, nowTicks, 1);
        }

        /// <summary>
        /// Whether a due check fires, given a roll in [0, RollRange).
        /// <para>
        /// True when roll &lt; RollRange / averageDays. Integer division rounds the threshold
        /// down, which makes invasions very slightly rarer than the label says rather than more
        /// frequent - the safe direction for a destructive event.
        /// </para>
        /// A non-positive averageDays never fires, so a corrupt or hand-edited setting cannot
        /// turn into an invasion every single day.
        /// </summary>
        public static bool ShouldFire(int averageDays, int roll)
        {
            if (averageDays <= 0) return false;
            if (roll < 0 || roll >= RollRange) return false;
            return roll < RollRange / averageDays;
        }

        /// <summary>Clamps the stored setting into the range the options slider offers.</summary>
        public static int ClampAverageDays(int days)
        {
            if (days < MinAverageDays) return MinAverageDays;
            if (days > MaxAverageDays) return MaxAverageDays;
            return days;
        }
    }
}
