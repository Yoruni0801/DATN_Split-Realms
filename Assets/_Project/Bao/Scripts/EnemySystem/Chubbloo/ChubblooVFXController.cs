using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Bao.Enemies
{
    [RequireComponent(typeof(ChubblooEnemy))]
    public class ChubblooVFXController : MonoBehaviour
    {
        [Header("Shader Setup")]
        [SerializeField] private Shader livingIceShader;
        [SerializeField] private Color icyDepthColor = new Color(0.05f, 0.16f, 0.42f, 1f);
        [SerializeField] private Color glacierColor = new Color(0.12f, 0.74f, 1.0f, 1f);
        [SerializeField] private Color frostColor = new Color(0.43f, 0.97f, 1.0f, 1f);
        [SerializeField] private Color snowColor = new Color(0.93f, 1f, 1f, 1f);
        [SerializeField] private float transitionSpeed = 7f;

        [Header("Body Treatment")]
        [SerializeField] private bool applyLivingIceBodyShader;

        [Header("Idle Ambient")]
        [SerializeField] private Vector2 idleMoteInterval = new Vector2(0.18f, 0.38f);
        [SerializeField] private Vector2 idleMoteArea = new Vector2(0.95f, 0.95f);
        [SerializeField] private float breathInterval = 1.35f;

        [Header("Attack Charge")]
        [SerializeField] private int chargeStartBurst = 6;
        [SerializeField] private float chargeMoteInterval = 0.035f;
        [SerializeField] private float chargeMoteRadius = 0.92f;
        [SerializeField] private float chargeCoreMaxSize = 0.28f;

        [Header("Shot Burst")]
        [SerializeField] private int muzzleShardCount = 8;
        [SerializeField] private int muzzleMistCount = 9;
        [SerializeField] private float recoilKick = 0.04f;

        private ChubblooEnemy chubbloo;
        private SpriteRenderer[] renderers;
        private Material[] iceMaterials;

        private Material spriteMaterial;
        private Material additiveMaterial;
        private Sprite softCircleSprite;
        private Sprite shardSprite;
        private Sprite ringSprite;
        private Sprite sparkleSprite;
        private Sprite mistSprite;
        private readonly List<Object> runtimeAssets = new List<Object>();

        private int baseSortingOrder;
        private Transform vfxContainer;
        private Transform chargeRoot;
        private SpriteRenderer chargeCore;
        private SpriteRenderer chargeHalo;

        private Coroutine idleMotesRoutine;
        private Coroutine breathRoutine;
        private Coroutine chargeRoutine;
        private Coroutine recoilRoutine;

        private float charge01;
        private float shotFlash;
        private float currentAttackLevel;
        private bool charging;

        private void Awake()
        {
            chubbloo = GetComponent<ChubblooEnemy>();
            renderers = chubbloo.GetVisualSpriteRenderers();

            if (livingIceShader == null)
                livingIceShader = Shader.Find("Bao/ChubblooLivingIceShader");

            vfxContainer = VFXContainerUtility.GetContainer();

            CacheSortingOrder();
            BuildRuntimeAssets();

            if (applyLivingIceBodyShader)
            {
                SetupMaterials();
            }

            CreateChargeCore();
        }

        private void OnEnable()
        {
            if (idleMotesRoutine == null)
                idleMotesRoutine = StartCoroutine(IdleMotesLoop());

            if (breathRoutine == null)
                breathRoutine = StartCoroutine(BreathLoop());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            idleMotesRoutine = null;
            breathRoutine = null;
            chargeRoutine = null;
            recoilRoutine = null;

            charging = false;
            charge01 = 0f;
            shotFlash = 0f;

            if (chargeRoot != null)
                chargeRoot.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (iceMaterials != null)
            {
                foreach (Material mat in iceMaterials)
                    if (mat != null)
                        Destroy(mat);
            }

            foreach (Object asset in runtimeAssets)
            {
                if (asset != null)
                    Destroy(asset);
            }
        }

        private void Update()
        {
            bool isAttacking = chubbloo.StateMachine?.CurrentState is ChubblooAttackState;

            float targetAttackLevel = isAttacking ? 1f : 0f;
            currentAttackLevel = Mathf.Lerp(
                currentAttackLevel,
                targetAttackLevel,
                1f - Mathf.Exp(-transitionSpeed * Time.deltaTime));

            if (!charging)
                charge01 = Mathf.MoveTowards(charge01, 0f, Time.deltaTime * 5f);

            if (shotFlash > 0f)
                shotFlash = Mathf.MoveTowards(shotFlash, 0f, Time.deltaTime * 6.5f);

            UpdateBodyShader();
            UpdateChargeCore();
        }

        // ---------------------------------------------------------------------
        // Public hooks
        // ---------------------------------------------------------------------

        public void TriggerChargeStart(float duration)
        {
            if (chargeRoutine != null)
                StopCoroutine(chargeRoutine);

            chargeRoutine = StartCoroutine(ChargeRoutine(Mathf.Max(0.08f, duration)));
        }

        public void TriggerChargeEnd()
        {
            charging = false;
        }

        public void TriggerShotFired(Vector3 worldPos, float direction)
        {
            charging = false;
            charge01 = 0f;
            shotFlash = 1f;

            if (chargeRoot != null)
                chargeRoot.gameObject.SetActive(false);

            SpawnMuzzleBurst(worldPos, direction);

            if (recoilRoutine != null)
                StopCoroutine(recoilRoutine);

            recoilRoutine = StartCoroutine(ShotRecoilRoutine(direction));
        }

        public void TriggerDeathBurst()
        {
            Vector3 center = transform.position + new Vector3(0f, 0.05f, 0f);
            SpawnRing(center, 0.82f, frostColor, 0.22f, true, baseSortingOrder + 6);
            SpawnRing(center, 1.02f, glacierColor, 0.28f, false, baseSortingOrder + 5);

            for (int i = 0; i < 14; i++)
                SpawnDeathShard(center);

            for (int i = 0; i < 12; i++)
                SpawnDeathMist(center);
        }

        // ---------------------------------------------------------------------
        // Setup
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

        private void BuildRuntimeAssets()
        {
            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader != null)
            {
                spriteMaterial = new Material(spriteShader) { name = "Chubbloo_SpriteVFX_Runtime" };
                additiveMaterial = new Material(spriteShader) { name = "Chubbloo_AdditiveVFX_Runtime" };
                runtimeAssets.Add(spriteMaterial);
                runtimeAssets.Add(additiveMaterial);
            }

            softCircleSprite = CreateSoftCircleSprite(64);
            shardSprite = CreateShardSprite(48, 72);
            ringSprite = CreateRingSprite(96);
            sparkleSprite = CreateSparkleSprite(64);
            mistSprite = CreateMistSprite(96);

            runtimeAssets.Add(softCircleSprite);
            runtimeAssets.Add(shardSprite);
            runtimeAssets.Add(ringSprite);
            runtimeAssets.Add(sparkleSprite);
            runtimeAssets.Add(mistSprite);

            runtimeAssets.Add(softCircleSprite.texture);
            runtimeAssets.Add(shardSprite.texture);
            runtimeAssets.Add(ringSprite.texture);
            runtimeAssets.Add(sparkleSprite.texture);
            runtimeAssets.Add(mistSprite.texture);
        }

        private void SetupMaterials()
        {
            if (livingIceShader == null || renderers == null)
                return;

            iceMaterials = new Material[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer sr = renderers[i];
                if (sr == null)
                    continue;

                Material sourceMaterial = sr.sharedMaterial;
                Material mat = sourceMaterial != null ? new Material(sourceMaterial) : new Material(livingIceShader);

                if (mat.shader != livingIceShader)
                {
                    Material upgraded = new Material(livingIceShader);
                    if (sourceMaterial != null && sourceMaterial.HasProperty("_MainTex") && upgraded.HasProperty("_MainTex"))
                        upgraded.SetTexture("_MainTex", sourceMaterial.GetTexture("_MainTex"));
                    mat = upgraded;
                }

                if (mat.HasProperty("_IceDepthColor")) mat.SetColor("_IceDepthColor", icyDepthColor);
                if (mat.HasProperty("_GlacierColor")) mat.SetColor("_GlacierColor", glacierColor);
                if (mat.HasProperty("_FrostColor")) mat.SetColor("_FrostColor", frostColor);
                if (mat.HasProperty("_SnowColor")) mat.SetColor("_SnowColor", snowColor);
                if (mat.HasProperty("_Charge")) mat.SetFloat("_Charge", 0f);
                if (mat.HasProperty("_AttackLevel")) mat.SetFloat("_AttackLevel", 0f);
                if (mat.HasProperty("_ShotFlash")) mat.SetFloat("_ShotFlash", 0f);

                sr.material = mat;
                iceMaterials[i] = mat;
            }
        }

        private void CreateChargeCore()
        {
            if (chubbloo == null)
                return;

            chargeRoot = new GameObject("Chubbloo_ChargeCore").transform;
            chargeRoot.SetParent(transform, false);
            chargeRoot.gameObject.SetActive(false);

            chargeHalo = CreateSpriteRenderer("Charge Halo", chargeRoot, softCircleSprite, true, baseSortingOrder + 3);
            chargeCore = CreateSpriteRenderer("Charge Core", chargeRoot, softCircleSprite, true, baseSortingOrder + 4);

            chargeHalo.color = new Color(glacierColor.r, glacierColor.g, glacierColor.b, 0f);
            chargeCore.color = new Color(snowColor.r, snowColor.g, snowColor.b, 0f);
        }

        // ---------------------------------------------------------------------
        // Body shader
        // ---------------------------------------------------------------------

        private void UpdateBodyShader()
        {
            if (iceMaterials == null)
                return;

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(1.9f, 9.5f, currentAttackLevel + charge01 * 0.55f));
            float pulseLevel = Mathf.Lerp(0.12f, 0.36f, pulse);

            foreach (Material mat in iceMaterials)
            {
                if (mat == null)
                    continue;

                if (mat.HasProperty("_Charge")) mat.SetFloat("_Charge", charge01);
                if (mat.HasProperty("_AttackLevel")) mat.SetFloat("_AttackLevel", currentAttackLevel + pulseLevel);
                if (mat.HasProperty("_ShotFlash")) mat.SetFloat("_ShotFlash", shotFlash);
            }
        }

        private void UpdateChargeCore()
        {
            if (chargeRoot == null || chargeCore == null || chargeHalo == null)
                return;

            if (!charging && charge01 <= 0.01f)
            {
                chargeRoot.gameObject.SetActive(false);
                return;
            }

            Transform anchor = chubbloo.ShootPoint != null ? chubbloo.ShootPoint : transform;
            chargeRoot.position = anchor.position;
            chargeRoot.gameObject.SetActive(true);

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(3f, 14f, charge01));
            float coreSize = Mathf.Lerp(0.05f, chargeCoreMaxSize, charge01) * Mathf.Lerp(0.92f, 1.08f, pulse);
            float haloSize = coreSize * Mathf.Lerp(1.8f, 2.7f, charge01);

            chargeCore.transform.localScale = Vector3.one * coreSize;
            chargeHalo.transform.localScale = Vector3.one * haloSize;

            chargeCore.color = new Color(snowColor.r, snowColor.g, snowColor.b, Mathf.Lerp(0f, 0.95f, charge01));
            chargeHalo.color = new Color(frostColor.r, frostColor.g, frostColor.b, Mathf.Lerp(0f, 0.42f, charge01));
        }

        // ---------------------------------------------------------------------
        // Ambient loops
        // ---------------------------------------------------------------------

        private IEnumerator IdleMotesLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(idleMoteInterval.x, idleMoteInterval.y));

                if (charging || shotFlash > 0.05f || chubbloo == null || chubbloo.IsDead)
                    continue;

                SpawnIdleMote();
            }
        }

        private IEnumerator BreathLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(breathInterval + Random.Range(-0.2f, 0.24f));

                if (charging || chubbloo == null || chubbloo.IsDead)
                    continue;

                SpawnColdBreath();
            }
        }

        private void SpawnIdleMote()
        {
            Vector3 center = transform.position + new Vector3(0f, 0.32f, 0f);
            Vector3 start = center + new Vector3(
                Random.Range(-idleMoteArea.x * 0.5f, idleMoteArea.x * 0.5f),
                Random.Range(-idleMoteArea.y * 0.5f, idleMoteArea.y * 0.5f),
                0f);

            GameObject go = CreateSpriteVFX("Chubbloo_IdleMote", Random.value < 0.3f ? sparkleSprite : softCircleSprite,
                Random.value < 0.35f ? frostColor : snowColor, false, baseSortingOrder + 2);

            go.transform.position = start;
            go.transform.localScale = Vector3.one * Random.Range(0.02f, 0.055f);

            StartCoroutine(AnimateAmbientMote(go, go.GetComponent<SpriteRenderer>().color, Random.Range(0.55f, 1.05f)));
        }

        private IEnumerator AnimateAmbientMote(GameObject go, Color color, float duration)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 velocity = new Vector3(Random.Range(-0.06f, 0.06f), Random.Range(0.05f, 0.16f), 0f);
            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                go.transform.position += velocity * Time.deltaTime;
                go.transform.Rotate(0f, 0f, 36f * Time.deltaTime);

                if (sr != null)
                {
                    Color c = color;
                    c.a = Mathf.Sin(t * Mathf.PI) * 0.75f;
                    sr.color = c;
                }

                yield return null;
            }

            if (go != null) Destroy(go);
        }

        private void SpawnColdBreath()
        {
            Transform anchor = chubbloo.ShootPoint != null ? chubbloo.ShootPoint : transform;
            Vector3 start = anchor.position + new Vector3(-chubbloo.FacingDirection * 0.03f, -0.02f, 0f);

            GameObject go = CreateSpriteVFX("Chubbloo_Breath", mistSprite, glacierColor, false, baseSortingOrder + 1);
            go.transform.position = start;
            go.transform.localScale = new Vector3(0.12f, 0.08f, 1f);

            StartCoroutine(AnimateBreath(go, start, chubbloo.FacingDirection));
        }

        private IEnumerator AnimateBreath(GameObject go, Vector3 start, float facingDir)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            float elapsed = 0f;
            const float duration = 0.48f;
            Vector3 drift = new Vector3(facingDir * 0.22f, 0.08f, 0f);

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                go.transform.position = start + drift * t + new Vector3(0f, Mathf.Sin(t * Mathf.PI) * 0.04f, 0f);
                go.transform.localScale = Vector3.Lerp(new Vector3(0.12f, 0.08f, 1f), new Vector3(0.34f, 0.20f, 1f), t);

                if (sr != null)
                {
                    Color c = glacierColor;
                    c.a = Mathf.Lerp(0.18f, 0f, t);
                    sr.color = c;
                }

                yield return null;
            }

            if (go != null) Destroy(go);
        }

        // ---------------------------------------------------------------------
        // Attack charge
        // ---------------------------------------------------------------------

        private IEnumerator ChargeRoutine(float duration)
        {
            charging = true;
            charge01 = 0f;

            Transform anchor = chubbloo.ShootPoint != null ? chubbloo.ShootPoint : transform;
            Vector3 center = anchor.position;

            for (int i = 0; i < chargeStartBurst; i++)
                SpawnChargeMote(center, true);

            float elapsed = 0f;
            float spawnTimer = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                spawnTimer += Time.deltaTime;
                charge01 = Mathf.Clamp01(elapsed / duration);

                float interval = Mathf.Lerp(chargeMoteInterval * 1.45f, chargeMoteInterval * 0.55f, charge01);

                while (spawnTimer >= interval)
                {
                    spawnTimer -= interval;
                    SpawnChargeMote(center, false);

                    if (charge01 > 0.58f && Random.value < 0.34f)
                        SpawnChargeShard(center);
                }

                yield return null;
            }

            charge01 = 1f;
            charging = false;
            chargeRoutine = null;
        }

        private void SpawnChargeMote(Vector3 center, bool burst)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(chargeMoteRadius * 0.48f, chargeMoteRadius);
            Vector3 start = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.62f, 0f);
            Vector3 target = center + (Vector3)(Random.insideUnitCircle * 0.04f);

            Color color = Random.value < 0.4f ? glacierColor : frostColor;
            GameObject go = CreateSpriteVFX("Chubbloo_ChargeMote", softCircleSprite, color, false, baseSortingOrder + 2);
            go.transform.position = start;
            go.transform.localScale = Vector3.one * (burst ? Random.Range(0.05f, 0.09f) : Random.Range(0.03f, 0.065f));

            StartCoroutine(AnimateChargeMote(go, start, target, color, burst ? 0.32f : 0.26f));
        }

        private IEnumerator AnimateChargeMote(GameObject go, Vector3 start, Vector3 target, Color color, float duration)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 initialScale = go.transform.localScale;
            Vector3 control = Vector3.Lerp(start, target, 0.56f) + new Vector3(
                Random.Range(-0.18f, 0.18f),
                Random.Range(-0.08f, 0.18f),
                0f);

            float elapsed = 0f;
            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 2f);

                Vector3 a = Vector3.Lerp(start, control, eased);
                Vector3 b = Vector3.Lerp(control, target, eased);
                go.transform.position = Vector3.Lerp(a, b, eased);
                go.transform.localScale = initialScale * Mathf.Lerp(1.1f, 0.1f, t);

                if (sr != null)
                {
                    Color c = Color.Lerp(color, snowColor, t * 0.7f);
                    c.a = Mathf.Sin(t * Mathf.PI) * 0.8f;
                    sr.color = c;
                }

                yield return null;
            }

            if (go != null) Destroy(go);
        }

        private void SpawnChargeShard(Vector3 center)
        {
            Vector3 start = center + new Vector3(Random.Range(-0.38f, 0.38f), Random.Range(-0.2f, 0.24f), 0f);
            GameObject go = CreateSpriteVFX("Chubbloo_ChargeShard", shardSprite, frostColor, true, baseSortingOrder + 4);
            go.transform.position = start;
            go.transform.localScale = new Vector3(Random.Range(0.05f, 0.08f), Random.Range(0.1f, 0.18f), 1f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            StartCoroutine(AnimateChargeShard(go, center));
        }

        private IEnumerator AnimateChargeShard(GameObject go, Vector3 target)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 start = go.transform.position;
            float duration = Random.Range(0.18f, 0.28f);
            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                go.transform.position = Vector3.Lerp(start, target, t);
                go.transform.Rotate(0f, 0f, 320f * Time.deltaTime);
                go.transform.localScale = Vector3.Lerp(go.transform.localScale, Vector3.zero, t);

                if (sr != null)
                {
                    Color c = frostColor;
                    c.a = Mathf.Lerp(0.65f, 0f, t);
                    sr.color = c;
                }

                yield return null;
            }

            if (go != null) Destroy(go);
        }

        // ---------------------------------------------------------------------
        // Shot burst
        // ---------------------------------------------------------------------

        private void SpawnMuzzleBurst(Vector3 center, float direction)
        {
            SpawnRing(center, 0.64f, frostColor, 0.18f, true, baseSortingOrder + 5);
            SpawnRing(center, 0.82f, glacierColor, 0.22f, false, baseSortingOrder + 4);

            for (int i = 0; i < muzzleMistCount; i++)
                SpawnMuzzleMist(center, direction);

            for (int i = 0; i < muzzleShardCount; i++)
                SpawnMuzzleShard(center, direction);

            for (int i = 0; i < 5; i++)
                SpawnMuzzleSpark(center, direction);
        }

        private IEnumerator ShotRecoilRoutine(float direction)
        {
            Vector3 startPos = transform.localPosition;
            Vector3 recoilOffset = new Vector3(-direction * recoilKick, 0f, 0f);

            float elapsed = 0f;
            const float duration = 0.16f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.localPosition = Vector3.Lerp(startPos + recoilOffset, startPos, t);
                yield return null;
            }

            transform.localPosition = startPos;
            recoilRoutine = null;
        }

        private void SpawnMuzzleMist(Vector3 center, float direction)
        {
            GameObject go = CreateSpriteVFX("Chubbloo_MuzzleMist", mistSprite, glacierColor, false, baseSortingOrder + 3);
            go.transform.position = center;
            go.transform.localScale = new Vector3(Random.Range(0.08f, 0.18f), Random.Range(0.06f, 0.12f), 1f);

            Vector3 velocity = new Vector3(
                direction * Random.Range(0.8f, 2.4f),
                Random.Range(-0.28f, 0.32f),
                0f);

            StartCoroutine(AnimateBurstMist(go, velocity, glacierColor, Random.Range(0.26f, 0.42f)));
        }

        private void SpawnMuzzleShard(Vector3 center, float direction)
        {
            GameObject go = CreateSpriteVFX("Chubbloo_MuzzleShard", shardSprite, frostColor, true, baseSortingOrder + 6);
            go.transform.position = center + new Vector3(Random.Range(-0.03f, 0.03f), Random.Range(-0.02f, 0.02f), 0f);
            go.transform.localScale = new Vector3(Random.Range(0.04f, 0.075f), Random.Range(0.09f, 0.18f), 1f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-30f, 30f) * direction);

            Vector3 velocity = new Vector3(
                direction * Random.Range(1.6f, 3.4f),
                Random.Range(-0.85f, 0.85f),
                0f);

            StartCoroutine(AnimateBurstShard(go, velocity, Random.Range(0.22f, 0.4f)));
        }

        private void SpawnMuzzleSpark(Vector3 center, float direction)
        {
            GameObject go = CreateSpriteVFX("Chubbloo_MuzzleSpark", sparkleSprite, snowColor, true, baseSortingOrder + 7);
            go.transform.position = center;
            go.transform.localScale = Vector3.one * Random.Range(0.03f, 0.06f);

            Vector3 velocity = new Vector3(
                direction * Random.Range(1.8f, 3.8f),
                Random.Range(-0.65f, 0.65f),
                0f);

            StartCoroutine(AnimateBurstSpark(go, velocity, Random.Range(0.12f, 0.24f)));
        }

        private void SpawnDeathShard(Vector3 center)
        {
            GameObject go = CreateSpriteVFX("Chubbloo_DeathShard", shardSprite, Random.value < 0.5f ? frostColor : snowColor, true, baseSortingOrder + 5);
            go.transform.position = center + (Vector3)(Random.insideUnitCircle * 0.06f);
            go.transform.localScale = new Vector3(Random.Range(0.05f, 0.09f), Random.Range(0.11f, 0.21f), 1f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            Vector3 velocity = Random.insideUnitCircle.normalized;
            velocity.y = Mathf.Abs(velocity.y) + 0.18f;
            StartCoroutine(AnimateBurstShard(go, velocity * Random.Range(0.85f, 1.8f), Random.Range(0.3f, 0.55f)));
        }

        private void SpawnDeathMist(Vector3 center)
        {
            GameObject go = CreateSpriteVFX("Chubbloo_DeathMist", mistSprite, glacierColor, false, baseSortingOrder + 2);
            go.transform.position = center;
            go.transform.localScale = new Vector3(Random.Range(0.08f, 0.18f), Random.Range(0.06f, 0.12f), 1f);

            Vector3 velocity = (Vector3)Random.insideUnitCircle * Random.Range(0.4f, 1.0f);
            velocity.y = Mathf.Abs(velocity.y) + 0.08f;
            StartCoroutine(AnimateBurstMist(go, velocity, glacierColor, Random.Range(0.32f, 0.52f)));
        }

        private void SpawnRing(Vector3 center, float radius, Color color, float duration, bool bright, int sortingOrder)
        {
            GameObject go = CreateSpriteVFX("Chubbloo_Ring", ringSprite, color, bright, sortingOrder);
            go.transform.position = center;
            go.transform.localScale = Vector3.one * 0.1f;
            StartCoroutine(AnimateRing(go, color, radius, duration));
        }

        private IEnumerator AnimateRing(GameObject go, Color color, float radius, float duration)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 startScale = Vector3.one * 0.1f;
            Vector3 endScale = Vector3.one * radius * 2f;
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
                    c.a = Mathf.Pow(1f - t, 1.55f);
                    sr.color = c;
                }

                yield return null;
            }

            if (go != null) Destroy(go);
        }

        private IEnumerator AnimateBurstMist(GameObject go, Vector3 velocity, Color color, float duration)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 startScale = go.transform.localScale;
            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                velocity *= Mathf.Exp(-3.2f * Time.deltaTime);
                go.transform.position += velocity * Time.deltaTime;
                go.transform.localScale = startScale * Mathf.Lerp(1f, 2.6f, t);

                if (sr != null)
                {
                    Color c = color;
                    c.a = Mathf.Lerp(0.28f, 0f, t);
                    sr.color = c;
                }

                yield return null;
            }

            if (go != null) Destroy(go);
        }

        private IEnumerator AnimateBurstShard(GameObject go, Vector3 velocity, float duration)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 startScale = go.transform.localScale;
            float rot = Random.Range(-720f, 720f);
            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                velocity.y -= 2.8f * Time.deltaTime;
                go.transform.position += velocity * Time.deltaTime;
                go.transform.Rotate(0f, 0f, rot * Time.deltaTime);
                go.transform.localScale = startScale * Mathf.Lerp(1f, 0.36f, t);

                if (sr != null && t > 0.45f)
                {
                    Color c = sr.color;
                    c.a = 1f - Mathf.InverseLerp(0.45f, 1f, t);
                    sr.color = c;
                }

                yield return null;
            }

            if (go != null) Destroy(go);
        }

        private IEnumerator AnimateBurstSpark(GameObject go, Vector3 velocity, float duration)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 startScale = go.transform.localScale;
            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                velocity *= Mathf.Exp(-4.2f * Time.deltaTime);
                go.transform.position += velocity * Time.deltaTime;
                go.transform.localScale = startScale * Mathf.Lerp(1.2f, 0f, t);

                if (sr != null)
                {
                    Color c = snowColor;
                    c.a = 1f - t;
                    sr.color = c;
                }

                yield return null;
            }

            if (go != null) Destroy(go);
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private GameObject CreateSpriteVFX(string objectName, Sprite sprite, Color color, bool bright, int sortingOrder)
        {
            GameObject go = new GameObject(objectName);

            if (vfxContainer != null)
                go.transform.SetParent(vfxContainer, true);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            sr.sharedMaterial = bright ? additiveMaterial : spriteMaterial;

            return go;
        }

        private SpriteRenderer CreateSpriteRenderer(string objectName, Transform parent, Sprite sprite, bool bright, int sortingOrder)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(parent, false);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = bright ? additiveMaterial : spriteMaterial;
            sr.sortingOrder = sortingOrder;
            return sr;
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

        private static Sprite CreateShardSprite(int width, int height)
        {
            Texture2D tex = NewTexture(width, height);

            for (int y = 0; y < height; y++)
            {
                float ny = (y / (float)(height - 1)) * 2f - 1f;
                float halfWidth = Mathf.Lerp(0f, 0.94f, 1f - Mathf.Abs(ny));

                for (int x = 0; x < width; x++)
                {
                    float nx = Mathf.Abs((x / (float)(width - 1)) * 2f - 1f);
                    float body = Mathf.Clamp01((halfWidth - nx) * 7.8f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, body));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.42f), height);
        }

        private static Sprite CreateRingSprite(int size)
        {
            Texture2D tex = NewTexture(size, size);
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.38f;
            float thickness = size * 0.05f;

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

        private static Sprite CreateSparkleSprite(int size)
        {
            Texture2D tex = NewTexture(size, size);
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y) - center;
                    float nx = Mathf.Abs(p.x) / center.x;
                    float ny = Mathf.Abs(p.y) / center.y;
                    float horizontal = Mathf.Clamp01(1f - (nx * 0.62f + ny * 8f));
                    float vertical = Mathf.Clamp01(1f - (ny * 0.62f + nx * 8f));
                    float alpha = Mathf.Max(horizontal, vertical);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateMistSprite(int size)
        {
            Texture2D tex = NewTexture(size, size);
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = (new Vector2(x, y) - center) / center.x;
                    float dist = p.magnitude;
                    float alpha = Mathf.Clamp01(1f - dist * 1.25f);
                    alpha = Mathf.Pow(alpha, 1.8f) * (0.72f + 0.28f * Mathf.PerlinNoise((x + 15f) * 0.09f, (y + 10f) * 0.09f));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
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
                name = "Chubbloo_ProceduralVFX"
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
