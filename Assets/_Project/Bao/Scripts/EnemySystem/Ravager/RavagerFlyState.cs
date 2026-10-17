using UnityEngine;

namespace Bao.Enemies
{
    public class RavagerFlyState : BaoEnemyState
    {
        private RavagerEnemy ravager;
        private float stateTimer;
        private float shootTimer;
        private float postShootTimer;
        private int shotsFired;
        private Vector3 wanderTargetPos;

        public RavagerFlyState(BaoEnemyBase enemy) : base(enemy)
        {
            ravager = enemy as RavagerEnemy;
        }

        public override void Enter()
        {
            stateTimer = 0f;
            shootTimer = 0f;
            postShootTimer = 0f;
            shotsFired = 0;
            ravager.Rigidbody2D.gravityScale = 0f;
            ravager.Rigidbody2D.linearVelocity = Vector2.zero;
            ravager.SetFlyingAnimation(true);
            ravager.SetLocomotionAnimation(false);
            PickNewWanderTarget();
        }

        public override void LogicUpdate()
        {
            stateTimer += Time.deltaTime;
            shootTimer += Time.deltaTime;

            while (shotsFired < ravager.ProjectileCountPerPhase && shootTimer >= ravager.ShootCooldown)
            {
                shootTimer -= ravager.ShootCooldown;
                ShootProjectileAtCurrentPlayer();
                shotsFired++;
            }

            // Chỉ dive sau khi bắn đủ số viên, rồi chờ thêm một nhịp ngắn
            if (shotsFired >= ravager.ProjectileCountPerPhase)
            {
                postShootTimer += Time.deltaTime;
                if (postShootTimer >= 0.5f)
                {
                    ravager.SetStateToDive();
                }
            }

            if (!ravager.HasTargetInDetectionRange())
            {
                ravager.SetStateToPatrol();
            }
        }

        public override void PhysicsUpdate()
        {
            if (ravager.Target == null)
            {
                ravager.Rigidbody2D.linearVelocity = Vector2.zero;
                return;
            }

            ravager.FacePosition(ravager.Target.position);

            if (Vector2.Distance(ravager.transform.position, wanderTargetPos) <= 0.35f)
            {
                PickNewWanderTarget();
            }

            // Bay tự do trong vùng giới hạn
            Vector3 moveDir = (wanderTargetPos - ravager.transform.position).normalized;
            Vector2 desiredVelocity = moveDir * ravager.FlySpeed;
            ravager.Rigidbody2D.linearVelocity = Vector2.Lerp(
                ravager.Rigidbody2D.linearVelocity,
                desiredVelocity,
                8f * Time.fixedDeltaTime
            );

            // Quét raycast xung quanh để tránh map
            if (CheckFlyCollision())
            {
                // Nếu gặp obstacle, chỉ đổi target mới để tránh giật teleport
                PickNewWanderTarget();
            }
        }

        private void ShootProjectileAtCurrentPlayer()
        {
            if (ravager.ProjectilePrefab == null)
            {
                return;
            }

            if (ravager.Target == null)
            {
                return;
            }

            ravager.FacePosition(ravager.Target.position);

            Vector2 shootDir = ((Vector2)ravager.Target.position - (Vector2)ravager.transform.position).normalized;
            if (shootDir == Vector2.zero)
            {
                shootDir = new Vector2(ravager.FacingDirection, 0f);
            }

            Vector3 spawnPos = ravager.transform.position + (Vector3)(shootDir * 0.5f);

            GameObject projectile = Object.Instantiate(
                ravager.ProjectilePrefab,
                spawnPos,
                Quaternion.identity
            );

            RavagerProjectile projScript = projectile.GetComponent<RavagerProjectile>();
            if (projScript != null)
            {
                projScript.Launch(shootDir, ravager.ProjectileSpeed, ravager.ProjectileDamage);
            }

            ravager.PlayShootAnimation();
        }

        private bool CheckFlyCollision()
        {
            RaycastHit2D[] hits = Physics2D.CircleCastAll(
                ravager.transform.position,
                ravager.FlyRaycastRange,
                Vector2.zero
            );

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider != null && hit.collider.gameObject != ravager.gameObject && !hit.collider.isTrigger)
                {
                    return true;
                }
            }

            return false;
        }

        public override void Exit()
        {
            ravager.Rigidbody2D.gravityScale = 1f;
            ravager.SetFlyingAnimation(false);
        }

        private void PickNewWanderTarget()
        {
            wanderTargetPos = ravager.GetRandomBoundaryPoint();
        }
    }
}
