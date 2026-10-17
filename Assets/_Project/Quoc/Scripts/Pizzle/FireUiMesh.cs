using UnityEngine;
using UnityEngine.UI;

internal static class FireUiMesh
{
    public static void AddRect(VertexHelper helper, Rect rect, Color color)
    {
        AddQuad(
            helper,
            new Vector2(rect.xMin, rect.yMin),
            new Vector2(rect.xMin, rect.yMax),
            new Vector2(rect.xMax, rect.yMax),
            new Vector2(rect.xMax, rect.yMin),
            color);
    }

    public static void AddQuad(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int start = helper.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = a;
        helper.AddVert(vertex);
        vertex.position = b;
        helper.AddVert(vertex);
        vertex.position = c;
        helper.AddVert(vertex);
        vertex.position = d;
        helper.AddVert(vertex);
        helper.AddTriangle(start, start + 1, start + 2);
        helper.AddTriangle(start, start + 2, start + 3);
    }

    public static void AddTriangle(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        int start = helper.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = a;
        helper.AddVert(vertex);
        vertex.position = b;
        helper.AddVert(vertex);
        vertex.position = c;
        helper.AddVert(vertex);
        helper.AddTriangle(start, start + 1, start + 2);
    }

    public static void AddLine(VertexHelper helper, Vector2 from, Vector2 to, float width, Color color)
    {
        Vector2 delta = to - from;
        if (delta.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (width * 0.5f);
        AddQuad(helper, from - normal, from + normal, to + normal, to - normal, color);
    }

    public static void AddCircle(VertexHelper helper, Vector2 center, float radius, int segments, Color color)
    {
        int centerIndex = helper.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = center;
        helper.AddVert(vertex);

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            vertex.position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            helper.AddVert(vertex);
        }

        for (int i = 0; i < segments; i++)
        {
            helper.AddTriangle(centerIndex, centerIndex + i + 1, centerIndex + i + 2);
        }
    }
}
