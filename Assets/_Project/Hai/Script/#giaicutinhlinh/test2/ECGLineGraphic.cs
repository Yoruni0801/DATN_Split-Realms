using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class ECGLineGraphic : MonoBehaviour
{
    public int pointCount = 120;
    public float width = 1000f;

    private LineRenderer line;
    private Vector3[] points;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        GenerateFlatLine();
    }

    void GenerateFlatLine()
    {
        points = new Vector3[pointCount];
        line.positionCount = pointCount;

        float step = width / (pointCount - 1);

        for (int i = 0; i < pointCount; i++)
        {
            float x = i * step;
            points[i] = new Vector3(x, 0f, 0f);
        }

        line.SetPositions(points);
    }
}