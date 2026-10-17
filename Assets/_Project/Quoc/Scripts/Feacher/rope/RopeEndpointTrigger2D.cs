using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class RopeEndpointTrigger2D : MonoBehaviour
{
    [SerializeField] private RopeSwing2D rope;

    private void Reset()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;
        rope = GetComponentInParent<RopeSwing2D>();
    }

    private void Awake()
    {
        if (rope == null)
        {
            rope = GetComponentInParent<RopeSwing2D>();
        }

        Collider2D trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        rope?.TryAttachFromEndpoint(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        rope?.TryAttachFromEndpoint(other);
    }
}
