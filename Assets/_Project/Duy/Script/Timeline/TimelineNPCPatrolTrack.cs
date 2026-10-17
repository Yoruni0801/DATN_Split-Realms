using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.2f, 0.7f, 0.4f)]
[TrackClipType(typeof(TimelineNPCPatrolClip))]
[TrackBindingType(typeof(NPCRandomPatrol))]
[System.ComponentModel.DisplayName("TimelineTrack/NPC Patrol")]
public class TimelineNPCPatrolTrack : TrackAsset
{
    public enum PostPlaybackState
    {
        EnableAll,
        DisableAll,
        Revert,
        LeaveAsIs
    }

    [SerializeField]
    private PostPlaybackState m_PostPlaybackState = PostPlaybackState.EnableAll;

    public PostPlaybackState postPlaybackState
    {
        get => m_PostPlaybackState;
        set => m_PostPlaybackState = value;
    }

    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        ScriptPlayable<TimelineNPCPatrolMixerBehaviour> mixer =
            ScriptPlayable<TimelineNPCPatrolMixerBehaviour>.Create(graph, inputCount);
        mixer.GetBehaviour().postPlaybackState = m_PostPlaybackState;
        return mixer;
    }
}

public class TimelineNPCPatrolMixerBehaviour : PlayableBehaviour
{
    public TimelineNPCPatrolTrack.PostPlaybackState postPlaybackState;

    private readonly System.Collections.Generic.Dictionary<NPCRandomPatrol, bool> originalStates =
        new System.Collections.Generic.Dictionary<NPCRandomPatrol, bool>();

    private TimelineNPCPatrolBehaviour activeClip;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        NPCRandomPatrol boundTarget = playerData as NPCRandomPatrol;

        TimelineNPCPatrolClipState nextClip = GetStrongestActiveClip(playable);
        if (nextClip.Behaviour == null)
        {
            ExitActiveClip();
            return;
        }

        if (activeClip != null && activeClip != nextClip.Behaviour)
        {
            activeClip.Exit();
        }

        activeClip = nextClip.Behaviour;
        activeClip.Enter(boundTarget, this);
    }

    public override void OnGraphStop(Playable playable)
    {
        ExitActiveClip();

        if (Application.isPlaying)
        {
            ApplyPostPlaybackState();
        }

        originalStates.Clear();
    }

    public void CacheOriginalState(NPCRandomPatrol patrol)
    {
        if (patrol != null && !originalStates.ContainsKey(patrol))
        {
            originalStates.Add(patrol, patrol.enablePatrol);
        }
    }

    private void ApplyPostPlaybackState()
    {
        switch (postPlaybackState)
        {
            case TimelineNPCPatrolTrack.PostPlaybackState.EnableAll:
                NPCRandomPatrol.EnableAllPatrol(true);
                break;

            case TimelineNPCPatrolTrack.PostPlaybackState.DisableAll:
                NPCRandomPatrol.DisableAllPatrol(true);
                break;

            case TimelineNPCPatrolTrack.PostPlaybackState.Revert:
                foreach (var pair in originalStates)
                {
                    if (pair.Key != null)
                    {
                        pair.Key.SetEnablePatrol(pair.Value);
                    }
                }
                break;

            case TimelineNPCPatrolTrack.PostPlaybackState.LeaveAsIs:
            default:
                break;
        }
    }

    private TimelineNPCPatrolClipState GetStrongestActiveClip(Playable playable)
    {
        TimelineNPCPatrolClipState strongestClip = default;
        float strongestWeight = 0f;

        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0.001f || weight <= strongestWeight)
            {
                continue;
            }

            if (playable.GetInput(i).GetPlayableType() != typeof(TimelineNPCPatrolBehaviour))
            {
                continue;
            }

            ScriptPlayable<TimelineNPCPatrolBehaviour> inputPlayable =
                (ScriptPlayable<TimelineNPCPatrolBehaviour>)playable.GetInput(i);

            strongestClip = new TimelineNPCPatrolClipState(inputPlayable.GetBehaviour());
            strongestWeight = weight;
        }

        return strongestClip;
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

    private struct TimelineNPCPatrolClipState
    {
        public TimelineNPCPatrolBehaviour Behaviour;

        public TimelineNPCPatrolClipState(TimelineNPCPatrolBehaviour behaviour)
        {
            Behaviour = behaviour;
        }
    }
}
