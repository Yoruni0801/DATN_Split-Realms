using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Quoc.PuzzleQuest
{
    [DisallowMultipleComponent]
    public sealed class PuzzleQuestGemView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private Image slotImage;
        private Image gemImage;
        private Image sheenImage;
        private TMP_Text fallbackLabel;
        private Outline outline;
        private CanvasGroup canvasGroup;
        private Vector2 pointerDownPosition;

        public PuzzleQuestController Owner { get; private set; }
        public Vector2Int Cell { get; private set; }
        public PuzzleGemType Type { get; private set; }
        public RectTransform RectTransform { get; private set; }
        public CanvasGroup CanvasGroup { get { return canvasGroup; } }

        public void Initialize(
            PuzzleQuestController owner,
            Vector2Int cell,
            PuzzleGemType type,
            Sprite icon,
            Color color,
            TMP_FontAsset font)
        {
            Owner = owner;
            Cell = cell;
            Type = type;
            EnsureVisuals(font);
            SetType(type, icon, color);
            ResetVisualState();
        }

        public void SetCell(Vector2Int cell)
        {
            Cell = cell;
        }

        public void SetType(PuzzleGemType type, Sprite icon, Color color)
        {
            Type = type;
            EnsureVisuals(null);

            Color slotColor = Color.Lerp(color, new Color(0.055f, 0.07f, 0.075f, 1f), 0.68f);
            slotColor.a = 0.98f;
            slotImage.color = slotColor;

            if (icon != null)
            {
                gemImage.sprite = icon;
                gemImage.color = Color.white;
                gemImage.preserveAspect = true;
                fallbackLabel.text = string.Empty;
            }
            else
            {
                gemImage.sprite = null;
                gemImage.color = color;
                fallbackLabel.text = GetFallbackGlyph(type);
                fallbackLabel.color = Color.white;
            }

            // Áp dụng aura material theo nguyên tố
            ApplyAuraMaterial(type, icon);

            /*
            Color sheen = Color.white;
            sheen.a = 0.2f;
            sheenImage.color = sheen;
            */
            outline.effectColor = Color.Lerp(color, Color.black, 0.36f);
        }

        private GemAuraProvider cachedAuraProvider;
        private bool auraProviderSearched;
        private Image auraImage;

        private void ApplyAuraMaterial(PuzzleGemType type, Sprite gemSprite)
        {
            if (!auraProviderSearched)
            {
                auraProviderSearched = true;
                if (Owner != null)
                {
                    cachedAuraProvider = Owner.GetComponent<GemAuraProvider>();
                }
            }

            if (cachedAuraProvider == null || !cachedAuraProvider.IsEnabled)
            {
                if (auraImage != null) auraImage.enabled = false;
                gemImage.enabled = true;
                return;
            }

            Material auraMat = cachedAuraProvider.GetAuraMaterial(type);
            Sprite auraSprite = cachedAuraProvider.GetAuraSprite(type);
            Vector2 sizeOffset = cachedAuraProvider.AuraSizeOffset;

            EnsureAuraImage();

            // Aura thay thế Gem Art — ẩn lớp gốc đi
            gemImage.enabled = false;

            // Sử dụng sprite riêng nếu có, không thì dùng sprite gốc của gem
            auraImage.sprite = auraSprite != null ? auraSprite : gemSprite;
            auraImage.material = auraMat;
            auraImage.color = Color.white;
            auraImage.preserveAspect = true;
            auraImage.enabled = true;

            // Áp dụng chiều rộng/cao do user chỉnh từ Inspector
            RectTransform rect = auraImage.rectTransform;
            rect.offsetMin = new Vector2(-sizeOffset.x, -sizeOffset.y);
            rect.offsetMax = new Vector2(sizeOffset.x, sizeOffset.y);
        }

        private void EnsureAuraImage()
        {
            if (auraImage != null) return;

            Transform auraTransform = transform.Find("Gem Aura");
            if (auraTransform == null)
            {
                GameObject auraObject = new GameObject("Gem Aura", typeof(RectTransform), typeof(Image));
                auraTransform = auraObject.transform;
                auraTransform.SetParent(transform, false);

                // Đặt aura trước Gem Art trong hierarchy (render phía sau)
                int gemArtIndex = 0;
                Transform gemArt = transform.Find("Gem Art");
                if (gemArt != null) gemArtIndex = gemArt.GetSiblingIndex();
                auraTransform.SetSiblingIndex(gemArtIndex);

                // Anchor full để fill toàn bộ ô ngọc, offset sẽ được chỉnh từ Provider
                RectTransform rect = (RectTransform)auraTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            auraImage = auraTransform.GetComponent<Image>();
            auraImage.raycastTarget = false;
        }

        public bool IsPetrified { get; private set; }
        private Image petrifiedOverlayImage;
        private TMP_Text petrifiedLabel;

        public void SetPetrified(bool petrified)
        {
            IsPetrified = petrified;
            EnsurePetrifiedVisuals();

            if (petrifiedOverlayImage != null)
            {
                petrifiedOverlayImage.gameObject.SetActive(petrified);
            }

            if (petrified)
            {
                slotImage.color = new Color(0.18f, 0.18f, 0.2f, 0.98f);
                outline.effectColor = new Color(0.45f, 0.45f, 0.5f, 1f);
                if (auraImage != null && auraImage.enabled)
                {
                    auraImage.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
                }
                if (gemImage != null && gemImage.enabled)
                {
                    gemImage.color = new Color(0.45f, 0.45f, 0.48f, 0.7f);
                }
            }
            else
            {
                // Khôi phục màu sắc bình thường
                if (Owner != null)
                {
                    Color normalColor = Owner.GetGemColor(Type);
                    Color slotColor = Color.Lerp(normalColor, new Color(0.055f, 0.07f, 0.075f, 1f), 0.68f);
                    slotColor.a = 0.98f;
                    slotImage.color = slotColor;
                    outline.effectColor = Color.Lerp(normalColor, Color.black, 0.36f);
                }
                if (auraImage != null) auraImage.color = Color.white;
                if (gemImage != null) gemImage.color = Color.white;
            }
        }

        private void EnsurePetrifiedVisuals()
        {
            if (petrifiedOverlayImage != null) return;

            Transform overlayTrans = transform.Find("Petrified Overlay");
            if (overlayTrans == null)
            {
                GameObject overlayObj = new GameObject("Petrified Overlay", typeof(RectTransform), typeof(Image));
                overlayTrans = overlayObj.transform;
                overlayTrans.SetParent(transform, false);
                overlayTrans.SetAsLastSibling();

                RectTransform rect = (RectTransform)overlayTrans;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                petrifiedOverlayImage = overlayObj.GetComponent<Image>();
                petrifiedOverlayImage.color = new Color(0.12f, 0.12f, 0.15f, 0.82f);
                petrifiedOverlayImage.raycastTarget = false;

                GameObject labelObj = new GameObject("Stone Rune", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObj.transform.SetParent(overlayTrans, false);
                RectTransform labelRect = (RectTransform)labelObj.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;

                petrifiedLabel = labelObj.GetComponent<TMP_Text>();
                petrifiedLabel.alignment = TextAlignmentOptions.Center;
                petrifiedLabel.fontSize = 26f;
                petrifiedLabel.fontStyle = FontStyles.Bold;
                petrifiedLabel.color = new Color(0.85f, 0.78f, 0.62f, 0.9f);
                petrifiedLabel.text = "🪨";
                petrifiedLabel.raycastTarget = false;
            }
            else
            {
                petrifiedOverlayImage = overlayTrans.GetComponent<Image>();
            }
        }

        public void SetSelected(bool selected)
        {
            if (IsPetrified) return;
            EnsureVisuals(null);
            outline.effectColor = selected
                ? new Color(1f, 0.82f, 0.28f, 1f)
                : Color.Lerp(Owner.GetGemColor(Type), Color.black, 0.36f);
            outline.effectDistance = selected ? new Vector2(4f, -4f) : new Vector2(2f, -2f);
            transform.localScale = selected ? Vector3.one * 1.08f : Vector3.one;
        }

        public void ResetVisualState()
        {
            EnsureVisuals(null);
            transform.localScale = Vector3.one;
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Owner == null)
            {
                return;
            }

            if (IsPetrified)
            {
                Owner.OnPetrifiedGemClicked(this);
                return;
            }

            pointerDownPosition = eventData.position;
            Owner.OnGemPointerDown(this);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (Owner == null || IsPetrified)
            {
                return;
            }

            Owner.OnGemPointerUp(this, eventData.position - pointerDownPosition);
        }

        private void EnsureVisuals(TMP_FontAsset font)
        {
            RectTransform = GetComponent<RectTransform>();
            if (RectTransform == null)
            {
                RectTransform = gameObject.AddComponent<RectTransform>();
            }

            slotImage = GetComponent<Image>();
            if (slotImage == null)
            {
                slotImage = gameObject.AddComponent<Image>();
            }

            slotImage.raycastTarget = true;

            outline = GetComponent<Outline>();
            if (outline == null)
            {
                outline = gameObject.AddComponent<Outline>();
            }

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            Transform gemTransform = transform.Find("Gem Art");
            if (gemTransform == null)
            {
                GameObject gemObject = new GameObject("Gem Art", typeof(RectTransform), typeof(Image));
                gemTransform = gemObject.transform;
                gemTransform.SetParent(transform, false);
                RectTransform rect = (RectTransform)gemTransform;
                rect.anchorMin = new Vector2(0.06f, 0.06f);
                rect.anchorMax = new Vector2(0.94f, 0.94f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            gemImage = gemTransform.GetComponent<Image>();
            gemImage.raycastTarget = false;

            Transform labelTransform = transform.Find("Gem Label");
            if (labelTransform == null)
            {
                GameObject labelObject = new GameObject("Gem Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelTransform = labelObject.transform;
                labelTransform.SetParent(transform, false);
                RectTransform rect = (RectTransform)labelTransform;
                rect.anchorMin = new Vector2(0.12f, 0.12f);
                rect.anchorMax = new Vector2(0.88f, 0.88f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            fallbackLabel = labelTransform.GetComponent<TMP_Text>();
            fallbackLabel.alignment = TextAlignmentOptions.Center;
            fallbackLabel.fontSize = 24f;
            fallbackLabel.fontStyle = FontStyles.Bold;
            fallbackLabel.enableAutoSizing = true;
            fallbackLabel.fontSizeMin = 10f;
            fallbackLabel.fontSizeMax = 32f;
            fallbackLabel.raycastTarget = false;
            if (font != null)
            {
                fallbackLabel.font = font;
            }

            /*
            Transform sheenTransform = transform.Find("Sheen");
            if (sheenTransform == null)
            {
                GameObject sheenObject = new GameObject("Sheen", typeof(RectTransform), typeof(Image));
                sheenTransform = sheenObject.transform;
                sheenTransform.SetParent(transform, false);
                RectTransform rect = (RectTransform)sheenTransform;
                rect.anchorMin = new Vector2(0.18f, 0.68f);
                rect.anchorMax = new Vector2(0.76f, 0.82f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localRotation = Quaternion.Euler(0f, 0f, -16f);
            }

            sheenImage = sheenTransform.GetComponent<Image>();
            sheenImage.raycastTarget = false;
            */
        }

        private static string GetFallbackGlyph(PuzzleGemType type)
        {
            switch (type)
            {
                case PuzzleGemType.Water:
                    return "W";
                case PuzzleGemType.Fire:
                    return "F";
                case PuzzleGemType.Wind:
                    return "A";
                default:
                    return "S";
            }
        }
    }
}
