using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bao.Bosses
{
    // ==========================================
    // IDLE STATE
    // ==========================================
    public class LithosIdleState : LithosMonolithBossState
    {
        public LithosIdleState(LithosMonolithBoss boss) : base(boss) { }

        public override void Enter()
        {
            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isIdle", true);
                boss.Animator.SetBool("isIdlePhase2", boss.CurrentPhase == BossPhase.Phase2);
            }
        }

        public override void LogicUpdate()
        {
            if (boss.IsDead || boss.IsTransitioningPhases) return;

            if (boss.IsTargetWithinDetectionRange())
            {
                // Try to pick the best skill
                PickAction();
            }
        }

        private void PickAction()
        {
            if (!boss.CanPerformAction()) return;

            float distToTarget = Vector2.Distance(boss.CenterPosition, boss.Target.position);

            // Phase 2: alternate between orbit AOE and barrage, with rock throw as filler
            if (boss.CurrentPhase == BossPhase.Phase2)
            {
                // Prioritize AOE skills, with variety logic
                if (boss.CanOrbit() && boss.LastUsedSkillCategory != LithosMonolithBoss.SkillCategory.AOE)
                {
                    boss.ChangeState(boss.OrbitAOEState);
                    return;
                }

                if (boss.CanBarrage() && boss.LastUsedSkillCategory != LithosMonolithBoss.SkillCategory.Ranged)
                {
                    boss.ChangeState(boss.RockBarrageState);
                    return;
                }

                // Fall through to rock throw
                if (boss.CanOrbit())
                {
                    boss.ChangeState(boss.OrbitAOEState);
                    return;
                }

                if (boss.CanBarrage())
                {
                    boss.ChangeState(boss.RockBarrageState);
                    return;
                }
            }

            // Phase 1 & Phase 2 filler: Rock Throw
            if (boss.CanRockThrow() && distToTarget <= boss.AttackRange)
            {
                boss.ChangeState(boss.RockThrowState);
                return;
            }
        }

        public override void Exit()
        {
            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isIdle", false);
                boss.Animator.SetBool("isIdlePhase2", false);
            }
        }
    }

    // ==========================================
    // ROCK THROW STATE (Phase 1 & 2)
    // ==========================================
    public class LithosRockThrowState : LithosMonolithBossState
    {
        private float enterTime;
        private Coroutine throwRoutine;

        public LithosRockThrowState(LithosMonolithBoss boss) : base(boss) { }

        public override void Enter()
        {
            enterTime = Time.time;
            boss.MarkRockThrowUsed();

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isIdle", false);
                boss.Animator.SetTrigger("Attack");
            }

            throwRoutine = boss.StartCoroutine(RockThrowSequence());
        }

        private IEnumerator RockThrowSequence()
        {
            // Wait for wind-up delay
            yield return new WaitForSeconds(boss.RockThrowDelay);

            if (boss.IsDead || boss.Target == null) yield break;

            // Pick a random rock projectile from the attack objects
            if (boss.RockProjectiles != null && boss.RockProjectiles.Length > 0)
            {
                int idx = Random.Range(0, boss.RockProjectiles.Length);
                GameObject rockPrefab = boss.RockProjectiles[idx];
                if (rockPrefab != null)
                {
                    Vector3 spawnPos = boss.CenterPosition + Vector3.up * 2f;
                    Vector2 dir = boss.GetDirectionToTarget();

                    GameObject rock = Object.Instantiate(rockPrefab, spawnPos, Quaternion.identity);
                    rock.SetActive(true);

                    // Add projectile behavior
                    LithosRockProjectile proj = rock.GetComponent<LithosRockProjectile>();
                    if (proj == null)
                        proj = rock.AddComponent<LithosRockProjectile>();

                    proj.Initialize(dir, boss.RockThrowSpeed, boss.RockThrowDamage, 6f);

                    // VFX for throw
                    boss.ShaderController?.SpawnThrowVFX(spawnPos);
                }
            }

            // Wait for remaining animation
            float remaining = boss.RockThrowAnimDuration - boss.RockThrowDelay;
            if (remaining > 0f)
                yield return new WaitForSeconds(remaining);

            boss.ChangeState(boss.CooldownState);
        }

        public override void Exit()
        {
            if (throwRoutine != null)
                boss.StopCoroutine(throwRoutine);
            if (boss.Animator != null)
                boss.Animator.ResetTrigger("Attack");
        }
    }

    // ==========================================
    // ORBIT AOE STATE (Phase 2 Only)
    // ==========================================
    public class LithosOrbitAOEState : LithosMonolithBossState
    {
        private Coroutine orbitRoutine;

        public LithosOrbitAOEState(LithosMonolithBoss boss) : base(boss) { }

        public override void Enter()
        {
            boss.MarkOrbitUsed();

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isIdle", false);
                boss.Animator.SetBool("isOrbit", true);
            }

            orbitRoutine = boss.StartCoroutine(OrbitSequence());
        }

        private IEnumerator OrbitSequence()
        {
            if (boss.OrbitStones == null || boss.OrbitStones.Length == 0)
            {
                Debug.LogWarning("[LithosMonolith] No orbit stones assigned!");
                yield return new WaitForSeconds(0.5f);
                boss.ChangeState(boss.CooldownState);
                yield break;
            }

            // VFX: Shake + glow to signal the attack
            boss.ShaderController?.TriggerScreenShake(0.3f, 0.08f);
            boss.ShaderController?.TriggerOrbitGlow(true);

            float elapsed = 0f;
            float lastDamageTime = 0f;
            int stoneCount = boss.OrbitStones.Length;
            Vector3 center = boss.CenterPosition;

            // Gather phase: pull stones to orbit radius (0.5s)
            float gatherTime = 0.5f;
            float gatherElapsed = 0f;
            Vector3[] startPositions = new Vector3[stoneCount];
            for (int i = 0; i < stoneCount; i++)
            {
                if (boss.OrbitStones[i] != null)
                    startPositions[i] = boss.OrbitStones[i].position;
            }

            while (gatherElapsed < gatherTime)
            {
                gatherElapsed += Time.deltaTime;
                float gt = Mathf.SmoothStep(0f, 1f, gatherElapsed / gatherTime);
                center = boss.CenterPosition;

                for (int i = 0; i < stoneCount; i++)
                {
                    if (boss.OrbitStones[i] == null) continue;
                    float angle = (360f / stoneCount) * i * Mathf.Deg2Rad;
                    Vector3 targetOrbitPos = center + new Vector3(
                        Mathf.Cos(angle) * boss.OrbitRadius,
                        Mathf.Sin(angle) * boss.OrbitRadius, 0f);
                    boss.OrbitStones[i].position = Vector3.Lerp(startPositions[i], targetOrbitPos, gt);
                }
                yield return null;
            }

            // Orbit phase: spin stones around center
            while (elapsed < boss.OrbitDuration)
            {
                elapsed += Time.deltaTime;
                center = boss.CenterPosition;

                for (int i = 0; i < stoneCount; i++)
                {
                    if (boss.OrbitStones[i] == null) continue;
                    float baseAngle = (360f / stoneCount) * i;
                    float currentAngle = (baseAngle + elapsed * boss.OrbitSpeed) * Mathf.Deg2Rad;
                    Vector3 orbitPos = center + new Vector3(
                        Mathf.Cos(currentAngle) * boss.OrbitRadius,
                        Mathf.Sin(currentAngle) * boss.OrbitRadius, 0f);
                    boss.OrbitStones[i].position = orbitPos;

                    // Spin the stone itself for visual flair
                    boss.OrbitStones[i].Rotate(0f, 0f, boss.OrbitSpeed * 2f * Time.deltaTime);
                }

                // Damage check
                if (Time.time - lastDamageTime >= boss.OrbitDamageInterval)
                {
                    lastDamageTime = Time.time;
                    for (int i = 0; i < stoneCount; i++)
                    {
                        if (boss.OrbitStones[i] == null) continue;
                        DamageInRadius(boss.OrbitStones[i].position, boss.OrbitDamageRadius, boss.OrbitDamage);
                    }
                }

                yield return null;
            }

            // Return stones to original positions
            Vector3[] currentPositions = new Vector3[stoneCount];
            for (int i = 0; i < stoneCount; i++)
            {
                if (boss.OrbitStones[i] != null)
                    currentPositions[i] = boss.OrbitStones[i].position;
            }

            float returnTime = 0.6f;
            float returnElapsed = 0f;
            while (returnElapsed < returnTime)
            {
                returnElapsed += Time.deltaTime;
                float rt = Mathf.SmoothStep(0f, 1f, returnElapsed / returnTime);
                for (int i = 0; i < stoneCount; i++)
                {
                    if (boss.OrbitStones[i] == null) continue;
                    boss.OrbitStones[i].position = Vector3.Lerp(
                        currentPositions[i],
                        boss.transform.TransformPoint(boss.OrbitStones[i].parent != null
                            ? boss.OrbitStones[i].parent.InverseTransformPoint(currentPositions[i])
                            : currentPositions[i]),
                        0f); // We'll just reset properly below
                }
                yield return null;
            }

            boss.ResetOrbitStonePositions();
            boss.ShaderController?.TriggerOrbitGlow(false);

            // Remaining anim time
            float remaining = boss.OrbitAnimDuration - boss.OrbitDuration - gatherTime - returnTime;
            if (remaining > 0f)
                yield return new WaitForSeconds(remaining);

            boss.ChangeState(boss.CooldownState);
        }

        private void DamageInRadius(Vector3 pos, float radius, int damage)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(pos, radius, boss.PlayerLayer);
            foreach (var hit in hits)
            {
                Bao.Enemies.IDamageable damageable = hit.GetComponent<Bao.Enemies.IDamageable>();
                if (damageable != null && damageable != (Bao.Enemies.IDamageable)boss)
                    damageable.TakeDamage(damage);
            }
        }

        public override void Exit()
        {
            if (orbitRoutine != null)
                boss.StopCoroutine(orbitRoutine);
            boss.ResetOrbitStonePositions();
            boss.ShaderController?.TriggerOrbitGlow(false);
            if (boss.Animator != null)
                boss.Animator.SetBool("isOrbit", false);
        }
    }

    // ==========================================
    // ROCK BARRAGE STATE (Phase 2 Only)
    // ==========================================
    public class LithosRockBarrageState : LithosMonolithBossState
    {
        private Coroutine barrageRoutine;

        public LithosRockBarrageState(LithosMonolithBoss boss) : base(boss) { }

        public override void Enter()
        {
            boss.MarkBarrageUsed();

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isIdle", false);
                boss.Animator.SetTrigger("Barrage");
            }

            barrageRoutine = boss.StartCoroutine(BarrageSequence());
        }

        private IEnumerator BarrageSequence()
        {
            // Wind-up delay
            yield return new WaitForSeconds(boss.BarrageDelay);

            if (boss.IsDead || boss.Target == null) yield break;

            // Fire multiple rocks in a spread pattern
            for (int i = 0; i < boss.BarrageCount; i++)
            {
                if (boss.Target == null) break;

                Vector2 baseDir = boss.GetDirectionToTarget();

                // Add spread angle
                float spreadAngle = Random.Range(-25f, 25f);
                float rad = spreadAngle * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(
                    baseDir.x * Mathf.Cos(rad) - baseDir.y * Mathf.Sin(rad),
                    baseDir.x * Mathf.Sin(rad) + baseDir.y * Mathf.Cos(rad)
                ).normalized;

                // Spawn position varies slightly
                Vector3 spawnPos = boss.CenterPosition + new Vector3(
                    Random.Range(-1f, 1f), Random.Range(1f, 3f), 0f);

                // Pick random rock
                if (boss.RockProjectiles != null && boss.RockProjectiles.Length > 0)
                {
                    int idx = Random.Range(0, boss.RockProjectiles.Length);
                    GameObject rockPrefab = boss.RockProjectiles[idx];
                    if (rockPrefab != null)
                    {
                        GameObject rock = Object.Instantiate(rockPrefab, spawnPos, Quaternion.identity);
                        rock.SetActive(true);

                        LithosRockProjectile proj = rock.GetComponent<LithosRockProjectile>();
                        if (proj == null)
                            proj = rock.AddComponent<LithosRockProjectile>();

                        float speed = boss.BarrageSpeed + Random.Range(-2f, 2f);
                        proj.Initialize(dir, speed, boss.BarrageDamage, 5f);

                        boss.ShaderController?.SpawnThrowVFX(spawnPos);
                    }
                }

                yield return new WaitForSeconds(boss.BarrageInterval);
            }

            // Wait for remaining animation
            float remaining = boss.BarrageAnimDuration - boss.BarrageDelay - (boss.BarrageCount * boss.BarrageInterval);
            if (remaining > 0f)
                yield return new WaitForSeconds(remaining);

            boss.ChangeState(boss.CooldownState);
        }

        public override void Exit()
        {
            if (barrageRoutine != null)
                boss.StopCoroutine(barrageRoutine);
            if (boss.Animator != null)
                boss.Animator.ResetTrigger("Barrage");
        }
    }

    // ==========================================
    // COOLDOWN STATE
    // ==========================================
    public class LithosCooldownState : LithosMonolithBossState
    {
        public LithosCooldownState(LithosMonolithBoss boss) : base(boss) { }

        public override void Enter()
        {
            boss.EnterCooldownState();
        }

        public override void LogicUpdate()
        {
            if (boss.IsDead)
                return;

            boss.StopMoving();

            if (boss.FacePlayerDuringCooldown && boss.Target != null)
            {
                boss.FacePosition(boss.Target.position);
            }
        }

        public override void PhysicsUpdate()
        {
            boss.StopMoving();
        }

        public override void Exit()
        {
            boss.ExitCooldownState();
        }
    }

    // ==========================================
    // DEAD STATE
    // ==========================================
    public class LithosDeadState : LithosMonolithBossState
    {
        public LithosDeadState(LithosMonolithBoss boss) : base(boss) { }

        public override void Enter()
        {
            Debug.Log("[LithosMonolith] DEAD!");

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isIdle", false);
                boss.Animator.SetBool("isOrbit", false);
                boss.Animator.SetTrigger("Death");
            }

            Boss15Anim spineAnim = boss.GetComponentInChildren<Boss15Anim>();
            if (spineAnim != null)
            {
                spineAnim.PlayDeath();
            }

            boss.ShaderController?.TriggerScreenShake(0.5f, 0.2f);
            boss.StartCoroutine(DeathSequence());
        }

        private IEnumerator DeathSequence()
        {
            boss.IsInvulnerable = true;

            // Crumble VFX
            boss.ShaderController?.SpawnCrumbleVFX(boss.CenterPosition);

            if (boss.ShaderController != null)
                boss.ShaderController.StartDeathDissolve(2f);

            yield return new WaitForSeconds(3f);

            // Optionally disable the boss
            boss.gameObject.SetActive(false);
        }
    }
}
