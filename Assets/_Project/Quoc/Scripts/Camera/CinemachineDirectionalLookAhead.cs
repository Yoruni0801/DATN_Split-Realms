using Unity.Cinemachine;
using UnityEngine;

[AddComponentMenu("Cinemachine/Extensions/Directional Look Ahead")]
[DisallowMultipleComponent]
public class CinemachineDirectionalLookAhead : CinemachineExtension
{
    [Header("Target")]
    [SerializeField, Tooltip("Object whose horizontal movement controls the camera offset. Uses the Cinemachine Camera Follow target when empty.")]
    private Transform target;

    [SerializeField, Min(0f), Tooltip("Minimum horizontal speed required before the camera starts looking ahead.")]
    private float movementThreshold = 0.05f;

    [Header("Look Ahead")]
    [SerializeField, Tooltip("World-space camera offset while the target is stationary.")]
    private Vector3 idleOffset = Vector3.zero;

    [SerializeField, Tooltip("World-space camera offset while the target moves right. Positive X shows more space on the right.")]
    private Vector3 moveRightOffset = new Vector3(3f, 0f, 0f);

    [SerializeField, Tooltip("World-space camera offset while the target moves left. Negative X shows more space on the left.")]
    private Vector3 moveLeftOffset = new Vector3(-3f, 0f, 0f);

    [SerializeField, Min(0.001f), Tooltip("Time for the camera to settle on a new offset. Lower values respond faster.")]
    private float smoothTime = 0.25f;

    [Header("Camera Bounds")]
    [SerializeField, Tooltip("Clamp the final camera position to the bounds below.")]
    private bool useBounds = true;

    [SerializeField, Tooltip("Minimum world position allowed for the camera.")]
    private Vector3 minBounds = new Vector3(-20f, -10f, -100f);

    [SerializeField, Tooltip("Maximum world position allowed for the camera.")]
    private Vector3 maxBounds = new Vector3(20f, 10f, 100f);

    private Vector3 previousTargetPosition;
    private Vector3 currentOffset;
    private Vector3 offsetVelocity;
    private bool hasPreviousTargetPosition;

    protected override void OnEnable()
    {
        base.OnEnable();
        ResetState();
    }

    private void OnDisable()
    {
        ResetState();
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase virtualCamera,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Body)
            return;

        Transform followTarget = target != null ? target : virtualCamera.Follow;
        if (followTarget == null)
            return;

        if (!hasPreviousTargetPosition || deltaTime < 0f)
        {
            previousTargetPosition = followTarget.position;
            currentOffset = idleOffset;
            offsetVelocity = Vector3.zero;
            hasPreviousTargetPosition = true;

            if (deltaTime < 0f)
                return;
        }

        float horizontalSpeed = deltaTime > 0f
            ? (followTarget.position.x - previousTargetPosition.x) / deltaTime
            : 0f;
        previousTargetPosition = followTarget.position;

        Vector3 targetOffset = idleOffset;
        if (horizontalSpeed > movementThreshold)
            targetOffset = moveRightOffset;
        else if (horizontalSpeed < -movementThreshold)
            targetOffset = moveLeftOffset;

        currentOffset = Vector3.SmoothDamp(
            currentOffset,
            targetOffset,
            ref offsetVelocity,
            smoothTime,
            Mathf.Infinity,
            Mathf.Max(deltaTime, 0f));

        state.RawPosition += currentOffset;

        if (useBounds)
        {
            Vector3 finalPosition = state.RawPosition;
            Vector3 clampedPosition = new Vector3(
                Mathf.Clamp(finalPosition.x, Mathf.Min(minBounds.x, maxBounds.x), Mathf.Max(minBounds.x, maxBounds.x)),
                Mathf.Clamp(finalPosition.y, Mathf.Min(minBounds.y, maxBounds.y), Mathf.Max(minBounds.y, maxBounds.y)),
                Mathf.Clamp(finalPosition.z, Mathf.Min(minBounds.z, maxBounds.z), Mathf.Max(minBounds.z, maxBounds.z)));
            state.RawPosition = clampedPosition;
        }
    }

    public override void OnTargetObjectWarped(
        CinemachineVirtualCameraBase virtualCamera,
        Transform warpedTarget,
        Vector3 positionDelta)
    {
        if (warpedTarget == (target != null ? target : virtualCamera.Follow))
            previousTargetPosition += positionDelta;
    }

    public override void ForceCameraPosition(
        CinemachineVirtualCameraBase virtualCamera,
        Vector3 position,
        Quaternion rotation)
    {
        ResetState();
    }

    private void ResetState()
    {
        hasPreviousTargetPosition = false;
        currentOffset = idleOffset;
        offsetVelocity = Vector3.zero;
    }

    private void OnValidate()
    {
        movementThreshold = Mathf.Max(0f, movementThreshold);
        smoothTime = Mathf.Max(0.001f, smoothTime);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 boundsCenter = (minBounds + maxBounds) * 0.5f;
        Vector3 boundsSize = new Vector3(
            Mathf.Abs(maxBounds.x - minBounds.x),
            Mathf.Abs(maxBounds.y - minBounds.y),
            Mathf.Abs(maxBounds.z - minBounds.z));

        Gizmos.color = useBounds
            ? new Color(0.1f, 0.8f, 1f, 0.9f)
            : new Color(0.5f, 0.5f, 0.5f, 0.65f);
        Gizmos.DrawWireCube(boundsCenter, boundsSize);

        if (useBounds)
        {
            Color fillColor = Gizmos.color;
            fillColor.a = 0.06f;
            Gizmos.color = fillColor;
            Gizmos.DrawCube(boundsCenter, boundsSize);
        }
    }
}
