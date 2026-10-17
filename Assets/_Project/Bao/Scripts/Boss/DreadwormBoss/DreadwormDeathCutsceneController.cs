using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

namespace Bao.Bosses
{
    [DisallowMultipleComponent]
    public sealed class DreadwormDeathCutsceneController : MonoBehaviour
    {
        [Header("Death Cutscene")]
        [SerializeField] private GameObject deathCutsceneObject;
        [SerializeField] private PlayableDirector deathCutsceneDirector;
        [SerializeField] private bool activateCutsceneObject = true;
        [SerializeField] private bool resetDirectorTime = true;
        [SerializeField] private bool playDirector = true;

        [Header("Fallback")]
        [SerializeField] private bool destroyBossWhenNoCutscene = true;
        [SerializeField] private float destroyDelayIfNoCutscene = 3f;

        private Coroutine cutsceneRoutine;
        private bool hasPlayed;

        public void ConfigureLegacyReferences(GameObject legacyDeathCutsceneObject, float legacyDestroyDelay)
        {
            if (deathCutsceneObject == null)
            {
                deathCutsceneObject = legacyDeathCutsceneObject;
            }

            if (legacyDestroyDelay > 0f && Mathf.Approximately(destroyDelayIfNoCutscene, 3f))
            {
                destroyDelayIfNoCutscene = legacyDestroyDelay;
            }

            ResolveDirector();
        }

        private void Awake()
        {
            ResolveDirector();
        }

        public void PlayDeathCutscene(GameObject bossObject)
        {
            if (hasPlayed)
            {
                return;
            }

            hasPlayed = true;
            ResolveDirector();

            if (deathCutsceneObject == null && deathCutsceneDirector == null)
            {
                if (destroyBossWhenNoCutscene && bossObject != null)
                {
                    Destroy(bossObject, Mathf.Max(0f, destroyDelayIfNoCutscene));
                }

                return;
            }

            if (cutsceneRoutine != null)
            {
                StopCoroutine(cutsceneRoutine);
            }

            cutsceneRoutine = StartCoroutine(PlayDeathCutsceneRoutine());
        }

        private IEnumerator PlayDeathCutsceneRoutine()
        {
            if (deathCutsceneObject != null && activateCutsceneObject)
            {
                deathCutsceneObject.SetActive(true);
            }

            if (deathCutsceneDirector != null)
            {
                if (resetDirectorTime)
                {
                    deathCutsceneDirector.time = 0d;
                    deathCutsceneDirector.Evaluate();
                }

                if (playDirector)
                {
                    deathCutsceneDirector.Play();
                }
            }

            yield break;
        }

        private void ResolveDirector()
        {
            if (deathCutsceneDirector != null)
            {
                return;
            }

            if (deathCutsceneObject != null)
            {
                deathCutsceneDirector = deathCutsceneObject.GetComponent<PlayableDirector>();
            }
        }
    }
}
