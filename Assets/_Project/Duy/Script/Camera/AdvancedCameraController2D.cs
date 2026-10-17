using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

namespace Duy.CameraSystem
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(9999)]
    public class AdvancedCameraController2D : MonoBehaviour
    {
        public static AdvancedCameraController2D Instance { get; private set; }

        [Header("Target & Focus Settings")]
        [SerializeField] private Transform defaultFocusTarget;
        [SerializeField] private Vector2 focusOffset = Vector2.zero;
        [SerializeField] private float fixedCameraZ = -10f;

        [Header("Cinemachine & Unity Camera References")]
        [SerializeField] private CinemachineCamera cinemachineCamera;
        [SerializeField] private Camera unityCamera;

        [Header("Focus Parameters")]
        [SerializeField] private float defaultFocusZoomSize = 6.0f;
        [SerializeField] private float defaultFocusMoveDuration = 0.5f;

        [Header("Focus Shake Settings (Rung liên tục cực mạnh trong lúc Focus)")]
        [SerializeField] private bool shakeWhileFocusing = true;
        [SerializeField] private float focusShakeMagnitude = 2.2f;
        [SerializeField] private float focusShakeFrequency = 45f;
        [SerializeField] private float focusShakeRotationAngle = 2.5f; // Độ xoay nghiêng Z tạo cảm giác chấn động mạnh

        [Header("Zoom Settings")]
        [SerializeField] private float defaultOrthoSize = 8.5f;
        [SerializeField] private float zoomSpeed = 6f;

        [Header("Camera Events (UnityEvent cho Inspector)")]
        [SerializeField] private UnityEvent onFocusStarted;
        [SerializeField] private UnityEvent onFocusCompleted;
        [SerializeField] private UnityEvent onReturnToPlayerStarted;
        [SerializeField] private UnityEvent onReturnToPlayerCompleted;

        private Transform originalCinemachineFollow;
        private Transform activeFocusTarget;
        private float currentOrthoSize;
        private float targetOrthoSize;
        private bool isFocusing;
        private Vector3 currentFocusWorldPos;
        private Coroutine focusCoroutine;
        private Coroutine returnCoroutine;
        private Coroutine shakeCoroutine;
        private Coroutine zoomCoroutine;

        // Custom standalone shake state
        private float customShakeTimer;
        private float customShakeDuration;
        private float customShakeMagnitude;
        private float customShakeFrequency;

        public bool IsFocusing => isFocusing;
        public float CurrentOrthoSize => currentOrthoSize;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            ResolveCameraReferences();

            currentOrthoSize = defaultOrthoSize;
            targetOrthoSize = defaultOrthoSize;
            ApplyOrthoSize(defaultOrthoSize);
            EnforceCameraZ();
        }

        private void Start()
        {
            ResolveCameraReferences();
            EnforceCameraZ();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void LateUpdate()
        {
            ResolveCameraReferences();

            Vector3 finalShakeOffset = Vector3.zero;
            float finalRotationZ = 0f;

            // 1. Tính toán độ rung camera
            // Nếu đang trong trạng thái Focus và có bật shakeWhileFocusing -> Rung LIÊN TỤC cực mạnh
            if (isFocusing && shakeWhileFocusing)
            {
                float seedTime = Time.time * focusShakeFrequency;
                float noiseX = (Mathf.PerlinNoise(seedTime, 0.15f) * 2f - 1f) * focusShakeMagnitude;
                float noiseY = (Mathf.PerlinNoise(0.45f, seedTime) * 2f - 1f) * focusShakeMagnitude;
                float jitterX = UnityEngine.Random.Range(-0.35f, 0.35f) * focusShakeMagnitude;
                float jitterY = UnityEngine.Random.Range(-0.35f, 0.35f) * focusShakeMagnitude;

                finalShakeOffset = new Vector3(noiseX + jitterX, noiseY + jitterY, 0f);
                finalRotationZ = (Mathf.PerlinNoise(seedTime, 0.85f) * 2f - 1f) * focusShakeRotationAngle;
            }
            // Rung tùy chỉnh độc lập (nếu có gọi hàm Shake rời)
            else if (customShakeTimer > 0f)
            {
                customShakeTimer -= Time.deltaTime;
                float progress = Mathf.Clamp01(customShakeTimer / Mathf.Max(0.01f, customShakeDuration));
                float damp = Mathf.Pow(progress, 1.5f);

                float seedTime = Time.time * customShakeFrequency;
                float noiseX = (Mathf.PerlinNoise(seedTime, 0.15f) * 2f - 1f) * customShakeMagnitude * damp;
                float noiseY = (Mathf.PerlinNoise(0.45f, seedTime) * 2f - 1f) * customShakeMagnitude * damp;
                float jitterX = UnityEngine.Random.Range(-0.25f, 0.25f) * customShakeMagnitude * damp;
                float jitterY = UnityEngine.Random.Range(-0.25f, 0.25f) * customShakeMagnitude * damp;

                finalShakeOffset = new Vector3(noiseX + jitterX, noiseY + jitterY, 0f);
                finalRotationZ = (Mathf.PerlinNoise(seedTime, 0.85f) * 2f - 1f) * (focusShakeRotationAngle * damp);
            }

            // 2. Cập nhật vị trí & zoom Camera
            if (isFocusing)
            {
                UpdateCameraZoom();

                // Cập nhật bám theo Boss (nếu Boss di chuyển và KHÔNG đang trong quá trình Return)
                if (activeFocusTarget != null && focusCoroutine == null && returnCoroutine == null)
                {
                    Vector3 targetDest = activeFocusTarget.position + (Vector3)focusOffset;
                    currentFocusWorldPos = Vector3.Lerp(currentFocusWorldPos, targetDest, 10f * Time.deltaTime);
                }

                Vector3 finalPos = new Vector3(
                    currentFocusWorldPos.x + finalShakeOffset.x,
                    currentFocusWorldPos.y + finalShakeOffset.y,
                    fixedCameraZ
                );
                Quaternion finalRot = Quaternion.Euler(0f, 0f, finalRotationZ);

                if (unityCamera != null)
                {
                    unityCamera.transform.position = finalPos;
                    unityCamera.transform.rotation = finalRot;
                }

                if (cinemachineCamera != null)
                {
                    cinemachineCamera.transform.position = finalPos;
                    cinemachineCamera.transform.rotation = finalRot;
                    cinemachineCamera.ForceCameraPosition(finalPos, finalRot);
                }

                transform.position = finalPos;
                transform.rotation = finalRot;
            }
            else
            {
                // Khi không focus (đã trả về Player) -> Cinemachine tự động bám theo Player.
                // Chỉ can thiệp nếu có rung rời (custom shake)
                if (finalShakeOffset.sqrMagnitude > 0.00001f || Mathf.Abs(finalRotationZ) > 0.001f)
                {
                    Quaternion rot = Quaternion.Euler(0f, 0f, finalRotationZ);
                    if (unityCamera != null)
                    {
                        Vector3 pos = unityCamera.transform.position + finalShakeOffset;
                        pos.z = fixedCameraZ;
                        unityCamera.transform.position = pos;
                        unityCamera.transform.rotation = rot;
                    }
                    if (cinemachineCamera != null)
                    {
                        Vector3 pos = cinemachineCamera.transform.position + finalShakeOffset;
                        pos.z = fixedCameraZ;
                        cinemachineCamera.transform.position = pos;
                        cinemachineCamera.transform.rotation = rot;
                    }
                }
                else
                {
                    // Trả lại góc xoay chuẩn 0
                    if (unityCamera != null && unityCamera.transform.rotation != Quaternion.identity)
                    {
                        unityCamera.transform.rotation = Quaternion.identity;
                    }
                    if (cinemachineCamera != null && cinemachineCamera.transform.rotation != Quaternion.identity)
                    {
                        cinemachineCamera.transform.rotation = Quaternion.identity;
                    }
                }
            }

            EnforceCameraZ();
        }

        private void EnforceCameraZ()
        {
            if (unityCamera != null)
            {
                Vector3 camPos = unityCamera.transform.position;
                if (Mathf.Abs(camPos.z - fixedCameraZ) > 0.001f)
                {
                    camPos.z = fixedCameraZ;
                    unityCamera.transform.position = camPos;
                }
            }

            if (cinemachineCamera != null)
            {
                Vector3 vcamPos = cinemachineCamera.transform.position;
                if (Mathf.Abs(vcamPos.z - fixedCameraZ) > 0.001f)
                {
                    vcamPos.z = fixedCameraZ;
                    cinemachineCamera.transform.position = vcamPos;
                }
            }

            Vector3 myPos = transform.position;
            if (Mathf.Abs(myPos.z - fixedCameraZ) > 0.001f)
            {
                myPos.z = fixedCameraZ;
                transform.position = myPos;
            }
        }

        #region Core Zoom

        private void UpdateCameraZoom()
        {
            if (Mathf.Abs(currentOrthoSize - targetOrthoSize) > 0.001f)
            {
                currentOrthoSize = Mathf.Lerp(currentOrthoSize, targetOrthoSize, zoomSpeed * Time.deltaTime);
                ApplyOrthoSize(currentOrthoSize);
            }
        }

        private void ApplyOrthoSize(float size)
        {
            if (unityCamera != null)
            {
                unityCamera.orthographicSize = size;
            }

            if (cinemachineCamera != null)
            {
                LensSettings lens = cinemachineCamera.Lens;
                lens.OrthographicSize = size;
                cinemachineCamera.Lens = lens;
            }
        }

        #endregion

        #region Public Methods - FOCUS (Chỉ quay về khi gọi hàm Return)

        /// <summary>
        /// Focus vào Boss/Target mặc định cài sẵn, RUNG MẠNH LIÊN TỤC và giữ nguyên cho đến khi gọi ReturnToPlayer.
        /// </summary>
        public void FocusBoss()
        {
            FocusDefaultTarget();
        }

        /// <summary>
        /// Focus vào Target mặc định cài sẵn, RUNG MẠNH LIÊN TỤC và giữ nguyên cho đến khi gọi ReturnToPlayer.
        /// </summary>
        public void FocusDefaultTarget()
        {
            if (defaultFocusTarget != null)
            {
                FocusOnTarget(defaultFocusTarget, defaultFocusZoomSize, defaultFocusMoveDuration);
            }
        }

        /// <summary>
        /// Focus vào GameObject truyền vào, rung mạnh liên tục và giữ nguyên cho đến khi gọi ReturnToPlayer.
        /// </summary>
        public void FocusTarget(GameObject targetObject)
        {
            if (targetObject != null)
            {
                FocusOnTarget(targetObject.transform, defaultFocusZoomSize, defaultFocusMoveDuration);
            }
        }

        /// <summary>
        /// Focus vào Transform truyền vào, rung mạnh liên tục và giữ nguyên cho đến khi gọi ReturnToPlayer.
        /// </summary>
        public void FocusTargetTransform(Transform targetTransform)
        {
            if (targetTransform != null)
            {
                FocusOnTarget(targetTransform, defaultFocusZoomSize, defaultFocusMoveDuration);
            }
        }

        /// <summary>
        /// Hàm Focus chính qua Code: Lia camera tới mục tiêu, bắt đầu rung liên tục và GIỮ NGUYÊN trạng thái Focus.
        /// </summary>
        public void FocusOnTarget(Transform target, float zoomSize = 0f, float moveDuration = 0.5f, Action onComplete = null)
        {
            if (target == null) target = defaultFocusTarget;
            if (target == null) return;

            activeFocusTarget = target;

            if (returnCoroutine != null)
            {
                StopCoroutine(returnCoroutine);
                returnCoroutine = null;
            }

            if (focusCoroutine != null)
            {
                StopCoroutine(focusCoroutine);
            }

            float targetZoom = zoomSize > 0f ? zoomSize : defaultFocusZoomSize;
            float moveDur = moveDuration > 0f ? moveDuration : defaultFocusMoveDuration;

            focusCoroutine = StartCoroutine(FocusRoutine(target, targetZoom, moveDur, onComplete));
        }

        private IEnumerator FocusRoutine(Transform target, float zoomSize, float moveDuration, Action onComplete)
        {
            isFocusing = true;
            onFocusStarted?.Invoke();

            // Tạm ngắt Follow của Cinemachine để script toàn quyền điều khiển Camera
            if (cinemachineCamera != null && originalCinemachineFollow == null)
            {
                originalCinemachineFollow = cinemachineCamera.Follow;
                cinemachineCamera.Follow = null;
            }

            targetOrthoSize = zoomSize;

            Vector3 startPos = unityCamera != null ? unityCamera.transform.position : transform.position;
            startPos.z = fixedCameraZ;
            currentFocusWorldPos = startPos;

            float startSize = currentOrthoSize;
            float elapsed = 0f;

            if (moveDuration > 0f)
            {
                while (elapsed < moveDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / moveDuration));

                    Vector3 targetPos = target != null ? target.position + (Vector3)focusOffset : startPos;
                    targetPos.z = fixedCameraZ;

                    currentFocusWorldPos = Vector3.Lerp(startPos, targetPos, t);
                    currentFocusWorldPos.z = fixedCameraZ;

                    currentOrthoSize = Mathf.Lerp(startSize, targetOrthoSize, t);
                    ApplyOrthoSize(currentOrthoSize);

                    yield return null;
                }
            }

            if (target != null)
            {
                currentFocusWorldPos = target.position + (Vector3)focusOffset;
                currentFocusWorldPos.z = fixedCameraZ;
            }

            onFocusCompleted?.Invoke();
            onComplete?.Invoke();

            focusCoroutine = null;
            // Ở lại trạng thái isFocusing = true và RUNG LIÊN TỤC cho tới khi gọi ReturnToPlayer!
        }

        #endregion

        #region Public Methods - RETURN TO PLAYER (Chỉ quay về khi gọi các hàm này)

        /// <summary>
        /// HÀM QUAY VỀ PLAYER (0 tham số, gán trực tiếp vào UnityEvent / Inspector).
        /// Lia camera mượt mà về Player (1 giây), tắt rung và trả lại Follow cho Cinemachine.
        /// </summary>
        public void ReturnToPlayer()
        {
            ReturnToPlayer(1.0f, null);
        }

        /// <summary>
        /// Quay về Player mượt mà (1 giây).
        /// </summary>
        public void ReturnToPlayerSmooth()
        {
            ReturnToPlayer(1.0f, null);
        }

        /// <summary>
        /// Quay về Player ngay lập tức không có thời gian chuyển tiếp.
        /// </summary>
        public void ReturnToPlayerImmediate()
        {
            ReturnToPlayer(0f, null);
        }

        /// <summary>
        /// Quay về Player với thời gian chuyển tiếp tùy chỉnh (giây).
        /// </summary>
        public void ReturnToPlayerWithDuration(float duration)
        {
            ReturnToPlayer(duration, null);
        }

        /// <summary>
        /// Hàm trả camera về Player chính thức.
        /// </summary>
        public void ReturnToPlayer(float returnDuration = 1.0f, Action onComplete = null)
        {
            if (focusCoroutine != null)
            {
                StopCoroutine(focusCoroutine);
                focusCoroutine = null;
            }

            if (returnCoroutine != null)
            {
                StopCoroutine(returnCoroutine);
            }

            returnCoroutine = StartCoroutine(ReturnRoutine(returnDuration, onComplete));
        }

        private IEnumerator ReturnRoutine(float returnDuration, Action onComplete)
        {
            onReturnToPlayerStarted?.Invoke();
            Transform playerTarget = FindPlayerTransform();

            targetOrthoSize = defaultOrthoSize;
            Vector3 startPos = currentFocusWorldPos;
            startPos.z = fixedCameraZ;
            float startSize = currentOrthoSize;
            float elapsed = 0f;

            if (returnDuration > 0f && playerTarget != null)
            {
                while (elapsed < returnDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / returnDuration));

                    Vector3 targetPos = playerTarget.position;
                    targetPos.z = fixedCameraZ;

                    currentFocusWorldPos = Vector3.Lerp(startPos, targetPos, t);
                    currentFocusWorldPos.z = fixedCameraZ;

                    currentOrthoSize = Mathf.Lerp(startSize, defaultOrthoSize, t);
                    ApplyOrthoSize(currentOrthoSize);

                    yield return null;
                }
            }

            // Đặt trực tiếp vị trí camera về Player
            if (playerTarget != null)
            {
                Vector3 endPos = playerTarget.position;
                endPos.z = fixedCameraZ;
                currentFocusWorldPos = endPos;
                if (unityCamera != null) unityCamera.transform.position = endPos;
                if (cinemachineCamera != null) cinemachineCamera.transform.position = endPos;
            }

            // Trả lại Follow ban đầu cho Cinemachine
            if (cinemachineCamera != null)
            {
                cinemachineCamera.Follow = originalCinemachineFollow != null ? originalCinemachineFollow : playerTarget;
            }

            // Đặt lại rotation thẳng đứng 0
            if (unityCamera != null) unityCamera.transform.rotation = Quaternion.identity;
            if (cinemachineCamera != null) cinemachineCamera.transform.rotation = Quaternion.identity;
            transform.rotation = Quaternion.identity;

            isFocusing = false;
            activeFocusTarget = null;
            originalCinemachineFollow = null;

            onReturnToPlayerCompleted?.Invoke();
            onComplete?.Invoke();
            returnCoroutine = null;
        }

        #endregion

        #region Public Methods - SHAKE & ZOOM ĐỘC LẬP

        /// <summary>
        /// Kích hoạt rung cực mạnh rời (nếu muốn rung ở ngoài lúc focus).
        /// </summary>
        public void Shake(float duration, float magnitude = 2.5f, float frequency = 45f)
        {
            if (duration <= 0f || magnitude <= 0f) return;

            customShakeDuration = duration;
            customShakeTimer = duration;
            customShakeMagnitude = magnitude;
            customShakeFrequency = frequency > 0f ? frequency : focusShakeFrequency;
        }

        public void ShakeHeavy()
        {
            Shake(1.5f, 2.8f, 45f);
        }

        public void ShakeExtreme()
        {
            Shake(2.5f, 4.0f, 50f);
        }

        public void SetZoom(float size, float duration = 0.5f)
        {
            if (duration <= 0f)
            {
                targetOrthoSize = size;
                currentOrthoSize = size;
                ApplyOrthoSize(size);
                return;
            }

            if (zoomCoroutine != null)
            {
                StopCoroutine(zoomCoroutine);
            }

            zoomCoroutine = StartCoroutine(ZoomRoutine(size, duration));
        }

        private IEnumerator ZoomRoutine(float targetSize, float duration)
        {
            float startSize = currentOrthoSize;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                currentOrthoSize = Mathf.Lerp(startSize, targetSize, t);
                ApplyOrthoSize(currentOrthoSize);
                yield return null;
            }

            currentOrthoSize = targetSize;
            targetOrthoSize = targetSize;
            ApplyOrthoSize(targetSize);
            zoomCoroutine = null;
        }

        public void ResetZoom()
        {
            SetZoom(defaultOrthoSize, 0.5f);
        }

        #endregion

        #region Helper Methods

        private Transform FindPlayerTransform()
        {
            if (PlayerRegistry.Current != null)
            {
                return PlayerRegistry.Current.transform;
            }

            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                return playerObj.transform;
            }

            Player playerComp = FindFirstObjectByType<Player>();
            if (playerComp != null)
            {
                return playerComp.transform;
            }

            return null;
        }

        private void ResolveCameraReferences()
        {
            if (unityCamera == null)
            {
                unityCamera = GetComponent<Camera>();
                if (unityCamera == null)
                {
                    unityCamera = Camera.main;
                }
            }

            if (cinemachineCamera == null)
            {
                cinemachineCamera = GetComponent<CinemachineCamera>();
                if (cinemachineCamera == null)
                {
                    cinemachineCamera = FindFirstObjectByType<CinemachineCamera>();
                }
            }
        }

        #endregion
    }
}
