using UnityEngine;

namespace Bao.Bosses
{
    public static class ProceduralDirtVFX
    {
        public static NidusBurrowGroundVFX SpawnBurrow(Vector3 groundPosition, float radius)
        {
            return NidusBurrowGroundVFX.Spawn(
                groundPosition,
                NidusBurrowVFXMode.Burrow,
                radius);
        }

        public static NidusBurrowGroundVFX SpawnTrail(Vector3 groundPosition, float radius)
        {
            return NidusBurrowGroundVFX.Spawn(
                groundPosition,
                NidusBurrowVFXMode.Trail,
                radius);
        }

        public static NidusBurrowGroundVFX SpawnWarning(
            Vector3 groundPosition,
            float radius,
            float duration,
            Color? accentColor = null)
        {
            NidusBurrowGroundVFX warning = NidusBurrowGroundVFX.Spawn(
                groundPosition,
                NidusBurrowVFXMode.Warning,
                radius,
                duration);
            if (accentColor.HasValue)
                warning.SetAccentColor(accentColor.Value);
            return warning;
        }

        public static NidusBurrowGroundVFX SpawnEmerge(Vector3 groundPosition, float radius)
        {
            return NidusBurrowGroundVFX.Spawn(
                groundPosition,
                NidusBurrowVFXMode.Emerge,
                radius);
        }

        // Compatibility for older callers. New boss code uses the explicit variants.
        public static void Spawn(Vector3 position, float radius, int rockCount, int dustCount)
        {
            if (radius >= 2f || rockCount >= 25)
            {
                SpawnEmerge(position, radius);
            }
            else if (radius <= 0.75f || rockCount <= 5)
            {
                SpawnTrail(position, radius);
            }
            else
            {
                SpawnBurrow(position, radius);
            }
        }
    }
}
