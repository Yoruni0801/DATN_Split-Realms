using UnityEngine;
using Tien.AdvancedLoading.Core;

namespace Tien.Minigame
{
    [RequireComponent(typeof(Collider2D))]
    public class MinigameNPC : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private string minigameSceneName = "MiniGame";
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private KeyCode interactKey = KeyCode.E;

        [Header("UI Prompt")]
        [SerializeField] private GameObject promptUI;

        private bool playerInRange;
        private Transform playerTransform;

        private void Reset()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void Awake()
        {
            if (promptUI != null) promptUI.SetActive(false);
        }

        private void Update()
        {
            if (playerInRange && Input.GetKeyDown(interactKey))
            {
                if (playerTransform != null)
                {
                    MinigameState.PlayerMapPosition = playerTransform.position;
                    MinigameState.IsReturningFromMinigame = true;
                }

                if (!string.IsNullOrEmpty(minigameSceneName))
                {
                    SceneLoader.Load(minigameSceneName);
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag(playerTag))
            {
                playerInRange = true;
                playerTransform = other.transform;
                if (promptUI != null) promptUI.SetActive(true);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag(playerTag))
            {
                playerInRange = false;
                playerTransform = null;
                if (promptUI != null) promptUI.SetActive(false);
            }
        }
    }
}
