namespace AlienInvasion.Game
{
    /// <summary>NaturalResourceManager の土壌汚染セルへの読み書きラッパ。</summary>
    public static class PollutionField
    {
        public static void ApplyMax(int cellIndex, byte intensity)
        {
            var arr = NaturalResourceManager.instance.m_naturalResources;
            if (cellIndex < 0 || cellIndex >= arr.Length) return;
            if (arr[cellIndex].m_pollution < intensity)
            {
                arr[cellIndex].m_pollution = intensity;
            }
        }

        public static void ClearCell(int cellIndex)
        {
            var arr = NaturalResourceManager.instance.m_naturalResources;
            if (cellIndex < 0 || cellIndex >= arr.Length) return;
            arr[cellIndex].m_pollution = 0;
        }

        public static void Refresh(int minX, int minZ, int maxX, int maxZ)
        {
            NaturalResourceManager.instance.AreaModifiedB(minX, minZ, maxX, maxZ);
        }
    }
}
