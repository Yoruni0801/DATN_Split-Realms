using System.Collections.Generic;
using UnityEngine;

namespace Bao.Fluid2D
{
    /// <summary>
    /// Rigidbody2D that receives GPU particle collision forces and applies
    /// smoothed multi-sample buoyancy / water drag (not raw spring bounce).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class Fluid2DObject : MonoBehaviour
    {
        public const int MaxSamples = 8;

        private static readonly List<Fluid2DObject> s_Active = new List<Fluid2DObject>(16);
        public static IReadOnlyList<Fluid2DObject> ActiveObjects => s_Active;

        public Fluid2DSystem fluidSystem;

        [Header("=== Interaction Settings ===")]
        [Tooltip("Bán kính vùng tròn tương tác va chạm với nước (Tự động tính nếu để <= 0).")]
        public float customRadius = 0f;

        [Tooltip("Tỷ lệ lực đẩy tác dụng lên vật thể (giữ tương thích scene cũ).")]
        public float buoyancyMultiplier = 1.0f;

        [Tooltip("Tỷ lệ lực cản nước cản chuyển động.")]
        public float dragMultiplier = 1.0f;

        [Header("=== Buoyancy Samples ===")]
        [Tooltip("Số điểm lấy mẫu buoyancy / va chạm từ Collider2D (box/circle/capsule/bounds).")]
        [Range(1, MaxSamples)]
        public int buoyancySampleCount = 4;

        [Header("=== Buoyancy & Drag Stabilization ===")]
        [Tooltip("Hệ số thêm cho lực fluid sau buoyancyMultiplier.")]
        public float buoyancyStrength = 1.0f;

        [Tooltip("Lực cản tuyến tính khi đang chạm nước (nhân với dragMultiplier).")]
        public float waterDrag = 2.5f;

        [Tooltip("Giảm tốc góc khi đang chạm nước.")]
        public float angularWaterDamping = 2.0f;

        [Tooltip("Giới hạn độ lớn tổng lực fluid mỗi FixedUpdate.")]
        public float maxFluidForce = 55f;

        [Tooltip("Giới hạn gia tốc fluid (force/mass) để tránh văng khi chạm nước lần đầu.")]
        public float maxFluidAcceleration = 18f;

        [Tooltip("0 = phản ứng tức thì, gần 1 = làm mượt lực mạnh (giảm nảy).")]
        [Range(0f, 0.95f)]
        public float forceSmoothing = 0.45f;

        [Tooltip("Ngưỡng |force| coi là đang tiếp xúc nước để bật drag.")]
        public float contactForceThreshold = 0.35f;

        private Rigidbody2D rb;
        private Collider2D cachedCollider;
        private float finalRadius = 0.5f;
        private float sampleRadius = 0.25f;
        private int activeSampleCount = 1;

        private readonly Vector2[] sampleLocalOffsets = new Vector2[MaxSamples];
        private readonly Vector2[] accumSampleForces = new Vector2[MaxSamples];
        private readonly Vector2[] accumSamplePoints = new Vector2[MaxSamples];
        private int accumSampleCount;
        private int forceFrameId = -1;

        private Vector2 smoothedForce;
        private float wetAmount;

        public Rigidbody2D Rigidbody => rb;
        public float Radius => finalRadius;
        public int ActiveSampleCount => activeSampleCount;
        public float SampleRadius => sampleRadius;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            cachedCollider = GetComponent<Collider2D>();
            RebuildSamples();
        }

        private void OnEnable()
        {
            if (!s_Active.Contains(this))
                s_Active.Add(this);

            if (rb == null)
                rb = GetComponent<Rigidbody2D>();
            if (cachedCollider == null)
                cachedCollider = GetComponent<Collider2D>();
            RebuildSamples();
        }

        private void OnDisable()
        {
            s_Active.Remove(this);
        }

        private void Start()
        {
            if (fluidSystem == null)
                fluidSystem = Object.FindFirstObjectByType<Fluid2DSystem>();

            RebuildSamples();
        }

        private void OnValidate()
        {
            buoyancySampleCount = Mathf.Clamp(buoyancySampleCount, 1, MaxSamples);
            maxFluidForce = Mathf.Max(0f, maxFluidForce);
            maxFluidAcceleration = Mathf.Max(0f, maxFluidAcceleration);
            waterDrag = Mathf.Max(0f, waterDrag);
            angularWaterDamping = Mathf.Max(0f, angularWaterDamping);
            RebuildSamples();
        }

        private void FixedUpdate()
        {
            if (rb == null || rb.bodyType == RigidbodyType2D.Static)
                return;

            // Residual damping for a short time after leaving dense contact (reduces bounce tail).
            if (wetAmount > 0f && forceFrameId != Time.frameCount)
            {
                float dampScale = wetAmount * 0.35f;
                ApplyWaterDrag(dampScale);
                wetAmount = Mathf.Max(0f, wetAmount - Time.fixedDeltaTime * 2.5f);
            }
        }

        /// <summary>World-space center of sample i (local offset transformed).</summary>
        public Vector2 GetSampleWorldPosition(int index)
        {
            if (index < 0 || index >= activeSampleCount)
                return transform.position;

            Vector3 local = sampleLocalOffsets[index];
            return transform.TransformPoint(local);
        }

        public float GetSampleRadius(int index)
        {
            return sampleRadius;
        }

        /// <summary>Called by Fluid2DSystem once per object before packing GPU bodies each physics step.</summary>
        public void BeginFluidForceFrame()
        {
            forceFrameId = Time.frameCount;
            accumSampleCount = 0;
        }

        /// <summary>
        /// Accumulate one GPU sample contact force (raw units already /10000 by system).
        /// </summary>
        public void AccumulateFluidForce(Vector2 rawGpuForce, Vector2 worldPoint)
        {
            if (rb == null || rb.bodyType == RigidbodyType2D.Static)
                return;

            if (forceFrameId != Time.frameCount)
                BeginFluidForceFrame();

            if (accumSampleCount >= MaxSamples)
                return;

            Vector2 scaled = rawGpuForce * buoyancyMultiplier * buoyancyStrength;
            accumSampleForces[accumSampleCount] = scaled;
            accumSamplePoints[accumSampleCount] = worldPoint;
            accumSampleCount++;
        }

        /// <summary>
        /// Smooth, clamp, apply multi-point forces + water drag. Called once after all samples.
        /// </summary>
        public void FinalizeFluidForces()
        {
            if (rb == null || rb.bodyType == RigidbodyType2D.Static)
                return;

            if (accumSampleCount <= 0)
            {
                // Decay residual force when no GPU contacts this step (kills bounce tail).
                float blend = 1f - forceSmoothing;
                smoothedForce = Vector2.Lerp(smoothedForce, Vector2.zero, Mathf.Max(blend, 0.2f));
                return;
            }

            Vector2 total = Vector2.zero;
            Vector2 weightedPoint = Vector2.zero;
            float weightSum = 0f;
            for (int i = 0; i < accumSampleCount; i++)
            {
                Vector2 f = accumSampleForces[i];
                total += f;
                float w = f.magnitude + 1e-4f;
                weightedPoint += accumSamplePoints[i] * w;
                weightSum += w;
            }

            if (weightSum > 1e-6f)
                weightedPoint /= weightSum;
            else
                weightedPoint = rb.worldCenterOfMass;

            float blendIn = 1f - forceSmoothing;
            smoothedForce = Vector2.Lerp(smoothedForce, total, blendIn);

            Vector2 applied = ClampFluidForce(smoothedForce);
            if (applied.sqrMagnitude > 1e-8f)
            {
                // One clamped force at contact centroid → natural torque without multi-spring explode.
                rb.AddForceAtPosition(applied, weightedPoint, ForceMode2D.Force);
            }

            if (total.magnitude >= contactForceThreshold)
            {
                wetAmount = 1f;
                ApplyWaterDrag(1f);
            }

            accumSampleCount = 0;
        }

        /// <summary>
        /// Legacy single-force API (center of mass). Still used if system has no samples.
        /// </summary>
        public void ApplyFluidForce(Vector2 force)
        {
            if (rb == null || rb.bodyType == RigidbodyType2D.Static)
                return;

            BeginFluidForceFrame();
            AccumulateFluidForce(force, rb.worldCenterOfMass);
            FinalizeFluidForces();
        }

        private Vector2 ClampFluidForce(Vector2 force)
        {
            float mag = force.magnitude;
            if (mag > maxFluidForce && mag > 1e-6f)
                force = force * (maxFluidForce / mag);

            float mass = Mathf.Max(rb.mass, 0.01f);
            float accel = force.magnitude / mass;
            if (accel > maxFluidAcceleration && accel > 1e-6f)
                force *= maxFluidAcceleration / accel;

            return force;
        }

        private void ApplyWaterDrag(float intensity)
        {
            if (rb == null)
                return;

            float dt = Time.fixedDeltaTime;
            if (dt <= 0f)
                return;

            float linear = Mathf.Max(0f, waterDrag * dragMultiplier * intensity);
            float angular = Mathf.Max(0f, angularWaterDamping * dragMultiplier * intensity);

            // Stable implicit damping: v /= (1 + k*dt) — never reverses velocity in one step.
            if (linear > 0f)
                rb.linearVelocity /= (1f + linear * dt);

            if (angular > 0f)
                rb.angularVelocity /= (1f + angular * dt);
        }

        private void CalculateRadius()
        {
            if (customRadius > 0f)
            {
                finalRadius = customRadius;
                return;
            }

            if (cachedCollider == null)
                cachedCollider = GetComponent<Collider2D>();

            if (cachedCollider != null)
            {
                Bounds bounds = cachedCollider.bounds;
                finalRadius = Mathf.Max(bounds.extents.x, bounds.extents.y);
            }
            else
            {
                finalRadius = 0.5f;
            }
        }

        /// <summary>
        /// Build local sample points from Collider2D shape (box / circle / capsule / generic bounds).
        /// </summary>
        public void RebuildSamples()
        {
            if (cachedCollider == null)
                cachedCollider = GetComponent<Collider2D>();

            CalculateRadius();

            int count = Mathf.Clamp(buoyancySampleCount, 1, MaxSamples);
            activeSampleCount = count;

            for (int i = 0; i < MaxSamples; i++)
                sampleLocalOffsets[i] = Vector2.zero;

            if (cachedCollider is CircleCollider2D circle)
            {
                BuildCircleSamples(circle, count);
            }
            else if (cachedCollider is CapsuleCollider2D capsule)
            {
                BuildCapsuleSamples(capsule, count);
            }
            else if (cachedCollider is BoxCollider2D box)
            {
                BuildBoxSamples(box, count);
            }
            else if (cachedCollider != null)
            {
                BuildBoundsSamples(cachedCollider, count);
            }
            else
            {
                sampleLocalOffsets[0] = Vector2.zero;
                activeSampleCount = 1;
                sampleRadius = finalRadius;
            }

            // Sample sphere radius: cover shape with modest overlap (avoids one huge spring circle).
            if (count <= 1)
            {
                sampleRadius = finalRadius;
            }
            else
            {
                sampleRadius = Mathf.Max(finalRadius * 0.35f, finalRadius / Mathf.Sqrt(count));
            }
        }

        private void BuildCircleSamples(CircleCollider2D circle, int count)
        {
            Vector2 center = circle.offset;
            sampleLocalOffsets[0] = center;
            if (count == 1)
                return;

            // Inner ring so particles hit volume, not only a single center.
            float ring = circle.radius * 0.55f;
            int ringCount = count - 1;
            for (int i = 0; i < ringCount; i++)
            {
                float ang = (Mathf.PI * 2f * i) / ringCount;
                sampleLocalOffsets[i + 1] = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * ring;
            }
        }

        private void BuildBoxSamples(BoxCollider2D box, int count)
        {
            Vector2 center = box.offset;
            Vector2 half = box.size * 0.5f;
            // Inset so samples sit inside the box.
            Vector2 inset = half * 0.65f;

            if (count == 1)
            {
                sampleLocalOffsets[0] = center;
                return;
            }

            // Prefer a small grid covering the box.
            int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt((float)count / cols);
            int written = 0;
            for (int y = 0; y < rows && written < count; y++)
            {
                for (int x = 0; x < cols && written < count; x++)
                {
                    float u = cols == 1 ? 0.5f : x / (float)(cols - 1);
                    float v = rows == 1 ? 0.5f : y / (float)(rows - 1);
                    float lx = Mathf.Lerp(-inset.x, inset.x, u);
                    float ly = Mathf.Lerp(-inset.y, inset.y, v);
                    sampleLocalOffsets[written++] = center + new Vector2(lx, ly);
                }
            }
            activeSampleCount = written;
        }

        private void BuildCapsuleSamples(CapsuleCollider2D capsule, int count)
        {
            Vector2 center = capsule.offset;
            Vector2 size = capsule.size;
            bool vertical = capsule.direction == CapsuleDirection2D.Vertical;

            float radius = vertical ? size.x * 0.5f : size.y * 0.5f;
            float halfHeight = vertical ? size.y * 0.5f : size.x * 0.5f;
            float shaftHalf = Mathf.Max(0f, halfHeight - radius);

            if (count == 1)
            {
                sampleLocalOffsets[0] = center;
                return;
            }

            // Samples along capsule axis (handles long capsules that a single circle misses).
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                float along = Mathf.Lerp(-shaftHalf, shaftHalf, t);
                Vector2 local = vertical
                    ? new Vector2(0f, along)
                    : new Vector2(along, 0f);
                sampleLocalOffsets[i] = center + local;
            }
        }

        private void BuildBoundsSamples(Collider2D col, int count)
        {
            // Approximate using world bounds converted into local space.
            Bounds b = col.bounds;
            Vector3 localCenter = transform.InverseTransformPoint(b.center);
            Vector3 localExt = transform.InverseTransformVector(b.extents);
            localExt = new Vector3(Mathf.Abs(localExt.x), Mathf.Abs(localExt.y), 0f);
            Vector2 center = localCenter;
            Vector2 inset = new Vector2(localExt.x, localExt.y) * 0.65f;

            if (count == 1)
            {
                sampleLocalOffsets[0] = center;
                return;
            }

            int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt((float)count / cols);
            int written = 0;
            for (int y = 0; y < rows && written < count; y++)
            {
                for (int x = 0; x < cols && written < count; x++)
                {
                    float u = cols == 1 ? 0.5f : x / (float)(cols - 1);
                    float v = rows == 1 ? 0.5f : y / (float)(rows - 1);
                    sampleLocalOffsets[written++] = center + new Vector2(
                        Mathf.Lerp(-inset.x, inset.x, u),
                        Mathf.Lerp(-inset.y, inset.y, v));
                }
            }
            activeSampleCount = written;
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying)
            {
                if (cachedCollider == null)
                    cachedCollider = GetComponent<Collider2D>();
                RebuildSamples();
            }

            Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, finalRadius);

            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.85f);
            for (int i = 0; i < activeSampleCount; i++)
            {
                Vector3 p = GetSampleWorldPosition(i);
                Gizmos.DrawWireSphere(p, sampleRadius);
            }
        }
    }
}
