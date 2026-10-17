using UnityEngine;
using System.Collections;

namespace Bao.Enemies
{
    [RequireComponent(typeof(MoonflutterEnemy))]
    public class MoonflutterVFXController : MonoBehaviour
    {
        [Header("Shader Setup")]
        [SerializeField] private Shader astralShader;
        [SerializeField] private Color astralGlowColor = new Color(0.9f, 0.3f, 0.8f, 1f);
        [SerializeField] private float transitionSpeed = 5.0f;

        [Header("Astral Sparkles")]
        [SerializeField] private Color shimmerColor = new Color(0.3f, 0.8f, 1.0f, 0.7f);

        private MoonflutterEnemy moonflutter;
        private SpriteRenderer[] renderers;
        private Material[] astralMaterials;
        private float currentIntensity = 0.0f;

        private void Awake()
        {
            moonflutter = GetComponent<MoonflutterEnemy>();
            renderers = moonflutter.GetVisualSpriteRenderers();

            if (astralShader == null)
            {
                astralShader = Shader.Find("Bao/MoonflutterAstralShader");
            }

            SetupMaterials();
        }

        private bool IsChildOfChamThan(Transform t)
        {
            while (t != null)
            {
                if (t.name.Equals("ChamThan", System.StringComparison.OrdinalIgnoreCase))
                    return true;
                t = t.parent;
            }
            return false;
        }

        private void SetupMaterials()
        {
            if (astralShader == null) return;

            astralMaterials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                Material mat = new Material(astralShader);
                mat.SetColor("_AstralGlow", astralGlowColor);
                mat.SetFloat("_GlowIntensity", 0.0f);

                if (renderers[i].sharedMaterial != null && renderers[i].sharedMaterial.HasProperty("_MainTex"))
                {
                    mat.SetTexture("_MainTex", renderers[i].sharedMaterial.GetTexture("_MainTex"));
                }

                renderers[i].material = mat;
                astralMaterials[i] = mat;
            }
        }

        private void Start()
        {
            StartCoroutine(EmitAstralSparkles());
        }

        private void Update()
        {
            bool isExploding = moonflutter.StateMachine?.CurrentState is MoonflutterExplodeState;

            if (isExploding)
            {
                // Rapid, violent pulse that speeds up over time (assuming 1.2s swell duration)
                float pulse = 5.0f + Mathf.Sin(Time.time * 20f) * 4.0f; 
                currentIntensity = Mathf.Lerp(currentIntensity, pulse, Time.deltaTime * 10f);
            }
            else
            {
                float idlePulse = 1.2f + Mathf.Sin(Time.time * 2f) * 0.3f;
                currentIntensity = Mathf.Lerp(currentIntensity, idlePulse, Time.deltaTime * transitionSpeed);
            }

            if (astralMaterials != null)
            {
                foreach (var mat in astralMaterials)
                {
                    if (mat != null)
                    {
                        mat.SetFloat("_GlowIntensity", currentIntensity);
                    }
                }
            }

            if (isExploding && Random.Range(0, 3) == 0)
            {
                TriggerStardustExplosion();
            }
        }

        private IEnumerator EmitAstralSparkles()
        {
            while (true)
            {
                bool isExploding = moonflutter.StateMachine?.CurrentState is MoonflutterExplodeState;
                float baseInterval = isExploding ? 0.02f : 0.1f;

                SpawnAstralDust(transform.position + (Vector3)(Random.insideUnitCircle * (isExploding ? 1.5f : 0.5f)), isExploding);
                yield return new WaitForSeconds(baseInterval);
            }
        }

        private void SpawnAstralDust(Vector3 position, bool isExploding = false)
        {
            GameObject dust = new GameObject("AstralStardust");
            dust.transform.SetParent(VFXContainerUtility.GetContainer());
            dust.transform.position = position;
            dust.transform.localScale = Vector3.one * Random.Range(0.06f, 0.15f);

            SpriteRenderer sr = dust.AddComponent<SpriteRenderer>();
            sr.color = Random.value < 0.5f ? shimmerColor : astralGlowColor;
            sr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));

            Texture2D tex = new Texture2D(16, 16);
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(8f, 8f));
                    float alpha = Mathf.Clamp01(1.0f - (dist / 7f));
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha * 0.9f));
                }
            }
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);

            StartCoroutine(AnimateDust(dust, sr, isExploding));
        }

        private IEnumerator AnimateDust(GameObject dust, SpriteRenderer sr, bool isExploding)
        {
            float duration = Random.Range(0.4f, 0.8f);
            float elapsed = 0f;
            Vector3 startPos = dust.transform.position;
            
            // Random drift for idle, but pull towards center (moonflutter) if exploding
            Vector3 drift = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.3f, 0.3f), 0f);

            while (elapsed < duration)
            {
                if (dust == null) yield break; // Might be destroyed early

                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                if (isExploding && moonflutter != null)
                {
                    // Suck particle into the center
                    dust.transform.position = Vector3.Lerp(startPos, moonflutter.transform.position, t * t); // accelerate inwards
                }
                else
                {
                    dust.transform.position += drift * Time.deltaTime;
                }
                
                dust.transform.localScale = Vector3.Lerp(dust.transform.localScale, Vector3.zero, t);

                if (sr != null)
                {
                    sr.color = Color.Lerp(sr.color, new Color(1, 1, 1, 0f), t);
                }

                yield return null;
            }

            if (dust != null) Destroy(dust);
        }

        private void TriggerStardustExplosion()
        {
            Vector3 center = transform.position;
            for (int i = 0; i < 8; i++)
            {
                SpawnAstralDust(center + (Vector3)(Random.insideUnitCircle * 2.0f), true);
            }
        }
    }
}
