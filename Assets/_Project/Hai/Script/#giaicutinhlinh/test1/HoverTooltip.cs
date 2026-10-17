using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Tự động ẩn/hiện một đối tượng UI (ví dụ Text mô tả) khi rê chuột vào/ra nút bấm.
/// </summary>
public class HoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject tooltipObject; // Kéo đối tượng Text cần ẩn/hiện vào đây

    private void Start()
    {
        // Ẩn chữ đi khi bắt đầu game
        if (tooltipObject != null)
        {
            tooltipObject.SetActive(false);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltipObject != null)
        {
            tooltipObject.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipObject != null)
        {
            tooltipObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        // Đảm bảo ẩn đi khi nút này bị tắt
        if (tooltipObject != null)
        {
            tooltipObject.SetActive(false);
        }
    }
}
