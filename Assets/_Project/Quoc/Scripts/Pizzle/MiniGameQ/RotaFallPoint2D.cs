using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class RotaFallPoint2D : MonoBehaviour
{
    [SerializeField] private PlayerRotaSurfaceController.SurfaceSide attachSurface = PlayerRotaSurfaceController.SurfaceSide.LeftWall;
    [SerializeField] private bool keepMoveDirection = true;
    [SerializeField] private string playerTag = "Player";

    private void Reset()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerRotaSurfaceController walker = other.GetComponentInParent<PlayerRotaSurfaceController>();
        if (walker == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(playerTag)
            && !other.CompareTag(playerTag)
            && !walker.CompareTag(playerTag)
            && !walker.transform.root.CompareTag(playerTag))
        {
            return;
        }

        walker.AttachToSurfaceFromFallPoint(attachSurface, keepMoveDirection);
    }
}
