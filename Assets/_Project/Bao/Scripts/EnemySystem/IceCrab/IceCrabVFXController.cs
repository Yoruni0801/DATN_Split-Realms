using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Bao.Enemies
{

    [RequireComponent(typeof(IceCrabEnemy))]
    public class IceCrabVFXController : MonoBehaviour
    {
        [Header("Body Shader")]
        [SerializeField] private Shader frostShader;
        [SerializeField] private Color deepTeal = new Color(0.03f, 0.34f, 0.42f, 1f);
        [SerializeField] private Color crabCyan = new Color(0.08f, 0.74f, 0.82f, 1f);
        [SerializeField] private Color frostCyan = new Color(0.47f, 0.96f, 1.0f, 1f);
        [SerializeField] private Color pearlWhite = new Color(0.96f, 1.0f, 1.0f, 1f);
        [SerializeField] private Color shellCream = new Color(1.0f, 0.88f, 0.48f, 1f);
        [SerializeField] private Color shellGold = new Color(0.95f, 0.66f, 0.25f, 1f);
        [SerializeField] private Color coralCore = new Color(0.91f, 0.33f, 0.20f, 1f);
        [SerializeField] private float shaderTransitionSpeed = 8f;

        [Header("Mouth Anchor")]
        [Tooltip("Optional. Leave empty to use the existing attackVFX transform (the bubble ParticleSystem mouth position).")]
        [SerializeField] private Transform mouthAnchorOverride;
        [SerializeField] private Vector2 mouthVisualOffset = Vector2.zero;

        [Header("Idle - Frozen Brine")]
        [SerializeField] private Vector2 idleMoteInterval = new Vector2(0.28f, 0.55f);
        [SerializeField] private float idleMistChance = 0.35f;

        [Header("Attack Charge")]
        [SerializeField] private int chargeStartBurst = 7;
        [SerializeField] private float chargeMoteInterval = 0.045f;
        [SerializeField] private float chargeRadius = 0.9f;
        [SerializeField] private float pressurePulseInterval = 0.15f;
        [SerializeField] private float mouthGlowMaxSize = 0.32f;

        [Header("Bubble Blow Supporting VFX")]
        [SerializeField] private float blowMistInterval = 0.065f;
        [SerializeField] private float blowSparkInterval = 0.11f;
        [SerializeField] private float blowPressureInterval = 0.24f;
        [SerializeField] private int muzzleMistBurst = 8;
        [SerializeField] private int muzzleSparkBurst = 6;

        [Header("Optional Additive Shader")]
        [SerializeField] private Shader additiveShader;

        private IceCrabEnemy iceCrab;
        private SpriteRenderer[] renderers;
        private Material[] frostMaterials;

        private Material spriteMaterial;
        private Material additiveMaterial;

        private Sprite softCircleSprite;
        private Sprite ringSprite;
        private Sprite sparkleSprite;
        private Sprite mistSprite;
        private Sprite shardSprite;

        private readonly List<Object> runtimeAssets = new List<Object>();

        private Transform vfxContainer;
        private int baseSortingOrder;
        private string baseSortingLayer = "Default";

        private Transform mouthGlowRoot;
        private SpriteRenderer mouthHalo;
        private SpriteRenderer mouthCore;

        private Coroutine idleRoutine;
        private Coroutine chargeRoutine;
        private Coroutine blowRoutine;

        private bool charging;
        private bool blowing;
        private float charge01;
        private float blow01;
        private float muzzleFlash;
        private float shellPulse;

        private void Awake()
        {
            iceCrab = GetComponent<IceCrabEnemy>();
            renderers = iceCrab.GetVisualSpriteRenderers();

            if (frostShader == null)
            {
                frostShader = Shader.Find("Bao/IceCrabFrostShader");
            }

            if (additiveShader == null)
            {
                additiveShader = Shader.Find("Bao/IceCrabFrostAdditive");
            }

            vfxContainer = VFXContainerUtility.GetContainer();

            CacheSorting();
            BuildRuntimeAssets();
            SetupMaterials();
            CreateMouthGlow();
        }

        private void OnEnable()
        {
            if (idleRoutine == null)
            {
                idleRoutine = StartCoroutine(IdleLoop());
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            idleRoutine = null;
            chargeRoutine = null;
            blowRoutine = null;

            charging = false;
            blowing = false;
            charge01 = 0f;
            blow01 = 0f;
            muzzleFlash = 0f;
            shellPulse = 0f;

            if (mouthGlowRoot != null)
            {
                mouthGlowRoot.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (frostMaterials != null)
            {
                foreach (Material mat in frostMaterials)
                {
                    if (mat != null)
                    {
                        Destroy(mat);
                    }
                }
            }

            foreach (Object asset in runtimeAssets)
            {
                if (asset != null)
                {
                    Destroy(asset);
                }
            }
        }

        private void Update()
        {
            if (!charging)
            {
                charge01 = Mathf.MoveTowards(charge01, 0f, Time.deltaTime * 4.5f);
            }

            blow01 = Mathf.MoveTowards(blow01, blowing ? 1f : 0f, Time.deltaTime * (blowing ? 7f : 4.5f));

            if (muzzleFlash > 0f)
            {
                muzzleFlash = Mathf.MoveTowards(muzzleFlash, 0f, Time.deltaTime * 7.5f);
            }

            float targetShellPulse = charging ? charge01 : (blowing ? 0.65f : 0f);
            shellPulse = Mathf.Lerp(
                shellPulse,
                targetShellPulse,
                1f - Mathf.Exp(-shaderTransitionSpeed * Time.deltaTime));

            UpdateBodyShader();
            UpdateMouthGlow();
        }

        public void TriggerChargeStart(float duration)
        {
            if (!isActiveAndEnabled)
                return;

            if (chargeRoutine != null)
            {
                StopCoroutine(chargeRoutine);
            }

            charging = true;
            blowing = false;
            chargeRoutine = StartCoroutine(ChargeRoutine(Mathf.Max(0.08f, duration)));
        }

        public void TriggerBubbleBlowStart(float expectedDuration)
        {
            if (!isActiveAndEnabled)
                return;

            charging = false;
            blowing = true;
            charge01 = 0f;
            muzzleFlash = 1f;

            SpawnMuzzleBurst();

            if (blowRoutine != null)
            {
                StopCoroutine(blowRoutine);
            }

            blowRoutine = StartCoroutine(BlowSupportRoutine(Mathf.Max(0.08f, expectedDuration)));
        }

        public void TriggerBubbleBlowStop()
        {
            blowing = false;

            if (blowRoutine != null)
            {
                StopCoroutine(blowRoutine);
                blowRoutine = null;
            }

            SpawnBlowCooldown();
        }

        public void TriggerAttackEnd()
        {
            charging = false;
            blowing = false;

            if (chargeRoutine != null)
            {
                StopCoroutine(chargeRoutine);
                chargeRoutine = null;
            }

            if (blowRoutine != null)
            {
                StopCoroutine(blowRoutine);
                blowRoutine = null;
            }
        }

        public void TriggerDeathBurst()
        {
            Vector3 center = transform.position + new Vector3(0f, 0.10f, 0f);

            SpawnRing(center, new Vector2(0.78f, 0.48f), shellCream, 0.22f, true, baseSortingOrder + 5);
            SpawnRing(center, new Vector2(1.08f, 0.64f), frostCyan, 0.28f, false, baseSortingOrder + 4);

            for (int i = 0; i < 8; i++)
            {
                SpawnShellChip(center);
            }

            for (int i = 0; i < 10; i++)
            {
                SpawnColdMist(center, Random.insideUnitCircle * Random.Range(0.35f, 1.05f), Random.Range(0.34f, 0.52f), baseSortingOrder + 1);
            }
        }

        private void CacheSorting()
        {
            baseSortingOrder = 0;

            if (renderers == null)
                return;

            foreach (SpriteRenderer sr in renderers)
            {
                if (sr == null)
                    continue;

                if (sr.sortingOrder >= baseSortingOrder)
                {
                    baseSortingOrder = sr.sortingOrder;
                    baseSortingLayer = sr.sortingLayerName;
                }
            }
        }

        private void SetupMaterials()
        {
            if (frostShader == null || renderers == null)
                return;

            frostMaterials = new Material[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer sr = renderers[i];
                if (sr == null)
                    continue;

                Material source = sr.sharedMaterial;
                Material mat = new Material(frostShader)
                {
                    name = $"IceCrabFrost_Runtime_{i}"
                };

                if (source != null && source.HasProperty("_MainTex") && mat.HasProperty("_MainTex"))
                {
                    mat.SetTexture("_MainTex", source.GetTexture("_MainTex"));
                }

                SetColorIfExists(mat, "_DeepTeal", deepTeal);
                SetColorIfExists(mat, "_CrabCyan", crabCyan);
                SetColorIfExists(mat, "_FrostColor", frostCyan);
                SetColorIfExists(mat, "_PearlWhite", pearlWhite);
                SetColorIfExists(mat, "_ShellCream", shellCream);
                SetColorIfExists(mat, "_ShellGold", shellGold);
                SetColorIfExists(mat, "_CoralCore", coralCore);

                SetFloatIfExists(mat, "_Charge", 0f);
                SetFloatIfExists(mat, "_Blow", 0f);
                SetFloatIfExists(mat, "_MuzzleFlash", 0f);
                SetFloatIfExists(mat, "_ShellPulse", 0f);

                sr.material = mat;
                frostMaterials[i] = mat;
            }
        }

        private void UpdateBodyShader()
        {
            if (frostMaterials == null)
                return;

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(2.2f, 8.5f, charge01 + blow01 * 0.55f));
            float pearlGleam = Mathf.Lerp(0.22f, 0.52f, pulse);

            foreach (Material mat in frostMaterials)
            {
                if (mat == null)
                    continue;

                SetFloatIfExists(mat, "_Charge", charge01);
                SetFloatIfExists(mat, "_Blow", blow01);
                SetFloatIfExists(mat, "_MuzzleFlash", muzzleFlash);
                SetFloatIfExists(mat, "_ShellPulse", shellPulse);
                SetFloatIfExists(mat, "_PearlGleam", pearlGleam);
                SetFloatIfExists(mat, "_FacingDirection", iceCrab.FacingDirection);
            }
        }

        private void CreateMouthGlow()
        {
            mouthGlowRoot = new GameObject("IceCrab_MouthChargeGlow").transform;
            mouthGlowRoot.SetParent(transform, false);

            mouthHalo = CreateLocalSpriteRenderer(
                "Mouth Halo",
                mouthGlowRoot,
                softCircleSprite,
                additiveMaterial,
                baseSortingOrder + 4);

            mouthCore = CreateLocalSpriteRenderer(
                "Mouth Core",
                mouthGlowRoot,
                softCircleSprite,
                additiveMaterial,
                baseSortingOrder + 5);

            mouthGlowRoot.gameObject.SetActive(false);
        }

        private void UpdateMouthGlow()
        {
            if (mouthGlowRoot == null || mouthHalo == null || mouthCore == null)
                return;

            float visibility = Mathf.Max(charge01, blow01 * 0.55f, muzzleFlash);
            if (visibility <= 0.01f)
            {
                mouthGlowRoot.gameObject.SetActive(false);
                return;
            }

            mouthGlowRoot.gameObject.SetActive(true);
            mouthGlowRoot.position = GetMouthPosition();

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(4f, 14f, Mathf.Max(charge01, blow01)));
            float coreSize = Mathf.Lerp(0.04f, mouthGlowMaxSize, charge01);
            coreSize = Mathf.Max(coreSize, 0.10f * blow01);
            coreSize *= Mathf.Lerp(0.92f, 1.08f, pulse);

            mouthCore.transform.localScale = Vector3.one * coreSize;
            mouthHalo.transform.localScale = Vector3.one * coreSize * Mathf.Lerp(2.1f, 2.8f, charge01);

            mouthCore.color = WithAlpha(pearlWhite, Mathf.Clamp01(visibility * 0.95f));
            mouthHalo.color = WithAlpha(frostCyan, Mathf.Clamp01(visibility * 0.30f + muzzleFlash * 0.28f));
        }

        private IEnumerator ChargeRoutine(float duration)
        {
            for (int i = 0; i < chargeStartBurst; i++)
            {
                SpawnChargeMote(true);
            }

            float elapsed = 0f;
            float moteTimer = 0f;
            float pulseTimer = 0f;

            while (elapsed < duration && charging)
            {
                elapsed += Time.deltaTime;
                moteTimer += Time.deltaTime;
                pulseTimer += Time.deltaTime;

                charge01 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

                float interval = Mathf.Lerp(chargeMoteInterval * 1.35f, chargeMoteInterval * 0.55f, charge01);
                while (moteTimer >= interval)
                {
                    moteTimer -= interval;
                    SpawnChargeMote(false);

                    if (charge01 > 0.55f && Random.value < 0.32f)
                    {
                        SpawnChargeSpark();
                    }
                }

                if (pulseTimer >= pressurePulseInterval)
                {
                    pulseTimer = 0f;
                    SpawnPressurePulse(
                        GetMouthPosition(),
                        Mathf.Lerp(0.28f, 0.52f, charge01),
                        Mathf.Lerp(0.16f, 0.27f, charge01));
                }

                yield return null;
            }

            charge01 = charging ? 1f : charge01;
            chargeRoutine = null;
        }

        private void SpawnChargeMote(bool burst)
        {
            Vector3 center = GetMouthPosition();
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(chargeRadius * 0.48f, chargeRadius);

            Vector3 start = center + new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius * 0.60f,
                0f);

            Vector3 target = center + (Vector3)(Random.insideUnitCircle * 0.025f);

            bool bright = Random.value < 0.24f;
            Color color = bright ? pearlWhite : Color.Lerp(crabCyan, frostCyan, Random.Range(0.20f, 0.75f));

            GameObject go = CreateWorldSprite(
                "IceCrab_ChargeMote",
                bright ? sparkleSprite : softCircleSprite,
                color,
                bright ? additiveMaterial : spriteMaterial,
                baseSortingOrder + 3);

            go.transform.position = start;
            go.transform.localScale = Vector3.one * (burst ? Random.Range(0.04f, 0.075f) : Random.Range(0.025f, 0.055f));

            float lifetime = burst ? Random.Range(0.30f, 0.40f) : Random.Range(0.22f, 0.30f);
            Destroy(go, lifetime + 0.05f);
            StartCoroutine(AnimateSuctionMote(go, start, target, color, lifetime));
        }

        private IEnumerator AnimateSuctionMote(GameObject go, Vector3 start, Vector3 target, Color color, float duration)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 startScale = go.transform.localScale;
            Vector3 control = Vector3.Lerp(start, target, 0.56f) + new Vector3(
                Random.Range(-0.12f, 0.12f),
                Random.Range(-0.08f, 0.14f),
                0f);

            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 2.1f);

                Vector3 a = Vector3.Lerp(start, control, eased);
                Vector3 b = Vector3.Lerp(control, target, eased);
                go.transform.position = Vector3.Lerp(a, b, eased);
                go.transform.localScale = startScale * Mathf.Lerp(1.1f, 0.08f, t);

                if (sr != null)
                {
                    Color c = Color.Lerp(color, pearlWhite, t * 0.65f);
                    c.a = Mathf.Sin(t * Mathf.PI) * 0.82f;
                    sr.color = c;
                }

                yield return null;
            }
        }

        private void SpawnChargeSpark()
        {
            Vector3 center = GetMouthPosition();
            GameObject go = CreateWorldSprite(
                "IceCrab_ChargeSpark",
                sparkleSprite,
                pearlWhite,
                additiveMaterial,
                baseSortingOrder + 5);

            go.transform.position = center + (Vector3)(Random.insideUnitCircle * Random.Range(0.12f, 0.28f));
            go.transform.localScale = Vector3.one * Random.Range(0.025f, 0.05f);

            float lifetime = Random.Range(0.12f, 0.20f);
            Destroy(go, lifetime + 0.05f);
            StartCoroutine(AnimateSparkFade(go, lifetime));
        }

        private void SpawnMuzzleBurst()
        {
            Vector3 mouth = GetMouthPosition();
            Vector2 direction = Vector2.right * iceCrab.FacingDirection;

            SpawnPressurePulse(mouth, 0.58f, 0.30f);

            for (int i = 0; i < muzzleMistBurst; i++)
            {
                Vector3 velocity =
                    (Vector3)(direction * Random.Range(0.55f, 1.55f)) +
                    new Vector3(Random.Range(-0.10f, 0.10f), Random.Range(-0.26f, 0.26f), 0f);

                SpawnColdMist(
                    mouth,
                    velocity,
                    Random.Range(0.24f, 0.38f),
                    baseSortingOrder + 2);
            }

            for (int i = 0; i < muzzleSparkBurst; i++)
            {
                SpawnForwardSpark(mouth, direction, true);
            }
        }

        private IEnumerator BlowSupportRoutine(float expectedDuration)
        {
            float elapsed = 0f;
            float mistTimer = 0f;
            float sparkTimer = 0f;
            float pressureTimer = 0f;

            while (blowing && elapsed < expectedDuration + 0.12f)
            {
                elapsed += Time.deltaTime;
                mistTimer += Time.deltaTime;
                sparkTimer += Time.deltaTime;
                pressureTimer += Time.deltaTime;

                if (mistTimer >= blowMistInterval)
                {
                    mistTimer = 0f;

                    Vector2 direction = Vector2.right * iceCrab.FacingDirection;
                    Vector3 velocity =
                        (Vector3)(direction * Random.Range(0.35f, 0.85f)) +
                        new Vector3(0f, Random.Range(-0.12f, 0.12f), 0f);

                    SpawnColdMist(
                        GetMouthPosition(),
                        velocity,
                        Random.Range(0.18f, 0.30f),
                        baseSortingOrder + 1);
                }

                if (sparkTimer >= blowSparkInterval)
                {
                    sparkTimer = 0f;
                    SpawnForwardSpark(
                        GetMouthPosition(),
                        Vector2.right * iceCrab.FacingDirection,
                        false);
                }

                if (pressureTimer >= blowPressureInterval)
                {
                    pressureTimer = 0f;
                    SpawnPressurePulse(GetMouthPosition(), 0.42f, 0.22f);
                }

                yield return null;
            }

            blowRoutine = null;
        }

        private void SpawnBlowCooldown()
        {
            Vector3 mouth = GetMouthPosition();
            Vector2 backwards = Vector2.left * iceCrab.FacingDirection;

            SpawnPressurePulse(mouth, 0.36f, 0.20f);

            for (int i = 0; i < 5; i++)
            {
                Vector3 velocity =
                    (Vector3)(backwards * Random.Range(0.10f, 0.35f)) +
                    new Vector3(Random.Range(-0.08f, 0.08f), Random.Range(0.04f, 0.22f), 0f);

                SpawnColdMist(
                    mouth,
                    velocity,
                    Random.Range(0.22f, 0.34f),
                    baseSortingOrder + 1);
            }
        }

        private void SpawnForwardSpark(Vector3 mouth, Vector2 direction, bool strong)
        {
            GameObject go = CreateWorldSprite(
                "IceCrab_FrostSpark",
                sparkleSprite,
                pearlWhite,
                additiveMaterial,
                baseSortingOrder + 4);

            go.transform.position = mouth + (Vector3)(Random.insideUnitCircle * 0.025f);
            go.transform.localScale = Vector3.one * Random.Range(
                strong ? 0.030f : 0.018f,
                strong ? 0.060f : 0.040f);

            Vector3 velocity =
                (Vector3)(direction * Random.Range(strong ? 1.0f : 0.55f, strong ? 2.3f : 1.25f)) +
                (Vector3)Random.insideUnitCircle * (strong ? 0.50f : 0.28f);

            float lifetime = Random.Range(strong ? 0.14f : 0.10f, strong ? 0.24f : 0.18f);
            Destroy(go, lifetime + 0.05f);
            StartCoroutine(AnimateMovingSpark(go, velocity, lifetime));
        }

        private void SpawnColdMist(Vector3 center, Vector3 velocity, float duration, int sortingOrder)
        {
            GameObject go = CreateWorldSprite(
                "IceCrab_ColdMist",
                mistSprite,
                Color.Lerp(crabCyan, frostCyan, 0.48f),
                spriteMaterial,
                sortingOrder);

            go.transform.position = center;
            go.transform.localScale = new Vector3(
                Random.Range(0.08f, 0.16f),
                Random.Range(0.05f, 0.10f),
                1f);

            Destroy(go, duration + 0.05f);
            StartCoroutine(AnimateMist(go, velocity, duration));
        }

        private IEnumerator AnimateMist(GameObject go, Vector3 velocity, float duration)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 startScale = go.transform.localScale;
            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                velocity *= Mathf.Exp(-3.1f * Time.deltaTime);
                go.transform.position += velocity * Time.deltaTime;
                go.transform.localScale = startScale * Mathf.Lerp(1f, 2.5f, t);

                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = Mathf.Lerp(0.22f, 0f, t);
                    sr.color = c;
                }

                yield return null;
            }
        }

        private void SpawnRing(Vector3 center, Vector2 size, Color color, float duration, bool bright, int sortingOrder)
        {
            GameObject go = CreateWorldSprite(
                "IceCrab_Ring",
                ringSprite,
                color,
                bright ? additiveMaterial : spriteMaterial,
                sortingOrder);

            go.transform.position = center;
            go.transform.localScale = new Vector3(0.08f, 0.05f, 1f);

            Destroy(go, duration + 0.05f);
            StartCoroutine(AnimateRing(go, size, color, duration));
        }

        private IEnumerator AnimateRing(GameObject go, Vector2 endSize, Color color, float duration)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 start = new Vector3(0.08f, 0.05f, 1f);
            Vector3 end = new Vector3(endSize.x, endSize.y, 1f);
            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                go.transform.localScale = Vector3.LerpUnclamped(start, end, eased);

                if (sr != null)
                {
                    Color c = color;
                    c.a = Mathf.Pow(1f - t, 1.5f) * 0.72f;
                    sr.color = c;
                }

                yield return null;
            }
        }

        private void SpawnPressurePulse(Vector3 center, float width, float height)
        {
            GameObject go = CreateWorldSprite(
                "IceCrab_PressurePulse",
                ringSprite,
                frostCyan,
                additiveMaterial,
                baseSortingOrder + 3);

            go.transform.position = center;
            go.transform.localScale = new Vector3(0.08f, 0.05f, 1f);

            float duration = Random.Range(0.15f, 0.21f);
            Destroy(go, duration + 0.05f);
            StartCoroutine(AnimatePressureRing(go, new Vector2(width, height), duration));
        }

        private IEnumerator AnimatePressureRing(GameObject go, Vector2 endSize, float duration)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 start = new Vector3(0.08f, 0.05f, 1f);
            Vector3 end = new Vector3(endSize.x, endSize.y, 1f);
            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                go.transform.localScale = Vector3.LerpUnclamped(start, end, eased);

                if (sr != null)
                {
                    Color c = frostCyan;
                    c.a = Mathf.Pow(1f - t, 1.55f) * 0.58f;
                    sr.color = c;
                }

                yield return null;
            }
        }

        private IEnumerator IdleLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(idleMoteInterval.x, idleMoteInterval.y));

                if (iceCrab == null || iceCrab.IsDead || charging || blowing)
                    continue;

                SpawnIdleBrineMote();

                if (Random.value < idleMistChance)
                {
                    Vector3 center = transform.position + new Vector3(
                        Random.Range(-0.28f, 0.28f),
                        Random.Range(-0.06f, 0.20f),
                        0f);

                    SpawnColdMist(
                        center,
                        new Vector3(Random.Range(-0.05f, 0.05f), Random.Range(0.04f, 0.12f), 0f),
                        Random.Range(0.36f, 0.52f),
                        baseSortingOrder);
                }
            }
        }

        private void SpawnIdleBrineMote()
        {
            Vector3 center = transform.position + new Vector3(
                Random.Range(-0.34f, 0.34f),
                Random.Range(-0.12f, 0.26f),
                0f);

            bool sparkle = Random.value < 0.35f;
            GameObject go = CreateWorldSprite(
                "IceCrab_BrineMote",
                sparkle ? sparkleSprite : shardSprite,
                sparkle ? pearlWhite : Color.Lerp(crabCyan, frostCyan, 0.38f),
                sparkle ? additiveMaterial : spriteMaterial,
                baseSortingOrder + 1);

            go.transform.position = center;
            go.transform.localScale = sparkle
                ? Vector3.one * Random.Range(0.018f, 0.038f)
                : new Vector3(Random.Range(0.018f, 0.035f), Random.Range(0.030f, 0.060f), 1f);

            float lifetime = Random.Range(0.48f, 0.82f);
            Destroy(go, lifetime + 0.05f);
            StartCoroutine(AnimateIdleMote(go, lifetime));
        }

        private IEnumerator AnimateIdleMote(GameObject go, float duration)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 velocity = new Vector3(
                Random.Range(-0.05f, 0.05f),
                Random.Range(0.04f, 0.12f),
                0f);

            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                go.transform.position += velocity * Time.deltaTime;
                go.transform.Rotate(0f, 0f, 55f * Time.deltaTime);

                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = Mathf.Sin(t * Mathf.PI) * 0.55f;
                    sr.color = c;
                }

                yield return null;
            }
        }

        private void SpawnShellChip(Vector3 center)
        {
            bool warm = Random.value < 0.62f;
            Color color = warm
                ? Color.Lerp(shellCream, shellGold, Random.Range(0.15f, 0.80f))
                : frostCyan;

            GameObject go = CreateWorldSprite(
                "IceCrab_ShellChip",
                shardSprite,
                color,
                warm ? spriteMaterial : additiveMaterial,
                baseSortingOrder + 4);

            go.transform.position = center + (Vector3)(Random.insideUnitCircle * 0.08f);
            go.transform.localScale = new Vector3(
                Random.Range(0.035f, 0.070f),
                Random.Range(0.075f, 0.14f),
                1f);

            go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            Vector3 velocity = (Vector3)Random.insideUnitCircle.normalized * Random.Range(0.55f, 1.45f);
            velocity.y = Mathf.Abs(velocity.y) + 0.18f;

            float duration = Random.Range(0.28f, 0.46f);
            Destroy(go, duration + 0.05f);
            StartCoroutine(AnimateShellChip(go, velocity, duration));
        }

        private IEnumerator AnimateShellChip(GameObject go, Vector3 velocity, float duration)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 startScale = go.transform.localScale;
            float spin = Random.Range(-720f, 720f);
            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                velocity.y -= 2.6f * Time.deltaTime;
                go.transform.position += velocity * Time.deltaTime;
                go.transform.Rotate(0f, 0f, spin * Time.deltaTime);
                go.transform.localScale = startScale * Mathf.Lerp(1f, 0.32f, t);

                if (sr != null && t > 0.45f)
                {
                    Color c = sr.color;
                    c.a = 1f - Mathf.InverseLerp(0.45f, 1f, t);
                    sr.color = c;
                }

                yield return null;
            }
        }

        private Vector3 GetMouthPosition()
        {
            Transform anchor = mouthAnchorOverride != null
                ? mouthAnchorOverride
                : iceCrab.BubbleMouthAnchor;

            Vector3 position = anchor != null ? anchor.position : transform.position;
            position += new Vector3(
                mouthVisualOffset.x * iceCrab.FacingDirection,
                mouthVisualOffset.y,
                0f);

            return position;
        }

        private void BuildRuntimeAssets()
        {
            Shader spriteShader = Shader.Find("Sprites/Default");

            if (spriteShader != null)
            {
                spriteMaterial = new Material(spriteShader)
                {
                    name = "IceCrab_SupportVFX_Runtime"
                };
                runtimeAssets.Add(spriteMaterial);
            }

            Shader glowShader = additiveShader != null ? additiveShader : spriteShader;
            if (glowShader != null)
            {
                additiveMaterial = new Material(glowShader)
                {
                    name = "IceCrab_SupportGlow_Runtime"
                };
                runtimeAssets.Add(additiveMaterial);
            }

            softCircleSprite = CreateSoftCircleSprite(64);
            ringSprite = CreateRingSprite(96);
            sparkleSprite = CreateSparkleSprite(64);
            mistSprite = CreateMistSprite(96);
            shardSprite = CreateShardSprite(48, 72);

            runtimeAssets.Add(softCircleSprite);
            runtimeAssets.Add(ringSprite);
            runtimeAssets.Add(sparkleSprite);
            runtimeAssets.Add(mistSprite);
            runtimeAssets.Add(shardSprite);

            runtimeAssets.Add(softCircleSprite.texture);
            runtimeAssets.Add(ringSprite.texture);
            runtimeAssets.Add(sparkleSprite.texture);
            runtimeAssets.Add(mistSprite.texture);
            runtimeAssets.Add(shardSprite.texture);
        }

        private SpriteRenderer CreateLocalSpriteRenderer(
            string name,
            Transform parent,
            Sprite sprite,
            Material material,
            int sortingOrder)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = material;
            sr.sortingLayerName = baseSortingLayer;
            sr.sortingOrder = sortingOrder;

            return sr;
        }

        private GameObject CreateWorldSprite(
            string name,
            Sprite sprite,
            Color color,
            Material material,
            int sortingOrder)
        {
            GameObject go = new GameObject(name);

            if (vfxContainer != null)
            {
                go.transform.SetParent(vfxContainer, true);
            }

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sharedMaterial = material;
            sr.sortingLayerName = baseSortingLayer;
            sr.sortingOrder = sortingOrder;

            return go;
        }

        private IEnumerator AnimateSparkFade(GameObject go, float duration)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 startScale = go.transform.localScale;
            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                go.transform.localScale = startScale * Mathf.Lerp(1.25f, 0f, t);

                if (sr != null)
                {
                    Color c = pearlWhite;
                    c.a = 1f - t;
                    sr.color = c;
                }

                yield return null;
            }
        }

        private IEnumerator AnimateMovingSpark(GameObject go, Vector3 velocity, float duration)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 startScale = go.transform.localScale;
            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                velocity *= Mathf.Exp(-3.6f * Time.deltaTime);
                go.transform.position += velocity * Time.deltaTime;
                go.transform.localScale = startScale * Mathf.Lerp(1.15f, 0f, t);

                if (sr != null)
                {
                    Color c = pearlWhite;
                    c.a = 1f - t;
                    sr.color = c;
                }

                yield return null;
            }
        }

        private static void SetFloatIfExists(Material mat, string property, float value)
        {
            if (mat != null && mat.HasProperty(property))
            {
                mat.SetFloat(property, value);
            }
        }

        private static void SetColorIfExists(Material mat, string property, Color value)
        {
            if (mat != null && mat.HasProperty(property))
            {
                mat.SetColor(property, value);
            }
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private static Sprite CreateSoftCircleSprite(int size)
        {
            Texture2D tex = NewTexture(size, size, "IceCrab_SoftCircle");
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
            Texture2D tex = NewTexture(size, size, "IceCrab_Ring");
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
                    alpha *= alpha;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateSparkleSprite(int size)
        {
            Texture2D tex = NewTexture(size, size, "IceCrab_Sparkle");
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y) - center;
                    float nx = Mathf.Abs(p.x) / center.x;
                    float ny = Mathf.Abs(p.y) / center.y;

                    float horizontal = Mathf.Clamp01(1f - (nx * 0.65f + ny * 8f));
                    float vertical = Mathf.Clamp01(1f - (ny * 0.65f + nx * 8f));
                    float alpha = Mathf.Max(horizontal, vertical);

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateMistSprite(int size)
        {
            Texture2D tex = NewTexture(size, size, "IceCrab_Mist");
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = (new Vector2(x, y) - center) / center.x;
                    float dist = p.magnitude;
                    float alpha = Mathf.Clamp01(1f - dist * 1.20f);

                    float textureNoise = 0.72f + 0.28f *
                        Mathf.PerlinNoise((x + 11f) * 0.09f, (y + 7f) * 0.09f);

                    alpha = Mathf.Pow(alpha, 1.8f) * textureNoise;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateShardSprite(int width, int height)
        {
            Texture2D tex = NewTexture(width, height, "IceCrab_Shard");

            for (int y = 0; y < height; y++)
            {
                float ny = (y / (float)(height - 1)) * 2f - 1f;
                float halfWidth = Mathf.Lerp(0f, 0.94f, 1f - Mathf.Abs(ny));

                for (int x = 0; x < width; x++)
                {
                    float nx = Mathf.Abs((x / (float)(width - 1)) * 2f - 1f);
                    float body = Mathf.Clamp01((halfWidth - nx) * 8f);

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, body));
                }
            }

            tex.Apply();
            return Sprite.Create(
                tex,
                new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.42f),
                height);
        }

        private static Texture2D NewTexture(int width, int height, string name)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = name
            };

            Color[] pixels = new Color[width * height];
            Color clear = new Color(1f, 1f, 1f, 0f);

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            tex.SetPixels(pixels);
            return tex;
        }
    }
}
