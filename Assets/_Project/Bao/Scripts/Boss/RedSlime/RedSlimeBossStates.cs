using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bao.Bosses
{
    // ==========================================
    // IDLE STATE
    // ==========================================
    public class RedSlimeIdleState : RedSlimeBossState
    {
        public RedSlimeIdleState(RedSlimeBoss boss) : base(boss) { }

        public override void Enter()
        {
            boss.StopHorizontalMovement();
            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isMoving", false);
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

        public override void PhysicsUpdate()
        {
            boss.StopHorizontalMovement();
        }

        public override void Exit()
        {
        }
    }

    // ==========================================
    // CHASE STATE (COMBAT DECISION MANAGER)
    // ==========================================
    public class RedSlimeChaseState : RedSlimeBossState
    {
        public RedSlimeChaseState(RedSlimeBoss boss) : base(boss) { }

        public override void Enter()
        {
            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isMoving", false);
            }
        }

        public override void LogicUpdate()
        {
            if (boss.IsDead || boss.IsTransitioningPhases) return;

            if (!boss.HasTargetInDetectionRange())
            {
                boss.ChangeState(boss.IdleState);
                return;
            }

            if (boss.Target != null)
            {
                boss.FacePosition(boss.Target.position);
            }

            float distToPlayer = Vector2.Distance(boss.CenterPosition, boss.Target.position);

            if (!boss.CanPerformAction())
            {
                boss.StopHorizontalMovement();
                if (boss.Animator != null) boss.Animator.SetBool("isMoving", false);
                return;
            }

            // Kiểm tra xem đã đến lúc gộp lại chưa (chỉ cho Slime B và Slime C)
            if (boss.CanStartMerge())
            {
                var partner = boss.FindNearestSameFormPartner();
                if (partner != null)
                {
                    boss.MergePartner = partner;
                    boss.ChangeState(boss.MergeState);
                    return;
                }
            }

            List<(System.Action action, float weight, RedSlimeBoss.SkillCategory category)> availableSkills
                = new List<(System.Action, float, RedSlimeBoss.SkillCategory)>();

            // Slime B đặc biệt ưu tiên kỹ năng Lướt Săn Mồi (Hunter Dash), Bắn Đạn Thạch (Tri-Burst) và Gọng Kìm Song Sát (Dual Cross-Dash)
            if (boss.CurrentForm == SlimeForm.SlimeB)
            {
                // Kỹ năng Phối Hợp Song Sát: Gọng Kìm Sấm Sét (Dual Pincer Cross-Dash) khi cả 2 Slime B còn sống
                if (boss.CanDualCrossDash())
                {
                    var partnerB = boss.FindDualCrossDashPartner();
                    if (partnerB != null)
                    {
                        float dualCrossDashWeight = 145f;
                        availableSkills.Add((() => {
                            Vector2 clashCenter = boss.Target != null ? (Vector2)boss.Target.position : (Vector2)boss.CenterPosition;
                            // Phân vai: Con khởi xướng làm Master (bên trái mục tiêu), con đối tác làm Slave (bên phải mục tiêu)
                            boss.BDualCrossDashState.Setup(partnerB, CrossDashRole.MasterLeft, clashCenter);
                            partnerB.BDualCrossDashState.Setup(boss, CrossDashRole.SlaveRight, clashCenter);

                            partnerB.ChangeState(partnerB.BDualCrossDashState);
                            boss.ChangeState(boss.BDualCrossDashState);
                        }, dualCrossDashWeight, RedSlimeBoss.SkillCategory.Dash));
                    }
                }

                // Kỹ năng Bắn Đạn Thạch 3 hướng (Ranged Spit / Tri-Burst) khi ở cự ly trung bình - xa
                if (boss.CanShootB())
                {
                    float shootBWeight = 90f;
                    if (distToPlayer > 3.0f && distToPlayer <= 12f) shootBWeight = 135f;
                    availableSkills.Add((() => boss.ChangeState(boss.BShootState), shootBWeight, RedSlimeBoss.SkillCategory.Shoot));
                }

                // Kỹ năng Lướt Săn Mồi (Hunter Dash / Slime Bullet)
                if (boss.CanDashB())
                {
                    float dashBWeight = 95f;
                    if (distToPlayer > boss.MeleeRadius && distToPlayer <= 8f) dashBWeight = 120f;
                    availableSkills.Add((() => boss.ChangeState(boss.BDashState), dashBWeight, RedSlimeBoss.SkillCategory.Dash));
                }
            }

            // Slime C: Kỹ năng Trận Đồ Tia Laser Tinh Thể Cube (Matrix Laser Link) khi có đồng đội ở gần
            if (boss.CurrentForm == SlimeForm.SlimeC && boss.CanLaserC())
            {
                var laserPartner = FindLaserPartner();
                if (laserPartner != null)
                {
                    float laserWeight = 110f;
                    availableSkills.Add((() => {
                        boss.CLaserMatrixState.SetPartner(laserPartner);
                        boss.ChangeState(boss.CLaserMatrixState);
                    }, laserWeight, RedSlimeBoss.SkillCategory.Laser));
                }
            }

            // Slime C: Kỹ năng Bật Nảy Đa Điểm Siêu Tốc (Solar Pinball Popcorn Bounce)
            if (boss.CurrentForm == SlimeForm.SlimeC && boss.CanPinballC())
            {
                float pinballWeight = 100f;
                if (distToPlayer > boss.MeleeRadius) pinballWeight = 135f;
                availableSkills.Add((() => boss.ChangeState(boss.CPinballState), pinballWeight, RedSlimeBoss.SkillCategory.Bounce));
            }

            float meleeWeight = (distToPlayer <= boss.MeleeRadius * 1.3f) ? 100f : 0f;
            if (boss.CanMelee() && distToPlayer <= boss.MeleeRadius * 1.3f)
            {
                availableSkills.Add((() => boss.ChangeState(boss.MeleeState), meleeWeight, RedSlimeBoss.SkillCategory.Melee));
            }

            // Chỉ Slime A sử dụng Ground Slam; Slime B dùng Hunter Dash, Slime C dùng Pinball & Laser Matrix
            float slamWeight = 40f;
            if (distToPlayer > boss.OptimalSpacingDistance * 0.5f) slamWeight = 70f;
            if (distToPlayer > boss.OptimalSpacingDistance) slamWeight = 90f;
            if (boss.CurrentForm == SlimeForm.SlimeA && boss.CanSlam())
            {
                availableSkills.Add((() => boss.ChangeState(boss.GroundSlamState), slamWeight, RedSlimeBoss.SkillCategory.AOE));
            }

            for (int i = 0; i < availableSkills.Count; i++)
            {
                var skill = availableSkills[i];
                if (skill.category == boss.LastUsedSkillCategory && skill.category != RedSlimeBoss.SkillCategory.None)
                {
                    availableSkills[i] = (skill.action, skill.weight * 0.2f, skill.category);
                }
            }

            if (availableSkills.Count > 0)
            {
                float totalWeight = 0f;
                foreach (var skill in availableSkills) totalWeight += skill.weight;

                float randomValue = Random.Range(0f, totalWeight);
                float currentWeight = 0f;

                foreach (var skill in availableSkills)
                {
                    currentWeight += skill.weight;
                    if (randomValue <= currentWeight)
                    {
                        skill.action.Invoke();
                        return;
                    }
                }
            }

            // Di chuyển tiếp cận người chơi và giữ khoảng cách né nhau giữa các Slime (Flocking Separation)
            if (boss.Target != null)
            {
                // Vector hướng về người chơi
                float targetDir = Mathf.Sign(boss.Target.position.x - boss.CenterPosition.x);

                // Tính lực đẩy né các con Slime khác ở gần
                float separationDir = 0f;
                for (int i = 0; i < RedSlimeBoss.ActiveSlimes.Count; i++)
                {
                    var other = RedSlimeBoss.ActiveSlimes[i];
                    if (other == null || other == boss || other.IsDead) continue;

                    float dist = Vector2.Distance(boss.CenterPosition, other.CenterPosition);
                    if (dist < boss.SeparationDistance && dist > 0.01f)
                    {
                        // Đẩy ngược hướng với con Slime ở gần
                        float pushDir = Mathf.Sign(boss.CenterPosition.x - other.CenterPosition.x);
                        // Càng gần thì lực đẩy càng mạnh
                        float pushStrength = (boss.SeparationDistance - dist) / boss.SeparationDistance;
                        separationDir += pushDir * pushStrength;
                    }
                }

                // Tổng hợp hướng di chuyển (Target Direction + Separation)
                float finalMoveDirection = targetDir + (separationDir * boss.SeparationWeight);
                if (Mathf.Abs(finalMoveDirection) > 0.05f)
                {
                    boss.MoveHorizontal(Mathf.Sign(finalMoveDirection), boss.MoveSpeed);
                    if (boss.Animator != null) boss.Animator.SetBool("isMoving", true);
                }
                else
                {
                    boss.StopHorizontalMovement();
                    if (boss.Animator != null) boss.Animator.SetBool("isMoving", false);
                }
            }
            else
            {
                boss.StopHorizontalMovement();
                if (boss.Animator != null) boss.Animator.SetBool("isMoving", false);
            }
        }

        public override void PhysicsUpdate()
        {
            if (boss.IsDead || boss.IsTransitioningPhases)
            {
                boss.StopHorizontalMovement();
            }
        }

        public override void Exit()
        {
            boss.StopHorizontalMovement();
            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isMoving", false);
            }
        }

        private RedSlimeBoss FindLaserPartner()
        {
            float maxRange = boss.LaserMaxRange;
            RedSlimeBoss closest = null;
            float minDist = maxRange;

            for (int i = 0; i < RedSlimeBoss.ActiveSlimes.Count; i++)
            {
                var other = RedSlimeBoss.ActiveSlimes[i];
                if (other == null || other == boss || other.IsDead || other.CurrentForm != SlimeForm.SlimeC) continue;
                if (other.IsMerging || other.IsPopping) continue;

                float dist = Vector2.Distance(boss.CenterPosition, other.CenterPosition);
                if (dist <= minDist)
                {
                    minDist = dist;
                    closest = other;
                }
            }

            return closest;
        }
    }

    // ==========================================
    // MELEE ATTACK STATE
    // ==========================================
    public class RedSlimeMeleeState : RedSlimeBossState
    {
        private float enterTime;
        private bool hasDealtDamage;

        public RedSlimeMeleeState(RedSlimeBoss boss) : base(boss) { }

        public override void Enter()
        {
            enterTime = Time.time;
            hasDealtDamage = false;

            boss.StopHorizontalMovement();
            boss.MarkMeleeUsed();
            boss.AudioController?.PlayMeleeSound();

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isMoving", false);
                boss.Animator.SetTrigger("AttackTrig");
            }
            if (boss.Target != null) boss.FacePosition(boss.Target.position);
        }

        public override void LogicUpdate()
        {
            float elapsed = Time.time - enterTime;

            if (elapsed >= boss.MeleeDelay && !hasDealtDamage)
            {
                DealDamage();
                hasDealtDamage = true;
            }

            if (elapsed >= boss.MeleeAnimDuration)
            {
                boss.ChangeState(boss.ChaseState);
            }
        }

        private void DealDamage()
        {
            Vector2 attackPoint = (Vector2)boss.CenterPosition + Vector2.right * boss.FacingDirection * boss.MeleeVFXOffset.x;
            Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint, boss.MeleeRadius);

            foreach (var hit in hits)
            {
                PlayerHealth playerHealth = hit.GetComponentInParent<PlayerHealth>();
                if (playerHealth != null || hit.CompareTag("Player"))
                {
                    PlayerHitResolver.TryApplyHitToPlayer(
                        hit,
                        boss.MeleeDamage,
                        ParryableAttackType.Melee,
                        EnemyParryResponder.Ensure(boss),
                        boss.transform,
                        hit.transform.position - boss.transform.position);
                    boss.AudioController?.PlayMeleeImpactSound();
                }
            }

            boss.ShaderController?.SpawnImpactVFX(attackPoint, boss.FacingDirection);
        }

        public override void PhysicsUpdate()
        {
            boss.StopHorizontalMovement();
        }

        public override void Exit()
        {
            boss.StopHorizontalMovement();
            if (boss.Animator != null) boss.Animator.ResetTrigger("AttackTrig");
        }
    }

    // ==========================================
    // GROUND SLAM & SPIKE WAVE STATE (NHẢY NỆN ĐẤT + GAI 2 BÊN)
    // ==========================================
    public class RedSlimeGroundSlamState : RedSlimeBossState
    {
        private Coroutine slamRoutine;
        private HashSet<int> slamHitTracker = new HashSet<int>();

        public RedSlimeGroundSlamState(RedSlimeBoss boss) : base(boss) { }

        public override void Enter()
        {
            boss.StopHorizontalMovement();
            boss.MarkSlamUsed();
            slamHitTracker.Clear();

            // Tang toc nhip dap tim neon 3x bao hieu don danh cuc manh
            boss.ShaderController?.SetChargingPulseSpeed(true);

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isMoving", false);
            }
            if (boss.Target != null) boss.FacePosition(boss.Target.position);

            slamRoutine = boss.StartCoroutine(GroundSlamSequence());
        }

        private IEnumerator GroundSlamSequence()
        {
            Vector3 startPos = boss.transform.position;
            float targetDir = boss.FacingDirection;
            if (boss.Target != null)
            {
                targetDir = Mathf.Sign(boss.Target.position.x - boss.CenterPosition.x);
                boss.FaceDirection(targetDir);
            }

            // GIAI ĐOẠN 1: LẤY ĐÀ VÀ NHẢY BẬT LÊN CAO VỀ PHÍA PLAYER
            float jumpHeight = 4.5f;
            float jumpHorizontalDistance = 2.5f * targetDir;
            Vector3 peakPos = startPos + new Vector3(jumpHorizontalDistance * 0.5f, jumpHeight, 0f);

            float jumpUpTime = Mathf.Max(0.25f, boss.SlamJumpDuration * 0.55f);
            float elapsed = 0f;

            if (boss.Animator != null)
            {
                boss.Animator.SetTrigger("AttackJump");
            }

            boss.AudioController?.PlaySlamJumpSound();

            // Windup: Tu nang luong do/cam vao tam Boss khi bat nhay
            boss.ShaderController?.TriggerWindupGathering(boss.CenterPosition, jumpUpTime);

            while (elapsed < jumpUpTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / jumpUpTime);
                float easeOut = Mathf.Sin(t * Mathf.PI * 0.5f);
                boss.transform.position = Vector3.Lerp(startPos, peakPos, easeOut);
                yield return null;
            }

            // GIAI ĐOẠN 2: LAO NỆN THẲNG XUỐNG ĐẤT CỰC MẠNH (DIVE SLAM)
            Vector3 landTarget = startPos + new Vector3(jumpHorizontalDistance, 0f, 0f);
            RaycastHit2D groundHit = Physics2D.Raycast(peakPos, Vector2.down, 15f, boss.EnvironmentLayer);
            if (groundHit.collider != null)
            {
                landTarget.y = groundHit.point.y;
            }

            float slamDownTime = Mathf.Max(0.18f, boss.SlamJumpDuration * 0.45f);
            elapsed = 0f;
            Vector3 currentPeak = boss.transform.position;

            float trailTimer = 0f;
            while (elapsed < slamDownTime)
            {
                elapsed += Time.deltaTime;
                trailTimer += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / slamDownTime);
                float easeIn = t * t;
                boss.transform.position = Vector3.Lerp(currentPeak, landTarget, easeIn);

                // Dive Motion Trail: Vet sao choi do ruc phia sau khi lao cam xuong dat
                if (trailTimer >= 0.04f)
                {
                    trailTimer = 0f;
                    boss.ShaderController?.SpawnDiveGhostTrail(boss.CenterPosition, boss.transform.localScale, boss.FacingDirection);
                }

                yield return null;
            }

            boss.transform.position = landTarget;

            // GIAI ĐOẠN 3: TIẾP ĐẤT CHUẨN TÂM ĐÁY CHÂN BOSS + OFFSET -> SHOCKWAVE + BỤI ĐÁ NỔ TUNG
            Vector3 groundCenter = boss.GroundPosition;
            Vector3 slamPoint = groundCenter + new Vector3(boss.SlamVFXOffset.x * boss.FacingDirection, boss.SlamVFXOffset.y, 0f);

            boss.ShaderController?.SpawnGroundImpactVFX(slamPoint);
            boss.ShaderController?.TriggerScreenShake(0.35f, 0.25f);
            boss.AudioController?.PlaySlamImpactSound();

            // Gây sát thương nện đất (single hit tracking)
            Vector2 aoeCenter = (Vector2)boss.CenterPosition + new Vector2(boss.SlamVFXOffset.x * boss.FacingDirection, boss.SlamVFXOffset.y);
            Collider2D[] hits = Physics2D.OverlapCircleAll(aoeCenter, boss.SlamAOERadius);
            foreach (var hit in hits)
            {
                PlayerHealth playerHealth = hit.GetComponentInParent<PlayerHealth>();
                if (playerHealth != null || hit.CompareTag("Player"))
                {
                    GameObject rootGo = hit.transform.root.gameObject;
                    int id = rootGo.GetInstanceID();
                    if (!slamHitTracker.Contains(id))
                    {
                        IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
                            hit,
                            boss.SlamDamage,
                            ParryableAttackType.Melee,
                            EnemyParryResponder.Ensure(boss),
                            boss.transform,
                            hit.transform.position - boss.transform.position);

                        if (resolution != IncomingAttackResolution.Ignored)
                        {
                            slamHitTracker.Add(id);
                        }
                    }
                }
            }

            // GIAI ĐOẠN 4: SÓNG GAI 2 BÊN TRỒI LÊN ĐỐI XỨNG GƯƠNG TỪ TÂM SLAM POINT (BAO GỒM CẢ OFFSET)
            if (boss.SpikePrefab != null)
            {
                boss.StartCoroutine(SpawnSpikeWaveRoutine(slamPoint));
            }

            float recoveryDelay = Mathf.Max(0.2f, boss.SlamAnimDuration - boss.SlamJumpDuration);
            yield return new WaitForSeconds(recoveryDelay);

            boss.ChangeState(boss.ChaseState);
        }

        private IEnumerator SpawnSpikeWaveRoutine(Vector2 slamCenter)
        {
            for (int i = 0; i < boss.SpikeCountPerSide; i++)
            {
                float offset = boss.SpikeStartOffset + (i * boss.SpikeSpacing);

                // Gai bên Phải (direction = +1)
                Vector2 rightOrigin = new Vector2(slamCenter.x + offset, slamCenter.y + 2f);
                SpawnGroundedSpike(rightOrigin, 1);

                // Gai bên Trái (direction = -1)
                Vector2 leftOrigin = new Vector2(slamCenter.x - offset, slamCenter.y + 2f);
                SpawnGroundedSpike(leftOrigin, -1);

                yield return new WaitForSeconds(boss.SpikeSpawnDelay);
            }
        }

        private void SpawnGroundedSpike(Vector2 origin, int direction)
        {
            Vector3 spawnPos = origin;
            float raycastDistance = Mathf.Max(15f, Mathf.Abs(boss.SlamVFXOffset.y) + 10f);
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, raycastDistance, boss.EnvironmentLayer);
            if (hit.collider != null)
            {
                spawnPos = hit.point;
            }
            else
            {
                spawnPos.y -= 2f;
            }

            float baseSpriteTilt = 41.12f;
            float targetOutwardAngle = Mathf.Max(0f, boss.SpikeAngle);
            float rotationZ = direction * (baseSpriteTilt - targetOutwardAngle);

            Quaternion rotation = Quaternion.Euler(0f, 0f, rotationZ);
            GameObject spikeObj = Object.Instantiate(boss.SpikePrefab, spawnPos, rotation);

            Vector3 currentScale = spikeObj.transform.localScale;
            currentScale.x = Mathf.Abs(currentScale.x) * -direction;
            spikeObj.transform.localScale = currentScale;

            RedSlimeGroundSpike spikeComponent = spikeObj.GetComponent<RedSlimeGroundSpike>();
            if (spikeComponent != null)
            {
                spikeComponent.Initialize(boss.SpikeDamage, boss.SpikeLifetime, slamHitTracker);
            }
            else
            {
                Object.Destroy(spikeObj, boss.SpikeLifetime);
            }
        }

        public override void LogicUpdate() { }

        public override void PhysicsUpdate()
        {
            boss.StopHorizontalMovement();
        }

        public override void Exit()
        {
            if (slamRoutine != null)
                boss.StopCoroutine(slamRoutine);
            boss.StopHorizontalMovement();
            boss.ShaderController?.SetChargingPulseSpeed(false);
        }
    }

    // ==========================================
    // DEAD STATE
    // ==========================================
    public class RedSlimeDeadState : RedSlimeBossState
    {
        public RedSlimeDeadState(RedSlimeBoss boss) : base(boss) { }

        public override void Enter()
        {
            boss.StopHorizontalMovement();

            if (boss.Collider2D != null)
            {
                boss.Collider2D.enabled = false;
            }

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isMoving", false);
                boss.Animator.SetTrigger("DieTrig");
            }

            boss.ShaderController?.StartDeathDissolve(2.0f);

            Debug.Log("[RedSlimeBoss] Dead!");
            Object.Destroy(boss.gameObject, 3.5f);
        }
    }

    // ==========================================
    // MERGE STATE (TỰ TÌM NHAU ĐỂ GỘP LẠI)
    // ==========================================
    public class RedSlimeMergeState : RedSlimeBossState
    {
        public RedSlimeMergeState(RedSlimeBoss boss) : base(boss) { }

        public override void Enter()
        {
            boss.AudioController?.PlayMergeGatherSound();
            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isMoving", true);
            }
        }

        public override void LogicUpdate()
        {
            if (boss.IsDead || boss.IsInvulnerable) return;

            // Kiểm tra đối tác còn hợp lệ không (cùng form, chưa chết)
            if (boss.MergePartner == null || boss.MergePartner.IsDead || boss.MergePartner.CurrentForm != boss.CurrentForm)
            {
                // Tìm đối tác khác
                var newPartner = boss.FindNearestSameFormPartner();
                if (newPartner != null)
                {
                    boss.MergePartner = newPartner;
                }
                else
                {
                    // Không còn đối tác nào cùng form -> quay về tấn công
                    boss.ResetMergeTimer();
                    boss.ChangeState(boss.ChaseState);
                    return;
                }
            }

            // Hướng mặt về phía đối tác
            boss.FacePosition(boss.MergePartner.CenterPosition);

            float dist = Vector2.Distance(boss.CenterPosition, boss.MergePartner.CenterPosition);

            // Khi đến đủ gần nhau -> Kích hoạt gộp (Merge)
            if (dist <= boss.MergeDistance)
            {
                boss.MergeWith(boss.MergePartner);
                return;
            }

            // Di chuyển nhanh lại gần đối tác
            float dir = Mathf.Sign(boss.MergePartner.CenterPosition.x - boss.CenterPosition.x);
            boss.MoveHorizontal(dir, boss.MoveSpeed * boss.MergeSpeedMultiplier);
        }

        public override void PhysicsUpdate()
        {
            if (boss.IsDead || boss.IsInvulnerable)
            {
                boss.StopHorizontalMovement();
            }
        }

        public override void Exit()
        {
            boss.StopHorizontalMovement();
            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isMoving", false);
            }
            boss.MergePartner = null;
        }
    }

    // ==========================================
    // SLIME B DASH ATTACK STATE (SLIME BULLET / HUNTER DASH)
    // ==========================================
    public class RedSlimeBDashState : RedSlimeBossState
    {
        private Coroutine dashRoutine;
        private HashSet<int> hitTracker = new HashSet<int>();

        public RedSlimeBDashState(RedSlimeBoss boss) : base(boss) { }

        public override void Enter()
        {
            boss.StopHorizontalMovement();
            boss.MarkDashBUsed();
            hitTracker.Clear();

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isMoving", false);
            }
            if (boss.Target != null) boss.FacePosition(boss.Target.position);

            dashRoutine = boss.StartCoroutine(DashSequence());
        }

        private IEnumerator DashSequence()
        {
            float targetDir = boss.FacingDirection;
            if (boss.Target != null)
            {
                targetDir = Mathf.Sign(boss.Target.position.x - boss.CenterPosition.x);
                boss.FaceDirection(targetDir);
            }

            // GIAI ĐOẠN 1: TỤ LỰC (WINDUP CHARGE & SQUASH)
            // Co nén thân Slime chuẩn bị bắn như viên đạn
            boss.ResetSlimeBElements();
            boss.ShaderController?.TriggerSquashAndStretch(boss.DashBWindupTime, 0.45f);
            boss.ShaderController?.TriggerSlimeBChargeGathering(boss.CenterPosition, boss.DashBWindupTime);
            boss.AudioController?.PlayDashBWindupSound();

            yield return new WaitForSeconds(boss.DashBWindupTime);

            // GIAI ĐOẠN 2: PHI LƯỚT TỐC ĐỘ CAO (HIGH-SPEED DASH / SLIME BULLET)
            float dashTime = boss.DashBDuration;
            float elapsed = 0f;
            float trailTimer = 0f;
            boss.AudioController?.PlayDashBLaunchSound();

            while (elapsed < dashTime)
            {
                elapsed += Time.deltaTime;
                trailTimer += Time.deltaTime;

                // Di chuyển lao về phía trước với vận tốc lớn
                boss.MoveHorizontal(targetDir, boss.DashBSpeed);

                // Spawn chuỗi bóng mờ (Afterimage Ghost Trail) & Electric Sparks
                if (trailTimer >= 0.035f)
                {
                    trailTimer = 0f;
                    boss.ShaderController?.SpawnSlimeBDashTrail(boss.CenterPosition, boss.transform.localScale, targetDir);
                }

                // Check va chạm gây sát thương liên tục dọc đường lướt
                Vector2 attackPoint = (Vector2)boss.CenterPosition + Vector2.right * targetDir * 0.5f;
                Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint, boss.DashBRadius);
                foreach (var hit in hits)
                {
                    PlayerHealth playerHealth = hit.GetComponentInParent<PlayerHealth>();
                    if (playerHealth != null || hit.CompareTag("Player"))
                    {
                        GameObject rootGo = hit.transform.root.gameObject;
                        int id = rootGo.GetInstanceID();
                        if (!hitTracker.Contains(id))
                        {
                            IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
                                hit,
                                boss.DashBDamage,
                                ParryableAttackType.Melee,
                                EnemyParryResponder.Ensure(boss),
                                boss.transform,
                                (hit.bounds.center - boss.CenterPosition));

                            if (resolution != IncomingAttackResolution.Ignored)
                            {
                                hitTracker.Add(id);

                                // Hiệu ứng nổ tia chớp & shockwave khi chém trúng Player
                                boss.ShaderController?.SpawnSlimeBDashImpactVFX(hit.bounds.center, targetDir);
                                boss.ShaderController?.TriggerScreenShake(0.18f, 0.2f);
                                boss.AudioController?.PlayDashBImpactSound(hit.bounds.center);
                            }
                        }
                    }
                }

                yield return null;
            }

            // GIAI ĐOẠN 3: HÃM TỐC VÀ HỒI PHỤC (RECOVERY SLIDE & IMPACT SPLASH)
            boss.StopHorizontalMovement();
            boss.ShaderController?.SpawnSlimeBDashImpactVFX(boss.CenterPosition, targetDir);
            boss.ShaderController?.TriggerSquashAndStretch(0.25f, 0.25f);

            yield return new WaitForSeconds(0.2f);

            boss.ChangeState(boss.ChaseState);
        }

        public override void LogicUpdate() { }

        public override void PhysicsUpdate() { }

        public override void Exit()
        {
            if (dashRoutine != null)
            {
                boss.StopCoroutine(dashRoutine);
            }
            boss.StopHorizontalMovement();
            if (boss.Animator != null)
            {
                boss.Animator.ResetTrigger("AttackBTrig");
            }
        }
    }

    // ==========================================
    // SLIME B TRI-BURST ELEMENTS SHOOT STATE (BẮN ĐẠN THẠCH 3 HƯỚNG)
    // ==========================================
    public class RedSlimeBShootState : RedSlimeBossState
    {
        private Coroutine shootRoutine;
        private float enterTime;
        private bool hasSpitFired;

        public RedSlimeBShootState(RedSlimeBoss boss) : base(boss) { }

        public override void Enter()
        {
            enterTime = Time.time;
            hasSpitFired = false;

            boss.StopHorizontalMovement();
            boss.MarkShootBUsed();
            boss.ResetSlimeBElements();

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isMoving", false);
                boss.Animator.SetTrigger("AttackBTrig");
            }

            if (boss.Target != null)
            {
                boss.FacePosition(boss.Target.position);
            }

            Vector3 mouthPos = (Vector3)boss.CenterPosition + new Vector3(boss.ShootBMouthOffset.x * boss.FacingDirection, boss.ShootBMouthOffset.y, 0f);
            boss.ShaderController?.TriggerSlimeBSpitGathering(mouthPos, boss.ShootBWindupDuration);
            boss.ShaderController?.TriggerSquashAndStretch(boss.ShootBWindupDuration, 0.35f);
            boss.AudioController?.PlayShootBWindupSound();

            shootRoutine = boss.StartCoroutine(ShootSequence());
        }

        public override void LogicUpdate()
        {
            float elapsed = Time.time - enterTime;

            // Xoay mặt bám theo Player trong 0.35s đầu của giai đoạn tụ lực
            if (elapsed < 0.35f && boss.Target != null)
            {
                boss.FacePosition(boss.Target.position);
            }

            // Fallback timer: Đảm bảo bắn đúng frame 0.667s kể cả khi animation event bị drop
            if (elapsed >= boss.ShootBWindupDuration && !hasSpitFired)
            {
                FireTriBurst();
            }

            // Kết thúc animation và quay lại ChaseState
            if (elapsed >= boss.ShootBAnimDuration)
            {
                boss.ChangeState(boss.ChaseState);
            }
        }

        /// <summary>
        /// Được gọi từ Animation Event AE_SpitShoot trên RedSlimeBoss
        /// </summary>
        public void OnAnimationSpitEvent()
        {
            if (!hasSpitFired)
            {
                FireTriBurst();
            }
        }

        private void FireTriBurst()
        {
            hasSpitFired = true;

            float facing = boss.FacingDirection;
            Vector3 mouthPos = (Vector3)boss.CenterPosition + new Vector3(boss.ShootBMouthOffset.x * facing, boss.ShootBMouthOffset.y, 0f);

            // Kích hoạt các đạn thạch animation Slime elements 1, 2, 3
            boss.ResetSlimeBElements();

            // Hiệu ứng lóe sáng miệng & rung màn hình khi nhả đạn
            boss.ShaderController?.SpawnTriBurstMuzzleFlash(mouthPos, facing);
            boss.ShaderController?.TriggerScreenShake(0.14f, 0.16f);
            boss.AudioController?.PlayShootBFireSound();
        }

        private IEnumerator ShootSequence()
        {
            yield return new WaitForSeconds(boss.ShootBAnimDuration);
            boss.ChangeState(boss.ChaseState);
        }

        public override void PhysicsUpdate()
        {
            boss.StopHorizontalMovement();
        }

        public override void Exit()
        {
            if (shootRoutine != null)
            {
                boss.StopCoroutine(shootRoutine);
            }
            boss.StopHorizontalMovement();
            if (boss.Animator != null)
            {
                boss.Animator.ResetTrigger("AttackBTrig");
            }
        }
    }

    // =========================================================================
    // SLIME B CO-OP: GỌNG KÌM SẤM SÉT SONG SÁT (DUAL PINCER CROSS-DASH STATE)
    // =========================================================================
    public enum CrossDashRole { MasterLeft, SlaveRight }

    public class RedSlimeBDualPincerCrossDashState : RedSlimeBossState
    {
        private RedSlimeBoss partnerSlime;
        private CrossDashRole role;
        private Vector2 clashCenter;
        private Coroutine coopRoutine;
        private HashSet<int> hitTracker = new HashSet<int>();
        private bool isParried = false;

        public RedSlimeBDualPincerCrossDashState(RedSlimeBoss boss) : base(boss) { }

        public void Setup(RedSlimeBoss partner, CrossDashRole assignedRole, Vector2 center)
        {
            partnerSlime = partner;
            role = assignedRole;
            clashCenter = center;
        }

        public override void Enter()
        {
            boss.StopHorizontalMovement();
            boss.MarkDualCrossDashUsed();
            boss.ResetSlimeBElements();
            hitTracker.Clear();
            isParried = false;

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isMoving", false);
            }

            coopRoutine = boss.StartCoroutine(DualPincerSequence());
        }

        private IEnumerator DualPincerSequence()
        {
            // GIAI ĐOẠN 1: DẠT 2 CÁNH & CHIẾU TIA NGẮM GIAO CẮT CHỮ X (0.55s)
            float flankOffset = (role == CrossDashRole.MasterLeft) ? -boss.DualCrossDashFlankDist : boss.DualCrossDashFlankDist;
            Vector3 flankTargetPos = new Vector3(clashCenter.x + flankOffset, boss.transform.position.y, boss.transform.position.z);

            // Kiểm tra bề mặt đất
            RaycastHit2D groundHit = Physics2D.Raycast(flankTargetPos + Vector3.up * 2f, Vector2.down, 10f);
            if (groundHit.collider != null)
            {
                flankTargetPos.y = groundHit.point.y;
            }

            // Nhảy dạt nhanh ra vị trí cánh
            float repositionDuration = 0.22f;
            float repElapsed = 0f;
            Vector3 repStartPos = boss.transform.position;

            while (repElapsed < repositionDuration)
            {
                repElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(repElapsed / repositionDuration);
                float curX = Mathf.Lerp(repStartPos.x, flankTargetPos.x, t);
                float curY = Mathf.Lerp(repStartPos.y, flankTargetPos.y, t) + Mathf.Sin(t * Mathf.PI) * 1.2f;
                boss.transform.position = new Vector3(curX, curY, repStartPos.z);
                yield return null;
            }
            boss.transform.position = flankTargetPos;
            boss.StopHorizontalMovement();

            // Quay mặt hướng vào tâm người chơi
            float aimFacingDir = (role == CrossDashRole.MasterLeft) ? 1f : -1f;
            boss.FaceDirection(aimFacingDir);

            // Tụ lực nén cơ thể & Bật Overdrive phát sáng rực rỡ
            boss.ShaderController?.TriggerSquashAndStretch(boss.DualCrossDashWindup, 0.45f);
            boss.ShaderController?.SetCombatStress(1.0f);

            // Bắn tia laser ngắm cảnh báo giao cắt chữ X hướng vào mục tiêu
            Vector3 laserStartPoint = boss.CenterPosition;
            boss.ShaderController?.SpawnSlimeBDualLaserCrossTelegraph(laserStartPoint, (Vector3)clashCenter, boss.DualCrossDashWindup);
            boss.AudioController?.PlayDualWarningSound();

            yield return new WaitForSeconds(boss.DualCrossDashWindup);

            // GIAI ĐOẠN 2: LƯỚT SIÊU THANH CẮT XUYÊN NHAU (0.30s - 20m/s)
            float dashDirection = aimFacingDir;
            float dashSpeed = boss.DualCrossDashSpeed;
            boss.Rigidbody2D.linearVelocity = new Vector2(dashDirection * dashSpeed, 0f);
            boss.AudioController?.PlayDualLaunchSound();

            // Sóng chấn động khi xuất phát
            boss.ShaderController?.SpawnSlimeBDashImpactVFX(boss.CenterPosition, dashDirection);
            boss.ShaderController?.TriggerScreenShake(0.18f, 0.20f);

            float dashDuration = boss.DualCrossDashDuration;
            float elapsed = 0f;
            float trailTimer = 0f;
            bool hasTriggeredCenterClash = false;

            while (elapsed < dashDuration)
            {
                if (isParried) yield break; // Nếu bị Parry thì thoát vòng lặp ngay

                elapsed += Time.deltaTime;
                trailTimer += Time.deltaTime;

                // Sinh vệt dư ảnh vàng hổ phách dày đặc
                if (trailTimer >= 0.025f)
                {
                    trailTimer = 0f;
                    boss.ShaderController?.SpawnSlimeBDualDashGhostStream(boss.CenterPosition, boss.transform.localScale, dashDirection);
                }

                // Kiểm tra điểm giao cắt tại trung tâm để phát nổ sóng xung kích Guillotine
                float distToClashCenter = Mathf.Abs(boss.CenterPosition.x - clashCenter.x);
                if (!hasTriggeredCenterClash && distToClashCenter <= 1.2f)
                {
                    hasTriggeredCenterClash = true;
                    if (role == CrossDashRole.MasterLeft)
                    {
                        // Con Master phụ trách kích hoạt vụ nổ sóng xung kích Guillotine tại điểm giao cắt
                        boss.ShaderController?.SpawnSlimeBCrossClashImpact(clashCenter);
                        boss.ShaderController?.TriggerScreenShake(0.28f, 0.32f);
                        boss.AudioController?.PlayDualClashSound(clashCenter);
                    }
                }

                // Quét va chạm gây sát thương chuẩn khi lướt qua người chơi
                Collider2D[] hits = Physics2D.OverlapCircleAll(boss.CenterPosition, boss.DualCrossDashRadius);
                foreach (var hit in hits)
                {
                    PlayerHealth playerHealth = hit.GetComponentInParent<PlayerHealth>();
                    if (playerHealth != null || hit.CompareTag("Player"))
                    {
                        GameObject rootGo = hit.transform.root.gameObject;
                        int id = rootGo.GetInstanceID();
                        if (!hitTracker.Contains(id))
                        {
                            IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
                                hit,
                                boss.DualCrossDashDamage,
                                ParryableAttackType.Melee,
                                EnemyParryResponder.Ensure(boss),
                                boss.transform,
                                new Vector2(dashDirection, 0.4f).normalized,
                                canBeBlocked: true,
                                isSlowAttack: false);

                            if (resolution == IncomingAttackResolution.Parried)
                            {
                                OnCrossDashParried();
                                if (partnerSlime != null && partnerSlime.CurrentState is RedSlimeBDualPincerCrossDashState partnerState)
                                {
                                    partnerState.OnCrossDashParried();
                                }
                                yield break;
                            }
                            else if (resolution != IncomingAttackResolution.Ignored)
                            {
                                hitTracker.Add(id);
                                boss.ShaderController?.SpawnImpactVFX(hit.bounds.center, dashDirection);
                            }
                        }
                    }
                }

                yield return null;
            }

            // GIAI ĐOẠN 3: TRƯỢT TIẾP ĐẤT & HÃM TỐC HỒI PHỤC (0.25s)
            boss.StopHorizontalMovement();
            boss.ResetSlimeBElements();
            boss.ShaderController?.SetCombatStress(0.0f);
            boss.ShaderController?.SpawnSlimeBDashImpactVFX(boss.CenterPosition, dashDirection);
            boss.ShaderController?.TriggerSquashAndStretch(0.25f, 0.25f);

            yield return new WaitForSeconds(0.25f);
            boss.ChangeState(boss.ChaseState);
        }

        /// <summary>
        /// Xử lý khi người chơi thực hiện Parry hoàn hảo trúng đòn lướt song sát
        /// </summary>
        public void OnCrossDashParried()
        {
            if (isParried) return;
            isParried = true;

            if (coopRoutine != null)
            {
                boss.StopCoroutine(coopRoutine);
            }

            boss.StartCoroutine(ParriedStunSequence());
        }

        private IEnumerator ParriedStunSequence()
        {
            boss.StopHorizontalMovement();
            boss.ShaderController?.SetCombatStress(0.0f);

            // Bị phản lực dội ngược lại về phía sau (-6 m/s)
            float recoilDir = -boss.FacingDirection;
            boss.Rigidbody2D.linearVelocity = new Vector2(recoilDir * 6.5f, 3.5f);

            // Hiệu ứng tia lửa phản đòn chói lóa
            boss.ShaderController?.TriggerHitFlash();
            boss.ShaderController?.TriggerScreenShake(0.22f, 0.25f);
            boss.AudioController?.PlayDualParriedSound();

            yield return new WaitForSeconds(0.35f);
            boss.StopHorizontalMovement();

            // Rơi vào trạng thái Choáng (Stun)
            boss.ShaderController?.TriggerSquashAndStretch(boss.DualCrossDashStunDuration, 0.35f);
            yield return new WaitForSeconds(boss.DualCrossDashStunDuration);

            boss.ChangeState(boss.ChaseState);
        }

        public override void LogicUpdate() { }

        public override void PhysicsUpdate() { }

        public override void Exit()
        {
            if (coopRoutine != null)
            {
                boss.StopCoroutine(coopRoutine);
            }
            boss.StopHorizontalMovement();
            boss.ResetSlimeBElements();
            boss.ShaderController?.SetCombatStress(0.0f);
            if (boss.Animator != null)
            {
                boss.Animator.ResetTrigger("AttackBTrig");
            }
        }
    }

    // ==========================================
    // SLIME C PINBALL POPCORN BOUNCE STATE
    // ==========================================
    public class RedSlimeCPinballState : RedSlimeBossState
    {
        private Coroutine pinballRoutine;
        private HashSet<int> hitTracker = new HashSet<int>();

        public RedSlimeCPinballState(RedSlimeBoss boss) : base(boss) { }

        public override void Enter()
        {
            boss.StopHorizontalMovement();
            boss.MarkPinballCUsed();
            hitTracker.Clear();

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isMoving", false);
            }
            if (boss.Target != null) boss.FacePosition(boss.Target.position);

            pinballRoutine = boss.StartCoroutine(PinballSequence());
        }

        private IEnumerator PinballSequence()
        {
            float targetDir = boss.FacingDirection;
            if (boss.Target != null)
            {
                targetDir = Mathf.Sign(boss.Target.position.x - boss.CenterPosition.x);
                boss.FaceDirection(targetDir);
            }

            // GIAI ĐOẠN 1: CO NÉN LÒ XO (Hyper Squash & Energy Charge - 0.22s)
            boss.ShaderController?.TriggerSquashAndStretch(0.22f, 0.45f);
            boss.ShaderController?.TriggerSlimeCChargeGathering(boss.CenterPosition, 0.22f);
            boss.AudioController?.PlayPinballChargeSound();

            yield return new WaitForSeconds(0.22f);

            int totalBounces = boss.PinballBounces;
            float bounceSpeed = boss.PinballSpeed;

            // GIAI ĐOẠN 2: CHUỖI BẬT NẢY ĐA ĐIỂM (Multi-Bounce Ricochet)
            for (int b = 0; b < totalBounces; b++)
            {
                if (boss.IsDead || boss.IsInvulnerable) yield break;

                // Tính toán hướng phóng của nhịp nảy
                Vector2 targetPos = (boss.Target != null) ? (Vector2)boss.Target.position : (Vector2)boss.CenterPosition + Vector2.right * targetDir * 4f;
                Vector2 startPos = boss.Rigidbody2D.position;
                Vector2 dirToTarget = (targetPos - startPos).normalized;

                // Lực đẩy parabol: Thành phần ngang + Lực bổng trục Y
                float horizontalVel = dirToTarget.x * bounceSpeed;
                float verticalVel = 7.5f + (b * 1.5f); // Nhịp sau nảy cao hơn nhịp trước

                boss.Rigidbody2D.linearVelocity = new Vector2(horizontalVel, verticalVel);
                boss.FaceDirection(Mathf.Sign(horizontalVel));

                // Sóng chấn động chân nảy
                boss.ShaderController?.SpawnSlimeCBounceImpact(boss.GroundPosition);
                boss.ShaderController?.TriggerScreenShake(0.12f, 0.12f);
                boss.AudioController?.PlayPinballBounceSound(boss.GroundPosition);

                float bounceDuration = 0.42f;
                float elapsed = 0f;
                float trailTimer = 0f;

                while (elapsed < bounceDuration)
                {
                    elapsed += Time.deltaTime;
                    trailTimer += Time.deltaTime;

                    if (trailTimer >= 0.04f)
                    {
                        trailTimer = 0f;
                        boss.ShaderController?.SpawnSlimeCPinballTrail(boss.CenterPosition, boss.transform.localScale, boss.FacingDirection);
                    }

                    // Kiểm tra va chạm gây sát thương trong lúc bay
                    Collider2D[] hits = Physics2D.OverlapCircleAll(boss.CenterPosition, boss.PinballRadius);
                    foreach (var hit in hits)
                    {
                        PlayerHealth playerHealth = hit.GetComponentInParent<PlayerHealth>();
                        if (playerHealth != null || hit.CompareTag("Player"))
                        {
                            GameObject rootGo = hit.transform.root.gameObject;
                            int id = rootGo.GetInstanceID();
                            if (!hitTracker.Contains(id))
                            {
                                IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
                                    hit,
                                    boss.PinballDamage,
                                    ParryableAttackType.Melee,
                                    EnemyParryResponder.Ensure(boss),
                                    boss.transform,
                                    (hit.bounds.center - boss.CenterPosition));

                                if (resolution != IncomingAttackResolution.Ignored)
                                {
                                    hitTracker.Add(id);

                                    boss.ShaderController?.SpawnSlimeCBounceImpact(hit.bounds.center);
                                    boss.ShaderController?.TriggerScreenShake(0.15f, 0.18f);
                                }
                            }
                        }
                    }

                    yield return null;
                }
            }

            // GIAI ĐOẠN 3: TIẾP ĐẤT & PHỤC HỒI ĐÀN HỒI (Landing Squash & Shake)
            boss.StopHorizontalMovement();
            boss.ShaderController?.SpawnSlimeCBounceImpact(boss.GroundPosition);
            boss.ShaderController?.TriggerSquashAndStretch(0.25f, 0.35f);
            boss.ShaderController?.TriggerScreenShake(0.16f, 0.16f);
            boss.AudioController?.PlayPinballLandSound(boss.GroundPosition);

            yield return new WaitForSeconds(0.2f);

            boss.ChangeState(boss.ChaseState);
        }

        public override void LogicUpdate() { }
        public override void PhysicsUpdate() { }

        public override void Exit()
        {
            if (pinballRoutine != null)
            {
                boss.StopCoroutine(pinballRoutine);
            }
            boss.StopHorizontalMovement();
        }
    }

    // ==========================================
    // SLIME C PRISMATIC CUBE LASER MATRIX STATE
    // ==========================================
    public class RedSlimeCLaserMatrixState : RedSlimeBossState
    {
        private Coroutine laserRoutine;
        private RedSlimeBoss partnerSlime;
        private HashSet<int> hitTracker = new HashSet<int>();

        public RedSlimeCLaserMatrixState(RedSlimeBoss boss) : base(boss) { }

        public void SetPartner(RedSlimeBoss partner)
        {
            partnerSlime = partner;
        }

        public override void Enter()
        {
            boss.StopHorizontalMovement();
            boss.MarkLaserCUsed();
            hitTracker.Clear();

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("isMoving", false);
            }
            if (boss.Target != null) boss.FacePosition(boss.Target.position);

            laserRoutine = boss.StartCoroutine(LaserMatrixSequence());
        }

        private IEnumerator LaserMatrixSequence()
        {
            // GIAI ĐOẠN 1: KHÓA VỊ TRÍ & QUÁ TẢI LÕI CUBE (Anchor Lock - 0.28s)
            boss.ShaderController?.SetCoreWarningOverdrive(1.0f);
            boss.ShaderController?.TriggerSlimeCChargeGathering(boss.CenterPosition, 0.28f);
            boss.ShaderController?.TriggerSquashAndStretch(0.28f, 0.3f);
            boss.AudioController?.PlayLaserChargeSound();

            yield return new WaitForSeconds(0.28f);

            // GIAI ĐOẠN 2: PHÁT TIA LASER MA TRẬN NỐI GIỮA 2 SLIME C (Laser Beam Sweeping)
            if (partnerSlime != null && !partnerSlime.IsDead && partnerSlime.gameObject.activeInHierarchy)
            {
                float duration = boss.LaserDuration;
                boss.ShaderController?.SpawnSlimeCLaserBeam(boss.transform, partnerSlime.transform, duration);
                boss.ShaderController?.TriggerScreenShake(duration * 0.5f, 0.10f);
                boss.AudioController?.PlayLaserLoopSound();

                float elapsed = 0f;
                float hitInterval = 0.2f;
                float lastHitCheck = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;

                    if (partnerSlime == null || partnerSlime.IsDead) break;

                    Vector2 p1 = (Vector2)boss.CenterPosition + Vector2.up * 0.25f;
                    Vector2 p2 = (Vector2)partnerSlime.CenterPosition + Vector2.up * 0.25f;

                    // Kiểm tra va chạm với Player theo đoạn thẳng Laser
                    if (Time.time - lastHitCheck >= hitInterval)
                    {
                        lastHitCheck = Time.time;
                        CheckLaserDamageAlongLine(p1, p2);
                    }

                    yield return null;
                }
            }

            // GIAI ĐOẠN 3: BỘC PHÁ NĂNG LƯỢNG & TẮT OVERDRIVE (Laser Burst & Reset)
            boss.ShaderController?.SetCoreWarningOverdrive(0.0f);
            boss.ShaderController?.SpawnSlimeCBounceImpact(boss.CenterPosition);
            boss.ShaderController?.TriggerSquashAndStretch(0.2f, 0.2f);
            boss.AudioController?.PlayLaserBurstSound(boss.CenterPosition);

            yield return new WaitForSeconds(0.15f);

            boss.ChangeState(boss.ChaseState);
        }

        private void CheckLaserDamageAlongLine(Vector2 p1, Vector2 p2)
        {
            if (boss.Target == null) return;

            Vector2 playerPos = boss.Target.position;
            float distToSegment = DistancePointToSegment(playerPos, p1, p2);

            if (distToSegment <= boss.LaserWidth + 0.5f)
            {
                Collider2D playerCol = boss.Target.GetComponent<Collider2D>();
                if (playerCol == null) playerCol = boss.Target.GetComponentInChildren<Collider2D>();

                if (playerCol != null)
                {
                    IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
                        playerCol,
                        boss.LaserDamage,
                        ParryableAttackType.Melee,
                        EnemyParryResponder.Ensure(boss),
                        boss.transform,
                        (playerPos - (Vector2)boss.CenterPosition));

                    if (resolution != IncomingAttackResolution.Ignored)
                    {
                        boss.ShaderController?.SpawnSlimeCBounceImpact(playerPos);
                        boss.ShaderController?.TriggerScreenShake(0.12f, 0.15f);
                    }
                }
            }
        }

        private float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            float l2 = (b - a).sqrMagnitude;
            if (l2 < 0.0001f) return Vector2.Distance(point, a);

            float t = Mathf.Clamp01(Vector2.Dot(point - a, b - a) / l2);
            Vector2 projection = a + t * (b - a);
            return Vector2.Distance(point, projection);
        }

        public override void LogicUpdate() { }
        public override void PhysicsUpdate() { }

        public override void Exit()
        {
            if (laserRoutine != null)
            {
                boss.StopCoroutine(laserRoutine);
            }
            boss.ShaderController?.SetCoreWarningOverdrive(0.0f);
            boss.StopHorizontalMovement();
            partnerSlime = null;
        }
    }
}



