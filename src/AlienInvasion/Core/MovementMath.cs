namespace AlienInvasion.Core
{
    /// <summary>Pure maths for interpolating the positions of the mothership and the set pieces.</summary>
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
