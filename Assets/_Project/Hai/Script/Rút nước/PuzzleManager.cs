using System;
using UnityEngine;
using DG.Tweening;
using UnityEngine.Events;
using UnityEngine.UI;

[Serializable]
public struct ButtonSymbols
{
    [Tooltip("Kéo 3 ảnh dành riêng cho nút này vào đây")]
    public Sprite[] symbols;
}

public class PuzzleManager : MonoBehaviour
{
    [Header("Sự kiện hoàn thành")]
    [SerializeField] private UnityEvent onPuzzleCompleted;
    public event Action OnPuzzleCompleted;

    [Header("Kết nối hệ thống")]
    [SerializeField] private WaterDrain[] waterSystems; 
    [SerializeField] private GameObject puzzlePanel;
    [SerializeField] private SpriteRenderer mechanismSprite;
    [SerializeField] private Color glowColor = new Color(0.4f, 0.8f, 1f, 1f);

    [Header("Cài đặt Hình ảnh, Nút & Bánh răng")]
    [SerializeField] private Image[] symbolImages; // Kéo 3 Image của 3 nút vào đây
    [SerializeField] private ButtonSymbols[] buttonSymbolsList; // Điền Size = 3. Mỗi phần tử sẽ chứa 3 ảnh riêng biệt.
    [SerializeField] private RectTransform gearTransform; // Kéo bánh răng vào đây

    [Header("Mật mã (The Lock)")]
    [SerializeField] private int[] correctCombo = {2, 0, 1};
    
    // Mảng người chơi (The Key)
    private int[] playerCombo = {0, 0, 0};

    [Header("Cài đặt Camera khi giải đúng")]
    [SerializeField] private bool enableCameraPan = true;
    [SerializeField] private Transform completionCameraTarget;
    [SerializeField] private float panTime = 0.5f;
    [SerializeField] private float holdTime = 1.5f;
    [SerializeField] private float shakeDuration = 2.0f;
    [SerializeField] private float shakeStrength = 0.2f;

    private bool _puzzleSolved = false;
    private bool _isPlayerInside = false; 
    private bool _isGearAnimating = false; // Tránh bấm nhiều lần khi bánh răng đang quay

    void Start()
    {
        if (puzzlePanel != null) puzzlePanel.SetActive(false);
        ResetInput();
    }

    void Update()
    {
        if (_isPlayerInside && Input.GetKeyDown(KeyCode.F) && !_puzzleSolved)
        {
            bool isActive = !puzzlePanel.activeSelf;
            puzzlePanel.SetActive(isActive);
            if (!isActive) ResetInput();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player")) _isPlayerInside = true;
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            _isPlayerInside = false;
            if (puzzlePanel != null && puzzlePanel.activeSelf)
            {
                puzzlePanel.SetActive(false);
                ResetInput();
            }
        }
    }

    public void OnSymbolClick(int buttonIndex)
    {
        if (_puzzleSolved) return;

        // Vòng lặp ký tự: 0 -> 1 -> 2 -> 0...
        playerCombo[buttonIndex] = (playerCombo[buttonIndex] + 1) % 3;
        
        // Cập nhật hình dáng ký tự
        UpdateSymbolUI(buttonIndex);
    }

    void UpdateSymbolUI(int index)
    {
        if (symbolImages != null && index < symbolImages.Length && 
            buttonSymbolsList != null && index < buttonSymbolsList.Length)
        {
            var symbolsForThisButton = buttonSymbolsList[index].symbols;
            
            if (symbolsForThisButton != null && playerCombo[index] < symbolsForThisButton.Length)
            {
                symbolImages[index].sprite = symbolsForThisButton[playerCombo[index]];
            }
        }
    }

    // Gắn hàm này vào sự kiện OnClick() của BÁNH RĂNG
    public void OnGearClick()
    {
        if (_puzzleSolved || _isGearAnimating) return;

        bool isCorrect = true;
        for (int i = 0; i < 3; i++)
        {
            if (playerCombo[i] != correctCombo[i])
            {
                isCorrect = false;
                break;
            }
        }

        if (isCorrect)
        {
            // Mật khẩu ĐÚNG: Xoay nửa vòng (180 độ) rồi xả nước
            _isGearAnimating = true;
            if (gearTransform != null)
            {
                gearTransform.DORotate(new Vector3(0, 0, -180f), 1f, RotateMode.LocalAxisAdd)
                    .SetEase(Ease.InOutQuad)
                    .OnComplete(() => {
                        _isGearAnimating = false;
                        UnlockWaterGate();
                    });
            }
            else
            {
                UnlockWaterGate();
            }
        }
        else
        {
            // Mật khẩu SAI: Bị kẹt, xoay một chút rồi giật về
            _isGearAnimating = true;
            if (gearTransform != null)
            {
                // Xoay đi 20 độ
                gearTransform.DORotate(new Vector3(0, 0, -20f), 0.15f, RotateMode.LocalAxisAdd)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() => {
                        // Giật về vị trí cũ
                        gearTransform.DORotate(new Vector3(0, 0, 20f), 0.3f, RotateMode.LocalAxisAdd)
                            .SetEase(Ease.OutBounce)
                            .OnComplete(() => {
                                _isGearAnimating = false;
                            });
                    });
            }
            else
            {
                _isGearAnimating = false;
            }
        }
    }

    // Gắn hàm này vào sự kiện OnClick() của nút THOÁT
    public void CloseUI()
    {
        if (_puzzleSolved || _isGearAnimating) return;
        
        if (puzzlePanel != null) puzzlePanel.SetActive(false);
        ResetInput();
    }

    void ResetInput()
    {
        for (int i = 0; i < 3; i++)
        {
            playerCombo[i] = 0;
            UpdateSymbolUI(i);
        }
    }

    void UnlockWaterGate()
    {
        _puzzleSolved = true;

        // Bảng UI tự động đóng lại
        if (puzzlePanel != null) puzzlePanel.SetActive(false);

        // Cơ quan phát sáng
        if (mechanismSprite != null)
        {
            mechanismSprite.DOColor(glowColor, 1.5f);
        }

        WaterDrain activeWater = null;
        if (waterSystems != null)
        {
            foreach (var water in waterSystems)
            {
                if (water != null)
                {
                    water.StartDrain(); // Kích hoạt rút nước
                    if (water.gameObject.activeInHierarchy)
                    {
                        activeWater = water;
                    }
                }
            }
        }

        if (enableCameraPan && CameraEffectsManager.Instance != null)
        {
            Transform finalTarget = completionCameraTarget;
            if (finalTarget == null && activeWater != null)
            {
                Transform customTarget = activeWater.transform.Find("CamTarget");
                finalTarget = customTarget != null ? customTarget : activeWater.transform;
            }

            if (finalTarget == null)
            {
                TriggerCompletionEvents();
                return;
            }

            CameraEffectsManager.Instance.PlayCinematicPan(
                finalTarget,
                panTime,
                holdTime,
                shakeDuration,
                shakeStrength,
                TriggerCompletionEvents
            );
        }
        else
        {
            TriggerCompletionEvents();
        }
    }

    private void TriggerCompletionEvents()
    {
        onPuzzleCompleted?.Invoke();
        OnPuzzleCompleted?.Invoke();
    }
}
