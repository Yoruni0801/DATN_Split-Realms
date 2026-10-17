// MINIgame/PuzzlePiece.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Tien.Minigames
{
    public class PuzzlePiece : MonoBehaviour, IPointerClickHandler
    {
        public int correctIndex;
        public int currentIndex;
        public RawImage image;

        private PuzzleManager manager;

        // Cài đặt thông số mảnh ghép khi tạo ra
        public void Setup(int correctIdx, Texture tex, Rect uv, PuzzleManager mgr)
        {
            correctIndex = correctIdx;
            manager = mgr;
            
            image = GetComponent<RawImage>();
            image.texture = tex;
            image.uvRect = uv;
        }

        // Xử lý khi người chơi click vào mảnh ghép này
        public void OnPointerClick(PointerEventData eventData)
        {
            if (manager != null)
            {
                manager.OnPieceClicked(this);
            }
        }

        // Làm nổi bật khi được chọn
        public void Highlight(bool on)
        {
            if (image != null)
            {
                image.color = on ? Color.gray : Color.white;
            }
        }
    }
}
