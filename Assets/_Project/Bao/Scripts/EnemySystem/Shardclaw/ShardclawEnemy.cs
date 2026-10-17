using UnityEngine;

namespace Bao.Enemies
{
    public class ShardclawEnemy : BaoEnemyBase
    {
        [Header("Shardclaw - Earth Slam creates Lava")]
        [SerializeField] private float anticipationDuration = 0.5f; // Thời gian gồng (hiện warning) trước khi đập
        [SerializeField] private float slamDuration = 1.0f;
        [SerializeField] private int slamDamage = 25;
        [SerializeField] private float emergeDamageRadius = 2.0f; // Reuse existing radius variable for slam range
        [SerializeField] private Transform vfxSpawnPoint;
        [SerializeField] private GameObject earthSlamPrefab;
        [SerializeField] private Vector3 earthSlamSpawnRotation;
        [SerializeField] private GameObject lavaPrefab;
        [SerializeField] private Vector3 lavaSpawnRotation;

        public float AnticipationDuration => anticipationDuration;
        public float SlamDuration => slamDuration;
        public int SlamDamage => slamDamage;
        public float EmergeDamageRadius => emergeDamageRadius;
        public Transform VfxSpawnPoint => vfxSpawnPoint;
        public GameObject EarthSlamPrefab => earthSlamPrefab;
        public Quaternion EarthSlamSpawnRotation => Quaternion.Euler(earthSlamSpawnRotation);
        public GameObject LavaPrefab => lavaPrefab;
        public Quaternion LavaSpawnRotation => Quaternion.Euler(lavaSpawnRotation);

        public ShardclawVFXController VfxController { get; private set; }

        // EmergeDamage is kept for potential legacy references
        public int EmergeDamage => slamDamage;

        // Đảo ngược Sprite khi di chuyển
        protected override float FacingVisualMultiplier => -1f;

        private ShardclawSlamState slamState;

        protected override void Awake()
        {
            base.Awake();
            slamState = new ShardclawSlamState(this);
            VfxController = GetComponent<ShardclawVFXController>();
        }

        public override void SetStateToAttack()
        {
            StateMachine.SetState(slamState);
        }

        public override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = Color.red;
            Vector3 spawnPos = vfxSpawnPoint != null ? vfxSpawnPoint.position : transform.position;
            Gizmos.DrawWireSphere(spawnPos, emergeDamageRadius);
        }
    }
}
