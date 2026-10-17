using UnityEngine;

namespace Bao.Fluid2D
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class Fluid2DSystem : MonoBehaviour
    {
        [Header("=== Configuration ===")]
        public Fluid2DSettings settings;
        public ComputeShader simulationShader;
        public Material particleMaterial;
        public Fluid2DObstacleManager obstacleManager;

        [Header("=== Runtime Status ===")]
        [SerializeField] private int activeParticleCount = 0;
        public int ActiveParticleCount => activeParticleCount;

        [Header("=== Runtime Overrides (do not mutate settings asset) ===")]
        [Tooltip("When >= 1, overrides Fluid2DSettings.substeps for this system only. -1 = use asset.")]
        [SerializeField] private int runtimeSubstepsOverride = -1;

        /// <summary>Last packed active drain count (diagnostics).</summary>
        [SerializeField] private int lastDrainPackCount;
        public int LastDrainPackCount => lastDrainPackCount;
        public int DrainRegistryCount => FluidDrainRegistry.RegisteredCount;
        public int DrainRegistryVersion => FluidDrainRegistry.Version;
        public int DrainFullScanCount => FluidDrainRegistry.FullScanCount;
        public int RuntimeSubstepsOverride => runtimeSubstepsOverride;

        /// <summary>
        /// Set per-instance substeps without mutating the shared Fluid2DSettings asset.
        /// Pass -1 to clear the override and use the asset value.
        /// </summary>
        public void SetRuntimeSubstepsOverride(int substepsOrNegativeToClear)
        {
            runtimeSubstepsOverride = substepsOrNegativeToClear >= 1
                ? Mathf.Clamp(substepsOrNegativeToClear, 1, 8)
                : -1;
        }

        public int GetEffectiveSubsteps()
        {
            if (runtimeSubstepsOverride >= 1)
                return runtimeSubstepsOverride;
            return settings != null ? Mathf.Max(1, settings.substeps) : 1;
        }

        // Reused drain bounds buffer (avoids per-step managed allocation).
        private Vector4[] drainBoundsArray;

        [Header("=== Debug Mode ===")]
        public bool debugMode = false;
        public bool showRawParticles = true;
        public bool showSpatialGrid = true;
        public bool showVelocityVectors = false;
        public bool showCollisionNormals = false;
#if UNITY_EDITOR
        [Header("=== Scene View Preview ===")]
        [Tooltip("Shows a throttled Fluid2D particle preview in the Scene View during Play Mode without changing Game View rendering.")]
        public bool showSceneViewPreview = false;
        private double nextScenePreviewReadTime;
        private double nextScenePreviewRepaintTime;
        private const double ScenePreviewInterval = 0.1d;
#endif
        private const int ScenePreviewMaxParticles = 400;
        private float lastGpuSimTime = 0f;
        private float minObservedDensity = 0f;
        private float maxObservedDensity = 0f;
        private float avgObservedDensity = 0f;
        private float avgObservedVelocity = 0f;
        private int particlesCollidingCount = 0;

        // GPU Buffers
        private GraphicsBuffer particlesBuffer;
        private GraphicsBuffer gridBuffer;
        private GraphicsBuffer gridCountBuffer;
        
        // Dynamic Rigid Coupling Buffers (multi-sample spheres across all Fluid2DObjects)
        private const int MaxDynamicBodies = 32;
        private GraphicsBuffer dynamicBodiesBuffer;
        private GraphicsBuffer dynamicForcesBuffer;

        public GraphicsBuffer ParticlesBuffer => particlesBuffer;

        // CPU Arrays
        private Fluid2DParticle[] particlesCPU;
        /// <summary>Monotonic stamp written on each emit; invalidates recycled pool slots for trackers.</summary>
        private int[] particleEmitStamps;
        private int nextEmitIndex = 0;
        private int emitStampCounter = 1;

        private DynamicBody[] dynamicBodiesCPU;
        private DynamicBodyForce[] dynamicForcesCPU;
        private Fluid2DObject[] bodyOwners;
        private int[] bodySampleIndices;
        private Fluid2DObject[] touchedDynamicObjects;
        private int touchedDynamicCount;

        // Compute Shader Kernels
        private int kernelClearGrid;
        private int kernelBuildGrid;
        private int kernelPredict;
        private int kernelSolveDensityPressure;
        private int kernelApplyViscosity;
        private int kernelResolveBounds;
        private int kernelUpdateVelocity;

        private void OnEnable()
        {
            InitializeBuffer();
        }

        private void OnDisable()
        {
            ReleaseBuffer();
        }

        private void OnDestroy()
        {
            ReleaseBuffer();
        }

        public void InitializeBuffer()
        {
            if (settings == null)
            {
                Debug.LogWarning("Fluid2DSystem: Thất bại do thiếu Fluid2DSettings!", this);
                return;
            }

            if (simulationShader == null)
            {
                Debug.LogWarning("Fluid2DSystem: Thất bại do thiếu Compute Shader mô phỏng!", this);
                return;
            }

            ReleaseBuffer();

            // Khởi tạo mảng CPU làm pool hạt
            particlesCPU = new Fluid2DParticle[settings.maxParticles];
            particleEmitStamps = new int[settings.maxParticles];
            for (int i = 0; i < settings.maxParticles; i++)
            {
                particlesCPU[i].active = 0;
                particlesCPU[i].type = 0;
                particleEmitStamps[i] = 0;
            }
            emitStampCounter = 1;

            // 1. Tạo GraphicsBuffer chứa mảng các hạt
            particlesBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                settings.maxParticles,
                System.Runtime.InteropServices.Marshal.SizeOf<Fluid2DParticle>()
            );
            particlesBuffer.SetData(particlesCPU);

            // 2. Tính toán kích thước ô lưới Uniform Grid dựa trên worldBounds và smoothingRadius
            float cellSize = settings.smoothingRadius;
            int gridWidth = Mathf.CeilToInt((settings.worldBounds.z - settings.worldBounds.x) / cellSize);
            int gridHeight = Mathf.CeilToInt((settings.worldBounds.w - settings.worldBounds.y) / cellSize);
            int numCells = gridWidth * gridHeight;

            // 3. Khởi tạo Buffers cho Grid
            gridBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                numCells * 16,
                sizeof(uint)
            );

            gridCountBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                numCells,
                sizeof(uint)
            );

            // 4. Khởi tạo Buffers cho Dynamic Body coupling (multi-sample, tối đa MaxDynamicBodies)
            dynamicBodiesCPU = new DynamicBody[MaxDynamicBodies];
            dynamicForcesCPU = new DynamicBodyForce[MaxDynamicBodies];
            bodyOwners = new Fluid2DObject[MaxDynamicBodies];
            bodySampleIndices = new int[MaxDynamicBodies];
            // One entry per packed body is enough (worst case: 1 sample per object).
            touchedDynamicObjects = new Fluid2DObject[MaxDynamicBodies];
            touchedDynamicCount = 0;

            dynamicBodiesBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                MaxDynamicBodies,
                System.Runtime.InteropServices.Marshal.SizeOf<DynamicBody>()
            );

            dynamicForcesBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                MaxDynamicBodies,
                System.Runtime.InteropServices.Marshal.SizeOf<DynamicBodyForce>()
            );

            // Tìm các kernel ID tương ứng trong Compute Shader
            kernelClearGrid = simulationShader.FindKernel("ClearGrid");
            kernelBuildGrid = simulationShader.FindKernel("BuildGrid");
            kernelPredict = simulationShader.FindKernel("PredictPositions");
            kernelSolveDensityPressure = simulationShader.FindKernel("SolveDensityPressure");
            kernelApplyViscosity = simulationShader.FindKernel("ApplyViscosity");
            kernelResolveBounds = simulationShader.FindKernel("ResolveStaticBounds");
            kernelUpdateVelocity = simulationShader.FindKernel("UpdateVelocity");

            activeParticleCount = 0;
            nextEmitIndex = 0;

            Debug.Log($"Fluid2DSystem: Đã khởi tạo thành công {settings.maxParticles} hạt. Grid Size: {gridWidth}x{gridHeight} ({numCells} cells).");
        }

        private void ReleaseBuffer()
        {
            if (particlesBuffer != null) { particlesBuffer.Release(); particlesBuffer = null; }
            if (gridBuffer != null) { gridBuffer.Release(); gridBuffer = null; }
            if (gridCountBuffer != null) { gridCountBuffer.Release(); gridCountBuffer = null; }
            if (dynamicBodiesBuffer != null) { dynamicBodiesBuffer.Release(); dynamicBodiesBuffer = null; }
            if (dynamicForcesBuffer != null) { dynamicForcesBuffer.Release(); dynamicForcesBuffer = null; }
        }

        /// <summary>
        /// One-shot GPU → CPU sync of the full particle pool.
        /// Use only on phase transitions / diagnostics — never every frame.
        /// </summary>
        public bool SyncParticlesFromGPU()
        {
            if (particlesCPU == null || particlesBuffer == null || settings == null)
                return false;

            particlesBuffer.GetData(particlesCPU);

            int active = 0;
            for (int i = 0; i < settings.maxParticles; i++)
            {
                if (particlesCPU[i].active == 1)
                {
                    active++;
                }
                else if (particleEmitStamps != null)
                {
                    // Slot is free on GPU — drop ownership so trackers don't treat recycled indices as live.
                    particleEmitStamps[i] = 0;
                }
            }
            activeParticleCount = active;
            return true;
        }

        /// <summary>
        /// Snapshot of water fragmentation / motion for objective tests.
        /// Union-find clusters particles within <paramref name="clusterRadius"/> (default ~2.2×smoothingRadius).
        /// Isolated = particles with no neighbor inside that radius.
        /// </summary>
        public WaterFragmentationDiagnostics ComputeWaterFragmentationDiagnostics(
            float clusterRadius = -1f, bool syncFromGpu = true)
        {
            var diag = new WaterFragmentationDiagnostics();
            if (particlesCPU == null || particlesBuffer == null || settings == null)
                return diag;

            if (syncFromGpu && !SyncParticlesFromGPU())
                return diag;

            float radius = clusterRadius > 1e-5f
                ? clusterRadius
                : Mathf.Max(settings.smoothingRadius * 2.2f, settings.particleRadius * 4f);
            float r2 = radius * radius;

            // Collect active indices.
            var actives = new System.Collections.Generic.List<int>(256);
            for (int i = 0; i < settings.maxParticles; i++)
            {
                if (particlesCPU[i].active == 1)
                    actives.Add(i);
            }

            int n = actives.Count;
            diag.activeCount = n;
            if (n == 0) return diag;

            double speedSum = 0.0;
            float maxSp = 0f;
            for (int a = 0; a < n; a++)
            {
                float sp = particlesCPU[actives[a]].velocity.magnitude;
                speedSum += sp;
                if (sp > maxSp) maxSp = sp;
            }
            diag.maxSpeed = maxSp;
            diag.meanSpeed = (float)(speedSum / n);

            // Union-find over active set.
            int[] parent = new int[n];
            int[] size = new int[n];
            for (int i = 0; i < n; i++)
            {
                parent[i] = i;
                size[i] = 1;
            }

            int Find(int x)
            {
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }
                return x;
            }

            void Union(int a, int b)
            {
                a = Find(a);
                b = Find(b);
                if (a == b) return;
                if (size[a] < size[b]) { int t = a; a = b; b = t; }
                parent[b] = a;
                size[a] += size[b];
            }

            int isolated = 0;
            for (int i = 0; i < n; i++)
            {
                Vector2 pi = particlesCPU[actives[i]].position;
                bool hasNeighbor = false;
                for (int j = i + 1; j < n; j++)
                {
                    Vector2 pj = particlesCPU[actives[j]].position;
                    float dx = pi.x - pj.x;
                    float dy = pi.y - pj.y;
                    if (dx * dx + dy * dy <= r2)
                    {
                        Union(i, j);
                        hasNeighbor = true;
                    }
                }
                // Second pass not needed for isolation of i if we also check j<i — do explicit.
                if (!hasNeighbor)
                {
                    for (int j = 0; j < i; j++)
                    {
                        Vector2 pj = particlesCPU[actives[j]].position;
                        float dx = pi.x - pj.x;
                        float dy = pi.y - pj.y;
                        if (dx * dx + dy * dy <= r2)
                        {
                            hasNeighbor = true;
                            break;
                        }
                    }
                }
                if (!hasNeighbor) isolated++;
            }

            int clusters = 0;
            int largest = 0;
            for (int i = 0; i < n; i++)
            {
                if (Find(i) != i) continue;
                clusters++;
                if (size[i] > largest) largest = size[i];
            }

            diag.isolatedCount = isolated;
            diag.clusterCount = clusters;
            diag.largestClusterFraction = n > 0 ? largest / (float)n : 0f;
            return diag;
        }

        /// <summary>
        /// Fragmentation diagnostics restricted to a tracked index list (stamp-validated).
        /// </summary>
        public WaterFragmentationDiagnostics ComputeTrackedFragmentationDiagnostics(
            System.Collections.Generic.IList<int> indices,
            System.Collections.Generic.IList<int> stamps = null,
            float clusterRadius = -1f,
            bool syncFromGpu = true)
        {
            var diag = new WaterFragmentationDiagnostics();
            if (particlesCPU == null || particlesBuffer == null || settings == null)
                return diag;
            if (indices == null || indices.Count == 0) return diag;

            if (syncFromGpu && !SyncParticlesFromGPU())
                return diag;

            float radius = clusterRadius > 1e-5f
                ? clusterRadius
                : Mathf.Max(settings.smoothingRadius * 2.2f, settings.particleRadius * 4f);
            float r2 = radius * radius;

            bool useStamps = stamps != null && particleEmitStamps != null && stamps.Count == indices.Count;
            var positions = new System.Collections.Generic.List<Vector2>(indices.Count);
            var velocities = new System.Collections.Generic.List<Vector2>(indices.Count);

            for (int i = 0; i < indices.Count; i++)
            {
                int index = indices[i];
                if (index < 0 || index >= settings.maxParticles) continue;
                if (useStamps && particleEmitStamps[index] != stamps[i]) continue;
                if (particlesCPU[index].active != 1) continue;
                positions.Add(particlesCPU[index].position);
                velocities.Add(particlesCPU[index].velocity);
            }

            int n = positions.Count;
            diag.activeCount = n;
            if (n == 0) return diag;

            double speedSum = 0.0;
            float maxSp = 0f;
            for (int i = 0; i < n; i++)
            {
                float sp = velocities[i].magnitude;
                speedSum += sp;
                if (sp > maxSp) maxSp = sp;
            }
            diag.maxSpeed = maxSp;
            diag.meanSpeed = (float)(speedSum / n);

            int[] parent = new int[n];
            int[] size = new int[n];
            for (int i = 0; i < n; i++)
            {
                parent[i] = i;
                size[i] = 1;
            }

            int Find(int x)
            {
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }
                return x;
            }

            void Union(int a, int b)
            {
                a = Find(a);
                b = Find(b);
                if (a == b) return;
                if (size[a] < size[b]) { int t = a; a = b; b = t; }
                parent[b] = a;
                size[a] += size[b];
            }

            int isolated = 0;
            for (int i = 0; i < n; i++)
            {
                bool hasNeighbor = false;
                for (int j = 0; j < n; j++)
                {
                    if (i == j) continue;
                    float dx = positions[i].x - positions[j].x;
                    float dy = positions[i].y - positions[j].y;
                    if (dx * dx + dy * dy <= r2)
                    {
                        if (j > i) Union(i, j);
                        hasNeighbor = true;
                    }
                }
                if (!hasNeighbor) isolated++;
            }

            int clusters = 0;
            int largest = 0;
            for (int i = 0; i < n; i++)
            {
                if (Find(i) != i) continue;
                clusters++;
                if (size[i] > largest) largest = size[i];
            }

            diag.isolatedCount = isolated;
            diag.clusterCount = clusters;
            diag.largestClusterFraction = n > 0 ? largest / (float)n : 0f;
            return diag;
        }

        /// <summary>
        /// Read world positions of still-active tracked indices after a GPU sync.
        /// When <paramref name="stamps"/> is provided, recycled pool slots (mismatched emit stamp) are skipped.
        /// Returns how many positions were written to <paramref name="outPositions"/>.
        /// </summary>
        public int GetActiveTrackedPositions(System.Collections.Generic.IList<int> indices,
            System.Collections.Generic.List<Vector2> outPositions, bool syncFromGpu = true,
            System.Collections.Generic.IList<int> stamps = null)
        {
            if (outPositions == null) return 0;
            outPositions.Clear();
            if (indices == null || indices.Count == 0) return 0;
            if (particlesCPU == null || particlesBuffer == null || settings == null) return 0;

            if (syncFromGpu && !SyncParticlesFromGPU())
                return 0;

            bool useStamps = stamps != null && particleEmitStamps != null && stamps.Count == indices.Count;

            for (int i = 0; i < indices.Count; i++)
            {
                int index = indices[i];
                if (index < 0 || index >= settings.maxParticles) continue;
                if (useStamps && particleEmitStamps[index] != stamps[i]) continue;
                if (particlesCPU[index].active != 1) continue;
                outPositions.Add(particlesCPU[index].position);
            }

            return outPositions.Count;
        }

        /// <summary>
        /// Kill only the provided indices after an optional GPU sync.
        /// Safe against stale CPU active flags and does not touch unrelated particles.
        /// When <paramref name="stamps"/> is provided, only kills slots that still match the emit stamp.
        /// </summary>
        public int KillTrackedParticles(System.Collections.Generic.IList<int> indices, bool syncFromGpu = true,
            System.Collections.Generic.IList<int> stamps = null)
        {
            if (indices == null || indices.Count == 0) return 0;
            if (particlesCPU == null || particlesBuffer == null || settings == null) return 0;

            if (syncFromGpu && !SyncParticlesFromGPU())
                return 0;

            bool useStamps = stamps != null && particleEmitStamps != null && stamps.Count == indices.Count;
            int killed = 0;
            for (int i = 0; i < indices.Count; i++)
            {
                int index = indices[i];
                if (index < 0 || index >= settings.maxParticles) continue;
                if (useStamps && particleEmitStamps[index] != stamps[i]) continue;
                if (particlesCPU[index].active != 1) continue;

                particlesCPU[index].active = 0;
                particlesCPU[index].velocity = Vector2.zero;
                if (particleEmitStamps != null)
                    particleEmitStamps[index] = 0;
                particlesBuffer.SetData(particlesCPU, index, index, 1);
                killed++;
            }

            if (killed > 0)
                activeParticleCount = Mathf.Max(0, activeParticleCount - killed);

            return killed;
        }

        /// <summary>Returns the emit stamp currently stored for a pool index (0 if free/unknown).</summary>
        public int GetParticleEmitStamp(int index)
        {
            if (particleEmitStamps == null || settings == null) return 0;
            if (index < 0 || index >= settings.maxParticles) return 0;
            return particleEmitStamps[index];
        }

        private int FindInactiveParticleSlot()
        {
            if (particlesCPU == null || settings == null) return -1;
            for (int n = 0; n < settings.maxParticles; n++)
            {
                int candidate = (nextEmitIndex + n) % settings.maxParticles;
                if (particlesCPU[candidate].active == 1) continue;
                return candidate;
            }
            return -1;
        }

        /// <summary>
        /// Sinh (Emit) một hạt mới vào hệ thống.
        /// </summary>
        public bool EmitParticle(Vector2 position, Vector2 initialVelocity, int fluidType = 0)
        {
            return EmitParticle(position, initialVelocity, fluidType, out _);
        }

        /// <summary>
        /// Sinh hạt và trả về index trong pool (để theo dõi / xóa theo patch).
        /// Scans for an inactive slot so re-emit after kill does not overwrite live particles.
        /// </summary>
        public bool EmitParticle(Vector2 position, Vector2 initialVelocity, int fluidType, out int particleIndex)
        {
            return EmitParticle(position, initialVelocity, fluidType, out particleIndex, out _);
        }

        /// <summary>
        /// Emit with an ownership stamp so callers can detect recycled pool slots later.
        /// </summary>
        public bool EmitParticle(Vector2 position, Vector2 initialVelocity, int fluidType,
            out int particleIndex, out int emitStamp)
        {
            particleIndex = -1;
            emitStamp = 0;
            if (particlesCPU == null || particlesBuffer == null || settings == null) return false;
            if (particleEmitStamps == null || particleEmitStamps.Length != settings.maxParticles)
                particleEmitStamps = new int[settings.maxParticles];

            int index = FindInactiveParticleSlot();
            // GPU drains may leave CPU active flags stale; resync once if the pool looks full.
            if (index < 0 && SyncParticlesFromGPU())
                index = FindInactiveParticleSlot();

            if (index < 0)
                return false; // pool full

            // Bump stamp even if counter wraps; 0 is reserved for empty.
            emitStampCounter = emitStampCounter == int.MaxValue ? 1 : emitStampCounter + 1;
            emitStamp = emitStampCounter;
            particleEmitStamps[index] = emitStamp;

            particlesCPU[index].position = position;
            particlesCPU[index].velocity = initialVelocity;
            particlesCPU[index].predictedPosition = position;
            particlesCPU[index].density = 0f;
            particlesCPU[index].nearDensity = 0f;
            particlesCPU[index].pressure = 0f;
            particlesCPU[index].nearPressure = 0f;
            particlesCPU[index].active = 1;
            particlesCPU[index].type = fluidType;

            // Đẩy dữ liệu hạt đơn lẻ này lên GPU buffer
            particlesBuffer.SetData(particlesCPU, index, index, 1);

            nextEmitIndex = (index + 1) % settings.maxParticles;

            if (!debugMode)
            {
                // Ở chế độ thường, ước lượng số hạt active
                activeParticleCount = Mathf.Min(settings.maxParticles, activeParticleCount + 1);
            }

            particleIndex = index;
            return true;
        }

        /// <summary>
        /// Thu hồi hoặc xóa một hạt tại index cụ thể.
        /// Always writes active=0 to the GPU so a stale CPU flag cannot leave a live particle behind.
        /// Prefer <see cref="KillTrackedParticles"/> when killing a whole tracked patch after simulation.
        /// </summary>
        public void KillParticle(int index, bool syncFromGpu = false)
        {
            if (particlesCPU == null || particlesBuffer == null || settings == null) return;
            if (index < 0 || index >= settings.maxParticles) return;

            if (syncFromGpu)
                SyncParticlesFromGPU();

            bool wasActive = particlesCPU[index].active == 1;
            particlesCPU[index].active = 0;
            particlesCPU[index].velocity = Vector2.zero;
            if (particleEmitStamps != null && index < particleEmitStamps.Length)
                particleEmitStamps[index] = 0;
            particlesBuffer.SetData(particlesCPU, index, index, 1);
            if (wasActive)
                activeParticleCount = Mathf.Max(0, activeParticleCount - 1);
        }

        /// <summary>
        /// Xóa các hạt active nằm trong AABB world-space (fallback khi không track được index).
        /// Syncs GPU positions first so the bounds test uses live particle locations.
        /// </summary>
        public int KillParticlesInBounds(Bounds worldBounds)
        {
            if (particlesCPU == null || particlesBuffer == null || settings == null) return 0;
            if (!SyncParticlesFromGPU()) return 0;

            int killed = 0;
            for (int i = 0; i < settings.maxParticles; i++)
            {
                if (particlesCPU[i].active != 1) continue;

                Vector2 pos = particlesCPU[i].position;
                if (!worldBounds.Contains(new Vector3(pos.x, pos.y, worldBounds.center.z)))
                    continue;

                particlesCPU[i].active = 0;
                particlesCPU[i].velocity = Vector2.zero;
                if (particleEmitStamps != null)
                    particleEmitStamps[i] = 0;
                particlesBuffer.SetData(particlesCPU, i, i, 1);
                activeParticleCount = Mathf.Max(0, activeParticleCount - 1);
                killed++;
            }

            return killed;
        }

        /// <summary>
        /// Reset toàn bộ hạt về trạng thái inactive.
        /// </summary>
        public void ClearAllParticles()
        {
            if (particlesCPU == null || particlesBuffer == null) return;

            for (int i = 0; i < settings.maxParticles; i++)
            {
                particlesCPU[i].active = 0;
                particlesCPU[i].velocity = Vector2.zero;
                if (particleEmitStamps != null)
                    particleEmitStamps[i] = 0;
            }

            particlesBuffer.SetData(particlesCPU);
            activeParticleCount = 0;
            nextEmitIndex = 0;
        }

        private void FixedUpdate()
        {
            if (!Application.isPlaying) return;

            RunSimulationStep();
        }

        /// <summary>
        /// Pack each Fluid2DObject's buoyancy samples into GPU DynamicBody slots.
        /// Returns number of active body samples this frame.
        /// </summary>
        private int PackDynamicBodies()
        {
            touchedDynamicCount = 0;
            int bodyCount = 0;

            var objects = Fluid2DObject.ActiveObjects;
            int objCount = objects != null ? objects.Count : 0;

            for (int o = 0; o < objCount && bodyCount < MaxDynamicBodies; o++)
            {
                Fluid2DObject obj = objects[o];
                if (obj == null || !obj.isActiveAndEnabled)
                    continue;
                if (obj.Rigidbody == null || obj.Rigidbody.bodyType == RigidbodyType2D.Static)
                    continue;

                int samples = Mathf.Max(1, obj.ActiveSampleCount);
                if (bodyCount + samples > MaxDynamicBodies)
                    samples = MaxDynamicBodies - bodyCount;
                if (samples <= 0)
                    break;

                obj.BeginFluidForceFrame();

                if (touchedDynamicCount < touchedDynamicObjects.Length)
                    touchedDynamicObjects[touchedDynamicCount++] = obj;

                Vector2 vel = obj.Rigidbody.linearVelocity;
                float mass = obj.Rigidbody.mass;

                for (int s = 0; s < samples; s++)
                {
                    dynamicBodiesCPU[bodyCount].position = obj.GetSampleWorldPosition(s);
                    dynamicBodiesCPU[bodyCount].velocity = vel;
                    dynamicBodiesCPU[bodyCount].radius = obj.GetSampleRadius(s);
                    dynamicBodiesCPU[bodyCount].mass = mass;
                    dynamicBodiesCPU[bodyCount].active = 1;

                    bodyOwners[bodyCount] = obj;
                    bodySampleIndices[bodyCount] = s;
                    dynamicForcesCPU[bodyCount].forceX = 0;
                    dynamicForcesCPU[bodyCount].forceY = 0;
                    bodyCount++;
                }
            }

            for (int i = bodyCount; i < MaxDynamicBodies; i++)
            {
                dynamicBodiesCPU[i].active = 0;
                bodyOwners[i] = null;
                bodySampleIndices[i] = 0;
                dynamicForcesCPU[i].forceX = 0;
                dynamicForcesCPU[i].forceY = 0;
            }

            return bodyCount;
        }

        private void RunSimulationStep()
        {
            if (particlesBuffer == null || gridBuffer == null || gridCountBuffer == null || 
                dynamicBodiesBuffer == null || dynamicForcesBuffer == null || settings == null || simulationShader == null) return;

            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

            float dt = Time.fixedDeltaTime;
            int numParticles = settings.maxParticles;
            int threadGroups = Mathf.CeilToInt(numParticles / 256f);

            // 1. Tính toán Grid Size
            float cellSize = settings.smoothingRadius;
            int gridWidth = Mathf.CeilToInt((settings.worldBounds.z - settings.worldBounds.x) / cellSize);
            int gridHeight = Mathf.CeilToInt((settings.worldBounds.w - settings.worldBounds.y) / cellSize);
            int numCells = gridWidth * gridHeight;
            int gridThreadGroups = Mathf.CeilToInt(numCells / 256f);

            // 2. Pack drains from enable/disable registry (no per-frame FindObjects scan).
            //    Disabled drains are ignored by FluidDrainRegistry.CopyActiveBounds.
            if (drainBoundsArray == null || drainBoundsArray.Length < 8)
                drainBoundsArray = new Vector4[8];
            FluidDrainRegistry.CopyActiveBounds(drainBoundsArray, 8, out int numDrains);
            lastDrainPackCount = numDrains;

            // 3. Pack multi-sample DynamicBodies từ Fluid2DObject đã đăng ký (không Find mỗi frame)
            int numDynamicBodies = PackDynamicBodies();
            dynamicBodiesBuffer.SetData(dynamicBodiesCPU);
            dynamicForcesBuffer.SetData(dynamicForcesCPU);

            // 4. Lấy Buffers chướng ngại vật tĩnh
            GraphicsBuffer circleBuf = null;
            GraphicsBuffer segmentBuf = null;
            int numCircles = 0;
            int numSegments = 0;

            if (obstacleManager != null)
            {
                circleBuf = obstacleManager.CircleBuffer;
                segmentBuf = obstacleManager.SegmentBuffer;
                numCircles = obstacleManager.CircleCount;
                numSegments = obstacleManager.SegmentCount;
            }

            // Substeps: runtime override (quality tier) never mutates the shared settings asset.
            int substeps = GetEffectiveSubsteps();
            float subDeltaTime = dt / substeps;

            for (int step = 0; step < substeps; step++)
            {
                // A. Gán các tham số chung cho Compute Shader
                simulationShader.SetInt("numParticles", numParticles);
                simulationShader.SetFloat("deltaTime", subDeltaTime);
                simulationShader.SetVector("gravity", settings.gravity);
                simulationShader.SetVector("worldBounds", settings.worldBounds);
                simulationShader.SetFloat("damping", settings.collisionRestitution);
                simulationShader.SetFloat("maxVelocity", settings.maxVelocity);

                simulationShader.SetFloat("cellSize", cellSize);
                simulationShader.SetInt("gridWidth", gridWidth);
                simulationShader.SetInt("gridHeight", gridHeight);
                simulationShader.SetInt("maxParticlesPerCell", 16);

                simulationShader.SetFloat("smoothingRadius", settings.smoothingRadius);
                simulationShader.SetFloat("restDensity", settings.restDensity);
                simulationShader.SetFloat("pressureStiffness", settings.pressureStiffness);
                simulationShader.SetFloat("nearPressureStiffness", settings.nearPressureStiffness);
                simulationShader.SetFloat("viscosity", settings.viscosity);
                simulationShader.SetFloat("surfaceTension", settings.surfaceTension);
                simulationShader.SetFloat("cohesionRadius",
                    settings.cohesionRadius > 1e-5f ? settings.cohesionRadius : 0f);
                simulationShader.SetFloat("reunificationStrength", settings.reunificationStrength);
                simulationShader.SetFloat("velocityDamping", settings.velocityDamping);
                simulationShader.SetFloat("collisionFriction", settings.collisionFriction);
                simulationShader.SetFloat("obstacleRestitution", settings.obstacleRestitution);
                simulationShader.SetFloat("collisionNormalAbsorption", settings.collisionNormalAbsorption);

                // Gửi mảng Drain
                simulationShader.SetVectorArray("drainBounds", drainBoundsArray);
                simulationShader.SetInt("numDrains", numDrains);

                // Gửi thông tin Dynamic Bodies
                simulationShader.SetBuffer(kernelResolveBounds, "DynamicBodies", dynamicBodiesBuffer);
                simulationShader.SetBuffer(kernelResolveBounds, "DynamicForces", dynamicForcesBuffer);
                simulationShader.SetInt("numDynamicBodies", numDynamicBodies);

                // 1. Predict positions (Tích phân Euler)
                simulationShader.SetBuffer(kernelPredict, "Particles", particlesBuffer);
                simulationShader.Dispatch(kernelPredict, threadGroups, 1, 1);

                // 2. Dọn dẹp Grid và xây dựng Grid mới
                simulationShader.SetBuffer(kernelClearGrid, "GridCount", gridCountBuffer);
                simulationShader.Dispatch(kernelClearGrid, gridThreadGroups, 1, 1);

                simulationShader.SetBuffer(kernelBuildGrid, "Particles", particlesBuffer);
                simulationShader.SetBuffer(kernelBuildGrid, "Grid", gridBuffer);
                simulationShader.SetBuffer(kernelBuildGrid, "GridCount", gridCountBuffer);
                simulationShader.Dispatch(kernelBuildGrid, threadGroups, 1, 1);

                // 3. Giải quyết mật độ áp suất (DDR Solver)
                simulationShader.SetBuffer(kernelSolveDensityPressure, "Particles", particlesBuffer);
                simulationShader.SetBuffer(kernelSolveDensityPressure, "Grid", gridBuffer);
                simulationShader.SetBuffer(kernelSolveDensityPressure, "GridCount", gridCountBuffer);
                simulationShader.Dispatch(kernelSolveDensityPressure, threadGroups, 1, 1);

                // 4. Áp dụng lực nhớt (Viscosity)
                simulationShader.SetBuffer(kernelApplyViscosity, "Particles", particlesBuffer);
                simulationShader.SetBuffer(kernelApplyViscosity, "Grid", gridBuffer);
                simulationShader.SetBuffer(kernelApplyViscosity, "GridCount", gridCountBuffer);
                simulationShader.Dispatch(kernelApplyViscosity, threadGroups, 1, 1);

                // 5. Va chạm tĩnh với chướng ngại vật và tường biên
                if (circleBuf != null)
                {
                    simulationShader.SetBuffer(kernelResolveBounds, "CircleObstacles", circleBuf);
                    simulationShader.SetInt("numCircleObstacles", numCircles);
                }
                else
                {
                    simulationShader.SetInt("numCircleObstacles", 0);
                }

                if (segmentBuf != null)
                {
                    simulationShader.SetBuffer(kernelResolveBounds, "SegmentObstacles", segmentBuf);
                    simulationShader.SetInt("numSegmentObstacles", numSegments);
                }
                else
                {
                    simulationShader.SetInt("numSegmentObstacles", 0);
                }

                simulationShader.SetFloat("particleRadius", settings.particleRadius);

                simulationShader.SetBuffer(kernelResolveBounds, "Particles", particlesBuffer);
                simulationShader.Dispatch(kernelResolveBounds, threadGroups, 1, 1);

                // 6. Cập nhật vận tốc thực tế của hạt
                simulationShader.SetBuffer(kernelUpdateVelocity, "Particles", particlesBuffer);
                simulationShader.Dispatch(kernelUpdateVelocity, threadGroups, 1, 1);
            }

            // B. Đọc lực GPU theo từng sample, mượt/clamp phía object (tránh nảy lò xo)
            if (numDynamicBodies > 0)
            {
                dynamicForcesBuffer.GetData(dynamicForcesCPU);
                for (int i = 0; i < numDynamicBodies; i++)
                {
                    Fluid2DObject owner = bodyOwners[i];
                    if (owner == null)
                        continue;

                    Vector2 force = new Vector2(dynamicForcesCPU[i].forceX, dynamicForcesCPU[i].forceY) / 10000.0f;
                    if (force.sqrMagnitude < 1e-10f)
                        continue;

                    Vector2 samplePos = owner.GetSampleWorldPosition(bodySampleIndices[i]);
                    owner.AccumulateFluidForce(force, samplePos);
                }

                for (int t = 0; t < touchedDynamicCount; t++)
                {
                    if (touchedDynamicObjects[t] != null)
                        touchedDynamicObjects[t].FinalizeFluidForces();
                }
            }

            sw.Stop();
            lastGpuSimTime = (float)sw.Elapsed.TotalMilliseconds;
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
#if UNITY_EDITOR
                double editorTime = UnityEditor.EditorApplication.timeSinceStartup;
                if (debugMode)
                {
                    UpdateDebugData();
                }
                else if (showSceneViewPreview && editorTime >= nextScenePreviewReadTime)
                {
                    UpdateDebugData();
                    nextScenePreviewReadTime = editorTime + ScenePreviewInterval;
                }

                if (showSceneViewPreview && editorTime >= nextScenePreviewRepaintTime)
                {
                    UnityEditor.SceneView.RepaintAll();
                    nextScenePreviewRepaintTime = editorTime + ScenePreviewInterval;
                }
#else
                if (debugMode)
                    UpdateDebugData();
#endif
            }

            RenderParticles();
        }

        private void UpdateDebugData()
        {
            if (particlesBuffer == null || particlesCPU == null || settings == null) return;

            particlesBuffer.GetData(particlesCPU);

            int activeCount = 0;
            float minD = float.MaxValue;
            float maxD = float.MinValue;
            float totalD = 0f;
            float totalVel = 0f;
            int collCount = 0;

            float radius = settings.particleRadius;
            float collThreshold = radius * 1.2f;

            for (int i = 0; i < settings.maxParticles; i++)
            {
                if (particlesCPU[i].active == 1)
                {
                    activeCount++;
                    float density = particlesCPU[i].density;
                    if (density < minD) minD = density;
                    if (density > maxD) maxD = density;
                    totalD += density;

                    float speed = particlesCPU[i].velocity.magnitude;
                    totalVel += speed;

                    // Check bounds collision
                    Vector2 pos = particlesCPU[i].position;
                    bool colBounds = (pos.x - settings.worldBounds.x < collThreshold) ||
                                     (settings.worldBounds.z - pos.x < collThreshold) ||
                                     (pos.y - settings.worldBounds.y < collThreshold) ||
                                     (settings.worldBounds.w - pos.y < collThreshold);

                    bool colObstacles = false;
                    if (obstacleManager != null)
                    {
                        var circles = obstacleManager.Circles;
                        if (circles != null)
                        {
                            for (int k = 0; k < circles.Count; k++)
                            {
                                if (circles[k].active == 1 && Vector2.Distance(pos, circles[k].center) < circles[k].radius + collThreshold)
                                {
                                    colObstacles = true;
                                    break;
                                }
                            }
                        }

                        if (!colObstacles)
                        {
                            var segments = obstacleManager.Segments;
                            if (segments != null)
                            {
                                for (int k = 0; k < segments.Count; k++)
                                {
                                    if (segments[k].active == 1)
                                    {
                                        Vector2 ab = segments[k].b - segments[k].a;
                                        Vector2 ap = pos - segments[k].a;
                                        float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / Vector2.Dot(ab, ab));
                                        Vector2 closest = segments[k].a + t * ab;
                                        if (Vector2.Distance(pos, closest) < segments[k].radius + collThreshold)
                                        {
                                            colObstacles = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (colBounds || colObstacles)
                    {
                        collCount++;
                    }
                }
            }

            activeParticleCount = activeCount;

            if (activeCount > 0)
            {
                minObservedDensity = minD;
                maxObservedDensity = maxD;
                avgObservedDensity = totalD / activeCount;
                avgObservedVelocity = totalVel / activeCount;
            }
            else
            {
                minObservedDensity = 0f;
                maxObservedDensity = 0f;
                avgObservedDensity = 0f;
                avgObservedVelocity = 0f;
            }
            particlesCollidingCount = collCount;
        }

        private void OnGUI()
        {
            if (!debugMode || !Application.isPlaying) return;

            Rect panelRect = new Rect(10, 10, 320, 260);
            GUI.Box(panelRect, "=== Fluid 2D Simulation Statistics ===");

            GUILayout.BeginArea(new Rect(20, 35, 300, 230));
            
            GUILayout.Label($"Active Particles: {activeParticleCount} / {settings.maxParticles}");
            GUILayout.Label($"World Bounds: [{settings.worldBounds.x:F1}, {settings.worldBounds.y:F1}] to [{settings.worldBounds.z:F1}, {settings.worldBounds.w:F1}]");
            
            float cellSize = settings.smoothingRadius;
            int gridWidth = Mathf.CeilToInt((settings.worldBounds.z - settings.worldBounds.x) / cellSize);
            int gridHeight = Mathf.CeilToInt((settings.worldBounds.w - settings.worldBounds.y) / cellSize);
            GUILayout.Label($"Spatial Grid: {gridWidth}x{gridHeight} ({gridWidth * gridHeight} cells, size={cellSize:F2})");
            
            GUILayout.Label($"Min Density: {minObservedDensity:F3}");
            GUILayout.Label($"Max Density: {maxObservedDensity:F3}");
            GUILayout.Label($"Avg Density: {avgObservedDensity:F3} (Rest: {settings.restDensity})");
            GUILayout.Label($"Avg Velocity: {avgObservedVelocity:F3} (Max: {settings.maxVelocity})");
            GUILayout.Label($"Particles in Collision: {particlesCollidingCount}");
            GUILayout.Label($"GPU Sim Time: {lastGpuSimTime:F3} ms");
            
            GUILayout.Space(5);
            if (GUILayout.Button("Clear All Particles"))
            {
                ClearAllParticles();
            }

            GUILayout.EndArea();
        }

        private void OnDrawGizmos()
        {
#if UNITY_EDITOR
            bool previewSceneView = showSceneViewPreview;
#else
            const bool previewSceneView = false;
#endif
            if ((!debugMode && !previewSceneView) || particlesCPU == null || settings == null) return;
            // 1. Vẽ Spatial Grid
            if (debugMode && showSpatialGrid)
            {
                Gizmos.color = new Color(1f, 1f, 1f, 0.15f);
                float cellSize = settings.smoothingRadius;
                float minX = settings.worldBounds.x;
                float minY = settings.worldBounds.y;
                float maxX = settings.worldBounds.z;
                float maxY = settings.worldBounds.w;

                for (float x = minX; x <= maxX; x += cellSize)
                {
                    Gizmos.DrawLine(new Vector3(x, minY, 0f), new Vector3(x, maxY, 0f));
                }
                Gizmos.DrawLine(new Vector3(maxX, minY, 0f), new Vector3(maxX, maxY, 0f));

                for (float y = minY; y <= maxY; y += cellSize)
                {
                    Gizmos.DrawLine(new Vector3(minX, y, 0f), new Vector3(maxX, y, 0f));
                }
                Gizmos.DrawLine(new Vector3(minX, maxY, 0f), new Vector3(maxX, maxY, 0f));
            }

            // 2. Vẽ từng hạt (Particle thô & Heatmap)
            float particleRadius = settings.particleRadius;
            float smoothRadius = settings.smoothingRadius;

            int previewStride = previewSceneView
                ? Mathf.Max(1, settings.maxParticles / ScenePreviewMaxParticles)
                : 1;

            for (int i = 0; i < settings.maxParticles; i++)
            {
                if (particlesCPU[i].active == 0) continue;
                if (previewSceneView && i % previewStride != 0) continue;

                Vector2 pos = particlesCPU[i].position;
                if (showRawParticles || previewSceneView)
                {
                    float ratio = particlesCPU[i].density / (settings.restDensity > 0 ? settings.restDensity : 1f);
                    Color c = Color.blue;
                    if (ratio < 1.0f)
                    {
                        c = Color.Lerp(Color.blue, Color.green, ratio);
                    }
                    else
                    {
                        c = Color.Lerp(Color.green, Color.red, Mathf.Min(ratio - 1.0f, 1.0f));
                    }
                    c.a = 0.8f;
                    Gizmos.color = previewSceneView
                        ? new Color(settings.surfaceColor.r, settings.surfaceColor.g, settings.surfaceColor.b, 0.85f)
                        : c;
                    if (previewSceneView)
                        Gizmos.DrawSphere(new Vector3(pos.x, pos.y, 0f), particleRadius * 2.5f);
                    else
                        Gizmos.DrawWireSphere(new Vector3(pos.x, pos.y, 0f), particleRadius);
                }

                // 3. Vẽ Velocity Vector                if (debugMode && showVelocityVectors)
                {
                    Gizmos.color = Color.yellow;
                    Vector2 vel = particlesCPU[i].velocity;
                    if (vel.sqrMagnitude > 0.001f)
                    {
                        Gizmos.DrawLine(pos, pos + vel * 0.1f);
                    }
                }

                // 4. Vẽ Collision Normal
                if (debugMode && showCollisionNormals)
                {
                    Gizmos.color = Color.cyan;
                    Vector2 norm = Vector2.zero;
                    float collThreshold = particleRadius * 1.2f;

                    if (pos.x - settings.worldBounds.x < collThreshold) norm += Vector2.right;
                    else if (settings.worldBounds.z - pos.x < collThreshold) norm += Vector2.left;
                    if (pos.y - settings.worldBounds.y < collThreshold) norm += Vector2.up;
                    else if (settings.worldBounds.w - pos.y < collThreshold) norm += Vector2.down;

                    if (obstacleManager != null)
                    {
                        var circles = obstacleManager.Circles;
                        if (circles != null)
                        {
                            for (int k = 0; k < circles.Count; k++)
                            {
                                if (circles[k].active == 1 && Vector2.Distance(pos, circles[k].center) < circles[k].radius + collThreshold)
                                {
                                    norm += (pos - circles[k].center).normalized;
                                }
                            }
                        }

                        var segments = obstacleManager.Segments;
                        if (segments != null)
                        {
                            for (int k = 0; k < segments.Count; k++)
                            {
                                if (segments[k].active == 1)
                                {
                                    Vector2 ab = segments[k].b - segments[k].a;
                                    Vector2 ap = pos - segments[k].a;
                                    float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / Vector2.Dot(ab, ab));
                                    Vector2 closest = segments[k].a + t * ab;
                                    if (Vector2.Distance(pos, closest) < segments[k].radius + collThreshold)
                                    {
                                        norm += (pos - closest).normalized;
                                    }
                                }
                            }
                        }
                    }

                    if (norm.sqrMagnitude > 0.001f)
                    {
                        Gizmos.DrawLine(pos, pos + norm.normalized * 0.3f);
                    }
                }
            }

            // Vẽ Smoothing Radius của hạt đầu tiên active để minh họa
            for (int i = 0; i < settings.maxParticles; i++)
            {
                if (particlesCPU[i].active == 1)
                {
                    Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
                    Gizmos.DrawWireSphere(particlesCPU[i].position, smoothRadius);
                    break;
                }
            }
        }

        private void RenderParticles()
        {
            if (particlesBuffer == null || particleMaterial == null || settings == null) return;

            particleMaterial.SetBuffer("Particles", particlesBuffer);
            particleMaterial.SetFloat("_ParticleRadius", settings.particleRadius);
            particleMaterial.SetColor("_Color", settings.surfaceColor);
            particleMaterial.SetFloat("_CoreStrength", Mathf.Lerp(0.75f, 1.35f, Mathf.Clamp01(settings.surfaceTension)));
            particleMaterial.SetFloat("_EdgeFeather", Mathf.Lerp(1.4f, 2.6f, Mathf.Clamp01(settings.viscosity * 8f)));

            // Prefer metaball water layer so particles land in the fluid RT even if host layer drifted.
            int drawLayer = gameObject.layer;
            var metaball = GetComponent<Fluid2DMetaballRenderer>();
            if (metaball != null)
                drawLayer = Mathf.Clamp(metaball.waterLayer, 0, 31);

            float boundsWidth = Mathf.Max(0.01f, settings.worldBounds.z - settings.worldBounds.x);
            float boundsHeight = Mathf.Max(0.01f, settings.worldBounds.w - settings.worldBounds.y);
            Vector3 boundsCenter = new Vector3(
                settings.worldBounds.x + boundsWidth * 0.5f,
                settings.worldBounds.y + boundsHeight * 0.5f,
                0f);
            Bounds drawingBounds = new Bounds(boundsCenter, new Vector3(boundsWidth, boundsHeight, 10f));
            Graphics.DrawProcedural(
                particleMaterial, 
                drawingBounds, 
                MeshTopology.Triangles, 
                6, 
                settings.maxParticles, 
                null, 
                null, 
                UnityEngine.Rendering.ShadowCastingMode.Off, 
                false, 
                drawLayer
            );
        }
    }
}
