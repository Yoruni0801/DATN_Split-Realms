using System.Collections;
using UnityEngine;

namespace Bao.Bosses.QuyVuongPhase4
{
    /// <summary>
    /// Quản lý từng đốt nứt vỡ địa chấn 3D (3D Ruptured Rock Segment)
    /// Trồi lên từ lòng đất khi boss cào qua, rực sáng lõi dung nham HDR, sau đó nguội đen thành đá Obsidian và chìm dần.
    /// </summary>
    public class Phase4MagmaTrenchSegment : MonoBehaviour
    {
        [Header("3D Meshes & Renderers")]
        [SerializeField] private MeshRenderer[] rockRenderers;
        [SerializeField] private ParticleSystem localFlamePS;
        [SerializeField] private ParticleSystem localSparksPS;

        private MaterialPropertyBlock propBlock;
        private Vector3 startLocalPos;
        private Vector3 targetLocalPos;
        private Quaternion targetRotation;
        private float coolingLifetime = 1.6f;

        private void Awake()
        {
            propBlock = new MaterialPropertyBlock();
            if (rockRenderers == null || rockRenderers.Length == 0)
            {
                rockRenderers = GetComponentsInChildren<MeshRenderer>();
            }
        }

        public void InitializeSegment(float lifetime, float randomTiltAngle)
        {
            coolingLifetime = lifetime;
            targetLocalPos = transform.localPosition;
            startLocalPos = targetLocalPos + new Vector3(0f, -0.6f, 0f);
            transform.localPosition = startLocalPos;

            targetRotation = transform.localRotation * Quaternion.Euler(
                Random.Range(-5f, 5f),
                Random.Range(-10f, 10f),
                randomTiltAngle
            );

            StartCoroutine(EruptAndCoolRoutine());
        }

        private IEnumerator EruptAndCoolRoutine()
        {
            // 1. Giai đoạn Trồi Lên Cực Nhanh (Eruption Rise: 0.08s)
            float eruptDuration = 0.08f;
            float elapsed = 0f;

            Color whiteHotHDR = new Color(2.5f, 1.8f, 0.9f, 1f);
            Color moltenOrangeHDR = new Color(1.8f, 0.65f, 0.1f, 1f);
            Color coolingCrimson = new Color(0.8f, 0.15f, 0.05f, 1f);
            Color charredObsidian = new Color(0.1f, 0.05f, 0.05f, 1f);

            while (elapsed < eruptDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / eruptDuration);
                // Ease-out cubic
                float smoothT = 1f - Mathf.Pow(1f - t, 3f);

                transform.localPosition = Vector3.Lerp(startLocalPos, targetLocalPos, smoothT);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, smoothT);

                SetEmissionColor(whiteHotHDR);
                yield return null;
            }

            transform.localPosition = targetLocalPos;
            transform.localRotation = targetRotation;

            // 2. Giai đoạn Sôi & Nguội Dần theo chu trình nhiệt độ (Cooling Cycle)
            elapsed = 0f;
            Vector3 finalScale = transform.localScale;

            while (elapsed < coolingLifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / coolingLifetime;

                Color currentGlow;
                if (t < 0.25f)
                {
                    // Giữ nhiệt dung nham rực cháy (White-Hot -> Molten Orange)
                    currentGlow = Color.Lerp(whiteHotHDR, moltenOrangeHDR, t / 0.25f);
                }
                else if (t < 0.65f)
                {
                    // Chuyển sang đỏ đậm nguội (Molten Orange -> Cooling Crimson)
                    currentGlow = Color.Lerp(moltenOrangeHDR, coolingCrimson, (t - 0.25f) / 0.4f);
                }
                else
                {
                    // Đóng băng thành đá obsidian đen (Cooling Crimson -> Charred Obsidian)
                    currentGlow = Color.Lerp(coolingCrimson, charredObsidian, (t - 0.65f) / 0.35f);
                }

                SetEmissionColor(currentGlow);

                // 30% cuối đời: Đá nứt chìm dần trở lại lòng đất và co lại
                if (t > 0.7f)
                {
                    float sinkRatio = (t - 0.7f) / 0.3f;
                    transform.localPosition = Vector3.Lerp(targetLocalPos, targetLocalPos + new Vector3(0f, -0.8f, 0f), sinkRatio);
                    transform.localScale = Vector3.Lerp(finalScale, new Vector3(finalScale.x, finalScale.y * 0.2f, finalScale.z), sinkRatio);
                }

                yield return null;
            }

            Destroy(gameObject);
        }

        private void SetEmissionColor(Color color)
        {
            if (rockRenderers == null) return;

            foreach (var r in rockRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(propBlock);
                propBlock.SetColor("_EmissionColor", color);
                propBlock.SetColor("_BaseColor", color);
                propBlock.SetColor("_Color", color);
                r.SetPropertyBlock(propBlock);
            }
        }
    }
}
