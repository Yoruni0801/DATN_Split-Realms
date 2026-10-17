using UnityEngine;
using Tien.AdvancedLoading.Core;

[RequireComponent(typeof(Collider2D))]
public class SceneTriggerr : MonoBehaviour
{
    [SerializeField] private string sceneName;
    [SerializeField] private bool resetSceneState = true;

    private bool hasTriggered;

    private void Reset()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered)
            return;

        Player player = other.GetComponent<Player>();
        if (player == null)
            player = other.GetComponentInParent<Player>();

        if (player == null && !other.CompareTag("Player"))
            return;

        hasTriggered = true;

        if (resetSceneState)
        {
            SceneRuntimeStateManager.ResetScene(sceneName);
        }

        // Tải scene đích với hiệu ứng mờ dần (Fade Transition) mượt mà 100% không giật lag
        SceneLoader.Load(sceneName);
    }
}