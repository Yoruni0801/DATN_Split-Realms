using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Duy.CameraSystem;

namespace Duy.FairySystem
{
    [DisallowMultipleComponent]
    public class FairyBossAbsorbController : MonoBehaviour
    {
        public static FairyBossAbsorbController Instance { get; private set; }

        [Header("Target Boss Settings")]
        [SerializeField] private Transform bossCenterTarget;
        [SerializeField] private Vector2 bossCenterOffset = new Vector2(0f, 1.5f);
        [SerializeField] private bool autoFindBossOnStart = true;
        [SerializeField] private string bossTag = "Boss";

        [Header("Ground Trigger Settings (Dừng rung sau 2s khi chạm đất & về Player)")]
        [SerializeField] private bool listenToGroundContact = true;
        [SerializeField] private float stopShakeDelayAfterGrounded = 2.0f;
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float groundCheckDistance = 1.0f;

        [Header("Trigger Option")]
        [SerializeField] private bool triggerOnEnable = false;
        [SerializeField] private float triggerDelay = 0.5f;

        [Header("Absorb Flight Settings (Quỹ đạo hút xoáy)")]
        [SerializeField] private float flyDuration = 1.6f;
        [SerializeField] private float spiralRadius = 2.0f;
        [SerializeField] private float spiralFrequency = 4.0f;
        [SerializeField] private float scaleDownDuration = 0.5f;
        [SerializeField] private float arrivalDistance = 0.35f;

        [Header("Visual & Feedback")]
        [SerializeField] private GameObject absorbVFXPrefab;
        [SerializeField] private AudioClip absorbSound;
        [SerializeField] private AudioClip absorbImpactSound;
        [SerializeField] private float shakeOnAbsorb = 1.2f;
        [SerializeField] private float shakeDuration = 0.4f;

        [Header("Events (Kích hoạt khi hoàn thành)")]
        [SerializeField] private UnityEvent onAbsorbStarted;
        [SerializeField] private UnityEvent onSingleFairyAbsorbed;
        [SerializeField] private UnityEvent onAllFairiesAbsorbed;
        [SerializeField] private UnityEvent onBossGrounded;

        public event Action OnAbsorbStarted;
        public event Action<FairyPlayerCompanionController> OnSingleFairyAbsorbed;
        public event Action OnAllFairiesAbsorbed;
        public event Action OnBossGrounded;

        private bool isAbsorbing;
        private bool hasHandledGrounded;
        private Coroutine absorbRoutine;
        private Coroutine groundStopShakeRoutine;
        private Quoc.QuyVuong.QuyVuongGroundCheck groundCheckComponent;

        public bool IsAbsorbing => isAbsorbing;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (bossCenterTarget == null && autoFindBossOnStart)
            {
                FindBossTarget();
            }

            ResolveBossGroundComponents();
        }

        private void OnEnable()
        {
            hasHandledGrounded = false;

            if (triggerOnEnable)
            {
                StartCoroutine(DelayedAutoStartRoutine());
            }
        }

        private void Update()
        {
            if (!listenToGroundContact || hasHandledGrounded) return;

            // Kiểm tra trạng thái tiếp đất của Quỷ Vương
            if (CheckIsBossGrounded())
            {
                OnBossHitGround();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void ResolveBossGroundComponents()
        {
            Transform target = bossCenterTarget != null ? bossCenterTarget : transform;
            groundCheckComponent = target.GetComponentInChildren<Quoc.QuyVuong.QuyVuongGroundCheck>(true);
        }

        private bool CheckIsBossGrounded()
        {
            // 1. Kiểm tra qua QuyVuongGroundCheck component nếu có
            if (groundCheckComponent != null && groundCheckComponent.IsGrounded)
            {
                return true;
            }

            // 2. Tự bắn Raycast kiểm tra ground layer
            Transform target = bossCenterTarget != null ? bossCenterTarget : transform;
            LayerMask mask = groundLayer != 0 ? groundLayer : PlayerLayerConfig.GroundLayer;
            RaycastHit2D hit = Physics2D.Raycast(target.position, Vector2.down, groundCheckDistance, mask);
            if (hit.collider != null)
            {
                return true;
            }

            return false;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!listenToGroundContact || hasHandledGrounded) return;

            if (collision.gameObject.CompareTag("Ground") || ((1 << collision.gameObject.layer) & groundLayer) != 0)
            {
                OnBossHitGround();
            }
        }

        /// <summary>
        /// Hàm gọi khi Quỷ Vương chạm đất: Tự động dừng rung sau 2s và lia camera quay về Player.
        /// </summary>
        public void OnBossHitGround()
        {
            if (hasHandledGrounded) return;
            hasHandledGrounded = true;

            Debug.Log("<color=yellow>[FairyBossAbsorbController] Quỷ Vương đã chạm đất! Chuẩn bị dừng rung và quay về Player sau 2 giây...</color>");

            onBossGrounded?.Invoke();
            OnBossGrounded?.Invoke();

            if (groundStopShakeRoutine != null)
            {
                StopCoroutine(groundStopShakeRoutine);
            }
            groundStopShakeRoutine = StartCoroutine(StopShakeAndReturnAfterDelayRoutine(stopShakeDelayAfterGrounded));
        }

        private IEnumerator StopShakeAndReturnAfterDelayRoutine(float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            // Dừng rung và trả Camera về Player
            if (AdvancedCameraController2D.Instance != null)
            {
                AdvancedCameraController2D.Instance.ReturnToPlayer();
            }
            else
            {
                AdvancedCameraController2D cam = FindFirstObjectByType<AdvancedCameraController2D>();
                if (cam != null)
                {
                    cam.ReturnToPlayer();
                }
            }

            groundStopShakeRoutine = null;
        }

        private IEnumerator DelayedAutoStartRoutine()
        {
            if (triggerDelay > 0f)
            {
                yield return new WaitForSeconds(triggerDelay);
            }
            StartAbsorbFairies();
        }

        #region Public Methods for UnityEvents / Timeline / Script Calls

        /// <summary>
        /// Kích hoạt kỹ năng hút và hấp thụ toàn bộ tinh linh về phía Quỷ Vương.
        /// </summary>
        public void StartAbsorbFairies()
        {
            StartAbsorbFairies(bossCenterTarget);
        }

        /// <summary>
        /// Kích hoạt hút tinh linh về vị trí mục tiêu Boss tùy chỉnh.
        /// </summary>
        public void StartAbsorbFairies(Transform customBossTarget)
        {
            if (isAbsorbing) return;

            if (customBossTarget != null)
            {
                bossCenterTarget = customBossTarget;
            }
            else if (bossCenterTarget == null)
            {
                FindBossTarget();
            }

            if (bossCenterTarget == null)
            {
                bossCenterTarget = transform;
            }

            ResolveBossGroundComponents();

            if (absorbRoutine != null)
            {
                StopCoroutine(absorbRoutine);
            }

            absorbRoutine = StartCoroutine(AbsorbAllFairiesRoutine());
        }

        public void SetBossTarget(Transform target)
        {
            bossCenterTarget = target;
            ResolveBossGroundComponents();
        }

        #endregion

        #region Absorb Routine

        private IEnumerator AbsorbAllFairiesRoutine()
        {
            isAbsorbing = true;
            onAbsorbStarted?.Invoke();
            OnAbsorbStarted?.Invoke();

            if (absorbSound != null)
            {
                AudioSource.PlayClipAtPoint(absorbSound, bossCenterTarget != null ? bossCenterTarget.position : transform.position);
            }

            // 1. Thu thập danh sách toàn bộ Tinh linh đang có
            List<FairyPlayerCompanionController> targetFairies = CollectAllFairies();

            if (targetFairies.Count == 0)
            {
                Debug.LogWarning("[FairyBossAbsorbController] Không tìm thấy Tinh linh nào trên Scene để hút.");
                isAbsorbing = false;
                onAllFairiesAbsorbed?.Invoke();
                OnAllFairiesAbsorbed?.Invoke();
                yield break;
            }

            // 2. Tắt chế độ điều khiển minigame (Possession) nếu đang mở
            FairyPossessionController.ReturnAllToPlayer();

            // 3. Khóa và ngắt logic di chuyển tự động của tất cả Tinh linh
            for (int i = 0; i < targetFairies.Count; i++)
            {
                var fairy = targetFairies[i];
                if (fairy == null) continue;

                fairy.SetCastingLocked(true);
                fairy.SetAutoFindPlayer(false);
                fairy.SetFollowOtherFairyEnabled(false);
                fairy.StopFollowingFairy();

                // Tắt component để script Fairy không ghi đè vị trí trong LateUpdate
                fairy.enabled = false;

                // Tắt collider và kinematic để bay tự do
                if (fairy.TryGetComponent(out Collider2D col)) col.enabled = false;
                if (fairy.TryGetComponent(out Rigidbody2D rb))
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.bodyType = RigidbodyType2D.Kinematic;
                }
            }

            // 4. Bắt đầu luồng hút đồng thời toàn bộ tinh linh theo vòng xoáy
            List<Coroutine> activeFairyRoutines = new List<Coroutine>();
            for (int i = 0; i < targetFairies.Count; i++)
            {
                var fairy = targetFairies[i];
                if (fairy != null)
                {
                    float angleOffset = (i * 360f / targetFairies.Count) * Mathf.Deg2Rad;
                    activeFairyRoutines.Add(StartCoroutine(AbsorbSingleFairyRoutine(fairy, angleOffset)));
                }
            }

            // 5. Đợi toàn bộ các tinh linh bị hút và biến mất hoàn toàn
            for (int i = 0; i < activeFairyRoutines.Count; i++)
            {
                if (activeFairyRoutines[i] != null)
                {
                    yield return activeFairyRoutines[i];
                }
            }

            isAbsorbing = false;
            absorbRoutine = null;

            Debug.Log("<color=magenta>[FairyBossAbsorbController] Quỷ Vương đã hấp thụ toàn bộ Tinh linh thành công!</color>");

            onAllFairiesAbsorbed?.Invoke();
            OnAllFairiesAbsorbed?.Invoke();
        }

        private IEnumerator AbsorbSingleFairyRoutine(FairyPlayerCompanionController fairy, float initialPhaseOffset)
        {
            if (fairy == null) yield break;

            Transform fairyTransform = fairy.transform;
            Vector3 startPos = fairyTransform.position;
            Vector3 startScale = fairyTransform.localScale;

            float elapsed = 0f;
            float duration = Mathf.Max(0.5f, flyDuration);

            while (elapsed < duration && fairyTransform != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                Vector3 bossPos = bossCenterTarget != null
                    ? bossCenterTarget.position + (Vector3)bossCenterOffset
                    : transform.position;
                bossPos.z = startPos.z;

                float currentSpiralRadius = Mathf.Lerp(spiralRadius, 0f, smoothT);
                float angle = (smoothT * spiralFrequency * Mathf.PI * 2f) + initialPhaseOffset;
                Vector3 spiralOffset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * currentSpiralRadius;

                Vector3 basePos = Vector3.Lerp(startPos, bossPos, smoothT);
                fairyTransform.position = basePos + spiralOffset;

                Vector3 dirToBoss = (bossPos - fairyTransform.position).normalized;
                if (dirToBoss.sqrMagnitude > 0.001f)
                {
                    fairy.FaceDirection(dirToBoss);
                }

                float scaleT = Mathf.Clamp01((t - (1f - scaleDownDuration)) / Mathf.Max(0.01f, scaleDownDuration));
                fairyTransform.localScale = Vector3.Lerp(startScale, Vector3.zero, Mathf.SmoothStep(0f, 1f, scaleT));

                if (Vector2.Distance(fairyTransform.position, bossPos) <= arrivalDistance && t >= 0.85f)
                {
                    break;
                }

                yield return null;
            }

            if (fairy != null && fairy.gameObject != null)
            {
                Vector3 impactPos = fairy.transform.position;

                if (absorbVFXPrefab != null)
                {
                    Instantiate(absorbVFXPrefab, impactPos, Quaternion.identity);
                }

                if (absorbImpactSound != null)
                {
                    AudioSource.PlayClipAtPoint(absorbImpactSound, impactPos);
                }

                if (shakeOnAbsorb > 0f)
                {
                    TriggerCameraShake(shakeDuration, shakeOnAbsorb);
                }

                onSingleFairyAbsorbed?.Invoke();
                OnSingleFairyAbsorbed?.Invoke(fairy);

                Destroy(fairy.gameObject);
            }
        }

        #endregion

        #region Helper Methods

        private List<FairyPlayerCompanionController> CollectAllFairies()
        {
            List<FairyPlayerCompanionController> result = new List<FairyPlayerCompanionController>();

            var staticList = FairyPlayerCompanionController.AllActiveFairies;
            if (staticList != null)
            {
                for (int i = 0; i < staticList.Count; i++)
                {
                    if (staticList[i] != null && !result.Contains(staticList[i]))
                    {
                        result.Add(staticList[i]);
                    }
                }
            }

            FairyPlayerCompanionController[] sceneFairies = FindObjectsByType<FairyPlayerCompanionController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < sceneFairies.Length; i++)
            {
                if (sceneFairies[i] != null && !result.Contains(sceneFairies[i]))
                {
                    result.Add(sceneFairies[i]);
                }
            }

            return result;
        }

        private void FindBossTarget()
        {
            if (!string.IsNullOrEmpty(bossTag))
            {
                GameObject bossObj = GameObject.FindGameObjectWithTag(bossTag);
                if (bossObj != null)
                {
                    bossCenterTarget = bossObj.transform;
                    return;
                }
            }

            GameObject foundObj = GameObject.Find("Quy_Vuong");
            if (foundObj == null) foundObj = GameObject.Find("Boss");
            if (foundObj != null)
            {
                bossCenterTarget = foundObj.transform;
            }
        }

        private void TriggerCameraShake(float duration, float magnitude)
        {
            if (AdvancedCameraController2D.Instance != null)
            {
                AdvancedCameraController2D.Instance.Shake(duration, magnitude);
                return;
            }

            if (CameraController.Instance != null)
            {
                CameraController.Instance.ShakeCamera(duration, magnitude);
            }
        }

        #endregion

        private void OnDrawGizmosSelected()
        {
            Vector3 center = bossCenterTarget != null
                ? bossCenterTarget.position + (Vector3)bossCenterOffset
                : transform.position + (Vector3)bossCenterOffset;

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(center, spiralRadius);
            Gizmos.DrawLine(center + Vector3.left * 0.5f, center + Vector3.right * 0.5f);
            Gizmos.DrawLine(center + Vector3.down * 0.5f, center + Vector3.up * 0.5f);

            Transform target = bossCenterTarget != null ? bossCenterTarget : transform;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(target.position, target.position + Vector3.down * groundCheckDistance);
        }
    }
}
