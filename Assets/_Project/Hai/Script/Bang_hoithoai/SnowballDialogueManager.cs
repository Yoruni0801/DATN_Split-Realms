using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

public class SnowballDialogueManager : MonoBehaviour
{
    public static SnowballDialogueManager Instance { get; private set; }

    [Header("Kết kết nối Player")]
    [SerializeField] private Player player;

    [Header("Kết nối Camera & Mục tiêu")]
    [SerializeField] private CinemachineCamera virtualCamera;
    [SerializeField] private Transform snowlineTransform; // Điểm Snowline (trước đây là Snowball)
    [SerializeField] private Transform slopeTransform; // Con dốc (Slop)
    [SerializeField] private Transform wallTransform;

    [Header("Ảnh Chân Dung Nhân Vật")]
    [Tooltip("Ảnh đại diện của Tinh Linh (kéo ảnh Sprite từ Project vào đây)")]
    [SerializeField] private Sprite tinhLinhPortrait;

    [Header("Cài đặt Phóng To / Thu Nhỏ Camera")]
    [Tooltip("Kích thước camera khi zoom out để nhìn toàn bộ dốc (Orthographic Size) - Nên để từ 10 đến 15")]
    [SerializeField] private float zoomOutSize = 12f;

    [Header("Thời gian hiển thị hội thoại")]
    [SerializeField] private float durationNormal = 3.5f;
    [SerializeField] private float durationLong = 5.0f;

    private Transform originalCameraTarget;
    private float originalZoomSize = 5f;
    private bool isIntroPlayed = false;
    private bool isVictoryPlayed = false;
    
    // Tự động hóa trạng thái và khóa quét sự kiện
    private bool spawnedDialogueTriggered = false;
    private bool hasWallBeenAssigned = false;
    private bool isPlayerReadyToRoll = false; // Chỉ cho phép quét sự kiện sau khi hết thoại Intro
    private bool hasPushTutorialPlayed = false; // Trạng thái đã hiện thoại hướng dẫn đẩy chưa
    private bool isBouncePlayed = false; // Đảm bảo thoại đập tinh thể chỉ chạy 1 lần
    private bool isPrepPlayed = false;

    public bool IsPlayerReadyToRoll() => isPlayerReadyToRoll;
    public bool HasPushTutorialPlayed() => hasPushTutorialPlayed;

    // Danh sách lưu các quả bóng có sẵn từ trước trong Editor để bỏ qua khi quét
    private HashSet<IceRockRoller> ignoredRollers = new HashSet<IceRockRoller>();

    // Lưu trữ tham chiếu các Coroutine đang chạy để tránh đè thoại
    private Coroutine introCoroutine;
    private Coroutine pushDialogueCoroutine;
    private Coroutine rollingDialogueCoroutine;
    private Coroutine bounceDialogueCoroutine;
    private Coroutine prepDialogueCoroutine;
    private Coroutine victoryCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (player == null)
        {
            player = PlayerRegistry.Current ?? FindFirstObjectByType<Player>();
        }

        // Lưu lại kích thước zoom ban đầu của camera
        if (virtualCamera != null)
        {
            originalZoomSize = virtualCamera.Lens.OrthographicSize;
            Debug.Log($"[DialogueManager] Đã lưu kích thước camera ban đầu: {originalZoomSize}");
        }
        else
        {
            Debug.LogWarning("[DialogueManager] Chưa gán Virtual Camera trong Inspector!");
        }

        // Kiểm tra xem đã gán wall chưa, tránh việc trigger chiến thắng ngay lập tức
        if (wallTransform != null)
        {
            hasWallBeenAssigned = true;
        }
    }

    private void Update()
    {
        // KHÓA TUYỆT ĐỐI: Chỉ quét sự kiện khi người chơi đã xem xong Intro và bắt đầu được chơi thật
        if (!isPlayerReadyToRoll) return;

        // 1. TỰ ĐỘNG BẮT SỰ KIỆN CHIẾN THẮNG (Cửa băng bị vỡ)
        if (hasWallBeenAssigned && !isVictoryPlayed && wallTransform == null)
        {
            Debug.Log("[DialogueManager] Phát hiện Cửa băng bị phá hủy! Kích hoạt thoại Chiến thắng.");
            TriggerVictory();
        }

        // 2. TỰ ĐỘNG PHÁT HIỆN BÓNG MỚI ĐƯỢC TẠO
        // Kiểm tra xem snowlineTransform hiện tại có thực sự là quả bóng tuyết mới (chứa IceRockRoller và không nằm trong danh sách bỏ qua)
        bool isRealSnowballActive = snowlineTransform != null && 
                                    snowlineTransform.GetComponent<IceRockRoller>() != null &&
                                    !ignoredRollers.Contains(snowlineTransform.GetComponent<IceRockRoller>());

        if (!isRealSnowballActive)
        {
            // Quét tìm tất cả quả bóng tuyết đang có trong Scene
            var activeRollers = FindObjectsByType<IceRockRoller>(FindObjectsSortMode.None);
            foreach (var roller in activeRollers)
            {
                // Chỉ nhận quả bóng mới sinh ra (không nằm trong danh sách bóng mẫu cũ có sẵn ở Editor)
                if (!ignoredRollers.Contains(roller))
                {
                    snowlineTransform = roller.transform;
                    isRealSnowballActive = true;
                    Debug.Log($"[DialogueManager] Đã phát hiện bóng tuyết MỚI vừa xuất hiện: {roller.name}");
                    break;
                }
            }
        }

        // 3. KÍCH HOẠT HỘI THOẠI LĂN BÓNG & ZOOM OUT CAMERA
        if (isRealSnowballActive && !spawnedDialogueTriggered)
        {
            spawnedDialogueTriggered = true;
            Debug.Log("[DialogueManager] Kích hoạt thoại Lăn bóng!");
            TriggerRollingDialogue();
        }
    }

    // Dừng tất cả các hội thoại đang chạy để tránh đè thoại lẫn nhau
    private void StopAllActiveDialogues()
    {
        if (introCoroutine != null) { StopCoroutine(introCoroutine); introCoroutine = null; }
        if (pushDialogueCoroutine != null) { StopCoroutine(pushDialogueCoroutine); pushDialogueCoroutine = null; }
        if (rollingDialogueCoroutine != null) { StopCoroutine(rollingDialogueCoroutine); rollingDialogueCoroutine = null; }
        if (bounceDialogueCoroutine != null) { StopCoroutine(bounceDialogueCoroutine); bounceDialogueCoroutine = null; }
        if (prepDialogueCoroutine != null) { StopCoroutine(prepDialogueCoroutine); prepDialogueCoroutine = null; }
        if (victoryCoroutine != null) { StopCoroutine(victoryCoroutine); victoryCoroutine = null; }
    }

    // --- HÀM GỌI HỘI THOẠI THÔNG QUA DIALOGUE UI MANAGER ---
    private void ShowDialogueLine(string speaker, string text, float duration)
    {
        Sprite activePortrait = null;
        if (speaker == "Tinh Linh")
        {
            activePortrait = tinhLinhPortrait;
        }

        if (DialogueUIManager.Instance != null)
        {
            DialogueUIManager.Instance.ShowDialogue(speaker, text, duration, activePortrait);
        }
        else
        {
            // Dự phòng tìm kiếm trong scene
            DialogueUIManager managerInScene = FindFirstObjectByType<DialogueUIManager>();
            if (managerInScene != null)
            {
                managerInScene.ShowDialogue(speaker, text, duration, activePortrait);
            }
            else
            {
                Debug.LogWarning($"[{speaker}] {text} (Thời gian: {duration}s) \n" +
                                 "=> LỖI: Thiếu component 'DialogueUIManager' trong Scene hiện tại!");
            }
        }
    }

    // --- PHẦN 1: HỘI THOẠI MỞ ĐẦU (Người chơi đến trước cánh cửa) ---
    [ContextMenu("Trigger Intro")]
    public void TriggerIntro()
    {
        if (isIntroPlayed) return;
        StopAllActiveDialogues();
        introCoroutine = StartCoroutine(IntroRoutine());
    }

    private IEnumerator IntroRoutine()
    {
        isIntroPlayed = true;
        Debug.Log("[DialogueManager] Bắt đầu chạy IntroRoutine.");

        // Khóa di chuyển của Player
        if (player != null) player.SetInputActive(false);

        // Lưu mục tiêu camera ban đầu (Player)
        if (virtualCamera != null)
        {
            originalCameraTarget = virtualCamera.Follow;
        }

        // 1. Camera hướng về cánh cổng
        if (virtualCamera != null && wallTransform != null)
        {
            virtualCamera.Follow = wallTransform;
            Debug.Log($"[DialogueManager] Camera lia về Cửa: {wallTransform.name}");
        }

        // Tinh Linh: "Con đường bị chặn rồi…"
        ShowDialogueLine("Tinh Linh", "Con đường bị chặn rồi…", durationNormal);
        yield return new WaitForSeconds(durationNormal);

        // Tinh Linh: "Lớp băng này... không phải được hình thành tự nhiên. Ta cảm nhận được năng lượng nguyên tố vẫn còn lưu lại bên trong nó, dù đã rất lâu rồi."
        ShowDialogueLine("Tinh Linh", "Lớp băng này... không phải được hình thành tự nhiên. Ta cảm nhận được năng lượng nguyên tố vẫn còn lưu lại bên trong nó, dù đã rất lâu rồi.", durationLong);
        yield return new WaitForSeconds(durationLong);

        // 2. Camera hướng lên điểm đầu (Snowline)
        if (virtualCamera != null && snowlineTransform != null)
        {
            virtualCamera.Follow = snowlineTransform;
            Debug.Log($"[DialogueManager] Camera lia về Snowline: {snowlineTransform.name}");
        }
        yield return new WaitForSeconds(1.5f); // Đợi camera lia xong

        // Tinh Linh: "Đường tuyết kia... và những tinh thể màu lam phía dưới... chúng đều mang cùng một luồng năng lượng."
        ShowDialogueLine("Tinh Linh", "Đường tuyết kia... và những tinh thể màu lam phía dưới... chúng đều mang cùng một luồng năng lượng.", durationLong);
        yield return new WaitForSeconds(durationLong);

        // Tinh Linh: "Có lẽ... chúng vốn thuộc về cùng một tạo vật."
        ShowDialogueLine("Tinh Linh", "Có lẽ... chúng vốn thuộc về cùng một tạo vật.", durationNormal);
        yield return new WaitForSeconds(durationNormal);

        // Tinh Linh: "Chúng ta thử xem nhé. Biết đâu vẫn còn hoạt động."
        ShowDialogueLine("Tinh Linh", "Chúng ta thử xem nhé. Biết đâu vẫn còn hoạt động.", durationNormal);
        yield return new WaitForSeconds(durationNormal);

        // Trả camera về cho Player
        if (virtualCamera != null && originalCameraTarget != null)
        {
            virtualCamera.Follow = originalCameraTarget;
            Debug.Log("[DialogueManager] Camera lia về lại Player");
        }
        yield return new WaitForSeconds(1.0f);

        // Mở khóa Player
        if (player != null) player.SetInputActive(true);

        // GHI LẠI CÁC QUẢ BÓNG CŨ: Quét toàn bộ bóng tuyết có sẵn trong Scene ở thời điểm này để đưa vào danh sách bỏ qua
        var existingRollers = FindObjectsByType<IceRockRoller>(FindObjectsSortMode.None);
        foreach (var r in existingRollers)
        {
            ignoredRollers.Add(r);
            Debug.Log($"[DialogueManager] Đã bỏ qua bóng mẫu ban đầu: {r.name}");
        }

        // Kích hoạt cờ cho phép quét bóng tuyết lăn và chiến thắng kể từ lúc này
        isPlayerReadyToRoll = true;

        // Bỏ việc gọi TriggerPushDialogue ở đây. Lời thoại hướng dẫn sẽ được gọi khi chạm vào SnowZone
    }

    // --- PHẦN 2: TƯƠNG TÁC QUẢ CẦU TUYẾT & HƯỚNG DẪN ĐẨY ---
    public void TriggerPushDialogue()
    {
        if (hasPushTutorialPlayed) return;
        hasPushTutorialPlayed = true;

        StopAllActiveDialogues();
        pushDialogueCoroutine = StartCoroutine(PushDialogueRoutine());
    }

    private IEnumerator PushDialogueRoutine()
    {
        Debug.Log("[DialogueManager] Bắt đầu chạy PushDialogueRoutine.");
        // Tinh Linh: "Thử lăn tuyết xem."
        ShowDialogueLine("Tinh Linh", "Thử lăn tuyết xem.", durationNormal);
        yield return new WaitForSeconds(durationNormal);

        // [Tutorial]: "Nhấn Giữ F để tạo bóng"
        ShowDialogueLine("[Hướng dẫn]", "Nhấn Giữ F để tạo bóng", durationLong);
    }

    // --- PHẦN 3: ĐANG LĂN QUẢ CẦU TUYẾT (Kích hoạt sau khi nhấn F tạo bóng) ---
    public void TriggerRollingDialogue()
    {
        StopAllActiveDialogues();
        rollingDialogueCoroutine = StartCoroutine(RollingDialogueRoutine());
    }

    private IEnumerator RollingDialogueRoutine()
    {
        Debug.Log("[DialogueManager] Bắt đầu chạy RollingDialogueRoutine.");

        // Tinh Linh: "Đúng như ta nghĩ..."
        ShowDialogueLine("Tinh Linh", "Đúng như ta nghĩ...", 2.0f);
        yield return new WaitForSeconds(2.0f);

        // Tinh Linh: "Tuyết đang bám dần vào bề mặt. Nó càng lăn thì càng lớn và càng nặng."
        ShowDialogueLine("Tinh Linh", "Tuyết đang bám dần vào bề mặt. Nó càng lăn thì càng lớn và càng nặng.", durationLong);
        yield return new WaitForSeconds(durationLong);

        // Sau khi hiện lời thoại xong mới thực hiện Zoom Out Camera
        StartFollowingSnowball();
    }

    // --- CAMERA ZOOM OUT RA TOÀN MAP ---
    public void StartFollowingSnowball()
    {
        if (virtualCamera != null)
        {
            // Zoom out camera bằng cách tăng Orthographic Size
            var lens = virtualCamera.Lens;
            lens.OrthographicSize = zoomOutSize;
            virtualCamera.Lens = lens;
            Debug.Log($"[DialogueManager] Thực hiện Zoom Out Camera. Size mới: {zoomOutSize}");

            // Hướng camera theo dõi tâm khu vực con dốc (slopeTransform) để nhìn toàn cảnh
            if (slopeTransform != null)
            {
                originalCameraTarget = virtualCamera.Follow;
                virtualCamera.Follow = slopeTransform;
                Debug.Log($"[DialogueManager] Cam chuyển theo dõi con dốc: {slopeTransform.name}");
            }
        }
    }

    public void StopFollowingSnowball()
    {
        if (virtualCamera != null)
        {
            // Reset kích thước camera về bình thường
            var lens = virtualCamera.Lens;
            lens.OrthographicSize = originalZoomSize;
            virtualCamera.Lens = lens;
            Debug.Log($"[DialogueManager] Reset Camera Zoom về lại: {originalZoomSize}");

            // Trả camera về lại cho người chơi
            if (originalCameraTarget != null)
            {
                virtualCamera.Follow = originalCameraTarget;
                Debug.Log($"[DialogueManager] Cam chuyển về lại theo dõi: {originalCameraTarget.name}");
            }
        }
    }

    // --- PHẦN 4: VA VÀO TINH THỂ VÀ ĐỔI HƯỚNG ---
    [ContextMenu("Trigger Bounce Dialogue")]
    public void TriggerBounceDialogue()
    {
        if (isBouncePlayed) return;
        isBouncePlayed = true;

        StopAllActiveDialogues();
        bounceDialogueCoroutine = StartCoroutine(BounceDialogueRoutine());
    }

    private IEnumerator BounceDialogueRoutine()
    {
        Debug.Log("[DialogueManager] Bắt đầu chạy BounceDialogueRoutine.");
        // Tinh Linh: "Nó đổi hướng rồi!"
        ShowDialogueLine("Tinh Linh", "Nó đổi hướng rồi!", durationNormal);
        yield return new WaitForSeconds(durationNormal);

        // Tinh Linh: "Thì ra những tinh thể này không phải vật trang trí..."
        ShowDialogueLine("Tinh Linh", "Thì ra những tinh thể này không phải vật trang trí...", durationNormal);
        yield return new WaitForSeconds(durationNormal);

        // Tinh Linh: "Chúng đang bẻ dòng năng lượng của vật thể chạm vào, khiến quỹ đạo bị thay đổi."
        ShowDialogueLine("Tinh Linh", "Chúng đang bẻ dòng năng lượng của vật thể chạm vào, khiến quỹ đạo bị thay đổi.", durationLong);
        yield return new WaitForSeconds(durationLong);

        // Tinh Linh: "Nếu vậy... chỉ cần chọn đúng vị trí thả xuống, chúng ta có thể điều khiển đường đi của quả cầu."
        ShowDialogueLine("Tinh Linh", "Nếu vậy... chỉ cần chọn đúng vị trí thả xuống, chúng ta có thể điều khiển đường đi của quả cầu.", durationLong);
        yield return new WaitForSeconds(durationLong);

        // Nối tiếp tự động sang phần thoại Chuẩn bị (Phần 5)
        TriggerPrepDialogue();
    }

    // --- PHẦN 5: CHUẨN BỊ BẮT ĐẦU CHƠI ---
    [ContextMenu("Trigger Prep Dialogue")]
    public void TriggerPrepDialogue()
    {
        if (isPrepPlayed) return;
        isPrepPlayed = true;

        StopAllActiveDialogues();
        prepDialogueCoroutine = StartCoroutine(PrepDialogueRoutine());
    }

    private IEnumerator PrepDialogueRoutine()
    {
        Debug.Log("[DialogueManager] Bắt đầu chạy PrepDialogueRoutine.");
        // Tinh Linh: "Hãy quan sát thật kỹ địa hình và vị trí các tinh thể."
        ShowDialogueLine("Tinh Linh", "Hãy quan sát thật kỹ địa hình và vị trí các tinh thể.", durationNormal);
        yield return new WaitForSeconds(durationNormal);

        // Tinh Linh: "Nếu suy luận của tớ đúng... quả cầu sẽ dẫn chúng ta mở được con đường phía trước."
        ShowDialogueLine("Tinh Linh", "Nếu suy luận của tớ đúng... quả cầu sẽ dẫn chúng ta mở được con đường phía trước.", durationLong);
    }

    // --- PHẦN 6: THÀNH CÔNG (Sau khi phá vỡ bức tường) ---
    [ContextMenu("Trigger Victory")]
    public void TriggerVictory()
    {
        if (isVictoryPlayed) return;
        StopAllActiveDialogues();
        victoryCoroutine = StartCoroutine(VictoryRoutine());
    }

    private IEnumerator VictoryRoutine()
    {
        isVictoryPlayed = true;
        Debug.Log("[DialogueManager] Bắt đầu chạy VictoryRoutine.");

        // Khóa di chuyển của Player
        if (player != null) player.SetInputActive(false);

        // Hướng Camera vào bức tường đá bị phá hủy
        if (virtualCamera != null && wallTransform != null)
        {
            originalCameraTarget = virtualCamera.Follow;
            virtualCamera.Follow = wallTransform;
            Debug.Log($"[DialogueManager] Camera lia về Cửa vỡ: {wallTransform.name}");
        }

        // Tinh Linh: "Thành công rồi..."
        ShowDialogueLine("Tinh Linh", "Thành công rồi...", durationNormal);
        yield return new WaitForSeconds(durationNormal);

        // Tinh Linh: "Xem ra đây từng là một cơ chế do ai đó tạo ra, chỉ là đã bị thời gian chôn vùi dưới lớp tuyết."
        ShowDialogueLine("Tinh Linh", "Xem ra đây từng là một cơ chế do ai đó tạo ra, chỉ là đã bị thời gian chôn vùi dưới lớp tuyết.", durationLong);
        yield return new WaitForSeconds(durationLong);

        // Trả camera về cho Player và khôi phục kích thước ban đầu
        if (virtualCamera != null)
        {
            var lens = virtualCamera.Lens;
            lens.OrthographicSize = originalZoomSize;
            virtualCamera.Lens = lens;
            Debug.Log($"[DialogueManager] Reset Camera Zoom về lại: {originalZoomSize}");

            if (player != null)
            {
                virtualCamera.Follow = player.transform;
                Debug.Log("[DialogueManager] Camera trả về lại cho Player.");
            }
        }
        yield return new WaitForSeconds(1.0f);

        // Mở khóa Player
        if (player != null) player.SetInputActive(true);
    }
}
