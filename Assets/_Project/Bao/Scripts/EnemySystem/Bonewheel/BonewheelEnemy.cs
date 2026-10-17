using UnityEngine;

namespace Bao.Enemies
{
    public class BonewheelEnemy : BaoEnemyBase
    {
        [Header("Bonewheel - Abilities")]
        [Tooltip("Thời gian Bonewheel tàng hình hoàn toàn trước khi xuất hiện lại.")]
        [SerializeField] private float invisibilityDuration = 1.5f;
        [Tooltip("Khoảng cách (offset) Bonewheel sẽ hiện ra ở phía sau lưng người chơi. Số dương = sau lưng.")]
        [SerializeField] private float appearOffset = 1.5f;
        [Tooltip("Thời gian chờ sau khi hiện ra rồi mới bắt đầu di chuyển/tấn công tiếp (giúp warning có đủ thời gian hiển thị)")]
        [SerializeField] private float appearDelay = 0.5f;

        [Header("Bonewheel - Combat")]
        [Tooltip("Thời gian đứng chờ (gồng/telegraph) TRƯỚC khi vung vũ khí. Chỉnh cái này để warning có đủ thời gian nhảy lên.")]
        [SerializeField] private float preAttackDelay = 0.5f;
        [Tooltip("Sát thương gây ra khi chém trúng người chơi.")]
        [SerializeField] private int attackDamage = 10;
        [Tooltip("Bán kính vùng sát thương của nhát chém.")]
        [SerializeField] private float attackRadius = 1.2f;
        [Tooltip("Thời gian tính từ lúc bắt đầu vung vũ khí đến lúc gây sát thương (thường là 0.1 - 0.2s).")]
        [SerializeField] private float attackDelay = 0.2f;

        public SpriteRenderer[] SpriteRenderers => spriteRenderers;
        public float InvisibilityDuration => invisibilityDuration;
        public float AppearOffset => appearOffset;
        public float AppearDelay => appearDelay;
        public float PreAttackDelay => preAttackDelay;
        public int AttackDamage => attackDamage;
        public float AttackRadius => attackRadius;
        public float AttackDelay => attackDelay;

        [Header("Bonewheel - Teleport")]
        [Tooltip("Khoảng cách tối đa mà Bonewheel có thể kích hoạt kỹ năng dịch chuyển ra sau lưng.")]
        [SerializeField] private float teleportRange = 5f;
        [Tooltip("Thời gian hồi chiêu (Cooldown) của kỹ năng dịch chuyển.")]
        [SerializeField] private float teleportCooldown = 4f;
        private float lastTeleportTime = -999f;
        
        public float TeleportRangeSqr => teleportRange * teleportRange;
        public float TeleportCooldown => teleportCooldown;
        public float LastTeleportTime => lastTeleportTime;

        // Đảo ngược Sprite khi di chuyển
        protected override float FacingVisualMultiplier => -1f;
        private BonewheelAttackState bonewheelAttackState;
        private BonewheelVFXController vfxController;
        private BonewheelChaseState bonewheelChaseState;
        private BonewheelTeleportState bonewheelTeleportState;

        protected override void Awake()
        {
            base.Awake();
            vfxController = GetComponent<BonewheelVFXController>();
            bonewheelAttackState = new BonewheelAttackState(this);
            bonewheelChaseState = new BonewheelChaseState(this);
            bonewheelTeleportState = new BonewheelTeleportState(this);
        }

        public override void SetStateToAttack()
        {
            StateMachine.SetState(bonewheelAttackState);
        }

        public void BeginWhipChargeVFX(float timeUntilHit)
        {
            vfxController?.BeginWhipCharge(timeUntilHit);
        }

        public void PlayWhipThrustVFX()
        {
            vfxController?.PlayWhipThrust();
        }

        public void PlayWhipImpactVFX(Vector3 hitPosition)
        {
            vfxController?.PlayWhipImpact(hitPosition);
        }

        public void CancelWhipAttackVFX()
        {
            vfxController?.CancelWhipAttack();
        }

        public override void SetStateToChase()
        {
            StateMachine.SetState(bonewheelChaseState);
        }

        public void SetStateToTeleport()
        {
            StateMachine.SetState(bonewheelTeleportState);
        }

        public bool HasTargetInTeleportRange()
        {
            if (Target == null) return false;
            float sqrDistance = ((Vector2)Target.position - (Vector2)CenterPosition).sqrMagnitude;
            return sqrDistance <= TeleportRangeSqr;
        }

        public bool CanTeleportNow()
        {
            return Time.time >= lastTeleportTime + teleportCooldown;
        }

        public void MarkTeleportStarted()
        {
            lastTeleportTime = Time.time;
        }

        public override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(CenterPosition, teleportRange);
            
            Gizmos.color = Color.red;
            Vector2 attackCenter = (Vector2)CenterPosition + new Vector2(FacingDirection * (attackRadius * 0.5f), 0f);
            Gizmos.DrawWireSphere(attackCenter, attackRadius);
        }

        private void OnDisable()
        {
            if (StateMachine != null)
            {
                if (StateMachine.CurrentState == bonewheelTeleportState || 
                    StateMachine.CurrentState == bonewheelAttackState)
                {
                    StopWarningRoutine();
                    vfxController?.CancelWhipAttack();
                    if (Animator != null)
                    {
                        Animator.ResetTrigger("Attack");
                        Animator.SetTrigger("Hit"); // Ép nhảy sang anim Hit/Stun ngay lập tức
                    }
                    SetStateToChase();
                }
            }
        }
    }
}
