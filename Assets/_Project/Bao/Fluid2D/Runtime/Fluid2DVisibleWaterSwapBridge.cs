using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bao.Fluid2D
{
    [DisallowMultipleComponent]
    public class Fluid2DVisibleWaterSwapBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WorldSwapManager worldSwapManager;
        [SerializeField] private Camera playerCamera;

        [Header("World Swap Melt Timing")]
        [Min(0f)]
        [SerializeField] private float meltDelayAfterSwapPressed = 2f;

        [Header("Water Cleanup")]
        [Min(0f)]
        [SerializeField] private float offscreenDestroyDelay = 5f;
        [Min(0f)]
        [SerializeField] private float visibilityPadding = 0.25f;

        [Header("Runtime Status")]
        [SerializeField] private int activeWaterRegionCount;

        private sealed class ActiveWaterRegion
        {
            public IceWaterPhaseToggle2D toggle;
            public float offscreenSince = -1f;
        }

        private readonly List<ActiveWaterRegion> activeWaterRegions = new List<ActiveWaterRegion>();
        private readonly Plane[] cameraFrustumPlanes = new Plane[6];
        private Coroutine pendingMeltRoutine;
        private int swapRequestId;
        private bool world2AppliedForPendingSwap;

        private void OnEnable()
        {
            ResolveReferences();
            if (worldSwapManager != null)
            {
                worldSwapManager.onWorldStateApplied.AddListener(HandleWorldStateApplied);
                worldSwapManager.onBusyChanged.AddListener(HandleSwapBusyChanged);
            }
        }

        private void OnDisable()
        {
            if (worldSwapManager != null)
            {
                worldSwapManager.onWorldStateApplied.RemoveListener(HandleWorldStateApplied);
                worldSwapManager.onBusyChanged.RemoveListener(HandleSwapBusyChanged);
            }

            if (pendingMeltRoutine != null)
            {
                StopCoroutine(pendingMeltRoutine);
                pendingMeltRoutine = null;
            }
        }

        private void Update()
        {
            if (!Application.isPlaying)
                return;

            CleanupOffscreenWater();
        }

        public bool MeltVisibleIceRegion()
        {
            ResolveReferences();
            if (playerCamera == null)
                return false;

            IceWaterPhaseToggle2D target = FindClosestVisibleIceRegion();
            if (target == null)
                return false;

            target.Melt();
            TrackWaterRegion(target);
            return true;
        }

        private void HandleSwapBusyChanged(bool isBusy)
        {
            if (!isBusy)
                return;

            swapRequestId++;
            world2AppliedForPendingSwap = false;

            if (pendingMeltRoutine != null)
                StopCoroutine(pendingMeltRoutine);
            pendingMeltRoutine = StartCoroutine(MeltAfterSwapDelay(swapRequestId));
        }

        private void HandleWorldStateApplied(bool isInWorldA)
        {
            if (worldSwapManager == null || !worldSwapManager.HasSuccessfulSwap || isInWorldA)
                return;

            world2AppliedForPendingSwap = true;
        }

        private IEnumerator MeltAfterSwapDelay(int requestId)
        {
            if (meltDelayAfterSwapPressed > 0f)
                yield return new WaitForSeconds(meltDelayAfterSwapPressed);

            while (requestId == swapRequestId &&
                   worldSwapManager != null &&
                   worldSwapManager.IsBusy &&
                   !world2AppliedForPendingSwap)
            {
                yield return null;
            }

            if (requestId == swapRequestId &&
                world2AppliedForPendingSwap &&
                worldSwapManager != null &&
                worldSwapManager.IsInParallelWorld)
            {
                MeltVisibleIceRegion();
            }

            if (requestId == swapRequestId)
                pendingMeltRoutine = null;
        }

        private IceWaterPhaseToggle2D FindClosestVisibleIceRegion()
        {
            IceWaterPhaseToggle2D[] candidates = FindObjectsByType<IceWaterPhaseToggle2D>(FindObjectsSortMode.None);
            IceWaterPhaseToggle2D closest = null;
            float closestScore = float.PositiveInfinity;

            for (int index = 0; index < candidates.Length; index++)
            {
                IceWaterPhaseToggle2D candidate = candidates[index];
                if (candidate == null || !candidate.isActiveAndEnabled || !candidate.IsIce)
                    continue;

                Bounds bounds = GetRegionBounds(candidate);
                if (!IsVisibleToPlayer(bounds))
                    continue;

                Vector3 viewportPoint = playerCamera.WorldToViewportPoint(bounds.center);
                float horizontal = viewportPoint.x - 0.5f;
                float vertical = viewportPoint.y - 0.5f;
                float score = horizontal * horizontal + vertical * vertical;
                if (score >= closestScore)
                    continue;

                closestScore = score;
                closest = candidate;
            }

            return closest;
        }

        private void CleanupOffscreenWater()
        {
            if (playerCamera == null)
                ResolveReferences();
            if (playerCamera == null)
                return;

            for (int index = activeWaterRegions.Count - 1; index >= 0; index--)
            {
                ActiveWaterRegion region = activeWaterRegions[index];
                IceWaterPhaseToggle2D toggle = region.toggle;
                if (toggle == null || toggle.CurrentPhase != IceWaterPhaseToggle2D.Phase.Water)
                {
                    activeWaterRegions.RemoveAt(index);
                    continue;
                }

                if (IsVisibleToPlayer(GetRegionBounds(toggle)))
                {
                    region.offscreenSince = -1f;
                    continue;
                }

                if (region.offscreenSince < 0f)
                {
                    region.offscreenSince = Time.time;
                    continue;
                }

                if (Time.time - region.offscreenSince < offscreenDestroyDelay)
                    continue;

                toggle.DiscardWater();
                activeWaterRegions.RemoveAt(index);
            }

            activeWaterRegionCount = activeWaterRegions.Count;
        }

        private bool IsVisibleToPlayer(Bounds bounds)
        {
            Bounds paddedBounds = bounds;
            paddedBounds.Expand(visibilityPadding * 2f);
            GeometryUtility.CalculateFrustumPlanes(playerCamera, cameraFrustumPlanes);
            return GeometryUtility.TestPlanesAABB(cameraFrustumPlanes, paddedBounds);
        }

        private static Bounds GetRegionBounds(IceWaterPhaseToggle2D toggle)
        {
            if (toggle.iceCollider != null)
                return toggle.iceCollider.bounds;
            if (toggle.spriteShapeRenderer != null)
                return toggle.spriteShapeRenderer.bounds;
            return new Bounds(toggle.transform.position, Vector3.zero);
        }

        private void TrackWaterRegion(IceWaterPhaseToggle2D toggle)
        {
            for (int index = 0; index < activeWaterRegions.Count; index++)
            {
                if (activeWaterRegions[index].toggle == toggle)
                    return;
            }

            activeWaterRegions.Add(new ActiveWaterRegion { toggle = toggle });
            activeWaterRegionCount = activeWaterRegions.Count;
        }


        private void ResolveReferences()
        {
            if (worldSwapManager == null)
                worldSwapManager = Object.FindFirstObjectByType<WorldSwapManager>();
            if (playerCamera == null)
                playerCamera = Camera.main;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            meltDelayAfterSwapPressed = Mathf.Max(0f, meltDelayAfterSwapPressed);
            offscreenDestroyDelay = Mathf.Max(0f, offscreenDestroyDelay);
            visibilityPadding = Mathf.Max(0f, visibilityPadding);
        }
#endif
    }
}