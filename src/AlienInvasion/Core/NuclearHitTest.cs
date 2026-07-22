namespace AlienInvasion.Core
{
    /// <summary>
    /// 核弾頭の着弾点とトライポッドの「直撃」判定（Unity非依存の純粋関数）。
    /// 水平距離のみで判定する（トライポッドは接地しているため高さは無視）。
    /// </summary>
    public static class NuclearHitTest
    {
        /// <summary>
        /// 着弾点からの水平差分 (dx, dz) が radius 以内なら直撃（true）。
        /// 負の radius は非該当（false）として扱う。
        /// </summary>
        public static bool IsDirectHit(float dx, float dz, float radius)
        {
            if (radius < 0f) return false;
            return dx * dx + dz * dz <= radius * radius;
        }
    }
}
