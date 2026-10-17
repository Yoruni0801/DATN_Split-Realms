using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.85f, 0.4f, 0.2f)]
[TrackClipType(typeof(CinemachineCameraShakeClip))]
[TrackBindingType(typeof(CinemachineVirtualCameraBase))]
[System.ComponentModel.DisplayName("TimelineTrack/Cinemachine Camera Shake")]
public sealed class CinemachineCameraShakeTrack : TrackAsset
{
    public override Playable CreateTrackMixer(
        PlayableGraph graph,
        GameObject gameObject,
        int inputCount)
    {
        return ScriptPlayable<CinemachineCameraShakeMixer>.Create(graph, inputCount);
    }
}

public sealed class CinemachineCameraShakeMixer : PlayableBehaviour
{
    private CinemachineTimelineShakeExtension shakeExtension;

    public override void ProcessFrame(
        Playable playable,
        FrameData info,
        object playerData)
    {
        CinemachineVirtualCameraBase camera = playerData as CinemachineVirtualCameraBase;
        if (camera == null)
        {
            DisableShake();
            return;
        }

        if (shakeExtension == null || shakeExtension.gameObject != camera.gameObject)
        {
            DisableShake();
            shakeExtension = camera.GetComponent<CinemachineTimelineShakeExtension>();
            if (shakeExtension == null)
            {
                shakeExtension = camera.gameObject.AddComponent<CinemachineTimelineShakeExtension>();
            }
        }

        float positionAmplitude = 0f;
        float rotationAmplitude = 0f;
        float frequency = 0f;

        for (int inputIndex = 0; inputIndex < playable.GetInputCount(); inputIndex++)
        {
            float weight = playable.GetInputWeight(inputIndex);
            if (weight <= 0f)
            {
                continue;
            }

            ScriptPlayable<CinemachineCameraShakeBehaviour> input =
                (ScriptPlayable<CinemachineCameraShakeBehaviour>)playable.GetInput(inputIndex);
            CinemachineCameraShakeBehaviour behaviour = input.GetBehaviour();
            positionAmplitude += behaviour.positionAmplitude * weight;
            rotationAmplitude += behaviour.rotationAmplitude * weight;
            frequency += behaviour.frequency * weight;
        }

        shakeExtension.SetShake(positionAmplitude, rotationAmplitude, frequency);
    }

    public override void OnGraphStop(Playable playable)
    {
        DisableShake();
    }

    private void DisableShake()
    {
        if (shakeExtension != null)
        {
            shakeExtension.SetShake(0f, 0f, 0f);
            shakeExtension = null;
        }
    }
}

public sealed class CinemachineCameraShakeClip : PlayableAsset, ITimelineClipAsset
{
    [Header("Shake")]
    [Min(0f)] public float positionAmplitude = 0.08f;
    [Min(0f)] public float rotationAmplitude = 1.5f;
    [Min(0f)] public float frequency = 8f;

    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.ClipIn | ClipCaps.SpeedMultiplier;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<CinemachineCameraShakeBehaviour> playable =
            ScriptPlayable<CinemachineCameraShakeBehaviour>.Create(graph);
        CinemachineCameraShakeBehaviour behaviour = playable.GetBehaviour();
        behaviour.positionAmplitude = positionAmplitude;
        behaviour.rotationAmplitude = rotationAmplitude;
        behaviour.frequency = frequency;
        return playable;
    }
}

public sealed class CinemachineCameraShakeBehaviour : PlayableBehaviour
{
    public float positionAmplitude;
    public float rotationAmplitude;
    public float frequency;
}

[DisallowMultipleComponent]
public sealed class CinemachineTimelineShakeExtension : CinemachineExtension
{
    private float positionAmplitude;
    private float rotationAmplitude;
    private float frequency;

    public void SetShake(float newPositionAmplitude, float newRotationAmplitude, float newFrequency)
    {
        positionAmplitude = newPositionAmplitude;
        rotationAmplitude = newRotationAmplitude;
        frequency = newFrequency;
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize || frequency <= 0f)
        {
            return;
        }

        float time = CinemachineCore.CurrentTime * frequency;
        Vector3 positionOffset = new Vector3(
            Mathf.PerlinNoise(time, 0.17f) - 0.5f,
            Mathf.PerlinNoise(0.43f, time) - 0.5f,
            0f) * (positionAmplitude * 2f);

        Vector3 orientedOffset = state.GetCorrectedOrientation() * positionOffset;
        orientedOffset.z = 0f; // Triệt tiêu hoàn toàn độ dịch chuyển theo trục Z

        state.PositionCorrection += orientedOffset;

        if (rotationAmplitude > 0f)
        {
            Vector3 rotationOffset = new Vector3(
                0f,
                0f,
                (Mathf.PerlinNoise(time, 0.71f) - 0.5f) * (rotationAmplitude * 2f));
            state.OrientationCorrection *= Quaternion.Euler(rotationOffset);
        }
    }
}
