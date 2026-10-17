using Tien.Minigames;
using UnityEngine;

public class BridgeRepairMinigameTrigger : MonoBehaviour
{
    [SerializeField] private BridgeRepairMinigame minigame;
    [SerializeField] private bool openOnlyOnce = true;

    private bool hasOpened;

    private void Awake()
    {
        if (minigame == null)
        {
            minigame = FindFirstObjectByType<BridgeRepairMinigame>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (openOnlyOnce && hasOpened)
        {
            return;
        }

        if (!IsPlayer(other) || minigame == null || minigame.isCompleted)
        {
            return;
        }

        hasOpened = true;
        minigame.gameObject.SetActive(true);
        minigame.OpenPuzzle();
    }

    private bool IsPlayer(Collider2D other)
    {
        return other != null
            && (other.CompareTag("Player") || other.GetComponentInParent<Player>() != null);
    }
}
