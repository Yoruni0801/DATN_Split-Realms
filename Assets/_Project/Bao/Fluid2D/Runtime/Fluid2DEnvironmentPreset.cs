using UnityEngine;
using UnityEngine.U2D;

namespace Bao.Fluid2D
{
    /// <summary>
    /// Reusable environment composition preset: simulation settings + look assets + common decisions.
    /// Does not duplicate every low-level Fluid2DSettings field — advanced knobs live on the settings asset.
    /// Applying this preset never mutates imported SpriteShape source assets.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Fluid2DEnvironmentPreset",
        menuName = "Bao/Fluid2D/Environment Preset",
        order = 10)]
    public class Fluid2DEnvironmentPreset : ScriptableObject
    {
        [Header("Simulation")]
        [Tooltip("Shared simulation/render settings asset (advanced knobs live here).")]
        public Fluid2DSettings settings;

        [Header("Look")]
        [Tooltip("Metaball compositing material (project asset; runtime clones are used per system).")]
        public Material waterMetaballMaterial;

        [Tooltip("Particle splat material used by Fluid2DSystem.")]
        public Material splatMaterial;

        [Tooltip("Ice SpriteShape profile reference. Assigned by value; freeze uses a runtime clone.")]
        public SpriteShape iceSpriteShapeProfile;

        [Header("Phase")]
        public IceWaterPhaseToggle2D.Phase initialPhase = IceWaterPhaseToggle2D.Phase.Ice;

        [Header("Quality")]
        [Tooltip("Default quality for Map ice/water scenes. Balanced is recommended for 2D production.")]
        public Fluid2DQualityLevel quality = Fluid2DQualityLevel.Balanced;

        [Header("Layers / Collision")]
        [Tooltip("Static terrain / ice obstacle layers for Fluid2DObstacleManager.")]
        public LayerMask terrainObstacleLayers = ~0;

        [Tooltip("Optional documentation of dynamic interaction layers (Fluid2DObject). Not auto-applied to physics matrix.")]
        public LayerMask dynamicInteractionLayers;

        [Tooltip("Dedicated water particle layer (camera culling).")]
        public int waterLayer = 16;

        [Header("Interaction Defaults")]
        [Min(0.05f)]
        [Tooltip("Preferred melt particle spacing for small/medium ice. Large shapes auto-widen when budget-limited.")]
        public float particleSpacing = 0.16f;

        [Min(1)]
        [Tooltip("Hard cap on particles per melt. Raise for large designer SpriteShapes. Also limited by Fluid2DSettings.maxParticles.")]
        public int maxParticlesPerToggle = 2000;

        [Tooltip("When false (default), melt spreads across the full world polygon and widens spacing if preferred density exceeds budget.")]
        public bool disableAdaptiveMeltSpacing = false;

        [Min(0f)]
        [Tooltip("Optional particles per world-unit². 0 = derive from particleSpacing (≈1/spacing²).")]
        public float particlesPerUnitArea = 0f;

        [Tooltip("Log melt budget/coverage diagnostics when the shape is large for the configured budget.")]
        public bool logMeltDiagnostics = true;

        [Tooltip("Designer notes shown in the controller inspector.")]
        [TextArea(2, 6)]
        public string designerNotes =
            "Compose settings + materials + ice profile. Apply via Fluid2DEnvironmentController or the Setup wizard. " +
            "Large ice: raise maxParticlesPerToggle (and Fluid2DSettings.maxParticles). Melt adapts spacing to keep silhouette coverage.";
    }
}
