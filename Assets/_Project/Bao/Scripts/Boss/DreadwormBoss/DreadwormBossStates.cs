using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Bao.Bosses
{
    // ==========================================
    // IDLE STATE
    // ==========================================
    public class DreadwormIdleState : DreadwormBossState
    {
        public DreadwormIdleState(DreadwormBoss boss) : base(boss) { }

        public override void Enter()
        {
            boss.StopHorizontalMovement();
            if (boss.Animator != null) boss.Animator.SetBool("isIdle", true);
        }

        public override void LogicUpdate()
        {
            if (boss.IsDead || boss.IsTransitioningPhases) return;

            if (boss.HasTargetInDetectionRange())
            {
                boss.ChangeState(boss.ChaseState);
            }
        }

        public override void Exit()
        {
            if (boss.Animator != null) boss.Animator.SetBool("isIdle", false);
        }
    }

    // ==========================================
    // CHASE STATE (COMBAT DECISION & COMBO ORCHESTRATOR)
    // ==========================================
    public class DreadwormChaseState : DreadwormBossState
    {
        public DreadwormChaseState(DreadwormBoss boss) : base(boss) { }

        public override void Enter()
        {
            if (boss.Animator != null) boss.Animator.SetBool("isIdle", false);
        }

        public override void LogicUpdate()
        {
            if (boss.IsDead || boss.IsTransitioningPhases) return;

            if (boss.Target != null)
                boss.FacePosition(boss.Target.position);

            if (!boss.CanPerformAction()) return;

            float distToPlayer = Vector2.Distance(boss.CenterPosition, boss.Target.position);
            List<System.Action> availableActions = new List<System.Action>();

            bool isPhase2 = boss.CurrentPhase == BossPhase.Phase2;

            // ==========================================
            // PHASE 2 ULTIMATE & HEAVY AREA SPELLS
            // ==========================================
            if (isPhase2)
            {
                // Defensive Cocoon Sanctuary
                if (boss.CanFrozenSanctuary())
                {
                    availableActions.Add(() => boss.ChangeState(boss.FrozenSanctuaryState));
                }

                // Electro Sphere Area Denial
                if (boss.CanSummonSphere() && boss.LastUsedSkillCategory != DreadwormBoss.SkillCategory.Ranged)
                {
                    for (int i = 0; i < 3; i++)
                        availableActions.Add(() => boss.ChangeState(boss.SummonSphereState));
                }

                // Crystal Spine Eruption (Mid/Long Range Trail)
                if (boss.CanSpineEruption() && distToPlayer >= boss.ChargeMinStartDistance)
                {
                    for (int i = 0; i < 4; i++)
                        availableActions.Add(() => boss.ChangeState(boss.SpineEruptionState));
                }

                // Ice Arrows (Fast Pressure / Crossfire)
                if (boss.CanIceArrow())
                {
                    for (int i = 0; i < 4; i++)
                        availableActions.Add(() => boss.ChangeState(boss.IceArrowState));
                }
            }

            // ==========================================
            // CLOSE RANGE / MELEE (TWIN SLAM COMBO)
            // ==========================================
            if (distToPlayer <= boss.ChargeMinStartDistance + 1.2f && boss.CanSlam())
            {
                int slamWeight = isPhase2 ? 4 : 6;
                for (int i = 0; i < slamWeight; i++)
                    availableActions.Add(() =>
                    {
                        boss.SlamState.ConfigureSlam(false); // Quick Slam Starter
                        boss.ChangeState(boss.SlamState);
                    });
            }

            // ==========================================
            // MID-LONG RANGE (CHARGE & DRIFT COMBO)
            // ==========================================
            if (distToPlayer >= boss.ChargeMinStartDistance && distToPlayer <= boss.ChargeRange && boss.CanCharge())
            {
                int chargeWeight = (distToPlayer > boss.ChargeMinStartDistance * 1.8f) ? 6 : 3;
                if (boss.LastUsedSkillCategory == DreadwormBoss.SkillCategory.Movement) chargeWeight = 1;

                for (int i = 0; i < chargeWeight; i++)
                    availableActions.Add(() => boss.ChangeState(boss.ChargeState));
            }

            // Fallback for Phase 2 Ranged if nothing else chosen
            if (availableActions.Count == 0 && isPhase2)
            {
                if (boss.CanIceArrow()) availableActions.Add(() => boss.ChangeState(boss.IceArrowState));
                else if (boss.CanSummonSphere()) availableActions.Add(() => boss.ChangeState(boss.SummonSphereState));
                else if (boss.CanSlam()) availableActions.Add(() => boss.ChangeState(boss.SlamState));
            }

            // Execute Chosen Attack Sequence
            if (availableActions.Count > 0)
            {
                int randomIndex = UnityEngine.Random.Range(0, availableActions.Count);
                availableActions[randomIndex].Invoke();
                return;
            }
        }

        public override void PhysicsUpdate()
        {
            if (boss.IsDead || boss.IsTransitioningPhases)
            {
                boss.StopHorizontalMovement();
                return;
            }

            if (boss.Target == null)
            {
                boss.StopHorizontalMovement();
                if (boss.Animator != null) boss.Animator.SetBool("isWalk", false);
                return;
            }

            float distanceToPlayer = Vector2.Distance(boss.CenterPosition, boss.Target.position);

            // Spacing logic: If skills are in brief recovery, maintain combat spacing
            if (!boss.CanPerformAction())
            {
                if (distanceToPlayer < boss.OptimalSpacingDistance)
                {
                    float backDir = boss.Target.position.x >= boss.transform.position.x ? -1f : 1f;
                    boss.FacePosition(boss.Target.position);
                    boss.Rigidbody2D.linearVelocity = new Vector2(backDir * boss.MoveSpeed * boss.BackingSpeedMultiplier, boss.Rigidbody2D.linearVelocity.y);
                    if (boss.Animator != null) boss.Animator.SetBool("isWalk", true);
                }
                else
                {
                    boss.StopHorizontalMovement();
                    if (boss.Animator != null) boss.Animator.SetBool("isWalk", false);
                }
                return;
            }

            if (boss.HasTargetInAttackRange())
            {
                boss.StopHorizontalMovement();
                if (boss.Animator != null) boss.Animator.SetBool("isWalk", false);
                return;
            }

            // Aggressive high-tempo close in
            boss.FacePosition(boss.Target.position);
            boss.MoveForward(boss.ChaseSpeedMultiplier);
            if (boss.Animator != null) boss.Animator.SetBool("isWalk", true);
        }

        public override void Exit()
        {
            boss.StopHorizontalMovement();
            if (boss.Animator != null) boss.Animator.SetBool("isWalk", false);
        }
    }

    // ==========================================
    // SLAM STATE (COMBO CHAINABLE: QUICK SLAM -> HEAVY SLAM)
    // ==========================================
    public class DreadwormSlamState : DreadwormBossState
    {
        private float enterTime;
        private bool hasDealtDamage;
        private bool isHeavyFollowup;
        private float activeDelay;
        private float activeRadius;
        private float activeDuration;

        public DreadwormSlamState(DreadwormBoss boss) : base(boss) { }

        public void ConfigureSlam(bool isHeavy)
        {
            isHeavyFollowup = isHeavy;
        }

        public override void Enter()
        {
            enterTime = Time.time;
            hasDealtDamage = false;

            activeDelay = isHeavyFollowup ? boss.HeavySlamDelay : boss.SlamDelay;
            activeRadius = isHeavyFollowup ? boss.HeavySlamRadius : boss.SlamRadius;
            activeDuration = activeDelay + 0.25f;

            boss.StopHorizontalMovement();
            boss.MarkSlamUsed();
            boss.ShowWarning();

            if (boss.Target != null)
                boss.FacePosition(boss.Target.position);

            boss.AudioController?.PlaySlamWindup();

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isWalk", false);
                boss.Animator.SetTrigger("isAttack");
                boss.Animator.speed = isHeavyFollowup ? 1.25f : 1.4f; // Snappy attack animation
            }

            boss.VFXController?.BeginSlamTelegraph(activeRadius, boss.FacingDirection);
        }

        public override void LogicUpdate()
        {
            float elapsed = Time.time - enterTime;
            boss.VFXController?.SetSlamTelegraphProgress(
                activeDelay > 0f ? elapsed / activeDelay : 1f);

            if (elapsed >= activeDelay && !hasDealtDamage)
            {
                boss.HideWarning();
                DealDamage();
                hasDealtDamage = true;
            }

            if (elapsed >= activeDuration)
            {
                // Combo Branching Evaluation
                if (!isHeavyFollowup && boss.Target != null)
                {
                    float distToPlayer = Vector2.Distance(boss.CenterPosition, boss.Target.position);

                    // If player is still in close proximity -> Branch into Heavy Earthquake Slam!
                    if (distToPlayer <= activeRadius + 0.8f)
                    {
                        ConfigureSlam(true);
                        Enter(); // Chain into Heavy Slam Finisher
                        return;
                    }
                }

                // Combo finished -> Enter brief recovery window
                boss.ChangeState(boss.CooldownState);
            }
        }

        private void DealDamage()
        {
            Vector2 attackPoint = (Vector2)boss.CenterPosition + Vector2.right * boss.FacingDirection * 1.5f;
            boss.VFXController?.PlaySlamImpact(
                attackPoint,
                activeRadius,
                boss.FacingDirection);

            if (isHeavyFollowup)
                boss.AudioController?.PlayHeavySlamImpact(attackPoint);
            else
                boss.AudioController?.PlaySlamImpact(attackPoint);

            if (boss.SlamVFXPrefab != null)
            {
                Vector3 spawnPos = boss.CenterPosition + new Vector3(boss.SlamVFXOffset.x * boss.FacingDirection, boss.SlamVFXOffset.y, 0f);
                GameObject vfx = Object.Instantiate(boss.SlamVFXPrefab, spawnPos, Quaternion.identity);
                Vector3 scale = vfx.transform.localScale;
                scale.x = Mathf.Abs(scale.x) * Mathf.Sign(boss.FacingDirection);
                vfx.transform.localScale = scale;
            }

            int damage = isHeavyFollowup ? Mathf.RoundToInt(boss.SlamDamage * 1.35f) : boss.SlamDamage;
            Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint, activeRadius);

            foreach (var hit in hits)
            {
                if (hit != null && hit.CompareTag("Player"))
                {
                    IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
                        hit,
                        damage,
                        ParryableAttackType.Melee,
                        EnemyParryResponder.Ensure(boss),
                        boss.transform,
                        hit.transform.position - boss.transform.position);

                    // Reward player with a satisfying Boss Micro-Stagger if parried!
                    if (resolution == IncomingAttackResolution.Parried)
                    {
                        boss.TriggerParryStagger();
                        return;
                    }
                }
            }
        }

        public override void Exit()
        {
            boss.HideWarning();
            boss.VFXController?.CancelSlamTelegraph();
            if (boss.Animator != null) boss.Animator.speed = 1f;
            isHeavyFollowup = false;
        }
    }

    // ==========================================
    // CHARGE STATE (HIGH SPEED & 180° DRIFT BRANCH)
    // ==========================================
    public class DreadwormChargeState : DreadwormBossState
    {
        private float enterTime;
        private bool hasStartedCharge;
        private bool chargeImpacted;
        private bool isDriftSlamActive;
        private CollisionDetectionMode2D previousCollisionMode;
        private float previousAnimatorSpeed;
        private readonly HashSet<Collider2D> damagedPlayers = new HashSet<Collider2D>();

        public DreadwormChargeState(DreadwormBoss boss) : base(boss) { }

        public override void Enter()
        {
            enterTime = Time.time;
            hasStartedCharge = false;
            chargeImpacted = false;
            isDriftSlamActive = false;
            damagedPlayers.Clear();
            boss.MarkChargeUsed();

            boss.StopHorizontalMovement();
            boss.ShowWarning();
            boss.AudioController?.PlayChargeWindup();

            if (boss.Target != null)
                boss.FacePosition(boss.Target.position);

            boss.VFXController?.BeginChargeTelegraph(
                boss.ChargeDelay,
                boss.FacingDirection,
                Mathf.Max(boss.ChargeRange, boss.ChargeTravelDistance));

            previousCollisionMode = boss.Rigidbody2D.collisionDetectionMode;
            previousAnimatorSpeed = boss.Animator != null ? boss.Animator.speed : 1f;

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isWalk", false);
                boss.Animator.SetTrigger("isAttack");
            }
        }

        public override void LogicUpdate()
        {
            float elapsed = Time.time - enterTime;

            if (!hasStartedCharge)
            {
                boss.VFXController?.SetChargeTelegraphProgress(
                    boss.ChargeDelay > 0f ? elapsed / boss.ChargeDelay : 1f);

                if (elapsed >= boss.ChargeDelay)
                {
                    boss.HideWarning();
                    hasStartedCharge = true;
                    enterTime = Time.time;
                    boss.Rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                    boss.VFXController?.BeginChargeDash();
                    boss.AudioController?.PlayChargeDash();

                    if (boss.Animator != null)
                    {
                        boss.Animator.SetBool("isWalk", true);
                        boss.Animator.speed = 1.75f;
                        boss.Animator.Play("Caterpillar Walk", 0, 0f);
                    }
                }
                return;
            }

            // Charge finished duration check
            if (Time.time - enterTime >= boss.ChargeMaxDuration)
            {
                // Dynamic Drift Combo Check: If player dodged behind boss, perform 180° Drift Turn Slam!
                if (!isDriftSlamActive && boss.Target != null)
                {
                    float deltaX = boss.Target.position.x - boss.transform.position.x;
                    bool playerBehind = (deltaX * boss.FacingDirection) < -0.3f;
                    float distToPlayer = Vector2.Distance(boss.CenterPosition, boss.Target.position);

                    if (playerBehind && distToPlayer <= 6.5f)
                    {
                        // Immediate 180° Drift Turn into Heavy Slam
                        isDriftSlamActive = true;
                        boss.FacePosition(boss.Target.position);
                        boss.AudioController?.PlayChargeDrift();
                        boss.VFXController?.SpawnChargeAfterimage();
                        boss.SlamState.ConfigureSlam(true);
                        boss.ChangeState(boss.SlamState);
                        return;
                    }
                }

                boss.ChangeState(boss.CooldownState);
                return;
            }
        }

        public override void PhysicsUpdate()
        {
            if (!hasStartedCharge) return;

            boss.MoveForward(boss.ChargeSpeed / boss.MoveSpeed); // High speed forward dash

            Bounds bounds = boss.Collider2D.bounds;
            Vector2 hitboxCenter = (Vector2)bounds.center + Vector2.right * boss.FacingDirection * 0.35f;
            Vector2 hitboxSize = new Vector2(
                bounds.size.x + boss.ChargeHitboxPadding.x,
                bounds.size.y + boss.ChargeHitboxPadding.y);

            Collider2D[] hits = Physics2D.OverlapBoxAll(hitboxCenter, hitboxSize, 0f);
            foreach (var hit in hits)
            {
                if (hit != null && hit.CompareTag("Player"))
                {
                    if (damagedPlayers.Contains(hit))
                        continue;

                    IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
                        hit,
                        boss.ChargeDamage,
                        ParryableAttackType.Melee,
                        EnemyParryResponder.Ensure(boss),
                        boss.transform,
                        hit.transform.position - boss.transform.position);

                    if (resolution == IncomingAttackResolution.Ignored)
                        continue;

                    // If parried by player, cancel dash and trigger micro-stagger
                    if (resolution == IncomingAttackResolution.Parried)
                    {
                        boss.TriggerParryStagger();
                        return;
                    }

                    chargeImpacted = true;
                    damagedPlayers.Add(hit);
                }
            }
        }

        public override void Exit()
        {
            boss.HideWarning();
            boss.StopHorizontalMovement();
            boss.Rigidbody2D.collisionDetectionMode = previousCollisionMode;
            boss.VFXController?.EndCharge(chargeImpacted);

            if (boss.Animator != null)
            {
                boss.Animator.speed = previousAnimatorSpeed;
                boss.Animator.SetBool("isWalk", false);
            }
        }
    }

    // ==========================================
    // ICE ARROW STATE (FAST CAST & CROSSFIRE PRESSURE)
    // ==========================================
    public class DreadwormIceArrowState : DreadwormBossState
    {
        private float enterTime;
        private bool hasSpawned;
        private bool hasShot;
        private List<DreadwormIceArrow> chargedArrows = new List<DreadwormIceArrow>();

        public DreadwormIceArrowState(DreadwormBoss boss) : base(boss) { }

        public override void Enter()
        {
            enterTime = Time.time;
            hasSpawned = false;
            hasShot = false;
            chargedArrows.Clear();

            boss.StopHorizontalMovement();
            boss.MarkIceArrowUsed();
            boss.ShowWarning();
            boss.AudioController?.PlayIceArrowCharge();

            if (boss.Animator != null) boss.Animator.SetTrigger("isAttack");
            boss.VFXController?.BeginIceCast(0.2f + boss.IceArrowChargeTime);
        }

        public override void LogicUpdate()
        {
            float elapsed = Time.time - enterTime;

            if (boss.Target != null)
                boss.FacePosition(boss.Target.position);

            float spawnDelay = 0.15f;
            if (elapsed >= spawnDelay && !hasSpawned)
            {
                SpawnSuspendedArrows();
                hasSpawned = true;
            }

            if (elapsed >= spawnDelay + boss.IceArrowChargeTime && !hasShot)
            {
                boss.HideWarning();
                if (boss.Animator != null) boss.Animator.SetTrigger("isAttack");
                boss.VFXController?.EndCast();
                boss.StartCoroutine(ShootIceArrowsRoutine());
                hasShot = true;
            }

            float totalWaitTime = spawnDelay + boss.IceArrowChargeTime + (boss.IceArrowCount * boss.IceArrowShootInterval) + 0.25f;
            if (hasShot && elapsed >= totalWaitTime)
            {
                // Dynamic Action Combo: 50% chance to immediately Charge while arrows are still active in flight!
                if (boss.Target != null && boss.CanCharge() && UnityEngine.Random.value <= 0.5f)
                {
                    boss.ChangeState(boss.ChargeState);
                    return;
                }

                boss.ChangeState(boss.CooldownState);
            }
        }

        private void SpawnSuspendedArrows()
        {
            if (boss.Target == null || boss.IceArrowPrefab == null) return;

            for (int i = 0; i < boss.IceArrowCount; i++)
            {
                Vector3 spawnPos = boss.GetIceArrowSpawnPosition(i);
                GameObject arrowObj = Object.Instantiate(boss.IceArrowPrefab, spawnPos, Quaternion.identity);
                DreadwormIceArrow arrowScript = arrowObj.GetComponent<DreadwormIceArrow>();

                if (arrowScript != null)
                {
                    Vector2 direction = ((Vector2)boss.Target.position - (Vector2)spawnPos).normalized;
                    float sequenceOffset = boss.IceArrowCount > 1
                        ? i / (boss.IceArrowCount - 1f)
                        : 0f;
                    arrowScript.Suspend(direction, boss.IceArrowChargeTime, sequenceOffset);
                    chargedArrows.Add(arrowScript);
                }
                else
                {
                    Object.Destroy(arrowObj);
                }
            }
        }

        private IEnumerator ShootIceArrowsRoutine()
        {
            boss.AudioController?.StopChargeSound();

            // Snapshot sang array để tránh InvalidOperationException khi Exit() Clear() list trong lúc đang iterate
            var arrowSnapshot = chargedArrows.ToArray();
            chargedArrows.Clear();

            foreach (var arrow in arrowSnapshot)
            {
                if (arrow != null && boss.Target != null)
                {
                    Vector2 direction = ((Vector2)boss.Target.position - (Vector2)arrow.transform.position).normalized;
                    arrow.Fire(direction, boss.IceArrowSpeed, boss.IceArrowDamage);
                    boss.AudioController?.PlayIceArrowFire(arrow.transform.position);
                }
                yield return new WaitForSeconds(boss.IceArrowShootInterval);
            }
        }

        public override void Exit()
        {
            boss.HideWarning();
            boss.VFXController?.EndCast();
            boss.AudioController?.StopChargeSound();
            foreach (var arrow in chargedArrows)
            {
                if (arrow != null) Object.Destroy(arrow.gameObject);
            }
            chargedArrows.Clear();
        }
    }

    // ==========================================
    // SUMMON SPHERE STATE (AREA DENIAL)
    // ==========================================
    public class DreadwormSummonSphereState : DreadwormBossState
    {
        private float enterTime;
        private bool hasSummoned;

        public DreadwormSummonSphereState(DreadwormBoss boss) : base(boss) { }

        public override void Enter()
        {
            enterTime = Time.time;
            hasSummoned = false;

            boss.StopHorizontalMovement();
            boss.MarkSummonSphereUsed();
            boss.ShowWarning();
            boss.AudioController?.PlaySphereSpawn();

            if (boss.Animator != null) boss.Animator.SetTrigger("isAttack");
            if (boss.Target != null) boss.FacePosition(boss.Target.position);
            boss.VFXController?.BeginSphereCast(
                boss.SummonSphereAnimDuration * 0.35f + boss.SummonSphereChargeTime);
        }

        public override void LogicUpdate()
        {
            float elapsed = Time.time - enterTime;
            float spawnDelay = boss.SummonSphereAnimDuration * 0.25f;

            if (elapsed >= spawnDelay && !hasSummoned)
            {
                SummonSpheres();
                hasSummoned = true;
            }

            float totalWaitTime = Mathf.Max(boss.SummonSphereAnimDuration * 0.7f, spawnDelay + boss.SummonSphereChargeTime);

            if (elapsed >= spawnDelay + boss.SummonSphereChargeTime)
            {
                boss.HideWarning();
                boss.VFXController?.EndCast();
            }

            if (elapsed >= totalWaitTime)
            {
                boss.ChangeState(boss.CooldownState);
            }
        }

        private void SummonSpheres()
        {
            if (boss.ElectroSpherePrefab == null || boss.Target == null) return;

            Vector3 spawnPos = boss.GetSphereSpawnPosition();
            GameObject sphere = Object.Instantiate(boss.ElectroSpherePrefab);
            sphere.transform.position = spawnPos;
            sphere.transform.rotation = Quaternion.identity;
            sphere.SetActive(true);

            DreadwormElectroSphere controller = sphere.GetComponent<DreadwormElectroSphere>();
            if (controller == null) controller = sphere.AddComponent<DreadwormElectroSphere>();

            Vector2 dir = new Vector2(boss.FacingDirection, 0f);
            float targetGroundY = boss.transform.position.y + boss.SweepYOffset;
            controller.InitializeAdvanced(dir, boss.SummonSphereSpeed, boss.SummonSphereDamage, targetGroundY, boss.SummonSphereChargeTime);
        }

        public override void Exit()
        {
            boss.HideWarning();
            boss.VFXController?.EndCast();
        }
    }

    // ==========================================
    // CRYSTAL SPINE ERUPTION STATE (RAPID WAVE -> SLAM FINISHER)
    // ==========================================
    public class DreadwormSpineEruptionState : DreadwormBossState
    {
        private float enterTime;
        private float eruptionStartTime;
        private bool eruptionStarted;
        private int nextImpactIndex;
        private int lastPulseIndex;
        private Vector3[] impactPoints;

        public DreadwormSpineEruptionState(DreadwormBoss boss) : base(boss) { }

        public override void Enter()
        {
            enterTime = Time.time;
            eruptionStartTime = 0f;
            eruptionStarted = false;
            nextImpactIndex = 0;
            lastPulseIndex = -1;

            boss.StopHorizontalMovement();
            boss.MarkSpineEruptionUsed();
            boss.ShowWarning();

            if (boss.Target != null)
                boss.FacePosition(boss.Target.position);

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isWalk", false);
                boss.Animator.SetTrigger("isAttack");
                boss.Animator.speed = 1.35f;
            }

            int count = boss.SpineEruptionSpikeCount;
            impactPoints = new Vector3[count];
            for (int i = 0; i < count; i++)
                impactPoints[i] = boss.GetSpineEruptionPoint(i);

            boss.VFXController?.BeginSpineEruptionTelegraph(
                impactPoints,
                boss.SpineEruptionHitRadius,
                boss.SpineEruptionWindup);
        }

        public override void LogicUpdate()
        {
            if (!eruptionStarted)
            {
                float elapsed = Time.time - enterTime;
                UpdateCrystalChargeSequence(elapsed);

                if (elapsed < boss.SpineEruptionWindup)
                    return;

                eruptionStarted = true;
                eruptionStartTime = Time.time;
                boss.HideWarning();
            }

            float eruptionElapsed = Time.time - eruptionStartTime;
            while (nextImpactIndex < impactPoints.Length &&
                   eruptionElapsed >= nextImpactIndex * boss.SpineEruptionInterval)
            {
                ImpactPoint(nextImpactIndex);
                nextImpactIndex++;
            }

            float finishTime = Mathf.Max(0f, impactPoints.Length - 1) * boss.SpineEruptionInterval +
                               boss.SpineEruptionRecovery;
            if (nextImpactIndex >= impactPoints.Length && eruptionElapsed >= finishTime)
            {
                // Dynamic Combo: If player is within attack reach, immediately chain into Heavy Slam Finisher!
                if (boss.Target != null)
                {
                    float distToPlayer = Vector2.Distance(boss.CenterPosition, boss.Target.position);
                    if (distToPlayer <= boss.HeavySlamRadius + 1.5f && boss.CanSlam())
                    {
                        boss.SlamState.ConfigureSlam(true);
                        boss.ChangeState(boss.SlamState);
                        return;
                    }
                }

                boss.ChangeState(boss.CooldownState);
            }
        }

        private void UpdateCrystalChargeSequence(float elapsed)
        {
            int pulseCount = Mathf.Max(1, boss.CrystalBoneCount > 0
                ? boss.CrystalBoneCount
                : boss.SpineEruptionSpikeCount);
            float pulseInterval = boss.SpineEruptionWindup / pulseCount;
            int pulseIndex = Mathf.Clamp(
                Mathf.FloorToInt(elapsed / Mathf.Max(0.01f, pulseInterval)),
                0,
                pulseCount - 1);

            if (pulseIndex <= lastPulseIndex)
                return;

            lastPulseIndex = pulseIndex;
            int mappedSpikeIndex = pulseCount <= 1
                ? 0
                : Mathf.RoundToInt((pulseIndex / (pulseCount - 1f)) * (boss.SpineEruptionSpikeCount - 1));
            Transform anchor = boss.GetCrystalBoneAnchor(mappedSpikeIndex);
            boss.AudioController?.PlaySpineChargePulse(anchor != null ? anchor.position : impactPoints[mappedSpikeIndex]);
            boss.VFXController?.PulseSpineCrystal(anchor, mappedSpikeIndex, boss.SpineEruptionSpikeCount);
        }

        private void ImpactPoint(int index)
        {
            Vector3 point = impactPoints[index];
            boss.AudioController?.PlaySpineErupt(point);
            boss.VFXController?.PlaySpineEruptionImpact(
                point,
                boss.SpineEruptionHitRadius,
                index);

            Collider2D[] hits = Physics2D.OverlapCircleAll(point, boss.SpineEruptionHitRadius);
            foreach (Collider2D hit in hits)
            {
                if (hit == null || !hit.CompareTag("Player"))
                    continue;

                IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
                    hit,
                    boss.SpineEruptionDamage,
                    ParryableAttackType.Melee,
                    EnemyParryResponder.Ensure(boss),
                    boss.transform,
                    ((Vector2)hit.transform.position - (Vector2)point).normalized + Vector2.up * 0.35f);

                if (resolution == IncomingAttackResolution.Parried)
                {
                    boss.TriggerParryStagger();
                    return;
                }
                break;
            }
        }

        public override void Exit()
        {
            boss.HideWarning();
            boss.VFXController?.CancelSpineEruptionTelegraph();
            if (boss.Animator != null) boss.Animator.speed = 1f;
        }
    }

    // ==========================================
    // FROZEN SANCTUARY & GLACIAL BARRAGE STATE
    // ==========================================
    public class DreadwormFrozenSanctuaryState : DreadwormBossState
    {
        private float enterTime;
        private float sanctuaryStartTime;
        private float lastIcicleSpawnTime;
        private int iciclesSpawned;
        private float currentShellHealth;
        private float maxShellHealth;
        private bool isWindupPhase;
        private bool isShellBroken;

        public bool IsWindupPhase => isWindupPhase;

        public DreadwormFrozenSanctuaryState(DreadwormBoss boss) : base(boss) { }

        public override void Enter()
        {
            enterTime = Time.time;
            sanctuaryStartTime = 0f;
            lastIcicleSpawnTime = 0f;
            iciclesSpawned = 0;
            isWindupPhase = true;
            isShellBroken = false;
            maxShellHealth = boss.IceShellMaxHealth;
            currentShellHealth = maxShellHealth;

            boss.StopHorizontalMovement();
            boss.MarkFrozenSanctuaryUsed();
            boss.ShowWarning();

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isWalk", false);
                boss.Animator.SetTrigger("isAttack");
            }

            if (boss.Target != null)
                boss.FacePosition(boss.Target.position);

            // Bắt đầu giai đoạn Tụ Khí Windup (Vortex hút mana băng & Vòng tròn cổ ngữ cực hàn)
            boss.AudioController?.PlaySanctuaryWindup();
            boss.VFXController?.BeginFrozenSanctuaryWindup(boss.FrozenSanctuaryWindup);
        }

        public override void LogicUpdate()
        {
            if (boss.IsDead) return;

            float elapsed = Time.time - enterTime;

            // 1. Giai đoạn Tụ Khí Windup (Kéo dài boss.FrozenSanctuaryWindup giây)
            if (isWindupPhase)
            {
                if (elapsed < boss.FrozenSanctuaryWindup)
                    return;

                // Kết thúc Windup -> Kích hoạt Đóng Băng & Giáp Kén Băng Hộ Thể
                isWindupPhase = false;
                sanctuaryStartTime = Time.time;
                lastIcicleSpawnTime = sanctuaryStartTime + 0.25f;
                boss.HideWarning();
                boss.AudioController?.PlaySanctuaryShieldUp();
                boss.VFXController?.BeginFrozenSanctuaryShield(boss.FrozenSanctuaryDuration);
            }

            // 2. Giai đoạn Phóng Mưa Băng Glacial Barrage & Hộ Thể
            float activeElapsed = Time.time - sanctuaryStartTime;

            if (iciclesSpawned < boss.GlacialBarrageCount && Time.time >= lastIcicleSpawnTime + boss.GlacialBarrageInterval)
            {
                SpawnGlacialIcicleWave();
                lastIcicleSpawnTime = Time.time;
                iciclesSpawned++;
            }

            if (activeElapsed >= boss.FrozenSanctuaryDuration && !isShellBroken)
            {
                boss.ChangeState(boss.CooldownState);
            }
        }

        private void SpawnGlacialIcicleWave()
        {
            if (boss.Target == null) return;

            Vector3 spawnTarget;
            bool targetPlayer = (UnityEngine.Random.value <= 0.7f) || (iciclesSpawned == 0);

            if (targetPlayer)
            {
                Vector3 playerPos = boss.Target.position;
                Vector3 playerVel = Vector3.zero;

                Rigidbody2D rb = boss.Target.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    playerVel = new Vector3(rb.linearVelocity.x, rb.linearVelocity.y, 0f);
                }

                Vector3 predictedOffset = playerVel * boss.IcicleTargetPredictionTime + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.65f);
                spawnTarget = playerPos + predictedOffset;
            }
            else
            {
                float xOffset = UnityEngine.Random.Range(-boss.IcicleRandomArenaRadius, boss.IcicleRandomArenaRadius);
                spawnTarget = new Vector3(boss.transform.position.x + xOffset, boss.Target.position.y, 0f);
            }

            if (boss.Collider2D != null)
            {
                spawnTarget.y = boss.Collider2D.bounds.min.y + 0.1f;
            }

            DreadwormGlacialIcicle.Spawn(
                boss,
                spawnTarget,
                boss.IcicleTelegraphDuration,
                boss.IcicleDamage,
                boss.IcicleSprites);
        }

        public void ApplyLureDamagePercent(float percent, Vector3? hitPoint = null)
        {
            if (isShellBroken || boss.IsDead) return;

            float damageAmount = maxShellHealth * percent;
            currentShellHealth -= damageAmount;

            boss.AudioController?.PlaySanctuaryShieldHit(hitPoint ?? boss.CenterPosition);
            float healthRatio = Mathf.Clamp01(currentShellHealth / maxShellHealth);
            boss.VFXController?.UpdateCocoonShieldHealth(healthRatio, hitPoint);

            if (currentShellHealth <= 0f)
            {
                TriggerShellBreak();
            }
        }

        public void ApplyDirectAttackShellDamage(int damage, Vector3? hitPoint = null)
        {
            if (isShellBroken || boss.IsDead) return;

            currentShellHealth -= damage * 0.5f;
            boss.AudioController?.PlaySanctuaryShieldHit(hitPoint ?? boss.CenterPosition);
            float healthRatio = Mathf.Clamp01(currentShellHealth / maxShellHealth);
            boss.VFXController?.UpdateCocoonShieldHealth(healthRatio, hitPoint);

            if (currentShellHealth <= 0f)
            {
                TriggerShellBreak();
            }
        }

        private void TriggerShellBreak()
        {
            isShellBroken = true;
            boss.AudioController?.PlaySanctuaryShieldBreak(boss.CenterPosition);
            boss.VFXController?.EndFrozenSanctuaryShield(true);
            boss.ChangeState(boss.StaggerState); // Massive Punish Window for player!
        }

        public override void Exit()
        {
            boss.HideWarning();
            if (!isShellBroken)
            {
                boss.VFXController?.EndFrozenSanctuaryShield(false);
            }
        }
    }

    // ==========================================
    // STAGGER STATE (PARRY DEFLECT / SHATTER PUNISH WINDOW)
    // ==========================================
    public class DreadwormStaggerState : DreadwormBossState
    {
        private float enterTime;
        private float activeDuration;

        public DreadwormStaggerState(DreadwormBoss boss) : base(boss) { }

        public override void Enter()
        {
            enterTime = Time.time;
            activeDuration = boss.BossStaggerDuration;

            // If triggered from quick parry deflect, use micro-stagger duration
            if (boss.LastUsedSkillCategory == DreadwormBoss.SkillCategory.Melee ||
                boss.LastUsedSkillCategory == DreadwormBoss.SkillCategory.Movement)
            {
                activeDuration = boss.ParryMicroStaggerDuration;
            }

            boss.AudioController?.PlayParriedStagger();
            boss.StopMoving();
            boss.HideWarning();

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isWalk", false);
                boss.Animator.SetBool("isIdle", true);
                boss.Animator.speed = 0.35f; // Dazed animation speed
            }

            boss.VFXController?.BeginStaggerVisuals(activeDuration);
        }

        public override void LogicUpdate()
        {
            if (boss.IsDead) return;
            boss.StopMoving();

            if (Time.time - enterTime >= activeDuration)
            {
                boss.ChangeState(boss.CooldownState);
            }
        }

        public override void PhysicsUpdate()
        {
            boss.StopMoving();
        }

        public override void Exit()
        {
            if (boss.Animator != null) boss.Animator.speed = 1f;
            boss.VFXController?.EndStaggerVisuals();
        }
    }

    // ==========================================
    // COOLDOWN STATE (BRIEF RECOVERY / PUNISH WINDOW)
    // ==========================================
    public class DreadwormCooldownState : DreadwormBossState
    {
        public DreadwormCooldownState(DreadwormBoss boss) : base(boss) { }

        public override void Enter()
        {
            boss.EnterCooldownState();
        }

        public override void LogicUpdate()
        {
            if (boss.IsDead) return;
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
    public class DreadwormDeadState : DreadwormBossState
    {
        public DreadwormDeadState(DreadwormBoss boss) : base(boss) { }

        public override void Enter()
        {
            boss.StopMoving();
            if (boss.Rigidbody2D != null)
            {
                boss.Rigidbody2D.linearVelocity = Vector2.zero;
                boss.Rigidbody2D.angularVelocity = 0f;
                boss.Rigidbody2D.gravityScale = 0f;
                boss.Rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
                boss.Rigidbody2D.constraints = RigidbodyConstraints2D.FreezeAll;
            }
            if (boss.Collider2D != null) boss.Collider2D.enabled = false;
            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isWalk", false);
                boss.Animator.SetBool("isIdle", false);
                boss.Animator.SetTrigger("Die");
            }
            Object.Destroy(boss.gameObject, 3f);
        }
    }
}
