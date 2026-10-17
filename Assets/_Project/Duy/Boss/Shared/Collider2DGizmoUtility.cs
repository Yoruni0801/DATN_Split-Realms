using UnityEngine;

public static class Collider2DGizmoUtility
{
    private const int CircleSegments = 32;

    public static void DrawEnabledCollidersInChildren(Transform root, Color color)
    {
        if (root == null)
        {
            return;
        }

        Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (!ShouldDraw(collider))
            {
                continue;
            }

            DrawCollider(collider, color);
        }
    }

    private static bool ShouldDraw(Collider2D collider)
    {
        return collider != null &&
            collider.enabled &&
            collider.gameObject.activeInHierarchy;
    }

    private static void DrawCollider(Collider2D collider, Color color)
    {
        Color drawColor = collider.isTrigger
            ? new Color(color.r, color.g, color.b, 0.45f)
            : color;

        Gizmos.color = drawColor;

        if (collider is BoxCollider2D box)
        {
            DrawBox(box);
            return;
        }

        if (collider is CircleCollider2D circle)
        {
            DrawCircle(circle.transform, circle.offset, circle.radius);
            return;
        }

        if (collider is CapsuleCollider2D capsule)
        {
            DrawCapsule(capsule);
            return;
        }

        if (collider is PolygonCollider2D polygon)
        {
            DrawPolygon(polygon);
            return;
        }

        if (collider is EdgeCollider2D edge)
        {
            DrawPoints(edge.transform, edge.points, edge.offset, false);
            return;
        }

        Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
    }

    private static void DrawBox(BoxCollider2D box)
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = box.transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.offset, box.size);
        Gizmos.matrix = previousMatrix;
    }

    private static void DrawCircle(Transform transform, Vector2 offset, float radius)
    {
        Vector3 previous = TransformCirclePoint(transform, offset, radius, 0);
        for (int i = 1; i <= CircleSegments; i++)
        {
            Vector3 next = TransformCirclePoint(transform, offset, radius, i);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }

    private static Vector3 TransformCirclePoint(Transform transform, Vector2 offset, float radius, int index)
    {
        float angle = (index / (float)CircleSegments) * Mathf.PI * 2f;
        Vector2 point = offset + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        return transform.TransformPoint(point);
    }

    private static void DrawCapsule(CapsuleCollider2D capsule)
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = capsule.transform.localToWorldMatrix;
        Gizmos.DrawWireCube(capsule.offset, capsule.size);
        Gizmos.matrix = previousMatrix;
    }

    private static void DrawPolygon(PolygonCollider2D polygon)
    {
        for (int i = 0; i < polygon.pathCount; i++)
        {
            DrawPoints(polygon.transform, polygon.GetPath(i), polygon.offset, true);
        }
    }

    private static void DrawPoints(Transform transform, Vector2[] points, Vector2 offset, bool closed)
    {
        if (points == null || points.Length < 2)
        {
            return;
        }

        Vector3 first = transform.TransformPoint(points[0] + offset);
        Vector3 previous = first;
        for (int i = 1; i < points.Length; i++)
        {
            Vector3 next = transform.TransformPoint(points[i] + offset);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }

        if (closed)
        {
            Gizmos.DrawLine(previous, first);
        }
    }
}
