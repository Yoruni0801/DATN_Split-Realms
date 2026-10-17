using UnityEngine;

namespace Bao.Bosses.QuyVuongPhase4
{
    /// <summary>
    /// Đạn gai lưng bay hình vòng cung (Parabolic Mortar) cắm xuống sàn đấu và gây sát thương
    /// </summary>
    public class Phase4SpineMortarProjectile : MonoBehaviour
    {
        private Vector3 startPos;
        private Vector3 targetPos;
        private float arcHeight = 7f;
        private float flightDuration = 1f;
        private float elapsedTime = 0f;
        private int damage = 18;
        private float knockback = 2f;
        private bool isInitialized = false;
        private bool hasHitPlayer = false;

        public void Initialize(Vector3 target, float height, float duration, int dmg = 18, float kb = 2f)
        {
            startPos = transform.position;
            targetPos = target;
            arcHeight = height;
            flightDuration = duration;
            elapsedTime = 0f;
            damage = dmg;
            knockback = kb;
            isInitialized = true;
            hasHitPlayer = false;

            // Đảm bảo có Collider2D dạng trigger để nhận va chạm
            var col = GetComponent<Collider2D>();
            if (col == null)
            {
                var box = gameObject.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(0.6f, 1.4f);
            }
            else
            {
                col.isTrigger = true;
            }
        }

        private void Update()
        {
            if (!isInitialized) return;

            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / flightDuration);

            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, t);
            currentPos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;

            transform.position = currentPos;

            // Xoay đầu gai theo quỹ đạo bay
            if (t < 0.98f)
            {
                float nextT = t + 0.02f;
                Vector3 nextPos = Vector3.Lerp(startPos, targetPos, nextT);
                nextPos.y += Mathf.Sin(nextT * Mathf.PI) * arcHeight;
                Vector3 moveDir = (nextPos - currentPos).normalized;
                if (moveDir != Vector3.zero)
                {
                    float angle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;
                    transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
                }
            }

            if (elapsedTime >= flightDuration)
            {
                // Khi gai chạm đất, kiểm tra vùng sát thương bán kính nhỏ tại điểm rơi
                CheckGroundImpactDamage();
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (hasHitPlayer || other == null) return;

            if (other.CompareTag("Player"))
            {
                hasHitPlayer = true;
                QuyVuongPhase4SkillBase.ApplyPlayerDamage(other.gameObject, damage, knockback);
            }
        }

        private void CheckGroundImpactDamage()
        {
            if (hasHitPlayer) return;

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 1.2f);
            foreach (var hit in hits)
            {
                if (hit != null && hit.CompareTag("Player"))
                {
                    hasHitPlayer = true;
                    QuyVuongPhase4SkillBase.ApplyPlayerDamage(hit.gameObject, damage, knockback);
                    break;
                }
            }
        }
    }
}
