using UnityEngine;

namespace Tien.Minigames
{
    public class GateQuestNPC : MonoBehaviour
    {
        [Header("Hội thoại NPC")]
        public string beforeQuestText = "Cánh cổng đá phía trước đang đóng. Hình như cần tìm những khối đá bị mất để mở nó.";
        public float textDuration = 3f;

        private bool isPlayerNear = false;

        void Update()
        {
            if (isPlayerNear && Input.GetKeyDown(KeyCode.E))
            {
                // Gọi UI hiện hội thoại (Sử dụng QuestUIManager có sẵn của bạn)
                if (QuestUIManager.Instance != null)
                {
                    QuestUIManager.Instance.ShowDialogue(beforeQuestText, textDuration);
                }

                // Nếu chưa bắt đầu nhiệm vụ thì bắt đầu
                if (StoneManager.Instance != null && !StoneManager.Instance.isQuestStarted)
                {
                    StoneManager.Instance.StartQuest();
                    Debug.Log("Đã nhận nhiệm vụ từ NPC!");
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player")) isPlayerNear = true;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player")) isPlayerNear = false;
        }
    }
}
