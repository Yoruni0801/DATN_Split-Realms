using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using System.Collections;
using System.Collections.Generic;
using System;

[RequireComponent(typeof(FairyPlayerCompanionController))]
public sealed class FairyPossessionController : MonoBehaviour
{
    private FairyPlayerCompanionController fairy;
    private CinemachineCamera[] cameras;
    private CinemachineBrain brain;

    [Header("Input")]
    [SerializeField] private Key possessionKey = Key.F;

    [Header("Camera")]
    [SerializeField] private CinemachineCamera targetCamera;
    [SerializeField] private bool snapCameraOnSwap = false;

    [Header("Camera Zoom on Possession")]
    [SerializeField] private bool zoomOnPossession = true;
    [SerializeField] private float fairyZoomFactor = 1.8f;
    [SerializeField] private float zoomDuration = 0.5f;

    // Quản lý trạng thái chuyển đổi
    private static bool isPossessionActive = false;
    private static bool hasSeparatedFairies = false;
    private static int currentFairyIndex = 0;
    private static int lastSwapFrame = -1;
    private static Transform currentTargetTransform;
    private static bool isSwapLocked = false;

    private static float originalCameraSize = 6.5f;
    private static bool hasCapturedOriginalCameraSize = false;
    private static Coroutine cameraZoomRoutine = null;
    private static FairyPossessionController activeInstance;

    public static bool IsPossessionActive => isPossessionActive;
    public static bool IsSwapLocked => isSwapLocked;
    public static void LockSwap() => isSwapLocked = true;
    public static void UnlockSwap() => isSwapLocked = false;
    [SerializeField] private bool returnToPlayerOnAwake = false;
    public static int CurrentFairyIndex => currentFairyIndex;
    public static event Action<bool> GlobalPossessionStateChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        isPossessionActive = false;
        hasSeparatedFairies = false;
        currentFairyIndex = 0;
        lastSwapFrame = -1;
        currentTargetTransform = null;
        isSwapLocked = false;
        hasCapturedOriginalCameraSize = false;
        cameraZoomRoutine = null;
        activeInstance = null;
        GlobalPossessionStateChanged = null;
    }

    private void Awake()
    {
        fairy = GetComponent<FairyPlayerCompanionController>();
        cameras = FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        brain = FindFirstObjectByType<CinemachineBrain>();

        if (returnToPlayerOnAwake)
        {
            ReturnToPlayer();
        }
    }

    private void OnEnable()
    {
        activeInstance = this;
        if (fairy != null)
        {
            fairy.OnFairyDied += HandleFairyDied;
        }
    }

    private void OnDisable()
    {
        if (activeInstance == this) activeInstance = null;
        if (fairy != null)
        {
            fairy.OnFairyDied -= HandleFairyDied;
        }
    }

    private void OnDestroy()
    {
        if (activeInstance == this) activeInstance = null;
        if (fairy != null)
        {
            fairy.OnFairyDied -= HandleFairyDied;
        }

        isPossessionActive = false;
        hasSeparatedFairies = false;
        currentFairyIndex = 0;
        lastSwapFrame = -1;
        currentTargetTransform = null;
    }

    private void HandleFairyDied()
    {
        // Khi chết, reset cờ tách để các tinh linh minigame lại bay chung nhau
        hasSeparatedFairies = false;
        var fairies = FairyPlayerCompanionController.AllActiveFairies;
        for (int i = 0; i < fairies.Count; i++)
        {
            if (fairies[i] != null && fairies[i].GetComponent<FairyPossessionController>() != null)
            {
                fairies[i].SetFollowOtherFairyEnabled(true);
            }
        }
    }

    private void Update()
    {
        // Chỉ cho phép bấm F đổi qua lại giữa các Tinh linh khi đang ở chế độ Possession và không bị khóa
        if (isPossessionActive && !isSwapLocked && Keyboard.current != null && Keyboard.current[possessionKey].wasPressedThisFrame)
        {
            bool busy = fairy.IsInteractingWithLever;
            if (!busy && Time.frameCount != lastSwapFrame)
            {
                lastSwapFrame = Time.frameCount;
                CycleFairyOnly();
            }
        }

        if (fairy.IsPossessed)
        {
            HandleFairyInput();
        }
    }

    /// <summary>
    /// Bắt đầu chuyển quyền điều khiển từ Player sang Tinh linh.
    /// </summary>
    public void StartFairyPossession()
    {
        StartFairyPossessionInternal(0);
    }

    /// <summary>
    /// Bắt đầu điều khiển Tinh linh với một chỉ số cụ thể (0 = con thứ nhất, 1 = con thứ hai,...).
    /// </summary>
    public void StartFairyPossession(int initialFairyIndex)
    {
        StartFairyPossessionInternal(initialFairyIndex);
    }

    /// <summary>
    /// Trả quyền điều khiển và Camera về lại cho Player.
    /// </summary>
    public void ReturnToPlayer()
    {
        Player player = ResolvePlayer();
        if (player == null)
        {
            Debug.LogWarning("[FairyPossession] Không tìm thấy Player để trả quyền điều khiển.");
            return;
        }

        isPossessionActive = false;
        GlobalPossessionStateChanged?.Invoke(false);
        hasSeparatedFairies = false;

        var fairies = FairyPlayerCompanionController.AllActiveFairies;

        for (int i = 0; i < fairies.Count; i++)
        {
            if (fairies[i] == null) continue;

            // Chỉ xử lý những tinh linh thuộc quyền quản lý của FairyPossessionController
            if (fairies[i].GetComponent<FairyPossessionController>() == null) continue;

            fairies[i].SetPossessed(false);
            fairies[i].GetComponent<FairyAudioController>()?.FadeOutAndStop(0.5f);

            var capturedFairy = fairies[i];
            capturedFairy.OnRecallCompleted += MakeFairyRecallHandler(capturedFairy);
            fairies[i].BeginRecall();
        }

        // Mở khóa Player
        player.SetControlLocked(false);

        // Chuyển Camera quay về theo dõi Player
        currentTargetTransform = player.transform;
        ApplyTargetToCamera(currentTargetTransform, snapCameraOnSwap);

        // Zoom Camera về kích thước Player ban đầu
        if (zoomOnPossession)
        {
            ApplyCameraZoom(originalCameraSize, zoomDuration);
        }

        Debug.Log($"[FairyPossession] Đã trả quyền điều khiển và Camera về Player ({player.name}).");
    }

    private Action MakeFairyRecallHandler(FairyPlayerCompanionController f)
    {
        Action handler = null;
        handler = () =>
        {
            f.OnRecallCompleted -= handler;
            f.SetFollowOtherFairyEnabled(true);
        };
        return handler;
    }

    private void StartFairyPossessionInternal(int targetIndex)
    {
        Player player = ResolvePlayer();
        if (player == null)
        {
            Debug.LogWarning("[FairyPossession] Không tìm thấy Player để chuyển sang Tinh linh.");
            return;
        }

        var allFairies = FairyPlayerCompanionController.AllActiveFairies;
        var switchableFairies = new List<FairyPlayerCompanionController>();
        for (int i = 0; i < allFairies.Count; i++)
        {
            if (allFairies[i] != null && allFairies[i].GetComponent<FairyPossessionController>() != null)
            {
                switchableFairies.Add(allFairies[i]);
            }
        }

        if (switchableFairies.Count == 0)
        {
            Debug.LogWarning("[FairyPossession] Không có Tinh linh nào có FairyPossessionController trong scene.");
            return;
        }

        isPossessionActive = true;
        GlobalPossessionStateChanged?.Invoke(true);
        player.SetControlLocked(true);

        CaptureOriginalCameraSizeIfNeeded();

        if (!hasSeparatedFairies)
        {
            // Cả 2 cùng nhận chung input và cùng di chuyển (chỉ áp dụng cho fairy có FairyPossessionController)
            for (int i = 0; i < switchableFairies.Count; i++)
            {
                switchableFairies[i].SetPossessed(true);
                switchableFairies[i].SetFollowOtherFairyEnabled(false);
            }
            currentFairyIndex = Mathf.Clamp(targetIndex, 0, switchableFairies.Count - 1);
            if (switchableFairies[currentFairyIndex] != null)
            {
                currentTargetTransform = switchableFairies[currentFairyIndex].transform;
                ApplyTargetToCamera(currentTargetTransform, snapCameraOnSwap);
            }
        }
        else
        {
            // Đã tách: Chỉ điều khiển 1 con theo index (switchable index)
            int switchableIndex = Mathf.Clamp(targetIndex, 0, switchableFairies.Count - 1);
            var targetFairy = switchableFairies[switchableIndex];

            // Đồng bộ currentFairyIndex sang global index
            currentFairyIndex = 0;
            for (int i = 0; i < allFairies.Count; i++)
            {
                if (allFairies[i] == targetFairy) { currentFairyIndex = i; break; }
            }

            ApplyFairyPossessionState(targetFairy);
        }

        // Phóng to / Zoom Camera sang kích thước của Tinh linh (Zoom Out = Camera gốc * fairyZoomFactor)
        if (zoomOnPossession)
        {
            float targetSize = originalCameraSize * Mathf.Max(1.01f, fairyZoomFactor);
            ApplyCameraZoom(targetSize, zoomDuration);
        }
    }

    /// <summary>
    /// Chuyển đổi vòng tròn chỉ giữa các Tinh linh với nhau.
    /// Chỉ cycle qua Tinh linh có component FairyPossessionController.
    /// </summary>
    private void CycleFairyOnly()
    {
        var allFairies = FairyPlayerCompanionController.AllActiveFairies;

        // Lọc chỉ lấy fairy có FairyPossessionController
        var switchableFairies = new System.Collections.Generic.List<FairyPlayerCompanionController>();
        for (int i = 0; i < allFairies.Count; i++)
        {
            if (allFairies[i] != null && allFairies[i].GetComponent<FairyPossessionController>() != null)
                switchableFairies.Add(allFairies[i]);
        }

        if (switchableFairies.Count <= 1) return;

        hasSeparatedFairies = true;

        // Khi tách: tắt tự động bám đuôi nhau để mỗi con đứng độc lập
        for (int i = 0; i < allFairies.Count; i++)
        {
            if (allFairies[i] != null)
                allFairies[i].SetFollowOtherFairyEnabled(false);
        }

        // Tìm index hiện tại trong danh sách switchable
        int currentSwitchableIndex = 0;
        FairyPlayerCompanionController currentFairy = currentFairyIndex < allFairies.Count ? allFairies[currentFairyIndex] : null;
        for (int i = 0; i < switchableFairies.Count; i++)
        {
            if (switchableFairies[i] == currentFairy) { currentSwitchableIndex = i; break; }
        }

        int nextSwitchableIndex = (currentSwitchableIndex + 1) % switchableFairies.Count;
        FairyPlayerCompanionController nextFairy = switchableFairies[nextSwitchableIndex];

        // Tìm global index của fairy tiếp theo để đồng bộ currentFairyIndex
        currentFairyIndex = 0;
        for (int i = 0; i < allFairies.Count; i++)
        {
            if (allFairies[i] == nextFairy) { currentFairyIndex = i; break; }
        }

        ApplyFairyPossessionState(nextFairy);
    }

    private void ApplyFairyPossessionState(FairyPlayerCompanionController targetFairy)
    {
        var allFairies = FairyPlayerCompanionController.AllActiveFairies;
        var switchableFairies = new List<FairyPlayerCompanionController>();
        for (int i = 0; i < allFairies.Count; i++)
        {
            if (allFairies[i] != null && allFairies[i].GetComponent<FairyPossessionController>() != null)
            {
                switchableFairies.Add(allFairies[i]);
            }
        }

        FairyPlayerCompanionController activeFairy = null;

        for (int i = 0; i < switchableFairies.Count; i++)
        {
            bool isTarget = (switchableFairies[i] == targetFairy);
            switchableFairies[i].SetPossessed(isTarget);
            if (isTarget)
            {
                activeFairy = switchableFairies[i];
            }
        }

        if (activeFairy != null)
        {
            currentTargetTransform = activeFairy.transform;
            ApplyTargetToCamera(currentTargetTransform, snapCameraOnSwap);
            activeFairy.GetComponent<FairyAudioController>()?.PlayPossessSwitchSound();
            Debug.Log($"[FairyPossession] Đã chuyển quyền điều khiển và Camera sang: Tinh linh [{activeFairy.CurrentElement}] ({activeFairy.name})");
        }
    }

    private Player ResolvePlayer()
    {
        if (fairy != null && fairy.ActivePlayer != null)
        {
            return fairy.ActivePlayer;
        }

        return PlayerRegistry.Current != null ? PlayerRegistry.Current : FindFirstObjectByType<Player>();
    }

    private void ApplyTargetToCamera(Transform targetTransform, bool snap)
    {
        if (targetTransform == null) return;

        if (targetCamera != null)
        {
            SetCamTarget(targetCamera, targetTransform, snap);
            return;
        }

        if (cameras != null && cameras.Length > 0)
        {
            for (int i = 0; i < cameras.Length; i++)
            {
                SetCamTarget(cameras[i], targetTransform, snap);
            }
        }

        if (brain != null && brain.ActiveVirtualCamera is CinemachineCamera activeCam)
        {
            SetCamTarget(activeCam, targetTransform, snap);
        }
    }

    private void SetCamTarget(CinemachineCamera cam, Transform targetTransform, bool snap)
    {
        if (cam == null || targetTransform == null) return;

        CameraTarget newTarget = new CameraTarget
        {
            TrackingTarget = targetTransform,
            CustomLookAtTarget = true,
            LookAtTarget = targetTransform
        };

        cam.Target = newTarget;
        cam.Follow = targetTransform;
        cam.LookAt = targetTransform;

        if (snap)
        {
            cam.PreviousStateIsValid = false;
        }
    }

    private void CaptureOriginalCameraSizeIfNeeded()
    {
        if (hasCapturedOriginalCameraSize) return;

        CinemachineCamera activeCam = targetCamera;
        if (activeCam == null && brain != null)
        {
            activeCam = brain.ActiveVirtualCamera as CinemachineCamera;
        }
        if (activeCam == null && cameras != null && cameras.Length > 0)
        {
            activeCam = cameras[0];
        }

        if (activeCam != null)
        {
            originalCameraSize = activeCam.Lens.OrthographicSize;
            hasCapturedOriginalCameraSize = true;
        }
    }

    private void ApplyCameraZoom(float targetSize, float duration)
    {
        if (duration <= 0f)
        {
            SetAllCamerasSize(targetSize);
            return;
        }

        if (cameraZoomRoutine != null)
        {
            StopCoroutine(cameraZoomRoutine);
        }
        cameraZoomRoutine = StartCoroutine(CameraZoomRoutine(targetSize, duration));
    }

    private IEnumerator CameraZoomRoutine(float targetSize, float duration)
    {
        float startSize = GetCurrentCameraSize();
        float elapsed = 0f;

        Debug.Log($"[FairyPossession] Camera Zooming: Start Size = {startSize} -> Target Size = {targetSize}");

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            SetAllCamerasSize(Mathf.Lerp(startSize, targetSize, t));
            yield return null;
        }

        SetAllCamerasSize(targetSize);
        cameraZoomRoutine = null;
    }

    private float GetCurrentCameraSize()
    {
        if (targetCamera != null) return targetCamera.Lens.OrthographicSize;
        if (brain != null && brain.ActiveVirtualCamera is CinemachineCamera activeCam) return activeCam.Lens.OrthographicSize;
        if (cameras != null && cameras.Length > 0 && cameras[0] != null) return cameras[0].Lens.OrthographicSize;
        return originalCameraSize;
    }

    private void SetAllCamerasSize(float size)
    {
        if (targetCamera != null)
        {
            var lens = targetCamera.Lens;
            lens.OrthographicSize = size;
            targetCamera.Lens = lens;
            return;
        }

        if (cameras != null)
        {
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] == null) continue;
                var lens = cameras[i].Lens;
                lens.OrthographicSize = size;
                cameras[i].Lens = lens;
            }
        }
    }

    // --- Camera Pan API (Dùng cho cutscene / puzzle) ---

    private Transform savedCameraTarget;
    private float savedCameraSize;

    public static void ReturnAllToPlayer()
    {
        if (activeInstance != null)
        {
            activeInstance.ReturnToPlayer();
        }
        else
        {
            var inst = FindFirstObjectByType<FairyPossessionController>();
            if (inst != null)
            {
                inst.ReturnToPlayer();
            }
        }
    }

    public static void PanActiveCamera(Transform target, float zoomSize, float duration)
    {
        if (activeInstance != null)
        {
            activeInstance.PanCameraTo(target, zoomSize, duration);
        }
        else
        {
            var inst = FindFirstObjectByType<FairyPossessionController>();
            if (inst != null)
            {
                inst.PanCameraTo(target, zoomSize, duration);
            }
        }
    }

    public static void ShakeCamera(float duration, float magnitude)
    {
        if (activeInstance != null)
        {
            activeInstance.ShakeActiveCamera(duration, magnitude);
        }
        else
        {
            var inst = FindFirstObjectByType<FairyPossessionController>();
            if (inst != null)
            {
                inst.ShakeActiveCamera(duration, magnitude);
            }
        }
    }

    public void PanCameraTo(Transform newTarget, float zoomSize, float duration)
    {
        savedCameraTarget = currentTargetTransform;
        savedCameraSize = GetCurrentCameraSize();

        ApplyTargetToCamera(newTarget, false);
        ApplyCameraZoom(zoomSize, duration);
    }

    public void RestoreCameraPan(float duration)
    {
        ApplyTargetToCamera(savedCameraTarget, false);
        ApplyCameraZoom(savedCameraSize, duration);
    }

    public void ShakeActiveCamera(float duration, float magnitude)
    {
        CinemachineCamera activeCam = targetCamera != null
            ? targetCamera
            : (brain != null && brain.ActiveVirtualCamera is CinemachineCamera bCam ? bCam : (cameras != null && cameras.Length > 0 ? cameras[0] : null));

        if (activeCam != null)
        {
            if (!activeCam.TryGetComponent(out CinemachineTimelineShakeExtension shake))
            {
                shake = activeCam.gameObject.AddComponent<CinemachineTimelineShakeExtension>();
            }
            StartCoroutine(CinemachineShakeRoutine(shake, duration, magnitude));
        }
    }

    private IEnumerator CinemachineShakeRoutine(CinemachineTimelineShakeExtension shake, float duration, float magnitude)
    {
        if (shake == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - Mathf.Clamp01(elapsed / duration);
            shake.SetShake(magnitude * t, magnitude * 5f * t, 25f);
            yield return null;
        }
        shake.SetShake(0f, 0f, 0f);
    }

    private void HandleFairyInput()
    {
        if (Keyboard.current == null) return;

        Vector2 moveInput = Vector2.zero;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) moveInput.y += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) moveInput.y -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveInput.x += 1f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveInput.x -= 1f;

        fairy.SetPossessedMoveInput(moveInput);
    }
}
