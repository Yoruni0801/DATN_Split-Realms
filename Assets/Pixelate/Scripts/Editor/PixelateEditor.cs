using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Pixelate
{
    [InitializeOnLoad]
    [CustomEditor(typeof(PixelateCaptureManager))]
    public class PixelateEditor : Editor
    {
        private enum DrawMode
        {
            Lit = 0,
            Unlit = 1,
            Normal = 2,
        }

        private enum PixelateSectionId
        {
            Unknown = 0,
            Camera = 1,
            Animation = 2,
            Preview = 4,
            ColorPalette = 5,
            Lighting = 6,
            ExportSettings = 8,
            HelpLinks = 10,
        }

        [System.Serializable]
        private class PreviewSectionSnapshot
        {
            public DrawMode drawMode;
            public bool loopPreviewAnimation;
        }

        [System.Serializable]
        private class NormalMapSectionSnapshot
        {
            public PixelateProfile.LightingSettings lighting = new PixelateProfile.LightingSettings();
        }

        private static PixelateSectionId copiedSectionId = PixelateSectionId.Unknown;
        private static string copiedSectionJson;
        private static PixelateCaptureManager activeInspectorIsolationManager;
        private static PixelateCaptureManager activeAnimationPreviewPoseManager;
        private static bool targetIsolationCallbacksRegistered;
        private static bool globalSelectionChangeQueued;

        private Texture2D headerTexture;
        private Texture2D pixelateLogo;
        private Rect headerSection;
        private PixelateCaptureManager helper;
        private SerializedObject profileSerializedObject;
        private PixelateProfile profileSerializedObjectTarget;
        private IEnumerator currentCaptureRoutine;
        private ReorderableList sourceClipsList;
        private string cellSizeXText;
        private string cellSizeYText;
        private bool suppressCellSizeTextSync;

        private static readonly Vector3 FixedPreviewLightSourceDirection = new Vector3(-0.45f, 0.65f, 0.6f).normalized;
        private static readonly Color FixedPreviewAmbientColor = new Color(113f / 255f, 113f / 255f, 113f / 255f);
        private static readonly Color FixedPreviewLightColor = Color.white;
        private const float FixedPreviewLightIntensity = 1.0f;

        private DrawMode drawMode = DrawMode.Lit;
        private Texture2D cachedLitPreviewImage;
        private Texture2D cachedLitPreviewSource;
        private Texture2D cachedLitPreviewNormalSource;
        private Texture2D animationPreviewDiffuseAtlas;
        private Texture2D animationPreviewNormalAtlas;
        private Texture2D animationPreviewLitAtlas;
        private int animationPreviewFrameCount;
        private int animationPreviewFramesPerSecond;
        private Vector2Int animationPreviewCellSize;
        private double animationPreviewLastUpdateTime;
        private float animationPreviewFrameAccumulator;
        private int currentAnimationPreviewFrameIndex;
        private bool isGeneratingAnimationPreview;
        private bool isPlayingAnimationPreview;
        private bool loopPreviewAnimation = true;
        private bool modeExpanded = true;
        private bool cameraExpanded = true;
        private bool colorExpanded = true;
        private bool lightingExpanded = true;
        private bool previewExpanded = true;
        private bool captureExpanded = true;
        private bool helpExpanded;
        private bool selectionPreviewRefreshQueued;
        private static GUIStyle volumeSmallTickboxStyle;
        private static GUIStyle cellSizeLinkButtonStyle;
        private static bool cellSizeLinkButtonUsesBuiltInHover;
        private const string ManagerFoldoutStatePrefix = "Pixelate.CaptureManager.Foldout.";
        private const string PreviewDrawModePreferenceKey = "Pixelate.CaptureManager.Preview.DrawMode";
        private const string PreviewLoopPreferenceKey = "Pixelate.CaptureManager.Preview.Loop";
        private static readonly GUIContent TargetContent = new GUIContent("Target", "The scene object Pixelate will render into sprites.");
        private static readonly GUIContent CaptureCameraContent = new GUIContent("Capture Camera", "The camera that frames the target for capture.");
        private static readonly GUIContent CellSizeContent = new GUIContent("Cell Size", "The pixel width and height of each captured frame.");
        private static readonly GUIContent PixelatedContent = new GUIContent("Pixelated", "Uses point filtering so exported sprites stay crisp.");
        private static readonly GUIContent ProfileContent = new GUIContent("", "The shared profile asset used by this capture manager.");
        private static readonly GUIContent AutoPaletteContent = new GUIContent("  Auto Generate Palette", "Creates a limited color palette from the capture.");
        private static readonly GUIContent CustomPaletteModeContent = new GUIContent("  Custom Palette", "Uses your palette texture to limit capture colors.");
        private static readonly GUIContent PaletteDetailContent = new GUIContent("Palette Detail", "Controls how many colors the auto palette keeps.");
        private static readonly GUIContent ColorCountContent = new GUIContent("Color Count", "The exact number of colors to keep in the palette.");
        private static readonly GUIContent CustomPaletteContent = new GUIContent("Custom Palette", "A 1-pixel-tall texture containing the colors to use.");
        private static readonly GUIContent PalettePreviewContent = new GUIContent("Palette Preview", "Shows the active palette colors.");
        private static readonly GUIContent LightingStepsContent = new GUIContent("Lighting Steps", "Controls how many stepped normal colors are exported.");
        private static readonly GUIContent MaterialPipelineContent = new GUIContent("Material Pipeline", "Choose the render pipeline your lit sprite material uses.");
        private static readonly GUIContent DrawModeContent = new GUIContent("Draw Mode", "Changes how the preview is shown in the inspector.");
        private static readonly GUIContent PreviewFrameContent = new GUIContent("Preview Frame", "Chooses which animation frame appears in the preview.");
        private static readonly GUIContent SourceClipsContent = new GUIContent("Source Clips", "Animation clips Pixelate will capture into sprite sheets.");
        private static readonly GUIContent SourceClipContent = new GUIContent("", "The animation clip to capture.");
        private static readonly GUIContent SpeedContent = new GUIContent("Speed", "Changes how fast this clip is sampled.");
        private static readonly GUIContent FramesPerSecondContent = new GUIContent("Frames Per Second", "How many animation frames Pixelate captures each second.");
        private static readonly GUIContent PivotContent = new GUIContent("Pivot", "Where each exported sprite pivots when placed in a scene.");
        private static readonly GUIContent PixelsPerUnitContent = new GUIContent("Pixels Per Unit", "How many sprite pixels equal one Unity world unit after export.");
        private static readonly GUIContent OverrideCapturesContent = new GUIContent("Override Captures", "Replaces existing files instead of creating numbered copies.");
        private static readonly GUIContent ExportLocationContent = new GUIContent("Export Location", "The Assets folder where Pixelate saves captured sprites.");

        static PixelateEditor()
        {
            RegisterTargetIsolationCallbacks();
        }

        private void OnEnable()
        {
            LoadFoldoutStates();
            LoadPreviewDrawModePreference();
            LoadPreviewLoopPreference();
            InitTextures();
            helper = (PixelateCaptureManager)target;
            EnsureActiveProfileForInspector();
            InitializeLists();
            SyncCellSizeTextFromActiveCameraSettings();
            if (helper != null)
            {
                helper.PreviewUpdated += HandlePreviewUpdated;
            }
            RegisterTargetIsolationCallbacks();
            RefreshInspectorTargetIsolationForSelection();
            PrimeSelectedAnimationPreviewPose();
            QueueSelectionPreviewRefresh();
            Undo.undoRedoPerformed += HandleUndoRedoPerformed;
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdateAnimationPreviewEditorLoop;
            EditorApplication.delayCall -= HandleSelectionPreviewRefresh;
            selectionPreviewRefreshQueued = false;
            CancelAnimationPreview(true);
            ClearAnimationPreviewPoseIfDeselected();
            ClearInspectorTargetIsolationFor(helper);
            if (helper != null)
            {
                helper.PreviewUpdated -= HandlePreviewUpdated;
            }
            Undo.undoRedoPerformed -= HandleUndoRedoPerformed;

            DestroyCachedLitPreview();
        }

        private void OnInspectorUpdate()
        {
            if (currentCaptureRoutine != null || IsAnimationPreviewLocked())
            {
                Repaint();
            }
        }

        public override void OnInspectorGUI()
        {
            bool inspectorLocked = currentCaptureRoutine != null || isGeneratingAnimationPreview || isPlayingAnimationPreview;
            using (new EditorGUI.DisabledScope(inspectorLocked))
            {
                serializedObject.Update();
                helper = (PixelateCaptureManager)target;
                PixelateProfile activeProfile = EnsureActiveProfileForInspector();
                if (activeProfile != null && activeProfile.Sanitize())
                {
                    SetProfileDirty(activeProfile);
                }
                UpdateProfileSerializedObject(activeProfile);
                bool shouldRefreshPreview = EnsureValidPreviewDrawModeForNormalMap();

                EnsureDefaultExportPath();
                InitiateBanner();

                shouldRefreshPreview |= DrawTopCaptureControls();
                shouldRefreshPreview |= DrawSetupFields();
                if (HasAssignedTarget() == false)
                {
                    ApplyProfileSerializedObjectChanges();
                    ApplySerializedObjectChanges();
                    if (shouldRefreshPreview && inspectorLocked == false)
                    {
                        RequestPreviewRefresh();
                    }

                    GUI.enabled = true;
                    DrawHelpAndLinksSection();
                    return;
                }

                shouldRefreshPreview |= DrawCameraSection();
                if (HasAssignedCamera() == false)
                {
                    ApplyProfileSerializedObjectChanges();
                    ApplySerializedObjectChanges();
                    if (shouldRefreshPreview && inspectorLocked == false)
                    {
                        RequestPreviewRefresh();
                    }

                    GUI.enabled = true;
                    DrawHelpAndLinksSection();
                    return;
                }

                shouldRefreshPreview |= DrawAnimationSection();
                if (ShouldBlockSectionsAfterColorOptions() == false)
                {
                    DrawOutputSection();
                    shouldRefreshPreview |= DrawUnlockedPreviewSection();
                }

                shouldRefreshPreview |= DrawPixelateProfileCard();

                if (profileSerializedObject != null)
                {
                    shouldRefreshPreview |= DrawColorOptionsSection();
                    if (ShouldBlockSectionsAfterColorOptions() == false)
                    {
                        shouldRefreshPreview |= DrawLightingSection();
                    }
                }

                ApplyProfileSerializedObjectChanges();
                ApplySerializedObjectChanges();
                if (shouldRefreshPreview && inspectorLocked == false)
                {
                    RequestPreviewRefresh();
                }

            }

            DrawHelpAndLinksSection();
        }

        private bool DrawUnlockedPreviewSection()
        {
            bool previousGuiEnabled = GUI.enabled;
            GUI.enabled = true;
            bool changed = DrawPreviewSection();
            GUI.enabled = previousGuiEnabled;
            return changed;
        }

        private void HandlePreviewUpdated()
        {
            DestroyCachedLitPreview();
            Repaint();
        }

        private void QueueSelectionPreviewRefresh()
        {
            if (selectionPreviewRefreshQueued)
            {
                return;
            }

            selectionPreviewRefreshQueued = true;
            EditorApplication.delayCall += HandleSelectionPreviewRefresh;
        }

        private void HandleSelectionPreviewRefresh()
        {
            selectionPreviewRefreshQueued = false;
            if (this == null || helper == null)
            {
                return;
            }

            PixelateCaptureManager selectedManager = GetSelectedCaptureManager();
            if (selectedManager != helper)
            {
                return;
            }

            serializedObject.Update();
            helper = (PixelateCaptureManager)target;
            PrimeSelectedAnimationPreviewPose();
            DestroyCachedLitPreview();
            RequestPreviewRefresh();
            Repaint();
        }

        private void HandleUndoRedoPerformed()
        {
            serializedObject.Update();
            helper = (PixelateCaptureManager)target;
            SyncCellSizeTextFromActiveCameraSettings();
            UpdateProfileSerializedObject(EnsureActiveProfileForInspector());
            DestroyCachedLitPreview();
            RefreshInspectorTargetIsolationForSelection(forceRebuild: true);
            RequestPreviewRefresh();
            Repaint();
        }

        private void ApplySerializedObjectChanges()
        {
            if (serializedObject.ApplyModifiedProperties())
            {
                helper = (PixelateCaptureManager)target;
                SyncLocalProfileWrapperIfNeeded();
                RefreshInspectorTargetIsolationForSelection();
            }
        }

        private static void RegisterTargetIsolationCallbacks()
        {
            if (targetIsolationCallbacksRegistered)
            {
                return;
            }

            Selection.selectionChanged += HandleGlobalSelectionChanged;
            EditorApplication.hierarchyChanged += HandleGlobalHierarchyChanged;
            AssemblyReloadEvents.beforeAssemblyReload += ClearActiveAnimationPreviewPose;
            AssemblyReloadEvents.beforeAssemblyReload += ClearActiveInspectorTargetIsolation;
            EditorApplication.playModeStateChanged += HandleGlobalPlayModeStateChanged;
            targetIsolationCallbacksRegistered = true;
        }

        private static void HandleGlobalSelectionChanged()
        {
            if (globalSelectionChangeQueued)
            {
                return;
            }

            globalSelectionChangeQueued = true;
            EditorApplication.delayCall += HandleDeferredGlobalSelectionChanged;
        }

        private static void HandleDeferredGlobalSelectionChanged()
        {
            globalSelectionChangeQueued = false;
            PixelateCaptureManager selectedManager = GetSelectedCaptureManager();
            bool selectedMayPrimeAnimation = selectedManager != null && selectedManager.captureType == PixelateCaptureManager.CaptureType.animation;
            if (activeAnimationPreviewPoseManager != null && activeAnimationPreviewPoseManager != selectedManager)
            {
                ClearActiveAnimationPreviewPose(repaintSceneView: selectedMayPrimeAnimation == false);
            }

            PrimeAnimationPreviewPoseForSelection(selectedManager);

            if (activeInspectorIsolationManager != null && activeInspectorIsolationManager != selectedManager)
            {
                ClearActiveInspectorTargetIsolation();
            }
        }

        private static void PrimeAnimationPreviewPoseForSelection(PixelateCaptureManager selectedManager)
        {
            if (selectedManager == null || selectedManager.captureType != PixelateCaptureManager.CaptureType.animation)
            {
                return;
            }

            selectedManager.BeginAnimationPreviewPoseVisibilityHoldForEditor();
            if (selectedManager.PrimeAnimationPreviewPoseForEditor())
            {
                activeAnimationPreviewPoseManager = selectedManager;
                selectedManager.QueueAnimationPreviewPoseVisibilityReleaseForEditor();
            }
            else
            {
                selectedManager.EndAnimationPreviewPoseVisibilityHoldForEditor(repaintSceneView: false);
            }
        }

        private static void HandleGlobalHierarchyChanged()
        {
            PixelateCaptureManager selectedManager = GetSelectedCaptureManager();
            if (activeInspectorIsolationManager == null || activeInspectorIsolationManager != selectedManager)
            {
                return;
            }

            if (CanIsolateSelectedTarget(activeInspectorIsolationManager) == false)
            {
                ClearActiveInspectorTargetIsolation();
                return;
            }

            activeInspectorIsolationManager.RefreshInspectorTargetIsolation(forceRebuild: true);
            SceneView.RepaintAll();
        }

        private static void HandleGlobalPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
            {
                ClearActiveAnimationPreviewPose();
                ClearActiveInspectorTargetIsolation();
            }
        }

        private void RefreshInspectorTargetIsolationForSelection(bool forceRebuild = false)
        {
            if (helper == null)
            {
                helper = target as PixelateCaptureManager;
            }

            PixelateCaptureManager selectedManager = GetSelectedCaptureManager();
            if (selectedManager != helper || CanIsolateSelectedTarget(helper) == false)
            {
                ClearInspectorTargetIsolationFor(helper);
                return;
            }

            if (activeInspectorIsolationManager != null && activeInspectorIsolationManager != helper)
            {
                ClearActiveInspectorTargetIsolation();
            }

            helper.RefreshInspectorTargetIsolation(forceRebuild);
            activeInspectorIsolationManager = helper;
            SceneView.RepaintAll();
        }

        private void PrimeSelectedAnimationPreviewPose()
        {
            if (helper == null || GetSelectedCaptureManager() != helper || helper.CanGeneratePreview() == false)
            {
                return;
            }

            helper.BeginAnimationPreviewPoseVisibilityHoldForEditor();
            if (helper.PrimeAnimationPreviewPoseForEditor())
            {
                activeAnimationPreviewPoseManager = helper;
                helper.QueueAnimationPreviewPoseVisibilityReleaseForEditor();
            }
            else
            {
                helper.EndAnimationPreviewPoseVisibilityHoldForEditor(repaintSceneView: false);
            }
        }

        private void ClearAnimationPreviewPoseIfDeselected()
        {
            if (helper == null || GetSelectedCaptureManager() == helper)
            {
                return;
            }

            ClearAnimationPreviewPoseFor(helper);
        }

        private static void ClearAnimationPreviewPoseFor(PixelateCaptureManager manager, bool repaintSceneView = true)
        {
            if (manager == null)
            {
                return;
            }

            manager.ClearAnimationPreviewPoseForEditor(repaintSceneView);
            if (activeAnimationPreviewPoseManager == manager)
            {
                activeAnimationPreviewPoseManager = null;
            }
        }

        private static void ClearActiveAnimationPreviewPose()
        {
            ClearActiveAnimationPreviewPose(repaintSceneView: true);
        }

        private static void ClearActiveAnimationPreviewPose(bool repaintSceneView)
        {
            if (activeAnimationPreviewPoseManager == null)
            {
                return;
            }

            ClearAnimationPreviewPoseFor(activeAnimationPreviewPoseManager, repaintSceneView);
        }

        private static void ClearInspectorTargetIsolationFor(PixelateCaptureManager manager)
        {
            if (manager == null || activeInspectorIsolationManager != manager)
            {
                return;
            }

            ClearActiveInspectorTargetIsolation();
        }

        private static void ClearActiveInspectorTargetIsolation()
        {
            if (activeInspectorIsolationManager == null)
            {
                return;
            }

            activeInspectorIsolationManager.ClearInspectorTargetIsolation();
            activeInspectorIsolationManager = null;
            SceneView.RepaintAll();
        }

        private static PixelateCaptureManager GetSelectedCaptureManager()
        {
            GameObject activeGameObject = Selection.activeGameObject;
            return activeGameObject != null ? activeGameObject.GetComponent<PixelateCaptureManager>() : null;
        }

        private static bool CanIsolateSelectedTarget(PixelateCaptureManager manager)
        {
            return manager != null
                && manager.localSections != null
                && manager.localSections.setup != null
                && manager.localSections.setup.target != null;
        }

        private bool IsAnimationPreviewLocked()
        {
            return isGeneratingAnimationPreview || isPlayingAnimationPreview;
        }

        private bool ShouldBlockSectionsAfterColorOptions()
        {
            return false;
        }

        private void StartAnimationPreview()
        {
            if (helper == null || helper.captureType != PixelateCaptureManager.CaptureType.animation || IsAnimationPreviewLocked())
            {
                return;
            }

            ClearAnimationPreviewAtlases();
            isGeneratingAnimationPreview = true;
            animationPreviewFrameCount = 0;
            animationPreviewFramesPerSecond = Mathf.Max(1, serializedObject.FindProperty("localSections.animation.framesPerSecond").intValue);
            animationPreviewCellSize = helper.localSections.camera.cellSize;
            RunRoutine(helper.GenerateAnimationPreviewAtlas(PreviewNeedsDiffuse(), PreviewNeedsNormal(), HandleAnimationPreviewGenerated));
        }

        private void HandleAnimationPreviewGenerated(Texture2D diffuseAtlas, Texture2D normalAtlas, int frameCount, Vector2Int cellSize)
        {
            ClearAnimationPreviewAtlases();
            animationPreviewDiffuseAtlas = diffuseAtlas;
            animationPreviewNormalAtlas = normalAtlas;
            animationPreviewFrameCount = Mathf.Max(1, frameCount);
            animationPreviewFramesPerSecond = Mathf.Max(1, serializedObject.FindProperty("localSections.animation.framesPerSecond").intValue);
            animationPreviewCellSize = cellSize;
            currentAnimationPreviewFrameIndex = GetCurrentSerializedPreviewFrame(animationPreviewFrameCount);
            animationPreviewFrameAccumulator = 0f;
            animationPreviewLastUpdateTime = 0d;
            isGeneratingAnimationPreview = false;
            isPlayingAnimationPreview = true;

            if (animationPreviewDiffuseAtlas != null && animationPreviewNormalAtlas != null)
            {
                animationPreviewLitAtlas = BuildLitAnimationPreviewAtlas(animationPreviewDiffuseAtlas, animationPreviewNormalAtlas);
            }

            EditorApplication.update -= UpdateAnimationPreviewEditorLoop;
            EditorApplication.update += UpdateAnimationPreviewEditorLoop;
            Repaint();
        }

        private void UpdateAnimationPreviewEditorLoop()
        {
            if (isGeneratingAnimationPreview == false && isPlayingAnimationPreview == false)
            {
                EditorApplication.update -= UpdateAnimationPreviewEditorLoop;
                return;
            }

            UpdateAnimationPreviewPlayback();
        }

        private void UpdateAnimationPreviewPlayback()
        {
            if (isGeneratingAnimationPreview)
            {
                Repaint();
                return;
            }

            if (isPlayingAnimationPreview == false)
            {
                return;
            }

            float frameDuration = 1f / Mathf.Max(1, animationPreviewFramesPerSecond);
            double now = EditorApplication.timeSinceStartup;
            if (animationPreviewLastUpdateTime <= 0d)
            {
                animationPreviewLastUpdateTime = now;
                Repaint();
                return;
            }

            float deltaTime = (float)System.Math.Max(0d, now - animationPreviewLastUpdateTime);
            animationPreviewLastUpdateTime = now;

            animationPreviewFrameAccumulator += Mathf.Min(deltaTime, frameDuration);
            if (animationPreviewFrameAccumulator < frameDuration)
            {
                return;
            }

            animationPreviewFrameAccumulator -= frameDuration;
            int nextFrame = currentAnimationPreviewFrameIndex + 1;
            if (nextFrame >= animationPreviewFrameCount)
            {
                if (loopPreviewAnimation)
                {
                    nextFrame = 0;
                }
                else
                {
                    currentAnimationPreviewFrameIndex = Mathf.Max(0, animationPreviewFrameCount - 1);
                    CancelAnimationPreview(false);
                    Repaint();
                    return;
                }
            }

            currentAnimationPreviewFrameIndex = nextFrame;
            Repaint();
        }

        private void CancelAnimationPreview(bool forceImmediateCleanup)
        {
            if (helper != null)
            {
                helper.CancelAnimationPreviewBuild();
            }

            if (forceImmediateCleanup || isPlayingAnimationPreview == false)
            {
                EditorApplication.update -= UpdateAnimationPreviewEditorLoop;
            }

            if (forceImmediateCleanup || isPlayingAnimationPreview)
            {
                ClearAnimationPreviewAtlases();
                isPlayingAnimationPreview = false;
                isGeneratingAnimationPreview = false;
            }
            else if (currentCaptureRoutine == null)
            {
                isGeneratingAnimationPreview = false;
            }
        }

        private void ClearAnimationPreviewAtlases()
        {
            if (animationPreviewDiffuseAtlas != null)
            {
                DestroyImmediate(animationPreviewDiffuseAtlas);
                animationPreviewDiffuseAtlas = null;
            }

            if (animationPreviewNormalAtlas != null)
            {
                DestroyImmediate(animationPreviewNormalAtlas);
                animationPreviewNormalAtlas = null;
            }

            if (animationPreviewLitAtlas != null)
            {
                DestroyImmediate(animationPreviewLitAtlas);
                animationPreviewLitAtlas = null;
            }

            animationPreviewFrameCount = 0;
            currentAnimationPreviewFrameIndex = 0;
            animationPreviewFrameAccumulator = 0f;
            animationPreviewLastUpdateTime = 0d;
        }

        private void RequestPreviewRefresh()
        {
            if (helper == null)
            {
                return;
            }

            helper.RefreshPreview(PreviewNeedsDiffuse(), PreviewNeedsNormal());
        }

        private PixelateProfile EnsureActiveProfileForInspector()
        {
            if (helper == null)
            {
                return null;
            }

            bool hadLocalProfile = helper.HasLocalProfile();
            PixelateProfile activeProfile = helper.GetActiveProfile();
            if (hadLocalProfile == false)
            {
                EditorUtility.SetDirty(helper);
            }

            return activeProfile;
        }

        private PixelateProfile GetActiveProfile()
        {
            return helper != null ? helper.GetActiveProfile() : null;
        }

        private void SetProfileDirty(PixelateProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            if (EditorUtility.IsPersistent(profile))
            {
                EditorUtility.SetDirty(profile);
            }
            else if (helper != null)
            {
                helper.SyncStoredLocalProfileSettingsFromLocalProfile();
                EditorUtility.SetDirty(helper);
            }
        }

        private bool IsUsingLocalProfile()
        {
            SerializedProperty profileProp = serializedObject.FindProperty("profile");
            return profileProp == null || profileProp.objectReferenceValue == null;
        }

        private SerializedProperty GetProfileSectionProperty(string sectionName)
        {
            if (IsUsingLocalProfile())
            {
                SerializedProperty localProfileSectionsProp = serializedObject.FindProperty("localProfileSections");
                return localProfileSectionsProp?.FindPropertyRelative(sectionName);
            }

            SerializedProperty profileSectionsProp = profileSerializedObject?.FindProperty("sections");
            return profileSectionsProp?.FindPropertyRelative(sectionName);
        }

        private void SyncLocalProfileWrapperIfNeeded()
        {
            if (helper != null && IsUsingLocalProfile())
            {
                helper.SyncLocalProfileFromStoredSettings();
            }
        }

        private bool PreviewNeedsDiffuse()
        {
            return GetEffectivePreviewDrawMode() != DrawMode.Normal;
        }

        private bool PreviewNeedsNormal()
        {
            DrawMode effectiveDrawMode = GetEffectivePreviewDrawMode();
            if (effectiveDrawMode == DrawMode.Unlit)
            {
                return false;
            }

            PixelateProfile activeProfile = GetActiveProfile();
            return activeProfile != null && activeProfile.sections.lighting.enabled;
        }

        private bool IsNormalMapEnabledForPreview()
        {
            SerializedProperty lightingProp = GetProfileSectionProperty("lighting");
            SerializedProperty enabledProp = lightingProp?.FindPropertyRelative("enabled");
            if (enabledProp != null)
            {
                return enabledProp.boolValue;
            }

            PixelateProfile activeProfile = GetActiveProfile();
            return activeProfile != null && activeProfile.sections.lighting.enabled;
        }

        private DrawMode GetEffectivePreviewDrawMode()
        {
            return IsNormalMapEnabledForPreview() ? drawMode : DrawMode.Unlit;
        }

        private bool EnsureValidPreviewDrawModeForNormalMap()
        {
            if (IsNormalMapEnabledForPreview() || drawMode == DrawMode.Unlit)
            {
                return false;
            }

            SetPreviewDrawMode(DrawMode.Unlit);
            DestroyCachedLitPreview();
            return true;
        }

        private static GUIStyle VolumeSmallTickboxStyle
        {
            get
            {
                if (volumeSmallTickboxStyle == null)
                {
                    volumeSmallTickboxStyle = new GUIStyle("ShurikenToggle");
                }

                return volumeSmallTickboxStyle;
            }
        }

        private static void RecordPropertyUndo(SerializedProperty property, string undoName)
        {
            if (property == null || property.serializedObject == null)
            {
                return;
            }

            Undo.RecordObjects(property.serializedObject.targetObjects, undoName);
        }

        private bool ApplyProfileSerializedObjectChanges()
        {
            if (profileSerializedObject == null)
            {
                return false;
            }

            PixelateProfile profile = profileSerializedObject.targetObject as PixelateProfile;
            string paletteFingerprintBefore = GetPaletteSettingsFingerprint(profile);
            bool hadModifiedProperties = profileSerializedObject.hasModifiedProperties;
            bool changed = profileSerializedObject.ApplyModifiedProperties() || hadModifiedProperties;
            if (profile != null)
            {
                changed |= profile.Sanitize();
                bool paletteChanged = paletteFingerprintBefore != GetPaletteSettingsFingerprint(profile);
                changed |= paletteChanged;
                if (changed)
                {
                    SetProfileDirty(profile);
                    profileSerializedObject.Update();
                }

                if (paletteChanged)
                {
                    PixelateCaptureManager.ClearGeneratedPalettesForProfile(profile);
                }
            }

            return changed;
        }

        private static string GetPaletteSettingsFingerprint(PixelateProfile profile)
        {
            PixelateProfile.PaletteSettings palette = profile != null && profile.sections != null
                ? profile.sections.palette
                : null;
            if (palette == null)
            {
                return string.Empty;
            }

            string customPaletteId = palette.customPalette != null
                ? GlobalObjectId.GetGlobalObjectIdSlow(palette.customPalette).ToString()
                : string.Empty;
            return $"{palette.enabled}|{palette.style}|{palette.autoDetail}|{customPaletteId}|{palette.colorCount}";
        }

        private void UpdateProfileSerializedObject(PixelateProfile profile)
        {
            if (profile == null)
            {
                profileSerializedObject = null;
                profileSerializedObjectTarget = null;
                return;
            }

            if (profileSerializedObject == null || profileSerializedObjectTarget != profile)
            {
                profileSerializedObject = new SerializedObject(profile);
                profileSerializedObjectTarget = profile;
            }

            profileSerializedObject.Update();
        }

        private bool DrawSection(string title, System.Func<bool> drawContent, SerializedProperty activeProperty = null)
        {
            PixelateSectionId sectionId = GetSectionId(title);
            bool active = activeProperty == null || activeProperty.boolValue;
            return DrawSectionInternal(title, sectionId, drawContent, activeProperty != null, active, value =>
            {
                RecordPropertyUndo(activeProperty, "Toggle Pixelate Section");
                activeProperty.boolValue = value;
            });
        }

        private bool DrawToggleSection(string title, bool active, System.Action<bool> setActive, System.Func<bool> drawContent)
        {
            return DrawSectionInternal(title, GetSectionId(title), drawContent, true, active, setActive);
        }

        private bool DrawSectionInternal(string title, PixelateSectionId sectionId, System.Func<bool> drawContent, bool hasToggle, bool active, System.Action<bool> setActive)
        {
            var section = new DelegatePixelateInspectorSection(
                title,
                () => GetSectionExpanded(sectionId),
                value => SetSectionExpanded(sectionId, value),
                drawContent,
                CollapseAllSections,
                ExpandAllSections,
                () => hasToggle,
                () => active,
                value =>
                {
                    setActive?.Invoke(value);
                    active = value;
                },
                () => HasSettingsMenu(sectionId),
                () => CanPasteSectionSettings(sectionId),
                () => CopySectionSettings(sectionId),
                () => PasteSectionSettings(sectionId),
                () => ResetSectionSettings(sectionId),
                () => GetSectionDocumentationUrl(sectionId));

            return section.Draw();
        }

        private void DrawSectionHeader(string title, ref bool expanded, bool hasToggle, ref bool active, System.Action<bool> setActive, ref bool changed)
        {
            Rect backgroundRect = EditorGUI.IndentedRect(GUILayoutUtility.GetRect(1f, 17f));
            Rect fullBackgroundRect = ToFullWidth(backgroundRect);
            DrawVolumeHeaderBackground(fullBackgroundRect);

            Rect foldoutRect = backgroundRect;
            foldoutRect.y += 1f;
            foldoutRect.width = 13f;
            foldoutRect.height = 13f;

            Rect toggleRect = backgroundRect;
            toggleRect.x += 16f;
            toggleRect.y += 2f;
            toggleRect.width = 13f;
            toggleRect.height = 13f;

            Rect labelRect = backgroundRect;
            labelRect.xMin += hasToggle ? 32f : 16f;
            labelRect.xMax -= 20f;

            bool nextExpanded = GUI.Toggle(foldoutRect, expanded, GUIContent.none, EditorStyles.foldout);
            if (nextExpanded != expanded)
            {
                expanded = nextExpanded;
                changed = true;
            }

            if (hasToggle)
            {
                bool nextActive = GUI.Toggle(toggleRect, active, GUIContent.none, VolumeSmallTickboxStyle);
                if (nextActive != active)
                {
                    setActive?.Invoke(nextActive);
                    active = nextActive;
                    changed = true;
                }
            }

            using (new EditorGUI.DisabledScope(hasToggle && active == false))
            {
                EditorGUI.LabelField(labelRect, title, EditorStyles.boldLabel);
            }

            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown
                && currentEvent.button == 0
                && fullBackgroundRect.Contains(currentEvent.mousePosition)
                && (hasToggle == false || toggleRect.Contains(currentEvent.mousePosition) == false)
                && foldoutRect.Contains(currentEvent.mousePosition) == false)
            {
                expanded = !expanded;
                changed = true;
                currentEvent.Use();
            }
        }

        private static void DrawVolumeSplitter()
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 1f);
            rect = ToFullWidth(rect);

            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin
                ? new Color(0.12f, 0.12f, 0.12f, 1.333f)
                : new Color(0.6f, 0.6f, 0.6f, 1.333f));
        }

        private static void DrawVolumeHeaderBackground(Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            float tint = EditorGUIUtility.isProSkin ? 0.1f : 1f;
            EditorGUI.DrawRect(rect, new Color(tint, tint, tint, 0.2f));
        }

        private static Rect ToFullWidth(Rect rect)
        {
            rect.xMin = 0f;
            rect.width += 4f;
            return rect;
        }

        private static PixelateSectionId GetSectionId(string title)
        {
            switch (title)
            {
                case "Camera": return PixelateSectionId.Camera;
                case "Animation": return PixelateSectionId.Animation;
                case "Preview": return PixelateSectionId.Preview;
                case "Color Palette": return PixelateSectionId.ColorPalette;
                case "Normal Map": return PixelateSectionId.Lighting;
                case "Lighting": return PixelateSectionId.Lighting;
                case "Export Settings": return PixelateSectionId.ExportSettings;
                case "Output": return PixelateSectionId.ExportSettings;
                case "Help / Links": return PixelateSectionId.HelpLinks;
                default: return PixelateSectionId.Unknown;
            }
        }

        private static string GetSectionDocumentationUrl(PixelateSectionId sectionId)
        {
            switch (sectionId)
            {
                case PixelateSectionId.Camera:
                    return PixelateDocumentationLinks.Camera;
                case PixelateSectionId.Animation:
                    return PixelateDocumentationLinks.Animation;
                case PixelateSectionId.Preview:
                    return PixelateDocumentationLinks.Preview;
                case PixelateSectionId.ColorPalette:
                    return PixelateDocumentationLinks.ColorPalette;
                case PixelateSectionId.Lighting:
                    return PixelateDocumentationLinks.NormalMap;
                case PixelateSectionId.ExportSettings:
                    return PixelateDocumentationLinks.Output;
                case PixelateSectionId.HelpLinks:
                    return PixelateDocumentationLinks.Faq;
                default:
                    return PixelateDocumentationLinks.Root;
            }
        }

        private static bool HasSettingsMenu(PixelateSectionId sectionId)
        {
            return sectionId != PixelateSectionId.Unknown
                && sectionId != PixelateSectionId.HelpLinks;
        }

        private bool GetSectionExpanded(PixelateSectionId sectionId)
        {
            switch (sectionId)
            {
                case PixelateSectionId.Camera: return cameraExpanded;
                case PixelateSectionId.Animation: return modeExpanded;
                case PixelateSectionId.Preview: return previewExpanded;
                case PixelateSectionId.ColorPalette: return colorExpanded;
                case PixelateSectionId.Lighting: return lightingExpanded;
                case PixelateSectionId.ExportSettings: return captureExpanded;
                case PixelateSectionId.HelpLinks: return helpExpanded;
                default: return true;
            }
        }

        private void SetSectionExpanded(PixelateSectionId sectionId, bool expanded)
        {
            switch (sectionId)
            {
                case PixelateSectionId.Camera:
                    cameraExpanded = expanded;
                    break;
                case PixelateSectionId.Animation:
                    modeExpanded = expanded;
                    break;
                case PixelateSectionId.Preview:
                    previewExpanded = expanded;
                    break;
                case PixelateSectionId.ColorPalette:
                    colorExpanded = expanded;
                    break;
                case PixelateSectionId.Lighting:
                    lightingExpanded = expanded;
                    break;
                case PixelateSectionId.ExportSettings:
                    captureExpanded = expanded;
                    break;
                case PixelateSectionId.HelpLinks:
                    helpExpanded = expanded;
                    break;
                default:
                    return;
            }

            EditorPrefs.SetBool(GetFoldoutStateKey(sectionId), expanded);
        }

        private void LoadFoldoutStates()
        {
            cameraExpanded = EditorPrefs.GetBool(GetFoldoutStateKey(PixelateSectionId.Camera), true);
            modeExpanded = EditorPrefs.GetBool(GetFoldoutStateKey(PixelateSectionId.Animation), true);
            previewExpanded = EditorPrefs.GetBool(GetFoldoutStateKey(PixelateSectionId.Preview), true);
            colorExpanded = EditorPrefs.GetBool(GetFoldoutStateKey(PixelateSectionId.ColorPalette), true);
            lightingExpanded = EditorPrefs.GetBool(GetFoldoutStateKey(PixelateSectionId.Lighting), true);
            captureExpanded = EditorPrefs.GetBool(GetFoldoutStateKey(PixelateSectionId.ExportSettings), true);
            helpExpanded = EditorPrefs.GetBool(GetFoldoutStateKey(PixelateSectionId.HelpLinks), false);
        }

        private void LoadPreviewDrawModePreference()
        {
            int savedDrawMode = EditorPrefs.GetInt(PreviewDrawModePreferenceKey, (int)DrawMode.Lit);
            drawMode = System.Enum.IsDefined(typeof(DrawMode), savedDrawMode)
                ? (DrawMode)savedDrawMode
                : DrawMode.Lit;
        }

        private void SetPreviewDrawMode(DrawMode nextDrawMode)
        {
            if (System.Enum.IsDefined(typeof(DrawMode), nextDrawMode) == false)
            {
                nextDrawMode = DrawMode.Unlit;
            }

            drawMode = nextDrawMode;
            EditorPrefs.SetInt(PreviewDrawModePreferenceKey, (int)drawMode);
        }

        private void LoadPreviewLoopPreference()
        {
            loopPreviewAnimation = EditorPrefs.GetBool(PreviewLoopPreferenceKey, true);
        }

        private void SetPreviewLoopAnimation(bool shouldLoop)
        {
            loopPreviewAnimation = shouldLoop;
            EditorPrefs.SetBool(PreviewLoopPreferenceKey, loopPreviewAnimation);
        }

        private static string GetFoldoutStateKey(PixelateSectionId sectionId)
        {
            return ManagerFoldoutStatePrefix + sectionId;
        }

        private static bool CanPasteSectionSettings(PixelateSectionId sectionId)
        {
            return sectionId != PixelateSectionId.Unknown
                && copiedSectionId == sectionId
                && string.IsNullOrEmpty(copiedSectionJson) == false;
        }

        private void CopySectionSettings(PixelateSectionId sectionId)
        {
            copiedSectionJson = GetSectionSettingsJson(sectionId);
            copiedSectionId = string.IsNullOrEmpty(copiedSectionJson) ? PixelateSectionId.Unknown : sectionId;
        }

        private void PasteSectionSettings(PixelateSectionId sectionId)
        {
            if (CanPasteSectionSettings(sectionId) == false)
            {
                return;
            }

            ApplySectionSettingsJson(sectionId, copiedSectionJson);
        }

        private void ResetSectionSettings(PixelateSectionId sectionId)
        {
            string defaultJson = GetDefaultSectionSettingsJson(sectionId);
            if (string.IsNullOrEmpty(defaultJson))
            {
                return;
            }

            ApplySectionSettingsJson(sectionId, defaultJson);
        }

        private string GetSectionSettingsJson(PixelateSectionId sectionId)
        {
            serializedObject.ApplyModifiedProperties();
            profileSerializedObject?.ApplyModifiedProperties();

            switch (sectionId)
            {
                case PixelateSectionId.Camera:
                    return helper != null ? EditorJsonUtility.ToJson(helper.localSections.camera) : null;
                case PixelateSectionId.Animation:
                    return helper != null ? EditorJsonUtility.ToJson(helper.localSections.animation) : null;
                case PixelateSectionId.Preview:
                    return EditorJsonUtility.ToJson(new PreviewSectionSnapshot
                    {
                        drawMode = drawMode,
                        loopPreviewAnimation = loopPreviewAnimation,
                    });
                case PixelateSectionId.ColorPalette:
                    {
                        PixelateProfile activeProfile = GetActiveProfile();
                        return activeProfile != null ? EditorJsonUtility.ToJson(activeProfile.sections.palette) : null;
                    }
                case PixelateSectionId.Lighting:
                    {
                        PixelateProfile activeProfile = GetActiveProfile();
                        if (activeProfile == null)
                        {
                            return null;
                        }

                        return EditorJsonUtility.ToJson(new NormalMapSectionSnapshot
                        {
                            lighting = activeProfile.sections.lighting,
                        });
                    }
                case PixelateSectionId.ExportSettings:
                    return helper != null ? EditorJsonUtility.ToJson(helper.localSections.exportSettings) : null;
                default:
                    return null;
            }
        }

        private string GetDefaultSectionSettingsJson(PixelateSectionId sectionId)
        {
            switch (sectionId)
            {
                case PixelateSectionId.Camera:
                    return EditorJsonUtility.ToJson(new PixelateManagerSections.CameraSection());
                case PixelateSectionId.Animation:
                    return EditorJsonUtility.ToJson(new PixelateManagerSections.AnimationSection());
                case PixelateSectionId.Preview:
                    return EditorJsonUtility.ToJson(new PreviewSectionSnapshot
                    {
                        drawMode = DrawMode.Lit,
                        loopPreviewAnimation = true,
                    });
                case PixelateSectionId.ColorPalette:
                    return EditorJsonUtility.ToJson(new PixelateProfile.PaletteSettings());
                case PixelateSectionId.Lighting:
                    return EditorJsonUtility.ToJson(new NormalMapSectionSnapshot
                    {
                        lighting = new PixelateProfile.LightingSettings(),
                    });
                case PixelateSectionId.ExportSettings:
                    return EditorJsonUtility.ToJson(new PixelateManagerSections.ExportSettingsSection());
                default:
                    return null;
            }
        }

        private void ApplySectionSettingsJson(PixelateSectionId sectionId, string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            serializedObject.ApplyModifiedProperties();
            profileSerializedObject?.ApplyModifiedProperties();

            bool shouldRefreshPreview = false;
            bool shouldDirtyProfile = false;
            switch (sectionId)
            {
                case PixelateSectionId.Camera:
                    if (helper == null) return;
                    Undo.RecordObject(helper, "Paste Pixelate Camera Settings");
                    EditorJsonUtility.FromJsonOverwrite(json, helper.localSections.camera);
                    helper.localSections.camera.Sanitize();
                    SyncCellSizeTextFromActiveCameraSettings();
                    shouldRefreshPreview = true;
                    break;
                case PixelateSectionId.Animation:
                    if (helper == null) return;
                    Undo.RecordObject(helper, "Paste Pixelate Animation Settings");
                    EditorJsonUtility.FromJsonOverwrite(json, helper.localSections.animation);
                    helper.localSections.animation.Sanitize();
                    shouldRefreshPreview = helper.captureType == PixelateCaptureManager.CaptureType.animation;
                    break;
                case PixelateSectionId.Preview:
                    Undo.RecordObject(this, "Paste Pixelate Preview Settings");
                    ApplyPreviewSectionSnapshot(JsonUtility.FromJson<PreviewSectionSnapshot>(json));
                    shouldRefreshPreview = true;
                    break;
                case PixelateSectionId.ColorPalette:
                    {
                        PixelateProfile activeProfile = GetActiveProfile();
                        if (activeProfile == null) return;
                        Undo.RecordObject(activeProfile, "Paste Pixelate Palette Settings");
                        EditorJsonUtility.FromJsonOverwrite(json, activeProfile.sections.palette);
                        activeProfile.sections.palette.Sanitize();
                    }
                    shouldRefreshPreview = true;
                    shouldDirtyProfile = true;
                    break;
                case PixelateSectionId.Lighting:
                    {
                        PixelateProfile activeProfile = GetActiveProfile();
                        if (activeProfile == null) return;
                        Undo.RecordObject(activeProfile, "Paste Pixelate Normal Map Settings");
                        ApplyNormalMapSectionSnapshot(JsonUtility.FromJson<NormalMapSectionSnapshot>(json));
                    }
                    shouldRefreshPreview = true;
                    shouldDirtyProfile = true;
                    break;
                case PixelateSectionId.ExportSettings:
                    if (helper == null) return;
                    Undo.RecordObject(helper, "Paste Pixelate Output Settings");
                    EditorJsonUtility.FromJsonOverwrite(json, helper.localSections.exportSettings);
                    helper.localSections.exportSettings.Sanitize();
                    break;
            }

            if (helper != null)
            {
                EditorUtility.SetDirty(helper);
            }
            if (shouldDirtyProfile)
            {
                SetProfileDirty(GetActiveProfile());
            }
            EditorUtility.SetDirty(this);
            serializedObject.Update();
            UpdateProfileSerializedObject(GetActiveProfile());
            InitializeLists();
            DestroyCachedLitPreview();
            if (shouldRefreshPreview)
            {
                RequestPreviewRefresh();
            }
            Repaint();
        }

        private void ApplyPreviewSectionSnapshot(PreviewSectionSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            SetPreviewDrawMode(snapshot.drawMode);
            SetPreviewLoopAnimation(snapshot.loopPreviewAnimation);
        }

        private void ApplyNormalMapSectionSnapshot(NormalMapSectionSnapshot snapshot)
        {
            PixelateProfile activeProfile = GetActiveProfile();
            if (snapshot == null || activeProfile == null)
            {
                return;
            }

            if (snapshot.lighting != null)
            {
                activeProfile.sections.lighting.enabled = snapshot.lighting.enabled;
                activeProfile.sections.lighting.lightingSteps = snapshot.lighting.lightingSteps;
                activeProfile.sections.lighting.materialPipeline = snapshot.lighting.materialPipeline;
                activeProfile.sections.lighting.Sanitize();
            }

        }

        private void CollapseAllSections()
        {
            SetAllSectionsExpanded(false);
        }

        private void ExpandAllSections()
        {
            SetAllSectionsExpanded(true);
        }

        private void SetAllSectionsExpanded(bool expanded)
        {
            SetSectionExpanded(PixelateSectionId.Camera, expanded);
            SetSectionExpanded(PixelateSectionId.Animation, expanded);
            SetSectionExpanded(PixelateSectionId.Preview, expanded);
            SetSectionExpanded(PixelateSectionId.ColorPalette, expanded);
            SetSectionExpanded(PixelateSectionId.Lighting, expanded);
            SetSectionExpanded(PixelateSectionId.ExportSettings, expanded);
            SetSectionExpanded(PixelateSectionId.HelpLinks, expanded);
            Repaint();
        }

        private bool DrawSetupFields()
        {
            bool changed;
            using (var changeScope = new EditorGUI.ChangeCheckScope())
            {
                SerializedProperty setupProp = serializedObject.FindProperty("localSections").FindPropertyRelative("setup");
                SerializedProperty targetProp = setupProp.FindPropertyRelative("target");
                EditorGUILayout.PropertyField(targetProp, TargetContent);
                if (targetProp.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox("Assign a Target GameObject to continue setting up Pixelate capture.", MessageType.Info);
                }

                GUILayout.Space(6f);
                changed = changeScope.changed;
            }

            if (changed)
            {
                ApplySerializedObjectChanges();
            }

            return changed;
        }

        private bool DrawTopCaptureControls()
        {
            GUILayout.Space(6f);
            DrawCaptureActionControls();
            DrawCaptureValidationMessages();
            GUILayout.Space(6f);
            return false;
        }

        private bool SanitizeCaptureTypeProperty(SerializedProperty captureTypeProp)
        {
            if (captureTypeProp.enumValueIndex == (int)PixelateCaptureManager.CaptureType.image
                || captureTypeProp.enumValueIndex == (int)PixelateCaptureManager.CaptureType.animation)
            {
                return false;
            }

            RecordPropertyUndo(captureTypeProp, "Reset Pixelate Animation Toggle");
            captureTypeProp.enumValueIndex = (int)PixelateCaptureManager.CaptureType.image;
            if (helper != null)
            {
                helper.captureType = PixelateCaptureManager.CaptureType.image;
            }

            return true;
        }

        private bool DrawCameraSection()
        {
            return DrawSection("Camera", () =>
            {
                using (var changeScope = new EditorGUI.ChangeCheckScope())
                {
                    SerializedProperty cameraProp = serializedObject.FindProperty("localSections").FindPropertyRelative("camera");
                    SerializedProperty captureCameraProp = cameraProp.FindPropertyRelative("captureCamera");
                    if (captureCameraProp.objectReferenceValue == null)
                    {
                        EditorGUILayout.HelpBox("Assign a Capture Camera to continue setting up Pixelate capture.", MessageType.Info);
                    }

                    EditorGUILayout.PropertyField(captureCameraProp, CaptureCameraContent);
                    DrawCellSizeField(
                        cameraProp.FindPropertyRelative("cellSize"),
                        cameraProp.FindPropertyRelative("linkCellSizeAxes"));
                    EditorGUILayout.PropertyField(cameraProp.FindPropertyRelative("pixelated"), PixelatedContent);
                    return changeScope.changed;
                }
            });
        }

        private void DrawCaptureActionControls()
        {
            bool captureControlsLocked = currentCaptureRoutine != null || IsAnimationPreviewLocked();

            using (new EditorGUI.DisabledScope(helper == null || helper.HasBlockingIssues(includeExportValidation: true) || captureControlsLocked))
            {
                if (GUILayout.Button("Capture", GUILayout.Height(31)))
                {
                    profileSerializedObject?.ApplyModifiedProperties();
                    ApplySerializedObjectChanges();
                    ExecuteCapture();
                }
            }
        }

        private bool DrawPixelateProfileCard()
        {
            SerializedProperty profileProp = serializedObject.FindProperty("profile");
            bool changed = false;
            PixelateProfile assignedProfile = profileProp.objectReferenceValue as PixelateProfile;
            PixelateProfile activeProfile = GetActiveProfile();
            bool usingLocalProfile = assignedProfile == null;

            DrawVolumeSplitter();
            Rect cardRect = GUILayoutUtility.GetRect(1f, 60f);
            cardRect = ToFullWidth(cardRect);

            Rect topLine = new Rect(cardRect.x, cardRect.y, cardRect.width, 1f);
            Rect bottomLine = new Rect(cardRect.x, cardRect.yMax - 1f, cardRect.width, 1f);
            if (Event.current.type == EventType.Repaint)
            {
                Color lineColor = EditorGUIUtility.isProSkin
                    ? new Color(0.12f, 0.12f, 0.12f, 1.333f)
                    : new Color(0.6f, 0.6f, 0.6f, 1.333f);
                EditorGUI.DrawRect(topLine, lineColor);
                EditorGUI.DrawRect(bottomLine, lineColor);
            }

            Rect iconRect = new Rect(cardRect.x + 13f, cardRect.y + 7f, 44f, 44f);
            Texture icon = EditorGUIUtility.ObjectContent(activeProfile, typeof(PixelateProfile)).image;
            if (icon != null)
            {
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
            }

            float fieldX = iconRect.xMax + 14f;
            Rect fieldRect = new Rect(fieldX, cardRect.y + 8f, cardRect.xMax - fieldX - 14f, EditorGUIUtility.singleLineHeight);
            EditorGUI.BeginChangeCheck();
            Object nextProfile = EditorGUI.ObjectField(fieldRect, GUIContent.none, activeProfile, typeof(PixelateProfile), false);
            GUI.Label(fieldRect, ProfileContent);
            if (EditorGUI.EndChangeCheck())
            {
                PixelateProfile nextPixelateProfile = nextProfile as PixelateProfile;
                if (nextPixelateProfile == null)
                {
                    changed |= UnlinkAssignedProfile(profileProp);
                }
                else if (nextPixelateProfile != activeProfile)
                {
                    if (EditorUtility.IsPersistent(nextPixelateProfile))
                    {
                        RecordPropertyUndo(profileProp, "Assign Pixelate Profile");
                        profileProp.objectReferenceValue = nextPixelateProfile;
                    }
                    else if (helper != null)
                    {
                        PixelateProfile localProfile = helper.GetLocalProfile();
                        Undo.RecordObject(localProfile, "Copy Pixelate Profile Settings");
                        localProfile.CopySettingsFrom(nextPixelateProfile);
                        RecordPropertyUndo(profileProp, "Use Local Pixelate Profile");
                        profileProp.objectReferenceValue = null;
                        SetProfileDirty(localProfile);
                    }
                    changed = true;
                }
            }

            const float buttonWidth = 60f;
            const float buttonSpacing = 6f;
            Rect buttonRect = new Rect(fieldX, fieldRect.yMax + 5f, buttonWidth, EditorGUIUtility.singleLineHeight + 2f);
            if (usingLocalProfile)
            {
                if (GUI.Button(buttonRect, "Save"))
                {
                    SaveLocalProfile(profileProp);
                    changed = true;
                }
            }
            else
            {
                if (GUI.Button(buttonRect, "Clone"))
                {
                    CloneAssignedProfile(profileProp);
                    changed = true;
                }

                Rect unlinkButtonRect = new Rect(buttonRect.xMax + buttonSpacing, buttonRect.y, buttonWidth, buttonRect.height);
                if (GUI.Button(unlinkButtonRect, "Unlink"))
                {
                    changed |= UnlinkAssignedProfile(profileProp);
                }
            }

            if (changed)
            {
                serializedObject.ApplyModifiedProperties();
                UpdateProfileSerializedObject(GetActiveProfile());
            }

            return changed;
        }

        private bool DrawAnimationSection()
        {
            SerializedProperty captureTypeProp = serializedObject.FindProperty("captureType");
            bool changed = captureTypeProp != null && SanitizeCaptureTypeProperty(captureTypeProp);
            if (changed)
            {
                serializedObject.ApplyModifiedProperties();
                helper = (PixelateCaptureManager)target;
            }

            bool animationEnabled = captureTypeProp != null
                && captureTypeProp.enumValueIndex == (int)PixelateCaptureManager.CaptureType.animation;

            bool sectionChanged = DrawToggleSection("Animation", animationEnabled, enabled =>
            {
                PixelateCaptureManager.CaptureType nextCaptureType = enabled
                    ? PixelateCaptureManager.CaptureType.animation
                    : PixelateCaptureManager.CaptureType.image;

                RecordPropertyUndo(captureTypeProp, "Toggle Pixelate Animation");
                captureTypeProp.enumValueIndex = (int)nextCaptureType;
                if (helper != null)
                {
                    helper.captureType = nextCaptureType;
                }

                CancelAnimationPreview(true);
                DestroyCachedLitPreview();
            }, () => DrawAnimationOptionsGUI(serializedObject.FindProperty("localSections.animation.sourceClipEntries")));

            return changed || sectionChanged;
        }

        private bool DrawColorOptionsSection()
        {
            SerializedProperty paletteProp = GetProfileSectionProperty("palette");
            if (paletteProp == null)
            {
                return false;
            }

            SerializedProperty enabledProp = paletteProp.FindPropertyRelative("enabled");
            return DrawSection("Color Palette", () =>
            {
                using (var changeScope = new EditorGUI.ChangeCheckScope())
                {
                    SerializedProperty styleProp = paletteProp.FindPropertyRelative("style");
                    GUIContent[] paletteText = { AutoPaletteContent, CustomPaletteModeContent };
                    int selectedStyle = styleProp.enumValueIndex;
                    if (selectedStyle != (int)PixelateCaptureManager.PaletteStyle.custom)
                    {
                        selectedStyle = (int)PixelateCaptureManager.PaletteStyle.auto;
                    }

                    selectedStyle = GUILayout.SelectionGrid(selectedStyle, paletteText, 1, EditorStyles.radioButton);
                    if (styleProp.enumValueIndex != selectedStyle)
                    {
                        RecordPropertyUndo(styleProp, "Change Pixelate Palette Mode");
                        styleProp.enumValueIndex = selectedStyle;
                    }

                    PixelateCaptureManager.PaletteStyle paletteStyle = enabledProp.boolValue
                        ? (PixelateCaptureManager.PaletteStyle)selectedStyle
                        : PixelateCaptureManager.PaletteStyle.none;

                    if (paletteStyle == PixelateCaptureManager.PaletteStyle.auto)
                    {
                        SerializedProperty autoPaletteDetailProp = paletteProp.FindPropertyRelative("autoDetail");
                        SerializedProperty colorCountProp = paletteProp.FindPropertyRelative("colorCount");
                        EditorGUILayout.PropertyField(autoPaletteDetailProp, PaletteDetailContent);

                        PixelateCaptureManager.AutoPaletteDetail autoPaletteDetail = (PixelateCaptureManager.AutoPaletteDetail)autoPaletteDetailProp.enumValueIndex;
                        if (autoPaletteDetail == PixelateCaptureManager.AutoPaletteDetail.custom)
                        {
                            EditorGUILayout.PropertyField(colorCountProp, ColorCountContent);
                        }
                        else
                        {
                            int displayedColorCount = PixelateCaptureManager.ResolveAutoPaletteDetailColorCount(
                                autoPaletteDetail,
                                colorCountProp.intValue);

                            using (new EditorGUI.DisabledScope(true))
                            {
                                EditorGUILayout.IntField(ColorCountContent, displayedColorCount);
                            }
                        }

                        SerializedProperty generatedPaletteProp = serializedObject.FindProperty("_generatedPalette");
                        Texture2D generatedPalette = generatedPaletteProp.objectReferenceValue as Texture2D;
                        DrawPalettePreview(generatedPalette, "Generate a preview first");

                        using (new EditorGUI.DisabledScope(!helper.HasGeneratedPalette() || helper.HasBlockingIssues(includeExportValidation: true)))
                        {
                            if (GUILayout.Button("Save Palette", GUILayout.Height(25)))
                            {
                                SavePaletteTexture(generatedPalette);
                            }
                        }
                    }
                    else if (paletteStyle == PixelateCaptureManager.PaletteStyle.custom)
                    {
                        SerializedProperty customPaletteProp = paletteProp.FindPropertyRelative("customPalette");
                        EditorGUILayout.PropertyField(customPaletteProp, CustomPaletteContent);
                        Texture2D customPalette = customPaletteProp.objectReferenceValue as Texture2D;
                        DrawPalettePreview(customPalette, "Assign a custom palette first");
                        DrawCustomPaletteImportSettingsWarning(customPalette);
                    }

                    return changeScope.changed;
                }
            }, enabledProp);
        }

        private void DrawCustomPaletteImportSettingsWarning(Texture2D customPalette)
        {
            if (customPalette == null)
            {
                return;
            }

            var issues = PixelateUtilities.GetPaletteValidationIssues(customPalette);
            if (issues.Count == 0)
            {
                return;
            }

            EditorGUILayout.HelpBox(PixelateUtilities.FormatPaletteValidationWarning(issues), MessageType.Warning);

            if (PixelateUtilities.HasFixablePaletteImportSettingIssues(customPalette) == false)
            {
                return;
            }

            if (GUILayout.Button("Fix import settings", GUILayout.Height(25)))
            {
                if (PixelateUtilities.FixPaletteImportSettings(customPalette))
                {
                    DestroyCachedLitPreview();
                    RequestPreviewRefresh();
                    Repaint();
                }

                GUIUtility.ExitGUI();
            }
        }

        private void DrawPalettePreview(Texture2D palette, string emptyMessage)
        {
            Rect controlRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 4f);
            Rect previewRect = EditorGUI.PrefixLabel(controlRect, PalettePreviewContent);
            previewRect.y += 2f;
            previewRect.height -= 4f;

            GUI.Box(previewRect, GUIContent.none, EditorStyles.helpBox);
            Rect innerRect = new Rect(previewRect.x + 2f, previewRect.y + 2f, Mathf.Max(1f, previewRect.width - 4f), Mathf.Max(1f, previewRect.height - 4f));
            if (palette != null)
            {
                GUI.DrawTexture(innerRect, palette, ScaleMode.StretchToFill, false);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.LabelField(innerRect, emptyMessage, EditorStyles.miniLabel);
            }
        }

        private void DrawCellSizeField(SerializedProperty cellSizeProperty, SerializedProperty linkAxesProperty)
        {
            if (cellSizeProperty == null)
            {
                return;
            }

            SerializedProperty xProp = cellSizeProperty.FindPropertyRelative("x");
            SerializedProperty yProp = cellSizeProperty.FindPropertyRelative("y");
            bool linkAxes = linkAxesProperty != null && linkAxesProperty.boolValue;
            if (!suppressCellSizeTextSync)
            {
                SyncCellSizeTextFromProperty(cellSizeProperty);
            }

            Rect controlRect = EditorGUILayout.GetControlRect();
            Rect contentRect = EditorGUI.PrefixLabel(controlRect, CellSizeContent);

            const float linkWidth = 18f;
            float axisLabelWidth = 14f;
            float spacing = 4f;
            Rect linkRect = new Rect(contentRect.x, contentRect.y, linkWidth, contentRect.height);
            Rect axesRect = new Rect(linkRect.xMax + spacing, contentRect.y, contentRect.width - linkWidth - spacing, contentRect.height);
            EditorGUIUtility.AddCursorRect(linkRect, MouseCursor.Link);
            if (Event.current.type == EventType.MouseMove)
            {
                Repaint();
            }

            if (cellSizeLinkButtonUsesBuiltInHover == false)
            {
                DrawCellSizeLinkHoverBackground(linkRect);
            }

            if (GUI.Button(linkRect, GetCellSizeLinkIconContent(linkAxes), CellSizeLinkButtonStyle))
            {
                RecordPropertyUndo(linkAxesProperty, "Toggle Pixelate Cell Size Link");
                linkAxes = !linkAxes;
                if (linkAxesProperty != null)
                {
                    linkAxesProperty.boolValue = linkAxes;
                }
            }

            float fieldWidth = Mathf.Max(24f, (axesRect.width - axisLabelWidth * 2f - spacing * 3f) * 0.5f);

            Rect xLabelRect = new Rect(axesRect.x, axesRect.y, axisLabelWidth, axesRect.height);
            Rect xFieldRect = new Rect(xLabelRect.xMax, axesRect.y, fieldWidth, axesRect.height);
            Rect yLabelRect = new Rect(xFieldRect.xMax + spacing, axesRect.y, axisLabelWidth, axesRect.height);
            Rect yFieldRect = new Rect(yLabelRect.xMax, axesRect.y, fieldWidth, axesRect.height);

            DrawCellSizeAxisField("X", xLabelRect, xFieldRect, ref cellSizeXText, xProp, yProp, ref cellSizeYText, linkAxes);
            DrawCellSizeAxisField("Y", yLabelRect, yFieldRect, ref cellSizeYText, yProp, xProp, ref cellSizeXText, linkAxes);
        }

        private static GUIStyle CellSizeLinkButtonStyle
        {
            get
            {
                if (cellSizeLinkButtonStyle == null)
                {
                    GUIStyle builtInStyle = GUI.skin.FindStyle("IconButton");
                    cellSizeLinkButtonUsesBuiltInHover = builtInStyle != null;
                    cellSizeLinkButtonStyle = new GUIStyle(builtInStyle ?? GUIStyle.none)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        padding = new RectOffset(0, 0, 0, 0),
                    };
                }

                return cellSizeLinkButtonStyle;
            }
        }

        private static void DrawCellSizeLinkHoverBackground(Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            bool hovering = rect.Contains(Event.current.mousePosition);
            if (hovering == false)
            {
                return;
            }

            Color backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.42f, 0.42f, 0.42f, 1f)
                : new Color(0.82f, 0.82f, 0.82f, 1f);

            EditorGUI.DrawRect(rect, backgroundColor);
        }

        private static GUIContent GetCellSizeLinkIconContent(bool linked)
        {
            string iconName = linked ? "Linked" : "UnLinked";
            string darkIconName = "d_" + iconName;
            string primaryIcon = EditorGUIUtility.isProSkin ? darkIconName : iconName;
            string fallbackIcon = EditorGUIUtility.isProSkin ? iconName : darkIconName;
            GUIContent iconContent = EditorGUIUtility.IconContent(primaryIcon, "|Link Cell Size X and Y");
            if (iconContent == null || iconContent.image == null)
            {
                iconContent = EditorGUIUtility.IconContent(fallbackIcon, "|Link Cell Size X and Y");
            }

            return iconContent != null && iconContent.image != null
                ? iconContent
                : new GUIContent(linked ? "=" : "-", "Link Cell Size X and Y");
        }

        private void DrawCellSizeAxisField(
            string axisLabel,
            Rect labelRect,
            Rect fieldRect,
            ref string textValue,
            SerializedProperty axisProperty,
            SerializedProperty linkedAxisProperty,
            ref string linkedTextValue,
            bool linkAxes)
        {
            int dragControlId = GUIUtility.GetControlID(FocusType.Passive, labelRect);
            EditorGUIUtility.AddCursorRect(labelRect, MouseCursor.SlideArrow);

            Event currentEvent = Event.current;
            switch (currentEvent.GetTypeForControl(dragControlId))
            {
                case EventType.MouseDown:
                    if (labelRect.Contains(currentEvent.mousePosition) && currentEvent.button == 0)
                    {
                        RecordPropertyUndo(axisProperty, "Change Pixelate Cell Size");
                        GUIUtility.hotControl = dragControlId;
                        currentEvent.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == dragControlId)
                    {
                        int delta = Mathf.RoundToInt(currentEvent.delta.x);
                        if (delta != 0)
                        {
                            SetCellSizeAxisValue(
                                axisProperty,
                                axisProperty.intValue + delta,
                                ref textValue,
                                linkedAxisProperty,
                                ref linkedTextValue,
                                linkAxes);
                        }

                        currentEvent.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == dragControlId)
                    {
                        GUIUtility.hotControl = 0;
                        currentEvent.Use();
                    }
                    break;
                case EventType.Repaint:
                    EditorStyles.label.Draw(labelRect, axisLabel, false, false, false, false);
                    break;
            }

            string controlName = "PixelateCellSize" + axisLabel;
            GUI.SetNextControlName(controlName);
            string newText = EditorGUI.TextField(fieldRect, textValue ?? axisProperty.intValue.ToString());

            if (newText != textValue)
            {
                textValue = newText;
                if (TryParsePositiveInt(newText, out int parsedValue))
                {
                    if (axisProperty.intValue != parsedValue
                        || (linkAxes && linkedAxisProperty != null && linkedAxisProperty.intValue != parsedValue))
                    {
                        RecordPropertyUndo(axisProperty, "Change Pixelate Cell Size");
                    }
                    SetCellSizeAxisValue(
                        axisProperty,
                        parsedValue,
                        ref textValue,
                        linkedAxisProperty,
                        ref linkedTextValue,
                        linkAxes);
                }
            }
            else if (GUI.GetNameOfFocusedControl() != controlName && TryParsePositiveInt(textValue, out int committedValue))
            {
                textValue = committedValue.ToString();
            }
        }

        private void SetCellSizeAxisValue(
            SerializedProperty axisProperty,
            int value,
            ref string textValue,
            SerializedProperty linkedAxisProperty,
            ref string linkedTextValue,
            bool linkAxes)
        {
            int sanitizedValue = Mathf.Max(1, value);
            axisProperty.intValue = sanitizedValue;
            textValue = sanitizedValue.ToString();

            if (linkAxes && linkedAxisProperty != null)
            {
                linkedAxisProperty.intValue = sanitizedValue;
                linkedTextValue = textValue;
            }

            suppressCellSizeTextSync = true;
            GUI.changed = true;
        }

        private void SyncCellSizeTextFromActiveCameraSettings()
        {
            if (helper == null)
            {
                return;
            }

            SerializedProperty cellSizeProperty = serializedObject.FindProperty("localSections.camera.cellSize");
            SyncCellSizeTextFromProperty(cellSizeProperty);
        }

        private void SyncCellSizeTextFromProperty(SerializedProperty cellSizeProperty)
        {
            if (cellSizeProperty == null)
            {
                return;
            }

            SerializedProperty xProp = cellSizeProperty.FindPropertyRelative("x");
            SerializedProperty yProp = cellSizeProperty.FindPropertyRelative("y");
            cellSizeXText = Mathf.Max(1, xProp.intValue).ToString();
            cellSizeYText = Mathf.Max(1, yProp.intValue).ToString();
            suppressCellSizeTextSync = false;
        }

        private static bool TryParsePositiveInt(string value, out int parsedValue)
        {
            if (int.TryParse(value, out parsedValue))
            {
                parsedValue = Mathf.Max(1, parsedValue);
                return true;
            }

            parsedValue = 1;
            return false;
        }

        private static string GetMaterialPipelineMismatchWarning(PixelateCaptureManager.MaterialPipeline selected)
        {
            bool projectUsesUrp = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null
                || UnityEngine.QualitySettings.renderPipeline != null;

            if (selected == PixelateCaptureManager.MaterialPipeline.urp && !projectUsesUrp)
            {
                return "Material Pipeline is set to URP, but the project appears to be using the Built-in render pipeline. Set it to Built-in, or assign a URP asset in Project Settings > Graphics and Quality.";
            }

            if (selected == PixelateCaptureManager.MaterialPipeline.builtIn && projectUsesUrp)
            {
                return "Material Pipeline is set to Built-in, but the project appears to be using a Scriptable Render Pipeline. Set it to URP, or remove the render pipeline asset from Project Settings > Graphics and Quality.";
            }

            return null;
        }

        private void DrawCaptureValidationMessages()
        {
            if (helper == null)
            {
                return;
            }

            var validations = helper.GetValidationMessages(includeExportValidation: true);
            var visibleValidations = validations
                .Where(validation => ShouldHideValidationMessage(validation.Message) == false)
                .ToList();

            if (visibleValidations.Count == 0)
            {
                return;
            }

            for (int i = 0; i < visibleValidations.Count; i++)
            {
                ShowMessage(visibleValidations[i].Message, ToMessageType(visibleValidations[i].Severity));
            }
        }

        private bool DrawLightingSection()
        {
            SerializedProperty lightingProp = GetProfileSectionProperty("lighting");
            if (lightingProp == null)
            {
                return false;
            }

            SerializedProperty enabledProp = lightingProp.FindPropertyRelative("enabled");
            bool changed = DrawSection("Normal Map", () =>
            {
                using (var changeScope = new EditorGUI.ChangeCheckScope())
                {
                    SerializedProperty lightingStepsProp = lightingProp.FindPropertyRelative("lightingSteps");
                    EditorGUILayout.IntSlider(lightingStepsProp, 1, 10, LightingStepsContent);

                    SerializedProperty materialPipelineProp = lightingProp.FindPropertyRelative("materialPipeline");
                    EditorGUILayout.PropertyField(materialPipelineProp, MaterialPipelineContent);
                    string pipelineMismatch = GetMaterialPipelineMismatchWarning((PixelateCaptureManager.MaterialPipeline)materialPipelineProp.enumValueIndex);
                    if (pipelineMismatch != null)
                    {
                        EditorGUILayout.HelpBox(pipelineMismatch, MessageType.Warning);
                    }

                    if (changeScope.changed)
                    {
                        DestroyCachedLitPreview();
                        Repaint();
                    }

                    return changeScope.changed;
                }
            }, enabledProp);

            if (enabledProp.boolValue == false)
            {
                changed |= EnsureValidPreviewDrawModeForNormalMap();
            }

            return changed;
        }

        private bool DrawPreviewSection()
        {
            Texture2D previewImage = helper != null ? helper.GetPreviewImage() : null;
            Texture2D previewNormalImage = helper != null ? helper.GetPreviewNormalImage() : null;
            bool previewControlsLocked = currentCaptureRoutine != null || IsAnimationPreviewLocked();
            bool hasAnimationClip = HasAssignedAnimationClip();
            return DrawSection("Preview", () =>
            {
                bool shouldRefreshPreview = false;
                shouldRefreshPreview |= EnsureValidPreviewDrawModeForNormalMap();
                bool normalMapEnabledForPreview = IsNormalMapEnabledForPreview();
                using (new EditorGUI.DisabledScope(previewControlsLocked))
                using (var drawModeChangeScope = new EditorGUI.ChangeCheckScope())
                {
                    DrawMode nextDrawMode;
                    using (new EditorGUI.DisabledScope(normalMapEnabledForPreview == false))
                    {
                        nextDrawMode = (DrawMode)EditorGUILayout.EnumPopup(DrawModeContent, GetEffectivePreviewDrawMode());
                    }

                    if (drawModeChangeScope.changed && normalMapEnabledForPreview)
                    {
                        Undo.RecordObject(this, "Change Pixelate Preview Draw Mode");
                        SetPreviewDrawMode(nextDrawMode);
                        DestroyCachedLitPreview();
                        shouldRefreshPreview = true;
                    }
                }

                using (new EditorGUI.DisabledScope(previewControlsLocked))
                {
                    if (helper != null && helper.captureType == PixelateCaptureManager.CaptureType.animation && hasAnimationClip)
                    {
                        SerializedProperty sourceClipEntriesProp = serializedObject.FindProperty("localSections.animation.sourceClipEntries");
                        if (TryGetFirstAssignedClipEntry(sourceClipEntriesProp, out AnimationClip sourceClip, out float clipSpeed))
                        {
                            SerializedProperty previewFrameProp = serializedObject.FindProperty("localSections.animation.currentFrame");
                            int numFrames = Mathf.Max(1, helper.GetFrameCount(sourceClip, clipSpeed));
                            int frame = previewFrameProp.intValue;
                            frame = EditorGUILayout.IntSlider(PreviewFrameContent, frame, 0, numFrames - 1);

                            if (previewFrameProp.intValue != frame)
                            {
                                RecordPropertyUndo(previewFrameProp, "Change Pixelate Preview Frame");
                                previewFrameProp.intValue = frame;
                                serializedObject.ApplyModifiedProperties();
                                RequestPreviewRefresh();
                                Repaint();
                                serializedObject.Update();
                            }

                        }
                    }
                }

                Texture2D layoutPreviewImage = GetLayoutPreviewImage(previewImage, previewNormalImage);

                GUILayout.Space(3);
                if (layoutPreviewImage == null)
                {
                    bool hasVisibleValidation = false;
                    if (helper != null)
                    {
                        var validations = helper.GetValidationMessages(includeExportValidation: false);
                        for (int i = 0; i < validations.Count; i++)
                        {
                            if (ShouldHideValidationMessage(validations[i].Message))
                            {
                                continue;
                            }

                            hasVisibleValidation = true;
                            break;
                        }
                    }

                    if (hasVisibleValidation == false && helper != null && helper.TryGetValidationMessage(includeExportValidation: false, out string validationMessage) == false)
                    {
                        EditorGUILayout.HelpBox(validationMessage, MessageType.Info);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Preview will appear here after the setup is valid and Pixelate has rendered a preview frame.", MessageType.None);
                    }
                    return shouldRefreshPreview;
                }

                const float previewPadding = 12f;
                const float toolbarHeight = 23f;
                bool showToolbar = helper != null && helper.captureType == PixelateCaptureManager.CaptureType.animation && hasAnimationClip;
                float inspectorWidth = Mathf.Max(120f, EditorGUIUtility.currentViewWidth - 58f);
                float imageAreaHeight = Mathf.Clamp(inspectorWidth * 0.9f, 180f, 320f);
                float previewBoxHeight = imageAreaHeight + (showToolbar ? toolbarHeight : 0f);

                Rect outerRect = GUILayoutUtility.GetRect(0f, previewBoxHeight, GUILayout.ExpandWidth(true));
                GUI.Box(outerRect, GUIContent.none, EditorStyles.helpBox);

                Rect toolbarRect = new Rect(outerRect.x + 1f, outerRect.y + 1f, outerRect.width - 2f, toolbarHeight);
                if (showToolbar)
                    GUI.Box(toolbarRect, GUIContent.none, EditorStyles.toolbar);

                float contentTop = outerRect.y + (showToolbar ? toolbarHeight : 0f) + previewPadding;
                float contentHeight = outerRect.height - (showToolbar ? toolbarHeight : 0f) - (previewPadding * 2f);
                Rect innerRect = new Rect(
                    outerRect.x + previewPadding,
                    contentTop,
                    Mathf.Max(1f, outerRect.width - (previewPadding * 2f)),
                    Mathf.Max(1f, contentHeight));

                Vector2 previewDimensions = GetPreviewDrawDimensions(layoutPreviewImage);
                float textureWidth = Mathf.Max(1f, previewDimensions.x);
                float textureHeight = Mathf.Max(1f, previewDimensions.y);
                float scale = Mathf.Min(innerRect.width / textureWidth, innerRect.height / textureHeight);
                float drawWidth = textureWidth * scale;
                float drawHeight = textureHeight * scale;

                Rect previewRect = new Rect(
                    innerRect.x + ((innerRect.width - drawWidth) * 0.5f),
                    innerRect.y + ((innerRect.height - drawHeight) * 0.5f),
                    drawWidth,
                    drawHeight);

                Texture2D displayPreviewImage = GetDisplayPreviewImage(previewImage, previewNormalImage) ?? layoutPreviewImage;
                Rect? atlasUv = GetAnimationPreviewTexCoords(displayPreviewImage);
                if (atlasUv.HasValue)
                {
                    GUI.DrawTextureWithTexCoords(previewRect, displayPreviewImage, atlasUv.Value, true);
                }
                else
                {
                    GUI.DrawTexture(previewRect, displayPreviewImage, ScaleMode.StretchToFill, true);
                }
                DrawPreviewOverlay(previewRect, displayPreviewImage);
                if (showToolbar)
                    DrawAnimationPreviewControls(toolbarRect);
                GUILayout.Space(3);
                return shouldRefreshPreview;
            });
        }

        private int GetCurrentSerializedPreviewFrame(int frameCount)
        {
            if (serializedObject == null)
            {
                return 0;
            }

            SerializedProperty previewFrameProp = serializedObject.FindProperty("localSections.animation.currentFrame");
            return previewFrameProp != null
                ? Mathf.Clamp(previewFrameProp.intValue, 0, Mathf.Max(0, frameCount - 1))
                : 0;
        }

        private void DrawAnimationPreviewControls(Rect toolbarRect)
        {
            const float buttonWidth = 32f;
            const float buttonVerticalPadding = 0f;
            float x = toolbarRect.x + (toolbarRect.width - buttonWidth * 2f) * 0.5f;
            float y = toolbarRect.y + buttonVerticalPadding;
            float h = Mathf.Max(1f, toolbarRect.height - buttonVerticalPadding * 2f);

            Rect playRect = new Rect(x, y, buttonWidth, h);
            Rect loopRect = new Rect(x + buttonWidth, y, buttonWidth, h);

            bool isPlaying = IsAnimationPreviewLocked();
            GUIContent playIcon = isPlaying
                ? EditorGUIUtility.IconContent("PreMatQuad")
                : EditorGUIUtility.IconContent("PlayButton");

            using (new EditorGUI.DisabledScope(!isPlaying && (helper.CanGeneratePreview() == false || currentCaptureRoutine != null)))
            {
                bool nextPlaying = GUI.Toggle(playRect, isPlaying, playIcon, EditorStyles.toolbarButton);
                if (nextPlaying != isPlaying)
                {
                    if (isPlaying)
                        CancelAnimationPreview(false);
                    else
                        StartAnimationPreview();
                }
            }

            bool nextLoopPreviewAnimation = GUI.Toggle(loopRect, loopPreviewAnimation, EditorGUIUtility.IconContent("preAudioLoopOff"), EditorStyles.toolbarButton);
            if (nextLoopPreviewAnimation != loopPreviewAnimation)
            {
                SetPreviewLoopAnimation(nextLoopPreviewAnimation);
            }
        }

        private void DrawPreviewOverlay(Rect previewRect, Texture2D previewImage)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (previewImage == null)
            {
                return;
            }

            Color previousColor = Handles.color;
            Color previewBorderColor = EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.65f)
                : new Color(0f, 0f, 0f, 0.28f);

            Handles.BeginGUI();
            Handles.color = previewBorderColor;
            Handles.DrawAAPolyLine(
                EditorGUIUtility.isProSkin ? 1.25f : 1.5f,
                new Vector3(previewRect.xMin, previewRect.yMin),
                new Vector3(previewRect.xMax, previewRect.yMin),
                new Vector3(previewRect.xMax, previewRect.yMax),
                new Vector3(previewRect.xMin, previewRect.yMax),
                new Vector3(previewRect.xMin, previewRect.yMin));

            Handles.EndGUI();

            Handles.color = previousColor;
        }

        private Texture2D GetDisplayPreviewImage(Texture2D previewImage, Texture2D previewNormalImage)
        {
            Texture2D animationPreviewTexture = GetAnimationPreviewTexture();
            if (animationPreviewTexture != null)
            {
                return animationPreviewTexture;
            }

            switch (GetEffectivePreviewDrawMode())
            {
                case DrawMode.Normal:
                    return previewNormalImage != null ? previewNormalImage : previewImage;
                case DrawMode.Unlit:
                    return previewImage;
                case DrawMode.Lit:
                default:
                    return GetLitPreviewImage(previewImage, previewNormalImage);
            }
        }

        private Texture2D GetLayoutPreviewImage(Texture2D previewImage, Texture2D previewNormalImage)
        {
            Texture2D animationPreviewTexture = GetAnimationPreviewTexture();
            if (animationPreviewTexture != null)
            {
                return animationPreviewTexture;
            }

            return previewImage != null ? previewImage : previewNormalImage;
        }

        private Texture2D GetAnimationPreviewTexture()
        {
            if (isPlayingAnimationPreview == false && isGeneratingAnimationPreview == false)
            {
                return null;
            }

            switch (GetEffectivePreviewDrawMode())
            {
                case DrawMode.Normal:
                    return animationPreviewNormalAtlas;
                case DrawMode.Unlit:
                    return animationPreviewDiffuseAtlas;
                case DrawMode.Lit:
                default:
                    return animationPreviewLitAtlas != null ? animationPreviewLitAtlas : animationPreviewDiffuseAtlas;
            }
        }

        private Vector2 GetPreviewDrawDimensions(Texture2D layoutPreviewImage)
        {
            if (GetAnimationPreviewTexture() != null && animationPreviewCellSize.x > 0 && animationPreviewCellSize.y > 0)
            {
                return new Vector2(animationPreviewCellSize.x, animationPreviewCellSize.y);
            }

            return new Vector2(layoutPreviewImage.width, layoutPreviewImage.height);
        }

        private Rect? GetAnimationPreviewTexCoords(Texture2D displayPreviewImage)
        {
            if (displayPreviewImage == null || GetAnimationPreviewTexture() == null || animationPreviewFrameCount <= 0)
            {
                return null;
            }

            int frameIndex = GetCurrentAnimationPreviewFrameIndex();
            int gridCellCount = Mathf.CeilToInt(Mathf.Sqrt(animationPreviewFrameCount));
            int row = frameIndex / gridCellCount;
            int column = frameIndex % gridCellCount;

            float atlasWidth = Mathf.Max(1f, displayPreviewImage.width);
            float atlasHeight = Mathf.Max(1f, displayPreviewImage.height);
            float cellWidth = animationPreviewCellSize.x / atlasWidth;
            float cellHeight = animationPreviewCellSize.y / atlasHeight;
            float x = column * cellWidth;
            float y = 1f - ((row + 1) * cellHeight);

            return new Rect(x, y, cellWidth, cellHeight);
        }

        private int GetCurrentAnimationPreviewFrameIndex()
        {
            return Mathf.Clamp(currentAnimationPreviewFrameIndex, 0, Mathf.Max(0, animationPreviewFrameCount - 1));
        }

        private Texture2D GetLitPreviewImage(Texture2D previewImage, Texture2D previewNormalImage)
        {
            if (previewImage == null)
            {
                DestroyCachedLitPreview();
                return null;
            }

            if (previewNormalImage == null)
            {
                DestroyCachedLitPreview();
                return previewImage;
            }

            if (cachedLitPreviewImage != null
                && cachedLitPreviewSource == previewImage
                && cachedLitPreviewNormalSource == previewNormalImage)
            {
                return cachedLitPreviewImage;
            }

            DestroyCachedLitPreview();

            Color32[] diffusePixels = previewImage.GetPixels32();
            Color32[] normalPixels = previewNormalImage.GetPixels32();
            if (diffusePixels.Length != normalPixels.Length)
            {
                return previewImage;
            }

            Color32[] litPixels = new Color32[diffusePixels.Length];
            const float baseIntensity = 0.85f;
            Vector3 lightDir = GetPreviewLightSourceDirection();

            for (int i = 0; i < diffusePixels.Length; i++)
            {
                Color32 diffuse = diffusePixels[i];
                if (diffuse.a == 0)
                {
                    litPixels[i] = diffuse;
                    continue;
                }

                Vector3 normal = DecodePreviewNormal(normalPixels[i]);
                float ndotl = Mathf.Max(0f, Vector3.Dot(normal, lightDir));
                float directional = ndotl * baseIntensity * FixedPreviewLightIntensity;
                float rLight = Mathf.Clamp01(FixedPreviewAmbientColor.r + directional * FixedPreviewLightColor.r);
                float gLight = Mathf.Clamp01(FixedPreviewAmbientColor.g + directional * FixedPreviewLightColor.g);
                float bLight = Mathf.Clamp01(FixedPreviewAmbientColor.b + directional * FixedPreviewLightColor.b);

                litPixels[i] = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt(diffuse.r * rLight), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(diffuse.g * gLight), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(diffuse.b * bLight), 0, 255),
                    diffuse.a);
            }

            cachedLitPreviewImage = new Texture2D(previewImage.width, previewImage.height, TextureFormat.RGBA32, false)
            {
                filterMode = previewImage.filterMode,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            cachedLitPreviewImage.SetPixels32(litPixels);
            cachedLitPreviewImage.Apply(false, false);
            cachedLitPreviewSource = previewImage;
            cachedLitPreviewNormalSource = previewNormalImage;
            return cachedLitPreviewImage;
        }

        private Texture2D BuildLitAnimationPreviewAtlas(Texture2D diffuseAtlas, Texture2D normalAtlas)
        {
            if (diffuseAtlas == null || normalAtlas == null)
            {
                return null;
            }

            Color32[] diffusePixels = diffuseAtlas.GetPixels32();
            Color32[] normalPixels = normalAtlas.GetPixels32();
            if (diffusePixels.Length != normalPixels.Length)
            {
                return null;
            }

            Color32[] litPixels = new Color32[diffusePixels.Length];
            const float baseIntensity = 0.85f;
            Vector3 lightDirection = GetPreviewLightSourceDirection();

            for (int i = 0; i < diffusePixels.Length; i++)
            {
                Color32 diffuse = diffusePixels[i];
                if (diffuse.a == 0)
                {
                    litPixels[i] = diffuse;
                    continue;
                }

                Vector3 normal = DecodePreviewNormal(normalPixels[i]);
                float ndotl = Mathf.Max(0f, Vector3.Dot(normal, lightDirection));
                float directional = ndotl * baseIntensity * FixedPreviewLightIntensity;
                float rLight = Mathf.Clamp01(FixedPreviewAmbientColor.r + directional * FixedPreviewLightColor.r);
                float gLight = Mathf.Clamp01(FixedPreviewAmbientColor.g + directional * FixedPreviewLightColor.g);
                float bLight = Mathf.Clamp01(FixedPreviewAmbientColor.b + directional * FixedPreviewLightColor.b);

                litPixels[i] = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt(diffuse.r * rLight), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(diffuse.g * gLight), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(diffuse.b * bLight), 0, 255),
                    diffuse.a);
            }

            var litAtlas = new Texture2D(diffuseAtlas.width, diffuseAtlas.height, TextureFormat.RGBA32, false)
            {
                filterMode = diffuseAtlas.filterMode,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            litAtlas.SetPixels32(litPixels);
            litAtlas.Apply(false, false);
            return litAtlas;
        }

        private static Vector3 GetPreviewLightSourceDirection()
        {
            return FixedPreviewLightSourceDirection;
        }

        private static Vector3 DecodePreviewNormal(Color32 encodedNormal)
        {
            Vector3 decoded = new Vector3(
                (encodedNormal.r / 255f) * 2f - 1f,
                (encodedNormal.g / 255f) * 2f - 1f,
                (encodedNormal.b / 255f) * 2f - 1f);

            decoded.z = Mathf.Abs(decoded.z);

            if (decoded.sqrMagnitude <= 0.0001f)
            {
                return Vector3.forward;
            }

            return decoded.normalized;
        }

        private void DestroyCachedLitPreview()
        {
            if (cachedLitPreviewImage != null)
            {
                DestroyImmediate(cachedLitPreviewImage);
                cachedLitPreviewImage = null;
            }

            cachedLitPreviewSource = null;
            cachedLitPreviewNormalSource = null;
        }

        private bool HasAssignedTarget()
        {
            SerializedProperty setupProp = serializedObject.FindProperty("localSections").FindPropertyRelative("setup");
            return setupProp.FindPropertyRelative("target").objectReferenceValue != null;
        }

        private bool HasAssignedCamera()
        {
            return serializedObject.FindProperty("localSections.camera.captureCamera").objectReferenceValue != null;
        }

        private bool TargetNeedsAnimator()
        {
            GameObject targetObject = GetAssignedTarget();
            return targetObject != null && targetObject.GetComponent<Animator>() == null;
        }

        private GameObject GetAssignedTarget()
        {
            SerializedProperty setupProp = serializedObject.FindProperty("localSections").FindPropertyRelative("setup");
            return setupProp.FindPropertyRelative("target").objectReferenceValue as GameObject;
        }

        private bool ShouldHideValidationMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return true;
            }

            SerializedProperty setupProp = serializedObject.FindProperty("localSections").FindPropertyRelative("setup");
            bool hasTarget = setupProp.FindPropertyRelative("target").objectReferenceValue != null;
            bool hasCamera = serializedObject.FindProperty("localSections.camera.captureCamera").objectReferenceValue != null;

            if (!hasTarget)
            {
                return true;
            }

            if (!hasCamera)
            {
                return true;
            }

            if (message.Contains("Animator component"))
            {
                return true;
            }

            return false;
        }

        private void ShowMessage(string message, MessageType messageType)
        {
            GUILayout.Space(8);
            EditorGUILayout.HelpBox(message, messageType);
        }

        private bool DrawAnimationOptionsGUI(SerializedProperty sourceClipEntriesProp)
        {
            bool shouldRefreshPreview = false;

            if (TargetNeedsAnimator())
            {
                EditorGUILayout.HelpBox("Animation capture needs the Target GameObject to have an Animator component.", MessageType.Error);
                if (GUILayout.Button("Add Animator", GUILayout.Height(25)))
                {
                    AddAnimatorToTarget();
                    shouldRefreshPreview = true;
                }
                GUILayout.Space(10f);
            }

            using (var changeScope = new EditorGUI.ChangeCheckScope())
            {
                sourceClipsList?.DoLayoutList();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("localSections.animation.framesPerSecond"), FramesPerSecondContent);
                shouldRefreshPreview |= changeScope.changed;
            }

            if (TryGetFirstAssignedClipEntry(sourceClipEntriesProp, out _, out _) == false)
            {
                return shouldRefreshPreview;
            }

            return shouldRefreshPreview;
        }

        private void AddAnimatorToTarget()
        {
            GameObject targetObject = GetAssignedTarget();
            if (targetObject == null || targetObject.GetComponent<Animator>() != null)
            {
                return;
            }

            Undo.AddComponent<Animator>(targetObject);
            EditorUtility.SetDirty(targetObject);
            Repaint();
        }

        private bool ShouldRunAnimationCapture()
        {
            return helper != null
                && helper.captureType == PixelateCaptureManager.CaptureType.animation
                && HasAssignedAnimationClip();
        }

        private bool HasAssignedAnimationClip()
        {
            SerializedProperty sourceClipEntriesProp = serializedObject.FindProperty("localSections.animation.sourceClipEntries");
            return TryGetFirstAssignedClipEntry(sourceClipEntriesProp, out _, out _);
        }

        private void InitiateBanner()
        {
            Rect contentRect = GUILayoutUtility.GetRect(0f, 76f, GUILayout.ExpandWidth(true));
            headerSection = ToFullWidth(contentRect);

            GUI.DrawTexture(headerSection, headerTexture);

            if (pixelateLogo == null)
            {
                return;
            }

            const float logoWidth = 175f;
            const float logoHeight = 45f;
            Rect logoRect = new Rect(
                contentRect.x,
                headerSection.y + ((headerSection.height - logoHeight) * 0.5f),
                logoWidth,
                logoHeight);
            GUI.DrawTexture(logoRect, pixelateLogo, ScaleMode.ScaleToFit, true);
        }

        private void DrawHelpAndLinksSection()
        {
            DrawSection("Help / Links", () =>
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Documentation", GUILayout.Height(26)))
                    {
                        Application.OpenURL("https://docs.pixelate.tomblack.ca/");
                    }

                    if (GUILayout.Button("Discord Community", GUILayout.Height(26)))
                    {
                        Application.OpenURL("https://discord.gg/ASkVNuet8K");
                    }
                }

                return false;
            });
        }

        private void RunRoutine(IEnumerator routine)
        {
            currentCaptureRoutine = routine;
            EditorApplication.update += UpdateRoutine;
        }

        private void UpdateRoutine()
        {
            if (currentCaptureRoutine == null)
            {
                EditorApplication.update -= UpdateRoutine;
                return;
            }

            if (!currentCaptureRoutine.MoveNext())
            {
                EditorApplication.update -= UpdateRoutine;
                currentCaptureRoutine = null;
                if (isGeneratingAnimationPreview && isPlayingAnimationPreview == false)
                {
                    isGeneratingAnimationPreview = false;
                }
            }
        }

        private void DrawOutputSection()
        {
            DrawSection("Output", () =>
            {
                SerializedProperty exportSettingsProp = serializedObject.FindProperty("localSections").FindPropertyRelative("exportSettings");
                using (var changeScope = new EditorGUI.ChangeCheckScope())
                {
                    EditorGUILayout.PropertyField(exportSettingsProp.FindPropertyRelative("pivot"), PivotContent);
                    SerializedProperty pixelsPerUnitProp = exportSettingsProp.FindPropertyRelative("pixelsPerUnit");
                    EditorGUILayout.PropertyField(pixelsPerUnitProp, PixelsPerUnitContent);
                    pixelsPerUnitProp.floatValue = Mathf.Max(0.01f, pixelsPerUnitProp.floatValue);
                    GUILayout.Space(4f);
                    EditorGUILayout.PropertyField(exportSettingsProp.FindPropertyRelative("overrideCaptures"), OverrideCapturesContent);
                    if (changeScope.changed)
                    {
                        serializedObject.ApplyModifiedProperties();
                    }
                }

                var spriteSavePath = exportSettingsProp.FindPropertyRelative("spriteSavePath");
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel(ExportLocationContent);
                    if (GUILayout.Button(new GUIContent(GetDisplayExportPath(spriteSavePath.stringValue), ExportLocationContent.tooltip), EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                    {
                        OpenExportFolderPicker(spriteSavePath);
                    }
                }

                return false;
            });
        }

        private void ExecuteCapture()
        {
            if (ShouldRunAnimationCapture())
            {
                RunRoutine(helper.CaptureAnimation(SaveCapture));
            }
            else
            {
                RunRoutine(helper.CaptureFrame(SaveCapture));
            }
        }

        private void SavePaletteTexture(Texture2D paletteMap)
        {
            if (paletteMap == null)
            {
                Debug.LogError("Save Palette is only available after Pixelate generates a palette preview.");
                return;
            }

            string paletteFileName = GetSafeTargetName() + "_Palette.png";
            if (!TryBuildAbsoluteSavePath(paletteFileName, out string absolutePalettePath, out string assetPalettePath, subfolder: null))
            {
                return;
            }

            File.WriteAllBytes(absolutePalettePath, paletteMap.EncodeToPNG());
            AssetDatabase.Refresh();

            Texture2D paletteAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPalettePath);
            if (paletteAsset == null)
            {
                Debug.LogError("Pixelate saved the palette PNG but could not reload it as a Unity asset.", helper);
                return;
            }

            ConfigureSavedPaletteAsset(assetPalettePath, paletteAsset);

            Debug.Log($"Saved Pixelate palette to '{assetPalettePath}'.", helper);
        }

        private void SaveLocalProfile(SerializedProperty profileProp)
        {
            if (profileProp == null || helper == null)
            {
                return;
            }

            ApplySerializedObjectChanges();
            profileSerializedObject?.ApplyModifiedProperties();
            helper.SyncLocalProfileFromStoredSettings();
            PixelateProfile localProfile = helper.GetLocalProfile();
            if (localProfile == null)
            {
                return;
            }
            localProfile.Sanitize();

            string folderPath = GetActiveProjectFolderPath();

            string profileName = GetSafeTargetName() + "_PixelateProfile.asset";
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(folderPath.TrimEnd('/') + "/" + profileName);
            var profile = Instantiate(localProfile);
            profile.name = Path.GetFileNameWithoutExtension(assetPath);
            profile.hideFlags = HideFlags.None;
            profile.Sanitize();
            AssetDatabase.CreateAsset(profile, assetPath);
            AssetDatabase.SaveAssets();
            RecordPropertyUndo(profileProp, "Assign Pixelate Profile");
            profileProp.objectReferenceValue = profile;
            serializedObject.ApplyModifiedProperties();
            UpdateProfileSerializedObject(profile);
            EditorGUIUtility.PingObject(profile);
        }

        private static string GetActiveProjectFolderPath()
        {
            const string defaultFolder = "Assets";
            string activeFolderPath = GetProjectWindowActiveFolderPath();
            if (IsProjectAssetFolder(activeFolderPath))
            {
                return activeFolderPath;
            }

            string selectedFolderPath = GetSelectedProjectFolderPath();
            return IsProjectAssetFolder(selectedFolderPath) ? selectedFolderPath : defaultFolder;
        }

        private static string GetProjectWindowActiveFolderPath()
        {
            System.Reflection.MethodInfo getActiveFolderPath = typeof(ProjectWindowUtil).GetMethod(
                "GetActiveFolderPath",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

            return getActiveFolderPath?.Invoke(null, null) as string;
        }

        private static string GetSelectedProjectFolderPath()
        {
            string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return null;
            }

            if (AssetDatabase.IsValidFolder(selectedPath))
            {
                return selectedPath.Replace('\\', '/');
            }

            return Path.GetDirectoryName(selectedPath)?.Replace('\\', '/');
        }

        private static bool IsProjectAssetFolder(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            string normalizedPath = assetPath.Replace('\\', '/');
            return normalizedPath.StartsWith("Assets", System.StringComparison.Ordinal)
                && AssetDatabase.IsValidFolder(normalizedPath);
        }

        private void CloneAssignedProfile(SerializedProperty profileProp)
        {
            if (profileProp == null)
            {
                return;
            }

            PixelateProfile sourceProfile = profileProp.objectReferenceValue as PixelateProfile;
            if (sourceProfile == null)
            {
                return;
            }

            string sourcePath = AssetDatabase.GetAssetPath(sourceProfile);
            string folderPath = string.IsNullOrEmpty(sourcePath) ? "Assets" : Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(folderPath) || EnsureAssetFolderExists(folderPath) == false)
            {
                folderPath = "Assets";
            }

            string assetPath = AssetDatabase.GenerateUniqueAssetPath(folderPath.TrimEnd('/') + "/" + sourceProfile.name + "_Copy.asset");
            var profile = Instantiate(sourceProfile);
            profile.name = Path.GetFileNameWithoutExtension(assetPath);
            profile.hideFlags = HideFlags.None;
            profile.Sanitize();
            AssetDatabase.CreateAsset(profile, assetPath);
            AssetDatabase.SaveAssets();
            RecordPropertyUndo(profileProp, "Assign Pixelate Profile");
            profileProp.objectReferenceValue = profile;
            serializedObject.ApplyModifiedProperties();
            UpdateProfileSerializedObject(profile);
            EditorGUIUtility.PingObject(profile);
        }

        private bool UnlinkAssignedProfile(SerializedProperty profileProp)
        {
            if (profileProp == null || helper == null)
            {
                return false;
            }

            PixelateProfile assignedProfile = profileProp.objectReferenceValue as PixelateProfile;
            if (assignedProfile == null)
            {
                return false;
            }

            profileSerializedObject?.ApplyModifiedProperties();
            RecordPropertyUndo(profileProp, "Unlink Pixelate Profile");
            profileProp.objectReferenceValue = null;
            serializedObject.ApplyModifiedProperties();
            PixelateProfile localProfile = helper.GetLocalProfile();
            Undo.RecordObject(helper, "Unlink Pixelate Profile");
            Undo.RecordObject(localProfile, "Unlink Pixelate Profile");
            helper.CopyProfileToLocal(assignedProfile);
            SetProfileDirty(localProfile);
            serializedObject.Update();
            UpdateProfileSerializedObject(localProfile);
            return true;
        }

        private void SaveCapture(Texture2D diffuseMap, Texture2D normalMap, Texture2D paletteMap, bool createNormalMap, bool slice, bool pixelated, Vector2Int cellSize, Vector2 pivot, float pixelsPerUnit, AnimationClip animClip, int fps, int frameCount)
        {
            if (diffuseMap == null)
            {
                return;
            }

            string targetName = GetSafeTargetName();
            string baseFileName = targetName;
            string captureSubfolder = null;
            if (animClip != null)
            {
                captureSubfolder = SanitizeFileName(animClip.name);
                baseFileName = targetName + "_" + captureSubfolder;
            }

            if (!TryBuildAbsoluteSavePath(baseFileName + ".png", out string diffuseAbsolutePath, out string diffuseAssetPath, captureSubfolder))
            {
                return;
            }

            string normalAbsolutePath = string.Empty;
            string normalAssetPath = string.Empty;
            bool shouldSaveNormalMap = createNormalMap
                && normalMap != null
                && TryBuildAbsoluteSavePath(baseFileName + "_N.png", out normalAbsolutePath, out normalAssetPath, captureSubfolder);

            AutoSpriteSlicer.PrepareExistingAssetForOverwrite(diffuseAssetPath);
            if (shouldSaveNormalMap)
            {
                AutoSpriteSlicer.PrepareExistingAssetForOverwrite(normalAssetPath);
            }

            File.WriteAllBytes(diffuseAbsolutePath, diffuseMap.EncodeToPNG());

            if (shouldSaveNormalMap)
            {
                File.WriteAllBytes(normalAbsolutePath, normalMap.EncodeToPNG());
            }

            ImportGeneratedTexture(diffuseAssetPath);
            if (shouldSaveNormalMap)
            {
                ImportGeneratedTexture(normalAssetPath);
            }

            Texture2D diffuseAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(diffuseAssetPath);
            AutoSpriteSlicer.Slice(diffuseAsset, cellSize, pivot, slice, pixelated, pixelsPerUnit, frameCount, new Vector2Int(diffuseMap.width, diffuseMap.height));

            if (shouldSaveNormalMap)
            {
                Texture2D normalAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(normalAssetPath);
                AutoSpriteSlicer.Slice(normalAsset, cellSize, pivot, slice, pixelated, pixelsPerUnit, frameCount, new Vector2Int(normalMap.width, normalMap.height));
                AssignSecondaryNormalMap(diffuseAssetPath, normalAsset);
            }
        }

        private static void ImportGeneratedTexture(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return;
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private void EnsureDefaultExportPath()
        {
            SerializedProperty spriteSavePath = serializedObject.FindProperty("localSections.exportSettings.spriteSavePath");
            if (PixelateUtilities.TryGetProjectFolderAssetPath(spriteSavePath.stringValue, out _, out _))
            {
                return;
            }

            spriteSavePath.stringValue = PixelateUtilities.DefaultExportFolderDisplayPath;
            serializedObject.ApplyModifiedProperties();
            helper = (PixelateCaptureManager)target;
        }

        private void ConfigureSavedPaletteAsset(string assetPalettePath, Texture2D paletteAsset)
        {
            AutoSpriteSlicer.Slice(paletteAsset, Vector2Int.zero, new Vector2(-1f, -1f), false, true);

            TextureImporter importer = AssetImporter.GetAtPath(assetPalettePath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            PixelateUtilities.ApplyPaletteImportSettings(importer, preserveSpriteTextureType: false);
            importer.SaveAndReimport();
        }

        private bool TryBuildAbsoluteSavePath(string fileName, out string absolutePath, out string assetPath, string subfolder)
        {
            absolutePath = string.Empty;
            assetPath = string.Empty;

            SerializedProperty spriteSavePath = serializedObject.FindProperty("localSections.exportSettings.spriteSavePath");
            if (!PixelateUtilities.TryGetProjectFolderAssetPath(spriteSavePath.stringValue, out string assetFolderPath, out _))
            {
                spriteSavePath.stringValue = PixelateUtilities.DefaultExportFolderDisplayPath;
                serializedObject.ApplyModifiedProperties();
                assetFolderPath = PixelateUtilities.DefaultExportFolderAssetPath;
            }

            string targetFolderAssetPath = BuildTargetExportFolderAssetPath(assetFolderPath);
            if (EnsureAssetFolderExists(targetFolderAssetPath) == false)
            {
                Debug.LogError($"Pixelate could not create or find the export folder '{targetFolderAssetPath}'.", helper);
                return false;
            }

            string outputFolderAssetPath = targetFolderAssetPath;
            if (string.IsNullOrWhiteSpace(subfolder) == false)
            {
                outputFolderAssetPath = (targetFolderAssetPath.TrimEnd('/') + "/" + SanitizeFileName(subfolder)).Replace("//", "/");
                if (EnsureAssetFolderExists(outputFolderAssetPath) == false)
                {
                    Debug.LogError($"Pixelate could not create or find the export folder '{outputFolderAssetPath}'.", helper);
                    return false;
                }
            }

            string candidateAssetPath = (outputFolderAssetPath.TrimEnd('/') + "/" + SanitizeFileName(fileName)).Replace("//", "/");
            bool overrideCaptures = serializedObject.FindProperty("localSections.exportSettings.overrideCaptures").boolValue;
            string resolvedAssetPath = overrideCaptures ? candidateAssetPath : PixelateUtilities.GetSavePathNoOverwrite(candidateAssetPath);
            string relativeAssetPath = resolvedAssetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar);

            assetPath = resolvedAssetPath;
            absolutePath = Path.Combine(Application.dataPath, relativeAssetPath);
            return true;
        }

        private string BuildTargetExportFolderAssetPath(string assetFolderPath)
        {
            return (assetFolderPath.TrimEnd('/') + "/" + GetSafeTargetName()).Replace("//", "/");
        }

        private bool EnsureAssetFolderExists(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath))
            {
                return true;
            }

            string parentFolder = Path.GetDirectoryName(assetFolderPath)?.Replace('\\', '/');
            string folderName = Path.GetFileName(assetFolderPath);
            if (string.IsNullOrWhiteSpace(parentFolder) || string.IsNullOrWhiteSpace(folderName))
            {
                return false;
            }

            if (EnsureAssetFolderExists(parentFolder) == false)
            {
                return false;
            }

            return AssetDatabase.CreateFolder(parentFolder, folderName).Length > 0 && AssetDatabase.IsValidFolder(assetFolderPath);
        }

        private void AssignSecondaryNormalMap(string diffuseAssetPath, Texture2D normalAsset)
        {
            if (normalAsset == null)
            {
                return;
            }

            TextureImporter importer = AssetImporter.GetAtPath(diffuseAssetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            const string normalMapSecondaryName = "_NormalMap";
            var existingSecondaryTextures = importer.secondarySpriteTextures
                .Where(texture => texture.name != normalMapSecondaryName)
                .ToList();

            existingSecondaryTextures.Add(new SecondarySpriteTexture
            {
                name = normalMapSecondaryName,
                texture = normalAsset
            });

            importer.secondarySpriteTextures = existingSecondaryTextures.ToArray();
            importer.SaveAndReimport();
        }

        private string GetSafeTargetName()
        {
            SerializedProperty setupProp = serializedObject.FindProperty("localSections").FindPropertyRelative("setup");
            Object targetObject = setupProp.FindPropertyRelative("target").objectReferenceValue;
            string targetName = targetObject != null ? targetObject.name : "PixelateCapture";
            return SanitizeFileName(targetName.Replace(" ", string.Empty));
        }

        private static string SanitizeFileName(string fileName)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalidChars.Length; i++)
            {
                fileName = fileName.Replace(invalidChars[i], '_');
            }

            return string.IsNullOrWhiteSpace(fileName) ? "PixelateCapture" : fileName;
        }

        private static bool TryGetFirstAssignedClipEntry(SerializedProperty sourceClipEntriesProp, out AnimationClip clip, out float speed)
        {
            clip = null;
            speed = 1f;

            if (sourceClipEntriesProp == null || sourceClipEntriesProp.arraySize == 0)
            {
                return false;
            }

            for (int i = 0; i < sourceClipEntriesProp.arraySize; i++)
            {
                SerializedProperty entry = sourceClipEntriesProp.GetArrayElementAtIndex(i);
                SerializedProperty clipProp = entry.FindPropertyRelative("clip");
                SerializedProperty speedProp = entry.FindPropertyRelative("speed");

                if (clipProp != null && clipProp.objectReferenceValue is AnimationClip assignedClip)
                {
                    clip = assignedClip;
                    speed = speedProp != null ? Mathf.Max(0.01f, speedProp.floatValue) : 1f;
                    return true;
                }
            }

            return false;
        }

        private static MessageType ToMessageType(PixelateCaptureManager.ValidationSeverity severity)
        {
            switch (severity)
            {
                case PixelateCaptureManager.ValidationSeverity.error:
                    return MessageType.Error;
                case PixelateCaptureManager.ValidationSeverity.warning:
                    return MessageType.Warning;
                default:
                    return MessageType.Info;
            }
        }

        private void InitTextures()
        {
            pixelateLogo = Resources.Load<Texture2D>("Editor\\Pixelate Logo Light");

            headerTexture = new Texture2D(1, 1);
            headerTexture.SetPixel(0, 0, EditorGUIUtility.isProSkin ? new Color32(28, 28, 28, 255) : new Color32(170, 170, 170, 255));
            headerTexture.Apply();
        }

        private void InitializeLists()
        {
            SerializedProperty sourceClipEntriesProp = serializedObject.FindProperty("localSections.animation.sourceClipEntries");
            sourceClipsList = new ReorderableList(serializedObject, sourceClipEntriesProp, true, true, true, true);
            sourceClipsList.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, SourceClipsContent);
            };
            sourceClipsList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                SerializedProperty element = sourceClipEntriesProp.GetArrayElementAtIndex(index);
                SerializedProperty clipProp = element.FindPropertyRelative("clip");
                SerializedProperty speedProp = element.FindPropertyRelative("speed");
                rect.y += 1f;
                rect.height = EditorGUIUtility.singleLineHeight;
                const float spacing = 6f;
                const float speedLabelWidth = 42f;
                const float speedFieldWidth = 64f;
                Rect clipRect = new Rect(rect.x, rect.y, Mathf.Max(40f, rect.width - speedLabelWidth - speedFieldWidth - spacing * 2f), rect.height);
                Rect speedLabelRect = new Rect(clipRect.xMax + spacing, rect.y, speedLabelWidth, rect.height);
                Rect speedRect = new Rect(speedLabelRect.xMax + spacing, rect.y, speedFieldWidth, rect.height);

                EditorGUI.PropertyField(clipRect, clipProp, GUIContent.none);
                GUI.Label(clipRect, SourceClipContent);
                EditorGUI.LabelField(speedLabelRect, SpeedContent);
                if (speedProp != null)
                {
                    using (var speedChangeScope = new EditorGUI.ChangeCheckScope())
                    {
                        float speed = Mathf.Max(0.01f, EditorGUI.FloatField(speedRect, speedProp.floatValue <= 0f ? 1f : speedProp.floatValue));
                        GUI.Label(speedRect, new GUIContent("", SpeedContent.tooltip));
                        if (speedChangeScope.changed)
                        {
                            RecordPropertyUndo(speedProp, "Change Pixelate Clip Speed");
                            speedProp.floatValue = speed;
                        }
                    }
                }
            };
            sourceClipsList.elementHeight = EditorGUIUtility.singleLineHeight + 4f;
            sourceClipsList.onAddCallback = list =>
            {
                int newIndex = sourceClipEntriesProp.arraySize;
                RecordPropertyUndo(sourceClipEntriesProp, "Add Pixelate Source Clip");
                sourceClipEntriesProp.arraySize++;
                SerializedProperty newEntry = sourceClipEntriesProp.GetArrayElementAtIndex(newIndex);
                SerializedProperty clipProp = newEntry.FindPropertyRelative("clip");
                SerializedProperty speedProp = newEntry.FindPropertyRelative("speed");
                if (clipProp != null)
                {
                    clipProp.objectReferenceValue = null;
                }

                if (speedProp != null)
                {
                    speedProp.floatValue = 1f;
                }
            };
        }

        private static string GetDisplayExportPath(string exportPath)
        {
            if (PixelateUtilities.TryGetProjectFolderAssetPath(exportPath, out string assetFolderPath, out _))
            {
                return assetFolderPath == PixelateUtilities.DefaultExportFolderAssetPath ? PixelateUtilities.DefaultExportFolderDisplayPath : assetFolderPath;
            }

            return PixelateUtilities.DefaultExportFolderDisplayPath;
        }

        private void OpenExportFolderPicker(SerializedProperty spriteSavePath)
        {
            string selectedPath = EditorUtility.OpenFolderPanel("Sprite Export Location", PixelateUtilities.GetAbsoluteProjectFolderPathOrDefault(spriteSavePath.stringValue), string.Empty);
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                RecordPropertyUndo(spriteSavePath, "Change Pixelate Export Location");
                spriteSavePath.stringValue = selectedPath;
            }

            serializedObject.ApplyModifiedProperties();
            GUIUtility.ExitGUI();
        }
    }
}
