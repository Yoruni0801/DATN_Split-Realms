# Bao Fluid2D — Interactive Environment Module

Production module for ice ↔ water regions (Map_IceandSnow style). Designers configure **look + quality + phase**; gameplay drives **Melt / Freeze** without touching GPU buffers.

## 5-minute setup (Map_IceandSnow or any scene)

1. Open your scene (do **not** need to edit `Map_IceandSnow.unity` for the module itself).
2. Select the ice `SpriteShape` object (or nothing for a starter region).
3. Menu: **Tools → Bao → Fluid2D → Environment Water Setup**.
4. Pick preset **Fluid2D_Map_IceandSnow** (V3 glacial look) or **Fluid2D_Starter**.
5. Click **Create New Region** (or **Repair Selected Region**).
6. On the root `Fluid2DEnvironmentController`: **Auto Wire** → **Validate Setup**.
7. Enter Play Mode → inspector **Melt** / **Freeze**, or call the API from scripts/Timeline.

Prefab: `Assets/_Project/Bao/Fluid2D/Prefabs/InteractiveWater2D.prefab`  
Presets: `Assets/_Project/Bao/Fluid2D/Presets/`

Camera may stay empty on the prefab — it auto-wires to `Camera.main` at scene time. Validation reports if missing.

## Change ice profile / water material / preset

| Goal | How |
|------|-----|
| Whole look package | Assign **Preset** on controller → **Apply Preset** (Undo-safe) |
| Ice silhouette style | **Ice SpriteShape Profile** field, or `SetIceSpriteShapeProfile(SpriteShape)` |
| Water paint | **Water Metaball Material**, or `SetWaterMaterial(Material)` |
| Simulation knobs | Edit / swap **Fluid2D Settings** asset (not duplicated on controller) |

Runtime freeze **clones** the SpriteShape profile for generated ice fill. **Source pack assets are never mutated.**

```csharp
var env = GetComponent<Fluid2DEnvironmentController>();
env.SetIceSpriteShapeProfile(otherProfile); // next Freeze uses it
env.SetWaterMaterial(otherMetaballMat);
env.ApplyPreset(mapIceAndSnowPreset);
```

## Trigger Melt / Freeze

### Script

```csharp
env.Melt();
env.Freeze();
env.TogglePhase();
env.SetPhase(IceWaterPhaseToggle2D.Phase.Water);
env.RefreshObstacles();
env.SetQuality(Fluid2DQualityLevel.High);
```

### UnityEvent / Trigger / Timeline

Wire `Fluid2DEnvironmentController` methods:

- `Melt`, `Freeze`, `TogglePhase`, `RefreshObstacles`
- Events (fire **once** per transition): `onPhaseChanging`, `onPhaseChanged`, `onEnteredWater`, `onEnteredIce`

Example: Trigger volume `OnTriggerEnter2D` → `env.Melt()`.  
Timeline Signal → `env.Freeze()`.

### Keyboard

**Off by default** on the production controller (no hard-coded Space). Enable explicitly:

```csharp
env.SetEnableKeyboardToggle(true, KeyCode.F);
```

## Quality (Low / Balanced / High)

| Quality | RT scale | Substeps (runtime override) |
|---------|----------|-----------------------------|
| Low | 0.50 | 2 |
| Balanced (default Map) | 0.75 | 3 |
| High | 1.00 | 4 |

Overrides apply **per system instance** — they do **not** dirty the shared `Fluid2DSettings` asset.

Diagnostics (controller / metaball):

- Material property apply count  
- RT recreation count / current RT size  
- Drain registry count / full-scan count (should stay 0 in play)  
- Last obstacle refresh time / count  

## Architecture constraints

- **One `Fluid2DMetaballRenderer` / GPU fluid system per main camera.** Multiple regions sharing one camera are rejected by **Validate Setup**.
- Hidden auto objects: `Fluid Camera (Auto)`, `Fluid Canvas Overlay (Auto)`, runtime materials / SpriteShape clones. Cleanup runs on disable/destroy.
- Drains use an enable/disable **registry** (no per-frame `FindObjects`). Disabled drains are ignored and do not kill mass.
- Conserved mass is established on first melt/freeze for that region. First melt budgets particles from the current world polygon area (preferred spacing or `particlesPerUnitArea`), clamps to `maxParticlesPerToggle` / `Fluid2DSettings.maxParticles`, and **spreads across the full silhouette** (adaptive spacing when budget-limited — never a tiny corner blob). Remelt uses the conserved budget + water-origin samples.
- Large designer ice (scaled SpriteShape): raise `maxParticlesPerToggle` and system `maxParticles`, or set a lower `particlesPerUnitArea`. **Validate Setup** warns when preferred density exceeds the budget.
- Starter ice profile: `Presets/Fluid2D_IceProfile_Starter.asset`. Map scenes should assign the authored ice SpriteShape profile from the map art pack (never mutated by freeze).

## Troubleshooting

| Symptom | Check |
|---------|--------|
| No water visible | Camera assigned? Metaball material? Water layer not culled incorrectly? `debugMode` off? Validate Setup. |
| White / hollow ice | SpriteShape profile + fill texture; freeze uses runtime solid-fill clone (`fillOffset≈0`). Coverage diagnostics on phase toggle. |
| Wrong sorting | Ice `SpriteShapeRenderer` sorting layer/order; metaball canvas plane distance. |
| Mass disappears | Disable drains during melt/freeze tests; ensure drains not overlapping the pool. |
| Tiny water blob after scaling ice | Preferred density exceeds `maxParticlesPerToggle`. Adaptive melt spacing still covers the silhouette (sparser). Raise `maxParticlesPerToggle` + `Fluid2DSettings.maxParticles`, or set `particlesPerUnitArea`. Check phase toggle melt diagnostics / Validate Setup. Do not enable `disableAdaptiveMeltSpacing` for large ice. |
| Water ignores terrain | `Fluid2DObstacleManager` layer mask; **Refresh Obstacles** after moving colliders. |
| Perf spikes | Prefer Balanced; watch RT recreation counter (should not climb every frame); material applies should be stable when unchanged. Large ice with high budget is costly — prefer adaptive spacing over huge particle counts. |
| Domain reload leaks | Stop Play Mode; validators check hidden cameras/canvases/RTs are destroyed with the system. |

## Menu map

| Menu | Purpose |
|------|---------|
| Tools/Bao/Fluid2D/Environment Water Setup | Create / repair region |
| Tools/Bao/Fluid2D/Ensure Environment Assets | Prefab + presets |
| Tools/Bao/Fluid2D/Validate Prefab & Presets | Edit-mode ref check |
| Tools/Bao/Fluid2D/Validate Environment (Play Mode) | Acceptance harness |
| Tools/Bao/Fluid2D/Diagnostics/Legacy… | Old V2/V3/demo tools |

## Core components

- `Fluid2DEnvironmentController` — production facade  
- `IceWaterPhaseToggle2D` — melt/freeze geometry + mass conservation  
- `Fluid2DSystem` + compute — GPU SPH  
- `Fluid2DMetaballRenderer` — RT + compositing (cached material props)  
- `Fluid2DObstacleManager` — static collider extract  
- `Fluid2DEnvironmentPreset` — composable look/settings package  
