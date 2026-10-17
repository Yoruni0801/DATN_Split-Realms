using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Quoc.QuyVuong
{
    [RequireComponent(typeof(QuyVuongBossHealth))]
    public class QuyVuongBossController : MonoBehaviour
    {
        [Header("Phase Models (GameObjects)")]
        [SerializeField]
        private QuyVuongBossPhase[] phaseModels;

        [Header("Phase Map Objects (GameObjects)")]
        [SerializeField]
        private GameObject[] phaseMapObjects;

        [Header("Phase Transition Settings")]
        [SerializeField]
        private float phaseTransitionDuration = 2.0f;

        [Tooltip("Thời gian mờ dần (Fade out) của VFX khi kết thúc chuyển phase (giây)")]
        [SerializeField]
        private float vfxFadeOutDuration = 1.0f;

        [Tooltip("Prefab hiệu ứng VFX xuất hiện khi đang chuyển phase")]
        [SerializeField]
        private GameObject phaseTransitionEffect;

        [Tooltip("Tùy chọn: Danh sách hiệu ứng VFX riêng biệt cho từng Phase (Nếu để trống sẽ dùng phaseTransitionEffect)")]
        [SerializeField]
        private GameObject[] perPhaseTransitionVfx;

        [Tooltip("Vị trí xuất hiện VFX (Nếu để trống sẽ xuất hiện tại vị trí Boss)")]
        [SerializeField]
        private Transform vfxSpawnPoint;

        [Tooltip("Tự động hủy VFX sau thời gian chuyển phase kết thúc")]
        [SerializeField]
        private bool destroyVfxAfterTransition = true;

        public QuyVuongBossHealth Health { get; private set; }
        public QuyVuongBossPhase CurrentPhaseModel => GetActivePhaseModel();

        private void Awake()
        {
            Health = GetComponent<QuyVuongBossHealth>();
        }

        private void OnEnable()
        {
            if (Health != null)
            {
                Health.OnPhaseTransitioning += HandlePhaseTransitioning;
                Health.OnDied += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (Health != null)
            {
                Health.OnPhaseTransitioning -= HandlePhaseTransitioning;
                Health.OnDied -= HandleDied;
            }
        }

        private void Start()
        {
            UpdatePhaseGameObjects(Health.CurrentPhaseIndex);
        }

        private QuyVuongBossPhase GetActivePhaseModel()
        {
            if (phaseModels == null || Health == null) return null;
            int phaseIndex = Health.CurrentPhaseIndex;
            if (phaseIndex >= 0 && phaseIndex < phaseModels.Length)
            {
                return phaseModels[phaseIndex];
            }
            return null;
        }

        /// <summary>
        /// Cập nhật bật/tắt các Phase GameObject và đồng bộ vị trí/hướng nhìn của Phase mới theo Phase cũ.
        /// </summary>
        public void UpdatePhaseGameObjects(int activeIndex, Vector3? targetPosition = null, Vector3? targetScale = null)
        {
            if (phaseModels != null && phaseModels.Length > 0)
            {
                for (int i = 0; i < phaseModels.Length; i++)
                {
                    if (phaseModels[i] != null)
                    {
                        bool isActive = (i == activeIndex);

                        // Đồng bộ vị trí và scale trước khi bật Active
                        if (isActive)
                        {
                            if (targetPosition.HasValue)
                            {
                                phaseModels[i].transform.position = targetPosition.Value;
                            }
                            if (targetScale.HasValue)
                            {
                                phaseModels[i].transform.localScale = targetScale.Value;
                            }
                        }

                        phaseModels[i].gameObject.SetActive(isActive);

                        if (isActive)
                        {
                            // Đảm bảo vị trí và scale vẫn giữ nguyên sau khi SetActive (ngăn chặn OnEnable của script con ghi đè)
                            if (targetPosition.HasValue)
                            {
                                phaseModels[i].transform.position = targetPosition.Value;
                            }
                            if (targetScale.HasValue)
                            {
                                phaseModels[i].transform.localScale = targetScale.Value;
                            }

                            phaseModels[i].PlayIdle();

                            // Mở khóa cho phase mới hoạt động
                            var newFlow = phaseModels[i].GetComponent<IQuyVuongPhaseFlow>() ?? phaseModels[i].GetComponentInChildren<IQuyVuongPhaseFlow>();
                            newFlow?.SetFrozen(false);
                        }
                    }
                }
            }

            if (phaseMapObjects != null && phaseMapObjects.Length > 0)
            {
                for (int i = 0; i < phaseMapObjects.Length; i++)
                {
                    if (phaseMapObjects[i] != null)
                    {
                        bool isActive = (i == activeIndex);
                        phaseMapObjects[i].SetActive(isActive);
                    }
                }
            }
        }

        private void HandlePhaseTransitioning()
        {
            _ = PerformPhaseTransitionAsync();
        }

        private async Task PerformPhaseTransitionAsync()
        {
            var ct = destroyCancellationToken;
            GameObject spawnedVfx = null;
            GameObject sceneVfx = null;

            try
            {
                // 1. Đóng băng phase model hiện tại (ngừng tấn công, di chuyển)
                QuyVuongBossPhase activeModel = CurrentPhaseModel;
                if (activeModel != null)
                {
                    var activeFlow = activeModel.GetComponent<IQuyVuongPhaseFlow>() ?? activeModel.GetComponentInChildren<IQuyVuongPhaseFlow>();
                    activeFlow?.SetFrozen(true);
                }

                // 2. Lưu lại vị trí và hướng nhìn hiện tại của Boss
                Vector3 currentPosition = activeModel != null ? activeModel.transform.position : transform.position;
                float currentFacingSign = activeModel != null ? Mathf.Sign(activeModel.transform.localScale.x) : 1f;

                // 3. Play animation transition trên phase model hiện tại
                if (activeModel != null)
                {
                    activeModel.PlayTransition();
                }

                // 4. Kích hoạt hiệu ứng VFX chuyển phase và xoay mặt theo Player trong suốt thời gian chuyển
                GameObject vfxPrefabToSpawn = null;
                int currentPhaseIdx = Health != null ? Health.CurrentPhaseIndex : 0;
                if (perPhaseTransitionVfx != null && currentPhaseIdx < perPhaseTransitionVfx.Length && perPhaseTransitionVfx[currentPhaseIdx] != null)
                {
                    vfxPrefabToSpawn = perPhaseTransitionVfx[currentPhaseIdx];
                }
                else
                {
                    vfxPrefabToSpawn = phaseTransitionEffect;
                }

                if (vfxPrefabToSpawn != null)
                {
                    Vector3 spawnPos = currentPosition;
                    if (vfxSpawnPoint != null)
                    {
                        spawnPos = vfxSpawnPoint.IsChildOf(transform)
                            ? currentPosition + vfxSpawnPoint.localPosition
                            : vfxSpawnPoint.position;
                    }

                    // Nếu là GameObject nằm sẵn trong Scene/Hierarchy
                    if (vfxPrefabToSpawn.scene.IsValid())
                    {
                        sceneVfx = vfxPrefabToSpawn;
                        sceneVfx.transform.position = spawnPos;
                        sceneVfx.SetActive(true);

                        var particles = sceneVfx.GetComponentsInChildren<ParticleSystem>(true);
                        foreach (var ps in particles)
                        {
                            ps.gameObject.SetActive(true);
                            ps.Clear();
                            ps.Play();
                        }
                    }
                    else
                    {
                        // Là Prefab Asset trong Project -> Instantiate vào Scene
                        spawnedVfx = Instantiate(vfxPrefabToSpawn, spawnPos, Quaternion.identity);
                        spawnedVfx.SetActive(true);

                        var particles = spawnedVfx.GetComponentsInChildren<ParticleSystem>(true);
                        foreach (var ps in particles)
                        {
                            ps.gameObject.SetActive(true);
                            ps.Clear();
                            ps.Play();
                        }
                    }
                }

                float elapsed = 0f;
                float durationSec = phaseTransitionDuration;
                while (elapsed < durationSec)
                {
                    Transform player = GetPlayerTransform();
                    if (player != null && activeModel != null)
                    {
                        activeModel.FacePlayer(player);
                    }

                    elapsed += Time.deltaTime;
                    await Task.Yield();
                    if (this == null || ct.IsCancellationRequested) return;
                }

                if (this == null) return;

                // Cập nhật lại vị trí mới nhất nếu có xê dịch
                if (activeModel != null)
                {
                    currentPosition = activeModel.transform.position;
                }

                // Dọn dẹp / Làm mờ dần (Fade Out) VFX mượt mà thay vì tắt phụt
                if (spawnedVfx != null)
                {
                    if (destroyVfxAfterTransition)
                    {
                        _ = FadeOutAndCleanVfxAsync(spawnedVfx, false, vfxFadeOutDuration);
                    }
                    spawnedVfx = null;
                }
                if (sceneVfx != null)
                {
                    _ = FadeOutAndCleanVfxAsync(sceneVfx, true, vfxFadeOutDuration);
                    sceneVfx = null;
                }

                // 5. Tính toán scale và chuyển sang phase tiếp theo tại vị trí của phase vừa đứng, luôn quay mặt về phía Player
                int nextPhaseIndex = Health.CurrentPhaseIndex + 1;
                Vector3? nextScale = null;
                if (nextPhaseIndex >= 0 && phaseModels != null && nextPhaseIndex < phaseModels.Length && phaseModels[nextPhaseIndex] != null)
                {
                    Transform player = GetPlayerTransform();
                    float facingSign = 1f;
                    if (player != null)
                    {
                        float dirX = player.position.x - currentPosition.x;
                        if (Mathf.Abs(dirX) > 0.05f) facingSign = dirX > 0f ? -1f : 1f;
                    }
                    else
                    {
                        facingSign = activeModel != null ? Mathf.Sign(activeModel.transform.localScale.x) : 1f;
                    }

                    Vector3 s = phaseModels[nextPhaseIndex].transform.localScale;
                    s.x = Mathf.Abs(s.x) * facingSign;
                    nextScale = s;
                }

                UpdatePhaseGameObjects(nextPhaseIndex, currentPosition, nextScale);

                // 6. Cập nhật lại health cho phase mới
                Health.AdvanceToNextPhase();
            }
            catch (OperationCanceledException)
            {
                // Task bị canceled khi GameObject bị destroy
                if (spawnedVfx != null) Destroy(spawnedVfx);
                if (sceneVfx != null) sceneVfx.SetActive(false);
            }
            catch (Exception ex)
            {
                if (spawnedVfx != null) Destroy(spawnedVfx);
                if (sceneVfx != null) sceneVfx.SetActive(false);

                Debug.LogError($"[QuyVuongBossController] Lỗi khi chuyển phase: {ex.Message}\n{ex.StackTrace}");
                // Nếu bị lỗi bất ngờ, đảm bảo vẫn advance phase để tránh đơ vĩnh viễn
                if (this != null && Health != null && Health.IsTransitioning)
                {
                    int nextPhaseIndex = Health.CurrentPhaseIndex + 1;
                    Vector3 fallbackPos = CurrentPhaseModel != null ? CurrentPhaseModel.transform.position : transform.position;
                    Vector3? fallbackScale = null;
                    if (nextPhaseIndex >= 0 && phaseModels != null && nextPhaseIndex < phaseModels.Length && phaseModels[nextPhaseIndex] != null)
                    {
                        float fallbackFacing = CurrentPhaseModel != null ? Mathf.Sign(CurrentPhaseModel.transform.localScale.x) : 1f;
                        Vector3 s = phaseModels[nextPhaseIndex].transform.localScale;
                        s.x = Mathf.Abs(s.x) * fallbackFacing;
                        fallbackScale = s;
                    }
                    UpdatePhaseGameObjects(nextPhaseIndex, fallbackPos, fallbackScale);
                    Health.AdvanceToNextPhase();
                }
            }
        }

        /// <summary>
        /// Fade out VFX mượt mà: Dừng sinh particle mới, giảm dần độ mờ của Sprite/Light/Canvas, sau đó dọn dẹp.
        /// </summary>
        private async Task FadeOutAndCleanVfxAsync(GameObject vfxObj, bool isSceneInstance, float fadeDuration)
        {
            if (vfxObj == null) return;

            var ct = destroyCancellationToken;

            try
            {
                // 1. Dừng sinh particle mới trên tất cả ParticleSystem, để các hạt hiện tại tan biến tự nhiên
                var particles = vfxObj.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in particles)
                {
                    if (ps != null)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }

                // 2. Thu thập các Light, SpriteRenderer, CanvasGroup để fade out độ trong suốt/cường độ sáng
                var lights = vfxObj.GetComponentsInChildren<Light>(true);
                float[] initialLightIntensities = new float[lights.Length];
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i] != null) initialLightIntensities[i] = lights[i].intensity;
                }

                var sprites = vfxObj.GetComponentsInChildren<SpriteRenderer>(true);
                Color[] initialSpriteColors = new Color[sprites.Length];
                for (int i = 0; i < sprites.Length; i++)
                {
                    if (sprites[i] != null) initialSpriteColors[i] = sprites[i].color;
                }

                var canvasGroups = vfxObj.GetComponentsInChildren<CanvasGroup>(true);
                float[] initialAlphas = new float[canvasGroups.Length];
                for (int i = 0; i < canvasGroups.Length; i++)
                {
                    if (canvasGroups[i] != null) initialAlphas[i] = canvasGroups[i].alpha;
                }

                if (fadeDuration > 0f)
                {
                    float elapsed = 0f;
                    while (elapsed < fadeDuration)
                    {
                        elapsed += Time.deltaTime;
                        float t = Mathf.Clamp01(elapsed / fadeDuration);
                        float alphaFactor = 1f - t;

                        // Fade lights
                        for (int i = 0; i < lights.Length; i++)
                        {
                            if (lights[i] != null)
                            {
                                lights[i].intensity = initialLightIntensities[i] * alphaFactor;
                            }
                        }

                        // Fade sprites
                        for (int i = 0; i < sprites.Length; i++)
                        {
                            if (sprites[i] != null)
                            {
                                Color c = initialSpriteColors[i];
                                c.a = initialSpriteColors[i].a * alphaFactor;
                                sprites[i].color = c;
                            }
                        }

                        // Fade canvas groups
                        for (int i = 0; i < canvasGroups.Length; i++)
                        {
                            if (canvasGroups[i] != null)
                            {
                                canvasGroups[i].alpha = initialAlphas[i] * alphaFactor;
                            }
                        }

                        await Task.Yield();
                        if (ct.IsCancellationRequested || vfxObj == null) return;
                    }
                }

                // 3. Kết thúc fade out -> Dọn dẹp hoặc tắt GameObject
                if (vfxObj != null)
                {
                    if (!isSceneInstance)
                    {
                        Destroy(vfxObj);
                    }
                    else
                    {
                        // Khôi phục giá trị ban đầu cho lần dùng tiếp theo trong Scene
                        for (int i = 0; i < lights.Length; i++)
                        {
                            if (lights[i] != null) lights[i].intensity = initialLightIntensities[i];
                        }
                        for (int i = 0; i < sprites.Length; i++)
                        {
                            if (sprites[i] != null) sprites[i].color = initialSpriteColors[i];
                        }
                        for (int i = 0; i < canvasGroups.Length; i++)
                        {
                            if (canvasGroups[i] != null) canvasGroups[i].alpha = initialAlphas[i];
                        }

                        vfxObj.SetActive(false);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogWarning($"[QuyVuongBossController] Lỗi trong lúc FadeOut VFX: {ex.Message}");
                if (vfxObj != null)
                {
                    if (!isSceneInstance) Destroy(vfxObj);
                    else vfxObj.SetActive(false);
                }
            }
        }

        private void HandleDied()
        {
            QuyVuongBossPhase activeModel = CurrentPhaseModel;
            if (activeModel != null)
            {
                activeModel.PlayDeath();
            }
            Debug.Log("[QuyVuongBossController] Boss đã chết!");
        }

        private Transform GetPlayerTransform()
        {
            if (PlayerRegistry.Current != null) return PlayerRegistry.Current.transform;
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) return playerObj.transform;
            var health = FindAnyObjectByType<PlayerHealth>();
            if (health != null) return health.transform;
            var playerComp = FindAnyObjectByType<Player>();
            if (playerComp != null) return playerComp.transform;
            return null;
        }
    }
}
