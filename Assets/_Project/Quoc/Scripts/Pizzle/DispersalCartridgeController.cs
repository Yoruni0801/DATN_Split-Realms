using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Tien.AdvancedLoading.Core;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class DispersalCartridgeController : MonoBehaviour
{
    public const string DefaultCompletionKey = "Map5FireCave.MagmaDispersal.Solved";

    private static readonly Vector2[] SlotPositions =
    {
        new Vector2(-305f, 30f),
        new Vector2(0f, 30f),
        new Vector2(305f, 30f),
        new Vector2(0f, 300f)
    };

    private static readonly string[] VesselNames =
    {
        "BÌNH LÕI",
        "BÌNH RÈN",
        "BÌNH TRO"
    };

    private static readonly Color Charcoal = Hex("0E0400");
    private static readonly Color Copper = Hex("C46233");
    private static readonly Color AncientGold = Hex("A59442");
    private static readonly Color TargetCyan = Hex("8FE8D7");
    private static readonly Color Bone = Hex("F0D6B2");

    [Header("Fire Cave Presentation")]
    [SerializeField] private Sprite fireCaveBackground;
    [SerializeField] private Font displayFont;

    [Header("Audio")]
    [SerializeField] private AudioClip ambientClip;
    [SerializeField] private AudioClip transferClip;
    [SerializeField] private AudioClip valveClip;
    [SerializeField] private AudioClip invalidClip;
    [SerializeField] private AudioClip chargedClip;
    [SerializeField] private AudioClip successClip;

    [Header("Scene Flow")]
    [SerializeField] private string returnSceneName = "Map5FireCave";
    [SerializeField] private bool persistCompletion = true;
    [SerializeField] private string completionKey = DefaultCompletionKey;
    [SerializeField] private UnityEvent onPuzzleSolved = new UnityEvent();

    [Header("UI Elements (Kéo thả từ Inspector)")]
    [SerializeField] private MagmaVesselGraphic[] vesselViews = new MagmaVesselGraphic[MagmaDispersalPuzzle.VesselCount + 1];
    [SerializeField] private RectTransform[] vesselRects = new RectTransform[MagmaDispersalPuzzle.VesselCount + 1];
    [SerializeField] private Button[] actionButtons = new Button[3];
    [SerializeField] private MagmaTransferGraphic transferGraphic;
    [SerializeField] private Text moveText;
    [SerializeField] private Text goalAmountText;
    [SerializeField] private Text goalLocationText;
    [SerializeField] private Text statusText;
    [SerializeField] private Text hintText;
    [SerializeField] private GameObject hintPanel;
    [SerializeField] private CanvasGroup successOverlay;
    [SerializeField] private Text successMoveText;
    [SerializeField] private Button hintButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button successReturnButton;
    [SerializeField] private Button successReplayButton;

    private readonly Coroutine[] buttonPulseCoroutines = new Coroutine[3];
    private MagmaDispersalPuzzle puzzle;
    private AudioSource effectsSource;
    private AudioSource ambientSource;
    private bool interfaceBuilt;
    private bool inputLocked;
    private bool hintVisible;
    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible;
    private bool cursorStateCaptured;

    public bool IsSolved => puzzle != null && puzzle.IsSolved;
    public int MoveCount => puzzle != null ? puzzle.MoveCount : 0;

    private void Awake()
    {
        Time.timeScale = 1f;
        CaptureAndUnlockCursor();
        effectsSource = GetComponent<AudioSource>();
        effectsSource.playOnAwake = false;
        effectsSource.loop = false;
        effectsSource.spatialBlend = 0f;

        puzzle = new MagmaDispersalPuzzle();
        
        interfaceBuilt = HasBakedInterface();
        if (!interfaceBuilt)
        {
            Debug.LogError("[DispersalCartridgeController] UI chưa được bake hoặc thiếu tham chiếu. Trong Inspector, dùng 'Bake UI vào Scene' rồi lưu scene.", this);
            return;
        }

        // Tự động gắn sự kiện OnClick cho các nút
        if (actionButtons != null && actionButtons.Length >= 3)
        {
            if (actionButtons[0] != null) actionButtons[0].onClick.AddListener(SwapLeft);
            if (actionButtons[1] != null) actionButtons[1].onClick.AddListener(TransferMagma);
            if (actionButtons[2] != null) actionButtons[2].onClick.AddListener(SwapRight);
        }

        if (hintButton != null) hintButton.onClick.AddListener(ToggleHint);
        if (resetButton != null) resetButton.onClick.AddListener(ResetPuzzle);
        if (exitButton != null) exitButton.onClick.AddListener(ReturnToFireCave);
        
        if (successReturnButton != null) successReturnButton.onClick.AddListener(ReturnToFireCave);
        if (successReplayButton != null) successReplayButton.onClick.AddListener(ResetPuzzle);

        SetupAmbientAudio();
        ResetPuzzle(false);
    }

    private void AutoAssignUI_Internal()
    {
        EnsureUiArraySizes();
        Transform[] allChildren = GetComponentsInChildren<Transform>(true);

        T FindChild<T>(string n) where T : Component
        {
            foreach (Transform t in allChildren)
            {
                if (t.name == n) return t.GetComponent<T>();
            }
            return null;
        }

        GameObject FindChildGO(string n)
        {
            foreach (Transform t in allChildren)
            {
                if (t.name == n) return t.gameObject;
            }
            return null;
        }

        vesselViews[0] = FindChild<MagmaVesselGraphic>("BÌNH LÕI");
        vesselViews[1] = FindChild<MagmaVesselGraphic>("BÌNH RÈN");
        vesselViews[2] = FindChild<MagmaVesselGraphic>("BÌNH TRO");
        vesselViews[3] = FindChild<MagmaVesselGraphic>("BÌNH CHỨA CHUNG");

        vesselRects[0] = FindChild<RectTransform>("BÌNH LÕI");
        vesselRects[1] = FindChild<RectTransform>("BÌNH RÈN");
        vesselRects[2] = FindChild<RectTransform>("BÌNH TRO");
        vesselRects[3] = FindChild<RectTransform>("BÌNH CHỨA CHUNG");

        if (actionButtons == null || actionButtons.Length < 3) actionButtons = new Button[3];
        actionButtons[0] = FindChild<Button>("Valve Left") ?? FindChild<Button>("Swap Left");
        actionButtons[1] = FindChild<Button>("Valve Middle") ?? FindChild<Button>("Transfer Magma");
        actionButtons[2] = FindChild<Button>("Valve Right") ?? FindChild<Button>("Swap Right");

        transferGraphic = FindChild<MagmaTransferGraphic>("Magma Transfer Flow");
        moveText = FindChild<Text>("Move Counter");
        goalAmountText = FindChild<Text>("Goal Amount");
        goalLocationText = FindChild<Text>("Goal Location");
        statusText = FindChild<Text>("Status");
        hintText = FindChild<Text>("Hint Text");
        
        hintPanel = FindChildGO("Hint Panel");
        successOverlay = FindChild<CanvasGroup>("Success Overlay");
        successMoveText = FindChild<Text>("Success Moves");

        hintButton = FindChild<Button>("Hint");
        resetButton = FindChild<Button>("Reset");
        exitButton = FindChild<Button>("Exit");
        
        successReturnButton = FindChild<Button>("Return Button");
        successReplayButton = FindChild<Button>("Replay Button");
    }

    private void EnsureUiArraySizes()
    {
        if (vesselViews == null || vesselViews.Length != MagmaDispersalPuzzle.VesselCount + 1)
        {
            System.Array.Resize(ref vesselViews, MagmaDispersalPuzzle.VesselCount + 1);
        }

        if (vesselRects == null || vesselRects.Length != MagmaDispersalPuzzle.VesselCount + 1)
        {
            System.Array.Resize(ref vesselRects, MagmaDispersalPuzzle.VesselCount + 1);
        }

        if (actionButtons == null || actionButtons.Length != 3)
        {
            System.Array.Resize(ref actionButtons, 3);
        }
    }

    private bool HasBakedInterface()
    {
        if (vesselViews == null || vesselViews.Length < 4 || vesselRects == null || vesselRects.Length < 4 || actionButtons == null || actionButtons.Length < 3)
        {
            return false;
        }

        for (int index = 0; index < 4; index++)
        {
            if (vesselViews[index] == null || vesselRects[index] == null) return false;
        }

        return actionButtons[0] != null && actionButtons[1] != null && actionButtons[2] != null
            && transferGraphic != null && moveText != null && goalAmountText != null && goalLocationText != null
            && statusText != null && hintText != null && hintPanel != null && successOverlay != null;
    }

#if UNITY_EDITOR
    [ContextMenu("1. Tạo UI gốc để tùy chỉnh (Generate UI)")]
    private void GenerateUIInEditMode()
    {
        // Xóa UI cũ nếu có để tránh trùng lặp
        Transform oldUI = transform.Find("Fire Cave Magma Dispersal UI");
        if (oldUI != null)
        {
            DestroyImmediate(oldUI.gameObject);
        }

        puzzle = new MagmaDispersalPuzzle();
        EnsureUiArraySizes();
        interfaceBuilt = false;
        BuildInterface();
        RefreshVesselModels(true);
        AutoAssignUI_Internal();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("Đã tạo UI trên Scene thành công! Bạn có thể bắt đầu tùy chỉnh.");
    }

    [ContextMenu("2. Gán lại UI (Auto Assign UI)")]
    private void AutoAssignUI()
    {
        AutoAssignUI_Internal();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("Đã tự động tìm và gán các UI Elements thành công!");
    }
#endif

    private void OnDestroy()
    {
        RestoreCursor();
    }

    private void Update()
    {
        if (!interfaceBuilt)
        {
            return;
        }

        if (WasEscapePressed())
        {
            ReturnToFireCave();
            return;
        }

        if (WasHintPressed())
        {
            ToggleHint();
        }

        if (WasResetPressed())
        {
            ResetPuzzle();
        }

        if (inputLocked || puzzle.IsSolved)
        {
            return;
        }

        if (WasLeftPressed())
        {
            SwapLeft();
        }
        else if (WasTransferPressed())
        {
            TransferMagma();
        }
        else if (WasRightPressed())
        {
            SwapRight();
        }
    }

    public void SwapLeft()
    {
        BeginAction(MagmaDispersalPuzzle.PuzzleAction.SwapLeft);
    }

    public void TransferMagma()
    {
        BeginAction(MagmaDispersalPuzzle.PuzzleAction.Transfer);
    }

    public void SwapRight()
    {
        BeginAction(MagmaDispersalPuzzle.PuzzleAction.SwapRight);
    }

    public void ToggleHint()
    {
        hintVisible = !hintVisible;
        hintPanel.SetActive(hintVisible);
        if (!hintVisible)
        {
            return;
        }

        MagmaDispersalPuzzle.PuzzleAction? hint = puzzle.FindHint();
        string nextStep = hint.HasValue
            ? GetActionLabel(hint.Value)
            : "HỎA MẠCH ĐÃ ỔN ĐỊNH";
        hintText.text =
            "LUẬT HỎA ẤN\n\n"
            + "• Ba bình Q/E/R được nối với BÌNH CHỨA CHUNG.\n"
            + "• Bình chung trống: van hút nước vào; có nước: van đổ về bình được chọn.\n"
            + "• Đưa BÌNH LÕI về đúng 10 nhịp.\n\n"
            + "GỢI Ý HIỆN TẠI\n"
            + nextStep;
        PlaySound(valveClip, 0.35f);
    }

    public void ResetPuzzle()
    {
        ResetPuzzle(true);
    }

    public void ReturnToFireCave()
    {
        if (string.IsNullOrWhiteSpace(returnSceneName))
        {
            Debug.LogWarning("Return scene is not configured for the magma dispersal puzzle.", this);
            return;
        }

        Time.timeScale = 1f;
        RestoreCursor();
        SceneLoader.Load(returnSceneName);
    }

    private void BeginAction(MagmaDispersalPuzzle.PuzzleAction action)
    {
        if (inputLocked || puzzle.IsSolved)
        {
            return;
        }

        float[] oldAmounts = CaptureDisplayedAmounts();
        MagmaDispersalPuzzle.MoveResult result = puzzle.Apply(action);

        if (!result.Changed)
        {
            SetStatus("VAN BỊ KHÓA — BÌNH NGUỒN CẠN HOẶC BÌNH NHẬN ĐÃ ĐẦY", false);
            PlaySound(invalidClip, 0.62f);
            PulseButton(action);
            return;
        }

        RefreshHud();
        RefreshVesselModels(false);
        RefreshHintIfVisible();

        PlaySound(valveClip, 0.5f);
        StartCoroutine(AnimateTransfer(oldAmounts, result));
    }

    private IEnumerator AnimateTransfer(float[] oldAmounts, MagmaDispersalPuzzle.MoveResult result)
    {
        SetInputLocked(true);
        transferGraphic.gameObject.SetActive(true);
        transferGraphic.SetFlow(result.SourceVessel, result.DestinationVessel);
        transferGraphic.SetProgress(0f);
        PlaySound(transferClip, 0.74f);

        const float duration = 0.72f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            transferGraphic.SetProgress(eased);

            for (int vessel = 0; vessel < vesselViews.Length; vessel++)
            {
                float target = GetDisplayedAmount(vessel);
                vesselViews[vessel].SetDisplayedAmount(Mathf.Lerp(oldAmounts[vessel], target, eased));
            }

            yield return null;
        }

        transferGraphic.SetProgress(1f);
        RefreshVesselModels(true);
        yield return new WaitForSecondsRealtime(0.08f);
        transferGraphic.SetProgress(0f);

        SetStatus(
            result.DrawsIntoShared
                ? "ĐÃ MỞ VAN: NƯỚC ĐI VÀO BÌNH CHỨA CHUNG"
                : "ĐÃ MỞ VAN: NƯỚC CHẢY TỪ BÌNH CHỨA CHUNG VỀ BÌNH ĐÃ CHỌN",
            true);
        FinishAction(result.Solved);
    }

    private void FinishAction(bool solved)
    {
        if (solved)
        {
            StartCoroutine(CompletePuzzle());
            return;
        }

        SetInputLocked(false);
    }

    private IEnumerator CompletePuzzle()
    {
        SetInputLocked(true);
        for (int i = 0; i < vesselViews.Length; i++)
        {
            vesselViews[i].SetSolvedGlow(i == MagmaDispersalPuzzle.CoreVessel);
        }

        SetStatus("HỎA MẠCH ỔN ĐỊNH — LÕI DẪN NHAM ĐÃ ĐƯỢC RÈN", true);
        PlaySound(chargedClip, 0.82f);

        if (persistCompletion && !string.IsNullOrWhiteSpace(completionKey))
        {
            PlayerPrefs.SetInt(completionKey, 1);
            PlayerPrefs.Save();
        }

        onPuzzleSolved?.Invoke();
        yield return new WaitForSecondsRealtime(0.48f);
        PlaySound(successClip, 0.86f);

        successMoveText.text = puzzle.MoveCount <= 8
            ? "HỎA ẤN HOÀN HẢO  •  " + puzzle.MoveCount + " THAO TÁC"
            : "LÕI ĐÃ ỔN ĐỊNH  •  " + puzzle.MoveCount + " THAO TÁC";
        successOverlay.gameObject.SetActive(true);
        successOverlay.alpha = 0f;
        successOverlay.interactable = false;
        successOverlay.blocksRaycasts = true;

        float elapsed = 0f;
        const float duration = 0.42f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            successOverlay.alpha = t * t * (3f - 2f * t);
            yield return null;
        }

        successOverlay.alpha = 1f;
        successOverlay.interactable = true;
    }

    private void ResetPuzzle(bool playSound)
    {
        StopAllCoroutines();
        inputLocked = false;
        puzzle.Reset();
        if (successOverlay != null)
        {
            successOverlay.alpha = 0f;
            successOverlay.interactable = false;
            successOverlay.blocksRaycasts = false;
            successOverlay.gameObject.SetActive(false);
        }

        if (transferGraphic != null)
        {
            transferGraphic.gameObject.SetActive(true);
            transferGraphic.SetProgress(0f);
        }

        for (int i = 0; i < vesselViews.Length; i++)
        {
            vesselViews[i].SetSolvedGlow(false);
        }

        for (int i = 0; i < actionButtons.Length; i++)
        {
            buttonPulseCoroutines[i] = null;
            if (actionButtons[i] != null)
            {
                actionButtons[i].transform.localScale = Vector3.one;
            }
        }

        SnapVesselsToSlots();
        RefreshVesselModels(true);
        RefreshHud();
        SetInputLocked(false);
        SetStatus("HỆ THỐNG SẴN SÀNG — VẶN VAN ĐỂ ĐƯA BÌNH LÕI VỀ 10 NHỊP", true);
        RefreshHintIfVisible();

        if (playSound)
        {
            PlaySound(valveClip, 0.42f);
        }
    }

    private void BuildInterface()
    {
        if (interfaceBuilt)
        {
            return;
        }

        interfaceBuilt = true;
        if (displayFont == null)
        {
            displayFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        CreateEventSystemIfNeeded();

        RectTransform canvasRect = CreateCanvas();
        CreateBackground(canvasRect);
        CreateHeader(canvasRect);
        CreateObjectivePanel(canvasRect);
        CreateMachine(canvasRect);
        CreateStatusBar(canvasRect);
        CreateUtilityButtons(canvasRect);
        CreateHintPanel(canvasRect);
        CreateSuccessOverlay(canvasRect);
    }

    private RectTransform CreateCanvas()
    {
        GameObject canvasObject = NewUiObject("Fire Cave Magma Dispersal UI", transform);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        RectTransform rect = canvasObject.GetComponent<RectTransform>();
        Stretch(rect);
        return rect;
    }

    private void CreateBackground(RectTransform canvasRect)
    {
        GameObject imageObject = NewUiObject("Map5 Fire Cave Backdrop", canvasRect);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        Stretch(rect);
        Image image = imageObject.AddComponent<Image>();
        image.sprite = fireCaveBackground;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = fireCaveBackground != null
            ? new Color(0.42f, 0.23f, 0.16f, 1f)
            : Charcoal;
        image.raycastTarget = false;

        GameObject decorObject = NewUiObject("Animated Ember Atmosphere", canvasRect);
        RectTransform decorRect = decorObject.GetComponent<RectTransform>();
        Stretch(decorRect);
        FireCaveBackdropGraphic decor = decorObject.AddComponent<FireCaveBackdropGraphic>();
        decor.raycastTarget = false;

        Image veil = CreateImage(
            canvasRect,
            "Cinematic Veil",
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero,
            new Color(0.025f, 0.006f, 0.002f, 0.34f));
        veil.raycastTarget = false;
    }

    private void CreateHeader(RectTransform canvasRect)
    {
        RectTransform panel = CreatePanel(
            canvasRect,
            "Title Plate",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(70f, -58f),
            new Vector2(760f, 116f),
            new Color(0.035f, 0.009f, 0.004f, 0.93f),
            Copper);

        Text chapter = CreateText(
            panel,
            "Chapter",
            "HANG LỬA  •  HỎA ẤN CỔ",
            17,
            TextAnchor.MiddleLeft,
            AncientGold,
            new Vector2(24f, -16f),
            new Vector2(690f, 24f));
        chapter.fontStyle = FontStyle.Bold;

        Text title = CreateText(
            panel,
            "Title",
            "HỎA ẤN ĐIỀU NHAM",
            42,
            TextAnchor.MiddleLeft,
            Bone,
            new Vector2(22f, 18f),
            new Vector2(710f, 62f));
        title.fontStyle = FontStyle.Bold;

        moveText = CreateText(
            canvasRect,
            "Move Counter",
            "THAO TÁC  00",
            18,
            TextAnchor.MiddleLeft,
            new Color(1f, 0.66f, 0.35f, 1f),
            new Vector2(78f, -188f),
            new Vector2(270f, 36f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f));
        moveText.fontStyle = FontStyle.Bold;
    }

    private void CreateObjectivePanel(RectTransform canvasRect)
    {
        RectTransform panel = CreatePanel(
            canvasRect,
            "Objective Plate",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-70f, -58f),
            new Vector2(430f, 134f),
            new Color(0.035f, 0.009f, 0.004f, 0.93f),
            AncientGold);

        Text label = CreateText(
            panel,
            "Goal Label",
            "MỤC TIÊU  /  BÌNH LÕI",
            15,
            TextAnchor.UpperLeft,
            AncientGold,
            new Vector2(22f, -14f),
            new Vector2(275f, 26f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f));
        label.fontStyle = FontStyle.Bold;

        goalAmountText = CreateText(
            panel,
            "Goal Amount",
            "13  →  10 NHỊP",
            31,
            TextAnchor.MiddleLeft,
            TargetCyan,
            new Vector2(20f, -57f),
            new Vector2(280f, 46f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f));
        goalAmountText.fontStyle = FontStyle.Bold;

        goalLocationText = CreateText(
            panel,
            "Goal Location",
            "VỊ TRÍ  •  KHOANG NHẬN",
            13,
            TextAnchor.MiddleLeft,
            Bone,
            new Vector2(22f, -105f),
            new Vector2(360f, 24f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f));
    }

    private void CreateMachine(RectTransform canvasRect)
    {
        GameObject machineObject = NewUiObject("Obsidian Magma Regulator", canvasRect);
        RectTransform machineRect = machineObject.GetComponent<RectTransform>();
        SetRect(machineRect, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(1260f, 650f));
        FireTerminalGraphic terminal = machineObject.AddComponent<FireTerminalGraphic>();
        terminal.raycastTarget = false;

        for (int slot = 0; slot < SlotPositions.Length; slot++)
        {
            Text station = CreateText(
                machineRect,
                "Station " + slot,
                "BÌNH CỐ ĐỊNH " + (slot + 1),
                16,
                TextAnchor.MiddleCenter,
                slot == 0 ? TargetCyan : AncientGold,
                new Vector2(SlotPositions[slot].x, 285f),
                new Vector2(245f, 30f));
            station.fontStyle = FontStyle.Bold;
        }

        Text flowLabel = CreateText(
            machineRect,
            "Flow Direction",
            "◀  ĐƯỜNG ỐNG NƯỚC  ◀",
            14,
            TextAnchor.MiddleCenter,
            new Color(1f, 0.48f, 0.16f, 0.92f),
            new Vector2(-150f, 207f),
            new Vector2(270f, 25f));
        flowLabel.fontStyle = FontStyle.Bold;

        GameObject flowObject = NewUiObject("Magma Transfer Flow", machineRect);
        RectTransform flowRect = flowObject.GetComponent<RectTransform>();
        Stretch(flowRect);
        transferGraphic = flowObject.AddComponent<MagmaTransferGraphic>();
        transferGraphic.raycastTarget = false;
        flowObject.SetActive(true);

        CreateVessels(machineRect);
        CreateActionButtons(machineRect);
    }

    private void CreateVessels(RectTransform machineRect)
    {
        Vector2[] sizes =
        {
            new Vector2(196f, 382f),
            new Vector2(178f, 342f),
            new Vector2(160f, 302f)
        };

        for (int vessel = 0; vessel <= MagmaDispersalPuzzle.SharedReservoir; vessel++)
        {
            bool isSharedReservoir = vessel == MagmaDispersalPuzzle.SharedReservoir;
            string vesselName = isSharedReservoir ? "BÌNH CHỨA CHUNG" : VesselNames[vessel];
            Vector2 vesselSize = isSharedReservoir ? new Vector2(170f, 190f) : sizes[vessel];
            GameObject vesselObject = NewUiObject(vesselName, machineRect);
            RectTransform rect = vesselObject.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(0.5f, 0.5f), SlotPositions[vessel], vesselSize);
            MagmaVesselGraphic graphic = vesselObject.AddComponent<MagmaVesselGraphic>();
            graphic.raycastTarget = false;
            graphic.SetVesselStyle(vessel);

            Text name = CreateText(
                rect,
                "Vessel Name",
                vesselName,
                15,
                TextAnchor.MiddleCenter,
                vessel == MagmaDispersalPuzzle.CoreVessel ? TargetCyan : Bone,
                new Vector2(0f, -vesselSize.y * 0.5f - 23f),
                new Vector2(205f, 26f));
            name.fontStyle = FontStyle.Bold;

            Text capacity = CreateText(
                rect,
                "Capacity",
                GetDisplayedCapacity(vessel) + " NHỊP",
                12,
                TextAnchor.MiddleCenter,
                new Color(0.92f, 0.57f, 0.32f, 1f),
                new Vector2(0f, -vesselSize.y * 0.5f - 43f),
                new Vector2(160f, 22f));

            vesselViews[vessel] = graphic;
            vesselRects[vessel] = rect;
        }
    }

    private void CreateActionButtons(RectTransform machineRect)
    {
        actionButtons[0] = CreateRuneButton(
            machineRect,
            "Valve Left",
            new Vector2(-305f, -250f),
            "Q",
            "VẶN VAN TRÁI",
            RuneButtonGraphic.RuneKind.Left,
            SwapLeft);

        actionButtons[1] = CreateRuneButton(
            machineRect,
            "Valve Middle",
            new Vector2(0f, -250f),
            "E",
            "VẶN VAN GIỮA",
            RuneButtonGraphic.RuneKind.Valve,
            TransferMagma);

        actionButtons[2] = CreateRuneButton(
            machineRect,
            "Valve Right",
            new Vector2(305f, -250f),
            "R",
            "VẶN VAN PHẢI",
            RuneButtonGraphic.RuneKind.Right,
            SwapRight);
    }

    private void CreateStatusBar(RectTransform canvasRect)
    {
        RectTransform bar = CreatePanel(
            canvasRect,
            "Status Bar",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 58f),
            new Vector2(1240f, 58f),
            new Color(0.035f, 0.009f, 0.004f, 0.93f),
            Copper);
        statusText = CreateText(
            bar,
            "Status",
            string.Empty,
            16,
            TextAnchor.MiddleCenter,
            Bone,
            Vector2.zero,
            new Vector2(1170f, 48f));
        statusText.fontStyle = FontStyle.Bold;
    }

    private void CreateUtilityButtons(RectTransform canvasRect)
    {
        CreateSmallButton(
            canvasRect,
            "Hint",
            new Vector2(70f, 55f),
            "H  GỢI Ý",
            ToggleHint,
            new Vector2(0f, 0f));

        CreateSmallButton(
            canvasRect,
            "Reset",
            new Vector2(-70f, 55f),
            "↻  LÀM LẠI",
            ResetPuzzle,
            new Vector2(1f, 0f));

        CreateSmallButton(
            canvasRect,
            "Exit",
            new Vector2(-70f, -188f),
            "ESC  TRỞ VỀ",
            ReturnToFireCave,
            new Vector2(1f, 1f));
    }

    private void CreateHintPanel(RectTransform canvasRect)
    {
        RectTransform panel = CreatePanel(
            canvasRect,
            "Hint Panel",
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-70f, 0f),
            new Vector2(480f, 310f),
            new Color(0.025f, 0.006f, 0.002f, 0.97f),
            AncientGold);
        hintPanel = panel.gameObject;
        hintText = CreateText(
            panel,
            "Hint Text",
            string.Empty,
            17,
            TextAnchor.UpperLeft,
            Bone,
            new Vector2(24f, -22f),
            new Vector2(430f, 260f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f));
        hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
        hintText.verticalOverflow = VerticalWrapMode.Overflow;
        hintPanel.SetActive(false);
    }

    private void CreateSuccessOverlay(RectTransform canvasRect)
    {
        GameObject overlayObject = NewUiObject("Success Overlay", canvasRect);
        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        Stretch(overlayRect);
        Image shade = overlayObject.AddComponent<Image>();
        shade.color = new Color(0.02f, 0.003f, 0.001f, 0.88f);
        shade.raycastTarget = true;
        successOverlay = overlayObject.AddComponent<CanvasGroup>();

        RectTransform card = CreatePanel(
            overlayRect,
            "Forged Core Card",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(760f, 420f),
            new Color(0.055f, 0.014f, 0.006f, 0.98f),
            TargetCyan);

        GameObject sigilObject = NewUiObject("Core Sigil", card);
        RectTransform sigilRect = sigilObject.GetComponent<RectTransform>();
        SetRect(sigilRect, new Vector2(0.5f, 0.5f), new Vector2(0f, 102f), new Vector2(112f, 112f));
        SuccessSigilGraphic sigil = sigilObject.AddComponent<SuccessSigilGraphic>();
        sigil.raycastTarget = false;

        Text title = CreateText(
            card,
            "Success Title",
            "LÕI DẪN NHAM",
            42,
            TextAnchor.MiddleCenter,
            TargetCyan,
            new Vector2(0f, 18f),
            new Vector2(650f, 62f));
        title.fontStyle = FontStyle.Bold;

        successMoveText = CreateText(
            card,
            "Success Moves",
            string.Empty,
            18,
            TextAnchor.MiddleCenter,
            AncientGold,
            new Vector2(0f, -42f),
            new Vector2(620f, 32f));
        successMoveText.fontStyle = FontStyle.Bold;

        Text description = CreateText(
            card,
            "Success Description",
            "Áp suất hỏa mạch đã cân bằng. Cơ cấu hang lửa có thể nhận năng lượng từ lõi mới.",
            16,
            TextAnchor.MiddleCenter,
            Bone,
            new Vector2(0f, -88f),
            new Vector2(610f, 54f));
        description.horizontalOverflow = HorizontalWrapMode.Wrap;

        CreatePanelButton(card, "Return Button", new Vector2(-160f, -158f), "TRỞ LẠI HANG LỬA", ReturnToFireCave);
        CreatePanelButton(card, "Replay Button", new Vector2(160f, -158f), "THỬ LẠI", ResetPuzzle);

        successOverlay.alpha = 0f;
        successOverlay.interactable = false;
        successOverlay.blocksRaycasts = false;
        overlayObject.SetActive(false);
    }

    private Button CreateRuneButton(
        RectTransform parent,
        string name,
        Vector2 position,
        string key,
        string label,
        RuneButtonGraphic.RuneKind rune,
        UnityAction action)
    {
        GameObject buttonObject = NewUiObject(name, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        SetRect(rect, new Vector2(0.5f, 0.5f), position, new Vector2(244f, 84f));
        RuneButtonGraphic graphic = buttonObject.AddComponent<RuneButtonGraphic>();
        graphic.SetRune(rune);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = graphic;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = FireButtonColors();
        button.onClick.AddListener(action);

        Text keyText = CreateText(
            rect,
            "Key",
            key,
            18,
            TextAnchor.MiddleCenter,
            Charcoal,
            new Vector2(-82f, 0f),
            new Vector2(42f, 42f));
        keyText.fontStyle = FontStyle.Bold;

        Text labelText = CreateText(
            rect,
            "Label",
            label,
            17,
            TextAnchor.MiddleCenter,
            Bone,
            new Vector2(24f, 0f),
            new Vector2(150f, 44f));
        labelText.fontStyle = FontStyle.Bold;
        return button;
    }

    private void CreateSmallButton(
        RectTransform parent,
        string name,
        Vector2 position,
        string label,
        UnityAction action,
        Vector2 anchor)
    {
        GameObject buttonObject = NewUiObject(name, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        SetRect(rect, anchor, position, new Vector2(190f, 48f));
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.11f, 0.045f, 0.025f, 0.96f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = FireButtonColors();
        button.onClick.AddListener(action);
        Text text = CreateText(rect, "Label", label, 14, TextAnchor.MiddleCenter, Bone, Vector2.zero, new Vector2(174f, 40f));
        text.fontStyle = FontStyle.Bold;
    }

    private void CreatePanelButton(RectTransform parent, string name, Vector2 position, string label, UnityAction action)
    {
        GameObject buttonObject = NewUiObject(name, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        SetRect(rect, new Vector2(0.5f, 0.5f), position, new Vector2(280f, 58f));
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.18f, 0.07f, 0.025f, 1f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = FireButtonColors();
        button.onClick.AddListener(action);
        Text text = CreateText(rect, "Label", label, 15, TextAnchor.MiddleCenter, Bone, Vector2.zero, new Vector2(260f, 48f));
        text.fontStyle = FontStyle.Bold;
    }

    private void SetupAmbientAudio()
    {
        if (ambientClip == null)
        {
            return;
        }

        ambientSource = gameObject.AddComponent<AudioSource>();
        ambientSource.clip = ambientClip;
        ambientSource.playOnAwake = false;
        ambientSource.loop = true;
        ambientSource.spatialBlend = 0f;
        ambientSource.volume = 0.18f;
        ambientSource.Play();
    }

    private void PlaySound(AudioClip clip, float volume)
    {
        if (clip != null && effectsSource != null)
        {
            effectsSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }
    }

    private void PulseButton(MagmaDispersalPuzzle.PuzzleAction action)
    {
        int index = action == MagmaDispersalPuzzle.PuzzleAction.SwapLeft
            ? 0
            : action == MagmaDispersalPuzzle.PuzzleAction.Transfer ? 1 : 2;
        if (buttonPulseCoroutines[index] != null)
        {
            StopCoroutine(buttonPulseCoroutines[index]);
        }

        RectTransform rect = actionButtons[index].transform as RectTransform;
        if (rect != null)
        {
            rect.localScale = Vector3.one;
        }

        buttonPulseCoroutines[index] = StartCoroutine(PulseRect(index, rect));
    }

    private IEnumerator PulseRect(int index, RectTransform rect)
    {
        if (rect == null)
        {
            buttonPulseCoroutines[index] = null;
            yield break;
        }

        float elapsed = 0f;
        const float duration = 0.18f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = 1f - Mathf.Sin(t * Mathf.PI) * 0.07f;
            rect.localScale = Vector3.one * scale;
            yield return null;
        }

        rect.localScale = Vector3.one;
        buttonPulseCoroutines[index] = null;
    }

    private void CaptureAndUnlockCursor()
    {
        previousCursorLockMode = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        cursorStateCaptured = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestoreCursor()
    {
        if (!cursorStateCaptured)
        {
            return;
        }

        Cursor.lockState = previousCursorLockMode;
        Cursor.visible = previousCursorVisible;
        cursorStateCaptured = false;
    }

    private void RefreshHud()
    {
        moveText.text = "THAO TÁC  " + puzzle.MoveCount.ToString("00");
        int amount = puzzle.GetAmount(MagmaDispersalPuzzle.CoreVessel);
        goalAmountText.text = amount + "  /  " + MagmaDispersalPuzzle.TargetAmount + " NHỊP";
        goalAmountText.color = amount == MagmaDispersalPuzzle.TargetAmount ? TargetCyan : Bone;
        goalLocationText.text = "BÌNH CHUNG  •  " + puzzle.GetSharedAmount() + " / " + puzzle.GetSharedCapacity() + " NHỊP";
    }

    private void RefreshVesselModels(bool snapAmounts)
    {
        for (int vessel = 0; vessel < vesselViews.Length; vessel++)
        {
            vesselViews[vessel].SetModelState(
                GetDisplayedCapacity(vessel),
                GetDisplayedAmount(vessel),
                vessel == MagmaDispersalPuzzle.CoreVessel,
                MagmaDispersalPuzzle.TargetAmount,
                snapAmounts);
        }
    }

    private void RefreshHintIfVisible()
    {
        if (!hintVisible)
        {
            return;
        }

        hintVisible = false;
        ToggleHint();
    }

    private void SnapVesselsToSlots()
    {
        for (int vessel = 0; vessel < vesselRects.Length; vessel++)
        {
            vesselRects[vessel].anchoredPosition = SlotPositions[vessel];
            vesselRects[vessel].localScale = Vector3.one;
        }
    }

    private float[] CaptureDisplayedAmounts()
    {
        float[] values = new float[vesselViews.Length];
        for (int vessel = 0; vessel < values.Length; vessel++)
        {
            values[vessel] = vesselViews[vessel].DisplayedAmount;
        }

        return values;
    }

    private int GetDisplayedAmount(int vessel) => vessel == MagmaDispersalPuzzle.SharedReservoir ? puzzle.GetSharedAmount() : puzzle.GetAmount(vessel);
    private int GetDisplayedCapacity(int vessel) => vessel == MagmaDispersalPuzzle.SharedReservoir ? puzzle.GetSharedCapacity() : puzzle.GetCapacity(vessel);

    private void SetInputLocked(bool locked)
    {
        inputLocked = locked;
        for (int i = 0; i < actionButtons.Length; i++)
        {
            actionButtons[i].interactable = !locked && !puzzle.IsSolved;
        }
    }

    private void SetStatus(string message, bool positive)
    {
        statusText.text = message;
        statusText.color = positive ? Bone : new Color(1f, 0.36f, 0.18f, 1f);
    }

    private static string GetActionLabel(MagmaDispersalPuzzle.PuzzleAction action)
    {
        switch (action)
        {
            case MagmaDispersalPuzzle.PuzzleAction.SwapLeft:
                return "Q  •  VẶN VAN BÌNH LÕI";
            case MagmaDispersalPuzzle.PuzzleAction.Transfer:
                return "E  •  VẶN VAN BÌNH RÈN";
            case MagmaDispersalPuzzle.PuzzleAction.SwapRight:
                return "R  •  VẶN VAN BÌNH TRO";
            default:
                return string.Empty;
        }
    }

    private void CreateEventSystemIfNeeded()
    {
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            eventSystemObject.transform.SetParent(transform, false);
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
        }

        StandaloneInputModule module = eventSystem.GetComponent<StandaloneInputModule>();
        if (module == null)
        {
            module = eventSystem.gameObject.AddComponent<StandaloneInputModule>();
        }

        BaseInputModule[] modules = eventSystem.GetComponents<BaseInputModule>();
        for (int i = 0; i < modules.Length; i++)
        {
            if (modules[i] != module)
            {
                modules[i].enabled = false;
            }
        }

        module.enabled = true;
    }

    private static bool WasLeftPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Q);
#endif
    }

    private static bool WasTransferPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }

    private static bool WasRightPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }

    private static bool WasHintPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.H);
#endif
    }

    private static bool WasResetPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.backspaceKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Backspace);
#endif
    }

    private static bool WasEscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private static ColorBlock FireButtonColors()
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.72f, 0.47f, 1f);
        colors.pressedColor = new Color(0.72f, 0.35f, 0.18f, 1f);
        colors.selectedColor = new Color(1f, 0.72f, 0.47f, 1f);
        colors.disabledColor = new Color(0.34f, 0.28f, 0.25f, 0.65f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        return colors;
    }

    private static GameObject NewUiObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        gameObject.layer = 5;
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static RectTransform CreatePanel(
        RectTransform parent,
        string name,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 position,
        Vector2 size,
        Color fill,
        Color trim)
    {
        GameObject panelObject = NewUiObject(name, parent);
        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = panelObject.AddComponent<Image>();
        image.color = fill;
        image.raycastTarget = false;

        Shadow shadow = panelObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
        shadow.effectDistance = new Vector2(7f, -8f);

        Image topTrim = CreateImage(
            rect,
            "Top Trim",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            Vector2.zero,
            new Vector2(0f, 3f),
            trim);
        topTrim.raycastTarget = false;
        return rect;
    }

    private static Image CreateImage(
        RectTransform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Color color)
    {
        GameObject imageObject = NewUiObject(name, parent);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private Text CreateText(
        RectTransform parent,
        string name,
        string value,
        int fontSize,
        TextAnchor alignment,
        Color color,
        Vector2 position,
        Vector2 size)
    {
        return CreateText(parent, name, value, fontSize, alignment, color, position, size, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
    }

    private Text CreateText(
        RectTransform parent,
        string name,
        string value,
        int fontSize,
        TextAnchor alignment,
        Color color,
        Vector2 position,
        Vector2 size,
        Vector2 anchor,
        Vector2 pivot)
    {
        GameObject textObject = NewUiObject(name, parent);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Text text = textObject.AddComponent<Text>();
        text.font = displayFont;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.text = value;
        text.raycastTarget = false;
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        Shadow shadow = textObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.78f);
        shadow.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    private static Color Hex(string html)
    {
        return ColorUtility.TryParseHtmlString("#" + html, out Color color) ? color : Color.white;
    }
}
