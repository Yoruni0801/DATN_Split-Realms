using UnityEngine;
using System.Collections;

namespace Bao.Enemies
{
    [RequireComponent(typeof(FungloomEnemy))]
    public class FungloomVFXController : MonoBehaviour
    {
        [Header("Shader Setup")]
        [SerializeField] private Shader poisonShader;
        [SerializeField] private Color toxicColor1 = new Color(0.1f, 0.9f, 0.3f, 1.0f);   // Xanh lá độc bảo hòa
        [SerializeField] private Color toxicColor2 = new Color(0.65f, 0.1f, 0.85f, 1.0f); // Tím độc huyền ảo
        [SerializeField] private float veinScale = 12.0f;
        [SerializeField] private float veinSpeed = 2.5f;
        [SerializeField] private float fadeSpeed = 5.0f;

        [Header("Toxic Spore Particles")]
        [SerializeField] private Color greenSporeColor = new Color(0.35f, 0.9f, 0.45f, 0.6f);
        [SerializeField] private Color purpleSporeColor = new Color(0.7f, 0.2f, 0.9f, 0.55f);
        [SerializeField] private float emitInterval = 0.08f;

        private FungloomEnemy fungloom;
        private SpriteRenderer[] renderers;
        private Material[] poisonMaterials;
        private float currentChargeGlow = 0.0f;

        private void Awake()
        {
            fungloom = GetComponent<FungloomEnemy>();
            
            // Get all SpriteRenderers but exclude "ChamThan" (the warning mark) and its children
            renderers = fungloom.GetVisualSpriteRenderers();

            if (poisonShader == null)
            {
                poisonShader = Shader.Find("Bao/PoisonMossShader");
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
            if (poisonShader == null)
            {
                Debug.LogWarning("PoisonMossShader not found! Defaulting to Sprites/Default.");
                return;
            }

            poisonMaterials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                Material mat = new Material(poisonShader);
                mat.SetColor("_ToxicColor1", toxicColor1);
                mat.SetColor("_ToxicColor2", toxicColor2);
                mat.SetFloat("_VeinScale", veinScale);
                mat.SetFloat("_VeinSpeed", veinSpeed);
                mat.SetFloat("_TriggerGlow", 0.0f);

                if (renderers[i].sharedMaterial != null && renderers[i].sharedMaterial.HasProperty("_MainTex"))
                {
                    mat.SetTexture("_MainTex", renderers[i].sharedMaterial.GetTexture("_MainTex"));
                }

                renderers[i].material = mat;
                poisonMaterials[i] = mat;
            }
        }

        private void Start()
        {
            StartCoroutine(EmitToxicSporesLoop());
        }

        private void Update()
        {
            if (fungloom.StateMachine == null) return;

            bool isShooting = fungloom.StateMachine.CurrentState is FungloomShootState;

            // Khi bắn, rực sáng nhẹ mạch độc tố (1.5f), bình thường thì mạch chảy dịu nhẹ (0.0f)
            float targetGlow = isShooting ? 1.5f : 0.0f;
            currentChargeGlow = Mathf.Lerp(currentChargeGlow, targetGlow, Time.deltaTime * fadeSpeed);

            if (poisonMaterials != null)
            {
                foreach (var mat in poisonMaterials)
                {
                    if (mat != null)
                    {
                        mat.SetFloat("_TriggerGlow", currentChargeGlow);
                    }
                }
            }

            // Kích hoạt vụ nổ bụi độc khi bắn
            if (isShooting && currentChargeGlow > 1.0f && Random.Range(0, 30) == 0 && fungloom.ShootPoint != null)
            {
                TriggerPollenBurst(fungloom.ShootPoint.position);
            }
        }

        private IEnumerator EmitToxicSporesLoop()
        {
            while (true)
            {
                if (fungloom.StateMachine != null)
                {
                    bool isShooting = fungloom.StateMachine.CurrentState is FungloomShootState;

                    // Thả các hạt bào tử trôi nổi huyền ảo quanh nấm
                    if (Random.Range(0, 2) == 0)
                    {
                        Vector3 offset = (Vector3)(Random.insideUnitCircle * 0.45f) + new Vector3(0f, 0.1f, 0f);
                        Color pickedColor = (Random.value < 0.5f) ? greenSporeColor : purpleSporeColor;
                        SpawnSpore(transform.position + offset, pickedColor, isShooting ? 1.8f : 0.8f);
                    }
                }

                yield return new WaitForSeconds(emitInterval);
            }
        }

        private void SpawnSpore(Vector3 position, Color color, float speedScale)
        {
            GameObject spore = new GameObject("FloatingToxicSpore");
            spore.transform.SetParent(VFXContainerUtility.GetContainer());
            spore.transform.position = position;
            spore.transform.localScale = Vector3.one * Random.Range(0.08f, 0.16f);

            SpriteRenderer sr = spore.AddComponent<SpriteRenderer>();
            sr.color = color;
            sr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));

            // Fuzzy spore particle
            Texture2D tex = new Texture2D(8, 8);
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(3.5f, 3.5f));
                    float alpha = Mathf.Clamp01(1.0f - (dist / 3.5f));
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f));

            StartCoroutine(AnimateSpore(spore, sr, color, speedScale));
        }

        private IEnumerator AnimateSpore(GameObject spore, SpriteRenderer sr, Color color, float speedScale)
        {
            float duration = Random.Range(0.7f, 1.4f);
            float elapsed = 0f;
            Vector3 floatVector = new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(0.35f, 0.8f) * speedScale, 0f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                spore.transform.position += floatVector * Time.deltaTime;
                spore.transform.localScale = Vector3.Lerp(spore.transform.localScale, Vector3.zero, t);

                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = Mathf.Lerp(color.a, 0f, t);
                    sr.color = c;
                }

                yield return null;
            }

            Destroy(spore);
        }

        public void TriggerPollenBurst(Vector3 shootPos)
        {
            int sporeCount = 15;
            for (int i = 0; i < sporeCount; i++)
            {
                GameObject pollen = new GameObject("ToxicPollen");
                pollen.transform.SetParent(VFXContainerUtility.GetContainer());
                pollen.transform.position = shootPos;
                pollen.transform.localScale = Vector3.one * Random.Range(0.06f, 0.12f);

                SpriteRenderer sr = pollen.AddComponent<SpriteRenderer>();
                Color burstColor = (Random.value < 0.5f) ? greenSporeColor : purpleSporeColor;
                sr.color = burstColor;
                sr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));

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

                float angle = Random.Range(-45f, 45f) * Mathf.Deg2Rad;
                float dirX = -transform.localScale.x * Mathf.Cos(angle);
                float dirY = Mathf.Sin(angle);
                Vector3 force = new Vector3(dirX, dirY, 0f) * Random.Range(1.5f, 3.5f);

                StartCoroutine(AnimatePollen(pollen, sr, burstColor, force));
            }
        }

        private IEnumerator AnimatePollen(GameObject pollen, SpriteRenderer sr, Color color, Vector3 velocity)
        {
            float duration = Random.Range(0.4f, 0.6f);
            float elapsed = 0f;
            Vector3 currentVel = velocity;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                currentVel = Vector3.Lerp(currentVel, Vector3.zero, t * 2.0f);
                pollen.transform.position += currentVel * Time.deltaTime;
                pollen.transform.localScale = Vector3.Lerp(pollen.transform.localScale, Vector3.zero, t);

                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = Mathf.Lerp(color.a, 0f, t);
                    sr.color = c;
                }

                yield return null;
            }

            Destroy(pollen);
        }
    }
}
