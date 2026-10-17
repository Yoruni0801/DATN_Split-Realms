using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum TimelineLoadSceneMode
{
    LoadAdditive,
    LoadSingle,
    ReturnToPrevious
}

public class TimelineLoadSceneClip : PlayableAsset, ITimelineClipAsset
{
    public TimelineLoadSceneMode mode = TimelineLoadSceneMode.LoadAdditive;
    public string sceneName;
    public string playerSpawnId;
    public bool freezeCurrentScene = true;
    public bool setLoadedSceneActive = true;
    public bool useFadeTransition = true;
    [Min(0f)] public float fadeDuration = 0.2f;

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<TimelineLoadSceneBehaviour> playable =
            ScriptPlayable<TimelineLoadSceneBehaviour>.Create(graph);

        TimelineLoadSceneBehaviour behaviour = playable.GetBehaviour();
        behaviour.mode = mode;
        behaviour.sceneName = sceneName;
        behaviour.playerSpawnId = playerSpawnId;
        behaviour.freezeCurrentScene = freezeCurrentScene;
        behaviour.setLoadedSceneActive = setLoadedSceneActive;
        behaviour.useFadeTransition = useFadeTransition;
        behaviour.fadeDuration = Mathf.Max(0f, fadeDuration);

        return playable;
    }
}
