using System;

namespace AlienInvasion.Core
{
    /// <summary>Pure maths for walking the tripods (no Unity dependency).</summary>
    public static class TripodWalk
    {
        /// <summary>Rotates the 2D unit direction (dx, dz) by angleRad.</summary>
        public static void Rotate(float dx, float dz, float angleRad, out float ndx, out float ndz)
        {
            float cos = (float)Math.Cos(angleRad);
            float sin = (float)Math.Sin(angleRad);
            ndx = dx * cos - dz * sin;
            ndz = dx * sin + dz * cos;
        }

        /// <summary>Reflects off the bounds on one axis: past [-half, half] the position is clamped and the direction flipped inwards.</summary>
        public static void BounceAxis(float pos, float dir, float half, out float newPos, out float newDir)
        {
            if (pos > half)
            {
                newPos = half;
                newDir = -Math.Abs(dir);
            }
            else if (pos < -half)
            {
                newPos = -half;
                newDir = Math.Abs(dir);
            }
            else
            {
                newPos = pos;
                newDir = dir;
            }
        }

        /// <summary>Advances one axis by speed times the elapsed time.</summary>
        public static float StepComponent(float pos, float dirComponent, float speed, float dt)
        {
            return pos + dirComponent * speed * dt;
        }
    }
}
