using UnityEngine;
using System.Collections;

namespace Bao.Enemies
{
    [RequireComponent(typeof(MoltenfishEnemy))]
    public class MoltenfishVFXController : MonoBehaviour
    {
        [Header("Shader Setup")]
        [SerializeField] private Shader magmaShader;
        [SerializeField] private Color magmaColor = new Color(1.0f, 0.3f, 0.0f, 1.0f); // Bright orange/red
        [SerializeField] private Color heatColor = new Color(1.0f, 0.9f, 0.3f, 1.0f); // Yellow/White hot
        [SerializeField] private float fadeSpeed = 8.0f;

        private MoltenfishEnemy moltenfish;
        private SpriteRenderer[] renderers;
        private Material[] magmaMaterials;
        private float currentHeat = 0.0f;

        // Particle Assets
        private Sprite circleSprite;
        private Sprite dropletSprite;
        private Material particleMaterial;

        private void Awake()
        {
            moltenfish = GetComponent<MoltenfishEnemy>();
            renderers = moltenfish.GetVisualSpriteRenderers();

            if (magmaShader == null)
            {
                magmaShader = Shader.Find("Bao/SuperheatedMagmaShader");
            }

            GenerateVFXAssets();
            SetupMaterials();
        }

        private void GenerateVFXAssets()
        {
            // Create a soft circle sprite for sparks/shockwaves
            Texture2D circleTex = new Texture2D(16, 16);
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(7.5f, 7.5f));
                    float alpha = Mathf.Clamp01(1.0f - (dist / 7.5f));
                    circleTex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }
            circleTex.Apply();
            circleSprite = Sprite.Create(circleTex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f));

            // Create a stretched droplet sprite for splashes
            Texture2D dropTex = new Texture2D(8, 16);
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    float yFactor = (float)y / 15f; 
                    float width = Mathf.Lerp(3f, 0f, yFactor); 
                    float dist = Mathf.Abs(x - 3.5f);
                    float alpha = dist < width ? 1f : 0f;
                    dropTex.SetPixel(x, y, new Color(1, 1, 1, alpha * (1f - yFactor * 0.5f)));
                }
            }
            dropTex.Apply();
            dropletSprite = Sprite.Create(dropTex, new Rect(0, 0, 8, 16), new Vector2(0.5f, 0.2f));

            Shader addShader = Shader.Find("Sprites/Default");
            particleMaterial = new Material(addShader);
        }

        private void SetupMaterials()
        {
            if (magmaShader == null) return;
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

        private void Update()
        {
            if (moltenfish == null || moltenfish.StateMachine == null) return;

            bool isJumping = moltenfish.StateMachine.CurrentState is MoltenfishJumpState;
            float targetHeat = 0.0f; // Default heat when swimming

            if (isJumping && moltenfish.Rigidbody2D != null)
            {
                // Glow gets hotter as it reaches the peak, and turns bright on the way down
                float velocityY = moltenfish.Rigidbody2D.linearVelocity.y;
                targetHeat = velocityY > 0 ? 1.5f : 2.5f; // Falling is hotter (Meteor effect) but clamped so it isn't completely blown out

                // Spawn blazing fire trail
                SpawnTrailParticle(transform.position);
            }

            currentHeat = Mathf.Lerp(currentHeat, targetHeat, Time.deltaTime * fadeSpeed);
            if (magmaMaterials != null)
            {
                foreach (var mat in magmaMaterials)
                {
                    if (mat != null) mat.SetFloat("_HeatStrength", currentHeat);
                }
            }
        }

        public void TriggerEruption(Vector3 position)
        {
            // Lava explosion when jumping out
            SpawnShockwave(position, magmaColor, 1.5f);
            for (int i = 0; i < 15; i++)
            {
                Vector3 vel = new Vector3(Random.Range(-2f, 2f), Random.Range(4f, 8f), 0f);
                SpawnDroplet(position, vel, magmaColor);
            }
        }

        public void TriggerPlungeImpact(Vector3 position)
        {
            // Massive splash when diving back in
            SpawnShockwave(position, heatColor, 2.5f);
            SpawnShockwave(position, magmaColor, 1.8f);
            for (int i = 0; i < 25; i++)
            {
                // Wide splash arc
                float angle = Random.Range(10f, 170f) * Mathf.Deg2Rad;
                Vector3 vel = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Random.Range(5f, 12f);
                SpawnDroplet(position, vel, Random.value > 0.5f ? heatColor : magmaColor);
            }
        }

        private void SpawnTrailParticle(Vector3 pos)
        {
            GameObject spark = new GameObject("TrailSpark");
            spark.transform.SetParent(VFXContainerUtility.GetContainer());
            // Randomize position slightly around the fish
            spark.transform.position = pos + (Vector3)(Random.insideUnitCircle * 0.4f);
            spark.transform.localScale = Vector3.one * Random.Range(0.2f, 0.5f);

            SpriteRenderer sr = spark.AddComponent<SpriteRenderer>();
            sr.sprite = circleSprite;
            sr.material = particleMaterial;
            sr.color = Random.value > 0.6f ? heatColor : magmaColor;
            sr.sortingOrder = 150;

            StartCoroutine(AnimateSpark(spark, sr));
        }

        private IEnumerator AnimateSpark(GameObject spark, SpriteRenderer sr)
        {
            float duration = Random.Range(0.3f, 0.6f);
            float elapsed = 0f;
            Vector3 startScale = spark.transform.localScale;
            Color startColor = sr.color;
            Color endColor = new Color(1f, 0f, 0f, 0f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Float slightly upwards
                spark.transform.position += Vector3.up * (Time.deltaTime * 1.5f);
                spark.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                sr.color = Color.Lerp(startColor, endColor, t);
                yield return null;
            }
            Destroy(spark);
        }

        private void SpawnDroplet(Vector3 pos, Vector3 velocity, Color color)
        {
            GameObject drop = new GameObject("LavaDroplet");
            drop.transform.SetParent(VFXContainerUtility.GetContainer());
            drop.transform.position = pos;
            drop.transform.localScale = Vector3.one * Random.Range(0.2f, 0.4f);

            SpriteRenderer sr = drop.AddComponent<SpriteRenderer>();
            sr.sprite = dropletSprite;
            sr.material = particleMaterial;
            sr.color = color;
            sr.sortingOrder = 151;

            StartCoroutine(AnimateDroplet(drop, sr, velocity));
        }

        private IEnumerator AnimateDroplet(GameObject drop, SpriteRenderer sr, Vector3 velocity)
        {
            float duration = Random.Range(0.5f, 0.9f);
            float elapsed = 0f;
            Color startColor = sr.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                velocity.y -= 15f * Time.deltaTime; // Heavy gravity
                drop.transform.position += velocity * Time.deltaTime;
                
                // Point droplet in direction of travel
                if (velocity != Vector3.zero)
                {
                    float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
                    drop.transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
                }

                drop.transform.localScale = Vector3.Lerp(Vector3.one * 0.3f, Vector3.zero, t);
                sr.color = Color.Lerp(startColor, new Color(0.5f, 0f, 0f, 0f), t * t);
                yield return null;
            }
            Destroy(drop);
        }

        private void SpawnShockwave(Vector3 pos, Color color, float maxScale)
        {
            GameObject wave = new GameObject("LavaShockwave");
            wave.transform.SetParent(VFXContainerUtility.GetContainer());
            wave.transform.position = pos;
            wave.transform.localScale = Vector3.zero;

            SpriteRenderer sr = wave.AddComponent<SpriteRenderer>();
            sr.sprite = circleSprite;
            sr.material = particleMaterial;
            sr.color = color;
            sr.sortingOrder = 149;

            StartCoroutine(AnimateShockwave(wave, sr, maxScale));
        }

        private IEnumerator AnimateShockwave(GameObject wave, SpriteRenderer sr, float maxScale)
        {
            float duration = 0.4f;
            float elapsed = 0f;
            Color startColor = sr.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Fast expansion, smooth slowdown
                float easeOut = 1f - Mathf.Pow(1f - t, 3f);
                wave.transform.localScale = new Vector3(easeOut * maxScale, easeOut * maxScale * 0.3f, 1f); // Flattened ring
                sr.color = Color.Lerp(startColor, new Color(1f, 0.2f, 0f, 0f), t);
                
                yield return null;
            }
            Destroy(wave);
        }
    }
}
