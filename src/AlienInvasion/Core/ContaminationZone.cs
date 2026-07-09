namespace AlienInvasion.Core
{
    /// <summary>ワールド座標中心・半径(m)・発生ゲーム内時刻(DateTime.Ticks)の汚染ゾーン。</summary>
    public struct ContaminationZone
    {
        public float CenterX;
        public float CenterZ;
        public float Radius;
        public long StartTicks;

        public ContaminationZone(float centerX, float centerZ, float radius, long startTicks)
        {
            CenterX = centerX;
            CenterZ = centerZ;
            Radius = radius;
            StartTicks = startTicks;
        }
    }
}
