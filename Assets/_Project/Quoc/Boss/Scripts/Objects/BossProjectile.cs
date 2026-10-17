using UnityEngine;

namespace Quoc.Boss
{
    [RequireComponent(typeof(Collider2D))]
    public class BossProjectile : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField, Tooltip("Tốc độ di chuyển của đạn")]
        private float speed = 12f;

        [SerializeField, Tooltip("Thời gian tồn tại tối đa (giây)")]
        private float lifetime = 4f;

        [SerializeField, Tooltip("Sát thương của đạn")]
        private int damage = 15;

        private Vector2 moveDirection = Vector2.right;
        private bool initialized = false;
        private Transform ownerTransform;

        public void Initialize(Vector2 direction, float projSpeed, int projDamage, Transform owner = null)
        {
            moveDirection = direction.normalized;
            speed = projSpeed;
            damage = projDamage;
            ownerTransform = owner;
            initialized = true;

            float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);

            Destroy(gameObject, lifetime);
        }

        private void Update()
        {
            if (!initialized) return;
            transform.position += (Vector3)(moveDirection * speed * Time.deltaTime);
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (ownerTransform != null && (collision.transform == ownerTransform || collision.transform.IsChildOf(ownerTransform.root) || collision.transform.root == ownerTransform.root))
            {
                return;
            }

            if (DamageUtility.DealDamage(collision.gameObject, damage, ownerTransform != null ? ownerTransform : transform))
            {
                Destroy(gameObject);
            }
        }
    }
}
