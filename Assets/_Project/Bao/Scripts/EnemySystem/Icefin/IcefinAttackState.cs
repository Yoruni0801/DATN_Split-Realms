using UnityEngine;

namespace Bao.Enemies
{
    public class IcefinAttackState : BaoEnemyState
    {
        private readonly IcefinEnemy icefin;

        private enum Phase { Anticipation, Dash, Recover }
        private Phase currentPhase;
        private float phaseTimer;

private Vector2 dashDirection;
        private readonly Collider2D[] overlapResults = new Collider2D[8];

        public bool IsDashing => currentPhase == Phase.Dash;

        public IcefinAttackState(BaoEnemyBase enemy) : base(enemy)
        {
            icefin = enemy as IcefinEnemy;
        }

        public override void Enter()
        {
            icefin.MarkAttackStarted();
            icefin.ShowWarningNow();

            if (icefin.Animator != null)
            {
                icefin.Animator.ResetTrigger("Die");
            }

            icefin.VfxController?.TriggerAnticipationStart(icefin.AnticipationDuration);
            StartPhase(Phase.Anticipation);
        }

        private void StartPhase(Phase phase)
        {
            currentPhase = phase;
            phaseTimer = 0f;

            switch (phase)
            {
                case Phase.Anticipation:
                    // Đứng yên xoay tại chỗ hoặc bay lơ lửng chờ (isAttack chưa bật nên vẫn anim Fly)
                    icefin.Rigidbody2D.linearVelocity = Vector2.zero;
                    if (icefin.Target != null)
                    {
                        icefin.FacePosition(icefin.Target.position);
                    }
                    break;

                case Phase.Dash:
                    icefin.StopWarningRoutine();
                    icefin.PlayAttackAnimation();

                    if (icefin.Animator != null)
                    {
                        icefin.Animator.SetBool("isAttack", true); // Bắt đầu phát anim xoay để lao tới
                    }

                    if (icefin.Target != null)
                    {
                        // Lấy hướng bay thẳng vào người chơi
                        dashDirection = ((Vector2)icefin.Target.position - (Vector2)icefin.transform.position).normalized;
                        icefin.FacePosition(icefin.Target.position);
                    }
                    else
                    {
                        dashDirection = Vector2.right * icefin.FacingDirection;
                    }

                    if (dashDirection.sqrMagnitude < 0.0001f)
                        dashDirection = Vector2.right * icefin.FacingDirection;

                    icefin.SetDashDirection(dashDirection);
                    icefin.BeginDashDamageWindow();
                    icefin.VfxController?.TriggerDashStart(dashDirection, icefin.DashDuration);
                    break;

                case Phase.Recover:
                    icefin.Rigidbody2D.linearVelocity = Vector2.zero;
                    if (icefin.Animator != null)
                    {
                        icefin.Animator.SetBool("isAttack", false);
                    }

                    icefin.VfxController?.TriggerDashEnd();
                    break;
            }
        }

        public override void LogicUpdate()
        {
            phaseTimer += Time.deltaTime;

            switch (currentPhase)
            {
                case Phase.Anticipation:
                    // Update mặt theo hướng player liên tục khi đang xoay lấy đà
                    if (icefin.Target != null)
                    {
                        icefin.FacePosition(icefin.Target.position);
                    }

                    if (phaseTimer >= icefin.AnticipationDuration)
                    {
                        StartPhase(Phase.Dash);
                    }
                    break;

                case Phase.Dash:
                    Collider2D icefinCollider = icefin.Collider2D;
                    if (icefinCollider == null)
                    {
                        StartPhase(Phase.Recover);
                        break;
                    }

                    Bounds bounds = icefinCollider.bounds;
                    Vector2 movementDirection = dashDirection.sqrMagnitude > 0.0001f
                        ? dashDirection.normalized
                        : Vector2.right * icefin.FacingDirection;
                    RaycastHit2D environmentHit = Physics2D.BoxCast(
                        bounds.center,
                        bounds.size * 0.9f,
                        0f,
                        movementDirection,
                        0.15f,
                        icefin.EnvironmentLayer);
                    bool hitEnvironment = environmentHit.collider != null && !environmentHit.collider.isTrigger;

                    bool hitPlayer = false;
                    ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
                    int count = icefinCollider.Overlap(filter, overlapResults);
                    for (int i = 0; i < count; i++)
                    {
                        Collider2D result = overlapResults[i];
                        if (result == null || result == icefinCollider || result.GetComponentInParent<PlayerHealth>() == null)
                            continue;

                        hitPlayer = icefin.TryDamagePlayer(result, movementDirection);

                        // Nếu state đã chuyển sang Stun (do bị Parry), ngắt ngay
                        if (icefin.StateMachine != null && icefin.StateMachine.CurrentState != this)
                        {
                            return;
                        }

                        if (hitPlayer)
                            break;
                    }

                    bool timedOut = phaseTimer >= icefin.DashDuration;
                    if (hitEnvironment || hitPlayer || timedOut)
                    {
                        if (hitEnvironment && !hitPlayer)
                            icefin.VfxController?.TriggerDashImpact(environmentHit.point, movementDirection, false);

                        StartPhase(Phase.Recover);
                    }
                    break;

                case Phase.Recover:
                    if (phaseTimer >= 0.5f)
                    {
                        if (icefin.HasTargetInDetectionRange())
                            icefin.SetStateToChase();
                        else
                            icefin.SetStateToPatrol();
                    }
                    break;
            }
        }

        public override void PhysicsUpdate()
        {
            if (icefin.StateMachine != null && icefin.StateMachine.CurrentState != this)
                return;

            switch (currentPhase)
            {
                case Phase.Dash:
                    if (icefin.Rigidbody2D != null)
                    {
                        icefin.Rigidbody2D.linearVelocity = dashDirection * icefin.DashSpeed;
                    }
                    break;
            }
        }

        public override void Exit()
        {
            icefin.StopWarningRoutine();
            icefin.Rigidbody2D.linearVelocity = Vector2.zero;
            icefin.VfxController?.TriggerDashEnd();

            if (icefin.Animator != null)
            {
                icefin.Animator.SetBool("isAttack", false);
            }
        }
    }
}
