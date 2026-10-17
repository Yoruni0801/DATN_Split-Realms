using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class WaterRefractionDeflector : MonoBehaviour, IBeamDeflector
{
    [Header("Refraction Settings")]
    [SerializeField] private float fixedBendAngle = 12f;
    [SerializeField] private bool useSnellPhysics = false;
    [SerializeField] private float refractionIndex = 1.33f;

    [Header("Laser Interaction VFX")]
    [SerializeField] private GameObject waterLaserVfxPrefab;

    [Header("Hazard Interaction")]
    [SerializeField] private FairyElement fatalToElement = FairyElement.Fire;
    [SerializeField] private FireSprayHazard fireSprayHazard;

    private SpriteRenderer _spriteRenderer;
    private MaterialPropertyBlock _block;
    private int _idLaserPoint;
    private int _idLaserDir;
    private float _lastHitTime;
    private GameObject _activeVfxInstance;
    private Rota.VFX.WaterLaserInteractionVFX _activeWaterVfx;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _block = new MaterialPropertyBlock();
        _idLaserPoint = Shader.PropertyToID("_LaserTransmissionPoint");
        _idLaserDir = Shader.PropertyToID("_LaserTransmissionDir");
    }

    public Vector3 GetDeflectDirection(Vector3 incomingDirection, Vector3 hitPoint, Vector3 hitNormal)
    {
        incomingDirection.z = 0f;
        Vector3 i = incomingDirection.normalized;

        Vector3 refractedDir;

        if (!useSnellPhysics)
        {
            float angle = -fixedBendAngle;
            Quaternion rot = Quaternion.Euler(0, 0, angle);
            refractedDir = rot * i;
            refractedDir.z = 0f;
            refractedDir = refractedDir.normalized;
        }
        else
        {
            hitNormal.z = 0f;
            float n1 = 1.0f;
            float n2 = Mathf.Max(refractionIndex, 1.0001f);
            float ratio = n1 / n2;

            Vector3 n = hitNormal.normalized;
            float cosI = -Vector3.Dot(n, i);
            float sinT2 = ratio * ratio * (1.0f - cosI * cosI);

            if (sinT2 > 1.0f)
            {
                Vector3 reflected = Vector3.Reflect(i, n);
                reflected.z = 0f;
                refractedDir = reflected.normalized;
            }
            else
            {
                float cosT = Mathf.Sqrt(1.0f - sinT2);
                refractedDir = ratio * i + (ratio * cosI - cosT) * n;
                refractedDir.z = 0f;
                refractedDir = refractedDir.normalized;
            }
        }

        // Cập nhật hiệu ứng truyền sáng Shader + VFX bốc hơi nước
        UpdateLaserTransmissionEffect(hitPoint, refractedDir);

        return refractedDir;
    }

    private void UpdateLaserTransmissionEffect(Vector3 hitPoint, Vector3 refractedDir)
    {
        _lastHitTime = Time.time;

        // 1. Cập nhật Shader Laser Transmission Glow
        if (_spriteRenderer != null)
        {
            _spriteRenderer.GetPropertyBlock(_block);
            _block.SetVector(_idLaserPoint, new Vector4(hitPoint.x, hitPoint.y, 1f, 1f));
            _block.SetVector(_idLaserDir, new Vector4(refractedDir.x, refractedDir.y, 0f, 0f));
            _spriteRenderer.SetPropertyBlock(_block);
        }

        // 2. Kích hoạt VFX bốc hơi nước tại điểm laser tiếp xúc
        if (waterLaserVfxPrefab != null)
        {
            if (_activeVfxInstance == null)
            {
                _activeVfxInstance = Instantiate(waterLaserVfxPrefab, transform);
                _activeWaterVfx = _activeVfxInstance.GetComponent<Rota.VFX.WaterLaserInteractionVFX>();
            }

            if (_activeWaterVfx != null)
            {
                _activeWaterVfx.SetPositionAndDirection(hitPoint, Vector3.zero, refractedDir);
                _activeWaterVfx.SetActive(true, 1.0f);
            }
            else
            {
                _activeVfxInstance.transform.position = hitPoint;
            }
        }
    }

    private void Update()
    {
        // Tắt hiệu ứng nếu tia laser không còn chiếu vào
        if (Time.time - _lastHitTime > 0.15f)
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.GetPropertyBlock(_block);
                _block.SetVector(_idLaserPoint, new Vector4(0f, 0f, 0f, 0f));
                _spriteRenderer.SetPropertyBlock(_block);
            }

            if (_activeWaterVfx != null)
            {
                _activeWaterVfx.SetActive(false, 0f);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other) => CheckFairyHazard(other.gameObject);
    private void OnTriggerStay2D(Collider2D other) => CheckFairyHazard(other.gameObject);
    private void OnCollisionEnter2D(Collision2D collision) => CheckFairyHazard(collision.gameObject);
    private void OnCollisionStay2D(Collision2D collision) => CheckFairyHazard(collision.gameObject);

    private void CheckFairyHazard(GameObject target)
    {
        var fairy = target.GetComponent<FairyPlayerCompanionController>();
        if (fairy == null)
            fairy = target.GetComponentInParent<FairyPlayerCompanionController>();

        if (fairy == null || !fairy.gameObject.activeInHierarchy) return;

        if (fairy.CurrentElement == fatalToElement)
        {
            if (fireSprayHazard == null)
                fireSprayHazard = FindFirstObjectByType<FireSprayHazard>();

            if (fireSprayHazard != null)
                fireSprayHazard.KillFairy(fairy);
            else
                fairy.Die();
        }
    }
}