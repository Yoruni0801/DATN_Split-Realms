using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public sealed class ReturnToCheckpointTrigger : MonoBehaviour
{
    [SerializeField, Min(0f)] private float reentryDelay = 2f;

    private float nextAvailableTime;

    private void Reset()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (Time.time < nextAvailableTime)
        {
            return;
        }

        PlayerHealth playerHealth = collision.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null || playerHealth.IsDead)
        {
            return;
        }

        Vector3 checkpointPosition = playerHealth.CurrentCheckpointPosition;
        string sceneName = SceneManager.GetActiveScene().name;
        if (SceneRuntimeStateManager.TryGetCheckpoint(sceneName, out Vector3 savedCheckpoint))
        {
            checkpointPosition = savedCheckpoint;
        }

        playerHealth.UpdateCheckpoint(checkpointPosition);

        Rigidbody2D playerBody = playerHealth.GetComponent<Rigidbody2D>();
        if (playerBody != null)
        {
            playerBody.position = checkpointPosition;
            playerBody.linearVelocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
        }
        else
        {
            playerHealth.transform.position = checkpointPosition;
        }

        nextAvailableTime = Time.time + reentryDelay;
    }
}
