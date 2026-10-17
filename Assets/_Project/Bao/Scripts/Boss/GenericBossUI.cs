using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace Bao.Bosses
{
    public class GenericBossUI : MonoBehaviour
    {
        [Header("Boss Reference")]
        [Tooltip("Kéo BossUIEventDispatcher của quái vào đây")]
        [SerializeField] private BossUIEventDispatcher bossEvents;

        [Header("Multi-Layer Health Bar (Tùy Chỉnh Máu Đa Tầng x4/x3/x2/x1)")]
        [Tooltip("Bật/Tắt chế độ hiển thị máu nhiều tầng (x4, x3, x2, x1). Tích chọn để bật cho con quái này, bỏ tích nếu muốn dùng 1 thanh máu truyền thống.")]
        [SerializeField] private bool enableMultiLayerHealth = true;
        [Tooltip("Số tầng máu khi bật Multi-Layer (Mặc định: 4 tầng).")]
        [SerializeField, Range(1, 10)] private int healthLayersCount = 4;

        [Header("UI Panel Elements")]
        [SerializeField] private GameObject uiPanel;
        [SerializeField] private TMP_Text bossNameText;
        [SerializeField] private TMP_Text layerBadgeText;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private Slider mainSlider;
        [SerializeField] private Slider easeSlider;

        [Header("HP Bar Animation")]
        [SerializeField] private float easeSpeed = 1f;
        [SerializeField] private float hideDelayAfterDeath = 2f;

        [Header("Phase Colors")]
        [SerializeField] private Image fillImage;
        [SerializeField] private Color phase1Color = new Color(0.85f, 0.15f, 0.15f, 1f); // Đỏ
        [SerializeField] private Color phase2Color = new Color(1f, 0.5f, 0f, 1f); // Cam

        [Header("Optional Phase Color Palette")]
        [Tooltip("Khi bật, màu thanh máu được lấy theo index phase trong danh sách bên dưới.")]
        [SerializeField] private bool usePhaseColorPalette = false;
        [SerializeField] private Color[] phaseColors =
        {
            new Color(0.85f, 0.08f, 0.08f, 1f),
            new Color(0.05f, 0.45f, 0.95f, 1f),
            new Color(0.86f, 0.66f, 0.20f, 1f),
            new Color(0.20f, 0.75f, 0.25f, 1f),
            new Color(1.00f, 0.30f, 0.03f, 1f)
        };

        [Header("Boss Specific Presentation")]
        [SerializeField] private NidusCocoonHealthBarVFX nidusHealthBarVFX;
        [SerializeField] private DreadwormHealthBarVFX dreadwormHealthBarVFX;
        [SerializeField] private RedSlimeHealthBarVFX redSlimeHealthBarVFX;

        [Header("Optional Audio Override")]
        [Tooltip("Gán nhạc nền Boss tại đây nếu muốn cấu hình trực tiếp trên UI Prefab thay vì trên Boss GameObject.")]
        [SerializeField] private AudioClip bossBgmOverride;

        private Vector3 positionOffset;
        private bool hasOffsetInit = false;
        private Transform canvasTransform;
        
        private int targetHealth;
        private bool isDead = false;

        private bool isInitialized = false;

        public void SetBossEvents(BossUIEventDispatcher dispatcher)
        {
            bossEvents = dispatcher;
            if (bossEvents != null && bossBgmOverride != null && bossEvents.BossBGM == null)
            {
                bossEvents.SetBossBGM(bossBgmOverride);
            }
            InitializeUI();
        }

        private void Start()
        {
            if (!isInitialized)
            {
                if (bossEvents == null)
                {
                    bossEvents = GetComponentInParent<BossUIEventDispatcher>();
                    if (bossEvents == null) bossEvents = FindFirstObjectByType<BossUIEventDispatcher>();
                    if (bossEvents == null)
                    {
                        var redSlime = GetComponentInParent<RedSlimeBoss>();
                        if (redSlime == null) redSlime = FindFirstObjectByType<RedSlimeBoss>();
                        if (redSlime != null)
                        {
                            bossEvents = redSlime.GetComponent<BossUIEventDispatcher>();
                            if (bossEvents == null) bossEvents = redSlime.gameObject.AddComponent<BossUIEventDispatcher>();
                        }
                    }
                }
                InitializeUI();
            }
        }

        private void InitializeUI()
        {
            if (isInitialized) return;
            isInitialized = true;

            if (nidusHealthBarVFX == null)
                nidusHealthBarVFX = GetComponentInChildren<NidusCocoonHealthBarVFX>(true);
            if (dreadwormHealthBarVFX == null)
                dreadwormHealthBarVFX = GetComponentInChildren<DreadwormHealthBarVFX>(true);
            if (redSlimeHealthBarVFX == null)
            {
                redSlimeHealthBarVFX = GetComponentInChildren<RedSlimeHealthBarVFX>(true);
                if (redSlimeHealthBarVFX == null && (bossEvents != null && bossEvents.GetComponent<RedSlimeBoss>() != null || GetComponentInParent<RedSlimeBoss>() != null || FindFirstObjectByType<RedSlimeBoss>() != null))
                {
                    redSlimeHealthBarVFX = gameObject.AddComponent<RedSlimeHealthBarVFX>();
                }
            }

            RectTransform panelRect = uiPanel != null ? uiPanel.transform as RectTransform : transform as RectTransform;

            if (nidusHealthBarVFX != null)
            {
                nidusHealthBarVFX.Configure(mainSlider, fillImage, bossNameText, panelRect);
            }
            if (dreadwormHealthBarVFX != null)
            {
                dreadwormHealthBarVFX.Configure(mainSlider, fillImage, bossNameText, panelRect);
            }
            if (redSlimeHealthBarVFX != null)
            {
                // Đồng bộ cấu hình Multi-Layer từ BossUIEventDispatcher nếu có
                if (bossEvents != null)
                {
                    enableMultiLayerHealth = bossEvents.UseMultiLayerHealthBar;
                    healthLayersCount = bossEvents.MultiLayerCount;
                }
                redSlimeHealthBarVFX.SetMultiLayerMode(enableMultiLayerHealth, healthLayersCount);
                redSlimeHealthBarVFX.Configure(mainSlider, fillImage, bossNameText, panelRect, layerBadgeText, easeSlider);
            }

            if (uiPanel != null)
                uiPanel.SetActive(false);

            if (bossEvents != null)
            {
                bossEvents.OnInitialized += HandleInitialized;
                bossEvents.OnEngaged += HandleEngaged;
                bossEvents.OnHealthChanged += HandleHealthChanged;
                bossEvents.OnDied += HandleDied;
                bossEvents.OnPhaseChanged += HandlePhaseChanged;

                // The boss can finish Start()/Engage() before this detached UI has
                // registered its listeners. Replay the current state so the panel
                // is never left hidden just because it missed those one-shot events.
                if (bossEvents.MaxHealth > 0)
                {
                    HandleInitialized(bossEvents.BossName, bossEvents.MaxHealth);
                    HandleHealthChanged(bossEvents.CurrentHealth, bossEvents.MaxHealth);
                }

                if (bossEvents.IsEngaged)
                    HandleEngaged();
            }

            // Tách UI ra khỏi Boss chỉ khi là WorldSpace Canvas (để thanh máu trên đầu không bị lật theo scale của Boss)
            if (uiPanel != null && bossEvents != null)
            {
                Canvas rootCanvas = uiPanel.GetComponentInParent<Canvas>();
                if (rootCanvas != null && rootCanvas.renderMode == RenderMode.WorldSpace)
                {
                    canvasTransform = rootCanvas.rootCanvas.transform;
                    Vector3 currentOffset = canvasTransform.position - bossEvents.transform.position;
                    
                    if (bossEvents.transform.localScale.x < 0)
                        currentOffset.x = -currentOffset.x;

                    positionOffset = currentOffset;
                    hasOffsetInit = true;

                    canvasTransform.SetParent(null);
                    canvasTransform.rotation = Quaternion.identity;

                    Vector3 scale = canvasTransform.localScale;
                    scale.x = Mathf.Abs(scale.x);
                    scale.y = Mathf.Abs(scale.y);
                    scale.z = Mathf.Abs(scale.z);
                    canvasTransform.localScale = scale;
                }
            }
        }

        private void OnDestroy()
        {
            if (bossEvents != null)
            {
                bossEvents.OnInitialized -= HandleInitialized;
                bossEvents.OnEngaged -= HandleEngaged;
                bossEvents.OnHealthChanged -= HandleHealthChanged;
                bossEvents.OnDied -= HandleDied;
                bossEvents.OnPhaseChanged -= HandlePhaseChanged;
            }
        }

        private void HandleInitialized(string name, int maxHealth)
        {
            if (bossNameText != null) bossNameText.text = name;
            targetHealth = maxHealth;

            if (redSlimeHealthBarVFX != null)
            {
                if (fillImage != null) fillImage.color = Color.white;
                redSlimeHealthBarVFX.Initialize(maxHealth);
            }
            else
            {
                if (mainSlider != null) { mainSlider.maxValue = maxHealth; mainSlider.value = maxHealth; }
                if (easeSlider != null) { easeSlider.maxValue = maxHealth; easeSlider.value = maxHealth; }
                if (nidusHealthBarVFX != null || dreadwormHealthBarVFX != null)
                {
                    if (fillImage != null) fillImage.color = Color.white;
                    nidusHealthBarVFX?.Initialize(maxHealth);
                    dreadwormHealthBarVFX?.Initialize(maxHealth);
                }
                else if (fillImage != null)
                {
                    fillImage.color = ResolvePhaseColor(0);
                }
            }

            UpdateHealthText(maxHealth, maxHealth);
            isDead = false;
        }

        private void HandleEngaged()
        {
            if (uiPanel != null && !isDead)
            {
                uiPanel.SetActive(true);
            }

            // Đảm bảo nhạc Boss được kích hoạt nếu BossUIEventDispatcher chưa kích hoạt
            if (AudioManager.Instance != null && !AudioManager.Instance.IsBossMusicActive())
            {
                if (bossBgmOverride != null)
                {
                    AudioManager.Instance.PlayBossMusic(bossBgmOverride, true, 1.2f);
                }
                else if (bossEvents != null && bossEvents.BossBGM != null)
                {
                    AudioManager.Instance.PlayBossMusic(bossEvents.BossBGM, true, bossEvents.BgmFadeDuration);
                }
            }

            nidusHealthBarVFX?.PlayEngageReveal();
            dreadwormHealthBarVFX?.PlayEngageReveal();
            redSlimeHealthBarVFX?.PlayEngageReveal();
        }

        private void HandleHealthChanged(int currentHealth, int maxHealth)
        {
            targetHealth = currentHealth;
            if (redSlimeHealthBarVFX != null)
            {
                redSlimeHealthBarVFX.NotifyHealthChanged(currentHealth, maxHealth);
            }
            else
            {
                if (mainSlider != null)
                {
                    mainSlider.maxValue = maxHealth;
                    // mainSlider.value will be lerped in Update()
                }
                if (easeSlider != null)
                {
                    easeSlider.maxValue = maxHealth;
                }
                nidusHealthBarVFX?.NotifyHealthChanged(currentHealth, maxHealth);
                dreadwormHealthBarVFX?.NotifyHealthChanged(currentHealth, maxHealth);
            }
            UpdateHealthText(currentHealth, maxHealth);
        }

        private void HandleDied()
        {
            isDead = true;
            nidusHealthBarVFX?.PlayDeathCollapse();
            dreadwormHealthBarVFX?.PlayDeathCollapse();
            redSlimeHealthBarVFX?.PlayDeathCollapse();
            StartCoroutine(HideUIAfterDelay());
        }

        private void HandlePhaseChanged(int phaseIndex)
        {
            if (nidusHealthBarVFX != null || dreadwormHealthBarVFX != null || redSlimeHealthBarVFX != null)
            {
                nidusHealthBarVFX?.SetPhase(phaseIndex);
                dreadwormHealthBarVFX?.SetPhase(phaseIndex);
                redSlimeHealthBarVFX?.SetPhase(phaseIndex);
            }
            else if (fillImage != null)
            {
                fillImage.color = ResolvePhaseColor(phaseIndex);
            }
        }

        private Color ResolvePhaseColor(int phaseIndex)
        {
            if (usePhaseColorPalette && phaseColors != null && phaseColors.Length > 0)
            {
                int safeIndex = Mathf.Clamp(phaseIndex, 0, phaseColors.Length - 1);
                return phaseColors[safeIndex];
            }

            return phaseIndex == 2 ? phase2Color : phase1Color;
        }

        private void UpdateHealthText(int current, int max)
        {
            if (hpText != null)
                hpText.text = $"{Mathf.Max(0, current)} / {max}";
        }

        private void Update()
        {
            // Hiệu ứng chạy thanh máu (chỉ dùng cho boss thường không có RedSlimeHealthBarVFX)
            if (bossEvents != null && (bossEvents.IsEngaged || isDead) && redSlimeHealthBarVFX == null)
            {
                float target = isDead ? 0f : targetHealth;

                if (mainSlider != null)
                {
                    mainSlider.value = Mathf.Lerp(mainSlider.value, target, easeSpeed * 2f * Time.deltaTime);
                }

                if (easeSlider != null)
                {
                    easeSlider.value = Mathf.Lerp(easeSlider.value, target, easeSpeed * Time.deltaTime);
                }
            }
        }

        private void LateUpdate()
        {
            if (canvasTransform != null && bossEvents != null && hasOffsetInit)
            {
                Canvas rootCanvas = canvasTransform.GetComponent<Canvas>();
                if (rootCanvas != null && rootCanvas.renderMode == RenderMode.WorldSpace)
                {
                    // Toàn bộ Canvas bám theo boss chỉ khi là WorldSpace Canvas
                    Vector3 currentOffset = positionOffset;
                    if (bossEvents.transform.localScale.x < 0)
                    {
                        currentOffset.x = -currentOffset.x;
                    }
                    canvasTransform.position = bossEvents.transform.position + currentOffset;
                }
            }
        }

        private IEnumerator HideUIAfterDelay()
        {
            yield return new WaitForSeconds(hideDelayAfterDeath);
            if (uiPanel != null)
            {
                uiPanel.SetActive(false);
            }
        }
    }
}
