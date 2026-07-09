using System;

namespace AlienInvasion.Core
{
    /// <summary>トライポッド歩行に使う純粋な数学関数（Unity非依存）。</summary>
    public static class TripodWalk
    {
        /// <summary>2D単位方向 (dx,dz) を angleRad だけ回転する。</summary>
        public static void Rotate(float dx, float dz, float angleRad, out float ndx, out float ndz)
        {
            float cos = (float)Math.Cos(angleRad);
            float sin = (float)Math.Sin(angleRad);
            ndx = dx * cos - dz * sin;
            ndz = dx * sin + dz * cos;
        }

        /// <summary>1軸の境界反射。範囲 [-half, half] を超えたら位置をクランプし方向を内向きに反転する。</summary>
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

        /// <summary>1軸方向成分に沿って速度*経過時間ぶん前進した位置を返す。</summary>
        public static float StepComponent(float pos, float dirComponent, float speed, float dt)
        {
            return pos + dirComponent * speed * dt;
        }
    }
}
