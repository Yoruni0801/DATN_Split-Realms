using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Minigame.LivingBookSearch
{
    [RequireComponent(typeof(Image))]
    public class LivingBook : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private Image bookImage;
        private RectTransform rectTransform;

        private bool isReal;

        // Trạng thái gốc
        private Color originalColor;
        private Vector3 originalScale;
        private Vector2 originalPos;
        private Quaternion originalRot;

        private Coroutine behaviorCoroutine;
        private BookHintData currentHint;

        // Outline component để phát sáng viền
        private Outline glowOutline;
        
        // Biến lưu trữ Glow do Code điều khiển
        private Color baseGlowColor = Color.clear;
        private float baseGlowSize = 0f;
        
        // Trạng thái tương tác chuột
        private bool isHovered = false;

        // Trạng thái cho hệ thống Swap
        public bool IsIdle { get; private set; }
        public bool IsSwapping { get; private set; }
        public Vector2 GetOriginalPosition() => originalPos;

        // ===============================================
        // === KHỞI TẠO ===
        // ===============================================

        private void Awake()
        {
            bookImage = GetComponent<Image>();
            rectTransform = GetComponent<RectTransform>();

            // Outline tạo viền phát sáng mịn hơn Shadow rất nhiều
            glowOutline = GetComponent<Outline>();
            if (glowOutline == null) glowOutline = gameObject.AddComponent<Outline>();
            glowOutline.effectDistance = Vector2.zero;
            glowOutline.effectColor = Color.clear;
            glowOutline.useGraphicAlpha = false;

            originalColor = bookImage.color;
            originalScale = rectTransform.localScale;
            originalPos = rectTransform.anchoredPosition;
            originalRot = rectTransform.localRotation;
        }

        private void Update()
        {
            if (glowOutline == null) return;
            
            // 1. Hơi thở ánh sáng nền (Outline Breathing) - Đập nhịp nhàng liên tục
            float breathe = Mathf.Abs(Mathf.Sin(Time.time * 2f)) * 0.8f; 
            
            // 2. Phản ứng chuột (Hover) - Sáng rực lên khi trỏ chuột vào
            float hover = isHovered && !IsSwapping ? 2f : 0f;
            
            float finalSize = baseGlowSize;
            Color finalColor = baseGlowColor;

            // Nếu đang ngủ, thêm hiệu ứng thở nền
            if (IsIdle && baseGlowColor == Color.clear) 
            {
                finalSize += breathe;
                finalColor = new Color(1f, 1f, 1f, 0.1f); 
            }

            // Ghi đè hiệu ứng Hover nếu đang trỏ chuột
            if (isHovered && !IsSwapping)
            {
                finalSize += hover;
                if (baseGlowColor == Color.clear || IsIdle) 
                    finalColor = new Color(1f, 1f, 1f, 0.4f);
            }

            glowOutline.effectDistance = new Vector2(finalSize, -finalSize);
            glowOutline.effectColor = finalColor;
        }

        public void SaveOriginalPosition()
        {
            if (rectTransform != null)
                originalPos = rectTransform.anchoredPosition;
        }

        public void SetupBook(bool isReal, BookHintData hint)
        {
            this.isReal = isReal;
            this.currentHint = hint;
            this.IsSwapping = false;
            this.IsIdle = false;
            this.isHovered = false;

            ResetVisuals();

            if (behaviorCoroutine != null) StopCoroutine(behaviorCoroutine);
            behaviorCoroutine = StartCoroutine(isReal ? RealBookLoop() : FakeBookLoop());
        }

        private void ResetVisuals()
        {
            rectTransform.localScale = originalScale;
            rectTransform.anchoredPosition = originalPos;
            rectTransform.localRotation = originalRot;
            bookImage.color = originalColor;
            SetGlow(Color.clear, 0f);
        }

        private void SetGlow(Color color, float size)
        {
            baseGlowColor = color;
            baseGlowSize = size;
        }

        // ===============================================
        // === TƯƠNG TÁC CHUỘT ===
        // ===============================================

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (IsSwapping) return;
            LivingBookManager.Instance.OnBookClicked(this);
        }

        // ===============================================
        // === ĐỔI CHỖ (SWAP) ===
        // ===============================================

        public void StartSwap(Vector2 newPos)
        {
            if (behaviorCoroutine != null) StopCoroutine(behaviorCoroutine);
            StartCoroutine(SwapRoutine(newPos));
        }

        private IEnumerator SwapRoutine(Vector2 newPos)
        {
            IsSwapping = true;
            IsIdle = false;

            Vector2 startPos = rectTransform.anchoredPosition;
            float duration = 0.8f;
            float elapsed = 0f;

            // Viền sáng vàng nhạt khi bay
            SetGlow(new Color(1f, 0.9f, 0.5f, 0.6f), 2f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);

                // Bay theo cung
                Vector2 pos = Vector2.Lerp(startPos, newPos, t);
                pos.y += Mathf.Sin(t * Mathf.PI) * 40f;
                rectTransform.anchoredPosition = pos;

                yield return null;
            }

            originalPos = newPos;
            ResetVisuals();
            IsSwapping = false;

            behaviorCoroutine = StartCoroutine(isReal ? RealBookLoop() : FakeBookLoop());
        }

        // ===============================================
        // === VÒNG LẶP SÁCH GIẢ ===
        // ===============================================
        // Vừa vào game, các cuốn sách sẽ có một khoảng trễ ngẫu nhiên (0 - 10s).
        // Cuốn nào trễ 0s sẽ diễn ngay lập tức, cuốn nào 10s sẽ diễn sau. 
        // Đảm bảo game luôn có chuyển động ngay từ đầu nhưng cực kỳ giãn cách.

        private IEnumerator FakeBookLoop()
        {
            // Trễ ban đầu để giãn cách nhịp độ
            yield return new WaitForSeconds(Random.Range(0f, 10f));

            while (true)
            {
                // === ACT: Làm 1 hành vi ngẫu nhiên ===
                BookBehaviorType fakeBehavior;
                do
                {
                    fakeBehavior = (BookBehaviorType)Random.Range(0, 5);
                } while (fakeBehavior == currentHint.targetBehavior);

                yield return StartCoroutine(DoAct(fakeBehavior, 1.2f, false));
                yield return StartCoroutine(EaseBackToNormal(0.6f));

                // === IDLE: Im lặng hoàn toàn để tạo khoảng trống ===
                ResetVisuals();
                IsIdle = true;
                yield return new WaitForSeconds(Random.Range(6f, 14f));
                IsIdle = false;
            }
        }

        // ===============================================
        // === VÒNG LẶP SÁCH THẬT ===
        // ===============================================
        private IEnumerator RealBookLoop()
        {
            // Trễ ban đầu (1-10s) để sách thật không diễn quá sớm hoặc quá muộn so với mặt bằng chung
            float initialWait = Random.Range(1f, 10f);
            float elapsedWait = 0f;
            
            // Nhịp thở siêu nhẹ lúc khởi đầu
            while (elapsedWait < initialWait)
            {
                elapsedWait += Time.deltaTime;
                float micro = 1f + Mathf.Sin(elapsedWait * 1.5f) * 0.01f;
                rectTransform.localScale = originalScale * micro;
                yield return null;
            }

            while (true)
            {
                // === ACT: Hành vi đúng theo hint ===
                yield return StartCoroutine(DoAct(currentHint.targetBehavior, 2f, true));
                yield return StartCoroutine(EaseBackToNormal(0.8f));

                // === IDLE: Im lặng, chỉ có nhịp thở siêu nhẹ ===
                ResetVisuals();
                IsIdle = true;

                float idleDuration = Random.Range(6f, 12f);
                float idleElapsed = 0f;
                while (idleElapsed < idleDuration)
                {
                    idleElapsed += Time.deltaTime;
                    // Nhịp thở cực nhẹ (±1%) - chỉ người tinh mắt mới thấy
                    float micro = 1f + Mathf.Sin(idleElapsed * 1.5f) * 0.01f;
                    rectTransform.localScale = originalScale * micro;
                    yield return null;
                }

                IsIdle = false;
            }
        }

        // ===============================================
        // === THỰC HIỆN HÀNH VI ===
        // ===============================================
        // Mỗi hành vi CHỈ thay đổi 1 trục duy nhất, cộng thêm viền sáng Outline.
        // Sách thật: hiệu ứng tinh tế, chậm rãi, đẹp.
        // Sách giả: hiệu ứng phóng đại, vụng về, nhanh.

        private IEnumerator DoAct(BookBehaviorType type, float duration, bool isTheRealDeal)
        {
            float elapsed = 0f;
            Color glowColor = isTheRealDeal
                ? currentHint.secretGlowColor
                : Color.HSVToRGB(Random.value, 0.4f, 0.7f); // Màu pastel ngẫu nhiên

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // Đường cong mượt: 0 → 1 → 0 (Ease In-Out)
                float wave = Mathf.Sin(t * Mathf.PI);

                // Viền sáng dùng chung cho mọi hành vi
                Color gc = glowColor;
                gc.a = wave * (isTheRealDeal ? 0.9f : 0.4f);
                SetGlow(gc, wave * (isTheRealDeal ? 3f : 1.5f));

                switch (type)
                {
                    case BookBehaviorType.FlashColor:
                        // Trục COLOR: Sách thật đổi đúng màu, sách giả đổi màu tào lao
                        bookImage.color = Color.Lerp(originalColor, glowColor, wave * 0.7f);
                        break;

                    case BookBehaviorType.FloatUp:
                        // Trục POSITION Y
                        float y = wave * (isTheRealDeal ? 12f : -8f);
                        rectTransform.anchoredPosition = originalPos + new Vector2(0, y);
                        break;

                    case BookBehaviorType.Breathe:
                        // Trục SCALE
                        float s = 1f + wave * (isTheRealDeal ? 0.15f : -0.12f);
                        rectTransform.localScale = originalScale * s;
                        break;

                    case BookBehaviorType.Jitter:
                        // Trục ROTATION
                        float maxAng = isTheRealDeal ? 3f : 12f;
                        float ang = Mathf.Sin(elapsed * 25f) * maxAng * wave;
                        rectTransform.localRotation = Quaternion.Euler(0, 0, ang);
                        break;

                    case BookBehaviorType.Phantom:
                        // Trục ALPHA
                        Color ca = originalColor;
                        if (isTheRealDeal)
                            ca.a = Mathf.Lerp(1f, 0.15f, wave);
                        else
                            ca.a = Mathf.Lerp(1f, 0.3f, wave);
                        bookImage.color = ca;
                        break;
                }

                yield return null;
            }
        }

        // ===============================================
        // === TRỞ VỀ TRẠNG THÁI GỐC (MƯỢT) ===
        // ===============================================

        private IEnumerator EaseBackToNormal(float duration)
        {
            Vector2 startPos = rectTransform.anchoredPosition;
            Quaternion startRot = rectTransform.localRotation;
            Vector3 startScale = rectTransform.localScale;
            Color startColor = bookImage.color;
            Color startGlow = glowOutline != null ? glowOutline.effectColor : Color.clear;
            Vector2 startGlowDist = glowOutline != null ? glowOutline.effectDistance : Vector2.zero;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                // Ease-out cubic cho cảm giác "hạ cánh" mềm mại
                float raw = elapsed / duration;
                float t = 1f - (1f - raw) * (1f - raw) * (1f - raw);

                rectTransform.anchoredPosition = Vector2.Lerp(startPos, originalPos, t);
                rectTransform.localRotation = Quaternion.Lerp(startRot, originalRot, t);
                rectTransform.localScale = Vector3.Lerp(startScale, originalScale, t);
                bookImage.color = Color.Lerp(startColor, originalColor, t);

                if (glowOutline != null)
                {
                    glowOutline.effectColor = Color.Lerp(startGlow, Color.clear, t);
                    glowOutline.effectDistance = Vector2.Lerp(startGlowDist, Vector2.zero, t);
                }

                yield return null;
            }

            ResetVisuals();
        }

        // ===============================================
        // === HIỆU ỨNG ĐÚNG / SAI ===
        // ===============================================

        public void PlayWrongEffect()
        {
            if (behaviorCoroutine != null) StopCoroutine(behaviorCoroutine);
            StartCoroutine(WrongSequence());
        }

        private IEnumerator WrongSequence()
        {
            Color red = new Color(1f, 0.25f, 0.2f);

            // Rung nhẹ 3 lần + viền đỏ
            for (int i = 0; i < 3; i++)
            {
                bookImage.color = red;
                SetGlow(red, 3f);
                rectTransform.localRotation = Quaternion.Euler(0, 0, 8f);
                yield return new WaitForSeconds(0.08f);

                bookImage.color = originalColor;
                SetGlow(Color.clear, 0f);
                rectTransform.localRotation = Quaternion.Euler(0, 0, -8f);
                yield return new WaitForSeconds(0.08f);
            }

            ResetVisuals();
            behaviorCoroutine = StartCoroutine(FakeBookLoop());
        }

        public void PlayCorrectEffect()
        {
            if (behaviorCoroutine != null) StopCoroutine(behaviorCoroutine);
            StartCoroutine(CorrectSequence());
        }

        private IEnumerator CorrectSequence()
        {
            Color glow = currentHint.secretGlowColor;
            float duration = 1.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Phình to mượt mà
                float s = 1f + t * 0.25f;
                rectTransform.localScale = originalScale * s;

                // Đổi màu dần sang màu Glow
                bookImage.color = Color.Lerp(originalColor, glow, t);

                // Viền sáng rực rỡ phình to ra
                Color gc = glow;
                gc.a = Mathf.Lerp(0f, 1f, t);
                SetGlow(gc, Mathf.Lerp(0f, 6f, t));

                yield return null;
            }
        }

        // ===============================================
        // === TIỆN ÍCH ===
        // ===============================================

        public void StopAllBehaviors()
        {
            if (behaviorCoroutine != null) StopCoroutine(behaviorCoroutine);
            StopAllCoroutines();
            ResetVisuals();
        }

        public void FadeOut()
        {
            StartCoroutine(FadeOutRoutine());
        }

        private IEnumerator FadeOutRoutine()
        {
            Color start = bookImage.color;
            Color startGlow = glowOutline != null ? glowOutline.effectColor : Color.clear;
            float elapsed = 0f;

            while (elapsed < 1f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed;

                Color c = start;
                c.a = Mathf.Lerp(start.a, 0f, t);
                bookImage.color = c;

                if (glowOutline != null)
                {
                    Color gc = startGlow;
                    gc.a = Mathf.Lerp(startGlow.a, 0f, t);
                    glowOutline.effectColor = gc;
                }

                yield return null;
            }
        }
    }
}
