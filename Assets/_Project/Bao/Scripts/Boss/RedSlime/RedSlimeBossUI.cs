using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace Bao.Bosses
{
    public class RedSlimeBossUI : MonoBehaviour
    {
        [Header("Boss Reference")]
        [SerializeField] private RedSlimeBoss boss;

        [Header("UI Panel Elements")]
        [SerializeField] private GameObject uiPanel;
        [SerializeField] private TMP_Text bossNameText;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private Slider mainSlider;
        [SerializeField] private Slider easeSlider;

        [Header("HP Bar Animation")]
        [SerializeField] private float easeSpeed = 1f;
        [SerializeField] private float hideDelayAfterDeath = 2f;

        private bool isBossEngaged = false;
        private bool hasInitialized = false;

        private void Start()
        {
            if (uiPanel != null)
            {
                uiPanel.SetActive(false);
            }

            FindBoss();

            if (boss != null && bossNameText != null)
            {
                bossNameText.text = "Red Slime";
            }
        }

        private void FindBoss()
        {
            if (boss == null)
            {
                boss = GetComponentInParent<RedSlimeBoss>();
                if (boss == null) boss = FindFirstObjectByType<RedSlimeBoss>();
            }
        }

        private void Update()
        {
            if (boss == null)
            {
                FindBoss();
                if (boss == null) return;
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

            // Detect combat start
            if (!isBossEngaged && boss.CurrentState != null && !(boss.CurrentState is RedSlimeIdleState))
            {
                isBossEngaged = true;
                if (uiPanel != null)
                {
                    uiPanel.SetActive(true);
                }
            }

            // Luôn cập nhật thanh máu
            float targetHealth = boss.IsDead ? 0f : boss.CurrentHealth;

            if (mainSlider != null)
            {
                mainSlider.value = Mathf.Lerp(mainSlider.value, targetHealth, easeSpeed * 2f * Time.deltaTime);
            }

            if (easeSlider != null)
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

        private void LateUpdate()
        {
            if (uiPanel != null && boss != null)
            {
                Vector3 localScale = uiPanel.transform.localScale;
                if (uiPanel.transform.IsChildOf(boss.transform))
                {
                    localScale.x = Mathf.Abs(localScale.x) * Mathf.Sign(boss.transform.localScale.x);
                }
                else
                {
                    localScale.x = Mathf.Abs(localScale.x);
                }
                uiPanel.transform.localScale = localScale;
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
