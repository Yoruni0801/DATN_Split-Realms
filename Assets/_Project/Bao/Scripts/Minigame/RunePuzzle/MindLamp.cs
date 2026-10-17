using UnityEngine;
using UnityEngine.EventSystems;

namespace Minigame.RunePuzzle
{
    /// <summary>
    /// Gắn vào nút Tâm Kính (kính lúp/ngọn đèn dầu).
    /// Khi người chơi nhấn giữ: sương mù tan, câu đố hiện.
    /// Khi thả: sương mù quay lại, không kéo đá được khi đang giữ.
    /// </summary>
    public class MindLamp : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public void OnPointerDown(PointerEventData eventData)
        {
            if (RunePuzzleManager.Instance != null)
            {
                RunePuzzleManager.Instance.SetLightActive(true);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (RunePuzzleManager.Instance != null)
            {
                RunePuzzleManager.Instance.SetLightActive(false);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Nếu kéo chuột ra ngoài thì tắt đèn
            if (RunePuzzleManager.Instance != null)
            {
                RunePuzzleManager.Instance.SetLightActive(false);
            }
        }
    }
}
