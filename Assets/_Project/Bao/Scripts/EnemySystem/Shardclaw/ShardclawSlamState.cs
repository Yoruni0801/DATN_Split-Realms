using UnityEngine;

namespace Bao.Enemies
{
    public class ShardclawSlamState : BaoEnemyState
    {
        private readonly ShardclawEnemy shardclaw;

        private enum Phase
        {
            Anticipation,
            Attack
        }

        private Phase currentPhase;
        private float phaseTimer;
        private bool hasSpawnedVFX;

        public ShardclawSlamState(BaoEnemyBase enemy) : base(enemy)
        {
            shardclaw = enemy as ShardclawEnemy;
        }

        public override void Enter()
        {
            shardclaw.MarkAttackStarted();
            shardclaw.ShowWarningNow();

            shardclaw.StopHorizontalMovement();
            shardclaw.SetLocomotionAnimation(false);

            StartPhase(Phase.Anticipation);
        }

        private void StartPhase(Phase phase)
        {
            currentPhase = phase;
            phaseTimer = 0f;

            if (phase == Phase.Anticipation)
            {
                if (shardclaw.Target != null)
                    shardclaw.FacePosition(shardclaw.Target.position);

                // NEW:
                // Ground energy is pulled into the amber crystals while the enemy charges.
                shardclaw.VfxController?.TriggerChargeStart(shardclaw.AnticipationDuration);
            }
            else if (phase == Phase.Attack)
            {
                shardclaw.StopWarningRoutine();
                hasSpawnedVFX = false;
                shardclaw.PlayAttackAnimation();
            }
        }

        public override void LogicUpdate()
        {
            phaseTimer += Time.deltaTime;

            switch (currentPhase)
            {
                case Phase.Anticipation:
                    if (shardclaw.Target != null)
                        shardclaw.FacePosition(shardclaw.Target.position);

                    if (phaseTimer >= shardclaw.AnticipationDuration)
                        StartPhase(Phase.Attack);
                    break;

                case Phase.Attack:
                    // Keep the gameplay timing identical to the original:
                    // VFX + damage happen at 40% of the attack duration.
                    if (!hasSpawnedVFX && phaseTimer >= shardclaw.SlamDuration * 0.4f)
                    {
                        Vector3 spawnPos = GetSpawnPosition();

                        SpawnSlamLavaVFX(spawnPos);
                        DealDamage(spawnPos);

                        // NEW:
                        // VFX center and radius now exactly match the gameplay hit.
                        shardclaw.VfxController?.TriggerSlamImpact(
                            spawnPos,
                            shardclaw.EmergeDamageRadius);

                        hasSpawnedVFX = true;
                    }

                    if (phaseTimer >= shardclaw.SlamDuration)
                    {
                        if (shardclaw.HasTargetInDetectionRange())
                            shardclaw.SetStateToChase();
                        else
                            shardclaw.SetStateToPatrol();
                    }
                    break;
            }
        }

        public override void PhysicsUpdate()
        {
            shardclaw.StopHorizontalMovement();
        }

        private Vector3 GetSpawnPosition()
        {
            return shardclaw.VfxSpawnPoint != null
                ? shardclaw.VfxSpawnPoint.position
                : shardclaw.transform.position;
        }

        private void SpawnSlamLavaVFX(Vector3 spawnPos)
        {
            if (shardclaw.EarthSlamPrefab != null)
            {
                GameObject earthInstance = Object.Instantiate(
                    shardclaw.EarthSlamPrefab,
                    spawnPos,
                    shardclaw.EarthSlamSpawnRotation);

                Vector3 scale = earthInstance.transform.localScale;
                earthInstance.transform.localScale = new Vector3(
                    Mathf.Abs(scale.x) * shardclaw.FacingDirection,
                    scale.y,
                    scale.z);

                Object.Destroy(earthInstance, 3.0f);
            }

            if (shardclaw.LavaPrefab != null)
            {
                GameObject lavaInstance = Object.Instantiate(
                    shardclaw.LavaPrefab,
                    spawnPos,
                    shardclaw.LavaSpawnRotation);

                Vector3 scale = lavaInstance.transform.localScale;
                lavaInstance.transform.localScale = new Vector3(
                    Mathf.Abs(scale.x) * shardclaw.FacingDirection,
                    scale.y,
                    scale.z);

                Object.Destroy(lavaInstance, 4.0f);
            }
        }

        private void DealDamage(Vector3 spawnPos)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                spawnPos,
                shardclaw.EmergeDamageRadius);

            foreach (Collider2D hit in hits)
            {
                if (!hit.CompareTag("Player"))
                    continue;

                PlayerHitResolver.TryApplyHitToPlayer(
                    hit,
                    shardclaw.SlamDamage,
                    ParryableAttackType.Melee,
                    EnemyParryResponder.Ensure(shardclaw),
                    shardclaw.transform,
                    hit.transform.position - shardclaw.transform.position);
            }
        }
    }
}
