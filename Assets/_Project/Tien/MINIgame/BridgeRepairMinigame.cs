using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Tien.Minigames
{
    public class BridgeRepairMinigame : MonoBehaviour
    {
        [Header("Trạng thái Mini-game")]
        public GameObject minigamePanel;
        public int totalPlanks = 3;
        private int securedPlanks = 0;
        public bool isCompleted = false;

        [Header("Giao diện UI")]
        public GameObject targetsContainer; // Chứa 3 khe hở
        public GameObject planksContainer; // Chứa 3 ván gỗ để kéo

        [Header("Âm thanh & Phản hồi")]
        public AudioSource audioSource;
        public AudioClip plankSnapClip;
        public AudioClip hammerHitClip;
        public AudioClip plankCompleteClip;
        public AudioClip gameWinClip;

        void Start()
        {
            if (minigamePanel != null)
            {
                minigamePanel.SetActive(false);
            }
            else if (transform.childCount > 0)
            {
                transform.GetChild(0).gameObject.SetActive(false);
            }
        }

        public void OpenPuzzle()
        {
            if (isCompleted) return;

            Player player = Object.FindFirstObjectByType<Player>();
            if (player != null) player.SetControlLocked(true);

            if (minigamePanel != null)
            {
                minigamePanel.SetActive(true);
            }
            else
            {
                // Fallback nếu minigamePanel chưa được gán
                transform.GetChild(0).gameObject.SetActive(true);
            }
        }

        public void ClosePuzzle()
        {
            if (minigamePanel != null)
            {
                minigamePanel.SetActive(false);
            }
            else if (transform.childCount > 0)
            {
                transform.GetChild(0).gameObject.SetActive(false);
            }

            Player player = Object.FindFirstObjectByType<Player>();
            if (player != null) player.SetControlLocked(false);
        }

        // --- CÁC HÀM DO DraggablePlank GỌI ---

        public void OnPlankPlaced()
        {
            // Được gọi khi người chơi thả ván trúng khe hở
            PlaySound(plankSnapClip);
        }

        public void PlayHammerSound()
        {
            // Được gọi mỗi khi gõ đinh
            PlaySound(hammerHitClip);
        }

        public void OnPlankSecured()
        {
            // Được gọi khi 4 cây đinh của 1 ván đã đóng xong
            securedPlanks++;
            PlaySound(plankCompleteClip);

            if (securedPlanks >= totalPlanks)
            {
                OnMinigameWon();
            }
        }

        private void OnMinigameWon()
        {
            isCompleted = true;
            PlaySound(gameWinClip);
            Debug.Log("[BridgeRepair] Minigame WON! Đang đóng puzzle...");

            // Gọi sự kiện lia cam ra cầu giống hệ thống cũ
            NPCQuestGiver npc = Object.FindFirstObjectByType<NPCQuestGiver>();
            if (npc != null) npc.TriggerPanCameraToBridge(3f);

            // Bật hình ảnh cầu nguyên (quan trọng!)
            Bridge bridge = Object.FindFirstObjectByType<Bridge>();
            if (bridge != null)
            {
                bridge.SetVisuals(true);
            }

            // Đóng puzzle ngay lập tức sau khi thắng (Delay 0.5s để nhìn thành quả)
            StartCoroutine(ClosePuzzleDelayed());
        }

        private System.Collections.IEnumerator ClosePuzzleDelayed()
        {
            yield return new WaitForSeconds(0.5f);
            ClosePuzzle();
            Debug.Log("[BridgeRepair] Puzzle đã tắt.");
        }

        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Auto Setup UI Kéo Thả (Drag & Hammer)")]
        private void AutoSetupUI()
        {
            // Xóa UI cũ nếu có
            GameObject oldContent = GameObject.Find("MinigameContentPanel");
            if (oldContent != null) DestroyImmediate(oldContent);

            // Tạo Panel chính (Nền mờ)
            GameObject content = new GameObject("MinigameContentPanel");
            content.transform.SetParent(this.transform, false);

            // Đảm bảo có EventSystem trong Scene (Bắt buộc để kéo thả UI hoạt động)
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Debug.Log("Tự động tạo EventSystem (vì trong Scene chưa có).");
            }
            RectTransform rtContent = content.AddComponent<RectTransform>();
            rtContent.anchorMin = Vector2.zero; rtContent.anchorMax = Vector2.one; rtContent.sizeDelta = Vector2.zero;
            Image bg = content.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.8f);
            minigamePanel = content;

            // 1. Tạo Container chứa các khe hở (Nằm trên thân cầu)
            targetsContainer = new GameObject("TargetsContainer");
            targetsContainer.transform.SetParent(content.transform, false);
            RectTransform rtTargets = targetsContainer.AddComponent<RectTransform>();
            rtTargets.anchoredPosition = new Vector2(0, 100); // Đẩy lên trên một chút

            // 2. Tạo Container chứa các ván gỗ có thể kéo (Nằm ở dưới cùng)
            planksContainer = new GameObject("PlanksContainer");
            planksContainer.transform.SetParent(content.transform, false);
            RectTransform rtPlanks = planksContainer.AddComponent<RectTransform>();
            rtPlanks.anchoredPosition = new Vector2(0, -250); // Nằm ở mép dưới

            for (int i = 0; i < totalPlanks; i++)
            {
                // A. Tạo Khe Hở (Target Slot)
                GameObject target = new GameObject($"TargetSlot_{i}");
                target.transform.SetParent(targetsContainer.transform, false);
                RectTransform rtTarget = target.AddComponent<RectTransform>();
                rtTarget.anchoredPosition = new Vector2(-250 + (i * 250), 0); // Xếp hàng ngang
                rtTarget.sizeDelta = new Vector2(60, 200); // Ván dọc (hình ||)
                Image tImg = target.AddComponent<Image>();
                tImg.color = new Color(0, 0, 0, 0.4f); // Bóng mờ đen
                
                // B. Tạo Ván Gỗ (Draggable Plank)
                GameObject plank = new GameObject($"DraggablePlank_{i}");
                plank.transform.SetParent(planksContainer.transform, false);
                RectTransform rtPlank = plank.AddComponent<RectTransform>();
                rtPlank.anchoredPosition = new Vector2(-250 + (i * 250), 0);
                rtPlank.sizeDelta = new Vector2(60, 200); // Ván dọc (hình ||)
                Image pImg = plank.AddComponent<Image>();
                pImg.color = new Color(0.6f, 0.4f, 0.2f, 1f); // Màu ván gỗ

                // Cấp quyền Kéo Thả
                DraggablePlank dragLogic = plank.AddComponent<DraggablePlank>();
                dragLogic.targetSlot = rtTarget; // Liên kết ván này với lỗ kia

                // C. Tạo 4 cây Đinh (Nails) gắn vào Ván Gỗ
                Vector2[] nailCorners = new Vector2[] {
                    new Vector2(-15, 80),  // Top Left
                    new Vector2(15, 80),   // Top Right
                    new Vector2(15, -80),  // Bottom Right
                    new Vector2(-15, -80)  // Bottom Left
                };

                for (int n = 0; n < 4; n++)
                {
                    GameObject nail = new GameObject($"Nail_{n}");
                    nail.transform.SetParent(plank.transform, false);
                    RectTransform rtNail = nail.AddComponent<RectTransform>();
                    rtNail.anchoredPosition = nailCorners[n];
                    rtNail.sizeDelta = new Vector2(20, 20); // Đinh nhỏ
                    Image nImg = nail.AddComponent<Image>();
                    nImg.color = Color.gray; // Đinh màu xám bạc
                    
                    nail.AddComponent<HammerNail>();
                    // Đinh sẽ tự động tàng hình khi Start() và chỉ hiện ra khi ván ráp đúng lỗ
                }
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("<color=green>[Drag & Hammer] Đã tự động tạo và Setup UI MỚI HOÀN TOÀN thành công!</color>");
        }
#endif
    }
}
