using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
public class ScreenFadeClip : PlayableAsset, ITimelineClipAsset
{
    [Header("FADE SETTINGS")]
    public Color fadeColor = Color.black;

    [Range(0f, 1f)]
    public float startAlpha = 1f;

    [Range(0f, 1f)]
    public float endAlpha = 0f;

    public AnimationCurve easeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<ScreenFadeBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();

        behaviour.fadeColor = fadeColor;
        behaviour.startAlpha = startAlpha;
        behaviour.endAlpha = endAlpha;
        behaviour.easeCurve = easeCurve;

        return playable;
    }
}
