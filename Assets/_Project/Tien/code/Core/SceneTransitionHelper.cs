using UnityEngine;

namespace Tien.Core
{
    public class SceneTransitionHelper : MonoBehaviour
    {
        [Tooltip("Điền tên chính xác của Scene bạn muốn chuyển tới (ví dụ: Map 1)")]
        public string sceneToLoad;

        // Hàm này dùng để gắn vào nút bấm (UI Button) hoặc gọi từ Event
        public void LoadScene()
        {
            if (!string.IsNullOrEmpty(sceneToLoad))
            {
                Tien.AdvancedLoading.Core.SceneLoader.Load(sceneToLoad);
            }
            else
            {
                Debug.LogWarning("Chưa điền tên Scene cần chuyển tới!");
            }
        }

        // Hàm này bạn có thể gọi từ Code khác (ví dụ: OnTriggerEnter khi chạm vào cửa)
        public void LoadSpecificScene(string specificSceneName)
        {
            Tien.AdvancedLoading.Core.SceneLoader.Load(specificSceneName);
        }
    }
}
