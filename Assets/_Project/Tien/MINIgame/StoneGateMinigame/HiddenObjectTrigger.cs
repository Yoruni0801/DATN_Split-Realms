using UnityEngine;

namespace Tien.Minigames
{
    public class HiddenObjectTrigger : MonoBehaviour
    {
        [Header("Tương tác")]
        [Tooltip("Khoảng cách tối đa để có thể tương tác")]
        public float interactDistance = 2f;

        [Tooltip("Kéo Minigame Tìm Đồ (HiddenObjectMinigame) vào đây")]
        public HiddenObjectMinigame minigame;

        [Header("Thông báo")]
        [Tooltip("Nếu người chơi đã tìm đủ đồ, hiển thị câu này")]
        public string completedText = "Không còn gì thú vị ở đây nữa.";

        private bool isPlayerNear = false;
        private Player currentPlayer;

        void Update()
        {
            if (isPlayerNear && Input.GetKeyDown(KeyCode.E))
            {
                if (minigame != null)
                {
                    minigame.OpenMinigame(currentPlayer);
                }
                else
                {
                    Debug.LogWarning($"[{gameObject.name}] Chưa gán HiddenObjectMinigame cho trigger!");
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                isPlayerNear = true;
                currentPlayer = other.GetComponent<Player>();

                // Hiện dấu chấm than hoặc hướng dẫn "Bấm E để tìm kiếm" (nếu có UI nhắc nhở)
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                isPlayerNear = false;
                currentPlayer = null;
            }
        }
    }
}
