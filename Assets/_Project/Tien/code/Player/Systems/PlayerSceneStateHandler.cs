using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PlayerSceneStateHandler
{
    private readonly Transform transform;
    private readonly Rigidbody2D rb;

    public PlayerSceneStateHandler(Transform transform, Rigidbody2D rb)
    {
        this.transform = transform;
        this.rb = rb;
    }

    public void RestorePosition()
    {
        RestorePosition(GetSceneName());
    }

    public void RestorePosition(string sceneName)
    {
        Vector3 targetPosition;
        if (SceneRuntimeStateManager.TryGetCheckpoint(sceneName, out Vector3 checkpointPosition))
        {
            targetPosition = checkpointPosition;
        }
        else if (SceneRuntimeStateManager.TryGetPlayerPosition(sceneName, out Vector3 savedPosition))
        {
            targetPosition = savedPosition;
        }
        else
        {
            PlayerStartPoint startPoint = FindStartPoint(sceneName);
            if (startPoint == null)
            {
                return;
            }
            targetPosition = startPoint.transform.position;
        }

        ApplyPosition(targetPosition);
    }

    public void RestoreStartPoint(string sceneName)
    {
        PlayerStartPoint startPoint = FindStartPoint(sceneName);
        if (startPoint == null)
        {
            return;
        }

        ApplyPosition(startPoint.transform.position);
    }

    private void ApplyPosition(Vector3 targetPosition)
    {
        transform.position = targetPosition;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    public void SavePosition()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        SceneRuntimeStateManager.SavePlayerPosition(GetSceneName(), transform.position);
    }

    public void SavePosition(Vector3 position)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        SceneRuntimeStateManager.SavePlayerPosition(GetSceneName(), position);
    }

    private string GetSceneName()
    {
        // Each scene owns its own placed player, so always read the scene the object
        // actually belongs to (not the currently active scene). This stays correct even
        // while a transition is mid-flight. If the scene is invalid (e.g. during unload),
        // return empty so callers like SavePosition gracefully no-op.
        Scene scene = transform != null ? transform.gameObject.scene : default;
        return scene.IsValid() && !string.IsNullOrWhiteSpace(scene.name)
            ? scene.name
            : string.Empty;
    }

    private static PlayerStartPoint FindStartPoint(string sceneName)
    {
        PlayerStartPoint[] startPoints = Object.FindObjectsByType<PlayerStartPoint>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < startPoints.Length; i++)
        {
            PlayerStartPoint startPoint = startPoints[i];
            if (startPoint != null && startPoint.gameObject.scene.name == sceneName)
            {
                return startPoint;
            }
        }

        return null;
    }
}
