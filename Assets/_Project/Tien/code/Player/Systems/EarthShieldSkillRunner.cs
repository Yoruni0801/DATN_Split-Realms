using System.Collections;
using UnityEngine;

public sealed class EarthShieldSkillRunner : MonoBehaviour
{
    private Coroutine routine;
    private GameObject activeShieldObject;
    private Player activeOwner;
    private PlayerHealth activeHealth;
    private EarthShieldSkillConfig activeConfig;
    private bool presentationActive;

    public void Begin(Player owner, SkillData skill, EarthShieldSkillConfig config)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            Finish();
        }

        activeOwner = owner;
        activeConfig = config;
        owner?.ShowSkillPresentation(skill, triggerSkillCollider: false);
        presentationActive = owner != null;
        activeHealth = owner != null ? owner.GetComponent<PlayerHealth>() : null;
        SubscribeHealthEvents(activeHealth, true);
        routine = StartCoroutine(Run(owner, config));
    }

    private IEnumerator Run(Player owner, EarthShieldSkillConfig config)
    {
        SpawnShield(owner, config);
        activeHealth?.SetSkillDamageReduction(config.DamageReductionPercent);

        float timer = 0f;
        float presentationDuration = Mathf.Max(0f, config.AnimationDuration);
        float duration = Mathf.Max(0f, config.Duration);
        while (timer < duration)
        {
            if (presentationActive && timer >= presentationDuration)
            {
                HidePresentation();
            }

            timer += Time.deltaTime;
            yield return null;
        }

        Finish();
    }

    private void SpawnShield(Player owner, EarthShieldSkillConfig config)
    {
        if (owner == null || config.ShieldPrefab == null)
        {
            return;
        }

        activeShieldObject = Instantiate(config.ShieldPrefab, owner.transform);
        activeShieldObject.transform.localPosition = config.ShieldLocalPosition;
        activeShieldObject.transform.localRotation = Quaternion.Euler(config.ShieldLocalEuler);
        activeShieldObject.transform.localScale = Vector3.Scale(activeShieldObject.transform.localScale, config.ShieldLocalScale);
    }

    private void BreakShield()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
        }

        SpawnBreakVfx();
        Finish();
    }

    private void SpawnBreakVfx()
    {
        if (activeConfig == null || activeConfig.BreakVfxPrefab == null)
        {
            return;
        }

        Vector3 position = activeShieldObject != null
            ? activeShieldObject.transform.position
            : transform.position;
        Quaternion rotation = activeConfig.BreakVfxPrefab.transform.rotation;
        GameObject breakVfx = Instantiate(activeConfig.BreakVfxPrefab, position, rotation);
        Destroy(breakVfx, Mathf.Max(0.05f, activeConfig.BreakVfxLifetime));
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
        SubscribeHealthEvents(activeHealth, false);
        activeHealth?.SetSkillDamageReduction(0);
        activeHealth = null;
        HidePresentation();
        activeOwner = null;
        activeConfig = null;

        if (activeShieldObject != null)
        {
            Destroy(activeShieldObject);
            activeShieldObject = null;
        }

        routine = null;
    }

    private void HidePresentation()
    {
        if (!presentationActive)
        {
            return;
        }

        activeOwner?.HideSkillPresentation();
        presentationActive = false;
    }

    private void SubscribeHealthEvents(PlayerHealth health, bool subscribed)
    {
        if (health == null)
        {
            return;
        }

        if (subscribed)
        {
            health.OnHurt += BreakShield;
            health.OnBlockedHit += BreakShield;
        }
        else
        {
            health.OnHurt -= BreakShield;
            health.OnBlockedHit -= BreakShield;
        }
    }
}
