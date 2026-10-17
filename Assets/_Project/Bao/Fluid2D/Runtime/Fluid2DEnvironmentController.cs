using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.U2D;

namespace Bao.Fluid2D
{
    /// <summary>
    /// Production facade for an interactive ice/water environment region.
    /// Designers wire look + quality here; gameplay calls Melt/Freeze without touching GPU buffers.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Bao/Fluid2D/Environment Controller")]
    public class Fluid2DEnvironmentController : MonoBehaviour
    {
        public enum ValidationSeverity
        {
            Ok = 0,
            Info = 1,
            Warning = 2,
            Error = 3
        }

        [Serializable]
        public struct ValidationIssue
        {
            public ValidationSeverity severity;
            public string code;
            public string message;
            public string fix;

            public ValidationIssue(ValidationSeverity severity, string code, string message, string fix)
            {
                this.severity = severity;
                this.code = code;
                this.message = message;
                this.fix = fix;
            }
        }

        [Header("Preset")]
        [SerializeField] private Fluid2DEnvironmentPreset preset;

        [Header("Auto-Wired References")]
        [SerializeField] private Fluid2DSystem fluidSystem;
        [SerializeField] private Fluid2DMetaballRenderer metaballRenderer;
        [SerializeField] private Fluid2DObstacleManager obstacleManager;
        [SerializeField] private IceWaterPhaseToggle2D phaseToggle;
        [SerializeField] private SpriteShapeController iceSpriteShapeController;
        [SerializeField] private SpriteShapeRenderer iceSpriteShapeRenderer;
        [SerializeField] private FluidEmitter2D emitter;
        [SerializeField] private FluidDrain2D drain;
        [SerializeField] private Camera mainCamera;

        [Header("Look Overrides (optional)")]
        [SerializeField] private Fluid2DSettings settingsOverride;
        [SerializeField] private Material waterMaterialOverride;
        [SerializeField] private Material splatMaterialOverride;
        [SerializeField] private SpriteShape iceProfileOverride;

        [Header("Phase / Quality")]
        [SerializeField] private IceWaterPhaseToggle2D.Phase initialPhase = IceWaterPhaseToggle2D.Phase.Ice;
        [SerializeField] private Fluid2DQualityLevel quality = Fluid2DQualityLevel.Balanced;

        [Header("Layers")]
        [SerializeField] private LayerMask terrainObstacleLayers = ~0;
        [SerializeField] private LayerMask dynamicInteractionLayers;
        [SerializeField] private int waterLayer = 16;

        [Header("Input")]
        [Tooltip("Production default is OFF. Enable only for local testing.")]
        [SerializeField] private bool enableKeyboardToggle = false;
        [SerializeField] private KeyCode toggleKey = KeyCode.None;

        [Header("Events (fire once per transition)")]
        public UnityEvent onPhaseChanging = new UnityEvent();
        public UnityEvent onPhaseChanged = new UnityEvent();
        public UnityEvent onEnteredWater = new UnityEvent();
        public UnityEvent onEnteredIce = new UnityEvent();

        public event Action<IceWaterPhaseToggle2D.Phase, IceWaterPhaseToggle2D.Phase> PhaseChanging;
        public event Action<IceWaterPhaseToggle2D.Phase, IceWaterPhaseToggle2D.Phase> PhaseChanged;
        public event Action EnteredWater;
        public event Action EnteredIce;

        [Header("Diagnostics (read-only)")]
        [SerializeField] private string lastValidationSummary = "Not validated";
        [SerializeField] private ValidationSeverity lastValidationSeverity = ValidationSeverity.Info;
        [SerializeField] private bool subscribedToPhaseEvents;

        private readonly List<ValidationIssue> lastIssues = new List<ValidationIssue>(16);
        private SpriteShape runtimeIceProfileInstance;
        private Material runtimeSettingsOwnerHint;
        private bool appliedInitialPhase;

        // ── Public read-only state ──────────────────────────────────────────

        public Fluid2DEnvironmentPreset Preset => preset;
        public Fluid2DSystem FluidSystem => fluidSystem;
        public Fluid2DMetaballRenderer MetaballRenderer => metaballRenderer;
        public Fluid2DObstacleManager ObstacleManager => obstacleManager;
        public IceWaterPhaseToggle2D PhaseToggle => phaseToggle;
        public SpriteShapeController IceSpriteShapeController => iceSpriteShapeController;
        public SpriteShapeRenderer IceSpriteShapeRenderer => iceSpriteShapeRenderer;
        public FluidEmitter2D Emitter => emitter;
        public FluidDrain2D Drain => drain;
        public Camera MainCamera => mainCamera;

        public IceWaterPhaseToggle2D.Phase CurrentPhase =>
            phaseToggle != null ? phaseToggle.CurrentPhase : initialPhase;

        public bool IsIce => CurrentPhase == IceWaterPhaseToggle2D.Phase.Ice;
        public bool IsWater => CurrentPhase == IceWaterPhaseToggle2D.Phase.Water;

        public int ActiveParticleCount => fluidSystem != null ? fluidSystem.ActiveParticleCount : 0;
        public int ConservedMass => phaseToggle != null ? phaseToggle.ConservedPhaseMass : 0;
        public bool HasConservedMass => phaseToggle != null && phaseToggle.HasConservedPhaseMass;
        public Fluid2DQualityLevel Quality => quality;
        public bool EnableKeyboardToggle => enableKeyboardToggle;
        public ValidationSeverity LastValidationSeverity => lastValidationSeverity;
        public string LastValidationSummary => lastValidationSummary;
        public IReadOnlyList<ValidationIssue> LastValidationIssues => lastIssues;

        public int MaterialPropertyApplyCount =>
            metaballRenderer != null ? metaballRenderer.MaterialPropertyApplyCount : 0;
        public int RenderTextureRecreationCount =>
            metaballRenderer != null ? metaballRenderer.RenderTextureRecreationCount : 0;
        public int DrainRegistryCount => FluidDrainRegistry.RegisteredCount;
        public int DrainFullScanCount => FluidDrainRegistry.FullScanCount;
        public float LastObstacleRefreshRealtime =>
            obstacleManager != null ? obstacleManager.LastObstacleRefreshRealtime : 0f;
        public int ObstacleRefreshCount =>
            obstacleManager != null ? obstacleManager.ObstacleRefreshCount : 0;
        public int CurrentRtWidth => metaballRenderer != null ? metaballRenderer.CurrentRtWidth : 0;
        public int CurrentRtHeight => metaballRenderer != null ? metaballRenderer.CurrentRtHeight : 0;

        private void Awake()
        {
            AutoWire(log: false);
            ApplyQualityInternal(quality, force: true);
            SubscribePhaseEvents();
        }

        private void Start()
        {
            EnsureMetaballCompositing();

            if (!appliedInitialPhase && phaseToggle != null && Application.isPlaying)
            {
                // Align with designer initial phase without double-firing if toggle already matches.
                if (phaseToggle.CurrentPhase != initialPhase)
                    phaseToggle.SetPhase(initialPhase);
                appliedInitialPhase = true;
            }

            EnsureMetaballCompositing();
            ValidateSetup();
        }

        private void OnEnable()
        {
            SubscribePhaseEvents();
        }

        private void OnDisable()
        {
            UnsubscribePhaseEvents();
        }

        private void OnDestroy()
        {
            UnsubscribePhaseEvents();
            ReleaseRuntimeIceProfileInstance();
        }

        private void Update()
        {
            if (!Application.isPlaying || !enableKeyboardToggle)
                return;
            if (toggleKey == KeyCode.None)
                return;
            if (Input.GetKeyDown(toggleKey))
                TogglePhase();
        }

        // ── Gameplay API ────────────────────────────────────────────────────

        public void Melt()
        {
            EnsurePhaseToggle();
            EnsureMetaballCompositing();
            phaseToggle?.Melt();
            EnsureMetaballCompositing();
        }

        public void Freeze()
        {
            EnsurePhaseToggle();
            phaseToggle?.Freeze();
        }

        public void TogglePhase()
        {
            EnsurePhaseToggle();
            EnsureMetaballCompositing();
            phaseToggle?.Toggle();
            EnsureMetaballCompositing();
        }

        public void SetPhase(IceWaterPhaseToggle2D.Phase phase)
        {
            EnsurePhaseToggle();
            EnsureMetaballCompositing();
            phaseToggle?.SetPhase(phase);
            EnsureMetaballCompositing();
        }

        public void RefreshObstacles()
        {
            if (obstacleManager == null)
                AutoWire(log: false);
            obstacleManager?.RefreshObstacles();
        }

        public void SetQuality(Fluid2DQualityLevel level)
        {
            quality = level;
            ApplyQualityInternal(level, force: true);
        }

        public void ApplyPreset(Fluid2DEnvironmentPreset source)
        {
            if (source == null)
            {
                Debug.LogWarning("Fluid2DEnvironmentController.ApplyPreset: preset is null.", this);
                return;
            }

            preset = source;
            if (source.settings != null)
                settingsOverride = source.settings;
            if (source.waterMetaballMaterial != null)
                waterMaterialOverride = source.waterMetaballMaterial;
            if (source.splatMaterial != null)
                splatMaterialOverride = source.splatMaterial;
            if (source.iceSpriteShapeProfile != null)
                iceProfileOverride = source.iceSpriteShapeProfile;

            initialPhase = source.initialPhase;
            quality = source.quality;
            terrainObstacleLayers = source.terrainObstacleLayers;
            dynamicInteractionLayers = source.dynamicInteractionLayers;
            waterLayer = source.waterLayer;

            AutoWire(log: false);
            PushLookAndSettingsToSystems();
            ApplyQualityInternal(quality, force: true);

            if (phaseToggle != null)
            {
                phaseToggle.startAsIce = initialPhase == IceWaterPhaseToggle2D.Phase.Ice;
                phaseToggle.particleSpacing = source.particleSpacing;
                phaseToggle.maxParticlesPerToggle = source.maxParticlesPerToggle;
                phaseToggle.disableAdaptiveMeltSpacing = source.disableAdaptiveMeltSpacing;
                phaseToggle.particlesPerUnitArea = source.particlesPerUnitArea;
                phaseToggle.logMeltDiagnostics = source.logMeltDiagnostics;
                // Production controller owns keyboard by default.
                if (!enableKeyboardToggle)
                    phaseToggle.enableKeyboardToggle = false;
            }

            if (source.iceSpriteShapeProfile != null)
                SetIceSpriteShapeProfile(source.iceSpriteShapeProfile);

            if (Application.isPlaying && phaseToggle != null)
                phaseToggle.SetPhase(initialPhase);
        }

        /// <summary>
        /// Assign ice SpriteShape profile. Authored controllers get a direct reference;
        /// freeze generation still clones at runtime and never mutates the source asset.
        /// </summary>
        public void SetIceSpriteShapeProfile(SpriteShape profile)
        {
            if (profile == null)
                return;

            iceProfileOverride = profile;
            if (iceSpriteShapeController == null)
                AutoWire(log: false);

            if (phaseToggle != null)
            {
                phaseToggle.SetIceSpriteShapeProfile(profile);
            }
            else if (iceSpriteShapeController != null)
            {
                iceSpriteShapeController.spriteShape = profile;
            }
        }

        public void SetWaterMaterial(Material material)
        {
            if (material == null)
                return;

            waterMaterialOverride = material;
            if (metaballRenderer == null)
                AutoWire(log: false);

            metaballRenderer?.SetWaterMaterial(material);
        }

        public void SetEnableKeyboardToggle(bool enabled, KeyCode key = KeyCode.None)
        {
            enableKeyboardToggle = enabled;
            if (key != KeyCode.None)
                toggleKey = key;

            // Avoid double-toggle when controller owns input.
            if (phaseToggle != null)
                phaseToggle.enableKeyboardToggle = false;
        }

        // ── Wiring / validation ─────────────────────────────────────────────

        [ContextMenu("Auto Wire")]
        public void AutoWire()
        {
            AutoWire(log: true);
        }

        /// <summary>
        /// Rebind ice + phaseToggle to a child SpriteShape (prefer name IceShape) when refs are
        /// missing, or when they point outside this hierarchy and <paramref name="forceIfExternal"/> is true.
        /// Does not steal unrelated map SpriteShapes that were intentionally assigned as external ice
        /// unless force is requested (Repair / starter create path).
        /// </summary>
        public bool TryBindIceFromHierarchy(bool forceIfExternal = false)
        {
            var childIce = FindPreferredChildIceShape();
            if (childIce == null)
                return false;

            bool iceMissing = iceSpriteShapeController == null;
            bool iceExternal = iceSpriteShapeController != null
                               && !iceSpriteShapeController.transform.IsChildOf(transform)
                               && iceSpriteShapeController.transform != transform;
            bool phaseMissing = phaseToggle == null;
            bool phaseExternal = phaseToggle != null
                                 && !phaseToggle.transform.IsChildOf(transform)
                                 && phaseToggle.transform != transform;

            bool shouldRebind = iceMissing || phaseMissing
                                || (forceIfExternal && (iceExternal || phaseExternal));
            if (!shouldRebind)
            {
                // Still ensure child phase has controller pointers if we already own the child ice.
                if (!iceExternal && iceSpriteShapeController != null)
                    CrossWirePhaseToIce(iceSpriteShapeController);
                return iceSpriteShapeController != null;
            }

            iceSpriteShapeController = childIce;
            iceSpriteShapeRenderer = childIce.GetComponent<SpriteShapeRenderer>();
            var childPhase = childIce.GetComponent<IceWaterPhaseToggle2D>()
                             ?? childIce.GetComponentInChildren<IceWaterPhaseToggle2D>(true);
            if (childPhase != null)
                phaseToggle = childPhase;

            CrossWirePhaseToIce(childIce);
            return true;
        }

        public void AutoWire(bool log)
        {
            // Prefer region-local children only for ice/phase — never climb to parents
            // (avoids wiring Snow Foreground / map SpriteShapes by accident).
            if (phaseToggle == null)
                phaseToggle = GetComponent<IceWaterPhaseToggle2D>()
                              ?? GetComponentInChildren<IceWaterPhaseToggle2D>(true);

            if (iceSpriteShapeController == null)
                iceSpriteShapeController = GetComponent<SpriteShapeController>()
                                           ?? FindPreferredChildIceShape()
                                           ?? (phaseToggle != null ? phaseToggle.spriteShapeController : null);

            if (iceSpriteShapeRenderer == null)
            {
                if (iceSpriteShapeController != null)
                    iceSpriteShapeRenderer = iceSpriteShapeController.GetComponent<SpriteShapeRenderer>();
                if (iceSpriteShapeRenderer == null)
                    iceSpriteShapeRenderer = GetComponent<SpriteShapeRenderer>()
                                             ?? GetComponentInChildren<SpriteShapeRenderer>(true)
                                             ?? (phaseToggle != null ? phaseToggle.spriteShapeRenderer : null);
            }

            // Fluid stack stays under this region first; only then fall back to scene search.
            if (fluidSystem == null)
                fluidSystem = GetComponent<Fluid2DSystem>()
                              ?? GetComponentInChildren<Fluid2DSystem>(true);

            if (metaballRenderer == null)
                metaballRenderer = GetComponent<Fluid2DMetaballRenderer>()
                                   ?? GetComponentInChildren<Fluid2DMetaballRenderer>(true)
                                   ?? (fluidSystem != null ? fluidSystem.GetComponent<Fluid2DMetaballRenderer>() : null);

            if (obstacleManager == null)
                obstacleManager = GetComponent<Fluid2DObstacleManager>()
                                  ?? GetComponentInChildren<Fluid2DObstacleManager>(true)
                                  ?? (fluidSystem != null ? fluidSystem.obstacleManager : null);

            if (emitter == null)
                emitter = GetComponentInChildren<FluidEmitter2D>(true);

            if (drain == null)
                drain = GetComponentInChildren<FluidDrain2D>(true);

            if (mainCamera == null)
            {
                if (metaballRenderer != null && metaballRenderer.mainCamera != null)
                    mainCamera = metaballRenderer.mainCamera;
                else
                    mainCamera = Camera.main ?? FindFirstObjectByType<Camera>();
            }

            // Cross-wire core refs.
            if (phaseToggle != null)
            {
                if (phaseToggle.fluidSystem == null)
                    phaseToggle.fluidSystem = fluidSystem;
                if (phaseToggle.spriteShapeController == null)
                    phaseToggle.spriteShapeController = iceSpriteShapeController;
                if (phaseToggle.spriteShapeRenderer == null)
                    phaseToggle.spriteShapeRenderer = iceSpriteShapeRenderer;
                if (phaseToggle.iceCollider == null && iceSpriteShapeController != null)
                    phaseToggle.iceCollider = iceSpriteShapeController.GetComponent<Collider2D>();
                // Production controller owns keyboard unless designer explicitly enables both.
                if (!enableKeyboardToggle)
                    phaseToggle.enableKeyboardToggle = false;
            }

            if (fluidSystem != null)
            {
                if (fluidSystem.obstacleManager == null)
                    fluidSystem.obstacleManager = obstacleManager;
            }

            if (obstacleManager != null && obstacleManager.fluidSystem == null)
                obstacleManager.fluidSystem = fluidSystem;

            if (metaballRenderer != null)
            {
                if (metaballRenderer.mainCamera == null)
                    metaballRenderer.mainCamera = mainCamera;
                metaballRenderer.waterLayer = waterLayer;
                // Particles are drawn on FluidSystem layer; keep it aligned with waterLayer.
                if (fluidSystem != null)
                    fluidSystem.gameObject.layer = Mathf.Clamp(waterLayer, 0, 31);
                else
                    metaballRenderer.gameObject.layer = Mathf.Clamp(waterLayer, 0, 31);
            }

            if (emitter != null && emitter.fluidSystem == null)
                emitter.fluidSystem = fluidSystem;
            if (drain != null && drain.fluidSystem == null)
                drain.fluidSystem = fluidSystem;

            if (obstacleManager != null)
                obstacleManager.obstacleLayer = terrainObstacleLayers;

            PushLookAndSettingsToSystems();
            // Always rebind phase events after wiring so a late-found phaseToggle is hooked.
            UnsubscribePhaseEvents();
            SubscribePhaseEvents();

            if (log)
                Debug.Log($"Fluid2DEnvironmentController: AutoWire complete on '{name}'.", this);
        }

        private SpriteShapeController FindPreferredChildIceShape()
        {
            var all = GetComponentsInChildren<SpriteShapeController>(true);
            SpriteShapeController named = null;
            SpriteShapeController withPhase = null;
            for (int i = 0; i < all.Length; i++)
            {
                var s = all[i];
                if (s == null) continue;
                if (s.name == "IceShape")
                    named = s;
                if (s.GetComponent<IceWaterPhaseToggle2D>() != null && withPhase == null)
                    withPhase = s;
            }
            return named ?? withPhase ?? (all.Length > 0 ? all[0] : null);
        }

        private void CrossWirePhaseToIce(SpriteShapeController ice)
        {
            if (ice == null || phaseToggle == null)
                return;
            if (phaseToggle.fluidSystem == null)
                phaseToggle.fluidSystem = fluidSystem;
            phaseToggle.spriteShapeController = ice;
            if (phaseToggle.spriteShapeRenderer == null)
                phaseToggle.spriteShapeRenderer = ice.GetComponent<SpriteShapeRenderer>();
            if (phaseToggle.iceCollider == null)
                phaseToggle.iceCollider = ice.GetComponent<Collider2D>();
        }

        [ContextMenu("Validate Setup")]
        public List<ValidationIssue> ValidateSetup()
        {
            lastIssues.Clear();
            AutoWire(log: false);

            if (mainCamera == null)
            {
                AddIssue(ValidationSeverity.Error, "missing_camera",
                    "No Camera found for fluid compositing.",
                    "Assign Main Camera on the Metaball Renderer or tag a Camera as MainCamera.");
            }

            // Multiple InteractiveWater2D roots (including inactive duplicates) confuse freeze diagnostics
            // and FindFirstObjectByType-based tools — warn designers early.
            WarnIfMultipleInteractiveWaterRoots();

            if (fluidSystem == null)
            {
                AddIssue(ValidationSeverity.Error, "missing_fluid_system",
                    "Fluid2DSystem is missing.",
                    "Add Fluid2DSystem (wizard/prefab) or assign the reference.");
            }
            else
            {
                if (fluidSystem.simulationShader == null)
                    AddIssue(ValidationSeverity.Error, "missing_compute",
                        "Fluid2DSystem has no Compute Shader.",
                        "Assign FluidSimulation.compute on Fluid2DSystem.");
                if (fluidSystem.settings == null)
                    AddIssue(ValidationSeverity.Error, "missing_settings",
                        "Fluid2DSystem has no Fluid2DSettings.",
                        "Assign Settings asset or apply an Environment Preset.");
                if (fluidSystem.particleMaterial == null)
                    AddIssue(ValidationSeverity.Warning, "missing_splat",
                        "Fluid2DSystem has no particle splat material.",
                        "Assign FluidSplat_Mat or apply a preset.");
            }

            if (metaballRenderer == null)
            {
                AddIssue(ValidationSeverity.Error, "missing_metaball",
                    "Fluid2DMetaballRenderer is missing.",
                    "Add Fluid2DMetaballRenderer next to Fluid2DSystem.");
            }
            else
            {
                if (metaballRenderer.metaballMaterial == null && waterMaterialOverride == null)
                {
                    AddIssue(ValidationSeverity.Error, "missing_water_mat",
                        "No water metaball material assigned.",
                        "Assign FluidMetaball_Mat or apply a preset.");
                }

                // Ensure / inspect compositing path (overlay is required for visible water).
                if (Application.isPlaying && metaballRenderer.isActiveAndEnabled)
                    metaballRenderer.EnsureCompositing(forceRebuild: false);

                ValidateMetaballCompositing(metaballRenderer);
            }

            if (phaseToggle == null)
            {
                AddIssue(ValidationSeverity.Error, "missing_phase",
                    "IceWaterPhaseToggle2D is missing.",
                    "Add IceWaterPhaseToggle2D on the ice SpriteShape object, or run Repair.");
            }
            else
            {
                var childIce = FindPreferredChildIceShape();
                bool phaseOutside = !phaseToggle.transform.IsChildOf(transform)
                                    && phaseToggle.transform != transform;
                if (phaseOutside && childIce != null)
                {
                    AddIssue(ValidationSeverity.Warning, "phase_external_with_child_ice",
                        $"phaseToggle points at '{phaseToggle.name}' outside this region while child ice '{childIce.name}' exists.",
                        "Run Tools/Bao/Fluid2D/Repair Scene Environment Regions or Repair Selected Region with Ice field empty.");
                }

                // Melt coverage: warn when authored ice is too large for preferred density / system pool.
                try
                {
                    var meltEst = phaseToggle.EstimateMeltCoverage();
                    if (meltEst.budgetLimited)
                    {
                        AddIssue(ValidationSeverity.Warning, "melt_budget_limited",
                            meltEst.diagnostic,
                            "Raise IceWaterPhaseToggle2D.maxParticlesPerToggle and Fluid2DSettings.maxParticles, " +
                            "or lower particlesPerUnitArea / increase particleSpacing. " +
                            "Adaptive melt spacing still covers the silhouette (sparser water). Disable only if you intentionally want preferred spacing.");
                    }

                    int systemCap = meltEst.systemParticleCap;
                    if (phaseToggle.maxParticlesPerToggle > systemCap)
                    {
                        AddIssue(ValidationSeverity.Warning, "melt_budget_exceeds_system",
                            $"maxParticlesPerToggle ({phaseToggle.maxParticlesPerToggle}) exceeds Fluid2DSettings.maxParticles ({systemCap}). " +
                            "Melt will clamp to the system pool size.",
                            "Increase Fluid2DSettings.maxParticles, or lower maxParticlesPerToggle.");
                    }

                    if (fluidSystem != null && fluidSystem.settings != null && iceSpriteShapeRenderer != null)
                    {
                        Vector4 wb = fluidSystem.settings.worldBounds;
                        Bounds iceB = iceSpriteShapeRenderer.bounds;
                        bool fullyOutside =
                            iceB.max.x < wb.x || iceB.min.x > wb.z ||
                            iceB.max.y < wb.y || iceB.min.y > wb.w;
                        bool partiallyOutside =
                            iceB.min.x < wb.x || iceB.max.x > wb.z ||
                            iceB.min.y < wb.y || iceB.max.y > wb.w;
                        if (fullyOutside)
                        {
                            AddIssue(ValidationSeverity.Error, "ice_outside_fluid_bounds",
                                $"Ice bounds center={iceB.center} size={iceB.size} lie fully outside Fluid2D worldBounds ({wb}). Melted particles will be discarded.",
                                "Expand Fluid2DSettings.worldBounds to enclose the ice SpriteShape, or move/scale ice inside the fluid domain.");
                        }
                        else if (partiallyOutside)
                        {
                            AddIssue(ValidationSeverity.Warning, "ice_partially_outside_fluid_bounds",
                                $"Ice bounds center={iceB.center} size={iceB.size} extend outside Fluid2D worldBounds ({wb}). Edge water may disappear.",
                                "Expand Fluid2DSettings.worldBounds to fully cover the authored ice silhouette.");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    AddIssue(ValidationSeverity.Warning, "melt_estimate_failed",
                        $"Could not estimate melt coverage: {ex.Message}",
                        "Ensure ice SpriteShape has a valid closed spline, then re-run Validate.");
                }
            }

            if (iceSpriteShapeController == null)
            {
                AddIssue(ValidationSeverity.Error, "missing_spriteshape",
                    "Ice SpriteShapeController is missing.",
                    "Leave Ice SpriteShape empty and Create New Region, or Repair to recover child IceShape.");
            }
            else if (iceSpriteShapeController.spriteShape == null)
            {
                AddIssue(ValidationSeverity.Error, "missing_profile",
                    "SpriteShape has no profile asset.",
                    "Assign an ice SpriteShape profile (preset or project asset).");
            }
            else
            {
                // Default Unity SpriteShapeRenderer AABB (~2^20) makes the region appear blank / unselectable.
                if (iceSpriteShapeRenderer != null)
                {
                    Bounds wb = iceSpriteShapeRenderer.bounds;
                    float maxExtent = Mathf.Max(wb.extents.x, Mathf.Max(wb.extents.y, wb.extents.z));
                    if (maxExtent > 10000f || float.IsInfinity(maxExtent) || float.IsNaN(maxExtent))
                    {
                        AddIssue(ValidationSeverity.Warning, "ice_bounds_invalid",
                            $"Ice SpriteShapeRenderer bounds are huge/invalid (extent≈{maxExtent}).",
                            "Run Repair Selected Region or Tools/Bao/Fluid2D/Repair Scene Environment Regions to reset starter AABB.");
                    }
                }
            }

            if (mainCamera != null && !mainCamera.orthographic)
            {
                AddIssue(ValidationSeverity.Warning, "camera_perspective",
                    $"Main camera '{mainCamera.name}' is perspective — starter ice/water framing is designed for 2D orthographic.",
                    "Tools/Bao/Fluid2D/Ensure Main Camera Orthographic (2D), or enable Frame camera on create in the wizard.");
            }

            if (obstacleManager == null)
            {
                AddIssue(ValidationSeverity.Warning, "missing_obstacles",
                    "Fluid2DObstacleManager is missing — water will not collide with terrain.",
                    "Add Fluid2DObstacleManager and set terrain layer mask.");
            }
            else if (obstacleManager.obstacleLayer.value == 0)
            {
                AddIssue(ValidationSeverity.Warning, "empty_obstacle_mask",
                    "Obstacle layer mask is empty.",
                    "Enable terrain/ice layers on Fluid2DObstacleManager.");
            }

            // Single GPU system per camera constraint.
            if (mainCamera != null)
            {
                var renderers = FindObjectsByType<Fluid2DMetaballRenderer>(FindObjectsSortMode.None);
                int sharing = 0;
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null || !r.isActiveAndEnabled) continue;
                    Camera cam = r.mainCamera != null ? r.mainCamera : Camera.main;
                    if (cam == mainCamera)
                        sharing++;
                }

                if (sharing > 1)
                {
                    AddIssue(ValidationSeverity.Error, "multi_system_same_camera",
                        $"Found {sharing} Fluid2DMetaballRenderer components sharing camera '{mainCamera.name}'.",
                        "Architecture supports one GPU fluid system per camera. Disable extras or use another camera.");
                }
            }

            if (metaballRenderer != null && metaballRenderer.resolutionScale < 0.25f)
            {
                AddIssue(ValidationSeverity.Warning, "low_rt_scale",
                    "Metaball resolutionScale is extremely low.",
                    "Use Quality Low/Balanced/High instead of manual extreme values.");
            }

            // Shared material mutation risk — we use runtime instances, but warn if designer edits shared mat at runtime.
            if (metaballRenderer != null && metaballRenderer.metaballMaterial != null)
            {
                string path = string.Empty;
#if UNITY_EDITOR
                path = UnityEditor.AssetDatabase.GetAssetPath(metaballRenderer.metaballMaterial);
#endif
                if (!string.IsNullOrEmpty(path))
                {
                    AddIssue(ValidationSeverity.Info, "shared_material_ok",
                        "Water material is a project asset; runtime uses a private instance (safe).",
                        "Do not write material.Set* on the shared asset from custom scripts — use SetWaterMaterial / settings.");
                }
            }

            if (enableKeyboardToggle && toggleKey == KeyCode.None)
            {
                AddIssue(ValidationSeverity.Warning, "keyboard_no_key",
                    "Keyboard toggle is enabled but Key is None.",
                    "Assign a KeyCode or disable Enable Keyboard Toggle.");
            }

            // Summarize.
            lastValidationSeverity = ValidationSeverity.Ok;
            int errors = 0, warnings = 0;
            for (int i = 0; i < lastIssues.Count; i++)
            {
                if (lastIssues[i].severity == ValidationSeverity.Error) errors++;
                else if (lastIssues[i].severity == ValidationSeverity.Warning) warnings++;
                if (lastIssues[i].severity > lastValidationSeverity)
                    lastValidationSeverity = lastIssues[i].severity;
            }

            lastValidationSummary = errors > 0
                ? $"INVALID — {errors} error(s), {warnings} warning(s)"
                : warnings > 0
                    ? $"OK with {warnings} warning(s)"
                    : "OK";

            return lastIssues;
        }

        public string FormatValidationReport()
        {
            var sb = new StringBuilder(512);
            sb.AppendLine(lastValidationSummary);
            for (int i = 0; i < lastIssues.Count; i++)
            {
                var issue = lastIssues[i];
                sb.Append('[').Append(issue.severity).Append("] ")
                    .Append(issue.code).Append(": ")
                    .Append(issue.message);
                if (!string.IsNullOrEmpty(issue.fix))
                    sb.Append(" | Fix: ").Append(issue.fix);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        // ── Internals ───────────────────────────────────────────────────────

        private void PushLookAndSettingsToSystems()
        {
            Fluid2DSettings settings = settingsOverride != null
                ? settingsOverride
                : (preset != null ? preset.settings : null);

            Material waterMat = waterMaterialOverride != null
                ? waterMaterialOverride
                : (preset != null ? preset.waterMetaballMaterial : null);

            Material splatMat = splatMaterialOverride != null
                ? splatMaterialOverride
                : (preset != null ? preset.splatMaterial : null);

            if (fluidSystem != null)
            {
                if (settings != null)
                    fluidSystem.settings = settings;
                if (splatMat != null)
                    fluidSystem.particleMaterial = splatMat;
            }

            if (metaballRenderer != null)
            {
                if (waterMat != null)
                    metaballRenderer.SetWaterMaterial(waterMat);
                if (mainCamera != null)
                    metaballRenderer.mainCamera = mainCamera;
                metaballRenderer.waterLayer = waterLayer;
                if (fluidSystem != null)
                    fluidSystem.gameObject.layer = Mathf.Clamp(waterLayer, 0, 31);
                else
                    metaballRenderer.gameObject.layer = Mathf.Clamp(waterLayer, 0, 31);
                EnsureMetaballCompositing();
            }

            if (iceProfileOverride != null)
            {
                if (phaseToggle != null)
                    phaseToggle.SetIceSpriteShapeProfile(iceProfileOverride);
                else if (iceSpriteShapeController != null)
                    iceSpriteShapeController.spriteShape = iceProfileOverride;
            }
        }

        private void ApplyQualityInternal(Fluid2DQualityLevel level, bool force)
        {
            var profile = Fluid2DQualityProfile.Get(level);

            if (metaballRenderer == null)
                AutoWire(log: false);

            if (metaballRenderer != null)
            {
                bool scaleChanged = !Mathf.Approximately(metaballRenderer.resolutionScale, profile.ResolutionScale);
                if (force || scaleChanged)
                {
                    metaballRenderer.resolutionScale = profile.ResolutionScale;
                    metaballRenderer.MarkResolutionDirty();
                    metaballRenderer.MarkMaterialDirty();
                    // Apply immediately so designers/tests see new RT dims without waiting a frame.
                    if (Application.isPlaying && metaballRenderer.isActiveAndEnabled && scaleChanged)
                        metaballRenderer.EnsureCompositing(forceRebuild: true);
                    else if (Application.isPlaying && metaballRenderer.isActiveAndEnabled)
                        metaballRenderer.EnsureCompositing(forceRebuild: false);
                }
            }

            if (fluidSystem != null)
                fluidSystem.SetRuntimeSubstepsOverride(profile.Substeps);
        }

        private void EnsurePhaseToggle()
        {
            if (phaseToggle == null)
                AutoWire(log: false);
        }

        private void SubscribePhaseEvents()
        {
            if (phaseToggle == null || subscribedToPhaseEvents)
                return;

            phaseToggle.PhaseChanging += HandlePhaseChanging;
            phaseToggle.PhaseChanged += HandlePhaseChanged;
            phaseToggle.EnteredWater += HandleEnteredWater;
            phaseToggle.EnteredIce += HandleEnteredIce;
            subscribedToPhaseEvents = true;
        }

        private void UnsubscribePhaseEvents()
        {
            if (phaseToggle == null || !subscribedToPhaseEvents)
                return;

            phaseToggle.PhaseChanging -= HandlePhaseChanging;
            phaseToggle.PhaseChanged -= HandlePhaseChanged;
            phaseToggle.EnteredWater -= HandleEnteredWater;
            phaseToggle.EnteredIce -= HandleEnteredIce;
            subscribedToPhaseEvents = false;
        }

        private void HandlePhaseChanging(IceWaterPhaseToggle2D.Phase prev, IceWaterPhaseToggle2D.Phase next)
        {
            PhaseChanging?.Invoke(prev, next);
            onPhaseChanging?.Invoke();
        }

        private void HandlePhaseChanged(IceWaterPhaseToggle2D.Phase prev, IceWaterPhaseToggle2D.Phase next)
        {
            PhaseChanged?.Invoke(prev, next);
            onPhaseChanged?.Invoke();
        }

        private void HandleEnteredWater()
        {
            EnsureMetaballCompositing();
            EnteredWater?.Invoke();
            onEnteredWater?.Invoke();
        }

        private void EnsureMetaballCompositing()
        {
            if (metaballRenderer == null)
                AutoWire(log: false);
            if (metaballRenderer == null)
                return;

            metaballRenderer.waterLayer = waterLayer;
            if (mainCamera != null)
                metaballRenderer.mainCamera = mainCamera;
            if (fluidSystem != null)
                fluidSystem.gameObject.layer = Mathf.Clamp(waterLayer, 0, 31);

            if (metaballRenderer.isActiveAndEnabled)
                metaballRenderer.EnsureCompositing(forceRebuild: false);
        }

        private void ValidateMetaballCompositing(Fluid2DMetaballRenderer renderer)
        {
            if (renderer == null)
                return;

            int layer = Mathf.Clamp(renderer.waterLayer, 0, 31);

            if (renderer.mainCamera == null && mainCamera == null)
            {
                AddIssue(ValidationSeverity.Error, "missing_compositing_camera",
                    "Metaball renderer has no main camera for compositing.",
                    "Assign Main Camera on Fluid2DMetaballRenderer or tag a Camera as MainCamera.");
            }

            Camera cam = renderer.mainCamera != null ? renderer.mainCamera : mainCamera;
            if (cam != null && (cam.cullingMask & (1 << layer)) != 0)
            {
                AddIssue(ValidationSeverity.Warning, "main_camera_sees_water_layer",
                    $"Main camera '{cam.name}' still includes water layer {layer} — raw particles may show or double-draw.",
                    "Exclude the water layer from the main camera (Metaball EnsureCompositing does this at runtime).");
            }

            if (fluidSystem != null && fluidSystem.gameObject.layer != layer)
            {
                AddIssue(ValidationSeverity.Error, "fluid_host_wrong_layer",
                    $"FluidSystem is on layer {fluidSystem.gameObject.layer} but waterLayer={layer}. Particles won't render into the fluid RT.",
                    "Set FluidSystem layer to the water layer (Auto Wire / Repair / EnsureCompositing).");
            }

            if (renderer.FluidCamera == null)
            {
                AddIssue(ValidationSeverity.Error, "missing_fluid_camera",
                    "Fluid Camera (Auto) is missing — particles are not rendered to the water RT.",
                    "Enable Fluid2DMetaballRenderer or call Setup Rendering / EnsureCompositing.");
            }
            else if (renderer.FluidRenderTexture == null || !renderer.FluidRenderTexture.IsCreated())
            {
                AddIssue(ValidationSeverity.Error, "missing_fluid_rt",
                    "Fluid RenderTexture is missing or not created.",
                    "Call Setup Rendering on Fluid2DMetaballRenderer (runs on enable / EnsureCompositing).");
            }

            if (renderer.CanvasOverlay == null)
            {
                AddIssue(ValidationSeverity.Error, "missing_overlay_canvas",
                    "Fluid Canvas Overlay is missing — water RT is not composited to Game View.",
                    "Ensure Fluid2DMetaballRenderer is enabled; overlay is auto-created as 'Fluid Canvas Overlay (Auto)'.");
            }
            else if (!renderer.CanvasOverlay.isActiveAndEnabled)
            {
                AddIssue(ValidationSeverity.Error, "overlay_canvas_disabled",
                    "Fluid Canvas Overlay exists but is disabled.",
                    "Enable the overlay canvas or turn off Fluid2DSystem.debugMode.");
            }

            if (renderer.RawImageBlit == null)
            {
                AddIssue(ValidationSeverity.Error, "missing_overlay_rawimage",
                    "Metaball Screen RawImage is missing — no fullscreen blit of the water RT.",
                    "Repair via EnsureCompositing / Setup Rendering on Fluid2DMetaballRenderer.");
            }
            else
            {
                if (!renderer.RawImageBlit.enabled)
                {
                    AddIssue(ValidationSeverity.Error, "overlay_rawimage_disabled",
                        "Metaball Screen RawImage is disabled.",
                        "Enable RawImage or repair compositing.");
                }
                if (renderer.FluidRenderTexture != null && renderer.RawImageBlit.texture != renderer.FluidRenderTexture)
                {
                    AddIssue(ValidationSeverity.Error, "overlay_rawimage_unbound",
                        "RawImage texture is not bound to the fluid RenderTexture.",
                        "Call EnsureCompositing on Fluid2DMetaballRenderer.");
                }
                if (renderer.RawImageBlit.material == null && renderer.metaballMaterial == null)
                {
                    AddIssue(ValidationSeverity.Error, "overlay_missing_material",
                        "RawImage has no metaball material — water RT would blit unfiltered or invisible.",
                        "Assign FluidMetaball_Mat on the renderer / preset.");
                }
            }

            if (Application.isPlaying && !renderer.IsCompositingReady)
            {
                AddIssue(ValidationSeverity.Error, "compositing_not_ready",
                    $"Water compositing is not ready ({renderer.LastCompositingStatus}).",
                    "Check camera, RT, Canvas overlay, and RawImage; run Validate after Melt.");
            }
        }

        private void HandleEnteredIce()
        {
            EnteredIce?.Invoke();
            onEnteredIce?.Invoke();
        }

        /// <summary>
        /// Scene may contain several InteractiveWater2D / Fluid2DEnvironmentController roots
        /// (often one inactive leftover). Tools and freeze tests that use FindFirstObjectByType
        /// can hit the wrong region — warn so designers clean up duplicates.
        /// </summary>
        private void WarnIfMultipleInteractiveWaterRoots()
        {
            var controllers = FindObjectsByType<Fluid2DEnvironmentController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (controllers == null || controllers.Length <= 1)
                return;

            int active = 0;
            int inactive = 0;
            var names = new System.Text.StringBuilder(128);
            for (int i = 0; i < controllers.Length; i++)
            {
                var c = controllers[i];
                if (c == null) continue;
                bool isActive = c.isActiveAndEnabled && c.gameObject.activeInHierarchy;
                if (isActive) active++;
                else inactive++;
                if (names.Length > 0) names.Append(", ");
                names.Append(c.gameObject.name);
                names.Append(isActive ? " (active)" : " (inactive)");
            }

            if (controllers.Length <= 1)
                return;

            AddIssue(ValidationSeverity.Warning, "multiple_interactive_water_roots",
                $"Found {controllers.Length} Fluid2DEnvironmentController roots " +
                $"({active} active, {inactive} inactive): {names}. " +
                "Freeze/melt diagnostics and FindFirst-based tools may target the wrong pool.",
                "Keep a single InteractiveWater2D region per test scene, or disable/delete inactive duplicates. " +
                "Wire gameplay to the intended controller reference instead of FindFirstObjectByType.");
        }

        private void AddIssue(ValidationSeverity severity, string code, string message, string fix)
        {
            lastIssues.Add(new ValidationIssue(severity, code, message, fix));
        }

        private void ReleaseRuntimeIceProfileInstance()
        {
            if (runtimeIceProfileInstance == null)
                return;
            if (Application.isPlaying) Destroy(runtimeIceProfileInstance);
            else DestroyImmediate(runtimeIceProfileInstance);
            runtimeIceProfileInstance = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Keep designer fields tidy in edit mode without running simulation.
            if (waterLayer < 0) waterLayer = 0;
        }
#endif
    }
}
