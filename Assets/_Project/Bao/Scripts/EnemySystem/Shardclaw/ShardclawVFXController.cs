using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Bao.Enemies
{
    /// <summary>
    /// High-polish VFX controller for Shardclaw.
    /// - Keeps compatibility with Bao/ShardclawEarthShader (_CrystalGlow, _BurrowState).
    /// - Adds crystal breathing, charge energy suction, idle motes,
    ///   impact shockwaves, dust, rock debris and crystal shards.
    /// - Procedural sprites are generated once and reused (no per-particle texture allocations).
    /// </summary>
    [RequireComponent(typeof(ShardclawEnemy))]
    public class ShardclawVFXController : MonoBehaviour
    {
        [Header("Main Enemy Shader")]
        [SerializeField] private Shader earthShader;
        [SerializeField] private Color crystalColor = new Color(1.0f, 0.46f, 0.08f, 1f);
        [SerializeField] private Color crystalHotColor = new Color(1.0f, 0.95f, 0.55f, 1f);
        [SerializeField] private Color tealAccent = new Color(0.08f, 0.95f, 0.78f, 1f);
        [SerializeField] private float transitionSpeed = 6.5f;

        [Header("Crystal Breathing")]
        [SerializeField, Range(0f, 1f)] private float idlePulseAmount = 0.16f;
        [SerializeField] private float idlePulseSpeed = 2.1f;
        [SerializeField] private float chargePulseSpeed = 12f;
        [SerializeField] private float chargeGlowBoost = 2.2f;
        [SerializeField] private float impactGlowBoost = 4.5f;

        [Header("Idle Crystal Motes")]
        [SerializeField] private bool enableIdleMotes = true;
        [SerializeField] private Vector2 idleMoteInterval = new Vector2(0.18f, 0.42f);
        [SerializeField] private Vector2 idleMoteOffset = new Vector2(0f, 0.32f);
        [SerializeField] private Vector2 idleMoteArea = new Vector2(1.2f, 0.65f);

        [Header("Charge / Anticipation")]
        [SerializeField] private float chargeRadius = 1.25f;
        [SerializeField] private float chargeTargetHeight = 0.45f;
        [SerializeField] private float chargeMoteInterval = 0.035f;
        [SerializeField] private float chargeMoteDuration = 0.34f;
        [SerializeField] private int chargeStartBurst = 8;

        [Header("Impact Palette")]
        [SerializeField] private Color earthDustColor = new Color(0.47f, 0.31f, 0.18f, 0.86f);
        [SerializeField] private Color hotDustColor = new Color(1.0f, 0.48f, 0.08f, 0.92f);
        [SerializeField] private Color obsidianRockColor = new Color(0.12f, 0.08f, 0.16f, 1f);
        [SerializeField] private Color shardColor = new Color(1.0f, 0.58f, 0.09f, 1f);

        [Header("Impact Burst")]
        [SerializeField, Range(0, 60)] private int dustCount = 24;
        [SerializeField, Range(0, 30)] private int rockCount = 10;
        [SerializeField, Range(0, 30)] private int crystalShardCount = 12;
        [SerializeField] private float impactRadiusMultiplier = 1.0f;
        [SerializeField] private float shockwaveDuration = 0.26f;
        [SerializeField] private float secondaryShockwaveDelay = 0.055f;

        [Header("Optional Additive Shader")]
        [Tooltip("Assign Bao/ShardclawAdditiveGlow. If left empty the controller will try Shader.Find.")]
        [SerializeField] private Shader additiveShader;


        private ShardclawEnemy shardclaw;
        private SpriteRenderer[] renderers;
        private Material[] earthMaterials;

        private Material spriteMaterial;
        private Material additiveMaterial;

        private Sprite softCircleSprite;
        private Sprite ringSprite;
        private Sprite shardSprite;
        private Sprite rockSprite;

        private float currentBurrow;
        private float charge01;
        private float impactFlash;
        private bool chargeActive;

        private Coroutine chargeRoutine;
        private Coroutine impactRoutine;
        private Coroutine idleRoutine;

        private int baseSortingOrder;
        private Transform vfxContainer;

        private readonly List<Object> runtimeAssets = new List<Object>();

        private void Awake()
        {
            shardclaw = GetComponent<ShardclawEnemy>();
            renderers = shardclaw.GetVisualSpriteRenderers();

            if (earthShader == null)
                earthShader = Shader.Find("Bao/ShardclawEarthShader");

            if (additiveShader == null)
                additiveShader = Shader.Find("Bao/ShardclawAdditiveGlow");

            vfxContainer = VFXContainerUtility.GetContainer();

            CacheSortingOrder();
            SetupMaterials();
            BuildRuntimeVFXAssets();
        }

        private void OnEnable()
        {
            if (idleRoutine == null)
                idleRoutine = StartCoroutine(IdleMoteLoop());
        }

        private void OnDisable()
        {
            if (idleRoutine != null)
            {
                StopCoroutine(idleRoutine);
                idleRoutine = null;
            }

            chargeActive = false;
            charge01 = 0f;
            impactFlash = 0f;
        }

        private void OnDestroy()
        {
            if (earthMaterials != null)
            {
                foreach (Material mat in earthMaterials)
                {
                    if (mat != null)
                        Destroy(mat);
                }
            }

            foreach (Object asset in runtimeAssets)
            {
                if (asset != null)
                    Destroy(asset);
            }

            runtimeAssets.Clear();
        }

        private void Update()
        {
            bool isSlamming = shardclaw.StateMachine?.CurrentState is ShardclawSlamState;

            float targetBurrow = isSlamming ? 7.5f : 0.5f;
            currentBurrow = Mathf.Lerp(
                currentBurrow,
                targetBurrow,
                1f - Mathf.Exp(-transitionSpeed * Time.deltaTime));

            if (!isSlamming && !chargeActive && impactFlash <= 0.001f)
                charge01 = Mathf.MoveTowards(charge01, 0f, Time.deltaTime * 4f);

            UpdateEnemyShader();
        }

        // ---------------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------------

        /// <summary>
        /// Call when Shardclaw enters the anticipation phase.
        /// The charge reaches 1.0 at the end and holds until impact.
        /// </summary>
        public void TriggerChargeStart(float duration)
        {
            if (!isActiveAndEnabled)
                return;

            if (chargeRoutine != null)
                StopCoroutine(chargeRoutine);

            chargeRoutine = StartCoroutine(ChargeRoutine(Mathf.Max(0.08f, duration)));
        }

        /// <summary>
        /// Backwards-compatible impact call.
        /// </summary>
        public void TriggerSlamImpact()
        {
            Vector3 position = shardclaw.VfxSpawnPoint != null
                ? shardclaw.VfxSpawnPoint.position
                : transform.position + new Vector3(0f, -0.35f, 0f);

            TriggerSlamImpact(position, shardclaw.EmergeDamageRadius);
        }

        /// <summary>
        /// Preferred impact call so VFX center/radius exactly match the attack.
        /// </summary>
        public void TriggerSlamImpact(Vector3 position, float gameplayRadius)
        {
            if (!isActiveAndEnabled)
                return;

            if (impactRoutine != null)
                StopCoroutine(impactRoutine);

            impactRoutine = StartCoroutine(SlamImpactRoutine());

            float radius = Mathf.Max(0.65f, gameplayRadius * impactRadiusMultiplier);

            SpawnShockwave(position, radius * 1.05f, crystalHotColor, shockwaveDuration, 0f);
            SpawnShockwave(position, radius * 1.32f, hotDustColor, shockwaveDuration * 1.25f, secondaryShockwaveDelay);

            for (int i = 0; i < dustCount; i++)
                SpawnImpactDust(position, radius);

            for (int i = 0; i < rockCount; i++)
                SpawnRockDebris(position, radius);

            for (int i = 0; i < crystalShardCount; i++)
                SpawnCrystalShard(position, radius);

            // A few short-lived teal sparks preserve the creature's teal biological accent.
            for (int i = 0; i < 5; i++)
                SpawnAccentSpark(position, radius, tealAccent);
        }

        // ---------------------------------------------------------------------
        // Main enemy shader
        // ---------------------------------------------------------------------

        private void CacheSortingOrder()
        {
            baseSortingOrder = 0;

            if (renderers == null)
                return;

            foreach (SpriteRenderer sr in renderers)
            {
                if (sr != null)
                    baseSortingOrder = Mathf.Max(baseSortingOrder, sr.sortingOrder);
            }
        }

        private void SetupMaterials()
        {
            if (earthShader == null || renderers == null)
                return;

            earthMaterials = new Material[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer sr = renderers[i];
                if (sr == null)
                    continue;

                Material source = sr.sharedMaterial;
                Material mat = new Material(earthShader)
                {
                    name = $"ShardclawEarth_Runtime_{i}"
                };

                if (mat.HasProperty("_CrystalGlow"))
                    mat.SetColor("_CrystalGlow", crystalColor);

                if (mat.HasProperty("_BurrowState"))
                    mat.SetFloat("_BurrowState", 0f);

                if (source != null && source.HasProperty("_MainTex") && mat.HasProperty("_MainTex"))
                    mat.SetTexture("_MainTex", source.GetTexture("_MainTex"));

                sr.material = mat;
                earthMaterials[i] = mat;
            }
        }

        private void UpdateEnemyShader()
        {
            if (earthMaterials == null)
                return;

            float pulseSpeed = Mathf.Lerp(idlePulseSpeed, chargePulseSpeed, charge01);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);

            float idleBoost = 1f + pulse * idlePulseAmount;
            float chargeBoost = 1f + charge01 * chargeGlowBoost;
            float impactBoost = 1f + impactFlash * impactGlowBoost;

            Color glow = Color.Lerp(crystalColor, crystalHotColor, Mathf.Clamp01(charge01 * 0.92f + impactFlash));
            float intensity = idleBoost * chargeBoost * impactBoost;

            // HDR color values work beautifully with Bloom, while still degrading gracefully without it.
            Color hdrGlow = new Color(
                glow.r * intensity,
                glow.g * intensity,
                glow.b * intensity,
                1f);

            foreach (Material mat in earthMaterials)
            {
                if (mat == null)
                    continue;

                if (mat.HasProperty("_BurrowState"))
                    mat.SetFloat("_BurrowState", currentBurrow);

                if (mat.HasProperty("_CrystalGlow"))
                    mat.SetColor("_CrystalGlow", hdrGlow);

                // Optional properties: safe if you later upgrade Bao/ShardclawEarthShader.
                if (mat.HasProperty("_CrystalIntensity"))
                    mat.SetFloat("_CrystalIntensity", intensity);

                if (mat.HasProperty("_Charge"))
                    mat.SetFloat("_Charge", charge01);

                if (mat.HasProperty("_ImpactFlash"))
                    mat.SetFloat("_ImpactFlash", impactFlash);
            }
        }

        // ---------------------------------------------------------------------
        // Charge / anticipation
        // ---------------------------------------------------------------------

        private IEnumerator ChargeRoutine(float duration)
        {
            chargeActive = true;
            charge01 = 0f;

            Vector3 center = GetGroundCenter();

            for (int i = 0; i < chargeStartBurst; i++)
                SpawnChargeMote(center, true);

            float elapsed = 0f;
            float spawnTimer = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                spawnTimer += Time.deltaTime;

                charge01 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

                float interval = Mathf.Lerp(chargeMoteInterval * 1.45f, chargeMoteInterval * 0.55f, charge01);

                while (spawnTimer >= interval)
                {
                    spawnTimer -= interval;
                    SpawnChargeMote(center, false);

                    if (charge01 > 0.72f && Random.value < 0.45f)
                        SpawnChargeMote(center, false);
                }

                yield return null;
            }

            charge01 = 1f;
            chargeActive = false;
            chargeRoutine = null;
        }

        private void SpawnChargeMote(Vector3 groundCenter, bool burst)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(chargeRadius * 0.45f, chargeRadius);
            Vector3 start = groundCenter + new Vector3(Mathf.Cos(angle) * radius, Random.Range(-0.05f, 0.16f), 0f);

            Vector3 target = transform.position + new Vector3(
                Random.Range(-0.28f, 0.28f),
                chargeTargetHeight + Random.Range(-0.08f, 0.38f),
                0f);

            Color color = Random.value < 0.15f
                ? tealAccent
                : Color.Lerp(shardColor, crystalHotColor, Random.Range(0.1f, 0.75f));

            float size = burst ? Random.Range(0.055f, 0.095f) : Random.Range(0.035f, 0.075f);
            float duration = burst ? chargeMoteDuration * Random.Range(0.8f, 1.2f) : chargeMoteDuration;

            GameObject go = CreateSpriteVFX("Shardclaw_ChargeMote", softCircleSprite, color, true, baseSortingOrder + 6);
            go.transform.position = start;
            go.transform.localScale = Vector3.one * size;

            StartCoroutine(AnimateChargeMote(go, start, target, color, duration));
        }

        private IEnumerator AnimateChargeMote(GameObject go, Vector3 start, Vector3 target, Color color, float duration)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 side = Vector3.right * Random.Range(-0.55f, 0.55f);
            Vector3 control = Vector3.Lerp(start, target, 0.5f) + side + Vector3.up * Random.Range(0.15f, 0.55f);

            Vector3 initialScale = go.transform.localScale;
            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 2.25f);

                Vector3 a = Vector3.Lerp(start, control, eased);
                Vector3 b = Vector3.Lerp(control, target, eased);
                go.transform.position = Vector3.Lerp(a, b, eased);

                float scalePulse = Mathf.Lerp(1f, 0.18f, eased);
                go.transform.localScale = initialScale * scalePulse;

                if (sr != null)
                {
                    Color c = Color.Lerp(color, crystalHotColor, eased * 0.55f);
                    c.a = Mathf.SmoothStep(0f, 1f, Mathf.Min(t * 5f, (1f - t) * 4f));
                    sr.color = c;
                }

                yield return null;
            }

            if (go != null)
                Destroy(go);
        }

        // ---------------------------------------------------------------------
        // Idle motes
        // ---------------------------------------------------------------------

        private IEnumerator IdleMoteLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(idleMoteInterval.x, idleMoteInterval.y));

                if (!enableIdleMotes || chargeActive || charge01 > 0.15f || impactFlash > 0.05f)
                    continue;

                SpawnIdleMote();
            }
        }

        private void SpawnIdleMote()
        {
            Vector3 center = transform.position + (Vector3)idleMoteOffset;
            Vector3 start = center + new Vector3(
                Random.Range(-idleMoteArea.x * 0.5f, idleMoteArea.x * 0.5f),
                Random.Range(-idleMoteArea.y * 0.5f, idleMoteArea.y * 0.5f),
                0f);

            bool teal = Random.value < 0.18f;
            Color color = teal ? tealAccent : crystalColor;

            GameObject go = CreateSpriteVFX("Shardclaw_IdleMote", softCircleSprite, color, true, baseSortingOrder + 4);
            go.transform.position = start;

            float size = Random.Range(0.018f, 0.045f);
            go.transform.localScale = Vector3.one * size;

            StartCoroutine(AnimateIdleMote(go, color));
        }

        private IEnumerator AnimateIdleMote(GameObject go, Color color)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 velocity = new Vector3(Random.Range(-0.07f, 0.07f), Random.Range(0.08f, 0.2f), 0f);
            float duration = Random.Range(0.55f, 1.15f);
            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                go.transform.position += velocity * Time.deltaTime;
                go.transform.Rotate(0f, 0f, 45f * Time.deltaTime);

                if (sr != null)
                {
                    Color c = color;
                    c.a = Mathf.Sin(t * Mathf.PI) * 0.75f;
                    sr.color = c;
                }

                yield return null;
            }

            if (go != null)
                Destroy(go);
        }

        // ---------------------------------------------------------------------
        // Impact
        // ---------------------------------------------------------------------

        private IEnumerator SlamImpactRoutine()
        {
            chargeActive = false;

            if (chargeRoutine != null)
            {
                StopCoroutine(chargeRoutine);
                chargeRoutine = null;
            }

            impactFlash = 1f;

            // Very sharp white-hot flash, followed by a slightly longer cooling tail.
            float elapsed = 0f;
            const float duration = 0.30f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                impactFlash = Mathf.Pow(1f - t, 3.2f);
                charge01 = Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0f, 1f, t));

                yield return null;
            }

            impactFlash = 0f;
            charge01 = 0f;
            impactRoutine = null;
        }

        private void SpawnImpactDust(Vector3 center, float radius)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Abs(Mathf.Sin(angle)) * 0.45f + 0.16f, 0f).normalized;

            Vector3 start = center + new Vector3(Random.Range(-0.12f, 0.12f), Random.Range(-0.03f, 0.08f), 0f);
            Color color = Color.Lerp(earthDustColor, hotDustColor, Random.Range(0f, 0.28f));

            GameObject go = CreateSpriteVFX("Shardclaw_ImpactDust", softCircleSprite, color, false, baseSortingOrder + 2);
            go.transform.position = start;

            float size = Random.Range(0.07f, 0.19f);
            go.transform.localScale = Vector3.one * size;

            Vector3 velocity = dir * Random.Range(radius * 1.25f, radius * 2.6f);
            velocity.y += Random.Range(0.35f, 1.25f);

            StartCoroutine(AnimateDust(go, color, velocity, Random.Range(0.38f, 0.72f)));
        }

        private IEnumerator AnimateDust(GameObject go, Color startColor, Vector3 velocity, float duration)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 initialScale = go.transform.localScale;
            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                velocity *= Mathf.Exp(-2.7f * Time.deltaTime);
                velocity.y += 0.25f * Time.deltaTime;

                go.transform.position += velocity * Time.deltaTime;
                go.transform.localScale = initialScale * Mathf.Lerp(0.7f, 2.8f, t);

                if (sr != null)
                {
                    Color c = Color.Lerp(startColor, new Color(0.22f, 0.13f, 0.09f, 0f), t);
                    c.a *= (1f - t);
                    sr.color = c;
                }

                yield return null;
            }

            if (go != null)
                Destroy(go);
        }

        private void SpawnRockDebris(Vector3 center, float radius)
        {
            float angle = Random.Range(15f, 165f) * Mathf.Deg2Rad;
            if (Random.value < 0.5f)
                angle = Mathf.PI - angle;

            Vector3 velocity = new Vector3(
                Mathf.Cos(angle) * Random.Range(radius * 0.7f, radius * 1.45f),
                Random.Range(1.3f, 2.65f),
                0f);

            Color c = Color.Lerp(obsidianRockColor, earthDustColor, Random.Range(0.05f, 0.35f));

            GameObject go = CreateSpriteVFX("Shardclaw_RockDebris", rockSprite, c, false, baseSortingOrder + 5);
            go.transform.position = center + new Vector3(Random.Range(-0.18f, 0.18f), Random.Range(0f, 0.08f), 0f);
            go.transform.localScale = Vector3.one * Random.Range(0.06f, 0.14f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            StartCoroutine(AnimateBallisticDebris(go, velocity, Random.Range(0.55f, 0.9f), 5.8f, false));
        }

        private void SpawnCrystalShard(Vector3 center, float radius)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            Vector3 velocity = new Vector3(
                side * Random.Range(radius * 0.85f, radius * 1.85f),
                Random.Range(1.35f, 3.15f),
                0f);

            Color c = Color.Lerp(shardColor, crystalHotColor, Random.Range(0f, 0.55f));

            GameObject go = CreateSpriteVFX("Shardclaw_CrystalShard", shardSprite, c, true, baseSortingOrder + 8);
            go.transform.position = center + new Vector3(Random.Range(-0.22f, 0.22f), Random.Range(0.03f, 0.18f), 0f);

            float scale = Random.Range(0.075f, 0.15f);
            go.transform.localScale = new Vector3(scale * Random.Range(0.65f, 1.1f), scale * Random.Range(1.3f, 2.0f), 1f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            StartCoroutine(AnimateBallisticDebris(go, velocity, Random.Range(0.42f, 0.7f), 5.2f, true));
        }

        private void SpawnAccentSpark(Vector3 center, float radius, Color color)
        {
            Vector2 dir2 = Random.insideUnitCircle.normalized;
            Vector3 velocity = new Vector3(dir2.x, Mathf.Abs(dir2.y) + 0.25f, 0f) * Random.Range(radius * 0.9f, radius * 1.6f);

            GameObject go = CreateSpriteVFX("Shardclaw_TealSpark", softCircleSprite, color, true, baseSortingOrder + 9);
            go.transform.position = center + (Vector3)(Random.insideUnitCircle * 0.12f);
            go.transform.localScale = Vector3.one * Random.Range(0.025f, 0.055f);

            StartCoroutine(AnimateSpark(go, velocity, color, Random.Range(0.2f, 0.34f)));
        }

        private IEnumerator AnimateSpark(GameObject go, Vector3 velocity, Color color, float duration)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 startScale = go.transform.localScale;
            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                velocity *= Mathf.Exp(-3.5f * Time.deltaTime);
                go.transform.position += velocity * Time.deltaTime;
                go.transform.localScale = startScale * Mathf.Lerp(1.25f, 0f, t);

                if (sr != null)
                {
                    Color c = color;
                    c.a = 1f - t;
                    sr.color = c;
                }

                yield return null;
            }

            if (go != null)
                Destroy(go);
        }

        private IEnumerator AnimateBallisticDebris(
            GameObject go,
            Vector3 velocity,
            float duration,
            float gravity,
            bool emissive)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 initialScale = go.transform.localScale;
            float rotationSpeed = Random.Range(-720f, 720f);
            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                velocity.y -= gravity * Time.deltaTime;
                go.transform.position += velocity * Time.deltaTime;
                go.transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);

                float shrink = Mathf.Lerp(1f, emissive ? 0.2f : 0.55f, t);
                go.transform.localScale = initialScale * shrink;

                if (sr != null && t > 0.58f)
                {
                    Color c = sr.color;
                    c.a = 1f - Mathf.InverseLerp(0.58f, 1f, t);
                    sr.color = c;
                }

                yield return null;
            }

            if (go != null)
                Destroy(go);
        }

        private void SpawnShockwave(Vector3 center, float radius, Color color, float duration, float delay)
        {
            StartCoroutine(ShockwaveRoutine(center, radius, color, duration, delay));
        }

        private IEnumerator ShockwaveRoutine(Vector3 center, float radius, Color color, float duration, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            GameObject go = CreateSpriteVFX("Shardclaw_Shockwave", ringSprite, color, true, baseSortingOrder + 3);
            go.transform.position = center;

            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 startScale = Vector3.one * 0.12f;
            Vector3 endScale = Vector3.one * (radius * 2f);

            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float eased = 1f - Mathf.Pow(1f - t, 3f);
                go.transform.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);

                if (sr != null)
                {
                    Color c = color;
                    c.a = Mathf.Pow(1f - t, 1.65f);
                    sr.color = c;
                }

                yield return null;
            }

            if (go != null)
                Destroy(go);
        }

        // ---------------------------------------------------------------------
        // Runtime VFX asset generation
        // ---------------------------------------------------------------------

        private void BuildRuntimeVFXAssets()
        {
            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader != null)
            {
                spriteMaterial = new Material(spriteShader)
                {
                    name = "Shardclaw_SpriteVFX_Runtime"
                };
                runtimeAssets.Add(spriteMaterial);
            }

            Shader glowShader = additiveShader != null ? additiveShader : spriteShader;
            if (glowShader != null)
            {
                additiveMaterial = new Material(glowShader)
                {
                    name = "Shardclaw_AdditiveVFX_Runtime"
                };
                runtimeAssets.Add(additiveMaterial);
            }

            softCircleSprite = CreateSoftCircleSprite(48);
            ringSprite = CreateRingSprite(96);
            shardSprite = CreateShardSprite(48, 72);
            rockSprite = CreateRockSprite(48);

            runtimeAssets.Add(softCircleSprite);
            runtimeAssets.Add(ringSprite);
            runtimeAssets.Add(shardSprite);
            runtimeAssets.Add(rockSprite);

            runtimeAssets.Add(softCircleSprite.texture);
            runtimeAssets.Add(ringSprite.texture);
            runtimeAssets.Add(shardSprite.texture);
            runtimeAssets.Add(rockSprite.texture);
        }

        private GameObject CreateSpriteVFX(
            string objectName,
            Sprite sprite,
            Color color,
            bool additive,
            int sortingOrder)
        {
            GameObject go = new GameObject(objectName);

            if (vfxContainer != null)
                go.transform.SetParent(vfxContainer, true);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;

            Material selected = additive ? additiveMaterial : spriteMaterial;
            if (selected != null)
                sr.sharedMaterial = selected;

            return go;
        }

        private Vector3 GetGroundCenter()
        {
            if (shardclaw != null && shardclaw.VfxSpawnPoint != null)
                return shardclaw.VfxSpawnPoint.position;

            return transform.position + new Vector3(0f, -0.4f, 0f);
        }

        private static Sprite CreateSoftCircleSprite(int size)
        {
            Texture2D tex = NewTexture(size, size);
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxRadius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxRadius;
                    float alpha = Mathf.Clamp01(1f - d);
                    alpha = alpha * alpha * (3f - 2f * alpha);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateRingSprite(int size)
        {
            Texture2D tex = NewTexture(size, size);
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.38f;
            float thickness = size * 0.055f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    float edgeDistance = Mathf.Abs(d - radius);
                    float alpha = Mathf.Clamp01(1f - edgeDistance / thickness);
                    alpha = alpha * alpha;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateShardSprite(int width, int height)
        {
            Texture2D tex = NewTexture(width, height);

            for (int y = 0; y < height; y++)
            {
                float ny = (y / (float)(height - 1)) * 2f - 1f;
                float halfWidth = Mathf.Lerp(0f, 0.95f, 1f - Mathf.Abs(ny));

                for (int x = 0; x < width; x++)
                {
                    float nx = Mathf.Abs((x / (float)(width - 1)) * 2f - 1f);
                    float body = Mathf.Clamp01((halfWidth - nx) * 8f);

                    float centerGlow = Mathf.Clamp01(1f - nx * 1.45f);
                    float alpha = body;

                    tex.SetPixel(x, y, new Color(
                        Mathf.Lerp(0.7f, 1f, centerGlow),
                        Mathf.Lerp(0.7f, 1f, centerGlow),
                        Mathf.Lerp(0.7f, 1f, centerGlow),
                        alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.42f), height);
        }

        private static Sprite CreateRockSprite(int size)
        {
            Texture2D tex = NewTexture(size, size);
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.34f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 delta = new Vector2(x, y) - center;
                    float angle = Mathf.Atan2(delta.y, delta.x);
                    float wobble =
                        Mathf.Sin(angle * 5f) * 0.08f +
                        Mathf.Sin(angle * 9f + 0.7f) * 0.045f;

                    float localRadius = radius * (1f + wobble);
                    float d = delta.magnitude;
                    float alpha = Mathf.Clamp01((localRadius - d) * 0.75f);

                    float shade = Mathf.Lerp(0.72f, 1f, Mathf.Clamp01((delta.y / radius + 1f) * 0.5f));
                    tex.SetPixel(x, y, new Color(shade, shade, shade, alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Texture2D NewTexture(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Shardclaw_ProceduralVFX"
            };

            Color clear = new Color(1f, 1f, 1f, 0f);
            Color[] pixels = new Color[width * height];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;

            tex.SetPixels(pixels);
            return tex;
        }
    }
}
