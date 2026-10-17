using UnityEngine;
using UnityEngine.Playables;

[RequireComponent(typeof(Collider2D))]
public sealed class TimelineTrigger2DD : MonoBehaviour
{
    [Header("Timeline")]
    [SerializeField] private PlayableDirector timelineDirector;
    [SerializeField] private bool playOnce = true;
    [SerializeField] private bool restartIfPlaying;

    [Header("Trigger Filter")]
    [SerializeField] private string playerTag = "Player";

    private bool hasTriggered;

    private void Reset()
    {
        timelineDirector = GetComponent<PlayableDirector>();

        Collider2D triggerCollider = GetComponent<Collider2D>();
        triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered || !IsPlayer(other))
        {
            return;
        }

        PlayTimeline();
    }

    public void PlayTimeline()
    {
        if (timelineDirector == null)
        {
            Debug.LogWarning($"[{nameof(TimelineTrigger2D)}] Missing PlayableDirector reference.", this);
            return;
        }

        if (timelineDirector.state == PlayState.Playing)
        {
            if (!restartIfPlaying)
            {
                return;
            }

            timelineDirector.Stop();
        }

        timelineDirector.Play();

        if (playOnce)
        {
            hasTriggered = true;
        }
    }

    public void ResetTrigger()
    {
        hasTriggered = false;
    }

    private bool IsPlayer(Collider2D other)
    {
        if (string.IsNullOrEmpty(playerTag))
        {
            return true;
        }

        return other.CompareTag(playerTag)
            || (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(playerTag))
            || other.transform.root.CompareTag(playerTag);
    }
}
