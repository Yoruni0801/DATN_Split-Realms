using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Dữ liệu cấu hình cho một cuốn sách trong MiniGame.
/// Chứa thông tin hiển thị 3D (prefab bìa ngoài) và nội dung trang (materials cho EndlessBook).
/// </summary>
[System.Serializable]
public class BookEntry
{
    [Header("Identification")]
    [Tooltip("Tên hiển thị của cuốn sách (dùng trong UI/hint)")]
    public string bookName;

    [Tooltip("Đây có phải cuốn sách đúng không?")]
    public bool isCorrectBook = false;

    [Header("3D Pile Visuals")]
    [Tooltip("Prefab model 3D bìa sách (dùng trong đống sách, KHÔNG phải EndlessBook)")]
    public GameObject bookPrefab;

    [Header("Hints")]
    [Tooltip("Gợi ý phụ hiển thị khi mở sách sai này")]
    [TextArea(2, 4)]
    public string subHintOnOpen = "";
}

/// <summary>
/// ScriptableObject chứa toàn bộ cấu hình cho minigame tìm sách.
/// Tạo bằng menu: Create > Bao > BookMiniGame > Game Data
/// </summary>
[CreateAssetMenu(fileName = "BookMiniGameData", menuName = "Bao/BookMiniGame/Game Data")]
public class BookMiniGameData : ScriptableObject
{
    [Header("=== DANH SÁCH SÁCH ===")]
    [Tooltip("Danh sách tất cả các cuốn sách trong minigame (1 đúng, còn lại sai)")]
    public List<BookEntry> books = new List<BookEntry>();

    [Header("=== GỢI Ý CHÍNH ===")]
    [Tooltip("Text gợi ý chính hiển thị ngay từ đầu game")]
    [TextArea(3, 6)]
    public string mainHint = "Tìm cuốn sách có vết xanh trên bìa...";

    [Header("=== THÔNG SỐ GAMEPLAY ===")]
    [Tooltip("Tốc độ lật trang tự động khi mở sách sai (TimePerPage)")]
    [Range(0.1f, 1f)]
    public float wrongBookFlipSpeed = 0.3f;

    [Tooltip("Thời gian mở/đóng bìa sách")]
    [Range(0.2f, 2f)]
    public float coverAnimationTime = 0.5f;

    [Tooltip("Thời gian chờ trước khi sách sai bị loại bỏ")]
    [Range(0.5f, 3f)]
    public float wrongBookDismissDelay = 1f;

    [Header("=== SPAWN SETTINGS ===")]
    [Tooltip("Tổng số sách sẽ được spawn ra (Gồm 1 cuốn đúng và nhiều cuốn sai nhân bản ngẫu nhiên)")]
    [Range(2, 100)]
    public int totalBooksToSpawn = 10;

    [Tooltip("Bán kính vùng spawn đống sách")]
    public float pileRadius = 2f;

    [Tooltip("Chiều cao tối đa của đống sách")]
    public float pileMaxHeight = 1.5f;

    [Tooltip("Góc xoay ngẫu nhiên tối đa (độ)")]
    [Range(0f, 45f)]
    public float maxRandomRotation = 25f;

    [Header("=== BỐ CỤC ĐỐNG SÁCH ===")]
    [Tooltip("Xếp sách thành các tầng/ring có chủ đích thay vì rải ngẫu nhiên hoàn toàn.")]
    public bool useLayeredPile = true;

    [Tooltip("Số cuốn sách ở mỗi tầng. Tăng số này để đống sách rộng hơn và thấp hơn.")]
    [Range(3, 12)]
    public int booksPerLayer = 6;

    [Tooltip("Khoảng cách chiều cao giữa hai tầng sách.")]
    [Range(0.06f, 0.45f)]
    public float pileLayerHeight = 0.18f;

    [Tooltip("Độ lệch ngẫu nhiên nhỏ để đống sách vẫn có cảm giác tự nhiên.")]
    [Range(0f, 0.5f)]
    public float pilePositionJitter = 0.15f;

}
