using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.2f, 0.65f, 0.9f)]
[TrackClipType(typeof(TimelineCameraPointBReturnClip))]
[TrackBindingType(typeof(CinemachineCamera))]
[System.ComponentModel.DisplayName("TimelineTrack/Camera Point B And Return")]
public sealed class TimelineCameraPointBReturnTrack : TrackAsset
{
    public override Playable CreateTrackMixer(
        PlayableGraph graph,
        GameObject go,
        int inputCount)
    {
        return ScriptPlayable<TimelineCameraPointBReturnMixer>.Create(
            graph,
            inputCount);
    }
}

public sealed class TimelineCameraPointBReturnMixer : PlayableBehaviour
{
    private TimelineCameraPointBReturnBehaviour activeClip;

    public override void ProcessFrame(
        Playable playable,
        FrameData info,
        object playerData)
    {
        CinemachineCamera camera = playerData as CinemachineCamera;
        TimelineCameraPointBReturnClipState clipState =
            GetStrongestActiveClip(playable);

        if (camera == null || clipState.Behaviour == null)
        {
            ExitActiveClip();
            return;
        }

        if (activeClip != null && activeClip != clipState.Behaviour)
        {
            activeClip.Exit();
        }

        activeClip = clipState.Behaviour;
        activeClip.Enter(camera);
        activeClip.Process(clipState.LocalTime, clipState.Duration);
    }

    public override void OnGraphStop(Playable playable)
    {
        ExitActiveClip();
    }

    private TimelineCameraPointBReturnClipState GetStrongestActiveClip(
        Playable playable)
    {
        TimelineCameraPointBReturnClipState result = default;
        float strongestWeight = 0f;

        for (int i = 0; i < playable.GetInputCount(); i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0.001f || weight <= strongestWeight)
            {
                continue;
            }

            ScriptPlayable<TimelineCameraPointBReturnBehaviour> input =
                (ScriptPlayable<TimelineCameraPointBReturnBehaviour>)playable.GetInput(i);
            result = new TimelineCameraPointBReturnClipState(
                input.GetBehaviour(),
                GetSafeTime(input.GetTime()),
                GetSafeDuration(input.GetDuration()));
            strongestWeight = weight;
        }

        return result;
    }

    private void ExitActiveClip()
    {
        if (activeClip == null)
        {
            return;
        }

        activeClip.Exit();
        activeClip = null;
    }

    private static float GetSafeTime(double time)
    {
        return double.IsNaN(time) || double.IsInfinity(time) || time < 0d
            ? 0f
            : (float)time;
    }

    private static float GetSafeDuration(double duration)
    {
        return double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0d
            ? 0.001f
            : (float)duration;
    }

    private readonly struct TimelineCameraPointBReturnClipState
    {
        public readonly TimelineCameraPointBReturnBehaviour Behaviour;
        public readonly float LocalTime;
        public readonly float Duration;

        public TimelineCameraPointBReturnClipState(
            TimelineCameraPointBReturnBehaviour behaviour,
            float localTime,
            float duration)
        {
            Behaviour = behaviour;
            LocalTime = localTime;
            Duration = duration;
        }
    }
}
