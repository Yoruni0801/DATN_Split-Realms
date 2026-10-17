using UnityEngine;
using System.Runtime.InteropServices;

namespace Bao.Fluid2D
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Fluid2DParticle
    {
        public Vector2 position;
        public Vector2 velocity;
        public Vector2 predictedPosition;
        public float density;
        public float nearDensity;
        public float pressure;
        public float nearPressure;
        public int active; // 0 = Inactive, 1 = Active
        public int type;   // Loại chất lỏng (ví dụ: 0: Nước, 1: Slime, 2: Dung nham)
    }

    /// <summary>
    /// Objective water fragmentation metrics (transition / test only — not per-frame).
    /// </summary>
    public struct WaterFragmentationDiagnostics
    {
        public int activeCount;
        public int isolatedCount;
        public int clusterCount;
        public float largestClusterFraction;
        public float maxSpeed;
        public float meanSpeed;

        public override string ToString()
        {
            return $"active={activeCount} isolated={isolatedCount} clusters={clusterCount} " +
                   $"largestFrac={largestClusterFraction:F3} maxSpd={maxSpeed:F2} meanSpd={meanSpeed:F2}";
        }
    }
}
