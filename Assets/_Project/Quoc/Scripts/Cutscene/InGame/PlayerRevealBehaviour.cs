using UnityEngine;
using UnityEngine.Playables;

public class PlayerRevealBehaviour : PlayableBehaviour
{
    [HideInInspector]
    public Transform startPoint;

    [HideInInspector]
    public Transform endPoint;


    [HideInInspector]
    public Vector3 circleFollowOffset;


    [HideInInspector]
    public AnimationCurve movementCurve;


    [HideInInspector]
    public Color revealColor;

    [HideInInspector]
    public float revealColorAlpha;

    [HideInInspector]
    public float startRadius;

    [HideInInspector]
    public float finalRadius;

    [HideInInspector]
    public float revealDuration;


    [HideInInspector]
    public float edgeSoftness;

    [HideInInspector]
    public bool showEdge;

    [HideInInspector]
    public float edgeIntensity;
}
