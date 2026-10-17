using System.Collections;
using UnityEngine;

public sealed class WaterHealSkillRunner : MonoBehaviour
{
    private Coroutine _routine;
    private Player _activeOwner;
    private bool _presentationActive;

    public void Begin(Player owner, SkillData skill, WaterHealSkillConfig config)
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            Finish();
        }

        _activeOwner = owner;
        owner?.ShowSkillPresentation(skill, triggerSkillCollider: false);
        _presentationActive = owner != null;
        _routine = StartCoroutine(Run(owner, config));
    }

    private IEnumerator Run(Player owner, WaterHealSkillConfig config)
    {
        PlayerHealth health = owner != null ? owner.GetComponent<PlayerHealth>() : null;

        // Apply heal immediately on cast
        health?.Heal(config.HealAmount);
        SpawnHealVfx(owner, config);

        float timer = 0f;
        float presentationDuration = Mathf.Max(0f, config.AnimationDuration);
        while (timer < presentationDuration)
        {
            if (_presentationActive && timer >= presentationDuration)
                HidePresentation();
            timer += Time.deltaTime;
            yield return null;
        }

        Finish();
    }

    private void SpawnHealVfx(Player owner, WaterHealSkillConfig config)
    {
        if (owner == null || config.HealVfxPrefab == null) return;
        GameObject vfx = Instantiate(config.HealVfxPrefab, owner.transform.position, Quaternion.identity);
        Destroy(vfx, Mathf.Max(0.05f, config.HealVfxLifetime));
    }

    private void OnDisable()
    {
        if (_routine != null) StopCoroutine(_routine);
        Finish();
    }

    private void Finish()
    {
        HidePresentation();
        _activeOwner = null;
        _routine = null;
    }

    private void HidePresentation()
    {
        if (!_presentationActive) return;
        _activeOwner?.HideSkillPresentation();
        _presentationActive = false;
    }
}
