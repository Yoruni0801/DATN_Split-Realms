using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace Bao.Bosses
{
    public class NidusBossUI : MonoBehaviour
    {
        [Header("Boss Reference")]
        [SerializeField] private NidusCocoonBoss boss;

        [Header("UI Panel Elements")]
        [SerializeField] private GameObject uiPanel;
        [SerializeField] private TMP_Text bossNameText;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private Slider mainSlider;
        [SerializeField] private Slider easeSlider;

        [Header("HP Bar Animation")]
        [SerializeField] private float easeSpeed = 2f;
        [SerializeField] private float hideDelayAfterDeath = 2f;

        private bool isBossEngaged = false;
        private bool hasInitialized = false;

        private Vector3 positionOffset;
        private bool hasOffsetInit = false;
        private Transform canvasTransform;

        private void Start()
        {
            if (uiPanel != null)
            {
                uiPanel.SetActive(false);
            }

            if (boss == null)
            {
                boss = FindFirstObjectByType<NidusCocoonBoss>();
            }

            if (boss != null && bossNameText != null)
            {
                bossNameText.text = "Nidus Cocoon";
            }

            // Tách UI ra khỏi Boss để không bao giờ bị lật.
            // PHẢI tách cả Canvas gốc ra, chứ không thể tách uiPanel vì UI cần phải nằm trong Canvas.
            if (uiPanel != null && boss != null)
            {
                Canvas rootCanvas = uiPanel.GetComponentInParent<Canvas>();
                if (rootCanvas != null)
                {
                    canvasTransform = rootCanvas.rootCanvas.transform;

                    Vector3 currentOffset = canvasTransform.position - boss.transform.position;
                    
                    // Nếu boss đang bị lật lúc game bắt đầu, lưu lại offset gốc
                    if (boss.transform.localScale.x < 0)
                    {
                        currentOffset.x = -currentOffset.x;
                    }
                    positionOffset = currentOffset;
                    hasOffsetInit = true;

                    canvasTransform.SetParent(null);
                    canvasTransform.rotation = Quaternion.identity;

                    // Cố định scale luôn dương
                    Vector3 scale = canvasTransform.localScale;
                    scale.x = Mathf.Abs(scale.x);
                    scale.y = Mathf.Abs(scale.y);
                    scale.z = Mathf.Abs(scale.z);
                    canvasTransform.localScale = scale;
                }
            }
        }

        private void Update()
        {
            if (boss == null) return;

            // Initialize slider bounds once boss is available
            if (!hasInitialized)
            {
                mainSlider.maxValue = boss.MaxHealth;
                mainSlider.value = boss.CurrentHealth;

                if (easeSlider != null)
                {
                    easeSlider.maxValue = boss.MaxHealth;
                    easeSlider.value = boss.CurrentHealth;
                }

                hasInitialized = true;
            }

            // Detect when combat starts (boss leaves IdleState)
            if (!isBossEngaged && boss.CurrentState != null && !(boss.CurrentState is NidusIdleState))
            {
                isBossEngaged = true;
                if (uiPanel != null)
                {
                    uiPanel.SetActive(true);
                }
            }

            // Update UI sliders and text
            if (isBossEngaged)
            {
                if (mainSlider != null)
                {
                    mainSlider.value = boss.CurrentHealth;
                }

                if (easeSlider != null)
                {
                    easeSlider.value = Mathf.Lerp(easeSlider.value, boss.CurrentHealth, easeSpeed * Time.deltaTime);
                }

                if (hpText != null)
                {
                    hpText.text = $"{Mathf.Max(0, boss.CurrentHealth)} / {boss.MaxHealth}";
                }

                // If boss dies, trigger fade out delay
                if (boss.IsDead)
                {
                    isBossEngaged = false;
                    StartCoroutine(HideUIAfterDelay());
                }
            }
        }

        private void LateUpdate()
        {
            if (canvasTransform != null && boss != null && hasOffsetInit)
            {
                // Toàn bộ Canvas bám theo boss
                Vector3 currentOffset = positionOffset;
                
                // Đổi hướng offset nếu boss quay đầu
                if (boss.transform.localScale.x < 0)
                {
                    currentOffset.x = -currentOffset.x;
                }
                
                canvasTransform.position = boss.transform.position + currentOffset;
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
