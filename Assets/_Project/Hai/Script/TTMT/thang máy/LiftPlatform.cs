using System.Collections.Generic;
using UnityEngine;

public class LiftPlatform : MonoBehaviour
{
    public Transform pointA, pointB;
    public float speed = 2f;

    private Rigidbody2D rb;
    private Vector2 target;
    private readonly HashSet<Rigidbody2D> passengers = new HashSet<Rigidbody2D>();

    void Awake() {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    void Start() {
        if (pointA != null && pointB != null) {
            target = pointB.position;
        }
    }

    void FixedUpdate() {
        if (pointA == null || pointB == null) return;

        Vector2 currentPos = rb.position;
        Vector2 targetPos = target;
        Vector2 direction = targetPos - currentPos;
        float distance = direction.magnitude;
        float step = speed * Time.fixedDeltaTime;

        Vector2 nextPos;
        if (distance <= step) {
            nextPos = targetPos;
            rb.linearVelocity = Vector2.zero;
            target = (Vector2.Distance(targetPos, pointA.position) < 0.05f) ? pointB.position : pointA.position;
        } else {
            nextPos = currentPos + direction.normalized * step;
            rb.linearVelocity = direction.normalized * speed;
        }

        Vector2 delta = nextPos - currentPos;
        rb.MovePosition(nextPos);

        // Kéo các passenger đứng trên mặt platform đi theo delta
        if (passengers.Count > 0) {
            passengers.RemoveWhere(p => p == null);

            foreach (Rigidbody2D passenger in passengers) {
                passenger.position += delta;
            }
        }
    }

    void OnCollisionEnter2D(Collision2D collision) {
        TryAddPassenger(collision);
    }

    void OnCollisionStay2D(Collision2D collision) {
        TryAddPassenger(collision);
    }

    void OnCollisionExit2D(Collision2D collision) {
        if (collision.rigidbody != null) {
            passengers.Remove(collision.rigidbody);
        }
    }

    private void TryAddPassenger(Collision2D collision) {
        Rigidbody2D passenger = collision.rigidbody;
        if (passenger == null || passenger.bodyType != RigidbodyType2D.Dynamic)
            return;

        for (int i = 0; i < collision.contactCount; i++) {
            // Chỉ thêm passenger khi tiếp xúc từ phía trên mặt sàn (normal hướng xuống)
            if (collision.GetContact(i).normal.y < -0.5f) {
                passengers.Add(passenger);
                return;
            }
        }
    }

    void OnDisable() {
        passengers.Clear();
    }
}