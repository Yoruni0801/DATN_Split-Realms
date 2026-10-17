using UnityEngine;

namespace Tien.Minigames
{
    public class StoneGate : MonoBehaviour
    {
        [Tooltip("Kéo UI Panel của cổng vào đây")]
        public StoneGateUI gateUI;

        [Tooltip("Kéo bức tường tàng hình ở Map 2 vào đây. Tường này ngăn người chơi lách luật bên Map 2. Nó sẽ bị vô hiệu hóa khi giải xong cổng.")]
        public GameObject map2InvisibleWall;

        private bool isPlayerNear = false;
        private Player currentPlayer;

        void Update()
        {
            if (isPlayerNear && Input.GetKeyDown(KeyCode.E) && !StoneManager.Instance.isGateSolved)
            {
                if (!StoneManager.Instance.isQuestStarted)
                {
                    // Nếu chưa nhận quest thì báo
                    if (QuestUIManager.Instance != null)
                    {
                        QuestUIManager.Instance.ShowDialogue("Cổng bị khóa. Có lẽ tôi nên tìm ai đó quanh đây để hỏi thăm.", 3f);
                    }
                    return;
                }

                if (gateUI != null)
                {
                    gateUI.OpenUI(this, currentPlayer);
                }
            }
        }

        public void OpenGate()
        {
            // Ẩn cổng ở Map 1
            gameObject.SetActive(false);

            // Đồng thời ẩn luôn bức tường tàng hình chắn đường ở Map 2
            if (map2InvisibleWall != null)
            {
                map2InvisibleWall.SetActive(false);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                isPlayerNear = true;
                currentPlayer = other.GetComponent<Player>();
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
        
        // Hỗ trợ cả 3D nếu cần
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                isPlayerNear = true;
                currentPlayer = other.GetComponent<Player>();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                isPlayerNear = false;
                currentPlayer = null;
            }
        }
    }
}
