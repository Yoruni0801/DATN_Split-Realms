using System.Collections;
using UnityEngine;

public sealed class BuffSkillExecutor : ISkillExecutor
{
    public SkillAttackType AttackType => SkillAttackType.Buff;

    public bool CanExecute(SkillData skill, SkillExecutionConfig config)
    {
        return skill != null && config is BuffSkillConfig;
    }

    public bool Execute(SkillData skill, SkillExecutionConfig config, SkillCastContext context)
    {
        BuffSkillConfig buffConfig = config as BuffSkillConfig;
        if (buffConfig == null || context == null || context.Owner == null)
        {
            return false;
        }

        context.Owner.StartCoroutine(ApplyBuffRoutine(context.Owner, buffConfig));
        return true;
    }

    private IEnumerator ApplyBuffRoutine(Player owner, BuffSkillConfig config)
    {
        GameObject vfx = null;
        if (config.BuffVfxPrefab != null)
        {
            vfx = Object.Instantiate(config.BuffVfxPrefab, owner.transform);
        }

        if (config.Invincible)
        {
            PlayerHealth health = owner.GetComponent<PlayerHealth>();
            health?.SetInvincible(true);
            yield return new WaitForSeconds(Mathf.Max(0f, config.Duration));
            health?.SetInvincible(false);
        }
        else
        {
            yield return new WaitForSeconds(Mathf.Max(0f, config.Duration));
        }

        if (vfx != null)
        {
            Object.Destroy(vfx);
        }
    }
}
