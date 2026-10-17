using System.Collections.Generic;
using UnityEngine;

namespace Bao.Fluid2D
{
    /// <summary>
    /// Lightweight enable/disable registry for <see cref="FluidDrain2D"/>.
    /// Simulation hot paths read this list instead of scanning the scene every FixedUpdate.
    /// </summary>
    public static class FluidDrainRegistry
    {
        private static readonly List<FluidDrain2D> s_Drains = new List<FluidDrain2D>(8);
        private static int s_Version;
        private static int s_RegisterCount;
        private static int s_UnregisterCount;
        private static int s_FullScanCount;
        private static bool s_NeedsCompact;

        /// <summary>Monotonic version bumped on register/unregister/compact.</summary>
        public static int Version => s_Version;

        /// <summary>Registered drains (may include recently destroyed entries until compact).</summary>
        public static int RegisteredCount => s_Drains.Count;

        public static int RegisterCount => s_RegisterCount;
        public static int UnregisterCount => s_UnregisterCount;

        /// <summary>
        /// Number of full scene scans performed (should stay 0 in play mode after registry is used).
        /// Reserved for diagnostics / emergency rebuild tooling.
        /// </summary>
        public static int FullScanCount => s_FullScanCount;

        public static IReadOnlyList<FluidDrain2D> Drains => s_Drains;

        public static void Register(FluidDrain2D drain)
        {
            if (drain == null)
                return;

            for (int i = 0; i < s_Drains.Count; i++)
            {
                if (s_Drains[i] == drain)
                    return;
            }

            s_Drains.Add(drain);
            s_RegisterCount++;
            s_Version++;
        }

        public static void Unregister(FluidDrain2D drain)
        {
            if (drain == null)
                return;

            for (int i = 0; i < s_Drains.Count; i++)
            {
                if (s_Drains[i] == drain)
                {
                    s_Drains[i] = null;
                    s_UnregisterCount++;
                    s_NeedsCompact = true;
                    s_Version++;
                    return;
                }
            }
        }

        /// <summary>
        /// Copies world bounds of active+enabled drains into dest[0..count).
        /// Disabled drains are ignored. Null slots are compacted lazily.
        /// </summary>
        public static void CopyActiveBounds(Vector4[] dest, int maxCount, out int count)
        {
            count = 0;
            if (dest == null || maxCount <= 0)
                return;

            if (s_NeedsCompact)
                Compact();

            for (int i = 0; i < s_Drains.Count && count < maxCount; i++)
            {
                FluidDrain2D drain = s_Drains[i];
                if (drain == null)
                {
                    s_NeedsCompact = true;
                    continue;
                }

                if (!drain.isActiveAndEnabled)
                    continue;

                dest[count++] = drain.GetBounds();
            }
        }

        /// <summary>
        /// Emergency rebuild via scene scan. Prefer lifecycle Register/Unregister.
        /// Counts as a full scan for diagnostics.
        /// </summary>
        public static void RebuildFromScene()
        {
            s_FullScanCount++;
            s_Drains.Clear();
            var found = Object.FindObjectsByType<FluidDrain2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null)
                    s_Drains.Add(found[i]);
            }
            s_NeedsCompact = false;
            s_Version++;
        }

        public static void ClearDiagnosticsCounters()
        {
            s_RegisterCount = 0;
            s_UnregisterCount = 0;
            s_FullScanCount = 0;
        }

        private static void Compact()
        {
            int write = 0;
            for (int i = 0; i < s_Drains.Count; i++)
            {
                if (s_Drains[i] != null)
                    s_Drains[write++] = s_Drains[i];
            }

            if (write < s_Drains.Count)
                s_Drains.RemoveRange(write, s_Drains.Count - write);

            s_NeedsCompact = false;
            s_Version++;
        }
    }
}
