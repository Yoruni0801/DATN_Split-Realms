using UnityEngine;

namespace Bao.Bosses
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class AcidWarningIndicator : MonoBehaviour
    {
        [Header("Warning Settings")]
        [SerializeField, Min(0.1f)] private float duration = 1.5f;
        [SerializeField, Min(0.25f)] private float maxRadius = 1.2f;
        [ColorUsage(true, true)]
        public Color warningColor = new Color(0.2f, 2.5f, 0.2f, 0.8f);

        private SpriteRenderer legacyRenderer;
        private NidusBurrowGroundVFX warningVisual;
        private float timer;
        private bool initialized;

        private void Awake()
        {
            legacyRenderer = GetComponent<SpriteRenderer>();
            legacyRenderer.enabled = false;
        }

        private void Start()
        {
            if (!initialized)
                BuildVisual();
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer >= duration)
                Destroy(gameObject);
        }

        public void Initialize(float warningDuration, float radius)
        {
            duration = Mathf.Max(0.1f, warningDuration);
            maxRadius = Mathf.Max(0.25f, radius);
            timer = 0f;
            initialized = true;
            BuildVisual();
        }

        private void BuildVisual()
        {
            if (warningVisual != null)
                Destroy(warningVisual.gameObject);

            warningVisual = ProceduralDirtVFX.SpawnWarning(
                transform.position,
                maxRadius,
                duration,
                warningColor);
            warningVisual.transform.SetParent(transform, true);
        }
    }
}
