using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public sealed class TimelineVortexPullBehaviour : PlayableBehaviour
{
    public VortexMotionType motionType;
    public TimelineSpeedMode speedMode;
    public float speedMultiplier;
    public float fixedSpeed;
    public float fixedDuration;
    public float speedVariation;

    public Transform targetPointA;
    public List<Transform> itemTargetList;
    public Transform itemsContainerParent;
    public bool autoFindItemsInRadius;
    public float searchRadius;
    public LayerMask searchLayerMask;
    public string searchTagFilter;
    public bool autoDetectGamePickups;
    public float staggerSpread;
    public bool disperseAngles;
    public float randomnessVariation;

    public Vector3 targetOffset;
    public VortexPlane plane;
    public VortexSpinDirection spinDirection;
    public float spiralRotations;
    public float initialRadiusOverride;
    public AnimationCurve suctionCurve;
    public AnimationCurve spiralRadiusCurve;
    public float heightArc;
    public AnimationCurve heightCurve;
    public bool orientTowardsTarget;
    public bool spinItem;
    public float itemSelfSpinMultiplier;
    public bool shrinkOnAbsorb;
    public AnimationCurve scaleCurve;
    public bool fadeAlpha;
    public AnimationCurve alphaCurve;
    public TimelineVortexEndAction endAction;
    public bool restoreStateOnExitIfCancelled;
    public bool applyInEditMode;

    private class ItemVortexState
    {
        public Transform transform;
        public Vector3 startPosition;
        public Quaternion startRotation;
        public Vector3 startScale;
        public bool initialActiveSelf;

        public float initialRadius;
        public float initialDistanceToTarget;
        public float initialAngle;
        public float radiusMultiplierVariance = 1f;
        public float speedMultiplierVariance = 1f;
        public float staggerOffset = 0f;
        public bool isCompleted = false;

        public Rigidbody2D rb2D;
        public Rigidbody rb3D;
        public SpriteRenderer[] spriteRenderers;
        public CanvasGroup[] canvasGroups;
        public float[] initialSpriteAlphas;
        public float[] initialCanvasAlphas;
    }

    private readonly List<ItemVortexState> trackedItems = new List<ItemVortexState>();
    private Transform resolvedTarget;
    private bool isEntered;
    private bool warnedMissingReferences;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (!Application.isPlaying && !applyInEditMode)
        {
            return;
        }

        TimelineSeekContext.Evaluate(playable, info);

        ResolveTargetPoint(playerData);

        if (resolvedTarget == null)
        {
            WarnOnce(ref warnedMissingReferences, "[TimelineVortexPull] Missing Target Point A binding or reference.");
            return;
        }

        if (!isEntered)
        {
            Enter(playerData);
        }

        if (trackedItems.Count == 0)
        {
            WarnOnce(ref warnedMissingReferences, "[TimelineVortexPull] No items found to pull into Point A.");
            return;
        }

        double duration = playable.GetDuration();
        double time = playable.GetTime();

        EvaluateMovement(playable, (float)time, (float)duration);
    }

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        if (!Application.isPlaying && !applyInEditMode)
        {
            return;
        }

        for (int i = 0; i < trackedItems.Count; i++)
        {
            if (trackedItems[i] != null)
            {
                trackedItems[i].isCompleted = false;
            }
        }
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        Exit();
    }

    public override void OnGraphStop(Playable playable)
    {
        Exit();
        trackedItems.Clear();
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        Exit();
        trackedItems.Clear();
    }

    private void Enter(object playerData)
    {
        isEntered = true;
        trackedItems.Clear();

        List<Transform> candidateTransforms = CollectItemCandidates(playerData);
        if (candidateTransforms.Count == 0)
        {
            return;
        }

        Vector3 targetCenter = resolvedTarget.position + targetOffset;
        int totalItems = candidateTransforms.Count;

        for (int i = 0; i < totalItems; i++)
        {
            Transform item = candidateTransforms[i];
            if (item == null || item == resolvedTarget) continue;

            ItemVortexState state = new ItemVortexState
            {
                transform = item,
                startPosition = item.position,
                startRotation = item.rotation,
                startScale = item.localScale,
                initialActiveSelf = item.gameObject.activeSelf,
                initialDistanceToTarget = Vector3.Distance(item.position, targetCenter),
                isCompleted = false
            };

            // Vector delta ban đầu từ tâm A đến điểm xuất phát
            Vector3 initialDelta = state.startPosition - targetCenter;
            float u0 = 0f;
            float v0 = 0f;

            switch (plane)
            {
                case VortexPlane.XY_2D:
                    u0 = initialDelta.x;
                    v0 = initialDelta.y;
                    break;
                case VortexPlane.XZ_3D:
                    u0 = initialDelta.x;
                    v0 = initialDelta.z;
                    break;
                case VortexPlane.YZ:
                    u0 = initialDelta.y;
                    v0 = initialDelta.z;
                    break;
            }

            float initialRadius = Mathf.Sqrt(u0 * u0 + v0 * v0);
            if (initialRadiusOverride > 0.001f)
            {
                initialRadius = initialRadiusOverride;
            }
            else if (initialRadius <= 0.05f)
            {
                initialRadius = 1.5f + (i * 0.3f);
            }

            float initialAngle = Mathf.Atan2(v0, u0);

            // Phân bổ góc ban đầu so le cho nhiều vật phẩm (nếu dùng VortexSpiral)
            if (disperseAngles && totalItems > 1)
            {
                initialAngle += (i / (float)totalItems) * Mathf.PI * 2f;
            }

            // Độ biến thiên ngẫu nhiên nhẹ dựa trên ID của item (100% deterministic)
            float pseudoRand = Mathf.Abs(Mathf.Sin(item.GetInstanceID() * 12.9898f + i * 78.233f) * 43758.5453f);
            pseudoRand -= Mathf.Floor(pseudoRand);

            state.radiusMultiplierVariance = 1f + (pseudoRand * 2f - 1f) * randomnessVariation;

            // Độ biến thiên tốc độ
            float speedVar = (pseudoRand * 2f - 1f) * speedVariation;
            state.speedMultiplierVariance = Mathf.Max(0.1f, 1f + speedVar);

            // Stagger thời gian hút so le
            if (staggerSpread > 0f && totalItems > 1)
            {
                state.staggerOffset = (i / (float)totalItems) * staggerSpread;
            }
            else
            {
                state.staggerOffset = 0f;
            }

            state.initialRadius = initialRadius;
            state.initialAngle = initialAngle;

            // Cache renderers & physics
            CacheItemComponents(state);
            DisablePhysics(state);

            trackedItems.Add(state);
        }
    }

    private void CacheItemComponents(ItemVortexState state)
    {
        state.rb2D = state.transform.GetComponent<Rigidbody2D>();
        state.rb3D = state.transform.GetComponent<Rigidbody>();

        state.spriteRenderers = state.transform.GetComponentsInChildren<SpriteRenderer>(true);
        if (state.spriteRenderers != null && state.spriteRenderers.Length > 0)
        {
            state.initialSpriteAlphas = new float[state.spriteRenderers.Length];
            for (int j = 0; j < state.spriteRenderers.Length; j++)
            {
                if (state.spriteRenderers[j] != null)
                {
                    state.initialSpriteAlphas[j] = state.spriteRenderers[j].color.a;
                }
            }
        }

        state.canvasGroups = state.transform.GetComponentsInChildren<CanvasGroup>(true);
        if (state.canvasGroups != null && state.canvasGroups.Length > 0)
        {
            state.initialCanvasAlphas = new float[state.canvasGroups.Length];
            for (int j = 0; j < state.canvasGroups.Length; j++)
            {
                if (state.canvasGroups[j] != null)
                {
                    state.initialCanvasAlphas[j] = state.canvasGroups[j].alpha;
                }
            }
        }
    }

    private void DisablePhysics(ItemVortexState state)
    {
        if (state.rb2D != null && Application.isPlaying)
        {
            state.rb2D.linearVelocity = Vector2.zero;
            state.rb2D.angularVelocity = 0f;
            state.rb2D.gravityScale = 0f;
        }

        if (state.rb3D != null && Application.isPlaying)
        {
            state.rb3D.linearVelocity = Vector3.zero;
            state.rb3D.angularVelocity = Vector3.zero;
            state.rb3D.isKinematic = true;
        }
    }

    private void EvaluateMovement(Playable playable, float currentTime, float clipDuration)
    {
        if (resolvedTarget == null) return;

        Vector3 targetCenter = resolvedTarget.position + targetOffset;
        float dirSign = spinDirection == VortexSpinDirection.CounterClockwise ? 1f : -1f;

        for (int i = 0; i < trackedItems.Count; i++)
        {
            ItemVortexState state = trackedItems[i];
            if (state == null || state.transform == null) continue;

            // 1. TÍNH TOÁN TIẾN ĐỘ THỜI GIAN THEO SPEED MODE
            float effectiveSpeedMult = speedMultiplier * state.speedMultiplierVariance;
            float rawNormalizedTime = 0f;

            switch (speedMode)
            {
                case TimelineSpeedMode.FixedSpeed:
                {
                    // Tốc độ mét/giây
                    float totalDist = Mathf.Max(0.1f, state.initialDistanceToTarget);
                    float travelTime = totalDist / Mathf.Max(0.1f, fixedSpeed * effectiveSpeedMult);
                    rawNormalizedTime = travelTime > 0.001f ? (currentTime / travelTime) : 1f;
                    break;
                }
                case TimelineSpeedMode.FixedDuration:
                {
                    // Thời gian cố định theo giây
                    float totalTime = Mathf.Max(0.05f, fixedDuration / effectiveSpeedMult);
                    rawNormalizedTime = currentTime / totalTime;
                    break;
                }
                case TimelineSpeedMode.FitToClipDuration:
                default:
                {
                    // Theo độ dài của clip trên Timeline kết hợp hệ số nhân tốc độ
                    float totalTime = clipDuration > 0.001f ? (clipDuration / effectiveSpeedMult) : 1f;
                    rawNormalizedTime = currentTime / totalTime;
                    break;
                }
            }

            // Tính thời gian chuẩn hóa riêng cho từng item theo độ lệch pha stagger
            float itemNormalizedTime = rawNormalizedTime;
            if (state.staggerOffset > 0f)
            {
                if (rawNormalizedTime < state.staggerOffset)
                {
                    itemNormalizedTime = 0f;
                }
                else
                {
                    float remainingWindow = 1f - state.staggerOffset;
                    itemNormalizedTime = remainingWindow > 0.001f
                        ? Mathf.Clamp01((rawNormalizedTime - state.staggerOffset) / remainingWindow)
                        : 1f;
                }
            }
            else
            {
                itemNormalizedTime = Mathf.Clamp01(rawNormalizedTime);
            }

            // Chưa đến lượt hút -> Giữ nguyên vị trí ban đầu
            if (itemNormalizedTime <= 0f)
            {
                state.transform.position = state.startPosition;
                state.transform.rotation = state.startRotation;
                state.transform.localScale = state.startScale;
                continue;
            }

            // Đánh giá đường cong tiến độ hút (suction progress: 0 -> 1)
            float suctionProgress = suctionCurve != null
                ? Mathf.Clamp01(suctionCurve.Evaluate(itemNormalizedTime))
                : itemNormalizedTime;

            Vector3 computedPosition = Vector3.zero;

            // ================= 1. CHUYỂN ĐỘNG THEO ĐƯỜNG THẲNG (STRAIGHT LINE) =================
            if (motionType == VortexMotionType.StraightLine)
            {
                computedPosition = Vector3.Lerp(state.startPosition, targetCenter, suctionProgress);

                if (heightArc != 0f)
                {
                    float h = heightArc * (heightCurve != null ? heightCurve.Evaluate(itemNormalizedTime) : 0f);
                    switch (plane)
                    {
                        case VortexPlane.XY_2D: computedPosition.z += h; break;
                        case VortexPlane.XZ_3D: computedPosition.y += h; break;
                        case VortexPlane.YZ:    computedPosition.x += h; break;
                    }
                }
            }
            // ================= 2. CHUYỂN ĐỘNG VÒNG CUNG PARABOL (ARC CURVE) =================
            else if (motionType == VortexMotionType.ArcCurve)
            {
                computedPosition = Vector3.Lerp(state.startPosition, targetCenter, suctionProgress);
                float arc = heightArc * (heightCurve != null ? heightCurve.Evaluate(itemNormalizedTime) : Mathf.Sin(suctionProgress * Mathf.PI));

                switch (plane)
                {
                    case VortexPlane.XY_2D: computedPosition.y += arc; break;
                    case VortexPlane.XZ_3D: computedPosition.y += arc; break;
                    case VortexPlane.YZ:    computedPosition.y += arc; break;
                }
            }
            // ================= 3. CHUYỂN ĐỘNG XOẮN ỐC LỐC XOÁY (VORTEX SPIRAL) =================
            else
            {
                float radiusMultiplier = spiralRadiusCurve != null
                    ? Mathf.Max(0f, spiralRadiusCurve.Evaluate(itemNormalizedTime))
                    : (1f - suctionProgress);

                float currentRadius = state.initialRadius * (1f - suctionProgress) * radiusMultiplier * state.radiusMultiplierVariance;

                float totalAngleDelta = dirSign * (spiralRotations * Mathf.PI * 2f * suctionProgress);
                float currentAngle = state.initialAngle + totalAngleDelta;

                float u = currentRadius * Mathf.Cos(currentAngle);
                float v = currentRadius * Mathf.Sin(currentAngle);

                float heightOffset = heightArc * (heightCurve != null ? heightCurve.Evaluate(itemNormalizedTime) : 0f);

                switch (plane)
                {
                    case VortexPlane.XY_2D:
                        computedPosition.x = targetCenter.x + u;
                        computedPosition.y = targetCenter.y + v;
                        computedPosition.z = Mathf.Lerp(state.startPosition.z, targetCenter.z, suctionProgress) + heightOffset;
                        break;

                    case VortexPlane.XZ_3D:
                        computedPosition.x = targetCenter.x + u;
                        computedPosition.y = Mathf.Lerp(state.startPosition.y, targetCenter.y, suctionProgress) + heightOffset;
                        computedPosition.z = targetCenter.z + v;
                        break;

                    case VortexPlane.YZ:
                        computedPosition.x = Mathf.Lerp(state.startPosition.x, targetCenter.x, suctionProgress) + heightOffset;
                        computedPosition.y = targetCenter.y + u;
                        computedPosition.z = targetCenter.z + v;
                        break;
                }
            }

            // Cập nhật vị trí vật phẩm
            if (state.rb2D != null && Application.isPlaying)
            {
                state.rb2D.position = (Vector2)computedPosition;
                state.rb2D.linearVelocity = Vector2.zero;
            }
            state.transform.position = computedPosition;

            // Cập nhật góc xoay vật phẩm
            if (orientTowardsTarget && (targetCenter - computedPosition).sqrMagnitude > 0.001f)
            {
                Vector3 toTarget = targetCenter - computedPosition;
                if (plane == VortexPlane.XY_2D)
                {
                    float angle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
                    state.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                }
                else if (plane == VortexPlane.XZ_3D)
                {
                    state.transform.rotation = Quaternion.LookRotation(toTarget, Vector3.up);
                }
            }
            else if (spinItem)
            {
                float selfSpinAngle = dirSign * (spiralRotations * 360f * itemSelfSpinMultiplier * suctionProgress);
                Vector3 startEuler = state.startRotation.eulerAngles;

                switch (plane)
                {
                    case VortexPlane.XY_2D:
                        state.transform.rotation = Quaternion.Euler(startEuler.x, startEuler.y, startEuler.z + selfSpinAngle);
                        break;
                    case VortexPlane.XZ_3D:
                        state.transform.rotation = Quaternion.Euler(startEuler.x, startEuler.y + selfSpinAngle, startEuler.z);
                        break;
                    case VortexPlane.YZ:
                        state.transform.rotation = Quaternion.Euler(startEuler.x + selfSpinAngle, startEuler.y, startEuler.z);
                        break;
                }
            }

            // Cập nhật thu nhỏ Scale vật phẩm
            if (shrinkOnAbsorb)
            {
                float scaleFactor = scaleCurve != null ? Mathf.Max(0f, scaleCurve.Evaluate(itemNormalizedTime)) : (1f - suctionProgress);
                state.transform.localScale = state.startScale * scaleFactor;
            }

            // Cập nhật độ trong suốt Alpha
            if (fadeAlpha)
            {
                float alphaFactor = alphaCurve != null ? Mathf.Clamp01(alphaCurve.Evaluate(itemNormalizedTime)) : (1f - suctionProgress);
                ApplyAlpha(state, alphaFactor);
            }

            // Khi chạm tâm Điểm A -> Kích hoạt hoàn tất từng vật phẩm
            if (itemNormalizedTime >= 0.999f && !state.isCompleted)
            {
                CompleteSingleItemAction(state);
            }
        }
    }

    private void ApplyAlpha(ItemVortexState state, float alphaFactor)
    {
        if (state.spriteRenderers != null && state.initialSpriteAlphas != null)
        {
            for (int i = 0; i < state.spriteRenderers.Length; i++)
            {
                if (state.spriteRenderers[i] != null)
                {
                    Color c = state.spriteRenderers[i].color;
                    c.a = state.initialSpriteAlphas[i] * alphaFactor;
                    state.spriteRenderers[i].color = c;
                }
            }
        }

        if (state.canvasGroups != null && state.initialCanvasAlphas != null)
        {
            for (int i = 0; i < state.canvasGroups.Length; i++)
            {
                if (state.canvasGroups[i] != null)
                {
                    state.canvasGroups[i].alpha = state.initialCanvasAlphas[i] * alphaFactor;
                }
            }
        }
    }

    private void CompleteSingleItemAction(ItemVortexState state)
    {
        state.isCompleted = true;

        if (!Application.isPlaying)
        {
            return;
        }

        if (state.transform == null) return;

        switch (endAction)
        {
            case TimelineVortexEndAction.DeactivateItem:
                state.transform.gameObject.SetActive(false);
                break;

            case TimelineVortexEndAction.DestroyItem:
                UnityEngine.Object.Destroy(state.transform.gameObject);
                break;

            case TimelineVortexEndAction.ParentToTarget:
                if (resolvedTarget != null)
                {
                    state.transform.SetParent(resolvedTarget, true);
                    state.transform.localPosition = targetOffset;
                }
                break;

            case TimelineVortexEndAction.CollectPickupIfApplicable:
                ExecuteCollectPickup(state.transform);
                break;

            case TimelineVortexEndAction.None:
            default:
                break;
        }
    }

    private void ExecuteCollectPickup(Transform item)
    {
        if (item == null) return;

        // 1. Kiểm tra CrystalPickupItem
        var crystalItem = item.GetComponent<EnemySystem.CrystalPickupItem>();
        if (crystalItem != null)
        {
            crystalItem.Collect();
            return;
        }

        // 2. Kiểm tra NidusDeathDropItem
        var nidusDrop = item.GetComponent<NidusDeathDropItem>();
        if (nidusDrop != null)
        {
            nidusDrop.MarkPickedUp(resolvedTarget != null ? resolvedTarget : item, targetOffset);
            return;
        }

        // Fallback mặc định: Deactivate
        item.gameObject.SetActive(false);
    }

    private void Exit()
    {
        if (!isEntered)
        {
            return;
        }

        if (restoreStateOnExitIfCancelled)
        {
            for (int i = 0; i < trackedItems.Count; i++)
            {
                ItemVortexState state = trackedItems[i];
                if (state != null && state.transform != null && !state.isCompleted)
                {
                    state.transform.position = state.startPosition;
                    state.transform.rotation = state.startRotation;
                    state.transform.localScale = state.startScale;
                    state.transform.gameObject.SetActive(state.initialActiveSelf);
                    ApplyAlpha(state, 1f);
                }
            }
        }

        isEntered = false;
    }

    private List<Transform> CollectItemCandidates(object playerData)
    {
        HashSet<Transform> candidates = new HashSet<Transform>();

        // 1. Nguồn danh sách các vật phẩm được kéo thả trực tiếp
        if (itemTargetList != null && itemTargetList.Count > 0)
        {
            for (int i = 0; i < itemTargetList.Count; i++)
            {
                if (itemTargetList[i] != null)
                {
                    candidates.Add(itemTargetList[i]);
                }
            }
        }

        // 2. Nguồn con của GameObject cha (nếu có)
        if (itemsContainerParent != null)
        {
            for (int i = 0; i < itemsContainerParent.childCount; i++)
            {
                Transform child = itemsContainerParent.GetChild(i);
                if (child != null && child != itemsContainerParent && child != resolvedTarget)
                {
                    candidates.Add(child);
                }
            }
        }

        // 3. Nguồn quét theo bán kính (nếu bật autoFindItemsInRadius)
        if (resolvedTarget != null && searchRadius > 0.01f && autoFindItemsInRadius)
        {
            Vector3 center = resolvedTarget.position + targetOffset;
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, searchRadius, searchLayerMask);
            if (hits != null)
            {
                for (int i = 0; i < hits.Length; i++)
                {
                    Collider2D hit = hits[i];
                    if (hit == null) continue;

                    Transform t = hit.transform;
                    if (t == resolvedTarget || t.IsChildOf(resolvedTarget)) continue;

                    if (!string.IsNullOrEmpty(searchTagFilter) && !hit.CompareTag(searchTagFilter) && !t.root.CompareTag(searchTagFilter))
                    {
                        continue;
                    }

                    candidates.Add(t);
                }
            }

            if (autoDetectGamePickups)
            {
                var crystals = UnityEngine.Object.FindObjectsByType<EnemySystem.CrystalPickupItem>(FindObjectsSortMode.None);
                if (crystals != null)
                {
                    for (int i = 0; i < crystals.Length; i++)
                    {
                        if (crystals[i] != null && Vector3.Distance(crystals[i].transform.position, center) <= searchRadius)
                        {
                            candidates.Add(crystals[i].transform);
                        }
                    }
                }

                var nidusDrops = UnityEngine.Object.FindObjectsByType<NidusDeathDropItem>(FindObjectsSortMode.None);
                if (nidusDrops != null)
                {
                    for (int i = 0; i < nidusDrops.Length; i++)
                    {
                        if (nidusDrops[i] != null && !nidusDrops[i].IsPickedUp && Vector3.Distance(nidusDrops[i].transform.position, center) <= searchRadius)
                        {
                            candidates.Add(nidusDrops[i].transform);
                        }
                    }
                }
            }
        }

        // 4. Nếu chưa có vật nào, thử lấy từ Track Binding (playerData)
        if (candidates.Count == 0)
        {
            Transform boundTransform = ResolveTransform(playerData);
            if (boundTransform != null && boundTransform != resolvedTarget)
            {
                candidates.Add(boundTransform);
            }
        }

        return new List<Transform>(candidates);
    }

    private void ResolveTargetPoint(object playerData)
    {
        if (resolvedTarget == null)
        {
            resolvedTarget = targetPointA;
        }

        // Nếu clip chưa gán targetPointA, lấy từ Track Binding nếu binding không nằm trong danh sách item
        if (resolvedTarget == null)
        {
            Transform boundTransform = ResolveTransform(playerData);
            if (boundTransform != null && (itemTargetList == null || !itemTargetList.Contains(boundTransform)))
            {
                resolvedTarget = boundTransform;
            }
        }
    }

    private static Transform ResolveTransform(object playerData)
    {
        if (playerData is Transform t) return t;
        if (playerData is GameObject go) return go.transform;
        if (playerData is Component comp) return comp.transform;
        return null;
    }

    private static void WarnOnce(ref bool warned, string message)
    {
        if (warned) return;
        warned = true;
        Debug.LogWarning(message);
    }
}
