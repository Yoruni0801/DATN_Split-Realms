using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Tien.AdvancedLoading.Core;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerHealth : MonoBehaviour, Bao.Enemies.IDamageable, EnemySystem.IDamageable
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 1000;
    [SerializeField] private int blockDamageReduction = 50;

    [Header("Timing")]
    [SerializeField] private float hurtDuration = 0.4f;
    [SerializeField] private float knockbackJumpLockDuration = 0.25f;
    [SerializeField] private float respawnDelay = 2f;
    [SerializeField] private bool autoRespawn = true;

    private Rigidbody2D rb;
    private RigidbodyType2D originalBodyType;
    private float originalGravityScale;
    private RigidbodyConstraints2D originalConstraints;
    private bool isReloadingScene;
    private Collider2D bodyCollider;
    private InputSystem_Actions inputActions;
    private PlayerParryController parryController;
    private Vector2 respawnPosition;
    private float hurtTimer;
    private float knockbackTimer;
    private float respawnTimer;
    private bool isInvincible;
    private bool isBlockHeld;
    private bool isBlockSuppressed;
    private float slowTimer;
    private int skillDamageReductionPercent;
    private int lastResolvedAttackFrame = -1;
    private int lastResolvedAttackKey;

    public event Action<int, int> OnHealthChanged;
    public event Action<int> OnDamageTaken;
    public event Action OnBlockedHit;
    public event Action OnHurt;
    public event Action OnDied;
    public event Action OnRespawned;
    public event Action<Vector2> OnCheckpointChanged;

    // Global events for UI decoupling
    public static event Action<int, int> OnGlobalHealthChanged;
    public static int GlobalCurrentHealth { get; private set; }
    public static int GlobalMaxHealth { get; private set; }

    public int MaxHealth => maxHealth;
    public int CurrentHealth { get; private set; }
    public bool IsBlocking => isBlockHeld && !isBlockSuppressed && !IsDead;
    public bool IsDead { get; private set; }
    public bool IsHurt => hurtTimer > 0f;
    public bool IsKnockbackActive => knockbackTimer > 0f;
    public bool IsSlowed => slowTimer > 0f;
    public Vector2 CurrentCheckpointPosition => respawnPosition;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        inputActions = new InputSystem_Actions();

        // Captured before anything can change them, so respawn restores the authored setup exactly.
        originalBodyType = rb.bodyType;
        originalGravityScale = rb.gravityScale;
        originalConstraints = rb.constraints;

        if (PlayerCarryData.TryGetCurrentHealth(out int carriedHealth))
        {
            CurrentHealth = carriedHealth > 0 ? carriedHealth : maxHealth;
        }
        else
        {
            CurrentHealth = maxHealth;
        }

        GlobalMaxHealth = maxHealth;
        GlobalCurrentHealth = CurrentHealth;
        PlayerCarryData.SetCurrentHealth(CurrentHealth);

        respawnPosition = transform.position;

        inputActions.Player.Block.performed += _ => isBlockHeld = true;
        inputActions.Player.Block.canceled += _ => isBlockHeld = false;
    }

    private void Start()
    {
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        OnGlobalHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    private void OnEnable()
    {
        inputActions.Enable();
    }

    private void OnDisable()
    {
        inputActions.Disable();
    }

    private void OnDestroy()
    {
        inputActions?.Dispose();
    }

    private void Update()
    {
        TickHurt(Time.deltaTime);
        TickKnockback(Time.deltaTime);
        TickRespawn(Time.deltaTime);
        if (slowTimer > 0f) slowTimer -= Time.deltaTime;
    }

    public void ApplySlow(float duration)
    {
        slowTimer = duration;
    }

    public void SetInvincible(bool value)
    {
        isInvincible = value;
    }

    public void SetBlockSuppressed(bool value)
    {
        isBlockSuppressed = value;
    }

    public void SetSkillDamageReduction(int percent)
    {
        skillDamageReductionPercent = Mathf.Clamp(percent, 0, 100);
    }

    public void SetAutoRespawn(bool value)
    {
        autoRespawn = value;
    }

    public IncomingAttackResolution TryReceiveAttack(IncomingAttackData attack, IParryable parryable)
    {
        if (IsDead || isInvincible)
        {
            return IncomingAttackResolution.Ignored;
        }

        if (WasAttackAlreadyResolvedThisFrame(attack, parryable))
        {
            return IncomingAttackResolution.Ignored;
        }

        MarkAttackResolved(attack, parryable);

        if (ResolveParryController() != null)
        {
            PlayerDefenseResult defenseResult = parryController.ResolveIncomingAttack(parryable, attack);
            if (defenseResult.Outcome == PlayerDefenseOutcome.PerfectParry)
            {
                if (defenseResult.PreventsDamage)
                {
                    return IncomingAttackResolution.Parried;
                }

                return ApplyDamage(defenseResult.DamageToApply, attack.SourceTransform, attack.CanBeBlocked)
                    ? IncomingAttackResolution.Damaged
                    : IncomingAttackResolution.Ignored;
            }

            if (defenseResult.Outcome == PlayerDefenseOutcome.Guard)
            {
                return ApplyDamage(defenseResult.DamageToApply, attack.SourceTransform, canBeBlocked: false)
                    ? IncomingAttackResolution.Guarded
                    : IncomingAttackResolution.Ignored;
            }
        }

        if (attack.IsSlowAttack)
        {
            ApplySlow(2f); // Apply slow effect for 2 seconds
            if (attack.Damage <= 0) return IncomingAttackResolution.Damaged; // Skip damage if it's a 0 damage slow attack
        }

        return ApplyDamage(attack.Damage, attack.SourceTransform, attack.CanBeBlocked)
            ? IncomingAttackResolution.Damaged
            : IncomingAttackResolution.Ignored;
    }
    public void TakeDamage(int damage, Transform attacker = null)
    {
        ApplyDamage(damage, attacker, canBeBlocked: true);
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;
        CurrentHealth = Mathf.Min(CurrentHealth + amount, maxHealth);
        GlobalCurrentHealth = CurrentHealth;
        PlayerCarryData.SetCurrentHealth(CurrentHealth);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        OnGlobalHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void ApplyEnvironmentalDamage(int damage)
    {
        ApplyDamage(damage, null, canBeBlocked: false);
    }

    public void ApplyEnvironmentalDamageWithoutReaction(int damage)
    {
        ApplyDamage(damage, null, canBeBlocked: false, playReaction: false);
    }

    void Bao.Enemies.IDamageable.TakeDamage(int damage)
    {
        ApplyDamage(damage, null, canBeBlocked: true);
    }

    void EnemySystem.IDamageable.TakeDamage(int damage)
    {
        ApplyDamage(damage, null, canBeBlocked: true);
    }

    void EnemySystem.IDamageable.Die()
    {
        if (!IsDead)
        {
            Die();
        }
    }

    public void UpdateCheckpoint(Vector2 newPosition)
    {
        respawnPosition = newPosition;
        OnCheckpointChanged?.Invoke(respawnPosition);
    }

    /// <summary>
    /// In place respawn kept as a manual fallback. It only restores the player, so the rest of the scene stays in
    /// whatever state the run left it; normal death retry goes through RetryCurrentScene instead.
    /// </summary>
    public void RespawnAtCheckpoint()
    {
        if (!IsDead)
        {
            return;
        }

        Respawn();
    }

    public void KillByFall()
    {
        if (IsDead)
        {
            return;
        }

        CurrentHealth = 0;
        GlobalCurrentHealth = CurrentHealth;
        PlayerCarryData.SetCurrentHealth(0);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        OnGlobalHealthChanged?.Invoke(CurrentHealth, maxHealth);
        Die();
    }

    private bool ApplyDamage(int damage, Transform attacker, bool canBeBlocked, bool playReaction = true)
    {
        if (IsDead || isInvincible || IsHurt)
        {
            return false;
        }

        int finalDamage = canBeBlocked && IsBlocking
            ? CombatDamageMath.ApplyPercentReduction(damage, blockDamageReduction)
            : damage;
        finalDamage = CombatDamageMath.ApplyPercentReduction(finalDamage, skillDamageReductionPercent);

        CurrentHealth = Mathf.Max(0, CurrentHealth - finalDamage);
        GlobalCurrentHealth = CurrentHealth;
        PlayerCarryData.SetCurrentHealth(CurrentHealth);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        OnGlobalHealthChanged?.Invoke(CurrentHealth, maxHealth);
        if (finalDamage > 0)
        {
            OnDamageTaken?.Invoke(finalDamage);
        }
        ShowDamagePopup(finalDamage);

        if (canBeBlocked && IsBlocking)
        {
            OnBlockedHit?.Invoke();
            if (attacker != null)
            {
                ApplyKnockback(attacker, 5f, 0f);
            }
        }
        else if (CurrentHealth <= 0)
        {
            Die();
        }
        else if (!playReaction)
        {
            parryController?.ResetDefenseState();
        }
        else
        {
            StartHurt(attacker);
        }

        return true;
    }

    private void TickHurt(float deltaTime)
    {
        if (hurtTimer > 0f)
        {
            hurtTimer -= deltaTime;
        }
    }

    private void TickKnockback(float deltaTime)
    {
        if (knockbackTimer > 0f)
        {
            knockbackTimer -= deltaTime;
        }
    }

    private void TickRespawn(float deltaTime)
    {
        if (!IsDead || !autoRespawn)
        {
            return;
        }

        respawnTimer -= deltaTime;
        if (respawnTimer <= 0f)
        {
            RetryCurrentScene();
        }
    }

    /// <summary>
    /// Normal death retry: reloads the active scene so enemies, boss health, activation objects, gates and every
    /// other runtime change return to their authored state. In place respawn only restores the player, which is
    /// why it is no longer the default path.
    /// </summary>
    public void RetryCurrentScene()
    {
        if (isReloadingScene)
        {
            return;
        }

        isReloadingScene = true;

        Scene activeScene = SceneManager.GetActiveScene();

        // Preserve recorded timeline history across retry; only resume state is carried over.
        TimelinePlayStateManager.BeginSceneChange(activeScene.name, true);

        // Reset carried health to max on death retry
        PlayerCarryData.SetCurrentHealth(maxHealth);

        // Each scene owns its own placed Player, so reloading the scene recreates the
        // player. The fresh player restores the checkpoint position from
        // SceneRuntimeStateManager and starts at full health.
        SceneLoader.Load(activeScene.name);
    }

    private void StartHurt(Transform attacker)
    {
        hurtTimer = hurtDuration;
        parryController?.ResetDefenseState();
        OnHurt?.Invoke();

        if (attacker != null)
        {
            ApplyKnockback(attacker, 10f, 5f);
        }
    }

    private void Die()
    {
        IsDead = true;
        hurtTimer = 0f;
        knockbackTimer = 0f;
        respawnTimer = respawnDelay;
        isBlockSuppressed = false;
        parryController?.ResetDefenseState();

        OnDied?.Invoke();

        // Zeroing velocity once is not enough: gravity, slopes, contacts and the collider material keep resolving
        // on a Dynamic body, so the corpse is pinned until respawn instead.
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
        rb.bodyType = RigidbodyType2D.Kinematic;
    }
    private void Respawn()
    {
        transform.position = respawnPosition;

        if (rb != null)
        {
            rb.position = respawnPosition;
            rb.bodyType = originalBodyType;
            rb.constraints = originalConstraints;
            rb.gravityScale = originalGravityScale;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        Physics2D.SyncTransforms();

        CurrentHealth = maxHealth;
        IsDead = false;
        hurtTimer = 0f;
        knockbackTimer = 0f;
        isInvincible = false;
        isBlockSuppressed = false;
        skillDamageReductionPercent = 0;
        slowTimer = 0f;
        bodyCollider.enabled = true;

        GlobalCurrentHealth = CurrentHealth;
        PlayerCarryData.SetCurrentHealth(CurrentHealth);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        OnGlobalHealthChanged?.Invoke(CurrentHealth, maxHealth);
        OnRespawned?.Invoke();
    }

    private void ApplyKnockback(Transform attacker, float pushX, float pushY)
    {
        rb.linearVelocity = CombatDamageMath.KnockbackVelocity(transform.position, attacker.position, pushX, pushY);
        knockbackTimer = Mathf.Max(knockbackTimer, knockbackJumpLockDuration);
    }

    private PlayerParryController ResolveParryController()
    {
        if (parryController == null)
        {
            parryController = GetComponent<PlayerParryController>();
        }

        return parryController;
    }

    private bool WasAttackAlreadyResolvedThisFrame(IncomingAttackData attack, IParryable parryable)
    {
        return lastResolvedAttackFrame == Time.frameCount
            && lastResolvedAttackKey == GetAttackKey(attack, parryable);
    }

    private void MarkAttackResolved(IncomingAttackData attack, IParryable parryable)
    {
        lastResolvedAttackFrame = Time.frameCount;
        lastResolvedAttackKey = GetAttackKey(attack, parryable);
    }

    private static int GetAttackKey(IncomingAttackData attack, IParryable parryable)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + attack.Damage;
            hash = hash * 31 + (int)attack.AttackType;
            hash = hash * 31 + (parryable != null ? parryable.GetHashCode() : 0);
            hash = hash * 31 + (attack.SourceTransform != null ? attack.SourceTransform.GetInstanceID() : 0);
            return hash;
        }
    }

    private void ShowDamagePopup(int damage)
    {
        global::DamagePopupSystem.ShowPlayer(transform.position + Vector3.up, damage);
    }
}
