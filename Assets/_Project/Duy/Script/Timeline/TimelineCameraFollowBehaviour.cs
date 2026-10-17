using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;

public class TimelineCameraFollowBehaviour : PlayableBehaviour
{
    public TimelineCameraFollowAction action;
    public TimelineCameraFollowExitMode exitMode;
    public TimelineCameraFollowTargetMode targetMode;
    public TimelineCameraFollowZMode targetZMode;
    public float fixedWorldZ;
    public Vector3 followOffset;
    public Transform target;
    public bool findPlayerWhenTargetMissing;
    public float smoothFollowDuration;
    public bool invalidatePreviousState;
    public bool applyInEditMode;
    public bool enableZoom;
    public TimelineCameraZoomDirection zoomDirection;
    public float zoomOrthographicSize;
    public float zoomFieldOfView;
    public float zoomDuration;
    public bool restoreZoomOnExit;
    public bool enableShake;
    public Camera shakeCamera;
    public float shakeAmplitude;
    public float shakeFrequency;
    public AnimationCurve shakeEnvelope;

    public TimelineCameraConfinerMode confinerMode;
    public Collider2D boundingShape2D;
    public bool restoreConfinerOnExit;

    public Transform ResolveTarget()
    {
        if (target != null || !findPlayerWhenTargetMissing)
        {
            return target;
        }

        Player player = PlayerRegistry.Current;
        return player != null ? player.transform : null;
    }
}

public class TimelineCameraFollowMixerBehaviour : PlayableBehaviour
{
    private CinemachineCamera activeCamera;
    private Transform originalFollow;
    private bool originalFollowCached;
    private bool clipApplied;
    private TimelineCameraFollowBehaviour lastAppliedClip;
    private Transform lastCameraFollow;
    private TimelineCameraShakeDriver activeShakeDriver;
    public TimelineCameraFollowTrack.PostPlaybackState postPlaybackState;
    private Transform postPlaybackFollow;
    private Vector3 postPlaybackHoldPosition;
    private bool postPlaybackHoldPositionCaptured;
    private Transform smoothFollowProxy;
    private Transform smoothFollowTarget;
    private TimelineCameraFollowBehaviour smoothFollowClip;
    private Vector3 smoothFollowStartPosition;
    private Transform projectedFollowProxy;
    private TimelineCameraFollowBehaviour projectedFollowClip;
    private Transform holdPositionProxy;
    private TimelineCameraFollowBehaviour holdPositionClip;
    private Transform restoreFollowProxy;
    private Transform restoreFollowTarget;
    private Vector3 restoreFollowStartPosition;
    private float restoreFollowElapsed;
    private float restoreFollowDuration;
    private bool restoreFollowActive;
    private LensSettings originalLens;
    private LensSettings zoomStartLens;
    private bool originalLensCached;
    private bool zoomStartLensCached;
    private bool zoomApplied;
    private TimelineCameraFollowBehaviour lastZoomClip;

    private CinemachineConfiner2D activeConfiner;
    private bool originalConfinerCached;
    private bool originalConfinerEnabled;
    private Collider2D originalBoundingShape2D;
    private bool confinerApplied;
    private TimelineCameraFollowBehaviour lastConfinerClip;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        TimelineSeekContext.Evaluate(playable, info);

        CinemachineCamera camera = playerData as CinemachineCamera;
        if (camera == null)
        {
            return;
        }

        if (activeCamera != camera)
        {
            StopShake();
            ClearSmoothFollowProxy();
            ClearProjectedFollowProxy();
            ClearHoldPositionProxy();
            ClearRestoreFollowProxy();
            RestoreZoomIfNeeded();
            RestoreConfinerIfNeeded();
            activeCamera = camera;
            CacheOriginalLens(camera);
            CacheOriginalConfiner(camera);
            clipApplied = false;
            lastAppliedClip = null;
            lastCameraFollow = camera.Follow;
        }

        if (TimelineSeekContext.IsSeeking)
        {
            ResetTransitionStateForSeek(camera);
        }

        ActiveCameraFollowClip activeClip = GetStrongestActiveClip(playable);
        if (activeClip.Behaviour == null)
        {
            StopShake();

            bool keepCurrent = postPlaybackState == TimelineCameraFollowTrack.PostPlaybackState.KeepCurrent;
            if (!keepCurrent)
            {
                RestoreZoomIfNeeded();
                RestoreConfinerIfNeeded();
            }

            if (Application.isPlaying)
            {
                if (!keepCurrent)
                {
                    RestoreAfterClip(camera);
                }
                TickRestoreFollow(camera);
            }

            return;
        }

        if (!Application.isPlaying && !activeClip.Behaviour.applyInEditMode)
        {
            StopShake();
            RestoreZoomIfNeeded();
            RestoreConfinerIfNeeded();
            return;
        }

        ApplyClip(camera, activeClip);
        ApplyZoom(camera, activeClip);
        ApplyConfiner(camera, activeClip);
        ApplyShake(activeClip);
    }

    public override void OnGraphStop(Playable playable)
    {
        StopShake();

        if (postPlaybackState != TimelineCameraFollowTrack.PostPlaybackState.KeepCurrent)
        {
            RestoreZoomIfNeeded();
            RestoreConfinerIfNeeded();
        }

        if (activeCamera != null && Application.isPlaying)
        {
            ApplyPostPlaybackState(activeCamera);
        }
        else if (activeCamera != null && !Application.isPlaying)
        {
            // Editor mode: always revert so that destroying proxies below
            // doesn't leave camera.Follow pointing at a destroyed object.
            // Without this, Cinemachine keeps the camera at its last position
            // and each subsequent play accumulates the body follow-offset on Z.
            RestoreZoomIfNeeded();
            if (originalFollowCached)
            {
                activeCamera.Follow = originalFollow;
            }
        }

        ClearSmoothFollowProxy();
        ClearProjectedFollowProxy();
        ClearHoldPositionProxy();
        ClearRestoreFollowProxy();

        originalFollowCached = false;
        originalFollow = null;
    }

    private void ApplyPostPlaybackState(CinemachineCamera camera)
    {
        switch (postPlaybackState)
        {
            case TimelineCameraFollowTrack.PostPlaybackState.Revert:
                RestoreAfterClip(camera, false);
                Transform targetFollow = originalFollow;
                if (targetFollow == null || (targetFollow != null && targetFollow.name.Contains("Proxy")))
                {
                    Player player = PlayerRegistry.Current;
                    if (player != null)
                    {
                        targetFollow = player.transform;
                    }
                }

                if (targetFollow != null)
                {
                    camera.Follow = targetFollow;
                }
                break;

            case TimelineCameraFollowTrack.PostPlaybackState.KeepCurrent:
                if (postPlaybackHoldPositionCaptured)
                {
                    // Last clip was HoldPosition — create a permanent proxy so the position
                    // survives the proxy cleanup that follows.
                    GameObject permanentProxy = new GameObject("Timeline Camera PostPlayback Hold Proxy");
                    permanentProxy.transform.position = postPlaybackHoldPosition;
                    camera.Follow = permanentProxy.transform;
                }
                else if (postPlaybackFollow != null)
                {
                    camera.Follow = postPlaybackFollow;
                }
                // else: no clip ever applied, leave as-is.
                break;

            case TimelineCameraFollowTrack.PostPlaybackState.ClearFollow:
                camera.Follow = null;
                break;

            case TimelineCameraFollowTrack.PostPlaybackState.LeaveAsIs:
            default:
                // Don't touch camera.Follow at all.
                break;
        }

        clipApplied = false;
        lastAppliedClip = null;
    }


    /// <summary>
    /// A seek invalidates the interpolation start points that were captured when a clip began: the smooth follow
    /// origin, the projected and hold proxies, and the zoom start lens. Dropping them makes the follow position
    /// and the zoom rebuild from the clip's own local time at the new playhead.
    /// </summary>
    private void ResetTransitionStateForSeek(CinemachineCamera camera)
    {
        ClearSmoothFollowProxy();
        ClearProjectedFollowProxy();
        ClearHoldPositionProxy();
        ClearRestoreFollowProxy();

        if (originalLensCached && camera != null)
        {
            camera.Lens = originalLens;
        }

        RestoreConfinerIfNeeded();

        zoomStartLensCached = false;
        lastZoomClip = null;
        clipApplied = false;
        lastAppliedClip = null;
    }

    private ActiveCameraFollowClip GetStrongestActiveClip(Playable playable)
    {
        ActiveCameraFollowClip strongestClip = default;
        float strongestWeight = 0f;

        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0.001f || weight <= strongestWeight)
            {
                continue;
            }
            if (playable.GetInput(i).GetPlayableType() != typeof(TimelineCameraFollowBehaviour)) continue;
            ScriptPlayable<TimelineCameraFollowBehaviour> inputPlayable = (ScriptPlayable<TimelineCameraFollowBehaviour>)playable.GetInput(i);

            strongestClip = new ActiveCameraFollowClip
            {
                Behaviour = inputPlayable.GetBehaviour(),
                LocalTime = inputPlayable.GetTime(),
                Duration = inputPlayable.GetDuration()
            };
            strongestWeight = weight;
        }

        return strongestClip;
    }

    private void ApplyClip(CinemachineCamera camera, ActiveCameraFollowClip activeClip)
    {
        TimelineCameraFollowBehaviour clip = activeClip.Behaviour;

        if (!originalFollowCached)
        {
            originalFollow = camera.Follow;
            if (originalFollow == null || (originalFollow != null && originalFollow.name.Contains("Proxy")))
            {
                Player player = PlayerRegistry.Current;
                if (player != null)
                {
                    originalFollow = player.transform;
                }
            }
            originalFollowCached = true;
        }

        Transform beforeFollow = camera.Follow;
        Vector3 beforeFollowPosition = ResolveCurrentFollowOrCameraPosition(camera, beforeFollow);
        bool snapCamera = false;
        bool clipEntered = !clipApplied || lastAppliedClip != clip;

        switch (clip.action)
        {
            case TimelineCameraFollowAction.FollowTarget:
            case TimelineCameraFollowAction.FollowTargetInstant:
                ClearHoldPositionProxy();
                Transform resolvedTarget = clip.ResolveTarget();
                if (resolvedTarget == null)
                {
                    ClearRestoreFollowProxy();
                    return;
                }

                snapCamera = clip.action == TimelineCameraFollowAction.FollowTargetInstant;
                Transform followTarget = ResolveProjectedFollow(camera, clip, resolvedTarget);
                camera.Follow = snapCamera
                    ? followTarget
                    : ResolveSmoothFollow(camera, clip, followTarget, activeClip.LocalTime, beforeFollowPosition, beforeFollow);
                ClearRestoreFollowProxy();
                break;

            case TimelineCameraFollowAction.ClearFollow:
                ClearSmoothFollowProxy();
                ClearProjectedFollowProxy();
                ClearHoldPositionProxy();
                ClearRestoreFollowProxy();
                camera.Follow = null;
                break;

            case TimelineCameraFollowAction.RestoreOriginal:
                ClearSmoothFollowProxy();
                ClearProjectedFollowProxy();
                ClearHoldPositionProxy();
                ClearRestoreFollowProxy();
                camera.Follow = originalFollow;
                break;

            case TimelineCameraFollowAction.HoldPosition:
                ClearSmoothFollowProxy();
                ClearProjectedFollowProxy();
                ClearRestoreFollowProxy();
                camera.Follow = ResolveHoldPositionProxy(camera, clip, clipEntered);
                break;
        }

        bool followChanged = camera.Follow != beforeFollow && camera.Follow != lastCameraFollow;
        bool shouldSnap = snapCamera && (clipEntered || followChanged);

        if (clip.invalidatePreviousState && shouldSnap)
        {
            camera.PreviousStateIsValid = false;
        }

        if (shouldSnap)
        {
            camera.CancelDamping(true);
        }

        lastCameraFollow = camera.Follow;
        clipApplied = true;
        lastAppliedClip = clip;

        // Remember the follow target for post-playback KeepCurrent.
        switch (clip.action)
        {
            case TimelineCameraFollowAction.FollowTarget:
            case TimelineCameraFollowAction.FollowTargetInstant:
                Transform resolved = clip.ResolveTarget();
                if (resolved != null)
                {
                    postPlaybackFollow = resolved;
                    postPlaybackHoldPositionCaptured = false;
                }
                break;
            case TimelineCameraFollowAction.ClearFollow:
                postPlaybackFollow = null;
                postPlaybackHoldPositionCaptured = false;
                break;
            case TimelineCameraFollowAction.RestoreOriginal:
                postPlaybackFollow = originalFollow;
                postPlaybackHoldPositionCaptured = false;
                break;
            case TimelineCameraFollowAction.HoldPosition:
                if (holdPositionProxy != null)
                {
                    postPlaybackHoldPosition = holdPositionProxy.position;
                    postPlaybackHoldPositionCaptured = true;
                }
                postPlaybackFollow = null;
                break;
        }
    }

    private void ApplyZoom(CinemachineCamera camera, ActiveCameraFollowClip activeClip)
    {
        TimelineCameraFollowBehaviour clip = activeClip.Behaviour;
        if (!clip.enableZoom)
        {
            RestoreZoomIfNeeded();
            return;
        }

        if (!originalLensCached)
        {
            CacheOriginalLens(camera);
        }

        if (lastZoomClip != clip || !zoomStartLensCached)
        {
            zoomStartLens = camera.Lens;
            zoomStartLensCached = true;
        }

        LensSettings lens = camera.Lens;
        float t = clip.zoomDuration > 0f
            ? Mathf.Clamp01((float)activeClip.LocalTime / clip.zoomDuration)
            : 1f;
        t = Mathf.SmoothStep(0f, 1f, t);

        float targetOrtho = clip.zoomOrthographicSize;
        if (zoomStartLens.OrthographicSize > 0f && targetOrtho > 0f)
        {
            if (clip.zoomDirection == TimelineCameraZoomDirection.ZoomIn)
            {
                targetOrtho = Mathf.Min(targetOrtho, zoomStartLens.OrthographicSize);
            }
            else if (clip.zoomDirection == TimelineCameraZoomDirection.ZoomOut)
            {
                targetOrtho = Mathf.Max(targetOrtho, zoomStartLens.OrthographicSize);
            }
        }

        float targetFov = clip.zoomFieldOfView;
        if (zoomStartLens.FieldOfView > 0f && targetFov > 0f)
        {
            if (clip.zoomDirection == TimelineCameraZoomDirection.ZoomIn)
            {
                targetFov = Mathf.Min(targetFov, zoomStartLens.FieldOfView);
            }
            else if (clip.zoomDirection == TimelineCameraZoomDirection.ZoomOut)
            {
                targetFov = Mathf.Max(targetFov, zoomStartLens.FieldOfView);
            }
        }

        lens.OrthographicSize = Mathf.Lerp(
            zoomStartLens.OrthographicSize,
            targetOrtho,
            t);
        lens.FieldOfView = Mathf.Lerp(
            zoomStartLens.FieldOfView,
            targetFov,
            t);
        camera.Lens = lens;

        if (activeConfiner != null)
        {
            activeConfiner.InvalidateLensCache();
        }

        zoomApplied = true;
        lastZoomClip = clip;
    }

    private void CacheOriginalLens(CinemachineCamera camera)
    {
        if (camera == null)
        {
            originalLensCached = false;
            return;
        }

        originalLens = camera.Lens;
        originalLensCached = true;
        zoomStartLens = originalLens;
        zoomStartLensCached = false;
        zoomApplied = false;
        lastZoomClip = null;
    }

    private void RestoreZoomIfNeeded()
    {
        if (!zoomApplied || activeCamera == null || !originalLensCached)
        {
            return;
        }

        if (lastZoomClip == null || lastZoomClip.restoreZoomOnExit)
        {
            activeCamera.Lens = originalLens;
        }
        else
        {
            originalLens = activeCamera.Lens;
        }

        if (activeConfiner != null)
        {
            activeConfiner.InvalidateLensCache();
        }

        zoomApplied = false;
        zoomStartLensCached = false;
        lastZoomClip = null;
    }

    private void CacheOriginalConfiner(CinemachineCamera camera)
    {
        if (camera == null)
        {
            activeConfiner = null;
            originalConfinerCached = false;
            return;
        }

        activeConfiner = camera.GetComponent<CinemachineConfiner2D>();
        if (activeConfiner != null)
        {
            originalConfinerEnabled = activeConfiner.enabled;
            originalBoundingShape2D = activeConfiner.BoundingShape2D;
            originalConfinerCached = true;
        }
        else
        {
            originalConfinerCached = false;
            originalBoundingShape2D = null;
        }

        confinerApplied = false;
        lastConfinerClip = null;
    }

    private void ApplyConfiner(CinemachineCamera camera, ActiveCameraFollowClip activeClip)
    {
        TimelineCameraFollowBehaviour clip = activeClip.Behaviour;
        if (activeConfiner == null)
        {
            activeConfiner = camera != null ? camera.GetComponent<CinemachineConfiner2D>() : null;
            if (activeConfiner == null)
            {
                return;
            }

            if (!originalConfinerCached)
            {
                originalConfinerEnabled = activeConfiner.enabled;
                originalBoundingShape2D = activeConfiner.BoundingShape2D;
                originalConfinerCached = true;
            }
        }

        switch (clip.confinerMode)
        {
            case TimelineCameraConfinerMode.KeepDefault:
                // Nếu clip trước đó đã can thiệp, hãy khôi phục lại mặc định
                if (confinerApplied && (lastConfinerClip == null || lastConfinerClip.restoreConfinerOnExit))
                {
                    activeConfiner.enabled = originalConfinerEnabled;
                    if (activeConfiner.BoundingShape2D != originalBoundingShape2D)
                    {
                        activeConfiner.BoundingShape2D = originalBoundingShape2D;
                        activeConfiner.InvalidateBoundingShapeCache();
                    }
                    confinerApplied = false;
                }
                break;

            case TimelineCameraConfinerMode.DisableConfiner:
                if (activeConfiner.enabled)
                {
                    activeConfiner.enabled = false;
                }
                confinerApplied = true;
                break;

            case TimelineCameraConfinerMode.OverrideBoundingShape:
                if (!activeConfiner.enabled)
                {
                    activeConfiner.enabled = true;
                }

                if (clip.boundingShape2D != null && activeConfiner.BoundingShape2D != clip.boundingShape2D)
                {
                    activeConfiner.BoundingShape2D = clip.boundingShape2D;
                    activeConfiner.InvalidateBoundingShapeCache();
                }
                confinerApplied = true;
                break;
        }

        lastConfinerClip = clip;
    }

    private void RestoreConfinerIfNeeded()
    {
        if (!confinerApplied || activeConfiner == null || !originalConfinerCached)
        {
            return;
        }

        if (lastConfinerClip == null || lastConfinerClip.restoreConfinerOnExit)
        {
            activeConfiner.enabled = originalConfinerEnabled;
            if (activeConfiner.BoundingShape2D != originalBoundingShape2D)
            {
                activeConfiner.BoundingShape2D = originalBoundingShape2D;
                activeConfiner.InvalidateBoundingShapeCache();
            }
        }
        else
        {
            // Keep current confiner settings as new baseline
            originalConfinerEnabled = activeConfiner.enabled;
            originalBoundingShape2D = activeConfiner.BoundingShape2D;
        }

        confinerApplied = false;
        lastConfinerClip = null;
    }

    private Transform ResolveSmoothFollow(
        CinemachineCamera camera,
        TimelineCameraFollowBehaviour clip,
        Transform resolvedTarget,
        double localTime,
        Vector3 initialSourcePosition,
        Transform beforeFollow)
    {
        if (clip.smoothFollowDuration <= 0f)
        {
            ClearSmoothFollowProxy();
            return resolvedTarget;
        }

        bool newSmoothTarget = smoothFollowProxy == null
            || smoothFollowClip != clip
            || smoothFollowTarget != resolvedTarget;

        if (newSmoothTarget)
        {
            smoothFollowProxy = GetOrCreateSmoothFollowProxy();
            smoothFollowStartPosition = ResolveSmoothStartPosition(camera, resolvedTarget, initialSourcePosition, beforeFollow);
            smoothFollowProxy.SetPositionAndRotation(smoothFollowStartPosition, Quaternion.identity);
            smoothFollowProxy.localScale = Vector3.one;
            smoothFollowClip = clip;
            smoothFollowTarget = resolvedTarget;
        }

        float duration = Mathf.Max(0.0001f, clip.smoothFollowDuration);
        float t = newSmoothTarget && localTime <= 0.0001
            ? 0f
            : Mathf.Clamp01((float)localTime / duration);
        t = Mathf.SmoothStep(0f, 1f, t);
        Vector3 targetPos = resolvedTarget.position;
        targetPos.z = EnsureSafeCameraZ(camera, targetPos.z);
        Vector3 nextPos = Vector3.Lerp(smoothFollowStartPosition, targetPos, t);
        nextPos.z = EnsureSafeCameraZ(camera, nextPos.z);
        smoothFollowProxy.position = nextPos;

        return t >= 0.999f ? resolvedTarget : smoothFollowProxy;
    }

    private Vector3 ResolveCurrentFollowOrCameraPosition(CinemachineCamera camera, Transform currentFollow)
    {
        if (currentFollow != null)
        {
            Vector3 pos = currentFollow.position;
            pos.z = EnsureSafeCameraZ(camera, pos.z);
            return pos;
        }

        return ResolveCurrentCameraWorldPosition(camera);
    }

    private Vector3 ResolveSmoothStartPosition(
        CinemachineCamera camera,
        Transform resolvedTarget,
        Vector3 initialSourcePosition,
        Transform beforeFollow)
    {
        if (beforeFollow != null && beforeFollow != smoothFollowProxy)
        {
            return initialSourcePosition;
        }

        if (smoothFollowProxy != null && camera != null && camera.Follow == smoothFollowProxy)
        {
            Vector3 pos = smoothFollowProxy.position;
            pos.z = EnsureSafeCameraZ(camera, pos.z);
            return pos;
        }

        if (camera != null)
        {
            Vector3 cameraPosition = camera.transform.position;
            cameraPosition.z = EnsureSafeCameraZ(camera, resolvedTarget != null ? resolvedTarget.position.z : -10f);
            return cameraPosition;
        }

        return initialSourcePosition;
    }

    private void ApplyShake(ActiveCameraFollowClip activeClip)
    {
        TimelineCameraFollowBehaviour clip = activeClip.Behaviour;
        if (!Application.isPlaying || !clip.enableShake || clip.shakeAmplitude <= 0f)
        {
            StopShake();
            return;
        }

        Camera shakeCam = ResolveShakeCamera(clip);
        if (shakeCam == null)
        {
            StopShake();
            return;
        }

        TimelineCameraShakeDriver driver = shakeCam.GetComponent<TimelineCameraShakeDriver>();
        if (driver == null)
        {
            driver = shakeCam.gameObject.AddComponent<TimelineCameraShakeDriver>();
        }

        activeShakeDriver = driver;
        driver.Apply(
            clip.shakeAmplitude,
            clip.shakeFrequency,
            clip.shakeEnvelope,
            activeClip.LocalTime,
            activeClip.Duration);
    }

    private Camera ResolveShakeCamera(TimelineCameraFollowBehaviour clip)
    {
        if (clip.shakeCamera != null)
        {
            return clip.shakeCamera;
        }

        if (Camera.main != null)
        {
            return Camera.main;
        }

#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<Camera>();
#else
        return Object.FindObjectOfType<Camera>();
#endif
    }

    private void StopShake()
    {
        if (activeShakeDriver == null)
        {
            return;
        }

        activeShakeDriver.StopShake();
        activeShakeDriver = null;
    }

    private void RestoreAfterClip(CinemachineCamera camera, bool smoothRestore = true)
    {
        if (!clipApplied || lastAppliedClip == null)
        {
            RestoreZoomIfNeeded();
            return;
        }

        TimelineCameraFollowBehaviour finishedClip = lastAppliedClip;
        RestoreZoomIfNeeded();

        switch (finishedClip.exitMode)
        {
            case TimelineCameraFollowExitMode.RestorePrevious:
                if (smoothRestore && finishedClip.smoothFollowDuration > 0f)
                {
                    BeginRestorePrevious(camera, finishedClip);
                }
                else
                {
                    KeepFinishedClipTarget(camera, finishedClip);
                }
                break;
            case TimelineCameraFollowExitMode.ClearFollow:
                ClearSmoothFollowProxy();
                ClearProjectedFollowProxy();
                ClearHoldPositionProxy();
                ClearRestoreFollowProxy();
                camera.Follow = null;
                break;
            case TimelineCameraFollowExitMode.KeepCurrent:
                if (projectedFollowProxy != null)
                {
                    camera.Follow = projectedFollowProxy;
                }
                else if (holdPositionProxy != null)
                {
                    camera.Follow = holdPositionProxy;
                }
                else if (smoothFollowTarget != null)
                {
                    camera.Follow = smoothFollowTarget;
                }
                ClearSmoothFollowProxy();
                ClearRestoreFollowProxy();
                break;
        }

        if (finishedClip.invalidatePreviousState
            && finishedClip.action == TimelineCameraFollowAction.FollowTargetInstant)
        {
            camera.PreviousStateIsValid = false;
        }

        lastCameraFollow = camera.Follow;
        clipApplied = false;
        lastAppliedClip = null;
    }

    private bool KeepFinishedClipTarget(CinemachineCamera camera, TimelineCameraFollowBehaviour finishedClip)
    {
        ClearSmoothFollowProxy();
        ClearProjectedFollowProxy();
        ClearHoldPositionProxy();
        ClearRestoreFollowProxy();

        Transform resolvedTarget = finishedClip.ResolveTarget();
        if (resolvedTarget != null
            && (finishedClip.action == TimelineCameraFollowAction.FollowTarget
                || finishedClip.action == TimelineCameraFollowAction.FollowTargetInstant))
        {
            camera.Follow = resolvedTarget;
            return true;
        }

        return false;
    }

    private void BeginRestorePrevious(CinemachineCamera camera, TimelineCameraFollowBehaviour finishedClip)
    {
        if (originalFollow == null || finishedClip.smoothFollowDuration <= 0f)
        {
            ClearSmoothFollowProxy();
            ClearProjectedFollowProxy();
            ClearHoldPositionProxy();
            ClearRestoreFollowProxy();
            camera.Follow = originalFollow;
            return;
        }

        restoreFollowProxy = GetOrCreateRestoreFollowProxy();
        restoreFollowTarget = originalFollow;
        restoreFollowStartPosition = ResolveCurrentCameraWorldPosition(camera);
        restoreFollowElapsed = 0f;
        restoreFollowDuration = Mathf.Max(0.0001f, finishedClip.smoothFollowDuration);
        restoreFollowActive = true;

        restoreFollowProxy.position = restoreFollowStartPosition;
        restoreFollowProxy.rotation = Quaternion.identity;
        restoreFollowProxy.localScale = Vector3.one;
        camera.Follow = restoreFollowProxy;

        ClearSmoothFollowProxy();
        ClearProjectedFollowProxy();
        ClearHoldPositionProxy();
    }

    private void TickRestoreFollow(CinemachineCamera camera)
    {
        if (!restoreFollowActive || restoreFollowProxy == null)
        {
            return;
        }

        if (restoreFollowTarget == null)
        {
            ClearRestoreFollowProxy();
            camera.Follow = null;
            lastCameraFollow = camera.Follow;
            return;
        }

        restoreFollowElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(restoreFollowElapsed / restoreFollowDuration);
        t = Mathf.SmoothStep(0f, 1f, t);
        restoreFollowProxy.position = Vector3.Lerp(
            restoreFollowStartPosition,
            restoreFollowTarget.position,
            t);

        if (t < 0.999f)
        {
            return;
        }

        camera.Follow = restoreFollowTarget;
        ClearRestoreFollowProxy();
        lastCameraFollow = camera.Follow;
    }

    private Transform GetOrCreateRestoreFollowProxy()
    {
        if (restoreFollowProxy != null)
        {
            return restoreFollowProxy;
        }

        GameObject proxyObject = new GameObject("Timeline Camera Restore Follow Proxy");
        proxyObject.hideFlags = HideFlags.HideAndDontSave;
        restoreFollowProxy = proxyObject.transform;
        return restoreFollowProxy;
    }

    private void ClearRestoreFollowProxy()
    {
        restoreFollowTarget = null;
        restoreFollowActive = false;
        restoreFollowElapsed = 0f;
        restoreFollowDuration = 0f;

        if (restoreFollowProxy == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(restoreFollowProxy.gameObject);
        }
        else
        {
            Object.DestroyImmediate(restoreFollowProxy.gameObject);
        }

        restoreFollowProxy = null;
    }

    private Transform GetOrCreateSmoothFollowProxy()
    {
        if (smoothFollowProxy != null)
        {
            return smoothFollowProxy;
        }

        GameObject proxyObject = new GameObject("Timeline Camera Smooth Follow Proxy");
        proxyObject.hideFlags = HideFlags.HideAndDontSave;
        smoothFollowProxy = proxyObject.transform;
        return smoothFollowProxy;
    }

    private Transform ResolveProjectedFollow(
        CinemachineCamera camera,
        TimelineCameraFollowBehaviour clip,
        Transform resolvedTarget)
    {
        if (resolvedTarget == null)
        {
            ClearProjectedFollowProxy();
            return resolvedTarget;
        }

        bool hasOffset = clip.followOffset.sqrMagnitude > 0.000001f;
        if (clip.targetMode == TimelineCameraFollowTargetMode.DirectTransform && !hasOffset)
        {
            ClearProjectedFollowProxy();
            return resolvedTarget;
        }

        projectedFollowProxy = GetOrCreateProjectedFollowProxy();
        projectedFollowClip = clip;

        Vector3 projectedPosition = resolvedTarget.position + clip.followOffset;
        if (clip.targetMode == TimelineCameraFollowTargetMode.ProjectTo2DPlane)
        {
            projectedPosition.z = ResolveProjectedZ(camera, resolvedTarget) + clip.followOffset.z;
        }

        projectedPosition.z = EnsureSafeCameraZ(camera, projectedPosition.z);
        projectedFollowProxy.position = projectedPosition;
        projectedFollowProxy.rotation = Quaternion.identity;
        projectedFollowProxy.localScale = Vector3.one;
        return projectedFollowProxy;
    }

    private float ResolveProjectedZ(CinemachineCamera camera, Transform resolvedTarget)
    {
        float z;
        switch (projectedFollowClip != null ? projectedFollowClip.targetZMode : TimelineCameraFollowZMode.KeepCurrentFollowZ)
        {
            case TimelineCameraFollowZMode.KeepCameraZ:
                z = camera != null ? camera.transform.position.z : resolvedTarget.position.z;
                break;

            case TimelineCameraFollowZMode.UseTargetZ:
                z = resolvedTarget.position.z;
                break;

            case TimelineCameraFollowZMode.FixedWorldZ:
                z = projectedFollowClip != null ? projectedFollowClip.fixedWorldZ : -10f;
                break;

            default:
                if (camera != null && camera.Follow != null)
                {
                    z = camera.Follow.position.z;
                }
                else
                {
                    z = projectedFollowProxy != null
                        ? projectedFollowProxy.position.z
                        : resolvedTarget.position.z;
                }
                break;
        }

        return EnsureSafeCameraZ(camera, z);
    }

    private static float EnsureSafeCameraZ(CinemachineCamera camera, float z)
    {
        if (Mathf.Abs(z) >= 0.1f)
        {
            return z;
        }

        if (camera != null && Mathf.Abs(camera.transform.position.z) >= 0.1f)
        {
            return camera.transform.position.z;
        }

        return -10f;
    }

    private Transform GetOrCreateProjectedFollowProxy()
    {
        if (projectedFollowProxy != null)
        {
            return projectedFollowProxy;
        }

        GameObject proxyObject = new GameObject("Timeline Camera 2D Follow Proxy");
        proxyObject.hideFlags = HideFlags.HideAndDontSave;
        projectedFollowProxy = proxyObject.transform;
        return projectedFollowProxy;
    }

    private void ClearSmoothFollowProxy()
    {
        smoothFollowClip = null;
        smoothFollowTarget = null;

        if (smoothFollowProxy == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(smoothFollowProxy.gameObject);
        }
        else
        {
            Object.DestroyImmediate(smoothFollowProxy.gameObject);
        }

        smoothFollowProxy = null;
    }

    private void ClearProjectedFollowProxy()
    {
        projectedFollowClip = null;

        if (projectedFollowProxy == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(projectedFollowProxy.gameObject);
        }
        else
        {
            Object.DestroyImmediate(projectedFollowProxy.gameObject);
        }

        projectedFollowProxy = null;
    }

    private Transform ResolveHoldPositionProxy(
        CinemachineCamera camera,
        TimelineCameraFollowBehaviour clip,
        bool clipEntered)
    {
        if (holdPositionProxy == null)
        {
            GameObject proxyObject = new GameObject("Timeline Camera Hold Position Proxy");
            proxyObject.hideFlags = HideFlags.HideAndDontSave;
            holdPositionProxy = proxyObject.transform;
        }

        if (clipEntered || holdPositionClip != clip)
        {
            holdPositionProxy.position = ResolveCurrentCameraWorldPosition(camera);
            holdPositionProxy.rotation = Quaternion.identity;
            holdPositionProxy.localScale = Vector3.one;
            holdPositionClip = clip;
        }

        return holdPositionProxy;
    }

    private Vector3 ResolveCurrentCameraWorldPosition(CinemachineCamera camera)
    {
        if (camera == null)
        {
            return new Vector3(0f, 0f, -10f);
        }

        Transform cameraTransform = camera.transform;
        Vector3 position = cameraTransform.position;
        if (camera.Follow != null)
        {
            position.z = camera.Follow.position.z;
        }

        position.z = EnsureSafeCameraZ(camera, position.z);
        return position;
    }

    private void ClearHoldPositionProxy()
    {
        holdPositionClip = null;

        if (holdPositionProxy == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(holdPositionProxy.gameObject);
        }
        else
        {
            Object.DestroyImmediate(holdPositionProxy.gameObject);
        }

        holdPositionProxy = null;
    }

    private struct ActiveCameraFollowClip
    {
        public TimelineCameraFollowBehaviour Behaviour;
        public double LocalTime;
        public double Duration;
    }
}

[DefaultExecutionOrder(10000)]
internal sealed class TimelineCameraShakeDriver : MonoBehaviour
{
    private float amplitude;
    private float frequency;
    private AnimationCurve envelope;
    private double localTime;
    private double duration;
    private int appliedFrame = -1;
    private Vector3 previousOffset;
    private bool hasOffset;
    private float seedX;
    private float seedY;

    private void Awake()
    {
        seedX = Random.Range(0f, 1000f);
        seedY = Random.Range(1000f, 2000f);
    }

    public void Apply(
        float shakeAmplitude,
        float shakeFrequency,
        AnimationCurve shakeEnvelope,
        double clipLocalTime,
        double clipDuration)
    {
        amplitude = Mathf.Max(0f, shakeAmplitude);
        frequency = Mathf.Max(0.01f, shakeFrequency);
        envelope = shakeEnvelope;
        localTime = clipLocalTime;
        duration = clipDuration;
        appliedFrame = Time.frameCount;
    }

    public void StopShake()
    {
        RemovePreviousOffset();
        appliedFrame = -1;
    }

    private void LateUpdate()
    {
        RemovePreviousOffset();

        if (appliedFrame != Time.frameCount || amplitude <= 0f)
        {
            return;
        }

        float normalizedTime = duration > 0d
            ? Mathf.Clamp01((float)(localTime / duration))
            : 0f;
        float envelopeValue = envelope != null ? Mathf.Max(0f, envelope.Evaluate(normalizedTime)) : 1f;
        if (envelopeValue <= 0f)
        {
            return;
        }

        float phase = (float)localTime * frequency;
        float x = Mathf.PerlinNoise(seedX, phase) * 2f - 1f;
        float y = Mathf.PerlinNoise(seedY, phase) * 2f - 1f;

        previousOffset = new Vector3(x, y, 0f) * (amplitude * envelopeValue);
        transform.position += previousOffset;
        hasOffset = true;
    }

    private void OnDisable()
    {
        StopShake();
    }

    private void OnDestroy()
    {
        StopShake();
    }

    private void RemovePreviousOffset()
    {
        if (!hasOffset)
        {
            return;
        }

        transform.position -= previousOffset;
        previousOffset = Vector3.zero;
        hasOffset = false;
    }
}

