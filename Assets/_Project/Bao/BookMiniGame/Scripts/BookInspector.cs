using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using echo17.EndlessBook;

/// <summary>
/// Quản lý quá trình kiểm tra sách khi đặt lên bàn.
/// Phiên bản này dùng trực tiếp component EndlessBook có sẵn trên từng prefab sách.
/// </summary>
public class BookInspector : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Thời gian mở/đóng bìa")]
    public float coverAnimTime = 0.5f;

    [Tooltip("Tốc độ lật trang tự động (sách sai)")]
    public float autoFlipSpeed = 0.3f;

    [Header("Audio Settings (SFX)")]
    [Tooltip("Âm thanh khi lật 1 trang sách")]
    public AudioClip pageTurnSFX;

    [Tooltip("Âm thanh khi lật liên tục nhiều trang (sách sai)")]
    public AudioClip pagesFlippingSFX;

    [Tooltip("Âm thanh khi mở bìa sách")]
    public AudioClip bookOpenSFX;

    [Tooltip("Âm thanh khi đóng bìa sách")]
    public AudioClip bookCloseSFX;

    [Tooltip("Độ biến thiên pitch ngẫu nhiên")]
    [Range(0f, 0.2f)]
    public float pitchVariance = 0.06f;

    [Header("Events")]
    public UnityEvent onWrongBookFlipComplete;
    public UnityEvent onCorrectBookReady;
    public UnityEvent onBookClosed;

    private bool isInspecting = false;
    private DraggableBookItem currentDraggableBook;
    private EndlessBook currentBook;
    private ManualBookController currentManualController;

    public bool IsInspecting => isInspecting;
    public DraggableBookItem CurrentDraggableBook => currentDraggableBook;

    private void Awake()
    {
        LoadDefaultAudio();
    }

    private void LoadDefaultAudio()
    {
        if (pageTurnSFX == null) pageTurnSFX = Resources.Load<AudioClip>("Audio/Book/PageTurn");
        if (pagesFlippingSFX == null) pagesFlippingSFX = Resources.Load<AudioClip>("Audio/Book/PagesFlipping");
        if (bookOpenSFX == null) bookOpenSFX = Resources.Load<AudioClip>("Audio/Book/BookOpen");
        if (bookCloseSFX == null) bookCloseSFX = Resources.Load<AudioClip>("Audio/Book/BookClose");
    }

    /// <summary>
    /// Bắt đầu kiểm tra một cuốn sách.
    /// Lấy trực tiếp EndlessBook từ cuốn sách được kéo lên.
    /// </summary>
    public void InspectBook(DraggableBookItem draggableBook)
    {
        if (isInspecting) return;

        isInspecting = true;
        currentDraggableBook = draggableBook;
        
        // Lấy trực tiếp component từ cuốn sách vừa quăng lên bàn
        currentBook = draggableBook.GetComponent<EndlessBook>();
        currentManualController = draggableBook.GetComponent<ManualBookController>();
        if (currentManualController == null && draggableBook.bookEntry != null && draggableBook.bookEntry.isCorrectBook)
        {
            currentManualController = draggableBook.gameObject.AddComponent<ManualBookController>();
        }

        if (currentBook == null)
        {
            Debug.LogError($"Cuốn sách {draggableBook.name} không có component EndlessBook! Vui lòng gắn vào Prefab.");
            isInspecting = false;
            return;
        }

        // Tạm tắt điều khiển thủ công
        if (currentManualController != null) currentManualController.enabled = false;

        // Đảm bảo sách đang ở trạng thái đóng
        currentBook.SetState(EndlessBook.StateEnum.ClosedFront, 0f, null);

        if (draggableBook.bookEntry.isCorrectBook)
        {
            StartCoroutine(InspectCorrectBook());
        }
        else
        {
            StartCoroutine(InspectWrongBook());
        }
    }

    /// <summary>
    /// Sách SAI: Mở bìa → Lật tự động tất cả trang → Đóng lại → Báo event
    /// </summary>
    private IEnumerator InspectWrongBook()
    {
        yield return null;

        // Mở bìa
        PlayBookOpenSound();
        bool stateChanged = false;
        currentBook.SetState(EndlessBook.StateEnum.OpenMiddle, coverAnimTime,
            onCompleted: (from, to, page) => { stateChanged = true; });

        yield return new WaitUntil(() => stateChanged);
        yield return new WaitForSeconds(0.2f);

        // Lật tự động đến trang cuối (nếu có trang)
        bool flipComplete = false;
        if (currentBook.LastPageNumber > 0)
        {
            if (pagesFlippingSFX != null)
            {
                PlayPagesFlippingSound();
            }

            currentBook.TurnToPage(currentBook.LastPageNumber,
                EndlessBook.PageTurnTimeTypeEnum.TimePerPage,
                autoFlipSpeed,
                onCompleted: (from, to, page) => { flipComplete = true; },
                onPageTurnStart: (page, f, b, v1, v2, dir) => {
                    if (pagesFlippingSFX == null) PlayPageTurnSound();
                }
            );
            yield return new WaitUntil(() => flipComplete);
        }
        else
        {
            // Sách không có trang nào, skip qua
            flipComplete = true;
        }
        
        yield return new WaitForSeconds(0.5f);

        // Mở ra bìa sau
        stateChanged = false;
        currentBook.SetState(EndlessBook.StateEnum.OpenBack, coverAnimTime,
            onCompleted: (from, to, page) => { stateChanged = true; });
        yield return new WaitUntil(() => stateChanged);

        // Đóng sách lại (mặt sau)
        PlayBookCloseSound();
        stateChanged = false;
        currentBook.SetState(EndlessBook.StateEnum.ClosedBack, coverAnimTime,
            onCompleted: (from, to, page) => { stateChanged = true; });
        yield return new WaitUntil(() => stateChanged);

        onWrongBookFlipComplete?.Invoke();
        isInspecting = false;
    }

    /// <summary>
    /// Sách ĐÚNG: Mở bìa → Bật ManualBookController cho người chơi lật thủ công
    /// </summary>
    private IEnumerator InspectCorrectBook()
    {
        yield return null;

        PlayBookOpenSound();
        bool stateChanged = false;
        currentBook.SetState(EndlessBook.StateEnum.OpenMiddle, coverAnimTime,
            onCompleted: (from, to, page) => { stateChanged = true; });

        yield return new WaitUntil(() => stateChanged);

        // Kích hoạt điều khiển bằng tay
        if (currentManualController == null && currentDraggableBook != null)
        {
            currentManualController = currentDraggableBook.GetComponent<ManualBookController>();
            if (currentManualController == null)
            {
                currentManualController = currentDraggableBook.gameObject.AddComponent<ManualBookController>();
            }
        }

        if (currentManualController != null)
        {
            currentManualController.enabled = true;
        }
        
        onCorrectBookReady?.Invoke();
    }
    
    /// <summary>
    /// Hủy bỏ quá trình kiểm tra ngay lập tức (dùng khi người chơi bấm F để skip / vứt sách)
    /// </summary>
    public void CancelInspection()
    {
        StopAllCoroutines();
        isInspecting = false;
        if (currentManualController != null)
        {
            currentManualController.enabled = false;
        }
    }

    /// <summary>
    /// Đóng sách thủ công (gọi bằng UI Button hoặc logic bên ngoài nếu cần)
    /// </summary>
    public void CloseCorrectBook()
    {
        if (currentBook != null && isInspecting)
        {
            if (currentManualController != null) currentManualController.enabled = false;
            
            PlayBookCloseSound();
            currentBook.SetState(EndlessBook.StateEnum.ClosedFront, coverAnimTime,
                onCompleted: (from, to, page) => { 
                    isInspecting = false;
                    onBookClosed?.Invoke();
                });
        }
    }

    public void PlayPageTurnSound()
    {
        PlaySFX(pageTurnSFX);
    }

    public void PlayPagesFlippingSound()
    {
        PlaySFX(pagesFlippingSFX != null ? pagesFlippingSFX : pageTurnSFX);
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
}
