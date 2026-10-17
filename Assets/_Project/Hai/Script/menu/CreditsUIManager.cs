using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Hai.Menu
{
    public class CreditsUIManager : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform contentRect;
        [SerializeField] private Button closeButton;

        [Header("Auto Scroll Settings")]
        [SerializeField] private bool autoScroll = true;
        [SerializeField] private float scrollSpeed = 35f; // Pixel mỗi giây

        private bool isScrolling = false;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(OnCloseClicked);
                closeButton.onClick.AddListener(OnCloseClicked);
            }
        }

        private void OnEnable()
        {
            ResetScroll();
            isScrolling = autoScroll;
        }

        private void Update()
        {
            if (isScrolling && scrollRect != null && contentRect != null)
            {
                // Tự động cuộn từ trên xuống dưới
                if (scrollRect.verticalNormalizedPosition > 0.001f)
                {
                    float totalHeight = contentRect.rect.height - scrollRect.viewport.rect.height;
                    if (totalHeight > 0)
                    {
                        float delta = (scrollSpeed * Time.unscaledDeltaTime) / totalHeight;
                        scrollRect.verticalNormalizedPosition = Mathf.Max(0f, scrollRect.verticalNormalizedPosition - delta);
                    }
                }
            }
        }

        public void ResetScroll()
        {
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 1f; // Đưa về vị trí trên cùng
            }
        }

        public void OnCloseClicked()
        {
            MainMenuUIManager mainMenu = GetComponentInParent<MainMenuUIManager>();
            if (mainMenu == null)
            {
#if UNITY_2023_1_OR_NEWER
                mainMenu = UnityEngine.Object.FindFirstObjectByType<MainMenuUIManager>();
#else
                mainMenu = UnityEngine.Object.FindObjectOfType<MainMenuUIManager>();
#endif
            }

            if (mainMenu != null)
            {
                mainMenu.ShowMainMenu();
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
