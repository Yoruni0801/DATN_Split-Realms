using UnityEngine;
using DG.Tweening;

public class WaterDrain : MonoBehaviour
{
    [Header("Cài đặt hạ nước")]
    [SerializeField] private float drainDistance = 5f;
    [SerializeField] private float duration = 2.5f;    
    
    private bool _isDraining = false; 
    private bool _wasTriggered = false;

    public void StartDrain() 
    {
        _wasTriggered = true;
        if (gameObject.activeInHierarchy)
        {
            ExecuteDrain();
        }
        else
        {
            ExecuteDrainInstant();
        }
    }

    private void OnEnable()
    {
        if (_wasTriggered && !_isDraining)
        {
            ExecuteDrainInstant();
        }
    }

    private void ExecuteDrain()
    {
        _isDraining = true;

        if (TryGetComponent<BoxCollider2D>(out var collider))
        {
            collider.enabled = false;
        }
        
        // Hạ nước xuống mượt mà
        transform.DOMoveY(transform.position.y - drainDistance, duration)
            .SetEase(Ease.InQuad) 
            .OnComplete(() => {
                Destroy(gameObject);
            });
    }

    private void ExecuteDrainInstant()
    {
        _isDraining = true;

        if (TryGetComponent<BoxCollider2D>(out var collider))
        {
            collider.enabled = false;
        }

        transform.position -= new Vector3(0f, drainDistance, 0f);
        Destroy(gameObject);
    }
}