using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Tien.Minigames
{
    public class StoneGateUI : MonoBehaviour
    {
        [Header("UI Cài đặt")]
        public GameObject uiPanel;
        public int requiredStones = 3;

        [Tooltip("Quy định thứ tự đúng: Ví dụ [2, 0, 1] nghĩa là Lỗ 1 cần Đá 3, Lỗ 2 cần Đá 1, Lỗ 3 cần Đá 2")]
        public int[] correctOrder = new int[] { 0, 1, 2 };

        [Header("DOTween Animation")]
        [Tooltip("Kéo 3 Hòn đá (UI Image/RectTransform) ở bên dưới vào đây")]
        public RectTransform[] bottomStones; 
        [Tooltip("Kéo 3 Ô trống (UI Image/RectTransform) ở trên vào đây để làm đích đến")]
        public RectTransform[] topSlots;

        [Tooltip("Độ phóng to khi chọn đá")]
        public float hoverScale = 1.15f;
        [Tooltip("Độ nảy lên (theo trục Y của UI) khi chọn đá")]
        public float hoverOffsetY = 20f;
        [Tooltip("Thời gian chuyển động animation (giây)")]
        public float tweenDuration = 0.2f;

        [Header("Âm thanh")]
        public AudioSource audioSource;
        public AudioClip selectClip;
        public AudioClip placeClip;
        public AudioClip errorClip;
        public AudioClip successClip;

        private StoneGate currentGate;
        private Player currentPlayer;

        private int selectedIndex = 0;
        private bool[] isStonePlaced;
        private int placedCount = 0;
        private int[] placedStoneIndices;
        private float[] originalY;

        void Start()
        {
            if (uiPanel != null) uiPanel.SetActive(false);

            if (bottomStones != null && bottomStones.Length > 0)
            {
                isStonePlaced = new bool[bottomStones.Length];
                originalY = new float[bottomStones.Length];
                for (int i = 0; i < bottomStones.Length; i++)
                {
                    // Lưu lại vị trí Y gốc của các viên đá
                    originalY[i] = bottomStones[i].anchoredPosition.y;
                }
            }
        }

        public void OpenUI(StoneGate gate, Player player)
        {
            currentGate = gate;
            currentPlayer = player;
            uiPanel.SetActive(true);

            // Khóa di chuyển và input của người chơi (dùng hàm có sẵn trong Player.cs)
            if (currentPlayer != null)
            {
                currentPlayer.SetInputActive(false);
            }

            // --- Lấy số lượng đá người chơi đang có ---
            int availableStones = Mathf.Min(StoneManager.Instance.stoneCount, bottomStones.Length);

            // --- Reset trạng thái puzzle ---
            placedCount = 0;
            selectedIndex = -1; // Sẽ tự động nhảy đến viên đá hợp lệ đầu tiên
            placedStoneIndices = new int[topSlots.Length];

            for (int i = 0; i < bottomStones.Length; i++)
            {
                isStonePlaced[i] = false;
                
                // Trả về kích thước và vị trí cũ
                bottomStones[i].localScale = Vector3.one;
                Vector2 pos = bottomStones[i].anchoredPosition;
                pos.y = originalY[i];
                bottomStones[i].anchoredPosition = pos;

                // Chỉ hiện những viên đá đã nhặt được
                if (i < availableStones)
                {
                    bottomStones[i].gameObject.SetActive(true);
                }
                else
                {
                    bottomStones[i].gameObject.SetActive(false);
                }
            }

            // Cập nhật con trỏ vào viên đầu tiên có sẵn
            if (availableStones > 0)
            {
                selectedIndex = 0;
                UpdateSelectionVisual();
            }
        }

        public void CloseUI()
        {
            uiPanel.SetActive(false);

            // Mở lại di chuyển cho Player
            if (currentPlayer != null)
            {
                currentPlayer.SetInputActive(true);
            }
        }

        void Update()
        {
            if (!uiPanel.activeSelf) return;

            // Xử lý Input phím A, D, J
            if (Input.GetKeyDown(KeyCode.A))
            {
                MoveSelection(-1);
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                MoveSelection(1);
            }
            else if (Input.GetKeyDown(KeyCode.J))
            {
                ConfirmSelection();
            }
        }

        private void MoveSelection(int direction)
        {
            // Nếu đã nhét hết số đá mình có, thì không cho chọn nữa
            if (placedCount >= StoneManager.Instance.stoneCount || placedCount >= requiredStones) return;

            int prevIndex = selectedIndex;
            
            // Tìm viên đá tiếp theo (bỏ qua những viên đã được đặt vào khe và những viên chưa nhặt được - bị ẩn)
            int loopCount = 0;
            do
            {
                selectedIndex += direction;
                if (selectedIndex < 0) selectedIndex = bottomStones.Length - 1;
                if (selectedIndex >= bottomStones.Length) selectedIndex = 0;
                
                loopCount++;
                if (loopCount > bottomStones.Length) 
                {
                    selectedIndex = prevIndex; // Không tìm thấy viên nào khác, giữ nguyên
                    return; 
                }
                
            } while (isStonePlaced[selectedIndex] || !bottomStones[selectedIndex].gameObject.activeSelf);

            if (prevIndex != selectedIndex && prevIndex >= 0)
            {
                // Đưa viên đá cũ (vừa bỏ chọn) về trạng thái bình thường
                if (!isStonePlaced[prevIndex])
                {
                    bottomStones[prevIndex].DOScale(1f, tweenDuration);
                    bottomStones[prevIndex].DOAnchorPosY(originalY[prevIndex], tweenDuration);
                }
            }
            
            UpdateSelectionVisual();
            PlaySound(selectClip);
        }

        private void UpdateSelectionVisual()
        {
            if (selectedIndex < 0 || isStonePlaced[selectedIndex] || !bottomStones[selectedIndex].gameObject.activeSelf) return;

            // Phóng to viên đá đang được chọn và nảy nó lên cao một chút
            bottomStones[selectedIndex].DOScale(hoverScale, tweenDuration);
            bottomStones[selectedIndex].DOAnchorPosY(originalY[selectedIndex] + hoverOffsetY, tweenDuration);
        }

        private void ConfirmSelection()
        {
            if (selectedIndex < 0) return;

            // Kiểm tra hợp lệ chung
            if (isStonePlaced[selectedIndex] || placedCount >= requiredStones || placedCount >= topSlots.Length) return;

            isStonePlaced[selectedIndex] = true;
            placedStoneIndices[placedCount] = selectedIndex;
            
            RectTransform movingStone = bottomStones[selectedIndex];
            RectTransform targetSlot = topSlots[placedCount];

            // Cho viên đá bay vào vị trí ô trống trên cùng bằng DOTween
            movingStone.DOMove(targetSlot.position, 0.5f).SetEase(Ease.OutCubic);
            movingStone.DOScale(1f, 0.5f);
            
            PlaySound(placeClip);

            placedCount++;

            if (placedCount >= requiredStones)
            {
                // Đã đặt đủ đá -> Kiểm tra kết quả
                DOVirtual.DelayedCall(0.6f, () => VerifySequence());
            }
            else
            {
                // Nếu chưa đủ mà vẫn còn đá chưa đặt thì nhảy con trỏ sang viên khác
                if (placedCount < StoneManager.Instance.stoneCount)
                {
                    MoveSelection(1);
                }
                else
                {
                    // Nếu đã nhét hết sạch đá trong túi mà vẫn chưa đủ
                    Debug.Log("Đã dùng hết đá trong túi nhưng chưa đủ!");
                    if (QuestUIManager.Instance != null)
                    {
                        QuestUIManager.Instance.ShowDialogue("Vẫn còn thiếu đá. Tôi cần phải đi tìm thêm ở đâu đó quanh đây.", 3f);
                    }
                }
            }
        }

        private void VerifySequence()
        {
            bool isCorrect = true;
            for (int i = 0; i < requiredStones; i++)
            {
                if (placedStoneIndices[i] != correctOrder[i])
                {
                    isCorrect = false;
                    break;
                }
            }

            if (isCorrect)
            {
                // Đúng hoàn toàn
                StoneManager.Instance.UseStones(requiredStones);
                StoneManager.Instance.isGateSolved = true;
                
                PlaySound(successClip);
                Debug.Log("Đã xếp đúng thứ tự đá, mở cổng thành công!");
                if (currentGate != null) currentGate.OpenGate();
                CloseUI();
            }
            else
            {
                // Sai thứ tự
                PlaySound(errorClip);
                Debug.Log("Sai thứ tự đá! Reset lại.");
                if (QuestUIManager.Instance != null)
                {
                    QuestUIManager.Instance.ShowDialogue("Thứ tự các viên đá không đúng. Hãy thử lại!", 2f);
                }

                // Animation báo lỗi (Rung)
                for (int i = 0; i < requiredStones; i++)
                {
                    int stoneIdx = placedStoneIndices[i];
                    bottomStones[stoneIdx].DOShakeAnchorPos(0.3f, 10f, 20);
                }

                // Đợi một chút rồi trả đá về chỗ cũ
                DOVirtual.DelayedCall(1f, () => 
                {
                    for (int i = 0; i < requiredStones; i++)
                    {
                        int stoneIdx = placedStoneIndices[i];
                        isStonePlaced[stoneIdx] = false;
                        bottomStones[stoneIdx].DOScale(1f, tweenDuration);
                        bottomStones[stoneIdx].DOAnchorPosY(originalY[stoneIdx], tweenDuration);
                    }
                    
                    placedCount = 0;
                    
                    // Cập nhật lại trỏ chuột vào viên đầu tiên
                    int availableStones = Mathf.Min(StoneManager.Instance.stoneCount, bottomStones.Length);
                    if (availableStones > 0)
                    {
                        selectedIndex = 0;
                        UpdateSelectionVisual();
                    }
                });
            }
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
                // Tự động tạo Panel nếu chưa có
                GameObject panel = new GameObject("StoneGate_Panel");
                panel.transform.SetParent(this.transform, false);
                panel.AddComponent<CanvasRenderer>();
                Image panelImg = panel.AddComponent<Image>();
                panelImg.color = new Color(0, 0, 0, 0.8f);
                
                RectTransform panelRect = panel.GetComponent<RectTransform>();
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.sizeDelta = Vector2.zero;
                
                uiPanel = panel;
            }

            // Đảm bảo có đủ 3 ô mảng
            if (bottomStones == null || bottomStones.Length != 3) bottomStones = new RectTransform[3];
            if (topSlots == null || topSlots.Length != 3) topSlots = new RectTransform[3];

            // Tự động sinh ra Top Slots (Khe trống phía trên)
            for (int i = 0; i < 3; i++)
            {
                if (topSlots[i] == null)
                {
                    GameObject slot = new GameObject($"TopSlot_{i + 1}");
                    slot.transform.SetParent(uiPanel.transform, false);
                    slot.AddComponent<CanvasRenderer>();
                    Image img = slot.AddComponent<Image>();
                    img.color = new Color(0.2f, 0.2f, 0.2f, 0.5f); // Màu tối mờ cho khe
                    
                    RectTransform rect = slot.GetComponent<RectTransform>();
                    rect.sizeDelta = new Vector2(100, 100);
                    // Dàn hàng ngang cách nhau 150px, lệch lên trên
                    rect.anchoredPosition = new Vector2(-150 + (i * 150), 150);
                    
                    topSlots[i] = rect;
                }
            }

            // Tự động sinh ra Bottom Stones (Đá ở dưới)
            for (int i = 0; i < 3; i++)
            {
                if (bottomStones[i] == null)
                {
                    GameObject stone = new GameObject($"BottomStone_{i + 1}");
                    stone.transform.SetParent(uiPanel.transform, false);
                    stone.AddComponent<CanvasRenderer>();
                    Image img = stone.AddComponent<Image>();
                    img.color = new Color(0.8f, 0.8f, 0.8f, 1f); // Màu xám sáng cho đá
                    
                    RectTransform rect = stone.GetComponent<RectTransform>();
                    rect.sizeDelta = new Vector2(80, 80);
                    // Dàn hàng ngang cách nhau 120px, lệch xuống dưới
                    rect.anchoredPosition = new Vector2(-120 + (i * 120), -150);
                    
                    bottomStones[i] = rect;
                }
            }

            requiredStones = 3;
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("Đã tự động tạo và Setup UI thành công! Vui lòng kiểm tra lại giao diện trong Canvas.");
        }
#endif
    }
}
