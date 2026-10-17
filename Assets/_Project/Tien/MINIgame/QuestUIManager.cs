using UnityEngine;
using UnityEngine.UI;

namespace Tien.Minigames
{
    public class QuestUIManager : MonoBehaviour
    {
        public static QuestUIManager Instance { get; private set; }

        [Header("UI Elements")]
        [Tooltip("Panel chứa toàn bộ UI nhiệm vụ")]
        public GameObject questPanel;
        [Tooltip("Text hiển thị mô tả nhiệm vụ")]
        public Text questDescriptionText;
        [Tooltip("Text hiển thị tiến độ (ví dụ: 0/3)")]
        public Text questProgressText;

        [Header("Dialogue UI (Hội thoại)")]
        [Tooltip("Panel chứa khung chat của NPC")]
        public GameObject dialoguePanel;
        [Tooltip("Text hiển thị lời thoại của NPC")]
        public Text dialogueText;
        [Tooltip("Image hiển thị hình ảnh đi kèm (Ví dụ: Quyển sách, cuộn giấy)")]
        public Image dialogueImageDisplay;
        [Tooltip("Image hiển thị Avatar của người nói")]
        public Image avatarImageDisplay;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Ẩn panel ban đầu
            if (questPanel != null) questPanel.SetActive(false);
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
            if (dialogueImageDisplay != null) dialogueImageDisplay.gameObject.SetActive(false);
            if (avatarImageDisplay != null) avatarImageDisplay.gameObject.SetActive(false);
        }

        public void ShowDialogue(string message, float duration = 3.5f, Sprite bigImage = null, Sprite avatar = null)
        {
            if (dialoguePanel != null) dialoguePanel.SetActive(true);
            if (dialogueText != null) dialogueText.text = message;

            // Xử lý hiển thị ảnh lớn (sách, cuộn giấy)
            if (dialogueImageDisplay != null)
            {
                if (bigImage != null)
                {
                    dialogueImageDisplay.sprite = bigImage;
                    dialogueImageDisplay.gameObject.SetActive(true);
                }
                else
                {
                    dialogueImageDisplay.gameObject.SetActive(false);
                }
            }

            // Xử lý hiển thị avatar
            if (avatarImageDisplay != null)
            {
                if (avatar != null)
                {
                    avatarImageDisplay.sprite = avatar;
                    avatarImageDisplay.gameObject.SetActive(true);
                }
                else
                {
                    avatarImageDisplay.gameObject.SetActive(false);
                }
            }
            else
            {
                Debug.LogWarning("[QuestUIManager] Bạn chưa kéo thả AvatarImage vào ô avatarImageDisplay! Hãy kiểm tra lại Inspector.");
            }

            StopAllCoroutines();
            StartCoroutine(HideDialogueAfterDelay(duration));
        }

        private System.Collections.IEnumerator HideDialogueAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
            if (dialogueImageDisplay != null) dialogueImageDisplay.gameObject.SetActive(false);
            if (avatarImageDisplay != null) avatarImageDisplay.gameObject.SetActive(false);
        }

        public void ShowQuest(string description, int currentAmount, int requiredAmount)
        {
            if (questPanel != null) questPanel.SetActive(true);
            if (questDescriptionText != null) questDescriptionText.text = description;
            UpdateProgress(currentAmount, requiredAmount);
        }

        public void UpdateProgress(int currentAmount, int requiredAmount)
        {
            if (questProgressText != null)
            {
                questProgressText.text = $"{currentAmount} / {requiredAmount}";
            }
        }

        public void HideQuest()
        {
            if (questPanel != null) questPanel.SetActive(false);
        }
    }
}
