using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum TimelineCameraFollowAction
{
    FollowTarget = 0,
    ClearFollow = 1,
    RestoreOriginal = 2,
    FollowTargetInstant = 3,
    HoldPosition = 4
}

public enum TimelineCameraFollowExitMode
{
    RestorePrevious,
    KeepCurrent,
    ClearFollow
}

public enum TimelineCameraFollowTargetMode
{
    DirectTransform,
    ProjectTo2DPlane
}

public enum TimelineCameraFollowZMode
{
    KeepCurrentFollowZ,
    KeepCameraZ,
    UseTargetZ,
    FixedWorldZ
}

public enum TimelineCameraZoomDirection
{
    ZoomIn,
    ZoomOut
}

public enum TimelineCameraConfinerMode
{
    KeepDefault = 0,
    OverrideBoundingShape = 1,
    DisableConfiner = 2
}

public class TimelineCameraFollowClip : PlayableAsset, ITimelineClipAsset
{

    public bool invalidatePreviousState = true;
    public bool applyInEditMode;
    public bool enableZoom = false;
    public bool restoreZoomOnExit = true;
    public bool enableShake;
    public bool restoreConfinerOnExit = false;

    public float smoothFollowDuration = 0.75f;
    public float shakeAmplitude = 0.3f;
    public float shakeFrequency = 30f;
    public float zoomOrthographicSize = 15f;
    public float zoomFieldOfView = 40f;
    public float zoomDuration = 0.5f;
    private AnimationCurve shakeEnvelope = AnimationCurve.EaseInOut(0f, 1f, 0f, 1f);
    public TimelineCameraZoomDirection zoomDirection = TimelineCameraZoomDirection.ZoomIn;

    public TimelineCameraConfinerMode confinerMode = TimelineCameraConfinerMode.KeepDefault;

    public TimelineCameraFollowAction action = TimelineCameraFollowAction.FollowTarget;
    public TimelineCameraFollowExitMode exitMode = TimelineCameraFollowExitMode.RestorePrevious;
    public TimelineCameraFollowTargetMode targetMode = TimelineCameraFollowTargetMode.ProjectTo2DPlane;
    public TimelineCameraFollowZMode targetZMode = TimelineCameraFollowZMode.KeepCurrentFollowZ;
    public float fixedWorldZ;
    public Vector3 followOffset;
    public ExposedReference<Collider2D> boundingShape2D;
    public ExposedReference<Camera> shakeCamera;
    public ExposedReference<Transform> target;

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<TimelineCameraFollowBehaviour> playable =
            ScriptPlayable<TimelineCameraFollowBehaviour>.Create(graph);

        TimelineCameraFollowBehaviour behaviour = playable.GetBehaviour();
        behaviour.action = action;
        behaviour.exitMode = exitMode;
        behaviour.targetMode = targetMode;
        behaviour.targetZMode = targetZMode;
        behaviour.fixedWorldZ = fixedWorldZ;
        behaviour.followOffset = followOffset;
        behaviour.target = target.Resolve(graph.GetResolver());
        behaviour.findPlayerWhenTargetMissing = behaviour.target == null;
        behaviour.smoothFollowDuration = Mathf.Max(0f, smoothFollowDuration);
        behaviour.invalidatePreviousState = invalidatePreviousState;
        behaviour.applyInEditMode = applyInEditMode;
        behaviour.enableZoom = enableZoom;
        behaviour.zoomDirection = zoomDirection;
        behaviour.zoomOrthographicSize = Mathf.Max(0.01f, zoomOrthographicSize);
        behaviour.zoomFieldOfView = Mathf.Max(1f, zoomFieldOfView);
        behaviour.zoomDuration = Mathf.Max(0f, zoomDuration);
        behaviour.restoreZoomOnExit = restoreZoomOnExit;
        behaviour.enableShake = enableShake;
        behaviour.shakeCamera = shakeCamera.Resolve(graph.GetResolver());
        behaviour.shakeAmplitude = shakeAmplitude;
        behaviour.shakeFrequency = shakeFrequency;
        behaviour.shakeEnvelope = shakeEnvelope;

        behaviour.confinerMode = confinerMode;
        behaviour.boundingShape2D = boundingShape2D.Resolve(graph.GetResolver());
        behaviour.restoreConfinerOnExit = restoreConfinerOnExit;

        return playable;
    }
}
