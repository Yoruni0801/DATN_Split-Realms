using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(AnimationSpine))]
public partial class DuyBossController : MonoBehaviour, IEnemyTarget, IParryable
{
    private const string DefaultSlashVfxPath = "Assets/_Project/Tien/MagicArsenal/Effects/Prefabs/Slash/FireSlash.prefab";

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        animationSpine = GetComponent<AnimationSpine>();
        if (hitFlash == null)
        {
            hitFlash = GetComponentInChildren<BossSpineHitFlash>(true);
        }

        if (hitFlash == null)
        {
            hitFlash = gameObject.AddComponent<BossSpineHitFlash>();
        }

        hitFlash.Initialize(animationSpine);
        currentHealth = maxHealth;
        InitializeBossCore();

        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        if (playerLayer.value == 0)
        {
            playerLayer = LayerMask.GetMask("Player");
        }

        if (movementLayer.value == 0)
        {
            movementLayer = LayerMask.GetMask("Default", "Ground", "Platform", "Wall", "Obstacle");
        }

        if (groundLayer.value == 0)
        {
            groundLayer = movementLayer;
        }

        if (weaponHitbox == null)
        {
            weaponHitbox = GetComponentInChildren<BossAttackHitbox>(true);
        }

        if (weaponHitbox != null)
        {
            WarnIfWeaponHitboxDoesNotFollowSpine();
            weaponHitbox.SetPlayerLayer(playerLayer);
            weaponHitbox.OnHit -= HandleAttackImpact;
            weaponHitbox.OnHit += HandleAttackImpact;
            weaponHitbox.CancelAttack();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (slashVfxPrefab == null)
        {
            slashVfxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultSlashVfxPath);
        }
    }
#endif

    private void OnEnable()
    {
        if (animationSpine != null && animationSpine.AnimationState != null)
        {
            animationSpine.AnimationState.Event -= HandleSpineEvent;
            animationSpine.AnimationState.Event += HandleSpineEvent;
        }

        EnterState(BossState.Idle, RandomDecisionInterval());
    }

    private void OnDisable()
    {
        if (animationSpine != null && animationSpine.AnimationState != null)
        {
            animationSpine.AnimationState.Event -= HandleSpineEvent;
        }

        weaponHitbox?.CancelAttack();
        RestoreSwordVisual();
    }

    private void WarnIfWeaponHitboxDoesNotFollowSpine()
    {
        if (weaponHitbox == null)
        {
            return;
        }

        bool followsBone = weaponHitbox.GetComponent<Spine.Unity.BoneFollower>() != null;
        bool followsBoundingBox = weaponHitbox.GetComponent<Spine.Unity.BoundingBoxFollower>() != null;
        if (followsBone || followsBoundingBox)
        {
            return;
        }

        Debug.LogWarning(
            "WeaponHitbox does not follow a Spine weapon bone/bounding box. Damage can feel like contact damage if this object is fixed under the boss root.",
            weaponHitbox);
    }

    private void Update()
    {
        if (state == BossState.Dead)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        TickTimers(deltaTime);
        ResolveTarget();
        EvaluateTarget(deltaTime);
        UpdateFacing();
        UpdateDecision();
        UpdateState();
        TickDebugStateLog(deltaTime);
    }

    private void FixedUpdate()
    {
        ApplyMovement();
    }
}
