using UnityEngine;

public partial class DuyBossController
{
    private void OnDrawGizmos()
    {
        Collider2DGizmoUtility.DrawEnabledCollidersInChildren(
            transform,
            new Color(1f, 0.25f, 0.05f, 0.9f));
    }

    private void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        if (!drawMovementDebug)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRadius);

        Collider2D previewCollider = bodyCollider != null ? bodyCollider : GetComponent<Collider2D>();
        if (previewCollider == null)
        {
            return;
        }

        Bounds bounds = previewCollider.bounds;
        Gizmos.color = Color.green;
        DrawGroundProbe(bounds, -1);
        DrawGroundProbe(bounds, 1);

        Gizmos.color = Color.cyan;
        DrawWallProbe(bounds, -1);
        DrawWallProbe(bounds, 1);

        if (hasSafeGroundedPosition)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(lastSafeGroundedPosition, 0.25f);
        }
#endif
    }

    private void DrawGroundProbe(Bounds bounds, int direction)
    {
        Vector2 origin = new Vector2(bounds.center.x + direction * bounds.extents.x, bounds.min.y + 0.05f);
        Gizmos.DrawLine(origin, origin + Vector2.down * groundCheckDistance);
    }

    private void DrawWallProbe(Bounds bounds, int direction)
    {
        Vector2 origin = new Vector2(bounds.center.x + direction * bounds.extents.x, bounds.center.y);
        Gizmos.DrawLine(origin, origin + Vector2.right * direction * wallCheckDistance);
    }
}
