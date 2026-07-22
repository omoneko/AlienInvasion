namespace AlienInvasion.Core
{
    /// <summary>
    /// トライポッド転倒アニメの純粋な進行計算（Unity非依存）。核直撃で「倒れ→横たわり→消滅」する演出に使う。
    /// </summary>
    public static class ToppleAnimation
    {
        /// <summary>
        /// 転倒の進行率 0..1。倒れ始めはゆっくり→加速する ease-in（t^2）で、根元から倒れ込む重さを表現する。
        /// duration が 0 以下なら即 1（安全側）。
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
        /// 転倒(duration)＋横たわり(dwell)を終え、消滅してよい状態か。
        /// 「倒れた後に消滅」する順序をこの1関数で表す。
        /// </summary>
        public static bool IsFinished(float elapsed, float duration, float dwell)
        {
            return elapsed >= duration + dwell;
        }
    }
}
