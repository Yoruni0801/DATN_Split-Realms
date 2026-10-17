using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class CameraMoveZoomCutsceneTrigger2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private bool autoFindMissingReferences = true;

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool triggerOnce = true;

    [Header("Camera Move")]
    [Min(0f)]
    [SerializeField] private float moveDuration = 1.2f;
    [SerializeField] private AnimationCurve moveCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool useUnscaledTime;

    [Header("Camera Zoom")]
    [SerializeField] private bool zoomAfterMove = true;
    [Min(0f)]
    [SerializeField] private float zoomDuration = 0.6f;
    [SerializeField] private float targetOrthographicSize = 7f;
    [SerializeField] private float targetFieldOfView = 45f;
    [SerializeField] private AnimationCurve zoomCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Player Lock")]
    [SerializeField] private bool lockPlayerDuringCutscene = true;
    [SerializeField] private bool unlockPlayerWhenFinished = true;
    [SerializeField] private bool stopPlayerVelocityOnLock = true;

    [Header("Restore")]
    [SerializeField] private bool restoreCameraFollowOnDisable = true;

    private Collider2D triggerCollider;
    private Coroutine cutsceneCoroutine;
    private Rigidbody2D playerRigidbody;
    private GameObject followProxyObject;
    private Transform followProxy;
    private Transform previousFollow;
    private bool previousFollowCached;
    private bool hasTriggered;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        EnsureTriggerCollider();
        ResolveReferences();
    }

    private void OnEnable()
    {
        EnsureTriggerCollider();
        ResolveReferences();
    }

    private void OnDisable()
    {
        bool wasInterrupted = cutsceneCoroutine != null;

        if (cutsceneCoroutine != null)
        {
            StopCoroutine(cutsceneCoroutine);
            cutsceneCoroutine = null;
        }

        if (wasInterrupted
            && restoreCameraFollowOnDisable
            && cinemachineCamera != null
            && previousFollowCached)
        {
            cinemachineCamera.Follow = previousFollow;
        }

        if (wasInterrupted)
        {
            UnlockPlayer();
        }

        DestroyFollowProxy();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (cutsceneCoroutine != null || (hasTriggered && triggerOnce))
        {
            return;
        }

        if (!IsPlayerCollider(other))
        {
            return;
        }

        Player enteredPlayer = other.GetComponent<Player>();
        if (enteredPlayer == null)
        {
            enteredPlayer = other.GetComponentInParent<Player>();
        }

        if (enteredPlayer != null)
        {
            player = enteredPlayer;
        }

        ResolveReferences();

        if (cameraTarget == null)
        {
            Debug.LogWarning(
                "[CameraMoveZoomCutsceneTrigger2D] Chua gan Camera Target.",
                this);
            return;
        }

        if (cinemachineCamera == null && targetCamera == null)
        {
            Debug.LogWarning(
                "[CameraMoveZoomCutsceneTrigger2D] Chua gan Camera hoac Cinemachine Camera.",
                this);
            return;
        }

        hasTriggered = true;
        cutsceneCoroutine = StartCoroutine(RunCutscene());
    }

    private IEnumerator RunCutscene()
    {
        LockPlayer();
        CachePreviousFollow();

        yield return MoveCameraToTarget();

        if (zoomAfterMove)
        {
            yield return ZoomCamera();
        }

        if (unlockPlayerWhenFinished)
        {
            UnlockPlayer();
        }

        cutsceneCoroutine = null;

        if (triggerOnce)
        {
            enabled = false;
        }
    }

    private IEnumerator MoveCameraToTarget()
    {
        float duration = Mathf.Max(0f, moveDuration);

        if (cinemachineCamera != null)
        {
            EnsureFollowProxy();

            Vector3 startCameraPosition = GetCurrentCinemachinePosition();
            Vector3 targetCameraPosition = cameraTarget.position;
            Quaternion startRotation = cinemachineCamera.transform.rotation;
            Quaternion targetRotation = cameraTarget.rotation;

            followProxy.position = GetFollowTargetPosition(startCameraPosition);
            followProxy.rotation = Quaternion.identity;

            cinemachineCamera.Follow = followProxy;
            cinemachineCamera.ForceCameraPosition(startCameraPosition, startRotation);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += GetDeltaTime();
                float t = EvaluateCurve(moveCurve, elapsed, duration);
                Vector3 cameraPosition = Vector3.LerpUnclamped(
                    startCameraPosition,
                    targetCameraPosition,
                    t);

                followProxy.position = GetFollowTargetPosition(cameraPosition);
                cinemachineCamera.ForceCameraPosition(
                    cameraPosition,
                    Quaternion.Slerp(startRotation, targetRotation, t));

                yield return null;
            }

            followProxy.position = GetFollowTargetPosition(targetCameraPosition);
            cinemachineCamera.ForceCameraPosition(targetCameraPosition, targetRotation);
            cinemachineCamera.Follow = null;
            cinemachineCamera.ForceCameraPosition(targetCameraPosition, targetRotation);
            DestroyFollowProxy();
            yield break;
        }

        Vector3 startPosition = targetCamera.transform.position;
        Quaternion startCameraRotation = targetCamera.transform.rotation;
        Vector3 endPosition = cameraTarget.position;
        Quaternion endRotation = cameraTarget.rotation;
        float timer = 0f;

        while (timer < duration)
        {
            timer += GetDeltaTime();
            float t = EvaluateCurve(moveCurve, timer, duration);
            targetCamera.transform.SetPositionAndRotation(
                Vector3.LerpUnclamped(startPosition, endPosition, t),
                Quaternion.Slerp(startCameraRotation, endRotation, t));

            yield return null;
        }

        targetCamera.transform.SetPositionAndRotation(endPosition, endRotation);
    }

    private IEnumerator ZoomCamera()
    {
        float duration = Mathf.Max(0f, zoomDuration);
        float elapsed = 0f;

        if (cinemachineCamera != null)
        {
            LensSettings startLens = cinemachineCamera.Lens;

            while (elapsed < duration)
            {
                elapsed += GetDeltaTime();
                ApplyCinemachineZoom(startLens, EvaluateCurve(zoomCurve, elapsed, duration));
                yield return null;
            }

            ApplyCinemachineZoom(startLens, 1f);
            yield break;
        }

        float startSize = targetCamera.orthographic
            ? targetCamera.orthographicSize
            : targetCamera.fieldOfView;

        while (elapsed < duration)
        {
            elapsed += GetDeltaTime();
            float t = EvaluateCurve(zoomCurve, elapsed, duration);
            ApplyUnityCameraZoom(startSize, t);
            yield return null;
        }

        ApplyUnityCameraZoom(startSize, 1f);
    }

    private void ApplyCinemachineZoom(LensSettings startLens, float t)
    {
        LensSettings lens = startLens;
        if (lens.Orthographic)
        {
            lens.OrthographicSize = Mathf.Lerp(
                startLens.OrthographicSize,
                targetOrthographicSize,
                t);
        }
        else
        {
            lens.FieldOfView = Mathf.Lerp(
                startLens.FieldOfView,
                targetFieldOfView,
                t);
        }

        cinemachineCamera.Lens = lens;
    }

    private void ApplyUnityCameraZoom(float startValue, float t)
    {
        if (targetCamera.orthographic)
        {
            targetCamera.orthographicSize = Mathf.Lerp(
                startValue,
                targetOrthographicSize,
                t);
        }
        else
        {
            targetCamera.fieldOfView = Mathf.Lerp(
                startValue,
                targetFieldOfView,
                t);
        }
    }

    private void LockPlayer()
    {
        if (!lockPlayerDuringCutscene || player == null)
        {
            return;
        }

        player.SetControlLocked(true);

        if (!stopPlayerVelocityOnLock)
        {
            return;
        }

        playerRigidbody = player.GetComponent<Rigidbody2D>();
        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector2.zero;
            playerRigidbody.angularVelocity = 0f;
        }
    }

    private void UnlockPlayer()
    {
        if (player != null && lockPlayerDuringCutscene)
        {
            player.SetControlLocked(false);
        }
    }

    private void ResolveReferences()
    {
        if (!autoFindMissingReferences)
        {
            return;
        }

        if (player == null)
        {
            player = FindAnyObjectByType<Player>();
        }

        if (cinemachineCamera == null)
        {
            cinemachineCamera = FindAnyObjectByType<CinemachineCamera>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private bool IsPlayerCollider(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(playerTag)
            && (other.CompareTag(playerTag) || other.GetComponentInParent<Player>() != null))
        {
            return true;
        }

        return string.IsNullOrEmpty(playerTag)
               && other.GetComponentInParent<Player>() != null;
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

    private Vector3 GetCurrentCinemachinePosition()
    {
        if (cinemachineCamera.PreviousStateIsValid)
        {
            return cinemachineCamera.State.GetFinalPosition();
        }

        return cinemachineCamera.transform.position;
    }

    private Vector3 GetFollowTargetPosition(Vector3 cameraPosition)
    {
        CinemachineFollow follow = cinemachineCamera.GetComponent<CinemachineFollow>();
        return follow != null
            ? cameraPosition - follow.FollowOffset
            : cameraPosition;
    }

    private float GetDeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
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

        followProxyObject = new GameObject("Camera Move Zoom Cutscene Target");
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

    private void EnsureTriggerCollider()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider2D>();
        }

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void OnValidate()
    {
        triggerCollider = GetComponent<Collider2D>();
        EnsureTriggerCollider();
    }

    private void OnDrawGizmosSelected()
    {
        if (cameraTarget == null)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(cameraTarget.position, 0.25f);
        Gizmos.DrawLine(transform.position, cameraTarget.position);
    }
}
