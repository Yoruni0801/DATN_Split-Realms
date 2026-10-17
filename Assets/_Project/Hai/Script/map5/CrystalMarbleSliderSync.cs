using UnityEngine;
using UnityEngine.UI;
#if TMPro
using TMPro;
#endif

namespace Project.Hai.Map5
{
    /// <summary>
    /// Đồng bộ góc lệch từ Minigame CrystalMarble (hoặc MiniGameChiaKhoa) sang thanh UI Slider/Image/RectTransform.
    /// Quy tắc:
    /// - Khi độ lệch <= 10 độ: Đạt 100% (1.0).
    /// - Khi độ lệch > 10 độ: Bắt đầu từ 99.9999% (0.999999) giảm dần về 0%.
    /// </summary>
    [AddComponentMenu("Project/Hai/Map5/Crystal Marble Slider Sync")]
    public class CrystalMarbleSliderSync : MonoBehaviour
    {
        [Header("--- NGUỒN DỮ LIỆU MINIGAME ---")]
        [Tooltip("Component CrystalMarblePuzzle trên minigame. Nếu để trống, script sẽ tự tìm trên cùng GameObject hoặc cha/con.")]
        [SerializeField] private CrystalMarblePuzzle puzzle;

        [Header("--- CÁC THÀNH PHẦN UI CẦN ĐỒNG BỘ ---")]
        [Tooltip("UI Slider (nếu dùng component Slider)")]
        [SerializeField] private Slider targetSlider;

        [Tooltip("UI Image dạng Filled (nếu dùng Image ImageType.Filled)")]
        [SerializeField] private Image targetFilledImage;

        [Tooltip("RectTransform thanh đo (nếu dùng thay đổi Width/SizeDelta hoặc LocalScale)")]
        [SerializeField] private RectTransform targetBarRect;

        public enum RectSizeMode
        {
            None,
            HorizontalWidth,
            LocalScaleX
        }

        [Tooltip("Kiểu thay đổi kích thước nếu dùng targetBarRect")]
        [SerializeField] private RectSizeMode rectSizeMode = RectSizeMode.None;

        [Tooltip("Chiều rộng tối đa (Width) khi đạt 100% (chỉ dùng nếu chọn HorizontalWidth)")]
        [SerializeField] private float maxBarWidth = 500f;

        [Header("--- HIỂN THỊ PHẦN TRĂM DẠNG CHỮ (TÙY CHỌN) ---")]
        [SerializeField] private Text percentText;
#if TMPro
        [SerializeField] private TMP_Text tmpPercentText;
#endif
        [SerializeField] private string textFormat = "{0:0.0}%";

        [Header("--- CẤU HÌNH NGƯỠNG GÓC (TÙY CHỈNH) ---")]
        [Tooltip("Ngưỡng góc đạt 100% trọn vẹn (mặc định 10 độ)")]
        [SerializeField] private float thresholdAngle = 10f;

        [Tooltip("Góc lệch tối đa tương ứng với 0% (mặc định 180 độ)")]
        [SerializeField] private float maxAngle = 180f;

        [Tooltip("Có dùng hiệu ứng làm mượt chuyển động thanh bar không")]
        [SerializeField] private bool smoothTransition = true;

        [Tooltip("Tốc độ làm mượt")]
        [SerializeField] private float smoothSpeed = 12f;

        private float currentDisplayPercent = 0f;

        private void Awake()
        {
            if (puzzle == null)
            {
                puzzle = GetComponent<CrystalMarblePuzzle>();
                if (puzzle == null)
                {
                    puzzle = GetComponentInParent<CrystalMarblePuzzle>();
                }
                if (puzzle == null)
                {
                    puzzle = GetComponentInChildren<CrystalMarblePuzzle>(true);
                }
                if (puzzle == null)
                {
                    puzzle = FindFirstObjectByType<CrystalMarblePuzzle>(FindObjectsInactive.Include);
                }
            }

            if (targetSlider == null) targetSlider = GetComponent<Slider>();
            if (targetFilledImage == null && targetSlider == null) targetFilledImage = GetComponent<Image>();
            if (percentText == null) percentText = GetComponent<Text>();
        }

        private void Update()
        {
            if (puzzle == null)
            {
                puzzle = FindFirstObjectByType<CrystalMarblePuzzle>(FindObjectsInactive.Include);
                if (puzzle == null) return;
            }

            float currentAngle = puzzle.CurrentAngle;
            float targetPercent = CalculatePercentage(currentAngle);

            if (smoothTransition)
            {
                currentDisplayPercent = Mathf.Lerp(currentDisplayPercent, targetPercent, Time.unscaledDeltaTime * smoothSpeed);
            }
            else
            {
                currentDisplayPercent = targetPercent;
            }

            ApplyToUI(currentDisplayPercent);
        }

        /// <summary>
        /// Tính toán phần trăm (0.0 -> 1.0):
        /// - angle <= 10 -> 1.0 (100%)
        /// - angle > 10 -> 0.999999 giảm dần về 0.0
        /// </summary>
        public float CalculatePercentage(float angle)
        {
            if (angle <= thresholdAngle)
            {
                return 1f; // Đạt 100% trọn vẹn
            }

            if (angle >= maxAngle)
            {
                return 0f; // 0%
            }

            // Tuyến tính từ 0.999999f (ngay sau mốc 10 độ) giảm về 0.0f (tại maxAngle)
            float t = (angle - thresholdAngle) / Mathf.Max(0.001f, maxAngle - thresholdAngle);
            float percent = Mathf.Lerp(0.999999f, 0f, Mathf.Clamp01(t));
            return percent;
        }

        private void ApplyToUI(float percent01)
        {
            // 1. Cập nhật Slider
            if (targetSlider != null)
            {
                targetSlider.value = Mathf.Lerp(targetSlider.minValue, targetSlider.maxValue, percent01);
            }

            // 2. Cập nhật Image Filled
            if (targetFilledImage != null)
            {
                targetFilledImage.fillAmount = percent01;
            }

            // 3. Cập nhật RectTransform
            if (targetBarRect != null)
            {
                if (rectSizeMode == RectSizeMode.HorizontalWidth)
                {
                    Vector2 size = targetBarRect.sizeDelta;
                    size.x = maxBarWidth * percent01;
                    targetBarRect.sizeDelta = size;
                }
                else if (rectSizeMode == RectSizeMode.LocalScaleX)
                {
                    Vector3 scale = targetBarRect.localScale;
                    scale.x = percent01;
                    targetBarRect.localScale = scale;
                }
            }

            // 4. Cập nhật Text hiển thị %
            string formattedPercent = string.Format(textFormat, percent01 * 100f);
            if (percentText != null)
            {
                percentText.text = formattedPercent;
            }

#if TMPro
            if (tmpPercentText != null)
            {
                tmpPercentText.text = formattedPercent;
            }
#endif
        }

        public void SetPuzzle(CrystalMarblePuzzle newPuzzle)
        {
            puzzle = newPuzzle;
        }
    }
}
