using System.Collections;
using UnityEngine;

namespace Bao.Enemies
{
    [RequireComponent(typeof(IcecrawlerEnemy))]
    public class IcecrawlerVFXController : MonoBehaviour
    {
        [Header("Shader Setup (Option A - High Contrast Fantasy)")]
        [SerializeField] private Shader frostShader;
        [SerializeField] private Color obsidianBodyColor = new Color(0.12f, 0.15f, 0.18f, 1f);
        [SerializeField] private Color cobaltSpikeColor = new Color(0.08f, 0.35f, 0.75f, 1f);
        [SerializeField] private Color frostEdgeColor = new Color(0.45f, 0.85f, 1.00f, 1f);
        [SerializeField] private Color amberCoreColor = new Color(1.50f, 0.55f, 0.05f, 1f);
        [SerializeField] private float transitionSpeed = 6.0f;
        [SerializeField] private float chargeGlowIntensity = 2.5f;

        [Header("High-Contrast Particle Trails")]
        [SerializeField] private Color cobaltCrystalColor = new Color(0.15f, 0.55f, 1.0f, 0.85f);
        [SerializeField] private Color emberSparkColor = new Color(1.0f, 0.6f, 0.1f, 0.9f);
        [SerializeField] private Color darkRockColor = new Color(0.1f, 0.12f, 0.15f, 0.9f);
        [SerializeField] private float emitInterval = 0.04f;

        private IcecrawlerEnemy icecrawler;
        private SpriteRenderer[] renderers;
        private Material[] frostMaterials;
        private float currentCharge;
        private float currentDash;
        private float currentSpikes;

        private static readonly int PropObsidianColor = Shader.PropertyToID("_ObsidianColor");
        private static readonly int PropCobaltIceColor = Shader.PropertyToID("_CobaltIceColor");
        private static readonly int PropFrostEdgeColor = Shader.PropertyToID("_FrostEdgeColor");
        private static readonly int PropAmberColor = Shader.PropertyToID("_AmberColor");
        private static readonly int PropCharge = Shader.PropertyToID("_Charge");
        private static readonly int PropDash = Shader.PropertyToID("_Dash");
        private static readonly int PropSpikeLevel = Shader.PropertyToID("_SpikeLevel");
        private static readonly int PropChargeIntensity = Shader.PropertyToID("_ChargeIntensity");
        private static readonly int PropMainTex = Shader.PropertyToID("_MainTex");

        private void Awake()
        {
            icecrawler = GetComponent<IcecrawlerEnemy>();
            renderers = GetEnemyRenderers();

            if (frostShader == null)
            {
                frostShader = Shader.Find("Bao/IcecrawlerFrostShader");
            }

            SetupMaterials();
        }

        private SpriteRenderer[] GetEnemyRenderers()
        {
            return icecrawler.GetVisualSpriteRenderers();
        }

        private void SetupMaterials()
        {
            if (frostShader == null || renderers == null) return;

            frostMaterials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;

                Material mat = new Material(frostShader);
                mat.SetColor(PropObsidianColor, obsidianBodyColor);
                mat.SetColor(PropCobaltIceColor, cobaltSpikeColor);
                mat.SetColor(PropFrostEdgeColor, frostEdgeColor);
                mat.SetColor(PropAmberColor, amberCoreColor);

                mat.SetFloat(PropCharge, 0.0f);
                mat.SetFloat(PropDash, 0.0f);
                mat.SetFloat(PropSpikeLevel, 0.0f);
                mat.SetFloat(PropChargeIntensity, 0.0f);

                if (renderers[i].sharedMaterial != null && renderers[i].sharedMaterial.HasProperty(PropMainTex))
                {
                    mat.SetTexture(PropMainTex, renderers[i].sharedMaterial.GetTexture(PropMainTex));
                }

                renderers[i].material = mat;
                frostMaterials[i] = mat;
            }
        }

        private void Start()
        {
            StartCoroutine(EmitFrostParticles());
        }

        private void Update()
        {
            IcecrawlerChargeState chargeState = icecrawler.StateMachine?.CurrentState as IcecrawlerChargeState;
            bool isCharging = chargeState != null;
            bool hasLaunched = chargeState != null && chargeState.IsChargingStarted;

            float targetCharge = isCharging && !hasLaunched ? 1.0f : 0.0f;
            float targetDash = hasLaunched ? 1.0f : 0.0f;
            float targetSpikes = isCharging ? (hasLaunched ? 10.0f : 7.0f) : 1.0f;

            currentCharge = Mathf.Lerp(currentCharge, targetCharge, Time.deltaTime * transitionSpeed);
            currentDash = Mathf.Lerp(currentDash, targetDash, Time.deltaTime * transitionSpeed);
            currentSpikes = Mathf.Lerp(currentSpikes, targetSpikes, Time.deltaTime * transitionSpeed);

            if (frostMaterials != null)
            {
                float glow = isCharging ? chargeGlowIntensity * (hasLaunched ? 1.0f : 0.6f) : 0.0f;
                foreach (Material mat in frostMaterials)
                {
                    if (mat == null) continue;
                    mat.SetFloat(PropCharge, currentCharge);
                    mat.SetFloat(PropDash, currentDash);
                    mat.SetFloat(PropSpikeLevel, currentSpikes);
                    mat.SetFloat(PropChargeIntensity, glow);
                }
            }

            if (hasLaunched && Random.Range(0, 3) == 0)
            {
                SpawnGroundSpike();
            }
        }

        private IEnumerator EmitFrostParticles()
        {
            while (true)
            {
                IcecrawlerChargeState chargeState = icecrawler.StateMachine?.CurrentState as IcecrawlerChargeState;
                bool isCharging = chargeState != null;
                bool hasLaunched = chargeState != null && chargeState.IsChargingStarted;

                if (hasLaunched)
                {
                    // Khi lướt: Tung ra tinh thể cobalt + mảnh đá đen vụn + tàn lửa hổ phách
                    Vector3 basePos = transform.position + new Vector3(Random.Range(-0.35f, 0.35f), -0.2f, 0f);
                    SpawnParticle(basePos, cobaltCrystalColor, Random.Range(0.09f, 0.18f), true);

                    if (Random.value < 0.6f)
                    {
                        SpawnParticle(basePos + (Vector3)(Random.insideUnitCircle * 0.1f), darkRockColor, Random.Range(0.06f, 0.12f), false);
                    }
                    if (Random.value < 0.4f)
                    {
                        SpawnParticle(basePos + Vector3.up * Random.Range(0f, 0.3f), emberSparkColor, Random.Range(0.04f, 0.09f), true);
                    }
                }
                else if (isCharging)
                {
                    // Khi đang gồng tụ lực: Tinh thể hổ phách & cobalt bốc lên xung quanh
                    Vector3 basePos = transform.position + new Vector3(Random.Range(-0.45f, 0.45f), Random.Range(-0.25f, 0.4f), 0f);
                    SpawnParticle(basePos, Random.value > 0.5f ? emberSparkColor : cobaltCrystalColor, Random.Range(0.05f, 0.11f), true);
                }

                yield return new WaitForSeconds(emitInterval);
            }
        }

        private void SpawnParticle(Vector3 position, Color color, float size, bool glow)
        {
            GameObject pObj = new GameObject("IcecrawlerTrailParticle");
            pObj.transform.SetParent(VFXContainerUtility.GetContainer());
            pObj.transform.position = position;
            pObj.transform.localScale = Vector3.one * size;

            SpriteRenderer sr = pObj.AddComponent<SpriteRenderer>();
            sr.color = color;
            sr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));

            Texture2D texture = new Texture2D(8, 8);
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(3.5f, 3.5f));
                    float alpha = Mathf.Clamp01(1.0f - dist / 3.5f);
                    texture.SetPixel(x, y, new Color(1, 1, 1, alpha * 0.95f));
                }
            }
            texture.Apply();
            sr.sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f));

            StartCoroutine(AnimateParticle(pObj, sr, color, glow));
        }

        private IEnumerator AnimateParticle(GameObject pObj, SpriteRenderer sr, Color baseColor, bool glow)
        {
            float duration = Random.Range(0.35f, 0.65f);
            float elapsed = 0f;
            Vector3 drift = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0.3f, 0.9f), 0f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                if (pObj != null)
                {
                    pObj.transform.position += drift * Time.deltaTime;
                    pObj.transform.localScale = Vector3.Lerp(pObj.transform.localScale, Vector3.zero, progress);
                }

                if (sr != null)
                {
                    Color targetColor = new Color(baseColor.r * 0.2f, baseColor.g * 0.2f, baseColor.b * 0.2f, 0f);
                    sr.color = Color.Lerp(baseColor, targetColor, progress);
                }

                yield return null;
            }

            if (pObj != null)
            {
                Destroy(pObj);
            }
        }

        private void SpawnGroundSpike()
        {
            Vector3 center = transform.position + new Vector3(Random.Range(-0.4f, 0.4f), -0.28f, 0);
            for (int i = 0; i < 3; i++)
            {
                SpawnParticle(center + (Vector3)(Random.insideUnitCircle * 0.15f), cobaltCrystalColor, Random.Range(0.08f, 0.15f), true);
            }
        }
    }
}
