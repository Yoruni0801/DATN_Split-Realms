using System.Collections;
using UnityEngine;
using Spine.Unity;

namespace Quoc.Boss
{
    public class TripleSlashAttack : BossAttackBase
    {
        public override string AttackName => "Triple Slash";

        [System.Serializable]
        public struct SlashComboStep
        {
            [SpineAnimation, Tooltip("Tên Spine Animation dành riêng cho phát chém này")]
            public string spineAnimationName;

            [Range(0.1f, 3.0f), Tooltip("Tốc độ animation Spine (Chỉnh < 1.0 để chạy chậm kéo dài thời gian animation, ví dụ 0.5 = kéo dài gấp đôi)")]
            public float animationSpeed;

            [Tooltip("Thời gian chờ trước khi tung đòn gây sát thương (giây)")]
            public float hitDelay;

            [Tooltip("Tổng thời gian diễn ra phát chém này trước khi sang phát chém tiếp theo (giây)")]
            public float stepDuration;
        }

        [Header("Combo Steps Animation & Timing")]
        [SerializeField, Tooltip("Cấu hình Spine Animation riêng, Tốc độ (Speed) và thời gian cho từng cú chém (3 phát chém)")]
        private SlashComboStep[] comboSteps = new SlashComboStep[]
        {
            new SlashComboStep { spineAnimationName = "Attack", animationSpeed = 1.0f, hitDelay = 0.15f, stepDuration = 0.40f },
            new SlashComboStep { spineAnimationName = "Skill", animationSpeed = 1.0f, hitDelay = 0.15f, stepDuration = 0.40f },
            new SlashComboStep { spineAnimationName = "Attack", animationSpeed = 1.0f, hitDelay = 0.20f, stepDuration = 0.50f }
        };

        [Header("Hitbox Settings")]
        [SerializeField, Tooltip("Bán kính vung chém gây sát thương")]
        private float attackRadius = 2.2f;

        [SerializeField, Tooltip("LayerMask của Player")]
        private LayerMask playerLayer = ~0;

        public override void Execute(BossController boss, BossAttackData data, System.Action onComplete)
        {
            base.Execute(boss, data, onComplete);
            StartCoroutine(TripleSlashRoutine());
        }

        private IEnumerator TripleSlashRoutine()
        {
            boss.Movement.StopMovement();

            if (comboSteps == null || comboSteps.Length == 0)
            {
                comboSteps = new SlashComboStep[]
                {
                    new SlashComboStep { spineAnimationName = "Attack", animationSpeed = 1.0f, hitDelay = 0.15f, stepDuration = 0.40f },
                    new SlashComboStep { spineAnimationName = "Skill", animationSpeed = 1.0f, hitDelay = 0.15f, stepDuration = 0.40f },
                    new SlashComboStep { spineAnimationName = "Attack", animationSpeed = 1.0f, hitDelay = 0.20f, stepDuration = 0.50f }
                };
            }

            for (int i = 0; i < comboSteps.Length; i++)
            {
                boss.Movement.StopMovement();
                boss.Movement.FacePlayer();

                SlashComboStep step = comboSteps[i];
                float speed = step.animationSpeed > 0f ? step.animationSpeed : 1.0f;

                // 1. Kích hoạt Spine Animation với Tốc Độ (Speed / TimeScale) được tùy chỉnh
                string animName = step.spineAnimationName;
                if (boss != null)
                {
                    if (i == 0)
                    {
                        if (!string.IsNullOrEmpty(boss.Slash1AnimName)) animName = boss.Slash1AnimName;
                        if (boss.Slash1AnimSpeed > 0f) speed = boss.Slash1AnimSpeed;
                    }
                    else if (i == 1)
                    {
                        if (!string.IsNullOrEmpty(boss.Slash2AnimName)) animName = boss.Slash2AnimName;
                        if (boss.Slash2AnimSpeed > 0f) speed = boss.Slash2AnimSpeed;
                    }
                    else if (i == 2)
                    {
                        if (!string.IsNullOrEmpty(boss.Slash3AnimName)) animName = boss.Slash3AnimName;
                        if (boss.Slash3AnimSpeed > 0f) speed = boss.Slash3AnimSpeed;
                    }
                }

                if (!string.IsNullOrEmpty(animName))
                {
                    boss.PlaySpineAnimation(animName, false, speed);
                }

                // 2. Chờ hitDelay (có tính đến tỷ lệ tốc độ animation) rồi gây sát thương
                float hitDelay = step.hitDelay > 0f ? step.hitDelay : 0.15f;
                yield return new WaitForSeconds(hitDelay);

                CheckHit();

                // 3. Chờ thời gian còn lại của animation phát chém này kết thúc mới sang phát chém tiếp theo
                float stepDuration = step.stepDuration > hitDelay ? step.stepDuration : (hitDelay + 0.2f);
                float remainingTime = stepDuration - hitDelay;
                yield return new WaitForSeconds(remainingTime);
            }

            boss.Movement.StopMovement();
            boss.PlayIdleAnimation();
            FinishAttack();
        }

        private void CheckHit()
        {
            Vector2 hitPoint = boss.MeleePoint.position;
            Collider2D[] hits = Physics2D.OverlapCircleAll(hitPoint, attackRadius, playerLayer);

            foreach (var hit in hits)
            {
                if (hit.transform == boss.transform || hit.transform.IsChildOf(boss.transform) || hit.transform.root == boss.transform.root) continue;
                DamageUtility.DealDamage(hit.gameObject, attackData != null ? attackData.damage : 20, boss.transform);
            }
        }

        private void OnDrawGizmosSelected()
        {
            BossController b = boss != null ? boss : GetComponent<BossController>();
            Transform point = b != null && b.MeleePoint != null ? b.MeleePoint : transform;

            Gizmos.color = new Color(1f, 0.4f, 0f, 0.35f);
            Gizmos.DrawSphere(point.position, attackRadius);
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(point.position, attackRadius);
        }
    }
}
