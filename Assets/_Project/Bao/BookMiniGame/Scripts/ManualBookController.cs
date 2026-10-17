using UnityEngine;
using UnityEngine.Events;
using echo17.EndlessBook;

/// <summary>
/// Điều khiển lật sách thủ công cho cuốn sách đúng.
/// Hỗ trợ cả Click chuột (trái/phải), Kéo trang (Drag), và phím bàn phím (A/D, Mũi tên, Space).
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class ManualBookController : MonoBehaviour
{
    [Header("Book Settings")]
    [Tooltip("Kéo object Endless Book vào đây. Nếu để trống, script sẽ tự tìm trên cùng object này.")]
    public EndlessBook book;
    
    [Tooltip("Camera dùng để lấy vị trí con trỏ chuột. Mặc định là Camera.main")]
    public Camera sceneCamera;

    [Header("Turn & State Settings")]
    [Tooltip("Tốc độ tự động lật trang (giây).")]
    public float turnSpeed = 0.45f;
    
    [Tooltip("Tốc độ đóng/mở các trang bìa cứng.")]
    public float stateAnimationTime = 0.5f;

    [Tooltip("Nếu thả chuột trước khi qua nửa quyển sách, trang sẽ tự lật ngược về chỗ cũ.")]
    public bool reverseIfNotMidway = true;

    [Header("Manual Page Interaction")]
    [Tooltip("Quãng đường chuột tối thiểu để được xem là vuốt trang thay vì click.")]
    [Range(0f, 50f)]
    public float dragActivationPixels = 12f;

    [Header("Audio Settings (SFX)")]
    [Tooltip("Âm thanh khi lật 1 trang sách")]
    public AudioClip pageTurnSFX;
    
    [Tooltip("Âm thanh khi mở bìa sách")]
    public AudioClip bookOpenSFX;
    
    [Tooltip("Âm thanh khi đóng bìa sách")]
    public AudioClip bookCloseSFX;

    [Tooltip("Độ biến thiên pitch ngẫu nhiên để âm thanh tự nhiên hơn")]
    [Range(0f, 0.2f)]
    public float pitchVariance = 0.06f;

    [Header("Events")]
    public UnityEvent onReachedEndOfBook;

    private BoxCollider boxCollider;
    private bool isMouseDown = false;
    private bool isDraggingPage = false;
    private bool reachedEndOfBook;
    private Vector3 mouseDownPos;
    private Vector3 originalColliderSize;
    private Vector3 originalColliderCenter;

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
        if (book == null) book = GetComponent<EndlessBook>();
        if (sceneCamera == null) sceneCamera = Camera.main;
        LoadDefaultAudio();
    }

    private void LoadDefaultAudio()
    {
        if (pageTurnSFX == null) pageTurnSFX = Resources.Load<AudioClip>("Audio/Book/PageTurn");
        if (bookOpenSFX == null) bookOpenSFX = Resources.Load<AudioClip>("Audio/Book/BookOpen");
        if (bookCloseSFX == null) bookCloseSFX = Resources.Load<AudioClip>("Audio/Book/BookClose");
    }

    private void OnEnable()
    {
        reachedEndOfBook = false;
        if (sceneCamera == null) sceneCamera = Camera.main;
        if (book == null) book = GetComponent<EndlessBook>();

        if (boxCollider != null)
        {
            originalColliderSize = boxCollider.size;
            originalColliderCenter = boxCollider.center;
            // Đảm bảo box collider bao phủ toàn bộ sách mở để bắt raycast chuẩn xác
            boxCollider.size = new Vector3(Mathf.Max(originalColliderSize.x * 2.5f, 2f), Mathf.Max(originalColliderSize.y, 0.5f), Mathf.Max(originalColliderSize.z * 1.5f, 1.5f));
            boxCollider.center = originalColliderCenter;
        }
    }

    private void OnDisable()
    {
        if (boxCollider != null && originalColliderSize != Vector3.zero)
        {
            boxCollider.size = originalColliderSize;
            boxCollider.center = originalColliderCenter;
        }
    }

    private void Update()
    {
        if (!enabled || book == null) return;
        if (sceneCamera == null) sceneCamera = Camera.main;

        // 1. HỖ TRỢ PHÍM BÀN PHÍM (A/D, Mũi tên Trái/Phải, Space, Enter)
        HandleKeyboardInput();

        // 2. HỖ TRỢ CHUỘT (CLICK & DRAG)
        HandleMouseInput();
    }

    private void HandleKeyboardInput()
    {
        if (book.IsTurningPages) return;

        // Lật tới (D / Phím Mũi Tên Phải / Space / Enter)
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
        {
            TurnPageNext();
        }
        // Lật lui (A / Phím Mũi Tên Trái)
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            TurnPagePrevious();
        }
    }

    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (book.IsTurningPages) return;

            // Kiểm tra click có trúng sách không (hoặc click trên màn hình nếu đang xem sách)
            if (IsPointerOnBook(out float normalizedX))
            {
                isMouseDown = true;
                isDraggingPage = false;
                mouseDownPos = Input.mousePosition;

                // Chuẩn bị drag nếu sách đang ở OpenMiddle
                if (book.CurrentState == EndlessBook.StateEnum.OpenMiddle)
                {
                    Page.TurnDirectionEnum direction = normalizedX > 0.5f ? Page.TurnDirectionEnum.TurnForward : Page.TurnDirectionEnum.TurnBackward;
                    if (book.TurnPageDragStart(direction))
                    {
                        isDraggingPage = true;
                    }
                }
            }
        }

        if (isMouseDown && Input.GetMouseButton(0))
        {
            if (isDraggingPage && book.IsDraggingPage)
            {
                float normalizedX = GetNormalizedMouseX();
                book.TurnPageDrag(normalizedX);
            }
        }

        if (isMouseDown && Input.GetMouseButtonUp(0))
        {
            if (!isMouseDown) return;
            isMouseDown = false;
            float dragDistance = Vector3.Distance(mouseDownPos, Input.mousePosition);

            // Nếu người chơi kéo trang với khoảng cách rõ ràng
            if (isDraggingPage && book.IsDraggingPage && dragDistance > dragActivationPixels)
            {
                bool shouldReverse = reverseIfNotMidway && (book.TurnPageDragNormalizedTime < 0.5f);
                if (!shouldReverse)
                {
                    PlayPageTurnSound();
                }
                book.TurnPageDragStop(turnSpeed, null, reverse: shouldReverse);
                isDraggingPage = false;
                return;
            }

            // Nếu là CLICK chuột (không kéo hoặc kéo rất ngắn)
            if (isDraggingPage && book.IsDraggingPage)
            {
                // Hủy drag dở dang và lật sang trang mới dứt khoát
                PlayPageTurnSound();
                book.TurnPageDragStop(turnSpeed, null, reverse: false);
                isDraggingPage = false;
                return;
            }

            // Xử lý Click theo nửa bên trái / bên phải của sách
            float clickX = GetNormalizedMouseX();
            if (clickX > 0.5f)
            {
                TurnPageNext();
            }
            else
            {
                TurnPagePrevious();
            }
        }
    }

    /// <summary>
    /// Lật sang trang tiếp theo
    /// </summary>
    public void TurnPageNext()
    {
        if (book == null || book.IsTurningPages) return;

        switch (book.CurrentState)
        {
            case EndlessBook.StateEnum.ClosedFront:
                PlayBookOpenSound();
                book.SetState(EndlessBook.StateEnum.OpenFront, stateAnimationTime, null);
                break;

            case EndlessBook.StateEnum.OpenFront:
                PlayBookOpenSound();
                book.SetState(EndlessBook.StateEnum.OpenMiddle, stateAnimationTime, null);
                break;

            case EndlessBook.StateEnum.OpenMiddle:
                if (!book.IsLastPageGroup)
                {
                    PlayPageTurnSound();
                    book.TurnForward(turnSpeed);
                }
                else
                {
                    // Đang ở trang cuối -> Mở ra bìa sau và hoàn thành
                    PlayBookCloseSound();
                    book.SetState(EndlessBook.StateEnum.OpenBack, stateAnimationTime,
                        onCompleted: (from, to, page) => NotifyReachedEndOfBook());
                }
                break;

            case EndlessBook.StateEnum.OpenBack:
                PlayBookCloseSound();
                book.SetState(EndlessBook.StateEnum.ClosedBack, stateAnimationTime,
                    onCompleted: (from, to, page) => NotifyReachedEndOfBook());
                break;
        }
    }

    /// <summary>
    /// Lật lùi lại trang trước
    /// </summary>
    public void TurnPagePrevious()
    {
        if (book == null || book.IsTurningPages) return;

        switch (book.CurrentState)
        {
            case EndlessBook.StateEnum.OpenMiddle:
                if (!book.IsFirstPageGroup)
                {
                    PlayPageTurnSound();
                    book.TurnBackward(turnSpeed);
                }
                else
                {
                    PlayBookCloseSound();
                    book.SetState(EndlessBook.StateEnum.OpenFront, stateAnimationTime, null);
                }
                break;

            case EndlessBook.StateEnum.OpenFront:
                PlayBookCloseSound();
                book.SetState(EndlessBook.StateEnum.ClosedFront, stateAnimationTime, null);
                break;

            case EndlessBook.StateEnum.OpenBack:
                PlayBookOpenSound();
                book.SetState(EndlessBook.StateEnum.OpenMiddle, stateAnimationTime, null);
                break;

            case EndlessBook.StateEnum.ClosedBack:
                PlayBookOpenSound();
                book.SetState(EndlessBook.StateEnum.OpenBack, stateAnimationTime, null);
                break;
        }
    }

    public void PlayPageTurnSound()
    {
        PlaySFX(pageTurnSFX);
    }

    public void PlayBookOpenSound()
    {
        PlaySFX(bookOpenSFX);
    }

    public void PlayBookCloseSound()
    {
        PlaySFX(bookCloseSFX);
    }

    private void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        if (AudioManager.Instance != null)
        {
            float pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
            AudioManager.Instance.PlaySFX(clip, transform.position, pitch);
        }
    }

    private bool IsPointerOnBook(out float normalizedX)
    {
        normalizedX = GetNormalizedMouseX();

        if (sceneCamera == null) sceneCamera = Camera.main;
        if (sceneCamera == null) return false;

        Ray ray = sceneCamera.ScreenPointToRay(Input.mousePosition);

        // 1. Raycast trực tiếp vào collider của sách
        if (Physics.Raycast(ray, out RaycastHit hit, 50f))
        {
            if (hit.collider == boxCollider || hit.collider.transform.IsChildOf(transform) || hit.collider.transform == transform)
            {
                return true;
            }
        }

        // 2. Fallback: Nếu sách đang hiển thị cận cảnh trước màn hình, click trong vùng giữa màn hình vẫn nhận
        Vector3 viewportPoint = sceneCamera.ScreenToViewportPoint(Input.mousePosition);
        if (viewportPoint.x >= 0.15f && viewportPoint.x <= 0.85f && viewportPoint.y >= 0.1f && viewportPoint.y <= 0.9f)
        {
            return true;
        }

        return false;
    }

    private float GetNormalizedMouseX()
    {
        if (sceneCamera == null) sceneCamera = Camera.main;
        if (sceneCamera == null) return 0.5f;

        Ray ray = sceneCamera.ScreenPointToRay(Input.mousePosition);

        // Raycast vào BoxCollider nếu trúng
        if (boxCollider != null && Physics.Raycast(ray, out RaycastHit hit, 50f))
        {
            if (hit.collider == boxCollider || hit.collider.transform.IsChildOf(transform))
            {
                Vector3 localPoint = transform.InverseTransformPoint(hit.point);
                float halfWidth = Mathf.Max(0.1f, boxCollider.size.x * 0.5f);
                return Mathf.Clamp01((localPoint.x + halfWidth) / (halfWidth * 2f));
            }
        }

        // Dựa trên vị trí chuột trên màn hình Viewport
        Vector3 vp = sceneCamera.ScreenToViewportPoint(Input.mousePosition);
        return Mathf.Clamp01(vp.x);
    }

    private void NotifyReachedEndOfBook()
    {
        if (reachedEndOfBook) return;
        reachedEndOfBook = true;
        onReachedEndOfBook?.Invoke();
    }
}
