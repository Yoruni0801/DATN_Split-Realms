using UnityEngine;

namespace Bao.Enemies
{
    public class EmberwormEnemy : BaoEnemyBase, IParryable
    {
        [Header("Emberworm - Timing")]
        [SerializeField] private float delayBeforeEmerge = 2f;        // Thời gian chờ trước khi mọc lên (lần đầu)
        [SerializeField] private float emergeTime = 1.5f;             // Thời gian mọc lên
        [SerializeField] private float activeTime = 3f;               // Thời gian ở trên mặt đất
        [SerializeField] private float retractTime = 1.5f;            // Thời gian rút lại

        [Header("Emberworm - Position")]
        [SerializeField] private float emergeDistance = 3f;           // Khoảng cách trồi lên (theo hướng Up của transform)
        
        private Vector3 emergingPosition;                             // Vị trí trên mặt đất (mục tiêu)
        private Vector3 hiddenPosition;                               // Vị trí ẩn dưới đất (vị trí ban đầu)

        [Header("Emberworm - Loop")]
        [SerializeField] private bool isLooping = true;               // Có lặp lại không
        [SerializeField] private float loopDelay = 1f;                // Thời gian chờ giữa các chu kỳ sau lần đầu

        [Header("Emberworm - Damage")]
        [SerializeField] private int damage = 10;                     // Sát thương gây ra cho player
        [SerializeField] private float damageCooldown = 1f;           // Thời gian hồi sát thương giữa các lần chạm
        private float lastDamageTime = 0f;

        public float DelayBeforeEmerge => delayBeforeEmerge;
        public float EmergeTime => emergeTime;
        public float ActiveTime => activeTime;
        public float RetractTime => retractTime;
        public Vector3 EmergingPosition { get => emergingPosition; set => emergingPosition = value; }
        public Vector3 HiddenPosition { get => hiddenPosition; set => hiddenPosition = value; }
        public bool IsLooping => isLooping;
        public float LoopDelay => loopDelay;
        public int Damage => damage;
        public float DamageCooldown => damageCooldown;

        public EmberwormIdleState IdleState { get; private set; }
        public EmberwormEmergeState EmergeState { get; private set; }
        public EmberwormActiveState ActiveState { get; private set; }
        public EmberwormRetractState RetractState { get; private set; }

        public bool IsFirstEmerge { get; set; } = true;

        protected override void Awake()
        {
            base.Awake();
            IdleState = new EmberwormIdleState(this);
            EmergeState = new EmberwormEmergeState(this);
            ActiveState = new EmberwormActiveState(this);
            RetractState = new EmberwormRetractState(this);
        }

        protected override void Start()
        {
            // Không gọi base.Start() để tránh set state sang PatrolState mặc định của BaoEnemyBase
            hiddenPosition = transform.position;
            emergingPosition = hiddenPosition + (transform.up * emergeDistance);

            ApplyFacing(FacingDirection);
            
            // Bắt đầu với IdleState
            StateMachine.SetState(IdleState);
        }

        public override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = Color.red;
            
            // Draw a line to the target emerge position
            Vector3 targetPos = Application.isPlaying ? emergingPosition : transform.position + (transform.up * emergeDistance);
            Gizmos.DrawLine(transform.position, targetPos);
            Gizmos.DrawWireSphere(targetPos, 0.5f);
        }

        public override void SetStateToAttack() { }
        public override void SetStateToChase() { }
        public override void SetStateToPatrol() { }

        public override void TakeDamage(int damage)
        {
            // Emberworm là bất tử, bỏ qua sát thương
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.CompareTag("Player"))
            {
                TryDealDamage(collision.gameObject);
            }
        }

        private void OnTriggerStay2D(Collider2D collision)
        {
            if (collision.CompareTag("Player"))
            {
                TryDealDamage(collision.gameObject);
            }
        }

        private void TryDealDamage(GameObject playerObj)
        {
            // Chỉ gây sát thương khi đang mọc lên hoặc đang hoạt động trên mặt đất
            if (StateMachine.CurrentState == EmergeState || StateMachine.CurrentState == ActiveState)
            {
                if (Time.time - lastDamageTime >= damageCooldown)
                {
                    Collider2D playerCollider = playerObj.GetComponent<Collider2D>();
                    if (playerCollider != null)
                    {
                        PlayerHitResolver.TryApplyHitToPlayer(
                            playerCollider,
                            damage,
                            ParryableAttackType.Melee,
                            transform,
                            playerObj.transform.position - transform.position,
                            canBeBlocked: false);
                        lastDamageTime = Time.time;
                    }
                }
            }
        }

        // Hỗ trợ kích hoạt nhô lên từ bên ngoài
        public void TriggerEmerge()
        {
            StateMachine.SetState(IdleState);
        }

        public ParryResult OnParried(ParryContext context)
        {
            // Quái này không thể parry
            return ParryResult.Failed;
        }
    }
}
