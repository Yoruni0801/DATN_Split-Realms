/*
 * Kịch bản này đã được VÔ HIỆU HÓA theo yêu cầu.
 * Hệ thống mới sử dụng RopeNode.cs
 *
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace Tien.Minigames
{
    public class ConnectNode : MonoBehaviour
    {
        public int nodeIndex;
        public Image nodeImage;
        private ConnectDotsManager manager;
        
        private Color inactiveColor = new Color(0.4f, 0.4f, 0.4f, 1f); // Xám
        private Color activeColor = Color.white; // Sáng trắng
        private Color completedColor = Color.cyan; // Xanh khi đã nối

        public void Setup(int index, ConnectDotsManager mgr)
        {
            nodeIndex = index;
            manager = mgr;
            if (nodeImage == null) nodeImage = GetComponent<Image>();
        }

        private void Awake()
        {
            if (manager == null) manager = GetComponentInParent<ConnectDotsManager>();
            if (nodeImage == null) nodeImage = GetComponent<Image>();
            
            if (nodeImage != null)
            {
                nodeImage.raycastTarget = true; // Bắt buộc nhận Click
            }

            // Gắn thêm Button mặc định của Unity để ĐẢM BẢO 100% bắt được click chuột
            Button btn = GetComponent<Button>();
            if (btn == null)
            {
                btn = gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
            }
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(ForceClick);
        }

        public void ForceClick()
        {
            if (manager != null) manager.OnNodePointerDown(this);
        }

        public void SetState(int state) // 0: Inactive, 1: Active (Current), 2: Completed
        {
            if (nodeImage == null) return;
            
            nodeImage.DOKill(); 
            nodeImage.transform.DOKill();

            if (state == 0)
            {
                nodeImage.color = inactiveColor;
                nodeImage.transform.localScale = Vector3.one;
            }
            else if (state == 1)
            {
                nodeImage.color = activeColor;
                nodeImage.transform.localScale = Vector3.one;
                // Hiệu ứng Pulse (Nhịp đập) báo hiệu cần bấm vào đây
                nodeImage.transform.DOScale(1.2f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
            }
            else if (state == 2)
            {
                nodeImage.color = completedColor;
                nodeImage.transform.localScale = Vector3.one;
            }
        }
    }
}
*/
