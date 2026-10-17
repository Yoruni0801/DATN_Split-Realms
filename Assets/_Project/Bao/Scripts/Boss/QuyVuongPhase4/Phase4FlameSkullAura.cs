using System.Collections;
using UnityEngine;

namespace Bao.Bosses.QuyVuongPhase4
{
    /// <summary>
    /// Hiệu ứng Biến Thân Đầu Lâu Hỏa Ma Khổng Lồ (Demonic Flame Skull Transform Ram)
    /// Xuất hiện bao trùm toàn thân Quỷ Vương khi lướt (Molten Trench Dash),
    /// hiển thị vững chắc, sắc nét, đúng chiều húc (trái/phải) và tan biến khi va chạm.
    /// </summary>
    public class Phase4FlameSkullAura : MonoBehaviour
    {
        [Header("Sprite & Visuals")]
        [SerializeField] private SpriteRenderer[] spriteRenderers;
        [Tooltip("Kích thước mặc định của Đầu Lâu Hỏa Ma")]
        [SerializeField] private Vector3 baseScale = new Vector3(1.2f, 1.2f, 1f);
        [SerializeField] private Color solidColor = Color.white;
        [SerializeField] private Color fadeColor = new Color(1f, 0.4f, 0.1f, 0f);

        [Header("Burst & Dissipate")]
        [SerializeField] private float burstDuration = 0.22f;
        [SerializeField] private Vector3 burstScaleMultiplier = new Vector3(1.3f, 1.3f, 1f);

        public Vector3 BaseScale
        {
            get => baseScale;
            set
            {
                baseScale = value;
                ApplyFacing();
            }
        }

        private ParticleSystem[] childParticleSystems;
        private bool isBursting = false;
        private bool isFacingRight = true;

        private void Awake()
        {
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            childParticleSystems = GetComponentsInChildren<ParticleSystem>(true);
        }

        private void Start()
        {
            if (spriteRenderers == null || spriteRenderers.Length == 0)
            {
                spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            }
            ApplyFacing();
        }

        /// <summary>
        /// Khởi tạo hướng húc và kích thước đầu lâu tuỳ chỉnh từ Skill Inspector.
        /// </summary>
        public void Initialize(Vector2 dashDirection, Vector3? customScale = null)
        {
            if (customScale.HasValue && customScale.Value != Vector3.zero)
            {
                baseScale = customScale.Value;
            }
            isFacingRight = dashDirection.x >= 0;
            ApplyFacing();
        }

        public void SetFacing(bool facingRight)
        {
            isFacingRight = facingRight;
            ApplyFacing();
        }

        private void ApplyFacing()
        {
            float signX = isFacingRight ? 1f : -1f;
            transform.localScale = new Vector3(signX * Mathf.Abs(baseScale.x), baseScale.y, baseScale.z);
            transform.localRotation = Quaternion.identity;

            if (spriteRenderers != null)
            {
                foreach (var sr in spriteRenderers)
                {
                    if (sr != null)
                    {
                        sr.color = solidColor;
                    }
                }
            }
        }

        /// <summary>
        /// Kích hoạt khi đụng người chơi hoặc kết thúc lướt: Tan biến mượt mà.
        /// </summary>
        public void TriggerImpactBurstAndFade(float customFadeTime = -1f)
        {
            if (isBursting) return;
            isBursting = true;

            float duration = customFadeTime > 0 ? customFadeTime : burstDuration;

            if (childParticleSystems != null)
            {
                foreach (var ps in childParticleSystems)
                {
                    if (ps != null)
                    {
                        var emission = ps.emission;
                        emission.enabled = false;
                    }
                }
            }

            StartCoroutine(BurstAndFadeRoutine(duration));
        }

        private IEnumerator BurstAndFadeRoutine(float duration)
        {
            Vector3 startBurstScale = transform.localScale;
            Vector3 targetBurstScale = new Vector3(
                startBurstScale.x * burstScaleMultiplier.x,
                startBurstScale.y * burstScaleMultiplier.y,
                startBurstScale.z
            );

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easeOut = Mathf.Sin(t * Mathf.PI * 0.5f);

                transform.localScale = Vector3.Lerp(startBurstScale, targetBurstScale, easeOut);

                if (spriteRenderers != null)
                {
                    foreach (var sr in spriteRenderers)
                    {
                        if (sr != null)
                        {
                            sr.color = Color.Lerp(solidColor, fadeColor, easeOut);
                        }
                    }
                }

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
