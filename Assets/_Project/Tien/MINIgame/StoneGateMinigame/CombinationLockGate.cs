using UnityEngine;
using DG.Tweening;

namespace Tien.Minigames
{
    public class CombinationLockGate : MonoBehaviour
    {
        [Header("UI Prompt & GameObject")]
        [Tooltip("Popup hiển thị phím E khi đứng gần cổng")]
        public GameObject ePromptUI;
        [Tooltip("GameObject / UI Panel được mở ra khi tương tác (mặc định ban đầu sẽ tắt)")]
        public GameObject targetGameObject;

        [Header("Trạng thái (Không tick vào)")]
        [Tooltip("Được mở bởi NPC khi nói chuyện xong")]
        public bool hasLearnedPassword = false;
        public bool isGateOpen = false;

        private bool _isPlayerNear = false;
        private Player _currentPlayer;

        private void Start()
        {
            if (ePromptUI != null) ePromptUI.SetActive(false);
            if (targetGameObject != null) targetGameObject.SetActive(false);
        }

        void Update()
        {
            // Nếu người chơi đứng gần, cửa chưa mở và bấm E
            if (_isPlayerNear && !isGateOpen && Input.GetKeyDown(KeyCode.E))
            {
                if (ePromptUI != null) ePromptUI.SetActive(false);
                if (targetGameObject != null) targetGameObject.SetActive(true);

                // Kiểm tra xem đã nói chuyện NPC chưa
                if (!hasLearnedPassword)
                {
                    // Lỗi: Chưa nói chuyện -> Từ chối
                    if (QuestUIManager.Instance != null)
                    {
                        QuestUIManager.Instance.ShowDialogue("Cần tìm manh mối trước! Có lẽ nên hỏi ai đó xung quanh đây...", 3f);
                    }
                    else
                    {
                        Debug.Log("Cần tìm manh mối trước!");
                    }
                    return; // Dừng tại đây, không cho mở UI Ổ khóa
                }

                // Đã biết pass -> Mở giao diện (UI) Minigame
                if (CombinationLockUI.Instance != null)
                {
                    CombinationLockUI.Instance.OpenUI(this, _currentPlayer);
                }
                else
                {
                    Debug.LogError("Chưa đưa CombinationLockUI vào Scene!");
                }
            }
        }

        public void CloseTargetGameObject()
        {
            if (targetGameObject != null) targetGameObject.SetActive(false);
            if (_isPlayerNear && !isGateOpen && ePromptUI != null) ePromptUI.SetActive(true);
        }

        // Hàm này được CombinationLockUI gọi tới khi người chơi giải xong
        public void OpenGate()
        {
            if (isGateOpen) return;
            isGateOpen = true;

            if (ePromptUI != null) ePromptUI.SetActive(false);
            if (targetGameObject != null) targetGameObject.SetActive(false);

            // Tắt vật cản để người chơi có thể đi qua
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            // Animation mở cửa (di chuyển cánh cửa bay lên trên 5m rồi ẩn biến mất)
            transform.DOMoveY(transform.position.y + 5f, 2f).SetEase(Ease.InOutCubic).OnComplete(() => gameObject.SetActive(false));
            
            // Hiện thông báo mừng
            if (QuestUIManager.Instance != null)
            {
                QuestUIManager.Instance.ShowDialogue("Cửa đã mở! Bạn có thể đi tiếp.", 3f);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                _isPlayerNear = true;
                _currentPlayer = other.GetComponent<Player>(); // Lấy script của nhân vật để khóa di chuyển sau này
                if (!isGateOpen && ePromptUI != null) ePromptUI.SetActive(true);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                _isPlayerNear = false;
                _currentPlayer = null;
                if (ePromptUI != null) ePromptUI.SetActive(false);
            }
        }
    }
}
