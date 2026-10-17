using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Playables;

public class WorldSwapCutscene : MonoBehaviour
{
    public bool IsPlaying => isPlaying;

    [Header("Timing")]
    #pragma warning disable CS0414
    [SerializeField] private float fadeInDuration = 0.15f;
    #pragma warning restore CS0414
    [SerializeField] private float holdDuration = 0.25f;
    #pragma warning disable CS0414
    [SerializeField] private float fadeOutDuration = 0.2f;
    #pragma warning restore CS0414

    [Header("Optional FX")]
    [SerializeField] private PlayableDirector timelineDirector;
    [SerializeField] private MonoBehaviour cutsceneEngineBehaviour;
    [SerializeField] private bool forcePlayOncePerTrigger = true;
    [SerializeField] private Animator cutsceneAnimator;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private bool waitTimelineToFinish = true;

    private bool isPlaying;

    public IEnumerator Play()
    {
        if (isPlaying) yield break;
        isPlaying = true;

        if (cutsceneAnimator != null)
        {
            cutsceneAnimator.SetTrigger("Play");
        }

        if (timelineDirector != null)
        {
            if (forcePlayOncePerTrigger)
            {
                timelineDirector.extrapolationMode = DirectorWrapMode.None;
            }

            timelineDirector.Stop();
            timelineDirector.time = 0;
            timelineDirector.Play();
        }
        else if (cutsceneEngineBehaviour != null)
        {
            MethodInfo playMethod = cutsceneEngineBehaviour.GetType().GetMethod(
                "Play",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

            if (playMethod != null)
            {
                playMethod.Invoke(cutsceneEngineBehaviour, null);
            }
            else
            {
                Debug.LogWarning($"WorldSwapCutscene: Method 'Play' not found on {cutsceneEngineBehaviour.GetType().Name}");
            }
        }

        if (sfxSource != null)
        {
            sfxSource.Play();
        }

        if (timelineDirector != null && waitTimelineToFinish)
        {
            while (timelineDirector.state == PlayState.Playing)
            {
                yield return null;
            }
        }
        else if (holdDuration > 0f)
        {
            yield return new WaitForSeconds(holdDuration);
        }

        isPlaying = false;
    }
}
