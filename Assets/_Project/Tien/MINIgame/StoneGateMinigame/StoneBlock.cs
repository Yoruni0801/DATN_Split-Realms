using UnityEngine;

namespace Tien.Minigames
{
    public class StoneBlock : MonoBehaviour
    {
        [Tooltip("Khoảng cách tối đa để nhặt được (Dùng phòng trường hợp Trigger bị lỗi)")]
        public float pickupRadius = 3f;
        
        private Transform playerTrans;
        private bool isPlayerInTrigger = false;

        void Start()
        {
            // Tìm Player bằng Component trước cho chắc chắn, nếu không thì dùng Tag
            Player p = Object.FindFirstObjectByType<Player>();
            if (p != null) playerTrans = p.transform;
            else
            {
                GameObject pObj = GameObject.FindGameObjectWithTag("Player");
                if (pObj != null) playerTrans = pObj.transform;
            }
            
            // Cảnh báo nếu bạn quên kéo StoneManager vào Scene
            if (StoneManager.Instance == null)
            {
                Debug.LogError("StoneBlock: Không tìm thấy StoneManager! Bạn đã tạo một GameObject chứa StoneManager trong Scene chưa?");
            }
        }

        void Update()
        {
            bool isPlayerNear = isPlayerInTrigger;

            // Tính toán khoảng cách (bỏ qua trục Z)
            if (playerTrans != null)
            {
                float distance = Vector2.Distance(transform.position, playerTrans.position);
                if (distance <= pickupRadius)
                {
                    isPlayerNear = true;
                }
            }

            // Người chơi nhấn E để nhặt khối đá
            if (isPlayerNear && Input.GetKeyDown(KeyCode.E))
            {
                Debug.Log("StoneBlock: Máy đã nhận phím E từ người chơi!");

                if (StoneManager.Instance == null)
                {
                    Debug.LogError("StoneBlock: StoneManager bị NULL, không thể nhặt!");
                    return;
                }

                if (!StoneManager.Instance.isQuestStarted)
                {
                    Debug.Log("StoneBlock: Chưa nhận nhiệm vụ từ NPC (isQuestStarted = false) nên không được nhặt.");
                    if (QuestUIManager.Instance != null)
                    {
                        QuestUIManager.Instance.ShowDialogue("Có cục đá gì ở đây này, nhưng tôi chưa cần nó lúc này.", 3f);
                    }
                    return; 
                }

                StoneManager.Instance.AddStone();
                Destroy(gameObject); 
            }
        }

        // Hỗ trợ thêm cả Trigger 2D
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player") || other.GetComponent<Player>() != null) 
            {
                isPlayerInTrigger = true;
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player") || other.GetComponent<Player>() != null) 
            {
                isPlayerInTrigger = false;
            }
        }

        // Vẽ vòng tròn vàng trong Scene để bạn biết phạm vi nhặt là bao xa
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, pickupRadius);
        }
    }
}
