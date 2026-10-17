using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class DoorLowerCutscene2D : MonoBehaviour
{
    [Header("Door References")]
    [SerializeField, Tooltip("GameObject / Transform cửa cần hạ xuống")]
    private GameObject doorObject;

    [SerializeField, Tooltip("GameObject vị trí hạ của cửa (vị trí đích khi cửa hạ xong)")]
    private GameObject doorLoweredTarget;

    [SerializeField, Tooltip("Vị trí camera hướng tới cửa (để trống sẽ tự lấy vị trí cửa)")]
    private Transform doorCameraTarget;

    [Header("References")]
    [SerializeField, Tooltip("Player trong scene (để trống sẽ tự tìm)")]
    private Player player;

    [SerializeField, Tooltip("Cinemachine Camera chính trong scene (để trống sẽ tự tìm)")]
    private CinemachineCamera cinemachineCamera;

    [Header("Timing Settings")]
    [SerializeField, Min(0f), Tooltip("Thời gian di chuyển camera từ Player tới cửa (giây)")]
    private float cameraMoveToDoorDuration = 1.0f;

    [SerializeField, Min(0f), Tooltip("Thời gian cửa hạ xuống (giây)")]
    private float doorLowerDuration = 2.0f;

    [SerializeField, Min(0f), Tooltip("Thời gian chờ sau khi cửa hạ xong trước khi quay về Player (giây)")]
    private float doorLowerHoldDelay = 0.4f;

    [SerializeField, Min(0f), Tooltip("Thời gian di chuyển camera từ cửa quay lại Player (giây)")]
    private float cameraReturnDuration = 1.0f;

    [Header("Camera Shake Settings")]
    [SerializeField, Min(0f), Tooltip("Cường độ rung vị trí camera khi cửa hạ")]
    private float shakePositionAmplitude = 0.15f;

    [SerializeField, Min(0f), Tooltip("Cường độ rung góc xoay camera khi cửa hạ (đặt 0 để không xoay/rung trục Z)")]
    private float shakeRotationAmplitude = 0f;

    [SerializeField, Min(0f), Tooltip("Tần số rung camera")]
    private float shakeFrequency = 18f;

    [Header("Animation Curves")]
    [SerializeField]
    private AnimationCurve cameraMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [SerializeField]
    private AnimationCurve doorLowerCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [SerializeField]
    private AnimationCurve cameraReturnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Events")]
    [SerializeField] private UnityEvent onCutsceneStarted;
    [SerializeField] private UnityEvent onDoorLowerStarted;
    [SerializeField] private UnityEvent onDoorLowerFinished;
    [SerializeField] private UnityEvent onCutsceneFinished;

    public bool IsPlaying { get; private set; }

    public GameObject DoorObject
    {
        get => doorObject;
        set => doorObject = value;
    }

    public GameObject DoorLoweredTarget
    {
        get => doorLoweredTarget;
        set => doorLoweredTarget = value;
    }

    private Coroutine cutsceneCoroutine;
    private GameObject followProxyObject;
    private Transform followProxy;
    private Transform previousFollow;
    private bool previousFollowCached;
    private CinemachineTimelineShakeExtension shakeExtension;

    private void OnDisable()
    {
        StopCutscene();
    }

    public void PlayCutscene(Action onComplete = null)
    {
        if (IsPlaying)
        {
            return;
        }

        cutsceneCoroutine = StartCoroutine(RunCutscene(onComplete));
    }

    public void StopCutscene()
    {
        if (cutsceneCoroutine != null)
        {
            StopCoroutine(cutsceneCoroutine);
            cutsceneCoroutine = null;
        }

        RestoreCameraState();
        UnlockPlayer();
        IsPlaying = false;
    }

    private IEnumerator RunCutscene(Action onComplete)
    {
        IsPlaying = true;
        ResolveReferences();

        if (doorObject == null || doorLoweredTarget == null)
        {
            Debug.LogWarning("[DoorLowerCutscene2D] Door Object hoặc Door Lowered Target chưa được gán.", this);
            IsPlaying = false;
            onComplete?.Invoke();
            yield break;
        }

        LockPlayer();
        CachePreviousFollow();
        EnsureShakeExtension();

        onCutsceneStarted?.Invoke();

        // Target camera destination
        Vector3 doorPos = doorCameraTarget != null ? doorCameraTarget.position : doorObject.transform.position;
        Vector3 startCamPos = GetCurrentCinemachinePosition();

        EnsureFollowProxy();
        Vector3 startFollowPos = GetFollowTargetPosition(startCamPos);
        Vector3 doorFollowPos = GetFollowTargetPosition(doorPos);
        // Cố định Z của proxy theo Z ban đầu của camera để không bị giật/đổi Z
        doorFollowPos.z = startFollowPos.z;

        followProxy.position = startFollowPos;
        followProxy.rotation = Quaternion.identity;

        if (cinemachineCamera != null)
        {
            cinemachineCamera.Follow = followProxy;
        }

        // 1. Move camera to door
        yield return AnimateProxyPosition(startFollowPos, doorFollowPos, cameraMoveToDoorDuration, cameraMoveCurve);

        // 2. Lower door with camera shake
        onDoorLowerStarted?.Invoke();
        Vector3 initialDoorPos = doorObject.transform.position;
        Vector3 finalDoorPos = doorLoweredTarget.transform.position;

        float elapsed = 0f;
        while (elapsed < doorLowerDuration)
        {
            elapsed += Time.deltaTime;
            float t = EvaluateCurve(doorLowerCurve, elapsed, doorLowerDuration);

            doorObject.transform.position = Vector3.Lerp(initialDoorPos, finalDoorPos, t);

            // Apply camera shake
            if (shakeExtension != null)
            {
                shakeExtension.SetShake(shakePositionAmplitude, shakeRotationAmplitude, shakeFrequency);
            }

            float noiseTime = Time.time * shakeFrequency;
            Vector3 shakeOffset = new Vector3(
                (Mathf.PerlinNoise(noiseTime, 0.17f) - 0.5f) * (shakePositionAmplitude * 2f),
                (Mathf.PerlinNoise(0.43f, noiseTime) - 0.5f) * (shakePositionAmplitude * 2f),
                0f
            );
            followProxy.position = doorFollowPos + shakeOffset;

            yield return null;
        }

        doorObject.transform.position = finalDoorPos;
        followProxy.position = doorFollowPos;

        if (shakeExtension != null)
        {
            shakeExtension.SetShake(0f, 0f, 0f);
        }

        onDoorLowerFinished?.Invoke();

        // 3. Hold pause briefly at door
        if (doorLowerHoldDelay > 0f)
        {
            yield return new WaitForSeconds(doorLowerHoldDelay);
        }

        // 4. Move camera back to player
        Transform returnTarget = previousFollowCached && previousFollow != null ? previousFollow : (player != null ? player.transform : null);
        Vector3 playerPos = returnTarget != null ? returnTarget.position : startCamPos;
        Vector3 playerFollowPos = GetFollowTargetPosition(playerPos);
        playerFollowPos.z = startFollowPos.z;

        yield return AnimateProxyPosition(doorFollowPos, playerFollowPos, cameraReturnDuration, cameraReturnCurve);

        RestoreCameraState();
        UnlockPlayer();

        onCutsceneFinished?.Invoke();
        IsPlaying = false;
        cutsceneCoroutine = null;

        onComplete?.Invoke();
    }

    private IEnumerator AnimateProxyPosition(Vector3 start, Vector3 end, float duration, AnimationCurve curve)
    {
        if (duration <= 0f)
        {
            followProxy.position = end;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = EvaluateCurve(curve, elapsed, duration);
            followProxy.position = Vector3.LerpUnclamped(start, end, t);
            yield return null;
        }

        followProxy.position = end;
    }

    private void LockPlayer()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<Player>();
        }

        if (player != null)
        {
            player.SetControlLocked(true);
            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }
    }

    private void UnlockPlayer()
    {
        if (player != null)
        {
            player.SetControlLocked(false);
        }
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<Player>();
        }

        if (cinemachineCamera == null)
        {
            cinemachineCamera = FindAnyObjectByType<CinemachineCamera>();
        }
    }

    private void CachePreviousFollow()
    {
        if (cinemachineCamera == null || previousFollowCached)
        {
            return;
        }

        previousFollow = cinemachineCamera.Follow;
        previousFollowCached = true;
    }

    private void RestoreCameraState()
    {
        if (shakeExtension != null)
        {
            shakeExtension.SetShake(0f, 0f, 0f);
        }

        if (cinemachineCamera != null && previousFollowCached)
        {
            cinemachineCamera.Follow = previousFollow;
        }

        DestroyFollowProxy();
        previousFollowCached = false;
    }

    private void EnsureShakeExtension()
    {
        if (cinemachineCamera == null)
        {
            return;
        }

        shakeExtension = cinemachineCamera.GetComponent<CinemachineTimelineShakeExtension>();
        if (shakeExtension == null)
        {
            shakeExtension = cinemachineCamera.gameObject.AddComponent<CinemachineTimelineShakeExtension>();
        }
    }

    private Vector3 GetCurrentCinemachinePosition()
    {
        if (cinemachineCamera == null)
        {
            return transform.position;
        }

        if (cinemachineCamera.PreviousStateIsValid)
        {
            return cinemachineCamera.State.GetFinalPosition();
        }

        return cinemachineCamera.transform.position;
    }

    private Vector3 GetFollowTargetPosition(Vector3 cameraPos)
    {
        if (cinemachineCamera == null)
        {
            return cameraPos;
        }

        CinemachineFollow follow = cinemachineCamera.GetComponent<CinemachineFollow>();
        return follow != null ? cameraPos - follow.FollowOffset : cameraPos;
    }

    private static float EvaluateCurve(AnimationCurve curve, float elapsed, float duration)
    {
        if (duration <= 0f)
        {
            return 1f;
        }

        float t = Mathf.Clamp01(elapsed / duration);
        return curve != null ? curve.Evaluate(t) : Mathf.SmoothStep(0f, 1f, t);
    }

    private void EnsureFollowProxy()
    {
        if (followProxy != null)
        {
            return;
        }

        followProxyObject = new GameObject("Door Cutscene Follow Proxy");
        followProxyObject.hideFlags = HideFlags.HideAndDontSave;
        followProxy = followProxyObject.transform;
    }

    private void DestroyFollowProxy()
    {
        if (followProxyObject == null)
        {
            followProxy = null;
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(followProxyObject);
        }
        else
        {
            DestroyImmediate(followProxyObject);
        }

        followProxyObject = null;
        followProxy = null;
    }

    private void OnDrawGizmosSelected()
    {
        if (doorObject != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(doorObject.transform.position, Vector3.one * 0.5f);
        }

        if (doorLoweredTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(doorLoweredTarget.transform.position, Vector3.one * 0.5f);

            if (doorObject != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(doorObject.transform.position, doorLoweredTarget.transform.position);
            }
        }
    }
}
