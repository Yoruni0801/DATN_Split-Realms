using UnityEngine;

namespace Bao.Fluid2D
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class FluidDrain2D : MonoBehaviour
    {
        public Fluid2DSystem fluidSystem;

        private BoxCollider2D boxCollider;

        private void Awake()
        {
            boxCollider = GetComponent<BoxCollider2D>();
            boxCollider.isTrigger = true;
        }

        private void OnEnable()
        {
            FluidDrainRegistry.Register(this);
        }

        private void OnDisable()
        {
            FluidDrainRegistry.Unregister(this);
        }

        private void Start()
        {
            if (fluidSystem == null)
            {
                fluidSystem = Object.FindFirstObjectByType<Fluid2DSystem>();
            }

            // Ensure registry entry if enable order was unusual.
            FluidDrainRegistry.Register(this);
        }

        /// <summary>
        /// Lấy tọa độ giới hạn thế giới (world-space bounds) dạng Vector4 (minX, minY, maxX, maxY).
        /// </summary>
        public Vector4 GetBounds()
        {
            if (boxCollider == null) boxCollider = GetComponent<BoxCollider2D>();

            Bounds bounds = boxCollider.bounds;
            return new Vector4(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);
        }

        private void OnDrawGizmos()
        {
            if (boxCollider == null) boxCollider = GetComponent<BoxCollider2D>();

            Gizmos.color = new Color(0.9f, 0.1f, 0.1f, 0.4f);
            Gizmos.DrawCube(transform.position, (Vector3)boxCollider.size);

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, (Vector3)boxCollider.size);
        }
    }
}
