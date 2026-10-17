using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tien.Minigames
{
    public class HammerNail : MonoBehaviour, IPointerDownHandler
    {
        public int requiredHits = 2; // Cần nhấp chuột 2 lần để đóng xong
        private int currentHits = 0;
        public bool isDriven = false;
        private DraggablePlank parentPlank;
        private RectTransform rectTransform;

        void Awake()
        {
            parentPlank = GetComponentInParent<DraggablePlank>();
            rectTransform = GetComponent<RectTransform>();

            // Ẩn đi khi mới bắt đầu, chỉ hiện ra khi Ván đã được lắp vào
            gameObject.SetActive(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (isDriven) return;

            currentHits++;

            // Hiệu ứng lún đinh (Nhỏ lại một chút)
            float shrinkRatio = 1f - (currentHits * 0.2f);
            rectTransform.localScale = new Vector3(shrinkRatio, shrinkRatio, 1f);

            // Báo cho Ván biết để phát âm thanh
            if (parentPlank != null) parentPlank.OnNailHit();

            if (currentHits >= requiredHits)
            {
                isDriven = true;
                // Có thể đổi màu đinh thành tối hơn để thể hiện là đã đóng xong
                Image img = GetComponent<Image>();
                if (img != null) img.color = new Color(0.6f, 0.6f, 0.6f, 1f);

                if (parentPlank != null) parentPlank.OnNailFullyDriven();
            }
        }
    }
}
