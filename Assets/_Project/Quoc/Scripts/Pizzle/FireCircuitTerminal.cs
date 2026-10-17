using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class FireCircuitTerminal : MaskableGraphic
{
    private const int North = 1;
    private const int East = 2;
    private const int South = 4;
    private const int West = 8;
    private static readonly int[] StartingRotations = { 1, 3, 2 };
    private static readonly int[,] BaseRuneMasks =
    {
        { North | East | South, North | South | East, North | East | West, South | West },
        { North | East | South, North | East | South, East | West, North | South },
        { North | East, North | South, North | East | West, East | South }
    };
    private static readonly int[] ClockwiseExit = { East, South, West, North };
    private static readonly int[] ClockwiseEntry = { North, East, South, West };
    private static readonly int[] InwardDirection = { South, West, North, East };
    private static readonly int[] OutwardDirection = { North, East, South, West };

    private readonly int[] rotations = new int[3];
    private readonly bool[,] poweredNodes = new bool[3, 4];
    private readonly bool[,] poweredRingSegments = new bool[3, 4];
    private readonly bool[,] poweredRadialSegments = new bool[2, 4];
    private readonly bool[] poweredSeals = new bool[3];
    [Header("Scene UI References")]
    [SerializeField] private Text progressText;
    [SerializeField] private Text selectedText;
    [SerializeField] private Text confirmText;
    [SerializeField] private Text statusText;
    [SerializeField] private Text[] ringTexts = new Text[3];
    [Header("Audio References")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip selectionClip;
    [SerializeField] private AudioClip rotationClip;
    [SerializeField] private AudioClip powerClip;
    [SerializeField] private AudioClip successClip;
    [SerializeField] private AudioClip failureClip;
    [SerializeField] private AudioClip resetClip;
    [Header("Audio Volumes")]
    [SerializeField, Range(0f, 1f)] private float selectionVolume = 0.55f;
    [SerializeField, Range(0f, 1f)] private float rotationVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float powerVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float successVolume = 0.85f;
    [SerializeField, Range(0f, 1f)] private float failureVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float resetVolume = 0.55f;

    private int selectedRing;
    private bool solved;
    private float feedbackTimer;
    private string feedback;
    private int hoveredControl = -1;
    private bool selectionInputHeld;
    private bool initialized;

    public int SelectedRing => selectedRing;
    public bool Solved => solved;
    public float FeedbackTimer => feedbackTimer;

    protected override void Awake()
    {
        base.Awake();
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        ResetPuzzle();
        initialized = true;
        BindSceneInterface();
        RefreshInterface();
    }

    private void Update()
    {
        HandleInput();

        if (feedbackTimer > 0f)
        {
            feedbackTimer = Mathf.Max(0f, feedbackTimer - Time.unscaledDeltaTime);
            if (feedbackTimer <= 0f)
            {
                feedback = string.Empty;
                RefreshInterface();
            }
        }

        SetVerticesDirty();
    }

    public int GetRotation(int ringIndex)
    {
        return rotations[Mathf.Clamp(ringIndex, 0, rotations.Length - 1)];
    }

    public int GetRuneMask(int ringIndex, int nodeIndex)
    {
        int safeRing = Mathf.Clamp(ringIndex, 0, 2);
        int safeNode = Mathf.Clamp(nodeIndex, 0, 3);
        return RotateMask(BaseRuneMasks[safeRing, safeNode], rotations[safeRing]);
    }

    public bool IsNodePowered(int ringIndex, int nodeIndex)
    {
        return poweredNodes[Mathf.Clamp(ringIndex, 0, 2), Mathf.Clamp(nodeIndex, 0, 3)];
    }

    public bool IsRingSegmentPowered(int ringIndex, int segmentIndex)
    {
        return poweredRingSegments[Mathf.Clamp(ringIndex, 0, 2), Mathf.Clamp(segmentIndex, 0, 3)];
    }

    public bool IsRadialSegmentPowered(int outerRingIndex, int nodeIndex)
    {
        return poweredRadialSegments[Mathf.Clamp(outerRingIndex, 0, 1), Mathf.Clamp(nodeIndex, 0, 3)];
    }

    public bool IsSealPowered(int sealIndex)
    {
        return poweredSeals[Mathf.Clamp(sealIndex, 0, 2)];
    }

    public bool IsSourceConnectionPowered()
    {
        return poweredNodes[0, 0] && HasDirection(GetRuneMask(0, 0), North);
    }

    public bool IsCoreConnectionPowered()
    {
        return poweredSeals[2];
    }

    public int PoweredSealCount()
    {
        int count = 0;
        for (int i = 0; i < poweredSeals.Length; i++)
        {
            if (poweredSeals[i])
            {
                count++;
            }
        }

        return count;
    }

    public void SelectRing(int ringIndex)
    {
        if (solved)
        {
            return;
        }

        int nextRing = Mathf.Clamp(ringIndex, 0, rotations.Length - 1);
        if (nextRing != selectedRing)
        {
            selectedRing = nextRing;
            PlaySound(selectionClip, selectionVolume);
        }

        feedback = string.Empty;
        feedbackTimer = 0f;
        RefreshInterface();
    }

    public void RotateSelectedRing()
    {
        if (solved)
        {
            return;
        }

        int previousPowered = PoweredSealCount();
        rotations[selectedRing] = (rotations[selectedRing] + 1) % 4;
        RecalculatePower();
        int powered = PoweredSealCount();
        PlaySound(rotationClip, rotationVolume);
        if (powered > previousPowered)
        {
            PlaySound(powerClip, powerVolume);
        }

        feedback = "ĐÃ THÔNG — " + powered + " / 3 HỎA ẤN";
        feedbackTimer = 1.5f;
        RefreshInterface();
    }

    public void ConfirmFlow()
    {
        if (solved)
        {
            return;
        }

        if (PoweredSealCount() == 3)
        {
            solved = true;
            feedback = "HỎA ẤN ĐÃ ỔN ĐỊNH — CÁNH CỬA ĐÃ MỞ";
            feedbackTimer = float.PositiveInfinity;
            PlaySound(successClip, successVolume);
        }
        else
        {
            feedback = "DÒNG LỬA BẤT ỔN — CĂN CHỈNH BA VÒNG ẤN";
            feedbackTimer = 1.4f;
            PlaySound(failureClip, failureVolume);
        }

        RefreshInterface();
    }

    public void ResetPuzzle()
    {
        for (int i = 0; i < rotations.Length; i++)
        {
            rotations[i] = StartingRotations[i];
        }

        RecalculatePower();
        selectedRing = 0;
        solved = false;
        feedback = string.Empty;
        feedbackTimer = 0f;
        if (initialized)
        {
            PlaySound(resetClip, resetVolume);
        }

        RefreshInterface();
    }

    private void PlaySound(AudioClip clip, float volume)
    {
        if (audioSource != null && clip != null && volume > 0f)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }

    private void RecalculatePower()
    {
        for (int ring = 0; ring < 3; ring++)
        {
            for (int node = 0; node < 4; node++)
            {
                poweredNodes[ring, node] = false;
                poweredRingSegments[ring, node] = false;
                if (ring < 2) poweredRadialSegments[ring, node] = false;
            }
        }

        if (HasDirection(GetRuneMask(0, 0), North))
        {
            poweredNodes[0, 0] = true;
        }

        bool changed;
        do
        {
            changed = false;

            for (int ring = 0; ring < 3; ring++)
            {
                for (int segment = 0; segment < 4; segment++)
                {
                    int next = (segment + 1) % 4;
                    if (!IsRingEdgeConnected(ring, segment) || (!poweredNodes[ring, segment] && !poweredNodes[ring, next]))
                    {
                        continue;
                    }

                    if (!poweredNodes[ring, segment])
                    {
                        poweredNodes[ring, segment] = true;
                        changed = true;
                    }

                    if (!poweredNodes[ring, next])
                    {
                        poweredNodes[ring, next] = true;
                        changed = true;
                    }
                }
            }

            for (int outerRing = 0; outerRing < 2; outerRing++)
            {
                for (int node = 0; node < 4; node++)
                {
                    if (!IsRadialEdgeConnected(outerRing, node) || (!poweredNodes[outerRing, node] && !poweredNodes[outerRing + 1, node]))
                    {
                        continue;
                    }

                    if (!poweredNodes[outerRing, node])
                    {
                        poweredNodes[outerRing, node] = true;
                        changed = true;
                    }

                    if (!poweredNodes[outerRing + 1, node])
                    {
                        poweredNodes[outerRing + 1, node] = true;
                        changed = true;
                    }
                }
            }
        }
        while (changed);

        for (int ring = 0; ring < 3; ring++)
        {
            for (int segment = 0; segment < 4; segment++)
            {
                int next = (segment + 1) % 4;
                poweredRingSegments[ring, segment] = IsRingEdgeConnected(ring, segment)
                    && poweredNodes[ring, segment]
                    && poweredNodes[ring, next];
                if (ring < 2)
                {
                    poweredRadialSegments[ring, segment] = IsRadialEdgeConnected(ring, segment)
                        && poweredNodes[ring, segment]
                        && poweredNodes[ring + 1, segment];
                }
            }
        }

        poweredSeals[0] = poweredNodes[0, 3] && HasDirection(GetRuneMask(0, 3), West);
        poweredSeals[1] = poweredNodes[0, 1] && HasDirection(GetRuneMask(0, 1), East);
        poweredSeals[2] = poweredNodes[2, 2] && HasDirection(GetRuneMask(2, 2), North);
    }

    private bool IsRingEdgeConnected(int ring, int segment)
    {
        int next = (segment + 1) % 4;
        return HasDirection(GetRuneMask(ring, segment), ClockwiseExit[segment])
            && HasDirection(GetRuneMask(ring, next), ClockwiseEntry[segment]);
    }

    private bool IsRadialEdgeConnected(int outerRing, int node)
    {
        return HasDirection(GetRuneMask(outerRing, node), InwardDirection[node])
            && HasDirection(GetRuneMask(outerRing + 1, node), OutwardDirection[node]);
    }

    private static bool HasDirection(int mask, int direction)
    {
        return (mask & direction) != 0;
    }

    private static int RotateMask(int mask, int quarterTurns)
    {
        for (int turn = 0; turn < quarterTurns; turn++)
        {
            int rotated = 0;
            if ((mask & North) != 0) rotated |= East;
            if ((mask & East) != 0) rotated |= South;
            if ((mask & South) != 0) rotated |= West;
            if ((mask & West) != 0) rotated |= North;
            mask = rotated;
        }

        return mask;
    }

    private void HandleInput()
    {
        bool rotate = false;
        bool confirm = false;
        bool reset = false;
        int select = -1;
        int selectionStep = 0;
        bool selectionHeld = false;
        Vector2 pointer = Vector2.zero;
        bool pointerAvailable = false;
        bool pointerPressed = false;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) select = 0;
            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) select = 1;
            if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) select = 2;
            bool leftHeld = keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed;
            bool rightHeld = keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed;
            selectionHeld = leftHeld || rightHeld;
            if (!selectionInputHeld)
            {
                if (rightHeld && !leftHeld) selectionStep = 1;
                else if (leftHeld && !rightHeld) selectionStep = -1;
            }
            rotate = keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame;
            confirm = keyboard.fKey.wasPressedThisFrame;
            reset = keyboard.rKey.wasPressedThisFrame;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            pointer = mouse.position.ReadValue();
            pointerAvailable = true;
            pointerPressed = mouse.leftButton.wasPressedThisFrame;
        }

        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
        {
            pointer = touchscreen.primaryTouch.position.ReadValue();
            pointerAvailable = true;
            pointerPressed |= touchscreen.primaryTouch.press.wasPressedThisFrame;
        }
#else
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) select = 0;
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) select = 1;
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) select = 2;
        bool leftHeld = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
        bool rightHeld = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
        selectionHeld = leftHeld || rightHeld;
        if (!selectionInputHeld)
        {
            if (rightHeld && !leftHeld) selectionStep = 1;
            else if (leftHeld && !rightHeld) selectionStep = -1;
        }
        rotate = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.E);
        confirm = Input.GetKeyDown(KeyCode.F);
        reset = Input.GetKeyDown(KeyCode.R);
        pointer = Input.mousePosition;
        pointerAvailable = true;
        pointerPressed = Input.GetMouseButtonDown(0);
#endif

        if (select >= 0)
        {
            SelectRing(select);
        }
        else if (selectionStep != 0 && !solved)
        {
            SelectRing((selectedRing + selectionStep + 3) % 3);
        }

        selectionInputHeld = selectionHeld;

        if (rotate) RotateSelectedRing();
        if (confirm) ConfirmFlow();
        if (reset) ResetPuzzle();

        UpdatePointer(pointer, pointerAvailable, pointerPressed);
    }

    private void BindSceneInterface()
    {
        color = Color.white;
        raycastTarget = false;
        SetAllDirty();
    }

    private void RefreshInterface()
    {
        if (progressText == null)
        {
            return;
        }

        int powered = PoweredSealCount();
        progressText.text = "THỨC TỈNH HỎA ẤN " + powered + " / 3";
        selectedText.text =  "ĐANG CHỌN " +  "VÒNG " + ToRoman(selectedRing + 1);

        for (int i = 0; ringTexts != null && i < ringTexts.Length; i++)
        {
            if (ringTexts[i] == null)
            {
                continue;
            }

            ringTexts[i].color = i == selectedRing
                ? new Color(1f, 0.86f, 0.45f)
                : new Color(0.76f, 0.61f, 0.5f);
        }

        if (solved)
        {
            statusText.text = feedback;
            statusText.color = new Color(1f, 0.91f, 0.46f);
            confirmText.text = "CỔNG ĐÃ THỨC TỈNH";
            confirmText.color = new Color(1f, 0.94f, 0.6f);
        }
        else
        {
            statusText.text = string.IsNullOrEmpty(feedback)
                ? powered == 0
                    ? "DÒNG LỬA BỊ NGẮT — HÃY XOAY CÁC VÒNG ẤN"
                    : powered == 3
                        ? "BA VÒNG ẤN ĐÃ KHỚP — HÃY ĐÁNH THỨC PHONG ẤN"
                        : "ĐÃ THÔNG — " + powered + " / 3 HỎA ẤN"
                : feedback;
            statusText.color = feedbackTimer > 0f && powered < 3
                ? new Color(1f, 0.43f, 0.23f)
                : new Color(1f, 0.67f, 0.26f);
            confirmText.text = powered == 3 ? "THỨC TỈNH  [F]" : "THỨC TỈNH";
            confirmText.color = powered == 3
                ? new Color(1f, 0.96f, 0.58f)
                : new Color(0.88f, 0.55f, 0.27f);
        }

        SetVerticesDirty();
    }

    private static string ToRoman(int value)
    {
        if (value == 1) return "I";
        if (value == 2) return "II";
        return "III";
    }
    private static readonly Rect[] RingButtons =
    {
        new Rect(289f, 68f, 232f, 48f),
        new Rect(289f, 4f, 232f, 48f),
        new Rect(289f, -60f, 232f, 48f)
    };

    private static readonly Rect ConfirmButton = new Rect(289f, -157f, 232f, 62f);
    private static readonly Rect ResetButton = new Rect(289f, -215f, 232f, 38f);
    private static readonly Vector2 BoardCenter = new Vector2(-135f, 10f);

    private readonly Color background = new Color(0.025f, 0.009f, 0.006f, 1f);
    private readonly Color basalt = new Color(0.075f, 0.052f, 0.046f, 1f);
    private readonly Color basaltLight = new Color(0.14f, 0.09f, 0.068f, 1f);
    private readonly Color dormant = new Color(0.25f, 0.12f, 0.075f, 1f);
    private readonly Color ember = new Color(1f, 0.3f, 0.055f, 1f);
    private readonly Color flame = new Color(1f, 0.76f, 0.17f, 1f);
    private readonly Color hot = new Color(1f, 0.96f, 0.54f, 1f);

    public void UpdatePointer(Vector2 screenPosition, bool available, bool pressed)
    {
        int previousHover = hoveredControl;
        hoveredControl = -1;

        if (available && RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPosition, null, out Vector2 local))
        {
            for (int i = 0; i < RingButtons.Length; i++)
            {
                if (RingButtons[i].Contains(local))
                {
                    hoveredControl = i;
                }
            }

            if (ConfirmButton.Contains(local)) hoveredControl = 3;
            if (ResetButton.Contains(local)) hoveredControl = 4;

            if (pressed)
            {
                if (hoveredControl >= 0 && hoveredControl <= 2)
                {
                    if (SelectedRing == hoveredControl) RotateSelectedRing();
                    else SelectRing(hoveredControl);
                }
                else if (hoveredControl == 3)
                {
                    ConfirmFlow();
                }
                else if (hoveredControl == 4)
                {
                    ResetPuzzle();
                }
            }
        }

        if (previousHover != hoveredControl)
        {
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        Rect screen = rectTransform.rect;
        float time = Application.isPlaying ? Time.unscaledTime : 0f;

        AddQuad(vertexHelper, screen, background);
        DrawCaveBackdrop(vertexHelper, screen, time);
        DrawTerminalFrame(vertexHelper, time);
        DrawBoard(vertexHelper, time);
        DrawControls(vertexHelper, time);
    }

    private void DrawCaveBackdrop(VertexHelper vh, Rect screen, float time)
    {
        AddQuad(vh, new Rect(screen.xMin, screen.yMin, screen.width, 115f), new Color(0.18f, 0.025f, 0.008f, 1f));
        AddQuad(vh, new Rect(screen.xMin, screen.yMin, screen.width, 7f), new Color(1f, 0.21f, 0.025f, 0.5f));

        for (int i = 0; i < 18; i++)
        {
            float x = screen.xMin + Mathf.Repeat(i * 191f + 37f, screen.width);
            float rise = Mathf.Repeat(time * (18f + i % 4 * 6f) + i * 53f, screen.height + 100f);
            float y = screen.yMin + rise - 45f;
            float pulse = 0.45f + 0.25f * Mathf.Sin(time * 2.2f + i);
            AddDiamond(vh, new Vector2(x, y), 2.5f + i % 3, new Color(1f, 0.24f + i % 2 * 0.16f, 0.035f, pulse));
        }

        AddQuad(vh, new Rect(screen.xMin, screen.yMin, 90f, screen.height), new Color(0f, 0f, 0f, 0.38f));
        AddQuad(vh, new Rect(screen.xMax - 90f, screen.yMin, 90f, screen.height), new Color(0f, 0f, 0f, 0.38f));
        AddQuad(vh, new Rect(screen.xMin, screen.yMax - 80f, screen.width, 80f), new Color(0f, 0f, 0f, 0.46f));
    }

    private void DrawTerminalFrame(VertexHelper vh, float time)
    {
        Rect shadow = new Rect(-600f, -390f, 1200f, 780f);
        AddQuad(vh, Expand(shadow, 20f), new Color(0f, 0f, 0f, 0.82f));
        AddFrame(vh, Expand(shadow, 11f), 18f, new Color(0.11f, 0.065f, 0.045f, 1f));
        AddFrame(vh, shadow, 5f, new Color(0.36f, 0.14f, 0.055f, 1f));
        AddFrame(vh, Contract(shadow, 10f), 2f, new Color(1f, 0.31f, 0.055f, 0.45f));
        AddQuad(vh, Contract(shadow, 14f), new Color(0.035f, 0.019f, 0.016f, 0.985f));

        float pulse = 0.34f + 0.12f * Mathf.Sin(time * 1.8f);
        AddLine(vh, new Vector2(-520f, 280f), new Vector2(520f, 280f), 2f, new Color(1f, 0.27f, 0.045f, pulse));
        AddLine(vh, new Vector2(-520f, -314f), new Vector2(520f, -314f), 1.5f, new Color(0.75f, 0.22f, 0.06f, 0.45f));

        DrawCrack(vh, new Vector2(-555f, 248f), new Vector2(-520f, 216f), 4, 1.5f);
        DrawCrack(vh, new Vector2(548f, -244f), new Vector2(515f, -210f), 3, 1.2f);
        DrawCrack(vh, new Vector2(-536f, -224f), new Vector2(-505f, -190f), 3, 1.2f);
    }

    private void DrawBoard(VertexHelper vh, float time)
    {
        Rect board = new Rect(-532f, -242f, 784f, 490f);
        AddQuad(vh, board, new Color(0.025f, 0.018f, 0.017f, 1f));
        AddFrame(vh, board, 3f, new Color(0.26f, 0.12f, 0.07f, 1f));
        AddFrame(vh, Contract(board, 7f), 1.2f, new Color(0.68f, 0.22f, 0.06f, 0.38f));

        DrawRadialConnections(vh, time);

        bool sourcePowered = IsSourceConnectionPowered();
        bool outerSealPowered = IsSealPowered(0);
        bool middleSealPowered = IsSealPowered(1);
        bool corePowered = IsCoreConnectionPowered();

        DrawEnergyLine(vh, new Vector2(BoardCenter.x, BoardCenter.y + 191f), new Vector2(BoardCenter.x, BoardCenter.y + 180f), sourcePowered, time, 0f);
        DrawEnergyLine(vh, new Vector2(BoardCenter.x - 180f, BoardCenter.y), new Vector2(-405f, BoardCenter.y), outerSealPowered, time, 0.3f);
        DrawEnergyLine(vh, new Vector2(BoardCenter.x + 180f, BoardCenter.y), new Vector2(80f, BoardCenter.y), middleSealPowered, time, 0.6f);
        DrawEnergyLine(vh, new Vector2(BoardCenter.x, BoardCenter.y - 70f), BoardCenter, corePowered, time, 0.8f);

        for (int ring = 0; ring < 3; ring++)
        {
            float halfSize = 180f - ring * 55f;
            bool selected = SelectedRing == ring && !Solved;
            DrawCircuitRing(vh, ring, halfSize, selected, time);
        }

        DrawSource(vh, time);
        DrawSeal(vh, new Vector2(-440f, BoardCenter.y), 1, outerSealPowered, time);
        DrawSeal(vh, new Vector2(116f, BoardCenter.y), 2, middleSealPowered, time);
        DrawSeal(vh, BoardCenter, 3, corePowered, time);
    }

    private void DrawSource(VertexHelper vh, float time)
    {
        Vector2 center = new Vector2(BoardCenter.x, BoardCenter.y + 210f);
        float pulse = 1f + Mathf.Sin(time * 4f) * 0.06f;
        AddCircle(vh, center, 30f * pulse, 24, new Color(1f, 0.18f, 0.025f, 0.15f));
        AddCircle(vh, center, 19f, 20, new Color(0.18f, 0.055f, 0.025f, 1f));
        AddCircle(vh, center, 15f, 20, ember);
        AddDiamond(vh, center + new Vector2(0f, 2f), 8f, hot);
        AddLine(vh, center + new Vector2(-5f, -7f), center + new Vector2(5f, 8f), 3f, hot);
    }

    private void DrawRadialConnections(VertexHelper vh, float time)
    {
        for (int outerRing = 0; outerRing < 2; outerRing++)
        {
            float outerHalf = 180f - outerRing * 55f;
            float innerHalf = outerHalf - 55f;
            Vector2[] outerNodes = GetRingNodes(outerHalf);
            Vector2[] innerNodes = GetRingNodes(innerHalf);

            for (int node = 0; node < 4; node++)
            {
                bool powered = IsRadialSegmentPowered(outerRing, node);
                DrawEnergyLine(vh, outerNodes[node], innerNodes[node], powered, time, 0.15f * (outerRing * 4 + node));
            }
        }
    }

    private void DrawCircuitRing(VertexHelper vh, int ring, float halfSize, bool selected, float time)
    {
        Rect circuit = new Rect(BoardCenter.x - halfSize, BoardCenter.y - halfSize, halfSize * 2f, halfSize * 2f);

        if (selected)
        {
            AddFrame(vh, Expand(circuit, 9f), 1.5f, new Color(0.62f, 0.19f, 0.045f, 0.22f + 0.08f * Mathf.Sin(time * 4f)));
        }

        Vector2[] nodes = GetRingNodes(halfSize);
        Vector2[] corners =
        {
            BoardCenter + new Vector2(halfSize, halfSize),
            BoardCenter + new Vector2(halfSize, -halfSize),
            BoardCenter + new Vector2(-halfSize, -halfSize),
            BoardCenter + new Vector2(-halfSize, halfSize)
        };

        for (int segment = 0; segment < 4; segment++)
        {
            bool powered = IsRingSegmentPowered(ring, segment);
            int next = (segment + 1) % 4;
            float phase = ring * 0.17f + segment * 0.23f;
            DrawEnergyLine(vh, nodes[segment], corners[segment], powered, time, phase);
            DrawEnergyLine(vh, corners[segment], nodes[next], powered, time, phase + 0.1f);
        }

        for (int i = 0; i < nodes.Length; i++)
        {
            int mask = GetRuneMask(ring, i);
            bool powered = IsNodePowered(ring, i);
            DrawRuneNode(vh, nodes[i], mask, powered, selected, time + i * 0.4f);
        }
    }

    private static Vector2[] GetRingNodes(float halfSize)
    {
        return new[]
        {
            BoardCenter + new Vector2(0f, halfSize),
            BoardCenter + new Vector2(halfSize, 0f),
            BoardCenter + new Vector2(0f, -halfSize),
            BoardCenter + new Vector2(-halfSize, 0f)
        };
    }

    private void DrawRuneNode(VertexHelper vh, Vector2 center, int mask, bool powered, bool selected, float time)
    {
        Rect tile = new Rect(center.x - 24f, center.y - 24f, 48f, 48f);
        AddQuad(vh, Expand(tile, selected ? 4f : 2f), selected ? new Color(0.78f, 0.25f, 0.055f, 0.68f) : new Color(0.17f, 0.075f, 0.04f, 1f));
        AddQuad(vh, tile, basalt);
        AddFrame(vh, tile, 2f, powered ? flame : basaltLight);
        AddCircle(vh, center, powered ? 6f : 5f, 12, powered ? hot : dormant);

        Color runeColor = powered ? hot : new Color(0.55f, 0.24f, 0.095f, 1f);
        if (powered)
        {
            AddCircle(vh, center, 12f + Mathf.Sin(time * 3f) * 2f, 16, new Color(1f, 0.22f, 0.03f, 0.12f));
        }

        if ((mask & 1) != 0) AddLine(vh, center, center + Vector2.up * 18f, 5f, runeColor);
        if ((mask & 2) != 0) AddLine(vh, center, center + Vector2.right * 18f, 5f, runeColor);
        if ((mask & 4) != 0) AddLine(vh, center, center + Vector2.down * 18f, 5f, runeColor);
        if ((mask & 8) != 0) AddLine(vh, center, center + Vector2.left * 18f, 5f, runeColor);
    }

    private void DrawEnergyLine(VertexHelper vh, Vector2 start, Vector2 end, bool powered, float time, float phase)
    {
        if (powered)
        {
            AddLine(vh, start, end, 12f, new Color(1f, 0.15f, 0.015f, 0.15f));
            AddLine(vh, start, end, 4f, flame);
            float t = Mathf.Repeat(time * 1.4f + phase, 1f);
            AddCircle(vh, Vector2.Lerp(start, end, t), 5f, 10, hot);
        }
        else
        {
            AddLine(vh, start, end, 3f, dormant);
        }
    }

    private void DrawSeal(VertexHelper vh, Vector2 center, int number, bool powered, float time)
    {
        Color glow = powered ? new Color(1f, 0.21f, 0.025f, 0.2f + Mathf.Sin(time * 3f + number) * 0.04f) : new Color(0.12f, 0.055f, 0.035f, 0.8f);
        AddCircle(vh, center, powered ? 34f : 30f, 28, glow);
        AddCircle(vh, center, 23f, 24, powered ? ember : new Color(0.16f, 0.075f, 0.045f, 1f));
        AddCircle(vh, center, 18f, 24, basalt);

        Color symbol = powered ? hot : dormant;
        Vector2 top = center + new Vector2(0f, 12f);
        Vector2 left = center + new Vector2(-9f, -8f);
        Vector2 right = center + new Vector2(9f, -8f);
        AddTriangle(vh, top, right, center + new Vector2(0f, 2f), symbol);
        AddTriangle(vh, top, center + new Vector2(0f, 2f), left, symbol);
        AddCircle(vh, center + new Vector2(0f, -7f), 7f, 12, symbol);
    }

    private void DrawControls(VertexHelper vh, float time)
    {
        Rect panel = new Rect(270f, -238f, 270f, 490f);
        AddQuad(vh, panel, new Color(0.055f, 0.032f, 0.027f, 1f));
        AddFrame(vh, panel, 2f, new Color(0.32f, 0.13f, 0.06f, 1f));

        for (int i = 0; i < RingButtons.Length; i++)
        {
            bool selected = SelectedRing == i && !Solved;
            bool hover = hoveredControl == i;
            Color fill = selected ? new Color(0.27f, 0.095f, 0.035f, 1f) : new Color(0.09f, 0.052f, 0.042f, 1f);
            if (hover) fill = new Color(0.32f, 0.12f, 0.045f, 1f);
            AddQuad(vh, RingButtons[i], fill);
            AddFrame(vh, RingButtons[i], selected ? 3f : 1.5f, selected ? flame : new Color(0.37f, 0.17f, 0.09f, 1f));

            if (selected)
            {
                float x = RingButtons[i].xMin + Mathf.Repeat(time * 95f, RingButtons[i].width);
                AddQuad(vh, new Rect(x - 12f, RingButtons[i].yMin, 24f, 3f), hot);
            }
        }

        bool ready = PoweredSealCount() == 3;
        bool solved = Solved;
        Color confirmFill = solved
            ? new Color(0.35f, 0.16f, 0.025f, 1f)
            : ready ? new Color(0.42f, 0.105f, 0.02f, 1f) : new Color(0.13f, 0.055f, 0.033f, 1f);
        if (hoveredControl == 3 && !solved) confirmFill *= 1.18f;
        AddQuad(vh, ConfirmButton, confirmFill);
        AddFrame(vh, ConfirmButton, ready || solved ? 3f : 2f, ready || solved ? flame : new Color(0.45f, 0.18f, 0.07f, 1f));

        if (ready || solved)
        {
            float pulse = 0.14f + Mathf.Sin(time * 4f) * 0.045f;
            AddQuad(vh, Expand(ConfirmButton, 7f), new Color(1f, 0.18f, 0.02f, pulse));
            AddFrame(vh, ConfirmButton, 3f, hot);
        }

        AddQuad(vh, ResetButton, hoveredControl == 4 ? new Color(0.18f, 0.075f, 0.045f, 1f) : new Color(0.075f, 0.045f, 0.038f, 1f));
        AddFrame(vh, ResetButton, 1f, new Color(0.31f, 0.14f, 0.08f, 1f));

        if (FeedbackTimer > 0f && !float.IsPositiveInfinity(FeedbackTimer) && PoweredSealCount() < 3)
        {
            AddFrame(vh, new Rect(-532f, -242f, 784f, 490f), 3f, new Color(1f, 0.16f, 0.03f, 0.4f + Mathf.Sin(time * 18f) * 0.15f));
        }
    }

    private void DrawCrack(VertexHelper vh, Vector2 start, Vector2 end, int segments, float thickness)
    {
        Vector2 previous = start;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector2 point = Vector2.Lerp(start, end, t);
            point.x += Mathf.Sin(i * 4.17f) * 6f;
            AddLine(vh, previous, point, thickness, new Color(0.55f, 0.17f, 0.055f, 0.5f));
            previous = point;
        }
    }

    private static Rect Expand(Rect rect, float amount)
    {
        return new Rect(rect.xMin - amount, rect.yMin - amount, rect.width + amount * 2f, rect.height + amount * 2f);
    }

    private static Rect Contract(Rect rect, float amount)
    {
        return Expand(rect, -amount);
    }

    private static void AddFrame(VertexHelper vh, Rect rect, float thickness, Color color)
    {
        AddQuad(vh, new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
        AddQuad(vh, new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
        AddQuad(vh, new Rect(rect.xMin, rect.yMin + thickness, thickness, rect.height - thickness * 2f), color);
        AddQuad(vh, new Rect(rect.xMax - thickness, rect.yMin + thickness, thickness, rect.height - thickness * 2f), color);
    }

    private static void AddLine(VertexHelper vh, Vector2 start, Vector2 end, float width, Color color)
    {
        Vector2 direction = end - start;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector2 normal = new Vector2(-direction.y, direction.x).normalized * width * 0.5f;
        int index = vh.currentVertCount;
        vh.AddVert(start - normal, color, Vector2.zero);
        vh.AddVert(start + normal, color, Vector2.zero);
        vh.AddVert(end + normal, color, Vector2.zero);
        vh.AddVert(end - normal, color, Vector2.zero);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }

    private static void AddQuad(VertexHelper vh, Rect rect, Color color)
    {
        int index = vh.currentVertCount;
        vh.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
        vh.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.zero);
        vh.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.zero);
        vh.AddVert(new Vector3(rect.xMax, rect.yMin), color, Vector2.zero);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }

    private static void AddCircle(VertexHelper vh, Vector2 center, float radius, int segments, Color color)
    {
        int centerIndex = vh.currentVertCount;
        vh.AddVert(center, color, Vector2.zero);
        for (int i = 0; i <= segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            vh.AddVert(point, color, Vector2.zero);
        }

        for (int i = 0; i < segments; i++)
        {
            vh.AddTriangle(centerIndex, centerIndex + i + 1, centerIndex + i + 2);
        }
    }

    private static void AddDiamond(VertexHelper vh, Vector2 center, float radius, Color color)
    {
        AddTriangle(vh, center + Vector2.up * radius, center + Vector2.right * radius, center + Vector2.down * radius, color);
        AddTriangle(vh, center + Vector2.up * radius, center + Vector2.down * radius, center + Vector2.left * radius, color);
    }

    private static void AddTriangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        int index = vh.currentVertCount;
        vh.AddVert(a, color, Vector2.zero);
        vh.AddVert(b, color, Vector2.zero);
        vh.AddVert(c, color, Vector2.zero);
        vh.AddTriangle(index, index + 1, index + 2);
    }
}
