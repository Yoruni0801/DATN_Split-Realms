using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace EnemySystem
{
    public class AshwingBossUI : MonoBehaviour
    {
        [Header("Boss Reference")]
        [SerializeField] private StandaloneFlyingShooterEnemy boss;

        [Header("UI Panel Elements")]
        [SerializeField] private GameObject uiPanel;
        [SerializeField] private TMP_Text bossNameText;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private Slider mainSlider;
        [SerializeField] private Slider easeSlider;

        [Header("HP Bar Animation")]
        [SerializeField] private float easeSpeed = 1f; // Tốc độ làm đầy của thanh HP
        [SerializeField] private float fadeOutDuration = 0.5f; // Tốc độ ẩn của thanh HP sau khi boss chết

        private CanvasGroup canvasGroup;
        private bool isBossEngaged = false;
        private bool hasInitialized = false;
        private Bao.Bosses.Ashwing.AshwingBossCombat ashwingCombat;
        private float displayedHealth = -1f;
        private float damageHoldUntil;

        private void Start()
        {
            if (uiPanel != null)
            {
                uiPanel.SetActive(false);
                canvasGroup = uiPanel.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = uiPanel.AddComponent<CanvasGroup>();
                }
            }

            FindBoss();

            if (boss != null && bossNameText != null)
            {
                bossNameText.text = "Ashwing";
            }
        }

        private void FindBoss()
        {
            if (boss == null)
            {
                boss = GetComponentInParent<StandaloneFlyingShooterEnemy>();
                if (boss == null)
                {
                    boss = FindFirstObjectByType<StandaloneFlyingShooterEnemy>();
                }
            }
            if (boss != null) ashwingCombat = boss.GetComponent<Bao.Bosses.Ashwing.AshwingBossCombat>();
        }

        private void Update()
        {
            if (boss == null)
            {
                FindBoss();
                if (boss == null)
                {
                    // Nếu không tìm thấy boss nào (boss đã bị Destroy), tắt UI luôn
                    if (uiPanel != null && uiPanel.activeSelf)
                    {
                        uiPanel.SetActive(false);
                        isBossEngaged = false;
                    }
                    return;
                }
            }

            // Safe Init - only if mainSlider is assigned
            if (!hasInitialized)
            {
                if (mainSlider != null)
                {
                    mainSlider.maxValue = boss.MaxHealth;
                    mainSlider.value = boss.CurrentHealth;
                }

                if (easeSlider != null)
                {
                    easeSlider.maxValue = boss.MaxHealth;
                    easeSlider.value = boss.CurrentHealth;
                }

                if (hpText != null)
                {
                    hpText.text = $"{Mathf.Max(0, boss.CurrentHealth)} / {boss.MaxHealth}";
                }

                hasInitialized = true;
            }

            // Detect combat start (Assuming Ashwing is engaged when its health is less than max or it's not dead yet)
            // You can also link this to your TimelineEnemyAggroController if you want it to show up only after timeline
            bool combatVisible = ashwingCombat == null || ashwingCombat.IsEngaged;
            if (!isBossEngaged && !boss.IsDead && combatVisible)
            {
                isBossEngaged = true;
                if (uiPanel != null)
                {
                    if (canvasGroup != null) canvasGroup.alpha = 1f;
                    uiPanel.SetActive(true);
                }
            }

            // Luôn cập nhật thanh máu (ngay cả khi đang mờ dần)
            float targetHealth = boss.IsDead ? 0f : boss.CurrentHealth;
            if (targetHealth < displayedHealth) damageHoldUntil = Time.time + .28f;
            displayedHealth = targetHealth;
            if (ashwingCombat != null)
            {
                if (mainSlider != null && mainSlider.fillRect != null)
                {
                    var fillImage = mainSlider.fillRect.GetComponent<Image>();
                    if (fillImage != null) fillImage.color = ashwingCombat.Enraged
                        ? new Color(1f, .24f, .075f) : new Color(1f, .58f, .18f);
                }
                if (!combatVisible && !boss.IsDead && uiPanel != null)
                {
                    uiPanel.SetActive(false);
                    isBossEngaged = false;
                }
            }

            if (mainSlider != null)
            {
                mainSlider.value = Mathf.Lerp(mainSlider.value, targetHealth,
                    ashwingCombat != null ? 1f - Mathf.Exp(-18f * Time.deltaTime) : easeSpeed * 2f * Time.deltaTime);
            }

            if (easeSlider != null && Time.time >= damageHoldUntil)
            {
                easeSlider.value = Mathf.Lerp(easeSlider.value, targetHealth, easeSpeed * Time.deltaTime);
            }

            if (hpText != null)
            {
                hpText.text = $"{Mathf.Max(0, (int)targetHealth)} / {boss.MaxHealth}";
            }

            if (isBossEngaged && boss.IsDead)
            {
                isBossEngaged = false;
                StartCoroutine(HideUIAfterDelay());
            }
        }

        private IEnumerator HideUIAfterDelay()
        {
            float elapsed = 0f;
            if (canvasGroup != null)
            {
                float startAlpha = canvasGroup.alpha;
                while (elapsed < fadeOutDuration)
                {
                    elapsed += Time.deltaTime;
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeOutDuration);
                    yield return null;
                }
                canvasGroup.alpha = 0f;
            }
            else
            {
                yield return new WaitForSeconds(fadeOutDuration);
            }

            if (uiPanel != null)
            {
                uiPanel.SetActive(false);
            }
        }
    }
}
