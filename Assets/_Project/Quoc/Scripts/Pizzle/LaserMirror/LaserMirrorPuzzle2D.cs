using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class LaserMirrorPuzzle2D : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════
    //  CORE NODES — Crystal nguồn, đích, và danh sách tất cả crystal
    // ═══════════════════════════════════════════════════════════════

    [Header("█ Core Nodes ——————————————")]

    // Crystal nguồn phát tia laser (tự tìm nếu để trống — crystal có IsSource = true).
    [SerializeField] private LaserMirrorCrystal2D sourceCrystal;

    // Crystal đích cuối cùng tia cần chạm (tự tìm nếu để trống — crystal có IsGoal = true).
    [SerializeField] private LaserMirrorCrystal2D goalCrystal;

    // Tất cả crystal tham gia puzzle (tự thu thập từ children nếu để trống).
    // THỨ TỰ QUAN TRỌNG: Crystal[0]=source, Crystal[N-1]=goal, giữa = đường đi đúng.
    [SerializeField] private List<LaserMirrorCrystal2D> crystals = new List<LaserMirrorCrystal2D>();

    // Tất cả các Trụ Điều Khiển (Pillars) tham gia puzzle (tự tìm nếu để trống).
    [SerializeField] private List<LaserMirrorStonePedestal2D> pillars = new List<LaserMirrorStonePedestal2D>();

    // ═══════════════════════════════════════════════════════════════
    //  BEAM & VISUALS — Cấu hình tia laser và hiệu ứng visual
    // ═══════════════════════════════════════════════════════════════

    [Header("█ Beam & Visuals ——————————————")]


    [SerializeField] private LaserMirrorBeamVisual2D beamVisual;

    // Layer của địa hình/mặt đất để tia laser dừng lại.
    [SerializeField] private LayerMask groundLayer;

    // Khoảng cách tối đa tia laser có thể đi (world unit).
    // 35 = đủ cho hầu hết puzzle. Tăng nếu crystal cách xa nhau.
    [SerializeField, Min(1f)] private float maxDistance = 35f;

    // Số lần phản xạ tối đa giữa các tinh thể (= số crystal trung gian tối đa + 1).
    [SerializeField, Min(1)] private int maxBounces = 16;

    // Khoảng offset nhỏ tia bắt đầu cách crystal (tránh tia chạm chính crystal nguồn).
    [SerializeField, Min(0f)] private float beamStartOffset = 0f;

    // true = tia laser đang bật. Điều khiển bởi PressureSwitch hoặc code.
    [SerializeField] private bool isBeamActive = false;

    // ═══════════════════════════════════════════════════════════════
    //  PORTAL & CONVERGENCE — Cổng dịch chuyển và hiệu ứng hội tụ
    // ═══════════════════════════════════════════════════════════════

    [Header("█ Portal & Convergence ——————————————")]

    // Tham chiếu đến cổng portal (tự tìm từ children nếu để trống).
    [SerializeField] private LaserMirrorCavePortal2D cavePortal;

    // Tự động co giãn portal ôm khít tất cả crystal.
    [SerializeField] private bool autoFitPortalToCrystals = true;

    // Tự động tính góc xoay cho các crystal dựa trên thứ tự trong danh sách.
    [SerializeField] private bool autoCalibrateTargetAngles = true;

    // GameObject hiện ra khi puzzle được giải (vd: kho báu, NPC, cầu thang).
    [SerializeField] private GameObject solvedObject;

    // Thời gian (giây) hoạt ảnh reveal solvedObject (scale + fade in).
    [SerializeField, Min(0.01f)] private float solvedRevealDuration = 0.8f;

    // Scale bắt đầu của solvedObject khi reveal (0.6 = bắt đầu ở 60% kích thước).
    [SerializeField, Range(0.1f, 1f)] private float solvedRevealStartScale = 0.6f;

    // ═══════════════════════════════════════════════════════════════
    //  AUDIO — Âm thanh puzzle
    // ═══════════════════════════════════════════════════════════════

    [Header("█ Audio ——————————————")]

    // AudioSource phát âm thanh puzzle (tự tìm nếu để trống).
    [SerializeField] private AudioSource puzzleAudioSource;

    // Âm thanh khi giải xong puzzle (one-shot fanfare).
    [SerializeField] private AudioClip solvedSound;

    // Âm thanh khi mỗi crystal nhận tia (one-shot ping).
    [SerializeField] private AudioClip nodeLitSound;

    // ═══════════════════════════════════════════════════════════════
    //  EVENTS — Sự kiện cho designer hook logic bên ngoài
    // ═══════════════════════════════════════════════════════════════

    [Header("█ Events ——————————————")]

    // Gọi khi puzzle được giải hoàn tất (tất cả crystal đúng thứ tự + tia chạm goal).
    [SerializeField] private UnityEvent onSolved;

    // ═══════════════════════════════════════════════════════════════

    private readonly List<Vector3> beamPoints = new List<Vector3>(20);
    private readonly List<Vector3> hitPoints = new List<Vector3>(16);
    private readonly List<bool> reflectionHitPoints = new List<bool>(16);
    private readonly List<LaserMirrorCrystal2D> tracedCrystals = new List<LaserMirrorCrystal2D>(16);
    private bool solved;
    private Coroutine solvedRevealCoroutine;
    private Coroutine convergenceCoroutine;
    private LaserMirrorCavePortal2D.PortalEnergyPalette lastPalette;
    private bool hasCachedPalette;

    public bool IsSolved => solved;
    public bool IsBeamActive => isBeamActive;

    public void SetBeamActive(bool active)
    {
        isBeamActive = active;
        RebuildBeam();
    }

    public void Configure(
        LaserMirrorCrystal2D newSourceCrystal,
        LaserMirrorCrystal2D newGoalCrystal,
        LaserMirrorCavePortal2D newPortal,
        GameObject newSolvedObject,
        IList<LaserMirrorCrystal2D> newCrystals)
    {
        sourceCrystal = newSourceCrystal;
        goalCrystal = newGoalCrystal;
        cavePortal = newPortal;
        solvedObject = newSolvedObject;
        crystals.Clear();

        if (newCrystals != null)
        {
            for (int i = 0; i < newCrystals.Count; i++)
            {
                if (newCrystals[i] != null && !crystals.Contains(newCrystals[i]))
                {
                    crystals.Add(newCrystals[i]);
                }
            }
        }

        ConfigureBeamRenderer();
    }

    private void Awake()
    {
        if (groundLayer.value == 0)
        {
            groundLayer = LayerMask.GetMask("Ground");
            if (groundLayer.value == 0)
            {
                groundLayer = LayerMask.GetMask("Default");
            }
        }

        AutoCollectCrystals();
        ConfigureBeamRenderer();
        if (puzzleAudioSource == null)
        {
            puzzleAudioSource = GetComponent<AudioSource>();
        }
        SyncPaletteWithPortal(true);
        CheckAutoFitPortal();
    }

    private void Start()
    {
        SyncPaletteWithPortal(true);
        CheckAutoFitPortal();
        RebuildBeam();
    }

    private void Update()
    {
        SyncPaletteWithPortal(false);
        CheckTransformChanges();
    }

    private void CheckTransformChanges()
    {
        bool changed = false;

        if (crystals != null)
        {
            for (int i = 0; i < crystals.Count; i++)
            {
                if (crystals[i] != null && crystals[i].transform.hasChanged)
                {
                    crystals[i].transform.hasChanged = false;
                    changed = true;
                }
            }
        }

        if (transform.hasChanged)
        {
            transform.hasChanged = false;
            changed = true;
        }

        if (changed)
        {
            AutoCalibrateAngles();
            CheckAutoFitPortal();
            RebuildBeam();
        }
    }

    private void OnValidate()
    {
        maxDistance = Mathf.Max(1f, maxDistance);
        maxBounces = Mathf.Max(1, maxBounces);
        beamStartOffset = Mathf.Max(0f, beamStartOffset);
        CheckAutoFitPortal();
    }

    private void CheckAutoFitPortal()
    {
        if (autoFitPortalToCrystals && cavePortal != null && crystals != null && crystals.Count > 0)
        {
            cavePortal.AutoFitToCrystals(crystals);
        }
    }

    [ContextMenu("Rebuild Beam")]
    public void RebuildBeam()
    {
        AutoCollectCrystals();
        ConfigureBeamRenderer();
        CheckAutoFitPortal();
        AutoCalibrateAngles();

        for (int i = 0; i < crystals.Count; i++)
        {
            if (crystals[i] != null)
            {
                crystals[i].SetLit(false);
            }
        }

        beamPoints.Clear();
        hitPoints.Clear();
        reflectionHitPoints.Clear();
        tracedCrystals.Clear();

        if (!isBeamActive)
        {
            SetBeamPoints();
            SetSolved(false);
            return;
        }

        if (sourceCrystal == null) sourceCrystal = FindSource();
        if (goalCrystal == null) goalCrystal = FindGoal();

        if (sourceCrystal == null)
        {
            SetBeamPoints();
            return;
        }

        sourceCrystal.SetLit(true);
        tracedCrystals.Add(sourceCrystal);

        Vector2 origin = sourceCrystal.Position;
        Vector2 direction = sourceCrystal.GetSourceDirection();
        beamPoints.Add(origin);

        bool reachedGoal = false;
        bool followedRequiredOrder = crystals.Count > 0 && crystals[0] == sourceCrystal;
        int nextRequiredCrystalIndex = 1;
        LaserMirrorCrystal2D previousCrystal = sourceCrystal;

        for (int bounce = 0; bounce < maxBounces; bounce++)
        {
            Vector2 currentRayOrigin = origin;
            Vector2 currentRayDir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            LaserMirrorCrystal2D hit = null;
            Vector2 incomingDirToCrystal = currentRayDir;

            // Bỏ reflection mặt đất. Ignore triggers để không bị chặn bởi CavePortal_Center
            ContactFilter2D filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = groundLayer,
                useTriggers = false
            };
            RaycastHit2D[] hits = new RaycastHit2D[1];
            int hitCount = Physics2D.Raycast(currentRayOrigin, currentRayDir, filter, hits, maxDistance);
            RaycastHit2D groundHit = hitCount > 0 ? hits[0] : default;

            float maxSegmentDist = (groundHit.collider != null && groundHit.fraction > 0f) ? groundHit.distance : maxDistance;

            LaserMirrorCrystal2D foundCrystal = FindCrystalAlongRay(currentRayOrigin, currentRayDir, previousCrystal, maxSegmentDist, out Vector2 crystalHitPos, out _);

            if (foundCrystal != null)
            {
                beamPoints.Add(crystalHitPos);
                hit = foundCrystal;
                incomingDirToCrystal = currentRayDir;
            }
            else
            {
                if (groundHit.collider != null && groundHit.fraction > 0f)
                {
                    Vector2 hitPointGround = groundHit.point;
                    beamPoints.Add(hitPointGround);
                    hitPoints.Add(hitPointGround);
                    reflectionHitPoints.Add(false); // Dừng lại ở mặt đất, không phản chiếu
                }
                else
                {
                    beamPoints.Add(currentRayOrigin + currentRayDir * maxDistance);
                }
                break;
            }

            if (hit == null)
            {
                break;
            }

            hit.SetLit(true);
            hitPoints.Add(hit.Position);

            bool isExpectedCrystal = followedRequiredOrder
                && nextRequiredCrystalIndex < crystals.Count
                && hit == crystals[nextRequiredCrystalIndex];

            if (isExpectedCrystal)
            {
                tracedCrystals.Add(hit);
                nextRequiredCrystalIndex++;
            }
            else
            {
                followedRequiredOrder = false;
            }

            if (hit == goalCrystal || hit.IsGoal)
            {
                reflectionHitPoints.Add(false);
                reachedGoal = true;
                break;
            }

            if (!hit.TryReflect(incomingDirToCrystal, out Vector2 reflectedDirection))
            {
                reflectionHitPoints.Add(false);
                break;
            }

            reflectionHitPoints.Add(true);
            origin = hit.Position;
            direction = reflectedDirection;
            previousCrystal = hit;
        }

        SetBeamPoints();

        bool visitedEveryRequiredCrystal = followedRequiredOrder
            && reachedGoal
            && nextRequiredCrystalIndex == crystals.Count
            && tracedCrystals.Count == crystals.Count
            && tracedCrystals[tracedCrystals.Count - 1] == goalCrystal;
        SetSolved(visitedEveryRequiredCrystal);
    }

    private LaserMirrorCrystal2D FindCrystalAlongRay(
        Vector2 rayOrigin,
        Vector2 rayDir,
        LaserMirrorCrystal2D ignoredCrystal,
        float maxRange,
        out Vector2 hitPoint,
        out float hitDistance)
    {
        hitPoint = rayOrigin + rayDir * maxRange;
        hitDistance = maxRange;
        LaserMirrorCrystal2D best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < crystals.Count; i++)
        {
            LaserMirrorCrystal2D crystal = crystals[i];
            if (crystal == null || crystal == ignoredCrystal)
            {
                continue;
            }

            Vector2 toCrystal = crystal.Position - rayOrigin;
            float alongRay = Vector2.Dot(toCrystal, rayDir);
            if (alongRay <= 0.05f || alongRay >= maxRange || alongRay >= bestDistance)
            {
                continue;
            }

            Vector2 closestPoint = rayOrigin + rayDir * alongRay;
            float missDistance = Vector2.Distance(closestPoint, crystal.Position);
            if (missDistance > crystal.HitRadius)
            {
                continue;
            }

            best = crystal;
            bestDistance = alongRay;
            hitDistance = alongRay;
            hitPoint = crystal.Position;
        }

        return best;
    }

    private void SetBeamPoints()
    {
        if (!isBeamActive)
        {
            if (beamVisual != null) beamVisual.SetVisible(false);
            return;
        }

        if (beamVisual != null)
        {
            beamVisual.SetVisible(true);
            beamVisual.SetBeamPoints(beamPoints);
            beamVisual.SetHitEffects(hitPoints, reflectionHitPoints);
        }
    }

    private void SetSolved(bool value)
    {
        if (solved == value)
        {
            return;
        }

        solved = value;

        if (cavePortal == null)
        {
            cavePortal = GetComponentInChildren<LaserMirrorCavePortal2D>(true);
        }

        if (value)
        {
            if (puzzleAudioSource != null && solvedSound != null)
            {
                puzzleAudioSource.PlayOneShot(solvedSound);
            }

            // Overcharge tất cả các viên pha lê trong mạng lưới vòm
            for (int i = 0; i < crystals.Count; i++)
            {
                if (crystals[i] != null)
                {
                    crystals[i].SetOvercharged(true);
                }
            }

            if (cavePortal != null)
            {
                cavePortal.OpenPortal();
            }

            if (solvedObject != null)
            {
                if (solvedRevealCoroutine != null) StopCoroutine(solvedRevealCoroutine);
                solvedRevealCoroutine = StartCoroutine(RevealSolvedObject());
            }

            if (beamVisual != null)
            {
                beamVisual.SetEndpointsActive(false);
            }

            StartConvergenceEffect();
            onSolved?.Invoke();
        }
        else
        {
            if (beamVisual != null)
            {
                beamVisual.SetEndpointsActive(true);
            }

            // Tắt Overcharge khi giải đố bị ngắt
            for (int i = 0; i < crystals.Count; i++)
            {
                if (crystals[i] != null)
                {
                    crystals[i].SetOvercharged(false);
                }
            }

            if (cavePortal != null)
            {
                cavePortal.ClosePortal();
            }

            if (solvedObject != null)
            {
                solvedObject.SetActive(false);
            }

            StopConvergenceEffect();
        }
    }

    private void StartConvergenceEffect()
    {
        if (cavePortal == null || crystals.Count == 0) return;
        if (convergenceCoroutine != null) StopCoroutine(convergenceCoroutine);
        convergenceCoroutine = StartCoroutine(ConvergenceRoutine());
    }

    private void StopConvergenceEffect()
    {
        if (convergenceCoroutine != null)
        {
            StopCoroutine(convergenceCoroutine);
            convergenceCoroutine = null;
        }

    }

    private IEnumerator ConvergenceRoutine()
    {
        yield return new WaitForSeconds(1.2f);

        StopConvergenceEffect();
    }

    private IEnumerator RevealSolvedObject()
    {
        Transform solvedTransform = solvedObject.transform;
        Vector3 targetScale = solvedTransform.localScale;
        SpriteRenderer[] renderers = solvedObject.GetComponentsInChildren<SpriteRenderer>(true);
        Color[] targetColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            targetColors[i] = renderers[i].color;
            Color transparent = targetColors[i];
            transparent.a = 0f;
            renderers[i].color = transparent;
        }

        solvedTransform.localScale = targetScale * solvedRevealStartScale;
        solvedObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < solvedRevealDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / solvedRevealDuration);
            float smoothProgress = progress * progress * (3f - 2f * progress);
            solvedTransform.localScale = Vector3.LerpUnclamped(targetScale * solvedRevealStartScale, targetScale, smoothProgress);

            for (int i = 0; i < renderers.Length; i++)
            {
                Color color = targetColors[i];
                color.a *= smoothProgress;
                renderers[i].color = color;
            }

            yield return null;
        }

        solvedTransform.localScale = targetScale;
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].color = targetColors[i];
        }

        solvedRevealCoroutine = null;
    }

    private void ConfigureBeamRenderer()
    {
        if (beamVisual == null)
        {
            beamVisual = GetComponent<LaserMirrorBeamVisual2D>();
            if (beamVisual == null)
            {
                beamVisual = gameObject.AddComponent<LaserMirrorBeamVisual2D>();
            }
        }
    }

    private void AutoCollectCrystals()
    {
        crystals.RemoveAll(item => item == null);

        LaserMirrorCrystal2D[] found = GetComponentsInChildren<LaserMirrorCrystal2D>(true);
        for (int i = 0; i < found.Length; i++)
        {
            if (!crystals.Contains(found[i]))
            {
                crystals.Add(found[i]);
            }
        }

        pillars.RemoveAll(item => item == null);
        LaserMirrorStonePedestal2D[] foundPillars = GetComponentsInChildren<LaserMirrorStonePedestal2D>(true);
        for (int i = 0; i < foundPillars.Length; i++)
        {
            if (!pillars.Contains(foundPillars[i]))
            {
                pillars.Add(foundPillars[i]);
            }
        }
    }

    private LaserMirrorCrystal2D FindSource()
    {
        for (int i = 0; i < crystals.Count; i++)
        {
            if (crystals[i] != null && crystals[i].IsSource)
            {
                return crystals[i];
            }
        }
        return null;
    }

    private LaserMirrorCrystal2D FindGoal()
    {
        for (int i = 0; i < crystals.Count; i++)
        {
            if (crystals[i] != null && crystals[i].IsGoal)
            {
                return crystals[i];
            }
        }
        return null;
    }

    private void SyncPaletteWithPortal(bool force)
    {
        if (cavePortal == null)
        {
            cavePortal = GetComponentInChildren<LaserMirrorCavePortal2D>(true);
        }

        if (cavePortal == null)
        {
            return;
        }

        if (cavePortal.TryGetEnergyPalette(out LaserMirrorCavePortal2D.PortalEnergyPalette palette))
        {
            if (force || !hasCachedPalette || !lastPalette.EqualsPalette(palette))
            {
                lastPalette = palette;
                hasCachedPalette = true;
                ApplyEnergyPalette(palette);
            }
        }
    }

    public void AutoCalibrateAngles()
    {
        if (!autoCalibrateTargetAngles || crystals == null || crystals.Count < 2)
        {
            return;
        }

        for (int i = 0; i < crystals.Count; i++)
        {
            LaserMirrorCrystal2D current = crystals[i];
            if (current == null) continue;

            if (current.AimTargets != null && current.AimTargets.Length > 0)
            {
                continue;
            }

            if (current.IsSource && i + 1 < crystals.Count && crystals[i + 1] != null)
            {
                Vector2 dir = crystals[i + 1].Position - current.Position;
                if (dir.sqrMagnitude > 0.001f)
                {
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    float[] angles = current.CustomRotationAngles;
                    if (angles != null && angles.Length >= 4)
                    {
                        // Giữ nguyên chỉ số hiện tại, cập nhật góc đúng vào slot 0 hoặc slot gần nhất
                        angles[0] = angle;
                        angles[1] = Mathf.Repeat(angle + 90f, 360f);
                        angles[2] = Mathf.Repeat(angle + 180f, 360f);
                        angles[3] = Mathf.Repeat(angle + 270f, 360f);
                        current.SetCustomAngles(angles, current.CurrentAngleIndex);
                    }
                    else if (angles != null && angles.Length > 0)
                    {
                        angles[0] = angle;
                        current.SetCustomAngles(angles, current.CurrentAngleIndex);
                    }
                    else
                    {
                        current.SetCustomAngles(new[] { angle, Mathf.Repeat(angle + 90f, 360f), Mathf.Repeat(angle + 180f, 360f), Mathf.Repeat(angle + 270f, 360f) }, 0);
                    }
                }
            }
            else if (current.CanReflect && i + 1 < crystals.Count && crystals[i + 1] != null)
            {
                Vector2 dir = crystals[i + 1].Position - current.Position;
                if (dir.sqrMagnitude > 0.001f)
                {
                    float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    float[] angles = current.CustomRotationAngles;
                    if (angles != null && angles.Length >= 4)
                    {
                        // Tìm vị trí góc gần đúng nhất trong 4 góc để thay thế bằng góc chuẩn xác
                        int solutionIdx = 0;
                        float bestDiff = float.MaxValue;
                        for (int k = 0; k < angles.Length; k++)
                        {
                            float diff = Mathf.Abs(Mathf.DeltaAngle(angles[k], targetAngle));
                            if (diff < bestDiff)
                            {
                                bestDiff = diff;
                                solutionIdx = k;
                            }
                        }

                        // Cập nhật cả 4 hướng xoay vuông góc quanh góc đúng mục tiêu
                        for (int k = 0; k < 4; k++)
                        {
                            int offsetSteps = (k - solutionIdx + 4) % 4;
                            angles[k] = Mathf.Repeat(targetAngle + offsetSteps * 90f, 360f);
                        }

                        current.SetCustomAngles(angles, current.CurrentAngleIndex);
                    }
                    else if (angles != null && angles.Length > 0)
                    {
                        angles[0] = targetAngle;
                        current.SetCustomAngles(angles, current.CurrentAngleIndex);
                    }
                    else
                    {
                        current.SetCustomAngles(new[]
                        {
                            targetAngle,
                            Mathf.Repeat(targetAngle + 90f, 360f),
                            Mathf.Repeat(targetAngle + 180f, 360f),
                            Mathf.Repeat(targetAngle + 270f, 360f)
                        }, 0);
                    }
                }
            }
        }
    }

    private void ApplyEnergyPalette(LaserMirrorCavePortal2D.PortalEnergyPalette palette)
    {
        if (beamVisual != null)
        {
            beamVisual.ApplyEnergyPalette(palette);
        }

        for (int i = 0; i < crystals.Count; i++)
        {
            if (crystals[i] != null)
            {
                crystals[i].ApplyEnergyPalette(palette);
            }
        }

        if (pillars != null)
        {
            for (int i = 0; i < pillars.Count; i++)
            {
                if (pillars[i] != null)
                {
                    pillars[i].ApplyEnergyPalette(palette);
                }
            }
        }
    }
}
