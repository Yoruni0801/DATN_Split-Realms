using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Spine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AnimationSpine))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerAudio))]
[RequireComponent(typeof(PlayerSwimController))]
[RequireComponent(typeof(PlayerSceneScaleController))]
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(PlayerRotaSurfaceController))]
public class Player : MonoBehaviour, IPlayerInputLock
{
    private const string MapHangSceneName = "MapHang";
    private const float DefaultAttackBufferTime = 0.45f;
    private const float WorldSwapSearchInterval = 0.5f;

    [Header("Data")]
    [SerializeField] private PlayerMovementDataSO movementData;
    [SerializeField] private PlayerCombatDataSO combatData;

    [Header("Elements")]
    [SerializeField] private List<SkillSetSO> elementSkillSets = new List<SkillSetSO>();
    [SerializeField] private int startingElementIndex;

    [Header("References")]
    [SerializeField] private PlayerMeleeHitbox normalAttackHitbox;
    [SerializeField] private PlayerAttackVfxController attackVfxController;

    [Header("World Swap Jump Boost")]
    [SerializeField] private bool enableWorldSwapJumpBoost = false;
    [SerializeField] private float worldSwapJumpBonus = 5f;
    [SerializeField] private float worldSwapBoostedGravityScale = 0.55f;
    [SerializeField] private bool worldSwapBoostedInWorldA;

    private Rigidbody2D rb;
    private AnimationSpine animationSpine;
    private PetrifyController petrifyController;
    private Collider2D bodyCollider;
    private PlayerHealth health;
    private PlayerAudio audioController;
    private PlayerSwimController swimController;
    private SimpleSpineSkinAssigner skinAssigner;
    private PlayerSceneScaleController sceneScaleController;
    private PlayerSceneJumpController sceneJumpController;
    private PlayerRotaSurfaceController rotaController;

    private PlayerMover mover;
    private PlayerAnimationController animationController;
    private PlayerVisualController visualController;
    private PlayerCombat combat;
    private PlayerElementController elementController;
    private PlayerInputController inputController;
    private PlayerSkillVisualController skillVisual;
    private PlayerSceneStateHandler sceneStateHandler;

    private PlayerParryController parryController;
    private PlayerDashHandler dashHandler;
    private SkillExecutorRegistry skillExecutorRegistry;
    private AreaSkillApexSpreadShotRunner areaSkillRunner;

    private FairyPlayerCompanionController fairyCompanion;

    private Vector2 moveInput;
    private bool jumpHeld;
    private bool controlLocked;
    private bool ropeGrabAnimationActive;
    private float attackBufferTimer;
    private WorldSwapManager worldSwapManager;
    private float baseJumpForce;
    private float originalGravityScale;
    private float nextWorldSwapSearchTime;
    private bool originalGravityScaleCached;
    private bool worldSwapBoostActive;
    private bool worldSwapListenerBound;
    private bool jumpForceOverrideActive;
    private float jumpForceOverride;
    
    private bool isRotaModeActive;
    private bool rotaJumpQueued;

    private bool inputLocked;
    private RigidbodyConstraints2D constraintsBeforeInputLock;
    private bool inputLockConstraintsCached;

    public void SetInputLocked(bool locked)
    {
        inputLocked = locked;

        if (locked)
        {
            moveInput = Vector2.zero;
            jumpHeld = false;
            if (rb != null)
            {
                if (!inputLockConstraintsCached)
                {
                    constraintsBeforeInputLock = rb.constraints;
                    inputLockConstraintsCached = true;
                }

                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.constraints |= RigidbodyConstraints2D.FreezePositionX
                                  | RigidbodyConstraints2D.FreezePositionY
                                  | RigidbodyConstraints2D.FreezeRotation;
            }

            animationSpine?.SetTimeScale(0f);
        }
        else
        {
            if (rb != null && inputLockConstraintsCached)
            {
                rb.constraints = constraintsBeforeInputLock;
            }

            inputLockConstraintsCached = false;
            animationSpine?.SetTimeScale(1f);
        }
    }

    public SkillSetSO CurrentSkillSet => elementController?.CurrentSkillSet;

    public SkillSetSO currentState => CurrentSkillSet;
    public int CurrentElementIndex => elementController?.CurrentIndex ?? 0;
    public int ElementCount => elementController?.ElementCount ?? 0;
    public float FacingDirectionSign => GetFacingDirectionSign();
    public bool IsControlLocked => controlLocked;
    public PlayerSceneScaleController SceneScaleController => sceneScaleController;
    public ICharacterScaleProvider ScaleProvider => sceneScaleController;
    public SkillData ActiveVisualSkill => skillVisual?.ActiveVisualSkill;
    public bool IsRotaModeActive => isRotaModeActive;
    public bool IsGravityInverted => mover != null ? mover.IsGravityInverted : (transform.localScale.y < 0f);
    public PlayerMover Mover => mover;

    public SkillSetSO GetSkillSet(int elementIndex)
    {
        if (elementSkillSets != null && elementIndex >= 0 && elementIndex < elementSkillSets.Count)
        {
            return elementSkillSets[elementIndex];
        }
        return null;
    }

    public event Action<SkillSetSO, int, int> OnElementChanged;
    public event Action<SkillData> OnSkillVisualStarted;
    public event Action<SkillData> OnSkillVisualEnded;
    public event Action OnGravityToggleRequested;


    private void Awake()
    {
        ResolveUnityComponents();
        parryController = PlayerParryController.Ensure(this);
        dashHandler = gameObject.AddComponent<PlayerDashHandler>();
        dashHandler.Configure(health, bodyCollider);
        areaSkillRunner = GetComponent<AreaSkillApexSpreadShotRunner>();

        sceneStateHandler = new PlayerSceneStateHandler(transform, rb);
        BuildSystems();
        BuildElementController();
        BuildInputController();
        sceneStateHandler.RestorePosition();
    }

    private void OnEnable()
    {
        PlayerRegistry.Register(this);
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        if (sceneJumpController != null)
        {
            sceneJumpController.JumpForceChanged -= HandleSceneJumpForceChanged;
            sceneJumpController.JumpForceChanged += HandleSceneJumpForceChanged;
        }
        RefreshInputState();
        RefreshJumpForceForActiveScene();
        ApplyCurrentWorldSwapState();
    }

    private void Start()
    {
        ApplyCurrentWorldSwapState();
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        if (sceneJumpController != null)
        {
            sceneJumpController.JumpForceChanged -= HandleSceneJumpForceChanged;
        }
        RestoreWorldSwapBoost();
        UnbindWorldSwapJumpBoost();
        PlayerRegistry.Unregister(this);
        skillVisual?.Cleanup();
        sceneStateHandler?.SavePosition();
        inputController?.Disable();
        swimController?.ClearWaterZones();
        mover?.SetSwimming(false);
        if (skinAssigner != null)
        {
            skinAssigner.SetWeaponsHiddenForSwimming(false);
        }
        
        if (isRotaModeActive && !Tien.Minigame.MinigameCheckpointState.IsInMinigame)
        {
            SetRotaMode(false);
        }
    }

    private void OnDestroy()
    {
        RestoreWorldSwapBoost();
        UnbindWorldSwapJumpBoost();
        PlayerRegistry.Unregister(this);
        skillVisual?.Cleanup();
        sceneStateHandler?.SavePosition();
        UnbindHealthAnimationEvents();
        inputController?.Dispose();
    }

    private void HandleActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        if (!newScene.IsValid() || !newScene.isLoaded)
        {
            return;
        }

        // Each scene owns its own placed player; only the player belonging to the
        // newly activated scene may restore its position.
        Scene ownScene = gameObject.scene;
        if (ownScene != newScene)
        {
            return;
        }

        sceneStateHandler?.RestorePosition(newScene.name);
    }

    private void Update()
    {

        if (inputLocked)
            return;

        if (petrifyController != null && petrifyController.IsPetrifying)
        {
            animationSpine.SetTimeScale(0f);
            return;
        }

        if (health.IsDead)
        {
            animationSpine.SetTimeScale(1f);
            return;
        }

        animationSpine.SetTimeScale(1f);

        if (!controlLocked)
        {
            inputController?.TickKeyboardFallback(elementSkillSets);
            moveInput = inputController?.ReadMoveInput() ?? moveInput;
            jumpHeld = inputController?.ReadJumpHeld() ?? jumpHeld;
        }

        TickAttackBuffer();
        mover.Tick(Time.deltaTime);
        combat.Tick(Time.deltaTime);
        TickParry();

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Backslash) && IsMapHangActive())
        {
            SetRotaMode(!isRotaModeActive);
        }
#endif

        if (!mover.IsDashing)
        {
            FlipCharacter();
            if (!isRotaModeActive || rotaController == null)
            {
                RefreshMovementAnimation(combat.IsActionActive || IsParryBusy() || IsParryVisualBusy());
            }

            TickFootstepAudio();
        }

        if (mover.IsDashing && mover.TickGhost(Time.deltaTime))
            SpawnDashGhost();
    }

    private void FixedUpdate()
    {

        if (inputLocked)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            return;
        }

        if (health.IsDead)
            return;

        if (!controlLocked)
        {
            moveInput = inputController?.ReadMoveInput() ?? moveInput;
            jumpHeld = inputController?.ReadJumpHeld() ?? jumpHeld;
        }

        // ROTA owns the Rigidbody2D exclusively while active. Do not run swim,
        // normal mover, or world-swap gravity in the same physics frame.
        if (isRotaModeActive && rotaController != null)
        {
            if (!RotaSurfaceZone.IsPlayerInAnyZone(this))
            {
                SetRotaMode(false);
                return;
            }

            if (controlLocked)
            {
                rotaJumpQueued = false;
                return;
            }

            rotaController.FixedTick(moveInput, rotaJumpQueued, jumpHeld);
            rotaJumpQueued = false;
            return;
        }

        Vector2 filteredInput = swimController.FilterAcidSwimInput(moveInput);
        PlayerMovementState swimState = swimController.FixedTick(filteredInput, GetFacingDirectionSign());
        if (skinAssigner != null)
        {
            skinAssigner.SetWeaponsHiddenForSwimming(swimController.IsSwimming);
        }

        if (IsWaterOnlyMovementState(swimState))
            CancelNonSwimActions();

        mover.FixedTick(
            filteredInput,
            combat.IsMovementLocked || (parryController != null && parryController.IsMovementLocked),
            health.IsHurt,
            !controlLocked && health.IsBlocking,
            combat.IsHovering,
            swimState,
            jumpHeld,
            health.IsSlowed ? 0.5f : 1f,
            false,
            swimController != null ? swimController.SwimUpSpeed : 0f);

        TickWorldSwapJumpBoost();
    }

    public void PlayHurtAnimation()
    {
        if (swimController != null && swimController.IsInAcid && swimController.IsSwimming)
        {
            return;
        }

        visualController?.PlayHurt();
    }

    public void PlayDeadAnimation()
    {
        CancelNonSwimActions();
        visualController?.PlayDeath();
    }

    private void CleanupDeathState()
    {
        moveInput = Vector2.zero;
        jumpHeld = false;
        attackBufferTimer = 0f;

        CancelNonSwimActions();
        parryController?.ReleaseParry();
        health?.SetBlockSuppressed(true);

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private void HandleRespawned()
    {
        PlayIdleAnimation();
        RefreshInputState();
    }

    public void PlayIdleAnimation() => visualController?.PlayIdle();

    public void ChangeElement(SkillSetSO newSkillSet)
    {
        SkillSetSO prev = CurrentSkillSet;
        elementController?.ChangeElement(newSkillSet);
        if (prev != CurrentSkillSet)
        {
            ResetAllSkillCooldowns();
        }
    }

    public bool EnsureCurrentElementIsUnlocked()
    {
        if (elementController == null || !elementController.EnsureCurrentElementIsUnlocked())
        {
            return false;
        }

        ResetAllSkillCooldowns();
        return true;
    }

    public bool TryChangeElement(SkillElementType elementType)
    {
        if (elementController == null || !elementController.TryChangeElement(elementType))
        {
            return false;
        }

        ResetAllSkillCooldowns();
        return true;
    }

    public void ResetAllSkillCooldowns()
    {
        combat?.ResetAllSkillCooldowns();
    }

    public void ResetSkillCooldown(SkillAttackType attackType)
        => combat.ResetSkillCooldown(attackType, CurrentSkillSet);

    public float GetSkillCooldownTimer(int index) => combat?.GetSkillCooldownTimer(index) ?? 0f;
    public float GetSkillMaxCooldown(int index) => combat?.GetSkillMaxCooldown(index, CurrentSkillSet) ?? 1f;

    public void ApplySkillHit(SkillHitData hitData, IEnemyTarget enemy, Vector3 hitPosition)
    {
        if (hitData.Skill == null || enemy == null || !enemy.IsAlive)
            return;

        EnemyDamageUtility.ApplyDamage(enemy, hitData.Damage, transform);
        ApplySkillForces(hitData, enemy, hitPosition);
    }

    public void ShowSkillPresentation(
        SkillData skill,
        Action<Collider2D> onSkillColliderHit = null,
        bool triggerSkillCollider = true)
    {
        if (skill == null)
            return;

        skillVisual.BeginSkillVisual(skill);

        if (triggerSkillCollider)
            normalAttackHitbox?.TriggerSkillCollider(skill, onSkillColliderHit);

        skillVisual.ShowBowSkin(skill.BowSkinName);

        if (skill.AnimationName != AnimationSpine.State.None)
            animationController.PlayCombat(skill.AnimationName);
    }

    public void TriggerSkillCollider(SkillData skill, Action<Collider2D> onSkillColliderHit = null)
        => normalAttackHitbox?.TriggerSkillCollider(skill, onSkillColliderHit);

    public void HideSkillPresentation()
    {
        skillVisual.EndActiveSkillVisual();
        skillVisual.HideBowSkin();
        animationController.ClearCombat();
    }

    public void CancelActiveSkillForTimeline()
    {
        if (combat != null && (combat.IsActionActive || combat.IsSkillExecutionActive || ActiveVisualSkill != null))
        {
            combat.CancelAction();
        }

        if (parryController != null && parryController.IsBusy)
        {
            parryController.ReleaseParry();
        }
    }

    public void SetGravityInverted(bool inverted)
    {
        mover.SetGravityInverted(inverted);
        mover.SetSwimming(swimController.IsSwimming);
        if (sceneScaleController != null)
        {
            sceneScaleController.SetGravityYSign(inverted);
        }
        else
        {
            Vector3 scale = transform.localScale;
            scale.y = inverted ? -Mathf.Abs(scale.y) : Mathf.Abs(scale.y);
            transform.localScale = scale;
        }
    }

    public void SetJumpForceOverride(float jumpForce)
    {
        jumpForceOverrideActive = true;
        jumpForceOverride = Mathf.Max(0f, jumpForce);
        mover?.SetJumpForce(jumpForceOverride);
    }

    public void ClearJumpForceOverride()
    {
        jumpForceOverrideActive = false;
        mover?.ResetJumpForce(baseJumpForce);
    }

    public void SetInputActive(bool active) => SetControlLocked(!active);

    public void SetControlLocked(bool locked)
    {
        controlLocked = locked;

        if (locked)
        {
            moveInput = Vector2.zero;
            jumpHeld = false;
            parryController?.ReleaseParry();
        }

        health?.SetBlockSuppressed(locked);
        RefreshInputState();
    }

    public void SetCutsceneMoveInput(Vector2 input)
    {
        if (controlLocked)
            moveInput = Vector2.ClampMagnitude(input, 1f);
    }

    public void StopCutsceneMoveInput()
    {
        if (controlLocked)
            moveInput = Vector2.zero;
    }

    public void SaveCheckpointPosition(Vector3 position)
    {
        sceneStateHandler?.SavePosition(position);
    }

    public void ApplyExternalLaunch(Vector2 velocity, float horizontalHoldTime)
    {
        // A dead body is frozen on purpose; launches and knockback must not shove it around.
        if (health != null && health.IsDead)
            return;

        mover?.ApplyExternalLaunch(velocity, horizontalHoldTime);
        if (mover == null)
            rb.linearVelocity = velocity;
    }

    public void PlayRopeGrabAnimation()
    {
        ropeGrabAnimationActive = true;
        animationController?.PlayRopeGrab();
    }

    public void ClearRopeGrabAnimation()
    {
        if (!ropeGrabAnimationActive)
            return;

        ropeGrabAnimationActive = false;
        animationController?.ForceRefreshMovement();
    }

    public void FacePosition(Vector3 worldPos)
        => SetFacingDirectionSign(worldPos.x >= transform.position.x ? 1f : -1f);

    private void ResolveUnityComponents()
    {
        rb = GetComponent<Rigidbody2D>();
        animationSpine = GetComponent<AnimationSpine>();
        petrifyController = GetComponent<PetrifyController>();
        bodyCollider = GetComponent<Collider2D>();
        health = GetComponent<PlayerHealth>();
        audioController = GetComponent<PlayerAudio>();
        skinAssigner = GetComponent<SimpleSpineSkinAssigner>();
        swimController = GetComponent<PlayerSwimController>();
        sceneScaleController = GetComponent<PlayerSceneScaleController>();
        if (sceneScaleController == null)
        {
            sceneScaleController = gameObject.AddComponent<PlayerSceneScaleController>();
        }

        sceneJumpController = GetComponent<PlayerSceneJumpController>();
        if (sceneJumpController == null)
        {
            sceneJumpController = gameObject.AddComponent<PlayerSceneJumpController>();
        }

        sceneJumpController.JumpForceChanged += HandleSceneJumpForceChanged;
        
        rotaController = GetComponent<PlayerRotaSurfaceController>();
        rotaController?.Configure(movementData);

        if (normalAttackHitbox != null)
            return;

        normalAttackHitbox = GetComponentInChildren<PlayerMeleeHitbox>(true);
        if (normalAttackHitbox != null)
            Debug.Log($"[Player] Auto-resolved melee hitbox '{normalAttackHitbox.name}'.", this);
        else
            Debug.LogWarning("[Player] Missing PlayerMeleeHitbox reference.", this);
    }

    private void BuildSystems()
    {
        baseJumpForce = sceneJumpController != null ? sceneJumpController.CurrentJumpForce : 10f;
        CacheOriginalGravityScale();
        mover = new PlayerMover(rb, transform, bodyCollider, PlayerLayerConfig.GroundLayer, movementData, baseJumpForce);

        animationController = new PlayerAnimationController(animationSpine);
        animationController.OnCombatVisualComplete += OnCombatVisualComplete;

        visualController = new PlayerVisualController(this, animationController, GetComponentsInChildren<SpriteRenderer>());

        skillVisual = new PlayerSkillVisualController(skinAssigner);
        skillVisual.OnSkillVisualStarted += skill => OnSkillVisualStarted?.Invoke(skill);
        skillVisual.OnSkillVisualEnded += skill => OnSkillVisualEnded?.Invoke(skill);

        normalAttackHitbox?.Configure(this);
        Transform meleePoint = normalAttackHitbox != null ? normalAttackHitbox.transform : transform;

        skillExecutorRegistry = GetComponent<SkillExecutorRegistry>()
            ?? gameObject.AddComponent<SkillExecutorRegistry>();
        skillExecutorRegistry.Configure(this, transform, meleePoint, PlayerLayerConfig.AttackTargetLayer);

        combat = new PlayerCombat(
            combatData,
            PlayerLayerConfig.AttackTargetLayer,
            transform,
            meleePoint,
            normalAttackHitbox,
            this,
            skillExecutorRegistry,
            health);

        WireCombatEvents();
        BindHealthAnimationEvents();
    }

    private void RefreshJumpForceForActiveScene()
    {
        RestoreWorldSwapBoost();
        sceneJumpController?.ApplyJumpForceForActiveScene();
        baseJumpForce = sceneJumpController != null ? sceneJumpController.CurrentJumpForce : 10f;
        mover?.ResetJumpForce(baseJumpForce);
    }

    private void HandleSceneJumpForceChanged(float jumpForce)
    {
        RestoreWorldSwapBoost();
        baseJumpForce = jumpForce;
        mover?.ResetJumpForce(baseJumpForce);
        ApplyCurrentWorldSwapState();
    }

    private void WireCombatEvents()
    {
        combat.OnPlayCombatAnim += state =>
        {
            TrackEntry entry = animationController.PlayCombat(state);
            if (entry != null)
            {
                attackVfxController?.Play(state);
            }
            else
            {
                attackVfxController?.Stop();
            }

            return entry;
        };
        combat.OnPlaySkillSound += clip => audioController.PlayOneShot(clip);
        combat.OnClearCombatAnim += OnCombatClear;
        combat.OnPauseCombatAnim += animationController.PauseCombat;
        combat.OnResumeCombatAnim += animationController.ResumeCombat;
        combat.OnShowSkillBow += skillVisual.ShowBowSkin;
        combat.OnHideSkillBow += skillVisual.HideBowSkin;
        combat.OnSkillVisualStarted += skillVisual.BeginSkillVisual;
        combat.OnSkillVisualEnded += skillVisual.EndSkillVisual;
        combat.OnShowSkillProjectilePreview += skillVisual.ShowProjectilePreview;
        combat.OnHideSkillProjectilePreview += skillVisual.HideProjectilePreview;
        combat.OnFreezeBody += () => mover.FreezeAll();
        combat.OnRestorePhysics += () =>
        {
            mover.FreezeRotation();
            mover.SetSwimming(swimController.IsSwimming);
        };
    }

    private void BuildElementController()
    {
        elementController = new PlayerElementController(
            elementSkillSets,
            startingElementIndex,
            IsElementUnlockedForSelection);
        elementController.OnElementChanged += HandleElementChanged;
        elementController.Init();
    }

    private void HandleElementChanged(SkillSetSO skillSet, int index, int count)
    {
        OnElementChanged?.Invoke(skillSet, index, count);
    }

    private void BuildInputController()
    {
        inputController = new PlayerInputController();
        inputController.MoveChanged += input => moveInput = input;
        inputController.JumpRequested += OnJump;
        inputController.DashRequested += OnDash;
        inputController.ParryStarted += OnParry;
        inputController.ParryReleased += () => parryController?.ReleaseParry();
        inputController.GravityToggleRequested += () => OnGravityToggleRequested?.Invoke();
        inputController.NextElementRequested += CycleElement;
        inputController.ElementRequested += ChangeElementByType;
        inputController.AttackRequested += OnAttackRequested;
        inputController.SkillRequested += OnSkillRequested;
        inputController.SkillReleased += index => combat.ReleaseSkill(index);
    }

    private void BindHealthAnimationEvents()
    {
        health.OnHurt += PlayHurtAnimation;
        health.OnDied += PlayDeadAnimation;
        health.OnDied += CleanupDeathState;
        health.OnRespawned += HandleRespawned;
    }

    private void UnbindHealthAnimationEvents()
    {
        if (health == null)
            return;

        health.OnHurt -= PlayHurtAnimation;
        health.OnDied -= PlayDeadAnimation;
        health.OnDied -= CleanupDeathState;
        health.OnRespawned -= HandleRespawned;
    }

    private void TickAttackBuffer()
    {
        if (attackBufferTimer <= 0f)
            return;

        attackBufferTimer -= Time.deltaTime;
        if (CanUseActionOutsideWater() && combat.CanNormalAttack() && TryPrimaryAttack())
            attackBufferTimer = 0f;
    }

    private void TickParry()
    {
        bool wasParryBusy = IsParryBusy();
        parryController?.Tick(Time.deltaTime);
        if (wasParryBusy && !IsParryBusy() && !IsParryVisualBusy())
            RefreshMovementAnimation(actionActive: false, force: true);
    }

    private void TickFootstepAudio()
    {
        bool isGrounded = isRotaModeActive && rotaController != null
            ? rotaController.IsGrounded
            : mover.IsGrounded && !swimController.IsSwimming;

        audioController.TickFootsteps(
            Time.deltaTime,
            isGrounded,
            moveInput,
            health.IsHurt,
            health.IsBlocking,
            false);
    }

    private void OnAttackRequested()
    {
        attackBufferTimer = GetAttackInputBufferTime();
        if (CanUseActionOutsideWater() && TryPrimaryAttack())
            attackBufferTimer = 0f;
    }

    private void OnSkillRequested(int elementIndex)
    {
        SkillSetSO targetSet = GetSkillSet(elementIndex);
        if (targetSet == null) return;

        if (!CanUseActionOutsideWater() || IsAreaSkillActive() || !CanUseSkillFromCurrentMovementState(targetSet, 0))
            return;

        ResolveFairyCompanion()?.CompletePendingElementTransition();
        combat.TryCastSkill(elementIndex, targetSet, 0);
    }

    private void OnJump()
    {
        if (isRotaModeActive)
        {
            if (rotaController != null && rotaController.IsGrounded)
            {
                audioController.PlayJump();
            }

            rotaJumpQueued = true;
            return;
        }

        if (controlLocked || swimController.IsInWaterMovementState || IsParryBusy())
            return;

        if (combat.IsActionActive || IsAreaSkillActive() || health.IsKnockbackActive)
            return;

        if (!mover.TryJump(health.IsBlocking, health.IsHurt, controlLocked || combat.IsMovementLocked))
            return;

        audioController.PlayJump();
        if (mover.JumpForce != 0f)
            animationController.ForceRefreshMovement();
    }

    private void OnDash()
    {
        if (isRotaModeActive || controlLocked || IsParryBusy())
            return;

        if (combat.IsActionActive)
            combat.CancelAction();

        bool isSwimming = swimController != null && swimController.IsSwimming;
        if (isSwimming)
        {
            float facing = GetFacingDirectionSign();
            if (moveInput.x > 0f && facing < 0f)
                SetFacingDirectionSign(1f);
            else if (moveInput.x < 0f && facing > 0f)
                SetFacingDirectionSign(-1f);
        }

        if (!mover.TryDash(combat.IsMovementLocked, health.IsBlocking, health.IsHurt, moveInput, isSwimming))
            return;

        dashHandler.StartDashIFrame();
        animationController.PlayDash();
        audioController.PlayDash();
        audioController.StopFootsteps();

        if (isSwimming)
        {
            SpawnSwimDashVfx();
        }
    }

    private void OnParry()
    {
        if (!CanUseActionOutsideWater() || parryController == null || combat.IsActionActive || mover.IsDashing)
            return;

        parryController.TryStartParry();
    }

    private void CycleElement()
    {
        if (elementController != null && elementController.CycleElement())
        {
            ResetAllSkillCooldowns();
            audioController.PlayElementSwitch();
        }
    }

    private void ChangeElementByType(SkillElementType elementType)
    {
        if (TryChangeElement(elementType))
        {
            audioController.PlayElementSwitch();
        }
    }

    private void OnCombatVisualComplete()
    {
        attackVfxController?.Stop();

        if (health != null && health.IsDead)
            return;

        RefreshMovementAnimation(actionActive: false, force: true);
    }

    private void OnCombatClear()
    {
        attackVfxController?.Stop();
        skillVisual.HideBowSkin();
        animationController.ClearCombat();
    }

    private void RefreshMovementAnimation(bool actionActive, bool force = false)
    {
        if (force)
            animationController.ForceRefreshMovement();

        if (ropeGrabAnimationActive)
        {
            animationController.PlayRopeGrab();
            return;
        }

        animationController.UpdateMovement(
            rb.linearVelocity,
            swimController.State,
            mover.JumpForce,
            mover.JumpAnimation,
            mover.FallAnimation,
            !controlLocked && health.IsBlocking,
            health.IsHurt,
            actionActive,
            mover.IsGrounded);
    }

    public void ApplyNormalAttackMovement(AnimationSpine.State attackState)
    {
        if (mover == null || combatData == null)
        {
            return;
        }

        switch (attackState)
        {
            case AnimationSpine.State.NormalAttack1:
                mover.ApplyAttackBrake(
                    combatData.EffectiveFirstAttackBrakeDuration,
                    combatData.EffectiveFirstAttackBrakeStrength);
                break;

            case AnimationSpine.State.NormalAttack2:
                mover.ApplyAttackLunge(
                    combatData.EffectiveSecondAttackLungeSpeed,
                    combatData.EffectiveSecondAttackLungeDuration,
                    GetFacingDirectionSign());
                break;
        }
    }

    private void FlipCharacter()
    {
        if (isRotaModeActive || controlLocked || health.IsBlocking || health.IsHurt || mover.IsDashing || mover.IsAttackLungeActive || combat.IsMovementLocked || IsParryBusy())
            return;

        float facing = GetFacingDirectionSign();
        if (moveInput.x > 0f && facing < 0f)
            SetFacingDirectionSign(1f);
        else if (moveInput.x < 0f && facing > 0f)
            SetFacingDirectionSign(-1f);
    }

    private void SetFacingDirectionSign(float directionSign)
    {
        Vector3 swimPivotPosition = default;
        bool preserveSwimPivot = swimController != null
            && swimController.TryGetFlipPivotPosition(out swimPivotPosition);

        if (sceneScaleController != null)
        {
            sceneScaleController.SetFacingXSign(directionSign);
        }
        else
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (directionSign >= 0f ? -1f : 1f);
            transform.localScale = scale;
        }

        if (preserveSwimPivot)
        {
            transform.position += swimPivotPosition - swimController.FlipPivot.position;
        }
    }

    private float GetFacingDirectionSign() => transform.localScale.x < 0f ? 1f : -1f;

    public void SetRotaMode(bool active)
    {
        if (isRotaModeActive == active || rotaController == null)
            return;

        if (active)
        {
            // ROTA chỉ được phép hoạt động khi nằm trong RotaSurfaceZone hoặc đang trong Minigame ROTA checkpoint
            bool inZone = RotaSurfaceZone.IsPlayerInAnyZone(this);
            bool inMinigame = Tien.Minigame.MinigameCheckpointState.IsInMinigame;

            if (!inZone && !inMinigame)
            {
                return;
            }

            // Stop every normal gravity/movement modifier before ROTA takes
            // exclusive ownership of the Rigidbody2D.
            CancelNonSwimActions();
            RestoreWorldSwapBoost();
            attackBufferTimer = 0f;
            rotaJumpQueued = false;
            mover?.ZeroVelocity();

            isRotaModeActive = true;
            rotaController.SetActive(true);
        }
        else
        {
            isRotaModeActive = false;
            rotaJumpQueued = false;

            rotaController.SetActive(false);

            if (mover != null)
            {
                mover.RestoreGravity();
                mover.FreezeRotation();
            }

            // Re-apply the current world-swap state only after the normal
            // controller owns the Rigidbody2D again.
            ApplyCurrentWorldSwapState();
        }
    }

    private bool IsParryBusy() => parryController != null && parryController.IsBusy;
    private bool IsParryVisualBusy() => parryController != null && parryController.IsVisualBusy;
    private bool CanUseActionOutsideWater() => !isRotaModeActive && !controlLocked && !swimController.IsInWaterMovementState && !IsParryBusy();
    private bool IsAreaSkillActive() => areaSkillRunner != null && areaSkillRunner.IsActive;
    private static bool IsWaterOnlyMovementState(PlayerMovementState state)
        => state == PlayerMovementState.Swimming || state == PlayerMovementState.ExitingWater;

    private bool CanUseSkillFromCurrentMovementState(SkillSetSO skillSet, int slotIndex)
    {
        if (skillSet == null || !skillSet.TryGetSkillSlot(slotIndex, out SkillSetSO.SkillSlot slot))
            return false;

        if (slot.ExecutionConfig is AreaSkillConfig areaConfig
            && areaConfig.DeliveryMode == AreaSkillDeliveryMode.JumpApexSpreadShot)
            return mover != null && mover.IsGrounded;

        return true;
    }

    private bool TryPrimaryAttack()
    {
        if (combat != null && combat.TryNormalAttack(CurrentSkillSet))
        {
            if (audioController != null) audioController.PlayAttack();
            return true;
        }
        return false;
    }

    private float GetAttackInputBufferTime()
        => combatData != null ? combatData.EffectiveAttackInputBufferTime : DefaultAttackBufferTime;

    private void TickWorldSwapJumpBoost()
    {
        if (!enableWorldSwapJumpBoost || !IsMapHangActive())
        {
            RestoreWorldSwapBoost();
            return;
        }

        if (worldSwapManager == null && Time.time >= nextWorldSwapSearchTime)
        {
            nextWorldSwapSearchTime = Time.time + WorldSwapSearchInterval;
            ApplyCurrentWorldSwapState();
        }

        ApplyWorldSwapGravityIfNeeded();
    }

    private void ApplyCurrentWorldSwapState()
    {
        if (!enableWorldSwapJumpBoost || !IsMapHangActive())
        {
            RestoreWorldSwapBoost();
            return;
        }

        BindWorldSwapJumpBoost();
        if (worldSwapManager != null && worldSwapManager.HasSuccessfulSwap)
        {
            ApplyWorldSwapState(worldSwapManager.IsInWorldA);
        }
        else
        {
            RestoreWorldSwapBoost();
        }
    }

    private void BindWorldSwapJumpBoost()
    {
        if (worldSwapListenerBound && worldSwapManager != null)
        {
            return;
        }

        if (worldSwapManager == null)
        {
            worldSwapManager = FindAnyObjectByType<WorldSwapManager>();
        }

        if (worldSwapManager == null || worldSwapListenerBound)
        {
            return;
        }

        worldSwapManager.onWorldStateApplied.AddListener(ApplyWorldSwapState);
        worldSwapListenerBound = true;
    }

    private void UnbindWorldSwapJumpBoost()
    {
        if (!worldSwapListenerBound || worldSwapManager == null)
        {
            worldSwapListenerBound = false;
            return;
        }

        worldSwapManager.onWorldStateApplied.RemoveListener(ApplyWorldSwapState);
        worldSwapListenerBound = false;
    }

    private void ApplyWorldSwapState(bool isInWorldA)
    {
        if (isRotaModeActive)
            return;

        if (!IsMapHangActive()
            || worldSwapManager == null
            || !worldSwapManager.HasSuccessfulSwap)
        {
            RestoreWorldSwapBoost();
            return;
        }

        bool shouldBoost = worldSwapBoostedInWorldA ? isInWorldA : !isInWorldA;
        if (shouldBoost)
        {
            ApplyWorldSwapBoost();
        }
        else
        {
            RestoreWorldSwapBoost();
        }
    }

    private void ApplyWorldSwapBoost()
    {
        if (isRotaModeActive)
            return;

        worldSwapBoostActive = true;
        SetJumpForceOverride(baseJumpForce + Mathf.Max(0f, worldSwapJumpBonus));
        ApplyWorldSwapGravityIfNeeded();
    }

    private void RestoreWorldSwapBoost()
    {
        worldSwapBoostActive = false;
        if (jumpForceOverrideActive)
        {
            ClearJumpForceOverride();
        }

        RestoreOriginalGravityScale();
    }

    private void ApplyWorldSwapGravityIfNeeded()
    {
        if (isRotaModeActive || !worldSwapBoostActive || rb == null)
        {
            return;
        }

        CacheOriginalGravityScale();
        float gravitySign = Mathf.Sign(rb.gravityScale);
        if (Mathf.Approximately(gravitySign, 0f))
        {
            gravitySign = Mathf.Sign(originalGravityScale);
        }

        if (Mathf.Approximately(gravitySign, 0f))
        {
            gravitySign = 1f;
        }

        SetBaseGravityScale(gravitySign * Mathf.Abs(worldSwapBoostedGravityScale));
    }

    private static bool IsMapHangActive()
    {
        return SceneManager.GetActiveScene().name == MapHangSceneName;
    }

    private void CacheOriginalGravityScale()
    {
        if (rb == null || originalGravityScaleCached)
        {
            return;
        }

        originalGravityScale = rb.gravityScale;
        originalGravityScaleCached = true;
    }

    private void RestoreOriginalGravityScale()
    {
        if (rb != null && originalGravityScaleCached)
        {
            float gravitySign = Mathf.Sign(rb.gravityScale);
            if (Mathf.Approximately(gravitySign, 0f))
            {
                gravitySign = Mathf.Sign(originalGravityScale);
            }

            if (Mathf.Approximately(gravitySign, 0f))
            {
                gravitySign = 1f;
            }

            SetBaseGravityScale(gravitySign * Mathf.Abs(originalGravityScale));
        }
    }

    private void SetBaseGravityScale(float gravityScale)
    {
        if (mover != null)
        {
            mover.SetGravityScale(gravityScale);
            return;
        }

        if (rb != null)
        {
            rb.gravityScale = gravityScale;
        }
    }

    private void CancelNonSwimActions()
    {
        if (combat.IsActionActive)
            combat.EndAction();

        if (parryController != null && parryController.IsBusy)
            parryController.ReleaseParry();
    }

    private static void ApplySkillForces(SkillHitData hitData, IEnemyTarget enemy, Vector3 hitPosition)
    {
        if (hitData.KnockbackForce <= 0f && hitData.KnockUpForce <= 0f)
            return;

        Vector2 velocity = Vector2.zero;
        if (hitData.KnockbackForce > 0f)
            velocity += ((Vector2)(enemy.Transform.position - hitPosition)).normalized * hitData.KnockbackForce;

        if (hitData.KnockUpForce > 0f)
            velocity = new Vector2(velocity.x, hitData.KnockUpForce);

        enemy.ApplyVelocity(velocity, forceDynamicBody: hitData.KnockUpForce > 0f);
    }

    private void RefreshInputState() => inputController?.SetControlLocked(controlLocked);

    private void SpawnDashGhost()
    {
        if (movementData?.ghostPrefab == null)
            return;

        GameObject ghost = EffectPool.Spawn(movementData.ghostPrefab, transform.position, transform.rotation);
        if (ghost.TryGetComponent(out GhostEffect effect))
        {
            Color color = CurrentSkillSet != null ? CurrentSkillSet.dashGhostColor : new Color(1f, 1f, 1f, 0.5f);
            effect.Setup(transform, color);
        }
    }

    private void SpawnSwimDashVfx()
    {
        GameObject vfxPrefab = (CurrentSkillSet != null && CurrentSkillSet.swimDashVfxOverride != null)
            ? CurrentSkillSet.swimDashVfxOverride
            : (movementData != null ? movementData.swimDashVfxPrefab : null);

        if (vfxPrefab == null)
            return;

        Vector2 dashDir = mover.DashDirection;
        float angle = Mathf.Atan2(dashDir.y, dashDir.x) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

        Vector3 spawnOffset = movementData != null ? movementData.swimDashVfxOffset : Vector3.zero;
        Vector3 spawnPosition = transform.position + rotation * spawnOffset;

        EffectPool.Spawn(vfxPrefab, spawnPosition, rotation);
    }

    private FairyPlayerCompanionController ResolveFairyCompanion()
    {
        if (fairyCompanion != null && fairyCompanion.isActiveAndEnabled)
            return fairyCompanion;

        fairyCompanion = FindFirstObjectByType<FairyPlayerCompanionController>();
        return fairyCompanion;
    }

    private bool IsElementUnlockedForSelection(SkillElementType elementType)
    {
        // 1. Kiểm tra mở khóa toàn cục từ tất cả các Tinh Linh đang bay theo, PlayerPrefs và các UnlockController
        if (FairyElementUnlockController.IsAnyElementUnlockedGlobally(elementType))
        {
            return true;
        }

        // 2. Fallback kiểm tra UnlockController gắn trực tiếp
        FairyElementUnlockController unlockController = ResolveElementUnlockController();
        return unlockController != null && unlockController.IsElementUnlocked(elementType);
    }

    private FairyElementUnlockController ResolveElementUnlockController()
    {
        FairyPlayerCompanionController companion = ResolveFairyCompanionForPlayer();
        if (companion != null && companion.TryGetComponent(out FairyElementUnlockController fairyController))
        {
            return fairyController;
        }

        return TryGetComponent(out FairyElementUnlockController playerController)
            ? playerController
            : null;
    }

    private FairyPlayerCompanionController ResolveFairyCompanionForPlayer()
    {
#if UNITY_2023_1_OR_NEWER
        FairyPlayerCompanionController[] companions =
            FindObjectsByType<FairyPlayerCompanionController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#else
        FairyPlayerCompanionController[] companions =
            FindObjectsOfType<FairyPlayerCompanionController>();
#endif

        FairyPlayerCompanionController disabledFallback = null;

        for (int i = 0; i < companions.Length; i++)
        {
            FairyPlayerCompanionController companion = companions[i];
            if (companion == null || companion.ActivePlayer != this)
            {
                continue;
            }

            if (companion.isActiveAndEnabled)
            {
                return companion;
            }

            if (disabledFallback == null)
            {
                disabledFallback = companion;
            }
        }

        if (disabledFallback != null)
        {
            return disabledFallback;
        }

        return ResolveFairyCompanion();
    }

    private void OnDrawGizmosSelected()
    {
        Collider2DGizmoUtility.DrawEnabledCollidersInChildren(
            transform,
            new Color(0.1f, 0.9f, 0.25f, 0.9f));
    }
}
