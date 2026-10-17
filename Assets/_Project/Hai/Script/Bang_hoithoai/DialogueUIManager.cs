using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogueUIManager : MonoBehaviour
{
    public static DialogueUIManager Instance { get; private set; }

    [Header("Thành phần UI Khung Thoại")]
    [Tooltip("Khung chứa hội thoại chính (Dialogue Panel)")]
    [SerializeField] private GameObject dialoguePanel;
    
    [Tooltip("Text hiển thị nội dung thoại (TextMeshPro)")]
    [SerializeField] private TMP_Text dialogueText;

    [Header("Thành phần Tên Nhân Vật")]
    [Tooltip("Khung chứa tên nhân vật (Name Frame) - Tùy chọn, sẽ ẩn nếu không có tên")]
    [SerializeField] private GameObject speakerNameFrame;

    [Tooltip("Text hiển thị tên nhân vật nói (TextMeshPro)")]
    [SerializeField] private TMP_Text speakerNameText;

    [Header("Thành phần Ảnh Đại Diện")]
    [Tooltip("Khung chứa ảnh chân dung (Portrait Frame) - Tùy chọn, sẽ ẩn nếu không có ảnh")]
    [SerializeField] private GameObject portraitFrame;

    [Tooltip("Ảnh hiển thị chân dung nhân vật (Image)")]
    [SerializeField] private Image portraitImage;

    [Header("Cấu hình Chữ Chạy (Typewriter)")]
    [Tooltip("Tốc độ hiện từng chữ (giây/ký tự)")]
    [SerializeField] private float typingSpeed = 0.03f;

    private Coroutine typewriterCoroutine;
    private Coroutine hideCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Ẩn bảng hội thoại khi bắt đầu game
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
    }

    /// <summary>
    /// Hiển thị hội thoại lên màn hình với hiệu ứng chữ chạy và ảnh đại diện.
    /// </summary>
    /// <param name="speakerName">Tên nhân vật nói</param>
    /// <param name="message">Nội dung thoại</param>
    /// <param name="duration">Thời gian hiển thị (giây)</param>
    /// <param name="portrait">Ảnh chân dung (nếu có)</param>
    public void ShowDialogue(string speakerName, string message, float duration, Sprite portrait = null)
    {
        if (dialoguePanel == null || dialogueText == null)
        {
            Debug.LogWarning("[DialogueUIManager] Chưa gán DialoguePanel hoặc DialogueText trong Inspector!");
            return;
        }

        dialoguePanel.SetActive(true);

        // 1. Xử lý hiển thị Tên nhân vật nói
        if (speakerNameText != null)
        {
            if (!string.IsNullOrEmpty(speakerName))
            {
                speakerNameText.text = speakerName;
                if (speakerNameFrame != null) speakerNameFrame.SetActive(true);
            }
            else
            {
                speakerNameText.text = "";
                if (speakerNameFrame != null) speakerNameFrame.SetActive(false);
            }
        }

        // 2. Xử lý hiển thị Ảnh chân dung & Khung chân dung
        if (portraitImage != null)
        {
            if (portrait != null)
            {
                portraitImage.gameObject.SetActive(true);
                portraitImage.sprite = portrait;
                if (portraitFrame != null) portraitFrame.SetActive(true);
            }
            else
            {
                portraitImage.gameObject.SetActive(false);
                if (portraitFrame != null) portraitFrame.SetActive(false);
            }
        }
        else if (portraitFrame != null)
        {
            portraitFrame.SetActive(portrait != null);
        }

        // 3. Chạy hiệu ứng chữ chạy (Typewriter) từ trái sang phải
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
        }
        typewriterCoroutine = StartCoroutine(TypewriterRoutine(message));

        // 4. Lên lịch ẩn bảng hội thoại sau khoảng thời gian hiển thị
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
        }
        hideCoroutine = StartCoroutine(HideDialogueAfterDelay(duration));
    }

    private IEnumerator TypewriterRoutine(string message)
    {
        dialogueText.text = "";
        WaitForSeconds waitTyping = new WaitForSeconds(typingSpeed);
        for (int i = 0; i < message.Length; i++)
        {
            dialogueText.text = message.Substring(0, i + 1);
            yield return waitTyping;
        }
        
        typewriterCoroutine = null;
    }

    private IEnumerator HideDialogueAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
    }
}
