using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum TimelineNPCPatrolTargetMode
{
    AllNPCs,
    BoundTarget
}

public class TimelineNPCPatrolClip : PlayableAsset, ITimelineClipAsset
{
    public ExposedReference<NPCRandomPatrol> targetOverride;
    public TimelineNPCPatrolTargetMode targetMode = TimelineNPCPatrolTargetMode.AllNPCs;
    public bool enablePatrol = true;
    public bool overrideInvertFacing;
    public bool invertFacing;
    public bool includeInactiveTargets = true;

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<TimelineNPCPatrolBehaviour> playable =
            ScriptPlayable<TimelineNPCPatrolBehaviour>.Create(graph);

        TimelineNPCPatrolBehaviour behaviour = playable.GetBehaviour();
        behaviour.targetMode = targetMode;
        behaviour.targetOverride = targetOverride.Resolve(graph.GetResolver());
        behaviour.enablePatrol = enablePatrol;
        behaviour.overrideInvertFacing = overrideInvertFacing;
        behaviour.invertFacing = invertFacing;
        behaviour.includeInactiveTargets = includeInactiveTargets;

        return playable;
    }
}
