using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Bao.IceSigil
{
    public class IceCarvingMinigameManager : MonoBehaviour
    {
        private enum Phase
        {
            Memorize,
            Carving,
            Prying,
            Won,
            Failed
        }

        private enum Tool
        {
            Chisel,
            Wedge
        }

        [Header("Templates")]
        [SerializeField] private ShapeTemplateData[] templates;
        [SerializeField] private int fallbackResolution = 256;
        [SerializeField] private bool randomizeTemplate = true;

        [Header("Feel")]
        #pragma warning disable CS0414
        [SerializeField] private float carveHitRadiusUv = 0.045f;
        #pragma warning restore CS0414
        [SerializeField] private float pryHitRadiusUv = 0.075f;
        [SerializeField] private float wrongCarveShake = 5f;
        [SerializeField] private float pryFailShake = 8f;
        [SerializeField] private float overlayUpdateInterval = 0.08f;

        private Canvas canvas;
        private Camera inputCamera;
        private RectTransform rootRect;
        private RectTransform playArea;
        private RectTransform carvingSurface;
        private RectTransform traceGuide;
        private RectTransform tracePathControls;
        private RectTransform shardsRoot;
        private RectTransform maskRoot;
        private RawImage sigilAura;
        private RawImage sigilShadow;
        private RawImage sigilImage;
        private RawImage releasedSigilImage;
        private RawImage templateOverlay;
        private RawImage carveOverlay;
        private RawImage toolCursor;
        private RectTransform pointerReticle;
        private Image[] pryAnchors;
        private Image[] toolSlots;
        private RawImage[] toolIcons;
        private TextMeshProUGUI titleText;

        private IceCarvingUIController ui;
        private CarvingMaskPainter painter;
        private ShapeEvaluator evaluator;
        private LocalIceStressMap stress;
        private PryToolController pryTool;
        private CameraFeedbackController cameraFeedback;
        private CarveVFXController vfx;
        private CarveAudioController audioFeedback;
        private ShapeTemplateRuntime activeTemplate;
        private ShapeEvaluationResult lastEvaluation = ShapeEvaluationResult.Empty;

        private Phase phase;
        private Tool selectedTool;
        private int activeTemplateIndex;
        private float phaseTimer;
        private float nextEvaluationTime;
        private float nextOverlayUpdateTime;
        private Vector2 previousMousePosition;
        private bool hasPreviousMousePosition;
        private bool isCarving;
        private Texture2D templateTexture;

        private static readonly Color Cyan = new(0.38f, 0.95f, 1f, 1f);
        private static readonly Color PaleCyan = new(0.78f, 1f, 1f, 1f);
        private static readonly Color Danger = new(1f, 0.28f, 0.30f, 1f);
        private static readonly Color Success = new(0.46f, 1f, 0.72f, 1f);

        private void Awake()
        {
            CacheReferences();
            EnsureRuntimeObjects();
            ResetGame();
        }

        private void OnEnable()
        {
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            Cursor.visible = true;
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (mouse == null || keyboard == null || activeTemplate == null)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;
            phaseTimer += dt;
            audioFeedback?.Update(dt);
            cameraFeedback?.Update(dt);

            Vector2 mousePosition = mouse.position.ReadValue();
            float mouseSpeed = GetMouseSpeed(mousePosition, dt);
            UpdateToolCursor(mousePosition, mouseSpeed);

            if (keyboard.rKey.wasPressedThisFrame)
            {
                ResetGame();
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (keyboard.dKey.wasPressedThisFrame)
            {
                BeginPryingPhase(true);
                return;
            }
#endif

            if (phase is Phase.Won or Phase.Failed)
            {
                return;
            }

            HandleToolHotkeys(keyboard);
            if (HandleToolTrayClick(mouse, mousePosition))
            {
                return;
            }

            switch (phase)
            {
                case Phase.Memorize:
                    UpdateMemorizePhase();
                    break;
                case Phase.Carving:
                    UpdateCarvingPhase(mouse, keyboard, mousePosition, mouseSpeed);
                    break;
                case Phase.Prying:
                    UpdatePryingPhase(mouse, mousePosition);
                    break;
            }

            ProcessCrackEvents();
            if (stress.TotalCrackAmount >= 100f && phase is not Phase.Failed)
            {
                Fail("MAT BANG DA VO", "Do nut dat toi gioi han\nNhan R de thu lai");
            }
        }

        private void CacheReferences()
        {
            canvas = GetComponent<Canvas>();
            rootRect = transform as RectTransform;
            inputCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            playArea = FindRect("PlayArea");
            sigilAura = Find<RawImage>("PlayArea/SigilAura");
            sigilShadow = Find<RawImage>("PlayArea/SigilShadow");
            sigilImage = Find<RawImage>("PlayArea/Sigil");
            releasedSigilImage = Find<RawImage>("PlayArea/ReleasedSigil");
            carvingSurface = sigilImage != null ? sigilImage.rectTransform : FindRect("PlayArea/TraceGuide");
            traceGuide = FindRect("PlayArea/TraceGuide");
            tracePathControls = FindRect("PlayArea/TracePathControls");
            shardsRoot = FindRect("PlayArea/Shards");
            toolCursor = Find<RawImage>("PlayArea/ToolCursor");
            pointerReticle = FindRect("PlayArea/PointerReticle");
            titleText = Find<TextMeshProUGUI>("Texts/Title");

            ui = new IceCarvingUIController(transform);
            cameraFeedback = new CameraFeedbackController(rootRect);
            audioFeedback = new CarveAudioController(gameObject);

            pryAnchors = new Image[5];
            for (int i = 0; i < pryAnchors.Length; i++)
            {
                pryAnchors[i] = Find<Image>($"PlayArea/PryAnchors/Anchor_{i}");
            }

            toolSlots = new Image[2];
            toolIcons = new RawImage[2];
            for (int i = 0; i < toolSlots.Length; i++)
            {
                toolSlots[i] = Find<Image>($"ToolTray/ToolSlot_{i}");
                toolIcons[i] = Find<RawImage>($"ToolTray/ToolSlot_{i}/Icon");
            }
        }

        private void EnsureRuntimeObjects()
        {
            if (playArea == null)
            {
                return;
            }

            if (traceGuide != null)
            {
                traceGuide.gameObject.SetActive(false);
            }

            if (tracePathControls != null)
            {
                tracePathControls.gameObject.SetActive(false);
            }

            maskRoot = FindRect("PlayArea/RuntimeMaskOverlays");
            if (maskRoot == null)
            {
                GameObject maskObject = new("RuntimeMaskOverlays", typeof(RectTransform));
                maskRoot = maskObject.GetComponent<RectTransform>();
                maskRoot.SetParent(playArea, false);
                Stretch(maskRoot);
            }

            templateOverlay = EnsureRawImage("TemplateOverlay", 0);
            carveOverlay = EnsureRawImage("CarveOverlay", 1);

            if (shardsRoot == null)
            {
                GameObject shardsObject = new("Shards", typeof(RectTransform));
                shardsRoot = shardsObject.GetComponent<RectTransform>();
                shardsRoot.SetParent(playArea, false);
                Stretch(shardsRoot);
            }

            vfx = new CarveVFXController(this, shardsRoot);
        }

        private RawImage EnsureRawImage(string name, int siblingIndex)
        {
            RectTransform rect = maskRoot.Find(name) as RectTransform;
            if (rect == null)
            {
                GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                rect = gameObject.GetComponent<RectTransform>();
                rect.SetParent(maskRoot, false);
            }

            RawImage image = rect.GetComponent<RawImage>();
            MatchCarvingSurface(rect);
            image.raycastTarget = false;
            rect.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, maskRoot.childCount - 1));
            return image;
        }

        public void ResetGame()
        {
            ClearRuntimeVfx();
            activeTemplateIndex = PickTemplateIndex();
            Vector2[] scenePolyline = ReadScenePolyline(activeTemplateIndex);
            Vector2[] scenePryPoints = ReadScenePryPoints(activeTemplateIndex);
            ShapeTemplateData templateData = templates != null && activeTemplateIndex < templates.Length ? templates[activeTemplateIndex] : null;
            activeTemplate = ShapeTemplateRuntime.FromDataOrPolyline(templateData, scenePolyline, scenePryPoints, fallbackResolution, activeTemplateIndex);

            painter = new CarvingMaskPainter(activeTemplate.Resolution);
            evaluator = new ShapeEvaluator(activeTemplate.Resolution);
            stress = new LocalIceStressMap(activeTemplate.Resolution);
            pryTool = new PryToolController(5);
            lastEvaluation = ShapeEvaluationResult.Empty;
            templateTexture = BuildTemplateTexture(activeTemplate);
            templateOverlay.texture = templateTexture;
            carveOverlay.texture = painter.OverlayTexture;
            painter.Clear();
            stress.Clear();
            MatchCarvingSurface(templateOverlay.rectTransform);
            MatchCarvingSurface(carveOverlay.rectTransform);

            SetSigilVariant(activeTemplateIndex);
            ApplySigilVisualState();
            SetPryAnchorsVisible(false);
            ui.HideResult();
            SelectTool(Tool.Chisel);
            phase = Phase.Memorize;
            phaseTimer = 0f;
            nextEvaluationTime = 0f;
            nextOverlayUpdateTime = 0f;
            isCarving = false;
            hasPreviousMousePosition = false;
            SetTemplateOverlayAlpha(0.42f);
            ui.SetPhase("GHI NHO DAU AN", "Quan sat mau roi tu khac lai len mat bang", "Dung chuot trai de khac khi mau mo di");
            ui.UpdateCarvingMeters(stress.TotalCrackAmount, lastEvaluation);
            Cursor.visible = false;
        }

        private void UpdateMemorizePhase()
        {
            float reveal = Mathf.Max(0.25f, activeTemplate.SampleRevealTime);
            float t = Mathf.Clamp01(phaseTimer / reveal);
            float targetAlpha = activeTemplate.Difficulty switch
            {
                IceCarvingDifficulty.Easy => 0.62f,
                IceCarvingDifficulty.Normal => Mathf.Lerp(0.72f, 0.18f, t),
                _ => Mathf.Lerp(0.72f, 0f, t)
            };
            SetTemplateOverlayAlpha(targetAlpha);

            if (phaseTimer >= reveal)
            {
                phase = Phase.Carving;
                phaseTimer = 0f;
                ui.SetPhase("KHAC HINH TU DO", "Giu chuot trai de khac; tha chuot hoac nhan Space de kiem tra", "Khac dung vung se sang lanh, khac lech se gay nut cuc bo");
            }
        }

        private void UpdateCarvingPhase(Mouse mouse, Keyboard keyboard, Vector2 mousePosition, float mouseSpeed)
        {
            if (selectedTool != Tool.Chisel)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    ui.SetStatus("Giai doan nay can dung duc de khac");
                }

                return;
            }

            bool hasUv = TryScreenToCarveUv(mousePosition, out Vector2 uv);
            if (mouse.leftButton.wasPressedThisFrame && hasUv)
            {
                isCarving = true;
                painter.BeginStroke(uv);
            }

            if (isCarving && mouse.leftButton.isPressed && hasUv)
            {
                CarveStrokeSample sample = painter.PaintTo(uv, mouseSpeed, activeTemplate);
                stress.AddStrokeStress(sample, mouseSpeed);
                bool valid = sample.OutsideRatio <= 0.60f;
                Vector2 local = UvToVfxLocal(sample.uv);
                vfx.EmitCarveContact(local, valid, sample.deepPixels > sample.touchedPixels * 0.28f, mouseSpeed);
                audioFeedback.PlayScrape(!valid || mouseSpeed > 950f, valid ? 0.22f : 0.48f);
                if (!valid)
                {
                    cameraFeedback.Shake(wrongCarveShake, 0.07f);
                    ui.SetStatus("Mui duc bi lech - vet nut bat dau lan ra");
                }
                else if (sample.RepeatRatio > 0.70f)
                {
                    ui.SetStatus("Khac lai qua nhieu lam bang yeu di");
                }
                else
                {
                    ui.SetStatus("Ranh khac dang an sau vao mat bang");
                }

                if (Time.unscaledTime >= nextOverlayUpdateTime)
                {
                    carveOverlay.texture = painter.BuildOverlay(activeTemplate, stress.DamageMask);
                    nextOverlayUpdateTime = Time.unscaledTime + overlayUpdateInterval;
                }
            }

            if (isCarving && mouse.leftButton.wasReleasedThisFrame)
            {
                isCarving = false;
                painter.EndStroke();
                carveOverlay.texture = painter.BuildOverlay(activeTemplate, stress.DamageMask);
                EvaluateAndMaybeAdvance(true);
            }

            if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)
            {
                carveOverlay.texture = painter.BuildOverlay(activeTemplate, stress.DamageMask);
                EvaluateAndMaybeAdvance(true);
            }

            if (Time.unscaledTime >= nextEvaluationTime)
            {
                EvaluateAndMaybeAdvance(false);
                nextEvaluationTime = Time.unscaledTime + activeTemplate.EvaluationInterval;
            }

            ui.UpdateCarvingMeters(stress.TotalCrackAmount, lastEvaluation);
        }

        private void EvaluateAndMaybeAdvance(bool fullCheck)
        {
            lastEvaluation = evaluator.Evaluate(activeTemplate, painter.PlayerCarveMask, painter.DepthMask, stress.DamageMask, stress.TotalCrackAmount);
            if (fullCheck)
            {
                if (lastEvaluation.passed)
                {
                    BeginPryingPhase(false);
                }
                else
                {
                    ui.SetStatus(lastEvaluation.failureReason);
                }
            }
        }

        private void BeginPryingPhase(bool debugSolve)
        {
            phase = Phase.Prying;
            phaseTimer = 0f;
            isCarving = false;
            painter.EndStroke();
            if (debugSolve)
            {
                lastEvaluation = new ShapeEvaluationResult(1f, 0f, 1f, 1f, 1f, true, "");
            }

            pryTool.Configure(activeTemplate, stress.TotalCrackAmount);
            SelectTool(Tool.Wedge);
            SetTemplateOverlayAlpha(0.08f);
            SetPryAnchorsVisible(true);
            UpdatePryAnchorVisuals();
            ui.SetPhase("CAY TACH AN BANG", "Chon diem cay, giu luc va nha trong vung an toan", "Diem co mau am hon se rui ro hon");
            ui.UpdatePryMeters(stress.TotalCrackAmount, 0f, activeTemplate.BaseSafeForceMin, activeTemplate.BaseSafeForceMax, pryTool.SuccessCount, pryTool.RequiredSuccesses);
        }

        private void UpdatePryingPhase(Mouse mouse, Vector2 mousePosition)
        {
            if (selectedTool != Tool.Wedge)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    ui.SetStatus("Can dung nem de tach an bang");
                }

                return;
            }

            TryScreenToCarveUv(mousePosition, out Vector2 uv);
            PryAttemptResult result = pryTool.Update(uv, mouse.leftButton.wasPressedThisFrame, mouse.leftButton.isPressed, mouse.leftButton.wasReleasedThisFrame, Time.unscaledTime, pryHitRadiusUv);
            if (result.state == PryAttemptState.Started)
            {
                ui.SetStatus("Giu luc va canh vung xanh");
            }
            else if (result.state == PryAttemptState.Holding)
            {
                ui.SetStatus("Khe bang dang mo dan");
            }
            else if (result.state == PryAttemptState.Success)
            {
                Vector2 local = UvToVfxLocal(result.uv);
                vfx.EmitPryBurst(local, true);
                audioFeedback.PlayPrySuccess();
                cameraFeedback.Shake(4f, 0.08f);
                ui.SetStatus($"Da tach duoc {pryTool.SuccessCount}/{pryTool.RequiredSuccesses} diem");
                if (pryTool.SuccessCount >= pryTool.RequiredSuccesses)
                {
                    Win();
                    return;
                }
            }
            else if (result.IsFailure)
            {
                float penalty = result.state == PryAttemptState.TooHigh ? 18f : 9f;
                stress.AddPryStress(result.uv, penalty);
                vfx.EmitPryBurst(UvToVfxLocal(result.uv), false);
                audioFeedback.PlayCrack(0.82f);
                cameraFeedback.Shake(pryFailShake, 0.12f);
                ui.SetStatus(result.state == PryAttemptState.TooHigh ? "Luc qua manh - bang bi ran" : "Luc qua yeu - khoa bang bat lai");
            }

            UpdatePryAnchorVisuals();
            float safeMin = result.safeMin > 0f ? result.safeMin : activeTemplate.BaseSafeForceMin;
            float safeMax = result.safeMax > 0f ? result.safeMax : activeTemplate.BaseSafeForceMax;
            ui.UpdatePryMeters(stress.TotalCrackAmount, pryTool.Force, safeMin, safeMax, pryTool.SuccessCount, pryTool.RequiredSuccesses);
        }

        private void Win()
        {
            phase = Phase.Won;
            isCarving = false;
            Cursor.visible = true;
            SetPryAnchorsVisible(false);
            if (sigilImage != null)
            {
                sigilImage.gameObject.SetActive(false);
            }

            if (releasedSigilImage != null)
            {
                releasedSigilImage.gameObject.SetActive(true);
                releasedSigilImage.color = Color.white;
                releasedSigilImage.rectTransform.localScale = Vector3.one * 1.05f;
            }

            vfx.EmitPryBurst(UvToVfxLocal(new Vector2(0.5f, 0.5f)), true);
            ui.ShowResult(true, "AN BANG DA DUOC GIAI PHONG",
                $"Diem {Mathf.RoundToInt(lastEvaluation.score * 100f)} | Nut con lai {Mathf.RoundToInt(100f - stress.TotalCrackAmount)}%\nNhan R de thu lai");
        }

        private void Fail(string title, string subtitle)
        {
            phase = Phase.Failed;
            isCarving = false;
            Cursor.visible = true;
            SetPryAnchorsVisible(false);
            audioFeedback.PlayShatter();
            cameraFeedback.Shake(14f, 0.24f);
            vfx.EmitPryBurst(UvToVfxLocal(new Vector2(0.5f, 0.5f)), false);
            if (sigilImage != null)
            {
                sigilImage.color = new Color(0.68f, 0.83f, 0.88f, 0.72f);
                sigilImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -7f);
            }

            ui.ShowResult(false, title, subtitle);
        }

        private void ProcessCrackEvents()
        {
            if (stress == null || stress.PendingCracks.Count == 0)
            {
                return;
            }

            foreach (LocalCrackEvent crack in stress.PendingCracks)
            {
                vfx.EmitCrack(UvToVfxLocal(crack.uv), crack.direction, crack.severity);
                audioFeedback.PlayCrack(Mathf.Lerp(0.35f, 0.85f, crack.severity));
            }

            carveOverlay.texture = painter.BuildOverlay(activeTemplate, stress.DamageMask);
            stress.ConsumePendingCracks();
        }

        private void SelectTool(Tool tool)
        {
            selectedTool = tool;
            if (toolCursor != null)
            {
                toolCursor.uvRect = tool == Tool.Chisel
                    ? new Rect(0f, 0.5f, 0.5f, 0.5f)
                    : new Rect(0f, 0f, 0.5f, 0.5f);
            }

            for (int i = 0; i < toolSlots.Length; i++)
            {
                if (toolSlots[i] == null)
                {
                    continue;
                }

                bool selected = i == (int)tool;
                toolSlots[i].color = selected ? new Color(0.36f, 0.96f, 1f, 0.20f) : new Color(0.01f, 0.15f, 0.20f, 0.12f);
                toolSlots[i].rectTransform.localScale = Vector3.one * (selected ? 1.08f : 1f);
                if (toolIcons[i] != null)
                {
                    toolIcons[i].color = selected ? Color.white : new Color(0.76f, 0.90f, 0.92f, 0.82f);
                }
            }
        }

        private bool HandleToolTrayClick(Mouse mouse, Vector2 mousePosition)
        {
            if (!mouse.leftButton.wasPressedThisFrame)
            {
                return false;
            }

            for (int i = 0; i < toolSlots.Length; i++)
            {
                if (toolSlots[i] != null && RectTransformUtility.RectangleContainsScreenPoint(toolSlots[i].rectTransform, mousePosition, inputCamera))
                {
                    SelectTool((Tool)i);
                    return true;
                }
            }

            return false;
        }

        private void HandleToolHotkeys(Keyboard keyboard)
        {
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            {
                SelectTool(Tool.Chisel);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            {
                SelectTool(Tool.Wedge);
            }
        }

        private void UpdateToolCursor(Vector2 screenPosition, float speed)
        {
            if (toolCursor == null || playArea == null)
            {
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(playArea, screenPosition, inputCamera, out Vector2 local);
            float jitter = isCarving ? Mathf.InverseLerp(780f, 1600f, speed) * 3.5f : 0f;
            toolCursor.rectTransform.anchoredPosition = local + new Vector2(34f, -34f) + Random.insideUnitCircle * jitter;
            toolCursor.rectTransform.localEulerAngles = new Vector3(0f, 0f, selectedTool == Tool.Chisel ? -22f : 8f);
            if (pointerReticle != null)
            {
                pointerReticle.anchoredPosition = local;
            }
        }

        private float GetMouseSpeed(Vector2 mousePosition, float deltaTime)
        {
            float speed = 0f;
            if (hasPreviousMousePosition && deltaTime > 0f)
            {
                speed = Vector2.Distance(mousePosition, previousMousePosition) / deltaTime;
            }

            previousMousePosition = mousePosition;
            hasPreviousMousePosition = true;
            return speed;
        }

        private bool TryScreenToCarveUv(Vector2 screenPosition, out Vector2 uv)
        {
            uv = Vector2.zero;
            if (carvingSurface == null)
            {
                return false;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(carvingSurface, screenPosition, inputCamera, out Vector2 local);
            Rect rect = carvingSurface.rect;
            uv = new Vector2(
                Mathf.InverseLerp(rect.xMin, rect.xMax, local.x),
                Mathf.InverseLerp(rect.yMin, rect.yMax, local.y));
            return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
        }

        private Vector2 UvToVfxLocal(Vector2 uv)
        {
            RectTransform targetRoot = shardsRoot != null ? shardsRoot : playArea;
            if (carvingSurface == null || targetRoot == null)
            {
                return Vector2.zero;
            }

            Rect rect = carvingSurface.rect;
            Vector2 local = new(Mathf.Lerp(rect.xMin, rect.xMax, uv.x), Mathf.Lerp(rect.yMin, rect.yMax, uv.y));
            Vector3 world = carvingSurface.TransformPoint(local);
            return targetRoot.InverseTransformPoint(world);
        }

        private Vector2[] ReadScenePolyline(int variant)
        {
            Transform group = tracePathControls != null ? tracePathControls.Find($"Variant_{variant % 3}") : null;
            if (group == null && tracePathControls != null && tracePathControls.childCount > 0)
            {
                group = tracePathControls.GetChild(variant % tracePathControls.childCount);
            }
            if (group == null || group.childCount < 2)
            {
                return null;
            }

            RectTransform guideForMapping = traceGuide != null ? traceGuide : group as RectTransform;
            RectTransform targetSurface = carvingSurface != null ? carvingSurface : sigilImage != null ? sigilImage.rectTransform : guideForMapping;
            Rect targetRect = targetSurface != null ? targetSurface.rect : new Rect(-290f, -290f, 580f, 580f);
            Vector2[] points = new Vector2[group.childCount];
            int count = 0;
            for (int i = 0; i < group.childCount; i++)
            {
                if (group.GetChild(i) is RectTransform point)
                {
                    Vector3 world = guideForMapping != null
                        ? guideForMapping.TransformPoint(point.anchoredPosition)
                        : group.TransformPoint(point.anchoredPosition);
                    Vector2 targetLocal = targetSurface != null
                        ? targetSurface.InverseTransformPoint(world)
                        : point.anchoredPosition;
                    points[count++] = new Vector2(
                        Mathf.Clamp01(Mathf.InverseLerp(targetRect.xMin, targetRect.xMax, targetLocal.x)),
                        Mathf.Clamp01(Mathf.InverseLerp(targetRect.yMin, targetRect.yMax, targetLocal.y)));
                }
            }

            if (count != points.Length)
            {
                System.Array.Resize(ref points, count);
            }

            return points;
        }

        private Vector2[] ReadScenePryPoints(int variant)
        {
            Vector2[] line = ReadScenePolyline(variant);
            return line == null || line.Length == 0 ? null : null;
        }

        private Texture2D BuildTemplateTexture(ShapeTemplateRuntime template)
        {
            int resolution = template.Resolution;
            Color32[] pixels = new Color32[resolution * resolution];
            for (int i = 0; i < pixels.Length; i++)
            {
                if (template.TargetLineMask[i] > 0)
                {
                    pixels[i] = new Color32(188, 255, 255, 118);
                }
                else
                {
                    pixels[i] = new Color32(0, 0, 0, 0);
                }
            }

            Texture2D texture = new(resolution, resolution, TextureFormat.RGBA32, false)
            {
                name = "Runtime_IceCarvingTemplate",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private void SetTemplateOverlayAlpha(float alpha)
        {
            if (templateOverlay != null)
            {
                templateOverlay.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }
        }

        private void SetPryAnchorsVisible(bool visible)
        {
            for (int i = 0; i < pryAnchors.Length; i++)
            {
                if (pryAnchors[i] != null)
                {
                    pryAnchors[i].gameObject.SetActive(visible);
                }
            }
        }

        private void UpdatePryAnchorVisuals()
        {
            if (pryTool == null)
            {
                return;
            }

            PryToolController.PryPointState[] points = pryTool.Points;
            for (int i = 0; i < pryAnchors.Length && i < points.Length; i++)
            {
                Image anchor = pryAnchors[i];
                if (anchor == null)
                {
                    continue;
                }

                anchor.gameObject.SetActive(phase == Phase.Prying && !points[i].completed);
                anchor.rectTransform.anchoredPosition = UvToVfxLocal(points[i].uv);
                anchor.color = Color.Lerp(PaleCyan, Danger, points[i].risk * 0.75f);
                anchor.rectTransform.localScale = Vector3.one * (points[i].completed ? 0.7f : 1.05f + Mathf.Sin(Time.unscaledTime * 4f + i) * 0.08f);
            }
        }

        private int PickTemplateIndex()
        {
            int templateCount = templates != null && templates.Length > 0 ? templates.Length : 3;
            if (!randomizeTemplate)
            {
                return Mathf.Clamp(activeTemplateIndex, 0, templateCount - 1);
            }

            return Random.Range(0, templateCount);
        }

        private void SetSigilVariant(int index)
        {
            Rect uv = (index % 3) switch
            {
                1 => new Rect(0f, 0f, 0.5f, 0.5f),
                2 => new Rect(0.5f, 0f, 0.5f, 0.5f),
                _ => new Rect(0.5f, 0.5f, 0.5f, 0.5f)
            };

            if (sigilImage != null)
            {
                sigilImage.uvRect = uv;
            }

            if (releasedSigilImage != null)
            {
                releasedSigilImage.uvRect = uv;
            }

            if (sigilAura != null)
            {
                sigilAura.uvRect = uv;
            }

            if (sigilShadow != null)
            {
                sigilShadow.uvRect = uv;
            }
        }

        private void ApplySigilVisualState()
        {
            if (titleText != null)
            {
                titleText.text = "TACH AN BANG";
            }

            if (sigilImage != null)
            {
                sigilImage.gameObject.SetActive(true);
                sigilImage.color = new Color(0.82f, 0.96f, 1f, 0.50f);
                sigilImage.rectTransform.localScale = Vector3.one;
                sigilImage.rectTransform.localRotation = Quaternion.identity;
            }

            if (releasedSigilImage != null)
            {
                releasedSigilImage.gameObject.SetActive(false);
                releasedSigilImage.color = Color.white;
                releasedSigilImage.rectTransform.localScale = Vector3.one;
                releasedSigilImage.rectTransform.localRotation = Quaternion.identity;
            }

            if (carveOverlay != null)
            {
                carveOverlay.color = Color.white;
            }
        }

        private void ClearRuntimeVfx()
        {
            if (shardsRoot == null)
            {
                return;
            }

            for (int i = shardsRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(shardsRoot.GetChild(i).gameObject);
            }
        }

        private void MatchCarvingSurface(RectTransform rect)
        {
            if (rect == null || carvingSurface == null)
            {
                return;
            }

            rect.anchorMin = carvingSurface.anchorMin;
            rect.anchorMax = carvingSurface.anchorMax;
            rect.pivot = carvingSurface.pivot;
            rect.anchoredPosition = carvingSurface.anchoredPosition;
            rect.sizeDelta = carvingSurface.sizeDelta;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private RectTransform FindRect(string path)
        {
            Transform child = transform.Find(path);
            return child != null ? child.GetComponent<RectTransform>() : null;
        }

        private T Find<T>(string path) where T : Component
        {
            Transform child = transform.Find(path);
            return child != null ? child.GetComponent<T>() : null;
        }
    }

    internal readonly struct LocalCrackEvent
    {
        public readonly Vector2 uv;
        public readonly float severity;
        public readonly Vector2 direction;

        public LocalCrackEvent(Vector2 uv, float severity, Vector2 direction)
        {
            this.uv = uv;
            this.severity = severity;
            this.direction = direction;
        }
    }

    internal sealed class LocalIceStressMap
    {
        private readonly int resolution;
        private readonly float[] stress;
        private readonly bool[] crackTriggered;
        private readonly List<LocalCrackEvent> pendingCracks = new();

        public byte[] DamageMask { get; }
        public float TotalCrackAmount { get; private set; }
        public IReadOnlyList<LocalCrackEvent> PendingCracks => pendingCracks;

        public LocalIceStressMap(int resolution)
        {
            this.resolution = Mathf.Clamp(resolution, 128, 1024);
            int length = this.resolution * this.resolution;
            stress = new float[length];
            crackTriggered = new bool[length];
            DamageMask = new byte[length];
        }

        public void Clear()
        {
            System.Array.Clear(stress, 0, stress.Length);
            System.Array.Clear(crackTriggered, 0, crackTriggered.Length);
            System.Array.Clear(DamageMask, 0, DamageMask.Length);
            pendingCracks.Clear();
            TotalCrackAmount = 0f;
        }

        public void ConsumePendingCracks()
        {
            pendingCracks.Clear();
        }

        public void AddStrokeStress(CarveStrokeSample sample, float speed)
        {
            if (!sample.HasPaint)
            {
                return;
            }

            float outsideStress = Mathf.InverseLerp(0.55f, 1f, sample.OutsideRatio) * 4f;
            float repeatStress = Mathf.InverseLerp(0.75f, 1f, sample.RepeatRatio) * 2f;
            float speedStress = speed > 1450f ? Mathf.InverseLerp(1450f, 2300f, speed) * 3f : 0f;
            float amount = outsideStress + repeatStress + speedStress;
            if (amount <= 0.01f)
            {
                return;
            }

            AddStressCircle(sample.uv, 12f + amount * 0.6f, amount, sample.OutsideRatio > 0.12f ? 1.15f : 0.55f);
        }

        public void AddPryStress(Vector2 uv, float amount)
        {
            AddStressCircle(uv, 26f, amount, 1.35f);
        }

        public void AddStressCircle(Vector2 uv, float radiusPixels, float amount, float globalCrackScale)
        {
            int cx = Mathf.RoundToInt(Mathf.Clamp01(uv.x) * (resolution - 1));
            int cy = Mathf.RoundToInt(Mathf.Clamp01(uv.y) * (resolution - 1));
            int r = Mathf.CeilToInt(radiusPixels);
            float radiusSq = radiusPixels * radiusPixels;

            for (int y = cy - r; y <= cy + r; y++)
            {
                if (y < 0 || y >= resolution)
                {
                    continue;
                }

                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || x >= resolution)
                    {
                        continue;
                    }

                    float dx = x - cx;
                    float dy = y - cy;
                    float distSq = dx * dx + dy * dy;
                    if (distSq > radiusSq)
                    {
                        continue;
                    }

                    int index = y * resolution + x;
                    float falloff = 1f - Mathf.Sqrt(distSq) / Mathf.Max(1f, radiusPixels);
                    stress[index] += amount * falloff;
                    DamageMask[index] = (byte)Mathf.Clamp(DamageMask[index] + amount * falloff * 9f, 0f, 255f);

                    if (!crackTriggered[index] && stress[index] >= 24f)
                    {
                        crackTriggered[index] = true;
                        Vector2 direction = Random.insideUnitCircle.normalized;
                        if (direction.sqrMagnitude < 0.1f)
                        {
                            direction = Vector2.right;
                        }

                        Vector2 crackUv = new(x / (resolution - 1f), y / (resolution - 1f));
                        float severity = Mathf.Clamp01(stress[index] / 60f);
                        pendingCracks.Add(new LocalCrackEvent(crackUv, severity, direction));
                        DrawCrack(crackUv, direction, severity);
                    }
                }
            }

            TotalCrackAmount = Mathf.Clamp(TotalCrackAmount + amount * Mathf.Max(0.15f, globalCrackScale), 0f, 100f);
        }

        private void DrawCrack(Vector2 uv, Vector2 direction, float severity)
        {
            Vector2 start = new(Mathf.Clamp01(uv.x) * (resolution - 1), Mathf.Clamp01(uv.y) * (resolution - 1));
            float length = Mathf.Lerp(18f, 76f, severity);
            int branches = severity > 0.55f ? 3 : 2;

            for (int branch = 0; branch < branches; branch++)
            {
                float angleOffset = branch == 0 ? 0f : Random.Range(-45f, 45f) * Mathf.Deg2Rad;
                Vector2 branchDirection = Rotate(direction, angleOffset).normalized;
                DrawDamageLine(start, start + branchDirection * length * Random.Range(0.55f, 1.1f), Mathf.Lerp(1.2f, 2.4f, severity));
            }
        }

        private void DrawDamageLine(Vector2 from, Vector2 to, float radius)
        {
            float distance = Vector2.Distance(from, to);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 2f));
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(from, to, i / (float)steps);
                PaintDamage(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), radius);
            }
        }

        private void PaintDamage(int cx, int cy, float radius)
        {
            int r = Mathf.CeilToInt(radius);
            float radiusSq = radius * radius;
            for (int y = cy - r; y <= cy + r; y++)
            {
                if (y < 0 || y >= resolution)
                {
                    continue;
                }

                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || x >= resolution)
                    {
                        continue;
                    }

                    float dx = x - cx;
                    float dy = y - cy;
                    if (dx * dx + dy * dy <= radiusSq)
                    {
                        int index = y * resolution + x;
                        DamageMask[index] = (byte)Mathf.Clamp(DamageMask[index] + 70, 0, 255);
                    }
                }
            }
        }

        private static Vector2 Rotate(Vector2 vector, float radians)
        {
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
        }
    }
}
