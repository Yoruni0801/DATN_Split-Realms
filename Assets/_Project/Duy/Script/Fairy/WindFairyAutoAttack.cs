using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Bao.Bosses;
using Bao.Enemies;
using Quoc.PuzzleQuest;

public class WindFairyAutoAttack : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private LithosMonolithBoss targetBoss;
    [SerializeField] private bool autoFindBoss = true;

    [Header("Attack Prefab & VFX")]
    [SerializeField] private GameObject attackPrefab;
    [SerializeField] private Vector3 vfxScale = Vector3.one;
    [SerializeField] private float vfxDuration = 1.5f;
    [SerializeField] private float travelSpeed = 12f;

    [Header("Timing & Animation")]
    [SerializeField] private string animationName = "Blow Petals";
    [SerializeField] private float castDelay = 0.25f;
    [SerializeField] private float attackCooldown = 2.0f;
    [SerializeField] private FairyElement requiredElement = FairyElement.Wind;
    [SerializeField] private bool autoAttackEnabled = true;

    private FairyPlayerCompanionController companion;
    private FairyElementVfxController vfxController;
    private Animator cachedAnimator;
    private float cooldownTimer;
    private bool isAttacking;
    private CancellationTokenSource attackCts;

    public bool IsAttacking => isAttacking;

    private void Awake()
    {
        companion = GetComponent<FairyPlayerCompanionController>();
        vfxController = GetComponent<FairyElementVfxController>();
        if (vfxController != null)
        {
            cachedAnimator = vfxController.GetComponentInChildren<Animator>();
        }
    }

    private void OnEnable()
    {
        PuzzleQuestController.OnPuzzleBossDefeated += HandleBossDefeated;
        LithosMonolithBoss.OnGlobalLithosBossDied += HandleBossDefeated;
    }

    private void OnDisable()
    {
        PuzzleQuestController.OnPuzzleBossDefeated -= HandleBossDefeated;
        LithosMonolithBoss.OnGlobalLithosBossDied -= HandleBossDefeated;
        StopAttack();
    }

    private void OnDestroy()
    {
        StopAttack();
    }

    private void HandleBossDefeated()
    {
        SetAutoAttackEnabled(false);
        StopAttack();
    }

    private void Start()
    {
        FindBossIfNeeded();
    }

    private void FindBossIfNeeded()
    {
        if (targetBoss == null && autoFindBoss)
        {
            targetBoss = FindFirstObjectByType<LithosMonolithBoss>();
        }
    }

    private void Update()
    {
        if (!autoAttackEnabled) return;
        if (companion != null && companion.CurrentElement != requiredElement) return;

        if (cooldownTimer > 0)
        {
            cooldownTimer -= Time.deltaTime;
            return;
        }

        if (isAttacking) return;

        if (targetBoss == null)
        {
            FindBossIfNeeded();
            if (targetBoss == null) return;
        }

        if (targetBoss.IsDead || targetBoss.IsTransitioningPhases) return;
        if (targetBoss.CurrentState != targetBoss.IdleState) return;

        _ = ExecuteAttackAsync();
    }

    public void SetAutoAttackEnabled(bool enabled)
    {
        autoAttackEnabled = enabled;
        if (!enabled) StopAttack();
    }

    public void StopAttack()
    {
        if (attackCts != null)
        {
            attackCts.Cancel();
            attackCts.Dispose();
            attackCts = null;
        }

        isAttacking = false;
        if (companion != null)
        {
            companion.SetCastingLocked(false);
        }

        if (targetBoss != null && !targetBoss.IsDead)
        {
            targetBoss.ForceReadyToAct();
        }
    }

    private async Task ExecuteAttackAsync()
    {
        if (targetBoss == null || isAttacking) return;

        isAttacking = true;
        attackCts?.Dispose();
        attackCts = new CancellationTokenSource();
        CancellationToken token = attackCts.Token;

        if (companion != null)
        {
            companion.SetCastingLocked(true);
        }
        Vector3 lockedPosition = transform.position;

        PlayFairyAnimation();

        float estimatedAttackTime = castDelay + vfxDuration + 0.2f;
        targetBoss.DelayActionBy(estimatedAttackTime);

        // Cast delay
        float timer = 0f;
        while (timer < castDelay)
        {
            if (token.IsCancellationRequested || this == null) return;
            transform.position = lockedPosition;
            timer += Time.deltaTime;
            await Task.Yield();
        }

        if (token.IsCancellationRequested || this == null || targetBoss == null || targetBoss.IsDead)
        {
            StopAttack();
            return;
        }

        // Quay mặt về phía Boss
        if (vfxController != null)
        {
            Vector3 localScale = vfxController.transform.localScale;
            float sign = Mathf.Sign(targetBoss.CenterPosition.x - transform.position.x);
            localScale.x = Mathf.Abs(localScale.x) * sign;
            vfxController.transform.localScale = localScale;
        }

        // Spawn VFX
        if (attackPrefab != null)
        {
            GameObject vfxInstance = Instantiate(attackPrefab, transform.position, Quaternion.identity);
            vfxInstance.transform.localScale = vfxScale;

            if (travelSpeed > 0.1f)
            {
                _ = FlyVfxToBossAsync(vfxInstance, targetBoss, token);
            }
            else
            {
                vfxInstance.transform.position = targetBoss.CenterPosition;
                Destroy(vfxInstance, vfxDuration);
            }
        }

        // Chờ animation hoàn tất
        timer = 0f;
        while (timer < 0.4f)
        {
            if (token.IsCancellationRequested || this == null) return;
            timer += Time.deltaTime;
            await Task.Yield();
        }

        if (companion != null)
        {
            companion.SetCastingLocked(false);
        }

        if (targetBoss != null && !targetBoss.IsDead)
        {
            targetBoss.SetTarget(transform);
            targetBoss.ForceAttack();
        }

        cooldownTimer = attackCooldown;
        isAttacking = false;
    }

    private async Task FlyVfxToBossAsync(GameObject vfx, LithosMonolithBoss boss, CancellationToken token)
    {
        if (vfx == null || boss == null) return;

        Destroy(vfx, vfxDuration + 2.0f);

        while (vfx != null && boss != null && !boss.IsDead && !token.IsCancellationRequested)
        {
            Vector3 targetPos = boss.CenterPosition;
            vfx.transform.position = Vector3.MoveTowards(vfx.transform.position, targetPos, travelSpeed * Time.deltaTime);

            if (Vector3.Distance(vfx.transform.position, targetPos) <= 0.4f)
            {
                break;
            }

            await Task.Yield();
        }

        if (vfx != null)
        {
            Destroy(vfx, vfxDuration);
        }
    }

    private void PlayFairyAnimation()
    {
        if (cachedAnimator == null && vfxController != null)
        {
            cachedAnimator = vfxController.GetComponentInChildren<Animator>();
        }

        if (cachedAnimator != null && !string.IsNullOrEmpty(animationName))
        {
            cachedAnimator.CrossFadeInFixedTime(animationName, 0.05f, 0, 0f);
        }
    }
}
