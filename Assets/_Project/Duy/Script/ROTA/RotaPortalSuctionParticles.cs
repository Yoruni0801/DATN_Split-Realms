using UnityEngine;

[DisallowMultipleComponent]
public class RotaPortalSuctionParticles : MonoBehaviour
{
    [Header("Suction Settings")]
    [SerializeField] private float outerRadius = 2.4f;
    [SerializeField] private float suctionSpeed = 2.6f;
    [SerializeField] private float orbitSpeed = 3.2f;
    [SerializeField] private int particleRate = 18;

    [Header("Color / Material")]
    [SerializeField] private Material suctionMaterial;
    [SerializeField] private Color particleColor = Color.white;

    [Header("Proximity Boost")]
    [SerializeField] private float proximityRange = 4.0f;
    [SerializeField] private float maxProximityMultiplier = 1.8f;

    private ParticleSystem suctionPs;
    private Transform playerTransform;
    private Transform visualRoot;
    private SpriteRenderer spriteRenderer;
    private float lastSpeedMultiplier = 1.0f;
    private bool isCurrentlyEmitting;

    private void Awake()
    {
        SetupVisualRootAndRenderer();
        SetupParticleSystem();
    }

    private void SetupVisualRootAndRenderer()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        BlackHolePortalEffect2D effect = GetComponent<BlackHolePortalEffect2D>();
        if (effect != null)
        {
            // Tìm visual root nếu có
            visualRoot = transform;
        }
        else
        {
            visualRoot = transform;
        }
    }

    private void OnEnable()
    {
        RotaSurfaceZone.PlayerEnteredAnyZone += HandlePlayerEntered;
        RotaSurfaceZone.PlayerExitedAllZones += HandlePlayerExited;
    }

    private void OnDisable()
    {
        RotaSurfaceZone.PlayerEnteredAnyZone -= HandlePlayerEntered;
        RotaSurfaceZone.PlayerExitedAllZones -= HandlePlayerExited;
        playerTransform = null;
        if (suctionPs != null)
        {
            suctionPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            isCurrentlyEmitting = false;
        }
    }

    private void HandlePlayerEntered(Player player)
    {
        if (player != null) playerTransform = player.transform;
    }

    private void HandlePlayerExited()
    {
        playerTransform = null;
    }

    private void SetupParticleSystem()
    {
        GameObject psObj = new GameObject("PortalSuction_VFX");
        psObj.transform.SetParent(transform, worldPositionStays: false);
        psObj.transform.localPosition = Vector3.zero;
        psObj.transform.localRotation = Quaternion.identity;
        psObj.transform.localScale = Vector3.one;

        suctionPs = psObj.AddComponent<ParticleSystem>();
        suctionPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystemRenderer psr = psObj.GetComponent<ParticleSystemRenderer>();

        if (suctionMaterial != null)
        {
            psr.material = suctionMaterial;
        }
        else if (spriteRenderer != null && spriteRenderer.sharedMaterial != null)
        {
            psr.material = spriteRenderer.sharedMaterial;
        }

        psr.sortingLayerID = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
        psr.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder + 1 : 25;

        var main = suctionPs.main;
        main.duration = 4.0f;
        main.loop = true;
        main.prewarm = false;
        main.startLifetime = 0.9f;
        main.startSpeed = -suctionSpeed;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
        main.startColor = particleColor;
        main.playOnAwake = false;

        var emission = suctionPs.emission;
        emission.rateOverTime = particleRate;

        var shape = suctionPs.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = outerRadius;
        shape.radiusThickness = 0.2f;

        var vel = suctionPs.velocityOverLifetime;
        vel.enabled = true;
        vel.orbitalZ = new ParticleSystem.MinMaxCurve(orbitSpeed);

        var colorOverLife = suctionPs.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(particleColor, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(1.0f, 0.3f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        colorOverLife.color = grad;

        var sizeOverLife = suctionPs.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve sc = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f);
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sc);

        // Kiểm tra ban đầu xem cổng có đang mở không
        UpdateGateVisibilityState(true);
    }

    private void Update()
    {
        if (suctionPs == null) return;

        UpdateGateVisibilityState(false);

        if (!isCurrentlyEmitting) return;

        // Proximity boost khi player lại gần
        float multiplier = 1.0f;
        if (playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist <= proximityRange)
            {
                float t = 1.0f - Mathf.Clamp01(dist / proximityRange);
                multiplier = Mathf.Lerp(1.0f, maxProximityMultiplier, t);
            }
        }

        if (Mathf.Abs(multiplier - lastSpeedMultiplier) > 0.05f)
        {
            lastSpeedMultiplier = multiplier;
            var main = suctionPs.main;
            main.simulationSpeed = multiplier;
        }
    }

    private void UpdateGateVisibilityState(bool force)
    {
        // Kiểm tra xem cổng có đang hiển thị hay đã thu nhỏ/ẩn đi (scale = 0 hoặc spriteRenderer tắt)
        bool isGateVisible = true;
        if (transform.localScale.sqrMagnitude < 0.01f)
        {
            isGateVisible = false;
        }
        else if (spriteRenderer != null && (!spriteRenderer.enabled || spriteRenderer.color.a < 0.05f))
        {
            isGateVisible = false;
        }

        if (isGateVisible != isCurrentlyEmitting || force)
        {
            isCurrentlyEmitting = isGateVisible;
            if (isCurrentlyEmitting)
            {
                suctionPs.Play();
            }
            else
            {
                // Khi cổng đóng lại -> Dừng phát hạt và xóa ngay các hạt còn lại để biến mất đồng thời với cổng
                suctionPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
