using UnityEngine;
using UnityEngine.UI;

namespace Bao.Enemies
{
    public class WorldSpaceHealthBar : MonoBehaviour
    {
        public static WorldSpaceHealthBar Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform rootRect;

        private Image fillImage;

        [Header("Settings")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0, 1.5f, 0);
        [SerializeField] private float showDuration = 3f;
        [SerializeField] private float fadeSpeed = 3f;
        [SerializeField] private float fillSpeed = 1f;
        [SerializeField] private Gradient colorGradient;

        private Canvas parentCanvas;
        private RectTransform canvasRect;
        private Camera cachedCamera;

        private bool useOverrideAnchor;
        private Vector3 overrideAnchorOffset;

        private Transform currentTarget;
        private float targetFill = 1f;
        private float timeSinceLastUpdate = 99f;
        private bool hasLoggedConversionFailure = false;

        private float cachedFillAmount = -1f;

        private void Awake()
        {
            if (healthSlider != null && healthSlider.fillRect != null)
            {
                fillImage = healthSlider.fillRect.GetComponent<Image>();
            }

            parentCanvas = transform.parent != null ? transform.parent.GetComponentInParent<Canvas>() : null;

            // If it's a legacy child on an enemy (no parent canvas), disable it.
            if (parentCanvas == null)
            {
                gameObject.SetActive(false);
                return;
            }

            // Permanently ignore/remove Canvas components so it acts as a normal RectTransform child
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null) Destroy(canvas);

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler != null) Destroy(scaler);

            GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster != null) Destroy(raycaster);

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            canvasRect = parentCanvas.GetComponent<RectTransform>();

            if (rootRect == null)
            {
                rootRect = GetComponent<RectTransform>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static void RegisterInstance(WorldSpaceHealthBar instance, Canvas targetCanvas)
        {
            if (instance == null || targetCanvas == null) return;

            Instance = instance;
            Instance.parentCanvas = targetCanvas;
            Instance.canvasRect = targetCanvas.GetComponent<RectTransform>();

            if (Instance.rootRect == null) Instance.rootRect = instance.GetComponent<RectTransform>();

            if (Instance.rootRect != null)
            {
                Instance.rootRect.anchorMin = new Vector2(0.5f, 0.5f);
                Instance.rootRect.anchorMax = new Vector2(0.5f, 0.5f);
                Instance.rootRect.pivot = new Vector2(0.5f, 0.5f);
                Instance.rootRect.sizeDelta = new Vector2(50f, 10f);
                Instance.rootRect.localScale = Vector3.one;
                Instance.rootRect.localRotation = Quaternion.identity;
            }

            if (Instance.canvasGroup == null)
            {
                Instance.canvasGroup = instance.GetComponent<CanvasGroup>();
                if (Instance.canvasGroup == null)
                {
                    Instance.canvasGroup = instance.gameObject.AddComponent<CanvasGroup>();
                }
            }

            Instance.canvasGroup.enabled = true;
            Instance.canvasGroup.alpha = 0f;
            Instance.canvasGroup.blocksRaycasts = false;
            Instance.hasLoggedConversionFailure = false;
        }

        public static void Show(Transform target, float current, float max)
        {
            if (Instance == null)
            {
                Debug.LogWarning("[WorldSpaceHealthBar] Show called but no static instance is registered! Ensure KeepObjectOnLoad has spawned the shared health bar.");
                return;
            }

            if (!Instance.enabled) return;
            Instance.useOverrideAnchor = false;
            Instance.DoShow(target, current, max);
        }

        /// <summary>Overload: pass explicit world anchor (e.g. collider top) instead of transform pivot.</summary>
        public static void Show(Transform target, Vector3 anchorWorld, float current, float max)
        {
            if (Instance == null) return;
            if (!Instance.enabled) return;
            Instance.overrideAnchorOffset = anchorWorld - target.position; // Lưu offset để di chuyển theo
            Instance.useOverrideAnchor = true;
            Instance.DoShow(target, current, max);
        }

        public static void Hide(Transform target)
        {
            if (Instance == null || !Instance.enabled) return;
            Instance.DoHide(target);
        }

        private void DoShow(Transform target, float current, float max)
        {
            if (max <= 0f) return;

            if (currentTarget != target)
            {
                currentTarget = target;
                float newFill = Mathf.Clamp01(current / max);
                targetFill = newFill;
                if (healthSlider != null)
                {
                    healthSlider.value = newFill;
                }
                if (fillImage != null && colorGradient != null)
                {
                    fillImage.color = colorGradient.Evaluate(newFill);
                }
                cachedFillAmount = newFill;
            }
            else
            {
                targetFill = Mathf.Clamp01(current / max);
            }

            timeSinceLastUpdate = 0f;
        }

        private void DoHide(Transform target)
        {
            if (currentTarget == target)
            {
                currentTarget = null;
                timeSinceLastUpdate = showDuration + 1f;
                if (canvasGroup != null) canvasGroup.alpha = 0f;
            }
        }

        private void Update()
        {
            if (currentTarget == null)
            {
                timeSinceLastUpdate += Time.deltaTime;
                UpdateVisibility(0f);
                return;
            }

            timeSinceLastUpdate += Time.deltaTime;

            if (Mathf.Abs(cachedFillAmount - targetFill) > 0.001f)
            {
                cachedFillAmount = Mathf.Lerp(cachedFillAmount, targetFill, Time.deltaTime * fillSpeed);
                if (healthSlider != null)
                {
                    healthSlider.value = cachedFillAmount;
                }
                if (fillImage != null && colorGradient != null)
                {
                    fillImage.color = colorGradient.Evaluate(cachedFillAmount);
                }
            }

            Camera cam = (cachedCamera != null && cachedCamera.isActiveAndEnabled)
                ? cachedCamera
                : (cachedCamera = Camera.main);
            if (cam == null)
            {
                UpdateVisibility(0f);
                return;
            }

            Vector3 anchorPos = useOverrideAnchor ? (currentTarget.position + overrideAnchorOffset) : currentTarget.position;
            Vector3 screenPoint = cam.WorldToScreenPoint(anchorPos + worldOffset);
            // Add generous margin so flying enemies don't immediately hide their health bar
            bool isInsideCamera = screenPoint.z > 0f &&
                                  screenPoint.x >= -300f && screenPoint.x <= Screen.width + 300f &&
                                  screenPoint.y >= -300f && screenPoint.y <= Screen.height + 300f;

            float targetAlpha = 0f;
            if (isInsideCamera && timeSinceLastUpdate <= showDuration)
            {
                // Clamp screenPoint so the health bar sticks to the screen edge if the enemy flies too high
                screenPoint.x = Mathf.Clamp(screenPoint.x, 50f, Screen.width - 50f);
                screenPoint.y = Mathf.Clamp(screenPoint.y, 50f, Screen.height - 50f);

                Vector2 localPoint;

                if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out localPoint))
                {
                    hasLoggedConversionFailure = false;
                    targetAlpha = 1f;
                    if (rootRect != null)
                    {
                        rootRect.anchoredPosition = localPoint;
                    }
                }
                else
                {
                    if (!hasLoggedConversionFailure)
                    {
                        Debug.LogWarning("[WorldSpaceHealthBar] Failed to convert enemy screen point to HUD canvas local point.");
                        hasLoggedConversionFailure = true;
                    }
                }
            }

            UpdateVisibility(targetAlpha);
        }


        private void UpdateVisibility(float targetAlpha)
        {
            if (canvasGroup != null)
            {
                if (Mathf.Abs(canvasGroup.alpha - targetAlpha) > 0.001f)
                {
                    canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
                }
                else
                {
                    canvasGroup.alpha = targetAlpha;
                }
            }
            else if (rootRect != null)
            {
                bool shouldBeActive = targetAlpha > 0.5f;
                if (rootRect.gameObject.activeSelf != shouldBeActive)
                {
                    rootRect.gameObject.SetActive(shouldBeActive);
                }
            }
        }
    }
}
