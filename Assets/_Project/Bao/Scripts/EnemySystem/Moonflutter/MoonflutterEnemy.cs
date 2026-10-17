using UnityEngine;

namespace Bao.Enemies
{
    public class MoonflutterEnemy : BaoEnemyBase
    {
        [Header("Moonflutter - Explode")]
        [SerializeField] private float explosionRadius = 2.5f;
        [SerializeField] private int explosionDamage = 35;
        [SerializeField] private float swellDuration = 1.2f; // Time spent swelling before exploding
        [SerializeField] private GameObject explosionEffectPrefab;

        [Header("Moonflutter - Lock-On Dive")]
        [SerializeField, Min(0f)] private float lockTargetDistance = 4f;
        [SerializeField, Min(0f)] private float approachSpeedMultiplier = 1.5f;
        [SerializeField, Min(0f)] private float lockedDashSpeed = 12f;
        [SerializeField, Min(0f)] private float lockWindupDuration = 0.18f;
        [SerializeField, Min(0f)] private float detonateDistance = 0.35f;
        [SerializeField, Min(0f)] private float maxLockedDashDuration = 1.2f;
        [SerializeField, Min(0f)] private float targetAimFallbackHeight = 1f;

        public float ExplosionRadius => explosionRadius;
        public int ExplosionDamage => explosionDamage;
        public float SwellDuration => swellDuration;
        public float LockTargetDistance => lockTargetDistance;
        public float ApproachSpeedMultiplier => approachSpeedMultiplier;
        public float LockedDashSpeed => lockedDashSpeed;
        public float LockWindupDuration => lockWindupDuration;
        public float DetonateDistance => detonateDistance;
        public float MaxLockedDashDuration => maxLockedDashDuration;
        public float TargetAimFallbackHeight => targetAimFallbackHeight;

        private MoonflutterExplodeState explodeState;
        private BaoEnemyFlyingPatrolState customPatrolState;
        private MoonflutterDiveBombState diveBombState;

        protected override void Awake()
        {
            base.Awake();
            explodeState = new MoonflutterExplodeState(this);
            customPatrolState = new BaoEnemyFlyingPatrolState(this);
            diveBombState = new MoonflutterDiveBombState(this);
            if (Rigidbody2D != null)
            {
                Rigidbody2D.gravityScale = 0f; // Moonflutter flies
            }
        }

        protected override void Start()
        {
            ApplyFacing(FacingDirection);
            StateMachine.SetState(customPatrolState);
        }

        public override void SetStateToPatrol()
        {
            StateMachine.SetState(customPatrolState);
        }

        public override void SetStateToChase()
        {
            StateMachine.SetState(diveBombState);
        }

        public override void SetStateToAttack()
        {
            StateMachine.SetState(explodeState);
        }

        public void Detonate()
        {
            // Spawn procedural beautiful shockwave
            SpawnShockwaveVFX();

            if (explosionEffectPrefab != null)
            {
                GameObject effect = Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
                Destroy(effect, 2.5f); // Auto-destroy the spawned visual effect after 2.5 seconds
            }

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player"))
                {
                    PlayerHitResolver.TryApplyHitToPlayer(
                        hit,
                        explosionDamage,
                        ParryableAttackType.Melee,
                        EnemyParryResponder.Ensure(this),
                        transform,
                        hit.transform.position - transform.position,
                        canBeBlocked: false);
                }
            }

            // Die after exploding
            TakeDamage(CurrentHealth);
            Destroy(gameObject); // Immediately destroy the enemy GameObject
        }

        private void SpawnShockwaveVFX()
        {
            GameObject wave = new GameObject("MoonflutterShockwave");
            wave.transform.position = transform.position;
            wave.transform.SetParent(VFXContainerUtility.GetContainer());
            
            SpriteRenderer sr = wave.AddComponent<SpriteRenderer>();
            sr.color = new Color(0.8f, 1f, 0.6f, 1f); // Lime/Yellowish white
            sr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            
            // Create high-res ring texture
            Texture2D tex = new Texture2D(256, 256);
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < 256; y++)
            {
                for (int x = 0; x < 256; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(128f, 128f));
                    // Smooth thick ring
                    if (dist > 110f && dist <= 126f)
                    {
                        float alpha = 1f - Mathf.Abs(dist - 118f) / 8f; // Peak at 118
                        tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 256, 256), new Vector2(0.5f, 0.5f), 100f);
            
            var anim = wave.AddComponent<ShockwaveAnimator>();
            anim.Init(sr, explosionRadius);
        }

        public override void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            lockTargetDistance = Mathf.Max(0f, lockTargetDistance);
            approachSpeedMultiplier = Mathf.Max(0f, approachSpeedMultiplier);
            lockedDashSpeed = Mathf.Max(0f, lockedDashSpeed);
            lockWindupDuration = Mathf.Max(0f, lockWindupDuration);
            detonateDistance = Mathf.Max(0f, detonateDistance);
            maxLockedDashDuration = Mathf.Max(0f, maxLockedDashDuration);
            targetAimFallbackHeight = Mathf.Max(0f, targetAimFallbackHeight);

            if (explosionEffectPrefab == null)
            {
                explosionEffectPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Tien/MagicArsenal/Effects/Prefabs/Missiles & Explosions/Shadow/ShadowExplosionNormal.prefab");
            }
        }
#endif
    }

    public class ShockwaveAnimator : MonoBehaviour
    {
        private SpriteRenderer sr;
        private float maxRadius;
        private float elapsed;
        private float duration = 0.4f;

        public void Init(SpriteRenderer spriteRenderer, float radius)
        {
            sr = spriteRenderer;
            maxRadius = radius;
            transform.localScale = Vector3.zero;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            // Explode outwards very fast then slow down
            float easeOut = 1f - Mathf.Pow(1f - t, 3f);
            
            // maxRadius is world space. The sprite is 2.56 units wide natively (256 pixels / 100 PPU).
            // To reach maxRadius, the scale should be (maxRadius * 2) / 2.56
            float targetScale = (maxRadius * 2f) / 2.56f;
            
            transform.localScale = Vector3.one * (easeOut * targetScale);
            
            Color c = sr.color;
            c.a = Mathf.Lerp(1f, 0f, t);
            sr.color = c;
        }
    }
}
