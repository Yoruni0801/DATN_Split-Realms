using UnityEngine;

public sealed class WindStaffBeamSkillExecutor : ISkillExecutor
{
    public SkillAttackType AttackType => SkillAttackType.WindStaffBeam;

    public bool CanExecute(SkillData skill, SkillExecutionConfig config)
    {
        return skill != null && config is WindStaffBeamSkillConfig;
    }

    public bool Execute(SkillData skill, SkillExecutionConfig config, SkillCastContext context)
    {
        WindStaffBeamSkillConfig beamConfig = config as WindStaffBeamSkillConfig;
        if (beamConfig == null || context == null || context.Owner == null || beamConfig.BeamPrefab == null)
        {
            return false;
        }

        FairyElementVfxController fairy = ResolveFairy(context.Owner);
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
        WindStaffBeamSkillRunner runner = runnerHost.GetComponent<WindStaffBeamSkillRunner>();
        if (runner == null)
        {
            runner = runnerHost.AddComponent<WindStaffBeamSkillRunner>();
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
            if (FairyBeamSkillExecutor.IsSelfCollider(hit, owner, fairy))
            {
                continue;
            }

            if (!FairyBeamSkillExecutor.IsValidEnemyTarget(hit))
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

    private static FairyElementVfxController ResolveFairy(Player owner)
    {
        FairyElementVfxController[] fairies = Object.FindObjectsByType<FairyElementVfxController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < fairies.Length; i++)
        {
            FairyPlayerCompanionController companion = fairies[i].GetComponent<FairyPlayerCompanionController>();
            if (companion != null && companion.ActivePlayer == owner)
            {
                return fairies[i];
            }
        }

        return fairies.Length > 0 ? fairies[0] : null;
    }
}
