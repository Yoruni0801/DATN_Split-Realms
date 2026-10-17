using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Pixelate
{
    [ExecuteInEditMode]
    [HelpURL("https://docs.pixelate.tomblack.ca/")]
    public class PixelateCaptureManager : MonoBehaviour
    {
        private const string BuiltInNormalCaptureShaderName = "Hidden/SpriteTangentNormal";
        private const string NormalLightingStepsPropertyName = "_NormalLightingSteps";
        private static readonly Color NormalCaptureBackgroundColor = new Color(0.5f, 0.5f, 1.0f, 0.0f);

        public enum CaptureType
        {
            image = 0,
            animation = 1,
        }

        public enum PaletteStyle
        {
            auto = 0,
            custom = 1,
            none = 2,
        }

        public enum AutoPaletteDetail
        {
            low = 0,
            medium = 1,
            high = 2,
            custom = 3,
        }

        public const int LowAutoPaletteColorCount = 8;
        public const int MediumAutoPaletteColorCount = 12;
        public const int HighAutoPaletteColorCount = 16;

        public enum MaterialPipeline
        {
            [InspectorName("URP")] urp = 0,
            [InspectorName("Built-in")] builtIn = 1,
        }

        public enum ValidationSeverity
        {
            info = 0,
            warning = 1,
            error = 2,
        }

        public struct ValidationMessage
        {
            public ValidationMessage(ValidationSeverity severity, string message)
            {
                Severity = severity;
                Message = message;
            }

            public ValidationSeverity Severity { get; }
            public string Message { get; }
            public bool IsBlocking => Severity == ValidationSeverity.error;
        }

        [Serializable]
        public struct SourceClipEntry
        {
            public AnimationClip clip;
            public float speed;
        }

        public PixelateProfile profile;
        [SerializeField, HideInInspector] private PixelateProfile localProfile;
        [SerializeField, HideInInspector] private PixelateProfileSections localProfileSections = new PixelateProfileSections();
        public PixelateManagerSections localSections = new PixelateManagerSections();

        public CaptureType captureType;
        [SerializeField, HideInInspector] private GameObject _target = null;

        [SerializeField, HideInInspector] private AnimationClip[] _sourceClips = null;
        [SerializeField, HideInInspector] private SourceClipEntry[] _sourceClipEntries = null;
        [SerializeField, HideInInspector] private int _framesPerSecond = 12;
        [SerializeField, HideInInspector] private int _currentFrame = 0;

        [SerializeField, HideInInspector] private Camera _captureCamera = null;
        [SerializeField] private Vector2Int _cellSize = new Vector2Int(64, 64);
        [SerializeField] private Vector2 _pivot = new Vector2(0.5f, 0.5f);
#pragma warning disable CS0414
        [SerializeField] private bool _createNormalMap = true;
#pragma warning restore CS0414
        [SerializeField] private int _normalLightingSteps = 2;
        [SerializeField] private MaterialPipeline _materialPipeline = MaterialPipeline.urp;
        [SerializeField, HideInInspector] private Texture2D _previewImage = null;
        [SerializeField, HideInInspector] private Texture2D _previewNormalImage = null;
        [SerializeField] private bool _pixelated = true;
#pragma warning disable CS0414
        [SerializeField, HideInInspector] private bool _overrideCaptures = true;
#pragma warning restore CS0414
        [SerializeField, HideInInspector] private string _spriteSavePath = "";

        public PaletteStyle paletteStyle = PaletteStyle.auto;
        public PaletteStyle oldPaletteStyle;
        [SerializeField] private AutoPaletteDetail _autoPaletteDetail = AutoPaletteDetail.medium;
        [SerializeField] private Texture2D _customPalette;
        [SerializeField] private int _paletteColorCount = 16;

        [SerializeField, HideInInspector] private bool migratedLocalSections;
        [SerializeField, HideInInspector] private bool migratedLocalProfileSections;
        [SerializeField, HideInInspector] private bool migratedRenderSettingsToLocal;
        [SerializeField, HideInInspector] private bool migratedRenderSettingsToCameraOutput;
        [SerializeField, HideInInspector] private bool migratedPixelatedToCamera;

        [SerializeField] private Texture2D _generatedPalette = null;
        private RenderTexture _blitRenderTexture;
        private Coroutine _previewCoroutine;
        private IEnumerator _editorPreviewRoutine;
        private bool _isCapturing;
        private int _previewRequestVersion;
        private bool _previewOwnsEditorAnimationMode;
        private bool _previewNeedsDiffuse = true;
        private bool _previewNeedsNormal = true;
        private GameObject _previewPoseTarget;
        private bool _cancelAnimationPreviewBuild;

        public event Action PreviewUpdated;

        private const int MaxAtlasSize = 4096;
        private const float PaletteColorConsolidationThreshold = 3.8f;

        public PixelateProfile GetActiveProfile()
        {
            PixelateProfile activeProfile = profile != null ? profile : GetOrCreateLocalProfile();
            activeProfile.Sanitize();
            return activeProfile;
        }

        public PixelateProfile GetLocalProfile()
        {
            return GetOrCreateLocalProfile();
        }

        public bool HasLocalProfile()
        {
            return localProfile != null;
        }

        public void CopyProfileToLocal(PixelateProfile sourceProfile)
        {
            PixelateProfile destinationProfile = GetOrCreateLocalProfile();
            destinationProfile.CopySettingsFrom(sourceProfile);
            SyncStoredLocalProfileSettingsFromLocalProfile();
        }

        private PixelateProfile GetOrCreateLocalProfile(bool preserveLegacyAutoPaletteColorCount = false)
        {
            bool shouldPreserveLegacyAutoPaletteColorCount = preserveLegacyAutoPaletteColorCount || migratedLocalSections == false;
            EnsureLocalProfileSections();
            if (migratedLocalProfileSections == false)
            {
                if (localProfile == null)
                {
                    localProfile = CreateLocalProfileInstance();
                    ApplyLegacyProfileFieldsToLocalProfile(shouldPreserveLegacyAutoPaletteColorCount);
                }

                localProfile.Sanitize();
                localProfile.CopySettingsTo(localProfileSections);
                migratedLocalProfileSections = true;
            }

            if (localProfile == null)
            {
                localProfile = CreateLocalProfileInstance();
            }

            localProfile.CopySettingsFrom(localProfileSections);
            localProfile.Sanitize();
            return localProfile;
        }

        private static PixelateProfile CreateLocalProfileInstance()
        {
            PixelateProfile profileInstance = ScriptableObject.CreateInstance<PixelateProfile>();
            profileInstance.name = "Local Pixelate Profile";
            profileInstance.hideFlags = HideFlags.HideInHierarchy;
            return profileInstance;
        }

        private void EnsureLocalProfileSections()
        {
            if (localProfileSections == null)
            {
                localProfileSections = new PixelateProfileSections();
            }

            localProfileSections.Sanitize();
        }

        public void SyncLocalProfileFromStoredSettings()
        {
            EnsureLocalProfileSections();
            if (localProfile == null)
            {
                localProfile = CreateLocalProfileInstance();
            }

            localProfile.CopySettingsFrom(localProfileSections);
            localProfile.Sanitize();
        }

        public void SyncStoredLocalProfileSettingsFromLocalProfile()
        {
            EnsureLocalProfileSections();
            if (localProfile == null)
            {
                return;
            }

            localProfile.Sanitize();
            localProfile.CopySettingsTo(localProfileSections);
        }

        private void ApplyLegacyProfileFieldsToLocalProfile(bool preserveLegacyAutoPaletteColorCount)
        {
            if (localProfile == null)
            {
                return;
            }

            localProfile.sections.palette.enabled = paletteStyle != PaletteStyle.none;
            localProfile.sections.palette.style = paletteStyle == PaletteStyle.none ? PaletteStyle.auto : paletteStyle;
            localProfile.sections.palette.autoDetail = preserveLegacyAutoPaletteColorCount && paletteStyle == PaletteStyle.auto
                ? AutoPaletteDetail.custom
                : _autoPaletteDetail;
            localProfile.sections.palette.customPalette = _customPalette;
            localProfile.sections.palette.colorCount = _paletteColorCount;
            localProfile.sections.lighting.enabled = _createNormalMap;
            localProfile.sections.lighting.lightingSteps = _normalLightingSteps;
            localProfile.sections.lighting.materialPipeline = _materialPipeline;
            localProfile.Sanitize();
        }

        private GameObject TargetObject => localSections.setup.target;
        private Camera CaptureCamera => localSections.camera.captureCamera;
        private SourceClipEntry[] SourceClipEntries
        {
            get => localSections.animation.sourceClipEntries;
            set => localSections.animation.sourceClipEntries = value;
        }
        private int FramesPerSecond
        {
            get => localSections.animation.framesPerSecond;
            set => localSections.animation.framesPerSecond = Mathf.Max(2, value);
        }
        private int CurrentFrame
        {
            get => localSections.animation.currentFrame;
            set => localSections.animation.currentFrame = Mathf.Max(0, value);
        }
        private Texture2D PreviewImage
        {
            get => localSections.preview.previewImage;
            set => localSections.preview.previewImage = value;
        }
        private Texture2D PreviewNormalImage
        {
            get => localSections.preview.previewNormalImage;
            set => localSections.preview.previewNormalImage = value;
        }
        private bool OverrideCaptures => localSections.exportSettings.overrideCaptures;
        private string SpriteSavePath
        {
            get => localSections.exportSettings.spriteSavePath;
            set => localSections.exportSettings.spriteSavePath = value;
        }
        private Vector2Int CellSize => localSections.camera.cellSize;
        private Vector2 Pivot => localSections.exportSettings.pivot;
        private PixelateProfile ActiveProfile => GetActiveProfile();
        private bool CreateNormalMap => ActiveProfile.sections.lighting.enabled;
        private int NormalLightingSteps => ActiveProfile.sections.lighting.lightingSteps;
        private MaterialPipeline ActiveMaterialPipeline => ActiveProfile.sections.lighting.materialPipeline;
        private bool Pixelated => localSections.camera.pixelated;
        private float PixelsPerUnit => localSections.exportSettings.pixelsPerUnit;
        private PaletteStyle ActivePaletteStyle => ActiveProfile.sections.palette.EffectiveStyle;
        private AutoPaletteDetail ActiveAutoPaletteDetail => ActiveProfile.sections.palette.autoDetail;
        private Texture2D ActiveCustomPalette => ActiveProfile.sections.palette.customPalette;
        private int ActivePaletteColorCount => ActiveProfile.sections.palette.colorCount;

        private sealed class NormalCaptureOverrideState
        {
            public Renderer[] Renderers;
            public Material[][] OriginalMaterials;
            public List<Material> TemporaryMaterials;
        }

#if UNITY_EDITOR
        private sealed class InspectorTargetIsolationState
        {
            public Renderer[] Renderers;
            public bool[] OriginalForceRenderingOff;
            public Terrain[] Terrains;
            public bool[] OriginalTerrainEnabled;
        }

        private sealed class PreviewPoseVisibilityState
        {
            public Renderer[] Renderers;
            public bool[] OriginalForceRenderingOff;
        }

        private InspectorTargetIsolationState _inspectorTargetIsolationState;
        private GameObject _inspectorTargetIsolationTarget;
        private PreviewPoseVisibilityState _previewPoseVisibilityState;
        private int _previewPoseVisibilityVersion;
#endif

        private void Reset()
        {
            MigrateUnsupportedCaptureTypeIfNeeded();
            MigrateLocalSectionsIfNeeded();
            MigrateRenderSettingsToLocalIfNeeded();
            MigrateRenderSettingsToCameraOutputIfNeeded();
            MigratePixelatedToCameraIfNeeded();
            GetOrCreateLocalProfile();
        }

        private void OnValidate()
        {
            bool preserveLegacyAutoPaletteColorCount = migratedLocalSections == false;
            MigrateUnsupportedCaptureTypeIfNeeded();
            MigrateLocalSectionsIfNeeded();
            MigrateRenderSettingsToLocalIfNeeded();
            MigrateRenderSettingsToCameraOutputIfNeeded();
            MigratePixelatedToCameraIfNeeded();
            MigrateLegacySourceClipsIfNeeded();
            localSections.Sanitize();
            _paletteColorCount = Mathf.Max(1, _paletteColorCount);
            _normalLightingSteps = Mathf.Max(1, _normalLightingSteps);
            _cellSize.x = Mathf.Max(1, _cellSize.x);
            _cellSize.y = Mathf.Max(1, _cellSize.y);
            _pivot.x = Mathf.Clamp01(_pivot.x);
            _pivot.y = Mathf.Clamp01(_pivot.y);
            GetOrCreateLocalProfile(preserveLegacyAutoPaletteColorCount);
#if UNITY_EDITOR
            ResetInvalidExportPathToDefault();
#endif
            if (profile != null)
            {
                profile.Sanitize();
            }

            if (CanGeneratePreview() == false)
            {
                PreviewImage = null;
                return;
            }
        }

        private void MigrateUnsupportedCaptureTypeIfNeeded()
        {
            if (captureType != CaptureType.image && captureType != CaptureType.animation)
            {
                captureType = CaptureType.image;
            }
        }

        private void MigrateLocalSectionsIfNeeded()
        {
            if (localSections == null)
            {
                localSections = new PixelateManagerSections();
            }

            localSections.Sanitize();

            if (migratedLocalSections)
            {
                return;
            }

            localSections.setup.target = _target;
            localSections.camera.captureCamera = _captureCamera;
            localSections.camera.cellSize = _cellSize;
            localSections.camera.pixelated = _pixelated;
            localSections.animation.sourceClipEntries = _sourceClipEntries;
            localSections.animation.framesPerSecond = _framesPerSecond;
            localSections.animation.currentFrame = _currentFrame;
            localSections.preview.previewImage = _previewImage;
            localSections.preview.previewNormalImage = _previewNormalImage;
            localSections.render.cellSize = _cellSize;
            localSections.render.pixelated = _pixelated;
            localSections.exportSettings.overrideCaptures = _overrideCaptures;
            localSections.exportSettings.pivot = _pivot;
            localSections.exportSettings.pixelated = _pixelated;
            localSections.exportSettings.spriteSavePath = _spriteSavePath;
            localSections.Sanitize();
            migratedLocalSections = true;
        }

#if UNITY_EDITOR
        private void ResetInvalidExportPathToDefault()
        {
            if (PixelateUtilities.TryGetProjectFolderAssetPath(SpriteSavePath, out _, out _))
            {
                return;
            }

            SpriteSavePath = PixelateUtilities.DefaultExportFolderDisplayPath;
        }
#endif

        private void MigrateRenderSettingsToLocalIfNeeded()
        {
            if (migratedRenderSettingsToLocal)
            {
                return;
            }

            if (profile != null)
            {
                profile.Sanitize();
                PixelateProfile.RenderSettings profileRender = profile.sections.render;
                localSections.render.cellSize = profileRender.cellSize;
                localSections.render.linkCellSizeAxes = profileRender.linkCellSizeAxes;
                localSections.render.pixelated = profileRender.pixelated;
                localSections.exportSettings.pivot = profileRender.pivot;
            }
            else
            {
                localSections.render.cellSize = _cellSize;
                localSections.render.pixelated = _pixelated;
                localSections.exportSettings.pivot = _pivot;
            }

            localSections.Sanitize();
            migratedRenderSettingsToLocal = true;
        }

        private void MigrateRenderSettingsToCameraOutputIfNeeded()
        {
            if (migratedRenderSettingsToCameraOutput)
            {
                return;
            }

            PixelateProfile.RenderSettings profileRender = null;
            if (profile != null)
            {
                profile.Sanitize();
                profileRender = profile.sections.render;
            }

            if (migratedRenderSettingsToLocal && localSections.render != null)
            {
                localSections.camera.cellSize = localSections.render.cellSize;
                localSections.camera.linkCellSizeAxes = localSections.render.linkCellSizeAxes;
                localSections.camera.pixelated = localSections.render.pixelated;
                localSections.exportSettings.pixelated = localSections.render.pixelated;
            }
            else if (profileRender != null)
            {
                localSections.camera.cellSize = profileRender.cellSize;
                localSections.camera.linkCellSizeAxes = profileRender.linkCellSizeAxes;
                localSections.camera.pixelated = profileRender.pixelated;
                localSections.exportSettings.pixelated = profileRender.pixelated;
                localSections.exportSettings.pivot = profileRender.pivot;
            }
            else
            {
                localSections.camera.cellSize = _cellSize;
                localSections.camera.pixelated = _pixelated;
                localSections.exportSettings.pixelated = _pixelated;
                localSections.exportSettings.pivot = _pivot;
            }

            localSections.Sanitize();
            migratedRenderSettingsToCameraOutput = true;
        }

        private void MigratePixelatedToCameraIfNeeded()
        {
            if (migratedPixelatedToCamera)
            {
                return;
            }

            if (migratedRenderSettingsToCameraOutput)
            {
                localSections.camera.pixelated = localSections.exportSettings.pixelated;
            }
            else if (migratedRenderSettingsToLocal && localSections.render != null)
            {
                localSections.camera.pixelated = localSections.render.pixelated;
            }
            else if (profile != null)
            {
                profile.Sanitize();
                localSections.camera.pixelated = profile.sections.render.pixelated;
            }
            else
            {
                localSections.camera.pixelated = _pixelated;
            }

            localSections.Sanitize();
            migratedPixelatedToCamera = true;
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            EndAnimationPreviewPoseVisibilityHoldForEditor(repaintSceneView: false);
            ClearInspectorTargetIsolation();
#endif
            CancelPendingPreviewRefresh();
        }

        private void MigrateLegacySourceClipsIfNeeded()
        {
            if (SourceClipEntries != null && SourceClipEntries.Length > 0)
            {
                for (int i = 0; i < SourceClipEntries.Length; i++)
                {
                    SourceClipEntries[i].speed = SanitizeClipSpeed(SourceClipEntries[i].speed);
                }

                return;
            }

            if (_sourceClips == null || _sourceClips.Length == 0)
            {
                return;
            }

            SourceClipEntries = new SourceClipEntry[_sourceClips.Length];
            for (int i = 0; i < _sourceClips.Length; i++)
            {
                SourceClipEntries[i] = new SourceClipEntry
                {
                    clip = _sourceClips[i],
                    speed = 1f,
                };
            }
        }

        public List<ValidationMessage> GetValidationMessages(bool includeExportValidation)
        {
            var messages = new List<ValidationMessage>();

            if (TargetObject == null)
            {
                messages.Add(new ValidationMessage(ValidationSeverity.error, "Assign a Target GameObject before previewing or capturing."));
            }

            if (CaptureCamera == null)
            {
                messages.Add(new ValidationMessage(ValidationSeverity.error, "Assign a Capture Camera before previewing or capturing."));
            }

            Vector2Int cellSize = CellSize;
            if (cellSize.x <= 0 || cellSize.y <= 0)
            {
                messages.Add(new ValidationMessage(ValidationSeverity.error, "Capture resolution is invalid. Set both Cell Size values above 0 before previewing or capturing."));
            }

            if (captureType == CaptureType.animation)
            {
                AnimationClip firstClip = GetPrimaryAnimationClip();
                if (firstClip != null && TargetHasAnimator() == false)
                {
                    messages.Add(new ValidationMessage(ValidationSeverity.error, "Animation capture requires the Target GameObject to have an Animator component."));
                }
            }

            var atlasSize = GetCurrentAtlasSize();
            if (atlasSize.x > MaxAtlasSize || atlasSize.y > MaxAtlasSize)
            {
                messages.Add(new ValidationMessage(ValidationSeverity.error, $"This setup would generate an atlas of {atlasSize.x}x{atlasSize.y}. Reduce the cell size, clip length, or Frames Per Second so the atlas stays at or below {MaxAtlasSize}x{MaxAtlasSize}."));
            }

#if UNITY_EDITOR
            if (includeExportValidation)
            {
                ResetInvalidExportPathToDefault();
            }
#endif

            return messages;
        }

        public bool HasBlockingIssues(bool includeExportValidation)
        {
            var messages = GetValidationMessages(includeExportValidation);
            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i].IsBlocking)
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasGeneratedPalette()
        {
            return _generatedPalette != null;
        }

        public Texture2D GetPreviewImage()
        {
            return PreviewImage;
        }

        public Texture2D GetPreviewNormalImage()
        {
            return PreviewNormalImage;
        }

        public Camera GetCaptureCamera()
        {
            return CaptureCamera;
        }

        public void CancelAnimationPreviewBuild()
        {
            _cancelAnimationPreviewBuild = true;
        }

        public void ClearAnimationPreviewPoseForEditor(bool repaintSceneView = true)
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                return;
            }

            if (_isCapturing)
            {
                _cancelAnimationPreviewBuild = true;
                return;
            }

            EndAnimationPreviewPoseVisibilityHoldForEditor(repaintSceneView: false);
            CancelPendingPreviewRefresh();
            if (repaintSceneView)
            {
                RepaintAnimationPoseViews();
            }
#endif
        }

        public void BeginAnimationPreviewPoseVisibilityHoldForEditor()
        {
#if UNITY_EDITOR
            if (Application.isPlaying || TargetObject == null)
            {
                return;
            }

            EndAnimationPreviewPoseVisibilityHoldForEditor(repaintSceneView: false);
            Renderer[] renderers = TargetObject.GetComponentsInChildren<Renderer>(true);
            var visibleRenderers = new List<Renderer>(renderers.Length);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || EditorUtility.IsPersistent(renderer))
                {
                    continue;
                }

                visibleRenderers.Add(renderer);
            }

            if (visibleRenderers.Count == 0)
            {
                return;
            }

            _previewPoseVisibilityVersion++;
            _previewPoseVisibilityState = new PreviewPoseVisibilityState
            {
                Renderers = visibleRenderers.ToArray(),
                OriginalForceRenderingOff = new bool[visibleRenderers.Count],
            };

            for (int i = 0; i < _previewPoseVisibilityState.Renderers.Length; i++)
            {
                Renderer renderer = _previewPoseVisibilityState.Renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                _previewPoseVisibilityState.OriginalForceRenderingOff[i] = renderer.forceRenderingOff;
                renderer.forceRenderingOff = true;
            }
#endif
        }

        public void QueueAnimationPreviewPoseVisibilityReleaseForEditor()
        {
#if UNITY_EDITOR
            int visibilityVersion = _previewPoseVisibilityVersion;
            EditorApplication.delayCall += () => QueueAnimationPreviewPoseVisibilityReleaseForEditor(visibilityVersion, 2);
#endif
        }

        private void QueueAnimationPreviewPoseVisibilityReleaseForEditor(int visibilityVersion, int remainingDelayCalls)
        {
#if UNITY_EDITOR
            if (this == null || _previewPoseVisibilityVersion != visibilityVersion || _previewPoseVisibilityState == null)
            {
                return;
            }

            if (remainingDelayCalls > 0)
            {
                EditorApplication.delayCall += () => QueueAnimationPreviewPoseVisibilityReleaseForEditor(visibilityVersion, remainingDelayCalls - 1);
                return;
            }

            EndAnimationPreviewPoseVisibilityHoldForEditor();
#endif
        }

        public void EndAnimationPreviewPoseVisibilityHoldForEditor(bool repaintSceneView = true)
        {
#if UNITY_EDITOR
            if (_previewPoseVisibilityState == null)
            {
                return;
            }

            for (int i = 0; i < _previewPoseVisibilityState.Renderers.Length; i++)
            {
                Renderer renderer = _previewPoseVisibilityState.Renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.forceRenderingOff = _previewPoseVisibilityState.OriginalForceRenderingOff[i];
            }

            _previewPoseVisibilityState = null;
            if (repaintSceneView)
            {
                RepaintAnimationPoseViews();
            }
#endif
        }

        public void RefreshPreview()
        {
            RefreshPreview(true, true);
        }

        public void RefreshPreview(bool includeDiffuse, bool includeNormal)
        {
            if (_isCapturing)
            {
                return;
            }

            bool preserveAnimationPreviewPose = ShouldPreserveAnimationPreviewPoseForRefresh();
            _previewNeedsDiffuse = includeDiffuse;
            _previewNeedsNormal = includeNormal;
            _previewRequestVersion++;
            NormalizePreviewStateForCurrentMode();
            CancelPendingPreviewRefresh(preserveAnimationPreviewPose);

            if (CanGeneratePreview() == false)
            {
#if UNITY_EDITOR
                EndAnimationPreviewPoseVisibilityHoldForEditor(repaintSceneView: false);
#endif
                ReplacePreviewTexture(ref localSections.preview.previewImage, null);
                ReplacePreviewTexture(ref localSections.preview.previewNormalImage, null);
                NotifyPreviewUpdated();
                return;
            }

#if UNITY_EDITOR
            if (Application.isPlaying == false)
            {
                PrimeAnimationPreviewPoseForEditor();
                StartEditorPreviewRoutine(_previewRequestVersion);
                return;
            }
#endif

            StartPreviewCoroutine(_previewRequestVersion);
        }

        public bool PrimeAnimationPreviewPoseForEditor(bool repaintSceneView = false)
        {
#if UNITY_EDITOR
            if (Application.isPlaying
                || captureType != CaptureType.animation
                || TargetObject == null
                || TargetHasAnimator() == false)
            {
                return false;
            }

            SourceClipEntry? previewEntry = GetPrimarySourceClipEntry();
            if (previewEntry.HasValue == false || previewEntry.Value.clip == null)
            {
                return false;
            }

            AnimationClip previewClip = previewEntry.Value.clip;
            float previewSpeed = SanitizeClipSpeed(previewEntry.Value.speed);
            int numFrames = Mathf.Max(1, GetFrameCount(previewClip, previewSpeed));
            float currentTime = GetAnimationSampleTime(previewClip, CurrentFrame, numFrames, previewSpeed);
            bool startedEditorAnimationMode = BeginEditorAnimationSampling();

            SampleAnimationForCapture(currentTime, previewClip, repaintSceneView: false);
            if (repaintSceneView)
            {
                RepaintAnimationPoseViews();
            }

            if (startedEditorAnimationMode || _previewOwnsEditorAnimationMode)
            {
                _previewOwnsEditorAnimationMode = true;
                _previewPoseTarget = TargetObject;
            }
            return true;
#else
            return false;
#endif
        }

        public bool CanGeneratePreview()
        {
            if (TargetObject == null || CaptureCamera == null)
            {
                return false;
            }

            Vector2Int cellSize = CellSize;
            if (cellSize.x <= 0 || cellSize.y <= 0)
            {
                return false;
            }

            if (captureType == CaptureType.animation && GetPrimaryAnimationClip() != null && TargetHasAnimator() == false)
            {
                return false;
            }

            return HasBlockingIssues(includeExportValidation: false) == false;
        }

        public bool TryGetValidationMessage(bool includeExportValidation, out string message)
        {
            List<ValidationMessage> messages = GetValidationMessages(includeExportValidation);
            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i].IsBlocking)
                {
                    message = messages[i].Message;
                    return false;
                }
            }

            for (int i = 0; i < messages.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(messages[i].Message) == false)
                {
                    message = messages[i].Message;
                    return false;
                }
            }

            message = string.Empty;
            return true;
        }

        public int GetFrameCount(AnimationClip clip)
        {
            if (clip == null)
            {
                return 1;
            }

            return GetFrameCount(clip, GetPrimaryAnimationClipSpeed());
        }

        public int GetFrameCount(AnimationClip clip, float speed)
        {
            if (clip == null)
            {
                return 1;
            }

            return GetFrameCount(GetEffectiveAnimationDuration(clip.length, speed));
        }

        public float GetPrimaryAnimationClipSpeed()
        {
            SourceClipEntry? entry = GetPrimarySourceClipEntry();
            return entry.HasValue ? SanitizeClipSpeed(entry.Value.speed) : 1f;
        }

        public void SampleAnimation(float time, AnimationClip animClip)
        {
            SampleAnimationForPreview(time, animClip, repaintSceneView: true);
        }

        private void SampleAnimationForPreview(float time, AnimationClip animClip, bool repaintSceneView)
        {
            if (TargetObject == null || animClip == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (Application.isPlaying == false)
            {
                bool startedAnimationMode = false;
                if (AnimationMode.InAnimationMode() == false)
                {
                    AnimationMode.StartAnimationMode();
                    startedAnimationMode = true;
                }

                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(TargetObject, animClip, time);
                AnimationMode.EndSampling();
                if (repaintSceneView)
                {
                    RepaintAnimationPoseViews();
                }

                if (startedAnimationMode)
                {
                    AnimationMode.StopAnimationMode();
                }
                return;
            }
#endif

            animClip.SampleAnimation(TargetObject, time);
#if UNITY_EDITOR
            if (repaintSceneView)
            {
                RepaintAnimationPoseViews();
            }
#endif
        }

        private void SampleAnimationForCapture(float time, AnimationClip animClip, bool repaintSceneView = true)
        {
            if (TargetObject == null || animClip == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (Application.isPlaying == false)
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(TargetObject, animClip, time);
                AnimationMode.EndSampling();
                if (repaintSceneView)
                {
                    RepaintAnimationPoseViews();
                }
                return;
            }
#endif

            animClip.SampleAnimation(TargetObject, time);
        }

        private static void RepaintAnimationPoseViews()
        {
#if UNITY_EDITOR
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
#endif
        }

        private void GenerateNewPalette(Texture2D diffuseMap)
        {
            Color32[] generatedPalette = PixelateUtilities.GenerateFallbackPalette(
                diffuseMap,
                ResolveAutoPaletteColorCount(diffuseMap),
                PaletteColorConsolidationThreshold);

            if (ShouldUseFallbackAutoPalette(generatedPalette))
            {
                ReplaceGeneratedPalette(null);
                return;
            }

            ReplaceGeneratedPalette(PixelateUtilities.DrawPalette32(generatedPalette));
        }

        public int ResolveAutoPaletteColorCount(Texture2D sourceTexture)
        {
            return ResolveAutoPaletteDetailColorCount(ActiveAutoPaletteDetail, ActivePaletteColorCount);
        }

        public int GetConfiguredAutoPaletteColorCount()
        {
            return ResolveAutoPaletteDetailColorCount(ActiveAutoPaletteDetail, ActivePaletteColorCount);
        }

        public static int ResolveAutoPaletteDetailColorCount(AutoPaletteDetail detail, int customColorCount)
        {
            switch (detail)
            {
                case AutoPaletteDetail.low:
                    return LowAutoPaletteColorCount;
                case AutoPaletteDetail.medium:
                    return MediumAutoPaletteColorCount;
                case AutoPaletteDetail.high:
                    return HighAutoPaletteColorCount;
                case AutoPaletteDetail.custom:
                default:
                    return Mathf.Max(1, customColorCount);
            }
        }

        public int GetGeneratedPaletteColorCount()
        {
            return _generatedPalette != null ? Mathf.Max(1, _generatedPalette.width) : 0;
        }

        private void ReplaceGeneratedPalette(Texture2D nextPalette)
        {
            if (_generatedPalette != null && _generatedPalette != nextPalette)
            {
                DestroyImmediate(_generatedPalette);
            }

            _generatedPalette = nextPalette;
        }

#if UNITY_EDITOR
        public void ClearGeneratedPalette()
        {
            if (_generatedPalette == null)
            {
                return;
            }

            ReplaceGeneratedPalette(null);
            EditorUtility.SetDirty(this);
        }

        public static void ClearGeneratedPalettesForProfile(PixelateProfile changedProfile)
        {
            if (changedProfile == null)
            {
                return;
            }

            PixelateCaptureManager[] managers = FindSceneObjectsIncludingInactive<PixelateCaptureManager>();
            for (int i = 0; i < managers.Length; i++)
            {
                PixelateCaptureManager manager = managers[i];
                PixelateProfile activeProfile = manager != null
                    ? (manager.profile != null ? manager.profile : manager.localProfile)
                    : null;
                if (manager == null || activeProfile != changedProfile)
                {
                    continue;
                }

                manager.ClearGeneratedPalette();
            }

            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
#endif

        public IEnumerator GeneratePreview(int requestVersion)
        {
            if (CanGeneratePreview() == false || requestVersion != _previewRequestVersion)
            {
                yield break;
            }

            bool captureDiffuse = _previewNeedsDiffuse;
            bool captureNormal = _previewNeedsNormal && CreateNormalMap;
            Vector2Int cellSize = CellSize;
            var atlasSize = cellSize;
            var atlasPos = new Vector2Int(0, atlasSize.y - cellSize.y);
            Texture2D diffuseMap = captureDiffuse ? CreateAtlasTexture(atlasSize, Color.clear) : null;
            Texture2D normalMap = captureNormal ? CreateAtlasTexture(atlasSize, NormalCaptureBackgroundColor) : null;
            var rtFrame = CreateFrameRenderTexture();
            Shader normalCaptureShader = captureNormal ? GetNormalCaptureShader() : null;

            var cachedCameraColor = CaptureCamera.backgroundColor;
            SourceClipEntry? previewEntry = captureType == CaptureType.animation ? GetPrimarySourceClipEntry() : null;
            bool shouldSampleAnimation = previewEntry.HasValue && previewEntry.Value.clip != null;
            bool continuedEditorAnimationMode = shouldSampleAnimation && _previewOwnsEditorAnimationMode;
            bool startedEditorAnimationMode = shouldSampleAnimation ? BeginEditorAnimationSampling() : false;
            _previewOwnsEditorAnimationMode = continuedEditorAnimationMode || startedEditorAnimationMode;

            try
            {
                CaptureCamera.targetTexture = rtFrame;

                if (shouldSampleAnimation)
                {
                    AnimationClip previewClip = previewEntry.Value.clip;
                    float previewSpeed = SanitizeClipSpeed(previewEntry.Value.speed);
                    int numFrames = GetFrameCount(previewClip, previewSpeed);
                    float currentTime = GetAnimationSampleTime(previewClip, CurrentFrame, numFrames, previewSpeed);
                    SampleAnimationForPreview(currentTime, previewClip, repaintSceneView: false);
                    yield return WaitForAnimationPoseToSettle();
                    EndAnimationPreviewPoseVisibilityHoldForEditor(repaintSceneView: false);
                    RepaintAnimationPoseViews();
                }
                else
                {
                    EndAnimationPreviewPoseVisibilityHoldForEditor(repaintSceneView: false);
                    yield return null;
                }

                if (captureDiffuse)
                {
                    CaptureCamera.backgroundColor = Color.clear;
                    CaptureCamera.Render();
                    Graphics.SetRenderTarget(rtFrame);
                    diffuseMap.ReadPixels(new Rect(0, 0, rtFrame.width, rtFrame.height), atlasPos.x, atlasPos.y);
                    diffuseMap.Apply();

                    ApplyPaletteIfNeeded(ref diffuseMap, allowPaletteGeneration: true);
                    diffuseMap.filterMode = Pixelated ? FilterMode.Point : FilterMode.Bilinear;
                }

                if (captureNormal && normalMap != null && normalCaptureShader != null)
                {
                    NormalCaptureOverrideState normalOverrideState = BeginNormalCaptureOverride();
                    try
                    {
                        RenderNormalFrame(rtFrame, normalMap, atlasPos, normalCaptureShader);
                        normalMap.filterMode = Pixelated ? FilterMode.Point : FilterMode.Bilinear;
                    }
                    finally
                    {
                        EndNormalCaptureOverride(normalOverrideState);
                    }
                }

                if (requestVersion == _previewRequestVersion)
                {
                    ReplacePreviewTexture(ref localSections.preview.previewImage, diffuseMap);
                    ReplacePreviewTexture(ref localSections.preview.previewNormalImage, normalMap);
                    NotifyPreviewUpdated();
                }
                else
                {
                    DestroyImmediate(diffuseMap);
                    if (normalMap != null)
                    {
                        DestroyImmediate(normalMap);
                    }
                }
            }
            finally
            {
                if (shouldSampleAnimation && requestVersion == _previewRequestVersion)
                {
                    _previewOwnsEditorAnimationMode = _previewOwnsEditorAnimationMode || continuedEditorAnimationMode || startedEditorAnimationMode;
                    _previewPoseTarget = TargetObject;
                }
                else
                {
                    EndEditorAnimationSampling(startedEditorAnimationMode);
                    _previewOwnsEditorAnimationMode = false;
                    _previewPoseTarget = null;
                }
                Graphics.SetRenderTarget(null);
                CaptureCamera.targetTexture = null;
                CaptureCamera.backgroundColor = cachedCameraColor;
                DestroyImmediate(rtFrame);
            }
        }

        public IEnumerator CaptureFrame(Action<Texture2D, Texture2D, Texture2D, bool, bool, bool, Vector2Int, Vector2, float, AnimationClip, int, int> onComplete)
        {
            if (HasBlockingIssues(includeExportValidation: false))
            {
                LogBlockingValidationMessages(includeExportValidation: false);
                yield break;
            }

            BeginCaptureSession();
            Vector2Int cellSize = CellSize;
            var atlasSize = cellSize;
            var atlasPos = new Vector2Int(0, atlasSize.y - cellSize.y);
            var diffuseMap = CreateAtlasTexture(atlasSize, Color.clear);
            var normalMap = CreateAtlasTexture(atlasSize, new Color(0.5f, 0.5f, 1.0f, 0.0f));
            var rtFrame = CreateFrameRenderTexture();
            Shader normalCaptureShader = GetNormalCaptureShader();

            var cachedCameraColor = CaptureCamera.backgroundColor;
            RenderPipelineAsset defaultGraphicsPipeline = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset defaultQualityPipeline = QualitySettings.renderPipeline;

            try
            {
                CaptureCamera.targetTexture = rtFrame;

                ApplyRenderPipelineForMainCapture(defaultGraphicsPipeline, defaultQualityPipeline);
                RenderDiffuseFrame(rtFrame, diffuseMap, atlasPos);

                if (CreateNormalMap && normalCaptureShader != null)
                {
                    ApplyRenderPipelineForNormalCapture(defaultGraphicsPipeline, defaultQualityPipeline);
                    NormalCaptureOverrideState normalOverrideState = BeginNormalCaptureOverride();
                    try
                    {
                        RenderNormalFrame(rtFrame, normalMap, atlasPos, normalCaptureShader);
                    }
                    finally
                    {
                        EndNormalCaptureOverride(normalOverrideState);
                    }
                }

                Texture2D palette = ApplyPaletteIfNeeded(ref diffuseMap, allowPaletteGeneration: true);

                yield return null;

                onComplete?.Invoke(diffuseMap, normalMap, palette, CreateNormalMap, false, Pixelated, cellSize, Pivot, PixelsPerUnit, null, -1, 1);
            }
            finally
            {
                EndCaptureSession();
                QualitySettings.renderPipeline = defaultQualityPipeline;
                GraphicsSettings.defaultRenderPipeline = defaultGraphicsPipeline;
                Graphics.SetRenderTarget(null);
                CaptureCamera.targetTexture = null;
                CaptureCamera.backgroundColor = cachedCameraColor;
                DestroyImmediate(rtFrame);
            }
        }

        public IEnumerator CaptureAnimation(Action<Texture2D, Texture2D, Texture2D, bool, bool, bool, Vector2Int, Vector2, float, AnimationClip, int, int> onComplete)
        {
            return CaptureAnimationEntries(GetEffectiveSourceClipEntries(), onComplete);
        }

        private IEnumerator CaptureAnimationEntries(SourceClipEntry[] sourceClipEntries, Action<Texture2D, Texture2D, Texture2D, bool, bool, bool, Vector2Int, Vector2, float, AnimationClip, int, int> onComplete)
        {
            if (HasBlockingIssues(includeExportValidation: false))
            {
                LogBlockingValidationMessages(includeExportValidation: false);
                yield break;
            }

            BeginCaptureSession();
            RenderPipelineAsset defaultGraphicsPipeline = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset defaultQualityPipeline = QualitySettings.renderPipeline;
            bool startedEditorAnimationMode = BeginEditorAnimationSampling();
            Vector2Int cellSize = CellSize;
            Vector2 pivot = Pivot;
            float pixelsPerUnit = PixelsPerUnit;
            bool createNormalMap = CreateNormalMap;
            bool pixelated = Pixelated;

            try
            {
                for (int sourceClipIndex = 0; sourceClipIndex < sourceClipEntries.Length; sourceClipIndex++)
                {
                    SourceClipEntry sourceClipEntry = sourceClipEntries[sourceClipIndex];
                    AnimationClip animClip = sourceClipEntry.clip;
                    if (animClip == null)
                    {
                        continue;
                    }

                    float clipSpeed = SanitizeClipSpeed(sourceClipEntry.speed);
                    int numFrames = GetFrameCount(animClip, clipSpeed);
                    int gridCellCount = SqrtCeil(numFrames);
                    Vector2Int atlasSize = new Vector2Int(cellSize.x * gridCellCount, cellSize.y * gridCellCount);
                    Vector2Int atlasPos = GetAtlasStartPosition(atlasSize);
                    Texture2D diffuseMap = CreateAtlasTexture(atlasSize, Color.clear);
                    Texture2D normalMap = CreateAtlasTexture(atlasSize, new Color(0.5f, 0.5f, 1.0f, 0.0f));
                    RenderTexture rtFrame = CreateFrameRenderTexture();
                    Shader normalCaptureShader = GetNormalCaptureShader();

                    CaptureCamera.targetTexture = rtFrame;
                    Color cachedCameraColor = CaptureCamera.backgroundColor;

                    try
                    {
                        ApplyRenderPipelineForMainCapture(defaultGraphicsPipeline, defaultQualityPipeline);
                        for (int frameIndex = 0; frameIndex < numFrames; frameIndex++)
                        {
                            float currentTime = GetAnimationSampleTime(animClip, frameIndex, numFrames, clipSpeed);
                            SampleAnimationForCapture(currentTime, animClip);
                            yield return WaitForAnimationPoseToSettle();

                            RenderDiffuseFrame(rtFrame, diffuseMap, atlasPos);
                            AdvanceAtlasPosition(ref atlasPos, frameIndex, gridCellCount);
                        }

                        Texture2D palette = ApplyPaletteIfNeeded(ref diffuseMap, allowPaletteGeneration: true);

                        if (createNormalMap && normalCaptureShader != null)
                        {
                            ApplyRenderPipelineForNormalCapture(defaultGraphicsPipeline, defaultQualityPipeline);
                            NormalCaptureOverrideState normalOverrideState = BeginNormalCaptureOverride();
                            atlasPos = GetAtlasStartPosition(atlasSize);
                            try
                            {
                                for (int frameIndex = 0; frameIndex < numFrames; frameIndex++)
                                {
                                    float currentTime = GetAnimationSampleTime(animClip, frameIndex, numFrames, clipSpeed);
                                    SampleAnimationForCapture(currentTime, animClip);
                                    yield return WaitForAnimationPoseToSettle();

                                    RenderNormalFrame(rtFrame, normalMap, atlasPos, normalCaptureShader);
                                    AdvanceAtlasPosition(ref atlasPos, frameIndex, gridCellCount);
                                }
                            }
                            finally
                            {
                                EndNormalCaptureOverride(normalOverrideState);
                            }
                        }

                        yield return null;

                        onComplete?.Invoke(diffuseMap, normalMap, palette, createNormalMap, true, pixelated, cellSize, pivot, pixelsPerUnit, animClip, FramesPerSecond, numFrames);
                    }
                    finally
                    {
                        QualitySettings.renderPipeline = defaultQualityPipeline;
                        GraphicsSettings.defaultRenderPipeline = defaultGraphicsPipeline;
                        Graphics.SetRenderTarget(null);
                        CaptureCamera.targetTexture = null;
                        CaptureCamera.backgroundColor = cachedCameraColor;
                        DestroyImmediate(rtFrame);
                    }
                }
            }
            finally
            {
                EndEditorAnimationSampling(startedEditorAnimationMode);
                EndCaptureSession();
            }

            CurrentFrame = 0;
            var firstClip = GetPrimaryAnimationClip();
            if (firstClip != null)
            {
                SampleAnimation(0f, firstClip);
            }
        }

        private Texture2D ApplyPaletteIfNeeded(ref Texture2D diffuseMap, bool allowPaletteGeneration)
        {
            Texture2D palette = null;

            if (ActivePaletteStyle == PaletteStyle.auto)
            {
                if (allowPaletteGeneration)
                {
                    GenerateNewPalette(diffuseMap);
                }

                if (_generatedPalette != null)
                {
                    PixelateUtilities.LimitPalette(ref diffuseMap, ref _generatedPalette, ref _blitRenderTexture);
                    palette = _generatedPalette;
                }
            }
            else if (ActivePaletteStyle == PaletteStyle.custom)
            {
                Texture2D customPalette = GetActiveValidCustomPalette();
                if (customPalette != null)
                {
                    PixelateUtilities.LimitPalette(ref diffuseMap, ref customPalette, ref _blitRenderTexture);
                }
            }

            diffuseMap.Apply();
            return palette;
        }

        private Texture2D GetActiveValidCustomPalette()
        {
            if (ActivePaletteStyle != PaletteStyle.custom)
            {
                return null;
            }

            Texture2D customPalette = ActiveCustomPalette;
            return PixelateUtilities.IsPaletteValid(customPalette) ? customPalette : null;
        }

        private AnimationClip GetPrimaryAnimationClip()
        {
            SourceClipEntry? entry = GetPrimarySourceClipEntry();
            return entry.HasValue ? entry.Value.clip : null;
        }

        private Vector2Int GetCurrentAtlasSize()
        {
            Vector2Int cellSize = CellSize;
            if (cellSize.x <= 0 || cellSize.y <= 0)
            {
                return Vector2Int.zero;
            }

            if (captureType == CaptureType.animation)
            {
                SourceClipEntry? entry = GetPrimarySourceClipEntry();
                if (entry.HasValue == false || entry.Value.clip == null)
                {
                    return cellSize;
                }

                int numFrames = Mathf.Max(1, GetFrameCount(entry.Value.clip, entry.Value.speed));
                int gridCellCount = Mathf.Max(1, SqrtCeil(numFrames));
                return new Vector2Int(cellSize.x * gridCellCount, cellSize.y * gridCellCount);
            }

            return cellSize;
        }

        private Texture2D CreateAtlasTexture(Vector2Int atlasSize, Color clearColor)
        {
            var texture = new Texture2D(atlasSize.x, atlasSize.y, TextureFormat.ARGB32, false)
            {
                filterMode = Pixelated ? FilterMode.Point : FilterMode.Bilinear
            };
            ClearAtlas(texture, clearColor);
            return texture;
        }

        private RenderTexture CreateFrameRenderTexture()
        {
            Vector2Int cellSize = CellSize;
            return new RenderTexture(cellSize.x, cellSize.y, 24, RenderTextureFormat.ARGB32)
            {
                filterMode = Pixelated ? FilterMode.Point : FilterMode.Bilinear,
                antiAliasing = 1,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private int GetFrameCount(float duration)
        {
            return Mathf.Max(1, Mathf.CeilToInt(duration * FramesPerSecond));
        }

        private SourceClipEntry[] GetEffectiveSourceClipEntries()
        {
            MigrateLegacySourceClipsIfNeeded();

            if (SourceClipEntries != null && SourceClipEntries.Length > 0)
            {
                return SourceClipEntries;
            }

            if (_sourceClips == null || _sourceClips.Length == 0)
            {
                return Array.Empty<SourceClipEntry>();
            }

            var fallbackEntries = new SourceClipEntry[_sourceClips.Length];
            for (int i = 0; i < _sourceClips.Length; i++)
            {
                fallbackEntries[i] = new SourceClipEntry
                {
                    clip = _sourceClips[i],
                    speed = 1f,
                };
            }

            return fallbackEntries;
        }

        private SourceClipEntry? GetPrimarySourceClipEntry()
        {
            SourceClipEntry[] entries = GetEffectiveSourceClipEntries();
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].clip != null)
                {
                    return entries[i];
                }
            }

            return null;
        }

        private float GetEffectiveAnimationDuration(float clipLength, float speed)
        {
            return clipLength / SanitizeClipSpeed(speed);
        }

        private float GetAnimationSampleTime(AnimationClip clip, int frameIndex, int numFrames, float speed)
        {
            if (clip == null)
            {
                return 0f;
            }

            float sanitizedSpeed = SanitizeClipSpeed(speed);
            float effectiveDuration = GetEffectiveAnimationDuration(clip.length, sanitizedSpeed);
            float playbackTime = (Mathf.Clamp(frameIndex, 0, Mathf.Max(0, numFrames - 1)) / (float)Mathf.Max(1, numFrames)) * effectiveDuration;
            return Mathf.Clamp(playbackTime * sanitizedSpeed, 0f, clip.length);
        }

        private float SanitizeClipSpeed(float speed)
        {
            return Mathf.Max(0.01f, speed);
        }

        private int SqrtCeil(int input)
        {
            return Mathf.CeilToInt(Mathf.Sqrt(input));
        }

        private void ClearAtlas(Texture2D texture, Color color)
        {
            var pixels = new Color[texture.width * texture.height];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply();
        }

        private void LogBlockingValidationMessages(bool includeExportValidation)
        {
            List<ValidationMessage> messages = GetValidationMessages(includeExportValidation);
            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i].IsBlocking)
                {
                    Debug.LogError(messages[i].Message, this);
                }
            }
        }

        private bool ShouldUseFallbackAutoPalette(Color32[] palette)
        {
            if (palette == null || palette.Length == 0)
            {
                return true;
            }

            if (palette.Length == 1)
            {
                return false;
            }

            bool allColorsMatch = true;
            Color32 first = palette[0];
            for (int i = 1; i < palette.Length; i++)
            {
                if (PixelateUtilities.ColorDiff(first, palette[i]) > 0.5f)
                {
                    allColorsMatch = false;
                    break;
                }
            }

            return allColorsMatch;
        }

        private void StartPreviewCoroutine(int requestVersion)
        {
            if (_isCapturing)
            {
                return;
            }

            if (_previewCoroutine != null)
            {
                StopCoroutine(_previewCoroutine);
                _previewCoroutine = null;
            }

            _previewCoroutine = StartCoroutine(GeneratePreview(requestVersion));
        }

        public IEnumerator GenerateAnimationPreviewAtlas(bool includeDiffuse, bool includeNormal, Action<Texture2D, Texture2D, int, Vector2Int> onComplete)
        {
            if (HasBlockingIssues(includeExportValidation: false))
            {
                LogBlockingValidationMessages(includeExportValidation: false);
                yield break;
            }

            SourceClipEntry? previewEntry = GetPrimarySourceClipEntry();
            if (previewEntry.HasValue == false || previewEntry.Value.clip == null)
            {
                yield break;
            }

            AnimationClip animClip = previewEntry.Value.clip;
            float previewSpeed = SanitizeClipSpeed(previewEntry.Value.speed);

            _cancelAnimationPreviewBuild = false;
            BeginCaptureSession();
            RenderPipelineAsset defaultGraphicsPipeline = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset defaultQualityPipeline = QualitySettings.renderPipeline;
            bool startedEditorAnimationMode = BeginEditorAnimationSampling();

            int numFrames = GetFrameCount(animClip, previewSpeed);
            int gridCellCount = SqrtCeil(numFrames);
            Vector2Int cellSize = CellSize;
            Vector2Int atlasSize = new Vector2Int(cellSize.x * gridCellCount, cellSize.y * gridCellCount);
            Vector2Int atlasPos = GetAtlasStartPosition(atlasSize);
            Texture2D diffuseMap = includeDiffuse ? CreateAtlasTexture(atlasSize, Color.clear) : null;
            Texture2D normalMap = includeNormal && CreateNormalMap ? CreateAtlasTexture(atlasSize, NormalCaptureBackgroundColor) : null;
            RenderTexture rtFrame = CreateFrameRenderTexture();
            Shader normalCaptureShader = normalMap != null ? GetNormalCaptureShader() : null;

            Color cachedCameraColor = CaptureCamera.backgroundColor;

            try
            {
                CaptureCamera.targetTexture = rtFrame;

                if (diffuseMap != null)
                {
                    ApplyRenderPipelineForMainCapture(defaultGraphicsPipeline, defaultQualityPipeline);
                    for (int frameIndex = 0; frameIndex < numFrames; frameIndex++)
                    {
                        if (_cancelAnimationPreviewBuild)
                        {
                            yield break;
                        }

                        float currentTime = GetAnimationSampleTime(animClip, frameIndex, numFrames, previewSpeed);
                        SampleAnimationForCapture(currentTime, animClip);
                        yield return WaitForAnimationPoseToSettle();

                        RenderDiffuseFrame(rtFrame, diffuseMap, atlasPos);
                        AdvanceAtlasPosition(ref atlasPos, frameIndex, gridCellCount);
                    }

                    ApplyPaletteIfNeeded(ref diffuseMap, allowPaletteGeneration: true);
                }

                if (normalMap != null && normalCaptureShader != null)
                {
                    ApplyRenderPipelineForNormalCapture(defaultGraphicsPipeline, defaultQualityPipeline);
                    NormalCaptureOverrideState normalOverrideState = BeginNormalCaptureOverride();
                    atlasPos = GetAtlasStartPosition(atlasSize);
                    try
                    {
                        for (int frameIndex = 0; frameIndex < numFrames; frameIndex++)
                        {
                            if (_cancelAnimationPreviewBuild)
                            {
                                yield break;
                            }

                            float currentTime = GetAnimationSampleTime(animClip, frameIndex, numFrames, previewSpeed);
                            SampleAnimationForCapture(currentTime, animClip);
                            yield return WaitForAnimationPoseToSettle();

                            RenderNormalFrame(rtFrame, normalMap, atlasPos, normalCaptureShader);
                            AdvanceAtlasPosition(ref atlasPos, frameIndex, gridCellCount);
                        }
                    }
                    finally
                    {
                        EndNormalCaptureOverride(normalOverrideState);
                    }
                }

                if (_cancelAnimationPreviewBuild == false)
                {
                    onComplete?.Invoke(diffuseMap, normalMap, numFrames, cellSize);
                    diffuseMap = null;
                    normalMap = null;
                }
            }
            finally
            {
                int previewFrameCount = Mathf.Max(1, GetFrameCount(animClip, previewSpeed));
                float previewTime = GetAnimationSampleTime(animClip, CurrentFrame, previewFrameCount, previewSpeed);
                SampleAnimationForCapture(previewTime, animClip);

                EndEditorAnimationSampling(startedEditorAnimationMode);
                EndCaptureSession();
                QualitySettings.renderPipeline = defaultQualityPipeline;
                GraphicsSettings.defaultRenderPipeline = defaultGraphicsPipeline;
                Graphics.SetRenderTarget(null);
                CaptureCamera.targetTexture = null;
                CaptureCamera.backgroundColor = cachedCameraColor;
                DestroyImmediate(rtFrame);

                if (diffuseMap != null)
                {
                    DestroyImmediate(diffuseMap);
                }

                if (normalMap != null)
                {
                    DestroyImmediate(normalMap);
                }

                _cancelAnimationPreviewBuild = false;
            }
        }

#if UNITY_EDITOR
        private void StartEditorPreviewRoutine(int requestVersion)
        {
            if (_isCapturing)
            {
                return;
            }

            if (_editorPreviewRoutine != null)
            {
                EditorApplication.update -= UpdateEditorPreviewRoutine;
                _editorPreviewRoutine = null;
            }

            _editorPreviewRoutine = GeneratePreview(requestVersion);
            EditorApplication.update -= UpdateEditorPreviewRoutine;
            EditorApplication.update += UpdateEditorPreviewRoutine;
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private void UpdateEditorPreviewRoutine()
        {
            if (_editorPreviewRoutine == null || this == null)
            {
                EditorApplication.update -= UpdateEditorPreviewRoutine;
                _editorPreviewRoutine = null;
                return;
            }

            if (_editorPreviewRoutine.MoveNext())
            {
                EditorApplication.QueuePlayerLoopUpdate();
                return;
            }

            EditorApplication.update -= UpdateEditorPreviewRoutine;
            _editorPreviewRoutine = null;
        }
#endif

        private void NotifyPreviewUpdated()
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
#endif
            PreviewUpdated?.Invoke();
        }

        private void ReplacePreviewTexture(ref Texture2D currentTexture, Texture2D nextTexture)
        {
            if (currentTexture != null && currentTexture != nextTexture)
            {
                DestroyImmediate(currentTexture);
            }

            currentTexture = nextTexture;
        }

        private bool TargetHasAnimator()
        {
            return TargetObject != null && TargetObject.GetComponent<Animator>() != null;
        }

        private Vector2Int GetAtlasStartPosition(Vector2Int atlasSize)
        {
            return new Vector2Int(0, atlasSize.y - CellSize.y);
        }

        private void AdvanceAtlasPosition(ref Vector2Int atlasPos, int frameIndex, int gridCellCount)
        {
            atlasPos.x += CellSize.x;
            if ((frameIndex + 1) % gridCellCount == 0)
            {
                atlasPos.x = 0;
                atlasPos.y -= CellSize.y;
            }
        }

        private Shader GetNormalCaptureShader()
        {
            return Shader.Find(BuiltInNormalCaptureShaderName);
        }

        private void RenderDiffuseFrame(RenderTexture rtFrame, Texture2D diffuseMap, Vector2Int atlasPos)
        {
            CaptureCamera.backgroundColor = Color.clear;
            CaptureCamera.Render();
            Graphics.SetRenderTarget(rtFrame);
            diffuseMap.ReadPixels(new Rect(0, 0, rtFrame.width, rtFrame.height), atlasPos.x, atlasPos.y);
            diffuseMap.Apply();
        }

        private void RenderNormalFrame(RenderTexture rtFrame, Texture2D normalMap, Vector2Int atlasPos, Shader normalCaptureShader)
        {
            if (rtFrame == null || normalMap == null || normalCaptureShader == null)
            {
                return;
            }

            RenderTexture previousTargetTexture = CaptureCamera.targetTexture;
            RenderTexture previousActiveTexture = RenderTexture.active;
            RenderTexture normalFrame = RenderTexture.GetTemporary(
                rtFrame.width,
                rtFrame.height,
                rtFrame.depth,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            normalFrame.filterMode = rtFrame.filterMode;
            normalFrame.hideFlags = HideFlags.HideAndDontSave;

            Texture2D normalFrameTexture = null;
            CaptureCamera.backgroundColor = NormalCaptureBackgroundColor;
            Shader.SetGlobalFloat(NormalLightingStepsPropertyName, Mathf.Max(1, NormalLightingSteps));
            CaptureCamera.targetTexture = normalFrame;

            try
            {
                if (ActiveMaterialPipeline == MaterialPipeline.urp)
                {
                    CaptureCamera.Render();
                }
                else
                {
                    CaptureCamera.RenderWithShader(normalCaptureShader, "");
                }

                Graphics.SetRenderTarget(normalFrame);
                normalFrameTexture = new Texture2D(rtFrame.width, rtFrame.height, TextureFormat.ARGB32, false, true)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                normalFrameTexture.ReadPixels(new Rect(0, 0, rtFrame.width, rtFrame.height), 0, 0);
                normalFrameTexture.Apply(false, false);

                Color32[] normalPixels = normalFrameTexture.GetPixels32();
                normalMap.SetPixels32(atlasPos.x, atlasPos.y, rtFrame.width, rtFrame.height, normalPixels);
                normalMap.Apply(false, false);
            }
            finally
            {
                if (normalFrameTexture != null)
                {
                    DestroyImmediate(normalFrameTexture);
                }

                Graphics.SetRenderTarget(previousActiveTexture);
                CaptureCamera.targetTexture = previousTargetTexture;
                RenderTexture.ReleaseTemporary(normalFrame);
            }
        }

        private NormalCaptureOverrideState BeginNormalCaptureOverride()
        {
            if (ActiveMaterialPipeline != MaterialPipeline.urp || TargetObject == null)
            {
                return null;
            }

            Shader normalCaptureShader = Shader.Find(BuiltInNormalCaptureShaderName);
            if (normalCaptureShader == null)
            {
                return null;
            }

            Renderer[] renderers = TargetObject.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return null;
            }

            var overrideState = new NormalCaptureOverrideState
            {
                Renderers = renderers,
                OriginalMaterials = new Material[renderers.Length][],
                TemporaryMaterials = new List<Material>()
            };

            var materialCache = new Dictionary<Material, Material>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] originalMaterials = renderers[i].sharedMaterials;
                overrideState.OriginalMaterials[i] = originalMaterials;

                if (originalMaterials == null || originalMaterials.Length == 0)
                {
                    continue;
                }

                Material[] replacementMaterials = new Material[originalMaterials.Length];
                for (int j = 0; j < originalMaterials.Length; j++)
                {
                    replacementMaterials[j] = GetOrCreateUrpNormalOverrideMaterial(originalMaterials[j], normalCaptureShader, materialCache, overrideState.TemporaryMaterials);
                }

                renderers[i].sharedMaterials = replacementMaterials;
            }

            return overrideState;
        }

        private void EndNormalCaptureOverride(NormalCaptureOverrideState overrideState)
        {
            if (overrideState == null)
            {
                return;
            }

            for (int i = 0; i < overrideState.Renderers.Length; i++)
            {
                Renderer renderer = overrideState.Renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.sharedMaterials = overrideState.OriginalMaterials[i];
            }

            for (int i = 0; i < overrideState.TemporaryMaterials.Count; i++)
            {
                if (overrideState.TemporaryMaterials[i] != null)
                {
                    DestroyImmediate(overrideState.TemporaryMaterials[i]);
                }
            }
        }

        private Material GetOrCreateUrpNormalOverrideMaterial(Material sourceMaterial, Shader normalCaptureShader, Dictionary<Material, Material> materialCache, List<Material> temporaryMaterials)
        {
            if (sourceMaterial == null)
            {
                return null;
            }

            if (materialCache.TryGetValue(sourceMaterial, out Material cachedMaterial))
            {
                return cachedMaterial;
            }

            var overrideMaterial = new Material(normalCaptureShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            overrideMaterial.SetFloat(NormalLightingStepsPropertyName, Mathf.Max(1, NormalLightingSteps));

            materialCache[sourceMaterial] = overrideMaterial;
            temporaryMaterials.Add(overrideMaterial);
            return overrideMaterial;
        }

        private void ApplyRenderPipelineForNormalCapture(RenderPipelineAsset defaultGraphicsPipeline, RenderPipelineAsset defaultQualityPipeline)
        {
            if (ActiveMaterialPipeline == MaterialPipeline.urp)
            {
                QualitySettings.renderPipeline = defaultQualityPipeline;
                GraphicsSettings.defaultRenderPipeline = defaultGraphicsPipeline;
                return;
            }

            QualitySettings.renderPipeline = null;
            GraphicsSettings.defaultRenderPipeline = null;
        }

        private void ApplyRenderPipelineForMainCapture(RenderPipelineAsset defaultGraphicsPipeline, RenderPipelineAsset defaultQualityPipeline)
        {
            if (ActiveMaterialPipeline == MaterialPipeline.builtIn)
            {
                QualitySettings.renderPipeline = null;
                GraphicsSettings.defaultRenderPipeline = null;
                return;
            }

            QualitySettings.renderPipeline = defaultQualityPipeline;
            GraphicsSettings.defaultRenderPipeline = defaultGraphicsPipeline;
        }

        private void BeginCaptureSession()
        {
#if UNITY_EDITOR
            EndAnimationPreviewPoseVisibilityHoldForEditor(repaintSceneView: false);
#endif
            _isCapturing = true;
            CancelPendingPreviewRefresh();
        }

        private void EndCaptureSession()
        {
            _isCapturing = false;
        }

        private void CancelPendingPreviewRefresh(bool preserveAnimationPreviewPose = false)
        {
#if UNITY_EDITOR
            if (_editorPreviewRoutine != null)
            {
                EditorApplication.update -= UpdateEditorPreviewRoutine;
                _editorPreviewRoutine = null;
            }
#endif

            if (_previewCoroutine != null)
            {
                StopCoroutine(_previewCoroutine);
                _previewCoroutine = null;
            }

            if (preserveAnimationPreviewPose)
            {
                ReleasePreviewRenderTargets();
                return;
            }

            CleanupCancelledPreviewState();
        }

        private void CleanupCancelledPreviewState()
        {
#if UNITY_EDITOR
            EndAnimationPreviewPoseVisibilityHoldForEditor(repaintSceneView: false);
            if (Application.isPlaying == false && _previewOwnsEditorAnimationMode && AnimationMode.InAnimationMode())
            {
                AnimationMode.StopAnimationMode();
            }
#endif

            _previewOwnsEditorAnimationMode = false;
            _previewPoseTarget = null;
            ReleasePreviewRenderTargets();
        }

        private void ReleasePreviewRenderTargets()
        {
            Graphics.SetRenderTarget(null);
            if (CaptureCamera != null)
            {
                CaptureCamera.targetTexture = null;
            }
        }

#if UNITY_EDITOR
        public void RefreshInspectorTargetIsolation(bool forceRebuild = false)
        {
            if (TargetObject == null)
            {
                ClearInspectorTargetIsolation();
                return;
            }

            if (forceRebuild == false && _inspectorTargetIsolationState != null && _inspectorTargetIsolationTarget == TargetObject)
            {
                return;
            }

            ClearInspectorTargetIsolation();
            _inspectorTargetIsolationTarget = TargetObject;
            _inspectorTargetIsolationState = CreateInspectorTargetIsolationState(TargetObject);
        }

        public void ClearInspectorTargetIsolation()
        {
            if (_inspectorTargetIsolationState == null)
            {
                _inspectorTargetIsolationTarget = null;
                return;
            }

            RestoreInspectorTargetIsolation(_inspectorTargetIsolationState);
            _inspectorTargetIsolationState = null;
            _inspectorTargetIsolationTarget = null;
        }

        private InspectorTargetIsolationState CreateInspectorTargetIsolationState(GameObject targetObject)
        {
            if (targetObject == null)
            {
                return null;
            }

            Transform targetRoot = targetObject.transform;
            var hiddenRenderers = new List<Renderer>();
            Renderer[] renderers = FindSceneObjectsIncludingInactive<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || IsInTargetHierarchy(renderer, targetRoot))
                {
                    continue;
                }

                hiddenRenderers.Add(renderer);
            }

            var hiddenTerrains = new List<Terrain>();
            Terrain[] terrains = FindSceneObjectsIncludingInactive<Terrain>();
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null || IsInTargetHierarchy(terrain, targetRoot))
                {
                    continue;
                }

                hiddenTerrains.Add(terrain);
            }

            var renderState = new InspectorTargetIsolationState
            {
                Renderers = hiddenRenderers.ToArray(),
                OriginalForceRenderingOff = new bool[hiddenRenderers.Count],
                Terrains = hiddenTerrains.ToArray(),
                OriginalTerrainEnabled = new bool[hiddenTerrains.Count]
            };

            for (int i = 0; i < renderState.Renderers.Length; i++)
            {
                Renderer renderer = renderState.Renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderState.OriginalForceRenderingOff[i] = renderer.forceRenderingOff;
                renderer.forceRenderingOff = true;
            }

            for (int i = 0; i < renderState.Terrains.Length; i++)
            {
                Terrain terrain = renderState.Terrains[i];
                if (terrain == null)
                {
                    continue;
                }

                renderState.OriginalTerrainEnabled[i] = terrain.enabled;
                terrain.enabled = false;
            }

            return renderState;
        }

        private static bool IsInTargetHierarchy(Component component, Transform targetRoot)
        {
            if (component == null || targetRoot == null)
            {
                return false;
            }

            Transform componentTransform = component.transform;
            return componentTransform == targetRoot || componentTransform.IsChildOf(targetRoot);
        }

        private static T[] FindSceneObjectsIncludingInactive<T>() where T : Component
        {
            T[] allObjects = Resources.FindObjectsOfTypeAll<T>();
            var sceneObjects = new List<T>(allObjects.Length);
            for (int i = 0; i < allObjects.Length; i++)
            {
                T sceneObject = allObjects[i];
                if (sceneObject == null
                    || EditorUtility.IsPersistent(sceneObject)
                    || sceneObject.gameObject.scene.IsValid() == false)
                {
                    continue;
                }

                sceneObjects.Add(sceneObject);
            }

            return sceneObjects.ToArray();
        }

        private static void RestoreInspectorTargetIsolation(InspectorTargetIsolationState renderState)
        {
            if (renderState == null)
            {
                return;
            }

            for (int i = 0; i < renderState.Renderers.Length; i++)
            {
                Renderer renderer = renderState.Renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.forceRenderingOff = renderState.OriginalForceRenderingOff[i];
            }

            for (int i = 0; i < renderState.Terrains.Length; i++)
            {
                Terrain terrain = renderState.Terrains[i];
                if (terrain == null)
                {
                    continue;
                }

                terrain.enabled = renderState.OriginalTerrainEnabled[i];
            }
        }
#endif

        private bool ShouldPreserveAnimationPreviewPoseForRefresh()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                return false;
            }

            if (_previewOwnsEditorAnimationMode == false)
            {
                return false;
            }

            if (AnimationMode.InAnimationMode() == false)
            {
                return false;
            }

            if (captureType != CaptureType.animation)
            {
                return false;
            }

            return TargetObject != null
                && _previewPoseTarget == TargetObject
                && GetPrimaryAnimationClip() != null;
#else
            return false;
#endif
        }

        private void NormalizePreviewStateForCurrentMode()
        {
            if (captureType == CaptureType.animation)
            {
                AnimationClip previewClip = GetPrimaryAnimationClip();
                int maxFrame = previewClip != null ? Mathf.Max(0, GetFrameCount(previewClip) - 1) : 0;
                CurrentFrame = Mathf.Clamp(CurrentFrame, 0, maxFrame);
            }
        }

        private bool BeginEditorAnimationSampling()
        {
#if UNITY_EDITOR
            if (Application.isPlaying == false && AnimationMode.InAnimationMode() == false)
            {
                AnimationMode.StartAnimationMode();
                return true;
            }
#endif
            return false;
        }

        private void EndEditorAnimationSampling(bool startedByPixelate)
        {
#if UNITY_EDITOR
            if (startedByPixelate && AnimationMode.InAnimationMode())
            {
                AnimationMode.StopAnimationMode();
            }
#endif
        }

        private IEnumerator WaitForAnimationPoseToSettle()
        {
#if UNITY_EDITOR
            if (Application.isPlaying == false)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
                yield break;
            }
#endif

            yield return null;
        }

    }
}
