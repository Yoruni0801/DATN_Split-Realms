using UnityEngine;
using UnityEngine.Playables;

public class TimelineLoadSceneBehaviour : PlayableBehaviour
{
    public TimelineLoadSceneMode mode;
    public string sceneName;
    public string playerSpawnId;
    public bool freezeCurrentScene;
    public bool setLoadedSceneActive;
    public bool useFadeTransition;
    public float fadeDuration;

    private bool executed;

    public override void OnGraphStart(Playable playable)
    {
        executed = false;
    }

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        TryExecute(playable, info);
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        TryExecute(playable, info);
    }

    private void TryExecute(Playable playable, FrameData info)
    {
        PlayableDirector director = ResolveDirector(playable);
        // if (info.evaluationType == FrameData.EvaluationType.Evaluate
        //     || TimelineSeekContext.IsExplicitlySeeking(director))
        // {
        //     return;
        // }

        if(TimelineSeekContext.IsExplicitlySeeking(director))
            return;

        if (executed || !Application.isPlaying || info.effectiveWeight <= 0.001f)
        {
            return;
        }

        executed = true;
        switch (mode)
        {
            case TimelineLoadSceneMode.LoadSingle:
                TimelineSceneTravelManager.LoadSceneSingle(
                    sceneName,
                    playerSpawnId,
                    useFadeTransition,
                    fadeDuration);
                break;
            case TimelineLoadSceneMode.ReturnToPrevious:
                TimelineSceneTravelManager.ReturnToPreviousScene(
                    useFadeTransition,
                    fadeDuration);
                break;
            default:
                TimelineSceneTravelManager.LoadSceneAdditive(
                    sceneName,
                    freezeCurrentScene,
                    setLoadedSceneActive,
                    director,
                    playerSpawnId,
                    useFadeTransition,
                    fadeDuration);
                break;
        }
    }

    private static PlayableDirector ResolveDirector(Playable playable)
    {
        if (!playable.IsValid())
        {
            return null;
        }

        PlayableGraph graph = playable.GetGraph();
        return graph.IsValid() ? graph.GetResolver() as PlayableDirector : null;
    }
}
