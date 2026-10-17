using UnityEngine;

public partial class DuyBossController
{
    private bool IsValidTarget(Transform candidate)
    {
        if (candidate == null || !candidate.gameObject.activeInHierarchy)
        {
            return false;
        }

        return (playerLayer.value & (1 << candidate.gameObject.layer)) != 0;
    }

    private void ActivateAggro(Transform attacker)
    {
        if (attacker == null)
        {
            return;
        }

        Transform root = attacker.root;
        if (!IsValidTarget(root))
        {
            return;
        }

        target = root;
        aggroTarget = root;
        isAggro = true;
        decisionTimer = 0f;
        targetOutOfRangeTimer = 0f;
        CacheTargetComponents();

        if (!IsHardLocked)
        {
            StartAggroApproach();
        }
    }

    private void StartAggroApproach()
    {
        if (target == null || state == BossState.Dead)
        {
            return;
        }

        float distance = DistanceToTarget();
        UpdateCombatZone(distance);
        if (!isChaseMode)
        {
            BeginCombatRecovery(CanStartRangedRotation(distance) ? CombatPhase.Ranged : CombatPhase.Melee, 0.2f, 0.4f);
            return;
        }

        int direction = DirectionToTarget();
        if (CanDash() && CanMoveSafely(direction, dashSpeed))
        {
            TryEnterDash(direction, DashMode.GapClose);
            return;
        }

        moveDirection = direction;
        EnterState(BossState.Chase, RandomDecisionInterval());
    }
}
