using System;

namespace AlienInvasion.Core
{
    /// <summary>汚染ゾーンの時間経過による消滅判定（ゲーム内時刻ベース）。</summary>
    public static class ExpiryClock
    {
        public static bool HasExpired(long startTicks, long nowTicks, int years)
        {
            DateTime start = new DateTime(startTicks);
            DateTime expiry = start.AddYears(years);
            return nowTicks >= expiry.Ticks;
        }
    }
}
