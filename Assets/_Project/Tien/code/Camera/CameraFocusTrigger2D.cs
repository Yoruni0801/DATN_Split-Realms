using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;

[RequireComponent(typeof(Collider2D))]
public sealed class CameraFocusTrigger2D : MonoBehaviour
{
    private const double CompletionThreshold = 0.05d;

    [Header("References")]
    [SerializeField] private CinemachineCamera virtualCamera;
    [SerializeField] private Transform cameraDestination;
    [SerializeField] private Vector3 cameraOffset = Vector3.zero;

    [Header("Timeline")]
    [SerializeField] private PlayableDirector timelineDirector;
    [SerializeField] private GameObject timelineObject;

    [Header("Timing")]
    [Min(0f)] [SerializeField] private float moveOutDuration = 0.75f;
    [Min(0f)] [SerializeField] private float holdDuration = 2f;
    [Min(0f)] [SerializeField] private float returnDuration = 0.75f;

    [Header("Zoom Out")]
    [Min(0.01f)] [SerializeField] private float zoomOutOrthographicSize = 10f;
    [SerializeField] private AnimationCurve transitionCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Trigger")]
    [SerializeField] private bool triggerOnce = true;

    private Coroutine focusCoroutine;
    private Transform savedFollowTarget;
    private LensSettings savedLens;
    private Vector3 savedPosition;
    private Quaternion savedRotation;
    private Player activePlayer;
    private bool playerWasControlLocked;
    private bool hasSavedState;
    private bool hasTriggered;
    private bool timelineIsRunning;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
        }

        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (virtualCamera == null)
        {
            virtualCamera = FindFirstObjectByType<CinemachineCamera>();
        }

        if (timelineDirector == null)
        {
            if (timelineObject != null)
            {
                timelineDirector = timelineObject.GetComponentInChildren<PlayableDirector>(true);
            }

            if (timelineDirector == null)
            {
                timelineDirector = GetComponentInChildren<PlayableDirector>(true);
            }
        }

        if (timelineObject == null && timelineDirector != null)
        {
            timelineObject = timelineDirector.gameObject;
        }
    }

    private void Update()
    {
        if (!timelineIsRunning || timelineDirector == null)
        {
            return;
        }

        if (timelineDirector.state != PlayState.Playing && HasTimelineCompleted())
        {
            HideTimeline();
        }
    }

    private bool HasTimelineCompleted()
    {
        if (timelineDirector == null || timelineDirector.playableAsset == null)
        {
            return true;
        }

        double duration = timelineDirector.duration;
        if (double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0d)
        {
            return true;
        }

        return timelineDirector.time >= (duration - CompletionThreshold);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Player player = other.GetComponentInParent<Player>();
        if (player == null)
        {
            player = PlayerRegistry.Current;
        }

        if (player == null || focusCoroutine != null || (triggerOnce && hasTriggered))
        {
            return;
        }

        ResolveReferences();

        if (virtualCamera == null || cameraDestination == null)
        {
            Debug.LogWarning($"{nameof(CameraFocusTrigger2D)} on {name} is missing a camera or destination.", this);
            return;
        }

        hasTriggered = true;
        PlayTimeline();
        focusCoroutine = StartCoroutine(FocusRoutine(player));
    }

    private void PlayTimeline()
    {
        ResolveReferences();
        if (timelineDirector == null || timelineDirector.playableAsset == null)
        {
            return;
        }

        GameObject objectToShow = timelineObject != null
            ? timelineObject
            : timelineDirector.gameObject;
        if (objectToShow != null && !objectToShow.activeSelf)
        {
            objectToShow.SetActive(true);
        }

        timelineDirector.stopped -= HandleTimelineStopped;
        timelineDirector.stopped += HandleTimelineStopped;
        timelineDirector.time = 0d;
        timelineIsRunning = true;
        timelineDirector.Play();
    }

    private void HandleTimelineStopped(PlayableDirector stoppedDirector)
    {
        if (stoppedDirector == timelineDirector)
        {
            HideTimeline();
        }
    }

    private void HideTimeline()
    {
        timelineIsRunning = false;

        if (timelineDirector != null)
        {
            timelineDirector.stopped -= HandleTimelineStopped;
        }

        GameObject objectToHide = timelineObject != null
            ? timelineObject
            : timelineDirector != null ? timelineDirector.gameObject : null;
        if (objectToHide != null)
        {
            objectToHide.SetActive(false);
        }

        if (focusCoroutine != null)
        {
            StopCoroutine(focusCoroutine);
            focusCoroutine = null;
        }

        RestoreCameraState();
        RestorePlayerControl();
    }

    private IEnumerator FocusRoutine(Player player)
    {
        activePlayer = player;
        playerWasControlLocked = player.IsControlLocked;
        player.SetControlLocked(true);

        SaveCameraState(player.transform);
        virtualCamera.Follow = null;
        Vector3 destinationPosition = new Vector3(
            cameraDestination.position.x + cameraOffset.x,
            cameraDestination.position.y + cameraOffset.y,
            savedPosition.z + cameraOffset.z);

        yield return AnimateCamera(
            savedPosition,
            destinationPosition,
            savedRotation,
            savedRotation,
            savedLens.OrthographicSize,
            zoomOutOrthographicSize,
            moveOutDuration);

        if (timelineDirector != null && timelineDirector.playableAsset != null)
        {
            if (holdDuration > 0f)
            {
                yield return new WaitForSeconds(holdDuration);
            }

            focusCoroutine = null;
            yield break;
        }

        if (holdDuration > 0f)
        {
            yield return new WaitForSeconds(holdDuration);
        }

        yield return AnimateCamera(
            destinationPosition,
            savedPosition,
            savedRotation,
            savedRotation,
            zoomOutOrthographicSize,
            savedLens.OrthographicSize,
            returnDuration);

        RestoreCameraState();
        RestorePlayerControl();
        focusCoroutine = null;
    }

    private void SaveCameraState(Transform player)
    {
        Transform currentFollow = virtualCamera.Follow;
        if (currentFollow != null && !IsTemporaryProxy(currentFollow))
        {
            savedFollowTarget = currentFollow;
        }
        else
        {
            savedFollowTarget = player != null ? player : (PlayerRegistry.Current != null ? PlayerRegistry.Current.transform : null);
        }

        savedLens = virtualCamera.Lens;
        savedPosition = virtualCamera.transform.position;
        savedRotation = virtualCamera.transform.rotation;
        hasSavedState = true;
    }

    private IEnumerator AnimateCamera(
        Vector3 fromPosition,
        Vector3 toPosition,
        Quaternion fromRotation,
        Quaternion toRotation,
        float fromSize,
        float toSize,
        float duration)
    {
        if (duration <= 0f)
        {
            ApplyCameraState(toPosition, toRotation, toSize);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float interpolation = transitionCurve != null
                ? transitionCurve.Evaluate(normalizedTime)
                : normalizedTime;

            ApplyCameraState(
                Vector3.LerpUnclamped(fromPosition, toPosition, interpolation),
                Quaternion.SlerpUnclamped(fromRotation, toRotation, interpolation),
                Mathf.LerpUnclamped(fromSize, toSize, interpolation));

            elapsed += Time.deltaTime;
            yield return null;
        }

        ApplyCameraState(toPosition, toRotation, toSize);
    }

    private void ApplyCameraState(Vector3 position, Quaternion rotation, float orthographicSize)
    {
        virtualCamera.transform.SetPositionAndRotation(position, rotation);

        LensSettings lens = virtualCamera.Lens;
        lens.OrthographicSize = orthographicSize;
        virtualCamera.Lens = lens;
    }

    private void RestoreCameraState()
    {
        if (!hasSavedState || virtualCamera == null)
        {
            return;
        }

        virtualCamera.transform.SetPositionAndRotation(savedPosition, savedRotation);
        virtualCamera.Lens = savedLens;

        Transform targetFollow = savedFollowTarget;
        if (targetFollow == null || IsTemporaryProxy(targetFollow))
        {
            Player currentPlayer = PlayerRegistry.Current;
            targetFollow = currentPlayer != null ? currentPlayer.transform : null;
        }

        virtualCamera.Follow = targetFollow;
        virtualCamera.PreviousStateIsValid = false;
        virtualCamera.CancelDamping(true);
        hasSavedState = false;
    }

    private static bool IsTemporaryProxy(Transform target)
    {
        if (target == null) return true;
        string targetName = target.name;
        return targetName.Contains("Proxy") || targetName.Contains("Timeline Camera");
    }

    private void RestorePlayerControl()
    {
        if (activePlayer != null)
        {
            activePlayer.SetControlLocked(playerWasControlLocked);
        }

        activePlayer = null;
    }

    private void OnDisable()
    {
        if (focusCoroutine != null)
        {
            StopCoroutine(focusCoroutine);
            focusCoroutine = null;
        }

        RestoreCameraState();
        RestorePlayerControl();
    }

    private void OnDestroy()
    {
        if (timelineDirector != null)
        {
            timelineDirector.stopped -= HandleTimelineStopped;
        }
    }
}
