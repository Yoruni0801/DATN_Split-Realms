using System.Collections.Generic;
using UnityEngine;

public sealed class SkillExecutorRegistry : MonoBehaviour
{
    private readonly Dictionary<SkillAttackType, ISkillExecutor> executors = new Dictionary<SkillAttackType, ISkillExecutor>();
    private SkillCastContext context;

    public void Configure(Player owner, Transform body, Transform castPoint, LayerMask targetLayer)
    {
        context = new SkillCastContext(owner, body, castPoint, targetLayer, owner != null ? owner.ScaleProvider : null);
        RegisterDefaults();
    }

    public bool TryExecute(SkillSetSO.SkillSlot slot, float holdDuration = 0f)
    {
        if (slot == null || slot.SkillData == null)
        {
            return false;
        }

        SkillData skill = slot.SkillData;
        SkillExecutionConfig config = slot.ExecutionConfig;
        if (config == null || config.AttackType != skill.AttackType)
        {
            Debug.LogWarning($"Skill '{skill.SkillName}' has missing or mismatched execution config.", this);
            return false;
        }

        if (!executors.TryGetValue(skill.AttackType, out ISkillExecutor executor))
        {
            Debug.LogWarning($"No executor registered for {skill.AttackType}.", this);
            return false;
        }

        SkillCastContext castContext = context != null ? context.WithHoldDuration(holdDuration) : null;
        return executor.CanExecute(skill, config) && executor.Execute(skill, config, castContext);
    }

    private void RegisterDefaults()
    {
        if (executors.Count > 0)
        {
            return;
        }

        Register(new MeleeSkillExecutor());
        Register(new ProjectileSkillExecutor());
        Register(new AreaSkillExecutor());
        Register(new BuffSkillExecutor());
        Register(new SummonSkillExecutor());
        Register(new FairyBeamSkillExecutor());
        Register(new FireSpearDashSkillExecutor());
        Register(new FireVfxFallExplosionSkillExecutor());
        Register(new FireSpearBarrageSkillExecutor());
        Register(new WindStaffBeamSkillExecutor());
        Register(new WindTornadoSkillExecutor());
        Register(new EarthSpraySkillExecutor());
        Register(new EarthPillarBlastSkillExecutor());
        Register(new EarthShieldSkillExecutor());
        Register(new WaterHealSkillExecutor());
    }

    private void Register(ISkillExecutor executor)
    {
        executors[executor.AttackType] = executor;
    }
}
