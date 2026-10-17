using System.Collections;
using UnityEngine;

public class SceneGateSpawnPoint2D : MonoBehaviour
{
    [SerializeField] private string gateId;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool matchRotation;
    [SerializeField] private int waitFramesBeforeSpawn = 1;
    [SerializeField] private float retryDuration = 1f;

    private IEnumerator Start()
    {
        if (!SceneGateTravelState.HasPendingSpawnGate
            || !SceneGateTravelState.MatchesPendingSpawnGate(gateId))
        {
            yield break;
        }

        for (int i = 0; i < waitFramesBeforeSpawn; i++)
        {
            yield return null;
        }

        float endTime = Time.realtimeSinceStartup + retryDuration;
        GameObject player = null;

        while (player == null && Time.realtimeSinceStartup <= endTime)
        {
            player = GameObject.FindGameObjectWithTag(playerTag);
            if (player == null)
            {
                yield return null;
            }
        }

        if (player == null)
        {
            Debug.LogWarning($"No player with tag '{playerTag}' was found for gate spawn '{gateId}'.", this);
            yield break;
        }

        SpawnPlayer(player.transform);
        SceneGateTravelState.ClearPendingSpawnGate();
    }

    private void SpawnPlayer(Transform player)
    {
        player.position = transform.position;

        if (matchRotation)
        {
            player.rotation = transform.rotation;
        }

        Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
        if (playerRb == null)
        {
            playerRb = player.GetComponentInChildren<Rigidbody2D>();
        }
        if (playerRb == null)
        {
            playerRb = player.GetComponentInParent<Rigidbody2D>();
        }

        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector2.zero;
            playerRb.angularVelocity = 0f;
        }
    }
}
