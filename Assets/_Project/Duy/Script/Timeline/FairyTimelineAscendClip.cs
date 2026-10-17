using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
public class FairyTimelineAscendClip : PlayableAsset, ITimelineClipAsset
{
    [Header("Scatter & Ascension")]
    [Tooltip("T?c d? bay chính")]
    public float ascentSpeed = 4f;

    [Tooltip("Bán kính/kho?ng cách to? ra các hu?ng khác nhau")]
    public float scatterRadius = 6f;

    [Tooltip("Ð? th? v?n t?c di chuy?n")]
    public AnimationCurve motionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Fade Out")]
    [Tooltip("Ð? th? thu nh?/làm m? d?n d?n khi bi?n m?t")]
    public AnimationCurve alphaScaleCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Tooltip("T?t h?n GameObject khi bay xong")]
    public bool deactivateOnComplete = true;

    [Header("Camera Settings")]
    public bool followWithCamera = true;
    public Vector3 cameraOffset = new Vector3(0f, 0f, -10f);

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<FairyTimelineAscendBehaviour> playable =
            ScriptPlayable<FairyTimelineAscendBehaviour>.Create(graph);

        FairyTimelineAscendBehaviour behaviour = playable.GetBehaviour();
        behaviour.ascentSpeed = ascentSpeed;
        behaviour.scatterRadius = scatterRadius;
        behaviour.motionCurve = motionCurve;
        behaviour.alphaScaleCurve = alphaScaleCurve;
        behaviour.deactivateOnComplete = deactivateOnComplete;
        behaviour.followWithCamera = followWithCamera;
        behaviour.cameraOffset = cameraOffset;

        return playable;
    }
}
