using UnityEngine;
using UnityEngine.UI;

public class PipeBlock : MonoBehaviour
{
    [Header("Grid")]
    public Vector2Int gridPos;

    [Header("Move")]
    public bool movable = true;

    public bool moveHorizontal = true;
    public bool moveVertical = false;

    [Header("Pipe")]
    public bool up;
    public bool down;
    public bool left;
    public bool right;

    [HideInInspector]
    public RectTransform rect;
}