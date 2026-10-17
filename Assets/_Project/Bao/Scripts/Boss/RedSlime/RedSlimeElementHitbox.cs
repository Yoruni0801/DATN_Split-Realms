using System.Collections.Generic;
using UnityEngine;

namespace Bao.Bosses
{
    /// <summary>
    /// Gắn trực tiếp lên các GameObject con (Slime elements 1, 2, 3) của Slime B:
    /// - Quản lý vệt đuôi vàng hổ phách (Amber TrailRenderer) khi animation kích hoạt.
    /// - Phát hiện va chạm và gây sát thương chuẩn PlayerHitResolver khi đạn bay trong animation.
    /// - Hiệu ứng nổ tóe thạch vàng khi trúng Player.
    /// </summary>
    public class RedSlimeElementHitbox : MonoBehaviour
    {
        [Header("Hit Settings")]
        [SerializeField] private float hitRadius = 0.75f;

        [Header("Colors")]
        [ColorUsage(true, true)]
        [SerializeField] private Color amberColor = new Color(1.0f, 0.88f, 0.20f, 1f); // Radiant Solar Amber
        [ColorUsage(true, true)]
        [SerializeField] private Color trailGlowColor = new Color(1.0f, 0.65f, 0.10f, 0.85f); // Molten Orange Gold

        private RedSlimeBoss boss;
        private SpriteRenderer spriteRenderer;
        private TrailRenderer trailRenderer;
        private HashSet<int> hitTargets = new HashSet<int>();
        private bool wasActive = false;

        private GameObject projectileGlowCore;
        private SpriteRenderer glowCoreSr;

        public void Initialize(RedSlimeBoss owner)
        {
            boss = owner;
            spriteRenderer = GetComponent<SpriteRenderer>();

            if (spriteRenderer != null)
            {
                // Áp tông màu vàng hổ phách đồng bộ với boss
                spriteRenderer.color = amberColor;
            }

            SetupGlowCore();
            SetupTrail();
        }

        private void SetupGlowCore()
        {
            if (projectileGlowCore != null) return;

            projectileGlowCore = new GameObject("AmberGlowCore");
            projectileGlowCore.transform.SetParent(transform, false);
            projectileGlowCore.transform.localPosition = Vector3.zero;
            projectileGlowCore.transform.localScale = Vector3.one * 1.4f;

            glowCoreSr = projectileGlowCore.AddComponent<SpriteRenderer>();
            Shader glowShader = Shader.Find("Custom/2D/RedSlimeGlow");
            if (glowShader == null) glowShader = Shader.Find("Sprites/Default");

            if (glowShader != null)
            {
                Material glowMat = new Material(glowShader);
                glowMat.SetColor("_Color", new Color(4.0f, 3.2f, 0.8f, 1f));
                glowMat.SetFloat("_Intensity", 2.8f);
                glowCoreSr.material = glowMat;
            }

            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                glowCoreSr.sprite = spriteRenderer.sprite;
            }
            glowCoreSr.sortingOrder = 29;
            projectileGlowCore.SetActive(false);
        }

        private void SetupTrail()
        {
            trailRenderer = GetComponent<TrailRenderer>();
            if (trailRenderer == null)
            {
                trailRenderer = gameObject.AddComponent<TrailRenderer>();
            }

            trailRenderer.time = 0.32f;
            trailRenderer.startWidth = 0.85f;
            trailRenderer.endWidth = 0.05f;
            trailRenderer.sortingOrder = 30;

            // Dùng AnimationCurve để tạo hình dạng vệt đạn thạch sao chổi mượt mà (Comet Silhouette)
            AnimationCurve widthCurve = new AnimationCurve();
            widthCurve.AddKey(new Keyframe(0f, 1f, 0f, -0.8f));
            widthCurve.AddKey(new Keyframe(0.25f, 0.75f, -1.2f, -1.2f));
            widthCurve.AddKey(new Keyframe(0.7f, 0.3f, -0.6f, -0.6f));
            widthCurve.AddKey(new Keyframe(1f, 0.0f, 0f, 0f));
            trailRenderer.widthCurve = widthCurve;

            // Sử dụng Custom Shader chuyên biệt cho Trail vàng hổ phách HDR
            Shader trailShader = Shader.Find("Custom/2D/AmberSlimeProjectileTrail");
            if (trailShader == null) trailShader = Shader.Find("Custom/2D/RedSlimeGlow");
            if (trailShader == null) trailShader = Shader.Find("Sprites/Default");

            if (trailShader != null)
            {
                Material trailMat = new Material(trailShader);
                trailMat.SetColor("_CoreColor", new Color(4.0f, 3.5f, 1.2f, 1.0f));  // Tâm lõi vàng chói
                trailMat.SetColor("_GlowColor", new Color(3.0f, 1.6f, 0.2f, 1.0f));  // Thân hổ phách
                trailMat.SetColor("_OuterColor", new Color(2.0f, 0.4f, 0.05f, 1.0f)); // Mép cam nóng
                trailMat.SetFloat("_Intensity", 2.6f);
                trailRenderer.material = trailMat;
            }

            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 1f, 0.6f), 0f),
                    new GradientColorKey(new Color(1f, 0.85f, 0.15f), 0.3f),
                    new GradientColorKey(new Color(1f, 0.45f, 0.05f), 0.75f),
                    new GradientColorKey(new Color(0.9f, 0.2f, 0.02f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1.0f, 0f),
                    new GradientAlphaKey(0.85f, 0.3f),
                    new GradientAlphaKey(0.4f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            trailRenderer.colorGradient = grad;
            trailRenderer.emitting = false;
        }

        private Vector3 lastLocalPos;
        private bool isShootingForward = false;

        public void ResetHitTracker()
        {
            hitTargets.Clear();
            isShootingForward = false;
            if (projectileGlowCore != null) projectileGlowCore.SetActive(false);
            if (trailRenderer != null)
            {
                trailRenderer.Clear();
                trailRenderer.emitting = false;
            }
        }

        private void Update()
        {
            if (boss == null || spriteRenderer == null) return;

            // Chỉ kích hoạt đạn thạch khi Boss ở dạng Slime B và ĐANG TRONG CHIÊU BẮN ĐẠN (RedSlimeBShootState)
            // Tránh hoàn toàn việc xuất hiện trail khi thực hiện các chiêu Dash hoặc Gọng Kìm Sấm Sét
            bool isShootState = boss.CurrentForm == SlimeForm.SlimeB && boss.CurrentState is RedSlimeBShootState;
            if (!isShootState)
            {
                if (isShootingForward || (trailRenderer != null && trailRenderer.emitting))
                {
                    ResetHitTracker();
                }
                lastLocalPos = transform.localPosition;
                return;
            }

            // Kiểm tra trạng thái hiển thị của viên đạn từ animation
            bool isVisible = spriteRenderer.enabled && spriteRenderer.color.a > 0.05f;

            Vector3 currentLocalPos = transform.localPosition;
            // Trong animation Slime B Attack, đạn bắn phóng thẳng ra phía trước (X âm chạy từ -7 đến -45)
            // Khi quay về, localPosition.x sẽ dịch chuyển ngược lại (tăng dần về phía 0) hoặc co lại
            float deltaLocalX = currentLocalPos.x - lastLocalPos.x;

            // Đạn đang trong pha bay ra nếu đang hiển thị và đang dịch chuyển ra xa (X âm sâu hơn hoặc tốc độ bay ra > 0.01f)
            bool isMovingOutward = isVisible && (deltaLocalX < -0.05f || (deltaLocalX <= 0f && currentLocalPos.x < -7.5f));

            // Chỉ kích hoạt Trail, Glow Core và Hitbox sát thương KHI ĐẠN ĐANG BAY RA PHÍA TRƯỚC
            if (isMovingOutward)
            {
                if (!isShootingForward)
                {
                    // Bắt đầu pha phóng đạn
                    isShootingForward = true;
                    if (projectileGlowCore != null) projectileGlowCore.SetActive(true);
                    if (trailRenderer != null)
                    {
                        trailRenderer.Clear();
                        trailRenderer.emitting = true;
                    }
                }

                // Quét va chạm gây sát thương chuẩn khi đạn đang bay tới Player
                CheckDamage();
            }
            else
            {
                // Khi đạn bay về hoặc đã hết tầm: tắt và xóa ngay lập tức Trail, Lõi sáng và tắt hitbox sát thương
                // Không bao giờ để TrailRenderer vẽ vệt quay ngược lại thân boss ("quay lại rất xấu")
                if (isShootingForward || (trailRenderer != null && trailRenderer.emitting))
                {
                    isShootingForward = false;
                    if (projectileGlowCore != null) projectileGlowCore.SetActive(false);
                    if (trailRenderer != null)
                    {
                        trailRenderer.Clear();
                        trailRenderer.emitting = false;
                    }
                }
            }

            lastLocalPos = currentLocalPos;
            wasActive = isVisible;
        }

        private void CheckDamage()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, hitRadius);
            foreach (var hit in hits)
            {
                PlayerHealth playerHealth = hit.GetComponentInParent<PlayerHealth>();
                if (playerHealth != null || hit.CompareTag("Player"))
                {
                    GameObject rootGo = hit.transform.root.gameObject;
                    int id = rootGo.GetInstanceID();

                    if (!hitTargets.Contains(id))
                    {
                        IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
                            hit,
                            boss.ShootBDamage,
                            ParryableAttackType.Projectile,
                            EnemyParryResponder.Ensure(boss),
                            boss.transform,
                            (hit.bounds.center - transform.position).normalized,
                            canBeBlocked: true,
                            isSlowAttack: false);

                        if (resolution != IncomingAttackResolution.Ignored)
                        {
                            hitTargets.Add(id);

                            // Hiệu ứng nổ tóe thạch vàng hổ phách phát quang và rung màn hình nhẹ
                            boss.ShaderController?.SpawnSlimeBElementImpact(transform.position);
                            boss.ShaderController?.TriggerScreenShake(0.12f, 0.15f);
                            boss.AudioController?.PlayShootBImpactSound(transform.position);
                        }
                    }
                }
            }
        }

        private void OnDisable()
        {
            if (trailRenderer != null)
            {
                trailRenderer.emitting = false;
            }
            wasActive = false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, hitRadius);
        }
    }
}
