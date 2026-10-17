using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;



/// <summary>
/// State machine cho toàn bộ minigame
/// </summary>
public enum GameState
{
    Exploring,      // Đang lục lọi đống sách
    Dragging,       // Đang kéo sách
    Inspecting,     // Đang kiểm tra sách trên bàn
    WrongBook,      // Sách sai - đang lật tự động
    CorrectBook,    // Sách đúng - người chơi đang đọc
    GameWon         // Đã tìm thấy sách đúng
}

/// <summary>
/// Bộ não điều phối toàn bộ minigame tìm sách.
/// Quản lý: spawn đống sách, state machine, input Raycast, phán xét đúng/sai.
/// </summary>
public class BookMiniGameManager : MonoBehaviour
{
    [Header("=== DATA ===")]
    [Tooltip("ScriptableObject chứa cấu hình minigame")]
    public BookMiniGameData gameData;

    [Header("=== REFERENCES ===")]
    [Tooltip("Vùng bàn kiểm tra")]
    public InspectionArea inspectionArea;

    [Tooltip("Bộ điều khiển EndlessBook trên bàn kiểm tra")]
    public BookInspector bookInspector;

    [Tooltip("UI gợi ý")]
    public HintUI hintUI;

    [Tooltip("Camera chính dùng cho Raycast")]
    public Camera mainCamera;

    [Header("=== DRAG SETTINGS ===")]
    [Tooltip("Độ cao nâng sách lên khỏi mặt đất khi kéo (Drag). Chỉnh nhỏ lại nếu sách bị dí quá gần màn hình.")]
    public float dragLiftHeight = 1.5f;

    [Header("=== INSPECT SETTINGS ===")]
    [Tooltip("Vị trí sách sẽ bay lơ lửng ra trước mặt Camera để mở (Tạo 1 Empty Object). Nếu để trống, sẽ dùng 2 thông số bên dưới.")]
    public Transform inspectionHoverPoint;

    [Tooltip("Khoảng cách từ Camera đến sách (chỉ dùng khi không có Hover Point)")]
    public float autoHoverDistance = 1.5f;

    [Tooltip("Góc bù trừ cho sách (chỉnh thông số này nếu sách mở ra bị úp mặt, nghiêng hoặc lật ngược)")]
    public Vector3 autoHoverRotationOffset = Vector3.zero;

    [Header("=== SPAWN ===")]
    [Tooltip("Transform cha chứa đống sách")]
    public Transform pileParent;

    [Tooltip("Vị trí trung tâm đống sách")]
    public Transform pileCenter;

    [Header("=== SETTINGS ===")]
    [Tooltip("Layer mask cho Raycast sách")]
    public LayerMask bookRaycastMask = ~0;

    [Tooltip("Khoảng cách Raycast tối đa")]
    public float maxRaycastDistance = 50f;

    [Header("=== TUTORIAL PRESENTATION ===")]
    [Tooltip("Tutorial không chặn raycast gameplay và sẽ tự mờ sau thao tác đầu tiên.")]
    [SerializeField] private bool autoFadeTutorial = true;

    [Tooltip("Thời gian tutorial được giữ lại tối thiểu trước khi tự mờ.")]
    [Min(0f)]
    [SerializeField] private float tutorialMinimumVisibleTime = 2.5f;

    [Tooltip("Tự ẩn tutorial nếu người chơi chưa tương tác sau thời gian này.")]
    [Min(1f)]
    [SerializeField] private float tutorialFallbackHideTime = 8f;

    [Tooltip("Thời lượng mờ dần của tutorial.")]
    [Range(0.1f, 2f)]
    [SerializeField] private float tutorialFadeDuration = 0.65f;

    [Header("=== HOVER FEEDBACK ===")]
    [Tooltip("Hiển thị viền, bụi giấy và prompt ở cuốn sách đang được trỏ tới.")]
    [SerializeField] private bool enableHoverFeedback = true;

    [Header("=== QUEST BOOK SAFETY ===")]
    [Tooltip("Tự đưa Tối Cổ Hội Thư về vùng nhìn thấy nếu bị ném mất.")]
    [SerializeField] private bool recoverLostCorrectBook = true;

    [Tooltip("Thời gian sách phải nằm ngoài vùng an toàn trước khi được gọi về.")]
    [Min(0.25f)]
    [SerializeField] private float correctBookRecoveryDelay = 1.15f;

    [Tooltip("Khoảng cách ngang tối đa của sách đúng so với đống sách.")]
    [Min(1f)]
    [SerializeField] private float correctBookMaxDistance = 6f;

    [Tooltip("Khoảng rơi cho phép bên dưới tâm đống sách.")]
    [Min(0.25f)]
    [SerializeField] private float correctBookMaxFallDistance = 2.5f;

    [Header("=== EVENTS (VFX/SFX Hooks) ===")]
    [Tooltip("Khi mở một cuốn sách sai")]
    public UnityEvent onWrongBookOpened;

    [Tooltip("Khi sách sai bị loại bỏ xong")]
    public UnityEvent onWrongBookDismissed;

    [Tooltip("Khi tìm thấy cuốn sách đúng")]
    public UnityEvent onCorrectBookFound;

    [Tooltip("Khi minigame hoàn thành")]
    public UnityEvent onGameWon;

    [Header("=== SCENE RETURN ===")]
    [SerializeField] private bool returnToPreviousTimelineSceneOnCompleted = true;

    [Header("=== AUDIO (BGM) ===")]
    [Tooltip("Nhạc nền khi chơi Minigame tìm sách (để trống sẽ tự động nạp BookBGM từ Resources)")]
    public AudioClip minigameBGM;
    [SerializeField] private bool playBGMOnStart = true;

    // Runtime state
    private GameState currentState = GameState.Exploring;
    private List<DraggableBookItem> spawnedBooks = new List<DraggableBookItem>();
    private DraggableBookItem currentDraggedBook;
    private ManualBookController currentCorrectBookController;
    private int wrongBooksOpened = 0;
    private bool completionStarted;
    private bool isDraggingWithF = false;
    private Vector3 dragStartMousePos;
    private DraggableBookItem correctBookItem;
    private RectTransform tutorialPanelRect;
    private Canvas tutorialCanvas;
    private CanvasGroup tutorialCanvasGroup;
    private GraphicRaycaster tutorialRaycaster;
    private float correctBookUnsafeTimer;
    private float tutorialStartedAt;
    private bool tutorialFadeRequested;
    private Coroutine tutorialFadeRoutine;
    private DraggableBookItem hoveredBook;

    public GameState CurrentState => currentState;

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        CacheTutorialPanel();
        tutorialStartedAt = Time.unscaledTime;

        // Đảm bảo AudioManager làm mới kết nối và kiểm tra AudioListener
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.EnsureAudioListener();
        }

        // Bắt đầu phát BGM cho minigame nếu được bật
        if (playBGMOnStart)
        {
            if (minigameBGM == null)
            {
                minigameBGM = Resources.Load<AudioClip>("Audio/Book/BookBGM");
            }
            if (minigameBGM != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayMusic(minigameBGM, true);
            }
        }

        // Đăng ký events từ BookInspector
        if (bookInspector != null)
        {
            bookInspector.onWrongBookFlipComplete.AddListener(OnWrongBookFlipComplete);
            bookInspector.onCorrectBookReady.AddListener(OnCorrectBookReady);
        }

        // Spawn đống sách
        SpawnBookPile();

        // Hiển thị gợi ý chính
        if (hintUI != null && gameData != null)
        {
            hintUI.ShowMainHint(gameData.mainHint);
        }
    }

    private void OnDisable()
    {
        UnregisterCorrectBookCompletionListener();
        SetHoveredBook(null);
    }

    void Update()
    {
        UpdateTutorialPresentation();
        UpdateCorrectBookSafety();

        switch (currentState)
        {
            case GameState.Exploring:
                UpdateHoverFeedback();
                HandleExploringInput();
                break;

            case GameState.Dragging:
                SetHoveredBook(null);
                HandleDraggingInput();
                break;

            case GameState.Inspecting:
            case GameState.WrongBook:
            case GameState.CorrectBook:
                SetHoveredBook(null);
                HandleDismissInput();
                break;
        }
    }

    private void HandleDismissInput()
    {
        // Sách đúng đang mở: bỏ qua F hoàn toàn, buộc người chơi tiếp tục đọc.
        if (currentState == GameState.CorrectBook) return;

        if (Input.GetKeyDown(KeyCode.F))
        {
            DraggableBookItem book = bookInspector != null ? bookInspector.CurrentDraggableBook : null;
            if (book == null) return;

            // Nếu là sách đúng thì không vứt
            if (book.bookEntry != null && book.bookEntry.isCorrectBook) return;

            if (hintUI != null)
            {
                hintUI.HideDismissPrompt();
                hintUI.HideAll();
            }

            if (bookInspector != null)
            {
                bookInspector.CancelInspection();
            }

            if (currentState == GameState.WrongBook || currentState == GameState.Inspecting)
            {
                // Lướt sách sang trái màn hình và tiêu hủy
                Vector3 throwDir = -mainCamera.transform.right;
                book.ThrowRightAndDestroy(throwDir, 12f);

                onWrongBookDismissed?.Invoke();
                currentState = GameState.Exploring;
            }
        }
    }

    #region Spawn

    /// <summary>
    /// Spawn tất cả sách từ gameData thành đống 3D lộn xộn
    /// </summary>
    private void SpawnBookPile()
    {
        if (gameData == null || pileCenter == null) return;

        // Phân loại sách đúng và sách sai
        List<BookEntry> correctBooks = new List<BookEntry>();
        List<BookEntry> wrongBooks = new List<BookEntry>();

        foreach (var book in gameData.books)
        {
            if (book.isCorrectBook) correctBooks.Add(book);
            else wrongBooks.Add(book);
        }

        // Chuẩn bị danh sách spawn
        List<BookEntry> spawnList = new List<BookEntry>();

        // Luôn đảm bảo có 1 cuốn đúng
        if (correctBooks.Count > 0)
        {
            spawnList.Add(correctBooks[0]);
        }

        // Nhặt ngẫu nhiên sách sai cho đủ số lượng
        int targetWrongCount = Mathf.Max(0, gameData.totalBooksToSpawn - spawnList.Count);
        if (wrongBooks.Count > 0)
        {
            for (int i = 0; i < targetWrongCount; i++)
            {
                spawnList.Add(wrongBooks[Random.Range(0, wrongBooks.Count)]);
            }
        }

        // Đảo lộn ngẫu nhiên danh sách để sách đúng không luôn nằm dưới đáy
        for (int i = 0; i < spawnList.Count; i++)
        {
            BookEntry temp = spawnList[i];
            int randomIndex = Random.Range(i, spawnList.Count);
            spawnList[i] = spawnList[randomIndex];
            spawnList[randomIndex] = temp;
        }

        Vector3 center = pileCenter.position;
        int booksPerLayer = Mathf.Max(3, gameData.booksPerLayer);
        int totalLayers = Mathf.Max(1, Mathf.CeilToInt(spawnList.Count / (float)booksPerLayer));

        for (int i = 0; i < spawnList.Count; i++)
        {
            BookEntry entry = spawnList[i];
            if (entry.bookPrefab == null) continue;

            GetPileSpawnPose(i, spawnList.Count, booksPerLayer, totalLayers, out Vector3 spawnPos, out Quaternion spawnRot);
            spawnPos += center;

            // Instantiate
            GameObject bookObj = Instantiate(entry.bookPrefab, spawnPos, spawnRot, pileParent);
            bookObj.name = $"Book_{i}_{entry.bookName}";

            if (entry.isCorrectBook)
            {
                if (bookObj.GetComponent<BookPageEffectsController>() == null)
                {
                    bookObj.AddComponent<BookPageEffectsController>();
                }

                if (bookObj.GetComponent<ManualBookController>() == null)
                {
                    bookObj.AddComponent<ManualBookController>();
                }
            }

            // Thêm DraggableBookItem component nếu chưa có
            DraggableBookItem draggable = bookObj.GetComponent<DraggableBookItem>();
            if (draggable == null) draggable = bookObj.AddComponent<DraggableBookItem>();

            // Đảm bảo có Collider
            if (bookObj.GetComponent<Collider>() == null)
            {
                bookObj.AddComponent<BoxCollider>();
            }

            // Đảm bảo có Rigidbody
            if (bookObj.GetComponent<Rigidbody>() == null)
            {
                bookObj.AddComponent<Rigidbody>();
            }

            draggable.Initialize(entry, i, this);
            spawnedBooks.Add(draggable);

            if (entry.isCorrectBook)
            {
                correctBookItem = draggable;
                correctBookUnsafeTimer = 0f;
            }
        }
    }

    private void GetPileSpawnPose(
        int bookIndex,
        int bookCount,
        int booksPerLayer,
        int totalLayers,
        out Vector3 localPosition,
        out Quaternion rotation)
    {
        if (!gameData.useLayeredPile)
        {
            float randomAngle = Random.Range(0f, 360f);
            float randomRadius = Random.Range(0f, gameData.pileRadius);
            localPosition = new Vector3(
                Mathf.Cos(randomAngle * Mathf.Deg2Rad) * randomRadius,
                Random.Range(0.1f, gameData.pileMaxHeight) + (bookIndex * 0.15f),
                Mathf.Sin(randomAngle * Mathf.Deg2Rad) * randomRadius);
            rotation = Quaternion.Euler(
                Random.Range(-gameData.maxRandomRotation, gameData.maxRandomRotation),
                Random.Range(0f, 360f),
                Random.Range(-gameData.maxRandomRotation, gameData.maxRandomRotation));
            return;
        }

        int layer = bookIndex / booksPerLayer;
        int indexInLayer = bookIndex % booksPerLayer;
        int booksInThisLayer = Mathf.Min(booksPerLayer, bookCount - layer * booksPerLayer);
        float layerProgress = totalLayers <= 1 ? 0f : layer / (float)(totalLayers - 1);
        float baseAngle = ((indexInLayer + 0.5f) / booksInThisLayer) * 360f + ((layer & 1) == 0 ? 0f : 29f);
        float angle = baseAngle + Random.Range(-11f, 11f);
        float radius = Mathf.Lerp(gameData.pileRadius * 0.88f, gameData.pileRadius * 0.26f, layerProgress);
        radius += Random.Range(-gameData.pilePositionJitter, gameData.pilePositionJitter);
        radius = Mathf.Max(0.05f, radius);

        float layerHeight = Mathf.Min(gameData.pileLayerHeight, gameData.pileMaxHeight / Mathf.Max(1, totalLayers));
        localPosition = new Vector3(
            Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
            0.08f + layer * layerHeight + Random.Range(0f, gameData.pilePositionJitter * 0.22f),
            Mathf.Sin(angle * Mathf.Deg2Rad) * radius);

        float tilt = gameData.maxRandomRotation * 0.48f;
        rotation = Quaternion.Euler(
            Random.Range(-tilt, tilt),
            angle + 90f + Random.Range(-18f, 18f),
            Random.Range(-tilt, tilt));
    }

    #endregion

    #region Input Handling

    /// <summary>
    /// Xử lý input khi đang ở trạng thái Exploring (lục lọi đống sách)
    /// </summary>
    private void HandleExploringInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            // Loại bỏ LayerMask chặn Raycast phòng trường hợp người dùng lỡ chọn Nothing trên Inspector
            if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance))
            {
                DraggableBookItem book = hit.collider.GetComponentInParent<DraggableBookItem>();
                if (book != null && !book.IsDismissed)
                {
                    Debug.Log($"[BookMiniGame] ĐÃ CLICK TRÚNG SÁCH: {book.name}");
                    currentDraggedBook = book;
                    dragStartMousePos = Input.mousePosition; // Lưu vị trí bắt đầu nhấn chuột
                    MarkTutorialUnderstood();
                    SetHoveredBook(null);

                    // Tính độ cao lý tưởng của mặt phẳng kéo
                    float targetY = (pileCenter != null ? pileCenter.position.y : 0f) + dragLiftHeight;

                    // Ép dính tâm sách vào chuột ngay cả khi bốc từ đống sách
                    currentDraggedBook.StartDrag(hit.point, targetY, true);
                    currentState = GameState.Dragging;
                }
            }
        }
    }

    /// <summary>
    /// Xử lý input khi đang kéo sách
    /// </summary>
    private void HandleDraggingInput()
    {
        if (currentDraggedBook == null)
        {
            currentState = GameState.Exploring;
            isDraggingWithF = false;
            return;
        }

        bool isHolding = isDraggingWithF ? Input.GetKey(KeyCode.F) : Input.GetMouseButton(0);
        bool isReleased = isDraggingWithF ? Input.GetKeyUp(KeyCode.F) : Input.GetMouseButtonUp(0);

        if (isHolding)
        {
            // Tính vị trí chuột trên mặt phẳng kéo
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            // Dùng DragPlaneY đã cố định từ StartDrag
            Plane dragPlane = new Plane(Vector3.up, new Vector3(0, currentDraggedBook.DragPlaneY, 0));

            if (dragPlane.Raycast(ray, out float distance))
            {
                Vector3 worldPos = ray.GetPoint(distance);
                currentDraggedBook.UpdateDrag(worldPos);
            }

            // Kiểm tra hover trên vùng bàn kiểm tra
            if (inspectionArea != null)
            {
                if (inspectionArea.IsInsideArea(currentDraggedBook.transform.position))
                {
                    inspectionArea.ShowHighlight();
                }
                else
                {
                    inspectionArea.HideHighlight();
                }
            }
        }

        // Thả chuột hoặc thả phím F
        if (isReleased)
        {
            // Kiểm tra khoảng cách chuột đã di chuyển so với lúc nhấn (phân biệt Click và Drag)
            float dragDistance = Vector3.Distance(Input.mousePosition, dragStartMousePos);

            // Nếu di chuyển rất ít (< 15 pixel) và là click chuột trái (không phải phím F) -> Click Mở Sách
            if (dragDistance < 15f && !isDraggingWithF)
            {
                currentDraggedBook.EndDrag();
                InspectBookHovering(currentDraggedBook);
            }
            else // Kéo xa hoặc dùng phím F -> Thả sách rớt tự do
            {
                currentDraggedBook.EndDrag();
                currentState = GameState.Exploring;
            }

            // Tắt highlight của bàn nếu có
            if (inspectionArea != null) inspectionArea.HideHighlight();

            currentDraggedBook = null;
            isDraggingWithF = false;
        }
    }

    private void UpdateHoverFeedback()
    {
        if (!enableHoverFeedback || mainCamera == null)
        {
            SetHoveredBook(null);
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        DraggableBookItem pointedBook = null;
        if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance))
        {
            DraggableBookItem candidate = hit.collider.GetComponentInParent<DraggableBookItem>();
            if (candidate != null && !candidate.IsDismissed && !candidate.IsDragging && !candidate.IsRecovering)
            {
                pointedBook = candidate;
            }
        }

        SetHoveredBook(pointedBook);
    }

    private void SetHoveredBook(DraggableBookItem book)
    {
        if (hoveredBook == book) return;

        if (hoveredBook != null)
        {
            BookHoverFeedback oldFeedback = hoveredBook.GetComponent<BookHoverFeedback>();
            if (oldFeedback != null) oldFeedback.SetHovered(false);
        }

        hoveredBook = book;
        if (hoveredBook != null && enableHoverFeedback)
        {
            BookHoverFeedback feedback = hoveredBook.GetComponent<BookHoverFeedback>();
            if (feedback == null) feedback = hoveredBook.gameObject.AddComponent<BookHoverFeedback>();
            feedback.SetHovered(true);
        }

    }

    #endregion

    #region Quest Book Safety

    private void CacheTutorialPanel()
    {
        GameObject panelObject = GameObject.Find("TutorialCanvas/TutorialPanel");
        if (panelObject == null) return;

        tutorialPanelRect = panelObject.GetComponent<RectTransform>();
        tutorialCanvas = panelObject.GetComponentInParent<Canvas>();
        tutorialCanvasGroup = panelObject.GetComponent<CanvasGroup>();
        if (tutorialCanvasGroup == null) tutorialCanvasGroup = panelObject.AddComponent<CanvasGroup>();

        // Tutorial chỉ để nhìn, không được phép trở thành một lớp UI chặn thao tác gameplay.
        tutorialCanvasGroup.blocksRaycasts = false;
        tutorialCanvasGroup.interactable = false;
        tutorialRaycaster = tutorialCanvas != null ? tutorialCanvas.GetComponent<GraphicRaycaster>() : null;
        if (tutorialRaycaster != null) tutorialRaycaster.enabled = false;
    }

    private void UpdateTutorialPresentation()
    {
        if (!autoFadeTutorial || tutorialCanvasGroup == null || tutorialFadeRequested) return;

        if (Time.unscaledTime >= tutorialStartedAt + tutorialFallbackHideTime)
        {
            RequestTutorialFade();
        }
    }

    private void MarkTutorialUnderstood()
    {
        if (!autoFadeTutorial || tutorialCanvasGroup == null || tutorialFadeRequested) return;
        RequestTutorialFade();
    }

    private void RequestTutorialFade()
    {
        if (tutorialFadeRequested || tutorialCanvasGroup == null) return;
        tutorialFadeRequested = true;
        tutorialFadeRoutine = StartCoroutine(FadeTutorialRoutine());
    }

    private IEnumerator FadeTutorialRoutine()
    {
        float waitUntil = tutorialStartedAt + tutorialMinimumVisibleTime;
        while (Time.unscaledTime < waitUntil)
        {
            yield return null;
        }

        float startAlpha = tutorialCanvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < tutorialFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            tutorialCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(elapsed / tutorialFadeDuration));
            yield return null;
        }

        tutorialCanvasGroup.alpha = 0f;
        if (tutorialPanelRect != null) tutorialPanelRect.gameObject.SetActive(false);
        tutorialFadeRoutine = null;
    }

    private void RestoreTutorial()
    {
        if (tutorialFadeRoutine != null) StopCoroutine(tutorialFadeRoutine);
        tutorialFadeRoutine = null;
        tutorialFadeRequested = false;
        tutorialStartedAt = Time.unscaledTime;
        if (tutorialPanelRect != null) tutorialPanelRect.gameObject.SetActive(true);
        if (tutorialCanvasGroup != null)
        {
            tutorialCanvasGroup.alpha = 1f;
            tutorialCanvasGroup.blocksRaycasts = false;
            tutorialCanvasGroup.interactable = false;
        }
        if (tutorialRaycaster != null) tutorialRaycaster.enabled = false;
    }

    private void UpdateCorrectBookSafety()
    {
        if (!recoverLostCorrectBook
            || correctBookItem == null
            || completionStarted
            || currentState != GameState.Exploring
            || correctBookItem.IsDragging
            || correctBookItem.IsRecovering
            || !correctBookItem.HasBeenDragged)
        {
            correctBookUnsafeTimer = 0f;
            return;
        }

        bool isUnsafe = IsCorrectBookOutsideSafeArea(correctBookItem);
        if (!isUnsafe)
        {
            correctBookUnsafeTimer = 0f;
            return;
        }

        correctBookUnsafeTimer += Time.deltaTime;
        if (correctBookUnsafeTimer < correctBookRecoveryDelay) return;

        correctBookUnsafeTimer = 0f;
        Vector3 recoveryPosition = GetCorrectBookRecoveryPosition();
        // Giữ sách ở vị trí này để nó không rơi trở lại đống và bị che lần nữa.
        correctBookItem.RecoverTo(recoveryPosition, keepAtRecoveryPoint: true);
    }

    private bool IsCorrectBookOutsideSafeArea(DraggableBookItem book)
    {
        if (mainCamera == null || pileCenter == null) return false;

        Vector3 center = book.InteractionCenter;
        Vector3 viewport = mainCamera.WorldToViewportPoint(center);
        const float viewportMargin = 0.035f;
        bool outsideCamera = viewport.z <= 0f
            || viewport.x < viewportMargin
            || viewport.x > 1f - viewportMargin
            || viewport.y < viewportMargin
            || viewport.y > 1f - viewportMargin;

        Vector2 horizontalDelta = new Vector2(
            center.x - pileCenter.position.x,
            center.z - pileCenter.position.z);
        bool tooFar = horizontalDelta.sqrMagnitude > correctBookMaxDistance * correctBookMaxDistance;
        bool fellBelowWorld = center.y < pileCenter.position.y - correctBookMaxFallDistance;
        bool hiddenByTutorial = IsScreenPointInsideTutorial(mainCamera.WorldToScreenPoint(center));

        return outsideCamera || tooFar || fellBelowWorld || hiddenByTutorial;
    }

    private bool IsScreenPointInsideTutorial(Vector3 screenPoint)
    {
        if (tutorialPanelRect == null
            || tutorialCanvas == null
            || !tutorialPanelRect.gameObject.activeInHierarchy
            || (tutorialCanvasGroup != null && tutorialCanvasGroup.alpha <= 0.02f)
            || screenPoint.z <= 0f)
        {
            return false;
        }

        Camera canvasCamera = tutorialCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : tutorialCanvas.worldCamera != null ? tutorialCanvas.worldCamera : mainCamera;

        return RectTransformUtility.RectangleContainsScreenPoint(
            tutorialPanelRect,
            new Vector2(screenPoint.x, screenPoint.y),
            canvasCamera);
    }

    private Vector3 GetCorrectBookRecoveryPosition()
    {
        float recoveryY = pileCenter.position.y + Mathf.Max(0.55f, dragLiftHeight * 0.42f);
        // Điểm giữa camera luôn nằm trước người chơi, không thể bị che bởi tutorial.
        Ray centerRay = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.55f, 0f));
        Plane safePlane = new Plane(Vector3.up, new Vector3(0f, recoveryY, 0f));
        if (safePlane.Raycast(centerRay, out float distance))
        {
            return centerRay.GetPoint(distance);
        }

        return pileCenter.position + Vector3.up * (recoveryY - pileCenter.position.y);
    }

    #endregion

    #region Book Inspection

    /// <summary>
    /// Mở sách lơ lửng trước mặt người chơi thay vì bắt buộc đặt lên bàn
    /// </summary>
    private void InspectBookHovering(DraggableBookItem book)
    {
        currentState = GameState.Inspecting;

        if (inspectionHoverPoint != null)
        {
            book.SnapToPosition(inspectionHoverPoint.position, inspectionHoverPoint.rotation);
        }
        else
        {
            // Tính toán tự động: Bay cách camera theo thông số autoHoverDistance
            Vector3 hoverPos = mainCamera.transform.position + mainCamera.transform.forward * autoHoverDistance;

            // Trục cơ sở: Xoay đối diện Camera
            Quaternion baseRot = Quaternion.LookRotation(mainCamera.transform.up, -mainCamera.transform.forward);

            // Cộng thêm bù trừ góc do người chơi tự chỉnh (nếu sách bị nghiêng/úp/ngược)
            Quaternion finalRot = baseRot * Quaternion.Euler(autoHoverRotationOffset);

            book.SnapToPosition(hoverPos, finalRot);
        }

        // Kiểm tra loại sách: nếu là sách sai thì hiện UI phím F để vứt/skip
        if (hintUI != null)
        {
            if (book.bookEntry != null && !book.bookEntry.isCorrectBook)
            {
                hintUI.ShowDismissPrompt();
            }
            else
            {
                hintUI.HideDismissPrompt();
            }
        }

        // Kiểm tra bằng BookInspector (sử dụng EndlessBook)
        if (bookInspector != null)
        {
            bookInspector.autoFlipSpeed = gameData.wrongBookFlipSpeed;
            bookInspector.coverAnimTime = gameData.coverAnimationTime;
            bookInspector.InspectBook(book);
        }
        else
        {
            Debug.LogError("LỖI NGHIÊM TRỌNG: Không tìm thấy BookInspector! Bạn đã lỡ xóa nó (cùng với cái bàn)? Hãy gắn script BookInspector vào Manager và kéo nó vào ô Book Inspector!");

            // Fallback khẩn cấp để game không bị kẹt (Đóng vai trò thay thế OnWrongBookFlipComplete)
            currentState = GameState.Exploring;
        }
    }

    /// <summary>
    /// Callback khi sách sai đã lật xong
    /// </summary>
    private void OnWrongBookFlipComplete()
    {
        currentState = GameState.WrongBook;
        wrongBooksOpened++;

        DraggableBookItem wrongBook = bookInspector.CurrentDraggableBook;

        // Hiển thị UI
        if (hintUI != null)
        {
            hintUI.ShowWrongResult();
            hintUI.ShowDismissPrompt();

            // Hiển thị sub hint nếu có
            if (wrongBook != null && !string.IsNullOrEmpty(wrongBook.bookEntry.subHintOnOpen))
            {
                hintUI.ShowSubHint(wrongBook.bookEntry.subHintOnOpen);
            }
        }

        // Fire event cho VFX/SFX
        onWrongBookOpened?.Invoke();

        // Sách sai không tự động loại bỏ nữa, chờ người chơi bấm F ở hàm HandleDismissInput

    }



    /// <summary>
    /// Callback khi sách đúng đã mở xong và sẵn sàng cho người chơi lật
    /// </summary>
    private void OnCorrectBookReady()
    {
        currentState = GameState.CorrectBook;
        RegisterCorrectBookCompletionListener();

        // Hiển thị UI
        if (hintUI != null)
        {
            hintUI.ShowCorrectResult();
            hintUI.HideDismissPrompt();
        }

        // Fire events cho VFX/SFX
        onCorrectBookFound?.Invoke();

        // Ẩn gợi ý chính vì đã tìm thấy rồi
        if (hintUI != null) hintUI.HideMainHint();
    }

    private void RegisterCorrectBookCompletionListener()
    {
        UnregisterCorrectBookCompletionListener();

        DraggableBookItem book = bookInspector != null ? bookInspector.CurrentDraggableBook : null;
        if (book == null)
        {
            return;
        }

        currentCorrectBookController = book.GetComponent<ManualBookController>();
        if (currentCorrectBookController == null)
        {
            currentCorrectBookController = book.gameObject.AddComponent<ManualBookController>();
        }

        if (currentCorrectBookController != null)
        {
            currentCorrectBookController.onReachedEndOfBook.AddListener(CompleteGame);
        }
    }

    private void UnregisterCorrectBookCompletionListener()
    {
        if (currentCorrectBookController == null)
        {
            return;
        }

        currentCorrectBookController.onReachedEndOfBook.RemoveListener(CompleteGame);
        currentCorrectBookController = null;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Gọi khi người chơi đã đọc xong sách đúng (ví dụ: bấm nút "Hoàn thành")
    /// </summary>
    public void CompleteGame()
    {
        if (completionStarted)
        {
            return;
        }

        completionStarted = true;
        UnregisterCorrectBookCompletionListener();
        currentState = GameState.GameWon;
        onGameWon?.Invoke();

        if (bookInspector != null) bookInspector.CloseCorrectBook();
        if (hintUI != null)
        {
            hintUI.HideDismissPrompt();
            hintUI.HideAll();
        }

        if (returnToPreviousTimelineSceneOnCompleted && global::TimelineSceneTravelManager.HasPreviousScene)
        {
            global::TimelineSceneTravelManager.ReturnToPreviousScene();
            return;
        }

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Reset toàn bộ minigame
    /// </summary>
    public void ResetGame()
    {
        StopAllCoroutines();

        // Xóa sách cũ
        foreach (var book in spawnedBooks)
        {
            if (book != null) Destroy(book.gameObject);
        }
        spawnedBooks.Clear();

        // Reset inspector
        if (bookInspector != null) bookInspector.CloseCorrectBook();

        // Reset state
        currentState = GameState.Exploring;
        wrongBooksOpened = 0;
        completionStarted = false;
        currentDraggedBook = null;
        correctBookItem = null;
        correctBookUnsafeTimer = 0f;
        UnregisterCorrectBookCompletionListener();
        SetHoveredBook(null);
        RestoreTutorial();

        // Spawn lại
        SpawnBookPile();

        // Hiển thị lại hint
        if (hintUI != null && gameData != null)
        {
            hintUI.HideDismissPrompt();
            hintUI.ShowMainHint(gameData.mainHint);
        }
    }

    #endregion
}
