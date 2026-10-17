using UnityEngine;
using System;
using System.Collections.Generic;
using Spine;

public sealed class PlayerCombat
{
    private readonly PlayerCombatDataSO _data;
    private readonly ComboTracker _combo;
    private readonly CooldownTracker _cooldowns;
    private readonly LayerMask _enemyLayer;
    private readonly Transform _body;
    private readonly Transform _meleePoint;
    private readonly PlayerMeleeHitbox _normalAttackHitbox;
    private readonly Player _owner;
    private readonly SkillExecutorRegistry _skillExecutorRegistry;
    private readonly PlayerHealth _health;

    private bool _isActionActive;
    private bool _isMovementLocked;
    private bool _isHovering;
    private float _actionTimeLeft;
    private bool _restoreInvincible;
    private int _pendingSkillIndex = -1;
    private SkillSetSO _pendingSkillSet;
    private SkillSetSO.SkillSlot _pendingSkillSlot;
    private bool _pendingSkillRelease;
    private bool _pendingSkillPaused;
    private float _pendingSkillStartedAt;
    private SkillData _activeSkillVisual;
    private bool _clearCombatVisualOnActionEnd = true;
    private float _skillExecutionLockTimeLeft;
    private readonly HashSet<DuyBossController> _notifiedBosses = new HashSet<DuyBossController>();

    // ─── State ───────────────────────────────────
    public bool IsActionActive => _isActionActive;
    public bool IsMovementLocked => _isMovementLocked;
    public bool IsHovering => _isHovering;
    public bool IsSkillExecutionActive => _skillExecutionLockTimeLeft > 0f || _pendingSkillSlot != null;

    public event Func<AnimationSpine.State, TrackEntry> OnPlayCombatAnim;
    public event Action<AudioClip> OnPlaySkillSound;
    public event Action OnClearCombatAnim;
    public event Action OnPauseCombatAnim;
    public event Action OnResumeCombatAnim;
    public event Action<string> OnShowSkillBow;
    public event Action OnHideSkillBow;
    public event Action<GameObject, Vector3, Quaternion> OnShowSkillProjectilePreview;
    public event Action OnHideSkillProjectilePreview;
    public event Action<SkillData> OnSkillVisualStarted;
    public event Action<SkillData> OnSkillVisualEnded;

    // ─── Constructor ─────────────────────────────────────────────────────────
    public PlayerCombat(
        PlayerCombatDataSO data, LayerMask enemyLayer,
        Transform body, Transform meleePoint, PlayerMeleeHitbox normalAttackHitbox, Player owner,
        SkillExecutorRegistry skillExecutorRegistry, PlayerHealth health)
    {
        _data = data;
        _enemyLayer = enemyLayer;
        _body = body;
        _meleePoint = meleePoint;
        _normalAttackHitbox = normalAttackHitbox;
        _owner = owner;
        _skillExecutorRegistry = skillExecutorRegistry;
        _health = health;
        _combo = new ComboTracker();
        _cooldowns = new CooldownTracker();
    }

    // ─── Tick ────────────────────────────────────────────────────────────────
    public void Tick(float dt)
    {
        _cooldowns.Tick(dt);
        if (_skillExecutionLockTimeLeft > 0f)
        {
            _skillExecutionLockTimeLeft = Mathf.Max(0f, _skillExecutionLockTimeLeft - dt);
            if (_skillExecutionLockTimeLeft <= 0f && !_isActionActive)
            {
                EndImmediateSkillVisual();
            }
        }

        if (_combo.ShouldReset(Time.time, _data.comboResetWindow, _isActionActive))
            _combo.Reset();

        if (_isActionActive)
        {
            if (_pendingSkillSlot != null)
            {
                _actionTimeLeft = Mathf.Max(0f, _actionTimeLeft - dt);
                UpdatePendingProjectilePreview(false);
                if (_actionTimeLeft <= 0f && !_pendingSkillPaused)
                {
                    _pendingSkillPaused = true;
                    OnPauseCombatAnim?.Invoke();
                }

                if (_pendingSkillRelease)
                {
                    ExecutePendingSkill();
                }

                return;
            }

            _actionTimeLeft -= dt;
            if (_actionTimeLeft <= 0f) EndAction();
        }
    }

    // ─── Normal attack ───────────────────────────────────────────────────────
    public bool CanNormalAttack()
    {
        float cancelWindow = _data != null ? _data.EffectiveNormalAttackCancelWindow : 0.18f;
        bool canComboCancel = _isActionActive && _actionTimeLeft <= cancelWindow;

        return !_health.IsDead && (!_isActionActive || canComboCancel) && !_health.IsBlocking && !_health.IsHurt;
    }

    public bool TryNormalAttack(SkillSetSO skillSet)
    {
        if (!CanNormalAttack()) return false;

        _combo.RegisterAttack(Time.time);

        AutoAim();
        NotifyBossesOfPlayerWindup(0.2f);

        if (!_combo.TryGetNextAnimation(
                _data.normalDamage,
                out AnimationSpine.State anim, out int damage)) return false;

        TrackEntry entry = OnPlayCombatAnim?.Invoke(anim);
        float fallbackDuration = _data != null ? _data.normalAttackDuration : 0.3f;
        float animDuration = PlayerAnimationController.GetAnimationDuration(entry, fallbackDuration);
        float attackCooldown = _data != null ? _data.EffectiveNormalAttackCooldown : animDuration;
        float hitboxActivationTime = _data != null ? _data.EffectiveNormalAttackHitboxActivationTime : 0.3f;
        float hitboxActiveDuration = _data != null ? _data.EffectiveNormalAttackHitboxActiveDuration : 0.1f;
        float animationSpeed = entry != null ? entry.TimeScale : 1f;
        float hitboxStartupDelay = animDuration * hitboxActivationTime;

        _owner?.ApplyNormalAttackMovement(anim);
        BeginAction(attackCooldown, lockMovement: false, invincible: false, freezeBody: false, clearCombatVisualOnEnd: false);
        if (_normalAttackHitbox != null)
        {
            _normalAttackHitbox.TriggerAttack(damage, hitboxStartupDelay, hitboxActiveDuration, animationSpeed);
        }
        else
        {
            DealDamageInArea(_data.attackRange, damage);
        }
        return true;
    }

    private void AutoAim()
    {
        if (_owner == null || _data == null || _body == null) return;

        // Quét bằng hình vuông (chữ nhật) dựa vào BoxSize
        Vector2 center = (Vector2)_body.position + _data.autoAimOffset;
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, _data.autoAimBoxSize, 0f, _enemyLayer);

        Transform closestTarget = null;
        float minDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                if (PlayerHitResolver.IsThreatSensorCollider(hit))
                {
                    continue;
                }

                // Kiểm tra xem collider này có thuộc về một Enemy hợp lệ không
                bool isValidEnemy = false;

            var enemyTarget = hit.GetComponentInParent<IEnemyTarget>();
            if (enemyTarget != null && enemyTarget.IsAlive) isValidEnemy = true;
            else
            {
                var baoEnemy = hit.GetComponentInParent<Bao.Enemies.BaoEnemyBase>();
                if (baoEnemy != null && !baoEnemy.IsDead) isValidEnemy = true;
                else
                {
                    // Các loại enemy/boss khác...
                    if (hit.GetComponentInParent<Bao.Enemies.IDamageable>() != null ||
                        hit.GetComponentInParent<EnemySystem.IDamageable>() != null ||
                        hit.GetComponentInParent<Quoc.QuyVuong.QuyVuongBossHealth>() != null ||
                        hit.GetComponentInParent<Bao.Bosses.DreadwormBoss>() != null ||
                        hit.GetComponentInParent<Bao.Bosses.NidusCocoonBoss>() != null ||
                        hit.GetComponentInParent<Bao.Bosses.RedSlimeBoss>() != null)
                    {
                        isValidEnemy = true;
                    }
                }
            }

            if (isValidEnemy)
            {
                float dist = Vector2.Distance(_body.position, hit.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestTarget = hit.transform;
                }
            }
        }

        if (closestTarget != null)
        {
            Debug.Log($"[AutoAim] Found closest target: {closestTarget.name}, aiming at it!");
            _owner.FacePosition(closestTarget.position);
        }
        else
        {
            Debug.Log($"[AutoAim] No valid enemy found in the red box.");
        }
    }

    public void CancelAction()
    {
        OnResumeCombatAnim?.Invoke();
        OnHideSkillBow?.Invoke();
        OnHideSkillProjectilePreview?.Invoke();
        EndSkillVisual();
        _isActionActive = false;
        _isMovementLocked = false;
        _actionTimeLeft = 0f;
        ClearPendingSkill();
        OnClearCombatAnim?.Invoke();
    }

    // ─── Skill ───────────────────────────────────────────────────────────────
    public bool TryCastSkill(int cooldownIndex, SkillSetSO skillSet, int slotIndex = 0)
    {
        if (_health.IsDead || skillSet == null) return false;
        if (!skillSet.TryGetSkillSlot(slotIndex, out SkillSetSO.SkillSlot slot)) return false;
        SkillData skill = slot.SkillData;
        if (skill == null) return false;
        if (!CanUseUnlockedElement(skill)) return false;
        if (_skillExecutorRegistry == null) return false;
        if (_isActionActive || IsSkillExecutionActive || _health.IsBlocking || _health.IsHurt) return false;
        if (!_cooldowns.IsReady(cooldownIndex)) return false;

        _combo.Reset();
        _combo.RegisterAttack(Time.time);
        AutoAim();
        NotifyBossesOfPlayerWindup(Mathf.Max(0.1f, skill != null ? skill.PullPoseTime : 0.1f));
        BeginSkillCastVisual(skill);

        if (ShouldExecuteImmediatelyOnPress(slot))
        {
            bool executed = ExecuteSkillNow(cooldownIndex, skillSet, slot, 0f);
            if (!executed)
            {
                EndImmediateSkillVisual();
            }

            return executed;
        }

        BeginAction(skill.PullPoseTime, lockMovement: true, invincible: false, freezeBody: false);
        _pendingSkillIndex = cooldownIndex;
        _pendingSkillSet = skillSet;
        _pendingSkillSlot = slot;
        _pendingSkillRelease = false;
        _pendingSkillStartedAt = Time.time;

        if (slot.ExecutionConfig is ProjectileSkillConfig projectileConfig)
        {
            UpdatePendingProjectilePreview(true);
        }

        if (skill.AnimationName != AnimationSpine.State.None)
        {
            OnPlayCombatAnim?.Invoke(skill.AnimationName);
        }

        return true;
    }

    private bool CanUseUnlockedElement(SkillData skill)
    {
        if (skill == null || _owner == null)
        {
            return false;
        }

        foreach (var fairy in FairyPlayerCompanionController.AllActiveFairies)
        {
            if (fairy != null && fairy.ActivePlayer == _owner && FairyElementUnlockController.MapFairyElement(fairy.CurrentElement) == skill.ElementType)
            {
                return true;
            }
        }

        return false;
    }

    public bool ReleaseSkill(int index)
    {
        if (_pendingSkillSlot == null || _pendingSkillIndex != index)
        {
            return false;
        }

        _pendingSkillRelease = true;
        return true;
    }

    private void ExecutePendingSkill()
    {
        SkillSetSO.SkillSlot slot = _pendingSkillSlot;
        SkillSetSO skillSet = _pendingSkillSet;
        int index = _pendingSkillIndex;
        if (slot == null || slot.SkillData == null)
        {
            EndAction();
            return;
        }

        float holdDuration = Mathf.Min(
            Mathf.Max(0f, Time.time - _pendingSkillStartedAt),
            ProjectileSkillConfig.MaxChargeTime);

        if (slot.ExecutionConfig is ProjectileSkillConfig projectileConfig && !projectileConfig.CanShowOrFire(holdDuration))
        {
            EndAction();
            return;
        }

        ExecuteSkillNow(index, skillSet, slot, holdDuration);

        EndAction();
    }

    private bool ExecuteSkillNow(int index, SkillSetSO skillSet, SkillSetSO.SkillSlot slot, float holdDuration)
    {
        _cooldowns.Start(index, slot.SkillData.Cooldown);
        OnResumeCombatAnim?.Invoke();
        OnPlaySkillSound?.Invoke(skillSet != null ? skillSet.GetSkillSound(index) : null);

        bool executed = _skillExecutorRegistry.TryExecute(slot, holdDuration);
        if (!executed)
        {
            _cooldowns.Start(index, 0.2f);
            return false;
        }

        BeginSkillExecutionLock(slot, holdDuration);
        return true;
    }

    private static bool ShouldExecuteImmediatelyOnPress(SkillSetSO.SkillSlot slot)
    {
        if (slot == null)
        {
            return false;
        }

        if (slot.ExecutionConfig is FairyBeamSkillConfig)
        {
            return true;
        }

        if (slot.ExecutionConfig is WindStaffBeamSkillConfig)
        {
            return true;
        }

        if (slot.ExecutionConfig is WindTornadoSkillConfig)
        {
            return true;
        }

        if (slot.ExecutionConfig is EarthSpraySkillConfig)
        {
            return true;
        }

        if (slot.ExecutionConfig is EarthPillarBlastSkillConfig)
        {
            return true;
        }

        if (slot.ExecutionConfig is EarthShieldSkillConfig)
        {
            return true;
        }

        if (slot.ExecutionConfig is FireSpearDashSkillConfig)
        {
            return true;
        }

        if (slot.ExecutionConfig is FireVfxFallExplosionSkillConfig)
        {
            return true;
        }

        if (slot.ExecutionConfig is FireSpearBarrageSkillConfig)
        {
            return true;
        }

        return slot.ExecutionConfig is AreaSkillConfig areaConfig
            && areaConfig.DeliveryMode == AreaSkillDeliveryMode.JumpApexSpreadShot;
    }

    private void NotifyBossesOfPlayerWindup(float estimatedTimeToImpact)
    {
        if (_body == null)
        {
            return;
        }

        Vector2 direction = _owner != null && _owner.FacingDirectionSign < 0f ? Vector2.left : Vector2.right;
        float notifyRadius = Mathf.Max(_data != null ? _data.attackRange + 6f : 8f, 8f);
        Collider2D[] hits = Physics2D.OverlapCircleAll(_body.position, notifyRadius, _enemyLayer);
        _notifiedBosses.Clear();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            if (PlayerHitResolver.IsThreatSensorCollider(hit))
            {
                continue;
            }

            DuyBossController boss = hit.GetComponentInParent<DuyBossController>();
            if (boss == null || !boss.IsAlive || !_notifiedBosses.Add(boss))
            {
                continue;
            }

            boss.NotifyIncomingAttack(
                _body.position,
                direction,
                DuyBossController.IncomingThreatType.Melee,
                estimatedTimeToImpact);
        }
    }

    private void BeginSkillExecutionLock(SkillSetSO.SkillSlot slot, float holdDuration)
    {
        _skillExecutionLockTimeLeft = Mathf.Max(
            _skillExecutionLockTimeLeft,
            ResolveSkillExecutionLockDuration(slot, holdDuration));
    }

    private static float ResolveSkillExecutionLockDuration(SkillSetSO.SkillSlot slot, float holdDuration)
    {
        if (slot == null || slot.SkillData == null)
        {
            return 0.1f;
        }

        SkillExecutionConfig config = slot.ExecutionConfig;
        if (config is ProjectileSkillConfig projectileConfig)
        {
            return Mathf.Max(0.1f, projectileConfig.Lifetime);
        }

        if (config is AreaSkillConfig areaConfig)
        {
            switch (areaConfig.DeliveryMode)
            {
                case AreaSkillDeliveryMode.JumpApexSpreadShot:
                    return Mathf.Max(0.1f, areaConfig.MaxApexWait + slot.SkillData.PullPoseTime + 0.1f);

                case AreaSkillDeliveryMode.ArcProjectile:
                    return Mathf.Max(0.1f, areaConfig.ProjectileLifetime);

                default:
                    return Mathf.Max(0.1f, areaConfig.Duration);
            }
        }

        if (config is FairyBeamSkillConfig fairyBeamConfig)
        {
            return Mathf.Max(
                0.1f,
                Mathf.Min(
                    Mathf.Max(fairyBeamConfig.CastDelay + 0.05f, fairyBeamConfig.AnimationDurationFallback),
                    fairyBeamConfig.Duration > 0f ? fairyBeamConfig.CastDelay + fairyBeamConfig.Duration : float.PositiveInfinity));
        }

        if (config is WindStaffBeamSkillConfig windBeamConfig)
        {
            return Mathf.Max(
                0.1f,
                Mathf.Min(
                    Mathf.Max(windBeamConfig.CastDelay + 0.05f, windBeamConfig.AnimationDurationFallback),
                    windBeamConfig.Duration > 0f ? windBeamConfig.CastDelay + windBeamConfig.Duration : float.PositiveInfinity));
        }

        if (config is WindTornadoSkillConfig windTornadoConfig)
        {
            float travelTime = windTornadoConfig.TargetSearchRadius / Mathf.Max(0.1f, windTornadoConfig.TravelSpeed) + 0.5f;
            return Mathf.Max(0.1f, windTornadoConfig.Duration + travelTime);
        }

        if (config is EarthSpraySkillConfig earthSprayConfig)
        {
            return Mathf.Max(
                0.1f,
                earthSprayConfig.CastDelay + Mathf.Max(0.05f, earthSprayConfig.Duration));
        }

        if (config is EarthPillarBlastSkillConfig earthPillarConfig)
        {
            return Mathf.Max(
                0.1f,
                earthPillarConfig.CastDelay + earthPillarConfig.DamageDelay + 0.05f);
        }

        if (config is EarthShieldSkillConfig earthShieldConfig)
        {
            return Mathf.Max(0.1f, earthShieldConfig.AnimationDuration);
        }

        if (config is FireSpearDashSkillConfig dashConfig)
        {
            float dashDuration = Mathf.Max(dashConfig.Duration, dashConfig.NearDashDuration, dashConfig.FarDashDuration);
            return Mathf.Max(0.1f, dashConfig.Startup + dashDuration + dashConfig.Recovery);
        }

        if (config is FireVfxFallExplosionSkillConfig fallConfig)
        {
            return Mathf.Max(0.1f, fallConfig.ActiveDuration + fallConfig.DropDuration + fallConfig.ExplosionDelay);
        }

        if (config is FireSpearBarrageSkillConfig barrageConfig)
        {
            return Mathf.Max(0.1f, barrageConfig.Duration);
        }

        return Mathf.Max(0.1f, slot.SkillData.PullPoseTime);
    }

    // ─── Skill cooldown reset (called by HitboxDamage on kill) ───────────────
    public void ResetSkillCooldown(SkillAttackType attackType, SkillSetSO skillSet)
    {
        if (skillSet == null) return;
        for (int i = 0; i < skillSet.SkillCount; i++)
            if (skillSet.TryGetSkill(i, out SkillData s) && s.AttackType == attackType)
            { _cooldowns.Reset(i); return; }
    }

    public void ResetAllSkillCooldowns()
    {
        _cooldowns.ResetAll();
    }

    public float GetSkillCooldownTimer(int index)
    {
        return _cooldowns[index];
    }

    public float GetSkillMaxCooldown(int index, SkillSetSO skillSet)
    {
        if (skillSet != null && skillSet.TryGetSkill(index, out SkillData skill))
        {
            return skill.Cooldown;
        }

        return 1f;
    }

    // ─── Action lifecycle ────────────────────────────────────────────────────
    private void BeginAction(float duration, bool lockMovement, bool invincible, bool freezeBody, bool clearCombatVisualOnEnd = true)
    {
        _isActionActive = true;
        _actionTimeLeft = duration;
        _isMovementLocked = lockMovement;
        _restoreInvincible = invincible;
        _clearCombatVisualOnActionEnd = clearCombatVisualOnEnd;
        _health.SetInvincible(invincible);

        if (freezeBody) OnFreezeBody?.Invoke();
    }

    public void EndAction()
    {
        OnResumeCombatAnim?.Invoke();
        bool clearCombatVisual = _clearCombatVisualOnActionEnd;
        if (clearCombatVisual)
        {
            OnHideSkillBow?.Invoke();
            OnHideSkillProjectilePreview?.Invoke();
            EndSkillVisual();
        }

        _isActionActive = false;
        _isMovementLocked = false;
        _isHovering = false;
        _actionTimeLeft = 0f;
        ClearPendingSkill();
        _clearCombatVisualOnActionEnd = true;

        if (_restoreInvincible)
        {
            _health.SetInvincible(false);
            _restoreInvincible = false;
        }

        OnRestorePhysics?.Invoke();
        if (clearCombatVisual)
        {
            OnClearCombatAnim?.Invoke();
        }

    }

    private void BeginSkillVisual(SkillData skill)
    {
        if (skill == null || _activeSkillVisual == skill)
        {
            return;
        }

        EndSkillVisual();
        _activeSkillVisual = skill;
        OnSkillVisualStarted?.Invoke(skill);
    }

    private void BeginSkillCastVisual(SkillData skill)
    {
        BeginSkillVisual(skill);
        if (skill != null && !string.IsNullOrWhiteSpace(skill.BowSkinName))
        {
            OnShowSkillBow?.Invoke(skill.BowSkinName);
        }
    }

    private void EndImmediateSkillVisual()
    {
        OnHideSkillBow?.Invoke();
        EndSkillVisual();
        OnClearCombatAnim?.Invoke();
    }

    private void EndSkillVisual()
    {
        if (_activeSkillVisual == null)
        {
            return;
        }

        SkillData endedSkill = _activeSkillVisual;
        _activeSkillVisual = null;
        OnSkillVisualEnded?.Invoke(endedSkill);
    }

    private void ClearPendingSkill()
    {
        _pendingSkillIndex = -1;
        _pendingSkillSet = null;
        _pendingSkillSlot = null;
        _pendingSkillRelease = false;
        _pendingSkillPaused = false;
        _pendingSkillStartedAt = 0f;
    }

    private void UpdatePendingProjectilePreview(bool force)
    {
        if (_pendingSkillSlot == null ||
            _pendingSkillSlot.SkillData == null ||
            !(_pendingSkillSlot.ExecutionConfig is ProjectileSkillConfig projectileConfig))
        {
            return;
        }

        float holdDuration = Mathf.Min(
            Mathf.Max(0f, Time.time - _pendingSkillStartedAt),
            ProjectileSkillConfig.MaxChargeTime);

        if (!projectileConfig.CanShowOrFire(holdDuration))
        {
            OnHideSkillProjectilePreview?.Invoke();
            return;
        }

        int chargeLevel = projectileConfig.ResolveChargeLevel(holdDuration);
        GameObject previewPrefab = projectileConfig.ResolveProjectilePrefab(holdDuration);
        if (previewPrefab == null)
        {
            OnHideSkillProjectilePreview?.Invoke();
            return;
        }

        Vector2 direction = _owner != null && _owner.FacingDirectionSign < 0f ? Vector2.left : Vector2.right;
        Vector3 origin = (_meleePoint != null ? _meleePoint.position : _body.position) +
                         ResolveProjectileSpawnOffset(projectileConfig, direction, holdDuration);
        direction = ResolveProjectileAimDirection(projectileConfig, origin, direction);
        Quaternion rotation = ResolveProjectilePreviewRotation(
            _pendingSkillSlot.SkillData,
            previewPrefab,
            direction);

        OnShowSkillProjectilePreview?.Invoke(previewPrefab, origin, rotation);
    }

    private Vector2 ResolveProjectileAimDirection(ProjectileSkillConfig config, Vector3 origin, Vector2 fallbackDirection)
    {
        Collider2D target = ResolveProjectileAimTarget(config, origin, fallbackDirection);
        if (target == null)
        {
            return fallbackDirection;
        }

        Vector2 aimDirection = ((Vector2)target.bounds.center - (Vector2)origin).normalized;
        return aimDirection.sqrMagnitude > 0.0001f ? aimDirection : fallbackDirection;
    }

    private Collider2D ResolveProjectileAimTarget(ProjectileSkillConfig config, Vector3 origin, Vector2 facingDirection)
    {
        if (config == null || !config.AutoAim || config.AutoAimRadius <= 0f)
        {
            return null;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, config.AutoAimRadius, _enemyLayer);
        Collider2D closest = null;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || (_owner != null && hit.transform.root == _owner.transform.root) || !FairyBeamSkillExecutor.IsValidEnemyTarget(hit))
            {
                continue;
            }

            Vector2 toTarget = (Vector2)hit.bounds.center - (Vector2)origin;
            if (config.AutoAimOnlyFacingDirection && toTarget.x * facingDirection.x < 0f)
            {
                continue;
            }

            float distance = toTarget.sqrMagnitude;
            if (distance >= closestDistance)
            {
                continue;
            }

            closestDistance = distance;
            closest = hit;
        }

        return closest;
    }

    private Vector3 ResolveProjectileSpawnOffset(ProjectileSkillConfig config, Vector2 direction, float holdDuration)
    {
        Vector2 offset = config.ResolveChargedSpawnOffset(holdDuration);
        switch (config.SpawnOffsetSpace)
        {
            case ProjectileSpawnOffsetSpace.PlayerLocal:
                return _body != null ? _body.TransformVector(offset) : (Vector3)offset;

            case ProjectileSpawnOffsetSpace.World:
                return ScaleProjectileOffset(offset);

            default:
                Vector3 scaledOffset = ScaleProjectileOffset(offset);
                return new Vector3(scaledOffset.x * direction.x, scaledOffset.y, 0f);
        }
    }

    private Vector3 ScaleProjectileOffset(Vector3 offset)
    {
        return _owner != null && _owner.ScaleProvider != null
            ? _owner.ScaleProvider.ScaleOffset(offset)
            : offset;
    }

    private static Quaternion ResolveProjectilePreviewRotation(SkillData skill, GameObject previewPrefab, Vector2 direction)
    {
        Quaternion rotation = previewPrefab != null ? previewPrefab.transform.rotation : Quaternion.identity;
        if (skill == null)
        {
            return rotation;
        }

        if (skill.SpawnRotationMode == SkillSpawnRotationMode.FaceDirection)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            return Quaternion.Euler(0f, 0f, angle + skill.SpawnRotationOffset);
        }

        if (!Mathf.Approximately(skill.SpawnRotationOffset, 0f))
        {
            rotation *= Quaternion.Euler(0f, 0f, skill.SpawnRotationOffset);
        }

        return rotation;
    }

    public void SetHovering(bool value) => _isHovering = value;

    public event Action OnFreezeBody;
    public event Action OnRestorePhysics;

    // ─── Damage helpers ──────────────────────────────────────────────────────
    private void DealDamageInArea(float range, float damage)
    {
        Vector3 origin = _meleePoint != null ? _meleePoint.position : _body.position;
        foreach (Collider2D col in Physics2D.OverlapCircleAll(origin, range, _enemyLayer))
            if (col.TryGetComponent(out IEnemyTarget t) && t.IsAlive)
                EnemyDamageUtility.ApplyDamage(t, damage, _owner != null ? _owner.transform : _body);
    }
}
