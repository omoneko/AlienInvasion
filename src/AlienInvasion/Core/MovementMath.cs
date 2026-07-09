namespace AlienInvasion.Core
{
    /// <summary>母船/演出の座標補間に使う純粋な数学関数。</summary>
    public static class MovementMath
    {
        public static float EaseInOut(float t)
        {
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return t * t * (3f - 2f * t);
        }

        public static float Lerp(float a, float b, float t)
        {
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return a + (b - a) * t;
        }

        public static bool IsNear(float a, float b, float epsilon)
        {
            float diff = a - b;
            if (diff < 0f) diff = -diff;
            return diff <= epsilon;
        }
    }
}
