using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// IMPORTANT: Keep PlayerRevealClip in PlayerRevealClip.cs.
// Timeline stores PlayableAsset clips as ScriptableObject sub-assets.
// Giving the PlayableAsset its own matching script file makes its serialized type stable across Editor restarts.

[System.Serializable]
public class PlayerRevealClip : PlayableAsset, ITimelineClipAsset
{
    // ========================================================
    // PLAYER PATH
    // ========================================================

    [Header("PLAYER PATH")]

    [Tooltip("Kéo Start Point từ Hierarchy vào đây.")]
    public ExposedReference<Transform> startPoint;

    [Tooltip("Kéo End Point từ Hierarchy vào đây.")]
    public ExposedReference<Transform> endPoint;


    // ========================================================
    // CIRCLE
    // ========================================================

    [Header("CIRCLE FOLLOW")]

    [Tooltip(
        "Circle luôn follow Player.\n" +
        "Offset này cho phép chỉnh vị trí circle so với Player."
    )]
    public Vector3 circleFollowOffset = Vector3.zero;


    // ========================================================
    // MOVEMENT
    // ========================================================

    [Header("MOVEMENT")]

    public AnimationCurve movementCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);


    // ========================================================
    // REVEAL
    // ========================================================

    [Header("REVEAL")]

    [Tooltip("Màu của vùng circle.")]
    public Color revealColor = Color.white;

    [Tooltip(
        "0 = bên trong circle giữ nguyên màu map.\n" +
        "Tăng lên nếu muốn tint màu."
    )]
    [Range(0f, 1f)]
    public float revealColorAlpha = 0f;


    [Tooltip("Kích thước circle ban đầu.")]
    [Min(0.001f)]
    public float startRadius = 0.08f;


    [Tooltip(
        "Kích thước circle cuối.\n" +
        "3 thường đủ để phủ toàn màn hình."
    )]
    [Min(0.1f)]
    public float finalRadius = 3.0f;


    [Tooltip(
        "Thời gian circle lan ra sau khi Player tới B."
    )]
    [Min(0f)]
    public float revealDuration = 1.5f;


    // ========================================================
    // EDGE
    // ========================================================

    [Header("CIRCLE EDGE")]

    [Tooltip("Độ mềm của mép circle.")]
    [Range(0.001f, 0.2f)]
    public float edgeSoftness = 0.025f;


    [Tooltip("Hiện viền circle.")]
    public bool showEdge = true;


    [Range(0f, 2f)]
    public float edgeIntensity = 0.35f;


    public ClipCaps clipCaps =>
        ClipCaps.Blending |
        ClipCaps.Extrapolation;


    // ========================================================
    // CREATE PLAYABLE
    // ========================================================

    public override Playable CreatePlayable(
        PlayableGraph graph,
        GameObject owner)
    {
        ScriptPlayable<PlayerRevealBehaviour> playable =
            ScriptPlayable<PlayerRevealBehaviour>.Create(
                graph
            );


        PlayerRevealBehaviour behaviour =
            playable.GetBehaviour();


        // ----------------------------------------------------
        // RESOLVE START / END
        // ----------------------------------------------------

        behaviour.startPoint =
            startPoint.Resolve(
                graph.GetResolver()
            );


        behaviour.endPoint =
            endPoint.Resolve(
                graph.GetResolver()
            );


        // ----------------------------------------------------
        // CIRCLE
        // ----------------------------------------------------

        behaviour.circleFollowOffset =
            circleFollowOffset;


        // ----------------------------------------------------
        // MOVEMENT
        // ----------------------------------------------------

        behaviour.movementCurve =
            movementCurve;


        // ----------------------------------------------------
        // REVEAL
        // ----------------------------------------------------

        behaviour.revealColor =
            revealColor;

        behaviour.revealColorAlpha =
            revealColorAlpha;

        behaviour.startRadius =
            startRadius;

        behaviour.finalRadius =
            finalRadius;

        behaviour.revealDuration =
            revealDuration;


        // ----------------------------------------------------
        // EDGE
        // ----------------------------------------------------

        behaviour.edgeSoftness =
            edgeSoftness;

        behaviour.showEdge =
            showEdge;

        behaviour.edgeIntensity =
            edgeIntensity;


        return playable;
    }
}
