using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public sealed class TimelineCameraReturnToPlayer2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayableDirector timeline;
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private Transform playerTarget;
    [SerializeField] private bool autoFindMissingReferences = true;

    [Header("Cutscene Camera")]
    [Tooltip("Bỏ Follow khi Timeline bắt đầu để camera không bám theo Player.")]
    [SerializeField] private bool detachWhenTimelineStarts = true;

    [Tooltip("Tự Stop Director khi Timeline dùng Wrap Mode Hold và đã chạy hết.")]
    [SerializeField] private bool stopHeldTimelineAtEnd = true;

    [Header("Return To Player")]
    [Min(0f)]
    [SerializeField] private float returnDuration = 1.5f;
    [SerializeField] private AnimationCurve returnCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool useUnscaledTime = true;

    [Tooltip("Khôi phục Follow cũ thay vì focus Player khi component bị tắt giữa chừng.")]
    [SerializeField] private bool restorePreviousFollowOnDisable;

    private Coroutine returnCoroutine;
    private GameObject followProxyObject;
    private Transform followProxy;
    private Transform followBeforeCutscene;
    private bool followBeforeCutsceneCached;
    private LensSettings lensBeforeCutscene;
    private bool lensBeforeCutsceneCached;
    private bool timelineIsRunning;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (timeline != null)
        {
            timeline.played += HandleTimelinePlayed;
            timeline.stopped += HandleTimelineStopped;
        }
    }

    private void Start()
    {
        // Hỗ trợ trường hợp Director đã Play On Awake trước khi script nhận event.
        if (timeline != null
            && timeline.state == PlayState.Playing
            && !timelineIsRunning)
        {
            HandleTimelinePlayed(timeline);
        }
    }

    private void LateUpdate()
    {
        // Wrap Mode Hold không tự phát event stopped. Stop Director khi đã tới
        // cuối để Timeline nhả camera, sau đó HandleTimelineStopped sẽ blend về Player.
        if (!stopHeldTimelineAtEnd
            || !timelineIsRunning
            || timeline == null
            || timeline.extrapolationMode != DirectorWrapMode.Hold
            || timeline.duration <= 0d
            || timeline.time < timeline.duration - 0.0001d)
        {
            return;
        }

        timeline.Stop();
    }

    private void OnDisable()
    {
        if (timeline != null)
        {
            timeline.played -= HandleTimelinePlayed;
            timeline.stopped -= HandleTimelineStopped;
        }

        StopReturnCoroutine();

        if (cinemachineCamera != null)
        {
            RestoreLensBeforeCutscene();

            if (restorePreviousFollowOnDisable && followBeforeCutsceneCached)
            {
                cinemachineCamera.Follow = followBeforeCutscene;
            }
            else if (cinemachineCamera.Follow == followProxy)
            {
                cinemachineCamera.Follow = playerTarget;
            }
        }

        DestroyFollowProxy();
        timelineIsRunning = false;
    }

    private void HandleTimelinePlayed(PlayableDirector director)
    {
        timelineIsRunning = true;
        ResolveReferences();

        if (cinemachineCamera == null)
        {
            Debug.LogWarning(
                "[TimelineCameraReturnToPlayer2D] Chưa tìm thấy Cinemachine Camera.",
                this);
            return;
        }

        followBeforeCutscene = cinemachineCamera.Follow != followProxy
            ? cinemachineCamera.Follow
            : playerTarget;
        followBeforeCutsceneCached = true;
        lensBeforeCutscene = cinemachineCamera.Lens;
        lensBeforeCutsceneCached = true;

        if (detachWhenTimelineStarts)
        {
            DetachCameraFromPlayer();
        }
    }

    private void HandleTimelineStopped(PlayableDirector director)
    {
        // Bỏ qua event Stop được gọi chỉ để reset Director trước Play().
        if (!timelineIsRunning)
        {
            return;
        }

        timelineIsRunning = false;
        FocusPlayerSmoothly();
    }

    public void DetachCameraFromPlayer()
    {
        ResolveReferences();
        if (cinemachineCamera == null)
        {
            return;
        }

        StopReturnCoroutine();

        Vector3 currentPosition = GetCurrentCameraPosition();
        Quaternion currentRotation = cinemachineCamera.transform.rotation;

        cinemachineCamera.Follow = null;
        cinemachineCamera.ForceCameraPosition(currentPosition, currentRotation);

        DestroyFollowProxy();
    }

    public void FocusPlayerSmoothly()
    {
        ResolveReferences();
        if (cinemachineCamera == null || playerTarget == null)
        {
            Debug.LogWarning(
                "[TimelineCameraReturnToPlayer2D] Thiếu Cinemachine Camera hoặc Player Target.",
                this);
            return;
        }

        StopReturnCoroutine();
        returnCoroutine = StartCoroutine(ReturnCameraToPlayer());
    }

    private IEnumerator ReturnCameraToPlayer()
    {
        Vector3 currentCameraPosition = GetCurrentCameraPosition();
        Quaternion currentCameraRotation = cinemachineCamera.transform.rotation;
        Vector3 startFollowPosition = GetFollowTargetPosition(currentCameraPosition);
        LensSettings startLens = cinemachineCamera.Lens;

        EnsureFollowProxy();
        followProxy.position = startFollowPosition;
        followProxy.rotation = Quaternion.identity;

        cinemachineCamera.Follow = followProxy;
        cinemachineCamera.ForceCameraPosition(
            currentCameraPosition,
            currentCameraRotation);

        float elapsed = 0f;
        float duration = Mathf.Max(0f, returnDuration);

        while (playerTarget != null && elapsed < duration)
        {
            elapsed += useUnscaledTime
                ? Time.unscaledDeltaTime
                : Time.deltaTime;

            float normalizedTime = duration > 0f
                ? Mathf.Clamp01(elapsed / duration)
                : 1f;
            float easedTime = returnCurve != null
                ? returnCurve.Evaluate(normalizedTime)
                : Mathf.SmoothStep(0f, 1f, normalizedTime);

            followProxy.position = Vector3.LerpUnclamped(
                startFollowPosition,
                playerTarget.position,
                easedTime);
            RestoreLens(startLens, easedTime);

            yield return null;
        }

        if (playerTarget != null)
        {
            followProxy.position = playerTarget.position;
            cinemachineCamera.Follow = playerTarget;
        }

        RestoreLensBeforeCutscene();
        returnCoroutine = null;
        DestroyFollowProxy();
    }

    private void RestoreLens(LensSettings startLens, float t)
    {
        if (!lensBeforeCutsceneCached)
        {
            return;
        }

        LensSettings lens = startLens;
        if (lens.Orthographic)
        {
            lens.OrthographicSize = Mathf.Lerp(
                startLens.OrthographicSize,
                lensBeforeCutscene.OrthographicSize,
                t);
        }
        else
        {
            lens.FieldOfView = Mathf.Lerp(
                startLens.FieldOfView,
                lensBeforeCutscene.FieldOfView,
                t);
        }

        cinemachineCamera.Lens = lens;
    }

    private void RestoreLensBeforeCutscene()
    {
        if (cinemachineCamera != null && lensBeforeCutsceneCached)
        {
            cinemachineCamera.Lens = lensBeforeCutscene;
            lensBeforeCutsceneCached = false;
        }
    }

    private Vector3 GetCurrentCameraPosition()
    {
        if (cinemachineCamera.PreviousStateIsValid)
        {
            return cinemachineCamera.State.GetFinalPosition();
        }

        return cinemachineCamera.transform.position;
    }

    private Vector3 GetFollowTargetPosition(Vector3 cameraPosition)
    {
        CinemachineFollow follow =
            cinemachineCamera.GetComponent<CinemachineFollow>();

        // Trong camera 2D, FollowOffset thường ở World Space. Trừ offset để
        // frame đầu của transition trùng frame cuối cutscene, không bị giật.
        return follow != null
            ? cameraPosition - follow.FollowOffset
            : cameraPosition;
    }

    private void ResolveReferences()
    {
        if (timeline == null)
        {
            timeline = GetComponent<PlayableDirector>();
        }

        if (!autoFindMissingReferences)
        {
            return;
        }

        if (cinemachineCamera == null)
        {
            cinemachineCamera = FindAnyObjectByType<CinemachineCamera>();
        }

        if (playerTarget == null)
        {
            Player player = FindAnyObjectByType<Player>();
            if (player != null)
            {
                playerTarget = player.transform;
            }
        }
    }

    private void EnsureFollowProxy()
    {
        if (followProxy != null)
        {
            return;
        }

        followProxyObject = new GameObject("Timeline Camera Return Proxy");
        followProxyObject.hideFlags = HideFlags.HideAndDontSave;
        followProxy = followProxyObject.transform;
    }

    private void StopReturnCoroutine()
    {
        if (returnCoroutine == null)
        {
            return;
        }

        StopCoroutine(returnCoroutine);
        returnCoroutine = null;
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
}
