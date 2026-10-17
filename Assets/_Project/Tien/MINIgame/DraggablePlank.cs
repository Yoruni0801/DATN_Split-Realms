using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Tien.Minigames
{
    public class DraggablePlank : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public RectTransform targetSlot;
        public float snapDistance = 150f; // Khoảng cách hút

        public List<HammerNail> nails;

        private RectTransform rectTransform;
        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private Vector2 originalPosition;
        private bool isLocked = false;
        private int drivenNails = 0;
        private BridgeRepairMinigame manager;

        void Start()
        {
            rectTransform = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>();

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            manager = Object.FindFirstObjectByType<BridgeRepairMinigame>();
            originalPosition = rectTransform.anchoredPosition;

            // Tìm đinh là con của ván
            if (nails == null || nails.Count == 0)
            {
                nails = new List<HammerNail>(GetComponentsInChildren<HammerNail>(true));
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (isLocked) return;
            canvasGroup.alpha = 0.7f;
            canvasGroup.blocksRaycasts = false; // Để tia ray xuyên qua ván, đụng trúng Background hoặc Target
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (isLocked || canvas == null) return;
            rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (isLocked) return;
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;

            // Tính toán khoảng cách (Chuyển vị trí của lỗ về cùng hệ trục tọa độ với ván gỗ)
            Vector2 targetLocalPos = rectTransform.parent.InverseTransformPoint(targetSlot.position);
            float distance = Vector2.Distance(rectTransform.localPosition, targetLocalPos);

            // Kiểm tra có gần ô đích không
            if (targetSlot != null && distance <= snapDistance)
            {
                // Khớp vào ô (chuyển vị trí thế giới để đảm bảo chính xác 100%)
                rectTransform.position = targetSlot.position;
                isLocked = true;

                // Hiện 4 cây đinh lên
                foreach (var nail in nails)
                {
                    nail.gameObject.SetActive(true);
                }

                if (manager != null) manager.OnPlankPlaced();
            }
            else
            {
                // Trượt! Bay về chỗ cũ
                rectTransform.anchoredPosition = originalPosition;
            }
        }

        // Đinh báo cáo khi bị gõ
        public void OnNailHit()
        {
            if (manager != null) manager.PlayHammerSound();
        }

        // Đinh báo cáo khi đóng xong
        public void OnNailFullyDriven()
        {
            drivenNails++;
            if (drivenNails >= nails.Count)
            {
                if (manager != null) manager.OnPlankSecured();
            }
        }
    }
}
