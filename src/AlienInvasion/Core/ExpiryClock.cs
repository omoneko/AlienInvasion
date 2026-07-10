using System;

namespace AlienInvasion.Core
{
    /// <summary>汚染ゾーンの時間経過による消滅判定（ゲーム内時刻ベース）。</summary>
    public static class ExpiryClock
    {
        public static bool HasExpired(long startTicks, long nowTicks, int months)
        {
            DateTime start = new DateTime(startTicks);
            DateTime expiry = start.AddMonths(months);
            return nowTicks >= expiry.Ticks;
        }

        /// <summary>
        /// start から days 日（ゲーム内時間）が経過したか。トライポッドの活動時間など
        /// 「実時間の秒」ではなく「ゲーム内の日数」で規定したい継続時間の判定に使う。
        /// ゲーム速度倍率で自然に伸縮し、一時停止中は進まない(呼び出し側がゲーム時刻を渡すため)。
        /// </summary>
        public static bool HasElapsedDays(long startTicks, long nowTicks, int days)
        {
            DateTime start = new DateTime(startTicks);
            DateTime expiry = start.AddDays(days);
            return nowTicks >= expiry.Ticks;
        }
    }
}
