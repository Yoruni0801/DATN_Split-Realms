using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.U2D;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Bao.Fluid2D
{
    /// <summary>
    /// Toggle ice platform (SpriteShape + solid collider) &lt;-&gt; Fluid2D water pool for the same region.
    /// Attach to "Ice ground". Keyboard toggle is optional (enableKeyboardToggle).
    /// Melt spawns water inside the current ice silhouette; freeze rebuilds ice from live GPU particle positions.
    /// Prefer <see cref="Fluid2DEnvironmentController"/> for production wiring and gameplay API.
    /// </summary>
    [DisallowMultipleComponent]
    public class IceWaterPhaseToggle2D : MonoBehaviour
    {
        public enum Phase
        {
            Ice = 0,
            Water = 1
        }

        private const string GeneratedBlobPrefix = "GeneratedIceBlob_";
        // SpriteShape Spline insert KEpsilon is 0.01; ValidateSpline rejects sqrMagnitude < 0.001 (≈0.0316).
        // Stay strictly above both so generated contours never log "control points too close".
        private const float MinSplinePointDistance = 0.035f;
        private const float MinBlobWorldRadius = 0.08f;
        // Safe runtime culling pad only. Do not copy authored boundsScale (often 2) onto tiny blobs.
        private const float GeneratedBoundsScale = 1f;
        /// <summary>AABB aspect (max/min) at or above this is treated as a long thin ribbon/pool.</summary>
        private const float ElongatedAspectThreshold = 2.5f;
        private const int MaxFreezeGridDim = 512;

        [Header("References")]
        public Fluid2DSystem fluidSystem;
        public SpriteShapeRenderer spriteShapeRenderer;
        public Collider2D iceCollider;
        public SpriteShapeController spriteShapeController;

        [Header("Input")]
        [Tooltip("When false, keyboard input is ignored. Production scenes should drive Melt/Freeze via Fluid2DEnvironmentController or UnityEvents.")]
        public bool enableKeyboardToggle = true;
        [Tooltip("Only used when enableKeyboardToggle is true. Not hard-wired in production controllers.")]
        public KeyCode toggleKey = KeyCode.Space;
        public bool startAsIce = true;

        [Header("Events (fire once per transition)")]
        public UnityEvent onPhaseChanging = new UnityEvent();
        public UnityEvent onPhaseChanged = new UnityEvent();
        public UnityEvent onEnteredWater = new UnityEvent();
        public UnityEvent onEnteredIce = new UnityEvent();

        /// <summary>C# event: (previous, next) before transition work.</summary>
        public event System.Action<Phase, Phase> PhaseChanging;
        /// <summary>C# event: (previous, next) after transition work.</summary>
        public event System.Action<Phase, Phase> PhaseChanged;
        public event System.Action EnteredWater;
        public event System.Action EnteredIce;

        [Header("Water Spawn")]
        [Tooltip("Preferred particle grid spacing for small/medium ice (world units). Large shapes auto-widen when the budget cannot keep this density.")]
        public float particleSpacing = 0.16f;
        [Tooltip("Inset spawn region from polygon/bounds edges to reduce edge leak.")]
        public float spawnPadding = 0.08f;
        public Vector2 initialVelocity = Vector2.zero;
        [Tooltip("Micro-jitter around sample points to avoid perfect lattice stacking.")]
        public float jitter = 0.02f;
        [Tooltip("Hard cap on particles spawned per melt. Also limited by Fluid2DSettings.maxParticles. Raise for large designer shapes.")]
        public int maxParticlesPerToggle = 2000;
        [Tooltip("When false (default), first melt spreads particles across the full world polygon silhouette and widens spacing if preferred density exceeds budget. Enable only to force preferred spacing (may under-fill large shapes if budget is low).")]
        public bool disableAdaptiveMeltSpacing = false;
        [Tooltip("Optional area-based density (particles per world-unit²). 0 = derive from particleSpacing (≈1/spacing²). Use this when designers scale ice far beyond the starter size.")]
        [Min(0f)]
        public float particlesPerUnitArea = 0f;
        [Tooltip("When true, log melt budget/coverage diagnostics (recommended while tuning large ice).")]
        public bool logMeltDiagnostics = true;
        public int fluidType = 0;

        /// <summary>True when melt may widen spacing / redistribute to preserve silhouette coverage (default).</summary>
        public bool AdaptSpacingToShape => !disableAdaptiveMeltSpacing;

        [Header("Ice Visual / Collision")]
        public bool hideIceWhenWater = true;
        public bool disableColliderWhenWater = true;
        [Tooltip("Cell size for freeze occupancy grid (world units).")]
        public float freezeGridCellSize = 0.12f;
        [Tooltip("Legacy integer-cell stamp radius. Used only when world stamp cannot be resolved.")]
        public float freezeParticleStampRadius = 1.35f;
        [Tooltip("Scale applied to Fluid2D particleRadius when stamping occupancy (splat shader uses ~5.2; 2.5 keeps solid ice close to particle bounds).")]
        [Range(1f, 8f)]
        public float freezeVisualRadiusScale = 2.5f;
        [Tooltip("Extra world padding added to the particle visual stamp radius.")]
        public float freezeStampPadding = 0.02f;
        [Tooltip("Extra outward expansion of frozen contours (world units). Helps cover SpriteShape edge width.")]
        public float freezeContourPadding = 0.04f;
        [Tooltip("SpriteShape edge half-width compensation so the solid fill is not visually thinner than water.")]
        public float freezeEdgeWidthCompensation = 0.02f;
        [Tooltip("Legacy min-cell filter. Clusters below this still freeze as a minimum-radius blob (never dropped silently).")]
        public int minBlobCells = 1;
        [Tooltip("Minimum world radius for tiny/one-particle ice blobs.")]
        public float minFrozenBlobRadius = 0.12f;
        [Tooltip("Simplify generated contours (higher = fewer vertices). Capped relative to grid cell size.")]
        [Range(0f, 0.5f)]
        public float contourSimplifyEpsilon = 0.06f;
        [Tooltip("Linear keeps spline edges on the contour (best collider match). Continuous smooths edges slightly.")]
        public ShapeTangentMode generatedSplineTangentMode = ShapeTangentMode.Linear;
        [Tooltip("Tangent length scale for Continuous mode (fraction of neighbor segment).")]
        [Range(0.05f, 0.45f)]
        public float continuousTangentScale = 0.22f;

        [Header("Freeze Topology Cleanup")]
        [Tooltip("Bridge empty gaps shorter than this world distance between nearby water stamps (morphological close).")]
        [Min(0f)]
        public float freezeBridgeGapWorld = 0.30f;
        [Tooltip("Fill enclosed empty holes up to this many cells (prevents hollow ice interiors, incl. small object footprints).")]
        [Min(0)]
        public int freezeMaxHoleCells = 48;
        [Tooltip("Majority-filter passes to smooth stair-step / notch noise on occupancy.")]
        [Range(0, 3)]
        public int freezeNotchSmoothPasses = 2;
        [Tooltip("Merge tiny satellite blobs into a nearby larger body if centroids are within this world distance.")]
        [Min(0f)]
        public float freezeMergeSatelliteDistance = 0.42f;
        [Tooltip("Clusters with cell count <= this are treated as satellites eligible for near-merge (not distant drop).")]
        [Min(1)]
        public int freezeSatelliteMaxCells = 14;
        [Tooltip("Laplacian contour smooth iterations after polygon extract (reduces spiky ice edges).")]
        [Range(0, 4)]
        public int freezeContourSmoothPasses = 2;

        [Header("Freeze Contour Safety")]
        [Tooltip("Max allowed ice AABB extent vs particle silhouette AABB (per axis). Excess triggers clamp/hull repair (prevents starburst bounds).")]
        [Range(1.1f, 4f)]
        public float maxIceBoundsInflation = 2.25f;
        [Tooltip("Max allowed ice polygon area vs particle silhouette AABB area. Excess triggers hull/simple repair.")]
        [Range(1.2f, 8f)]
        public float maxIceAreaInflation = 3.5f;
        [Tooltip("When true, Continuous tangents fall back to Linear on sharp/large contours that would balloon SpriteShape edges.")]
        public bool forceLinearTangentsOnSharpContours = true;
        [Tooltip("Log contour self-intersect / bounds-clamp repairs (useful for rapid toggle debugging).")]
        public bool logFreezeContourSafety = false;

        [Header("Generated SpriteShape Visual")]
        [Tooltip("Max spline height for generated ice (1 = same edge thickness as authored Ice ground).")]
        [Range(0.05f, 1f)]
        public float maxGeneratedSplineHeight = 1f;
        [Tooltip("Min spline height so tiny droplets keep a thin edge without flower-like overshoot.")]
        [Range(0.02f, 0.5f)]
        public float minGeneratedSplineHeight = 0.05f;
        [Tooltip("Target max (rendererSize - colliderSize) / colliderSize from edge sprites. Keeps ratio near 1.x.")]
        [Range(0.15f, 1f)]
        public float maxEdgeOvershootFraction = 0.35f;
        [Tooltip("Runtime profile fillOffset (0 enables UTess2D solid fill). Authored Ice ground uses 0.5 which hollows thin pools.")]
        [Range(0f, 0.5f)]
        public float generatedFillOffset = 0f;
        [Tooltip("Max edge thickness as a fraction of local contour min dimension (thin pools).")]
        [Range(0.05f, 0.4f)]
        public float maxEdgeThicknessVsMinDim = 0.14f;
        [Tooltip("Absolute max spline height for water-derived ice (crystal rim only; fill owns the interior).")]
        [Range(0.04f, 0.35f)]
        public float thinPoolMaxSplineHeight = 0.10f;

        [Header("Runtime Status")]
        [SerializeField] private Phase currentPhase = Phase.Ice;
        [SerializeField] private int trackedParticleCount;
        [SerializeField] private int generatedBlobCount;
        [SerializeField] private float lastMeltShapeArea;
        [SerializeField] private int lastMeltIdealParticleCount;
        [SerializeField] private int lastMeltBudget;
        [SerializeField] private int lastMeltSpawnedCount;
        [SerializeField] private float lastMeltEffectiveSpacing;
        [SerializeField] private bool lastMeltBudgetLimited;
        [SerializeField] private string lastMeltDiagnostic = string.Empty;

        private readonly List<int> trackedParticleIndices = new List<int>(512);
        private readonly List<int> trackedParticleStamps = new List<int>(512);
        private readonly List<Vector2> scratchPositions = new List<Vector2>(512);
        private readonly List<List<Vector2>> activeIcePolygonsWorld = new List<List<Vector2>>();
        private readonly List<GameObject> generatedIceBlobs = new List<GameObject>();
        private readonly List<Collider2D> managedColliders = new List<Collider2D>(8);
        private readonly List<Vector2> lastFreezeParticlePositions = new List<Vector2>(512);

        private Color iceTint = new Color(0.75f, 0.92f, 1f, 0.95f);
        #pragma warning disable CS0414
        private bool createdFallbackCollider;
        #pragma warning restore CS0414
        private bool usingGeneratedIce;
        private PhysicsMaterial2D cachedSharedMaterial;
        private float cachedAuthoredSplineHeight = 1f;
        private float cachedAuthoredContourMinSize = 4f;
        private float cachedEdgeSpriteWorldHeight = 2.5f;
        /// <summary>World-size edge overshoot observed on authored ice at height≈1 (renderer - spline).</summary>
        private float cachedAuthoredEdgeOvershoot = 2.2f;
        private float lastGeneratedSplineHeight = 1f;
        /// <summary>
        /// Runtime clone of the authored SpriteShape profile for thin water-derived ice.
        /// - useSpriteBorders disabled (authored ~1wu borders explode tiny closed contours)
        /// - fillOffset forced near 0 so UTess2D solid fill runs and edges do not hide the interior
        /// Source asset is never modified.
        /// </summary>
        private SpriteShape generatedSpriteShapeProfile;

        // Transition-only freeze diagnostics (no per-frame GPU readback).
        private int lastCapturedParticleCount;
        private int lastGeneratedContourCount;
        private int lastClusterCount;
        private int lastInputClusterCount;
        private int lastHolesFilled;
        private int lastBridgesApplied;
        private int lastSatellitesMerged;
        private float lastCoverage;
        private float lastIceArea;
        private float lastParticleIceIoU;
        private Bounds lastParticleBounds;
        private Bounds lastIceBounds;
        private bool lastUsedFallback;
        private bool lastUsedConvexHullFallback;
        private bool lastUsedBoxFallback;
        private bool lastUsedRibbonFallback;
        private string lastFallbackReason = string.Empty;
        private int lastSelfIntersectRepairs;
        private int lastBoundsClamps;
        private int lastHullRepairs;
        private int lastSimpleContourRepairs;
        private bool lastForcedLinearTangents;
        private string lastContourSafetyReason = string.Empty;
        private int lastDensifiedSampleCount;
        private int lastOutOfGridParticleCount;
        private float lastFreezeGridCellUsed;

        // Phase mass conservation: remelt spawns this exact count (not expanded-polygon grid density).
        // Established on first authored melt; updated on freeze to the actual captured live count
        // (may decrease if particles left simulation bounds by design).
        private int conservedPhaseMass;
        private bool hasConservedPhaseMass;
        private readonly List<int> polygonMassWeights = new List<int>(8);

        // Water-origin mass samples: exact freeze-time particle centers for remelt respawn.
        // Visual ice polygon may close gaps/fill holes (morphology), but that cleanup must NOT
        // become the next cycle's mass distribution domain — only these samples do.
        private readonly List<Vector2> massSamplePositions = new List<Vector2>(512);
        private bool hasWaterOriginMassSamples;

        // Last water-phase fragmentation snapshot (taken just before Freeze).
        private WaterFragmentationDiagnostics lastWaterFragmentation;

        public Phase CurrentPhase => currentPhase;
        public bool IsIce => currentPhase == Phase.Ice;
        public int TrackedParticleCount => trackedParticleIndices.Count;
        public int GeneratedBlobCount => generatedIceBlobs.Count;
        public bool UsingGeneratedIce => usingGeneratedIce;

        /// <summary>Particle count captured on the last Freeze transition (before kill).</summary>
        public int LastCapturedParticleCount => lastCapturedParticleCount;
        /// <summary>Conserved phase mass used for remelt distribution (exact particle budget).</summary>
        public int ConservedPhaseMass => conservedPhaseMass;
        /// <summary>True after first melt/freeze has established a mass budget.</summary>
        public bool HasConservedPhaseMass => hasConservedPhaseMass;
        /// <summary>True when remelt will respawn from freeze-time particle samples (water-origin ice).</summary>
        public bool HasWaterOriginMassSamples => hasWaterOriginMassSamples;
        /// <summary>Number of freeze-time particle position samples held for remelt.</summary>
        public int MassSampleCount => massSamplePositions.Count;
        /// <summary>Number of closed contours / SpriteShape blobs generated on last Freeze.</summary>
        public int LastGeneratedContourCount => lastGeneratedContourCount;
        /// <summary>Number of 8-connected occupancy clusters after topology cleanup on last Freeze.</summary>
        public int LastClusterCount => lastClusterCount;
        /// <summary>Raw stamped clusters before satellite merge (input topology).</summary>
        public int LastInputClusterCount => lastInputClusterCount;
        /// <summary>Enclosed holes filled during occupancy cleanup.</summary>
        public int LastHolesFilled => lastHolesFilled;
        /// <summary>Bridge dilate radius used (cells); >0 means bridging ran.</summary>
        public int LastBridgesApplied => lastBridgesApplied;
        /// <summary>Tiny satellites merged into nearby larger bodies.</summary>
        public int LastSatellitesMerged => lastSatellitesMerged;
        /// <summary>Fraction of last-freeze particle centers inside any generated ice polygon [0..1].</summary>
        public float LastCoverage => lastCoverage;
        /// <summary>Water fragmentation snapshot captured immediately before last Freeze.</summary>
        public WaterFragmentationDiagnostics LastWaterFragmentation => lastWaterFragmentation;
        /// <summary>Union area of generated ice polygons from last Freeze.</summary>
        public float LastIceArea => lastIceArea;
        /// <summary>Axis-aligned bounds IoU between particle stamp bounds and ice bounds.</summary>
        public float LastParticleIceIoU => lastParticleIceIoU;
        public Bounds LastParticleBounds => lastParticleBounds;
        public Bounds LastIceBounds => lastIceBounds;
        public bool LastUsedFallback => lastUsedFallback;
        public bool LastUsedConvexHullFallback => lastUsedConvexHullFallback;
        /// <summary>True when last freeze used an oriented ribbon/strip contour for elongated water.</summary>
        public bool LastUsedRibbonFallback => lastUsedRibbonFallback;
        /// <summary>Particle samples after sparse-ribbon densify (may exceed captured live count).</summary>
        public int LastDensifiedSampleCount => lastDensifiedSampleCount;
        /// <summary>Particles that fell outside the occupancy grid (should be 0 after adaptive cell).</summary>
        public int LastOutOfGridParticleCount => lastOutOfGridParticleCount;
        public bool LastUsedBoxFallback => lastUsedBoxFallback;
        public string LastFallbackReason => lastFallbackReason;
        /// <summary>Contours repaired due to self-intersection on last Freeze.</summary>
        public int LastSelfIntersectRepairs => lastSelfIntersectRepairs;
        /// <summary>Contours clamped because ice AABB exceeded particle silhouette inflation budget.</summary>
        public int LastBoundsClamps => lastBoundsClamps;
        /// <summary>Contours replaced with convex hull of cluster samples.</summary>
        public int LastHullRepairs => lastHullRepairs;
        /// <summary>Contours rebuilt via angular-ordered simple ring around centroid.</summary>
        public int LastSimpleContourRepairs => lastSimpleContourRepairs;
        /// <summary>True when Continuous tangents were forced to Linear for safety on last blob build.</summary>
        public bool LastForcedLinearTangents => lastForcedLinearTangents;
        /// <summary>Human-readable summary of last contour safety actions.</summary>
        public string LastContourSafetyReason => lastContourSafetyReason;

        /// <summary>World polygon area used for the last authored melt budget estimate.</summary>
        public float LastMeltShapeArea => lastMeltShapeArea;
        /// <summary>Particles needed to fill the last melt shape at preferred density.</summary>
        public int LastMeltIdealParticleCount => lastMeltIdealParticleCount;
        /// <summary>Particle budget actually used for the last melt (after hard caps).</summary>
        public int LastMeltBudget => lastMeltBudget;
        /// <summary>Particles successfully emitted on the last melt.</summary>
        public int LastMeltSpawnedCount => lastMeltSpawnedCount;
        /// <summary>Effective sample spacing used for the last melt (may be wider than particleSpacing).</summary>
        public float LastMeltEffectiveSpacing => lastMeltEffectiveSpacing;
        /// <summary>True when preferred density exceeded maxParticlesPerToggle / system capacity.</summary>
        public bool LastMeltBudgetLimited => lastMeltBudgetLimited;
        /// <summary>Human-readable melt coverage diagnostic from the last authored melt.</summary>
        public string LastMeltDiagnostic => lastMeltDiagnostic;

        private void Awake()
        {
            ResolveReferences();
            EnsureIceCollider();
            CaptureIceVisualProfile();
            // Keep edge budget conservative even if older serialized values were looser.
            maxEdgeOvershootFraction = Mathf.Clamp(maxEdgeOvershootFraction, 0.15f, 0.5f);
            minGeneratedSplineHeight = Mathf.Clamp(minGeneratedSplineHeight, 0.02f, 0.2f);
            RebuildSourcePolygonsFromAuthoring();
            ApplyPhase(startAsIce ? Phase.Ice : Phase.Water, force: true);
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (!enableKeyboardToggle) return;
            if (Input.GetKeyDown(toggleKey))
                Toggle();
        }

        private void OnDisable()
        {
            // Avoid leaving orphaned tracked particles if the component is disabled while water.
            if (Application.isPlaying && currentPhase == Phase.Water)
                ClearTrackedParticles(syncGpu: true);
        }

        private void OnDestroy()
        {
            if (Application.isPlaying && currentPhase == Phase.Water)
                ClearTrackedParticles(syncGpu: true);

            DestroyGeneratedIce(refreshObstacles: false);
            ReleaseGeneratedSpriteShapeProfile();
        }

        public void Toggle()
        {
            SetPhase(currentPhase == Phase.Ice ? Phase.Water : Phase.Ice);
        }

        public void SetPhase(Phase phase)
        {
            ApplyPhase(phase, force: false);
        }

        public void Melt() => SetPhase(Phase.Water);
        public void Freeze() => SetPhase(Phase.Ice);

        public bool DiscardWater()
        {
            if (currentPhase != Phase.Water)
                return false;

            ClearTrackedParticles(syncGpu: false);
            SetAuthoringIceVisible(false);
            SetManagedCollidersEnabled(false);
            ExcludeManagedCollidersFromObstacles();
            RefreshFluidObstacles();
            return true;
        }
        /// <summary>
        /// Assign ice SpriteShape profile for authoring and next freeze generation.
        /// Does not mutate the source asset; generated freeze profiles remain runtime clones.
        /// </summary>
        public void SetIceSpriteShapeProfile(SpriteShape profile)
        {
            if (profile == null)
                return;

            ResolveReferences();
            if (spriteShapeController == null)
                spriteShapeController = GetComponent<SpriteShapeController>();
            if (spriteShapeController == null)
            {
                Debug.LogWarning("IceWaterPhaseToggle2D: No SpriteShapeController for profile assign.", this);
                return;
            }

            spriteShapeController.spriteShape = profile;
            ReleaseGeneratedSpriteShapeProfile();
            CaptureIceVisualProfile();
        }

        /// <summary>Current authored SpriteShape profile reference (shared asset, not runtime clone).</summary>
        public SpriteShape GetIceSpriteShapeProfile()
        {
            ResolveReferences();
            return spriteShapeController != null ? spriteShapeController.spriteShape : null;
        }

        /// <summary>
        /// Snapshot of last Freeze transition diagnostics for automated tests (no reflection needed).
        /// </summary>
        public void GetLastFreezeDiagnostics(
            out int capturedParticles,
            out int contours,
            out int clusters,
            out float coverage,
            out float iceArea,
            out float iou,
            out Bounds particleBounds,
            out Bounds iceBounds,
            out bool usedFallback,
            out string fallbackReason)
        {
            capturedParticles = lastCapturedParticleCount;
            contours = lastGeneratedContourCount;
            clusters = lastClusterCount;
            coverage = lastCoverage;
            iceArea = lastIceArea;
            iou = lastParticleIceIoU;
            particleBounds = lastParticleBounds;
            iceBounds = lastIceBounds;
            usedFallback = lastUsedFallback;
            fallbackReason = lastFallbackReason ?? string.Empty;
        }

        /// <summary>
        /// Extended freeze topology diagnostics for automated tests.
        /// </summary>
        public void GetLastFreezeTopologyDiagnostics(
            out int inputClusters,
            out int outputBlobs,
            out int holesFilled,
            out int bridgesApplied,
            out int satellitesMerged,
            out float coverage,
            out float iou)
        {
            inputClusters = lastInputClusterCount;
            outputBlobs = lastGeneratedContourCount;
            holesFilled = lastHolesFilled;
            bridgesApplied = lastBridgesApplied;
            satellitesMerged = lastSatellitesMerged;
            coverage = lastCoverage;
            iou = lastParticleIceIoU;
        }

        /// <summary>
        /// Contour safety diagnostics for automated tests (self-intersect / bounds / hull repairs).
        /// </summary>
        public void GetLastFreezeContourSafetyDiagnostics(
            out int selfIntersectRepairs,
            out int boundsClamps,
            out int hullRepairs,
            out int simpleContourRepairs,
            out bool forcedLinearTangents,
            out string safetyReason)
        {
            selfIntersectRepairs = lastSelfIntersectRepairs;
            boundsClamps = lastBoundsClamps;
            hullRepairs = lastHullRepairs;
            simpleContourRepairs = lastSimpleContourRepairs;
            forcedLinearTangents = lastForcedLinearTangents;
            safetyReason = lastContourSafetyReason ?? string.Empty;
        }

        /// <summary>
        /// Geometry unit-test helper: true when a closed ring has proper edge crossings (starburst risk).
        /// </summary>
        public static bool ContourHasSelfIntersection(IList<Vector2> poly)
        {
            return PolygonSelfIntersects(poly);
        }

        /// <summary>
        /// Geometry unit-test helper: convex hull of sample points (CCW when possible).
        /// </summary>
        public static List<Vector2> BuildConvexHullForTest(IList<Vector2> points)
        {
            if (points == null || points.Count == 0) return null;
            var list = new List<Vector2>(points.Count);
            for (int i = 0; i < points.Count; i++)
                list.Add(points[i]);
            List<Vector2> hull = ComputeConvexHull(list);
            if (hull != null && hull.Count >= 3 && SignedArea(hull) < 0f)
                hull.Reverse();
            return hull;
        }

        /// <summary>
        /// Geometry unit-test helper: angular-ordered simple contour around centroid (non-self-intersecting star collapse).
        /// </summary>
        public static List<Vector2> BuildAngularSimpleContourForTest(IList<Vector2> points, float minRadius = 0.08f)
        {
            if (points == null || points.Count == 0) return null;
            var list = new List<Vector2>(points.Count);
            for (int i = 0; i < points.Count; i++)
                list.Add(points[i]);
            return BuildAngularOrderedContour(list, minRadius);
        }

        /// <summary>
        /// Geometry unit-test helper: oriented ribbon/strip contour that preserves length of elongated point sets.
        /// </summary>
        public static List<Vector2> BuildOrientedRibbonContourForTest(
            IList<Vector2> points, float halfWidth, float endPad = 0f)
        {
            if (points == null || points.Count == 0) return null;
            var list = new List<Vector2>(points.Count);
            for (int i = 0; i < points.Count; i++)
                list.Add(points[i]);
            return BuildOrientedRibbonContour(list, halfWidth, endPad);
        }

        /// <summary>True when point-set AABB aspect (max/min extent) meets elongated ribbon threshold.</summary>
        public static bool IsElongatedPointSetForTest(IList<Vector2> points, float threshold = ElongatedAspectThreshold)
        {
            if (points == null || points.Count < 2) return false;
            var list = new List<Vector2>(points.Count);
            for (int i = 0; i < points.Count; i++)
                list.Add(points[i]);
            return IsElongatedPointSet(list, threshold);
        }

        /// <summary>
        /// Capture tracked-water fragmentation now (syncs GPU). Safe in play mode for tests.
        /// </summary>
        public WaterFragmentationDiagnostics CaptureWaterFragmentationDiagnostics(bool syncFromGpu = true)
        {
            lastWaterFragmentation = default;
            if (fluidSystem == null || trackedParticleIndices.Count == 0)
                return lastWaterFragmentation;

            lastWaterFragmentation = fluidSystem.ComputeTrackedFragmentationDiagnostics(
                trackedParticleIndices, trackedParticleStamps, -1f, syncFromGpu);
            return lastWaterFragmentation;
        }

        /// <summary>
        /// Diagnostic/test entry: build generated ice from explicit world positions (e.g. disconnected clusters).
        /// Does not modify fluid particles. Returns true if at least one blob was created.
        /// </summary>
        public bool BuildIceFromWorldPositionsForTest(IList<Vector2> worldPositions)
        {
            if (worldPositions == null || worldPositions.Count == 0)
                return false;

            ResolveReferences();
            EnsureIceCollider();

            scratchPositions.Clear();
            for (int i = 0; i < worldPositions.Count; i++)
                scratchPositions.Add(worldPositions[i]);

            lastFreezeParticlePositions.Clear();
            lastFreezeParticlePositions.AddRange(scratchPositions);
            lastCapturedParticleCount = scratchPositions.Count;
            StoreWaterOriginMassSamples(scratchPositions);
            conservedPhaseMass = lastCapturedParticleCount;
            hasConservedPhaseMass = conservedPhaseMass > 0;

            DestroyGeneratedIce(refreshObstacles: false);
            activeIcePolygonsWorld.Clear();

            bool built = TryBuildIceFromParticlePositions(scratchPositions);
            if (built)
            {
                usingGeneratedIce = true;
                currentPhase = Phase.Ice;
                SetAuthoringIceVisible(false);
                if (iceCollider != null)
                    iceCollider.enabled = false;
            }

            generatedBlobCount = generatedIceBlobs.Count;
            trackedParticleCount = trackedParticleIndices.Count;
            RefreshFluidObstacles();
            return built;
        }

        private void ApplyPhase(Phase phase, bool force)
        {
            if (!force && phase == currentPhase)
                return;

            Phase previous = currentPhase;
            RaisePhaseChanging(previous, phase);

            ResolveReferences();
            EnsureIceCollider();

            if (phase == Phase.Water)
                EnterWater();
            else
                EnterIce();

            currentPhase = phase;
            trackedParticleCount = trackedParticleIndices.Count;
            generatedBlobCount = generatedIceBlobs.Count;

            RaisePhaseChanged(previous, phase);
        }

        private void RaisePhaseChanging(Phase previous, Phase next)
        {
            try
            {
                PhaseChanging?.Invoke(previous, next);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex, this);
            }

            if (onPhaseChanging == null)
                onPhaseChanging = new UnityEvent();
            onPhaseChanging.Invoke();
        }

        private void RaisePhaseChanged(Phase previous, Phase next)
        {
            try
            {
                PhaseChanged?.Invoke(previous, next);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex, this);
            }

            if (onPhaseChanged == null)
                onPhaseChanged = new UnityEvent();
            onPhaseChanged.Invoke();

            if (next == Phase.Water)
            {
                try { EnteredWater?.Invoke(); }
                catch (System.Exception ex) { Debug.LogException(ex, this); }
                if (onEnteredWater == null)
                    onEnteredWater = new UnityEvent();
                onEnteredWater.Invoke();
            }
            else
            {
                try { EnteredIce?.Invoke(); }
                catch (System.Exception ex) { Debug.LogException(ex, this); }
                if (onEnteredIce == null)
                    onEnteredIce = new UnityEvent();
                onEnteredIce.Invoke();
            }
        }

        private void EnterWater()
        {
            // 1) Remove solid ice collision BEFORE spawn so water is free under gravity.
            //    Polygons were captured on the previous freeze (or authoring rebuild) into
            //    activeIcePolygonsWorld, so generated blob objects can be stripped first.
            SetAuthoringIceVisible(false);
            SetManagedCollidersEnabled(false);
            ExcludeManagedCollidersFromObstacles();
            DestroyGeneratedIce(refreshObstacles: false);
            RefreshFluidObstacles();

            // 2) Spawn only inside current ice silhouette (generated blobs or original spline).
            ClearTrackedParticles(syncGpu: false);
            SpawnWaterInCurrentIceShape();
        }

        private void EnterIce()
        {
            // 0) Water fragmentation diagnostics while particles still live (single GPU sync shared).
            if (fluidSystem != null && trackedParticleIndices.Count > 0)
            {
                lastWaterFragmentation = fluidSystem.ComputeTrackedFragmentationDiagnostics(
                    trackedParticleIndices, trackedParticleStamps, -1f, syncFromGpu: true);
            }
            else
            {
                lastWaterFragmentation = default;
            }

            // 1) Capture live GPU positions for this patch only (stamp-validated, not recycled slots).
            //    Skip second GPU sync — cache already filled by fragmentation diagnostics above.
            scratchPositions.Clear();
            if (fluidSystem != null && trackedParticleIndices.Count > 0)
            {
                fluidSystem.GetActiveTrackedPositions(
                    trackedParticleIndices, scratchPositions, syncFromGpu: false, trackedParticleStamps);
            }

            lastFreezeParticlePositions.Clear();
            lastFreezeParticlePositions.AddRange(scratchPositions);
            lastCapturedParticleCount = scratchPositions.Count;

            // 2) Build ice contours from the water silhouette (may be multiple blobs).
            DestroyGeneratedIce(refreshObstacles: false);
            activeIcePolygonsWorld.Clear();

            bool builtFromWater = scratchPositions.Count > 0 &&
                                  TryBuildIceFromParticlePositions(scratchPositions);

            // 3) Remove only this patch's particles after the silhouette is captured.
            ClearTrackedParticles(syncGpu: false);

            if (builtFromWater)
            {
                usingGeneratedIce = true;
                SetAuthoringIceVisible(false);
                if (iceCollider != null)
                    iceCollider.enabled = false;

                // Mass budget = exact particles captured this freeze (not expanded ice area).
                // Morphological closing/fill may grow the visual silhouette for coverage, but
                // remelt must respawn from these captured centers — never redistribute into
                // the expanded visual polygon (that causes monotonic silhouette inflation).
                CapturePolygonMassWeights(lastFreezeParticlePositions);
                StoreWaterOriginMassSamples(lastFreezeParticlePositions);
                conservedPhaseMass = lastCapturedParticleCount;
                hasConservedPhaseMass = conservedPhaseMass > 0;
            }
            else
            {
                // No water left — restore authored ice shape and clear mass budget.
                usingGeneratedIce = false;
                RebuildSourcePolygonsFromAuthoring();
                SetAuthoringIceVisible(true);
                if (iceCollider != null)
                    iceCollider.enabled = true;
                IncludeManagedCollidersInObstacles();
                ResetFreezeDiagnostics(partial: true);
                ClearConservedPhaseMass();
            }

            RefreshFluidObstacles();
        }

        private void SpawnWaterInCurrentIceShape()
        {
            if (fluidSystem == null)
            {
                Debug.LogWarning("IceWaterPhaseToggle2D: Missing Fluid2DSystem reference.", this);
                return;
            }

            if (activeIcePolygonsWorld.Count == 0)
                RebuildSourcePolygonsFromAuthoring();

            // Water-origin ice: respawn from freeze-time particle samples (positions matter;
            // velocities reset safely). Do NOT fill the morphology-expanded visual polygon.
            if (hasWaterOriginMassSamples && massSamplePositions.Count > 0)
            {
                SpawnFromWaterOriginMassSamples();
                return;
            }

            if (activeIcePolygonsWorld.Count == 0)
            {
                // Absolute fallback: padded AABB (authored ice with no polygon extract).
                if (hasConservedPhaseMass && conservedPhaseMass > 0)
                    SpawnConservedMassInBounds(GetRegionBounds(), conservedPhaseMass);
                else
                {
                    SpawnInBoundsFallback(GetRegionBounds());
                    EstablishConservedMassFromTracked();
                }
                return;
            }

            // Authored initial ice only: no captured samples yet — area-budget silhouette fill once.
            // Subsequent freezes store samples so remelt never re-enters this path for water-origin ice.
            if (hasConservedPhaseMass && conservedPhaseMass > 0 && !hasWaterOriginMassSamples)
            {
                // Rare: count known but samples lost — fall back to bounded exact-count fill
                // still proportional to freeze weights if any, else polygon area.
                SpawnConservedMassAcrossPolygons(conservedPhaseMass);
                return;
            }

            SpawnGridFillAcrossPolygons();
            EstablishConservedMassFromTracked();
        }

        private void StoreWaterOriginMassSamples(List<Vector2> positions)
        {
            massSamplePositions.Clear();
            if (positions != null)
            {
                for (int i = 0; i < positions.Count; i++)
                    massSamplePositions.Add(positions[i]);
            }
            hasWaterOriginMassSamples = massSamplePositions.Count > 0;
        }

        /// <summary>
        /// Remelt path for water-origin ice: emit one particle at each freeze-time sample.
        /// Preserves dynamic silhouette intent; velocities reset to <see cref="initialVelocity"/>.
        /// </summary>
        private void SpawnFromWaterOriginMassSamples()
        {
            int sampleCount = massSamplePositions.Count;
            if (sampleCount <= 0) return;

            int budget = hasConservedPhaseMass && conservedPhaseMass > 0
                ? Mathf.Min(conservedPhaseMass, sampleCount)
                : sampleCount;
            budget = Mathf.Clamp(budget, 0, maxParticlesPerToggle);
            if (budget <= 0) return;

            // Exact positions in capture order (stable mass representation).
            for (int i = 0; i < budget; i++)
            {
                if (!TryEmitTracked(massSamplePositions[i], initialVelocity))
                    break;
            }

            // Emit failures only: recover remaining budget near existing samples (not expanded poly fill).
            int shortfall = budget - trackedParticleIndices.Count;
            if (shortfall <= 0) return;

            for (int i = 0; i < sampleCount && shortfall > 0; i++)
            {
                Vector2 p = massSamplePositions[i] + Random.insideUnitCircle * 0.02f;
                if (TryEmitTracked(p, initialVelocity))
                    shortfall--;
            }
        }

        private void EstablishConservedMassFromTracked()
        {
            conservedPhaseMass = trackedParticleIndices.Count;
            hasConservedPhaseMass = conservedPhaseMass > 0;
            // First authored melt: no water-origin samples yet (grid was the spawn domain).
            hasWaterOriginMassSamples = false;
            massSamplePositions.Clear();
            // Weights = relative polygon area (occupancy unknown until freeze).
            polygonMassWeights.Clear();
            for (int i = 0; i < activeIcePolygonsWorld.Count; i++)
            {
                List<Vector2> poly = activeIcePolygonsWorld[i];
                float a = poly != null && poly.Count >= 3 ? Mathf.Abs(SignedArea(poly)) : 0f;
                polygonMassWeights.Add(Mathf.Max(1, Mathf.RoundToInt(a * 100f)));
            }
        }

        private void ClearConservedPhaseMass()
        {
            conservedPhaseMass = 0;
            hasConservedPhaseMass = false;
            polygonMassWeights.Clear();
            massSamplePositions.Clear();
            hasWaterOriginMassSamples = false;
        }

        /// <summary>
        /// Count freeze-time particles inside each active ice polygon for proportional remelt.
        /// Particles outside all polys (rare after EnsureParticlesInside) join the nearest poly.
        /// </summary>
        private void CapturePolygonMassWeights(List<Vector2> positions)
        {
            polygonMassWeights.Clear();
            int polyCount = activeIcePolygonsWorld.Count;
            if (polyCount == 0 || positions == null || positions.Count == 0)
                return;

            for (int i = 0; i < polyCount; i++)
                polygonMassWeights.Add(0);

            for (int i = 0; i < positions.Count; i++)
            {
                Vector2 p = positions[i];
                int best = -1;
                float bestDist = float.MaxValue;
                for (int pi = 0; pi < polyCount; pi++)
                {
                    List<Vector2> poly = activeIcePolygonsWorld[pi];
                    if (poly == null || poly.Count < 3) continue;
                    if (PointInPolygon(p, poly))
                    {
                        best = pi;
                        bestDist = -1f;
                        break;
                    }
                    Vector2 c = PolygonCentroid(poly);
                    float d = (c - p).sqrMagnitude;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = pi;
                    }
                }
                if (best >= 0)
                    polygonMassWeights[best]++;
            }

            // Ensure every non-empty polygon gets at least weight 1 if total mass > 0 and it has area.
            int weightSum = 0;
            for (int i = 0; i < polygonMassWeights.Count; i++)
                weightSum += polygonMassWeights[i];
            if (weightSum <= 0)
            {
                for (int i = 0; i < polyCount; i++)
                {
                    List<Vector2> poly = activeIcePolygonsWorld[i];
                    float a = poly != null && poly.Count >= 3 ? Mathf.Abs(SignedArea(poly)) : 0f;
                    polygonMassWeights[i] = Mathf.Max(1, Mathf.RoundToInt(a * 100f));
                }
            }
        }

        private void SpawnConservedMassAcrossPolygons(int totalMass)
        {
            int budget = Mathf.Clamp(totalMass, 0, maxParticlesPerToggle);
            if (budget <= 0) return;

            int polyCount = activeIcePolygonsWorld.Count;
            var weights = new int[polyCount];
            int weightSum = 0;
            for (int i = 0; i < polyCount; i++)
            {
                int w = (i < polygonMassWeights.Count) ? polygonMassWeights[i] : 0;
                if (w <= 0)
                {
                    List<Vector2> poly = activeIcePolygonsWorld[i];
                    float a = poly != null && poly.Count >= 3 ? Mathf.Abs(SignedArea(poly)) : 0f;
                    w = Mathf.Max(0, Mathf.RoundToInt(a * 100f));
                }
                // Skip degenerate polygons.
                if (activeIcePolygonsWorld[i] == null || activeIcePolygonsWorld[i].Count < 3)
                    w = 0;
                weights[i] = w;
                weightSum += w;
            }

            if (weightSum <= 0)
            {
                // Equal split among valid polys, or single-centroid fallback.
                int valid = 0;
                for (int i = 0; i < polyCount; i++)
                {
                    if (activeIcePolygonsWorld[i] != null && activeIcePolygonsWorld[i].Count >= 3)
                        valid++;
                }
                if (valid == 0)
                {
                    SpawnConservedMassInBounds(GetRegionBounds(), budget);
                    return;
                }
                for (int i = 0; i < polyCount; i++)
                {
                    if (activeIcePolygonsWorld[i] != null && activeIcePolygonsWorld[i].Count >= 3)
                        weights[i] = 1;
                }
                weightSum = valid;
            }

            // Largest-remainder allocation so sum(counts) == budget exactly.
            var counts = new int[polyCount];
            var remainders = new float[polyCount];
            int assigned = 0;
            for (int i = 0; i < polyCount; i++)
            {
                if (weights[i] <= 0) continue;
                float exact = budget * (weights[i] / (float)weightSum);
                counts[i] = Mathf.FloorToInt(exact);
                remainders[i] = exact - counts[i];
                assigned += counts[i];
            }
            int leftover = budget - assigned;
            // leftover is always < number of positive-weight polys for classic largest-remainder.
            while (leftover > 0)
            {
                int best = -1;
                float bestR = -1f;
                for (int i = 0; i < polyCount; i++)
                {
                    if (weights[i] <= 0) continue;
                    if (remainders[i] > bestR)
                    {
                        bestR = remainders[i];
                        best = i;
                    }
                }
                if (best < 0) break;
                counts[best]++;
                remainders[best] = -1f; // consume this remainder slot
                leftover--;
            }

            int spawned = 0;
            for (int i = 0; i < polyCount && spawned < budget; i++)
            {
                if (counts[i] <= 0) continue;
                spawned += SpawnExactCountInPolygon(activeIcePolygonsWorld[i], counts[i], budget - spawned);
            }

            // Safety: if some slots failed (emit full), try remaining budget in largest poly.
            int shortfall = budget - trackedParticleIndices.Count;
            if (shortfall > 0)
            {
                int best = 0;
                float bestA = -1f;
                for (int i = 0; i < polyCount; i++)
                {
                    List<Vector2> poly = activeIcePolygonsWorld[i];
                    if (poly == null || poly.Count < 3) continue;
                    float a = Mathf.Abs(SignedArea(poly));
                    if (a > bestA) { bestA = a; best = i; }
                }
                if (bestA > 0f)
                    SpawnExactCountInPolygon(activeIcePolygonsWorld[best], shortfall, shortfall);
            }
        }

        /// <summary>
        /// Place exactly <paramref name="count"/> particles inside a polygon via stratified
        /// candidate grid + random fill (not area-density grid that scales with expanded ice).
        /// Spacing adapts to polygon area so a limited budget still covers the silhouette.
        /// </summary>
        private int SpawnExactCountInPolygon(List<Vector2> poly, int count, int hardCap)
        {
            if (poly == null || poly.Count < 3 || count <= 0 || hardCap <= 0)
                return 0;

            int target = Mathf.Min(count, hardCap);
            int before = trackedParticleIndices.Count;

            Bounds b = BoundsOfPolygon(poly);
            float pad = Mathf.Max(0f, spawnPadding * 0.5f);
            float minX = b.min.x + pad;
            float maxX = b.max.x - pad;
            float minY = b.min.y + pad;
            float maxY = b.max.y - pad;

            if (maxX <= minX || maxY <= minY)
            {
                Vector2 c = PolygonCentroid(poly);
                for (int i = 0; i < target; i++)
                {
                    Vector2 p = c;
                    if (jitter > 0f)
                        p += Random.insideUnitCircle * jitter * 0.5f;
                    if (!PointInPolygon(p, poly))
                        p = c;
                    if (TryEmitTracked(p, initialVelocity))
                    { }
                    else
                        break;
                }
                return trackedParticleIndices.Count - before;
            }

            // Candidate spacing adapts to area/target so large polygons stay fully sampled.
            // Aim for ~2.5x candidates vs target, then shuffle + take first N for even coverage.
            float area = Mathf.Max(1e-4f, (maxX - minX) * (maxY - minY));
            float spacing = Mathf.Sqrt(area / Mathf.Max(1f, target * 2.5f));
            float preferred = Mathf.Max(0.05f, particleSpacing);
            float maxSpacing = AdaptSpacingToShape
                ? Mathf.Max(preferred * 8f, Mathf.Sqrt(area / Mathf.Max(1f, target)), preferred)
                : Mathf.Max(0.05f, preferred * 2f);
            // Also cap by the larger axis so pathological skinny shapes do not explode spacing.
            float axisCap = Mathf.Max(0.05f, Mathf.Max(maxX - minX, maxY - minY));
            spacing = Mathf.Clamp(spacing, 0.04f, Mathf.Min(maxSpacing, axisCap));

            int candidateCap = Mathf.Clamp(target * 4, 64, 8192);
            var candidates = new List<Vector2>(Mathf.Min(candidateCap, target * 3 + 16));
            for (float y = minY; y <= maxY + 1e-4f && candidates.Count < candidateCap; y += spacing)
            {
                for (float x = minX; x <= maxX + 1e-4f && candidates.Count < candidateCap; x += spacing)
                {
                    Vector2 pos = new Vector2(x, y);
                    // Deterministic micro-jitter from grid index keeps distribution stable per melt.
                    float jx = (Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f);
                    jx = (jx - Mathf.Floor(jx)) * 2f - 1f;
                    float jy = (Mathf.Sin(x * 39.346f + y * 11.135f) * 24634.6345f);
                    jy = (jy - Mathf.Floor(jy)) * 2f - 1f;
                    float jAmt = Mathf.Min(jitter, spacing * 0.35f);
                    pos.x += jx * jAmt;
                    pos.y += jy * jAmt;
                    if (PointInPolygon(pos, poly))
                        candidates.Add(pos);
                }
            }

            // Shuffle lightly (Fisher-Yates) for even coverage when taking a subset.
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            int need = target;
            for (int i = 0; i < candidates.Count && need > 0; i++)
            {
                if (TryEmitTracked(candidates[i], initialVelocity))
                    need--;
            }

            // Rejection sampling for remainder (thin polygons / emit failures).
            int guard = target * 40;
            while (need > 0 && guard-- > 0)
            {
                Vector2 pos = new Vector2(
                    Random.Range(minX, maxX),
                    Random.Range(minY, maxY));
                if (!PointInPolygon(pos, poly))
                    continue;
                if (TryEmitTracked(pos, initialVelocity))
                    need--;
            }

            // Last resort: stack at centroid so mass is not lost.
            Vector2 centroid = PolygonCentroid(poly);
            while (need > 0)
            {
                Vector2 p = centroid + Random.insideUnitCircle * 0.02f;
                if (!TryEmitTracked(p, initialVelocity))
                    break;
                need--;
            }

            return trackedParticleIndices.Count - before;
        }

        private void SpawnConservedMassInBounds(Bounds bounds, int totalMass)
        {
            int budget = Mathf.Clamp(totalMass, 0, maxParticlesPerToggle);
            if (budget <= 0) return;

            float pad = Mathf.Max(0f, spawnPadding);
            float minX = bounds.min.x + pad;
            float maxX = bounds.max.x - pad;
            float minY = bounds.min.y + pad;
            float maxY = bounds.max.y - pad;
            if (maxX <= minX || maxY <= minY)
            {
                for (int i = 0; i < budget; i++)
                {
                    if (!TryEmitTracked(bounds.center, initialVelocity))
                        break;
                }
                return;
            }

            // Stratified samples across the full bounds — never walk a dense grid that stops
            // after budget particles in one corner.
            float area = Mathf.Max(1e-4f, (maxX - minX) * (maxY - minY));
            float spacing = Mathf.Sqrt(area / Mathf.Max(1f, budget * 2.5f));
            float preferred = Mathf.Max(0.05f, particleSpacing);
            float maxSpacing = AdaptSpacingToShape
                ? Mathf.Max(preferred * 8f, Mathf.Sqrt(area / Mathf.Max(1f, budget)), preferred)
                : Mathf.Max(0.05f, preferred * 2f);
            float axisCap = Mathf.Max(0.05f, Mathf.Max(maxX - minX, maxY - minY));
            spacing = Mathf.Clamp(spacing, 0.04f, Mathf.Min(maxSpacing, axisCap));

            int candidateCap = Mathf.Clamp(budget * 4, 64, 8192);
            var candidates = new List<Vector2>(Mathf.Min(candidateCap, budget * 3 + 16));
            for (float y = minY; y <= maxY + 1e-4f && candidates.Count < candidateCap; y += spacing)
            {
                for (float x = minX; x <= maxX + 1e-4f && candidates.Count < candidateCap; x += spacing)
                {
                    Vector2 pos = new Vector2(x, y);
                    if (jitter > 0f)
                        pos += Random.insideUnitCircle * Mathf.Min(jitter, spacing * 0.35f);
                    pos.x = Mathf.Clamp(pos.x, minX, maxX);
                    pos.y = Mathf.Clamp(pos.y, minY, maxY);
                    candidates.Add(pos);
                }
            }

            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            int spawned = 0;
            for (int i = 0; i < candidates.Count && spawned < budget; i++)
            {
                if (TryEmitTracked(candidates[i], initialVelocity))
                    spawned++;
            }
            while (spawned < budget)
            {
                Vector2 pos = new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));
                if (!TryEmitTracked(pos, initialVelocity))
                    break;
                spawned++;
            }
        }

        /// <summary>
        /// First-melt path for authored ice: budget particles by world polygon area and spread
        /// them across the full silhouette (adaptive spacing when the hard cap is hit).
        /// </summary>
        private void SpawnGridFillAcrossPolygons()
        {
            float totalArea = ComputeActivePolygonArea();
            if (totalArea <= 1e-6f)
            {
                // Degenerate authored polygons — fall back to region bounds.
                SpawnInBoundsFallback(GetRegionBounds());
                return;
            }

            int budget = ResolveAuthoringMeltBudget(totalArea, out int ideal, out float effectiveSpacing, out bool limited);
            RecordMeltPlan(totalArea, ideal, budget, effectiveSpacing, limited, "authored-polygon");
            if (budget <= 0) return;

            // Seed area weights so proportional allocation matches silhouette mass.
            polygonMassWeights.Clear();
            for (int i = 0; i < activeIcePolygonsWorld.Count; i++)
            {
                List<Vector2> poly = activeIcePolygonsWorld[i];
                float a = poly != null && poly.Count >= 3 ? Mathf.Abs(SignedArea(poly)) : 0f;
                polygonMassWeights.Add(Mathf.Max(0, Mathf.RoundToInt(a * 1000f)));
            }

            SpawnConservedMassAcrossPolygons(budget);
            lastMeltSpawnedCount = trackedParticleIndices.Count;
            // Always warn when budget-limited so designers see large-shape guidance even if
            // logMeltDiagnostics was not yet enabled on older serialized components.
            if (limited)
            {
                Debug.LogWarning(
                    $"IceWaterPhaseToggle2D [{name}]: melt budget-limited. " +
                    $"shapeArea≈{totalArea:F1} wu² needs ~{ideal} particles at preferred density " +
                    $"(spacing={particleSpacing:F3}, density={ResolvePreferredDensity():F2}/wu²), " +
                    $"but budget={budget} (maxParticlesPerToggle={maxParticlesPerToggle}, " +
                    $"systemCap={ResolveSystemParticleCap()}). " +
                    $"Using effectiveSpacing≈{effectiveSpacing:F3} to preserve silhouette coverage. " +
                    $"Raise maxParticlesPerToggle and/or Fluid2DSettings.maxParticles for denser water.",
                    this);
            }
            else if (logMeltDiagnostics)
            {
                Debug.Log(
                    $"IceWaterPhaseToggle2D [{name}]: melt spawn shapeArea≈{totalArea:F1} wu², " +
                    $"ideal={ideal}, budget={budget}, spawned={lastMeltSpawnedCount}, " +
                    $"effectiveSpacing≈{effectiveSpacing:F3}.",
                    this);
            }
        }

        private void SpawnInBoundsFallback(Bounds bounds)
        {
            float pad = Mathf.Max(0f, spawnPadding);
            float minX = bounds.min.x + pad;
            float maxX = bounds.max.x - pad;
            float minY = bounds.min.y + pad;
            float maxY = bounds.max.y - pad;

            if (maxX <= minX || maxY <= minY)
            {
                TryEmitTracked(bounds.center, initialVelocity);
                lastMeltSpawnedCount = trackedParticleIndices.Count;
                return;
            }

            float area = Mathf.Max(1e-4f, (maxX - minX) * (maxY - minY));
            int budget = ResolveAuthoringMeltBudget(area, out int ideal, out float effectiveSpacing, out bool limited);
            RecordMeltPlan(area, ideal, budget, effectiveSpacing, limited, "bounds-fallback");
            if (budget <= 0) return;

            SpawnConservedMassInBounds(bounds, budget);
            lastMeltSpawnedCount = trackedParticleIndices.Count;

            if (limited)
            {
                Debug.LogWarning(
                    $"IceWaterPhaseToggle2D [{name}]: bounds-fallback melt budget-limited. " +
                    $"area≈{area:F1} needs ~{ideal} particles, budget={budget}. " +
                    $"Spreading with effectiveSpacing≈{effectiveSpacing:F3}.",
                    this);
            }
        }

        private float ComputeActivePolygonArea()
        {
            float total = 0f;
            for (int i = 0; i < activeIcePolygonsWorld.Count; i++)
            {
                List<Vector2> poly = activeIcePolygonsWorld[i];
                if (poly == null || poly.Count < 3) continue;
                total += Mathf.Abs(SignedArea(poly));
            }
            return total;
        }

        private float ResolvePreferredDensity()
        {
            if (particlesPerUnitArea > 1e-6f)
                return particlesPerUnitArea;
            float s = Mathf.Max(0.05f, particleSpacing);
            return 1f / (s * s);
        }

        private int ResolveSystemParticleCap()
        {
            if (fluidSystem != null && fluidSystem.settings != null)
                return Mathf.Max(1, fluidSystem.settings.maxParticles);
            return Mathf.Max(1, maxParticlesPerToggle);
        }

        /// <summary>
        /// Compute authored-melt particle budget from world area, preferred density, and hard caps.
        /// When budget &lt; ideal and adaptive melt spacing is on, effective spacing widens
        /// so particles still cover the silhouette instead of packing into a tiny dense blob.
        /// </summary>
        private int ResolveAuthoringMeltBudget(
            float area,
            out int idealCount,
            out float effectiveSpacing,
            out bool budgetLimited)
        {
            area = Mathf.Max(0f, area);
            float density = ResolvePreferredDensity();
            idealCount = area > 1e-6f
                ? Mathf.Max(1, Mathf.CeilToInt(area * density))
                : 1;

            int systemCap = ResolveSystemParticleCap();
            int hardCap = Mathf.Min(Mathf.Max(1, maxParticlesPerToggle), systemCap);
            int budget = Mathf.Clamp(idealCount, 0, hardCap);
            budgetLimited = budget < idealCount;

            float preferred = Mathf.Max(0.05f, particleSpacing);
            if (budget > 0 && area > 1e-6f)
                effectiveSpacing = Mathf.Sqrt(area / budget);
            else
                effectiveSpacing = preferred;

            // Preferred density fits: report preferred spacing. Budget-limited + adaptive: widened spacing.
            if (!budgetLimited || !AdaptSpacingToShape)
                effectiveSpacing = preferred;
            else if (budget > 0 && area > 1e-6f)
                effectiveSpacing = Mathf.Sqrt(area / budget);

            return budget;
        }

        private void RecordMeltPlan(
            float area,
            int ideal,
            int budget,
            float effectiveSpacing,
            bool limited,
            string mode)
        {
            lastMeltShapeArea = area;
            lastMeltIdealParticleCount = ideal;
            lastMeltBudget = budget;
            lastMeltEffectiveSpacing = effectiveSpacing;
            lastMeltBudgetLimited = limited;
            lastMeltSpawnedCount = 0;
            lastMeltDiagnostic = limited
                ? $"[{mode}] shapeArea={area:F1} ideal={ideal} budget={budget} " +
                  $"(limited by maxParticlesPerToggle={maxParticlesPerToggle} / systemCap={ResolveSystemParticleCap()}). " +
                  $"effectiveSpacing={effectiveSpacing:F3}. Raise budget for denser water; silhouette coverage is preserved."
                : $"[{mode}] shapeArea={area:F1} ideal={ideal} budget={budget} effectiveSpacing={effectiveSpacing:F3}. OK.";
        }

        /// <summary>
        /// Designer/runtime helper: estimate melt particle needs for the current authored ice shape
        /// without spawning. Useful for Validate Setup and inspector diagnostics.
        /// </summary>
        public MeltCoverageEstimate EstimateMeltCoverage()
        {
            if (activeIcePolygonsWorld.Count == 0)
                RebuildSourcePolygonsFromAuthoring();

            float area = ComputeActivePolygonArea();
            if (area <= 1e-6f)
            {
                Bounds b = GetRegionBounds();
                float pad = Mathf.Max(0f, spawnPadding);
                float w = Mathf.Max(0f, b.size.x - pad * 2f);
                float h = Mathf.Max(0f, b.size.y - pad * 2f);
                area = w * h;
            }

            int budget = ResolveAuthoringMeltBudget(area, out int ideal, out float spacing, out bool limited);
            return new MeltCoverageEstimate
            {
                shapeArea = area,
                idealParticleCount = ideal,
                budget = budget,
                effectiveSpacing = spacing,
                preferredSpacing = Mathf.Max(0.05f, particleSpacing),
                preferredDensity = ResolvePreferredDensity(),
                budgetLimited = limited,
                maxParticlesPerToggle = maxParticlesPerToggle,
                systemParticleCap = ResolveSystemParticleCap(),
                adaptSpacingToShape = AdaptSpacingToShape,
                diagnostic = limited
                    ? $"Shape area {area:F1} wu² needs ~{ideal} particles at preferred density, " +
                      $"but budget is {budget}. Water will cover the silhouette with wider spacing " +
                      $"({spacing:F3}). Raise maxParticlesPerToggle (and Fluid2DSettings.maxParticles if needed) for denser fill."
                    : $"Shape area {area:F1} wu² fits preferred density (~{ideal} particles, spacing {spacing:F3})."
            };
        }

        /// <summary>Read-only melt coverage estimate for designers / validation.</summary>
        public struct MeltCoverageEstimate
        {
            public float shapeArea;
            public int idealParticleCount;
            public int budget;
            public float effectiveSpacing;
            public float preferredSpacing;
            public float preferredDensity;
            public bool budgetLimited;
            public int maxParticlesPerToggle;
            public int systemParticleCap;
            public bool adaptSpacingToShape;
            public string diagnostic;
        }

        private bool TryEmitTracked(Vector2 position, Vector2 velocity)
        {
            if (fluidSystem == null) return false;
            if (!fluidSystem.EmitParticle(position, velocity, fluidType, out int index, out int stamp))
                return false;

            trackedParticleIndices.Add(index);
            trackedParticleStamps.Add(stamp);
            return true;
        }

        private void ClearTrackedParticles(bool syncGpu)
        {
            if (fluidSystem != null && trackedParticleIndices.Count > 0)
            {
                if (syncGpu)
                {
                    fluidSystem.KillTrackedParticles(
                        trackedParticleIndices, syncFromGpu: true, trackedParticleStamps);
                }
                else
                {
                    for (int i = 0; i < trackedParticleIndices.Count; i++)
                    {
                        int idx = trackedParticleIndices[i];
                        // Only kill if this slot still belongs to our patch.
                        if (i < trackedParticleStamps.Count &&
                            fluidSystem.GetParticleEmitStamp(idx) != trackedParticleStamps[i])
                            continue;
                        fluidSystem.KillParticle(idx);
                    }
                }
            }

            trackedParticleIndices.Clear();
            trackedParticleStamps.Clear();
            trackedParticleCount = 0;
        }

        #region Freeze geometry

        private void ResetFreezeDiagnostics(bool partial)
        {
            lastGeneratedContourCount = 0;
            lastClusterCount = 0;
            lastInputClusterCount = 0;
            lastHolesFilled = 0;
            lastBridgesApplied = 0;
            lastSatellitesMerged = 0;
            lastCoverage = 0f;
            lastIceArea = 0f;
            lastParticleIceIoU = 0f;
            lastIceBounds = new Bounds(Vector3.zero, Vector3.zero);
            lastUsedFallback = false;
            lastUsedConvexHullFallback = false;
            lastUsedBoxFallback = false;
            lastUsedRibbonFallback = false;
            lastFallbackReason = string.Empty;
            lastSelfIntersectRepairs = 0;
            lastBoundsClamps = 0;
            lastHullRepairs = 0;
            lastSimpleContourRepairs = 0;
            lastForcedLinearTangents = false;
            lastContourSafetyReason = string.Empty;
            lastDensifiedSampleCount = 0;
            lastOutOfGridParticleCount = 0;
            lastFreezeGridCellUsed = 0f;
            if (!partial)
            {
                lastCapturedParticleCount = 0;
                lastParticleBounds = new Bounds(Vector3.zero, Vector3.zero);
                lastWaterFragmentation = default;
            }
        }

        private float ResolveStampWorldRadius(float cell)
        {
            float simRadius = 0f;
            if (fluidSystem != null && fluidSystem.settings != null)
                simRadius = Mathf.Max(0f, fluidSystem.settings.particleRadius);

            // Match splat bulk: visual radius ≈ particleRadius * scale, plus configurable padding.
            float visual = simRadius > 1e-5f
                ? simRadius * Mathf.Max(1f, freezeVisualRadiusScale)
                : Mathf.Max(cell * 0.5f, freezeParticleStampRadius * cell);

            return Mathf.Max(MinBlobWorldRadius * 0.5f, visual + Mathf.Max(0f, freezeStampPadding));
        }

        private bool TryBuildIceFromParticlePositions(List<Vector2> positions)
        {
            ResetFreezeDiagnostics(partial: true);

            if (positions == null || positions.Count == 0)
                return false;

            float stampWorld = ResolveStampWorldRadius(Mathf.Max(0.05f, freezeGridCellSize));
            float expandPad = Mathf.Max(0f, freezeContourPadding) + Mathf.Max(0f, freezeEdgeWidthCompensation);

            // Sparse long-thin pools: insert intermediate stamp centers so occupancy matches the
            // rendered ribbon (metaball visual is continuous even when particle samples are sparse).
            List<Vector2> stampSamples = DensifySparseRibbonSamples(positions, stampWorld);
            lastDensifiedSampleCount = stampSamples.Count;

            Bounds centerBounds = new Bounds(stampSamples[0], Vector3.zero);
            for (int i = 1; i < stampSamples.Count; i++)
                centerBounds.Encapsulate(stampSamples[i]);
            // Particle silhouette bounds ≈ original live centers expanded by stamp radius (fair IoU).
            // Use live positions when available so densify points do not inflate diagnostics.
            Bounds liveCenterBounds = new Bounds(positions[0], Vector3.zero);
            for (int i = 1; i < positions.Count; i++)
                liveCenterBounds.Encapsulate(positions[i]);
            Bounds silhouetteBounds = liveCenterBounds;
            silhouetteBounds.Expand(stampWorld * 2f);
            lastParticleBounds = silhouetteBounds;

            // Adaptive cell: keep the full elongated pool on-grid (clamp was truncating long strips).
            float cell = Mathf.Max(0.05f, freezeGridCellSize);
            Bounds b = silhouetteBounds;
            b.Encapsulate(centerBounds);
            b.Expand(expandPad * 2.2f + cell * 2f + stampWorld * 0.5f);
            float needX = b.size.x / cell + 2f;
            float needY = b.size.y / cell + 2f;
            if (needX > MaxFreezeGridDim || needY > MaxFreezeGridDim)
            {
                float grow = Mathf.Max(needX / MaxFreezeGridDim, needY / MaxFreezeGridDim);
                cell = Mathf.Max(0.05f, cell * grow);
                // Recompute pad box with the larger cell.
                b = silhouetteBounds;
                b.Encapsulate(centerBounds);
                b.Expand(expandPad * 2.2f + cell * 2f + stampWorld * 0.5f);
            }
            lastFreezeGridCellUsed = cell;

            int width = Mathf.Clamp(Mathf.CeilToInt(b.size.x / cell) + 2, 2, MaxFreezeGridDim);
            int height = Mathf.Clamp(Mathf.CeilToInt(b.size.y / cell) + 2, 2, MaxFreezeGridDim);
            float originX = b.min.x;
            float originY = b.min.y;

            bool[] filled = new bool[width * height];
            StampParticlesOntoGrid(stampSamples, filled, width, height, originX, originY, cell, stampWorld);

            lastOutOfGridParticleCount = CountOutOfGridSamples(
                positions, width, height, originX, originY, cell);

            // Topology-aware occupancy cleanup BEFORE clustering/polygon extraction:
            // bridge small gaps, fill enclosed holes, smooth stair notches, then
            // merge only nearby tiny satellites (distant droplets remain as mini blobs).
            // Elongated sparse ribbons get a larger bridge so visually continuous water stays one strip.
            float bridgeWorld = freezeBridgeGapWorld;
            if (IsElongatedPointSet(positions, ElongatedAspectThreshold))
                bridgeWorld = Mathf.Max(bridgeWorld, stampWorld * 2.25f);
            ApplyOccupancyTopologyCleanup(filled, width, height, cell, bridgeWorld);

            // 8-connected components so diagonally touching stamps stay one visual blob.
            int[] labels = new int[width * height];
            int nextLabel = 1;
            var labelCellCounts = new List<int> { 0 };
            var queue = new Queue<int>(128);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    if (!filled[idx] || labels[idx] != 0) continue;

                    int label = nextLabel++;
                    labelCellCounts.Add(0);
                    labels[idx] = label;
                    queue.Clear();
                    queue.Enqueue(idx);

                    while (queue.Count > 0)
                    {
                        int cur = queue.Dequeue();
                        labelCellCounts[label]++;
                        int cx = cur % width;
                        int cy = cur / width;

                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                int nx = cx + dx;
                                int ny = cy + dy;
                                if ((uint)nx >= (uint)width || (uint)ny >= (uint)height) continue;
                                int nIdx = ny * width + nx;
                                if (!filled[nIdx] || labels[nIdx] != 0) continue;
                                labels[nIdx] = label;
                                queue.Enqueue(nIdx);
                            }
                        }
                    }
                }
            }

            lastInputClusterCount = nextLabel - 1;

            // Merge tiny satellites into nearby large bodies only (configurable physical distance).
            int merged = MergeNearbySatelliteLabels(
                labels, labelCellCounts, width, height, originX, originY, cell,
                freezeMergeSatelliteDistance, freezeSatelliteMaxCells);
            lastSatellitesMerged = merged;

            // Compact labels after merges so iteration stays dense.
            CompactLabels(labels, labelCellCounts, out nextLabel);
            lastClusterCount = nextLabel - 1;

            // Per-cluster particle counts (for min-radius fallback when contour fails).
            var labelParticleCounts = new int[nextLabel];
            var labelParticleSum = new Vector2[nextLabel];
            for (int i = 0; i < positions.Count; i++)
            {
                Vector2 p = positions[i];
                int cx = Mathf.FloorToInt((p.x - originX) / cell);
                int cy = Mathf.FloorToInt((p.y - originY) / cell);
                if ((uint)cx >= (uint)width || (uint)cy >= (uint)height) continue;
                int lab = labels[cy * width + cx];
                if (lab <= 0)
                {
                    // Particle may sit just outside stamped cells — assign nearest filled cell of any label.
                    lab = FindNearestLabel(labels, width, height, cx, cy);
                }
                if (lab <= 0) continue;
                labelParticleCounts[lab]++;
                labelParticleSum[lab] += p;
            }

            int blobsBuilt = 0;
            float simplifyEps = Mathf.Min(
                Mathf.Max(0f, contourSimplifyEpsilon),
                cell * 0.85f);
            var safetyReasons = new List<string>(4);

            for (int label = 1; label < nextLabel; label++)
            {
                // Never silently drop a tracked cluster solely due to minBlobCells.
                // Tiny clusters always freeze as a minimum-radius SpriteShape blob.
                bool isTiny = labelCellCounts[label] < Mathf.Max(1, minBlobCells);

                // Cluster particle samples for bounds clamp / hull repair reference.
                var clusterPts = new List<Vector2>(16);
                for (int i = 0; i < positions.Count; i++)
                {
                    Vector2 p = positions[i];
                    int cx = Mathf.FloorToInt((p.x - originX) / cell);
                    int cy = Mathf.FloorToInt((p.y - originY) / cell);
                    int lab = 0;
                    if ((uint)cx < (uint)width && (uint)cy < (uint)height)
                        lab = labels[cy * width + cx];
                    if (lab != label)
                        lab = FindNearestLabel(labels, width, height, cx, cy);
                    if (lab == label)
                        clusterPts.Add(p);
                }

                Bounds clusterSilhouette = BuildClusterSilhouetteBounds(clusterPts, stampWorld, labelParticleSum, labelParticleCounts, label,
                    labels, width, height, originX, originY, cell);

                List<Vector2> contour = null;
                // Extract occupancy boundary whenever the cluster has enough cells. Tiny clusters
                // still get a coverage contour (ribbon/AABB/circle) so sparse long strips never
                // collapse to a single min-radius blob at the centroid.
                if (!isTiny)
                {
                    var loops = ExtractCellBoundaryContours(
                        labels, label, width, height, originX, originY, cell);
                    contour = SelectOuterLoop(loops);
                }

                if (contour == null || contour.Count < 3)
                {
                    contour = BuildClusterCoverageContour(
                        clusterPts, clusterSilhouette, stampWorld, cell, minFrozenBlobRadius, safetyReasons);
                }
                else
                {
                    SimplifyClosedRingInPlace(contour, simplifyEps);
                    if (contour == null || contour.Count < 3)
                    {
                        contour = BuildClusterCoverageContour(
                            clusterPts, clusterSilhouette, stampWorld, cell, minFrozenBlobRadius, safetyReasons);
                    }
                    else
                    {
                        // Smooth stair-step / spiky cell boundaries before offset.
                        // Elongated thin ribbons: light smooth only — heavy Laplacian + miter offset
                        // often self-intersects and used to force convex-hull/circle collapse.
                        int smoothPasses = freezeContourSmoothPasses;
                        if (IsElongatedPointSet(clusterPts, ElongatedAspectThreshold) ||
                            IsElongatedBounds(clusterSilhouette, ElongatedAspectThreshold))
                            smoothPasses = Mathf.Min(smoothPasses, 1);
                        SmoothClosedContourLaplacian(contour, smoothPasses, 0.35f);
                        // Laplacian can reintroduce self-crossings on concave notches — validate early.
                        if (PolygonSelfIntersects(contour))
                        {
                            lastSelfIntersectRepairs++;
                            contour = RepairToSimpleContour(
                                contour, clusterPts, minFrozenBlobRadius, stampWorld, safetyReasons,
                                preferRibbonForElongated: true);
                        }
                    }
                }

                // Ensure CCW for positive PolygonCollider2D area.
                if (SignedArea(contour) < 0f)
                    contour.Reverse();

                if (expandPad > 0f)
                    OffsetPolygonOutward(contour, expandPad);

                // Second light smooth after offset keeps silhouette solid without reintroducing grid notches.
                SmoothClosedContourLaplacian(contour, Mathf.Min(1, freezeContourSmoothPasses), 0.25f);

                // Guarantee every particle assigned to this cluster remains inside the polygon.
                // Cap expansion so mid-air droplets / mislabels cannot inflate into a starburst.
                EnsureParticlesInsidePolygon(
                    contour, positions, labels, label, width, height, originX, originY, cell,
                    stampWorld * 0.15f, clusterSilhouette, maxIceBoundsInflation);

                if (contour == null || Mathf.Abs(SignedArea(contour)) < 1e-6f)
                {
                    contour = BuildClusterCoverageContour(
                        clusterPts, clusterSilhouette, stampWorld, cell, minFrozenBlobRadius, safetyReasons);
                }

                // Final safety: self-intersection, area/bounds inflation → ribbon / simple / hull / clamp.
                // Elongated pools avoid hull/circle unless true self-intersection remains after ribbon repair.
                contour = ValidateAndRepairGeneratedContour(
                    contour, clusterPts, clusterSilhouette, stampWorld, expandPad, cell, safetyReasons);

                if (!CreateIceBlobObject(contour, blobsBuilt))
                    continue;

                activeIcePolygonsWorld.Add(new List<Vector2>(contour));
                blobsBuilt++;
            }

            if (safetyReasons.Count > 0)
            {
                lastContourSafetyReason = string.Join("; ", safetyReasons);
                if (logFreezeContourSafety)
                    Debug.Log($"IceWaterPhaseToggle2D [{name}]: contour safety — {lastContourSafetyReason}", this);
            }

            // If labeling produced nothing (should not happen with stamps), fall back to hull/box.
            if (blobsBuilt == 0)
            {
                lastUsedFallback = true;
                List<Vector2> hull = ComputeConvexHull(positions);
                if (hull != null && hull.Count >= 3)
                {
                    if (SignedArea(hull) < 0f) hull.Reverse();
                    OffsetPolygonOutward(hull, Mathf.Max(stampWorld * 0.75f, expandPad + cell * 0.5f));
                    EnsureMinimumPolygonThickness(hull, cell * 1.5f);
                    EnsureParticlesInsidePolygonSimple(hull, positions, stampWorld * 0.15f);
                    if (Mathf.Abs(SignedArea(hull)) >= 1e-6f && CreateIceBlobObject(hull, 0))
                    {
                        activeIcePolygonsWorld.Add(new List<Vector2>(hull));
                        blobsBuilt = 1;
                        lastUsedConvexHullFallback = true;
                        lastFallbackReason = "convex_hull";
                    }
                }
            }

            if (blobsBuilt == 0)
            {
                lastUsedFallback = true;
                lastUsedBoxFallback = true;
                lastFallbackReason = "aabb_box";
                Bounds pb = new Bounds(positions[0], Vector3.zero);
                for (int i = 1; i < positions.Count; i++)
                    pb.Encapsulate(positions[i]);
                pb.Expand(Mathf.Max(cell, stampWorld));
                if (pb.size.y < cell * 1.5f)
                    pb.Expand(new Vector3(0f, cell * 1.5f - pb.size.y, 0f));
                if (pb.size.x < cell * 1.5f)
                    pb.Expand(new Vector3(cell * 1.5f - pb.size.x, 0f, 0f));

                var box = new List<Vector2>
                {
                    new Vector2(pb.min.x, pb.min.y),
                    new Vector2(pb.max.x, pb.min.y),
                    new Vector2(pb.max.x, pb.max.y),
                    new Vector2(pb.min.x, pb.max.y)
                };
                if (CreateIceBlobObject(box, 0))
                {
                    activeIcePolygonsWorld.Add(box);
                    blobsBuilt = 1;
                }
            }

            generatedBlobCount = generatedIceBlobs.Count;
            lastGeneratedContourCount = blobsBuilt;
            ComputePostFreezeDiagnostics(positions);
            return blobsBuilt > 0;
        }

        private void StampParticlesOntoGrid(
            List<Vector2> positions, bool[] filled, int width, int height,
            float originX, float originY, float cell, float stampWorld)
        {
            float stampCells = stampWorld / cell;
            int stampR = Mathf.Max(1, Mathf.CeilToInt(stampCells + 0.001f));
            float stampR2 = stampWorld * stampWorld;

            for (int i = 0; i < positions.Count; i++)
            {
                Vector2 p = positions[i];
                float fx = (p.x - originX) / cell;
                float fy = (p.y - originY) / cell;
                int cx = Mathf.FloorToInt(fx);
                int cy = Mathf.FloorToInt(fy);

                for (int dy = -stampR; dy <= stampR; dy++)
                {
                    for (int dx = -stampR; dx <= stampR; dx++)
                    {
                        int x = cx + dx;
                        int y = cy + dy;
                        if ((uint)x >= (uint)width || (uint)y >= (uint)height) continue;

                        // Distance from particle to cell center (world).
                        float cellCx = originX + (x + 0.5f) * cell;
                        float cellCy = originY + (y + 0.5f) * cell;
                        float ddx = cellCx - p.x;
                        float ddy = cellCy - p.y;
                        if (ddx * ddx + ddy * ddy > stampR2) continue;
                        filled[y * width + x] = true;
                    }
                }
            }
        }

        /// <summary>
        /// Bridge small gaps (morphological close), fill small enclosed holes, smooth notches.
        /// Does not remove distant real droplets.
        /// </summary>
        private void ApplyOccupancyTopologyCleanup(bool[] filled, int width, int height, float cell)
        {
            ApplyOccupancyTopologyCleanup(filled, width, height, cell, freezeBridgeGapWorld);
        }

        private void ApplyOccupancyTopologyCleanup(
            bool[] filled, int width, int height, float cell, float bridgeGapWorld)
        {
            lastHolesFilled = 0;
            lastBridgesApplied = 0;

            int bridgeCells = 0;
            if (bridgeGapWorld > 1e-5f && cell > 1e-5f)
            {
                // Close with radius ≈ half the max gap we want to bridge.
                // Cap slightly higher for elongated sparse ribbons (still local, not distant merge).
                bridgeCells = Mathf.Clamp(Mathf.CeilToInt((bridgeGapWorld * 0.5f) / cell), 0, 10);
            }

            if (bridgeCells > 0)
            {
                MorphologicalClose(filled, width, height, bridgeCells);
                lastBridgesApplied = bridgeCells;
            }

            if (freezeMaxHoleCells > 0)
                lastHolesFilled = FillSmallEnclosedHoles(filled, width, height, freezeMaxHoleCells);

            int smoothPasses = Mathf.Clamp(freezeNotchSmoothPasses, 0, 3);
            for (int p = 0; p < smoothPasses; p++)
                MajoritySmoothOccupied(filled, width, height);

            // Second light close (1 cell) re-seals micro gaps opened by majority open steps.
            if (bridgeCells > 0)
                MorphologicalClose(filled, width, height, 1);

            // Re-fill holes that majority may have enlarged slightly.
            if (freezeMaxHoleCells > 0)
                lastHolesFilled += FillSmallEnclosedHoles(filled, width, height, freezeMaxHoleCells);
        }

        /// <summary>
        /// Mild closed-ring Laplacian smooth. Preserves overall mass/silhouette while killing grid spikes.
        /// </summary>
        private static void SmoothClosedContourLaplacian(List<Vector2> poly, int passes, float amount)
        {
            if (poly == null || poly.Count < 4 || passes <= 0) return;
            amount = Mathf.Clamp01(amount);
            int n = poly.Count;
            var next = new Vector2[n];
            for (int p = 0; p < passes; p++)
            {
                for (int i = 0; i < n; i++)
                {
                    Vector2 prev = poly[(i - 1 + n) % n];
                    Vector2 curr = poly[i];
                    Vector2 nxt = poly[(i + 1) % n];
                    Vector2 avg = (prev + nxt) * 0.5f;
                    next[i] = Vector2.Lerp(curr, avg, amount);
                }
                for (int i = 0; i < n; i++)
                    poly[i] = next[i];
            }
        }

        private static void MorphologicalClose(bool[] filled, int width, int height, int radius)
        {
            if (radius <= 0) return;
            bool[] dilated = (bool[])filled.Clone();
            DilateBinary(filled, dilated, width, height, radius);
            bool[] closed = (bool[])dilated.Clone();
            ErodeBinary(dilated, closed, width, height, radius);
            System.Array.Copy(closed, filled, filled.Length);
        }

        private static void DilateBinary(bool[] src, bool[] dst, int width, int height, int radius)
        {
            int n = width * height;
            for (int i = 0; i < n; i++) dst[i] = src[i];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!src[y * width + x]) continue;
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            if (dx * dx + dy * dy > radius * radius) continue;
                            int nx = x + dx;
                            int ny = y + dy;
                            if ((uint)nx >= (uint)width || (uint)ny >= (uint)height) continue;
                            dst[ny * width + nx] = true;
                        }
                    }
                }
            }
        }

        private static void ErodeBinary(bool[] src, bool[] dst, int width, int height, int radius)
        {
            int n = width * height;
            for (int i = 0; i < n; i++) dst[i] = false;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!src[y * width + x]) continue;
                    bool keep = true;
                    for (int dy = -radius; dy <= radius && keep; dy++)
                    {
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            if (dx * dx + dy * dy > radius * radius) continue;
                            int nx = x + dx;
                            int ny = y + dy;
                            if ((uint)nx >= (uint)width || (uint)ny >= (uint)height ||
                                !src[ny * width + nx])
                            {
                                keep = false;
                                break;
                            }
                        }
                    }
                    if (keep) dst[y * width + x] = true;
                }
            }
        }

        /// <summary>
        /// Flood-fill empty components; fill those that do not touch the grid border and are small.
        /// </summary>
        private static int FillSmallEnclosedHoles(bool[] filled, int width, int height, int maxHoleCells)
        {
            int n = width * height;
            int[] visited = new int[n]; // 0 = unseen, 1 = exterior empty, 2 = interior hole candidate
            var queue = new Queue<int>(64);
            int holesFilled = 0;

            // Mark exterior: empty cells flood-filled from border.
            for (int x = 0; x < width; x++)
            {
                EnqueueIfEmptyUnvisited(filled, visited, queue, width, height, x, 0);
                EnqueueIfEmptyUnvisited(filled, visited, queue, width, height, x, height - 1);
            }
            for (int y = 0; y < height; y++)
            {
                EnqueueIfEmptyUnvisited(filled, visited, queue, width, height, 0, y);
                EnqueueIfEmptyUnvisited(filled, visited, queue, width, height, width - 1, y);
            }

            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                int cx = cur % width;
                int cy = cur / width;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = cx + dx;
                        int ny = cy + dy;
                        if ((uint)nx >= (uint)width || (uint)ny >= (uint)height) continue;
                        int nIdx = ny * width + nx;
                        if (visited[nIdx] != 0 || filled[nIdx]) continue;
                        visited[nIdx] = 1;
                        queue.Enqueue(nIdx);
                    }
                }
            }

            // Remaining empty unvisited cells are enclosed holes — fill if small enough.
            var component = new List<int>(32);
            for (int i = 0; i < n; i++)
            {
                if (filled[i] || visited[i] != 0) continue;

                component.Clear();
                queue.Clear();
                visited[i] = 2;
                queue.Enqueue(i);
                component.Add(i);

                while (queue.Count > 0)
                {
                    int cur = queue.Dequeue();
                    int cx = cur % width;
                    int cy = cur / width;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = cx + dx;
                            int ny = cy + dy;
                            if ((uint)nx >= (uint)width || (uint)ny >= (uint)height) continue;
                            int nIdx = ny * width + nx;
                            if (visited[nIdx] != 0 || filled[nIdx]) continue;
                            visited[nIdx] = 2;
                            queue.Enqueue(nIdx);
                            component.Add(nIdx);
                        }
                    }
                }

                if (component.Count > 0 && component.Count <= maxHoleCells)
                {
                    for (int c = 0; c < component.Count; c++)
                        filled[component[c]] = true;
                    holesFilled++;
                }
            }

            return holesFilled;
        }

        private static void EnqueueIfEmptyUnvisited(
            bool[] filled, int[] visited, Queue<int> queue, int width, int height, int x, int y)
        {
            if ((uint)x >= (uint)width || (uint)y >= (uint)height) return;
            int idx = y * width + x;
            if (filled[idx] || visited[idx] != 0) return;
            visited[idx] = 1;
            queue.Enqueue(idx);
        }

        /// <summary>
        /// 8-neighbor majority: empty cell with >=5 filled neighbors becomes filled;
        /// filled cell with <=2 filled neighbors becomes empty (removes single-cell spikes/notches).
        /// Never clears cells that are the only bridge — conservative thresholds.
        /// </summary>
        private static void MajoritySmoothOccupied(bool[] filled, int width, int height)
        {
            bool[] next = (bool[])filled.Clone();
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int idx = y * width + x;
                    int neighbors = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            if (filled[(y + dy) * width + (x + dx)]) neighbors++;
                        }
                    }

                    if (!filled[idx] && neighbors >= 5)
                        next[idx] = true;
                    else if (filled[idx] && neighbors <= 1)
                        next[idx] = false;
                }
            }
            System.Array.Copy(next, filled, filled.Length);
        }

        /// <summary>
        /// Relabel tiny clusters into the nearest large cluster if within mergeDistance.
        /// Far satellites are left alone (become compact mini blobs later).
        /// Returns number of satellite clusters merged.
        /// </summary>
        private static int MergeNearbySatelliteLabels(
            int[] labels, List<int> labelCellCounts,
            int width, int height, float originX, float originY, float cell,
            float mergeDistance, int satelliteMaxCells)
        {
            if (mergeDistance <= 0f || labelCellCounts == null || labelCellCounts.Count <= 2)
                return 0;

            int labelCount = labelCellCounts.Count;
            var centroids = new Vector2[labelCount];
            var counts = new int[labelCount];
            for (int i = 0; i < labelCount; i++)
            {
                centroids[i] = Vector2.zero;
                counts[i] = 0;
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int lab = labels[y * width + x];
                    if (lab <= 0 || lab >= labelCount) continue;
                    float cx = originX + (x + 0.5f) * cell;
                    float cy = originY + (y + 0.5f) * cell;
                    centroids[lab] += new Vector2(cx, cy);
                    counts[lab]++;
                }
            }

            for (int i = 1; i < labelCount; i++)
            {
                if (counts[i] > 0)
                    centroids[i] /= counts[i];
            }

            int maxCells = Mathf.Max(1, satelliteMaxCells);
            float mergeD2 = mergeDistance * mergeDistance;
            int merged = 0;
            var remap = new int[labelCount];
            for (int i = 0; i < labelCount; i++) remap[i] = i;

            for (int sat = 1; sat < labelCount; sat++)
            {
                if (counts[sat] <= 0 || counts[sat] > maxCells) continue;

                int bestLarge = -1;
                float bestD2 = float.MaxValue;
                for (int big = 1; big < labelCount; big++)
                {
                    if (big == sat) continue;
                    if (counts[big] <= maxCells) continue; // only merge into larger bodies
                    float d2 = (centroids[sat] - centroids[big]).sqrMagnitude;
                    if (d2 < bestD2)
                    {
                        bestD2 = d2;
                        bestLarge = big;
                    }
                }

                if (bestLarge > 0 && bestD2 <= mergeD2)
                {
                    remap[sat] = bestLarge;
                    merged++;
                }
            }

            if (merged == 0) return 0;

            // Path-compress remap chains.
            for (int i = 1; i < labelCount; i++)
            {
                int r = i;
                int guard = 8;
                while (remap[r] != r && guard-- > 0)
                    r = remap[r];
                remap[i] = r;
            }

            for (int i = 0; i < labels.Length; i++)
            {
                int lab = labels[i];
                if (lab > 0 && lab < labelCount)
                    labels[i] = remap[lab];
            }

            // Rebuild cell counts.
            for (int i = 0; i < labelCellCounts.Count; i++)
                labelCellCounts[i] = 0;
            for (int i = 0; i < labels.Length; i++)
            {
                int lab = labels[i];
                if (lab > 0 && lab < labelCellCounts.Count)
                    labelCellCounts[lab]++;
            }

            return merged;
        }

        /// <summary>
        /// Compact possibly sparse labels after satellite merge into 1..N dense ids.
        /// </summary>
        private static void CompactLabels(int[] labels, List<int> labelCellCounts, out int nextLabel)
        {
            int maxLab = labelCellCounts != null ? labelCellCounts.Count - 1 : 0;
            var map = new Dictionary<int, int>(16);
            int next = 1;
            for (int i = 0; i < labels.Length; i++)
            {
                int lab = labels[i];
                if (lab <= 0) continue;
                if (!map.TryGetValue(lab, out int mapped))
                {
                    mapped = next++;
                    map[lab] = mapped;
                }
                labels[i] = mapped;
            }

            labelCellCounts.Clear();
            labelCellCounts.Add(0);
            for (int i = 1; i < next; i++)
                labelCellCounts.Add(0);

            for (int i = 0; i < labels.Length; i++)
            {
                int lab = labels[i];
                if (lab > 0 && lab < labelCellCounts.Count)
                    labelCellCounts[lab]++;
            }

            nextLabel = next;
        }

        private static int FindNearestLabel(int[] labels, int width, int height, int cx, int cy)
        {
            int best = 0;
            int bestD2 = int.MaxValue;
            int r = 4;
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = cx + dx;
                    int y = cy + dy;
                    if ((uint)x >= (uint)width || (uint)y >= (uint)height) continue;
                    int lab = labels[y * width + x];
                    if (lab <= 0) continue;
                    int d2 = dx * dx + dy * dy;
                    if (d2 < bestD2)
                    {
                        bestD2 = d2;
                        best = lab;
                    }
                }
            }
            return best;
        }

        private static Vector2 ClusterCellCentroid(
            int[] labels, int label, int width, int height,
            float originX, float originY, float cell)
        {
            double sx = 0, sy = 0;
            int n = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (labels[y * width + x] != label) continue;
                    sx += originX + (x + 0.5f) * cell;
                    sy += originY + (y + 0.5f) * cell;
                    n++;
                }
            }
            if (n == 0) return new Vector2(originX, originY);
            return new Vector2((float)(sx / n), (float)(sy / n));
        }

        private static List<Vector2> MakeCirclePolygon(Vector2 center, float radius, int sides)
        {
            sides = Mathf.Clamp(sides, 6, 24);
            radius = Mathf.Max(MinBlobWorldRadius, radius);
            var poly = new List<Vector2>(sides);
            for (int i = 0; i < sides; i++)
            {
                float a = (i / (float)sides) * Mathf.PI * 2f;
                poly.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
            return poly;
        }

        /// <summary>
        /// Deterministic cell-boundary polygonization: emit directed edges for every filled cell
        /// side adjacent to empty, keep ALL outgoing edges (junction-safe), stitch every closed loop.
        /// Labels represent filled cells (not dual corners).
        /// Winding: outer loops CCW (interior on left while walking).
        /// </summary>
        private static List<List<Vector2>> ExtractCellBoundaryContours(
            int[] labels, int label, int width, int height,
            float originX, float originY, float cell)
        {
            // Adjacency: quantized grid vertex key -> list of outgoing vertex keys.
            // Vertex grid is (width+1) x (height+1).
            var outgoing = new Dictionary<long, List<long>>(256);
            var edgeSeen = new HashSet<long>(); // (fromKey << 1) mixed with to — prevent dup edges

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (labels[y * width + x] != label) continue;

                    // Cell corners in grid-vertex coords.
                    // Bottom-left (x,y), bottom-right (x+1,y), top-right (x+1,y+1), top-left (x,y+1)
                    // CCW with interior on left:
                    // Bottom empty: (x,y)->(x+1,y)
                    // Right empty:  (x+1,y)->(x+1,y+1)
                    // Top empty:    (x+1,y+1)->(x,y+1)
                    // Left empty:   (x,y+1)->(x,y)

                    if (!IsLabelCell(labels, width, height, x, y - 1, label))
                        AddDirectedGridEdge(outgoing, edgeSeen, x, y, x + 1, y);
                    if (!IsLabelCell(labels, width, height, x + 1, y, label))
                        AddDirectedGridEdge(outgoing, edgeSeen, x + 1, y, x + 1, y + 1);
                    if (!IsLabelCell(labels, width, height, x, y + 1, label))
                        AddDirectedGridEdge(outgoing, edgeSeen, x + 1, y + 1, x, y + 1);
                    if (!IsLabelCell(labels, width, height, x - 1, y, label))
                        AddDirectedGridEdge(outgoing, edgeSeen, x, y + 1, x, y);
                }
            }

            var loops = new List<List<Vector2>>();
            if (outgoing.Count == 0)
                return loops;

            // Consume every directed edge exactly once.
            while (true)
            {
                long start = 0;
                bool found = false;
                foreach (var kv in outgoing)
                {
                    if (kv.Value != null && kv.Value.Count > 0)
                    {
                        start = kv.Key;
                        found = true;
                        break;
                    }
                }
                if (!found) break;

                var loop = new List<Vector2>(32);
                long current = start;
                // Previous direction for left-hand (CCW wall-follow) choice at junctions.
                long prevKey = -1;
                int guard = outgoing.Count * 4 + 8;
                bool closed = false;

                while (guard-- > 0)
                {
                    DecodeGridVertex(current, out int gx, out int gy);
                    loop.Add(new Vector2(originX + gx * cell, originY + gy * cell));

                    if (!outgoing.TryGetValue(current, out List<long> outs) || outs == null || outs.Count == 0)
                        break;

                    // Prefer the leftmost turn so junction vertices do not produce figure-8 / starburst rings.
                    int pick = 0;
                    if (outs.Count > 1 && prevKey >= 0)
                        pick = PickLeftmostOutgoingIndex(prevKey, current, outs);
                    long next = outs[pick];
                    outs.RemoveAt(pick);
                    if (outs.Count == 0)
                        outgoing.Remove(current);

                    prevKey = current;
                    current = next;
                    if (current == start)
                    {
                        closed = true;
                        break;
                    }
                }

                if (!closed || loop.Count < 3)
                    continue;

                // Drop closing duplicate if present.
                if (loop.Count >= 2 && (loop[0] - loop[loop.Count - 1]).sqrMagnitude < 1e-12f)
                    loop.RemoveAt(loop.Count - 1);

                RemoveDuplicateRingPoints(loop);
                if (loop.Count >= 3)
                    loops.Add(loop);
            }

            return loops;
        }

        private static bool IsLabelCell(int[] labels, int width, int height, int x, int y, int label)
        {
            if ((uint)x >= (uint)width || (uint)y >= (uint)height) return false;
            return labels[y * width + x] == label;
        }

        private static void AddDirectedGridEdge(
            Dictionary<long, List<long>> outgoing, HashSet<long> edgeSeen,
            int fromX, int fromY, int toX, int toY)
        {
            long a = EncodeGridVertex(fromX, fromY);
            long b = EncodeGridVertex(toX, toY);
            if (a == b) return;

            // Unique directed edge: pack 4 small grid indices into one 64-bit key.
            // Grid is clamped to <=512, so 10 bits per axis is enough; use 16 for safety.
            long packed =
                ((long)(fromX & 0xffff) << 48) |
                ((long)(fromY & 0xffff) << 32) |
                ((long)(toX & 0xffff) << 16) |
                (long)(toY & 0xffff);
            if (!edgeSeen.Add(packed))
                return;

            if (!outgoing.TryGetValue(a, out List<long> list))
            {
                list = new List<long>(2);
                outgoing[a] = list;
            }
            list.Add(b);
        }

        private static long EncodeGridVertex(int x, int y)
        {
            // Offset so negative never occurs; grid coords are >= 0 for our tracer.
            return ((long)(x + 1) << 32) | (uint)(y + 1);
        }

        private static void DecodeGridVertex(long key, out int x, out int y)
        {
            x = (int)(key >> 32) - 1;
            y = (int)(key & 0xffffffff) - 1;
        }

        /// <summary>
        /// At multi-outgoing junctions pick the leftmost (most CCW) turn relative to the arrival direction.
        /// Prevents arbitrary stack-pop order from stitching self-crossing / star contours.
        /// </summary>
        private static int PickLeftmostOutgoingIndex(long fromPrev, long at, List<long> outs)
        {
            DecodeGridVertex(fromPrev, out int px, out int py);
            DecodeGridVertex(at, out int ax, out int ay);
            float inDx = ax - px;
            float inDy = ay - py;
            // Incoming direction as angle of travel.
            float inAngle = Mathf.Atan2(inDy, inDx);

            int best = 0;
            float bestScore = float.MinValue;
            for (int i = 0; i < outs.Count; i++)
            {
                DecodeGridVertex(outs[i], out int nx, out int ny);
                float outAngle = Mathf.Atan2(ny - ay, nx - ax);
                // Cross-product sign + relative angle: larger left-turn score is preferred.
                float delta = Mathf.DeltaAngle(inAngle * Mathf.Rad2Deg, outAngle * Mathf.Rad2Deg);
                // Prefer CCW (positive delta in Unity's left-handed 2D screen? Atan2 CCW positive).
                // Wall-follow interior-on-left for CCW loops → take the sharpest left turn.
                float score = delta;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Pick the largest-area loop as the outer contour (holes ignored for SpriteShape fill).</summary>
        private static List<Vector2> SelectOuterLoop(List<List<Vector2>> loops)
        {
            if (loops == null || loops.Count == 0) return null;
            List<Vector2> best = null;
            float bestAbs = -1f;
            for (int i = 0; i < loops.Count; i++)
            {
                var loop = loops[i];
                if (loop == null || loop.Count < 3) continue;
                float a = Mathf.Abs(SignedArea(loop));
                if (a > bestAbs)
                {
                    bestAbs = a;
                    best = loop;
                }
            }
            return best;
        }

        private void ComputePostFreezeDiagnostics(List<Vector2> positions)
        {
            lastGeneratedContourCount = activeIcePolygonsWorld.Count;
            lastIceArea = 0f;
            lastCoverage = 0f;

            if (activeIcePolygonsWorld.Count == 0)
            {
                lastIceBounds = new Bounds(Vector3.zero, Vector3.zero);
                lastParticleIceIoU = 0f;
                return;
            }

            Bounds iceB = BoundsOfPolygon(activeIcePolygonsWorld[0]);
            for (int i = 0; i < activeIcePolygonsWorld.Count; i++)
            {
                var poly = activeIcePolygonsWorld[i];
                if (poly == null || poly.Count < 3) continue;
                lastIceArea += Mathf.Abs(SignedArea(poly));
                if (i > 0) iceB.Encapsulate(BoundsOfPolygon(poly));
            }
            lastIceBounds = iceB;

            if (positions != null && positions.Count > 0)
            {
                int inside = 0;
                for (int i = 0; i < positions.Count; i++)
                {
                    if (PointInAnyPolygon(positions[i], activeIcePolygonsWorld))
                        inside++;
                }
                lastCoverage = inside / (float)positions.Count;

                Bounds pb = lastParticleBounds.size.sqrMagnitude > 1e-12f
                    ? lastParticleBounds
                    : BoundsOfPoints(positions);
                lastParticleBounds = pb;
                lastParticleIceIoU = BoundsIoU(pb, iceB);
            }
            else
            {
                lastParticleIceIoU = 0f;
            }
        }

        private static Bounds BoundsOfPoints(List<Vector2> pts)
        {
            Bounds b = new Bounds(pts[0], Vector3.zero);
            for (int i = 1; i < pts.Count; i++)
                b.Encapsulate(pts[i]);
            return b;
        }

        private static float BoundsIoU(Bounds a, Bounds b)
        {
            float ix0 = Mathf.Max(a.min.x, b.min.x);
            float iy0 = Mathf.Max(a.min.y, b.min.y);
            float ix1 = Mathf.Min(a.max.x, b.max.x);
            float iy1 = Mathf.Min(a.max.y, b.max.y);
            float iw = ix1 - ix0;
            float ih = iy1 - iy0;
            if (iw <= 0f || ih <= 0f) return 0f;
            float inter = iw * ih;
            float areaA = Mathf.Max(0f, a.size.x) * Mathf.Max(0f, a.size.y);
            float areaB = Mathf.Max(0f, b.size.x) * Mathf.Max(0f, b.size.y);
            float uni = areaA + areaB - inter;
            return uni > 1e-12f ? inter / uni : 0f;
        }

        private static bool PointInAnyPolygon(Vector2 p, List<List<Vector2>> polys)
        {
            for (int i = 0; i < polys.Count; i++)
            {
                var poly = polys[i];
                if (poly != null && poly.Count >= 3 && PointInPolygon(p, poly))
                    return true;
            }
            return false;
        }

        private static void EnsureParticlesInsidePolygon(
            List<Vector2> poly, List<Vector2> positions,
            int[] labels, int label, int width, int height,
            float originX, float originY, float cell, float margin,
            Bounds referenceSilhouette = default,
            float maxBoundsInflation = 2.25f)
        {
            if (poly == null || poly.Count < 3 || positions == null) return;

            bool hasRef = referenceSilhouette.size.sqrMagnitude > 1e-8f;
            float maxExtent = hasRef
                ? Mathf.Max(referenceSilhouette.size.x, referenceSilhouette.size.y) * Mathf.Max(1.1f, maxBoundsInflation)
                : float.MaxValue;

            for (int i = 0; i < positions.Count; i++)
            {
                Vector2 p = positions[i];
                int cx = Mathf.FloorToInt((p.x - originX) / cell);
                int cy = Mathf.FloorToInt((p.y - originY) / cell);
                int lab = 0;
                if ((uint)cx < (uint)width && (uint)cy < (uint)height)
                    lab = labels[cy * width + cx];
                if (lab != label)
                {
                    lab = FindNearestLabel(labels, width, height, cx, cy);
                    if (lab != label) continue;
                }

                if (PointInPolygon(p, poly)) continue;

                // Expand from centroid until point is inside (bounded steps).
                Vector2 c = PolygonCentroid(poly);
                for (int step = 0; step < 6; step++)
                {
                    // Abort expansion if the contour already balloons past the particle silhouette budget.
                    if (hasRef)
                    {
                        Bounds pb = BoundsOfPolygon(poly);
                        if (pb.size.x > maxExtent || pb.size.y > maxExtent)
                            break;
                    }

                    OffsetPolygonOutward(poly, Mathf.Max(margin, cell * 0.25f));
                    if (PointInPolygon(p, poly)) break;
                    // Radial push of vertices toward the outside relative to particle.
                    for (int k = 0; k < poly.Count; k++)
                    {
                        Vector2 dir = poly[k] - c;
                        if (dir.sqrMagnitude < 1e-8f) dir = p - c;
                        if (dir.sqrMagnitude < 1e-8f) dir = Vector2.up;
                        poly[k] = poly[k] + dir.normalized * (cell * 0.12f);
                    }
                }
            }
        }

        private static void EnsureParticlesInsidePolygonSimple(List<Vector2> poly, List<Vector2> positions, float margin)
        {
            if (poly == null || poly.Count < 3 || positions == null) return;
            for (int i = 0; i < positions.Count; i++)
            {
                if (PointInPolygon(positions[i], poly)) continue;
                for (int step = 0; step < 6; step++)
                {
                    OffsetPolygonOutward(poly, Mathf.Max(0.02f, margin));
                    if (PointInPolygon(positions[i], poly)) break;
                }
            }
        }

        /// <summary>
        /// Outward offset along vertex normals (CCW polygon → left normals). Safer than pure radial expand.
        /// </summary>
        private static void OffsetPolygonOutward(List<Vector2> poly, float amount)
        {
            if (poly == null || poly.Count < 3 || amount == 0f) return;
            if (SignedArea(poly) < 0f)
                poly.Reverse();

            int n = poly.Count;
            var result = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = poly[(i - 1 + n) % n];
                Vector2 curr = poly[i];
                Vector2 next = poly[(i + 1) % n];

                Vector2 e0 = (curr - prev).normalized;
                Vector2 e1 = (next - curr).normalized;
                // Inward edges going CCW; outward normal is rotate edge by -90° (right) wait:
                // CCW: edge (curr-prev), left normal = (-ey, ex) points outward? 
                // For CCW unit square (0,0)-(1,0)-(1,1)-(0,1): at (1,0), prev=(0,0), edge=(1,0), left=(-0,1)=(0,1) = up = inward.
                // So left normal is INWARD for CCW. Outward = right normal = (ey, -ex).
                Vector2 n0 = new Vector2(e0.y, -e0.x);
                Vector2 n1 = new Vector2(e1.y, -e1.x);
                Vector2 nrm = n0 + n1;
                if (nrm.sqrMagnitude < 1e-10f)
                    nrm = n0.sqrMagnitude > 1e-10f ? n0 : Vector2.up;
                nrm.Normalize();

                // Miter scale to approximate constant offset.
                // Cap tightly: sharp reflex corners with miter≈4 balloon into starburst spikes.
                float miter = 1f;
                float dot = Vector2.Dot(n0, nrm);
                if (Mathf.Abs(dot) > 1e-4f)
                    miter = Mathf.Clamp(1f / dot, 0.5f, 2f);

                result[i] = curr + nrm * (amount * miter);
            }

            for (int i = 0; i < n; i++)
                poly[i] = result[i];
        }

        private bool CreateIceBlobObject(List<Vector2> worldPoly, int index)
        {
            if (worldPoly == null || worldPoly.Count < 3) return false;

            Vector2 centroid = PolygonCentroid(worldPoly);
            var go = new GameObject($"{GeneratedBlobPrefix}{index}");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, worldPositionStays: true);
            go.transform.position = new Vector3(centroid.x, centroid.y, transform.position.z);
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            // Local contour for both SpriteShape spline and solid PolygonCollider2D.
            var rawLocal = new List<Vector2>(worldPoly.Count);
            for (int i = 0; i < worldPoly.Count; i++)
                rawLocal.Add((Vector2)go.transform.InverseTransformPoint(worldPoly[i]));

            List<Vector2> localPts = SanitizeSplinePolygon(rawLocal);
            if (localPts.Count < 3)
            {
                if (Application.isPlaying) Object.Destroy(go);
                else Object.DestroyImmediate(go);
                return false;
            }

            if (SignedArea(localPts) < 0f)
                localPts.Reverse();

            // Final local-space self-intersect guard (sanitize spacing can rarely re-order seam badly).
            if (PolygonSelfIntersects(localPts))
            {
                lastSelfIntersectRepairs++;
                List<Vector2> repaired = null;
                // Elongated local contours: preserve length with ribbon before isotropic star collapse.
                if (IsElongatedPointSet(localPts, ElongatedAspectThreshold))
                {
                    float halfW = EstimateRibbonHalfWidth(localPts, MinBlobWorldRadius);
                    repaired = BuildOrientedRibbonContour(localPts, halfW, MinBlobWorldRadius * 0.25f);
                    if (repaired != null && repaired.Count >= 3 && !PolygonSelfIntersects(repaired))
                        lastUsedRibbonFallback = true;
                    else
                        repaired = null;
                }
                // True self-intersection on compact / star shapes: convex hull (preserves starburst safeguards).
                if (repaired == null || repaired.Count < 3 || PolygonSelfIntersects(repaired))
                {
                    repaired = ComputeConvexHull(localPts);
                    if (repaired != null && repaired.Count >= 3)
                        lastHullRepairs++;
                }
                if (repaired == null || repaired.Count < 3 || PolygonSelfIntersects(repaired))
                    repaired = BuildRadialOuterEnvelope(localPts, 16, MinBlobWorldRadius);
                if (repaired == null || repaired.Count < 3 || PolygonSelfIntersects(repaired))
                    repaired = BuildAngularOrderedContour(localPts, MinBlobWorldRadius);
                if (repaired != null && repaired.Count >= 3)
                {
                    if (SignedArea(repaired) < 0f) repaired.Reverse();
                    localPts = SanitizeSplinePolygon(repaired);
                }
            }

            if (localPts == null || localPts.Count < 3 || Mathf.Abs(SignedArea(localPts)) < 1e-8f)
            {
                if (Application.isPlaying) Object.Destroy(go);
                else Object.DestroyImmediate(go);
                return false;
            }

            // Real Sprite Shape visual (profile + edge/fill sprites), not a MeshRenderer white blob.
            // SpriteShapeController requires SpriteShapeRenderer (auto-added).
            var blobShape = go.AddComponent<SpriteShapeController>();
            var blobRenderer = go.GetComponent<SpriteShapeRenderer>();
            ConfigureGeneratedSpriteShape(blobShape, blobRenderer);

            float contourMinSize = ContourMinDimensionAndAspect(localPts, out float aspect);
            lastContourAspectRatio = aspect;
            float splineHeight = ComputeGeneratedSplineHeight(contourMinSize);
            lastGeneratedSplineHeight = splineHeight;

            if (!TryBuildClosedSpline(blobShape.spline, localPts, splineHeight))
            {
                if (Application.isPlaying) Object.Destroy(go);
                else Object.DestroyImmediate(go);
                return false;
            }

            // Keep solid collision on the same contour; do not let SpriteShape overwrite it.
            // Collision is immediate via PolygonCollider2D; visual mesh may appear next rendered frame.
            blobShape.autoUpdateCollider = false;
            var polyCol = go.AddComponent<PolygonCollider2D>();
            polyCol.pathCount = 1;
            polyCol.SetPath(0, localPts.ToArray());
            polyCol.isTrigger = false;
            if (cachedSharedMaterial != null)
                polyCol.sharedMaterial = cachedSharedMaterial;

            // Tight finite AABB + RefreshSpriteShape lifecycle (no immediate BakeMesh.Complete).
            // Immediate bake was observed to leave SpriteShapeRenderer with ~2^21 invalid extents.
            FinalizeGeneratedSpriteShapeVisual(blobShape, blobRenderer, localPts, splineHeight);

            generatedIceBlobs.Add(go);
            managedColliders.Add(polyCol);
            return true;
        }

        private void ConfigureGeneratedSpriteShape(SpriteShapeController blobShape, SpriteShapeRenderer blobRenderer)
        {
            if (blobShape == null) return;

            SpriteShapeController src = spriteShapeController;
            if (src != null)
            {
                // Same edge/fill sprites as authored ice, but borders disabled on a runtime clone
                // so tiny contours do not grow ~1wu head/tail quads into flower-like meshes.
                blobShape.spriteShape = GetOrCreateGeneratedSpriteShapeProfile(src.spriteShape);
                blobShape.fillPixelsPerUnit = src.fillPixelsPerUnit;
                blobShape.stretchTiling = src.stretchTiling;
                blobShape.splineDetail = src.splineDetail > 0 ? src.splineDetail : 16;
                blobShape.cornerAngleThreshold = src.cornerAngleThreshold;
                blobShape.colliderDetail = src.colliderDetail;
                blobShape.colliderOffset = src.colliderOffset;
                blobShape.enableTangents = src.enableTangents;
                blobShape.worldSpaceUVs = src.worldSpaceUVs;
            }
            else
            {
                blobShape.splineDetail = 16;
                blobShape.fillPixelsPerUnit = 100f;
            }

            // Collision is owned by our PolygonCollider2D (same contour as the spline).
            blobShape.autoUpdateCollider = false;
            // boundsScale is applied after the contour spline is written (see FinalizeGeneratedSpriteShapeVisual).

            if (blobRenderer == null)
                blobRenderer = blobShape.spriteShapeRenderer;
            if (blobRenderer == null) return;

            if (spriteShapeRenderer != null)
            {
                blobRenderer.color = spriteShapeRenderer.color;
                blobRenderer.sortingLayerID = spriteShapeRenderer.sortingLayerID;
                // Keep generated ice slightly above authored so fill is not z-sorted under ground props.
                blobRenderer.sortingOrder = spriteShapeRenderer.sortingOrder;

                Material[] mats = spriteShapeRenderer.sharedMaterials;
                if (mats != null && mats.Length > 0)
                {
                    // Clone array so generated blobs never share a mutated material slot reference.
                    var copy = new Material[mats.Length];
                    for (int i = 0; i < mats.Length; i++)
                        copy[i] = mats[i];
                    blobRenderer.sharedMaterials = copy;
                }
            }
            else
            {
                blobRenderer.color = iceTint;
            }

            // Opaque white keeps fill texture / edge sprites at authored crystal brightness.
            if (blobRenderer.color.a < 0.99f)
            {
                Color c = blobRenderer.color;
                c.a = 1f;
                blobRenderer.color = c;
            }
        }

        /// <summary>
        /// Build a closed spline from local contour points. Points must stay far enough apart for
        /// both Spline.InsertPointAt (0.01) and SpriteShapeController.ValidateSpline (≈0.0316).
        /// </summary>
        private bool TryBuildClosedSpline(Spline spline, List<Vector2> localPts, float height)
        {
            if (spline == null || localPts == null || localPts.Count < 3)
                return false;

            spline.Clear();
            spline.isOpenEnded = false;

            int inserted = 0;
            for (int i = 0; i < localPts.Count; i++)
            {
                Vector3 p = localPts[i];
                if (inserted > 0)
                {
                    Vector3 prev = spline.GetPosition(inserted - 1);
                    if ((p - prev).magnitude < MinSplinePointDistance)
                        continue;
                }

                try
                {
                    spline.InsertPointAt(inserted, p);
                    inserted++;
                }
                catch (System.ArgumentException)
                {
                    // Neighbor too close — skip.
                }
            }

            // Closed ring: first/last must also respect min distance (including ValidateSpline chain).
            if (inserted >= 3)
            {
                Vector3 first = spline.GetPosition(0);
                Vector3 last = spline.GetPosition(inserted - 1);
                if ((first - last).magnitude < MinSplinePointDistance)
                {
                    spline.RemovePointAt(inserted - 1);
                    inserted--;
                }
            }

            // Final pass: drop any remaining adjacent pairs under epsilon (pathological dense rings).
            inserted = PurgeCloseSplinePoints(spline, MinSplinePointDistance);

            if (inserted < 3)
                return false;

            spline.isOpenEnded = false;
            ApplySplineTangents(spline, height);
            return true;
        }

        private static int PurgeCloseSplinePoints(Spline spline, float minDist)
        {
            if (spline == null) return 0;
            int guard = spline.GetPointCount() + 4;
            while (guard-- > 0 && spline.GetPointCount() >= 3)
            {
                int n = spline.GetPointCount();
                int removeAt = -1;
                for (int i = 0; i < n; i++)
                {
                    Vector3 a = spline.GetPosition(i);
                    Vector3 b = spline.GetPosition((i + 1) % n);
                    if ((a - b).magnitude < minDist)
                    {
                        // Prefer removing the later index; for last→first seam remove last.
                        removeAt = (i + 1) % n;
                        if (removeAt == 0)
                            removeAt = n - 1;
                        break;
                    }
                }

                if (removeAt < 0)
                    break;

                try
                {
                    spline.RemovePointAt(removeAt);
                }
                catch (System.ArgumentException)
                {
                    break;
                }
            }

            return spline.GetPointCount();
        }

        private void ApplySplineTangents(Spline spline, float height)
        {
            int n = spline.GetPointCount();
            if (n < 3) return;

            float h = Mathf.Clamp(height, minGeneratedSplineHeight, maxGeneratedSplineHeight);
            ShapeTangentMode mode = generatedSplineTangentMode;
            // Broken is treated as Continuous for generated contours.
            if (mode == ShapeTangentMode.Broken)
                mode = ShapeTangentMode.Continuous;

            // Large / sharp closed polygons with Continuous tangents produce crossing Bezier fans
            // (starburst SpriteShape). Force Linear when geometry looks dangerous.
            if (mode == ShapeTangentMode.Continuous && forceLinearTangentsOnSharpContours &&
                ContourNeedsLinearTangents(spline))
            {
                mode = ShapeTangentMode.Linear;
                lastForcedLinearTangents = true;
            }

            if (mode == ShapeTangentMode.Linear)
            {
                for (int i = 0; i < n; i++)
                {
                    spline.SetTangentMode(i, ShapeTangentMode.Linear);
                    spline.SetLeftTangent(i, Vector3.zero);
                    spline.SetRightTangent(i, Vector3.zero);
                    spline.SetHeight(i, h);
                    // Corners off: with huge authored edge sprites, corner slices balloon tiny blobs.
                    spline.SetCorner(i, false);
                }
                return;
            }

            // Continuous: keep scale conservative; never exceed 0.28 of segment for large rings.
            float scale = Mathf.Clamp(continuousTangentScale, 0.05f, 0.28f);
            if (n >= 12)
                scale = Mathf.Min(scale, 0.18f);

            for (int i = 0; i < n; i++)
            {
                Vector3 prev = spline.GetPosition((i - 1 + n) % n);
                Vector3 curr = spline.GetPosition(i);
                Vector3 next = spline.GetPosition((i + 1) % n);
                Vector3 dir = (next - prev) * 0.5f * scale;

                // Cap tangent length so short segments and sharp corners don't balloon / self-intersect.
                float segA = (curr - prev).magnitude;
                float segB = (next - curr).magnitude;
                float maxLen = Mathf.Min(segA, segB) * 0.35f;
                // Extra clamp on acute corners (dot of incoming/outgoing edges).
                Vector3 e0 = (curr - prev);
                Vector3 e1 = (next - curr);
                if (e0.sqrMagnitude > 1e-10f && e1.sqrMagnitude > 1e-10f)
                {
                    float edgeDot = Vector3.Dot(e0.normalized, e1.normalized);
                    if (edgeDot < 0.2f)
                        maxLen = Mathf.Min(maxLen, Mathf.Min(segA, segB) * 0.15f);
                    if (edgeDot < -0.3f)
                    {
                        // Near-spike: zero continuous tangents at this vertex.
                        spline.SetTangentMode(i, ShapeTangentMode.Linear);
                        spline.SetLeftTangent(i, Vector3.zero);
                        spline.SetRightTangent(i, Vector3.zero);
                        spline.SetHeight(i, h);
                        spline.SetCorner(i, false);
                        continue;
                    }
                }

                if (dir.magnitude > maxLen && dir.sqrMagnitude > 1e-12f)
                    dir = dir.normalized * maxLen;

                spline.SetTangentMode(i, ShapeTangentMode.Continuous);
                spline.SetLeftTangent(i, -dir);
                spline.SetRightTangent(i, dir);
                spline.SetHeight(i, h);
                spline.SetCorner(i, false);
            }
        }

        /// <summary>
        /// Detect contours where Continuous tangents are likely to fan into a self-intersecting mesh.
        /// </summary>
        private static bool ContourNeedsLinearTangents(Spline spline)
        {
            int n = spline.GetPointCount();
            if (n < 3) return false;
            if (n >= 16) return true; // dense extracted rings — linear is safer and matches collider

            int sharp = 0;
            float maxSeg = 0f;
            float minSeg = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                Vector3 prev = spline.GetPosition((i - 1 + n) % n);
                Vector3 curr = spline.GetPosition(i);
                Vector3 next = spline.GetPosition((i + 1) % n);
                Vector3 e0 = curr - prev;
                Vector3 e1 = next - curr;
                float la = e0.magnitude;
                float lb = e1.magnitude;
                maxSeg = Mathf.Max(maxSeg, la, lb);
                minSeg = Mathf.Min(minSeg, la, lb);
                if (la > 1e-6f && lb > 1e-6f)
                {
                    float d = Vector3.Dot(e0 / la, e1 / lb);
                    if (d < 0.05f) sharp++;
                }
            }

            if (sharp >= 3) return true;
            if (minSeg > 1e-5f && maxSeg / minSeg > 12f) return true;
            return false;
        }

        private float ComputeGeneratedSplineHeight(float contourMinSize)
        {
            float refSize = Mathf.Max(cachedAuthoredContourMinSize, 1e-3f);
            float authoredH = Mathf.Max(cachedAuthoredSplineHeight, 1e-3f);
            float minH = Mathf.Clamp(minGeneratedSplineHeight, 0.02f, 0.5f);
            float maxH = Mathf.Clamp(maxGeneratedSplineHeight, minH, 1f);
            float size = Mathf.Max(contourMinSize, 1e-4f);

            // Large blobs (near authored size) keep authored edge thickness; tiny ones scale down.
            float sizeRatio = Mathf.Clamp01(size / refSize);
            float proportional = authoredH * sizeRatio;

            // Cap height so expected edge overshoot stays within budget of contour size.
            // Generated profiles disable sprite borders and force fillOffset≈0 (see
            // GetOrCreateGeneratedSpriteShapeProfile), so edges sit as a crystal rim outside the
            // solid UTess2D fill instead of overlapping inward into a hollow ring.
            float overshootAtFullHeight = Mathf.Max(cachedEdgeSpriteWorldHeight * 0.65f, 0.4f);
            float t = sizeRatio;
            float smallBlobPenalty = t < 0.35f ? Mathf.Lerp(1.75f, 1f, t / 0.35f) : 1f;
            float overshootFrac = Mathf.Min(Mathf.Clamp(maxEdgeOvershootFraction, 0.1f, 1f), 0.3f);
            float maxByOvershoot = (size * overshootFrac) / (overshootAtFullHeight * smallBlobPenalty);

            // Keep opposing edge rims from meeting across thin/flat pools (glow spikes / hollow look).
            // World edge thickness ≈ edgeSpriteH * height; cap vs local min dimension.
            float edgeSpriteH = Mathf.Max(cachedEdgeSpriteWorldHeight, 0.25f);
            float thickFrac = Mathf.Clamp(maxEdgeThicknessVsMinDim, 0.05f, 0.4f);
            float maxByFillVisible = (size * thickFrac) / edgeSpriteH;

            // Freeze always rebuilds from water particles — never the original 4×4 authored block.
            // AABB minDim overestimates local thickness on splash/Y shapes (global box can be tall
            // while necks are thin), so never unlock full authored height from size alone.
            // Hard-cap keeps crystal rims thin; Ice ground fill texture owns the interior.
            float thinCap = Mathf.Clamp(thinPoolMaxSplineHeight, minH, 0.12f);

            float h = Mathf.Min(proportional, maxByOvershoot, maxByFillVisible, thinCap);
            return Mathf.Clamp(h, minH, maxH);
        }

        /// <summary>min(size)/max(size) of last contour AABB; used so long flat pools never unlock bulk edge height.</summary>
        private float lastContourAspectRatio = 1f;

        private static float ContourMinDimension(List<Vector2> localPts)
        {
            if (localPts == null || localPts.Count == 0)
                return 0f;

            Bounds b = new Bounds(localPts[0], Vector3.zero);
            for (int i = 1; i < localPts.Count; i++)
                b.Encapsulate(localPts[i]);

            return Mathf.Max(Mathf.Min(b.size.x, b.size.y), 1e-4f);
        }

        private float ContourMinDimensionAndAspect(List<Vector2> localPts, out float aspectRatio)
        {
            aspectRatio = 1f;
            if (localPts == null || localPts.Count == 0)
                return 0f;

            Bounds b = new Bounds(localPts[0], Vector3.zero);
            for (int i = 1; i < localPts.Count; i++)
                b.Encapsulate(localPts[i]);

            float minD = Mathf.Max(Mathf.Min(b.size.x, b.size.y), 1e-4f);
            float maxD = Mathf.Max(Mathf.Max(b.size.x, b.size.y), 1e-4f);
            aspectRatio = minD / maxD;
            return minD;
        }

        private void FinalizeGeneratedSpriteShapeVisual(
            SpriteShapeController blobShape,
            SpriteShapeRenderer blobRenderer,
            List<Vector2> localPts,
            float splineHeight)
        {
            if (blobShape == null) return;

            if (blobRenderer == null)
                blobRenderer = blobShape.spriteShapeRenderer;

            // Seed a tight finite local AABB from the contour (+ bounded edge pad) so culling and
            // renderer bounds never inherit the default ~2^21 invalid extents.
            if (blobRenderer != null && localPts != null && localPts.Count > 0)
            {
                Bounds aabb = new Bounds(localPts[0], Vector3.zero);
                for (int i = 1; i < localPts.Count; i++)
                    aabb.Encapsulate(localPts[i]);

                float contourMin = ContourMinDimension(localPts);
                // Pad only enough for scaled edge thickness; Expand adds amount on each side.
                float edgePad = Mathf.Max(cachedAuthoredEdgeOvershoot, cachedEdgeSpriteWorldHeight * 0.5f)
                    * Mathf.Max(splineHeight, 0f);
                edgePad = Mathf.Min(edgePad, contourMin * Mathf.Clamp(maxEdgeOvershootFraction, 0.15f, 1f) * 0.5f);
                edgePad = Mathf.Max(edgePad, MinSplinePointDistance * 0.5f);

                aabb.Expand(edgePad);
                // Force zero Z extent for 2D.
                Vector3 size = aabb.size;
                size.z = 0f;
                aabb.size = size;
                blobRenderer.SetLocalAABB(aabb);
            }

            // Prefer 1.0: authored boundsScale=2 is for large platforms and inflates tiny-blob AABBs.
            blobShape.boundsScale = GeneratedBoundsScale;
            blobShape.RefreshSpriteShape();
            // Visual mesh is generated on the next OnWillRenderObject; collision is already live.
        }

        /// <summary>
        /// Clean contour for SpriteShape: drop near-duplicates and points closer than spline epsilon.
        /// </summary>
        private static List<Vector2> SanitizeSplinePolygon(List<Vector2> pts)
        {
            var list = new List<Vector2>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
                list.Add(pts[i]);

            RemoveDuplicateRingPoints(list);
            RemoveNearCollinearRingPoints(list);
            EnforceMinRingSpacing(list, MinSplinePointDistance);
            // Second spacing pass after collinear removal can reintroduce near-duplicates at the seam.
            EnforceMinRingSpacing(list, MinSplinePointDistance);
            return list;
        }

        private static void RemoveDuplicateRingPoints(List<Vector2> poly)
        {
            if (poly == null || poly.Count < 2) return;
            const float dupEps2 = 1e-10f;

            for (int i = poly.Count - 1; i > 0; i--)
            {
                if ((poly[i] - poly[i - 1]).sqrMagnitude <= dupEps2)
                    poly.RemoveAt(i);
            }

            if (poly.Count >= 2 && (poly[0] - poly[poly.Count - 1]).sqrMagnitude <= dupEps2)
                poly.RemoveAt(poly.Count - 1);
        }

        private static void RemoveNearCollinearRingPoints(List<Vector2> poly)
        {
            if (poly == null || poly.Count < 3) return;
            const float collinearAbs = 1e-8f;
            const float collinearRel = 1e-5f;

            bool removed = true;
            int guard = poly.Count + 2;
            while (removed && guard-- > 0 && poly.Count >= 3)
            {
                removed = false;
                for (int i = 0; i < poly.Count && poly.Count >= 3;)
                {
                    Vector2 prev = poly[(i + poly.Count - 1) % poly.Count];
                    Vector2 curr = poly[i];
                    Vector2 next = poly[(i + 1) % poly.Count];
                    float cross = Cross(prev, curr, next);
                    float ab = (curr - prev).magnitude;
                    float bc = (next - curr).magnitude;
                    float thresh = Mathf.Max(collinearAbs, collinearRel * ab * bc);
                    if (Mathf.Abs(cross) <= thresh)
                    {
                        poly.RemoveAt(i);
                        removed = true;
                        continue;
                    }
                    i++;
                }
            }
        }

        private static void EnforceMinRingSpacing(List<Vector2> poly, float minDist)
        {
            if (poly == null || poly.Count < 3 || minDist <= 0f) return;
            float minDist2 = minDist * minDist;

            bool removed = true;
            int guard = poly.Count + 2;
            while (removed && guard-- > 0 && poly.Count >= 3)
            {
                removed = false;
                for (int i = poly.Count - 1; i >= 1 && poly.Count >= 3; i--)
                {
                    if ((poly[i] - poly[i - 1]).sqrMagnitude < minDist2)
                    {
                        poly.RemoveAt(i);
                        removed = true;
                    }
                }

                if (poly.Count >= 3 && (poly[0] - poly[poly.Count - 1]).sqrMagnitude < minDist2)
                {
                    poly.RemoveAt(poly.Count - 1);
                    removed = true;
                }
            }
        }

        private void DestroyGeneratedIce(bool refreshObstacles)
        {
            for (int i = 0; i < generatedIceBlobs.Count; i++)
                DestroyBlobObject(generatedIceBlobs[i]);
            generatedIceBlobs.Clear();

            // Sweep orphans not tracked in the list (deferred-destroy leftovers, rename races).
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != null && child.name.StartsWith(GeneratedBlobPrefix, System.StringComparison.Ordinal))
                    DestroyBlobObject(child.gameObject);
            }

            generatedBlobCount = 0;

            managedColliders.RemoveAll(c => c == null);
            if (iceCollider != null && !managedColliders.Contains(iceCollider))
                managedColliders.Add(iceCollider);

            if (refreshObstacles)
                RefreshFluidObstacles();
        }

        private void DestroyBlobObject(GameObject go)
        {
            if (go == null) return;

            // Immediately strip collision/visuals so same-frame fluid/obstacle refresh sees no ghost box.
            var cols = go.GetComponentsInChildren<Collider2D>(true);
            for (int c = 0; c < cols.Length; c++)
            {
                if (cols[c] == null) continue;
                cols[c].enabled = false;
                managedColliders.Remove(cols[c]);
            }

            var ssr = go.GetComponent<SpriteShapeRenderer>();
            if (ssr != null)
                ssr.enabled = false;

            var ssc = go.GetComponent<SpriteShapeController>();
            if (ssc != null)
            {
                ssc.enabled = false;
                // Drop spline so a deferred-destroy frame cannot leave a stale silhouette.
                if (ssc.spline != null)
                    ssc.spline.Clear();
            }

            // Legacy mesh path (if any leftover from older builds) — never leave white mesh ghosts.
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
                mr.enabled = false;
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null)
                mf.sharedMesh = null;

            go.name = "DESTROYING_" + go.name;
            go.SetActive(false);

            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }

        #endregion

        #region Shape sources / helpers

        private void RebuildSourcePolygonsFromAuthoring()
        {
            activeIcePolygonsWorld.Clear();

            // Prefer closed SpriteShape spline (true ice silhouette).
            if (spriteShapeController != null && spriteShapeController.spline != null)
            {
                Spline spline = spriteShapeController.spline;
                int count = spline.GetPointCount();
                if (count >= 3 && !spline.isOpenEnded)
                {
                    var poly = new List<Vector2>(count);
                    for (int i = 0; i < count; i++)
                    {
                        Vector3 local = spline.GetPosition(i);
                        Vector3 world = spriteShapeController.transform.TransformPoint(local);
                        poly.Add(world);
                    }
                    if (SignedArea(poly) < 0f) poly.Reverse();
                    activeIcePolygonsWorld.Add(poly);
                    return;
                }
            }

            // Collider-based fallback.
            if (iceCollider is PolygonCollider2D polyCol)
            {
                for (int p = 0; p < polyCol.pathCount; p++)
                {
                    Vector2[] path = polyCol.GetPath(p);
                    if (path == null || path.Length < 3) continue;
                    var poly = new List<Vector2>(path.Length);
                    for (int i = 0; i < path.Length; i++)
                        poly.Add(iceCollider.transform.TransformPoint(path[i] + polyCol.offset));
                    if (SignedArea(poly) < 0f) poly.Reverse();
                    activeIcePolygonsWorld.Add(poly);
                }
                if (activeIcePolygonsWorld.Count > 0) return;
            }

            if (iceCollider is BoxCollider2D box)
            {
                Vector2 half = box.size * 0.5f;
                Vector2 o = box.offset;
                Vector2[] corners =
                {
                    iceCollider.transform.TransformPoint(o + new Vector2(-half.x, -half.y)),
                    iceCollider.transform.TransformPoint(o + new Vector2(-half.x,  half.y)),
                    iceCollider.transform.TransformPoint(o + new Vector2( half.x,  half.y)),
                    iceCollider.transform.TransformPoint(o + new Vector2( half.x, -half.y)),
                };
                activeIcePolygonsWorld.Add(new List<Vector2>(corners));
            }
        }

        private SpriteShape GetOrCreateGeneratedSpriteShapeProfile(SpriteShape source)
        {
            if (source == null)
                return null;

            // Authored Ice ground uses fillOffset≈0.5 (edge centered on spline). On thick platforms
            // that looks correct, but on thin water-derived pools opposing edge sprites hide the
            // interior and SpriteShape disables UTess2D when fillOffset != 0 — producing a bright
            // hollow ring with ground showing through the center. Force FO=0 + no borders on a
            // runtime clone only (never mutate the source asset).
            float targetFillOffset = Mathf.Clamp(generatedFillOffset, 0f, 0.5f);

            if (generatedSpriteShapeProfile != null)
            {
                bool sameFill = generatedSpriteShapeProfile.fillTexture == source.fillTexture;
                bool sameOffset = Mathf.Abs(generatedSpriteShapeProfile.fillOffset - targetFillOffset) < 1e-4f;
                bool noBorders = !generatedSpriteShapeProfile.useSpriteBorders;
                if (sameFill && sameOffset && noBorders)
                    return generatedSpriteShapeProfile;
                ReleaseGeneratedSpriteShapeProfile();
            }

            generatedSpriteShapeProfile = Object.Instantiate(source);
            generatedSpriteShapeProfile.name = source.name + "_RuntimeGeneratedSolidFill";
            generatedSpriteShapeProfile.useSpriteBorders = false;
            generatedSpriteShapeProfile.fillOffset = targetFillOffset;
            // Preserve fill texture + edge angle sprites from source for crystalline look.
            if (generatedSpriteShapeProfile.fillTexture == null && source.fillTexture != null)
                generatedSpriteShapeProfile.fillTexture = source.fillTexture;
            generatedSpriteShapeProfile.hideFlags = HideFlags.HideAndDontSave;
            return generatedSpriteShapeProfile;
        }

        private void ReleaseGeneratedSpriteShapeProfile()
        {
            if (generatedSpriteShapeProfile == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(generatedSpriteShapeProfile);
            else
                Object.DestroyImmediate(generatedSpriteShapeProfile);
            generatedSpriteShapeProfile = null;
        }

        private void CaptureIceVisualProfile()
        {
            // Generated blobs reuse the authored SpriteShape profile + renderer props at freeze time.
            // Cache tint, physics material, authored height/size, and edge-sprite world height.
            if (spriteShapeRenderer != null)
                iceTint = spriteShapeRenderer.color;

            if (iceCollider != null)
                cachedSharedMaterial = iceCollider.sharedMaterial;

            cachedAuthoredSplineHeight = 1f;
            cachedAuthoredContourMinSize = 4f;
            cachedEdgeSpriteWorldHeight = 2.5f;
            cachedAuthoredEdgeOvershoot = 2.2f;

            SpriteShapeController src = spriteShapeController;
            if (src != null && src.spline != null && src.spline.GetPointCount() >= 3)
            {
                int n = src.spline.GetPointCount();
                float sumH = 0f;
                Bounds lb = new Bounds(src.spline.GetPosition(0), Vector3.zero);
                for (int i = 0; i < n; i++)
                {
                    sumH += src.spline.GetHeight(i);
                    lb.Encapsulate(src.spline.GetPosition(i));
                }

                cachedAuthoredSplineHeight = Mathf.Max(sumH / n, 0.05f);
                float minDim = Mathf.Min(lb.size.x, lb.size.y);
                if (minDim > 1e-3f)
                    cachedAuthoredContourMinSize = minDim;

                // Measure how much the authored renderer grows beyond the spline (edge decoration).
                if (spriteShapeRenderer != null)
                {
                    float rMin = Mathf.Min(spriteShapeRenderer.localBounds.size.x, spriteShapeRenderer.localBounds.size.y);
                    if (rMin > 1e-3f && rMin < 1e5f)
                        cachedAuthoredEdgeOvershoot = Mathf.Max(0.1f, rMin - minDim);
                }
            }

            float maxEdgeH = 0f;
            SpriteShape profile = src != null ? src.spriteShape : null;
            if (profile != null && profile.angleRanges != null)
            {
                for (int ai = 0; ai < profile.angleRanges.Count; ai++)
                {
                    var sprites = profile.angleRanges[ai].sprites;
                    if (sprites == null) continue;
                    for (int si = 0; si < sprites.Count; si++)
                    {
                        Sprite sp = sprites[si];
                        if (sp == null) continue;
                        float ppu = Mathf.Max(sp.pixelsPerUnit, 1f);
                        float worldH = sp.rect.height / ppu;
                        if (worldH > maxEdgeH)
                            maxEdgeH = worldH;
                    }
                }
            }

            if (maxEdgeH > 1e-3f)
                cachedEdgeSpriteWorldHeight = maxEdgeH;

            // Prefer the larger of measured overshoot and edge-sprite thickness as the h=1 budget.
            cachedAuthoredEdgeOvershoot = Mathf.Max(cachedAuthoredEdgeOvershoot, cachedEdgeSpriteWorldHeight * 0.65f);
        }

        private void SetAuthoringIceVisible(bool visible)
        {
            if (spriteShapeRenderer == null) return;

            // When hideIceWhenWater is false we still hide authoring visuals while generated ice is shown.
            if (usingGeneratedIce && !visible)
            {
                spriteShapeRenderer.enabled = false;
                return;
            }

            if (hideIceWhenWater)
                spriteShapeRenderer.enabled = visible;
            else if (visible)
                spriteShapeRenderer.enabled = true;
        }

        private void SetManagedCollidersEnabled(bool enabled)
        {
            managedColliders.RemoveAll(c => c == null);
            if (iceCollider != null && !managedColliders.Contains(iceCollider))
                managedColliders.Add(iceCollider);

            if (!disableColliderWhenWater && !enabled)
            {
                // Still must disable for fluid freedom when melting; flag only controls authoring intent.
            }

            for (int i = 0; i < managedColliders.Count; i++)
            {
                if (managedColliders[i] != null)
                    managedColliders[i].enabled = enabled;
            }
        }

        private void ExcludeManagedCollidersFromObstacles()
        {
            var obs = GetObstacleManager();
            if (obs == null) return;
            managedColliders.RemoveAll(c => c == null);
            if (iceCollider != null && !managedColliders.Contains(iceCollider))
                managedColliders.Add(iceCollider);
            obs.ExcludeColliders(managedColliders);
        }

        private void IncludeManagedCollidersInObstacles()
        {
            var obs = GetObstacleManager();
            if (obs == null) return;
            managedColliders.RemoveAll(c => c == null);
            if (iceCollider != null && !managedColliders.Contains(iceCollider))
                managedColliders.Add(iceCollider);
            obs.IncludeColliders(managedColliders);
        }

        private void RefreshFluidObstacles()
        {
            var obs = GetObstacleManager();
            if (obs != null)
                obs.RefreshObstacles();
        }

        private Fluid2DObstacleManager GetObstacleManager()
        {
            if (fluidSystem != null && fluidSystem.obstacleManager != null)
                return fluidSystem.obstacleManager;
            return Object.FindFirstObjectByType<Fluid2DObstacleManager>();
        }

        private void ResolveReferences()
        {
            if (spriteShapeRenderer == null)
                spriteShapeRenderer = GetComponent<SpriteShapeRenderer>();

            if (spriteShapeController == null)
                spriteShapeController = GetComponent<SpriteShapeController>();

            if (iceCollider == null)
                iceCollider = GetComponent<Collider2D>();

            if (fluidSystem == null)
                fluidSystem = Object.FindFirstObjectByType<Fluid2DSystem>();

            if (iceCollider != null && !managedColliders.Contains(iceCollider))
                managedColliders.Add(iceCollider);
        }

        private void EnsureIceCollider()
        {
            if (iceCollider != null)
            {
                if (!managedColliders.Contains(iceCollider))
                    managedColliders.Add(iceCollider);
                return;
            }

            iceCollider = GetComponent<Collider2D>();
            if (iceCollider != null)
            {
                managedColliders.Add(iceCollider);
                return;
            }

            // Conservative fallback: solid box from SpriteShapeRenderer world bounds.
            Bounds worldBounds = GetRegionBounds();
            var box = gameObject.AddComponent<BoxCollider2D>();

            Vector3 lossy = transform.lossyScale;
            float sx = Mathf.Abs(lossy.x) < 1e-5f ? 1f : Mathf.Abs(lossy.x);
            float sy = Mathf.Abs(lossy.y) < 1e-5f ? 1f : Mathf.Abs(lossy.y);

            box.offset = transform.InverseTransformPoint(worldBounds.center);
            box.size = new Vector2(worldBounds.size.x / sx, worldBounds.size.y / sy);
            box.isTrigger = false;

            iceCollider = box;
            createdFallbackCollider = true;
            managedColliders.Add(iceCollider);
        }

        private Bounds GetRegionBounds()
        {
            if (activeIcePolygonsWorld.Count > 0)
            {
                Bounds b = BoundsOfPolygon(activeIcePolygonsWorld[0]);
                for (int i = 1; i < activeIcePolygonsWorld.Count; i++)
                    b.Encapsulate(BoundsOfPolygon(activeIcePolygonsWorld[i]));
                return b;
            }

            if (spriteShapeRenderer != null)
            {
                Bounds b = spriteShapeRenderer.bounds;
                if (b.size.sqrMagnitude > 1e-6f)
                    return b;
            }

            if (iceCollider != null)
            {
                Bounds b = iceCollider.bounds;
                if (b.size.sqrMagnitude > 1e-6f)
                    return b;
            }

            return new Bounds(transform.position, Vector3.one);
        }

        private static Bounds BoundsOfPolygon(List<Vector2> poly)
        {
            Bounds b = new Bounds(poly[0], Vector3.zero);
            for (int i = 1; i < poly.Count; i++)
                b.Encapsulate(poly[i]);
            return b;
        }

        private static Vector2 PolygonCentroid(List<Vector2> poly)
        {
            float a = 0f;
            float cx = 0f;
            float cy = 0f;
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 p0 = poly[i];
                Vector2 p1 = poly[(i + 1) % poly.Count];
                float cross = p0.x * p1.y - p1.x * p0.y;
                a += cross;
                cx += (p0.x + p1.x) * cross;
                cy += (p0.y + p1.y) * cross;
            }
            a *= 0.5f;
            if (Mathf.Abs(a) < 1e-8f)
            {
                Vector2 sum = Vector2.zero;
                for (int i = 0; i < poly.Count; i++) sum += poly[i];
                return sum / poly.Count;
            }
            cx /= (6f * a);
            cy /= (6f * a);
            return new Vector2(cx, cy);
        }

        private static float SignedArea(List<Vector2> poly)
        {
            float a = 0f;
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 p0 = poly[i];
                Vector2 p1 = poly[(i + 1) % poly.Count];
                a += p0.x * p1.y - p1.x * p0.y;
            }
            return a * 0.5f;
        }

        private static bool PointInPolygon(Vector2 point, List<Vector2> poly)
        {
            // Ray cast
            bool inside = false;
            int n = poly.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 pi = poly[i];
                Vector2 pj = poly[j];
                if (((pi.y > point.y) != (pj.y > point.y)) &&
                    (point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y + 1e-12f) + pi.x))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>
        /// Closed-loop Ramer–Douglas–Peucker: never treats the ring seam as an open polyline.
        /// Finds two extreme points, runs RDP on both arcs, preserves topology of major lobes.
        /// </summary>
        private static void SimplifyClosedRingInPlace(List<Vector2> points, float epsilon)
        {
            if (points == null || points.Count < 4 || epsilon <= 0f) return;

            int n = points.Count;
            // Find left-most then farthest point to split the ring into two arcs.
            int i0 = 0;
            for (int i = 1; i < n; i++)
            {
                if (points[i].x < points[i0].x - 1e-8f ||
                    (Mathf.Abs(points[i].x - points[i0].x) < 1e-8f && points[i].y < points[i0].y))
                    i0 = i;
            }

            int i1 = i0;
            float maxD = -1f;
            for (int i = 0; i < n; i++)
            {
                float d = (points[i] - points[i0]).sqrMagnitude;
                if (d > maxD)
                {
                    maxD = d;
                    i1 = i;
                }
            }

            if (i0 == i1)
            {
                // Degenerate — keep all.
                return;
            }

            var keep = new bool[n];
            keep[i0] = true;
            keep[i1] = true;

            RdpClosedArc(points, i0, i1, epsilon, keep);
            RdpClosedArc(points, i1, i0, epsilon, keep);

            var simplified = new List<Vector2>(n);
            // Walk from i0 around the ring in order.
            int idx = i0;
            for (int step = 0; step < n; step++)
            {
                if (keep[idx])
                    simplified.Add(points[idx]);
                idx = (idx + 1) % n;
            }

            if (simplified.Count >= 3)
            {
                points.Clear();
                points.AddRange(simplified);
            }
        }

        private static void RdpClosedArc(List<Vector2> pts, int first, int last, float eps, bool[] keep)
        {
            int n = pts.Count;
            // Collect indices along the arc first -> last (exclusive of last for distance scan).
            var arc = new List<int>(n);
            int cur = first;
            arc.Add(cur);
            int guard = n + 2;
            while (guard-- > 0)
            {
                cur = (cur + 1) % n;
                arc.Add(cur);
                if (cur == last) break;
            }

            if (arc.Count <= 2) return;

            RdpArcIndices(pts, arc, 0, arc.Count - 1, eps, keep);
        }

        private static void RdpArcIndices(List<Vector2> pts, List<int> arc, int a, int b, float eps, bool[] keep)
        {
            if (b <= a + 1) return;
            float maxDist = 0f;
            int index = -1;
            Vector2 pa = pts[arc[a]];
            Vector2 pb = pts[arc[b]];
            for (int i = a + 1; i < b; i++)
            {
                float d = PerpDistance(pts[arc[i]], pa, pb);
                if (d > maxDist)
                {
                    maxDist = d;
                    index = i;
                }
            }

            if (maxDist > eps && index >= 0)
            {
                keep[arc[index]] = true;
                RdpArcIndices(pts, arc, a, index, eps, keep);
                RdpArcIndices(pts, arc, index, b, eps, keep);
            }
        }

        private static float PerpDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-12f) return (p - a).magnitude;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            Vector2 proj = a + t * ab;
            return (p - proj).magnitude;
        }

        private static void EnsureMinimumPolygonThickness(List<Vector2> poly, float minThickness)
        {
            if (poly == null || poly.Count < 3 || minThickness <= 0f) return;
            Bounds b = BoundsOfPolygon(poly);
            if (b.size.y >= minThickness && b.size.x >= minThickness) return;
            Vector2 c = b.center;
            float halfX = Mathf.Max(b.extents.x, minThickness * 0.5f);
            float halfY = Mathf.Max(b.extents.y, minThickness * 0.5f);
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 d = poly[i] - (Vector2)c;
                float nx = d.x == 0f ? 0f : Mathf.Sign(d.x) * Mathf.Max(Mathf.Abs(d.x), halfX * 0.35f);
                float ny = d.y == 0f ? 0f : Mathf.Sign(d.y) * Mathf.Max(Mathf.Abs(d.y), halfY * 0.35f);
                if (Mathf.Abs(d.x) < halfX)
                    nx = d.x + Mathf.Sign(d.x == 0f ? (i % 2 == 0 ? 1f : -1f) : d.x) * (halfX - Mathf.Abs(d.x)) * 0.5f;
                if (Mathf.Abs(d.y) < halfY)
                    ny = d.y + Mathf.Sign(d.y == 0f ? 1f : d.y) * (halfY - Mathf.Abs(d.y)) * 0.5f;
                poly[i] = (Vector2)c + new Vector2(nx, ny);
            }
        }

        #region Contour safety (self-intersect / bounds clamp / simple repair)

        private static Bounds BuildClusterSilhouetteBounds(
            List<Vector2> clusterPts,
            float stampWorld,
            Vector2[] labelParticleSum,
            int[] labelParticleCounts,
            int label,
            int[] labels, int width, int height,
            float originX, float originY, float cell)
        {
            if (clusterPts != null && clusterPts.Count > 0)
            {
                Bounds b = BoundsOfPoints(clusterPts);
                b.Expand(stampWorld * 2f);
                return b;
            }

            Vector2 center;
            if (labelParticleCounts != null && label > 0 && label < labelParticleCounts.Length &&
                labelParticleCounts[label] > 0)
                center = labelParticleSum[label] / labelParticleCounts[label];
            else
                center = ClusterCellCentroid(labels, label, width, height, originX, originY, cell);

            float r = Mathf.Max(stampWorld, cell) * 2f;
            return new Bounds(center, new Vector3(r, r, 0f));
        }

        /// <summary>
        /// Validate contour for self-intersection, zero/tiny area, and bounds/area inflation vs particle silhouette.
        /// Elongated pools: ribbon / soft-clamp first; convex hull only for true self-intersection; avoid circle collapse.
        /// Compact / star shapes keep starburst safeguards (hull → radial envelope → angular → circle).
        /// </summary>
        private List<Vector2> ValidateAndRepairGeneratedContour(
            List<Vector2> contour,
            List<Vector2> clusterPts,
            Bounds clusterSilhouette,
            float stampWorld,
            float expandPad,
            float cell,
            List<string> safetyReasons)
        {
            bool elongated = IsElongatedPointSet(clusterPts, ElongatedAspectThreshold) ||
                             IsElongatedBounds(clusterSilhouette, ElongatedAspectThreshold);

            if (contour == null || contour.Count < 3)
            {
                List<Vector2> cov = BuildClusterCoverageContour(
                    clusterPts, clusterSilhouette, stampWorld, cell, minFrozenBlobRadius, safetyReasons);
                if (cov != null && cov.Count >= 3)
                    return cov;
                Vector2 c = clusterSilhouette.center;
                float r = Mathf.Max(minFrozenBlobRadius, stampWorld, cell);
                lastSimpleContourRepairs++;
                safetyReasons?.Add("degenerate→circle");
                return MakeCirclePolygon(c, r, 10);
            }

            if (SignedArea(contour) < 0f)
                contour.Reverse();

            bool badIntersect = PolygonSelfIntersects(contour);
            bool badArea = Mathf.Abs(SignedArea(contour)) < 1e-6f;
            bool badBounds = IsContourInflated(contour, clusterSilhouette, maxIceBoundsInflation, maxIceAreaInflation);

            if (!badIntersect && !badArea && !badBounds)
            {
                // Soft clamp still applied when slightly large but under hard fail — keeps mesh tight.
                // Independent-axis clamp preserves long-thin aspect (uniform min-scale crushed ribbons).
                if (SoftClampPolygonToReference(contour, clusterSilhouette, maxIceBoundsInflation))
                {
                    lastBoundsClamps++;
                    safetyReasons?.Add("soft-bounds-clamp");
                }
                return contour;
            }

            if (badIntersect)
            {
                lastSelfIntersectRepairs++;
                safetyReasons?.Add("self-intersect");
            }
            if (badBounds)
                safetyReasons?.Add("bounds/area-inflate");
            if (badArea)
                safetyReasons?.Add("zero-area");

            // Bounds/area inflate alone on a clean elongated ring: soft-clamp / ribbon, do not hull-collapse.
            if (!badIntersect && elongated && badBounds)
            {
                if (SoftClampPolygonToReference(contour, clusterSilhouette, maxIceBoundsInflation))
                {
                    lastBoundsClamps++;
                    safetyReasons?.Add("elongated-soft-clamp");
                }
                if (!PolygonSelfIntersects(contour) &&
                    !IsContourInflated(contour, clusterSilhouette, maxIceBoundsInflation * 1.2f, maxIceAreaInflation * 1.35f))
                    return contour;

                List<Vector2> ribbon = TryBuildRibbonFromCluster(
                    clusterPts, clusterSilhouette, stampWorld, minFrozenBlobRadius, safetyReasons);
                if (ribbon != null)
                    return ribbon;
            }

            List<Vector2> repaired = RepairToSimpleContour(
                contour, clusterPts, minFrozenBlobRadius, stampWorld, safetyReasons,
                preferRibbonForElongated: elongated || badIntersect);

            if (expandPad > 0f && repaired != null && repaired.Count >= 3)
            {
                // Re-apply a reduced pad on repaired simple shapes (full pad already inflated the bad ring).
                OffsetPolygonOutward(repaired, Mathf.Min(expandPad, stampWorld * 0.35f + cell * 0.25f));
                if (PolygonSelfIntersects(repaired))
                {
                    // Drop pad if it reintroduces crossings.
                    repaired = RepairToSimpleContour(
                        contour, clusterPts, minFrozenBlobRadius, stampWorld, null,
                        preferRibbonForElongated: elongated);
                }
            }

            if (repaired == null || repaired.Count < 3 || Mathf.Abs(SignedArea(repaired)) < 1e-6f)
            {
                repaired = BuildClusterCoverageContour(
                    clusterPts, clusterSilhouette, stampWorld, cell, minFrozenBlobRadius, safetyReasons);
            }

            if (repaired == null || repaired.Count < 3 || Mathf.Abs(SignedArea(repaired)) < 1e-6f)
            {
                Vector2 c = clusterSilhouette.center;
                float r = Mathf.Max(minFrozenBlobRadius, stampWorld, cell);
                if (clusterPts != null && clusterPts.Count > 0)
                {
                    float maxD = 0f;
                    for (int i = 0; i < clusterPts.Count; i++)
                        maxD = Mathf.Max(maxD, (clusterPts[i] - (Vector2)c).magnitude);
                    r = Mathf.Max(r, maxD + stampWorld * 0.5f);
                }
                repaired = MakeCirclePolygon(c, r, 10);
                lastSimpleContourRepairs++;
                safetyReasons?.Add("fallback-circle");
            }

            if (SignedArea(repaired) < 0f)
                repaired.Reverse();

            if (SoftClampPolygonToReference(repaired, clusterSilhouette, maxIceBoundsInflation))
            {
                lastBoundsClamps++;
                safetyReasons?.Add("post-repair-clamp");
            }

            // Guarantee cluster particles remain covered after repair (bounded, no runaway expand).
            if (clusterPts != null)
            {
                for (int i = 0; i < clusterPts.Count; i++)
                {
                    if (PointInPolygon(clusterPts[i], repaired)) continue;
                    OffsetPolygonOutward(repaired, Mathf.Max(cell * 0.2f, stampWorld * 0.1f));
                    if (IsContourInflated(repaired, clusterSilhouette, maxIceBoundsInflation * 1.15f, maxIceAreaInflation * 1.25f))
                    {
                        SoftClampPolygonToReference(repaired, clusterSilhouette, maxIceBoundsInflation);
                        break;
                    }
                }
            }

            return repaired;
        }

        private List<Vector2> RepairToSimpleContour(
            List<Vector2> badContour,
            List<Vector2> clusterPts,
            float minRadius,
            float stampWorld,
            List<string> safetyReasons,
            bool preferRibbonForElongated = true)
        {
            bool elongated = preferRibbonForElongated &&
                             (IsElongatedPointSet(clusterPts, ElongatedAspectThreshold) ||
                              IsElongatedPointSet(badContour, ElongatedAspectThreshold));

            // Elongated: oriented ribbon first (preserves length). Convex hull only if ribbon fails
            // and we still need a simple covering polygon (true self-intersect residual).
            if (elongated)
            {
                List<Vector2> ribbon = TryBuildRibbonFromCluster(
                    clusterPts != null && clusterPts.Count >= 2 ? clusterPts : badContour,
                    default,
                    stampWorld,
                    minRadius,
                    safetyReasons);
                if (ribbon != null && ribbon.Count >= 3 && !PolygonSelfIntersects(ribbon))
                    return ribbon;

                // OBB strip from AABB of samples (axis-aligned fallback still keeps length).
                List<Vector2> strip = BuildAxisAlignedStripContour(
                    clusterPts != null && clusterPts.Count >= 2 ? clusterPts : badContour,
                    Mathf.Max(stampWorld * 0.85f, minRadius));
                if (strip != null && strip.Count >= 3 && !PolygonSelfIntersects(strip))
                {
                    lastSimpleContourRepairs++;
                    lastUsedRibbonFallback = true;
                    if (string.IsNullOrEmpty(lastFallbackReason))
                        lastFallbackReason = "aabb_strip";
                    safetyReasons?.Add("aabb-strip");
                    return strip;
                }
            }

            // Compact / star / residual self-intersect: convex hull (starburst safeguard).
            // Also used for elongated only after ribbon/strip failed.
            List<Vector2> hullSrc = (clusterPts != null && clusterPts.Count >= 2) ? clusterPts : badContour;
            if (hullSrc != null && hullSrc.Count >= 2)
            {
                // Include both samples and contour verts so hull covers the visual stamp.
                if (clusterPts != null && badContour != null && clusterPts.Count >= 2 && badContour.Count >= 3)
                {
                    hullSrc = new List<Vector2>(clusterPts.Count + badContour.Count);
                    hullSrc.AddRange(clusterPts);
                    hullSrc.AddRange(badContour);
                }

                List<Vector2> hull = ComputeConvexHull(hullSrc);
                if (hull != null && hull.Count >= 3)
                {
                    if (SignedArea(hull) < 0f) hull.Reverse();
                    OffsetPolygonOutward(hull, Mathf.Max(stampWorld * 0.55f, minRadius * 0.35f));
                    if (!PolygonSelfIntersects(hull) && Mathf.Abs(SignedArea(hull)) > 1e-6f)
                    {
                        lastHullRepairs++;
                        lastUsedConvexHullFallback = true;
                        if (string.IsNullOrEmpty(lastFallbackReason))
                            lastFallbackReason = "contour_hull_repair";
                        safetyReasons?.Add("convex-hull");
                        return hull;
                    }
                }
            }

            // Radial outer envelope collapses stars; skip for elongated (isotropic sectors crush ribbons).
            if (!elongated)
            {
                List<Vector2> envelope = BuildRadialOuterEnvelope(
                    clusterPts != null && clusterPts.Count >= 3 ? clusterPts : badContour,
                    sectorCount: 16,
                    minRadius);
                if (envelope != null && envelope.Count >= 3 && !PolygonSelfIntersects(envelope) &&
                    Mathf.Abs(SignedArea(envelope)) > 1e-6f)
                {
                    OffsetPolygonOutward(envelope, Mathf.Max(stampWorld * 0.4f, minRadius * 0.25f));
                    if (!PolygonSelfIntersects(envelope))
                    {
                        lastSimpleContourRepairs++;
                        safetyReasons?.Add("radial-envelope");
                        return envelope;
                    }
                }
            }

            // Angular-ordered ring (works for unordered clouds, not for stars).
            List<Vector2> simple = BuildAngularOrderedContour(badContour, minRadius);
            if (simple != null && simple.Count >= 3 && !PolygonSelfIntersects(simple) &&
                Mathf.Abs(SignedArea(simple)) > 1e-6f)
            {
                lastSimpleContourRepairs++;
                safetyReasons?.Add("angular-simple");
                return simple;
            }

            // Circle at centroid — expand to cover all cluster points (never a tiny center blob).
            Vector2 c = badContour != null && badContour.Count > 0
                ? PolygonCentroid(badContour)
                : (clusterPts != null && clusterPts.Count > 0 ? clusterPts[0] : Vector2.zero);
            float r = Mathf.Max(minRadius, stampWorld * 0.85f);
            if (clusterPts != null && clusterPts.Count > 0)
            {
                float maxD = 0f;
                for (int i = 0; i < clusterPts.Count; i++)
                    maxD = Mathf.Max(maxD, (clusterPts[i] - c).magnitude);
                r = Mathf.Max(r, maxD + stampWorld * 0.5f);
            }
            lastSimpleContourRepairs++;
            safetyReasons?.Add("repair-circle");
            return MakeCirclePolygon(c, r, 12);
        }

        /// <summary>
        /// Coverage contour when occupancy extract fails: ribbon for elongated, hull for multi-point compact, circle for single drop.
        /// Never returns a tiny centroid circle when particle span exceeds min radius.
        /// </summary>
        private List<Vector2> BuildClusterCoverageContour(
            List<Vector2> clusterPts,
            Bounds clusterSilhouette,
            float stampWorld,
            float cell,
            float minRadius,
            List<string> safetyReasons)
        {
            if (clusterPts != null && clusterPts.Count >= 2 &&
                (IsElongatedPointSet(clusterPts, ElongatedAspectThreshold) ||
                 IsElongatedBounds(clusterSilhouette, ElongatedAspectThreshold)))
            {
                List<Vector2> ribbon = TryBuildRibbonFromCluster(
                    clusterPts, clusterSilhouette, stampWorld, minRadius, safetyReasons);
                if (ribbon != null)
                    return ribbon;

                List<Vector2> strip = BuildAxisAlignedStripContour(
                    clusterPts, Mathf.Max(stampWorld * 0.85f, minRadius));
                if (strip != null && strip.Count >= 3)
                {
                    lastUsedRibbonFallback = true;
                    lastSimpleContourRepairs++;
                    if (string.IsNullOrEmpty(lastFallbackReason))
                        lastFallbackReason = "aabb_strip";
                    safetyReasons?.Add("coverage-aabb-strip");
                    return strip;
                }
            }

            if (clusterPts != null && clusterPts.Count >= 3)
            {
                List<Vector2> hull = ComputeConvexHull(clusterPts);
                if (hull != null && hull.Count >= 3)
                {
                    if (SignedArea(hull) < 0f) hull.Reverse();
                    OffsetPolygonOutward(hull, Mathf.Max(stampWorld * 0.55f, minRadius * 0.35f, cell * 0.5f));
                    if (!PolygonSelfIntersects(hull) && Mathf.Abs(SignedArea(hull)) > 1e-6f)
                    {
                        lastHullRepairs++;
                        lastUsedConvexHullFallback = true;
                        if (string.IsNullOrEmpty(lastFallbackReason))
                            lastFallbackReason = "coverage_hull";
                        safetyReasons?.Add("coverage-hull");
                        return hull;
                    }
                }
            }

            Vector2 center = clusterSilhouette.size.sqrMagnitude > 1e-10f
                ? (Vector2)clusterSilhouette.center
                : (clusterPts != null && clusterPts.Count > 0 ? clusterPts[0] : Vector2.zero);
            float r = Mathf.Max(minRadius, stampWorld * 0.85f, cell * 0.75f);
            if (clusterPts != null && clusterPts.Count > 0)
            {
                float maxD = 0f;
                for (int i = 0; i < clusterPts.Count; i++)
                    maxD = Mathf.Max(maxD, (clusterPts[i] - center).magnitude);
                r = Mathf.Max(r, maxD + stampWorld * 0.5f);
            }
            else if (clusterSilhouette.size.sqrMagnitude > 1e-10f)
            {
                r = Mathf.Max(r, 0.5f * Mathf.Max(clusterSilhouette.size.x, clusterSilhouette.size.y));
            }
            lastSimpleContourRepairs++;
            safetyReasons?.Add("coverage-circle");
            return MakeCirclePolygon(center, r, 12);
        }

        private List<Vector2> TryBuildRibbonFromCluster(
            List<Vector2> pts,
            Bounds silhouette,
            float stampWorld,
            float minRadius,
            List<string> safetyReasons)
        {
            if (pts == null || pts.Count < 2)
                return null;

            float halfW = EstimateRibbonHalfWidth(pts, Mathf.Max(stampWorld * 0.75f, minRadius));
            if (silhouette.size.sqrMagnitude > 1e-10f)
            {
                float silHalf = 0.5f * Mathf.Min(silhouette.size.x, silhouette.size.y);
                if (silHalf > 1e-4f)
                    halfW = Mathf.Max(halfW, silHalf * 0.45f);
            }

            List<Vector2> ribbon = BuildOrientedRibbonContour(pts, halfW, stampWorld * 0.35f);
            if (ribbon == null || ribbon.Count < 3 || PolygonSelfIntersects(ribbon) ||
                Mathf.Abs(SignedArea(ribbon)) < 1e-6f)
                return null;

            lastUsedRibbonFallback = true;
            lastSimpleContourRepairs++;
            if (string.IsNullOrEmpty(lastFallbackReason))
                lastFallbackReason = "oriented_ribbon";
            safetyReasons?.Add("oriented-ribbon");
            return ribbon;
        }

        /// <summary>
        /// Insert intermediate stamp centers along sparse elongated chains so freeze occupancy
        /// spans the full rendered pool length (metaballs look continuous between samples).
        /// Original particle list is not mutated; mass remelt still uses live samples only.
        /// </summary>
        private static List<Vector2> DensifySparseRibbonSamples(List<Vector2> positions, float stampWorld)
        {
            if (positions == null || positions.Count == 0)
                return new List<Vector2>();
            if (positions.Count < 3 || !IsElongatedPointSet(positions, ElongatedAspectThreshold))
                return new List<Vector2>(positions);

            // Principal axis via longest AABB edge + PCA-ish covariance of offsets.
            Bounds b = BoundsOfPoints(positions);
            Vector2 major = b.size.x >= b.size.y ? Vector2.right : Vector2.up;
            // Refine major from covariance for diagonal ribbons.
            Vector2 mean = Vector2.zero;
            for (int i = 0; i < positions.Count; i++)
                mean += positions[i];
            mean /= positions.Count;
            float sxx = 0f, sxy = 0f, syy = 0f;
            for (int i = 0; i < positions.Count; i++)
            {
                Vector2 d = positions[i] - mean;
                sxx += d.x * d.x;
                sxy += d.x * d.y;
                syy += d.y * d.y;
            }
            // Dominant eigenvector of 2x2 covariance (analytic).
            float trace = sxx + syy;
            float det = sxx * syy - sxy * sxy;
            float disc = Mathf.Max(0f, trace * trace * 0.25f - det);
            float lambda = trace * 0.5f + Mathf.Sqrt(disc);
            Vector2 eig = Mathf.Abs(sxy) > 1e-8f
                ? new Vector2(lambda - syy, sxy)
                : (sxx >= syy ? Vector2.right : Vector2.up);
            if (eig.sqrMagnitude > 1e-10f)
                major = eig.normalized;

            // Sort by projection on major axis.
            var ordered = new List<Vector2>(positions);
            ordered.Sort((a, b2) =>
            {
                float pa = Vector2.Dot(a, major);
                float pb = Vector2.Dot(b2, major);
                return pa.CompareTo(pb);
            });

            float maxGap = Mathf.Max(stampWorld * 1.35f, 0.08f);
            var densified = new List<Vector2>(ordered.Count * 2);
            densified.Add(ordered[0]);
            for (int i = 1; i < ordered.Count; i++)
            {
                Vector2 a = ordered[i - 1];
                Vector2 b2 = ordered[i];
                float dist = Vector2.Distance(a, b2);
                if (dist > maxGap)
                {
                    int steps = Mathf.Clamp(Mathf.CeilToInt(dist / maxGap), 1, 24);
                    for (int s = 1; s < steps; s++)
                        densified.Add(Vector2.Lerp(a, b2, s / (float)steps));
                }
                densified.Add(b2);
            }
            return densified;
        }

        private static int CountOutOfGridSamples(
            List<Vector2> positions, int width, int height, float originX, float originY, float cell)
        {
            if (positions == null) return 0;
            int miss = 0;
            for (int i = 0; i < positions.Count; i++)
            {
                int cx = Mathf.FloorToInt((positions[i].x - originX) / cell);
                int cy = Mathf.FloorToInt((positions[i].y - originY) / cell);
                if ((uint)cx >= (uint)width || (uint)cy >= (uint)height)
                    miss++;
            }
            return miss;
        }

        private static bool IsElongatedPointSet(IList<Vector2> pts, float threshold)
        {
            if (pts == null || pts.Count < 2) return false;
            Bounds b = new Bounds(pts[0], Vector3.zero);
            for (int i = 1; i < pts.Count; i++)
                b.Encapsulate(pts[i]);
            return IsElongatedBounds(b, threshold);
        }

        private static bool IsElongatedBounds(Bounds b, float threshold)
        {
            float minD = Mathf.Min(b.size.x, b.size.y);
            float maxD = Mathf.Max(b.size.x, b.size.y);
            if (maxD < 1e-5f) return false;
            if (minD < 1e-5f) return maxD > 1e-4f;
            return (maxD / minD) >= Mathf.Max(1.5f, threshold);
        }

        /// <summary>
        /// Oriented ribbon (stadium-like OBB) along the principal axis of the point set.
        /// Preserves length of long thin water; halfWidth covers stamp/visual thickness.
        /// </summary>
        private static List<Vector2> BuildOrientedRibbonContour(
            List<Vector2> points, float halfWidth, float endPad)
        {
            if (points == null || points.Count == 0) return null;
            halfWidth = Mathf.Max(MinBlobWorldRadius * 0.5f, halfWidth);
            endPad = Mathf.Max(0f, endPad);

            Vector2 mean = Vector2.zero;
            for (int i = 0; i < points.Count; i++)
                mean += points[i];
            mean /= points.Count;

            float sxx = 0f, sxy = 0f, syy = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 d = points[i] - mean;
                sxx += d.x * d.x;
                sxy += d.x * d.y;
                syy += d.y * d.y;
            }

            Vector2 major;
            if (Mathf.Abs(sxy) > 1e-10f || Mathf.Abs(sxx - syy) > 1e-10f)
            {
                float trace = sxx + syy;
                float det = sxx * syy - sxy * sxy;
                float disc = Mathf.Max(0f, trace * trace * 0.25f - det);
                float lambda = trace * 0.5f + Mathf.Sqrt(disc);
                major = Mathf.Abs(sxy) > 1e-10f
                    ? new Vector2(lambda - syy, sxy)
                    : (sxx >= syy ? Vector2.right : Vector2.up);
                if (major.sqrMagnitude < 1e-12f)
                    major = Vector2.right;
                else
                    major.Normalize();
            }
            else
            {
                Bounds bb = BoundsOfPoints(points);
                major = bb.size.x >= bb.size.y ? Vector2.right : Vector2.up;
            }

            Vector2 minor = new Vector2(-major.y, major.x);
            float minP = float.PositiveInfinity, maxP = float.NegativeInfinity;
            float maxAbsMinor = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 d = points[i] - mean;
                float p = Vector2.Dot(d, major);
                float m = Vector2.Dot(d, minor);
                if (p < minP) minP = p;
                if (p > maxP) maxP = p;
                maxAbsMinor = Mathf.Max(maxAbsMinor, Mathf.Abs(m));
            }

            halfWidth = Mathf.Max(halfWidth, maxAbsMinor + halfWidth * 0.15f);
            minP -= endPad;
            maxP += endPad;
            if (maxP - minP < halfWidth * 2f)
            {
                float mid = 0.5f * (minP + maxP);
                minP = mid - halfWidth;
                maxP = mid + halfWidth;
            }

            // Stadium polygon: rectangular body + semi-disk tips (SpriteShape-friendly, non-crossing).
            const int capSides = 6;
            Vector2 p0 = mean + major * minP;
            Vector2 p1 = mean + major * maxP;
            var poly = new List<Vector2>(capSides * 2 + 4);

            // Lower edge min → max, then right semicircle (-minor → +major → +minor),
            // upper edge max → min, then left semicircle (+minor → -major → -minor).
            poly.Add(p0 - minor * halfWidth);
            poly.Add(p1 - minor * halfWidth);
            for (int i = 1; i < capSides; i++)
            {
                float a = Mathf.PI * (i / (float)capSides); // 0 → π
                Vector2 dir = (-minor * Mathf.Cos(a) + major * Mathf.Sin(a));
                poly.Add(p1 + dir * halfWidth);
            }
            poly.Add(p1 + minor * halfWidth);
            poly.Add(p0 + minor * halfWidth);
            for (int i = 1; i < capSides; i++)
            {
                float a = Mathf.PI * (i / (float)capSides); // 0 → π
                Vector2 dir = (minor * Mathf.Cos(a) - major * Mathf.Sin(a));
                poly.Add(p0 + dir * halfWidth);
            }

            RemoveDuplicateRingPoints(poly);
            EnforceMinRingSpacing(poly, MinSplinePointDistance);
            if (poly.Count < 3) return null;
            if (SignedArea(poly) < 0f) poly.Reverse();
            return poly;
        }

        private static float EstimateRibbonHalfWidth(List<Vector2> points, float minHalf)
        {
            if (points == null || points.Count == 0)
                return Mathf.Max(MinBlobWorldRadius, minHalf);

            Bounds b = BoundsOfPoints(points);
            float thin = 0.5f * Mathf.Min(b.size.x, b.size.y);
            // At least minHalf; if points are collinear thin≈0, keep minHalf (stamp thickness).
            return Mathf.Max(minHalf, thin * 0.5f + minHalf * 0.35f, MinBlobWorldRadius * 0.5f);
        }

        private static List<Vector2> BuildAxisAlignedStripContour(List<Vector2> points, float pad)
        {
            if (points == null || points.Count == 0) return null;
            Bounds b = BoundsOfPoints(points);
            pad = Mathf.Max(pad, MinBlobWorldRadius * 0.5f);
            float minX = b.min.x - pad;
            float maxX = b.max.x + pad;
            float minY = b.min.y - pad;
            float maxY = b.max.y + pad;
            // Ensure non-zero thickness on the short axis.
            if (maxX - minX < pad * 2f)
            {
                float c = 0.5f * (minX + maxX);
                minX = c - pad;
                maxX = c + pad;
            }
            if (maxY - minY < pad * 2f)
            {
                float c = 0.5f * (minY + maxY);
                minY = c - pad;
                maxY = c + pad;
            }

            return new List<Vector2>
            {
                new Vector2(minX, minY),
                new Vector2(maxX, minY),
                new Vector2(maxX, maxY),
                new Vector2(minX, maxY)
            };
        }

        /// <summary>
        /// Collapse star-like point sets by keeping the farthest sample in each angle sector (simple outer ring).
        /// </summary>
        private static List<Vector2> BuildRadialOuterEnvelope(List<Vector2> points, int sectorCount, float minRadius)
        {
            if (points == null || points.Count == 0) return null;
            sectorCount = Mathf.Clamp(sectorCount, 8, 32);

            Vector2 c = Vector2.zero;
            for (int i = 0; i < points.Count; i++)
                c += points[i];
            c /= points.Count;

            var best = new Vector2[sectorCount];
            var bestR = new float[sectorCount];
            for (int i = 0; i < sectorCount; i++)
                bestR[i] = -1f;

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 d = points[i] - c;
                float r = d.magnitude;
                if (r < 1e-8f) continue;
                float ang = Mathf.Atan2(d.y, d.x);
                if (ang < 0f) ang += Mathf.PI * 2f;
                int sec = Mathf.Clamp((int)(ang / (Mathf.PI * 2f) * sectorCount), 0, sectorCount - 1);
                if (r > bestR[sec])
                {
                    bestR[sec] = r;
                    best[sec] = points[i];
                }
            }

            var ring = new List<Vector2>(sectorCount);
            for (int i = 0; i < sectorCount; i++)
            {
                if (bestR[i] > 0f)
                    ring.Add(best[i]);
            }

            if (ring.Count < 3)
            {
                float r = minRadius;
                for (int i = 0; i < points.Count; i++)
                    r = Mathf.Max(r, (points[i] - c).magnitude);
                return MakeCirclePolygon(c, Mathf.Max(r, minRadius), Mathf.Max(10, sectorCount / 2));
            }

            if (SignedArea(ring) < 0f)
                ring.Reverse();
            RemoveDuplicateRingPoints(ring);
            EnforceMinRingSpacing(ring, MinSplinePointDistance);
            return ring.Count >= 3 ? ring : null;
        }

        private static List<Vector2> BuildAngularOrderedContour(List<Vector2> points, float minRadius)
        {
            if (points == null || points.Count == 0) return null;

            // Centroid of unique-ish points.
            Vector2 c = Vector2.zero;
            int n = 0;
            for (int i = 0; i < points.Count; i++)
            {
                c += points[i];
                n++;
            }
            if (n == 0) return null;
            c /= n;

            // Dedupe near-identical points, keep farthest along each coarse angle bucket + full sort.
            var pts = new List<Vector2>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p = points[i];
                bool dup = false;
                for (int j = 0; j < pts.Count; j++)
                {
                    if ((pts[j] - p).sqrMagnitude < 1e-8f)
                    {
                        dup = true;
                        break;
                    }
                }
                if (!dup) pts.Add(p);
            }

            if (pts.Count < 3)
            {
                float r = Mathf.Max(minRadius, 0.08f);
                return MakeCirclePolygon(c, r, 10);
            }

            pts.Sort((a, b) =>
            {
                float aa = Mathf.Atan2(a.y - c.y, a.x - c.x);
                float bb = Mathf.Atan2(b.y - c.y, b.x - c.x);
                int cmp = aa.CompareTo(bb);
                if (cmp != 0) return cmp;
                // Same angle: keep farther first (then compact).
                float da = (a - c).sqrMagnitude;
                float db = (b - c).sqrMagnitude;
                return db.CompareTo(da);
            });

            // Collapse same-angle rays to the farthest vertex (star → simple outer ring).
            var ring = new List<Vector2>(pts.Count);
            const float angEps = 1e-3f;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 p = pts[i];
                float ang = Mathf.Atan2(p.y - c.y, p.x - c.x);
                if (ring.Count == 0)
                {
                    ring.Add(p);
                    continue;
                }
                Vector2 last = ring[ring.Count - 1];
                float lastAng = Mathf.Atan2(last.y - c.y, last.x - c.x);
                if (Mathf.Abs(Mathf.DeltaAngle(lastAng * Mathf.Rad2Deg, ang * Mathf.Rad2Deg)) * Mathf.Deg2Rad < angEps)
                {
                    if ((p - c).sqrMagnitude > (last - c).sqrMagnitude)
                        ring[ring.Count - 1] = p;
                }
                else
                {
                    ring.Add(p);
                }
            }

            // Seam collapse first/last same angle.
            if (ring.Count >= 2)
            {
                float a0 = Mathf.Atan2(ring[0].y - c.y, ring[0].x - c.x);
                float a1 = Mathf.Atan2(ring[ring.Count - 1].y - c.y, ring[ring.Count - 1].x - c.x);
                if (Mathf.Abs(Mathf.DeltaAngle(a0 * Mathf.Rad2Deg, a1 * Mathf.Rad2Deg)) * Mathf.Deg2Rad < angEps)
                {
                    if ((ring[ring.Count - 1] - c).sqrMagnitude > (ring[0] - c).sqrMagnitude)
                        ring[0] = ring[ring.Count - 1];
                    ring.RemoveAt(ring.Count - 1);
                }
            }

            if (ring.Count < 3)
            {
                float r = minRadius;
                for (int i = 0; i < pts.Count; i++)
                    r = Mathf.Max(r, (pts[i] - c).magnitude);
                return MakeCirclePolygon(c, Mathf.Max(r, minRadius), 10);
            }

            if (SignedArea(ring) < 0f)
                ring.Reverse();

            RemoveDuplicateRingPoints(ring);
            EnforceMinRingSpacing(ring, MinSplinePointDistance);
            return ring.Count >= 3 ? ring : null;
        }

        private static bool IsContourInflated(
            List<Vector2> poly, Bounds reference, float maxBoundsInflation, float maxAreaInflation)
        {
            if (poly == null || poly.Count < 3) return true;
            if (reference.size.sqrMagnitude < 1e-10f) return false;

            Bounds pb = BoundsOfPolygon(poly);
            float maxInf = Mathf.Max(1.1f, maxBoundsInflation);
            // Allow a small absolute pad so thin stamps aren't flagged.
            float padX = Mathf.Max(reference.size.x * maxInf, reference.size.x + 0.05f);
            float padY = Mathf.Max(reference.size.y * maxInf, reference.size.y + 0.05f);
            if (pb.size.x > padX * 1.02f || pb.size.y > padY * 1.02f)
                return true;

            float refArea = Mathf.Max(1e-6f, reference.size.x * reference.size.y);
            float polyArea = Mathf.Abs(SignedArea(poly));
            if (polyArea > refArea * Mathf.Max(1.2f, maxAreaInflation))
                return true;

            return false;
        }

        /// <summary>
        /// Scale vertices toward centroid if AABB exceeds reference * maxInflation. Returns true if clamped.
        /// Uses independent per-axis scales so long thin ribbons are not crushed by uniform min(scaleX,scaleY).
        /// </summary>
        private static bool SoftClampPolygonToReference(List<Vector2> poly, Bounds reference, float maxInflation)
        {
            if (poly == null || poly.Count < 3 || reference.size.sqrMagnitude < 1e-10f)
                return false;

            Bounds pb = BoundsOfPolygon(poly);
            float maxInf = Mathf.Max(1.1f, maxInflation);
            float limitX = Mathf.Max(reference.size.x * maxInf, 1e-4f);
            float limitY = Mathf.Max(reference.size.y * maxInf, 1e-4f);
            if (pb.size.x <= limitX * 1.01f && pb.size.y <= limitY * 1.01f)
                return false;

            Vector2 c = PolygonCentroid(poly);
            // Prefer reference center when available (keeps ice on the pool).
            if (reference.size.sqrMagnitude > 1e-8f)
                c = Vector2.Lerp(c, (Vector2)reference.center, 0.35f);

            float scaleX = pb.size.x > 1e-6f ? Mathf.Min(1f, limitX / pb.size.x) : 1f;
            float scaleY = pb.size.y > 1e-6f ? Mathf.Min(1f, limitY / pb.size.y) : 1f;
            if (scaleX >= 0.999f && scaleY >= 0.999f)
                return false;

            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 d = poly[i] - c;
                poly[i] = c + new Vector2(d.x * scaleX, d.y * scaleY);
            }

            return true;
        }

        /// <summary>
        /// Proper segment intersection test for non-adjacent edges of a closed ring.
        /// Shared endpoints of adjacent edges are not treated as intersections.
        /// </summary>
        private static bool PolygonSelfIntersects(IList<Vector2> poly)
        {
            if (poly == null || poly.Count < 4) return false;
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a1 = poly[i];
                Vector2 a2 = poly[(i + 1) % n];
                for (int j = i + 1; j < n; j++)
                {
                    // Skip adjacent edges and the same edge.
                    if (j == i) continue;
                    if (j == (i + 1) % n) continue;
                    if (i == (j + 1) % n) continue;
                    // Also skip the wrap-around adjacency (0 and n-1).
                    if (i == 0 && j == n - 1) continue;
                    if (j == 0 && i == n - 1) continue;

                    Vector2 b1 = poly[j];
                    Vector2 b2 = poly[(j + 1) % n];
                    if (SegmentsProperlyIntersect(a1, a2, b1, b2))
                        return true;
                }
            }
            return false;
        }

        private static bool SegmentsProperlyIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
        {
            float d1 = Cross(a1, a2, b1);
            float d2 = Cross(a1, a2, b2);
            float d3 = Cross(b1, b2, a1);
            float d4 = Cross(b1, b2, a2);

            // Proper intersection: endpoints strictly on opposite sides of each segment.
            if (((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f)) &&
                ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f)))
                return true;

            // Collinear overlaps that are more than a shared endpoint also corrupt fill.
            const float eps = 1e-7f;
            if (Mathf.Abs(d1) <= eps && OnSegment(a1, a2, b1) && !NearlyEqual(b1, a1) && !NearlyEqual(b1, a2))
                return true;
            if (Mathf.Abs(d2) <= eps && OnSegment(a1, a2, b2) && !NearlyEqual(b2, a1) && !NearlyEqual(b2, a2))
                return true;
            if (Mathf.Abs(d3) <= eps && OnSegment(b1, b2, a1) && !NearlyEqual(a1, b1) && !NearlyEqual(a1, b2))
                return true;
            if (Mathf.Abs(d4) <= eps && OnSegment(b1, b2, a2) && !NearlyEqual(a2, b1) && !NearlyEqual(a2, b2))
                return true;

            return false;
        }

        private static bool OnSegment(Vector2 a, Vector2 b, Vector2 p)
        {
            return p.x <= Mathf.Max(a.x, b.x) + 1e-6f && p.x + 1e-6f >= Mathf.Min(a.x, b.x) &&
                   p.y <= Mathf.Max(a.y, b.y) + 1e-6f && p.y + 1e-6f >= Mathf.Min(a.y, b.y) &&
                   Mathf.Abs(Cross(a, b, p)) <= 1e-5f;
        }

        private static bool NearlyEqual(Vector2 a, Vector2 b)
        {
            return (a - b).sqrMagnitude <= 1e-10f;
        }

        #endregion

        private static List<Vector2> ComputeConvexHull(List<Vector2> points)
        {
            if (points == null || points.Count == 0) return null;
            var pts = new List<Vector2>(points);
            pts.Sort((a, b) =>
            {
                int cx = a.x.CompareTo(b.x);
                return cx != 0 ? cx : a.y.CompareTo(b.y);
            });

            // Dedup
            for (int i = pts.Count - 2; i >= 0; i--)
            {
                if ((pts[i] - pts[i + 1]).sqrMagnitude < 1e-10f)
                    pts.RemoveAt(i + 1);
            }
            if (pts.Count < 3)
                return pts.Count >= 1 ? new List<Vector2>(pts) : null;

            List<Vector2> lower = new List<Vector2>();
            for (int i = 0; i < pts.Count; i++)
            {
                while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], pts[i]) <= 0f)
                    lower.RemoveAt(lower.Count - 1);
                lower.Add(pts[i]);
            }

            List<Vector2> upper = new List<Vector2>();
            for (int i = pts.Count - 1; i >= 0; i--)
            {
                while (upper.Count >= 2 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], pts[i]) <= 0f)
                    upper.RemoveAt(upper.Count - 1);
                upper.Add(pts[i]);
            }

            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);
            return lower.Count >= 3 ? lower : null;
        }

        private static float Cross(Vector2 o, Vector2 a, Vector2 b)
        {
            return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        }

        #endregion

#if UNITY_EDITOR
        private void OnValidate()
        {
            particleSpacing = Mathf.Max(0.05f, particleSpacing);
            spawnPadding = Mathf.Max(0f, spawnPadding);
            jitter = Mathf.Max(0f, jitter);
            maxParticlesPerToggle = Mathf.Max(1, maxParticlesPerToggle);
            particlesPerUnitArea = Mathf.Max(0f, particlesPerUnitArea);
            freezeGridCellSize = Mathf.Max(0.05f, freezeGridCellSize);
            freezeParticleStampRadius = Mathf.Max(0.25f, freezeParticleStampRadius);
            freezeVisualRadiusScale = Mathf.Clamp(freezeVisualRadiusScale, 1f, 8f);
            freezeStampPadding = Mathf.Max(0f, freezeStampPadding);
            freezeEdgeWidthCompensation = Mathf.Max(0f, freezeEdgeWidthCompensation);
            minBlobCells = Mathf.Max(1, minBlobCells);
            minFrozenBlobRadius = Mathf.Max(0.05f, minFrozenBlobRadius);
            continuousTangentScale = Mathf.Clamp(continuousTangentScale, 0.05f, 0.45f);
            freezeBridgeGapWorld = Mathf.Max(0f, freezeBridgeGapWorld);
            freezeMaxHoleCells = Mathf.Max(0, freezeMaxHoleCells);
            freezeNotchSmoothPasses = Mathf.Clamp(freezeNotchSmoothPasses, 0, 3);
            freezeMergeSatelliteDistance = Mathf.Max(0f, freezeMergeSatelliteDistance);
            freezeSatelliteMaxCells = Mathf.Max(1, freezeSatelliteMaxCells);
            freezeContourSmoothPasses = Mathf.Clamp(freezeContourSmoothPasses, 0, 4);
            maxIceBoundsInflation = Mathf.Clamp(maxIceBoundsInflation, 1.1f, 4f);
            maxIceAreaInflation = Mathf.Clamp(maxIceAreaInflation, 1.2f, 8f);
        }

        private void OnDrawGizmosSelected()
        {
            Bounds b = GetRegionBounds();
            Gizmos.color = currentPhase == Phase.Ice
                ? new Color(0.6f, 0.85f, 1f, 0.35f)
                : new Color(0.2f, 0.5f, 1f, 0.35f);
            Gizmos.DrawWireCube(b.center, b.size);

            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.8f);
            for (int p = 0; p < activeIcePolygonsWorld.Count; p++)
            {
                var poly = activeIcePolygonsWorld[p];
                if (poly == null || poly.Count < 2) continue;
                for (int i = 0; i < poly.Count; i++)
                {
                    Vector2 a = poly[i];
                    Vector2 c = poly[(i + 1) % poly.Count];
                    Gizmos.DrawLine(a, c);
                }
            }
        }
#endif
    }
}
