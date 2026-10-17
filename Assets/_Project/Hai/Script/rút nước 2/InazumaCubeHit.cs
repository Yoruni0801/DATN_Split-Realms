using UnityEngine;

public class InazumaCubeHit : MonoBehaviour
{
    [Tooltip("Set 1, 2 or 3 to match this cube order in the puzzle")]
    [SerializeField] private int cubeID = 1;

    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color gizmoColor = new Color(1f, 0.92f, 0.16f, 0.75f);

    private InazumaPuzzleManager manager;

    private void Start()
    {
        manager = FindFirstObjectByType<InazumaPuzzleManager>();
    }

    public void TakeDamage(int damage)
    {
        TriggerRotationLogic();
    }

    public void OnMeleeHit()
    {
        TriggerRotationLogic();
    }

    private void TriggerRotationLogic()
    {
        if (manager == null)
        {
            return;
        }

        if (cubeID == 1)
        {
            manager.OnCube1Hit();
        }
        else if (cubeID == 2)
        {
            manager.OnCube2Hit();
        }
        else if (cubeID == 3)
        {
            manager.OnCube3Hit();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
        {
            return;
        }
        Collider2D targetCollider = GetComponent<Collider2D>();
        if (targetCollider == null)
        {
            return;
        }

        Bounds bounds = targetCollider.bounds;
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(bounds.center, bounds.size);

        Color fillColor = gizmoColor;
        fillColor.a *= 0.2f;
        Gizmos.color = fillColor;
        Gizmos.DrawCube(bounds.center, bounds.size);
    }
}
