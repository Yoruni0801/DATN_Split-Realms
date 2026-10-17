using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

public class CutsceneAutoNext : MonoBehaviour
{
    [Header("Cutscene References")]
    [SerializeField] private PlayableDirector currentCutscene;
    [SerializeField] private PlayableDirector nextCutscene;

    [Header("Optional Objects")]
    [SerializeField] private GameObject currentCutsceneObject;
    [SerializeField] private GameObject nextCutsceneObject;

    [Header("Settings")]
    [SerializeField] private bool playCurrentOnStart = false;
    [SerializeField] private bool disableCurrentAfterFinished = true;
    [SerializeField] private bool resetNextCutsceneTime = true;

    private Coroutine waitCoroutine;

    private void Start()
    {
        if (playCurrentOnStart)
        {
            PlayCurrentCutscene();
        }
    }

    public void PlayCurrentCutscene()
    {
        if (currentCutscene == null)
        {
            Debug.LogWarning("[CutsceneAutoNext] Missing current cutscene.");
            return;
        }

        if (currentCutsceneObject != null)
        {
            currentCutsceneObject.SetActive(true);
        }

        currentCutscene.Stop();
        currentCutscene.time = 0;
        currentCutscene.Play();

        if (waitCoroutine != null)
        {
            StopCoroutine(waitCoroutine);
        }

        waitCoroutine = StartCoroutine(WaitForCurrentCutsceneFinished());
    }

    private IEnumerator WaitForCurrentCutsceneFinished()
    {
        // Đợi cutscene bắt đầu chạy
        yield return null;

        while (currentCutscene != null &&
               currentCutscene.state == PlayState.Playing)
        {
            yield return null;
        }

        if (disableCurrentAfterFinished)
        {
            GameObject target = currentCutsceneObject != null
                ? currentCutsceneObject
                : currentCutscene.gameObject;

            target.SetActive(false);
        }

        PlayNextCutscene();

        waitCoroutine = null;
    }

    private void PlayNextCutscene()
    {
        if (nextCutscene == null)
        {
            return;
        }

        if (nextCutsceneObject != null)
        {
            nextCutsceneObject.SetActive(true);
        }

        nextCutscene.Stop();

        if (resetNextCutsceneTime)
        {
            nextCutscene.time = 0;
        }

        nextCutscene.Play();
    }

    private void OnDisable()
    {
        if (waitCoroutine != null)
        {
            StopCoroutine(waitCoroutine);
            waitCoroutine = null;
        }
    }
}