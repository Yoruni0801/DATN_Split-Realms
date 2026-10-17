using UnityEngine;
using UnityEngine.Events;

public class TriggerEnvent : MonoBehaviour
{
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private UnityEvent onTrigger;

    private bool hasTriggered;

    private void OnTriggerEnter(Collider other)
    {
        InvokeTrigger();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(other.CompareTag("Player"))
        {
            InvokeTrigger();
        }
    }

    private void InvokeTrigger()
    {
        if (triggerOnce && hasTriggered)
        {
            return;
        }

        hasTriggered = true;
        onTrigger?.Invoke();
    }
}
