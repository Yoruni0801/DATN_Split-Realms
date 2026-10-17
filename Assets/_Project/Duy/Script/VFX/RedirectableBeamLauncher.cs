using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class RedirectableBeamLauncher : MonoBehaviour
{
    [Header("VFX prefab")]
    [SerializeField] private GameObject beamLineRendererPrefab;
    [SerializeField] private GameObject beamStartPrefab;
    [SerializeField] private GameObject beamEndPrefab;

    [Header("Adjustable Variables")]
    [SerializeField] private float beamEndOffset = 0.1f;
    [SerializeField] private float textureScrollSpeed = 8f;
    [SerializeField] private float textureLengthScale = 3f;

    [Header("Beam Parameters")]
    [SerializeField] private LayerMask hitLayers = ~0;
    [SerializeField] private int maxBounceCount = 10;
    [SerializeField] private float maxRange = 100f;
    [SerializeField] private float bounceOffset = 0.05f;

    [Header("Physics Mode")]
    [SerializeField] private bool use2DPhysics = true;

    [Header("Initial State")]
    [SerializeField] private bool playOnStart = false;
    private bool isEmitting = false;
    [SerializeField] private Transform firePoint;

    [Header("Warm-up (Tinh linh vào vùng)")]
    [SerializeField] private float warmupDelay = 0.5f;
    [SerializeField] private GameObject warmupVfxPrefab;

    [Header("Trigger Zone - Tinh linh kích hoạt")]
    [SerializeField] private Collider2D triggerZone;
    [SerializeField] private bool requireSpecificElement = false;
    private FairyElement requiredFairyElement = FairyElement.Fire;
    [SerializeField] private bool oneShot = false;

    [Header("Input Interaction")]
    [SerializeField] private bool requireInteractKeyToFire = true;
    [SerializeField] private Key interactKey = Key.Space;

    [Header("Targeting")]
    [SerializeField] private Transform targetDeflector;
    [SerializeField] private bool autoFindDeflector = true;
    [SerializeField] private bool useFixedDirection = false;
    [SerializeField] private Vector3 fixedDirection = Vector3.down;

    private static readonly int IdMainTexST = Shader.PropertyToID("_MainTex_ST");
    private static readonly int IdBaseMapST = Shader.PropertyToID("_BaseMap_ST");
    private static readonly Vector3 BoundsExpandSize = new(0.6f, 0.6f, 2f);

    private bool hasBeenTriggered;
    private CancellationTokenSource warmupCts;
    private CancellationTokenSource destroyCts;
    private FairyPlayerCompanionController lockingFairy;

    private Collider2D[] selfColliders;
    private ContactFilter2D contactFilter2D;
    private GameObject pooledWarmupVfx;

    private static readonly RaycastHit2D[] RaycastHitBuffer = new RaycastHit2D[16];
    private static readonly RaycastHit[] RaycastHit3DBuffer = new RaycastHit[16];
    private static readonly RaycastHitComparer HitComparer = new();
    private static readonly Raycast3DHitComparer Hit3DComparer = new();

    private readonly List<BeamInstance> activeBeamInstances = new();
    private readonly List<BeamPathData> calculatedPaths = new();

    private class BeamInstance
    {
        public GameObject BeamRoot;
        public GameObject BeamStart;
        public GameObject BeamEnd;
        public LineRenderer Line;
        public MaterialPropertyBlock PropertyBlock = new();
        public float TextureOffset;
    }

    private struct BeamPathData
    {
        public Vector3 StartPoint;
        public Vector3 EndPoint;
        public bool IsReflected;
        public bool HideStartVfx;
    }

    private void Awake()
    {
        destroyCts = new CancellationTokenSource();
        if (firePoint == null) firePoint = transform;
        selfColliders = GetComponents<Collider2D>();

        InitPhysicsFilter();
    }

    private void InitPhysicsFilter()
    {
        contactFilter2D = new ContactFilter2D
        {
            useTriggers = true
        };
        int waterLayer = LayerMask.NameToLayer("Water");
        LayerMask effectiveMask = hitLayers | (1 << 0);
        if (waterLayer >= 0) effectiveMask |= (1 << waterLayer);
        contactFilter2D.SetLayerMask(effectiveMask);
    }

    private void Start()
    {
        if (autoFindDeflector && targetDeflector == null)
        {
            var deflector = FindFirstObjectByType<MirrorBeamDeflector>();
            if (deflector != null)
            {
                targetDeflector = deflector.transform;
            }
        }

        if (playOnStart)
        {
            FireBeam(true);
        }
    }

    private void OnDisable()
    {
        CancelWarmup();
        ClearBeamInstances();
    }

    private void OnDestroy()
    {
        CancelWarmup();
        destroyCts?.Cancel();
        destroyCts?.Dispose();
        destroyCts = null;

        if (pooledWarmupVfx != null)
        {
            Destroy(pooledWarmupVfx);
            pooledWarmupVfx = null;
        }

        ClearBeamInstances();
    }

    private void Update()
    {
        UpdateTriggerDetection();

        if (isEmitting && lockingFairy != null)
        {
            if (!lockingFairy.gameObject.activeInHierarchy || Vector2.Distance(firePoint.position, lockingFairy.transform.position) > 4f)
            {
                FireBeam(false);
            }
        }

        if (!isEmitting || beamLineRendererPrefab == null)
        {
            ClearBeamInstances();
            return;
        }

        CalculatePaths();
        RenderBeams();
    }

    public void FireBeam(bool emit = true)
    {
        isEmitting = emit;
        if (!emit)
        {
            ClearBeamInstances();
            if (lockingFairy != null)
            {
                lockingFairy.SetCastingLocked(false);
                lockingFairy = null;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggerZone != null && other != triggerZone) return;
        TryTriggerFromCollider(other);
    }

    private void UpdateTriggerDetection()
    {
        if (hasBeenTriggered && oneShot) return;
        if (triggerZone == null) return;

        Bounds triggerBounds = triggerZone.bounds;
        Bounds expandedBounds = new(triggerBounds.center, triggerBounds.size + BoundsExpandSize);

        bool keyPressed = !requireInteractKeyToFire || (Keyboard.current != null && Keyboard.current[interactKey].wasPressedThisFrame);

        var fairies = FairyPlayerCompanionController.AllActiveFairies;
        int count = fairies.Count;
        for (int i = 0; i < count; i++)
        {
            var fairy = fairies[i];
            if (fairy == null) continue;

            Vector3 fairyPos = fairy.transform.position;
            fairyPos.z = triggerBounds.center.z;

            bool inZone = expandedBounds.Contains(fairyPos);

            if (!inZone && fairy.TryGetComponent(out Collider2D fairyCol) && fairyCol.enabled)
            {
                inZone = triggerBounds.Intersects(fairyCol.bounds);
            }

            if (inZone)
            {
                if (requireSpecificElement && fairy.CurrentElement != requiredFairyElement)
                {
                    continue;
                }

                if (requireInteractKeyToFire && !keyPressed)
                {
                    continue;
                }

                if (TryTriggerFromFairy(fairy)) break;
            }
        }
    }

    private bool TryTriggerFromCollider(Collider2D other)
    {
        if (other == null) return false;
        if (triggerZone != null && other == triggerZone) return false;
        if (hasBeenTriggered && oneShot) return false;

        if (!other.TryGetComponent(out FairyPlayerCompanionController companion))
        {
            companion = other.GetComponentInParent<FairyPlayerCompanionController>();
        }

        if (companion == null) return false;

        bool keyPressed = !requireInteractKeyToFire || (Keyboard.current != null && Keyboard.current[interactKey].wasPressedThisFrame);
        if (requireInteractKeyToFire && !keyPressed) return false;

        return TryTriggerFromFairy(companion);
    }

    private bool TryTriggerFromFairy(FairyPlayerCompanionController fairy)
    {
        if (hasBeenTriggered && oneShot) return false;
        if (requireSpecificElement && fairy.CurrentElement != requiredFairyElement) return false;

        lockingFairy = fairy;
        fairy.SetCastingLocked(true);

        StartWarmupAndFire();
        return true;
    }

    private void StartWarmupAndFire()
    {
        hasBeenTriggered = true;
        CancelWarmup();
        warmupCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCts.Token);
        _ = WarmupAndFireAsync(warmupCts.Token);
    }

    private void CancelWarmup()
    {
        if (warmupCts != null)
        {
            warmupCts.Cancel();
            warmupCts.Dispose();
            warmupCts = null;
        }

        if (pooledWarmupVfx != null)
        {
            pooledWarmupVfx.SetActive(false);
        }
    }

    private async Task WarmupAndFireAsync(CancellationToken ct)
    {
        try
        {
            if (warmupVfxPrefab != null)
            {
                if (pooledWarmupVfx == null)
                {
                    pooledWarmupVfx = Instantiate(warmupVfxPrefab, firePoint.position, firePoint.rotation);
                }
                else
                {
                    pooledWarmupVfx.transform.SetPositionAndRotation(firePoint.position, firePoint.rotation);
                    pooledWarmupVfx.SetActive(true);
                }
            }

            float elapsed = 0f;
            while (elapsed < warmupDelay)
            {
                if (this == null || ct.IsCancellationRequested) return;
                elapsed += Time.deltaTime;
                await Task.Yield();
            }

            if (this == null || ct.IsCancellationRequested) return;

            if (pooledWarmupVfx != null)
            {
                pooledWarmupVfx.SetActive(false);
            }

            FireBeam(true);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (oneShot) return;
        if (triggerZone != null && other != triggerZone) return;

        if (IsFairyTrigger(other))
        {
            CancelWarmup();
            FireBeam(false);
        }
    }

    private bool IsFairyTrigger(Collider2D col)
    {
        if (!col.TryGetComponent(out FairyPlayerCompanionController companion))
        {
            companion = col.GetComponentInParent<FairyPlayerCompanionController>();
        }

        if (companion == null) return false;

        if (requireSpecificElement)
        {
            return companion.CurrentElement == requiredFairyElement;
        }

        return true;
    }

    public void StopBeam()
    {
        isEmitting = false;
        ClearBeamInstances();
        if (lockingFairy != null)
        {
            lockingFairy.SetCastingLocked(false);
            lockingFairy = null;
        }
    }

    private void CalculatePaths()
    {
        calculatedPaths.Clear();

        Vector3 currentOrigin = firePoint.position;
        if (use2DPhysics) currentOrigin.z = 0f;
        Vector3 currentDir;

        if (useFixedDirection)
        {
            currentDir = fixedDirection;
        }
        else if (targetDeflector != null)
        {
            Vector3 diff = targetDeflector.position - currentOrigin;
            if (use2DPhysics) diff.z = 0f;
            currentDir = diff.sqrMagnitude > 0.0001f ? diff.normalized : (use2DPhysics ? firePoint.right : firePoint.forward);
        }
        else
        {
            currentDir = use2DPhysics ? firePoint.right : firePoint.forward;
        }
        if (use2DPhysics) currentDir.z = 0f;
        currentDir.Normalize();

        float remainingDistance = maxRange;
        Collider2D lastHitCollider2D = null;
        Collider lastHitCollider3D = null;
        bool nextHideStartVfx = false;

        for (int i = 0; i < maxBounceCount; i++)
        {
            if (remainingDistance <= 0.01f) break;

            bool hasHit = false;
            Vector3 hitPoint = Vector3.zero;
            Vector3 hitNormal = Vector3.up;
            Collider2D hitCollider2D = null;
            Collider hitCollider3D = null;

            if (use2DPhysics)
            {
                Vector3 currentRayOrigin = currentOrigin;
                float currentRayRemaining = remainingDistance;

                while (currentRayRemaining > 0.01f)
                {
                    int hitCount = Physics2D.Raycast(currentRayOrigin, currentDir, contactFilter2D, RaycastHitBuffer, currentRayRemaining);
                    if (hitCount == 0) break;

                    if (hitCount > 1)
                    {
                        Array.Sort(RaycastHitBuffer, 0, hitCount, HitComparer);
                    }

                    bool foundValidHit = false;
                    for (int hIdx = 0; hIdx < hitCount; hIdx++)
                    {
                        var h = RaycastHitBuffer[hIdx];
                        if (h.collider == null || h.collider == lastHitCollider2D || h.fraction <= 0.00001f) continue;

                        bool isSelf = false;
                        for (int s = 0; s < selfColliders.Length; s++)
                        {
                            if (selfColliders[s] == h.collider) { isSelf = true; break; }
                        }
                        if (isSelf) continue;

                        if (!h.collider.TryGetComponent(out Rota.VFX.BurnDissolveController burnTarget))
                        {
                            burnTarget = h.collider.GetComponentInParent<Rota.VFX.BurnDissolveController>();
                        }

                        if (burnTarget != null)
                        {
                            burnTarget.ProcessLaserRay(currentRayOrigin, currentDir, currentRayRemaining, out Vector3 beamEnd, out bool isPenetrated, out bool hasWoodHit);

                            if (!hasWoodHit)
                            {
                                continue;
                            }

                            if (isPenetrated)
                            {
                                calculatedPaths.Add(new BeamPathData
                                {
                                    StartPoint = currentOrigin,
                                    EndPoint = beamEnd,
                                    IsReflected = false,
                                    HideStartVfx = nextHideStartVfx
                                });

                                currentOrigin = beamEnd + (currentDir.normalized * 0.05f);
                                if (use2DPhysics) currentOrigin.z = 0f;
                                remainingDistance -= Vector3.Distance(currentRayOrigin, beamEnd);
                                lastHitCollider2D = h.collider;
                                nextHideStartVfx = true;
                                foundValidHit = true;
                                break;
                            }
                            else
                            {
                                calculatedPaths.Add(new BeamPathData
                                {
                                    StartPoint = currentOrigin,
                                    EndPoint = beamEnd,
                                    IsReflected = false,
                                    HideStartVfx = nextHideStartVfx
                                });
                                hasHit = true;
                                hitPoint = beamEnd;
                                foundValidHit = true;
                                remainingDistance = 0f;
                                break;
                            }
                        }

                        hasHit = true;
                        hitPoint = h.point;
                        hitNormal = h.normal;
                        hitCollider2D = h.collider;
                        remainingDistance -= h.distance;
                        foundValidHit = true;
                        break;
                    }

                    if (foundValidHit)
                    {
                        break;
                    }
                    else
                    {
                        float advanceDist = RaycastHitBuffer[hitCount - 1].distance + 0.1f;
                        if (advanceDist >= currentRayRemaining) break;
                        currentRayOrigin += currentDir * advanceDist;
                        currentRayRemaining -= advanceDist;
                    }
                }

                if (remainingDistance <= 0f) break;
                if (!hasHit)
                {
                    continue;
                }
            }
            else
            {
                int hitCount = Physics.RaycastNonAlloc(currentOrigin, currentDir, RaycastHit3DBuffer, remainingDistance, hitLayers);
                if (hitCount > 0)
                {
                    if (hitCount > 1)
                    {
                        Array.Sort(RaycastHit3DBuffer, 0, hitCount, Hit3DComparer);
                    }

                    for (int hIdx = 0; hIdx < hitCount; hIdx++)
                    {
                        var h = RaycastHit3DBuffer[hIdx];
                        if (h.collider != null && h.collider != lastHitCollider3D)
                        {
                            hasHit = true;
                            hitPoint = h.point;
                            hitNormal = h.normal;
                            hitCollider3D = h.collider;
                            remainingDistance -= h.distance;
                            break;
                        }
                    }
                }
            }

            Vector3 endPos;
            bool willReflect = false;
            bool isWater = false;
            IBeamDeflector deflector = null;

            if (hasHit)
            {
                if (hitCollider2D != null)
                {
                    if (!hitCollider2D.TryGetComponent(out deflector))
                        deflector = hitCollider2D.GetComponentInParent<IBeamDeflector>();
                }
                else if (hitCollider3D != null)
                {
                    if (!hitCollider3D.TryGetComponent(out deflector))
                        deflector = hitCollider3D.GetComponentInParent<IBeamDeflector>();
                }

                isWater = deflector is WaterRefractionDeflector;
                willReflect = deflector != null;

                if (isWater)
                {
                    endPos = hitPoint;
                }
                else
                {
                    endPos = hitPoint - (currentDir.normalized * beamEndOffset);
                }

                IBeamReceiver receiver = null;
                if (hitCollider2D != null)
                {
                    if (!hitCollider2D.TryGetComponent(out receiver))
                        receiver = hitCollider2D.GetComponentInParent<IBeamReceiver>();
                }
                else if (hitCollider3D != null)
                {
                    if (!hitCollider3D.TryGetComponent(out receiver))
                        receiver = hitCollider3D.GetComponentInParent<IBeamReceiver>();
                }
                receiver?.ReceiveBeam(hitPoint);
            }
            else
            {
                endPos = currentOrigin + (currentDir.normalized * remainingDistance);
            }

            calculatedPaths.Add(new BeamPathData
            {
                StartPoint = currentOrigin,
                EndPoint = endPos,
                IsReflected = willReflect,
                HideStartVfx = nextHideStartVfx
            });

            nextHideStartVfx = false;

            if (!hasHit) break;

            if (deflector != null)
            {
                Vector3 deflectedDir = deflector.GetDeflectDirection(currentDir, hitPoint, hitNormal);
                if (use2DPhysics) deflectedDir.z = 0f;
                if (deflectedDir.sqrMagnitude > 0.0001f)
                {
                    currentDir = deflectedDir.normalized;
                    lastHitCollider2D = hitCollider2D;
                    lastHitCollider3D = hitCollider3D;

                    if (isWater) nextHideStartVfx = true;

                    float offset = isWater ? 0f : Mathf.Max(bounceOffset, 0.05f);
                    currentOrigin = hitPoint + (currentDir * offset);
                    if (use2DPhysics) currentOrigin.z = 0f;
                    continue;
                }
            }

            break;
        }
    }

    private void RenderBeams()
    {
        int requiredCount = calculatedPaths.Count;

        while (activeBeamInstances.Count < requiredCount)
        {
            activeBeamInstances.Add(CreateNewBeamInstance());
        }

        for (int i = 0; i < activeBeamInstances.Count; i++)
        {
            BeamInstance inst = activeBeamInstances[i];
            if (inst == null) continue;

            if (i < requiredCount)
            {
                SetInstanceActive(inst, true);
                bool showStart = (i == 0) || !calculatedPaths[i].HideStartVfx;
                UpdateSingleBeam(inst, calculatedPaths[i].StartPoint, calculatedPaths[i].EndPoint,
                    showStart, calculatedPaths[i].IsReflected);
            }
            else
            {
                SetInstanceActive(inst, false);
            }
        }
    }

    private void UpdateSingleBeam(BeamInstance inst, Vector3 start, Vector3 end, bool showStartVfx = true, bool isBouncedEnd = false)
    {
        if (inst.Line == null) return;

        inst.Line.positionCount = 2;
        inst.Line.SetPosition(0, start);
        inst.Line.SetPosition(1, end);

        if (inst.BeamStart != null)
        {
            inst.BeamStart.SetActive(showStartVfx);
            if (showStartVfx)
            {
                inst.BeamStart.transform.position = start;
                if (Vector3.Distance(start, end) > 0.001f)
                {
                    inst.BeamStart.transform.LookAt(end);
                }
            }
        }

        if (inst.BeamEnd != null)
        {
            bool showEnd = !isBouncedEnd;
            inst.BeamEnd.SetActive(showEnd);
            if (showEnd)
            {
                inst.BeamEnd.transform.position = end;
                if (Vector3.Distance(start, end) > 0.001f)
                {
                    inst.BeamEnd.transform.LookAt(start);
                }
            }
        }

        if (inst.Line != null)
        {
            inst.PropertyBlock ??= new MaterialPropertyBlock();

            inst.Line.GetPropertyBlock(inst.PropertyBlock);

            float distance = Vector3.Distance(start, end);
            float scaleX = (textureLengthScale > 0.001f) ? (distance / textureLengthScale) : 1f;

            inst.TextureOffset = Mathf.Repeat(inst.TextureOffset - (Time.deltaTime * textureScrollSpeed), 1000f);

            Vector4 st = new(scaleX, 1f, inst.TextureOffset, 0f);
            inst.PropertyBlock.SetVector(IdMainTexST, st);
            inst.PropertyBlock.SetVector(IdBaseMapST, st);

            inst.Line.SetPropertyBlock(inst.PropertyBlock);
        }
    }

    private BeamInstance CreateNewBeamInstance()
    {
        BeamInstance inst = new()
        {
            PropertyBlock = new MaterialPropertyBlock()
        };

        if (beamLineRendererPrefab != null)
        {
            inst.BeamRoot = Instantiate(beamLineRendererPrefab, transform);
            inst.Line = inst.BeamRoot.GetComponent<LineRenderer>();

            if (inst.Line != null)
            {
                inst.Line.numCapVertices = 4;
                inst.Line.numCornerVertices = 4;
            }
        }

        if (beamStartPrefab != null)
        {
            inst.BeamStart = Instantiate(beamStartPrefab, transform);
        }

        if (beamEndPrefab != null)
        {
            inst.BeamEnd = Instantiate(beamEndPrefab, transform);
        }

        return inst;
    }

    private static void SetInstanceActive(BeamInstance inst, bool active)
    {
        if (inst.BeamRoot != null) inst.BeamRoot.SetActive(active);
        if (inst.BeamStart != null) inst.BeamStart.SetActive(active);
        if (inst.BeamEnd != null) inst.BeamEnd.SetActive(active);
    }

    private void ClearBeamInstances()
    {
        for (int i = 0; i < activeBeamInstances.Count; i++)
        {
            if (activeBeamInstances[i] != null) SetInstanceActive(activeBeamInstances[i], false);
        }
    }

    private class RaycastHitComparer : IComparer<RaycastHit2D>
    {
        public int Compare(RaycastHit2D a, RaycastHit2D b) => a.distance.CompareTo(b.distance);
    }

    private class Raycast3DHitComparer : IComparer<RaycastHit>
    {
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }
}
