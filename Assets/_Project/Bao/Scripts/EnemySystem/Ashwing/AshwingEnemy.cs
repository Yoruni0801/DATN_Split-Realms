using UnityEngine;

namespace Bao.Enemies
{
    public class AshwingEnemy : BaoEnemyBase
    {
        [Header("Ashwing - Cinder Razor")]
        [SerializeField] private GameObject featherProjectilePrefab;
        [SerializeField] private Transform shootPoint;
        [SerializeField] private float shootDelay = 0.5f;
        [SerializeField] private float projectileSpeed = 12f;
        [Tooltip("Z rotation, in degrees, applied to the feather when it spawns.")]
        [SerializeField] private float projectileSpawnRotation;

        public GameObject FeatherProjectilePrefab => featherProjectilePrefab;
        public Transform ShootPoint => shootPoint;
        public float ShootDelay => shootDelay;
        public float ProjectileSpeed => projectileSpeed;

        protected override float FacingVisualMultiplier => -1f;

        private AshwingShootState shootState;
        private BaoEnemyFlyingPatrolState customPatrolState;
        private BaoEnemyFlyingChaseState customChaseState;
        private AshwingVFXController vfxController;

        protected override void Awake()
        {
            base.Awake();

            shootState = new AshwingShootState(this);
            customPatrolState = new BaoEnemyFlyingPatrolState(this);
            customChaseState = new BaoEnemyFlyingChaseState(this);

            if (Rigidbody2D != null)
            {
                Rigidbody2D.gravityScale = 0f; // Ashwing flies
            }

            vfxController = GetComponent<AshwingVFXController>();
            if (vfxController == null)
            {
                vfxController = gameObject.AddComponent<AshwingVFXController>();
            }
        }

        protected override void Start()
        {
            StateMachine.SetState(customPatrolState);
        }

        public override void SetStateToPatrol()
        {
            StateMachine.SetState(customPatrolState);
        }

        public override void SetStateToChase()
        {
            StateMachine.SetState(customChaseState);
        }

        public override void SetStateToAttack()
        {
            StateMachine.SetState(shootState);
        }

        public void BeginChargeVFX()
        {
            vfxController?.BeginCharge(shootDelay);
        }

        public void SpawnProjectile()
        {
            if (featherProjectilePrefab == null || shootPoint == null)
                return;

            Vector2 shotDirection = new Vector2(FacingDirection, 0f);
            if (Target != null)
            {
                shotDirection = ((Vector2)Target.position - (Vector2)shootPoint.position).normalized;
            }

            if (shotDirection.sqrMagnitude < 0.001f)
            {
                shotDirection = new Vector2(FacingDirection, 0f);
            }

            GameObject proj = Instantiate(
                featherProjectilePrefab,
                shootPoint.position,
                Quaternion.Euler(0f, 0f, projectileSpawnRotation));

            proj.transform.localScale = new Vector3(
                Mathf.Abs(proj.transform.localScale.x),
                proj.transform.localScale.y,
                proj.transform.localScale.z);

            if (proj.GetComponent<AshwingFeatherVFX>() == null)
            {
                proj.AddComponent<AshwingFeatherVFX>();
            }

            if (proj.TryGetComponent<Rigidbody2D>(out var rb))
            {
                rb.gravityScale = 0f;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                rb.linearVelocity = shotDirection * projectileSpeed;
            }

            vfxController?.PlayReleaseBurst(shotDirection);
        }

        public override void SetLocomotionAnimation(bool isWalking)
        {
            if (Animator == null) return;

            Animator.SetBool("isWalk", true);
            Animator.SetBool("isIdle", false);
        }
    }
}
