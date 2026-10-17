using System;
using System.Collections;
using Spine;
using Spine.Unity;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerAttackHitboxWindow : MonoBehaviour
{
    [Serializable]
    private class AttackHitboxWindow
    {
        [SpineAnimation] public string animationName;
        [Min(0f)] public float showDelay;
        [Min(0f)] public float activeDuration = 0.1f;
        public GameObject[] hitboxObjects;
    }

    [Header("References")]
    [SerializeField] private SkeletonAnimation skeletonAnimation;

    [Header("Attack Windows")]
    [SerializeField] private AttackHitboxWindow[] attackWindows;

    private Coroutine activeWindowRoutine;

    private void Reset()
    {
        skeletonAnimation = GetComponent<SkeletonAnimation>();
    }

    private void Awake()
    {
        if (skeletonAnimation == null)
        {
            skeletonAnimation = GetComponent<SkeletonAnimation>();
        }

        SetAllHitboxesActive(false);
    }

    private void OnEnable()
    {
        if (skeletonAnimation != null)
        {
            skeletonAnimation.AnimationState.Start += HandleAnimationStart;
        }
    }

    private void OnDisable()
    {
        if (skeletonAnimation != null)
        {
            skeletonAnimation.AnimationState.Start -= HandleAnimationStart;
        }

        StopActiveWindow();
        SetAllHitboxesActive(false);
    }

    private void HandleAnimationStart(TrackEntry trackEntry)
    {
        if (trackEntry?.Animation == null)
        {
            return;
        }

        AttackHitboxWindow attackWindow = FindWindow(trackEntry.Animation.Name);
        if (attackWindow == null)
        {
            return;
        }

        StopActiveWindow();
        activeWindowRoutine = StartCoroutine(ShowHitboxWindow(attackWindow, trackEntry.TimeScale));
    }

    private AttackHitboxWindow FindWindow(string animationName)
    {
        if (attackWindows == null)
        {
            return null;
        }

        foreach (AttackHitboxWindow attackWindow in attackWindows)
        {
            if (attackWindow != null && attackWindow.animationName == animationName)
            {
                return attackWindow;
            }
        }

        return null;
    }

    private IEnumerator ShowHitboxWindow(AttackHitboxWindow attackWindow, float animationSpeed)
    {
        float timeScale = 1f / Mathf.Max(0.01f, animationSpeed);

        if (attackWindow.showDelay > 0f)
        {
            yield return new WaitForSeconds(attackWindow.showDelay * timeScale);
        }

        SetHitboxesActive(attackWindow.hitboxObjects, true);

        yield return new WaitForFixedUpdate();

        if (attackWindow.activeDuration > 0f)
        {
            yield return new WaitForSeconds(attackWindow.activeDuration * timeScale);
        }

        SetHitboxesActive(attackWindow.hitboxObjects, false);
        activeWindowRoutine = null;
    }

    private void StopActiveWindow()
    {
        if (activeWindowRoutine != null)
        {
            StopCoroutine(activeWindowRoutine);
            activeWindowRoutine = null;
        }

        SetAllHitboxesActive(false);
    }

    private void SetAllHitboxesActive(bool active)
    {
        if (attackWindows == null)
        {
            return;
        }

        foreach (AttackHitboxWindow attackWindow in attackWindows)
        {
            if (attackWindow != null)
            {
                SetHitboxesActive(attackWindow.hitboxObjects, active);
            }
        }
    }

    private static void SetHitboxesActive(GameObject[] hitboxObjects, bool active)
    {
        if (hitboxObjects == null)
        {
            return;
        }

        foreach (GameObject hitboxObject in hitboxObjects)
        {
            if (hitboxObject != null)
            {
                hitboxObject.SetActive(active);
            }
        }
    }
}
