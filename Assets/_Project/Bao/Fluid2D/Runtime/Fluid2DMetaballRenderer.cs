using UnityEngine;
using UnityEngine.UI;

namespace Bao.Fluid2D
{
    /// <summary>
    /// Screen-space metaball compositing for Fluid2D particles.
    /// Uses a private runtime material instance and only reapplies properties when settings change.
    /// Recreates RenderTextures only when camera size / quality resolution actually changes.
    /// Maintains a fullscreen RawImage overlay so water stays visible even if auto objects are stripped.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class Fluid2DMetaballRenderer : MonoBehaviour
    {
        private const float RtSizeDebounceSeconds = 0.12f;
        private const int MinRtDimension = 64;
        private const string FluidCameraName = "Fluid Camera (Auto)";
        private const string CanvasOverlayName = "Fluid Canvas Overlay (Auto)";
        private const string MetaballScreenName = "Metaball Screen";
        /// <summary>Keep under typical gameplay UI (sortingOrder 0+).</summary>
        private const int OverlaySortingOrder = -50;
        private const int SceneWaterLayer = 4;

        public Camera mainCamera;

        [Header("=== Layer Settings ===")]
        [Tooltip("Layer dành riêng để vẽ hạt nước. Camera chính phải BỎ chọn layer này, còn Camera phụ chỉ CHỌN duy nhất layer này.")]
        public int waterLayer = 16;

        [Header("=== Rendering Settings ===")]
        [Tooltip("Shared project material. A private runtime instance is used at play/edit time so shared assets are never mutated.")]
        public Material metaballMaterial;

        [Range(0.5f, 2f)]
        [Tooltip("Hệ số độ phân giải của nước so với màn hình (Giảm nhẹ giúp nước mịn và mượt hơn).")]
        public float resolutionScale = 1.0f;

        [Header("=== Dynamic Elements (Auto-Generated) ===")]
        [SerializeField] private Camera fluidCamera;
        [SerializeField] private RenderTexture fluidRT;
        [SerializeField] private Canvas canvasOverlay;
        [SerializeField] private RawImage rawImageBlit;
        [SerializeField] private Fluid2DSystem fluidSystem;

        // Runtime material instance — never write properties to the shared project material.
        private Material runtimeMetaballMaterial;
        private Material runtimeMaterialSource;
        private int lastAppliedSettingsHash;
        private bool materialPropsDirty = true;

        private int lastRtWidth;
        private int lastRtHeight;
        private float lastResolutionScale = -1f;
        private float pendingRtChangeRealtime = -1f;
        private int pendingRtWidth;
        private int pendingRtHeight;

        // Diagnostics (read-only for validation tooling).
        [SerializeField] private int materialPropertyApplyCount;
        [SerializeField] private int renderTextureRecreationCount;
        [SerializeField] private int lastStableFrameCount;
        [SerializeField] private int compositingRepairCount;
        [SerializeField] private string lastCompositingStatus = "Not checked";
        private int consecutiveUnchangedMaterialFrames;
        private int consecutiveUnchangedRtFrames;

        public int MaterialPropertyApplyCount => materialPropertyApplyCount;
        public int RenderTextureRecreationCount => renderTextureRecreationCount;
        public int ConsecutiveUnchangedMaterialFrames => consecutiveUnchangedMaterialFrames;
        public int ConsecutiveUnchangedRtFrames => consecutiveUnchangedRtFrames;
        public int CompositingRepairCount => compositingRepairCount;
        public string LastCompositingStatus => lastCompositingStatus;
        public RenderTexture FluidRenderTexture => fluidRT;
        public Camera FluidCamera => fluidCamera;
        public Canvas CanvasOverlay => canvasOverlay;
        public RawImage RawImageBlit => rawImageBlit;
        public Material RuntimeMetaballMaterial => runtimeMetaballMaterial;
        public int CurrentRtWidth => fluidRT != null ? fluidRT.width : 0;
        public int CurrentRtHeight => fluidRT != null ? fluidRT.height : 0;

        /// <summary>
        /// True when fluid camera, RT, overlay canvas, and RawImage are present and wired for Game View.
        /// </summary>
        public bool IsCompositingReady
        {
            get
            {
                if (mainCamera == null || fluidCamera == null || fluidRT == null || !fluidRT.IsCreated())
                    return false;
                if (canvasOverlay == null || rawImageBlit == null)
                    return false;
                if (rawImageBlit.texture != fluidRT)
                    return false;
                if (!canvasOverlay.gameObject.activeInHierarchy || !rawImageBlit.enabled)
                    return false;
                return true;
            }
        }

        private void OnEnable()
        {
            materialPropsDirty = true;
            EnsureCompositing(forceRebuild: true);
        }

        private void OnDisable()
        {
            CleanupRendering();
        }

        private void OnDestroy()
        {
            CleanupRendering();
            ReleaseRuntimeMaterial();
        }

        /// <summary>Force material property re-upload on next Update (e.g. after quality or look change).</summary>
        public void MarkMaterialDirty()
        {
            materialPropsDirty = true;
            lastAppliedSettingsHash = 0;
        }

        /// <summary>Force RT recreate check (e.g. after quality resolution scale change).</summary>
        public void MarkResolutionDirty()
        {
            lastResolutionScale = -1f;
            lastRtWidth = 0;
            lastRtHeight = 0;
        }

        public void ResetDiagnosticsCounters()
        {
            materialPropertyApplyCount = 0;
            renderTextureRecreationCount = 0;
            consecutiveUnchangedMaterialFrames = 0;
            consecutiveUnchangedRtFrames = 0;
            lastStableFrameCount = 0;
            compositingRepairCount = 0;
            lastCompositingStatus = "Counters reset";
        }

        /// <summary>
        /// Public entry used by setup/repair/phase transitions: ensure overlay path exists and masks are correct.
        /// </summary>
        public void EnsureCompositing(bool forceRebuild = false)
        {
            if (!isActiveAndEnabled && !forceRebuild)
                return;

            ResolveMainCamera();
            if (mainCamera == null)
            {
                lastCompositingStatus = "Missing main camera";
                return;
            }

            SyncParticleHostLayer();

            bool needsRebuild = forceRebuild
                                || fluidCamera == null
                                || fluidRT == null
                                || !fluidRT.IsCreated()
                                || canvasOverlay == null
                                || rawImageBlit == null
                                || rawImageBlit.texture != fluidRT
                                || fluidCamera.targetTexture != fluidRT;

            // Reclaim orphan auto children after domain reload / partial destroy (serialized refs null).
            if (!needsRebuild)
            {
                needsRebuild = !IsCompositingReady;
            }
            else if (fluidCamera == null || canvasOverlay == null || rawImageBlit == null)
            {
                TryRecoverExistingAutoObjects();
                needsRebuild = forceRebuild
                               || fluidCamera == null
                               || fluidRT == null
                               || !fluidRT.IsCreated()
                               || canvasOverlay == null
                               || rawImageBlit == null
                               || rawImageBlit.texture != fluidRT
                               || fluidCamera.targetTexture != fluidRT
                               || !IsCompositingReady;
            }

            if (needsRebuild)
            {
                if (!forceRebuild)
                    compositingRepairCount++;
                SetupRendering();
            }
            else
            {
                ApplyCameraAndLayerMasks();
                BindOverlayToRenderTexture();
                lastCompositingStatus = "OK";
            }
        }

        [ContextMenu("Setup Rendering")]
        public void SetupRendering()
        {
            CleanupRendering(releaseRuntimeMaterial: false);

            ResolveMainCamera();
            if (mainCamera == null)
            {
                lastCompositingStatus = "Missing main camera";
                Debug.LogWarning("Fluid2DMetaballRenderer: Thiếu Camera chính để đồng bộ!", this);
                return;
            }

            fluidSystem = GetComponent<Fluid2DSystem>();
            SyncParticleHostLayer();
            EnsureRuntimeMaterial();

            int w, h;
            ComputeDesiredRtSize(out w, out h);

            fluidRT = CreateFluidRenderTexture(w, h);
            lastRtWidth = w;
            lastRtHeight = h;
            lastResolutionScale = resolutionScale;
            pendingRtChangeRealtime = -1f;
            renderTextureRecreationCount++;

            GameObject camObj = new GameObject(FluidCameraName);
            camObj.transform.SetParent(transform, false);
            camObj.transform.localPosition = Vector3.zero;
            camObj.transform.localRotation = Quaternion.identity;
            camObj.hideFlags = HideFlags.DontSave;
            // Fluid camera object itself must not be on water layer (avoids recursive layer issues).
            camObj.layer = 0;

            fluidCamera = camObj.AddComponent<Camera>();
            fluidCamera.CopyFrom(mainCamera);
            fluidCamera.clearFlags = CameraClearFlags.Color;
            fluidCamera.backgroundColor = new Color(0, 0, 0, 0);
            fluidCamera.cullingMask = 1 << Mathf.Clamp(waterLayer, 0, 31);
            fluidCamera.targetTexture = fluidRT;
            fluidCamera.depth = mainCamera.depth - 1;
            fluidCamera.enabled = true;

            CreateOrRepairOverlay();
            ApplyCameraAndLayerMasks();
            BindOverlayToRenderTexture();

            materialPropsDirty = true;
            ApplyMaterialSettingsIfNeeded(force: true);

            lastCompositingStatus = IsCompositingReady
                ? "OK (setup)"
                : "Setup finished but compositing still incomplete";

            if (Application.isPlaying)
                Debug.Log("Fluid2DMetaballRenderer: Đã cài đặt thành công hệ thống camera phụ và canvas metaball.", this);
        }

        public void CleanupRendering()
        {
            CleanupRendering(releaseRuntimeMaterial: true);
        }

        private void CleanupRendering(bool releaseRuntimeMaterial)
        {
            if (mainCamera != null)
            {
                // Restore water layer visibility on main camera when tearing down.
                mainCamera.cullingMask |= (1 << Mathf.Clamp(waterLayer, 0, 31));
            }

            if (fluidCamera != null)
            {
                if (Application.isPlaying) Destroy(fluidCamera.gameObject);
                else DestroyImmediate(fluidCamera.gameObject);
                fluidCamera = null;
            }

            if (canvasOverlay != null)
            {
                if (Application.isPlaying) Destroy(canvasOverlay.gameObject);
                else DestroyImmediate(canvasOverlay.gameObject);
                canvasOverlay = null;
            }

            rawImageBlit = null;

            // Also destroy any orphan auto children left with broken serialized refs.
            DestroyNamedAutoChildren();

            if (fluidRT != null)
            {
                fluidRT.Release();
                if (Application.isPlaying) Destroy(fluidRT);
                else DestroyImmediate(fluidRT);
                fluidRT = null;
            }

            lastRtWidth = 0;
            lastRtHeight = 0;
            lastCompositingStatus = "Cleaned up";

            if (releaseRuntimeMaterial)
                ReleaseRuntimeMaterial();
        }

        private void Update()
        {
            // Always try to keep compositing alive (missing overlay was the live invisibility root cause).
            EnsureCompositing(forceRebuild: false);

            if (fluidCamera == null || mainCamera == null)
                return;

            var system = fluidSystem != null ? fluidSystem : GetComponent<Fluid2DSystem>();
            if (system != null && system.debugMode)
            {
                if (canvasOverlay != null && canvasOverlay.enabled)
                {
                    canvasOverlay.enabled = false;
                    mainCamera.cullingMask |= (1 << Mathf.Clamp(waterLayer, 0, 31));
                }
            }
            else
            {
                if (canvasOverlay != null && !canvasOverlay.enabled)
                {
                    canvasOverlay.enabled = true;
                    ApplyCameraAndLayerMasks();
                }

                if (rawImageBlit != null && !rawImageBlit.enabled)
                    rawImageBlit.enabled = true;
            }

            // Sync fluid camera pose/size every frame (cheap; no allocations).
            fluidCamera.orthographic = mainCamera.orthographic;
            fluidCamera.orthographicSize = mainCamera.orthographicSize;
            fluidCamera.fieldOfView = mainCamera.fieldOfView;
            fluidCamera.nearClipPlane = mainCamera.nearClipPlane;
            fluidCamera.farClipPlane = mainCamera.farClipPlane;
            fluidCamera.transform.position = mainCamera.transform.position;
            fluidCamera.transform.rotation = mainCamera.transform.rotation;
            fluidCamera.cullingMask = 1 << Mathf.Clamp(waterLayer, 0, 31);

            MaybeRecreateRenderTexture();
            ApplyMaterialSettingsIfNeeded(force: false);
        }

        private void ResolveMainCamera()
        {
            if (mainCamera != null)
                return;

            mainCamera = Camera.main;
            if (mainCamera == null)
                mainCamera = Object.FindFirstObjectByType<Camera>();
        }

        /// <summary>
        /// Particles are drawn on this GameObject's layer (see Fluid2DSystem.RenderParticles).
        /// Host must be on waterLayer so the fluid camera captures them into the RT.
        /// </summary>
        private void SyncParticleHostLayer()
        {
            int layer = Mathf.Clamp(waterLayer, 0, 31);
            if (gameObject.layer != layer)
                gameObject.layer = layer;
        }

        private void ApplyCameraAndLayerMasks()
        {
            int layer = Mathf.Clamp(waterLayer, 0, 31);
            if (fluidCamera != null)
            {
                fluidCamera.cullingMask = 1 << layer;
                fluidCamera.clearFlags = CameraClearFlags.Color;
                fluidCamera.backgroundColor = new Color(0, 0, 0, 0);
                if (fluidRT != null)
                    fluidCamera.targetTexture = fluidRT;
            }

            if (mainCamera != null)
            {
                // Hide raw water particles from the main camera; show only the metaball overlay.
                mainCamera.cullingMask &= ~(1 << layer);
                mainCamera.cullingMask |= 1 << SceneWaterLayer;
            }
        }

        private void CreateOrRepairOverlay()
        {
            if (canvasOverlay == null)
            {
                GameObject canvasObj = new GameObject(CanvasOverlayName);
                // Parent to this system so cleanup is local, but force Default layer so Screen Space
                // paths are never culled with the water particle layer.
                canvasObj.transform.SetParent(transform, false);
                canvasObj.transform.localPosition = Vector3.zero;
                canvasObj.transform.localRotation = Quaternion.identity;
                canvasObj.transform.localScale = Vector3.one;
                canvasObj.layer = 0;
                canvasObj.hideFlags = HideFlags.DontSave;

                canvasOverlay = canvasObj.AddComponent<Canvas>();
                canvasOverlay.renderMode = RenderMode.ScreenSpaceCamera;
                canvasOverlay.worldCamera = mainCamera;
                canvasOverlay.planeDistance = 1f;
                canvasOverlay.overrideSorting = true;
                canvasOverlay.sortingOrder = OverlaySortingOrder;
                canvasOverlay.pixelPerfect = false;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                // No GraphicRaycaster: overlay must never block gameplay input.
            }
            else
            {
                canvasOverlay.gameObject.layer = 0;
                canvasOverlay.gameObject.SetActive(true);
                canvasOverlay.enabled = true;
                canvasOverlay.renderMode = RenderMode.ScreenSpaceCamera;
                canvasOverlay.worldCamera = mainCamera;
                canvasOverlay.planeDistance = 1f;
                canvasOverlay.overrideSorting = true;
                canvasOverlay.sortingOrder = OverlaySortingOrder;
            }

            if (rawImageBlit == null)
            {
                Transform existing = canvasOverlay.transform.Find(MetaballScreenName);
                GameObject imgObj = existing != null
                    ? existing.gameObject
                    : new GameObject(MetaballScreenName);

                if (existing == null)
                {
                    imgObj.transform.SetParent(canvasOverlay.transform, false);
                    imgObj.hideFlags = HideFlags.DontSave;
                }

                imgObj.layer = 0;
                imgObj.SetActive(true);

                RectTransform rect = imgObj.GetComponent<RectTransform>();
                if (rect == null)
                    rect = imgObj.AddComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one;

                rawImageBlit = imgObj.GetComponent<RawImage>();
                if (rawImageBlit == null)
                    rawImageBlit = imgObj.AddComponent<RawImage>();
            }

            rawImageBlit.raycastTarget = false;
            rawImageBlit.enabled = true;
            rawImageBlit.color = Color.white;
            rawImageBlit.material = runtimeMetaballMaterial != null ? runtimeMetaballMaterial : metaballMaterial;
        }

        private void BindOverlayToRenderTexture()
        {
            if (rawImageBlit == null || fluidRT == null)
                return;

            rawImageBlit.texture = fluidRT;
            if (runtimeMetaballMaterial != null)
                rawImageBlit.material = runtimeMetaballMaterial;
            else if (metaballMaterial != null)
                rawImageBlit.material = metaballMaterial;
        }

        private void TryRecoverExistingAutoObjects()
        {
            if (fluidCamera == null)
            {
                Transform camT = transform.Find(FluidCameraName);
                if (camT != null)
                    fluidCamera = camT.GetComponent<Camera>();
            }

            if (canvasOverlay == null)
            {
                Transform canvasT = transform.Find(CanvasOverlayName);
                if (canvasT != null)
                    canvasOverlay = canvasT.GetComponent<Canvas>();
            }

            if (canvasOverlay != null && rawImageBlit == null)
            {
                Transform imgT = canvasOverlay.transform.Find(MetaballScreenName);
                if (imgT != null)
                    rawImageBlit = imgT.GetComponent<RawImage>();
                if (rawImageBlit == null)
                    rawImageBlit = canvasOverlay.GetComponentInChildren<RawImage>(true);
            }
        }

        private void DestroyNamedAutoChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child == null) continue;
                if (child.name != FluidCameraName && child.name != CanvasOverlayName)
                    continue;

                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        private void MaybeRecreateRenderTexture()
        {
            ComputeDesiredRtSize(out int expectedW, out int expectedH);

            bool scaleChanged = !Mathf.Approximately(lastResolutionScale, resolutionScale);
            bool sizeChanged = fluidRT == null || fluidRT.width != expectedW || fluidRT.height != expectedH;

            if (!sizeChanged && !scaleChanged)
            {
                consecutiveUnchangedRtFrames++;
                pendingRtChangeRealtime = -1f;
                return;
            }

            // Debounce rapid resize (window drag / Game view thrash).
            if (pendingRtChangeRealtime < 0f ||
                pendingRtWidth != expectedW ||
                pendingRtHeight != expectedH)
            {
                pendingRtChangeRealtime = Time.realtimeSinceStartup;
                pendingRtWidth = expectedW;
                pendingRtHeight = expectedH;
            }

            bool immediate = scaleChanged || fluidRT == null;
            bool debounced = (Time.realtimeSinceStartup - pendingRtChangeRealtime) >= RtSizeDebounceSeconds;

            if (!immediate && !debounced)
                return;

            // Resize RT in place without tearing down cameras/canvas when only size changes.
            if (fluidRT != null && fluidCamera != null && rawImageBlit != null)
            {
                fluidRT.Release();
                if (Application.isPlaying) Destroy(fluidRT);
                else DestroyImmediate(fluidRT);

                fluidRT = CreateFluidRenderTexture(expectedW, expectedH);
                fluidCamera.targetTexture = fluidRT;
                rawImageBlit.texture = fluidRT;
                lastRtWidth = expectedW;
                lastRtHeight = expectedH;
                lastResolutionScale = resolutionScale;
                pendingRtChangeRealtime = -1f;
                renderTextureRecreationCount++;
                consecutiveUnchangedRtFrames = 0;
                return;
            }

            SetupRendering();
            consecutiveUnchangedRtFrames = 0;
        }

        private void ComputeDesiredRtSize(out int w, out int h)
        {
            float scale = Mathf.Max(0.25f, resolutionScale);
            int srcW = mainCamera != null ? mainCamera.pixelWidth : 1920;
            int srcH = mainCamera != null ? mainCamera.pixelHeight : 1080;
            if (srcW <= 1) srcW = Screen.width > 0 ? Screen.width : 1920;
            if (srcH <= 1) srcH = Screen.height > 0 ? Screen.height : 1080;
            w = Mathf.Max(MinRtDimension, Mathf.RoundToInt(srcW * scale));
            h = Mathf.Max(MinRtDimension, Mathf.RoundToInt(srcH * scale));
            if (w <= 0 || h <= 0)
            {
                w = 1920;
                h = 1080;
            }
        }

        private static RenderTexture CreateFluidRenderTexture(int w, int h)
        {
            // URP Render Graph requires camera target textures to have a depth/stencil buffer.
            var rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Fluid2D_MetaballRT"
            };
            rt.Create();
            return rt;
        }

        private void EnsureRuntimeMaterial()
        {
            if (metaballMaterial == null)
            {
                ReleaseRuntimeMaterial();
                return;
            }

            if (runtimeMetaballMaterial != null && runtimeMaterialSource == metaballMaterial)
                return;

            ReleaseRuntimeMaterial();
            runtimeMaterialSource = metaballMaterial;
            runtimeMetaballMaterial = new Material(metaballMaterial)
            {
                name = metaballMaterial.name + " (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
            materialPropsDirty = true;

            if (rawImageBlit != null)
                rawImageBlit.material = runtimeMetaballMaterial;
        }

        private void ReleaseRuntimeMaterial()
        {
            if (runtimeMetaballMaterial != null)
            {
                if (Application.isPlaying) Destroy(runtimeMetaballMaterial);
                else DestroyImmediate(runtimeMetaballMaterial);
                runtimeMetaballMaterial = null;
            }
            runtimeMaterialSource = null;
        }

        /// <summary>
        /// Assign a new shared metaball material. Uses a fresh runtime instance; never mutates the source asset.
        /// </summary>
        public void SetWaterMaterial(Material material)
        {
            metaballMaterial = material;
            ReleaseRuntimeMaterial();
            EnsureRuntimeMaterial();
            if (rawImageBlit != null)
                rawImageBlit.material = runtimeMetaballMaterial != null ? runtimeMetaballMaterial : metaballMaterial;
            MarkMaterialDirty();
            ApplyMaterialSettingsIfNeeded(force: true);
        }

        private void ApplyMaterialSettingsIfNeeded(bool force)
        {
            EnsureRuntimeMaterial();
            Material target = runtimeMetaballMaterial != null ? runtimeMetaballMaterial : metaballMaterial;
            if (target == null) return;

            if (fluidSystem == null) fluidSystem = GetComponent<Fluid2DSystem>();
            var settings = fluidSystem != null ? fluidSystem.settings : null;
            if (settings == null) return;

            int hash = ComputeSettingsHash(settings);
            if (!force && !materialPropsDirty && hash == lastAppliedSettingsHash)
            {
                consecutiveUnchangedMaterialFrames++;
                return;
            }

            ApplyMaterialSettings(target, settings);
            lastAppliedSettingsHash = hash;
            materialPropsDirty = false;
            materialPropertyApplyCount++;
            consecutiveUnchangedMaterialFrames = 0;
            lastStableFrameCount = Time.frameCount;
        }

        private static void ApplyMaterialSettings(Material mat, Fluid2DSettings settings)
        {
            mat.SetColor("_WaterColor", settings.surfaceColor);
            mat.SetColor("_DeepColor", settings.deepColor);
            mat.SetColor("_FoamColor", settings.foamColor);
            mat.SetColor("_OutlineColor", settings.outlineColor);
            mat.SetFloat("_Threshold", settings.densityThreshold);
            mat.SetFloat("_OutlineWidth", settings.outlineWidth);
            mat.SetFloat("_EdgeSoftness", settings.edgeSoftness);
            mat.SetFloat("_DepthStrength", settings.depthDarkening);
            mat.SetFloat("_HighlightStrength", settings.highlightStrength);
            mat.SetFloat("_RippleStrength", settings.rippleStrength);
            mat.SetFloat("_VerticalGradient", settings.verticalGradient);
            mat.SetFloat("_SurfaceBandStrength", settings.surfaceBandStrength);
            mat.SetFloat("_SurfaceBandWidth", settings.surfaceBandWidth);
            mat.SetFloat("_NoiseStrength", settings.noiseStrength);
            mat.SetFloat("_NoiseScale", settings.noiseScale);
            mat.SetFloat("_NoiseScaleB", settings.noiseScaleB);
            mat.SetFloat("_NoiseSpeed", settings.noiseSpeed);
            mat.SetFloat("_FresnelStrength", settings.fresnelStrength);
            mat.SetFloat("_FresnelPower", settings.fresnelPower);
            mat.SetFloat("_FoamCurvature", settings.foamCurvature);
            mat.SetFloat("_SparkleStrength", settings.sparkleStrength);
            mat.SetFloat("_BodyAlpha", settings.bodyAlpha);
            mat.SetFloat("_BandContrast", settings.bandContrast);
            mat.SetFloat("_BottomRimScale", settings.bottomRimScale);
        }

        /// <summary>
        /// Stable hash of visual settings fields that affect the metaball material.
        /// </summary>
        public static int ComputeSettingsHash(Fluid2DSettings s)
        {
            if (s == null) return 0;
            unchecked
            {
                int h = 17;
                h = h * 31 + s.surfaceColor.GetHashCode();
                h = h * 31 + s.deepColor.GetHashCode();
                h = h * 31 + s.foamColor.GetHashCode();
                h = h * 31 + s.outlineColor.GetHashCode();
                h = h * 31 + s.densityThreshold.GetHashCode();
                h = h * 31 + s.outlineWidth.GetHashCode();
                h = h * 31 + s.edgeSoftness.GetHashCode();
                h = h * 31 + s.depthDarkening.GetHashCode();
                h = h * 31 + s.highlightStrength.GetHashCode();
                h = h * 31 + s.rippleStrength.GetHashCode();
                h = h * 31 + s.verticalGradient.GetHashCode();
                h = h * 31 + s.surfaceBandStrength.GetHashCode();
                h = h * 31 + s.surfaceBandWidth.GetHashCode();
                h = h * 31 + s.noiseStrength.GetHashCode();
                h = h * 31 + s.noiseScale.GetHashCode();
                h = h * 31 + s.noiseScaleB.GetHashCode();
                h = h * 31 + s.noiseSpeed.GetHashCode();
                h = h * 31 + s.fresnelStrength.GetHashCode();
                h = h * 31 + s.fresnelPower.GetHashCode();
                h = h * 31 + s.foamCurvature.GetHashCode();
                h = h * 31 + s.sparkleStrength.GetHashCode();
                h = h * 31 + s.bodyAlpha.GetHashCode();
                h = h * 31 + s.bandContrast.GetHashCode();
                h = h * 31 + s.bottomRimScale.GetHashCode();
                h = h * 31 + s.GetInstanceID();
                return h;
            }
        }
    }
}
