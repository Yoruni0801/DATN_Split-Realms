using System.Collections;
using UnityEngine;

public class PooledEffect : MonoBehaviour
{
    [SerializeField, Min(0f)] private float lifetime = 2f;

    private Coroutine lifetimeRoutine;

    public void Play()
    {
        if (lifetimeRoutine != null)
        {
            StopCoroutine(lifetimeRoutine);
        }

        if (lifetime > 0f)
        {
            lifetimeRoutine = StartCoroutine(ReturnAfterLifetime());
        }
    }

    private void OnDisable()
    {
        if (lifetimeRoutine == null)
        {
            return;
        }

        StopCoroutine(lifetimeRoutine);
        lifetimeRoutine = null;
    }

    private IEnumerator ReturnAfterLifetime()
    {
        yield return new WaitForSeconds(lifetime);
        lifetimeRoutine = null;
        EffectPool.Release(gameObject);
    }
}
