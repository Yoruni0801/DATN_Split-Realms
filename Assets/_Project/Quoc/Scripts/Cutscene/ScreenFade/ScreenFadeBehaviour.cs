using UnityEngine;
using UnityEngine.Playables;

[System.Serializable]
public class ScreenFadeBehaviour : PlayableBehaviour
{
    public Color fadeColor = Color.black;
    public float startAlpha = 1f;
    public float endAlpha = 0f;
    public AnimationCurve easeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
}
