using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;

public sealed class TimelineCameraPointBReturnBehaviour : PlayableBehaviour
{
    public Transform pointB;
    public float moveToBDuration;
    public float zoomDuration;
    public float holdDuration;
    public float returnDuration;
    public float targetOrthographicSize;
    public float targetFieldOfView;
    public AnimationCurve moveCurve;
    public AnimationCurve zoomCurve;
    public AnimationCurve returnCurve;

    private CinemachineCamera camera;
    private Transform initialFollow;
    private LensSettings initialLens;
    private Vector3 pointAPosition;
    private Quaternion pointARotation;
    private bool active;

    public void Enter(CinemachineCamera boundCamera)
    {
        if (boundCamera == null || pointB == null)
        {
            Exit();
            return;
        }

        if (active && camera == boundCamera)
        {
            return;
        }

        Exit();
        camera = boundCamera;
        initialFollow = camera.Follow;
        initialLens = camera.Lens;
        pointAPosition = GetCurrentCameraPosition();
        pointARotation = camera.transform.rotation;
        camera.Follow = null;
        camera.ForceCameraPosition(pointAPosition, pointARotation);
        active = true;
    }

    public void Process(float localTime, float clipDuration)
    {
        if (!active || camera == null || pointB == null)
        {
            return;
        }

        float configuredDuration = GetConfiguredDuration();
        float normalizedClipTime = clipDuration > 0f
            ? Mathf.Clamp01(localTime / clipDuration)
            : 1f;
        float time = normalizedClipTime * configuredDuration;

        float moveEnd = moveToBDuration;
        float zoomEnd = moveEnd + zoomDuration;
        float holdEnd = zoomEnd + holdDuration;
        Vector3 pointBPosition = GetPointBPosition();

        if (time < moveEnd)
        {
            float t = Evaluate(moveCurve, time, moveToBDuration);
            ApplyPose(
                Vector3.LerpUnclamped(pointAPosition, pointBPosition, t),
                Quaternion.Slerp(pointARotation, pointB.rotation, t));
            camera.Lens = initialLens;
            return;
        }

        if (time < zoomEnd)
        {
            ApplyPose(pointBPosition, pointB.rotation);
            ApplyZoom(Evaluate(zoomCurve, time - moveEnd, zoomDuration));
            return;
        }

        if (time < holdEnd)
        {
            ApplyPose(pointBPosition, pointB.rotation);
            ApplyZoom(1f);
            return;
        }

        float returnT = Evaluate(returnCurve, time - holdEnd, returnDuration);
        ApplyPose(
            Vector3.LerpUnclamped(pointBPosition, pointAPosition, returnT),
            Quaternion.Slerp(pointB.rotation, pointARotation, returnT));
        ApplyZoom(1f - returnT);
    }

    public void Exit()
    {
        if (!active)
        {
            camera = null;
            return;
        }

        if (camera != null)
        {
            camera.Lens = initialLens;
            camera.ForceCameraPosition(pointAPosition, pointARotation);
            camera.Follow = initialFollow;
            camera.PreviousStateIsValid = false;
        }

        active = false;
        camera = null;
        initialFollow = null;
    }

    private void ApplyPose(Vector3 position, Quaternion rotation)
    {
        position.z = pointAPosition.z;
        camera.ForceCameraPosition(position, rotation);
    }

    private Vector3 GetPointBPosition()
    {
        Vector3 position = pointB.position;
        position.z = pointAPosition.z;
        return position;
    }

    private void ApplyZoom(float t)
    {
        LensSettings lens = initialLens;
        if (lens.Orthographic)
        {
            lens.OrthographicSize = Mathf.Lerp(
                initialLens.OrthographicSize,
                targetOrthographicSize,
                t);
        }
        else
        {
            lens.FieldOfView = Mathf.Lerp(
                initialLens.FieldOfView,
                targetFieldOfView,
                t);
        }

        camera.Lens = lens;
    }

    private Vector3 GetCurrentCameraPosition()
    {
        if (camera.PreviousStateIsValid)
        {
            return camera.State.GetFinalPosition();
        }

        return camera.transform.position;
    }

    private float GetConfiguredDuration()
    {
        return Mathf.Max(
            0.001f,
            moveToBDuration + zoomDuration + holdDuration + returnDuration);
    }

    private static float Evaluate(AnimationCurve curve, float elapsed, float duration)
    {
        if (duration <= 0f)
        {
            return 1f;
        }

        float t = Mathf.Clamp01(elapsed / duration);
        return curve != null ? curve.Evaluate(t) : Mathf.SmoothStep(0f, 1f, t);
    }
}
