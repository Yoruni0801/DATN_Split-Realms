using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class IceRockPassThroughCollider : MonoBehaviour
{
    private const string IceRockTag = "IceRock";

    private BoxCollider2D blockingCollider;

    private void Awake()
    {
        blockingCollider = GetComponent<BoxCollider2D>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Collider2D otherCollider = collision.collider;
        Rigidbody2D otherRigidbody = collision.rigidbody;

        bool isIceRock = otherCollider.CompareTag(IceRockTag)
            || (otherRigidbody != null && otherRigidbody.CompareTag(IceRockTag));

        if (isIceRock)
        {
            Physics2D.IgnoreCollision(blockingCollider, otherCollider, true);
        }
    }
}
