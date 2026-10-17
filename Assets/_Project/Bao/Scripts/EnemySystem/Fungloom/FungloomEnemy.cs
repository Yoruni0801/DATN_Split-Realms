using UnityEngine;

namespace Bao.Enemies
{
    public class FungloomEnemy : BaoEnemyBase
    {
        [Header("Fungloom - Shoot Bees")]
        [SerializeField] private GameObject beeProjectilePrefab;
        [SerializeField] private Transform shootPoint;
        [SerializeField] private float anticipationDuration = 0.4f;
        [SerializeField] private float shootDelay = 0.6f;
        
        public GameObject BeeProjectilePrefab => beeProjectilePrefab;
        public Transform ShootPoint => shootPoint;
        public float AnticipationDuration => anticipationDuration;
        public float ShootDelay => shootDelay;

        protected override float FacingVisualMultiplier => -1f;

        private FungloomShootState shootState;
        private FungloomIdleState customIdleState;
        private FungloomChaseState customChaseState;

        protected override void Awake()
        {
            base.Awake();
            shootState = new FungloomShootState(this);
            customIdleState = new FungloomIdleState(this);
            customChaseState = new FungloomChaseState(this);
            
            if (Rigidbody2D != null)
            {
                Rigidbody2D.gravityScale = 0f; // Fungloom is a hive floating maybe? Or just a mushroom.
            }
        }

        protected override void Start()
        {
            ApplyFacing(FacingDirection);
            StateMachine.SetState(customIdleState);
        }

        public override void SetStateToPatrol()
        {
            StateMachine.SetState(customIdleState);
        }

        public override void SetStateToChase()
        {
            StateMachine.SetState(customChaseState);
        }

        public override void SetStateToAttack()
        {
            StateMachine.SetState(shootState);
        }
        
        public void SpawnBee()
        {
            if (beeProjectilePrefab != null && shootPoint != null)
            {
                var bee = Instantiate(beeProjectilePrefab, shootPoint.position, Quaternion.identity);
                
                // Add the HiveBeeProjectile component to enable homing and movement
                if (bee.GetComponent<HiveBeeProjectile>() == null)
                {
                    bee.AddComponent<HiveBeeProjectile>();
                }

                HiveBeeProjectile projectile = bee.GetComponent<HiveBeeProjectile>();
                if (projectile != null)
                {
                    projectile.Initialize(Target, transform);
                }
                
                // Add visual effects
                if (bee.GetComponent<HiveBeeVFX>() == null)
                {
                    bee.AddComponent<HiveBeeVFX>();
                }

                // Trigger a burst of toxic pollen when shooting
                var vfxController = GetComponent<FungloomVFXController>();
                if (vfxController != null)
                {
                    vfxController.TriggerPollenBurst(shootPoint.position);
                }

                // Parent under permanent temporary container if needed, or let it free-roam
                if (VFXContainerUtility.GetContainer() != null)
                {
                    bee.transform.SetParent(VFXContainerUtility.GetContainer());
                }
            }
        }
    }
}
