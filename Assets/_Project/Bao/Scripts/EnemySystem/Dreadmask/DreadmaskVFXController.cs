using UnityEngine;
using System.Collections;

namespace Bao.Enemies
{
    [RequireComponent(typeof(DreadmaskEnemy))]
    public class DreadmaskVFXController : MonoBehaviour
    {
        [Header("Shader Setup")]
        [SerializeField] private Shader spiritShader;
        [SerializeField] private Color ghostGlowColor = new Color(0.2f, 0.8f, 0.7f, 1f);
        [SerializeField] private float transitionSpeed = 4.0f;

        [Header("Spirit Clouds")]
        [SerializeField] private Color spiritWispColor = new Color(0.3f, 0.9f, 0.8f, 0.6f);
        [SerializeField] private float emitInterval = 0.06f;

        private DreadmaskEnemy dreadmask;
        private SpriteRenderer[] renderers;
        private Material[] spiritMaterials;
        private float currentSpirit = 0.0f;

        private void Awake()
        {
            dreadmask = GetComponent<DreadmaskEnemy>();
            renderers = dreadmask.GetVisualSpriteRenderers();

            if (spiritShader == null)
            {
                spiritShader = Shader.Find("Bao/DreadmaskSpiritShader");
            }

            SetupMaterials();
        }

        private void SetupMaterials()
        {
            if (spiritShader == null) return;

            spiritMaterials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                Material mat = new Material(spiritShader);
                mat.SetColor("_GhostGlow", ghostGlowColor);
                mat.SetFloat("_SpiritIntensity", 0.0f);

                if (renderers[i].sharedMaterial != null && renderers[i].sharedMaterial.HasProperty("_MainTex"))
                {
                    mat.SetTexture("_MainTex", renderers[i].sharedMaterial.GetTexture("_MainTex"));
                }

                renderers[i].material = mat;
                spiritMaterials[i] = mat;
            }
        }

        private void Start()
        {
            StartCoroutine(EmitSpiritWisps());
        }

        private void Update()
        {
            bool isShooting = dreadmask.StateMachine?.CurrentState is DreadmaskShootState;

            float targetSpirit = isShooting ? 8.0f : 2.0f;
            currentSpirit = Mathf.Lerp(currentSpirit, targetSpirit, Time.deltaTime * transitionSpeed);

            if (spiritMaterials != null)
            {
                foreach (var mat in spiritMaterials)
                {
                    if (mat != null)
                    {
                        mat.SetFloat("_SpiritIntensity", currentSpirit);
                    }
                }
            }

            if (isShooting && Random.Range(0, 8) == 0)
            {
                TriggerGhostlyScream();
            }
        }

        private IEnumerator EmitSpiritWisps()
        {
            while (true)
            {
                bool isShooting = dreadmask.StateMachine?.CurrentState is DreadmaskShootState;
                float baseChance = isShooting ? 1.0f : 0.35f;

                if (Random.value < baseChance)
                {
                    SpawnGhostWisp(transform.position + (Vector3)(Random.insideUnitCircle * 0.4f));
                }

                yield return new WaitForSeconds(emitInterval);
            }
        }

        private void SpawnGhostWisp(Vector3 position)
        {
            GameObject wisp = new GameObject("SpectralWisp");
            wisp.transform.SetParent(VFXContainerUtility.GetContainer());
            wisp.transform.position = position;
            wisp.transform.localScale = Vector3.one * Random.Range(0.08f, 0.18f);

            SpriteRenderer sr = wisp.AddComponent<SpriteRenderer>();
            sr.color = spiritWispColor;
            sr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));

            Texture2D tex = new Texture2D(8, 8);
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(3.5f, 3.5f));
                    float alpha = Mathf.Clamp01(1.0f - (dist / 3.5f));
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha * 0.8f));
                }
            }
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f));

            StartCoroutine(AnimateWisp(wisp, sr));
        }

        private IEnumerator AnimateWisp(GameObject wisp, SpriteRenderer sr)
        {
            float duration = Random.Range(0.5f, 0.9f);
            float elapsed = 0f;
            Vector3 drift = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0.6f, 1.3f), 0f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                wisp.transform.position += drift * Time.deltaTime;
                wisp.transform.localScale = Vector3.Lerp(wisp.transform.localScale, Vector3.zero, t);

                if (sr != null)
                {
                    sr.color = Color.Lerp(spiritWispColor, new Color(0.1f, 0.5f, 0.4f, 0f), t);
                }

                yield return null;
            }

            Destroy(wisp);
        }

        private void TriggerGhostlyScream()
        {
            Vector3 center = transform.position;
            for (int i = 0; i < 5; i++)
            {
                SpawnGhostWisp(center + (Vector3)(Random.insideUnitCircle * 0.3f));
            }
        }
    }
}
