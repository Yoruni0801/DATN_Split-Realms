using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Tien.Minigames
{
    [System.Serializable]
    public class LockSlot
    {
        public Graphic background;        // Viền nền
        public RectTransform reelTransform; // Trục cuộn chứa 5 tấm ảnh
        
        [Tooltip("0: ExtraTop, 1: Top, 2: Center, 3: Bottom, 4: ExtraBottom")]
        public Image[] images = new Image[5]; 
    }

    public class CombinationLockUI : MonoBehaviour
    {
        public static CombinationLockUI Instance { get; private set; }

        [Header("UI Cài đặt Đầu vào")]
        public GameObject uiPanel;
        public LockSlot[] slots = new LockSlot[3];
        public Sprite[] symbolSprites = new Sprite[6];

        [Header("==== TÙY CHỈNH KÍCH THƯỚC (Kéo thả để căn chỉnh) ====")]
        [Tooltip("Kích thước của 1 hình ảnh biểu tượng")]
        public Vector2 symbolSize = new Vector2(80, 80);
        
        [Tooltip("Khoảng cách ngang giữa 3 cột (Kéo để kéo giãn ra hoặc thu hẹp lại)")]
        public float slotSpacingX = 140f;
        
        [Tooltip("Khoảng cách dọc giữa các biểu tượng trên con lăn (Kéo để nó khớp với các rãnh trên trụ đá)")]
        public float symbolSpacingY = 110f;

        [Header("==== TÙY CHỈNH MÀU SẮC & 3D ====")]
        [Tooltip("Màu của biểu tượng chính giữa")]
        public Color centerSymbolColor = Color.white;
        
        [Tooltip("Màu của biểu tượng ở trên/dưới rãnh trụ (Nên để xám hoặc hơi trong suốt)")]
        public Color edgeSymbolColor = new Color(0.4f, 0.4f, 0.4f, 0.8f);
        
        [Tooltip("Độ dẹp (Ép lùn) của ảnh ở mép để tạo cảm giác bị cong đi theo khối trụ (1 là không dẹp, 0.5 là dẹp một nửa)")]
        [Range(0.1f, 1f)]
        public float edgeSymbolScaleY = 0.6f;

        [Header("Màu chọn (Chớp nháy)")]
        public Color selectedColor = new Color(1f, 0.9f, 0.5f, 1f);
        public Color normalColor = Color.clear;

        [Header("Âm thanh")]
        public AudioSource audioSource;
        public AudioClip rollClip;
        public AudioClip errorClip;
        public AudioClip successClip;

        [Header("Mật khẩu Cài đặt")]
        public int[] correctPassword = new int[3] { 1, 4, 2 };

        private int[] currentValues = new int[3] { 0, 0, 0 };
        private int selectedSlot = 0;
        private bool isAnimating = false;

        private CombinationLockGate currentGate;
        private Player currentPlayer;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (uiPanel != null) uiPanel.SetActive(false);
        }

        public void OpenUI(CombinationLockGate gate, Player player)
        {
            currentGate = gate;
            currentPlayer = player;
            uiPanel.SetActive(true);

            if (currentPlayer != null) currentPlayer.SetInputActive(false);

            selectedSlot = 0;
            currentValues = new int[3] { 0, 0, 0 };
            isAnimating = false;
            
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].reelTransform != null) slots[i].reelTransform.anchoredPosition = Vector2.zero;
            }
            UpdateVisuals(true);
        }

        public void CloseUI()
        {
            uiPanel.SetActive(false);
            if (currentPlayer != null) currentPlayer.SetInputActive(true);
        }

        void Update()
        {
            if (!uiPanel.activeSelf || isAnimating) return;

            if (Input.GetKeyDown(KeyCode.A))
            {
                selectedSlot--;
                if (selectedSlot < 0) selectedSlot = 2;
                UpdateVisuals(false);
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                selectedSlot++;
                if (selectedSlot > 2) selectedSlot = 0;
                UpdateVisuals(false);
            }

            if (Input.GetKeyDown(KeyCode.W)) RollReel(-1);
            else if (Input.GetKeyDown(KeyCode.S)) RollReel(1);

            if (Input.GetKeyDown(KeyCode.J)) CheckPassword();
            if (Input.GetKeyDown(KeyCode.Escape)) CloseUI();
        }

        private void RollReel(int increment)
        {
            isAnimating = true;
            LockSlot slot = slots[selectedSlot];
            RectTransform reel = slot.reelTransform;
            
            // Hủy bỏ Tween cũ (nếu có) trên trục cuộn
            reel.DOKill();
            
            PlaySound(rollClip);
            
            int newValue = WrapIndex(currentValues[selectedSlot] + increment);
            currentValues[selectedSlot] = newValue;

            float targetY = increment * symbolSpacingY;
            float animDuration = 0.25f;

            // Animate vị trí cuộn
            reel.DOAnchorPosY(targetY, animDuration).SetEase(Ease.InOutSine).OnComplete(() =>
            {
                reel.anchoredPosition = Vector2.zero;
                UpdateVisuals(true);
                isAnimating = false;
            });

            // Animate Color và Scale để tạo hiệu ứng 3D cuộn
            for (int i = 0; i < 5; i++)
            {
                if (slot.images[i] == null) continue;
                int targetStateIndex = i - increment;
                ApplyTargetState(slot.images[i], targetStateIndex, animDuration);
            }
        }

        private void UpdateVisuals(bool updateImages)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                LockSlot slot = slots[i];
                if (slot == null) continue;

                if (slot.background != null)
                {
                    slot.background.color = (i == selectedSlot) ? selectedColor : normalColor;
                }

                if (updateImages && symbolSprites.Length > 0)
                {
                    int centerVal = currentValues[i];
                    
                    // Gán 5 ảnh
                    for (int j = 0; j < 5; j++)
                    {
                        if (slot.images[j] != null)
                        {
                            int spriteIndex = WrapIndex(centerVal + (j - 2));
                            slot.images[j].sprite = symbolSprites[spriteIndex];
                            
                            // Trả về trạng thái chuẩn
                            ApplyTargetState(slot.images[j], j, 0f);
                        }
                    }
                }
            }
        }

        private void ApplyTargetState(Image img, int stateIndex, float duration)
        {
            Color targetColor = Color.clear;
            float targetScaleY = edgeSymbolScaleY;

            if (stateIndex == 2) 
            {
                targetColor = centerSymbolColor;
                targetScaleY = 1f;
            }
            else if (stateIndex == 1 || stateIndex == 3)
            {
                targetColor = edgeSymbolColor;
                targetScaleY = edgeSymbolScaleY;
            }

            // QUAN TRỌNG: Phải Kill toàn bộ Animation đang chạy trên ảnh này trước khi gán lệnh mới.
            // Nếu không, Animation cũ sẽ kết thúc muộn hơn 1 mili-giây và ghi đè lên trạng thái chuẩn (Gây lỗi dịch ảnh loạn xạ)
            img.DOKill();
            img.rectTransform.DOKill();

            if (duration > 0)
            {
                img.DOColor(targetColor, duration);
                img.rectTransform.DOScaleY(targetScaleY, duration);
            }
            else
            {
                img.color = targetColor;
                img.rectTransform.localScale = new Vector3(1, targetScaleY, 1);
            }
        }

        private int WrapIndex(int index)
        {
            while (index < 0) index += 6;
            return index % 6;
        }

        private void CheckPassword()
        {
            bool isCorrect = true;
            for (int i = 0; i < 3; i++)
            {
                if (currentValues[i] != correctPassword[i])
                {
                    isCorrect = false;
                    break;
                }
            }

            if (isCorrect)
            {
                PlaySound(successClip);
                CloseUI();
                if (currentGate != null) currentGate.OpenGate();
            }
            else
            {
                PlaySound(errorClip);
                if (QuestUIManager.Instance != null)
                {
                    QuestUIManager.Instance.ShowDialogue("Mật khẩu không đúng! Các biểu tượng chưa khớp...", 2f);
                }
                uiPanel.transform.DOShakePosition(0.3f, 10f, 20);
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
        // Tự động căn chỉnh khi bạn kéo thanh trượt trong Inspector
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            if (slots == null || slots.Length == 0) return;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null || slots[i].reelTransform == null) continue;

                // Căn khoảng cách X giữa 3 cột
                if (slots[i].background != null)
                {
                    RectTransform bgRect = slots[i].background.rectTransform;
                    if (bgRect != null)
                    {
                        bgRect.anchoredPosition = new Vector2(-slotSpacingX + (i * slotSpacingX), 0);
                    }
                }

                // Căn khoảng cách Y, kích thước và Hiệu ứng 3D
                if (slots[i].images != null && slots[i].images.Length == 5)
                {
                    for (int j = 0; j < 5; j++)
                    {
                        Image img = slots[i].images[j];
                        if (img != null && img.rectTransform != null)
                        {
                            img.rectTransform.sizeDelta = symbolSize;
                            
                            float yPos = 0;
                            if (j == 0) yPos = symbolSpacingY * 2;
                            if (j == 1) yPos = symbolSpacingY;
                            if (j == 2) yPos = 0;
                            if (j == 3) yPos = -symbolSpacingY;
                            if (j == 4) yPos = -symbolSpacingY * 2;

                            img.rectTransform.anchoredPosition = new Vector2(0, yPos);

                            ApplyTargetState(img, j, 0f); // Tự động hiển thị dạng dẹp/trong suốt
                        }
                    }
                }
            }
        }

        [ContextMenu("Auto Setup UI")]
        public void AutoSetupUI()
        {
            if (uiPanel == null)
            {
                GameObject panel = new GameObject("CombinationLock_Panel");
                panel.transform.SetParent(this.transform, false);
                panel.AddComponent<CanvasRenderer>();
                Image panelImg = panel.AddComponent<Image>();
                // Để nền trống (trong suốt) để bạn có thể lót hình cái ổ khóa đá ở dưới
                panelImg.color = Color.clear;
                
                RectTransform panelRect = panel.GetComponent<RectTransform>();
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.sizeDelta = Vector2.zero;
                
                uiPanel = panel;
            }

            slots = new LockSlot[3];

            for (int i = 0; i < 3; i++)
            {
                slots[i] = new LockSlot();

                GameObject bgObj = new GameObject($"SlotBg_{i + 1}");
                bgObj.transform.SetParent(uiPanel.transform, false);
                bgObj.AddComponent<CanvasRenderer>();
                Image bgImg = bgObj.AddComponent<Image>();
                bgImg.color = normalColor;
                RectTransform bgRect = bgObj.GetComponent<RectTransform>();
                bgRect.sizeDelta = new Vector2(100, 100);
                slots[i].background = bgImg;

                GameObject reelObj = new GameObject($"Reel");
                reelObj.transform.SetParent(bgObj.transform, false);
                RectTransform reelRect = reelObj.AddComponent<RectTransform>();
                reelRect.anchorMin = Vector2.zero;
                reelRect.anchorMax = Vector2.one;
                reelRect.sizeDelta = Vector2.zero;
                slots[i].reelTransform = reelRect;

                slots[i].images = new Image[5];
                slots[i].images[0] = CreateImage("ExtraTop", reelRect);
                slots[i].images[1] = CreateImage("Top", reelRect);
                slots[i].images[2] = CreateImage("Center", reelRect);
                slots[i].images[3] = CreateImage("Bottom", reelRect);
                slots[i].images[4] = CreateImage("ExtraBottom", reelRect);
            }

            correctPassword = new int[] { 1, 4, 2 };
            UnityEditor.EditorUtility.SetDirty(this);
            OnValidate(); // Tự động sắp xếp vị trí sau khi tạo xong
            Debug.Log("Đã tự động Setup lại UI Ổ Khóa chuẩn 3D. Bạn có thể kéo các thanh trượt trong Inspector để tinh chỉnh vị trí ảnh!");
        }

        private Image CreateImage(string name, RectTransform parent)
        {
            GameObject imgObj = new GameObject(name);
            imgObj.transform.SetParent(parent, false);
            imgObj.AddComponent<CanvasRenderer>();
            Image img = imgObj.AddComponent<Image>();
            RectTransform rect = imgObj.GetComponent<RectTransform>();
            
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            
            return img;
        }
#endif
    }
}
