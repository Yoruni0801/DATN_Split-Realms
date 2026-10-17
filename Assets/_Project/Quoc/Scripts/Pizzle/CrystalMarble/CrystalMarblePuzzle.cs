using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// Runtime controller for the fire-cave Crystal Marble puzzle.
/// The geometry is intentionally authored in the scene; this component only
/// rotates the authored rig, measures the alignment and drives feedback.
/// </summary>
[DisallowMultipleComponent]
public sealed class CrystalMarblePuzzle : MonoBehaviour
{
    public enum PuzzleState
    {
        Playing,
        Solved
    }

    [Header("Authored Scene References")]
    [SerializeField] private Transform marbleRig;
    [SerializeField] private Transform solutionReference;
    [SerializeField] private Transform startReference;
    [SerializeField] private GameObject hintGuide;
    [SerializeField] private GameObject solvedFx;
    [SerializeField] private Light crystalLight;
    [SerializeField] private Renderer crystalShellRenderer;

    [Header("UI References")]
    [SerializeField] private Text angleText;
    [SerializeField] private Text statusText;
    [SerializeField] private Text instructionText;
    [SerializeField] private Text hintText;
    [SerializeField] private Text stateText;
    [SerializeField] private Image alignmentFill;
    [SerializeField] private CanvasGroup successOverlay;
    [SerializeField] private Text successTitleText;
    [SerializeField] private Button continueButton;

    [Header("Audio")]
    [SerializeField] private AudioSource feedbackSource;
    [SerializeField] private AudioSource ambienceSource;
    [SerializeField] private AudioClip ambienceClip;
    [SerializeField] private AudioClip rotateClip;
    [SerializeField] private AudioClip nearClip;
    [SerializeField] private AudioClip solvedClip;
    [SerializeField] private AudioClip resetClip;
    [SerializeField] private AudioClip hintClip;
    [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.18f;
    [SerializeField, Range(0f, 1f)] private float rotateVolume = 0.12f;
    [SerializeField, Range(0f, 1f)] private float nearVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float solvedVolume = 0.8f;
    [SerializeField, Min(0.02f)] private float rotateSoundCooldown = 0.1f;

    [Header("Puzzle Tuning")]
    [SerializeField, Min(1f)] private float keyboardRotationSpeed = 78f;
    [SerializeField, Min(0.01f)] private float dragRotationSpeed = 0.32f;
    [SerializeField, Min(0f)] private float dragInertia = 4.5f;
    [SerializeField, Min(0.1f)] private float solveTolerance = 8f;
    [SerializeField, Min(0.05f)] private float solveHoldDuration = 0.55f;
    [SerializeField, Min(1f)] private float angleForEmptyMeter = 120f;
    [SerializeField, Min(0.05f)] private float fragmentGlowDuration = 0.28f;
    [SerializeField, Min(0.05f)] private float fragmentRevealDuration = 0.8f;
    [SerializeField, Min(0.01f)] private float fragmentMinScale = 0.08f;
    [SerializeField, Min(0.05f)] private float successOverlayDelay = 0.35f;
    [SerializeField] private string returnSceneName = "Map5";
    [SerializeField] private GameObject embeddedCloseTarget;

    private PuzzleState state;
    private bool suppressSuccessOverlay;
    private Quaternion startRotation;
    private Vector3 angularVelocity;
    private Vector3 previousPointerPosition;
    private bool dragging;
    private bool hintVisible;
    private bool nearPlayed;
    private float solveTimer;
    private float currentAngle;
    private float rotateSoundTimer;
    private Coroutine successRoutine;
    private bool revealRunning;
    private Renderer[] hintRenderers;
    private MaterialPropertyBlock[] hintInitialBlocks;
    private MaterialPropertyBlock[] hintWorkingBlocks;
    private Color[] hintBaseEmissions;
    private Vector3[] hintBaseScales;
    private Vector3 hintGuideBaseScale;

    private static readonly Color Ember = new Color(1f, 0.29f, 0.08f, 1f);
    private static readonly Color WarmWhite = new Color(1f, 0.86f, 0.64f, 1f);
    private static readonly Color Ash = new Color(0.48f, 0.42f, 0.37f, 1f);
    private static readonly Color Success = new Color(0.66f, 1f, 0.56f, 1f);

    public event System.Action OnPuzzleSolved;
    public event System.Action OnContinueRequested;
    [SerializeField] private UnityEvent onPuzzleSolved;

    public PuzzleState State => state;
    public float CurrentAngle => currentAngle;
    public bool IsSolved => state == PuzzleState.Solved;

    public void SetEmbeddedCloseTarget(GameObject target)
    {
        embeddedCloseTarget = target;
    }

    public void SuppressSuccessOverlay()
    {
        suppressSuccessOverlay = true;
        SetSuccessOverlay(false);
    }

    private void Awake()
    {
        if (marbleRig == null)
        {
            marbleRig = transform.Find("Puzzle_Editable/CrystalOrb/RotatablePelletRig");
        }

        if (solutionReference == null)
        {
            solutionReference = transform.Find("Puzzle_Editable/SolutionPoseMarker");
        }

        if (startReference != null)
        {
            startRotation = startReference.rotation;
        }
        else if (marbleRig != null)
        {
            startRotation = marbleRig.rotation;
        }

        if (feedbackSource == null)
        {
            feedbackSource = GetComponent<AudioSource>();
        }

        if (feedbackSource != null)
        {
            feedbackSource.playOnAwake = false;
            feedbackSource.loop = false;
        }

        if (ambienceSource != null)
        {
            ambienceSource.playOnAwake = false;
            ambienceSource.loop = true;
            ambienceSource.spatialBlend = 0f;
            ambienceSource.clip = ambienceClip;
            // Volume will be updated in Update
            if (ambienceClip != null)
            {
                ambienceSource.Play();
            }
        }

        hintVisible = hintGuide != null && hintGuide.activeSelf;
        CacheHintRevealState();
        SetSuccessOverlay(false);
    }

    private void Start()
    {
        ResetPuzzle();
    }

    private void Update()
    {
        if (ambienceSource != null)
        {
            float targetAmbience = AudioManager.HasInstance ? AudioManager.Instance.GetEffectiveSFXVolume() * ambienceVolume : ambienceVolume;
            ambienceSource.volume = targetAmbience;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            PlayClip(resetClip, 0.28f);
            ResetPuzzle();
            return;
        }

        if (Input.GetKeyDown(KeyCode.H))
        {
            if (state != PuzzleState.Solved)
            {
                PlayClip(hintClip, 0.2f);
                SetHintVisible(!hintVisible);
            }
        }

        rotateSoundTimer = Mathf.Max(0f, rotateSoundTimer - Time.unscaledDeltaTime);

        if (state == PuzzleState.Solved)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                ContinueAfterSolved();
                return;
            }

            UpdateSolvedPresentation();
            return;
        }

        HandlePointerInput();
        HandleKeyboardInput();
        ApplyInertia();
        EvaluateAlignment();
    }

    public void ResetPuzzle()
    {
        state = PuzzleState.Playing;
        suppressSuccessOverlay = false;
        solveTimer = 0f;
        angularVelocity = Vector3.zero;
        dragging = false;
        nearPlayed = false;
        rotateSoundTimer = 0f;

        if (marbleRig != null)
        {
            marbleRig.rotation = startRotation;
        }

        if (solvedFx != null)
        {
            solvedFx.SetActive(false);
        }

        if (successRoutine != null)
        {
            StopCoroutine(successRoutine);
            successRoutine = null;
        }

        revealRunning = false;
        RestoreHintRevealState();
        SetSuccessOverlay(false);
        SetHintVisible(hintVisible);
        SetInstruction("Kéo chuột để xoay • WASD / mũi tên xoay trục • Q / E lật mặt • R đặt lại");
        SetStatus("ĐANG TÌM HÌNH CHIẾU", Ash);
        if (stateText != null)
        {
            stateText.text = "TÌNH TRẠNG  /  ĐANG CĂN CHỈNH";
            stateText.color = WarmWhite;
        }

        EvaluateAlignment();
    }

    public void ContinueAfterSolved()
    {
        if (state != PuzzleState.Solved)
        {
            return;
        }

        OnContinueRequested?.Invoke();
        Time.timeScale = 1f;
        if (embeddedCloseTarget != null)
        {
            embeddedCloseTarget.SetActive(false);
            return;
        }

        if (string.IsNullOrEmpty(returnSceneName))
        {
            SetSuccessOverlay(false);
            return;
        }

        CrystalMarbleTravelState.JustCompletedMiniGame = true;

        if (Application.CanStreamedLevelBeLoaded(returnSceneName))
        {
            try
            {
                Tien.AdvancedLoading.Core.SceneLoader.Load(returnSceneName);
            }
            catch
            {
                SceneManager.LoadScene(returnSceneName, LoadSceneMode.Single);
            }
        }
        else
        {
            Debug.LogWarning("Crystal Marble return scene is not in Build Settings: " + returnSceneName, this);
        }
    }

    public void SetHintVisible(bool visible)
    {
        hintVisible = visible;
        if (hintGuide != null)
        {
            hintGuide.SetActive(visible && state != PuzzleState.Solved);
        }

        if (hintText != null)
        {
            hintText.text = visible ? "H  •  ẨN HỎA ẤN" : "H  •  HIỆN HỎA ẤN";
            hintText.color = visible ? WarmWhite : Ash;
        }
    }

    private void HandlePointerInput()
    {
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            dragging = true;
            previousPointerPosition = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1))
        {
            dragging = false;
        }

        if (dragging && (Input.GetMouseButton(0) || Input.GetMouseButton(1)))
        {
            Vector3 pointer = Input.mousePosition;
            Vector3 delta = pointer - previousPointerPosition;
            previousPointerPosition = pointer;

            Vector3 rotationDelta = new Vector3(
                delta.y * dragRotationSpeed,
                -delta.x * dragRotationSpeed,
                Input.GetMouseButton(1) ? -delta.x * dragRotationSpeed * 0.65f : 0f);

            ApplyRotation(rotationDelta, true);
        }

        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Moved)
            {
                Vector2 delta = touch.deltaPosition;
                ApplyRotation(new Vector3(delta.y, -delta.x, 0f) * dragRotationSpeed, true);
            }
        }
    }

    private void HandleKeyboardInput()
    {
        float pitch = 0f;
        float yaw = 0f;
        float roll = 0f;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) pitch += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) pitch -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) yaw += 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) yaw -= 1f;
        if (Input.GetKey(KeyCode.E)) roll += 1f;
        if (Input.GetKey(KeyCode.Q)) roll -= 1f;

        Vector3 keyboardDelta = new Vector3(pitch, yaw, roll) * keyboardRotationSpeed * Time.unscaledDeltaTime;
        if (keyboardDelta.sqrMagnitude > 0f)
        {
            ApplyRotation(keyboardDelta, false);
        }
    }

    private void ApplyInertia()
    {
        if (dragging || angularVelocity.sqrMagnitude < 0.01f || marbleRig == null)
        {
            return;
        }

        ApplyRotation(angularVelocity * Time.unscaledDeltaTime, false);
        angularVelocity = Vector3.MoveTowards(angularVelocity, Vector3.zero, dragInertia * 90f * Time.unscaledDeltaTime);
    }

    private void ApplyRotation(Vector3 eulerDelta, bool fromPointer)
    {
        if (marbleRig == null || eulerDelta.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        Quaternion yaw = Quaternion.AngleAxis(eulerDelta.y, Vector3.up);
        Quaternion pitch = Quaternion.AngleAxis(eulerDelta.x, Vector3.right);
        Quaternion roll = Quaternion.AngleAxis(eulerDelta.z, Vector3.forward);
        marbleRig.rotation = yaw * pitch * roll * marbleRig.rotation;

        if (rotateClip != null && feedbackSource != null && rotateSoundTimer <= 0f)
        {
            float speed = Mathf.Clamp01(eulerDelta.magnitude / 8f);
            float baseVol = rotateVolume * Mathf.Lerp(0.65f, 1f, speed);
            float finalVol = AudioManager.HasInstance ? baseVol * AudioManager.Instance.GetEffectiveSFXVolume() : baseVol;
            
            feedbackSource.pitch = Mathf.Lerp(0.9f, 1.08f, speed);
            feedbackSource.PlayOneShot(rotateClip, finalVol);
            feedbackSource.pitch = 1f;
            rotateSoundTimer = rotateSoundCooldown;
        }

        if (fromPointer)
        {
            angularVelocity = Vector3.Lerp(
                angularVelocity,
                eulerDelta / Mathf.Max(Time.unscaledDeltaTime, 0.001f),
                0.45f);
        }
    }

    private void EvaluateAlignment()
    {
        if (marbleRig == null || solutionReference == null)
        {
            return;
        }

        currentAngle = Quaternion.Angle(marbleRig.rotation, solutionReference.rotation);
        float alignment = 1f - Mathf.Clamp01(currentAngle / Mathf.Max(1f, angleForEmptyMeter));
        if (alignmentFill != null)
        {
            alignmentFill.fillAmount = alignment;
        }

        if (angleText != null)
        {
            angleText.text = "LỆCH  " + Mathf.RoundToInt(currentAngle).ToString("000") + "°";
            angleText.color = currentAngle <= solveTolerance ? Success : WarmWhite;
        }

        if (currentAngle <= solveTolerance)
        {
            solveTimer += Time.unscaledDeltaTime;
            SetStatus("HỎA ẤN ĐANG CỘNG HƯỞNG", Success);
            if (!nearPlayed)
            {
                nearPlayed = true;
                PlayClip(nearClip, nearVolume);
            }

            if (solveTimer >= solveHoldDuration)
            {
                SolvePuzzle();
            }
        }
        else
        {
            solveTimer = 0f;
            nearPlayed = false;
            SetStatus(currentAngle < 24f ? "GẦN ĐÚNG  /  GIỮ NHỊP XOAY" : "ĐANG TÌM HÌNH CHIẾU", currentAngle < 24f ? Ember : Ash);
        }

        if (crystalLight != null)
        {
            crystalLight.intensity = Mathf.Lerp(1.4f, 5.2f, alignment);
        }

        if (crystalShellRenderer != null)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            crystalShellRenderer.GetPropertyBlock(block);
            block.SetColor("_EmissionColor", Color.Lerp(new Color(0.18f, 0.02f, 0.005f), new Color(1f, 0.12f, 0.015f), alignment));
            crystalShellRenderer.SetPropertyBlock(block);
        }
    }

    private void SolvePuzzle()
    {
        state = PuzzleState.Solved;
        OnPuzzleSolved?.Invoke();
        onPuzzleSolved?.Invoke();
        solveTimer = 0f;
        angularVelocity = Vector3.zero;
        dragging = false;

        if (marbleRig != null && solutionReference != null)
        {
            marbleRig.rotation = solutionReference.rotation;
        }

        if (solvedFx != null)
        {
            solvedFx.SetActive(true);
        }

        if (stateText != null)
        {
            stateText.text = "TÌNH TRẠNG  /  HỎA ẤN ĐÃ THỨC TỈNH";
            stateText.color = Success;
        }

        SetStatus("HỎA ẤN ĐÃ THỨC TỈNH", Success);
        SetHintVisible(false);
        PlayClip(solvedClip, solvedVolume);

        if (successRoutine != null)
        {
            StopCoroutine(successRoutine);
        }
        successRoutine = StartCoroutine(PlaySolvedRevealRoutine());
    }

    private IEnumerator PlaySolvedRevealRoutine()
    {
        revealRunning = true;

        if (solvedFx != null)
        {
            solvedFx.SetActive(false);
        }

        if (hintGuide != null)
        {
            hintGuide.SetActive(true);
        }

        if (hintRenderers != null)
        {
            for (int i = 0; i < hintRenderers.Length; i++)
            {
                Renderer renderer = hintRenderers[i];
                if (renderer == null || hintBaseScales == null || i >= hintBaseScales.Length)
                {
                    continue;
                }

                renderer.transform.localScale = new Vector3(
                    hintBaseScales[i].x,
                    hintBaseScales[i].y * fragmentMinScale,
                    hintBaseScales[i].z);
            }
        }

        if (hintGuide != null)
        {
            hintGuide.transform.localScale = hintGuideBaseScale * 0.985f;
        }

        float glowTimer = 0f;
        while (glowTimer < fragmentGlowDuration)
        {
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(glowTimer / fragmentGlowDuration));
            UpdateHintReveal(t, 0f, true);
            glowTimer += Time.unscaledDeltaTime;
            yield return null;
        }

        float revealTimer = 0f;
        while (revealTimer < fragmentRevealDuration)
        {
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(revealTimer / fragmentRevealDuration));
            UpdateHintReveal(1f, t, true);
            revealTimer += Time.unscaledDeltaTime;
            yield return null;
        }

        UpdateHintReveal(1f, 1f, false);

        if (solvedFx != null)
        {
            solvedFx.SetActive(true);
        }

        yield return new WaitForSecondsRealtime(successOverlayDelay);
        if (!suppressSuccessOverlay)
        {
            SetSuccessOverlay(true);
        }
        successRoutine = null;
        revealRunning = false;
    }

    private void UpdateSolvedPresentation()
    {
        if (revealRunning)
        {
            return;
        }

        if (crystalLight != null)
        {
            crystalLight.intensity = 4.8f + Mathf.Sin(Time.unscaledTime * 2.2f) * 0.7f;
        }
    }

    public void SetSuccessOverlay(bool visible)
    {
        if (successOverlay == null)
        {
            return;
        }

        successOverlay.gameObject.SetActive(true);
        successOverlay.alpha = visible ? 1f : 0f;
        successOverlay.interactable = visible;
        successOverlay.blocksRaycasts = visible;
        if (continueButton != null)
        {
            continueButton.interactable = visible;
        }
        if (successTitleText != null)
        {
            successTitleText.text = "HỎA ẤN THỨC TỈNH";
        }
    }

    private void SetInstruction(string message)
    {
        if (instructionText != null)
        {
            instructionText.text = message;
        }
    }

    private void SetStatus(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }
    }

    private void PlayClip(AudioClip clip, float volume)
    {
        if (feedbackSource != null && clip != null)
        {
            float finalVolume = AudioManager.HasInstance ? volume * AudioManager.Instance.GetEffectiveSFXVolume() : volume;
            feedbackSource.PlayOneShot(clip, finalVolume);
        }
    }

    private void CacheHintRevealState()
    {
        if (hintGuide == null)
        {
            hintRenderers = null;
            hintInitialBlocks = null;
            hintWorkingBlocks = null;
            hintBaseEmissions = null;
            hintBaseScales = null;
            return;
        }

        hintGuideBaseScale = hintGuide.transform.localScale;
        hintRenderers = hintGuide.GetComponentsInChildren<Renderer>(true);
        hintInitialBlocks = new MaterialPropertyBlock[hintRenderers.Length];
        hintWorkingBlocks = new MaterialPropertyBlock[hintRenderers.Length];
        hintBaseEmissions = new Color[hintRenderers.Length];
        hintBaseScales = new Vector3[hintRenderers.Length];

        for (int i = 0; i < hintRenderers.Length; i++)
        {
            Renderer renderer = hintRenderers[i];
            hintInitialBlocks[i] = new MaterialPropertyBlock();
            hintWorkingBlocks[i] = new MaterialPropertyBlock();
            if (renderer == null)
            {
                continue;
            }

            renderer.GetPropertyBlock(hintInitialBlocks[i]);
            hintBaseScales[i] = renderer.transform.localScale;
            hintBaseEmissions[i] = renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_EmissionColor")
                ? renderer.sharedMaterial.GetColor("_EmissionColor")
                : Ember;
        }
    }

    private void RestoreHintRevealState()
    {
        if (hintGuide != null)
        {
            hintGuide.transform.localScale = hintGuideBaseScale;
        }

        if (hintRenderers == null)
        {
            return;
        }

        for (int i = 0; i < hintRenderers.Length; i++)
        {
            Renderer renderer = hintRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (hintInitialBlocks != null && i < hintInitialBlocks.Length && hintInitialBlocks[i] != null)
            {
                renderer.SetPropertyBlock(hintInitialBlocks[i]);
            }
            else
            {
                renderer.SetPropertyBlock(null);
            }

            if (hintBaseScales != null && i < hintBaseScales.Length)
            {
                renderer.transform.localScale = hintBaseScales[i];
            }
        }
    }

    private void UpdateHintReveal(float glowT, float revealT, bool keepHintPulsing)
    {
        if (hintRenderers != null)
        {
            for (int i = 0; i < hintRenderers.Length; i++)
            {
                Renderer renderer = hintRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                float segmentDelay = hintRenderers.Length <= 1 ? 0f : (float)i / (hintRenderers.Length - 1);
                float segmentT = Mathf.Clamp01((revealT - segmentDelay) / Mathf.Max(0.0001f, 1f - segmentDelay));
                float eased = Mathf.SmoothStep(0f, 1f, segmentT);
                float scaleY = Mathf.Lerp(fragmentMinScale, 1f, eased);
                Vector3 baseScale = hintBaseScales != null && i < hintBaseScales.Length ? hintBaseScales[i] : renderer.transform.localScale;
                renderer.transform.localScale = new Vector3(baseScale.x, baseScale.y * scaleY, baseScale.z);

                Color baseEmission = hintBaseEmissions != null && i < hintBaseEmissions.Length ? hintBaseEmissions[i] : Ember;
                Color emission = Color.Lerp(baseEmission, WarmWhite * 2.3f, Mathf.Max(glowT, eased));
                ApplyEmission(renderer, hintWorkingBlocks != null && i < hintWorkingBlocks.Length ? hintWorkingBlocks[i] : null, emission);
            }
        }

        if (hintGuide != null && keepHintPulsing)
        {
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 18f) * 0.018f;
            hintGuide.transform.localScale = hintGuideBaseScale * Mathf.Lerp(0.985f, 1.02f, glowT) * pulse;
        }
    }

    private void ApplyEmission(Renderer renderer, MaterialPropertyBlock block, Color color)
    {
        if (renderer == null)
        {
            return;
        }

        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }

        renderer.GetPropertyBlock(block);
        block.SetColor("_EmissionColor", color);
        renderer.SetPropertyBlock(block);
    }

    private void OnValidate()
    {
        keyboardRotationSpeed = Mathf.Max(1f, keyboardRotationSpeed);
        dragRotationSpeed = Mathf.Max(0.01f, dragRotationSpeed);
        solveTolerance = Mathf.Max(0.1f, solveTolerance);
        solveHoldDuration = Mathf.Max(0.05f, solveHoldDuration);
        angleForEmptyMeter = Mathf.Max(1f, angleForEmptyMeter);
        ambienceVolume = Mathf.Clamp01(ambienceVolume);
        rotateVolume = Mathf.Clamp01(rotateVolume);
        nearVolume = Mathf.Clamp01(nearVolume);
        solvedVolume = Mathf.Clamp01(solvedVolume);
        rotateSoundCooldown = Mathf.Max(0.02f, rotateSoundCooldown);
        fragmentGlowDuration = Mathf.Max(0.05f, fragmentGlowDuration);
        fragmentRevealDuration = Mathf.Max(0.05f, fragmentRevealDuration);
        fragmentMinScale = Mathf.Max(0.01f, fragmentMinScale);
        successOverlayDelay = Mathf.Max(0.05f, successOverlayDelay);
    }
}
