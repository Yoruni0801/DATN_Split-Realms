using UnityEngine;
using System.Collections;

namespace Bao.Enemies
{
    [RequireComponent(typeof(RavagerEnemy))]
    public class RavagerVFXController : MonoBehaviour
    {
        [Header("Shader Settings")]
        [SerializeField] private Shader windShader;
        [SerializeField] private Color windColor = new Color(0.5f, 0.85f, 1.0f, 1.0f);
        [SerializeField] private float fadeSpeed = 5.0f;

        [Header("Dive / Flight Particles")]
        [SerializeField] private Color windParticleColor = new Color(0.9f, 0.95f, 1.0f, 0.6f);
        [SerializeField] private float emitRate = 0.03f;

        private RavagerEnemy ravager;
        private SpriteRenderer[] renderers;
        private Material[] windMaterials;
        private float currentWindPower = 0.0f;
        private Vector3 prevPosition;

        private void Awake()
        {
            ravager = GetComponent<RavagerEnemy>();
            renderers = ravager.GetVisualSpriteRenderers();

            if (windShader == null)
            {
                windShader = Shader.Find("Bao/SupersonicWindShader");
            }

            SetupMaterials();
        }

        private void SetupMaterials()
        {
            if (windShader == null)
            {
                Debug.LogWarning("SupersonicWindShader not found! Please assign in the inspector.");
                return;
            }

            windMaterials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                Material mat = new Material(windShader);
                mat.SetColor("_WindColor", windColor);
                mat.SetFloat("_WindPower", 0.0f);

                if (renderers[i].sharedMaterial != null && renderers[i].sharedMaterial.HasProperty("_MainTex"))
                {
                    mat.SetTexture("_MainTex", renderers[i].sharedMaterial.GetTexture("_MainTex"));
                }

                renderers[i].material = mat;
                windMaterials[i] = mat;
            }
        }

        private void Start()
        {
            prevPosition = transform.position;
            StartCoroutine(EmitWindParticles());
        }

        private void Update()
        {
            // Detect active flying or diving state
            bool isFlyingOrDiving = ravager.StateMachine?.CurrentState is RavagerFlyState ||
                                    ravager.StateMachine?.CurrentState is RavagerDiveState;

            float targetPower = isFlyingOrDiving ? 1.0f : 0.0f;
            currentWindPower = Mathf.Lerp(currentWindPower, targetPower, Time.deltaTime * fadeSpeed);

            if (windMaterials != null)
            {
                foreach (var mat in windMaterials)
                {
                    if (mat != null)
                    {
                        mat.SetFloat("_WindPower", currentWindPower);
                    }
                }
            }
        }

        private IEnumerator EmitWindParticles()
        {
            while (true)
            {
                bool isDiving = ravager.StateMachine?.CurrentState is RavagerDiveState;
                bool isFlying = ravager.StateMachine?.CurrentState is RavagerFlyState;

                if ((isDiving || isFlying) && Vector3.Distance(transform.position, prevPosition) > 0.06f)
                {
                    int spawnCount = isDiving ? 3 : 1;
                    for (int i = 0; i < spawnCount; i++)
                    {
                        CreateWindStreak(transform.position);
                    }
                    prevPosition = transform.position;
                }

                yield return new WaitForSeconds(emitRate);
            }
        }

        private void CreateWindStreak(Vector3 position)
        {
            // Programmatically spawn lightweight wind sweep trail elements
            GameObject streak = new GameObject("WindStreak");
            streak.transform.SetParent(VFXContainerUtility.GetContainer());
            streak.transform.position = position + (Vector3)(Random.insideUnitCircle * 0.4f);
            
            // Random horizontal/vertical scale to make them look like wind streaks
            streak.transform.localScale = new Vector3(Random.Range(0.6f, 1.2f), Random.Range(0.04f, 0.08f), 1f);

            // Rotate parallel to movement direction or randomly horizontal
            float angle = Random.Range(-10f, 10f);
            streak.transform.rotation = Quaternion.Euler(0, 0, angle);

            SpriteRenderer sr = streak.AddComponent<SpriteRenderer>();
            sr.color = windParticleColor;
            sr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));

            // Simple 8x2 wind stripe texture
            Texture2D tex = new Texture2D(8, 2);
            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    float factor = 1.0f - Mathf.Abs(x - 3.5f) / 3.5f;
                    tex.SetPixel(x, y, new Color(1, 1, 1, factor));
                }
            }
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 8, 2), new Vector2(0.5f, 0.5f));

            StartCoroutine(AnimateStreak(streak, sr));
        }

        private IEnumerator AnimateStreak(GameObject streak, SpriteRenderer sr)
        {
            float duration = Random.Range(0.2f, 0.4f);
            float elapsed = 0f;
            Vector3 startScale = streak.transform.localScale;

            // Blow opposite to movement or expand horizontally
            Vector3 movement = new Vector3(ravager.transform.localScale.x * Random.Range(-1.5f, -0.5f), Random.Range(-0.2f, 0.2f), 0f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                streak.transform.position += movement * Time.deltaTime;
                streak.transform.localScale = Vector3.Lerp(startScale, new Vector3(0f, startScale.y, 1f), t);

                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = Mathf.Lerp(windParticleColor.a, 0f, t);
                    sr.color = c;
                }

                yield return null;
            }

            Destroy(streak);
        }
    }
}
