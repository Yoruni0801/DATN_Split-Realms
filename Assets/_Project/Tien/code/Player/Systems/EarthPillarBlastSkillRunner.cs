using System.Collections;
using UnityEngine;

public sealed class EarthPillarBlastSkillRunner : MonoBehaviour
{
    private Coroutine routine;
    private Player activeOwner;

    public void Begin(
        Player owner,
        SkillData skill,
        EarthPillarBlastSkillConfig config,
        Vector3 position,
        LayerMask targetLayer)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            Finish();
        }

        activeOwner = owner;
        owner?.ShowSkillPresentation(skill, triggerSkillCollider: false);
        routine = StartCoroutine(Run(owner, config, position, targetLayer));
    }

    private IEnumerator Run(Player owner, EarthPillarBlastSkillConfig config, Vector3 position, LayerMask targetLayer)
    {
        if (config.CastDelay > 0f)
        {
            yield return new WaitForSeconds(config.CastDelay);
        }

        SpawnPillar(config, position);

        if (config.DamageDelay > 0f)
        {
            yield return new WaitForSeconds(config.DamageDelay);
        }

        ApplyDamage(owner, config, position, targetLayer);
        Finish();
    }

    private static void SpawnPillar(EarthPillarBlastSkillConfig config, Vector3 position)
    {
        if (config.PillarPrefab == null)
        {
            return;
        }

        Quaternion rotation = Quaternion.Euler(config.PillarLocalEuler) * config.PillarPrefab.transform.rotation;
        GameObject pillar = Instantiate(config.PillarPrefab, position, rotation);
        pillar.transform.localScale = Vector3.Scale(pillar.transform.localScale, config.PillarLocalScale);
        Destroy(pillar, Mathf.Max(0.05f, config.VfxLifetime));
    }

    private static void ApplyDamage(Player owner, EarthPillarBlastSkillConfig config, Vector3 position, LayerMask targetLayer)
    {
        float radius = Mathf.Max(0f, config.DamageRadius);
        if (radius <= 0f)
        {
            return;
        }

        Collider2D[] hits = targetLayer.value != 0
            ? Physics2D.OverlapCircleAll(position, radius, targetLayer)
            : Physics2D.OverlapCircleAll(position, radius);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (FairyBeamSkillExecutor.IsSelfCollider(hit, owner) ||
                !FairyBeamSkillExecutor.IsValidEnemyTarget(hit))
            {
                continue;
            }

            PlayerHitResolver.TryApplyDamageToEnemy(hit, Mathf.Max(1, config.Damage), owner != null ? owner.transform : null);
        }
    }

    private void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
        }

        Finish();
    }

    private void Finish()
    {
        activeOwner?.HideSkillPresentation();
        activeOwner = null;
        routine = null;
    }
}
