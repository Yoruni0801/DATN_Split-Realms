using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ECGUILine : MaskableGraphic
{
    [SerializeField] private float lineThickness = 4f;
    [SerializeField] private bool revealPointsSequentially = true;
    [SerializeField, Min(0.001f)] private float revealInterval = 0.02f;

    private readonly List<Vector2> points = new List<Vector2>();
    private readonly List<Vector2> queuedPoints = new List<Vector2>();
    private float revealTimer;

    public RectTransform RectTransformCached => rectTransform;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    public void Clear()
    {
        points.Clear();
        queuedPoints.Clear();
        revealTimer = 0f;
        SetVerticesDirty();
    }

    public void AddPoint(Vector2 point)
    {
        if (points.Count == 0)
        {
            points.Add(point);
            SetVerticesDirty();
            return;
        }

        if (revealPointsSequentially)
        {
            queuedPoints.Add(point);
            return;
        }

        points.Add(point);
        SetVerticesDirty();
    }

    private void Update()
    {
        if (!revealPointsSequentially || queuedPoints.Count == 0)
        {
            return;
        }

        revealTimer += Time.deltaTime;
        if (revealTimer < revealInterval)
        {
            return;
        }

        revealTimer = 0f;
        points.Add(queuedPoints[0]);
        queuedPoints.RemoveAt(0);
        SetVerticesDirty();
    }

    public void FlushQueuedPoints()
    {
        if (queuedPoints.Count == 0)
        {
            return;
        }

        points.AddRange(queuedPoints);
        queuedPoints.Clear();
        revealTimer = 0f;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (points.Count < 2)
            return;

        for (int i = 0; i < points.Count - 1; i++)
        {
            DrawSegment(vh, points[i], points[i + 1]);
        }
    }

    private void DrawSegment(VertexHelper vh, Vector2 a, Vector2 b)
    {
        Vector2 dir = b - a;

        if (dir.sqrMagnitude <= 0.001f)
            return;

        dir.Normalize();

        Vector2 normal = new Vector2(-dir.y, dir.x) * lineThickness * 0.5f;

        int index = vh.currentVertCount;

        UIVertex vert = UIVertex.simpleVert;
        vert.color = color;

        vert.position = a + normal;
        vh.AddVert(vert);

        vert.position = a - normal;
        vh.AddVert(vert);

        vert.position = b - normal;
        vh.AddVert(vert);

        vert.position = b + normal;
        vh.AddVert(vert);

        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index + 2, index + 3, index);
    }
}
