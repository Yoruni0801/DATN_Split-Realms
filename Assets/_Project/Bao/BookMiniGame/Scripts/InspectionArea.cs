using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Vùng "Bàn kiểm tra" - Khi sách được thả vào đây, nó sẽ snap vào vị trí
/// và kích hoạt cơ chế mở sách bằng EndlessBook.
/// </summary>
[RequireComponent(typeof(Collider))]
public class InspectionArea : MonoBehaviour
{
    [Header("Snap Settings")]
    [Tooltip("Vị trí sách sẽ snap vào khi thả lên bàn (local offset)")]
    public Transform snapPoint;

    [Header("Visual Feedback")]
    [Tooltip("Renderer vùng highlight khi đang kéo sách qua")]
    public Renderer highlightRenderer;

    [Tooltip("Màu highlight khi hover")]
    public Color highlightColor = new Color(0.2f, 0.8f, 1f, 0.3f);

    [Header("Events")]
    public UnityEvent<DraggableBookItem> onBookPlaced;

    private Color originalHighlightColor;
    private bool isHighlighted = false;

    void Start()
    {
        if (highlightRenderer != null)
        {
            originalHighlightColor = highlightRenderer.material.color;
            highlightRenderer.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Kiểm tra xem một vị trí có nằm trong vùng bàn kiểm tra không
    /// </summary>
    public bool IsInsideArea(Vector3 worldPosition)
    {
        Collider col = GetComponent<Collider>();
        // Ép Y của vị trí kiểm tra ngang với tâm của mặt bàn
        // Nhờ vậy ta có thể bỏ qua việc cuốn sách đang bị nhấc lên cao (dragLiftHeight)
        Vector3 checkPos = worldPosition;
        checkPos.y = col.bounds.center.y;
        
        return col.bounds.Contains(checkPos);
    }

    /// <summary>
    /// Bật highlight khi người chơi kéo sách qua vùng này
    /// </summary>
    public void ShowHighlight()
    {
        if (highlightRenderer != null && !isHighlighted)
        {
            highlightRenderer.gameObject.SetActive(true);
            highlightRenderer.material.color = highlightColor;
            isHighlighted = true;
        }
    }

    /// <summary>
    /// Tắt highlight
    /// </summary>
    public void HideHighlight()
    {
        if (highlightRenderer != null && isHighlighted)
        {
            highlightRenderer.gameObject.SetActive(false);
            isHighlighted = false;
        }
    }

    /// <summary>
    /// Đặt sách lên bàn kiểm tra
    /// </summary>
    public void PlaceBook(DraggableBookItem book)
    {
        if (snapPoint != null)
        {
            book.SnapToPosition(snapPoint.position, snapPoint.rotation);
        }
        else
        {
            book.SnapToPosition(transform.position + Vector3.up * 0.1f, Quaternion.identity);
        }

        HideHighlight();
        onBookPlaced?.Invoke(book);
    }
}
