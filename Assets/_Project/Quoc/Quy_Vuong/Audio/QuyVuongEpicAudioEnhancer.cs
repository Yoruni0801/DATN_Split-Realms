using System.Collections;
using UnityEngine;
using Quoc.QuyVuong;

namespace Quoc.QuyVuong.Audio
{
    /// <summary>
    /// Component nâng cấp trải nghiệm âm thanh điện ảnh cho Trùm Cuối (Final Boss):
    /// - Quản lý hiệu ứng Sub-Bass siêu trầm làm rung chuyển đấu trường.
    /// - Đồng bộ rung màn hình (Camera Shake) theo nhịp âm thanh đòn đánh hủy diệt.
    /// - Hiệu ứng nhịp tim căng thẳng (Heartbeat Tension) khi Boss ở mức HP nguy kịch.
    /// - An toàn tuyệt đối 100%: Kiểm tra activeInHierarchy trước khi StartCoroutine, không bao giờ bị lỗi GameObject Inactive.
    /// </summary>
    [RequireComponent(typeof(QuyVuongAudioController))]
    public class QuyVuongEpicAudioEnhancer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private QuyVuongAudioController audioController;
        [SerializeField] private QuyVuongBossHealth bossHealth;

        [Header("Epic Final Boss Settings")]
        [SerializeField] private EpicFinalBossAudioData epicAudioData = new EpicFinalBossAudioData();

        [Header("Screen Shake Integration (Tùy chọn)")]
        [Tooltip("Tự động rung camera khi có va chạm Sub-bass cực mạnh")]
        [SerializeField] private bool triggerScreenShake = true;
        [SerializeField] private float defaultShakeIntensity = 0.35f;
        [SerializeField] private float defaultShakeDuration = 0.3f;

        private AudioSource subBassAudioSource;
        private AudioSource heartbeatAudioSource;
        private Coroutine heartbeatCoroutine;
        private bool isLowHpActive = false;

        private void Awake()
        {
            if (epicAudioData == null)
            {
                epicAudioData = new EpicFinalBossAudioData();
            }

            if (audioController == null)
            {
                audioController = GetComponent<QuyVuongAudioController>();
            }

            if (bossHealth == null)
            {
                bossHealth = GetComponent<QuyVuongBossHealth>() ?? GetComponentInParent<QuyVuongBossHealth>() ?? GetComponentInChildren<QuyVuongBossHealth>();
            }

            InitializeSources();
        }

        private void InitializeSources()
        {
            // Sub-Bass AudioSource
            Transform existingSubBass = transform.Find("SubBass_Impact_Source");
            GameObject subBassChild = existingSubBass != null ? existingSubBass.gameObject : new GameObject("SubBass_Impact_Source");
            subBassChild.transform.SetParent(transform);
            subBassChild.transform.localPosition = Vector3.zero;

            subBassAudioSource = subBassChild.GetComponent<AudioSource>();
            if (subBassAudioSource == null)
            {
                subBassAudioSource = subBassChild.AddComponent<AudioSource>();
            }
            subBassAudioSource.playOnAwake = false;
            subBassAudioSource.loop = false;
            subBassAudioSource.spatialBlend = 0f;

            // Heartbeat AudioSource
            Transform existingHeartbeat = transform.Find("Heartbeat_Tension_Source");
            GameObject heartbeatChild = existingHeartbeat != null ? existingHeartbeat.gameObject : new GameObject("Heartbeat_Tension_Source");
            heartbeatChild.transform.SetParent(transform);
            heartbeatChild.transform.localPosition = Vector3.zero;

            heartbeatAudioSource = heartbeatChild.GetComponent<AudioSource>();
            if (heartbeatAudioSource == null)
            {
                heartbeatAudioSource = heartbeatChild.AddComponent<AudioSource>();
            }
            heartbeatAudioSource.playOnAwake = false;
            heartbeatAudioSource.loop = true;
            heartbeatAudioSource.spatialBlend = 0f;
            heartbeatAudioSource.volume = 0f;
        }

        private void OnEnable()
        {
            if (bossHealth != null)
            {
                bossHealth.OnHealthChanged -= CheckLowHpTension;
                bossHealth.OnHealthChanged += CheckLowHpTension;

                bossHealth.OnDied -= HandleBossDied;
                bossHealth.OnDied += HandleBossDied;
            }
        }

        private void OnDisable()
        {
            if (bossHealth != null)
            {
                bossHealth.OnHealthChanged -= CheckLowHpTension;
                bossHealth.OnDied -= HandleBossDied;
            }
            StopHeartbeatTension(instant: true);
        }

        private void CheckLowHpTension(int currentHealth, int maxHealth)
        {
            if (maxHealth <= 0 || epicAudioData == null) return;
            float hpPercent = (float)currentHealth / maxHealth;

            if (hpPercent <= epicAudioData.lowHpThreshold && !isLowHpActive && currentHealth > 0)
            {
                isLowHpActive = true;
                StartHeartbeatTension();
            }
            else if (hpPercent > epicAudioData.lowHpThreshold && isLowHpActive)
            {
                isLowHpActive = false;
                StopHeartbeatTension(instant: false);
            }
        }

        private void HandleBossDied()
        {
            isLowHpActive = false;
            StopHeartbeatTension(instant: true);
        }

        public void PlayHeavySubBassImpact(float volume = 1f, bool shakeCam = true)
        {
            if (epicAudioData != null && epicAudioData.subBassImpactClip != null && subBassAudioSource != null)
            {
                subBassAudioSource.pitch = Random.Range(0.92f, 1.05f);
                subBassAudioSource.PlayOneShot(epicAudioData.subBassImpactClip, volume);
            }

            if (shakeCam && triggerScreenShake && gameObject.activeInHierarchy)
            {
                ApplyScreenShake(defaultShakeIntensity, defaultShakeDuration);
            }
        }

        public void StartHeartbeatTension()
        {
            if (epicAudioData == null || epicAudioData.heartbeatTensionClip == null || heartbeatAudioSource == null) return;

            if (heartbeatCoroutine != null) StopCoroutine(heartbeatCoroutine);

            if (gameObject.activeInHierarchy)
            {
                heartbeatCoroutine = StartCoroutine(FadeInHeartbeatRoutine());
            }
            else
            {
                heartbeatAudioSource.clip = epicAudioData.heartbeatTensionClip;
                heartbeatAudioSource.volume = 0.7f;
                heartbeatAudioSource.Play();
            }
        }

        public void StopHeartbeatTension(bool instant = false)
        {
            if (heartbeatAudioSource == null) return;

            if (heartbeatCoroutine != null)
            {
                StopCoroutine(heartbeatCoroutine);
                heartbeatCoroutine = null;
            }

            if (instant || !gameObject.activeInHierarchy)
            {
                heartbeatAudioSource.volume = 0f;
                heartbeatAudioSource.Stop();
            }
            else
            {
                heartbeatCoroutine = StartCoroutine(FadeOutHeartbeatRoutine());
            }
        }

        private IEnumerator FadeInHeartbeatRoutine()
        {
            if (epicAudioData == null || heartbeatAudioSource == null) yield break;

            heartbeatAudioSource.clip = epicAudioData.heartbeatTensionClip;
            heartbeatAudioSource.Play();
            float elapsed = 0f;
            float duration = 1.5f;

            while (elapsed < duration)
            {
                if (heartbeatAudioSource == null) yield break;
                elapsed += Time.deltaTime;
                heartbeatAudioSource.volume = Mathf.Lerp(0f, 0.7f, elapsed / duration);
                yield return null;
            }
            if (heartbeatAudioSource != null) heartbeatAudioSource.volume = 0.7f;
        }

        private IEnumerator FadeOutHeartbeatRoutine()
        {
            if (heartbeatAudioSource == null) yield break;

            float startVol = heartbeatAudioSource.volume;
            float elapsed = 0f;
            float duration = 1.0f;

            while (elapsed < duration)
            {
                if (heartbeatAudioSource == null) yield break;
                elapsed += Time.deltaTime;
                heartbeatAudioSource.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
                yield return null;
            }
            if (heartbeatAudioSource != null)
            {
                heartbeatAudioSource.volume = 0f;
                heartbeatAudioSource.Stop();
            }
        }

        private void ApplyScreenShake(float intensity, float duration)
        {
            if (!gameObject.activeInHierarchy) return;

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                StartCoroutine(DoSimpleCameraShake(mainCam.transform, intensity, duration));
            }
        }

        private IEnumerator DoSimpleCameraShake(Transform camTransform, float intensity, float duration)
        {
            if (camTransform == null) yield break;

            Vector3 originalPos = camTransform.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (camTransform == null) yield break;
                elapsed += Time.deltaTime;
                float currentIntensity = Mathf.Lerp(intensity, 0f, elapsed / duration);
                Vector2 randomOffset = Random.insideUnitCircle * currentIntensity;
                camTransform.localPosition = new Vector3(originalPos.x + randomOffset.x, originalPos.y + randomOffset.y, originalPos.z);
                yield return null;
            }

            if (camTransform != null) camTransform.localPosition = originalPos;
        }
    }
}
