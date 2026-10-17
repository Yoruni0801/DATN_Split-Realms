using UnityEngine;
using System.Collections;

namespace Bao.Enemies
{
    [RequireComponent(typeof(EmberwormEnemy))]
    public class EmberwormVFXController : MonoBehaviour
    {
        [Header("Shader Setup")]
        [SerializeField] private Shader magmaShader;
        [SerializeField] private Color magmaColor = new Color(1.0f, 0.25f, 0.0f, 1.0f);
        [SerializeField] private Color heatColor = new Color(1.0f, 0.9f, 0.35f, 1.0f);
        [SerializeField] private float fadeSpeed = 4.0f;

        [Header("Procedural Lava & Fire Effects")]
        [SerializeField] private Color sparkColor = new Color(1.0f, 0.6f, 0.0f, 0.8f);
        [SerializeField] private Color ashColor = new Color(0.2f, 0.15f, 0.15f, 0.6f);
        [SerializeField] private float emitInterval = 0.04f;

        private EmberwormEnemy worm;
        private SpriteRenderer[] renderers;
        private Material[] magmaMaterials;
        private float currentHeat = 0.0f;

        private void Awake()
        {
            worm = GetComponent<EmberwormEnemy>();
            renderers = worm.GetVisualSpriteRenderers();

            if (magmaShader == null)
            {
                magmaShader = Shader.Find("Bao/SuperheatedMagmaShader");
            }

            SetupMaterials();
        }

        private void SetupMaterials()
        {
            if (magmaShader == null)
            {
                Debug.LogWarning("SuperheatedMagmaShader not found! Defaulting to Sprites/Default.");
                return;
            }

            magmaMaterials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                Material mat = new Material(magmaShader);
                mat.SetColor("_MagmaColor", magmaColor);
                mat.SetColor("_WhiteHotColor", heatColor);
                mat.SetFloat("_HeatStrength", 0.0f);

                if (renderers[i].sharedMaterial != null && renderers[i].sharedMaterial.HasProperty("_MainTex"))
                {
                    mat.SetTexture("_MainTex", renderers[i].sharedMaterial.GetTexture("_MainTex"));
                }

                renderers[i].material = mat;
                magmaMaterials[i] = mat;
            }
        }

        private void Start()
        {
            StartCoroutine(EmitEffectsLoop());
        }

        private void Update()
        {
            if (worm.StateMachine == null) return;

            var currentState = worm.StateMachine.CurrentState;
            float targetHeat = 0.2f; // Có ánh sáng magma cực kỳ nhẹ dịu dưới hố để nhận diện vị trí

            if (currentState is EmberwormEmergeState)
            {
                targetHeat = 0.35f; // Trồi lên ấm áp dịu dàng
            }
            else if (currentState is EmberwormActiveState)
            {
                targetHeat = 0.45f; // Rực sáng mạch dung nham rất nhẹ, tôn vinh trọn vẹn nét vẽ gốc của sprite
            }
            else if (currentState is EmberwormRetractState)
            {
                targetHeat = 0.3f; // Chui xuống nguội dần dịu nhẹ
            }

            currentHeat = Mathf.Lerp(currentHeat, targetHeat, Time.deltaTime * fadeSpeed);

            if (magmaMaterials != null)
            {
                foreach (var mat in magmaMaterials)
                {
                    if (mat != null)
                    {
                        mat.SetFloat("_HeatStrength", currentHeat);
                    }
                }
            }
        }

        private IEnumerator EmitEffectsLoop()
        {
            while (true)
            {
                if (worm.StateMachine != null)
                {
                    var currentState = worm.StateMachine.CurrentState;

                    if (currentState is EmberwormEmergeState)
                    {
                        // Erupt lava splashes and ash clouds at base as it emerges
                        SpawnSparks(transform.position + new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(0f, 1f), 0f), 2);
                        if (Random.value < 0.3f) SpawnAsh(transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0f, 0f));
                    }
                    else if (currentState is EmberwormActiveState)
                    {
                        // Gentle ambient flame sparks and heat vents
                        if (Random.value < 0.4f)
                        {
                            SpawnSparks(transform.position + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(0f, 6f), 0f), 1);
                        }
                    }
                    else if (currentState is EmberwormRetractState)
                    {
                        // Large splash when retracting back into base
                        SpawnSparks(transform.position + new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(0f, 1f), 0f), 3);
                        if (Random.value < 0.5f) SpawnAsh(transform.position + new Vector3(Random.Range(-0.6f, 0.6f), 0f, 0f));
                    }
                }

                yield return new WaitForSeconds(emitInterval);
            }
        }

        private void SpawnSparks(Vector3 position, int count)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject spark = new GameObject("MagmaSpark");
                spark.transform.SetParent(VFXContainerUtility.GetContainer());
                spark.transform.position = position;
                spark.transform.localScale = Vector3.one * Random.Range(0.08f, 0.18f);

                SpriteRenderer sr = spark.AddComponent<SpriteRenderer>();
                sr.color = sparkColor;
                sr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));

                // Soft circular texture
                Texture2D tex = new Texture2D(4, 4);
                for (int y = 0; y < 4; y++)
                {
                    for (int x = 0; x < 4; x++)
                    {
                        tex.SetPixel(x, y, Color.white);
                    }
                }
                tex.Apply();
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));

                float angle = Random.Range(45f, 135f) * Mathf.Deg2Rad;
                Vector3 velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Random.Range(2f, 4.5f);

                StartCoroutine(AnimateSpark(spark, sr, velocity));
            }
        }

        private IEnumerator AnimateSpark(GameObject spark, SpriteRenderer sr, Vector3 velocity)
        {
            float duration = Random.Range(0.4f, 0.7f);
            float elapsed = 0f;
            Vector3 curVel = velocity;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                curVel.y -= 3f * Time.deltaTime; // slight gravity pull
                spark.transform.position += curVel * Time.deltaTime;
                spark.transform.localScale = Vector3.Lerp(spark.transform.localScale, Vector3.zero, t);

                if (sr != null)
                {
                    sr.color = Color.Lerp(sparkColor, new Color(1f, 0.2f, 0f, 0f), t);
                }

                yield return null;
            }

            Destroy(spark);
        }

        private void SpawnAsh(Vector3 position)
        {
            GameObject ash = new GameObject("VolcanicAsh");
            ash.transform.SetParent(VFXContainerUtility.GetContainer());
            ash.transform.position = position;
            ash.transform.localScale = Vector3.one * Random.Range(0.2f, 0.5f);

            SpriteRenderer sr = ash.AddComponent<SpriteRenderer>();
            sr.color = ashColor;
            sr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));

            Texture2D tex = new Texture2D(8, 8);
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(3.5f, 3.5f));
                    float alpha = Mathf.Clamp01(1.0f - (d / 3.5f));
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha * 0.7f));
                }
            }
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f));

            StartCoroutine(AnimateAsh(ash, sr));
        }

        private IEnumerator AnimateAsh(GameObject ash, SpriteRenderer sr)
        {
            float duration = Random.Range(0.8f, 1.4f);
            float elapsed = 0f;
            Vector3 drift = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.6f, 1.3f), 0f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                ash.transform.position += drift * Time.deltaTime;
                ash.transform.localScale = Vector3.Lerp(ash.transform.localScale, Vector3.one * 0.8f, t);

                if (sr != null)
                {
                    sr.color = Color.Lerp(ashColor, new Color(0.1f, 0.1f, 0.1f, 0f), t);
                }

                yield return null;
            }

            Destroy(ash);
        }
    }
}
