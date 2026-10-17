using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.2f, 0.55f, 0.9f)]
[TrackClipType(typeof(TimelineCameraFollowClip))]
[TrackBindingType(typeof(CinemachineCamera))]
[System.ComponentModel.DisplayName("TimelineTrack/Camera Follow")]
public class TimelineCameraFollowTrack : TrackAsset
{
    /// <summary>
    /// Specify what state to leave the CinemachineCamera follow in after the Timeline has finished playing.
    /// </summary>
    public enum PostPlaybackState
    {
        /// <summary>
        /// Revert the CinemachineCamera.Follow to the target it had before the Timeline started playing.
        /// </summary>
        Revert,

        /// <summary>
        /// Keep the CinemachineCamera.Follow set to whatever the last clip left it as.
        /// </summary>
        KeepCurrent,

        /// <summary>
        /// Clear CinemachineCamera.Follow (set to null).
        /// </summary>
        ClearFollow,

        /// <summary>
        /// Leave the CinemachineCamera.Follow untouched when the Timeline stops.
        /// </summary>
        LeaveAsIs
    }

    [SerializeField]
    private PostPlaybackState m_PostPlaybackState = PostPlaybackState.Revert;

    /// <summary>
    /// Specifies what state to leave the CinemachineCamera.Follow in after the Timeline has finished playing.
    /// </summary>
    public PostPlaybackState postPlaybackState
    {
        get => m_PostPlaybackState;
        set => m_PostPlaybackState = value;
    }

    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        ScriptPlayable<TimelineCameraFollowMixerBehaviour> mixer =
            ScriptPlayable<TimelineCameraFollowMixerBehaviour>.Create(graph, inputCount);
        mixer.GetBehaviour().postPlaybackState = m_PostPlaybackState;
        return mixer;
    }
}
