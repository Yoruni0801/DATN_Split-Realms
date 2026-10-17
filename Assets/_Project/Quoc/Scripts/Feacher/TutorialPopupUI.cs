using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TutorialPopupUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Text tiêu đề (Title)")]
    [SerializeField] private TextMeshProUGUI titleText;
    [Tooltip("Text nội dung hướng dẫn (Description / Content)")]
    [SerializeField] private TextMeshProUGUI descriptionText;
    [Tooltip("Text gợi ý phím bấm (Key Hint)")]
    [SerializeField] private TextMeshProUGUI keyHintText;
    [Tooltip("Hình ảnh minh họa / Icon (nếu có)")]
    [SerializeField] private Image iconImage;
    [Tooltip("GameObject cha chứa Icon (để ẩn nếu không có icon)")]
    [SerializeField] private GameObject iconContainer;
    [Tooltip("Nút đóng chữ X góc phải")]
    [SerializeField] private Button closeButton;
    [Tooltip("Nút xác nhận / Đã hiểu ở dưới")]
    [SerializeField] private Button confirmButton;

    [Header("Default Content")]
    [SerializeField] private string defaultTitle = "HƯỚNG DẪN";
    [TextArea(3, 8)]
    [SerializeField] private string defaultDescription = "Nhấn phím kỹ năng và vượt qua chướng ngại vật.";
    [SerializeField] private string defaultKeyHint = "Bấm [SPACE] hoặc [E] để đóng";
    [SerializeField] private Sprite defaultIcon;

    [Header("Events")]
    public UnityEvent onPopupOpened;
    public UnityEvent onPopupClosed;

    public static event System.Action OnAnyPopupClosed;
    private static readonly System.Collections.Generic.List<TutorialPopupUI> activePopups = new System.Collections.Generic.List<TutorialPopupUI>();

    public Button CloseButton => closeButton;
    public Button ConfirmButton => confirmButton;
    public bool IsOpen => gameObject.activeInHierarchy;

    public static void CloseAllActivePopups()
    {
        var popupsToClose = new System.Collections.Generic.List<TutorialPopupUI>(activePopups);
        activePopups.Clear();
        for (int i = 0; i < popupsToClose.Count; i++)
        {
            if (popupsToClose[i] != null)
            {
                popupsToClose[i].Close();
            }
        }
    }

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(Close);
        }
    }

    private void OnEnable()
    {
        if (!activePopups.Contains(this))
        {
            activePopups.Add(this);
        }

        ApplyDefaultContentIfEmpty();
        onPopupOpened?.Invoke();
    }

    private void OnDisable()
    {
        activePopups.Remove(this);
    }

    public void SetContent(string title, string description, Sprite icon = null, string keyHint = null)
    {
        if (titleText != null && !string.IsNullOrEmpty(title))
        {
            titleText.text = title;
        }

        if (descriptionText != null && !string.IsNullOrEmpty(description))
        {
            descriptionText.text = description;
        }

        if (keyHintText != null && !string.IsNullOrEmpty(keyHint))
        {
            keyHintText.text = keyHint;
        }

        if (iconImage != null)
        {
            if (icon != null)
            {
                iconImage.sprite = icon;
                if (iconContainer != null) iconContainer.SetActive(true);
            }
            else if (iconContainer != null)
            {
                iconContainer.SetActive(false);
            }
        }
    }

    private void ApplyDefaultContentIfEmpty()
    {
        if (titleText != null && string.IsNullOrEmpty(titleText.text))
        {
            titleText.text = defaultTitle;
        }

        if (descriptionText != null && string.IsNullOrEmpty(descriptionText.text))
        {
            descriptionText.text = defaultDescription;
        }

        if (keyHintText != null && string.IsNullOrEmpty(keyHintText.text))
        {
            keyHintText.text = defaultKeyHint;
        }

        if (iconImage != null && defaultIcon != null && iconImage.sprite == null)
        {
            iconImage.sprite = defaultIcon;
            if (iconContainer != null) iconContainer.SetActive(true);
        }
    }

    public void Open()
    {
        gameObject.SetActive(true);
    }

    public void Close()
    {
        activePopups.Remove(this);
        onPopupClosed?.Invoke();
        OnAnyPopupClosed?.Invoke();
        gameObject.SetActive(false);
    }
}
