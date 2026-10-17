using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.85f, 0.4f, 0.2f)]
[TrackClipType(typeof(TimelineActionClip))]
[TrackBindingType(typeof(GameObject))]
[HideInMenu]
public class TimelineTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        ScriptPlayable<TimelineTrackMixerBehaviour> mixer =
            ScriptPlayable<TimelineTrackMixerBehaviour>.Create(graph, inputCount);

        // Feeds the Spine Move and Petrify seek rebuilds, which need to know where each clip sits on the Timeline.
        mixer.GetBehaviour().SetClipTimes(GetClips());
        return mixer;
    }
}

public class TimelineTrackMixerBehaviour : PlayableBehaviour
{
    private readonly TimelineSpineMoveMixerBehaviour spineMoveMixer = new TimelineSpineMoveMixerBehaviour();
    private readonly TimelineSpineAnimationMixerBehaviour spineAnimationMixer = new TimelineSpineAnimationMixerBehaviour();
    private readonly TimelineCameraFollowMixerBehaviour cameraFollowMixer = new TimelineCameraFollowMixerBehaviour();
    private readonly TimelineLoadSceneMixerBehaviour loadSceneMixer = new TimelineLoadSceneMixerBehaviour();
    private readonly TimelinePetrifyMixerBehaviour petrifyMixer = new TimelinePetrifyMixerBehaviour();
    private readonly TimelineUnpetrifyMixerBehaviour unpetrifyMixer = new TimelineUnpetrifyMixerBehaviour();
    private readonly TimelineEnemyNpcAttackMixerBehaviour enemyNpcAttackMixer = new TimelineEnemyNpcAttackMixerBehaviour();
    private readonly TimelineAshwingPickupDropMixerBehaviour ashwingPickupDropMixer = new TimelineAshwingPickupDropMixerBehaviour();

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        GameObject go = playerData as GameObject;
        AnimationSpine spine = go != null ? go.GetComponent<AnimationSpine>() : null;
        CinemachineCamera cam = go != null ? go.GetComponent<CinemachineCamera>() : null;
        PetrifyController petrifyCtrl = go != null ? go.GetComponent<PetrifyController>() : null;

        spineMoveMixer.ProcessFrame(playable, info, spine);
        spineAnimationMixer.ProcessFrame(playable, info, spine);
        cameraFollowMixer.ProcessFrame(playable, info, cam);
        loadSceneMixer.ProcessFrame(playable, info, null);
        petrifyMixer.ProcessFrame(playable, info, petrifyCtrl);
        unpetrifyMixer.ProcessFrame(playable, info, petrifyCtrl);
        enemyNpcAttackMixer.ProcessFrame(playable, info, null);
        ashwingPickupDropMixer.ProcessFrame(playable, info, null);
    }

    public void SetClipTimes(System.Collections.Generic.IEnumerable<TimelineClip> clips)
    {
        spineMoveMixer.SetClipTimes(clips);
        petrifyMixer.SetClipTimes(clips);
        unpetrifyMixer.SetClipTimes(clips);
    }

    public override void OnGraphStart(Playable playable)
    {
        spineMoveMixer.OnGraphStart(playable);
        petrifyMixer.OnGraphStart(playable);
        unpetrifyMixer.OnGraphStart(playable);
    }

    public override void OnGraphStop(Playable playable)
    {
        spineMoveMixer.OnGraphStop(playable);
        spineAnimationMixer.OnGraphStop(playable);
        cameraFollowMixer.OnGraphStop(playable);
        loadSceneMixer.OnGraphStop(playable);
        petrifyMixer.OnGraphStop(playable);
        unpetrifyMixer.OnGraphStop(playable);
        enemyNpcAttackMixer.OnGraphStop(playable);
        ashwingPickupDropMixer.OnGraphStop(playable);
    }
}
