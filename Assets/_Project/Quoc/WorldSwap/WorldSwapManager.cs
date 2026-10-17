using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class WorldSwapManager : MonoBehaviour
{
    [Header("World Roots")]
    [SerializeField] private GameObject worldA;
    [SerializeField] private GameObject worldB;

    [Header("Input")]
    [SerializeField] private KeyCode swapKey = KeyCode.M;

    [Header("Unlock")]
    [SerializeField] private bool requireUnlock = true;
    [SerializeField] private bool unlockedOnStart;

    [Header("Cutscene")]
    [SerializeField] private WorldSwapCutscene cutscene;
    [SerializeField] private bool lockInputDuringCutscene = true;
    [SerializeField] private bool swapWhileCutscenePlays = true;
    [SerializeField] private float swapDelaySeconds = 2f;

    [Header("Particle Effect")]
    [SerializeField] private GameObject swapParticlePrefab;
    [Tooltip("Vị trí spawn particle. Nếu để trống sẽ spawn tại vị trí của Player hoặc WorldSwapManager.")]
    [SerializeField] private Transform particleSpawnPoint;
    [Tooltip("Thời gian hoãn (delay) trước khi hiệu ứng particle xuất hiện.")]
    [SerializeField] private float particleSpawnDelaySeconds = 0f;
    [Tooltip("Tỷ lệ tốc độ phát particle (Simulation Speed). 1 = Bình thường, 2 = Nhanh gấp đôi...")]
    [SerializeField] private float particleSimulationSpeed = 1f;
    [Tooltip("Thời gian tồn tại của particle (Duration). Nhập > 0 để đè độ dài phát, <= 0 để giữ mặc định của Particle System.")]
    [SerializeField] private float particleDuration = 0f;
    [Tooltip("Thời gian fade in của particle khi xuất hiện (tính bằng giây).")]
    [SerializeField] private float particleFadeInDuration = 0.5f;
    [Tooltip("Thời gian fade out của particle trước khi kết thúc (tính bằng giây).")]
    [SerializeField] private float particleFadeOutDuration = 0.5f;

    [Header("Start State")]
    private bool startInWorldA = true;

    [Header("Safe Swap Check")]
    [Tooltip("Nếu bật, sẽ kiểm tra xem vị trí map đích có bị kẹt tường không")]
    [SerializeField] private bool preventSwapIfBlocked = true;

    [Header("Events")]
    [HideInInspector] public UnityEvent<bool> onWorldStateApplied;
    [HideInInspector] public UnityEvent onSwapUnlocked;
    [HideInInspector] public UnityEvent onSwapLocked;
    [HideInInspector] public UnityEvent<bool> onBusyChanged;
    [HideInInspector] public UnityEvent onSwapDenied;
    [Tooltip("Sự kiện bắn ra ở lần đầu tiên swap map thành công")]
    public UnityEvent onFirstSuccessfulSwap;

    public static event System.Action<bool> GlobalSwapUnlockStateChanged;
    public static event System.Action<bool> GlobalWorldStateApplied;
    public static event System.Action<bool> GlobalBusyChanged;
    public static event System.Action GlobalSwapDenied;

    public static bool IsAnyUnlockedGlobally { get; private set; }

    private bool isInWorldA;
    private bool isBusy;
    private bool isUnlocked;
    private bool hasSuccessfulSwap;
    private Collider2D cachedPlayerCollider;
    private readonly Collider2D[] blockedCheckResults = new Collider2D[1];
    private ContactFilter2D blockedCheckFilter;
    private int blockedCheckLayerMask;

    public bool IsInWorldA => isInWorldA;
    public bool IsInParallelWorld => !isInWorldA;
    public bool IsUnlocked => !requireUnlock || isUnlocked;
    public bool IsBusy => isBusy;
    public bool HasSuccessfulSwap => hasSuccessfulSwap;

    private void OnEnable()
    {
        PlayerRespawnController.RespawnRequested += HandlePlayerRespawnRequested;
        PlayerRegistry.OnCurrentChanged += HandlePlayerChanged;
        RefreshPlayerCollider();
    }

    private void OnDisable()
    {
        PlayerRespawnController.RespawnRequested -= HandlePlayerRespawnRequested;
        PlayerRegistry.OnCurrentChanged -= HandlePlayerChanged;
    }

    private void HandlePlayerChanged(Player player)
    {
        RefreshPlayerCollider();
    }

    private void RefreshPlayerCollider()
    {
        Player current = PlayerRegistry.Current;
        cachedPlayerCollider = current != null ? current.GetComponent<Collider2D>() : null;
    }

    private void Start()
    {
        isInWorldA = startInWorldA;
        SetSwapUnlocked(!requireUnlock || unlockedOnStart);
        ApplyWorldState();
    }

    private void Update()
    {
        if (Input.GetKeyDown(swapKey))
        {
            if (!CanSwap())
            {
                return;
            }

            if (IsTargetLocationBlocked())
            {
                onSwapDenied?.Invoke();
                GlobalSwapDenied?.Invoke();
            }
            else
            {
                SpawnSwapParticle();
                StartCoroutine(SwapRoutine());
            }
        }
    }

    private bool IsTargetLocationBlocked()
    {
        if (!preventSwapIfBlocked) return false;

        if (cachedPlayerCollider == null)
        {
            RefreshPlayerCollider();
        }
        if (cachedPlayerCollider == null) return false;

        LayerMask targetLayer = isInWorldA
            ? PlayerLayerConfig.WorldBObstacleLayer
            : PlayerLayerConfig.WorldAObstacleLayer;

        UpdateBlockedCheckFilter(targetLayer);

        Vector2 center = cachedPlayerCollider.bounds.center;
        Vector2 size = cachedPlayerCollider.bounds.size;
        size.x *= 0.95f;
        size.y *= 0.95f;

        int hitCount = Physics2D.OverlapBox(center, size, 0f, blockedCheckFilter, blockedCheckResults);

        if (hitCount > 0)
        {
            Debug.Log($"[SafeSwap] Bị kẹt! Va chạm với: {blockedCheckResults[0].gameObject.name} (Layer: {LayerMask.LayerToName(blockedCheckResults[0].gameObject.layer)})");
        }

        return hitCount > 0;
    }

    private void UpdateBlockedCheckFilter(int targetLayerMask)
    {
        if (blockedCheckLayerMask == targetLayerMask)
        {
            return;
        }

        blockedCheckLayerMask = targetLayerMask;
        blockedCheckFilter.SetLayerMask(targetLayerMask);
        blockedCheckFilter.useLayerMask = true;
        blockedCheckFilter.useTriggers = true;
    }

    private IEnumerator SwapRoutine()
    {
        SetBusy(true);

        if (cutscene != null && swapWhileCutscenePlays)
        {
            StartCoroutine(cutscene.Play());

            if (swapDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(swapDelaySeconds);
            }

            CompleteSwap();

            if (lockInputDuringCutscene)
            {
                while (cutscene.IsPlaying)
                {
                    yield return null;
                }
            }
        }
        else
        {
            if (cutscene != null)
            {
                yield return cutscene.Play();
            }

            CompleteSwap();
        }

        if (!lockInputDuringCutscene)
        {
            SetBusy(false);
            yield break;
        }

        yield return null;
        SetBusy(false);
    }

    private void CompleteSwap()
    {
        bool isFirstSwap = !hasSuccessfulSwap;
        isInWorldA = !isInWorldA;
        hasSuccessfulSwap = true;

        if (isFirstSwap)
        {
            onFirstSuccessfulSwap?.Invoke();
        }

        ApplyWorldState();
    }

    public void PerformWorldSwap()
    {
        CompleteSwap();
    }

    private void SetBusy(bool busy)
    {
        if (isBusy == busy)
        {
            return;
        }

        isBusy = busy;
        onBusyChanged?.Invoke(busy);
        GlobalBusyChanged?.Invoke(busy);
    }

    private void ApplyWorldState()
    {
        if (worldA != null) worldA.SetActive(isInWorldA);
        if (worldB != null) worldB.SetActive(!isInWorldA);
        onWorldStateApplied?.Invoke(isInWorldA);
        GlobalWorldStateApplied?.Invoke(isInWorldA);
    }

    public void ForceSwap()
    {
        if (!CanSwap()) return;

        if (IsTargetLocationBlocked())
        {
            return;
        }

        SpawnSwapParticle();
        StartCoroutine(SwapRoutine());
    }

    public void ForceWorldA()
    {
        if (isInWorldA)
        {
            return;
        }

        if (IsTargetLocationBlocked())
        {
            onSwapDenied?.Invoke();
            GlobalSwapDenied?.Invoke();
            return;
        }

        isInWorldA = true;
        ApplyWorldState();
    }

    private void HandlePlayerRespawnRequested(PlayerRespawnController controller)
    {
        if (IsInParallelWorld)
        {
            ForceWorldA();
        }
    }

    public void UnlockSwap()
    {
        if (isUnlocked)
        {
            return;
        }

        isUnlocked = true;
        onSwapUnlocked?.Invoke();
        IsAnyUnlockedGlobally = true;
        GlobalSwapUnlockStateChanged?.Invoke(true);
    }

    public void LockSwap()
    {
        if (!isUnlocked)
        {
            return;
        }

        isUnlocked = false;
        onSwapLocked?.Invoke();

#if UNITY_2023_1_OR_NEWER
        var allManagers = FindObjectsByType<WorldSwapManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        var allManagers = FindObjectsOfType<WorldSwapManager>();
#endif
        bool anyUnlocked = false;
        if (allManagers != null)
        {
            foreach (var manager in allManagers)
            {
                if (manager.IsUnlocked)
                {
                    anyUnlocked = true;
                    break;
                }
            }
        }

        IsAnyUnlockedGlobally = anyUnlocked;
        GlobalSwapUnlockStateChanged?.Invoke(anyUnlocked);
    }

    public void SetSwapUnlocked(bool unlocked)
    {
        if (unlocked)
        {
            UnlockSwap();
        }
        else
        {
            LockSwap();
        }
    }

    private void SpawnSwapParticle()
    {
        if (swapParticlePrefab == null) return;

        if (particleSpawnDelaySeconds > 0f)
        {
            StartCoroutine(SpawnSwapParticleDelayed());
        }
        else
        {
            ExecuteSpawnSwapParticle();
        }
    }

    private IEnumerator SpawnSwapParticleDelayed()
    {
        yield return new WaitForSeconds(particleSpawnDelaySeconds);
        ExecuteSpawnSwapParticle();
    }

    private void ExecuteSpawnSwapParticle()
    {
        if (swapParticlePrefab == null) return;

        Transform parentTarget = particleSpawnPoint;
        if (parentTarget == null)
        {
            Player current = PlayerRegistry.Current;
            if (current != null)
            {
                parentTarget = current.transform;
            }
        }

        Vector3 spawnPos = parentTarget != null ? parentTarget.position : transform.position;
        GameObject particleObj = Instantiate(swapParticlePrefab, spawnPos, Quaternion.identity, parentTarget);
        ParticleSystem[] systems = particleObj.GetComponentsInChildren<ParticleSystem>();

        float maxLifetime = 0f;

        foreach (ParticleSystem ps in systems)
        {
            var main = ps.main;
            if (particleSimulationSpeed > 0f)
            {
                main.simulationSpeed = particleSimulationSpeed;
            }

            if (particleDuration > 0f)
            {
                main.duration = particleDuration;
            }

            float currentLifetime = main.duration + main.startLifetime.constantMax;
            if (particleSimulationSpeed > 0f)
            {
                currentLifetime /= particleSimulationSpeed;
            }

            if (currentLifetime > maxLifetime)
            {
                maxLifetime = currentLifetime;
            }

            ps.Play();
        }

        float destroyDelay = particleDuration > 0f ? particleDuration : maxLifetime;
        if (particleSimulationSpeed > 0f && particleDuration > 0f)
        {
            destroyDelay /= particleSimulationSpeed;
        }

        if (particleFadeInDuration > 0f || particleFadeOutDuration > 0f)
        {
            StartCoroutine(AnimateParticleFade(systems, destroyDelay, particleFadeInDuration, particleFadeOutDuration));
        }

        Destroy(particleObj, Mathf.Max(0.5f, destroyDelay + 0.2f));
    }

    private IEnumerator AnimateParticleFade(ParticleSystem[] systems, float totalDuration, float fadeInTime, float fadeOutTime)
    {
        if (systems == null || systems.Length == 0) yield break;

        ParticleSystem.MinMaxGradient[] initialColors = new ParticleSystem.MinMaxGradient[systems.Length];
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] != null)
            {
                initialColors[i] = systems[i].main.startColor;
            }
        }

        float elapsed = 0f;
        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;

            float alpha = 1f;

            if (fadeInTime > 0f && elapsed < fadeInTime)
            {
                alpha = Mathf.Clamp01(elapsed / fadeInTime);
            }
            else if (fadeOutTime > 0f && elapsed > (totalDuration - fadeOutTime))
            {
                float fadeOutElapsed = elapsed - (totalDuration - fadeOutTime);
                alpha = Mathf.Clamp01(1f - (fadeOutElapsed / fadeOutTime));
            }

            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] == null) continue;

                var main = systems[i].main;
                ParticleSystem.MinMaxGradient baseGrad = initialColors[i];

                switch (baseGrad.mode)
                {
                    case ParticleSystemGradientMode.Color:
                        Color c = baseGrad.color;
                        c.a = baseGrad.color.a * alpha;
                        main.startColor = c;
                        break;
                    case ParticleSystemGradientMode.TwoColors:
                        Color c1 = baseGrad.colorMin;
                        Color c2 = baseGrad.colorMax;
                        c1.a = baseGrad.colorMin.a * alpha;
                        c2.a = baseGrad.colorMax.a * alpha;
                        main.startColor = new ParticleSystem.MinMaxGradient(c1, c2);
                        break;
                    case ParticleSystemGradientMode.Gradient:
                        Gradient g = baseGrad.gradient;
                        Gradient newG = new();
                        GradientColorKey[] colorKeys = g.colorKeys;
                        GradientAlphaKey[] alphaKeys = g.alphaKeys;
                        for (int k = 0; k < alphaKeys.Length; k++)
                        {
                            alphaKeys[k].alpha *= alpha;
                        }
                        newG.SetKeys(colorKeys, alphaKeys);
                        main.startColor = newG;
                        break;
                }
            }

            yield return null;
        }
    }

    public bool CanSwap()
    {
        return !isBusy && (!requireUnlock || isUnlocked) && !Tien.Minigame.MinigameCheckpointState.IsInMinigame;
    }
}
