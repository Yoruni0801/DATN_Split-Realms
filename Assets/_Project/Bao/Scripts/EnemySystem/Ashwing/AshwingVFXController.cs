using UnityEngine;
using System.Collections;

namespace Bao.Enemies
{
    [RequireComponent(typeof(AshwingEnemy))]
    public class AshwingVFXController : MonoBehaviour
    {
        [Header("Shader Setup")]
        [SerializeField] private Shader cinderShader;
        [SerializeField] private Color fireColor = new Color(1.0f, 0.34f, 0.02f, 1f);
        [SerializeField] private Color hotColor = new Color(1.0f, 0.82f, 0.24f, 1f);
        [SerializeField] private float transitionSpeed = 5.5f;

        [Header("Ambient Embers")]
        [SerializeField] private Color sparkColor = new Color(1.0f, 0.72f, 0.18f, 0.9f);
        [SerializeField] private float ambientEmitInterval = 0.12f;

        [Header("Charge")]
        [SerializeField] private int chargeEmberCount = 9;
        [SerializeField] private float chargeRadius = 1.0f;

        private AshwingEnemy ashwing;
        private SpriteRenderer[] renderers;
        private Material[] cinderMaterials;
        private float currentCinder = 1.0f;
        private Coroutine chargeRoutine;

        private void Awake()
        {
            ashwing = GetComponent<AshwingEnemy>();
            AshwingVFXRuntime.EnsureAssets();

            renderers = ashwing.GetVisualSpriteRenderers();

            if (cinderShader == null)
            {
                cinderShader = Shader.Find("Bao/AshwingCinderShader");
            }

            SetupMaterials();
        }

        private void SetupMaterials()
        {
            if (cinderShader == null || renderers == null)
                return;

            cinderMaterials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                Material mat = new Material(cinderShader)
                {
                    name = $"Ashwing_Cinder_{i}"
                };
                mat.SetColor("_GlowColor", fireColor);
                mat.SetFloat("_CinderThreshold", 1.0f);

                if (renderers[i].sharedMaterial != null && renderers[i].sharedMaterial.HasProperty("_MainTex"))
                {
                    mat.SetTexture("_MainTex", renderers[i].sharedMaterial.GetTexture("_MainTex"));
                }

                renderers[i].material = mat;
                cinderMaterials[i] = mat;
            }
        }

        private void Start()
        {
            StartCoroutine(EmitAmbientEmbers());
        }

        private void Update()
        {
            bool isAttacking = ashwing != null && ashwing.StateMachine?.CurrentState is AshwingShootState;
            float targetCinder = isAttacking ? 8.0f : 1.0f;

            currentCinder = Mathf.Lerp(currentCinder, targetCinder, Time.deltaTime * transitionSpeed);

            if (cinderMaterials == null)
                return;

            float pulse = isAttacking ? 0.88f + Mathf.Sin(Time.time * 18f) * 0.12f : 1f;
            Color pulsedGlow = Color.Lerp(fireColor, hotColor, isAttacking ? 0.35f : 0f) * pulse;
            pulsedGlow.a = 1f;

            foreach (Material mat in cinderMaterials)
            {
                if (mat == null)
                    continue;

                mat.SetFloat("_CinderThreshold", currentCinder);
                mat.SetColor("_GlowColor", pulsedGlow);
            }
        }

        public void BeginCharge(float duration)
        {
            if (chargeRoutine != null)
            {
                StopCoroutine(chargeRoutine);
            }

            chargeRoutine = StartCoroutine(ChargeRoutine(Mathf.Max(0.12f, duration)));
        }

        public void PlayReleaseBurst(Vector2 direction)
        {
            if (chargeRoutine != null)
            {
                StopCoroutine(chargeRoutine);
                chargeRoutine = null;
            }

            Vector3 origin = ashwing != null && ashwing.ShootPoint != null
                ? ashwing.ShootPoint.position
                : transform.position;

            Vector2 normalizedDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            float baseAngle = Mathf.Atan2(normalizedDirection.y, normalizedDirection.x) * Mathf.Rad2Deg;

            // Compact white-hot release flash.
            SpawnDetachedSprite(
                "AshwingReleaseFlash",
                AshwingVFXRuntime.CircleSprite,
                origin,
                Quaternion.identity,
                Vector3.one * 0.20f,
                new Color(1f, 0.95f, 0.65f, 0.95f),
                0.10f,
                Vector3.zero,
                0f,
                new Color(1f, 0.2f, 0f, 0f),
                Vector3.one * 0.95f,
                250);

            // Two short wing/razor streaks make the release feel snappy, but remain
            // small enough for a normal mob attack.
            for (int i = 0; i < 2; i++)
            {
                float offset = i == 0 ? 24f : -24f;
                SpawnDetachedSprite(
                    "AshwingReleaseSlash",
                    AshwingVFXRuntime.StreakSprite,
                    origin,
                    Quaternion.Euler(0f, 0f, baseAngle + offset),
                    new Vector3(0.95f, 0.13f, 1f),
                    i == 0 ? hotColor : fireColor,
                    0.13f,
                    -(Vector3)normalizedDirection * 0.4f,
                    i == 0 ? 55f : -55f,
                    new Color(fireColor.r, 0f, 0f, 0f),
                    new Vector3(1.22f, 0.02f, 1f),
                    248);
            }

            // Backward ember kick gives the projectile physical punch.
            for (int i = 0; i < 10; i++)
            {
                Vector2 tangent = new Vector2(-normalizedDirection.y, normalizedDirection.x);
                Vector3 velocity = -(Vector3)normalizedDirection * Random.Range(1.8f, 4.0f)
                                 + (Vector3)tangent * Random.Range(-1.4f, 1.4f)
                                 + Vector3.up * Random.Range(0.15f, 1.1f);

                SpawnEmber(
                    origin + (Vector3)(Random.insideUnitCircle * 0.12f),
                    velocity,
                    Random.Range(0.08f, 0.17f),
                    Random.Range(0.22f, 0.42f),
                    246);
            }
        }

        private IEnumerator ChargeRoutine(float duration)
        {
            float elapsed = 0f;
            int spawned = 0;
            float interval = duration / Mathf.Max(1, chargeEmberCount);
            float nextSpawn = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                while (spawned < chargeEmberCount && elapsed >= nextSpawn)
                {
                    SpawnChargeEmber(duration);
                    spawned++;
                    nextSpawn += interval;
                }

                yield return null;
            }

            chargeRoutine = null;
        }

        private void SpawnChargeEmber(float totalChargeDuration)
        {
            if (ashwing == null)
                return;

            Transform target = ashwing.ShootPoint != null ? ashwing.ShootPoint : transform;
            Vector2 radial = Random.insideUnitCircle.normalized;
            if (radial.sqrMagnitude < 0.01f)
                radial = Vector2.up;

            float radius = Random.Range(chargeRadius * 0.55f, chargeRadius * 1.15f);
            Vector3 startPosition = transform.position + (Vector3)(radial * radius);

            GameObject ember = new GameObject("AshwingChargeEmber");
            ember.transform.SetParent(VFXContainerUtility.GetContainer());
            ember.transform.position = startPosition;
            ember.transform.localScale = Vector3.one * Random.Range(0.09f, 0.16f);

            SpriteRenderer sr = ember.AddComponent<SpriteRenderer>();
            sr.sprite = AshwingVFXRuntime.CircleSprite;
            sr.sharedMaterial = AshwingVFXRuntime.SpriteMaterial;
            sr.color = Random.value > 0.35f ? sparkColor : fireColor;
            sr.sortingOrder = 247;

            AshwingChargeEmberVFX fx = ember.AddComponent<AshwingChargeEmberVFX>();
            fx.Initialize(
                sr,
                target,
                Mathf.Clamp(totalChargeDuration * Random.Range(0.30f, 0.48f), 0.16f, 0.30f),
                Random.value > 0.5f ? 1f : -1f,
                Random.Range(0.16f, 0.32f));
        }

        private IEnumerator EmitAmbientEmbers()
        {
            while (true)
            {
                bool isAttacking = ashwing != null && ashwing.StateMachine?.CurrentState is AshwingShootState;
                float chance = isAttacking ? 0.28f : 0.10f;

                if (Random.value < chance)
                {
                    Vector3 origin = transform.position + (Vector3)(Random.insideUnitCircle * 0.38f);
                    SpawnEmber(
                        origin,
                        new Vector3(Random.Range(-0.45f, 0.45f), Random.Range(0.7f, 1.45f), 0f),
                        Random.Range(0.06f, 0.12f),
                        Random.Range(0.35f, 0.62f),
                        232);
                }

                yield return new WaitForSeconds(ambientEmitInterval);
            }
        }

        private void SpawnEmber(
            Vector3 position,
            Vector3 velocity,
            float scale,
            float lifetime,
            int sortingOrder)
        {
            Color start = Random.value > 0.30f ? sparkColor : fireColor;
            SpawnDetachedSprite(
                "AshwingEmber",
                AshwingVFXRuntime.CircleSprite,
                position,
                Quaternion.identity,
                Vector3.one * scale,
                start,
                lifetime,
                velocity,
                Random.Range(-180f, 180f),
                new Color(fireColor.r * 0.45f, 0f, 0f, 0f),
                Vector3.zero,
                sortingOrder);
        }

        private static void SpawnDetachedSprite(
            string objectName,
            Sprite sprite,
            Vector3 position,
            Quaternion rotation,
            Vector3 startScale,
            Color startColor,
            float lifetime,
            Vector3 velocity,
            float angularSpeed,
            Color endColor,
            Vector3 endScale,
            int sortingOrder)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(VFXContainerUtility.GetContainer());
            go.transform.position = position;
            go.transform.rotation = rotation;
            go.transform.localScale = startScale;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = AshwingVFXRuntime.SpriteMaterial;
            sr.color = startColor;
            sr.sortingOrder = sortingOrder;

            AshwingTransientSpriteVFX fx = go.AddComponent<AshwingTransientSpriteVFX>();
            fx.Initialize(sr, lifetime, velocity, angularSpeed, endColor, endScale);
        }

        private void OnDestroy()
        {
            if (cinderMaterials == null)
                return;

            foreach (Material mat in cinderMaterials)
            {
                if (mat != null)
                {
                    Destroy(mat);
                }
            }
        }
    }

    /// <summary>
    /// Curved charge particle that spirals into the shoot point.
    /// </summary>
    public class AshwingChargeEmberVFX : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private Transform target;
        private Vector3 startPosition;
        private float lifetime;
        private float elapsed;
        private float curveSign;
        private float curveAmount;
        private Color startColor;
        private Vector3 startScale;

        public void Initialize(
            SpriteRenderer renderer,
            Transform targetTransform,
            float duration,
            float sign,
            float curvature)
        {
            spriteRenderer = renderer;
            target = targetTransform;
            lifetime = Mathf.Max(0.05f, duration);
            curveSign = Mathf.Sign(sign);
            curveAmount = curvature;
            startPosition = transform.position;
            startColor = renderer != null ? renderer.color : Color.white;
            startScale = transform.localScale;
        }

        private void Update()
        {
            if (target == null)
            {
                Destroy(gameObject);
                return;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / lifetime);
            float eased = t * t * (3f - 2f * t);

            Vector3 end = target.position;
            Vector3 line = end - startPosition;
            Vector3 perpendicular = new Vector3(-line.y, line.x, 0f).normalized;
            Vector3 control = (startPosition + end) * 0.5f + perpendicular * curveAmount * curveSign;

            float oneMinusT = 1f - eased;
            transform.position = oneMinusT * oneMinusT * startPosition
                               + 2f * oneMinusT * eased * control
                               + eased * eased * end;

            transform.localScale = Vector3.Lerp(startScale, startScale * 0.25f, eased);

            if (spriteRenderer != null)
            {
                Color targetColor = new Color(1f, 0.22f, 0f, 0f);
                spriteRenderer.color = Color.Lerp(startColor, targetColor, t);
            }

            if (elapsed >= lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}
