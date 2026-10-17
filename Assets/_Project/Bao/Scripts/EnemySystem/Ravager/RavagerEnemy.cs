using UnityEngine;

namespace Bao.Enemies
{
    public class RavagerEnemy : BaoEnemyBase
    {
        protected override float FacingVisualMultiplier => -1f;

        [Header("Ravager - Chase")]
        [SerializeField] private float chaseSpeedMultiplier = 1.5f;

        [Header("Ravager - Combat Mode")]
        [SerializeField] private int dodgeChancePercent = 50;

        [Header("Ravager - Dodge")]
        [SerializeField] private float dodgeBackDistance = 2f;
        [SerializeField] private float dodgeRaycastDistance = 1f;

        [Header("Ravager - Fly")]
        [SerializeField] private float flyHeight = 3f;
        [SerializeField] private float flySpeed = 2f;
        [SerializeField] private float flyRaycastRange = 1.5f;
        [SerializeField] private int projectileCountPerPhase = 3;
        [SerializeField] private float shootCooldown = 0.3f;
        public int flyChancePercent = 50;

        [Header("Ravager - Dive")]
        [SerializeField] private float diveSpeed = 5f;
        [SerializeField] private int diveDamage = 15;

        [Header("Ravager - Projectile")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private float projectileSpeed = 4f;
        [SerializeField] private int projectileDamage = 5;

        [Header("Ravager - Boundary")]
        [SerializeField] private float boundaryMinX = 0f;
        [SerializeField] private float boundaryMaxX = 10f;
        [SerializeField] private float boundaryMinY = 0f;
        [SerializeField] private float boundaryMaxY = 10f;

        [Header("Ravager - Debug")]
        [SerializeField] private bool drawBoundary = true;

        public float ChaseSpeedMultiplier => chaseSpeedMultiplier;
        public float FlyHeight => flyHeight;
        public float FlySpeed => flySpeed;
        public float DiveSpeed => diveSpeed;
        public int ProjectileCountPerPhase => projectileCountPerPhase;
        public float ProjectileSpeed => projectileSpeed;
        public int ProjectileDamage => projectileDamage;
        public int DodgeChancePercent => dodgeChancePercent;
        public float DodgeBackDistance => dodgeBackDistance;
        public float DodgeRaycastDistance => dodgeRaycastDistance;
        public GameObject ProjectilePrefab => projectilePrefab;
        public float ShootCooldown => shootCooldown;
        public int DiveDamage => diveDamage;
        public float FlyRaycastRange => flyRaycastRange;

        private RavagerChaseState ravagerChaseState;
        private RavagerGroundCombatState groundCombatState;
        private RavagerFlyState flyState;
        private RavagerDiveState diveState;
        private RavagerDodgeState dodgeState;

        protected override void Awake()
        {
            base.Awake();

            ravagerChaseState = new RavagerChaseState(this);
            groundCombatState = new RavagerGroundCombatState(this);
            flyState = new RavagerFlyState(this);
            diveState = new RavagerDiveState(this);
            dodgeState = new RavagerDodgeState(this);
        }

        protected override void Start()
        {
            base.Start();
        }

        public bool IsWithinBoundary()
        {
            float x = transform.position.x;
            float y = transform.position.y;
            return x >= boundaryMinX && x <= boundaryMaxX && y >= boundaryMinY && y <= boundaryMaxY;
        }

        public Vector2 ClampToBoundary(Vector2 position)
        {
            position.x = Mathf.Clamp(position.x, boundaryMinX, boundaryMaxX);
            position.y = Mathf.Clamp(position.y, boundaryMinY, boundaryMaxY);
            return position;
        }

        public Vector2 GetRandomBoundaryPoint(float padding = 0.75f)
        {
            float minX = Mathf.Min(boundaryMinX, boundaryMaxX) + padding;
            float maxX = Mathf.Max(boundaryMinX, boundaryMaxX) - padding;
            float minY = Mathf.Min(boundaryMinY, boundaryMaxY) + padding;
            float maxY = Mathf.Max(boundaryMinY, boundaryMaxY) - padding;

            if (minX > maxX)
            {
                float centerX = (boundaryMinX + boundaryMaxX) * 0.5f;
                minX = centerX;
                maxX = centerX;
            }

            if (minY > maxY)
            {
                float centerY = (boundaryMinY + boundaryMaxY) * 0.5f;
                minY = centerY;
                maxY = centerY;
            }

            return new Vector2(
                Random.Range(minX, maxX),
                Random.Range(minY, maxY)
            );
        }

        public void SetStateToRavagerChase()
        {
            StateMachine.SetState(ravagerChaseState);
        }

        public void SetStateToGroundCombat()
        {
            StateMachine.SetState(groundCombatState);
        }

        public void SetStateToFly()
        {
            StateMachine.SetState(flyState);
        }

        public void SetStateToDive()
        {
            StateMachine.SetState(diveState);
        }

        public void SetStateToDodge()
        {
            StateMachine.SetState(dodgeState);
        }

        public override void SetStateToChase()
        {
            // Override để vào RavagerChaseState thay vì BaoEnemyChaseState
            StateMachine.SetState(ravagerChaseState);
        }

        public override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            if (!drawBoundary)
            {
                return;
            }

            Gizmos.color = Color.green;
            Vector3 bottomLeft = new Vector3(boundaryMinX, boundaryMinY, 0);
            Vector3 bottomRight = new Vector3(boundaryMaxX, boundaryMinY, 0);
            Vector3 topLeft = new Vector3(boundaryMinX, boundaryMaxY, 0);
            Vector3 topRight = new Vector3(boundaryMaxX, boundaryMaxY, 0);

            Gizmos.DrawLine(bottomLeft, bottomRight);
            Gizmos.DrawLine(bottomRight, topRight);
            Gizmos.DrawLine(topRight, topLeft);
            Gizmos.DrawLine(topLeft, bottomLeft);
        }
    }
}
