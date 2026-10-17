using UnityEngine;
using System.Runtime.InteropServices;

namespace Bao.Fluid2D
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CircleObstacle
    {
        public Vector2 center; // 8 bytes
        public float radius;   // 4 bytes
        public int active;     // 4 bytes (0: Inactive, 1: Active)
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SegmentObstacle
    {
        public Vector2 a;      // 8 bytes
        public Vector2 b;      // 8 bytes
        public float radius;   // 4 bytes
        public int active;     // 4 bytes (0: Inactive, 1: Active)
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DynamicBody
    {
        public Vector2 position; // 8 bytes
        public Vector2 velocity; // 8 bytes
        public float radius;     // 4 bytes
        public float mass;       // 4 bytes
        public int active;       // 4 bytes (0: Inactive, 1: Active)
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DynamicBodyForce
    {
        public int forceX; // 4 bytes (Lực X nhân 10000)
        public int forceY; // 4 bytes (Lực Y nhân 10000)
    }
}
