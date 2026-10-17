using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

namespace Minigame.RunePuzzle
{
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(Image))]
    public class RuneStone : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Rune Identity")]
        public string runeId; // VD: "Fire", "Water", "Light", "Earth", "Desert"

        [Header("Visual")]
        [SerializeField] private float hoverScale = 1.15f;
        [SerializeField] private float hoverGlowAlpha = 0.4f;
        [SerializeField] private Image glowOutline; // Optional: Image phát sáng viền bao quanh rune

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Image runeImage;
        private Vector2 originalPosition;
        private Transform originalParent;
        private RuneSlot currentSlot;
        private Vector3 originalScale;
        private Color originalColor;
        private Canvas rootCanvas;
        private bool isDragging = false;
        public static bool IsAnyRuneDragging { get; private set; }
        private Coroutine slideCoroutine;
        private bool hasCachedInitialLayout = false;
        
        // Track anchor and pivot to prevent offset
        private Vector2 originalAnchorMin;
        private Vector2 originalAnchorMax;
        private Vector2 originalPivot;

        private void Awake()
        {
            CacheComponentsIfNeeded();
        }

        private void CacheComponentsIfNeeded()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            runeImage = GetComponent<Image>();
            if (!hasCachedInitialLayout)
            {
                originalScale = rectTransform != null ? rectTransform.localScale : transform.localScale;

                if (rectTransform != null)
                {
                    originalPosition = rectTransform.anchoredPosition;
                    originalAnchorMin = rectTransform.anchorMin;
                    originalAnchorMax = rectTransform.anchorMax;
                    originalPivot = rectTransform.pivot;
                }

                if (runeImage != null)
                {
                    originalColor = runeImage.color;
                }
            }

            if (originalParent == null)
            {
                originalParent = transform.parent;
            }

            hasCachedInitialLayout = true;
        }

        private void OnDisable()
        {
            if (isDragging) IsAnyRuneDragging = false;
        }

        private void Start()
        {
            // Tìm Root Canvas để tính scale
            rootCanvas = GetComponentInParent<Canvas>();
            while (rootCanvas != null && !rootCanvas.isRootCanvas)
                rootCanvas = rootCanvas.transform.parent?.GetComponentInParent<Canvas>();

            // Ẩn glow outline ban đầu
            if (glowOutline != null)
            {
                Color c = glowOutline.color;
                c.a = 0f;
                glowOutline.color = c;
            }
        }

        private Coroutine scaleCoroutine;
        private Coroutine flashCoroutine;

        // ===========================================
        // === HOVER EFFECTS ===
        // ===========================================

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isDragging) return;

            // Scale lên nhẹ
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(ScaleTo(originalScale * hoverScale, 0.15f));

            // Hiện glow viền
            if (glowOutline != null)
                StartCoroutine(FadeImage(glowOutline, hoverGlowAlpha, 0.15f));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (isDragging) return;

            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(ScaleTo(originalScale, 0.15f));

            if (glowOutline != null)
                StartCoroutine(FadeImage(glowOutline, 0f, 0.15f));
        }

        public void UpdateOriginalPosition(Vector2 pos)
        {
            originalPosition = pos;
            hasCachedInitialLayout = true;
        }

        public void UpdateOriginalAnchors(Vector2 aMin, Vector2 aMax, Vector2 piv)
        {
            originalAnchorMin = aMin;
            originalAnchorMax = aMax;
            originalPivot = piv;
            hasCachedInitialLayout = true;
        }

        // ===========================================
        // === DRAG & DROP ===
        // ===========================================

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (RunePuzzleManager.Instance != null && !RunePuzzleManager.Instance.CanDragRunes())
            {
                eventData.pointerDrag = null; // Cancel drag
                return;
            }

            if (currentSlot != null)
            {
                // KHÓA LẠI: Nếu đã khảm vào lõm rồi thì không cho lấy ra nữa
                eventData.pointerDrag = null;
                return;
            }

            isDragging = true;
            IsAnyRuneDragging = true;

            canvasGroup.blocksRaycasts = false; // Cho raycast xuyên qua để slot bên dưới nhận
            canvasGroup.alpha = 0.85f;

            // Scale nhẹ khi kéo
            rectTransform.localScale = originalScale * 1.1f;

            // Đưa ra root canvas để nổi lên trên cùng khi đang kéo
            if (rootCanvas != null)
            {
                transform.SetParent(rootCanvas.transform);
                transform.SetAsLastSibling();
            }

            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)transform.parent, 
                eventData.position, 
                eventData.pressEventCamera, 
                out Vector3 worldPoint))
            {
                transform.position = worldPoint;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (RunePuzzleManager.Instance != null && !RunePuzzleManager.Instance.CanDragRunes()) return;

            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)transform.parent, 
                eventData.position, 
                eventData.pressEventCamera, 
                out Vector3 worldPoint))
            {
                transform.position = worldPoint;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            isDragging = false;
            IsAnyRuneDragging = false;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
            rectTransform.localScale = originalScale;

            // Nếu không được nhận bởi slot nào qua OnDrop (do Unity thả trượt viền)
            if (currentSlot == null)
            {
                // TỰ ĐỘNG DÒ TÌM LÕM GẦN NHẤT (Proximity Snapping)
                RuneSlot closestSlot = null;
                float minDistance = RunePuzzleManager.Instance != null ? RunePuzzleManager.Instance.snapRadius : 100f; // Sử dụng biến từ Manager

                if (RunePuzzleManager.Instance != null && RunePuzzleManager.Instance.runeSlots != null)
                {
                    foreach (var slot in RunePuzzleManager.Instance.runeSlots)
                    {
                        if (slot.gameObject.activeInHierarchy) // Chỉ xét những rãnh đang hiện
                        {
                            float dist = Vector2.Distance(rectTransform.position, slot.transform.position);
                            if (dist < minDistance)
                            {
                                minDistance = dist;
                                closestSlot = slot;
                            }
                        }
                    }
                }

                if (closestSlot != null && !closestSlot.HasStone())
                {
                    // Ép rãnh nhận viên đá này
                    closestSlot.ReceiveStone(this);
                }
                else
                {
                    // Trả về parent cũ và trượt mượt về vị trí ban đầu
                    transform.SetParent(originalParent);
                    StartCoroutine(SlideTo(originalPosition, 0.25f));
                }
            }
        }

        // ===========================================
        // === SLOT INTERACTION ===
        // ===========================================

        public void SetCurrentSlot(RuneSlot slot)
        {
            currentSlot = slot;
            // Cho đá làm con của Lõm luôn để dính vào chính giữa 100%
            transform.SetParent(slot.transform);
            
            // Ép Anchor và Pivot về chính giữa để đảm bảo đá luôn nằm hoàn hảo ở giữa Lõm (sửa lỗi lệch góc)
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
        }

        // ===========================================
        // === EFFECTS ===
        // ===========================================

        public void PlayCorrectEffect(Color elementColor)
        {
            if (runeImage != null)
            {
                if (flashCoroutine != null) StopCoroutine(flashCoroutine);
                flashCoroutine = StartCoroutine(PulseColor(elementColor));
            }
            if (glowOutline != null)
            {
                glowOutline.color = new Color(elementColor.r, elementColor.g, elementColor.b, 0.6f);
            }
        }

        public void PlayWrongEffect()
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(FlashRed());
        }

        // ===========================================
        // === RESET ===
        // ===========================================

        public void ResetStone(Vector2 newOriginalPosition)
        {
            CacheComponentsIfNeeded();

            if (currentSlot != null)
            {
                currentSlot.RemoveStone();
                currentSlot = null; // Mở khóa đá
            }

            isDragging = false;
            canvasGroup.blocksRaycasts = true; // Cho phép tương tác lại
            canvasGroup.alpha = 1f;

            transform.SetParent(originalParent);
            rectTransform.anchorMin = originalAnchorMin;
            rectTransform.anchorMax = originalAnchorMax;
            rectTransform.pivot = originalPivot;
            rectTransform.localScale = originalScale;
            
            originalPosition = newOriginalPosition;
            
            // Slide mượt mà về chỗ cũ
            if (slideCoroutine != null) StopCoroutine(slideCoroutine);
            slideCoroutine = StartCoroutine(SlideTo(originalPosition, 0.35f));

            if (flashCoroutine != null) StopCoroutine(flashCoroutine);

            if (runeImage != null)
                runeImage.color = originalColor;

            if (glowOutline != null)
            {
                Color c = glowOutline.color;
                c.a = 0f;
                glowOutline.color = c;
            }
        }

        public void GlowSuccess()
        {
            // Legacy
        }

        // ===========================================
        // === COROUTINE ANIMATIONS ===
        // ===========================================

        private IEnumerator ScaleTo(Vector3 target, float duration)
        {
            Vector3 start = rectTransform.localScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                t = t * t * (3f - 2f * t); // SmoothStep easing
                rectTransform.localScale = Vector3.Lerp(start, target, t);
                yield return null;
            }
            rectTransform.localScale = target;
        }

        private IEnumerator SlideTo(Vector2 target, float duration)
        {
            Vector2 start = rectTransform.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                t = 1f - Mathf.Pow(1f - t, 3f); // EaseOutCubic
                rectTransform.anchoredPosition = Vector2.Lerp(start, target, t);
                yield return null;
            }
            rectTransform.anchoredPosition = target;
        }

        private IEnumerator PulseColor(Color targetColor)
        {
            if (runeImage == null) yield break;

            // Pulse sáng lên 3 lần
            for (int i = 0; i < 3; i++)
            {
                float elapsed = 0f;
                while (elapsed < 0.2f)
                {
                    elapsed += Time.deltaTime;
                    runeImage.color = Color.Lerp(originalColor, targetColor, elapsed / 0.2f);
                    yield return null;
                }
                elapsed = 0f;
                while (elapsed < 0.15f)
                {
                    elapsed += Time.deltaTime;
                    runeImage.color = Color.Lerp(targetColor, originalColor, elapsed / 0.15f);
                    yield return null;
                }
            }
            runeImage.color = originalColor; // Giữ lại màu gốc thay vì màu targetColor
        }

        private IEnumerator FlashRed()
        {
            if (runeImage == null) yield break;

            Color flashColor = new Color(1f, 0.15f, 0.15f);
            for (int i = 0; i < 2; i++)
            {
                runeImage.color = flashColor;
                yield return new WaitForSeconds(0.1f);
                runeImage.color = originalColor;
                yield return new WaitForSeconds(0.1f);
            }
        }

        private IEnumerator FadeImage(Image img, float targetAlpha, float duration)
        {
            Color c = img.color;
            float startAlpha = c.a;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                img.color = c;
                yield return null;
            }
            c.a = targetAlpha;
            img.color = c;
        }
    }
}
