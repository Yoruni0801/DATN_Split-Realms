using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using DG.Tweening;

/// <summary>
/// Quản lý UI gợi ý cho minigame tìm sách.
/// Hiển thị Main Hint, Sub Hint, Result và UI phím F (Dismiss / Skip) với DOTween animation.
/// </summary>
public class HintUI : MonoBehaviour
{
    [Header("Main Hint")]
    [Tooltip("Text hiển thị gợi ý chính")]
    public TextMeshProUGUI mainHintText;

    [Tooltip("Panel chứa gợi ý chính")]
    public GameObject mainHintPanel;

    [Header("Sub Hint")]
    [Tooltip("Text hiển thị gợi ý phụ")]
    public TextMeshProUGUI subHintText;

    [Tooltip("Panel chứa gợi ý phụ")]
    public GameObject subHintPanel;

    [Header("Result")]
    [Tooltip("Text hiển thị kết quả (Không phải cuốn này / Đã tìm thấy)")]
    public TextMeshProUGUI resultText;

    [Tooltip("Panel chứa kết quả")]
    public GameObject resultPanel;

    [Header("Dismiss / Skip Prompt (Phím F)")]
    [Tooltip("Panel chứa UI gợi ý phím F để vứt sách / skip")]
    public GameObject dismissPromptPanel;

    [Tooltip("Text hiển thị nội dung phím F")]
    public TextMeshProUGUI dismissPromptText;

    [Tooltip("Nội dung text mặc định cho phím F")]
    [SerializeField] private string defaultDismissPrompt = "<color=#FFD54F><b>[F]</b></color> Vứt sách / Bỏ qua";

    [Header("Dismiss Prompt DOTween Animation")]
    [Tooltip("Khoảng cách trượt từ bên phải vào (pixel)")]
    [SerializeField] private float slideOffset = 300f;

    [Tooltip("Thời gian trượt vào")]
    [SerializeField] private float slideInDuration = 0.45f;

    [Tooltip("Thời gian trượt ra")]
    [SerializeField] private float slideOutDuration = 0.3f;

    [Tooltip("Hiệu ứng trượt vào")]
    [SerializeField] private Ease slideInEase = Ease.OutBack;

    [Tooltip("Hiệu ứng trượt ra")]
    [SerializeField] private Ease slideOutEase = Ease.InQuad;

    [Header("Animation")]
    [Tooltip("Thời gian hiển thị sub hint trước khi tự ẩn")]
    public float subHintDisplayTime = 3f;

    [Tooltip("Thời gian hiển thị result trước khi tự ẩn")]
    public float resultDisplayTime = 2f;

    private Coroutine subHintCoroutine;
    private Coroutine resultCoroutine;

    private Vector2 dismissPromptOriginalPos;
    private RectTransform dismissPromptRect;
    private CanvasGroup dismissPromptCanvasGroup;
    private Tween dismissPosTween;
    private Tween dismissFadeTween;
    private bool hasCachedOriginalPos;

    private void Awake()
    {
        EnsureDismissPromptCreated();
        CacheDismissPromptTransform();
    }

    private void Start()
    {
        // Ẩn tất cả panel lúc đầu
        if (subHintPanel != null) subHintPanel.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(false);
        if (dismissPromptPanel != null)
        {
            dismissPromptPanel.SetActive(false);
            if (dismissPromptCanvasGroup != null) dismissPromptCanvasGroup.alpha = 0f;
        }
    }

    private void OnDestroy()
    {
        KillDismissTweens();
    }

    private void CacheDismissPromptTransform()
    {
        if (dismissPromptPanel != null && !hasCachedOriginalPos)
        {
            dismissPromptRect = dismissPromptPanel.GetComponent<RectTransform>();
            if (dismissPromptRect != null)
            {
                dismissPromptOriginalPos = dismissPromptRect.anchoredPosition;
                hasCachedOriginalPos = true;
            }

            dismissPromptCanvasGroup = dismissPromptPanel.GetComponent<CanvasGroup>();
            if (dismissPromptCanvasGroup == null)
            {
                dismissPromptCanvasGroup = dismissPromptPanel.AddComponent<CanvasGroup>();
            }
        }
    }

    private void KillDismissTweens()
    {
        if (dismissPosTween != null && dismissPosTween.IsActive()) dismissPosTween.Kill();
        if (dismissFadeTween != null && dismissFadeTween.IsActive()) dismissFadeTween.Kill();
    }

    /// <summary>
    /// Tự động tạo Dismiss Prompt UI nếu chưa được gán trong Inspector
    /// </summary>
    private void EnsureDismissPromptCreated()
    {
        if (dismissPromptPanel != null) return;

        // Tìm Canvas phù hợp
        Canvas targetCanvas = GetComponentInParent<Canvas>();
        if (targetCanvas == null) targetCanvas = FindFirstObjectByType<Canvas>();

        if (targetCanvas == null) return;

        // Tạo root panel cho prompt phím F
        GameObject panelObj = new GameObject("DismissPromptPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObj.transform.SetParent(targetCanvas.transform, false);

        RectTransform rect = panelObj.GetComponent<RectTransform>();
        // Căn góc trên bên phải màn hình
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-40f, -40f);
        rect.sizeDelta = new Vector2(300f, 48f);

        Image bg = panelObj.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.12f, 0.85f);

        // Tạo Text con
        GameObject textObj = new GameObject("DismissPromptText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(panelObj.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.offsetMin = new Vector2(10f, 5f);
        textRect.offsetMax = new Vector2(-10f, -5f);

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = defaultDismissPrompt;
        tmp.fontSize = 20;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        dismissPromptPanel = panelObj;
        dismissPromptText = tmp;
        dismissPromptPanel.SetActive(false);
    }

    /// <summary>
    /// Hiển thị gợi ý phím F khi cầm/mở sách với hiệu ứng DOTween trượt từ phải sang
    /// </summary>
    public void ShowDismissPrompt(string customText = null)
    {
        EnsureDismissPromptCreated();
        CacheDismissPromptTransform();

        if (dismissPromptText != null)
        {
            dismissPromptText.text = string.IsNullOrEmpty(customText) ? defaultDismissPrompt : customText;
        }

        if (dismissPromptPanel != null && dismissPromptRect != null)
        {
            KillDismissTweens();

            dismissPromptPanel.SetActive(true);

            // Bắt đầu từ lệch sang bên phải (+slideOffset)
            Vector2 startPos = new Vector2(dismissPromptOriginalPos.x + slideOffset, dismissPromptOriginalPos.y);
            dismissPromptRect.anchoredPosition = startPos;

            if (dismissPromptCanvasGroup != null)
            {
                dismissPromptCanvasGroup.alpha = 0f;
                dismissFadeTween = dismissPromptCanvasGroup.DOFade(1f, slideInDuration * 0.7f).SetUpdate(true);
            }

            // Trượt mượt mà về vị trí ban đầu (vị trí người dùng đã căn chỉnh trong scene)
            dismissPosTween = dismissPromptRect.DOAnchorPos(dismissPromptOriginalPos, slideInDuration)
                .SetEase(slideInEase)
                .SetUpdate(true);
        }
        else if (dismissPromptPanel != null)
        {
            dismissPromptPanel.SetActive(true);
        }
    }

    /// <summary>
    /// Ẩn gợi ý phím F với hiệu ứng trượt sang phải rồi ẩn
    /// </summary>
    public void HideDismissPrompt()
    {
        if (dismissPromptPanel == null || !dismissPromptPanel.activeSelf) return;

        CacheDismissPromptTransform();

        if (dismissPromptRect != null)
        {
            KillDismissTweens();

            Vector2 exitPos = new Vector2(dismissPromptOriginalPos.x + slideOffset, dismissPromptOriginalPos.y);

            if (dismissPromptCanvasGroup != null)
            {
                dismissFadeTween = dismissPromptCanvasGroup.DOFade(0f, slideOutDuration).SetUpdate(true);
            }

            dismissPosTween = dismissPromptRect.DOAnchorPos(exitPos, slideOutDuration)
                .SetEase(slideOutEase)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (dismissPromptPanel != null)
                    {
                        dismissPromptPanel.SetActive(false);
                        dismissPromptRect.anchoredPosition = dismissPromptOriginalPos;
                    }
                });
        }
        else
        {
            dismissPromptPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Hiển thị gợi ý chính ngay từ đầu game
    /// </summary>
    public void ShowMainHint(string hint)
    {
        if (mainHintText != null) mainHintText.text = hint;
        if (mainHintPanel != null) mainHintPanel.SetActive(true);
    }

    /// <summary>
    /// Ẩn gợi ý chính
    /// </summary>
    public void HideMainHint()
    {
        if (mainHintPanel != null) mainHintPanel.SetActive(false);
    }

    /// <summary>
    /// Hiển thị gợi ý phụ khi mở sách sai
    /// </summary>
    public void ShowSubHint(string hint)
    {
        if (subHintCoroutine != null) StopCoroutine(subHintCoroutine);

        if (subHintText != null) subHintText.text = hint;
        if (subHintPanel != null) subHintPanel.SetActive(true);

        subHintCoroutine = StartCoroutine(AutoHideSubHint());
    }

    /// <summary>
    /// Hiển thị kết quả sách sai
    /// </summary>
    public void ShowWrongResult()
    {
        ShowResult("Không phải cuốn này...", Color.red);
    }

    /// <summary>
    /// Hiển thị kết quả sách đúng
    /// </summary>
    public void ShowCorrectResult(string message = "Đã tìm thấy cuốn sách!")
    {
        ShowResult(message, new Color(0.2f, 0.8f, 1f));
    }

    private void ShowResult(string text, Color color)
    {
        if (resultCoroutine != null) StopCoroutine(resultCoroutine);

        if (resultText != null)
        {
            resultText.text = text;
            resultText.color = color;
        }
        if (resultPanel != null) resultPanel.SetActive(true);

        resultCoroutine = StartCoroutine(AutoHideResult());
    }

    private IEnumerator AutoHideSubHint()
    {
        yield return new WaitForSeconds(subHintDisplayTime);
        if (subHintPanel != null) subHintPanel.SetActive(false);
    }

    private IEnumerator AutoHideResult()
    {
        yield return new WaitForSeconds(resultDisplayTime);
        if (resultPanel != null) resultPanel.SetActive(false);
    }

    /// <summary>
    /// Ẩn tất cả UI
    /// </summary>
    public void HideAll()
    {
        if (mainHintPanel != null) mainHintPanel.SetActive(false);
        if (subHintPanel != null) subHintPanel.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(false);
        HideDismissPrompt();
    }
}
