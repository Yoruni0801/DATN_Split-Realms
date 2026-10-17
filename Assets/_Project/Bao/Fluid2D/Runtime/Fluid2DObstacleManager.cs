using UnityEngine;
using System.Collections.Generic;

namespace Bao.Fluid2D
{
    public class Fluid2DObstacleManager : MonoBehaviour
    {
        public Fluid2DSystem fluidSystem;

        [Header("=== Obstacle Settings ===")]
        [Tooltip("Layer chứa các vật thể tĩnh làm địa hình va chạm.")]
        public LayerMask obstacleLayer;

        [Header("=== Debug Info ===")]
        [SerializeField] private int circleCount = 0;
        [SerializeField] private int segmentCount = 0;
        [SerializeField] private float lastObstacleRefreshRealtime;
        [SerializeField] private int obstacleRefreshCount;

        /// <summary>RealtimeSinceStartup of the last RefreshObstacles call.</summary>
        public float LastObstacleRefreshRealtime => lastObstacleRefreshRealtime;
        public int ObstacleRefreshCount => obstacleRefreshCount;

        private readonly List<CircleObstacle> tempCircles = new List<CircleObstacle>();
        private readonly List<SegmentObstacle> tempSegments = new List<SegmentObstacle>();

        /// <summary>
        /// Colliders temporarily excluded from fluid obstacle extraction
        /// (e.g. ice that has melted into water).
        /// </summary>
        private readonly HashSet<Collider2D> excludedColliders = new HashSet<Collider2D>();

        // GPU Buffers cho chướng ngại vật
        private GraphicsBuffer circleBuffer;
        private GraphicsBuffer segmentBuffer;

        public GraphicsBuffer CircleBuffer => circleBuffer;
        public GraphicsBuffer SegmentBuffer => segmentBuffer;
        public int CircleCount => circleCount;
        public int SegmentCount => segmentCount;
        public IReadOnlyList<CircleObstacle> Circles => tempCircles;
        public IReadOnlyList<SegmentObstacle> Segments => tempSegments;

        private void OnEnable()
        {
            if (fluidSystem == null)
            {
                fluidSystem = GetComponent<Fluid2DSystem>();
                if (fluidSystem == null) fluidSystem = Object.FindFirstObjectByType<Fluid2DSystem>();
            }

            RefreshObstacles();
        }

        private void OnDisable()
        {
            ReleaseBuffers();
        }

        private void OnDestroy()
        {
            ReleaseBuffers();
            excludedColliders.Clear();
        }

        private void ReleaseBuffers()
        {
            if (circleBuffer != null)
            {
                circleBuffer.Release();
                circleBuffer = null;
            }
            if (segmentBuffer != null)
            {
                segmentBuffer.Release();
                segmentBuffer = null;
            }
        }

        /// <summary>
        /// Exclude a collider from the next (and subsequent) obstacle extracts until re-included.
        /// Call <see cref="RefreshObstacles"/> after changing exclusions.
        /// </summary>
        public void ExcludeCollider(Collider2D collider)
        {
            if (collider != null)
                excludedColliders.Add(collider);
        }

        public void ExcludeColliders(IEnumerable<Collider2D> colliders)
        {
            if (colliders == null) return;
            foreach (var c in colliders)
            {
                if (c != null)
                    excludedColliders.Add(c);
            }
        }

        public void IncludeCollider(Collider2D collider)
        {
            if (collider != null)
                excludedColliders.Remove(collider);
        }

        public void IncludeColliders(IEnumerable<Collider2D> colliders)
        {
            if (colliders == null) return;
            foreach (var c in colliders)
            {
                if (c != null)
                    excludedColliders.Remove(c);
            }
        }

        public void ClearExclusions()
        {
            excludedColliders.Clear();
        }

        public bool IsExcluded(Collider2D collider)
        {
            return collider != null && excludedColliders.Contains(collider);
        }

        [ContextMenu("Refresh Obstacles")]
        public void RefreshObstacles()
        {
            lastObstacleRefreshRealtime = Time.realtimeSinceStartup;
            obstacleRefreshCount++;

            ReleaseBuffers();
            tempCircles.Clear();
            tempSegments.Clear();

            // 1. Quét Collider2D theo LayerMask (dùng Physics broadphase để tránh quét toàn bộ Scene)
            Collider2D[] colliders;
            if (obstacleLayer.value != 0)
            {
                colliders = Physics2D.OverlapBoxAll(Vector2.zero, new Vector2(100000f, 100000f), 0f, obstacleLayer);
            }
            else
            {
                colliders = Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
            }

            foreach (var col in colliders)
            {
                if (col == null) continue;
                // Disabled colliders must not remain as invisible fluid walls.
                if (!col.enabled || !col.gameObject.activeInHierarchy) continue;
                if (col.isTrigger) continue;
                if (excludedColliders.Contains(col)) continue;
                if (((1 << col.gameObject.layer) & obstacleLayer.value) == 0) continue;

                Rigidbody2D rb = col.attachedRigidbody;
                if (rb != null && rb.bodyType != RigidbodyType2D.Static) continue; // Bỏ qua vật thể động (xử lý riêng ở Giai đoạn 3)

                ExtractObstaclesFromCollider(col);
            }

            circleCount = tempCircles.Count;
            segmentCount = tempSegments.Count;

            // 2. Khởi tạo GPU Buffers (nếu rỗng thì tạo buffer giả 1 phần tử inactive để tránh lỗi Compute Shader null buffer)
            if (circleCount > 0)
            {
                circleBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    circleCount,
                    System.Runtime.InteropServices.Marshal.SizeOf<CircleObstacle>()
                );
                circleBuffer.SetData(tempCircles.ToArray());
            }
            else
            {
                // Buffer dự phòng
                circleBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, System.Runtime.InteropServices.Marshal.SizeOf<CircleObstacle>());
                CircleObstacle dummy = new CircleObstacle { active = 0 };
                circleBuffer.SetData(new CircleObstacle[] { dummy });
            }

            if (segmentCount > 0)
            {
                segmentBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    segmentCount,
                    System.Runtime.InteropServices.Marshal.SizeOf<SegmentObstacle>()
                );
                segmentBuffer.SetData(tempSegments.ToArray());
            }
            else
            {
                // Buffer dự phòng
                segmentBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, System.Runtime.InteropServices.Marshal.SizeOf<SegmentObstacle>());
                SegmentObstacle dummy = new SegmentObstacle { active = 0 };
                segmentBuffer.SetData(new SegmentObstacle[] { dummy });
            }

            Debug.Log($"Fluid2DObstacleManager: Đã trích xuất {circleCount} hình tròn và {segmentCount} đoạn thẳng chướng ngại vật.");
        }

        private void ExtractObstaclesFromCollider(Collider2D col)
        {
            Transform tf = col.transform;

            if (col is CircleCollider2D circle)
            {
                CircleObstacle obs = new CircleObstacle();
                obs.center = tf.TransformPoint(circle.offset);
                obs.radius = circle.radius * Mathf.Max(tf.lossyScale.x, tf.lossyScale.y);
                obs.active = 1;
                tempCircles.Add(obs);
            }
            else if (col is BoxCollider2D box)
            {
                Vector2 size = box.size;
                Vector2 half = size * 0.5f;

                // Tính toán 4 góc ở World Space
                Vector2 p0 = tf.TransformPoint(box.offset + new Vector2(-half.x, -half.y));
                Vector2 p1 = tf.TransformPoint(box.offset + new Vector2(-half.x, half.y));
                Vector2 p2 = tf.TransformPoint(box.offset + new Vector2(half.x, half.y));
                Vector2 p3 = tf.TransformPoint(box.offset + new Vector2(half.x, -half.y));

                // Tạo ra 4 cạnh đoạn thẳng bao quanh hộp
                AddSegment(p0, p1, 0.02f);
                AddSegment(p1, p2, 0.02f);
                AddSegment(p2, p3, 0.02f);
                AddSegment(p3, p0, 0.02f);
            }
            else if (col is EdgeCollider2D edge)
            {
                for (int i = 0; i < edge.pointCount - 1; i++)
                {
                    Vector2 pA = tf.TransformPoint(edge.points[i] + edge.offset);
                    Vector2 pB = tf.TransformPoint(edge.points[i + 1] + edge.offset);
                    AddSegment(pA, pB, edge.edgeRadius);
                }
            }
            else if (col is PolygonCollider2D poly)
            {
                for (int pathIdx = 0; pathIdx < poly.pathCount; pathIdx++)
                {
                    Vector2[] path = poly.GetPath(pathIdx);
                    for (int i = 0; i < path.Length; i++)
                    {
                        Vector2 pA = tf.TransformPoint(path[i] + poly.offset);
                        Vector2 pB = tf.TransformPoint(path[(i + 1) % path.Length] + poly.offset); // Khép kín đường đi
                        AddSegment(pA, pB, 0.02f);
                    }
                }
            }
            else if (col is CompositeCollider2D composite)
            {
                // Trích xuất các cạnh từ Composite Collider (thường dùng cho Tilemap)
                for (int pathIdx = 0; pathIdx < composite.pathCount; pathIdx++)
                {
                    Vector2[] path = new Vector2[composite.GetPathPointCount(pathIdx)];
                    composite.GetPath(pathIdx, path);
                    for (int i = 0; i < path.Length - 1; i++)
                    {
                        Vector2 pA = tf.TransformPoint(path[i] + composite.offset);
                        Vector2 pB = tf.TransformPoint(path[i + 1] + composite.offset);
                        AddSegment(pA, pB, 0.02f);
                    }
                    if (path.Length > 2 && composite.geometryType == CompositeCollider2D.GeometryType.Outlines)
                    {
                        // Nối điểm cuối về đầu nếu ở chế độ outline
                        Vector2 pA = tf.TransformPoint(path[path.Length - 1] + composite.offset);
                        Vector2 pB = tf.TransformPoint(path[0] + composite.offset);
                        AddSegment(pA, pB, 0.02f);
                    }
                }
            }
        }

        private void AddSegment(Vector2 a, Vector2 b, float radius)
        {
            SegmentObstacle seg = new SegmentObstacle();
            seg.a = a;
            seg.b = b;
            seg.radius = radius;
            seg.active = 1;
            tempSegments.Add(seg);
        }
    }
}
