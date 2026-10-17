using UnityEngine;

namespace Tien.Minigames
{
    public class CombinationLockNPC : MonoBehaviour
    {
        [Header("UI Prompt & GameObject")]
        [Tooltip("Popup hiển thị phím E khi đứng gần NPC")]
        public GameObject ePromptUI;
        [Tooltip("GameObject / UI Panel được mở ra khi nhấn E (mặc định ban đầu sẽ tắt)")]
        public GameObject targetGameObject;

        [Header("Cài đặt Hội thoại")]
        [Tooltip("Các câu thoại NPC sẽ lần lượt nói ra")]
        [TextArea(2, 3)]
        public string[] dialogueLines = new string[] 
        {
            "Bắt đầu bằng thứ luôn mở khi ánh sáng xuất hiện.",
            "Tiếp theo là thứ rơi xuống trước khi mùa đông tới.",
            "Kết thúc bằng thứ các Pharaoh mang theo để đánh bại thần chết."
        };

        [Tooltip("Hình ảnh đi kèm cho mỗi câu thoại (Số lượng ô phải bằng số câu thoại. Có thể dùng chung 1 ảnh cho nhiều câu)")]
        public Sprite[] dialogueImages;

        [Tooltip("Thời gian hiển thị mỗi câu thoại (giây)")]
        public float timePerLine = 4f;
        
        [Tooltip("Kéo Cánh Cửa (CombinationLockGate) vào ô này để NPC mở khóa nó!")]
        public CombinationLockGate targetGate;

        private bool _isPlayerNear = false;
        private bool _isTalking = false; // Cờ để tránh bấm E nhiều lần liên tục

        private void Start()
        {
            if (ePromptUI != null) ePromptUI.SetActive(false);
            if (targetGameObject != null) targetGameObject.SetActive(false);
        }

        void Update()
        {
            // Bấm E khi đứng gần và không đang nói chuyện
            if (_isPlayerNear && !_isTalking && Input.GetKeyDown(KeyCode.E))
            {
                if (ePromptUI != null) ePromptUI.SetActive(false);
                if (targetGameObject != null) targetGameObject.SetActive(true);

                StartCoroutine(PlayDialogueRoutine());
            }
        }

        public void CloseTargetGameObject()
        {
            if (targetGameObject != null) targetGameObject.SetActive(false);
            if (_isPlayerNear && !_isTalking && ePromptUI != null) ePromptUI.SetActive(true);
        }

        private System.Collections.IEnumerator PlayDialogueRoutine()
        {
            _isTalking = true;

            for (int i = 0; i < dialogueLines.Length; i++)
            {
                if (QuestUIManager.Instance != null)
                {
                    // Lấy ảnh tương ứng (nếu có)
                    Sprite imgToDisplay = null;
                    if (dialogueImages != null && dialogueImages.Length > i)
                    {
                        imgToDisplay = dialogueImages[i];
                    }

                    QuestUIManager.Instance.ShowDialogue(dialogueLines[i], timePerLine, imgToDisplay);
                }
                else
                {
                    Debug.Log("NPC: " + dialogueLines[i]);
                }
                
                // Nghỉ chờ câu thoại này nói xong rồi mới nói câu tiếp theo
                yield return new WaitForSeconds(timePerLine);
            }

            // Đánh dấu là đã biết pass sau khi nghe hết gợi ý, cho phép mở cửa
            if (targetGate != null)
            {
                targetGate.hasLearnedPassword = true;
            }
            else
            {
                Debug.LogWarning("[CombinationLockNPC] Bạn chưa kéo Cánh Cửa vào ô targetGate trong Inspector của NPC!");
            }

            _isTalking = false; // Cho phép bấm E để nghe lại từ đầu nếu muốn
            if (_isPlayerNear && ePromptUI != null) ePromptUI.SetActive(true);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                _isPlayerNear = true;
                if (ePromptUI != null) ePromptUI.SetActive(true);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                _isPlayerNear = false;
                if (ePromptUI != null) ePromptUI.SetActive(false);
            }
        }
    }
}
