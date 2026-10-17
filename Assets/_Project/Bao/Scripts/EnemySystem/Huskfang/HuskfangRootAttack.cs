using UnityEngine;
using System.Collections;

namespace Bao.Enemies
{
    public class HuskfangRootAttack : MonoBehaviour
    {
        [Header("Root Stats")]
        [SerializeField] private float warningDuration = 0.5f;
        [SerializeField] private float attackDuration = 0.2f;
        [SerializeField] private float lingerDuration = 1.0f;
        [SerializeField] private int damage = 15;
        [SerializeField] private Vector2 hitboxSize = new Vector2(1.2f, 1.8f);
        [SerializeField] private Vector2 hitboxOffset = new Vector2(0f, 0.9f);
        [SerializeField] private Color sapColor = new Color(0.4f, 1.0f, 0.0f, 1f);
        [Header("Root Visual")]
        [SerializeField] private Sprite rootSprite;
        [SerializeField] private Shader rootBriarShader;
        [Tooltip("Offset from the spawn point (the target's ground position).")]
        [SerializeField] private Vector2 rootVisualOffset = Vector2.zero;
        [Tooltip("Rotation around the Z axis, in degrees.")]
        [SerializeField] private float rootVisualRotation;
        [Tooltip("Final size of the root visual. The Y value is animated from zero during the strike.")]
        [SerializeField] private Vector2 rootVisualScale = Vector2.one;
        [SerializeField] private int rootSortingOrder = 10;

        private SpriteRenderer rootSr;
        private SpriteRenderer warningSr;
        private BoxCollider2D col;
        private HuskfangEnemy owner;
        private Material rootMaterial;
        private bool canDamage = false;
        private bool hasHit;

        public void Initialize(HuskfangEnemy owner)
        {
            this.owner = owner;
        }

        private void Awake()
        {
            gameObject.layer = LayerMask.NameToLayer("Enemy");
            gameObject.tag = "Enemy";

            // Create Visuals
            CreateVisuals();

            // Set up collider
            col = GetComponent<BoxCollider2D>();
            if (col == null)
            {
                col = gameObject.AddComponent<BoxCollider2D>();
            }
            col.isTrigger = true;
            col.enabled = false; // Disabled during warning
            col.size = hitboxSize;
            col.offset = hitboxOffset; // Base is at 0
        }

        private void CreateVisuals()
        {
            // Root Sprite
            GameObject rootVisual = new GameObject("RootVisual");
            rootVisual.transform.SetParent(transform);
            rootVisual.transform.localPosition = rootVisualOffset;
            rootVisual.transform.localRotation = Quaternion.Euler(0f, 0f, rootVisualRotation);
            
            rootSr = rootVisual.AddComponent<SpriteRenderer>();
            rootSr.sprite = rootSprite;
            rootSr.color = Color.white;
            rootSr.sortingOrder = rootSortingOrder;

            if (rootBriarShader == null)
            {
                rootBriarShader = Shader.Find("Bao/HuskfangBriarShader");
            }
            if (rootBriarShader != null)
            {
                rootMaterial = new Material(rootBriarShader);
                rootMaterial.SetColor("_SapColor", sapColor);
                rootMaterial.SetFloat("_VenomLevel", 0.7f);
                rootSr.material = rootMaterial;
            }
            
            // Warning Circle (Telegraph on ground)
            GameObject warningObj = new GameObject("WarningCircle");
            warningObj.transform.SetParent(transform);
            warningObj.transform.localPosition = Vector3.zero;
            warningObj.transform.localScale = new Vector3(1f, 0.3f, 1f); // Flattened circle
            
            warningSr = warningObj.AddComponent<SpriteRenderer>();
            warningSr.sprite = GenerateProceduralCircle();
            warningSr.color = new Color(1f, 0f, 0f, 0.5f);
            warningSr.sortingOrder = 5;

            // Start completely shrunk
            rootVisual.transform.localScale = new Vector3(rootVisualScale.x, 0f, 1f);
        }

        private Sprite GenerateProceduralCircle()
        {
            int size = 256;
            Texture2D tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Bilinear;
            
            float center = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    if (dist <= center)
                    {
                        // Sharp edge anti-aliasing
                        float alpha = Mathf.Clamp01((center - dist) / 2f); 
                        
                        // Solid outer ring, slightly transparent inner
                        if (dist > center * 0.8f)
                        {
                            tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                        }
                        else
                        {
                            tex.SetPixel(x, y, new Color(1, 1, 1, 0.25f));
                        }
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private void Start()
        {
            StartCoroutine(AttackSequence());
        }

        private IEnumerator AttackSequence()
        {
            yield return null;

            // 1. WARNING PHASE
            float elapsed = 0f;
            while (elapsed < warningDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / warningDuration;
                // Blink warning circle
                float alpha = Mathf.PingPong(Time.time * 5f, 0.5f) + 0.2f;
                warningSr.color = new Color(sapColor.r, sapColor.g, sapColor.b, alpha);
                yield return null;
            }

            // 2. STRIKE PHASE
            Destroy(warningSr.gameObject); // Remove warning
            col.enabled = true; // Enable damage
            canDamage = true;
            hasHit = false;
            
            SpawnDustBurst(transform.position);
            TryDamageOverlappingPlayers();
            
            elapsed = 0f;
            Transform rootTf = rootSr.transform;
            
            // Add some sap effect
            if (rootMaterial != null)
            {
                rootMaterial.SetFloat("_VenomLevel", 6.5f);
            }
            rootSr.color = Color.white;

            while (elapsed < attackDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / attackDuration;
                // Easing out (fast start, slow end)
                float easeOut = 1f - Mathf.Pow(1f - t, 3f);
                rootTf.localScale = new Vector3(rootVisualScale.x, rootVisualScale.y * easeOut, 1f);
                
                if (rootMaterial != null)
                {
                    rootMaterial.SetFloat("_VenomLevel", Mathf.Lerp(6.5f, 1.2f, t));
                }

                TryDamageOverlappingPlayers();
                
                yield return null;
            }
            rootTf.localScale = new Vector3(rootVisualScale.x, rootVisualScale.y, 1f);

            // 3. LINGER PHASE
            canDamage = false; // Maybe only damage on initial upward thrust? Or keep it active?
            // Actually, roots in games usually only damage during the thrust or slightly after.
            // Let's keep it active for half the linger duration.
            yield return new WaitForSeconds(lingerDuration * 0.5f);
            col.enabled = false;
            yield return new WaitForSeconds(lingerDuration * 0.5f);

            // 4. RETRACT PHASE
            elapsed = 0f;
            float retractDuration = 0.3f;
            while (elapsed < retractDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / retractDuration;
                float easeIn = t * t * t;
                rootTf.localScale = new Vector3(rootVisualScale.x, rootVisualScale.y * (1f - easeIn), 1f);
                yield return null;
            }

            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (rootMaterial != null)
            {
                Destroy(rootMaterial);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryDamage(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryDamage(other);
        }

        private void TryDamage(Collider2D other)
        {
            if (!canDamage || hasHit || other == null)
            {
                return;
            }

            PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null)
            {
                return;
            }

            Transform attacker = owner != null ? owner.transform : transform;
            IParryable parryable = owner != null ? EnemyParryResponder.Ensure(owner) : null;
            Vector2 attackDirection = other.bounds.center - transform.position;

            IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
                other,
                damage,
                ParryableAttackType.Melee,
                parryable,
                attacker,
                attackDirection,
                canBeBlocked: true);

            if (resolution == IncomingAttackResolution.Ignored)
            {
                return;
            }

            hasHit = true;
            canDamage = false; // Only hit once
        }

        private void TryDamageOverlappingPlayers()
        {
            if (!canDamage || hasHit || col == null)
            {
                return;
            }

            Collider2D[] hits = Physics2D.OverlapBoxAll(col.bounds.center, col.bounds.size, transform.eulerAngles.z);
            for (int i = 0; i < hits.Length; i++)
            {
                TryDamage(hits[i]);
                if (hasHit)
                {
                    return;
                }
            }
        }

        private void SpawnDustBurst(Vector3 center)
        {
            for (int i = 0; i < 8; i++)
            {
                GameObject dust = new GameObject("RootDust");
                dust.transform.position = center + (Vector3)(Random.insideUnitCircle * 0.2f);
                dust.transform.localScale = Vector3.one * Random.Range(0.2f, 0.4f);
                
                SpriteRenderer sr = dust.AddComponent<SpriteRenderer>();
                sr.sprite = GenerateProceduralCircle();
                sr.color = new Color(0.3f, 0.2f, 0.1f, 0.8f);
                sr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                sr.sortingOrder = 8;
                
                var anim = dust.AddComponent<DustAnimator>();
                anim.Init(sr);
            }
        }
    }

    public class DustAnimator : MonoBehaviour
    {
        private SpriteRenderer sr;
        private Vector3 velocity;
        private float duration;
        private float elapsed;

        public void Init(SpriteRenderer spriteRenderer)
        {
            sr = spriteRenderer;
            duration = Random.Range(0.4f, 0.6f);
            
            // Explode outwards and up
            float angle = Random.Range(30f, 150f) * Mathf.Deg2Rad;
            float speed = Random.Range(1.5f, 3.5f);
            velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * speed;
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
            
            velocity += Vector3.down * 4f * Time.deltaTime; // Gravity
            transform.position += velocity * Time.deltaTime;
            transform.rotation = Quaternion.Euler(0, 0, elapsed * 300f);
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, t * 0.5f);
            
            Color c = sr.color;
            c.a = Mathf.Lerp(0.8f, 0f, t);
            sr.color = c;
        }
    }
}
