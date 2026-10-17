using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Spine;
using Spine.Unity;

namespace Quoc.QuyVuong
{
    public class QuyVuongPhaseSwitcher : MonoBehaviour
    {
        [Header("Phase GameObjects Setup (4 Phases)")]
        [Tooltip("Danh sách 4 Phase GameObject (Phase 1: Cung thủ, Phase 2: Liềm tử thần, Phase 3: Thương thủ, Phase 4: Hỏa nham ma). Chứa component triển khai IQuyVuongPhaseFlow")]
        [SerializeField] private GameObject[] phaseObjects;

        [Tooltip("Tùy chọn: Danh sách Map/Environment Object tương ứng cho từng Phase (bật/tắt song song với Phase)")]
        [SerializeField] private GameObject[] phaseMapObjects;

        [Header("References")]
        [SerializeField] private QuyVuongBossHealth bossHealth;

        [Header("Phase Switch Settings")]
        [Tooltip("Bật/Tắt tính năng tự động chuyển phase khi hoàn thành 1 flow combo/hành động")]
        [SerializeField] private bool autoSwitchEnabled = true;

        [Tooltip("Thời gian đứng yên bất động giữa các lần chuyển phase (tính bằng giây)")]
        [SerializeField] private float transitionDuration = 2.0f;

        [Tooltip("Tự động lặp lại từ Phase đầu tiên khi chạy hết 4 phase (1 -> 2 -> 3 -> 4 -> 1...). Nếu tắt, boss sẽ kết thúc/ẩn sau khi hết phase 4")]
        [SerializeField] private bool loopPhases = true;

        [Header("Transition Animation Settings")]
        [Tooltip("Animation Spine phát trong lúc boss đứng yên chuyển phase (Ví dụ: 'Skill', 'Idle', 'Transition')")]
        [SerializeField] private string transitionAnimationName = "Skill";

        [Tooltip("Có phát animation Idle lại trước khi bắt đầu Phase mới hay không")]
        [SerializeField] private bool playIdleOnNewPhase = true;

        [Header("Transition VFX Settings")]
        [Tooltip("Prefab hiệu ứng VFX xuất hiện khi đang chuyển phase")]
        [SerializeField] private GameObject transitionVfxPrefab;

        [Tooltip("Tùy chọn: Danh sách hiệu ứng VFX riêng biệt cho từng Phase trong 4 phase (Nếu để trống hoặc thiếu sẽ dùng transitionVfxPrefab)")]
        [SerializeField] private GameObject[] perPhaseTransitionVfx;

        [Tooltip("Vị trí xuất hiện VFX (Nếu để trống sẽ xuất hiện tại vị trí Boss)")]
        [SerializeField] private Transform vfxSpawnPoint;

        [Tooltip("Thời gian mờ dần (Fade out) của VFX khi kết thúc chuyển phase (giây)")]
        [SerializeField] private float vfxFadeOutDuration = 1.0f;

        [Tooltip("Tự động hủy VFX sau thời gian chuyển phase kết thúc")]
        [SerializeField] private bool destroyVfxAfterTransition = true;

        [Header("Hurt Flash Settings (Hiệu ứng khi bị dính đòn)")]
        [Tooltip("Bật/Tắt hiệu ứng Hurt Flash (nhấp nháy đổi màu) khi Boss nhận sát thương")]
        [SerializeField] private bool enableHurtFlash = true;

        [Tooltip("Màu sắc nhấp nháy khi bị trúng đòn (mặc định đỏ cam sáng)")]
        [SerializeField] private Color hurtFlashColor = new Color(1f, 0.2f, 0.2f, 1f);

        [Tooltip("Thời gian nhấp nháy đổi màu cho mỗi lần chớp (giây)")]
        [SerializeField] private float hurtFlashDuration = 0.15f;

        [Tooltip("Số lần chớp nhấp nháy mỗi khi nhận đòn (mặc định: 1)")]
        [SerializeField] private int hurtFlashCount = 1;

        [Tooltip("Khoảng thời gian nghỉ giữa các lần chớp nếu hurtFlashCount > 1 (giây)")]
        [SerializeField] private float hurtFlashInterval = 0.05f;

        [Tooltip("Tự động lắng nghe BossHealth để kích hoạt Hurt Flash khi mất máu")]
        [SerializeField] private bool autoTriggerOnHealthLoss = true;

        [Tooltip("Sử dụng MaterialPropertyBlock cho Spine Shaders hỗ trợ _FillPhase/_FillColor")]
        [SerializeField] private bool useSpineFillProperty = false;

        [Header("Defeat / Death Settings (Khi Boss Bị Đánh Bại)")]
        [Tooltip("Tự động ẩn Boss (SetActive false) ngay lập tức sau khi bị đánh bại")]
        [SerializeField] private bool hideOnDefeat = true;

        [Tooltip("Thời gian chờ trước khi ẩn Boss sau khi chết (giây). Đặt = 0 để ẩn NGAY LẬP TỨC")]
        [SerializeField] private float hideDelayAfterDeath = 0f;

        [Tooltip("Có phát animation chết trên Phase hiện tại trước khi ẩn không (Nếu hideDelayAfterDeath = 0 thì sẽ ẩn ngay)")]
        [SerializeField] private bool playDeathAnimation = false;

        [Tooltip("Tên animation chết (Spine)")]
        [SerializeField] private string deathAnimationName = "Death";

        [Tooltip("Prefab hiệu ứng VFX xuất hiện khi Boss bị tiêu diệt / tan biến")]
        [SerializeField] private GameObject deathVfxPrefab;

        [Tooltip("Có tắt cả Map/Environment objects khi boss chết không")]
        [SerializeField] private bool hideMapOnDefeat = false;

        [Tooltip("Có tắt toàn bộ GameObject Root chứa script này khi boss bị đánh bại không")]
        [SerializeField] private bool hideRootGameObjectOnDefeat = false;

        public int CurrentPhaseIndex { get; private set; } = 0;
        public bool IsTransitioning { get; private set; } = false;
        public bool IsDefeated { get; private set; } = false;

        // Events
        public event Action<int> OnPhaseChanged;
        public event Action OnBossDefeated;

        private IQuyVuongPhaseFlow currentActiveFlow;
        private CancellationTokenSource hurtFlashCts;
        private int lastObservedHealth = -1;

        private void Awake()
        {
            if (bossHealth == null)
            {
                bossHealth = GetComponent<QuyVuongBossHealth>() ?? GetComponentInParent<QuyVuongBossHealth>() ?? GetComponentInChildren<QuyVuongBossHealth>();
            }
        }

        private void OnEnable()
        {
            IsDefeated = false;
            ActivatePhase(CurrentPhaseIndex);

            if (bossHealth != null)
            {
                bossHealth.OnDied -= HandleBossDied;
                bossHealth.OnDied += HandleBossDied;

                lastObservedHealth = bossHealth.CurrentHealth;
                bossHealth.OnHealthChanged -= HandleBossHealthChanged;
                bossHealth.OnHealthChanged += HandleBossHealthChanged;
            }
        }

        private void OnDisable()
        {
            StopHurtFlash();
            ResetAllPhaseColors();
            UnsubscribeCurrentFlow();

            if (bossHealth != null)
            {
                bossHealth.OnDied -= HandleBossDied;
                bossHealth.OnHealthChanged -= HandleBossHealthChanged;
            }
        }

        private void OnDestroy()
        {
            StopHurtFlash();
        }

        private void HandleBossHealthChanged(int currentHealth, int maxHealth)
        {
            if (autoTriggerOnHealthLoss && lastObservedHealth > 0 && currentHealth < lastObservedHealth && !IsDefeated && !IsTransitioning)
            {
                TriggerHurtFlash();
            }
            lastObservedHealth = currentHealth;
        }

        /// <summary>
        /// Kích hoạt phase theo index (0 -> Phase 1, 1 -> Phase 2, 2 -> Phase 3, 3 -> Phase 4),
        /// đồng thời đăng ký sự kiện lắng nghe khi phase đó hoàn thành 1 flow.
        /// Luôn tự động quay mặt về phía Player khi kích hoạt.
        /// </summary>
        public void ActivatePhase(int phaseIndex, Vector3? targetPosition = null, float? facingSign = null)
        {
            if (phaseObjects == null || phaseObjects.Length == 0) return;

            StopHurtFlash();
            ResetAllPhaseColors();
            UnsubscribeCurrentFlow();

            CurrentPhaseIndex = Mathf.Clamp(phaseIndex, 0, phaseObjects.Length - 1);

            // Xác định hướng nhìn chuẩn theo vị trí Player hiện tại
            Transform player = GetPlayerTransform();
            Vector3 spawnPos = targetPosition.HasValue ? targetPosition.Value : transform.position;
            float targetFacing = 1f;

            if (player != null)
            {
                float dirX = player.position.x - spawnPos.x;
                if (Mathf.Abs(dirX) > 0.05f)
                {
                    targetFacing = dirX > 0f ? -1f : 1f;
                }
            }
            else if (facingSign.HasValue)
            {
                targetFacing = facingSign.Value;
            }

            // Bật/tắt các Phase GameObject và đồng bộ vị trí + hướng nhìn theo Player
            for (int i = 0; i < phaseObjects.Length; i++)
            {
                if (phaseObjects[i] != null)
                {
                    bool isActive = (i == CurrentPhaseIndex);
                    if (isActive)
                    {
                        if (targetPosition.HasValue)
                        {
                            phaseObjects[i].transform.position = targetPosition.Value;
                        }

                        // Cập nhật scale.x luôn nhìn theo hướng đảo (quay mặt chuẩn của Spine)
                        Vector3 scale = phaseObjects[i].transform.localScale;
                        scale.x = Mathf.Abs(scale.x) * targetFacing;
                        phaseObjects[i].transform.localScale = scale;

                        var bossPhaseComp = phaseObjects[i].GetComponent<QuyVuongBossPhase>() ?? phaseObjects[i].GetComponentInChildren<QuyVuongBossPhase>();
                        if (bossPhaseComp != null && bossPhaseComp.transform != phaseObjects[i].transform)
                        {
                            bossPhaseComp.SetFacingDirection(player != null ? player.position.x - spawnPos.x : (targetFacing > 0 ? -1f : 1f));
                        }
                    }
                    phaseObjects[i].SetActive(isActive);
                }
            }

            // Bật/tắt Map Objects tương ứng cho từng phase
            if (phaseMapObjects != null && phaseMapObjects.Length > 0)
            {
                for (int i = 0; i < phaseMapObjects.Length; i++)
                {
                    if (phaseMapObjects[i] != null)
                    {
                        phaseMapObjects[i].SetActive(i == CurrentPhaseIndex);
                    }
                }
            }

            // Đăng ký nhận sự kiện OnFlowCompleted từ phase mới
            GameObject activeObj = phaseObjects[CurrentPhaseIndex];
            if (activeObj != null)
            {
                currentActiveFlow = activeObj.GetComponent<IQuyVuongPhaseFlow>() ?? activeObj.GetComponentInChildren<IQuyVuongPhaseFlow>();
                if (currentActiveFlow != null)
                {
                    currentActiveFlow.OnFlowCompleted += HandlePhaseFlowCompleted;
                }
            }

            OnPhaseChanged?.Invoke(CurrentPhaseIndex);
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

        private void UnsubscribeCurrentFlow()
        {
            if (currentActiveFlow != null)
            {
                currentActiveFlow.OnFlowCompleted -= HandlePhaseFlowCompleted;
                currentActiveFlow = null;
            }
        }

        private void HandlePhaseFlowCompleted()
        {
            if (!autoSwitchEnabled || IsTransitioning || IsDefeated || (bossHealth != null && bossHealth.IsDead)) return;

            // Nếu không lặp lại và đã ở phase cuối cùng (Phase 4), hoàn tất boss
            if (!loopPhases && CurrentPhaseIndex >= phaseObjects.Length - 1)
            {
                DefeatAndHideBoss();
                return;
            }

            _ = PerformPhaseTransitionAsync();
        }

        private void HandleBossDied()
        {
            if (IsDefeated) return;
            IsDefeated = true;

            StopHurtFlash();
            ResetAllPhaseColors();
            UnsubscribeCurrentFlow();

            // 1. Đóng băng tất cả phase và dọn dẹp toàn bộ skill / đạn / vật thể trên sân
            Quoc.QuyVuong.Phase1.QuyVuongWaterCage.ClearAll();
            if (phaseObjects != null)
            {
                foreach (var obj in phaseObjects)
                {
                    if (obj != null)
                    {
                        var flow = obj.GetComponent<IQuyVuongPhaseFlow>() ?? obj.GetComponentInChildren<IQuyVuongPhaseFlow>();
                        if (flow != null)
                        {
                            flow.SetFrozen(true);
                            flow.ClearAllSpawnedSkills();
                        }
                    }
                }
            }

            // 2. Nếu hideDelayAfterDeath <= 0 thì ẨN NGAY LẬP TỨC
            if (hideOnDefeat && hideDelayAfterDeath <= 0f)
            {
                SpawnDeathVfxIfConfigured();
                HideAllPhases();

                if (hideMapOnDefeat && phaseMapObjects != null)
                {
                    foreach (var mapObj in phaseMapObjects)
                    {
                        if (mapObj != null) mapObj.SetActive(false);
                    }
                }

                if (hideRootGameObjectOnDefeat)
                {
                    gameObject.SetActive(false);
                }

                OnBossDefeated?.Invoke();
                Debug.Log("[QuyVuongPhaseSwitcher] Boss đã bị đánh bại và ẩn NGAY LẬP TỨC.");
                return;
            }

            // 3. Nếu có cài đặt delay thì chạy quy trình chờ delay
            _ = HandleBossDefeatSequenceAsync();
        }

        private void SpawnDeathVfxIfConfigured()
        {
            if (deathVfxPrefab != null)
            {
                GameObject currentObj = (phaseObjects != null && CurrentPhaseIndex < phaseObjects.Length) ? phaseObjects[CurrentPhaseIndex] : null;
                Vector3 spawnPos = currentObj != null ? currentObj.transform.position : transform.position;
                if (vfxSpawnPoint != null)
                {
                    spawnPos = vfxSpawnPoint.IsChildOf(transform)
                        ? (currentObj != null ? currentObj.transform.position : transform.position) + vfxSpawnPoint.localPosition
                        : vfxSpawnPoint.position;
                }

                GameObject spawnedDeathVfx = Instantiate(deathVfxPrefab, spawnPos, Quaternion.identity);
                spawnedDeathVfx.SetActive(true);

                var particles = spawnedDeathVfx.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in particles)
                {
                    ps.gameObject.SetActive(true);
                    ps.Clear();
                    ps.Play();
                }
            }
        }

        /// <summary>
        /// Chuỗi xử lý khi Boss bị đánh bại với thời gian delay: Phát Animation chết, sinh VFX chết, chờ delay và ẩn hoàn toàn Boss.
        /// </summary>
        private async Task HandleBossDefeatSequenceAsync()
        {
            var ct = destroyCancellationToken;

            try
            {
                OnBossDefeated?.Invoke();

                GameObject currentObj = (phaseObjects != null && CurrentPhaseIndex < phaseObjects.Length) ? phaseObjects[CurrentPhaseIndex] : null;

                // 1. Phát Animation Death trên Phase hiện tại
                if (playDeathAnimation && currentObj != null)
                {
                    var currentBossPhase = currentObj.GetComponent<QuyVuongBossPhase>() ?? currentObj.GetComponentInChildren<QuyVuongBossPhase>();
                    if (currentBossPhase != null)
                    {
                        if (!string.IsNullOrEmpty(deathAnimationName))
                        {
                            currentBossPhase.PlaySpineAnimation(deathAnimationName, false);
                        }
                        else
                        {
                            currentBossPhase.PlayDeath();
                        }
                    }
                }

                // 2. Sinh hiệu ứng VFX chết nếu có cấu hình
                SpawnDeathVfxIfConfigured();

                // 3. Chờ hết thời gian delay trước khi ẩn (để animation chết / VFX hoàn thành)
                if (hideDelayAfterDeath > 0f)
                {
                    int delayMs = Mathf.RoundToInt(hideDelayAfterDeath * 1000f);
                    await Task.Delay(delayMs, ct);
                }

                if (this == null || ct.IsCancellationRequested) return;

                // 4. Ẩn Boss sau khi bị đánh bại
                if (hideOnDefeat)
                {
                    HideAllPhases();

                    if (hideMapOnDefeat && phaseMapObjects != null)
                    {
                        foreach (var mapObj in phaseMapObjects)
                        {
                            if (mapObj != null) mapObj.SetActive(false);
                        }
                    }

                    if (hideRootGameObjectOnDefeat)
                    {
                        gameObject.SetActive(false);
                    }

                    Debug.Log("[QuyVuongPhaseSwitcher] Boss đã bị đánh bại và tự động ẩn đi hoàn toàn.");
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogError($"[QuyVuongPhaseSwitcher] Lỗi khi xử lý ẩn Boss sau khi bị đánh bại: {ex.Message}");
                if (hideOnDefeat && this != null)
                {
                    HideAllPhases();
                }
            }
        }

        /// <summary>
        /// Xử lý quá trình chuyển phase: Đóng băng Boss (dừng di chuyển, dừng target Player),
        /// phát Animation & VFX transition, chờ hết thời gian, sau đó đổi sang Phase tiếp theo (1 -> 2 -> 3 -> 4) và cho Boss hoạt động lại.
        /// </summary>
        public async Task PerformPhaseTransitionAsync()
        {
            if (IsTransitioning || IsDefeated || (bossHealth != null && bossHealth.IsDead)) return;
            IsTransitioning = true;

            StopHurtFlash();
            ResetAllPhaseColors();

            var ct = destroyCancellationToken;
            GameObject spawnedVfx = null;

            try
            {
                // 1. Đóng băng Phase hiện tại: Boss đứng yên, không di chuyển, không target Player
                currentActiveFlow?.SetFrozen(true);

                // 2. Lưu lại vị trí và hướng nhìn (dấu scale.x) của Phase hiện tại trước khi tắt
                GameObject currentObj = (phaseObjects != null && CurrentPhaseIndex < phaseObjects.Length) ? phaseObjects[CurrentPhaseIndex] : null;
                Vector3 currentPosition = currentObj != null ? currentObj.transform.position : transform.position;
                float currentFacingSign = currentObj != null ? Mathf.Sign(currentObj.transform.localScale.x) : 1f;

                QuyVuongBossPhase currentBossPhase = null;

                if (currentObj != null)
                {
                    currentBossPhase = currentObj.GetComponent<QuyVuongBossPhase>() ?? currentObj.GetComponentInChildren<QuyVuongBossPhase>();
                }

                if (currentBossPhase != null && !string.IsNullOrEmpty(transitionAnimationName))
                {
                    currentBossPhase.PlaySpineAnimation(transitionAnimationName, false);
                }

                // 3. Spawn / Bật hiệu ứng VFX được chọn (tại vị trí chính xác của Phase vừa rồi)
                GameObject vfxPrefabToSpawn = null;
                if (perPhaseTransitionVfx != null && CurrentPhaseIndex < perPhaseTransitionVfx.Length && perPhaseTransitionVfx[CurrentPhaseIndex] != null)
                {
                    vfxPrefabToSpawn = perPhaseTransitionVfx[CurrentPhaseIndex];
                }
                else
                {
                    vfxPrefabToSpawn = transitionVfxPrefab;
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

                    spawnedVfx = Instantiate(vfxPrefabToSpawn, spawnPos, Quaternion.identity);
                    spawnedVfx.SetActive(true);

                    // Tự động Play tất cả ParticleSystem trên VFX vừa spawn
                    var particles = spawnedVfx.GetComponentsInChildren<ParticleSystem>(true);
                    foreach (var ps in particles)
                    {
                        ps.gameObject.SetActive(true);
                        ps.Clear();
                        ps.Play();
                    }
                }

                // 4. Đứng yên chuyển phase nhưng vẫn luôn quay mặt về phía Player
                float elapsed = 0f;
                float durationSec = transitionDuration;
                while (elapsed < durationSec)
                {
                    Transform player = GetPlayerTransform();
                    if (player != null && currentObj != null)
                    {
                        float dirX = player.position.x - currentObj.transform.position.x;
                        if (Mathf.Abs(dirX) > 0.05f)
                        {
                            float sign = dirX > 0f ? -1f : 1f;
                            Vector3 scale = currentObj.transform.localScale;
                            scale.x = Mathf.Abs(scale.x) * sign;
                            currentObj.transform.localScale = scale;

                            if (currentBossPhase != null && currentBossPhase.transform != currentObj.transform)
                            {
                                currentBossPhase.SetFacingDirection(dirX);
                            }
                        }
                    }

                    elapsed += Time.deltaTime;
                    await Task.Yield();
                    if (this == null || ct.IsCancellationRequested || IsDefeated) return;
                }

                if (this == null || ct.IsCancellationRequested || IsDefeated) return;

                // 5. Dọn dẹp / Làm mờ dần (Fade Out) VFX mượt mà thay vì tắt phụt
                if (spawnedVfx != null)
                {
                    if (destroyVfxAfterTransition)
                    {
                        _ = FadeOutAndCleanVfxAsync(spawnedVfx, false, vfxFadeOutDuration);
                    }
                    spawnedVfx = null;
                }

                // 6. Tính toán Phase tiếp theo (vòng lặp 1 -> 2 -> 3 -> 4 -> 1...)
                int nextPhaseIndex = CurrentPhaseIndex + 1;
                if (nextPhaseIndex >= phaseObjects.Length)
                {
                    nextPhaseIndex = loopPhases ? 0 : phaseObjects.Length - 1;
                }

                // 7. Chuyển sang Phase mới và đồng bộ vị trí + hướng nhìn theo Player
                ActivatePhase(nextPhaseIndex, currentPosition);

                // 8. Phát Idle cho phase mới nếu có cài đặt
                if (playIdleOnNewPhase)
                {
                    GameObject newObj = phaseObjects[CurrentPhaseIndex];
                    if (newObj != null)
                    {
                        var newBossPhase = newObj.GetComponent<QuyVuongBossPhase>() ?? newObj.GetComponentInChildren<QuyVuongBossPhase>();
                        newBossPhase?.PlayIdle();
                    }
                }

                // 9. Mở khóa Phase mới: Cho phép Boss bắt đầu di chuyển và tấn công Player lại
                currentActiveFlow?.SetFrozen(false);
            }
            catch (OperationCanceledException)
            {
                if (spawnedVfx != null) Destroy(spawnedVfx);
            }
            catch (Exception ex)
            {
                if (spawnedVfx != null) Destroy(spawnedVfx);
                Debug.LogError($"[QuyVuongPhaseSwitcher] Lỗi khi thực hiện chuyển Phase: {ex.Message}");
            }
            finally
            {
                if (this != null)
                {
                    IsTransitioning = false;
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
                // 1. Dừng sinh particle mới trên tất cả ParticleSystem
                var particles = vfxObj.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in particles)
                {
                    if (ps != null)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }

                // 2. Thu thập các Light, SpriteRenderer, CanvasGroup để fade out
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

                        for (int i = 0; i < lights.Length; i++)
                        {
                            if (lights[i] != null)
                            {
                                lights[i].intensity = initialLightIntensities[i] * alphaFactor;
                            }
                        }

                        for (int i = 0; i < sprites.Length; i++)
                        {
                            if (sprites[i] != null)
                            {
                                Color c = initialSpriteColors[i];
                                c.a = initialSpriteColors[i].a * alphaFactor;
                                sprites[i].color = c;
                            }
                        }

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

                if (vfxObj != null)
                {
                    if (!isSceneInstance)
                    {
                        Destroy(vfxObj);
                    }
                    else
                    {
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
                Debug.LogWarning($"[QuyVuongPhaseSwitcher] Lỗi trong lúc FadeOut VFX: {ex.Message}");
                if (vfxObj != null)
                {
                    if (!isSceneInstance) Destroy(vfxObj);
                    else vfxObj.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Ẩn tất cả các Phase GameObjects của Boss.
        /// </summary>
        public void HideAllPhases()
        {
            if (phaseObjects != null)
            {
                foreach (var obj in phaseObjects)
                {
                    if (obj != null)
                    {
                        obj.SetActive(false);
                    }
                }
            }
        }

        /// <summary>
        /// Hiện lại Phase chỉ định (mặc định Phase 0) và reset trạng thái.
        /// </summary>
        public void ShowPhase(int phaseIndex = 0)
        {
            IsDefeated = false;
            ActivatePhase(phaseIndex);
        }

        /// <summary>
        /// Đánh bại và ẩn boss ngay lập tức (gọi từ Cutscene / UnityEvent / Debug).
        /// </summary>
        public void DefeatAndHideBoss()
        {
            HandleBossDied();
        }

        /// <summary>
        /// Cho phép gọi thủ công từ Inspector Button hoặc Script khác để nhảy sang Phase tiếp theo.
        /// </summary>
        public void SwitchToNextPhase()
        {
            if (!IsTransitioning && !IsDefeated)
            {
                _ = PerformPhaseTransitionAsync();
            }
        }

        /// <summary>
        /// Cho phép chuyển trực tiếp tới Phase mong muốn theo index (0 -> 3).
        /// </summary>
        public void SwitchToPhase(int phaseIndex)
        {
            if (!IsTransitioning && !IsDefeated)
            {
                Vector3 currentPosition = transform.position;
                float facingSign = 1f;
                GameObject currentObj = (phaseObjects != null && CurrentPhaseIndex < phaseObjects.Length) ? phaseObjects[CurrentPhaseIndex] : null;
                if (currentObj != null)
                {
                    currentPosition = currentObj.transform.position;
                    facingSign = Mathf.Sign(currentObj.transform.localScale.x);
                }
                ActivatePhase(phaseIndex, currentPosition, facingSign);
            }
        }

        #region Hurt Flash Implementation

        /// <summary>
        /// Kích hoạt hiệu ứng Hurt Flash (nhấp nháy đổi màu) trên Phase hiện tại của Boss theo cấu hình Inspector.
        /// </summary>
        public void TriggerHurtFlash()
        {
            TriggerHurtFlash(hurtFlashColor, hurtFlashDuration, hurtFlashCount);
        }

        /// <summary>
        /// Kích hoạt hiệu ứng Hurt Flash với màu sắc, thời lượng và số lần nhấp nháy tùy chỉnh.
        /// </summary>
        public void TriggerHurtFlash(Color color, float duration, int count = 1)
        {
            if (!enableHurtFlash || IsDefeated) return;

            StopHurtFlash();

            hurtFlashCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            _ = PerformHurtFlashAsync(color, duration, count, hurtFlashCts.Token);
        }

        /// <summary>
        /// Dừng ngay hiệu ứng Hurt Flash đang chạy và hủy Token.
        /// </summary>
        public void StopHurtFlash()
        {
            if (hurtFlashCts != null)
            {
                try
                {
                    hurtFlashCts.Cancel();
                    hurtFlashCts.Dispose();
                }
                catch { }
                finally
                {
                    hurtFlashCts = null;
                }
            }
        }

        /// <summary>
        /// Task bất đồng bộ xử lý vòng lặp nhấp nháy màu Hurt Flash trên Phase đang kích hoạt.
        /// </summary>
        private async Task PerformHurtFlashAsync(Color flashColor, float duration, int count, CancellationToken ct)
        {
            GameObject currentObj = GetCurrentActivePhaseObject();
            if (currentObj == null) return;

            int flashMs = Mathf.Max(10, Mathf.RoundToInt(duration * 1000f));
            int intervalMs = Mathf.Max(10, Mathf.RoundToInt(hurtFlashInterval * 1000f));
            int totalFlashes = Mathf.Max(1, count);

            try
            {
                for (int i = 0; i < totalFlashes; i++)
                {
                    if (ct.IsCancellationRequested || this == null || currentObj == null) break;

                    // Đổi màu sang Hurt Flash
                    ApplyColorToPhase(currentObj, flashColor, true);

                    await Task.Delay(flashMs, ct);

                    if (ct.IsCancellationRequested || this == null || currentObj == null) break;

                    // Khôi phục màu gốc bình thường (trắng)
                    ApplyColorToPhase(currentObj, Color.white, false);

                    if (i < totalFlashes - 1)
                    {
                        await Task.Delay(intervalMs, ct);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogWarning($"[QuyVuongPhaseSwitcher] Lỗi trong quá trình chạy Hurt Flash: {ex.Message}");
            }
            finally
            {
                if (currentObj != null)
                {
                    ApplyColorToPhase(currentObj, Color.white, false);
                }
            }
        }

        /// <summary>
        /// Áp dụng màu sắc lên tất cả các Renderer/Spine Skeleton của Phase GameObject được chỉ định.
        /// </summary>
        private void ApplyColorToPhase(GameObject phaseObj, Color color, bool isFlashActive)
        {
            if (phaseObj == null) return;

            // 1. QuyVuongBossPhase component (Spine wrapper)
            var bossPhaseComp = phaseObj.GetComponent<QuyVuongBossPhase>() ?? phaseObj.GetComponentInChildren<QuyVuongBossPhase>();
            if (bossPhaseComp != null)
            {
                bossPhaseComp.SetColor(color);
            }

            // 2. Spine SkeletonAnimation / SkeletonRenderer
            var skeletons = phaseObj.GetComponentsInChildren<Spine.Unity.SkeletonAnimation>(true);
            foreach (var skel in skeletons)
            {
                if (skel != null && skel.Skeleton != null)
                {
                    skel.Skeleton.SetColor(color);
                }
            }

            // 3. MaterialPropertyBlock với Shader Spine (_FillPhase / _FillColor) nếu được bật
            if (useSpineFillProperty)
            {
                var meshRenderers = phaseObj.GetComponentsInChildren<MeshRenderer>(true);
                int fillPhaseId = Shader.PropertyToID("_FillPhase");
                int fillColorId = Shader.PropertyToID("_FillColor");
                MaterialPropertyBlock mpb = new MaterialPropertyBlock();

                foreach (var mr in meshRenderers)
                {
                    if (mr != null)
                    {
                        mr.GetPropertyBlock(mpb);
                        if (isFlashActive)
                        {
                            mpb.SetColor(fillColorId, color);
                            mpb.SetFloat(fillPhaseId, 1f);
                        }
                        else
                        {
                            mpb.SetFloat(fillPhaseId, 0f);
                        }
                        mr.SetPropertyBlock(mpb);
                    }
                }
            }

            // 4. SpriteRenderer (nếu sử dụng sprite 2D thông thường)
            var spriteRenderers = phaseObj.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in spriteRenderers)
            {
                if (sr != null)
                {
                    sr.color = color;
                }
            }
        }

        /// <summary>
        /// Khôi phục màu gốc (Color.white) cho 1 Phase GameObject.
        /// </summary>
        private void ResetPhaseColor(GameObject phaseObj)
        {
            if (phaseObj == null) return;
            ApplyColorToPhase(phaseObj, Color.white, false);
        }

        /// <summary>
        /// Khôi phục màu gốc cho tất cả các Phase GameObjects trong danh sách.
        /// </summary>
        public void ResetAllPhaseColors()
        {
            if (phaseObjects != null)
            {
                foreach (var obj in phaseObjects)
                {
                    if (obj != null)
                    {
                        ResetPhaseColor(obj);
                    }
                }
            }
        }

        /// <summary>
        /// Lấy GameObject của Phase đang kích hoạt hiện tại.
        /// </summary>
        public GameObject GetCurrentActivePhaseObject()
        {
            if (phaseObjects != null && CurrentPhaseIndex >= 0 && CurrentPhaseIndex < phaseObjects.Length)
            {
                if (phaseObjects[CurrentPhaseIndex] != null && phaseObjects[CurrentPhaseIndex].activeInHierarchy)
                {
                    return phaseObjects[CurrentPhaseIndex];
                }
            }

            // Fallback: Tìm phase object bất kỳ đang active trong danh sách
            if (phaseObjects != null)
            {
                foreach (var obj in phaseObjects)
                {
                    if (obj != null && obj.activeInHierarchy) return obj;
                }
            }

            return gameObject;
        }

        #endregion
    }
}
