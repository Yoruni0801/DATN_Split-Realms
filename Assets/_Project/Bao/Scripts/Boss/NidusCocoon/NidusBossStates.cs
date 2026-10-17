using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bao.Bosses
{
    // ==========================================
    // IDLE STATE
    // ==========================================
    public class NidusIdleState : NidusBossState
    {
        public NidusIdleState(NidusCocoonBoss boss) : base(boss) { }

        public override void Enter()
        {
            boss.StopHorizontalMovement();
            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isIdle", true);
                boss.Animator.SetBool("isWalk", false);
            }
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
            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isIdle", false);
            }
        }
    }

    // ==========================================
    // CHASE STATE (COMBAT MANAGER)
    // ==========================================
    public class NidusChaseState : NidusBossState
    {
        private struct SkillCandidate
        {
            public NidusCocoonBoss.SkillId skill;
            public NidusCocoonBoss.SkillCategory category;
            public float weight;

            public SkillCandidate(NidusCocoonBoss.SkillId skill, NidusCocoonBoss.SkillCategory category, float weight)
            {
                this.skill = skill;
                this.category = category;
                this.weight = weight;
            }
        }

        private readonly List<SkillCandidate> availableSkills = new List<SkillCandidate>(8);

        public NidusChaseState(NidusCocoonBoss boss) : base(boss) { }

        public override void Enter()
        {
            if (boss.Animator != null)
                boss.Animator.SetBool("isIdle", false);
        }

        public override void LogicUpdate()
        {
            if (boss.IsDead || boss.IsTransitioningPhases) return;

            if (!boss.HasTargetInDetectionRange())
            {
                boss.ChangeState(boss.IdleState);
                return;
            }

            if (boss.Target == null)
                return;

            boss.FacePosition(boss.Target.position);
            float distToPlayer = Vector2.Distance(boss.CenterPosition, boss.Target.position);

            // Close the gap first. This prevents long-range casts from making the
            // boss feel like a stationary turret at the edge of the arena.
            if (distToPlayer > boss.ActionEngageRange)
            {
                float direction = boss.Target.position.x >= boss.transform.position.x ? 1f : -1f;
                boss.MoveForward(direction);
                if (boss.Animator != null) boss.Animator.SetBool("isWalk", true);
                return;
            }

            // --- PACING & SPACING ---
            if (!boss.CanPerformAction())
            {
                if (distToPlayer < boss.OptimalSpacingDistance * 0.7f)
                {
                    float dir = boss.Target.position.x >= boss.transform.position.x ? -1f : 1f;
                    boss.MoveForward(dir * boss.BackingSpeedMultiplier);
                    if (boss.Animator != null) boss.Animator.SetBool("isWalk", true);
                }
                else
                {
                    boss.StopHorizontalMovement();
                    if (boss.Animator != null) boss.Animator.SetBool("isWalk", false);
                }
                return;
            }

            availableSkills.Clear();

            float meleeWeight = distToPlayer <= boss.AttackRange ? 150f : 0f;
            float rangedMinDistance = Mathf.Max(boss.OptimalSpacingDistance * 1.15f, boss.AttackRange * 0.7f);
            float rootSpikeWeight = distToPlayer >= rangedMinDistance
                ? (boss.CurrentPhase == BossPhase.Phase1 ? 82f : 58f)
                : 0f;

            float summonWeight = boss.CurrentPhase == BossPhase.Phase1 ? 28f : 16f;
            if (distToPlayer < boss.OptimalSpacingDistance * 0.5f) summonWeight *= 0.55f;

            float acidRainWeight = distToPlayer >= rangedMinDistance ? 72f : 0f;
            float burrowWeight = distToPlayer > boss.OptimalSpacingDistance * 1.2f ? 68f : 24f;
            float rootPrisonWeight = distToPlayer >= boss.OptimalSpacingDistance * 0.8f ? 82f : 35f;
            float toxicWeight = 95f;

            if (boss.LastUsedSkill == NidusCocoonBoss.SkillId.AcidRain && boss.ActiveAcidPoolCount >= 2)
                toxicWeight *= 1.4f;

            if (boss.LastUsedSkill == NidusCocoonBoss.SkillId.Burrow)
                rootPrisonWeight *= 0.35f;

            if (boss.CanMelee() && meleeWeight > 0f)
                AddCandidate(NidusCocoonBoss.SkillId.Melee, NidusCocoonBoss.SkillCategory.Melee, meleeWeight);

            if (boss.CanRootSpike())
                AddCandidate(NidusCocoonBoss.SkillId.RootSpike, NidusCocoonBoss.SkillCategory.Ranged, rootSpikeWeight);

            if (boss.CanSummon())
                AddCandidate(NidusCocoonBoss.SkillId.ParasiteSeed, NidusCocoonBoss.SkillCategory.Summon, summonWeight);

            if (boss.CurrentPhase == BossPhase.Phase2)
            {
                if (boss.CanAcidRain())
                    AddCandidate(NidusCocoonBoss.SkillId.AcidRain, NidusCocoonBoss.SkillCategory.Hazard, acidRainWeight);

                if (boss.CanBurrow())
                    AddCandidate(NidusCocoonBoss.SkillId.Burrow, NidusCocoonBoss.SkillCategory.Movement, burrowWeight);

                if (boss.CanRootPrison())
                    AddCandidate(NidusCocoonBoss.SkillId.RootPrison, NidusCocoonBoss.SkillCategory.Control, rootPrisonWeight);

                if (boss.CanToxicDetonation())
                    AddCandidate(NidusCocoonBoss.SkillId.ToxicDetonation, NidusCocoonBoss.SkillCategory.Hazard, toxicWeight);
            }

            // Anti-spam: same category is strongly discouraged and the exact skill cannot repeat immediately.
            for (int i = 0; i < availableSkills.Count; i++)
            {
                SkillCandidate candidate = availableSkills[i];

                if (candidate.skill == boss.LastUsedSkill && candidate.skill != NidusCocoonBoss.SkillId.None)
                {
                    // Close combat needs a safe fallback: otherwise this anti-repeat
                    // rule can make the boss idle when melee is the only valid move.
                    candidate.weight = candidate.skill == NidusCocoonBoss.SkillId.Melee
                        ? candidate.weight * 0.45f
                        : 0f;
                }
                else if (candidate.category == boss.LastUsedSkillCategory
                    && candidate.category != NidusCocoonBoss.SkillCategory.None)
                {
                    candidate.weight *= 0.2f;
                }

                availableSkills[i] = candidate;
            }

            if (TrySelectSkill(out NidusCocoonBoss.SkillId selected))
            {
                ChangeToSelectedSkill(selected);
                return;
            }

            // If only the exact previous skill is available, do not force it. Reposition until another option opens.
            if (distToPlayer > boss.AttackRange * 0.9f)
            {
                float direction = boss.Target.position.x >= boss.transform.position.x ? 1f : -1f;
                boss.MoveForward(direction);
                if (boss.Animator != null) boss.Animator.SetBool("isWalk", true);
            }
            else
            {
                boss.StopHorizontalMovement();
                if (boss.Animator != null) boss.Animator.SetBool("isWalk", false);
            }
        }

        private void AddCandidate(NidusCocoonBoss.SkillId skill, NidusCocoonBoss.SkillCategory category, float weight)
        {
            if (weight > 0f)
                availableSkills.Add(new SkillCandidate(skill, category, weight));
        }

        private bool TrySelectSkill(out NidusCocoonBoss.SkillId selected)
        {
            selected = NidusCocoonBoss.SkillId.None;
            float totalWeight = 0f;
            for (int i = 0; i < availableSkills.Count; i++)
                totalWeight += Mathf.Max(0f, availableSkills[i].weight);

            if (totalWeight <= 0.001f)
                return false;

            float roll = Random.Range(0f, totalWeight);
            float accumulated = 0f;
            for (int i = 0; i < availableSkills.Count; i++)
            {
                accumulated += Mathf.Max(0f, availableSkills[i].weight);
                if (roll <= accumulated)
                {
                    selected = availableSkills[i].skill;
                    return selected != NidusCocoonBoss.SkillId.None;
                }
            }

            return false;
        }

        private void ChangeToSelectedSkill(NidusCocoonBoss.SkillId skill)
        {
            switch (skill)
            {
                case NidusCocoonBoss.SkillId.Melee:
                    boss.ChangeState(boss.MeleeState);
                    break;
                case NidusCocoonBoss.SkillId.ParasiteSeed:
                    boss.ChangeState(boss.SummonState);
                    break;
                case NidusCocoonBoss.SkillId.RootSpike:
                    boss.ChangeState(boss.RootSpikeState);
                    break;
                case NidusCocoonBoss.SkillId.AcidRain:
                    boss.ChangeState(boss.AcidRainState);
                    break;
                case NidusCocoonBoss.SkillId.Burrow:
                    boss.ChangeState(boss.BurrowState);
                    break;
                case NidusCocoonBoss.SkillId.RootPrison:
                    boss.ChangeState(boss.RootPrisonState);
                    break;
                case NidusCocoonBoss.SkillId.ToxicDetonation:
                    boss.ChangeState(boss.ToxicDetonationState);
                    break;
            }
        }

        public override void PhysicsUpdate()
        {
            if (boss.IsDead || boss.IsTransitioningPhases)
                boss.StopHorizontalMovement();
        }

        public override void Exit()
        {
            boss.StopHorizontalMovement();
            if (boss.Animator != null)
                boss.Animator.SetBool("isWalk", false);
        }
    }

    // ==========================================
    // MELEE STATE
    // ==========================================
    public class NidusMeleeState : NidusBossState
    {
        private float enterTime;
        private bool hasDealtDamage;
        private bool isWarning;

        public NidusMeleeState(NidusCocoonBoss boss) : base(boss) { }

        public override void Enter()
        {
            base.Enter();
            enterTime = Time.time;
            hasDealtDamage = false;
            isWarning = true;

            boss.StopHorizontalMovement();
            boss.MarkMeleeUsed();
            boss.ShowWarning();

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isWalk", false);
                boss.Animator.SetBool("isIdle", false);
            }

            if (boss.Target != null)
            {
                boss.FacePosition(boss.Target.position);
            }
        }

        public override void LogicUpdate()
        {
            float elapsed = Time.time - enterTime;

            if (isWarning)
            {
                if (elapsed >= boss.ChargeDelay)
                {
                    isWarning = false;
                    boss.HideWarning();
                    if (boss.Animator != null) boss.Animator.SetTrigger("Attack");
                    enterTime = Time.time;
                }
                return;
            }

            if (elapsed >= boss.MeleeDelay && !hasDealtDamage)
            {
                DealDamage();
                hasDealtDamage = true;
            }

            if (elapsed >= boss.MeleeAnimDuration)
            {
                boss.ChangeState(boss.CooldownState);
            }
        }

        private void DealDamage()
        {
            Vector2 attackPoint = (Vector2)boss.CenterPosition + Vector2.right * boss.FacingDirection * 0.8f;
            Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint, boss.MeleeRadius);

            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player"))
                {
                    PlayerHitResolver.TryApplyHitToPlayer(
                        hit,
                        boss.MeleeDamage,
                        ParryableAttackType.Melee,
                        EnemyParryResponder.Ensure(boss),
                        boss.transform,
                        hit.transform.position - boss.transform.position);
                }
            }
        }

        public override void PhysicsUpdate()
        {
            boss.StopHorizontalMovement();
            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isWalk", false);
            }
        }

        public override void Exit()
        {
            boss.StopHorizontalMovement();
            boss.HideWarning();
            if (boss.Animator != null)
            {
                boss.Animator.ResetTrigger("Attack");
            }
        }
    }

    // ==========================================
    // SUMMON STATE
    // ==========================================
    public class NidusSummonState : NidusBossState
    {
        private float enterTime;
        private bool hasSummoned;
        private bool isWarning;
        private readonly float summonDuration = 1.2f;

        public NidusSummonState(NidusCocoonBoss boss) : base(boss) { }

        public override void Enter()
        {
            base.Enter();
            enterTime = Time.time;
            hasSummoned = false;
            isWarning = true;

            boss.StopHorizontalMovement();
            boss.MarkSummonUsed();
            boss.ShowWarning();

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isWalk", false);
                boss.Animator.SetBool("isIdle", false);
            }

            if (boss.Target != null)
                boss.FacePosition(boss.Target.position);
        }

        public override void LogicUpdate()
        {
            float elapsed = Time.time - enterTime;

            if (isWarning)
            {
                if (elapsed >= boss.ChargeDelay)
                {
                    isWarning = false;
                    boss.HideWarning();
                    if (boss.Animator != null) boss.Animator.SetTrigger("Summon");
                    enterTime = Time.time;
                }
                return;
            }

            if (elapsed >= summonDuration * 0.38f && !hasSummoned)
            {
                SpawnParasiteSeeds();
                hasSummoned = true;
            }

            if (elapsed >= summonDuration)
                boss.ChangeState(boss.CooldownState);
        }

        private void SpawnParasiteSeeds()
        {
            if (boss.MinionPrefab == null)
            {
                Debug.LogWarning("Nidus Boss: Minion prefab is not set; parasite seeds cannot hatch.");
                return;
            }

            int count = Mathf.Min(boss.EffectiveSummonCount, boss.AvailableBroodSlots);
            if (count <= 0)
                return;

            Vector3 origin = boss.CenterPosition + Vector3.up * 0.35f;
            float spacingBias = count > 1 ? boss.SummonRadius / Mathf.Max(1, count - 1) : 0f;

            for (int i = 0; i < count; i++)
            {
                float structuredOffset = count > 1
                    ? Mathf.Lerp(-boss.SummonRadius, boss.SummonRadius, i / (count - 1f))
                    : Random.Range(-boss.SummonRadius * 0.6f, boss.SummonRadius * 0.6f);
                float jitter = Random.Range(-Mathf.Max(0.15f, spacingBias * 0.18f), Mathf.Max(0.15f, spacingBias * 0.18f));
                Vector3 desired = boss.CenterPosition + new Vector3(structuredOffset + jitter, 0f, 0f);
                Vector3 ground = boss.ResolveGroundPoint(desired, 5f);

                if (boss.ParasiteSeedPrefab == null)
                {
                    GameObject minionObject = Object.Instantiate(boss.MinionPrefab, ground, Quaternion.identity);
                    NidusMinion minion = minionObject.GetComponent<NidusMinion>();
                    if (minion != null)
                        minion.InitializeOwner(boss);
                    continue;
                }

                NidusParasiteSeed.Spawn(
                    boss.ParasiteSeedPrefab,
                    boss,
                    boss.MinionPrefab,
                    origin,
                    ground,
                    boss.ParasiteSeedHealth,
                    boss.ParasiteSeedHatchTime);
            }
        }

        public override void PhysicsUpdate()
        {
            boss.StopHorizontalMovement();
            if (boss.Animator != null)
                boss.Animator.SetBool("isWalk", false);
        }

        public override void Exit()
        {
            boss.StopHorizontalMovement();
            boss.HideWarning();
            if (boss.Animator != null)
                boss.Animator.ResetTrigger("Summon");
        }
    }

    // ==========================================
    // ACID RAIN STATE
    // ==========================================
    public class NidusAcidRainState : NidusBossState
    {
        private float enterTime;
        private bool hasCasted;
        private bool isWarning;

        public NidusAcidRainState(NidusCocoonBoss boss) : base(boss) { }

        public override void Enter()
        {
            base.Enter();
            enterTime = Time.time;
            hasCasted = false;
            isWarning = true;

            boss.StopHorizontalMovement();
            boss.MarkAcidRainUsed();
            boss.ShowWarning();

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isWalk", false);
                boss.Animator.SetBool("isIdle", false);
            }

            Debug.Log("Nidus Boss: Casting Acid Rain!");
        }

        public override void LogicUpdate()
        {
            float elapsed = Time.time - enterTime;

            if (isWarning)
            {
                if (elapsed >= boss.ChargeDelay)
                {
                    isWarning = false;
                    boss.HideWarning();
                    if (boss.Animator != null) boss.Animator.SetTrigger("AcidRain");
                    enterTime = Time.time;
                }
                return;
            }

            // Wait for the upward cast animation to finish before spawning warnings/rain
            if (elapsed >= boss.AcidRainCastDelay && !hasCasted)
            {
                boss.StartCoroutine(SpawnAcidRainRoutine());
                hasCasted = true;
            }

            // Wait for the full animation to finish before moving/changing state
            if (elapsed >= boss.AcidRainAnimDuration)
            {
                boss.ChangeState(boss.CooldownState);
            }
        }

        private IEnumerator SpawnAcidRainRoutine()
        {
            if (boss.Target == null) yield break;

            List<Vector3> targets = new List<Vector3>();
            List<GameObject> warnings = new List<GameObject>();
            Vector3 centerPos = boss.PredictTargetPosition(0.55f, 3.5f);

            for (int i = 0; i < boss.EffectiveAcidRainProjectileCount; i++)
            {
                float randomX = Random.Range(-boss.AcidRainSpreadRadius, boss.AcidRainSpreadRadius);
                Vector3 targetPos = centerPos + new Vector3(randomX, 0f, 0f);

                RaycastHit2D hit = Physics2D.Raycast(targetPos + Vector3.up * 5f, Vector2.down, 30f, boss.EnvironmentLayer);
                if (hit.collider != null)
                {
                    targetPos = hit.point;
                }
                else
                {
                    targetPos.y -= 15f;
                }

                targets.Add(targetPos);

                if (boss.AcidWarningPrefab != null)
                {
                    GameObject warning = Object.Instantiate(boss.AcidWarningPrefab, targetPos, Quaternion.identity);

                    AcidWarningIndicator indicator = warning.GetComponent<AcidWarningIndicator>();
                    if (indicator != null)
                    {
                        indicator.Initialize(boss.AcidWarningDuration, boss.AcidDamageRadius);
                    }

                    warnings.Add(warning);
                    Object.Destroy(warning, boss.AcidWarningDuration + 0.1f);
                }
            }

            yield return new WaitForSeconds(boss.AcidWarningDuration);

            // Clean up warnings just in case
            foreach (var w in warnings)
            {
                if (w != null) Object.Destroy(w);
            }

            // Spawn dropping projectiles
            for (int i = 0; i < targets.Count; i++)
            {
                Vector3 targetPos = targets[i];

                if (boss.AcidProjectilePrefab != null)
                {
                    GameObject proj = Object.Instantiate(boss.AcidProjectilePrefab, targetPos + Vector3.up * 8f, Quaternion.Euler(0f, 0f, -90f));
                    
                    // Apply boss shader/material to make it premium
                    SpriteRenderer projSprite = proj.GetComponentInChildren<SpriteRenderer>();
                    if (projSprite != null && boss.SpriteRenderers != null && boss.SpriteRenderers.Length > 0)
                    {
                        projSprite.material = boss.SpriteRenderers[0].material;
                    }

                    NidusAcidProjectile projectileScript = proj.GetComponent<NidusAcidProjectile>();
                    if (projectileScript != null)
                    {
                        projectileScript.Initialize(
                            targetPos,
                            boss.AcidDamage,
                            boss.AcidDamageRadius,
                            boss.AcidPoolDuration,
                            boss.AcidPoolDamagePerSecond,
                            boss.AcidPoolPrefab,
                            boss.EnvironmentLayer,
                            boss
                        );
                    }
                    else
                    {
                        proj.transform.position = targetPos;
                        Object.Destroy(proj, 1f);
                    }
                }
            }
        }

        public override void PhysicsUpdate()
        {
            boss.StopHorizontalMovement();
            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isWalk", false);
            }
        }

        public override void Exit()
        {
            boss.StopHorizontalMovement();
            if (boss.Animator != null)
            {
                boss.Animator.ResetTrigger("AcidRain");
            }
        }
    }

    // ==========================================
    // ROOT SPIKE WAVE STATE
    // ==========================================
    public class NidusRootSpikeWaveState : NidusBossState
    {
        private Coroutine routine;

        public NidusRootSpikeWaveState(NidusCocoonBoss boss) : base(boss) { }

        public override void Enter()
        {
            boss.StopHorizontalMovement();
            boss.MarkRootSpikeUsed();
            if (boss.Target != null)
                boss.FacePosition(boss.Target.position);

            routine = boss.StartCoroutine(RootSpikeRoutine());
        }

        private IEnumerator RootSpikeRoutine()
        {
            if (boss.Target == null)
            {
                boss.ChangeState(boss.CooldownState);
                yield break;
            }

            yield return boss.PlaySkillWarning();
            if (boss.IsDead || boss.Target == null || boss.CurrentState != this)
                yield break;

            Vector3 startGround = boss.ResolveGroundPoint(boss.CenterPosition, 6f);
            Vector3 targetSnapshot = boss.PredictTargetPosition(0.38f, 2.5f);

            Vector3 targetGround = boss.ResolveGroundPoint(targetSnapshot, 7f);
            int count = Mathf.Max(3, boss.EffectiveRootSpikeCount);

            if (Mathf.Abs(targetGround.x - startGround.x) < 1.2f)
                targetGround.x = startGround.x + boss.FacingDirection * Mathf.Max(2.6f, boss.OptimalSpacingDistance * 0.75f);

            for (int i = 0; i < count; i++)
            {
                float t = (i + 1f) / count;
                Vector3 desired = Vector3.Lerp(startGround, targetGround, t);
                Vector3 ground = boss.ResolveGroundPoint(desired, 6f);
                float individualWarning = boss.RootSpikeWarningDuration + i * boss.RootSpikeInterval;

                NidusRootSpikeHazard.Spawn(
                    boss,
                    ground,
                    individualWarning,
                    boss.RootSpikeDamage,
                    boss.RootSpikeRadius,
                    activeDuration: 0.26f,
                    rootHeight: Mathf.Lerp(1.8f, 2.55f, t),
                    large: i == count - 1,
                    shakeOnErupt: i == count - 1);
            }

            float total = boss.RootSpikeWarningDuration + (count - 1) * boss.RootSpikeInterval + 0.62f;
            yield return new WaitForSeconds(total);

            if (!boss.IsDead && boss.CurrentState == this)
                boss.ChangeState(boss.CooldownState);
        }

        public override void PhysicsUpdate()
        {
            boss.StopHorizontalMovement();
        }

        public override void Exit()
        {
            boss.StopHorizontalMovement();
            boss.HideWarning();
            if (routine != null)
            {
                boss.StopCoroutine(routine);
                routine = null;
            }
        }
    }

    // ==========================================
    // ROOT PRISON STATE
    // ==========================================
    public class NidusRootPrisonState : NidusBossState
    {
        private Coroutine routine;

        public NidusRootPrisonState(NidusCocoonBoss boss) : base(boss) { }

        public override void Enter()
        {
            boss.StopHorizontalMovement();
            boss.MarkRootPrisonUsed();
            if (boss.Target != null)
                boss.FacePosition(boss.Target.position);

            routine = boss.StartCoroutine(RootPrisonRoutine());
        }

        private IEnumerator RootPrisonRoutine()
        {
            if (boss.Target == null)
            {
                boss.ChangeState(boss.CooldownState);
                yield break;
            }

            yield return boss.PlaySkillWarning();
            if (boss.IsDead || boss.Target == null || boss.CurrentState != this)
                yield break;

            Vector3 targetSnapshot = boss.ResolveGroundPoint(boss.PredictTargetPosition(0.3f, 2f), 7f);
            Vector3 left = boss.ResolveGroundPoint(targetSnapshot + Vector3.left * boss.RootPrisonHalfWidth, 7f);
            Vector3 right = boss.ResolveGroundPoint(targetSnapshot + Vector3.right * boss.RootPrisonHalfWidth, 7f);

            NidusRootSpikeHazard.Spawn(
                boss, left, boss.RootPrisonOuterWarning,
                boss.RootPrisonWallDamage, 0.72f,
                boss.RootPrisonWallLifetime, 3.0f, true);

            NidusRootSpikeHazard.Spawn(
                boss, right, boss.RootPrisonOuterWarning,
                boss.RootPrisonWallDamage, 0.72f,
                boss.RootPrisonWallLifetime, 3.0f, true);

            yield return new WaitForSeconds(boss.RootPrisonOuterWarning + 0.25f);

            if (boss.IsDead || boss.Target == null || boss.CurrentState != this)
                yield break;

            // The central lash gets a fresh snapshot only when its own warning begins.
            Vector3 center = boss.ResolveGroundPoint(boss.PredictTargetPosition(0.18f, 1.25f), 7f);
            NidusRootSpikeHazard.Spawn(
                boss, center, boss.RootPrisonCenterWarning,
                boss.RootPrisonCenterDamage, 0.62f,
                0.28f, 2.6f, false);

            yield return new WaitForSeconds(boss.RootPrisonCenterWarning + 0.36f);

            if (!boss.IsDead && boss.CurrentState == this)
                boss.ChangeState(boss.CooldownState);
        }

        public override void PhysicsUpdate()
        {
            boss.StopHorizontalMovement();
        }

        public override void Exit()
        {
            boss.StopHorizontalMovement();
            boss.HideWarning();
            if (routine != null)
            {
                boss.StopCoroutine(routine);
                routine = null;
            }
        }
    }

    // ==========================================
    // TOXIC DETONATION STATE
    // ==========================================
    public class NidusToxicDetonationState : NidusBossState
    {
        private Coroutine routine;

        public NidusToxicDetonationState(NidusCocoonBoss boss) : base(boss) { }

        public override void Enter()
        {
            boss.StopHorizontalMovement();
            boss.MarkToxicDetonationUsed();
            if (boss.Target != null)
                boss.FacePosition(boss.Target.position);

            routine = boss.StartCoroutine(DetonationRoutine());
        }

        private IEnumerator DetonationRoutine()
        {
            yield return boss.PlaySkillWarning();
            if (boss.IsDead || boss.CurrentState != this)
                yield break;

            List<NidusAcidPool> pools = boss.GetBestDetonationPools(boss.ToxicDetonationMaxPools);
            if (pools.Count < boss.ToxicDetonationMinimumPools)
            {
                boss.ChangeState(boss.CooldownState);
                yield break;
            }

            int primed = 0;
            for (int i = 0; i < pools.Count; i++)
            {
                if (pools[i] != null && pools[i].PrimeAndDetonate(
                    boss.ToxicDetonationPrimeDuration,
                    boss.ToxicDetonationDamage,
                    boss.ToxicDetonationRadiusMultiplier,
                    boss.ToxicDetonationKnockbackForce))
                {
                    primed++;
                }
            }

            if (primed < boss.ToxicDetonationMinimumPools)
            {
                boss.ChangeState(boss.CooldownState);
                yield break;
            }

            yield return new WaitForSeconds(boss.ToxicDetonationPrimeDuration + 0.32f);

            if (!boss.IsDead && boss.CurrentState == this)
                boss.ChangeState(boss.CooldownState);
        }

        public override void PhysicsUpdate()
        {
            boss.StopHorizontalMovement();
        }

        public override void Exit()
        {
            boss.StopHorizontalMovement();
            boss.HideWarning();
            if (routine != null)
            {
                boss.StopCoroutine(routine);
                routine = null;
            }
        }
    }

    // ==========================================
    // BURROW STATE (BURROW & EMERGE SEQUENCE)
    // ==========================================
    public class NidusBurrowState : NidusBossState
    {
        private Coroutine burrowRoutine;
        private float baseGravityScale;
        private float rootToGroundOffset;
        private float rootToColliderCenterX;
        private float groundEffectRadius;
        private Vector3 lastSurfacePoint;
        private bool hasSurfacePoint;

        public NidusBurrowState(NidusCocoonBoss boss) : base(boss) { }

        public override void Enter()
        {
            baseGravityScale = boss.Rigidbody2D.gravityScale;
            CaptureGroundGeometry();
            boss.StopHorizontalMovement();
            boss.MarkBurrowUsed();

            burrowRoutine = boss.StartCoroutine(BurrowSequence());
        }

        public override void LogicUpdate() { }

        private IEnumerator BurrowSequence()
        {
            Debug.Log("Nidus Boss: Burrowing Down!");

            yield return boss.PlaySkillWarning();
            if (boss.IsDead || boss.CurrentState != this)
                yield break;

            // --- PHASE 1: BURROW DOWN ---
            boss.IsInvulnerable = true;
            boss.Rigidbody2D.gravityScale = 0f;
            boss.Rigidbody2D.linearVelocity = Vector2.zero;

            if (boss.Animator != null)
            {
                boss.Animator.SetTrigger("BurrowDown");
                boss.Animator.SetBool("IsUnderground", true);
            }

            ProceduralDirtVFX.SpawnBurrow(lastSurfacePoint, groundEffectRadius);

            if (boss.BurrowFadeOutDelay > 0f)
                yield return new WaitForSeconds(boss.BurrowFadeOutDelay);

            float elapsed = 0f;
            float fadeDuration = Mathf.Max(0.32f, boss.BurrowFadeOutDuration);
            bool useShaderDissolve = boss.ShaderController != null && boss.ShaderController.HasBurrowMaterial;
            if (useShaderDissolve)
            {
                boss.SetRenderersAlpha(1f);
                boss.ShaderController.StartBurrowDissolve(fadeDuration);
            }

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                if (!useShaderDissolve)
                    boss.SetRenderersAlpha(Mathf.Lerp(1f, 0f, elapsed / fadeDuration));
                yield return null;
            }
            boss.SetRenderersAlpha(0f);

            if (boss.Collider2D != null)
            {
                boss.Collider2D.enabled = false;
            }

            // --- PHASE 2: UNDERGROUND MOVEMENT ---
            float undergroundTimer = 0f;
            float trailTimer = 0f;
            float undergroundRootY = boss.transform.position.y;
            float trailRadius = Mathf.Clamp(groundEffectRadius * 0.24f, 0.65f, 1.15f);

            while (undergroundTimer < boss.BurrowTimeUnderground && boss.Target != null)
            {
                undergroundTimer += Time.deltaTime;
                trailTimer += Time.deltaTime;

                Vector2 currentPosition = boss.Rigidbody2D.position;
                float targetRootX = boss.Target.position.x - rootToColliderCenterX;
                Vector2 targetPosition = new Vector2(targetRootX, undergroundRootY);
                boss.Rigidbody2D.position = Vector2.MoveTowards(
                    currentPosition,
                    targetPosition,
                    boss.EffectiveBurrowSpeed * Time.deltaTime);

                if (trailTimer >= boss.BurrowTrailInterval)
                {
                    trailTimer = 0f;
                    Vector3 trailPoint = new Vector3(
                        boss.Rigidbody2D.position.x + rootToColliderCenterX,
                        lastSurfacePoint.y,
                        lastSurfacePoint.z);
                    ProceduralDirtVFX.SpawnTrail(trailPoint, trailRadius);
                }

                yield return null;
            }

            // --- PHASE 3: EMERGE WARNING ---
            Vector3 desiredSurfacePosition = boss.Target != null
                ? boss.Target.position
                : new Vector3(
                    boss.Rigidbody2D.position.x + rootToColliderCenterX,
                    lastSurfacePoint.y,
                    lastSurfacePoint.z);
            lastSurfacePoint = ResolveGroundSurface(desiredSurfacePosition);
            hasSurfacePoint = true;
            PlaceBossAtSurface(lastSurfacePoint);

            GameObject warningDecal = null;
            if (boss.EmergeWarningPrefab != null)
            {
                warningDecal = Object.Instantiate(boss.EmergeWarningPrefab, lastSurfacePoint, Quaternion.identity);

                AcidWarningIndicator indicator = warningDecal.GetComponent<AcidWarningIndicator>();
                if (indicator != null)
                {
                    indicator.Initialize(boss.EmergeWarningDuration, boss.EmergeDamageRadius);
                }
                else
                {
                    ProceduralDirtVFX.SpawnWarning(
                        lastSurfacePoint,
                        boss.EmergeDamageRadius,
                        boss.EmergeWarningDuration);
                }

                Object.Destroy(warningDecal, boss.EmergeWarningDuration + 0.5f);
            }
            else
            {
                warningDecal = ProceduralDirtVFX.SpawnWarning(
                    lastSurfacePoint,
                    boss.EmergeDamageRadius,
                    boss.EmergeWarningDuration).gameObject;
            }

            yield return new WaitForSeconds(boss.EmergeWarningDuration);

            if (warningDecal != null)
            {
                Object.Destroy(warningDecal);
            }

            // --- PHASE 4: EMERGE IMPACT ---
            Debug.Log("Nidus Boss: Emerging!");

            PlaceBossAtSurface(lastSurfacePoint);
            boss.Rigidbody2D.gravityScale = 0f;
            boss.Rigidbody2D.linearVelocity = Vector2.zero;
            if (boss.Collider2D != null)
            {
                boss.Collider2D.enabled = true;
            }
            Physics2D.SyncTransforms();

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("IsUnderground", false);
                boss.Animator.SetTrigger("Emerge");
            }

            float emergeRadius = Mathf.Max(boss.EmergeDamageRadius, groundEffectRadius * 0.88f);
            ProceduralDirtVFX.SpawnEmerge(lastSurfacePoint, emergeRadius);

            elapsed = 0f;
            float fadeInDuration = Mathf.Max(0.42f, boss.EmergeFadeInDuration);
            if (useShaderDissolve)
            {
                boss.SetRenderersAlpha(1f);
                boss.ShaderController.StartEmergeDissolve(fadeInDuration);
            }
            else
            {
                boss.SetRenderersAlpha(0f);
            }

            ApplyEmergeDamage(lastSurfacePoint);

            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                PlaceBossAtSurface(lastSurfacePoint);
                boss.Rigidbody2D.linearVelocity = Vector2.zero;
                if (!useShaderDissolve)
                    boss.SetRenderersAlpha(Mathf.Lerp(0f, 1f, elapsed / fadeInDuration));
                yield return null;
            }
            boss.SetRenderersAlpha(1f);

            PlaceBossAtSurface(lastSurfacePoint);
            boss.Rigidbody2D.linearVelocity = Vector2.zero;
            boss.Rigidbody2D.gravityScale = baseGravityScale;
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            CorrectGroundContact(lastSurfacePoint);

            boss.IsInvulnerable = false;
            yield return new WaitForSeconds(0.45f);

            boss.ChangeState(boss.CooldownState);
        }

        private void ApplyEmergeDamage(Vector3 emergeSurfacePoint)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(emergeSurfacePoint, boss.EmergeDamageRadius);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player"))
                {
                    PlayerHitResolver.TryApplyHitToPlayer(
                        hit,
                        boss.BurrowDamage,
                        ParryableAttackType.Melee,
                        EnemyParryResponder.Ensure(boss),
                        boss.transform,
                        hit.transform.position - emergeSurfacePoint,
                        canBeBlocked: false);

                    Rigidbody2D playerRb = hit.GetComponent<Rigidbody2D>();
                    if (playerRb != null)
                    {
                        Vector2 direction = ((Vector2)hit.transform.position - (Vector2)emergeSurfacePoint).normalized;
                        direction.y = Mathf.Max(direction.y, 0.5f);
                        playerRb.AddForce(direction.normalized * boss.BurrowKnockbackForce, ForceMode2D.Impulse);
                    }
                }
            }
        }

        private void CaptureGroundGeometry()
        {
            if (boss.Collider2D != null)
            {
                Bounds bounds = boss.Collider2D.bounds;
                rootToGroundOffset = bounds.min.y - boss.transform.position.y;
                rootToColliderCenterX = bounds.center.x - boss.transform.position.x;
                groundEffectRadius = Mathf.Clamp(bounds.extents.x * 1.05f, 2.0f, 4.8f);
                lastSurfacePoint = new Vector3(bounds.center.x, bounds.min.y, boss.transform.position.z);
                hasSurfacePoint = true;
                return;
            }

            rootToGroundOffset = 0f;
            rootToColliderCenterX = 0f;
            groundEffectRadius = 2.2f;
            lastSurfacePoint = boss.transform.position;
            hasSurfacePoint = true;
        }

        private Vector3 ResolveGroundSurface(Vector3 desiredPosition)
        {
            float castHeight = Mathf.Max(8f, groundEffectRadius * 2.5f);
            Vector2 castOrigin = new Vector2(desiredPosition.x, desiredPosition.y + castHeight);
            RaycastHit2D groundHit = Physics2D.Raycast(
                castOrigin,
                Vector2.down,
                castHeight * 2.5f,
                boss.EnvironmentLayer);

            if (groundHit.collider != null)
                return new Vector3(groundHit.point.x, groundHit.point.y, boss.transform.position.z);

            return new Vector3(desiredPosition.x, lastSurfacePoint.y, boss.transform.position.z);
        }

        private void PlaceBossAtSurface(Vector3 surfacePoint)
        {
            Vector2 rootPosition = new Vector2(
                surfacePoint.x - rootToColliderCenterX,
                surfacePoint.y - rootToGroundOffset);
            boss.Rigidbody2D.position = rootPosition;
            boss.transform.position = new Vector3(rootPosition.x, rootPosition.y, boss.transform.position.z);
            Physics2D.SyncTransforms();
        }

        private void CorrectGroundContact(Vector3 surfacePoint)
        {
            if (boss.Collider2D == null || !boss.Collider2D.enabled)
                return;

            float verticalError = boss.Collider2D.bounds.min.y - surfacePoint.y;
            if (Mathf.Abs(verticalError) <= 0.001f)
                return;

            boss.Rigidbody2D.position += Vector2.down * verticalError;
            boss.transform.position = new Vector3(
                boss.Rigidbody2D.position.x,
                boss.Rigidbody2D.position.y,
                boss.transform.position.z);
            Physics2D.SyncTransforms();
        }

        public override void Exit()
        {
            if (burrowRoutine != null)
            {
                boss.StopCoroutine(burrowRoutine);
            }

            boss.IsInvulnerable = false;
            boss.HideWarning();
            boss.SetRenderersAlpha(1f);
            boss.ShaderController?.CancelBurrowVisuals();
            if (hasSurfacePoint)
                PlaceBossAtSurface(lastSurfacePoint);
            if (boss.Collider2D != null)
            {
                boss.Collider2D.enabled = true;
            }
            boss.Rigidbody2D.linearVelocity = Vector2.zero;
            boss.Rigidbody2D.gravityScale = baseGravityScale;
            Physics2D.SyncTransforms();
            if (boss.Animator != null)
            {
                boss.Animator.SetBool("IsUnderground", false);
                boss.Animator.ResetTrigger("BurrowDown");
                boss.Animator.ResetTrigger("Emerge");
            }
            burrowRoutine = null;
        }
    }

    // ==========================================
    // COOLDOWN STATE
    // ==========================================
    public class NidusCooldownState : NidusBossState
    {
        public NidusCooldownState(NidusCocoonBoss boss) : base(boss) { }

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
    public class NidusDeadState : NidusBossState
    {
        public NidusDeadState(NidusCocoonBoss boss) : base(boss) { }

        public override void Enter()
        {
            boss.StopHorizontalMovement();
            AnchorBossOnDeathGround();

            if (boss.Rigidbody2D != null)
            {
                boss.Rigidbody2D.linearVelocity = Vector2.zero;
                boss.Rigidbody2D.gravityScale = 0f;
                boss.Rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            }

            Physics2D.SyncTransforms();
            if (boss.Collider2D != null)
            {
                boss.Collider2D.enabled = false;
            }

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isWalk", false);
                boss.Animator.SetBool("isIdle", false);
                boss.Animator.SetTrigger("Die");
            }

            Debug.Log("Nidus Boss: Dead!");
        }

        private void AnchorBossOnDeathGround()
        {
            if (boss.Collider2D == null || boss.Rigidbody2D == null)
                return;

            bool colliderWasEnabled = boss.Collider2D.enabled;
            if (!colliderWasEnabled)
                boss.Collider2D.enabled = true;

            Physics2D.SyncTransforms();

            Bounds bounds = boss.Collider2D.bounds;
            Vector2 castOrigin = new Vector2(bounds.center.x, bounds.max.y + 2f);
            float castDistance = Mathf.Max(80f, bounds.size.y + 24f);
            RaycastHit2D groundHit = Physics2D.Raycast(
                castOrigin,
                Vector2.down,
                castDistance,
                boss.EnvironmentLayer);

            if (groundHit.collider != null)
            {
                float verticalError = bounds.min.y - groundHit.point.y;
                if (Mathf.Abs(verticalError) > 0.001f)
                {
                    boss.Rigidbody2D.position -= Vector2.up * verticalError;
                    boss.transform.position = new Vector3(
                        boss.Rigidbody2D.position.x,
                        boss.Rigidbody2D.position.y,
                        boss.transform.position.z);
                    Physics2D.SyncTransforms();
                }
            }
        }
    }
}
