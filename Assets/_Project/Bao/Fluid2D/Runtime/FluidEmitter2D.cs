using UnityEngine;

namespace Bao.Fluid2D
{
    public class FluidEmitter2D : MonoBehaviour
    {
        public Fluid2DSystem fluidSystem;
        
        [Header("=== Emitter Config ===")]
        [Tooltip("Số lượng hạt phun ra mỗi giây.")]
        public float emitRate = 100f;
        
        [Tooltip("Vận tốc phun ban đầu.")]
        public Vector2 initialVelocity = new Vector2(0f, -5f);
        
        [Tooltip("Chiều rộng vòi phun.")]
        public float emitWidth = 0.5f;
        
        [Tooltip("Góc lệch ngẫu nhiên (spread angle) khi phun (đơn vị: độ).")]
        [Range(0f, 45f)]
        public float spreadAngle = 10f;
        
        [Tooltip("Tự động phun khi bắt đầu game.")]
        public bool emitOnStart = true;
        
        private float spawnAccumulator = 0f;
        private bool isEmitting = false;

        private void Start()
        {
            if (fluidSystem == null)
            {
                fluidSystem = Object.FindFirstObjectByType<Fluid2DSystem>();
            }
            
            isEmitting = emitOnStart;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (fluidSystem == null || !isEmitting) return;

            // Tích lũy số hạt cần sinh theo thời gian
            spawnAccumulator += emitRate * Time.deltaTime;
            int spawnCount = Mathf.FloorToInt(spawnAccumulator);
            spawnAccumulator -= spawnCount;

            for (int i = 0; i < spawnCount; i++)
            {
                SpawnSingleParticle();
            }
        }

        private void SpawnSingleParticle()
        {
            // Tính toán vị trí ngẫu nhiên dọc theo chiều ngang của vòi phun
            float offset = Random.Range(-emitWidth * 0.5f, emitWidth * 0.5f);
            
            // Lấy vector vuông góc với hướng phun để làm hướng dàn ngang
            Vector3 emitDir = initialVelocity.normalized;
            Vector3 rightDir = new Vector3(-emitDir.y, emitDir.x, 0f);
            Vector3 spawnPos = transform.position + rightDir * offset;

            // Áp dụng độ lệch góc ngẫu nhiên cho vận tốc hạt
            float angle = Random.Range(-spreadAngle, spreadAngle);
            Vector2 rotatedVelocity = Quaternion.Euler(0f, 0f, angle) * initialVelocity;

            fluidSystem.EmitParticle(spawnPos, rotatedVelocity);
        }

        public void SetEmitting(bool emitting)
        {
            isEmitting = emitting;
        }
        
        private void OnDrawGizmos()
        {
            // Vẽ vòi phun trực quan trong Editor
            Gizmos.color = Color.cyan;
            Vector3 emitDir = initialVelocity.magnitude > 0.001f ? (Vector3)initialVelocity.normalized : Vector3.down;
            Vector3 rightDir = new Vector3(-emitDir.y, emitDir.x, 0f);
            
            Vector3 p1 = transform.position - rightDir * (emitWidth * 0.5f);
            Vector3 p2 = transform.position + rightDir * (emitWidth * 0.5f);
            
            Gizmos.DrawLine(p1, p2);
            Gizmos.DrawRay(transform.position, emitDir * 1.5f);
        }
    }
}
