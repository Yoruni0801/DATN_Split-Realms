/*
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using DG.Tweening;

namespace Tien.Minigames
{
    public class ConnectDotsManager : MonoBehaviour
    {
        [Header("UI Cài đặt Đầu vào")]
        [Tooltip("Giao diện tổng của Minigame")]
        public GameObject puzzleUI;
        [Tooltip("Nơi chứa các đường thẳng nối")]
        public RectTransform linesContainer;
        [Tooltip("Ảnh sẽ hiện ra khi nối xong toàn bộ (để Alpha = 0 lúc đầu)")]
        public Image finalImageDisplay;
        [Tooltip("Nơi chứa các điểm nối")]
        public RectTransform nodesContainer;
        
        [Header("Cài đặt Nodes (Sắp xếp theo thứ tự nối)")]
        public List<ConnectNode> nodes = new List<ConnectNode>();
        
        [Header("Cài đặt Đường vẽ (Line)")]
        [Tooltip("Bề ngang của đường nối")]
        public float lineWidth = 15f;
        public Color lineColor = Color.cyan;
        public Color errorLineColor = Color.red;

        [Header("Âm thanh (Tùy chọn)")]
        public AudioSource audioSource;
        public AudioClip tickSound;
        public AudioClip winSound;
        public AudioClip failSound;

        [Header("Khác")]
        [Tooltip("Prefab hiệu ứng Particle khi thành công (Tùy chọn)")]
        public GameObject winParticlePrefab;
        [Tooltip("Cầu sẽ hiện khi xong (Dành cho NPC)")]
        public GameObject bridgeObject;

        private int currentNodeIndex = 0;
        private int startingNodeIndex = 0;
        private int previousNodeIndex = -1;
        private int nodesConnectedCount = 0;
        public bool isCompleted { get; private set; } = false;

        private Player currentPlayer;
        private List<RectTransform> drawnLines = new List<RectTransform>();

        // Cho phép nhận sự kiện Click ra ngoài nền thông qua EventTrigger
        private UnityEngine.EventSystems.EventTrigger bgEventTrigger;

        private void Awake()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject esObj = new GameObject("EventSystem_Auto");
                esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Debug.LogWarning("[ConnectDots] Không tìm thấy EventSystem trong Scene, tự động tạo mới để UI có thể nhận click!");
            }
        }

        void Start()
        {
            // Tự động đồng bộ nodeIndex để tránh lỗi user quên đổi index khi nhân bản Node
            if (nodes != null)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i] != null)
                    {
                        nodes[i].nodeIndex = i;
                        nodes[i].Setup(i, this);
                    }
                }
            }

            if (puzzleUI != null)
            {
                // Tự động tìm Nút Đóng (CloseBtn) để gán lệnh đóng (Sửa lỗi nút không hoạt động)
                Button[] buttons = puzzleUI.GetComponentsInChildren<Button>(true);
                foreach(var b in buttons)
                {
                    if (b.gameObject.name == "CloseBtn")
                    {
                        b.onClick.RemoveAllListeners();
                        b.onClick.AddListener(ClosePuzzle);
                    }
                }

                puzzleUI.SetActive(false);
                SetupBackgroundEventTrigger();
            }

            if (finalImageDisplay != null)
            {
                Color c = finalImageDisplay.color;
                c.a = 0f;
                finalImageDisplay.color = c;
            }
        }

        private void SetupBackgroundEventTrigger()
        {
            // Tự động gắn EventTrigger vào background để bắt sự kiện thả tay hoặc bấm ra ngoài
            bgEventTrigger = puzzleUI.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (bgEventTrigger == null) bgEventTrigger = puzzleUI.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            UnityEngine.EventSystems.EventTrigger.Entry entryDown = new UnityEngine.EventSystems.EventTrigger.Entry();
            entryDown.eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown;
            entryDown.callback.AddListener((data) => { OnBackgroundPointerDown(); });
            bgEventTrigger.triggers.Add(entryDown);

            UnityEngine.EventSystems.EventTrigger.Entry entryUp = new UnityEngine.EventSystems.EventTrigger.Entry();
            entryUp.eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp;
            entryUp.callback.AddListener((data) => { OnBackgroundPointerUp(); });
            bgEventTrigger.triggers.Add(entryUp);
        }

        public void OpenPuzzle()
        {
            if (isCompleted) return;

            // Thử tìm Player nếu NPC gọi (có thể tùy biến theo game của bạn)
            if (currentPlayer == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) currentPlayer = p.GetComponent<Player>();
            }

            if (currentPlayer != null) currentPlayer.SetInputActive(false);
            
            puzzleUI.SetActive(true);
            ResetMinigame();
        }

        public void ClosePuzzle()
        {
            // Tắt UI
            if (puzzleUI != null) puzzleUI.SetActive(false);
            if (currentPlayer != null) currentPlayer.SetInputActive(true);
        }

        void Update()
        {
            // Bấm Right Control để mở nhanh minigame test (không cần qua NPC)
            if (Input.GetKeyDown(KeyCode.RightControl))
            {
                Debug.Log("[ConnectDots] Phím tắt mở minigame!");
                OpenPuzzle();
            }
        }

        private void ResetMinigame()
        {
            if (nodes != null && nodes.Count > 0)
            {
                startingNodeIndex = Random.Range(0, nodes.Count);
            }
            else
            {
                startingNodeIndex = 0;
            }

            currentNodeIndex = startingNodeIndex;
            previousNodeIndex = -1;
            nodesConnectedCount = 0;
            
            // Đảm bảo xóa hết GameObject đường kẻ cũ nếu có
            if (drawnLines != null)
            {
                foreach(var line in drawnLines)
                {
                    if (line != null) Destroy(line.gameObject);
                }
                drawnLines.Clear();
            }
            else
            {
                drawnLines = new List<RectTransform>();
            }

            if (nodes == null || nodes.Count == 0) return;

            // Đặt lại trạng thái Nodes
            for (int i = 0; i < nodes.Count; i++)
            {
                if (i == startingNodeIndex) nodes[i].SetState(1); // Điểm bắt đầu nhấp nháy, chờ bấm đầu tiên
                else nodes[i].SetState(0); // Còn lại xám
            }
        }

        private RectTransform CreateLine(string name)
        {
            GameObject lineObj = new GameObject(name);
            lineObj.transform.SetParent(linesContainer != null ? linesContainer : puzzleUI.transform, false);
            
            Image img = lineObj.AddComponent<Image>();
            img.color = lineColor;
            img.raycastTarget = false; // QUAN TRỌNG: Đường thẳng không được chắn click của chuột!
            
            RectTransform rect = lineObj.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0, 0.5f); // Neo ở mép trái ở giữa
            rect.sizeDelta = new Vector2(0, lineWidth);
            
            return rect;
        }

        private void DrawLine(RectTransform lineRect, Vector3 startPosWorld, Vector3 endPosWorld)
        {
            lineRect.position = startPosWorld;
            Vector3 direction = endPosWorld - startPosWorld;
            float distance = direction.magnitude;
            
            // Xoay RectTransform hướng về điểm cuối
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            lineRect.rotation = Quaternion.Euler(0, 0, angle);

            // Gắn độ dài (Chia cho scale của canvas để hiển thị đúng độ dài trên màn hình)
            lineRect.sizeDelta = new Vector2(distance / lineRect.lossyScale.x, lineWidth);
        }

        public void OnNodePointerDown(ConnectNode node)
        {
            Debug.Log($"[ConnectDots] Người chơi vừa click vào Node: {node.nodeIndex}. Điểm đang cần click là: {currentNodeIndex}");

            if (isCompleted)
            {
                Debug.Log("[ConnectDots] Minigame đã hoàn thành, bỏ qua click!");
                return;
            }

            // Nếu người chơi bấm đúng điểm tiếp theo cần bấm
            if (node.nodeIndex == currentNodeIndex)
            {
                Debug.Log($"[ConnectDots] Bấm ĐÚNG Node {node.nodeIndex}!");
                PlaySound(tickSound);

                if (nodesConnectedCount > 0)
                {
                    // Vẽ đường thẳng từ điểm trước đó tới điểm này
                    RectTransform line = CreateLine("Line_" + nodesConnectedCount);
                    DrawLine(line, nodes[previousNodeIndex].transform.position, node.transform.position);
                    drawnLines.Add(line);
                }

                nodes[currentNodeIndex].SetState(2); // Đánh dấu đã xong
                previousNodeIndex = currentNodeIndex;
                nodesConnectedCount++;

                if (nodesConnectedCount < nodes.Count)
                {
                    // Chuyển sang điểm tiếp theo, nếu vượt quá số lượng thì quay lại 0 (wrap around)
                    currentNodeIndex = (currentNodeIndex + 1) % nodes.Count;
                    nodes[currentNodeIndex].SetState(1);
                }
                else if (nodesConnectedCount == nodes.Count)
                {
                    // Nếu đã nối hết các điểm khác biệt, điểm tiếp theo cần bấm là điểm xuất phát để khép vòng
                    currentNodeIndex = startingNodeIndex;
                    nodes[currentNodeIndex].SetState(1);
                }
                else if (nodesConnectedCount > nodes.Count)
                {
                    // Đã khép vòng thành công
                    Debug.Log("[ConnectDots] Khép vòng thành công. WIN!");
                    WinMinigame();
                }
            }
            else
            {
                // Bấm sai điểm -> Phạt
                Debug.Log($"[ConnectDots] Bấm SAI Node! Cần {currentNodeIndex} nhưng lại bấm {node.nodeIndex}");
                FailMinigame();
            }
        }

        public void OnNodePointerEnter(ConnectNode node) { }
        public void OnNodePointerUp() { }

        private void OnBackgroundPointerDown() { }
        private void OnBackgroundPointerUp() { }

        private void FailMinigame()
        {
            if (!puzzleUI.activeSelf || isCompleted) return;

            PlaySound(failSound);

            // Animation phạt: Đổi màu đường nối thành đỏ, rung nhẹ rồi reset
            Sequence failSeq = DOTween.Sequence();
            foreach (var line in drawnLines)
            {
                if (line != null)
                {
                    Image img = line.GetComponent<Image>();
                    if (img != null) failSeq.Join(img.DOColor(errorLineColor, 0.2f).SetLoops(2, LoopType.Yoyo));
                }
            }
            failSeq.OnComplete(() => ResetMinigame());
        }

        private void WinMinigame()
        {
            isCompleted = true;
            PlaySound(winSound);

            nodes[0].SetState(2);

            // Animation phát sáng toàn bộ đường vẽ
            foreach(var line in drawnLines)
            {
                if (line != null)
                {
                    Image img = line.GetComponent<Image>();
                    if (img != null) img.DOColor(Color.white, 0.5f).SetLoops(-1, LoopType.Yoyo);
                }
            }

            // Animation hiện dần bức tranh ẩn phía dưới (Fade in)
            if (finalImageDisplay != null)
            {
                finalImageDisplay.DOFade(1f, 1.5f).OnComplete(() => {
                    if (winParticlePrefab != null) Instantiate(winParticlePrefab, finalImageDisplay.transform.position, Quaternion.identity, finalImageDisplay.transform);
                    
                    // Đóng UI Minigame
                    ClosePuzzle();
                    
                    // Thêm phần thưởng (Ví dụ hiện chiếc cầu)
                    if (bridgeObject != null)
                    {
                        bridgeObject.SetActive(true);
                        Bridge bridgeComp = bridgeObject.GetComponent<Bridge>();
                        if (bridgeComp != null) bridgeComp.SetVisuals(true);
                    }
                    
                    // Lia Camera
                    NPCQuestGiver npc = Object.FindFirstObjectByType<NPCQuestGiver>();
                    if (npc != null) npc.TriggerPanCameraToBridge(3f);
                });
            }
            else
            {
                DOVirtual.DelayedCall(1.5f, () => {
                    ClosePuzzle();
                    
                    if (bridgeObject != null)
                    {
                        bridgeObject.SetActive(true);
                        Bridge bridgeComp = bridgeObject.GetComponent<Bridge>();
                        if (bridgeComp != null) bridgeComp.SetVisuals(true);
                    }
                    
                    NPCQuestGiver npc = Object.FindFirstObjectByType<NPCQuestGiver>();
                    if (npc != null) npc.TriggerPanCameraToBridge(3f);
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
        [ContextMenu("Auto Setup UI Nối Điểm")]
        public void AutoSetupUI()
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("MinigameCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            if (puzzleUI == null)
            {
                puzzleUI = new GameObject("ConnectDotsUI");
                puzzleUI.transform.SetParent(canvas.transform, false);
                RectTransform uiRect = puzzleUI.AddComponent<RectTransform>();
                uiRect.anchorMin = Vector2.zero;
                uiRect.anchorMax = Vector2.one;
                uiRect.sizeDelta = Vector2.zero;
                
                Image bg = puzzleUI.AddComponent<Image>();
                bg.color = new Color(0, 0, 0, 0.8f);

                // Thêm nút Đóng
                GameObject closeBtnObj = new GameObject("CloseBtn");
                closeBtnObj.transform.SetParent(puzzleUI.transform, false);
                RectTransform btnRect = closeBtnObj.AddComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(1, 1);
                btnRect.anchorMax = new Vector2(1, 1);
                btnRect.pivot = new Vector2(1, 1);
                btnRect.anchoredPosition = new Vector2(-50, -50);
                btnRect.sizeDelta = new Vector2(50, 50);
                Image btnImage = closeBtnObj.AddComponent<Image>();
                btnImage.color = Color.red;
                Button closeBtn = closeBtnObj.AddComponent<Button>();
                
                GameObject closeTextObj = new GameObject("Text");
                closeTextObj.transform.SetParent(closeBtnObj.transform, false);
                Text closeText = closeTextObj.AddComponent<Text>();
                closeText.text = "X";
                closeText.alignment = TextAnchor.MiddleCenter;
                closeText.color = Color.white;
                closeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                closeTextObj.GetComponent<RectTransform>().anchorMin = Vector2.zero;
                closeTextObj.GetComponent<RectTransform>().anchorMax = Vector2.one;
                closeTextObj.GetComponent<RectTransform>().sizeDelta = Vector2.zero;

                UnityEditor.Events.UnityEventTools.AddPersistentListener(closeBtn.onClick, ClosePuzzle);
            }

            if (finalImageDisplay == null)
            {
                GameObject finalImgObj = new GameObject("FinalImage");
                finalImgObj.transform.SetParent(puzzleUI.transform, false);
                RectTransform fRect = finalImgObj.AddComponent<RectTransform>();
                fRect.anchorMin = new Vector2(0.5f, 0.5f);
                fRect.anchorMax = new Vector2(0.5f, 0.5f);
                fRect.sizeDelta = new Vector2(400, 400);
                finalImageDisplay = finalImgObj.AddComponent<Image>();
                finalImageDisplay.color = new Color(1,1,1,0); 
            }

            if (linesContainer == null)
            {
                GameObject lineCont = new GameObject("LinesContainer");
                lineCont.transform.SetParent(puzzleUI.transform, false);
                linesContainer = lineCont.AddComponent<RectTransform>();
                linesContainer.anchorMin = Vector2.zero;
                linesContainer.anchorMax = Vector2.one;
                linesContainer.sizeDelta = Vector2.zero;
            }

            if (nodesContainer == null)
            {
                GameObject nodeCont = new GameObject("NodesContainer");
                nodeCont.transform.SetParent(puzzleUI.transform, false);
                nodesContainer = nodeCont.AddComponent<RectTransform>();
                nodesContainer.anchorMin = Vector2.zero;
                nodesContainer.anchorMax = Vector2.one;
                nodesContainer.sizeDelta = Vector2.zero;
            }

            // Dọn Node cũ
            foreach (var node in nodes) {
                if (node != null) DestroyImmediate(node.gameObject);
            }
            nodes.Clear();

            // Vẽ Ngôi sao 5 cánh (Vòng ngoài)
            float radius = 200f;
            for (int i = 0; i < 5; i++)
            {
                GameObject nodeObj = new GameObject($"Node_{i}");
                nodeObj.transform.SetParent(nodesContainer, false);
                RectTransform nodeRect = nodeObj.AddComponent<RectTransform>();
                nodeRect.anchorMin = new Vector2(0.5f, 0.5f);
                nodeRect.anchorMax = new Vector2(0.5f, 0.5f);
                
                // Trừ 90 độ để điểm đầu tiên nằm ở vị trí 12h
                float angle = (i * Mathf.PI * 2f / 5f) - (Mathf.PI / 2f);
                nodeRect.anchoredPosition = new Vector2(Mathf.Cos(angle) * radius, -Mathf.Sin(angle) * radius);
                nodeRect.sizeDelta = new Vector2(40, 40);

                Image img = nodeObj.AddComponent<Image>();
                ConnectNode cNode = nodeObj.AddComponent<ConnectNode>();
                cNode.Setup(i, this);
                nodes.Add(cNode);
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[ConnectDots] Đã tự động tạo ra một giao diện mẫu 5 điểm. Bạn có thể kéo thả để di chuyển các điểm này tạo hình!");
        }
#endif
    }
}
*/
