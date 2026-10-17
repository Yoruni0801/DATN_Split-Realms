using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

namespace Tien.Minigames
{
    public class HiddenObjectMinigame : MonoBehaviour
    {
        [Header("UI Cài đặt")]
        public GameObject uiPanel;

        [Tooltip("TextMeshPro hiển thị tiến độ nhiệm vụ")]
        public TextMeshProUGUI questText;

        [Tooltip("Nội dung tiêu đề nhiệm vụ")]
        public string questPrefix = "Nhiệm vụ: Tìm 3 viên đá cổ đại";

        [Tooltip("Các nút ẩn tương ứng với 3 vị trí cần tìm")]
        public Button[] targetButtons;

        [Tooltip("Các hình vòng tròn khoanh đỏ sẽ hiện ra khi tìm đúng")]
        public GameObject[] circleGraphics;

        [Header("Phần Thưởng")]
        public int stonesToGive = 3;

        [Tooltip("Kéo Prefab vật phẩm (ví dụ: Mảnh đá) muốn rớt ra vào đây")]
        public GameObject stonePrefab;

        [Tooltip("Vị trí rớt vật phẩm. Nếu để trống sẽ rớt ngay tại vị trí của nhân vật")]
        public Transform dropPoint;

        [Header("Âm thanh")]
        public AudioSource audioSource;
        public AudioClip clickClip;
        public AudioClip winClip;

        private int foundCount = 0;
        private bool[] isFound;
        private Player currentPlayer;
        private bool isCompleted = false;

        void Start()
        {
            if (questText == null && uiPanel != null)
            {
                questText = uiPanel.GetComponentInChildren<TextMeshProUGUI>();
            }

            UpdateQuestText();

            if (uiPanel != null)
            {
                Button[] buttons = uiPanel.GetComponentsInChildren<Button>(true);
                foreach (var b in buttons)
                {
                    if (b.gameObject.name == "CloseBtn")
                    {
                        b.onClick.RemoveAllListeners();
                        b.onClick.AddListener(CloseMinigame);
                    }
                }
                uiPanel.SetActive(false);
            }

            if (targetButtons != null && targetButtons.Length > 0)
            {
                isFound = new bool[targetButtons.Length];

                // Gán sự kiện cho từng nút
                for (int i = 0; i < targetButtons.Length; i++)
                {
                    int index = i; // Fix bug capture variable trong lambda
                    targetButtons[i].onClick.AddListener(() => OnTargetClicked(index));

                    if (circleGraphics != null && i < circleGraphics.Length && circleGraphics[i] != null)
                    {
                        circleGraphics[i].SetActive(false); // Ẩn vòng tròn lúc đầu
                    }
                }
            }
        }

        public void OpenMinigame(Player player)
        {
            if (isCompleted)
            {
                if (QuestUIManager.Instance != null)
                {
                    QuestUIManager.Instance.ShowDialogue("Bạn đã tìm thấy đủ các khối đá ở khu vực này rồi.", 2f);
                }
                return;
            }

            currentPlayer = player;
            if (uiPanel != null) uiPanel.SetActive(true);

            // Khóa di chuyển
            if (currentPlayer != null)
            {
                currentPlayer.SetInputActive(false);
            }
        }

        public void CloseMinigame()
        {
            if (uiPanel != null) uiPanel.SetActive(false);

            if (currentPlayer != null)
            {
                currentPlayer.SetInputActive(true);
            }
        }

        private void OnTargetClicked(int index)
        {
            if (isCompleted || isFound[index]) return;

            // Đánh dấu đã tìm thấy
            isFound[index] = true;
            foundCount++;
            UpdateQuestText();
            
            PlaySound(clickClip);

            // Hiện vòng tròn khoanh đỏ với hiệu ứng
            if (circleGraphics != null && index < circleGraphics.Length && circleGraphics[index] != null)
            {
                GameObject circle = circleGraphics[index];
                circle.SetActive(true);

                // Animation pop-up nhỏ nhắn xinh xắn
                circle.transform.localScale = Vector3.zero;
                circle.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
            }

            // Kiểm tra chiến thắng
            if (foundCount >= targetButtons.Length)
            {
                WinMinigame();
            }
        }

        private void UpdateQuestText()
        {
            if (questText != null)
            {
                int total = (targetButtons != null && targetButtons.Length > 0) ? targetButtons.Length : 3;
                questText.text = $"{questPrefix} ({foundCount}/{total})";
            }
        }

        private void WinMinigame()
        {
            isCompleted = true;
            PlaySound(winClip);
            Debug.Log($"Đã tìm đủ {targetButtons.Length} điểm! Nhận được {stonesToGive} viên đá.");

            // Rớt vật phẩm hoặc cộng thẳng vào kho
            if (stonePrefab != null)
            {
                for (int i = 0; i < stonesToGive; i++)
                {
                    Vector3 spawnPos = dropPoint != null ? dropPoint.position : (currentPlayer != null ? currentPlayer.transform.position : transform.position);
                    // Rớt tản ra một chút để không đè lên nhau
                    spawnPos += new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(0f, 0.5f), 0);
                    Instantiate(stonePrefab, spawnPos, Quaternion.identity);
                }
            }
            else
            {
                if (StoneManager.Instance != null)
                {
                    StoneManager.Instance.stoneCount += stonesToGive;
                }
            }

            if (QuestUIManager.Instance != null)
            {
                QuestUIManager.Instance.ShowDialogue($"Thật tuyệt! Đã tìm được {stonesToGive} khối đá kỳ lạ. Hãy nhặt chúng lên!", 3f);
            }

            // Đóng game sau 1.5 giây
            DOVirtual.DelayedCall(1.5f, () =>
            {
                CloseMinigame();
            });
        }

        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Auto Setup UI")]
        public void AutoSetupUI()
        {
            if (uiPanel == null)
            {
                GameObject panel = new GameObject("HiddenObject_Panel");
                panel.transform.SetParent(this.transform, false);
                panel.AddComponent<CanvasRenderer>();
                
                Image panelImg = panel.AddComponent<Image>();
                // Để màu trắng để bạn tự kéo ảnh vào Source Image sau
                panelImg.color = Color.white; 
                
                RectTransform panelRect = panel.GetComponent<RectTransform>();
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.sizeDelta = Vector2.zero;
                
                uiPanel = panel;
            }

            // Đảm bảo có nút thoát
            GameObject closeBtnObj = new GameObject("CloseBtn");
            closeBtnObj.transform.SetParent(uiPanel.transform, false);
            Image closeImg = closeBtnObj.AddComponent<Image>();
            closeImg.color = Color.red;
            Button closeBtn = closeBtnObj.AddComponent<Button>();

            RectTransform closeRect = closeBtnObj.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1, 1);
            closeRect.anchorMax = new Vector2(1, 1);
            closeRect.pivot = new Vector2(1, 1);
            closeRect.anchoredPosition = new Vector2(-20, -20);
            closeRect.sizeDelta = new Vector2(50, 50);

            // Tạo 3 điểm ẩn
            if (targetButtons == null || targetButtons.Length != 3) targetButtons = new Button[3];
            if (circleGraphics == null || circleGraphics.Length != 3) circleGraphics = new GameObject[3];

            for (int i = 0; i < 3; i++)
            {
                if (targetButtons[i] == null)
                {
                    // 1. Tạo vùng bấm tàng hình
                    GameObject btnObj = new GameObject($"TargetZone_{i + 1}");
                    btnObj.transform.SetParent(uiPanel.transform, false);
                    Image btnImg = btnObj.AddComponent<Image>();
                    btnImg.color = new Color(1, 1, 1, 0); // Trong suốt hoàn toàn
                    Button btn = btnObj.AddComponent<Button>();
                    
                    RectTransform btnRect = btnObj.GetComponent<RectTransform>();
                    btnRect.sizeDelta = new Vector2(100, 100);
                    btnRect.anchoredPosition = new Vector2(-200 + (i * 200), 0); // Trải ngang ra
                    
                    targetButtons[i] = btn;

                    // 2. Tạo vòng tròn đỏ bên trong
                    GameObject circleObj = new GameObject("CircleGraphic");
                    circleObj.transform.SetParent(btnObj.transform, false);
                    Image circleImg = circleObj.AddComponent<Image>();
                    circleImg.color = Color.red; // Khung đỏ (Bạn có thể kéo Sprite hình tròn rỗng vào đây sau)
                    
                    RectTransform circleRect = circleObj.GetComponent<RectTransform>();
                    circleRect.anchorMin = Vector2.zero;
                    circleRect.anchorMax = Vector2.one;
                    circleRect.sizeDelta = Vector2.zero; // Scale full theo cha
                    
                    circleGraphics[i] = circleObj;
                    circleObj.SetActive(false);
                }
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("Setup xong Minigame Tìm Đồ. Hãy kéo Ảnh Của Bạn vào Image của HiddenObject_Panel, và thay đổi vị trí của các TargetZone cho khớp nhé!");
        }
#endif
    }
}
