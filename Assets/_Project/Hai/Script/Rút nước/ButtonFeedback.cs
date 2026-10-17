using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

public class ButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Cài đặt hiệu ứng nhấn")]
    [SerializeField] private float punchScale = 0.9f;
    [SerializeField] private float duration = 0.1f;

    [Header("Hiệu ứng glow màu sắc")]
    [SerializeField] private Image buttonImage;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color correctColor = new Color(0.4f, 0.8f, 1f, 1f); // Xanh lam khi chọn
    [SerializeField] private Color wrongColor = new Color(1f, 0.3f, 0.3f, 1f);   // Đỏ khi sai (Mới thêm)

    private Vector3 _originalScale;

    void Start()
    {
        _originalScale = transform.localScale;
        if (buttonImage != null)
            buttonImage.color = normalColor;
    }

    public void ShowGlow()
    {
        if (buttonImage != null)
            buttonImage.DOColor(correctColor, 0.15f);
    }

    // Hàm mới: Chuyển nút sang màu đỏ mượt mà
    public void ShowWrongGlow()
    {
        if (buttonImage != null)
            buttonImage.DOColor(wrongColor, 0.1f);
    }

    public void HideGlow()
    {
        if (buttonImage != null)
            buttonImage.DOColor(normalColor, 0.15f);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        transform.DOScale(_originalScale * punchScale, duration).SetEase(Ease.OutQuad);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        transform.DOScale(_originalScale, duration).SetEase(Ease.OutBack);
    }
}