using UnityEngine;
using DG.Tweening;

/// <summary>
/// Tường Ma Thuật 2D (Magic Barrier / Wall).
/// Chặn Tinh linh bay qua khi đang BẬT.
/// Tự động thu nhỏ trục X về 0 (thu hẹp bức tường mượt mà) khi nhận tín hiệu từ Nút bấm / Cần gạt.
/// </summary>
public class MagicWall2D : MonoBehaviour
{
    public enum BarrierFilterType
    {
        BlockAll,        // Chặn tất cả Tinh linh (Cần nhấn nút để tắt bức tường)
        BlockWaterOnly,  // Chỉ chặn Tinh linh Nước (Tinh linh Lửa đi qua tự do)
        BlockFireOnly    // Chỉ chặn Tinh linh Lửa (Tinh linh Nước đi qua tự do)
    }

    [Header("Cấu hình Rào Cản")]
    [SerializeField] private BarrierFilterType filterType = BarrierFilterType.BlockAll;
    [SerializeField] private bool startActive = true;

    [Header("Thành phần Visual & Physics")]
    [SerializeField] private Collider2D barrierCollider;
    [SerializeField] private SpriteRenderer barrierSprite;
    [SerializeField] private ParticleSystem barrierParticles;

    [Header("Hiệu ứng Co giãn Trục X")]
    [Tooltip("Thời gian thu nhỏ / mở rộng bức tường (giây)")]
    [SerializeField] private float scaleDuration = 0.4f;
    [SerializeField] private Ease openEase = Ease.InQuad;
    [SerializeField] private Ease closeEase = Ease.OutQuad;

    private bool isActive;
    private Vector3 originalScale;
    private Tween scaleTween;

    private void Awake()
    {
        if (barrierCollider == null) barrierCollider = GetComponent<Collider2D>();
        if (barrierSprite == null) barrierSprite = GetComponent<SpriteRenderer>();
        if (barrierParticles == null) barrierParticles = GetComponentInChildren<ParticleSystem>();

        originalScale = transform.localScale;
    }

    private void Start()
    {
        SetBarrierState(startActive, true);
    }

    /// <summary>
    /// Hàm gọi từ OnPressed của Nút bấm -> Thu nhỏ trục X về 0 (Mở đường)
    /// </summary>
    public void DeactivateBarrier()
    {
        SetBarrierState(false);
    }

    /// <summary>
    /// Hàm gọi từ OnReleased của Nút bấm -> Phóng to trục X về ban đầu (Đóng đường)
    /// </summary>
    public void ActivateBarrier()
    {
        SetBarrierState(true);
    }

    /// <summary>
    /// Đảo ngược trạng thái tường (Dành cho Cần gạt Lever)
    /// </summary>
    public void ToggleBarrier()
    {
        SetBarrierState(!isActive);
    }

    private void SetBarrierState(bool active, bool instant = false)
    {
        isActive = active;
        scaleTween?.Kill();

        if (instant)
        {
            Vector3 targetScale = originalScale;
            if (!isActive) targetScale.x = 0f;

            transform.localScale = targetScale;
            if (barrierCollider != null) barrierCollider.enabled = isActive;
            if (barrierParticles != null)
            {
                if (isActive) barrierParticles.Play();
                else barrierParticles.Stop();
            }
            return;
        }

        if (isActive)
        {
            // Bật lại rào cản: Bật collider và phóng to trục X lại kích thước ban đầu
            if (barrierCollider != null) barrierCollider.enabled = true;
            if (barrierParticles != null) barrierParticles.Play();

            scaleTween = transform.DOScaleX(originalScale.x, scaleDuration)
                .SetEase(closeEase);
        }
        else
        {
            // Tắt rào cản: Co trục X về 0, sau đó tắt collider
            if (barrierParticles != null) barrierParticles.Stop();

            scaleTween = transform.DOScaleX(0f, scaleDuration)
                .SetEase(openEase)
                .OnComplete(() =>
                {
                    if (barrierCollider != null) barrierCollider.enabled = false;
                });
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (filterType == BarrierFilterType.BlockWaterOnly && IsMatchTag(collision, "FirePlayer"))
        {
            Physics2D.IgnoreCollision(collision.collider, barrierCollider, true);
        }
        else if (filterType == BarrierFilterType.BlockFireOnly && IsMatchTag(collision, "WaterPlayer"))
        {
            Physics2D.IgnoreCollision(collision.collider, barrierCollider, true);
        }
    }

    private bool IsMatchTag(Collision2D collision, string tag)
    {
        if (collision.gameObject.CompareTag(tag)) return true;
        if (collision.transform.parent != null && collision.transform.parent.CompareTag(tag)) return true;
        return false;
    }
}
