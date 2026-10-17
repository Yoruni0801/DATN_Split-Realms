using UnityEngine;

namespace Quoc.Boss
{
    [RequireComponent(typeof(Collider2D))]
    public class BossShockwave : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField, Tooltip("Tốc độ di chuyển của sóng thần")]
        private float moveSpeed = 8f;

        [SerializeField, Tooltip("Thời gian tồn tại tự hủy (giây)")]
        private float lifetime = 3f;

        [SerializeField, Tooltip("Sát thương gây ra")]
        private int damage = 25;

        private float directionX = 1f;
        private bool initialized = false;
        private Transform ownerTransform;

        public void Initialize(float dirX, float speed, int dmg, Transform owner = null)
        {
            directionX = Mathf.Sign(dirX);
            if (Mathf.Approximately(dirX, 0f)) directionX = 1f;
            moveSpeed = speed;
            damage = dmg;
            ownerTransform = owner;
            initialized = true;

            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * directionX;
            transform.localScale = scale;

            Destroy(gameObject, lifetime);
        }

        private void Update()
        {
            if (!initialized) return;
            transform.Translate(Vector3.right * directionX * moveSpeed * Time.deltaTime);
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (ownerTransform != null && (collision.transform == ownerTransform || collision.transform.IsChildOf(ownerTransform.root) || collision.transform.root == ownerTransform.root))
            {
                return;
            }
            DamageUtility.DealDamage(collision.gameObject, damage, ownerTransform != null ? ownerTransform : transform);
        }
    }
}
