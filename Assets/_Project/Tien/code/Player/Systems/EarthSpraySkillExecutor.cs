using UnityEngine;

public sealed class EarthSpraySkillExecutor : ISkillExecutor
{
    public SkillAttackType AttackType => SkillAttackType.EarthSpray;

    public bool CanExecute(SkillData skill, SkillExecutionConfig config)
    {
        return skill != null
            && skill.ElementType == SkillElementType.Earth
            && config is EarthSpraySkillConfig;
    }

    public bool Execute(SkillData skill, SkillExecutionConfig config, SkillCastContext context)
    {
        if (context == null || context.Owner == null || !(config is EarthSpraySkillConfig sprayConfig))
        {
            return false;
        }

        FairyElementVfxController fairy = ResolveFairy(context.Owner, skill);
        Vector3 origin = ResolveOrigin(context, fairy, sprayConfig);
        Collider2D target = EarthSkillTargeting.ResolveClosestEnemy(
            context.Owner,
            origin,
            sprayConfig.TargetSearchRadius,
            context.TargetLayer);
        if (target == null)
        {
            return false;
        }

        Vector2 direction = ((Vector2)target.bounds.center - (Vector2)origin).normalized;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        EarthSpraySkillRunner runner = context.Owner.GetComponent<EarthSpraySkillRunner>();
        if (runner == null)
        {
            runner = context.Owner.gameObject.AddComponent<EarthSpraySkillRunner>();
        }

        runner.Begin(context.Owner, fairy, skill, sprayConfig, origin, direction, context.TargetLayer);
        return true;
    }

    private static Vector3 ResolveOrigin(
        SkillCastContext context,
        FairyElementVfxController fairy,
        EarthSpraySkillConfig config)
    {
        Transform originTransform = fairy != null && fairy.BeamOrigin != null
            ? fairy.BeamOrigin
            : context.CastPoint != null ? context.CastPoint : context.Owner.transform;
        float facing = context.FacingDirection < 0f ? -1f : 1f;
        Vector3 offset = context.ScaleOffset(config.OriginOffset);
        return originTransform.position + new Vector3(offset.x * facing, offset.y, 0f);
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
