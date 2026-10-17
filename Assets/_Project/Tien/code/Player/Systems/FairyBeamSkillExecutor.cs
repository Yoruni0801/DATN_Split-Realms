using UnityEngine;

public sealed class FairyBeamSkillExecutor : ISkillExecutor
{
    public SkillAttackType AttackType => SkillAttackType.FairyBeam;

    public bool CanExecute(SkillData skill, SkillExecutionConfig config)
    {
        return skill != null && config is FairyBeamSkillConfig;
    }

    public bool Execute(SkillData skill, SkillExecutionConfig config, SkillCastContext context)
    {
        FairyBeamSkillConfig beamConfig = config as FairyBeamSkillConfig;
        if (beamConfig == null || context == null || context.Owner == null || beamConfig.BeamPrefab == null)
        {
            return false;
        }

        FairyElementVfxController fairy = ResolveFairy(context.Owner, skill);
        Collider2D target = ResolveTarget(context.Owner, fairy, context.Owner.transform.position, beamConfig.TargetSearchRadius);
        if (target == null)
        {
            return false;
        }

        Transform originTransform = fairy != null ? fairy.BeamOrigin : context.CastPoint;
        if (originTransform == null)
        {
            return false;
        }

        float facing = context.FacingDirection < 0f ? -1f : 1f;
        Vector3 offset = context.ScaleOffset(beamConfig.FairyOriginOffset);
        Vector3 origin = originTransform.position + new Vector3(offset.x * facing, offset.y, 0f);
        Vector2 direction = ((Vector2)target.bounds.center - (Vector2)origin).normalized;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        GameObject runnerHost = fairy != null ? fairy.gameObject : context.Owner.gameObject;
        FairyBeamSkillRunner runner = runnerHost.GetComponent<FairyBeamSkillRunner>();
        if (runner == null)
        {
            runner = runnerHost.AddComponent<FairyBeamSkillRunner>();
        }

        runner.Begin(
            context.Owner,
            fairy,
            skill,
            beamConfig,
            origin,
            direction,
            target,
            context.TargetLayer);

        return true;
    }

    private static Collider2D ResolveTarget(Player owner, FairyElementVfxController fairy, Vector3 origin, float radius)
    {
        if (owner == null || radius <= 0f)
        {
            return null;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, radius);
        Collider2D closest = null;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (IsSelfCollider(hit, owner, fairy))
            {
                continue;
            }

            if (!IsValidEnemyTarget(hit))
            {
                continue;
            }

            float distance = ((Vector2)hit.bounds.center - (Vector2)origin).sqrMagnitude;
            if (distance >= closestDistance)
            {
                continue;
            }

            closestDistance = distance;
            closest = hit;
        }

        return closest;
    }

    public static bool IsSelfCollider(Collider2D target, Player owner, FairyElementVfxController fairy = null)
    {
        if (target == null)
        {
            return false;
        }

        Transform targetTransform = target.transform;
        if (owner != null && targetTransform.root == owner.transform.root)
        {
            return true;
        }

        return fairy != null && targetTransform.root == fairy.transform.root;
    }

    public static bool IsValidEnemyTarget(Collider2D target)
    {
        if (target == null)
        {
            return false;
        }

        if (PlayerHitResolver.IsThreatSensorCollider(target))
        {
            return false;
        }

        IEnemyTarget enemyTarget = target.GetComponentInParent<IEnemyTarget>();
        if (enemyTarget != null)
        {
            return enemyTarget.IsAlive;
        }

        Bao.Enemies.BaoEnemyBase baoEnemy = target.GetComponentInParent<Bao.Enemies.BaoEnemyBase>();
        if (baoEnemy != null)
        {
            return !baoEnemy.IsDead;
        }

        if (target.GetComponentInParent<Bao.Enemies.IDamageable>() != null)
        {
            return true;
        }

        if (target.GetComponentInParent<EnemySystem.IDamageable>() != null)
        {
            return true;
        }

        if (target.GetComponentInParent<Bao.Bosses.DreadwormBoss>() != null)
        {
            return true;
        }

        if (target.GetComponentInParent<Bao.Bosses.NidusCocoonBoss>() != null)
        {
            return true;
        }

        return target.GetComponentInParent<Bao.Bosses.RedSlimeBoss>() != null;
    }

    private static FairyElementVfxController ResolveFairy(Player owner, SkillData skill)
    {
        FairyElementVfxController[] fairies = Object.FindObjectsByType<FairyElementVfxController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        FairyElement fairyElement = MapSkillElementToFairy(skill.ElementType);
        FairyElementVfxController fallback = null;
        for (int i = 0; i < fairies.Length; i++)
        {
            FairyPlayerCompanionController companion = fairies[i].GetComponent<FairyPlayerCompanionController>();
            if (companion != null && companion.ActivePlayer == owner)
            {
                if (companion.CurrentElement == fairyElement)
                {
                    return fairies[i];
                }
                fallback ??= fairies[i];
            }
        }

        return fallback != null ? fallback : (fairies.Length > 0 ? fairies[0] : null);
    }

    private static FairyElement MapSkillElementToFairy(SkillElementType elementType)
    {
        switch (elementType)
        {
            case SkillElementType.Fire: return FairyElement.Fire;
            case SkillElementType.Earth: return FairyElement.Earth;
            case SkillElementType.Wind: return FairyElement.Wind;
            case SkillElementType.Water:
            default: return FairyElement.Water;
        }
    }
}
