using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Minigame.RunePuzzle
{
    public class LensDragger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, ICanvasRaycastFilter
    {
        [Header("Lens Settings")]
        public float lensRadius = 150f;
        public float lensSoftness = 20f;
        
        [Header("Fog Reference")]
        public Material fogMaterial; // Gán material asset để shader không bị strip khi build
        public Image fogImage;       // Kéo Image Fog vào đây
        
        private RectTransform rectTransform;
        private Canvas rootCanvas;
        private Vector3 originalPosition;
        private bool isHoldingLens = false;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            // Let a dragged rune reach the slot beneath the lens, while preserving lens dragging otherwise.
            return !RuneStone.IsAnyRuneDragging;
        }

        private void Start()
        {
            originalPosition = rectTransform.anchoredPosition;

            rootCanvas = GetComponentInParent<Canvas>();
            while (rootCanvas != null && !rootCanvas.isRootCanvas)
                rootCanvas = rootCanvas.transform.parent?.GetComponentInParent<Canvas>();

            if (fogImage == null)
            {
                GameObject fogObj = GameObject.Find("Fog");
                if (fogObj != null) fogImage = fogObj.GetComponent<Image>();
            }

            if (fogImage != null)
            {
                if (fogMaterial != null)
                {
                    fogMaterial = new Material(fogMaterial);
                    fogImage.material = fogMaterial;
                }
                // Fallback trong editor/dev nếu chưa gán material asset.
                else if (fogImage.material == null || fogImage.material.shader.name != "Custom/UI_FogWithLens")
                {
                    Shader shader = Shader.Find("Custom/UI_FogWithLens");
                    if (shader != null)
                    {
                        fogMaterial = new Material(shader);
                        fogImage.material = fogMaterial;
                    }
                }
                else
                {
                    fogMaterial = fogImage.material;
                }
            }
            
            UpdateFogShader();
        }

        private void Update()
        {
            // Cập nhật liên tục trong trường hợp Kính lúp bị di chuyển bởi script khác hoặc animation
            UpdateFogShader();
        }

        private void OnDisable()
        {
            ShowCursor();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isHoldingLens = true;
            Cursor.visible = false;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ShowCursor();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            transform.SetAsLastSibling(); // Lên trên cùng
            isHoldingLens = true;
            Cursor.visible = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)transform.parent, 
                eventData.position, 
                eventData.pressEventCamera, 
                out Vector3 worldPoint))
            {
                transform.position = worldPoint;
            }
            
            UpdateFogShader();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            ShowCursor();

            // Không trượt về, để nguyên kính lúp ở đó để người chơi tiện nhìn
            // (hoặc nếu muốn nó trượt về thì uncomment code dưới)
            /*
            StartCoroutine(SlideTo(originalPosition, 0.3f));
            */
        }

        private void ShowCursor()
        {
            if (!isHoldingLens) return;

            isHoldingLens = false;
            Cursor.visible = true;
        }

        private void UpdateFogShader()
        {
            if (fogMaterial != null)
            {
                // Chuyển World Position của kính lúp sang Screen Position
                Camera cam = null;
                if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    cam = rootCanvas.worldCamera;
                }
                
                Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, rectTransform.position);
                
                // Truyền Screen Position vào Shader
                fogMaterial.SetVector("_LensWorldPos", new Vector4(screenPos.x, screenPos.y, 0, 0));
                
                // Tùy theo scale của màn hình mà radius có thể cần nhân với scaleFactor
                float actualRadius = lensRadius;
                float actualSoftness = lensSoftness;
                
                if (rootCanvas != null)
                {
                    actualRadius *= rootCanvas.scaleFactor;
                    actualSoftness *= rootCanvas.scaleFactor;
                }
                
                fogMaterial.SetFloat("_LensRadius", actualRadius);
                fogMaterial.SetFloat("_LensSoftness", actualSoftness);
            }
        }
    }
}
