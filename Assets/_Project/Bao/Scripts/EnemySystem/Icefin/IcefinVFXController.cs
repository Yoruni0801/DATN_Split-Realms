using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Bao.Enemies
{
    [RequireComponent(typeof(IcefinEnemy))]
    public class IcefinVFXController : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        // Inspector — Shader Setup
        // ─────────────────────────────────────────────────────────────────────
        [Header("Shader Setup")]
        [SerializeField] private Shader glacialShader;
        [SerializeField] private Material glacialMaterial;

        // Palette — Frost Crystal (Sáng vừa, trong trẻo, nổi bật trên nền băng tuyết)
        [SerializeField] private Color deepIceColor   = new Color(0.12f, 0.28f, 0.58f, 1f);  // Base Blue vừa
        [SerializeField] private Color glacierColor   = new Color(0.35f, 0.82f, 1.00f, 1f);  // Cerulean sáng
        [SerializeField] private Color frostColor     = new Color(0.82f, 0.96f, 1.00f, 1f);  // Frost Highlight
        [SerializeField] private Color snowColor      = new Color(1.00f, 1.00f, 1.00f, 1f);  // Pure Ice Sparkle
        [SerializeField] private Color glowColor      = new Color(0.20f, 1.25f, 1.45f, 1f);  // Vibrant Cyan Core
        [SerializeField] private Color accentColor    = new Color(1.20f, 0.75f, 0.15f, 1f);  // Sky Cyan
        [SerializeField] private Color rimColor       = new Color(0.65f, 0.95f, 1.35f, 1f);  // Electric Rim
        [SerializeField] private Color outlineColor   = new Color(0.06f, 0.14f, 0.32f, 1f);  // Deep Border Line

        [SerializeField] private float transitionSpeed = 8.0f;

        // ─────────────────────────────────────────────────────────────────────
        // Inspector — VFX Tuning
        // ─────────────────────────────────────────────────────────────────────
        [Header("Ambient")]
        [SerializeField] private Vector2 ambientInterval    = new Vector2(0.20f, 0.40f);
        [SerializeField] private Vector2 breathingInterval  = new Vector2(1.4f,  2.0f);

        [Header("Anticipation")]
        [SerializeField] private int   anticipationBurstCount    = 8;
        [SerializeField] private float anticipationSpawnInterval = 0.040f;
        [SerializeField] private float anticipationRadius        = 0.90f;
        [SerializeField] private int   crystalFormationCount     = 6;

        [Header("Dash")]
        [SerializeField] private float dashTrailInterval   = 0.028f;
        [SerializeField] private float afterimageInterval  = 0.065f;
        [SerializeField] private int   impactShardCount    = 10;

        // ─────────────────────────────────────────────────────────────────────
        // Private state
        // ─────────────────────────────────────────────────────────────────────
        private IcefinEnemy  icefin;
        private SpriteRenderer[] renderers;

        // Runtime-generated materials
        private Material spriteMaterial;       // Sprites/Default normal blend
        private Material additiveMaterial;     // Additive — glow rõ trên nền tối
        private readonly List<Object>     runtimeAssets     = new List<Object>();
        private readonly List<GameObject> spawnedVfxObjects = new List<GameObject>();

        // Procedural sprites (generated once in Awake)
        private Sprite softCircleSprite;
        private Sprite ringSprite;
        private Sprite shardSprite;
        private Sprite streakSprite;
        private Sprite mistSprite;
        private Sprite sparkleSprite;
        private Sprite hexFlakeSprite;   // ❄ hex snowflake cho ambient

        // Sorting
        private int baseSortingOrder;
        private int sortingLayerId;
        private Transform vfxContainer;

        // Coroutines
        private Coroutine ambientRoutine;
        private Coroutine breathingRoutine;
        private Coroutine anticipationRoutine;
        private Coroutine dashTrailRoutine;

        // Shader lerp targets
        private float currentCharge;
        private float currentDash;
        private float impactFlash;

        private bool inAnticipation;
        private bool inDash;
        private Vector2 dashDirection = Vector2.right;

        // Crystal formation GameObjects — despawn sạch khi dash bắt đầu
        private readonly List<GameObject> crystalFormationObjects = new List<GameObject>();

        private bool CanSpawnVfx => isActiveAndEnabled && gameObject.activeInHierarchy;

        // ═════════════════════════════════════════════════════════════════════
        // Unity lifecycle
        // ═════════════════════════════════════════════════════════════════════
        private void Awake()
        {
            icefin    = GetComponent<IcefinEnemy>();
            renderers = icefin.GetVisualSpriteRenderers();

            if (glacialShader == null)
                glacialShader = Shader.Find("Bao/IcefinGlacialShader");

            if (glacialMaterial == null)
                glacialMaterial = Resources.Load<Material>("Materials/IcefinGlacial");

            vfxContainer = VFXContainerUtility.GetContainer();

            CacheSortingOrder();
            BuildRuntimeAssets();
            SetupMaterials();
        }

        private void OnEnable()
        {
            if (ambientRoutine  == null) ambientRoutine  = StartCoroutine(AmbientLoop());
            if (breathingRoutine == null) breathingRoutine = StartCoroutine(BreathingLoop());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            ambientRoutine    = null;
            breathingRoutine  = null;
            anticipationRoutine = null;
            dashTrailRoutine  = null;
            inAnticipation    = false;
            inDash            = false;
            currentCharge     = 0f;
            currentDash       = 0f;
            impactFlash       = 0f;
            UpdateShaderState();
            DespawnCrystalFormation(false);
            CleanupSpawnedVfx();
        }

        private void OnDestroy()
        {
            CleanupSpawnedVfx();
            foreach (Object asset in runtimeAssets)
            {
                // Skip null; skip glacialMaterial (shared asset từ Resources — không Destroy)
                // spriteMaterial & additiveMaterial là runtime-created → phải Destroy
                if (asset == null) continue;
                if (asset == glacialMaterial) continue;
                DestroyRuntimeObject(asset);
            }
            runtimeAssets.Clear();
        }

        private void Update()
        {
            float dt  = Time.deltaTime;
            float exp = 1f - Mathf.Exp(-transitionSpeed * dt);

            currentCharge = Mathf.Lerp(currentCharge, inAnticipation ? 1f : 0f, exp);
            currentDash   = Mathf.Lerp(currentDash,   inDash         ? 1f : 0f, exp * 1.2f);

            if (impactFlash > 0f)
                impactFlash = Mathf.MoveTowards(impactFlash, 0f, dt * 7f);

            UpdateShaderState();
        }

        // ═════════════════════════════════════════════════════════════════════
        // Public API — called from IcefinAttackState / IcefinEnemy
        // ═════════════════════════════════════════════════════════════════════

        public void TriggerAnticipationStart(float duration)
        {
            if (!CanSpawnVfx) return;

            if (anticipationRoutine != null) StopCoroutine(anticipationRoutine);

            inDash         = false;
            inAnticipation = true;
            anticipationRoutine = StartCoroutine(AnticipationRoutine(Mathf.Max(0.08f, duration)));
        }

        public void TriggerDashStart(Vector2 direction, float maxDuration)
        {
            if (!CanSpawnVfx) return;

            dashDirection  = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            inAnticipation = false;
            inDash         = true;

            // Vỡ crystals trước khi dash
            DespawnCrystalFormation(true);

            if (dashTrailRoutine != null) StopCoroutine(dashTrailRoutine);
            dashTrailRoutine = StartCoroutine(DashTrailLoop(Mathf.Max(0.1f, maxDuration)));

            SpawnLaunchBurst(transform.position, dashDirection);
        }

        public void TriggerDashEnd()
        {
            inAnticipation = false;
            inDash         = false;
        }

        public void TriggerDashImpact(Vector3 worldPos, Vector2 direction, bool hitPlayer)
        {
            inDash      = false;
            impactFlash = 1f;
            if (!CanSpawnVfx) return;

            Vector2 dir = direction.sqrMagnitude > 0.001f ? direction.normalized : dashDirection;
            SpawnImpactBurst(worldPos, dir, hitPlayer);
        }

        public void TriggerParryBreak()
        {
            inAnticipation = false;
            inDash         = false;
            impactFlash    = 1f;
            DespawnCrystalFormation(true);
            if (!CanSpawnVfx) return;

            SpawnParryBurst(transform.position + new Vector3(0f, 0.05f, 0f));
        }

        public void TriggerDeathBurst()
        {
            inAnticipation = false;
            inDash         = false;
            impactFlash    = 1f;
            DespawnCrystalFormation(false);
            if (!CanSpawnVfx) return;

            StartCoroutine(CrystallizeThenBurst(transform.position));
        }

        // ═════════════════════════════════════════════════════════════════════
        // Setup
        // ═════════════════════════════════════════════════════════════════════
        private void CacheSortingOrder()
        {
            baseSortingOrder = 0;
            sortingLayerId   = 0;
            if (renderers == null) return;

            bool found = false;
            foreach (SpriteRenderer sr in renderers)
            {
                if (sr == null) continue;
                baseSortingOrder = Mathf.Max(baseSortingOrder, sr.sortingOrder);
                if (!found) { sortingLayerId = sr.sortingLayerID; found = true; }
            }
        }

        private void BuildRuntimeAssets()
        {
            // Normal sprite material
            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader != null)
            {
                spriteMaterial   = new Material(spriteShader) { name = "Icefin_SpriteVFX" };
                additiveMaterial = new Material(spriteShader) { name = "Icefin_AdditiveVFX" };
                // Additive blending — glow toa trên nền tối map băng
                additiveMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                additiveMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                runtimeAssets.Add(spriteMaterial);
                runtimeAssets.Add(additiveMaterial);
            }

            softCircleSprite = CreateSoftCircleSprite(72);
            ringSprite       = CreateRingSprite(108);
            shardSprite      = CreateShardSprite(52, 80);
            streakSprite     = CreateStreakSprite(112, 36);
            mistSprite       = CreateMistSprite(108);
            sparkleSprite    = CreateSparkleSprite(72);
            hexFlakeSprite   = CreateHexFlakeSprite(64);

            Sprite[] sprites = { softCircleSprite, ringSprite, shardSprite,
                                  streakSprite, mistSprite, sparkleSprite, hexFlakeSprite };
            foreach (Sprite s in sprites)
            {
                runtimeAssets.Add(s);
                runtimeAssets.Add(s.texture);
            }
        }

        private void SetupMaterials()
        {
            if (renderers == null) return;

            if (glacialMaterial == null)
            {
                Debug.LogError("[IcefinVFX] glacialMaterial missing. Place IcefinGlacial.mat at Resources/Materials/.", this);
                return;
            }

            if (glacialShader == null) glacialShader = glacialMaterial.shader;

            if (glacialMaterial.shader != glacialShader)
            {
                Debug.LogError("[IcefinVFX] Material shader mismatch.", this);
                return;
            }

            // Đẩy palette mới vào material
            glacialMaterial.SetColor("_DeepIceColor", deepIceColor);
            glacialMaterial.SetColor("_GlacierColor", glacierColor);
            glacialMaterial.SetColor("_FrostColor",   frostColor);
            glacialMaterial.SetColor("_SnowColor",    snowColor);
            glacialMaterial.SetColor("_GlowColor",    glowColor);
            glacialMaterial.SetColor("_AccentColor",  accentColor);
            glacialMaterial.SetColor("_RimColor",     rimColor);
            glacialMaterial.SetColor("_OutlineColor", outlineColor);
            UpdateShaderState();

            foreach (SpriteRenderer sr in renderers)
                if (sr != null) sr.sharedMaterial = glacialMaterial;
        }

        private void UpdateShaderState()
        {
            if (glacialMaterial == null) return;

            glacialMaterial.SetFloat("_Charge",      currentCharge);
            glacialMaterial.SetFloat("_Dash",        currentDash);
            glacialMaterial.SetFloat("_ImpactFlash", impactFlash);
            glacialMaterial.SetVector("_DashDirection",
                new Vector4(dashDirection.x, dashDirection.y, 0f, 0f));
        }

        // ═════════════════════════════════════════════════════════════════════
        // AMBIENT — Bioluminescent motes + snowflakes
        // ═════════════════════════════════════════════════════════════════════
        private IEnumerator AmbientLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(ambientInterval.x, ambientInterval.y));
                if (icefin == null || icefin.IsDead || inDash) continue;
                SpawnAmbientMote();
            }
        }

        private void SpawnAmbientMote()
        {
            Vector3 center = transform.position + new Vector3(0f, 0.06f, 0f);

            // 50% sparkle, 30% hex snowflake, 20% soft circle
            float roll = Random.value;
            Sprite spr = roll < 0.5f ? sparkleSprite :
                         roll < 0.8f ? hexFlakeSprite :
                                       softCircleSprite;

            // Palette: glow > frost > glacier
            float r2 = Random.value;
            Color col = r2 < 0.45f ? glowColor :
                        r2 < 0.75f ? frostColor :
                                     glacierColor;

            bool  useAdditive = Random.value < 0.55f; // additive mạnh hơn trên nền tối
            float size        = Random.Range(0.030f, 0.065f);

            GameObject go = CreateSpriteVFX("Icefin_Mote", spr, col, useAdditive, baseSortingOrder + 3);
            go.transform.position   = center + (Vector3)(Random.insideUnitCircle * 0.50f);
            go.transform.localScale = Vector3.one * size;

            StartCoroutine(AnimateAmbientMote(go, col, Random.Range(0.50f, 1.0f)));
        }

        private IEnumerator AnimateAmbientMote(GameObject go, Color color, float duration)
        {
            SpriteRenderer sr  = go?.GetComponent<SpriteRenderer>();
            Vector3 vel        = new Vector3(Random.Range(-0.07f, 0.07f),
                                             Random.Range(0.02f,  0.09f), 0f);
            float   elapsed    = 0f;
            float   rotSpeed   = Random.Range(-60f, 60f);

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                go.transform.position += vel * Time.deltaTime;
                vel.y *= 1f - Time.deltaTime * 0.8f; // giảm dần
                go.transform.Rotate(0f, 0f, rotSpeed * Time.deltaTime);

                if (sr != null)
                {
                    Color c = color;
                    c.a = Mathf.Sin(t * Mathf.PI) * 0.52f;  // giảm từ 0.75 → 0.52
                    sr.color = c;
                }
                yield return null;
            }
            ReleaseVfxObject(go);
        }

        // ─────────────────────────────────────────────────────────────────────
        // BREATHING RING — nhịp thở tĩnh mịch khi idle
        // ─────────────────────────────────────────────────────────────────────
        private IEnumerator BreathingLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(breathingInterval.x, breathingInterval.y));
                if (icefin == null || icefin.IsDead || inDash || inAnticipation) continue;

                Vector3 center = transform.position;
                Color breathCol = new Color(glowColor.r, glowColor.g, glowColor.b, 0.10f);  // ring thở rất nhẹ
                SpawnRing(center, 0.28f, breathCol, 0.9f, true, baseSortingOrder - 1);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // ANTICIPATION — Crystal formation + charge motes
        // ═════════════════════════════════════════════════════════════════════
        private IEnumerator AnticipationRoutine(float duration)
        {
            // Burst đầu tiên — motes phóng ra rồi kéo vào tâm
            for (int i = 0; i < anticipationBurstCount; i++)
                SpawnChargeMote(true);

            // Spawn crystal formation vòng quanh thân
            SpawnCrystalFormation(0f);

            float elapsed    = 0f;
            float spawnTimer = 0f;
            float arcTimer   = 0f;
            float crystalTimer = 0f;
            int   crystalStage = 0; // 0=small, 1=medium, 2=full

            while (elapsed < duration && inAnticipation)
            {
                elapsed     += Time.deltaTime;
                spawnTimer  += Time.deltaTime;
                arcTimer    += Time.deltaTime;
                crystalTimer += Time.deltaTime;

                float charge01 = Mathf.Clamp01(elapsed / duration);

                // Motes: tăng tần suất theo charge
                float interval = Mathf.Lerp(anticipationSpawnInterval * 1.4f,
                                            anticipationSpawnInterval * 0.5f, charge01);
                while (spawnTimer >= interval)
                {
                    spawnTimer -= interval;
                    SpawnChargeMote(false);
                    if (charge01 > 0.4f && Random.value < 0.40f)
                        SpawnChargeShard();
                }

                // Spin arc — tinh thể xoay quanh
                if (arcTimer >= 0.08f)
                {
                    arcTimer = 0f;
                    SpawnSpinArc(charge01);
                }

                // Update crystal formation — lớn dần theo stage
                if (crystalStage == 0 && charge01 >= 0.35f) { crystalStage = 1; RegrowCrystalFormation(0.5f); }
                if (crystalStage == 1 && charge01 >= 0.70f) { crystalStage = 2; RegrowCrystalFormation(1.0f); }

                yield return null;
            }

            anticipationRoutine = null;
        }

        // ── Charge mote: bay vào tâm theo đường cong Bezier ─────────────────
        private void SpawnChargeMote(bool burst)
        {
            Vector3 center = transform.position + new Vector3(0f, 0.02f, 0f);
            float   angle  = Random.Range(0f, Mathf.PI * 2f);
            float   radius = Random.Range(anticipationRadius * 0.50f, anticipationRadius);

            Vector3 start  = center + new Vector3(Mathf.Cos(angle) * radius,
                                                   Mathf.Sin(angle) * radius * 0.60f, 0f);
            Vector3 target = center + (Vector3)(Random.insideUnitCircle * 0.04f);

            float r = Random.value;
            Color col = r < 0.40f ? glowColor :
                        r < 0.72f ? frostColor :
                                    glacierColor;

            bool additive = Random.value < 0.65f;
            float sz = burst ? Random.Range(0.050f, 0.090f) : Random.Range(0.032f, 0.065f);

            GameObject go = CreateSpriteVFX("Icefin_ChargeMote", softCircleSprite,
                                            col, additive, baseSortingOrder + 3);
            go.transform.position   = start;
            go.transform.localScale = Vector3.one * sz;

            float dur = burst ? 0.38f : Random.Range(0.20f, 0.30f);
            StartCoroutine(AnimateChargeMote(go, start, target, col, dur));
        }

        private IEnumerator AnimateChargeMote(GameObject go, Vector3 start,
                                               Vector3 target, Color color, float duration)
        {
            SpriteRenderer sr      = go?.GetComponent<SpriteRenderer>();
            Vector3 initialScale   = go.transform.localScale;
            // Control point lệch nhẹ — đường cong hữu cơ
            Vector3 control = Vector3.Lerp(start, target, 0.52f)
                            + new Vector3(Random.Range(-0.20f, 0.20f),
                                          Random.Range(-0.12f, 0.12f), 0f);
            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t     = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 2.5f);

                Vector3 a = Vector3.Lerp(start,   control, eased);
                Vector3 b = Vector3.Lerp(control, target,  eased);
                go.transform.position   = Vector3.Lerp(a, b, eased);
                go.transform.localScale = initialScale * Mathf.Lerp(1.15f, 0.05f, t);

                if (sr != null)
                {
                    // Lerp về frostColor thay vì snowColor — tránh flash trắng
                    Color c = Color.Lerp(color, frostColor, t * 0.60f);
                    c.a = Mathf.Sin(t * Mathf.PI) * 0.70f;  // giảm từ 0.88
                    sr.color = c;
                }
                yield return null;
            }
            ReleaseVfxObject(go);
        }

        // ── Charge shard: tinh thể nhỏ bắn vào tâm ─────────────────────────
        private void SpawnChargeShard()
        {
            Vector3 center = transform.position;
            Color   col    = Random.value < 0.55f ? glowColor : frostColor;
            GameObject go  = CreateSpriteVFX("Icefin_ChargeShard", shardSprite,
                                             col, true, baseSortingOrder + 5);
            go.transform.position   = center + (Vector3)(Random.insideUnitCircle * 0.40f);
            go.transform.localScale = new Vector3(Random.Range(0.04f, 0.08f),
                                                  Random.Range(0.10f, 0.18f), 1f);
            go.transform.rotation   = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            StartCoroutine(AnimateChargeShard(go, center));
        }

        private IEnumerator AnimateChargeShard(GameObject go, Vector3 target)
        {
            SpriteRenderer sr = go?.GetComponent<SpriteRenderer>();
            Vector3 start     = go.transform.position;
            float duration    = Random.Range(0.16f, 0.24f);
            float elapsed     = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t  = Mathf.Clamp01(elapsed / duration);
                go.transform.position   = Vector3.Lerp(start, target, t);
                go.transform.Rotate(0f, 0f, 480f * Time.deltaTime);
                go.transform.localScale = Vector3.Lerp(go.transform.localScale, Vector3.zero, t * 0.8f);

                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = Mathf.Lerp(0.85f, 0f, t);
                    sr.color = c;
                }
                yield return null;
            }
            ReleaseVfxObject(go);
        }

        // ── Spin arc: vành sáng xoay xung quanh ─────────────────────────────
        private void SpawnSpinArc(float charge01)
        {
            Vector3 center = transform.position;
            Color col = Color.Lerp(glacierColor, glowColor, charge01 * 0.7f);
            col = Color.Lerp(col, snowColor, charge01 * 0.25f);

            GameObject go = CreateSpriteVFX("Icefin_SpinArc", streakSprite, col,
                                            true, baseSortingOrder + 4);
            go.transform.position   = center;
            go.transform.localScale = new Vector3(Mathf.Lerp(0.50f, 0.90f, charge01),
                                                  Mathf.Lerp(0.08f, 0.15f, charge01), 1f);
            go.transform.rotation   = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            StartCoroutine(AnimateSpinArc(go, col, charge01));
        }

        private IEnumerator AnimateSpinArc(GameObject go, Color color, float charge01)
        {
            SpriteRenderer sr  = go?.GetComponent<SpriteRenderer>();
            float duration     = Mathf.Lerp(0.12f, 0.20f, charge01);
            Vector3 startScale = go.transform.localScale;
            Vector3 endScale   = new Vector3(startScale.x * 1.20f, startScale.y * 0.85f, 1f);
            float rotSpeed     = Random.Range(340f, 520f) * (Random.value < 0.5f ? -1f : 1f);
            float elapsed      = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t  = Mathf.Clamp01(elapsed / duration);
                go.transform.Rotate(0f, 0f, rotSpeed * Time.deltaTime);
                go.transform.localScale = Vector3.Lerp(startScale, endScale, t);

                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = Mathf.Lerp(0.65f, 0f, t);
                    sr.color = c;
                }
                yield return null;
            }
            ReleaseVfxObject(go);
        }

        // ── Crystal Formation — mọc ra xung quanh thân ──────────────────────
        private void SpawnCrystalFormation(float chargeLevel)
        {
            DespawnCrystalFormation(false);

            int count = crystalFormationCount;
            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.15f, 0.15f);
                Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * 0.65f, 0f).normalized;
                float dist  = Random.Range(0.32f, 0.55f);
                Vector3 pos = transform.position + dir * dist;

                float w = Mathf.Lerp(0.030f, 0.065f, chargeLevel) * Random.Range(0.8f, 1.2f);
                float h = Mathf.Lerp(0.090f, 0.200f, chargeLevel) * Random.Range(0.8f, 1.2f);

                Color col = Random.value < 0.6f ? glowColor : frostColor;
                GameObject go = CreateSpriteVFX("Icefin_Crystal", shardSprite,
                                                col, true, baseSortingOrder + 6);
                go.transform.position   = pos;
                go.transform.localScale = Vector3.zero;

                // Xoay đầu nhọn ra ngoài
                float angle2D = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                go.transform.rotation = Quaternion.Euler(0f, 0f, angle2D + Random.Range(-15f, 15f));

                crystalFormationObjects.Add(go);
                StartCoroutine(GrowCrystal(go, new Vector3(w, h, 1f), 0.18f));
            }
        }

        private void RegrowCrystalFormation(float chargeLevel)
        {
            // Phá crystal cũ, mọc lại lớn hơn
            DespawnCrystalFormation(false);
            SpawnCrystalFormation(chargeLevel);
        }

        private void DespawnCrystalFormation(bool burst)
        {
            if (crystalFormationObjects.Count == 0) return;
            GameObject[] tempCrystals = crystalFormationObjects.ToArray();
            crystalFormationObjects.Clear();

            for (int i = 0; i < tempCrystals.Length; i++)
            {
                GameObject go = tempCrystals[i];
                if (go == null) continue;
                if (burst)
                {
                    Vector3 dir = (go.transform.position - transform.position).normalized;
                    SpawnImpactShard(go.transform.position,
                                     new Vector2(dir.x, dir.y) + Random.insideUnitCircle * 0.4f,
                                     true, 0.4f, 1.0f);
                    ReleaseVfxObject(go);
                }
                else
                {
                    StartCoroutine(ShrinkAndRemove(go, 0.08f));
                }
            }
        }

                private IEnumerator GrowCrystal(GameObject go, Vector3 targetScale, float duration)
        {
            SpriteRenderer sr = go?.GetComponent<SpriteRenderer>();
            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float e = 1f - Mathf.Pow(1f - t, 3f);
                go.transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, e);

                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = t;
                    sr.color = c;
                }
                yield return null;
            }
        }

        private IEnumerator ShrinkAndRemove(GameObject go, float duration)
        {
            if (go == null) yield break;
            SpriteRenderer sr  = go.GetComponent<SpriteRenderer>();
            Vector3 startScale = go.transform.localScale;
            float elapsed      = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t  = Mathf.Clamp01(elapsed / duration);
                go.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                if (sr != null) { Color c = sr.color; c.a = 1f - t; sr.color = c; }
                yield return null;
            }
            ReleaseVfxObject(go);
        }

        // ═════════════════════════════════════════════════════════════════════
        // DASH — Trail, Afterimage, Launch burst
        // ═════════════════════════════════════════════════════════════════════
        private IEnumerator DashTrailLoop(float maxDuration)
        {
            float elapsed    = 0f;
            float trailTimer = 0f;
            float imageTimer = 0f;

            while (inDash && elapsed < maxDuration + 0.10f)
            {
                elapsed    += Time.deltaTime;
                trailTimer += Time.deltaTime;
                imageTimer += Time.deltaTime;

                while (trailTimer >= dashTrailInterval)
                {
                    trailTimer -= dashTrailInterval;
                    SpawnDashTrailElement();
                }

                if (imageTimer >= afterimageInterval)
                {
                    imageTimer = 0f;
                    SpawnAfterimage();
                }

                yield return null;
            }
            dashTrailRoutine = null;
        }

        private void SpawnDashTrailElement()
        {
            Vector3 center = transform.position;
            Vector3 back   = -(Vector3)dashDirection;

            // 1. Streak cyan sắc nét
            Color streakCol = Color.Lerp(glowColor, frostColor, Random.Range(0.2f, 0.6f));
            SpawnStreak(center + back * 0.10f, dashDirection, streakCol,
                        Random.Range(0.16f, 0.26f));

            // 2. Double mist: glowColor phía sau, frostColor phía trước
            SpawnMist(center + back * 0.08f,
                      back * Random.Range(0.35f, 0.85f),
                      glowColor, 0.11f, 0.22f, baseSortingOrder + 2, true);

            SpawnMist(center + (Vector3)dashDirection * 0.04f,
                      (Vector3)dashDirection * Random.Range(0.10f, 0.25f),
                      frostColor, 0.07f, 0.14f, baseSortingOrder + 1, false);

            // 3. Freeze crystal đọng lại trong không gian
            if (Random.value < 0.50f)
                SpawnFreezeTrailCrystal(center + back * Random.Range(0.1f, 0.25f));

            // 4. Shard nhỏ bắn sang hai bên
            if (Random.value < 0.35f)
            {
                Vector2 side = new Vector2(-dashDirection.y, dashDirection.x);
                SpawnImpactShard(center, (side + dashDirection * 0.2f) * (Random.value < 0.5f ? 1f : -1f),
                                 false, 0.2f, 0.6f);
            }
        }

        // Freeze crystal đóng băng không gian — xuất hiện rồi đứng yên tan dần
        private void SpawnFreezeTrailCrystal(Vector3 pos)
        {
            Color col  = Random.value < 0.55f ? glacierColor : accentColor;
            GameObject go = CreateSpriteVFX("Icefin_FreezeTrail", shardSprite,
                                            col, false, baseSortingOrder);
            go.transform.position   = pos;
            go.transform.localScale = new Vector3(Random.Range(0.018f, 0.032f),
                                                  Random.Range(0.055f, 0.090f), 1f);
            go.transform.rotation   = Quaternion.Euler(0f, 0f,
                                          Random.Range(-40f, 40f));
            StartCoroutine(AnimateFreezeTrailCrystal(go));
        }

        private IEnumerator AnimateFreezeTrailCrystal(GameObject go)
        {
            SpriteRenderer sr  = go?.GetComponent<SpriteRenderer>();
            Vector3 targetScale = go.transform.localScale;
            go.transform.localScale = Vector3.zero;

            // Mọc ra nhanh
            float growDur = 0.055f;
            for (float t = 0f; t < growDur && go != null; t += Time.deltaTime)
            {
                go.transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, t / growDur);
                if (sr != null) { Color c = sr.color; c.a = (t / growDur) * 0.55f; sr.color = c; }
                yield return null;
            }
            if (go == null) yield break;
            if (sr != null) { Color c = sr.color; c.a = 0.55f; sr.color = c; }

            // Hold
            yield return new WaitForSeconds(Random.Range(0.25f, 0.45f));

            // Tan dần
            float fadeDur = Random.Range(0.15f, 0.25f);
            float startA  = 0.55f;
            for (float t = 0f; t < fadeDur && go != null; t += Time.deltaTime)
            {
                if (sr != null) { Color c = sr.color; c.a = Mathf.Lerp(startA, 0f, t / fadeDur); sr.color = c; }
                yield return null;
            }
            ReleaseVfxObject(go);
        }

        // Afterimage — bản sao mờ của sprite chính
        private void SpawnAfterimage()
        {
            if (renderers == null || renderers.Length == 0) return;
            SpriteRenderer source = renderers[0];
            if (source == null || source.sprite == null) return;

            Color col = new Color(glowColor.r, glowColor.g, glowColor.b, 0.13f);  // afterimage nhạt hơn
            GameObject go = CreateSpriteVFX("Icefin_Afterimage", source.sprite,
                                            col, true, source.sortingOrder - 2);
            go.transform.position   = source.transform.position;
            go.transform.rotation   = source.transform.rotation;
            go.transform.localScale = source.transform.lossyScale;
            StartCoroutine(AnimateAfterimage(go, col));
        }

        private IEnumerator AnimateAfterimage(GameObject go, Color startColor)
        {
            SpriteRenderer sr  = go?.GetComponent<SpriteRenderer>();
            Vector3 startScale = go.transform.localScale;
            const float dur    = 0.14f;
            float elapsed      = 0f;

            while (go != null && elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t  = Mathf.Clamp01(elapsed / dur);
                go.transform.localScale = Vector3.Lerp(startScale, startScale * 0.90f, t);

                if (sr != null)
                {
                    Color c = startColor;
                    c.a = Mathf.Lerp(0.13f, 0f, t);  // fade từ 0.13 (không phải 0.22)
                    sr.color = c;
                }
                yield return null;
            }
            ReleaseVfxObject(go);
        }

        // Launch burst khi bắt đầu dash
        private void SpawnLaunchBurst(Vector3 center, Vector2 direction)
        {
            // Ring frostColor (không dùng snowColor thuần trắng)
            SpawnRing(center, 0.60f, frostColor,   0.14f, true,  baseSortingOrder + 8);
            // Ring cyan chính
            SpawnRing(center, 0.90f, glowColor,    0.22f, true,  baseSortingOrder + 7);
            // Ring teal phụ — additive false để không quá sáng
            SpawnRing(center, 1.20f, glacierColor, 0.30f, false, baseSortingOrder + 5);

            // Streaks — dùng frostColor thay snowColor
            for (int i = 0; i < 6; i++)          // giảm từ 8 → 6
                SpawnStreak(center, direction + Random.insideUnitCircle * 0.22f,
                            i % 2 == 0 ? frostColor : glowColor,
                            Random.Range(0.14f, 0.22f));

            // Mist back-blast: giảm count 7→5, startSize nhỏ hơn
            for (int i = 0; i < 5; i++)
                SpawnMist(center,
                          -(Vector3)direction * Random.Range(0.25f, 0.90f)
                           + (Vector3)Random.insideUnitCircle * 0.28f,
                          glowColor, 0.10f, 0.25f, baseSortingOrder + 3, true);

            // 3 large shards (giảm từ 4)
            for (int i = 0; i < 3; i++)
                SpawnLargeShard(center, direction + Random.insideUnitCircle * 0.20f);
        }

        // ═════════════════════════════════════════════════════════════════════
        // IMPACT BURST — Hit tường / hit player
        // ═════════════════════════════════════════════════════════════════════
        private void SpawnImpactBurst(Vector3 center, Vector2 direction, bool hitPlayer)
        {
            float r = hitPlayer ? 1.0f : 0.72f;

            // Shockwave: frostColor thay snowColor — không flash trắng bệch
            SpawnRing(center, r * 1.5f, frostColor,   0.10f, true,  baseSortingOrder + 10);
            // Ring glow chính
            SpawnRing(center, r,         glowColor,   0.22f, true,  baseSortingOrder + 9);
            // Ring glacier echo
            SpawnRing(center, r * 0.65f, glacierColor, 0.38f, false, baseSortingOrder + 7);

            int nStreaks = hitPlayer ? 8 : 6;    // giảm count
            int nMists   = hitPlayer ? 8 : 6;
            int nShards  = hitPlayer ? impactShardCount + 2 : impactShardCount;

            // Streaks — frostColor thay snowColor
            for (int i = 0; i < nStreaks; i++)
            {
                Color col = i % 3 == 0 ? frostColor : (i % 3 == 1 ? glowColor : accentColor);
                SpawnStreak(center, direction + Random.insideUnitCircle * 0.50f,
                            col, Random.Range(0.16f, 0.28f));
            }

            // Shards tung ra
            if (hitPlayer)
            {
                // Hit player: 12 shards vòng tròn + shards hướng impact
                for (int i = 0; i < 12; i++)
                {
                    float angle = (i / 12f) * Mathf.PI * 2f;
                    Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    SpawnImpactShard(center, dir, true, 0.7f, 2.0f);
                }
                for (int i = 0; i < 5; i++)
                    SpawnLargeShard(center, direction + Random.insideUnitCircle * 0.30f);
            }
            else
            {
                // Hit tường: shards theo hướng + hướng ngược
                for (int i = 0; i < nShards; i++)
                    SpawnImpactShard(center, direction + Random.insideUnitCircle * 0.80f,
                                     false, 0.45f, 1.40f);
                for (int i = 0; i < 3; i++)
                    SpawnLargeShard(center, direction + Random.insideUnitCircle * 0.45f);
            }

            // Mist cloud — startSize nhỏ hơn, duration ngắn hơn
            for (int i = 0; i < nMists; i++)
                SpawnMist(center, (Vector3)Random.insideUnitCircle * Random.Range(0.30f, 1.00f),
                          i % 2 == 0 ? glowColor : glacierColor,
                          0.12f, Random.Range(0.25f, 0.38f),
                          baseSortingOrder + 3, i % 3 == 0);
        }

        // ═════════════════════════════════════════════════════════════════════
        // PARRY BURST
        // ═════════════════════════════════════════════════════════════════════
        private void SpawnParryBurst(Vector3 center)
        {
            SpawnRing(center, 0.80f, frostColor,   0.12f, true,  baseSortingOrder + 10);
            SpawnRing(center, 1.10f, glowColor,    0.20f, true,  baseSortingOrder + 9);
            SpawnRing(center, 1.45f, glacierColor, 0.30f, false, baseSortingOrder + 7);

            for (int i = 0; i < 12; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                SpawnImpactShard(center, dir, i % 2 == 0, 0.45f, 1.30f);
            }
            for (int i = 0; i < 4; i++)
                SpawnLargeShard(center, Random.insideUnitCircle.normalized);

            for (int i = 0; i < 7; i++)
                SpawnStreak(center, Random.insideUnitCircle.normalized,
                            i % 2 == 0 ? frostColor : glowColor,  // frostColor thay snowColor
                            Random.Range(0.16f, 0.26f));

            for (int i = 0; i < 6; i++)                              // giảm 8→6
                SpawnMist(center, (Vector3)Random.insideUnitCircle * Random.Range(0.4f, 1.0f),
                          i % 2 == 0 ? glowColor : accentColor,       // accentColor thay frostColor
                          0.11f, 0.26f, baseSortingOrder + 3, true);
        }

        // ═════════════════════════════════════════════════════════════════════
        // DEATH — Crystallize, pause, cascade burst
        // ═════════════════════════════════════════════════════════════════════
        private IEnumerator CrystallizeThenBurst(Vector3 center)
        {
            // Phase 1: freeze pause — flash trắng, shader xử lý whitening
            yield return new WaitForSeconds(0.18f);
            if (!CanSpawnVfx) yield break;

            // Phase 2: 3 rings cascade — frostColor thay snowColor
            SpawnRing(center, 0.55f, frostColor,   0.13f, true,  baseSortingOrder + 10);
            yield return new WaitForSeconds(0.045f);
            SpawnRing(center, 1.05f, glowColor,    0.22f, true,  baseSortingOrder + 9);
            yield return new WaitForSeconds(0.045f);
            SpawnRing(center, 1.65f, glacierColor, 0.34f, false, baseSortingOrder + 7);
            yield return new WaitForSeconds(0.02f);
            SpawnRing(center, 2.20f, accentColor,  0.40f, false, baseSortingOrder + 5);

            // Phase 3: 18 shards (giảm từ 24) — ít overburn hơn
            for (int i = 0; i < 18; i++)
            {
                float   angle = (i / 18f) * Mathf.PI * 2f;
                Vector2 dir   = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                bool    big   = i % 5 == 0;           // 1/5 large (thay 1/4)
                if (big)
                    SpawnLargeShard(center, dir);
                else
                    SpawnImpactShard(center, dir, i % 2 == 0, 0.65f, 2.10f);
            }

            // Phase 4: mist cloud — giảm count 22→14, size nhỏ hơn
            for (int i = 0; i < 14; i++)
                SpawnMist(center,
                          Random.insideUnitCircle * Random.Range(0.30f, 1.30f),
                          i % 3 == 0 ? glowColor : (i % 3 == 1 ? glacierColor : accentColor),
                          Random.Range(0.12f, 0.22f), Random.Range(0.35f, 0.55f),
                          baseSortingOrder + 3, i % 3 == 0);   // additive chỉ 1/3 (thay 1/2)

            // Phase 5: sparkles — giảm count 14→8, size nhỏ hơn
            for (int i = 0; i < 8; i++)
            {
                Vector3 pos = center + (Vector3)Random.insideUnitCircle * Random.Range(0.4f, 1.6f);
                GameObject go = CreateSpriteVFX("Icefin_DeathSparkle", sparkleSprite,
                                                glowColor, true, baseSortingOrder + 6);
                go.transform.position   = pos;
                go.transform.localScale = Vector3.one * Random.Range(0.03f, 0.07f);  // nhỏ hơn
                StartCoroutine(AnimateAmbientMote(go, glowColor, Random.Range(0.35f, 0.65f)));
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // Primitive spawners
        // ═════════════════════════════════════════════════════════════════════

        private void SpawnStreak(Vector3 center, Vector2 direction, Color color, float duration)
        {
            GameObject go = CreateSpriteVFX("Icefin_Streak", streakSprite,
                                            color, true, baseSortingOrder + 5);
            go.transform.position = center;
            go.transform.right    = direction.sqrMagnitude > 0.0001f
                                    ? (Vector3)direction : Vector3.right;
            go.transform.localScale = new Vector3(Random.Range(0.38f, 0.64f),
                                                  Random.Range(0.055f, 0.105f), 1f);
            StartCoroutine(AnimateStreak(go, color, duration));
        }

        private IEnumerator AnimateStreak(GameObject go, Color color, float duration)
        {
            SpriteRenderer sr  = go?.GetComponent<SpriteRenderer>();
            Vector3 startScale = go.transform.localScale;
            float elapsed      = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t  = Mathf.Clamp01(elapsed / duration);
                go.transform.localScale = Vector3.Lerp(startScale,
                    new Vector3(startScale.x * 1.18f, 0f, 1f), t);

                if (sr != null)
                {
                    Color c = color; c.a = Mathf.Lerp(0.58f, 0f, t); sr.color = c;  // giảm từ 0.80
                }
                yield return null;
            }
            ReleaseVfxObject(go);
        }

        private void SpawnMist(Vector3 center, Vector3 velocity, Color color,
                               float startSize, float duration, int sortingOrder, bool additive)
        {
            GameObject go = CreateSpriteVFX("Icefin_Mist", mistSprite, color,
                                            additive, sortingOrder);
            go.transform.position   = center;
            go.transform.localScale = new Vector3(startSize, startSize * 0.68f, 1f);
            StartCoroutine(AnimateMist(go, velocity, color, duration));
        }

        private IEnumerator AnimateMist(GameObject go, Vector3 velocity, Color color, float duration)
        {
            SpriteRenderer sr  = go?.GetComponent<SpriteRenderer>();
            Vector3 startScale = go.transform.localScale;
            float elapsed      = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t  = Mathf.Clamp01(elapsed / duration);

                velocity *= Mathf.Exp(-3.2f * Time.deltaTime);
                go.transform.position   += velocity * Time.deltaTime;
                go.transform.localScale  = startScale * Mathf.Lerp(1f, 2.6f, t);

                if (sr != null)
                {
                    Color c = color; c.a = Mathf.Lerp(0.18f, 0f, t); sr.color = c;  // mist nhạt hơn
                }
                yield return null;
            }
            ReleaseVfxObject(go);
        }

        private void SpawnImpactShard(Vector3 center, Vector2 direction,
                                      bool bright, float minSpeed, float maxSpeed)
        {
            float r = Random.value;
            Color col = r < 0.45f ? glowColor :
                        r < 0.75f ? frostColor :
                                    snowColor;

            GameObject go = CreateSpriteVFX("Icefin_Shard", shardSprite,
                                            col, bright, baseSortingOrder + 6);
            go.transform.position   = center;
            go.transform.localScale = new Vector3(Random.Range(0.025f, 0.055f),
                                                  Random.Range(0.075f, 0.160f), 1f);
            go.transform.rotation   = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            Vector2 normDir = direction.sqrMagnitude > 0.0001f
                              ? direction.normalized
                              : Random.insideUnitCircle.normalized;
            Vector3 vel = (Vector3)normDir * Random.Range(minSpeed, maxSpeed)
                        + (Vector3)Random.insideUnitCircle * 0.28f;

            float dur = bright ? Random.Range(0.24f, 0.40f) : Random.Range(0.18f, 0.30f);
            StartCoroutine(AnimateShard(go, vel, dur));
        }

        // Large shard — 2.5x size, tốc độ thấp hơn, sống lâu hơn
        private void SpawnLargeShard(Vector3 center, Vector2 direction)
        {
            Color col = Random.value < 0.6f ? glowColor : frostColor;
            GameObject go = CreateSpriteVFX("Icefin_LargeShard", shardSprite,
                                            col, true, baseSortingOrder + 7);
            go.transform.position   = center + (Vector3)direction.normalized * 0.08f;
            go.transform.localScale = new Vector3(Random.Range(0.065f, 0.110f),
                                                  Random.Range(0.200f, 0.340f), 1f);
            float angle2D = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle2D + Random.Range(-20f, 20f));

            Vector3 vel = (Vector3)direction.normalized * Random.Range(0.9f, 2.2f)
                        + (Vector3)Random.insideUnitCircle * 0.22f;
            StartCoroutine(AnimateShard(go, vel, Random.Range(0.38f, 0.55f)));
        }

        private IEnumerator AnimateShard(GameObject go, Vector3 velocity, float duration)
        {
            SpriteRenderer sr  = go?.GetComponent<SpriteRenderer>();
            Vector3 startScale = go.transform.localScale;
            float rot          = Random.Range(-640f, 640f);
            float elapsed      = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t  = Mathf.Clamp01(elapsed / duration);

                velocity            *= Mathf.Exp(-1.6f * Time.deltaTime);
                go.transform.position   += velocity * Time.deltaTime;
                go.transform.Rotate(0f, 0f, rot * Time.deltaTime);
                go.transform.localScale  = startScale * Mathf.Lerp(1f, 0.25f, t);

                if (sr != null && t > 0.35f)
                {
                    Color c = sr.color;
                    c.a = 1f - Mathf.InverseLerp(0.35f, 1f, t);
                    sr.color = c;
                }
                yield return null;
            }
            ReleaseVfxObject(go);
        }

        private void SpawnRing(Vector3 center, float radius, Color color,
                               float duration, bool additive, int sortingOrder)
        {
            GameObject go = CreateSpriteVFX("Icefin_Ring", ringSprite,
                                            color, additive, sortingOrder);
            go.transform.position   = center;
            go.transform.localScale = Vector3.one * 0.06f;
            StartCoroutine(AnimateRing(go, color, radius, duration));
        }

        private IEnumerator AnimateRing(GameObject go, Color color, float radius, float duration)
        {
            SpriteRenderer sr  = go?.GetComponent<SpriteRenderer>();
            Vector3 startScale = Vector3.one * 0.06f;
            Vector3 endScale   = Vector3.one * radius * 2f;
            float elapsed      = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t     = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                go.transform.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);

                if (sr != null)
                {
                    Color c = color;
                    c.a = Mathf.Pow(1f - t, 1.4f) * color.a;
                    sr.color = c;
                }
                yield return null;
            }
            ReleaseVfxObject(go);
        }

        // ═════════════════════════════════════════════════════════════════════
        // Object factory
        // ═════════════════════════════════════════════════════════════════════
        private GameObject CreateSpriteVFX(string objectName, Sprite sprite,
                                           Color color, bool additive, int sortingOrder)
        {
            GameObject go = new GameObject(objectName);
            if (vfxContainer != null) go.transform.SetParent(vfxContainer, true);

            SpriteRenderer sr   = go.AddComponent<SpriteRenderer>();
            sr.sprite            = sprite;
            sr.color             = color;
            sr.sortingLayerID    = sortingLayerId;
            sr.sortingOrder      = sortingOrder;
            sr.sharedMaterial    = additive ? additiveMaterial : spriteMaterial;

            spawnedVfxObjects.Add(go);
            return go;
        }

        private void ReleaseVfxObject(GameObject go)
        {
            if (go == null) return;
            spawnedVfxObjects.Remove(go);
            crystalFormationObjects.Remove(go);
            DestroyRuntimeObject(go);
        }

        private static void DestroyRuntimeObject(Object asset)
        {
            if (asset == null) return;
            if (Application.isPlaying) Destroy(asset);
            else DestroyImmediate(asset);
        }

        private void CleanupSpawnedVfx()
        {
            foreach (GameObject go in spawnedVfxObjects)
                if (go != null) DestroyRuntimeObject(go);
            spawnedVfxObjects.Clear();
        }

        // ═════════════════════════════════════════════════════════════════════
        // Procedural sprite generators
        // ═════════════════════════════════════════════════════════════════════

        private static Sprite CreateSoftCircleSprite(int size)
        {
            Texture2D tex    = NewTexture(size, size, "Icefin_SoftCircle");
            Vector2   center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float     maxR   = size * 0.5f;

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxR;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a); // smoothstep
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0,0,size,size), new Vector2(0.5f,0.5f), size);
        }

        private static Sprite CreateRingSprite(int size)
        {
            Texture2D tex    = NewTexture(size, size, "Icefin_Ring");
            Vector2   center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float     radius = size * 0.40f;
            float     thick  = size * 0.045f;

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    float e = Mathf.Abs(d - radius);
                    float a = Mathf.Clamp01(1f - e / thick);
                    a = a * a;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0,0,size,size), new Vector2(0.5f,0.5f), size);
        }

        private static Sprite CreateShardSprite(int width, int height)
        {
            Texture2D tex = NewTexture(width, height, "Icefin_Shard");

            for (int y = 0; y < height; y++)
            {
                float ny       = (y / (float)(height - 1)) * 2f - 1f;
                float halfW    = Mathf.Lerp(0f, 0.96f, 1f - Mathf.Abs(ny));

                // Inner vein — làm mạch sáng giữa shard
                float vein = Mathf.Clamp01(1f - Mathf.Abs(ny) * 3f) * 0.35f;

                for (int x = 0; x < width; x++)
                {
                    float nx   = Mathf.Abs((x / (float)(width - 1)) * 2f - 1f);
                    float body = Mathf.Clamp01((halfW - nx) * 9f);
                    float a    = Mathf.Max(body, vein * body);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0,0,width,height), new Vector2(0.5f, 0.42f), height);
        }

        private static Sprite CreateStreakSprite(int width, int height)
        {
            Texture2D tex = NewTexture(width, height, "Icefin_Streak");

            for (int y = 0; y < height; y++)
            {
                float ny       = Mathf.Abs((y / (float)(height - 1)) * 2f - 1f);
                float vertical = Mathf.Clamp01(1f - ny * 2.6f);

                for (int x = 0; x < width; x++)
                {
                    float nx   = x / (float)(width - 1);
                    float head = Mathf.Clamp01(1f - Mathf.Abs(nx - 0.18f) / 0.17f);
                    float tail = Mathf.Clamp01(1f - Mathf.Abs(nx - 0.60f) / 0.54f);
                    float a    = Mathf.Max(head, tail * 0.78f) * vertical;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0,0,width,height), new Vector2(0.10f,0.5f), width);
        }

        private static Sprite CreateMistSprite(int size)
        {
            Texture2D tex    = NewTexture(size, size, "Icefin_Mist");
            Vector2   center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 p    = (new Vector2(x, y) - center) / center.x;
                    float   dist = p.magnitude;
                    float   a    = Mathf.Clamp01(1f - dist * 1.20f);
                    a = Mathf.Pow(a, 1.7f)
                      * (0.70f + 0.30f * Mathf.PerlinNoise((x + 12f) * 0.09f,
                                                             (y + 4f)  * 0.09f));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0,0,size,size), new Vector2(0.5f,0.5f), size);
        }

        private static Sprite CreateSparkleSprite(int size)
        {
            Texture2D tex    = NewTexture(size, size, "Icefin_Sparkle");
            Vector2   center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 p  = new Vector2(x, y) - center;
                    float   nx = Mathf.Abs(p.x) / center.x;
                    float   ny = Mathf.Abs(p.y) / center.y;
                    float   h  = Mathf.Clamp01(1f - (nx * 0.62f + ny * 8.5f));
                    float   v  = Mathf.Clamp01(1f - (ny * 0.62f + nx * 8.5f));
                    // 4-ray sparkle + soft core
                    float core = Mathf.Clamp01(1f - (nx + ny) * 2.2f);
                    float a    = Mathf.Max(Mathf.Max(h, v), core * 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0,0,size,size), new Vector2(0.5f,0.5f), size);
        }

        // ❄ Hex snowflake — 6 cánh đối xứng
        private static Sprite CreateHexFlakeSprite(int size)
        {
            Texture2D tex    = NewTexture(size, size, "Icefin_HexFlake");
            Vector2   center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float     maxR   = size * 0.45f;

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 p    = new Vector2(x, y) - center;
                    float   dist = p.magnitude;
                    if (dist > maxR) { tex.SetPixel(x, y, Color.clear); continue; }

                    float angle = Mathf.Atan2(p.y, p.x);
                    // 6-fold symmetry: cos(6θ)
                    float hex6 = Mathf.Cos(angle * 6f);
                    // Arm pattern: sáng dọc 6 hướng, mờ giữa
                    float arm   = Mathf.Clamp01(hex6 * hex6 * hex6);
                    // Inner ring
                    float ring  = Mathf.Clamp01(1f - Mathf.Abs(dist / maxR - 0.45f) * 8f);
                    // Center dot
                    float core  = Mathf.Clamp01(1f - dist / (maxR * 0.25f));
                    float a     = Mathf.Max(arm * (1f - dist / maxR),
                                  Mathf.Max(ring * 0.6f, core * 0.8f));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0,0,size,size), new Vector2(0.5f,0.5f), size);
        }

        private static Texture2D NewTexture(int width, int height, string name)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp,
                name       = name
            };
            Color clear = new Color(1f, 1f, 1f, 0f);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
            tex.SetPixels(pixels);
            return tex;
        }
    }
}
