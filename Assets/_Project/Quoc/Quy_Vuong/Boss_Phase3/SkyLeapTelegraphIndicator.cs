using System.Collections.Generic;
using UnityEngine;

namespace Quoc.QuyVuong.Boss_Phase3
{
    /// <summary>
    /// Vùng cảnh báo Sky Leap như một dấu ấn đã bị ép xuống đất: mảng màu đặc,
    /// sứt cạnh, lẫn bụi và vết nứt. Không dùng vòng sáng hoặc chuyển động kiểu HUD.
    /// </summary>
    public sealed class SkyLeapTelegraphIndicator : MonoBehaviour
    {
        private readonly List<LineRenderer> fractures = new();
        private readonly List<Material> runtimeMaterials = new();

        private float trackingDuration;
        private float lockDuration;
        private float radius;
        private float trackingElapsed;
        private float lockElapsed;
        private bool isLocked;

        private SpriteRenderer groundMark;
        private Texture2D markTexture;
        private Sprite markSprite;
        private Vector3 groundMarkScale;
        private Material fractureMaterial;

        public bool IsLocked => isLocked;

        public void Initialize(float trackingTime, float lockTime, float impactRadius)
        {
            trackingDuration = Mathf.Max(0.05f, trackingTime);
            lockDuration = Mathf.Max(0.05f, lockTime);
            radius = Mathf.Max(0.6f, impactRadius);
            transform.position = new Vector3(transform.position.x, transform.position.y + 0.025f, -0.12f);

            BuildGroundMark();
            BuildFractures();
            SetFracturesVisible(false);
            UpdateTrackingVisuals();
        }

        public void LockAt(Vector3 worldPosition)
        {
            if (isLocked) return;
            isLocked = true;
            lockElapsed = 0f;
            transform.position = new Vector3(worldPosition.x, worldPosition.y + 0.025f, -0.12f);
            SetFracturesVisible(true);
            SkyLeapVfxUtility.SpawnLockPulse(transform.position, radius);
        }

        private void Update()
        {
            if (!isLocked)
            {
                trackingElapsed += Time.deltaTime;
                UpdateTrackingVisuals();
            }
            else
            {
                lockElapsed += Time.deltaTime;
                UpdateLockedVisuals();
            }
        }

        private void BuildGroundMark()
        {
            GameObject markObject = new GameObject("Weathered_Earth_Imprint");
            markObject.transform.SetParent(transform, false);
            markObject.transform.localPosition = Vector3.zero;

            groundMark = markObject.AddComponent<SpriteRenderer>();
            markSprite = SkyLeapVfxUtility.CreateWeatheredGroundMark(out markTexture);
            groundMark.sprite = markSprite;
            groundMark.sortingOrder = 21;

            Vector2 spriteSize = markSprite.bounds.size;
            groundMarkScale = new Vector3(
                radius * 2.08f / Mathf.Max(0.01f, spriteSize.x),
                radius * 0.64f / Mathf.Max(0.01f, spriteSize.y),
                1f);
            markObject.transform.localScale = groundMarkScale;
        }

        private void BuildFractures()
        {
            fractureMaterial = SkyLeapVfxUtility.CreateFlatPigmentMaterial(Color.white);
            if (fractureMaterial != null) runtimeMaterials.Add(fractureMaterial);

            const int count = 7;
            for (int i = 0; i < count; i++)
            {
                float angle = 0.16f + i * Mathf.PI * 2f / count
                    + Mathf.Sin(i * 2.19f) * 0.11f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * 0.30f, 0f);
                Vector3 tangent = new Vector3(-direction.y, direction.x, 0f).normalized;
                float length = radius * Mathf.Lerp(0.68f, 0.96f, Mathf.Abs(Mathf.Sin(i * 1.73f)));

                LineRenderer crack = GroundLineVfxUtility.CreateLine(
                    transform, $"Dark_Buried_Crack_{i}", fractureMaterial, 22, 0.052f);
                crack.numCapVertices = 1;
                crack.numCornerVertices = 1;
                crack.positionCount = 5;
                crack.SetPosition(0, direction * radius * 0.12f);
                crack.SetPosition(1, direction * length * 0.32f + tangent * 0.075f);
                crack.SetPosition(2, direction * length * 0.54f - tangent * 0.11f);
                crack.SetPosition(3, direction * length * 0.76f + tangent * 0.05f);
                crack.SetPosition(4, direction * length);
                crack.startColor = crack.endColor = new Color(0.14f, 0.058f, 0.022f, 0.78f);
                fractures.Add(crack);
            }
        }

        private void UpdateTrackingVisuals()
        {
            float progress = Mathf.Clamp01(trackingElapsed / trackingDuration);
            float settle = 1f - Mathf.Pow(1f - progress, 2f);
            float breath = 0.985f + Mathf.Sin(trackingElapsed * 2.4f) * 0.008f;
            if (groundMark != null)
            {
                groundMark.transform.localScale = groundMarkScale * Mathf.Lerp(0.92f, breath, settle);
                groundMark.color = new Color(0.90f, 0.78f, 0.63f, Mathf.Lerp(0.34f, 0.68f, settle));
            }
        }

        private void UpdateLockedVisuals()
        {
            float progress = Mathf.Clamp01(lockElapsed / lockDuration);
            float stamp = 1f - Mathf.Pow(1f - progress, 3f);
            if (groundMark != null)
            {
                groundMark.transform.localScale = groundMarkScale * Mathf.Lerp(1.035f, 0.975f, stamp);
                groundMark.color = new Color(0.96f, 0.83f, 0.66f, Mathf.Lerp(0.82f, 0.62f, progress));
            }

            for (int i = 0; i < fractures.Count; i++)
            {
                float reveal = Mathf.Clamp01(stamp * 1.45f - i * 0.06f);
                fractures[i].transform.localScale = new Vector3(reveal, reveal, 1f);
                Color ink = new Color(0.13f, 0.052f, 0.019f, reveal * 0.86f);
                fractures[i].startColor = fractures[i].endColor = ink;
            }
        }

        private void SetFracturesVisible(bool visible)
        {
            foreach (LineRenderer line in fractures) line.enabled = visible;
        }

        private void OnDestroy()
        {
            GroundLineVfxUtility.DestroyMaterials(runtimeMaterials);
            if (markSprite != null) Destroy(markSprite);
            if (markTexture != null) Destroy(markTexture);
        }
    }
}
