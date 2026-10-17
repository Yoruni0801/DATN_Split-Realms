using UnityEngine;

namespace Bao.Bosses.QuyVuongPhase4
{
    public class Phase4SlashVFX : MonoBehaviour
    {
        [SerializeField] private float duration = 0.5f;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Vector3 startScale = new Vector3(1.2f, 1.2f, 1f);
        [SerializeField] private Vector3 endScale = new Vector3(1.8f, 1.8f, 1f);
        [SerializeField] private Color startColor = new Color(1f, 0.95f, 0.7f, 1f);
        [SerializeField] private Color endColor = new Color(1f, 0.3f, 0f, 0f);

        private float elapsed = 0f;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        private void Start()
        {
            transform.localScale = startScale;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = startColor;
                spriteRenderer.sortingOrder = 25;
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            transform.localScale = Vector3.Lerp(startScale, endScale, t);

            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.Lerp(startColor, endColor, t);
            }

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
