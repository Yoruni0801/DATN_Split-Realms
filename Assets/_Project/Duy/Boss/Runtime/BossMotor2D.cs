using UnityEngine;

public sealed class BossMotor2D
{
    private readonly Rigidbody2D rb;
    private readonly Transform transform;
    private readonly Collider2D bodyCollider;

    public BossMotor2D(Rigidbody2D rb, Transform transform, Collider2D bodyCollider)
    {
        this.rb = rb;
        this.transform = transform;
        this.bodyCollider = bodyCollider;
    }

    public Vector2 Velocity => rb != null ? rb.linearVelocity : Vector2.zero;
    public bool HasBodyCollider => bodyCollider != null && bodyCollider.enabled && !bodyCollider.isTrigger;

    public void ConfigureBody()
    {
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        if (bodyCollider != null)
        {
            bodyCollider.isTrigger = false;
        }
    }

    public void MoveHorizontal(float speed, int direction)
    {
        if (rb == null)
        {
            return;
        }

        rb.linearVelocity = new Vector2(direction * speed, rb.linearVelocity.y);
    }

    public void StopHorizontal()
    {
        if (rb == null)
        {
            return;
        }

        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    public void SetVelocity(Vector2 velocity, bool preserveY)
    {
        if (rb == null)
        {
            return;
        }

        rb.linearVelocity = preserveY ? new Vector2(velocity.x, rb.linearVelocity.y) : velocity;
    }

    public void Jump(int direction, float forwardSpeed, float force)
    {
        if (rb == null)
        {
            return;
        }

        rb.linearVelocity = new Vector2(direction * forwardSpeed, force);
    }

    public void FaceDirection(int direction)
    {
        if (transform == null || direction == 0)
        {
            return;
        }

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(direction);
        transform.localScale = scale;
    }
}
