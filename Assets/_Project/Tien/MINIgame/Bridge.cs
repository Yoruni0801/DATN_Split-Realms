// MINIgame/Bridge.cs
using UnityEngine;

public class Bridge : MonoBehaviour
{
    [Header("Cách 1: Đổi hình ảnh (Sprite 2D)")]
    [Tooltip("Kéo SpriteRenderer của cây cầu vào đây")]
    public SpriteRenderer bridgeSpriteRenderer;
    [Tooltip("Hình ảnh cây cầu lúc bị gãy")]
    public Sprite brokenSprite;
    [Tooltip("Hình ảnh cây cầu lúc đã sửa xong")]
    public Sprite repairedSprite;

    [Header("Cách 2: Bật/tắt Object (Dùng nếu có 2 Object riêng)")]
    public GameObject brokenBridgeVisual;
    public GameObject repairedBridgeVisual;

    void Awake()
    {
        // Đảm bảo hình ảnh cầu hiển thị đúng trạng thái ban đầu (đang hỏng)
        SetVisuals(false);
    }

    // Hàm chuyển đổi bật/tắt giữa hình ảnh cầu hỏng và cầu đã sửa
    // Gọi hàm này từ PuzzleManager khi minigame hoàn thành
    public void SetVisuals(bool repaired)
    {
        // Xử lý đổi Sprite 2D
        if (bridgeSpriteRenderer != null)
        {
            if (repaired && repairedSprite != null)
                bridgeSpriteRenderer.sprite = repairedSprite;
            else if (!repaired && brokenSprite != null)
                bridgeSpriteRenderer.sprite = brokenSprite;
        }

        // Xử lý bật/tắt 2 Object riêng biệt
        if (brokenBridgeVisual != null) brokenBridgeVisual.SetActive(!repaired);
        if (repairedBridgeVisual != null) repairedBridgeVisual.SetActive(repaired);
    }
}
