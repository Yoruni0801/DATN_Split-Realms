using System.Collections;
using System.Collections.Generic;
using Bao.Bosses;
using UnityEngine;
using UnityEngine.Serialization;

namespace Bao.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DreadwormBoss))]
    public sealed class DreadwormVFXController : MonoBehaviour
    {
        [Header("Crystal Body Shader")]
        [FormerlySerializedAs("subterraneanShader")]
        [SerializeField] private Shader crystalBossShader;
        [FormerlySerializedAs("lavaColor")]
        [ColorUsage(true, true)]
        [SerializeField] private Color crystalGlowColor = new Color(0.45f, 1.85f, 2.35f, 1f);
        [FormerlySerializedAs("sparkColor")]
        [ColorUsage(true, true)]
        [SerializeField] private Color stormGlowColor = new Color(0.12f, 0.35f, 1.8f, 1f);
        [ColorUsage(true, true)]
        [SerializeField] private Color enrageVeinColor = new Color(2.35f, 0.45f, 0.22f, 1f);
        
        [ColorUsage(true, true)]
        [SerializeField] private Color enrageRimColor = new Color(2.15f, 0.24f, 0.38f, 1f);
        
        [SerializeField, Min(0.1f)] private float transitionSpeed = 5f; // Forced Recompile

        [Header("Spine Eruption - Spike Customization")]
        [Tooltip("Khoảng scale ngẫu nhiên cho các trụ gai băng (X = min scale, Y = max scale)")]
        [SerializeField] private Vector2 spikeScaleVariance = new Vector2(0.85f, 1.35f);
        [Tooltip("Khoảng góc xoay nghiêng ngẫu nhiên cho các trụ gai băng (độ)")]
        [SerializeField] private Vector2 spikeRotationVariance = new Vector2(-7f, 7f);
        [Tooltip("Offset Y (world units) tính từ gốc trụ băng lên điểm spawn hiệu ứng vỡ vụn (Shatter Nova). Điều chỉnh nếu bụi kim cương bị lệch khỏi đỉnh trụ.")]
        [SerializeField] private float spikeShatterAnchorY = 0.85f;

        [Header("Charge Telegraph - Ancient Crystal Fissure")]
        [SerializeField, Min(0.4f)] private float chargeLaneWidth = 1.2f;
        [FormerlySerializedAs("chargeChevronCount")]
        [SerializeField, Range(3, 9)] private int chargeFissureShardCount = 6;
        [SerializeField, Min(0.05f)] private float emitInterval = 0.08f;
        [SerializeField, Min(0.1f)] private float afterimageInterval = 0.11f;

        [Header("Impact Feedback")]
        [SerializeField, Min(0f)] private float chargeShakeMagnitude = 0.11f;
        [SerializeField, Min(0f)] private float slamShakeMagnitude = 0.16f;

        [Header("MeshFX Frost VFX Prefab & Materials (Proposals 2 & 3)")]
        [SerializeField] private GameObject meshFXFrostPrefab;
        [SerializeField] private Material frostAuraMaterial;
        [SerializeField] private Material alphaSmokeMaterial;
        [SerializeField] private Material specklesMaterial;
        [SerializeField] private Material iceCrystalMaterial;

        private readonly List<Material> runtimeMaterials = new List<Material>();

        private DreadwormBoss dreadworm;
        private SpriteRenderer[] renderers;
        private Material[] bossMaterials;

        private Material frostAdditiveMaterial;
        private Material frostAlphaMaterial;
        private Material brightAdditiveMaterial;
        private Material stormAdditiveMaterial;
        private Material dustAlphaMaterial;
        private Material shardAdditiveMaterial;
        private Material shardAlphaMaterial;
        private Material streakMaterial;
        private Material ghostMaterial;
        private Material frozenCocoonMaterial;

        // --- Ruby & Crimson Materials for Skill 3 & Subterranean Spikes ---
        private Material rubyAdditiveMaterial;
        private Material amberAdditiveMaterial;
        private Material maroonAlphaMaterial;
        private Material terracottaDustMaterial;
        private Material rubyShardAdditiveMaterial;

        // --- Glacial Arctic Jade & Crystal Materials for Spine Eruption (AAA Overhaul) ---
        private Material glacialCrystalAlphaMaterial;
        private Material glacialJadeAdditiveMaterial;
        private Material glacialDiamondAdditiveMaterial;
        private Material basaltDebrisMaterial;
        private Material cryoMistAlphaMaterial;

        private float currentIntensity;
        private float currentFrozenArmor;
        private float chargeProgress;
        private float hitFlash;
        private Coroutine hitFlashCoroutine;

        private GameObject chargeTelegraphRoot;
        private LineRenderer chargeBackdropLine;
        private LineRenderer chargeFissureLine;
        private LineRenderer chargeFissureBranchTop;
        private LineRenderer chargeFissureBranchBottom;
        private SpriteRenderer chargeOriginGlow;
        private SpriteRenderer chargeFissureEndGlow;
        private SpriteRenderer[] chargeFissureShards;
        private bool chargeTelegraphActive;
        private bool chargeDashActive;
        private float chargeDirection;
        private float chargeTravelDistance;
        private float chargeTelegraphDuration;
        private float lastAfterimageTime;
        private float lastDebrisTime;
        private Vector3 lastDebrisPosition;

        // --- Proposal 3: Bone-attached Frost Trail Emitters (Body bone 1 to 5) ---
        private readonly List<Transform> chargeTrailBones = new List<Transform>();
        private readonly List<ParticleSystem> chargeTrailDustSystems = new List<ParticleSystem>();
        private readonly List<ParticleSystem> chargeTrailSmokeSystems = new List<ParticleSystem>();
        private GameObject chargeTrailEmittersRoot;

        private GameObject slamTelegraphRoot;
        private SpriteRenderer slamAreaFill;
        private SpriteRenderer slamAreaRing;
        private LineRenderer[] slamCracks;
        private bool slamTelegraphActive;
        private float slamProgress;
        private float slamRadius;
        private float slamDirection;

        private GameObject castAuraRoot;
        private SpriteRenderer castCore;
        private SpriteRenderer castRingA;
        private SpriteRenderer castRingB;
        private SpriteRenderer castRingC;
        private bool castAuraActive;
        private bool sphereCast;
        private float castStartTime;
        private float castDuration;

        // --- Frozen Sanctuary Cocoon Shield & Stagger VFX ---
        private GameObject sanctuaryWindupRoot;
        private SpriteRenderer windupGroundRuneA;
        private SpriteRenderer windupGroundRuneB;
        private SpriteRenderer windupCorePulse;
        private ParticleSystem windupManaVortexSystem;
        private bool sanctuaryWindupActive;
        private float sanctuaryWindupStartTime;
        private float sanctuaryWindupDuration;

        private GameObject cocoonShieldRoot;
        private SpriteRenderer cocoonCoreShield;
        private SpriteRenderer cocoonOuterRing;
        private SpriteRenderer cocoonHexRing;
        private SpriteRenderer[] cocoonOrbitCrystals;
        private ParticleSystem cocoonFrostAuraSystem;
        private ParticleSystem cocoonAlphaSmokeSystem;
        private ParticleSystem cocoonSpecklesSystem;
        private ParticleSystem cocoonIceCrystalsSystem;
        private bool cocoonShieldActive;
        private float cocoonStartTime;
        private float cocoonDuration;
        private float cocoonHealthRatio = 1f;

        private GameObject staggerStarsRoot;
        private SpriteRenderer[] staggerStars;
        private bool staggerActive;
        private float staggerStartTime;
        private float staggerDuration;


        private GameObject spineTelegraphRoot;
        private SpriteRenderer[] spineTelegraphFills;
        private SpriteRenderer[] spineTelegraphRings;
        private LineRenderer[] spineTelegraphCrackLines;
        private ParticleSystem[] spineTelegraphSuctionSystems;
        private Vector3[] spineTelegraphPoints;
        private float spineTelegraphRadius;
        private float spineTelegraphStartTime;
        private float spineTelegraphDuration;
        private bool spineTelegraphActive;

        private void Awake()
        {
            dreadworm = GetComponent<DreadwormBoss>();
            if (crystalBossShader == null)
                crystalBossShader = Shader.Find("Bao/DreadwormCrystalBossShader");
        }

        private void Start()
        {
            renderers = dreadworm != null ? dreadworm.SpriteRenderers : GetComponentsInChildren<SpriteRenderer>(true);
            CreateEffectMaterials();
            SetupBossMaterials();
            BuildChargeTelegraph();
            BuildChargeTrailEmitters();
            BuildSlamTelegraph();
            BuildCastAura();
            SetPhase(dreadworm != null ? dreadworm.CurrentPhase : BossPhase.Phase1);
        }

        private void Update()
        {
            if (dreadworm == null)
                return;

            UpdateBossShader();

            if (chargeTelegraphActive)
                RefreshChargeTelegraph();

            if (chargeDashActive)
                UpdateChargeDashVFX();

            if (slamTelegraphActive)
                RefreshSlamTelegraph();

            if (castAuraActive)
                RefreshCastAura();

            if (sanctuaryWindupActive)
                RefreshSanctuaryWindup();

            if (cocoonShieldActive)
                RefreshCocoonShield();

            if (staggerActive)
                RefreshStaggerStars();

            if (spineTelegraphActive)
                RefreshSpineEruptionTelegraph();
        }

        private void OnDestroy()
        {
            DestroyRuntimeObject(chargeTelegraphRoot);
            DestroyRuntimeObject(slamTelegraphRoot);
            DestroyRuntimeObject(castAuraRoot);
            DestroyRuntimeObject(sanctuaryWindupRoot);
            DestroyRuntimeObject(spineTelegraphRoot);
            DestroyRuntimeObject(cocoonShieldRoot);
            DestroyRuntimeObject(staggerStarsRoot);
            DestroyRuntimeObject(chargeTrailEmittersRoot);

            for (int i = 0; i < runtimeMaterials.Count; i++)
                DestroyRuntimeObject(runtimeMaterials[i]);
            runtimeMaterials.Clear();
        }

        public void BeginChargeTelegraph(float duration, float direction, float travelDistance)
        {
            if (chargeTelegraphRoot == null)
                BuildChargeTelegraph();

            chargeDirection = Mathf.Approximately(direction, 0f) ? 1f : Mathf.Sign(direction);
            chargeTravelDistance = Mathf.Max(2f, travelDistance);
            chargeTelegraphDuration = Mathf.Max(0.08f, duration);
            chargeProgress = 0f;
            chargeTelegraphActive = true;
            chargeDashActive = false;
            chargeTelegraphRoot.SetActive(true);
            RefreshChargeTelegraph();

            SpawnGatherBurst(GetChargeHeadPosition(), chargeTelegraphDuration, false);
        }

        public void SetChargeTelegraphProgress(float normalizedProgress)
        {
            chargeProgress = Mathf.Clamp01(normalizedProgress);
        }

        public void BeginChargeDash()
        {
            chargeTelegraphActive = false;
            if (chargeTelegraphRoot != null)
                chargeTelegraphRoot.SetActive(false);

            chargeDashActive = true;
            lastAfterimageTime = -99f;
            lastDebrisTime = -99f;
            lastDebrisPosition = transform.position;

            // Enable Bone-attached Charge Trail Emitters (Body bone 1 to 5)
            SetChargeTrailEmittersActive(true);

            Vector3 burstPosition = GetChargeHeadPosition();
            SpawnRadialBurst(burstPosition, 18, 4.8f, DreadwormVFXUtility.BloodRubyCore, true);
            SpawnShockwave(burstPosition, new Vector2(2.8f, 1.7f), 0.3f, DreadwormVFXUtility.MoltenAmber);
            ShakeCamera(0.09f, chargeShakeMagnitude * 0.65f);
        }

        public void EndCharge(bool impacted)
        {
            bool wasCharging = chargeDashActive || chargeTelegraphActive;
            chargeDashActive = false;
            chargeTelegraphActive = false;

            // Disable Bone-attached Charge Trail Emitters (lingering particles fade in World space)
            SetChargeTrailEmittersActive(false);

            if (chargeTelegraphRoot != null)
                chargeTelegraphRoot.SetActive(false);

            if (!wasCharging)
                return;

            Vector3 impactPosition = GetChargeHeadPosition();
            SpawnRadialBurst(
                impactPosition,
                impacted ? 26 : 14,
                impacted ? 7f : 4.2f,
                impacted ? DreadwormVFXUtility.MoltenAmber : DreadwormVFXUtility.BloodRubyCore,
                true);
            SpawnGroundBurst(impactPosition, impacted ? 20 : 10, impacted ? 5.5f : 3.4f, chargeDirection);
            SpawnShockwave(
                impactPosition,
                impacted ? new Vector2(6.8f, 2.1f) : new Vector2(3.8f, 1.2f),
                impacted ? 0.42f : 0.3f,
                DreadwormVFXUtility.MoltenAmber);

            if (impacted)
                ShakeCamera(0.15f, chargeShakeMagnitude);
        }

        public void SpawnChargeAfterimage()
        {
            if (renderers == null || ghostMaterial == null)
                return;

            GameObject root = new GameObject("Dreadworm_ChargeAfterimage");
            root.transform.SetParent(VFXContainerUtility.GetContainer(), true);
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            List<SpriteRenderer> ghosts = new List<SpriteRenderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer source = renderers[i];
                if (source == null || !source.enabled || !source.gameObject.activeInHierarchy || source.sprite == null)
                    continue;

                GameObject part = new GameObject(source.gameObject.name + "_Echo");
                part.transform.SetParent(root.transform, true);
                part.transform.position = source.transform.position;
                part.transform.rotation = source.transform.rotation;
                part.transform.localScale = source.transform.lossyScale;

                SpriteRenderer ghost = part.AddComponent<SpriteRenderer>();
                ghost.sprite = source.sprite;
                ghost.sharedMaterial = ghostMaterial;
                ghost.color = new Color(0.28f, 1f, 1f, 0.34f);
                DreadwormVFXUtility.ConfigureRenderer(ghost, 3 + Mathf.Clamp(source.sortingOrder, 0, 4));
                ghosts.Add(ghost);
            }

            StartCoroutine(FadeAfterimage(root, ghosts, 0.28f));
        }

        public void BeginSlamTelegraph(float radius, float direction)
        {
            if (slamTelegraphRoot == null)
                BuildSlamTelegraph();

            slamRadius = Mathf.Max(0.8f, radius);
            slamDirection = Mathf.Approximately(direction, 0f) ? 1f : Mathf.Sign(direction);
            slamProgress = 0f;
            slamTelegraphActive = true;
            slamTelegraphRoot.SetActive(true);
            RefreshSlamTelegraph();
        }

        public void SetSlamTelegraphProgress(float normalizedProgress)
        {
            slamProgress = Mathf.Clamp01(normalizedProgress);
        }

        public void PlaySlamImpact(Vector3 attackPoint, float radius, float direction)
        {
            slamTelegraphActive = false;
            if (slamTelegraphRoot != null)
                slamTelegraphRoot.SetActive(false);

            float safeRadius = Mathf.Max(1f, radius);
            Vector3 groundPoint = GetGroundAlignedPosition(attackPoint);

            // High contrast Terracotta / Crimson Ground Debris & Shards
            SpawnGroundBurst(groundPoint, 34, safeRadius * 0.88f, direction);
            SpawnRadialBurst(groundPoint + Vector3.up * 0.35f, 26, safeRadius * 0.78f, DreadwormVFXUtility.BloodRubyCore, true);
            SpawnRadialBurst(groundPoint + Vector3.up * 0.2f, 16, safeRadius * 0.55f, DreadwormVFXUtility.MoltenAmber, true);

            // Dual high-intensity Seismic Shockwaves
            SpawnShockwave(
                groundPoint,
                new Vector2(safeRadius * 2.2f, safeRadius * 0.58f),
                0.45f,
                DreadwormVFXUtility.SeismicAmber);
            SpawnShockwave(
                groundPoint,
                new Vector2(safeRadius * 1.55f, safeRadius * 0.38f),
                0.3f,
                DreadwormVFXUtility.BloodRubyCore);

            ShakeCamera(0.2f, slamShakeMagnitude * 1.15f);
        }

        public void CancelSlamTelegraph()
        {
            slamTelegraphActive = false;
            if (slamTelegraphRoot != null)
                slamTelegraphRoot.SetActive(false);
        }

        public void BeginIceCast(float duration)
        {
            BeginCastAura(false, duration);
        }

        public void BeginSphereCast(float duration)
        {
            BeginCastAura(true, duration);
        }

        public void EndCast()
        {
            castAuraActive = false;
            if (castAuraRoot != null)
                castAuraRoot.SetActive(false);
        }


        public void BeginSpineEruptionTelegraph(Vector3[] points, float radius, float duration)
        {
            CancelSpineEruptionTelegraph();
            if (points == null || points.Length == 0)
                return;

            spineTelegraphPoints = (Vector3[])points.Clone();
            spineTelegraphRadius = Mathf.Max(0.3f, radius);
            spineTelegraphDuration = Mathf.Max(0.2f, duration);
            spineTelegraphStartTime = Time.time;
            spineTelegraphActive = true;

            spineTelegraphRoot = new GameObject("Dreadworm_SpineEruptionTelegraph");
            spineTelegraphRoot.transform.SetParent(VFXContainerUtility.GetContainer(), true);
            DreadwormVFXUtility.ConfigureSortingGroup(spineTelegraphRoot, 9);

            spineTelegraphFills = new SpriteRenderer[points.Length];
            spineTelegraphRings = new SpriteRenderer[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                // Layer 0: Concentrated Cryo Soft Disc Base (Telegraph Ground Glow)
                spineTelegraphFills[i] = CreateSpriteLayer(
                    spineTelegraphRoot,
                    "EruptionGroundDisc_" + i,
                    DreadwormVFXUtility.SoftDiscSprite,
                    glacialJadeAdditiveMaterial != null ? glacialJadeAdditiveMaterial : shardAdditiveMaterial,
                    8);

                // Layer 1: Converging Cryo Energy Pulse Ring
                spineTelegraphRings[i] = CreateSpriteLayer(
                    spineTelegraphRoot,
                    "EruptionCryoRing_" + i,
                    DreadwormVFXUtility.RingSprite,
                    glacialJadeAdditiveMaterial != null ? glacialJadeAdditiveMaterial : shardAdditiveMaterial,
                    10);
            }

            RefreshSpineEruptionTelegraph();
            if (dreadworm != null)
            {
                Bounds bounds = dreadworm.Collider2D != null
                    ? dreadworm.Collider2D.bounds
                    : new Bounds(dreadworm.CenterPosition, Vector3.one * 2f);
                SpawnGatherBurst(bounds.center + Vector3.up * bounds.extents.y * 0.7f, spineTelegraphDuration, false);
            }

            // --- Proposal 1: Cryo Crack Lines (LineRenderer toả từ mỗi điểm) ---
            spineTelegraphCrackLines = new LineRenderer[points.Length * 3];
            for (int i = 0; i < points.Length; i++)
            {
                for (int c = 0; c < 3; c++)
                {
                    int idx = i * 3 + c;
                    GameObject crackObj = new GameObject("CryoCrack_" + i + "_" + c);
                    crackObj.transform.SetParent(spineTelegraphRoot.transform, false);
                    LineRenderer lr = crackObj.AddComponent<LineRenderer>();
                    lr.material = glacialJadeAdditiveMaterial != null ? glacialJadeAdditiveMaterial : shardAdditiveMaterial;
                    lr.startColor = new Color(0.04f, 0.10f, 0.22f, 0.85f);  // GlacialDeepNavy
                    lr.endColor   = new Color(0.25f, 0.85f, 1.15f, 0f);      // CryoMistCyan → transparent
                    lr.startWidth = 0.06f;
                    lr.endWidth   = 0.01f;
                    lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    lr.receiveShadows = false;
                    float angle = (c / 3f) * 360f + i * 47f;
                    float len = Random.Range(0.7f, 1.4f) * spineTelegraphRadius * 2f;
                    Vector3 dir = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, 0f).normalized;
                    SetFissureLine(lr, points[i], points[i] + dir * len, 0.15f, i * 3.7f + c);
                    spineTelegraphCrackLines[idx] = lr;
                }
            }

            // --- Proposal 1: Ground Frost Bloom — Suction particle per spike point ---
            spineTelegraphSuctionSystems = new ParticleSystem[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                GameObject suctionRoot = new GameObject("FrostBloom_" + i);
                suctionRoot.transform.SetParent(spineTelegraphRoot.transform, true);
                suctionRoot.transform.position = points[i];
                spineTelegraphSuctionSystems[i] = CreateMeshFXParticleLayer(
                    suctionRoot, "SuctionParticles",
                    cryoMistAlphaMaterial != null ? cryoMistAlphaMaterial : frostAlphaMaterial,
                    9,
                    emissionRate: 18f,
                    particleLifetime: 0.55f,
                    particleSpeed: -2.8f,
                    particleSize: new Vector2(0.08f, 0.22f),
                    color: DreadwormVFXUtility.CryoMistCyan,
                    radius: spineTelegraphRadius * 1.6f);
                spineTelegraphSuctionSystems[i].Play();
            }
        }

        public void PulseSpineCrystal(Transform anchor, int sequenceIndex, int totalCount)
        {
            Vector3 position;
            if (anchor != null)
            {
                position = anchor.position;
            }
            else if (dreadworm != null && dreadworm.Collider2D != null)
            {
                Bounds bounds = dreadworm.Collider2D.bounds;
                float normalized = totalCount <= 1 ? 0.5f : Mathf.Clamp01(sequenceIndex / (totalCount - 1f));
                position = new Vector3(
                    Mathf.Lerp(bounds.min.x, bounds.max.x, normalized),
                    bounds.max.y - bounds.extents.y * 0.2f,
                    transform.position.z);
            }
            else
            {
                position = transform.position + Vector3.up;
            }

            // High-intensity Mint Jade & Diamond ripple pulse on boss spine
            SpawnRadialBurst(position, 10, 3.2f, DreadwormVFXUtility.GlacialJadeCore, true);
            SpawnShockwave(position, new Vector2(1.35f, 1.35f), 0.22f, DreadwormVFXUtility.GlacialApexDiamond);
        }

        public void PlaySpineEruptionImpact(Vector3 point, float radius, int index)
        {
            ConsumeSpineEruptionPoint(index);
            Vector3 groundPoint = GetGroundAlignedPosition(point);
            float safeRadius = Mathf.Max(0.35f, radius);

            // Tier determination (0-1: Tier 1, 2-3: Tier 2, 4-5+: Tier 3)
            int tier = index <= 1 ? 1 : (index <= 3 ? 2 : 3);
            float tierCamShake = tier == 1 ? 0.07f : (tier == 2 ? 0.12f : 0.20f);

            // 1. Single-Hierarchy Glacial Crystal Spike Pillar Visual (Proper World Scale)
            SpawnCrystalSpikeVisual(groundPoint, safeRadius, index, tier);

            // 2. Basalt/Rock ground debris burst
            SpawnBasaltGroundBurst(groundPoint, 10 + tier * 4, 3.8f + tier * 1.2f, dreadworm != null ? dreadworm.FacingDirection : 1f);

            // 3. Bioluminescent Glacial Ice Crystal Radial Shards
            SpawnRadialBurst(
                groundPoint + Vector3.up * 0.4f,
                12 + tier * 4,
                4.2f + tier * 1.2f,
                DreadwormVFXUtility.GlacialJadeCore,
                true);
            SpawnRadialBurst(
                groundPoint + Vector3.up * 0.8f,
                8 + tier * 3,
                3.2f + tier * 0.8f,
                DreadwormVFXUtility.GlacialApexDiamond,
                true);

            // 4. Ground Seismic Rupture Shockwave
            SpawnShockwave(
                groundPoint,
                new Vector2(safeRadius * (1.8f + tier * 0.4f), safeRadius * (0.5f + tier * 0.15f)),
                0.32f + tier * 0.06f,
                DreadwormVFXUtility.GlacialJadeCore);

            // --- Proposal 4: Eruption Impact Frame ---

            // 4a. 2nd Echo Shockwave Ring (delayed 0.12s, larger & Diamond-tinted)
            StartCoroutine(DelayedShockwave(
                groundPoint,
                new Vector2(safeRadius * (2.6f + tier * 0.5f), safeRadius * (0.35f + tier * 0.1f)),
                0.38f,
                DreadwormVFXUtility.GlacialApexDiamond,
                0.12f));

            // 4b. Impact Flash: 1-2 frame cyan burst (0.06s)
            StartCoroutine(SpawnImpactFlash(groundPoint + Vector3.up * 0.3f, tier));

            // 4c. Ground Shatter Chunks: low-angle ice fragments with gravity & rotation
            SpawnGroundShatterChunks(groundPoint, tier);

            // 5. Crescendo Screen Shake
            ShakeCamera(0.08f + tier * 0.04f, tierCamShake);
        }

        public void CancelSpineEruptionTelegraph()
        {
            spineTelegraphActive = false;
            DestroyRuntimeObject(spineTelegraphRoot);
            spineTelegraphRoot = null;
            spineTelegraphFills = null;
            spineTelegraphRings = null;
            spineTelegraphCrackLines = null;
            spineTelegraphSuctionSystems = null;
            spineTelegraphPoints = null;
        }

        public void BeginFrozenSanctuaryWindup(float duration)
        {
            sanctuaryWindupDuration = Mathf.Max(0.2f, duration);
            sanctuaryWindupStartTime = Time.time;
            sanctuaryWindupActive = true;

            if (sanctuaryWindupRoot == null)
            {
                sanctuaryWindupRoot = new GameObject("Dreadworm_SanctuaryWindup");
                sanctuaryWindupRoot.transform.SetParent(VFXContainerUtility.GetContainer(), true);
                DreadwormVFXUtility.ConfigureSortingGroup(sanctuaryWindupRoot, 10);

                windupGroundRuneA = CreateSpriteLayer(
                    sanctuaryWindupRoot,
                    "GroundRune_Outer",
                    DreadwormVFXUtility.RingSprite,
                    stormAdditiveMaterial != null ? stormAdditiveMaterial : frostAdditiveMaterial,
                    10);

                windupGroundRuneB = CreateSpriteLayer(
                    sanctuaryWindupRoot,
                    "GroundRune_Inner",
                    DreadwormVFXUtility.RingSprite,
                    frostAdditiveMaterial != null ? frostAdditiveMaterial : brightAdditiveMaterial,
                    11);

                windupCorePulse = CreateSpriteLayer(
                    sanctuaryWindupRoot,
                    "CorePulse",
                    DreadwormVFXUtility.SoftDiscSprite,
                    brightAdditiveMaterial,
                    12);

                windupManaVortexSystem = CreateMeshFXParticleLayer(
                    sanctuaryWindupRoot,
                    "ManaVortexParticles",
                    shardAdditiveMaterial != null ? shardAdditiveMaterial : brightAdditiveMaterial,
                    13,
                    emissionRate: 40f,
                    particleLifetime: 0.7f,
                    particleSpeed: -5.5f, // Inward suction
                    particleSize: new Vector2(0.12f, 0.38f),
                    color: DreadwormVFXUtility.PlasmaElectricCyan,
                    radius: 4.8f);
            }

            sanctuaryWindupRoot.SetActive(true);
            if (windupManaVortexSystem != null)
                windupManaVortexSystem.Play();

            RefreshSanctuaryWindup();

            // Khởi động luồng khí lạnh tụ hợp cực mạnh
            Vector3 center = dreadworm != null ? dreadworm.CenterPosition : transform.position;
            SpawnGatherBurst(center, sanctuaryWindupDuration, true);
            ShakeCamera(sanctuaryWindupDuration * 0.5f, 0.045f);
        }

        public void EndFrozenSanctuaryWindup()
        {
            sanctuaryWindupActive = false;
            if (windupManaVortexSystem != null)
                windupManaVortexSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (sanctuaryWindupRoot != null)
                sanctuaryWindupRoot.SetActive(false);
        }

        private void RefreshSanctuaryWindup()
        {
            if (dreadworm == null || sanctuaryWindupRoot == null)
                return;

            Vector3 center = dreadworm.CenterPosition;
            Vector3 groundPos = GetGroundAlignedPosition(center);
            sanctuaryWindupRoot.transform.position = groundPos;

            float elapsed = Time.time - sanctuaryWindupStartTime;
            float normalized = Mathf.Clamp01(elapsed / sanctuaryWindupDuration);
            float eased = DreadwormVFXUtility.EaseInOut(normalized);
            float pulse = 0.85f + Mathf.Sin(Time.time * 16f) * 0.15f;

            // Vòng tròn cổ ngữ xoay tốc độ tăng dần theo thời gian tụ khí
            float spinSpeed = Mathf.Lerp(60f, 260f, eased);

            if (windupGroundRuneA != null)
            {
                float runeScale = Mathf.Lerp(1.2f, 5.8f, eased) * pulse;
                windupGroundRuneA.transform.position = groundPos;
                windupGroundRuneA.transform.localScale = new Vector3(runeScale, runeScale * 0.35f, 1f);
                windupGroundRuneA.transform.rotation = Quaternion.Euler(0f, 0f, Time.time * spinSpeed);
                windupGroundRuneA.color = new Color(0.2f, 0.6f, 1.4f, Mathf.Lerp(0.3f, 0.95f, eased));
            }

            if (windupGroundRuneB != null)
            {
                float runeScale = Mathf.Lerp(0.8f, 4.2f, eased);
                windupGroundRuneB.transform.position = groundPos;
                windupGroundRuneB.transform.localScale = new Vector3(runeScale, runeScale * 0.35f, 1f);
                windupGroundRuneB.transform.rotation = Quaternion.Euler(0f, 0f, -Time.time * spinSpeed * 1.35f);
                windupGroundRuneB.color = new Color(0.45f, 1.2f, 2.2f, Mathf.Lerp(0.4f, 1f, eased) * pulse);
            }

            if (windupCorePulse != null)
            {
                windupCorePulse.transform.position = center;
                float coreScale = Mathf.Lerp(0.5f, 3.2f, eased) * (0.8f + Mathf.Sin(Time.time * 24f) * 0.2f);
                windupCorePulse.transform.localScale = new Vector3(coreScale * 1.2f, coreScale * 0.75f, 1f);
                windupCorePulse.color = new Color(0.8f, 1.5f, 2.4f, Mathf.Lerp(0.2f, 0.85f, eased));
            }
        }

        public void BeginFrozenSanctuaryShield(float duration)
        {
            EndFrozenSanctuaryWindup();
            cocoonStartTime = Time.time;
            cocoonDuration = Mathf.Max(1f, duration);
            cocoonHealthRatio = 1f;
            cocoonShieldActive = true;

            if (cocoonShieldRoot == null)
            {
                cocoonShieldRoot = new GameObject("Dreadworm_CocoonShield");
                cocoonShieldRoot.transform.SetParent(VFXContainerUtility.GetContainer(), true);
                DreadwormVFXUtility.ConfigureSortingGroup(cocoonShieldRoot, 14);

                cocoonCoreShield = CreateSpriteLayer(
                    cocoonShieldRoot,
                    "CocoonCoreShield",
                    DreadwormVFXUtility.SoftDiscSprite,
                    frozenCocoonMaterial != null ? frozenCocoonMaterial : frostAlphaMaterial,
                    14);

                cocoonOuterRing = CreateSpriteLayer(
                    cocoonShieldRoot,
                    "CocoonOuterRing",
                    DreadwormVFXUtility.RingSprite,
                    frostAlphaMaterial,
                    15);

                cocoonHexRing = CreateSpriteLayer(
                    cocoonShieldRoot,
                    "CocoonHexRing",
                    DreadwormVFXUtility.RingSprite,
                    shardAdditiveMaterial,
                    16);

                cocoonOrbitCrystals = new SpriteRenderer[6];
                for (int i = 0; i < 6; i++)
                {
                    cocoonOrbitCrystals[i] = CreateSpriteLayer(
                        cocoonShieldRoot,
                        "OrbitCrystal_" + i,
                        DreadwormVFXUtility.ShardSprite,
                        shardAdditiveMaterial,
                        17);
                }

                // --- MeshFX_Frost Particle Systems (Pure Particle Layers) ---
                // 1. Frost Aura Particles (surrounding floating crystals)
                cocoonFrostAuraSystem = CreateMeshFXParticleLayer(
                    cocoonShieldRoot,
                    "FrostAura",
                    frostAuraMaterial != null ? frostAuraMaterial : shardAdditiveMaterial,
                    15,
                    emissionRate: 14f,
                    particleLifetime: 1.4f,
                    particleSpeed: 1.1f,
                    particleSize: new Vector2(0.2f, 0.45f),
                    color: new Color(0.4f, 0.75f, 0.95f, 0.6f),
                    radius: 3.2f);

                // 2. AlphaSmoke Particles (dense freezing shroud enveloping the boss)
                cocoonAlphaSmokeSystem = CreateMeshFXParticleLayer(
                    cocoonShieldRoot,
                    "AlphaSmoke",
                    alphaSmokeMaterial != null ? alphaSmokeMaterial : frostAlphaMaterial,
                    13,
                    emissionRate: 24f,
                    particleLifetime: 2.0f,
                    particleSpeed: 0.7f,
                    particleSize: new Vector2(1.6f, 3.0f),
                    color: new Color(0.25f, 0.55f, 0.75f, 0.45f),
                    radius: 2.6f);

                // 3. Speckles Particles (glittering frost apex dust)
                cocoonSpecklesSystem = CreateMeshFXParticleLayer(
                    cocoonShieldRoot,
                    "Speckles",
                    specklesMaterial != null ? specklesMaterial : shardAdditiveMaterial,
                    16,
                    emissionRate: 25f,
                    particleLifetime: 1.0f,
                    particleSpeed: 1.5f,
                    particleSize: new Vector2(0.08f, 0.2f),
                    color: new Color(0.5f, 0.85f, 1.1f, 0.75f),
                    radius: 3.6f);

                // 4. Ice Crystals Outer Cluster
                cocoonIceCrystalsSystem = CreateMeshFXParticleLayer(
                    cocoonShieldRoot,
                    "IceCrystals",
                    iceCrystalMaterial != null ? iceCrystalMaterial : shardAdditiveMaterial,
                    16,
                    emissionRate: 12f,
                    particleLifetime: 1.2f,
                    particleSpeed: 1.2f,
                    particleSize: new Vector2(0.35f, 0.75f),
                    color: new Color(0.45f, 0.8f, 1.05f, 0.75f),
                    radius: 3.8f);
            }

            cocoonShieldRoot.SetActive(true);
            SetMeshFXCocoonSystemsActive(true);
            RefreshCocoonShield();

            // Initial Cocoon Crystallization Burst
            Vector3 center = dreadworm != null ? dreadworm.CenterPosition : transform.position;
            SpawnRadialBurst(center, 24, 6f, DreadwormVFXUtility.PlasmaElectricCyan, true);
            SpawnShockwave(center, new Vector2(4.5f, 2.2f), 0.4f, DreadwormVFXUtility.FrostCyan);
            ShakeCamera(0.18f, 0.08f);
        }

        public void UpdateCocoonShieldHealth(float healthRatio, Vector3? hitPoint = null)
        {
            cocoonHealthRatio = Mathf.Clamp01(healthRatio);
            TriggerHitFlash();
            Vector3 center = dreadworm != null ? dreadworm.CenterPosition : transform.position;
            Vector3 impactLoc = hitPoint.HasValue ? hitPoint.Value : center;

            // 1. Tia va chạm nổ bung mảnh pha lê băng cực quang tại điểm trúng (Apex Ice Shard Splinters)
            SpawnRadialBurst(impactLoc, 24, 7.2f, DreadwormVFXUtility.GlintFrostApex, true);
            SpawnRadialBurst(impactLoc, 16, 5.0f, DreadwormVFXUtility.PlasmaElectricCyan, true);

            // 2. Vòng tròn xung kích cộng hưởng va đập kén băng (Cocoon Surface Resonance Ring)
            SpawnShockwave(impactLoc, new Vector2(4.2f, 2.2f), 0.28f, DreadwormVFXUtility.PlasmaElectricCyan);
            SpawnShockwave(center, new Vector2(5.5f, 2.8f), 0.35f, DreadwormVFXUtility.FrostCyan);

            // 3. Rung chấn Camera cảm nhận lực va đập mạnh từ thiên thạch băng rơi
            ShakeCamera(0.18f, 0.14f);
        }

        public void EndFrozenSanctuaryShield(bool shattered)
        {
            cocoonShieldActive = false;
            SetMeshFXCocoonSystemsActive(false);
            if (cocoonShieldRoot != null)
                cocoonShieldRoot.SetActive(false);

            Vector3 center = dreadworm != null ? dreadworm.CenterPosition : transform.position;
            if (shattered)
            {
                // Massive AAA Shell Shatter Nova Burst
                SpawnRadialBurst(center, 46, 10.5f, DreadwormVFXUtility.PlasmaElectricCyan, true);
                SpawnRadialBurst(center, 32, 7.5f, DreadwormVFXUtility.GlintFrostApex, true);
                SpawnRadialBurst(center, 18, 5.0f, DreadwormVFXUtility.CrystalWhite, true);
                SpawnShockwave(center, new Vector2(7.8f, 3.8f), 0.5f, DreadwormVFXUtility.PlasmaElectricCyan);
                SpawnShockwave(center, new Vector2(5.2f, 2.6f), 0.32f, DreadwormVFXUtility.GlintFrostApex);
                ShakeCamera(0.35f, 0.22f);
            }
        }

        private void SetMeshFXCocoonSystemsActive(bool active)
        {
            if (active)
            {
                if (cocoonFrostAuraSystem != null) cocoonFrostAuraSystem.Play();
                if (cocoonAlphaSmokeSystem != null) cocoonAlphaSmokeSystem.Play();
                if (cocoonSpecklesSystem != null) cocoonSpecklesSystem.Play();
                if (cocoonIceCrystalsSystem != null) cocoonIceCrystalsSystem.Play();
            }
            else
            {
                if (cocoonFrostAuraSystem != null) cocoonFrostAuraSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (cocoonAlphaSmokeSystem != null) cocoonAlphaSmokeSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (cocoonSpecklesSystem != null) cocoonSpecklesSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (cocoonIceCrystalsSystem != null) cocoonIceCrystalsSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private static ParticleSystem CreateMeshFXParticleLayer(
            GameObject parent,
            string layerName,
            Material material,
            int sortingOrder,
            float emissionRate,
            float particleLifetime,
            float particleSpeed,
            Vector2 particleSize,
            Color color,
            float radius)
        {
            GameObject layerObj = new GameObject(layerName);
            layerObj.transform.SetParent(parent.transform, false);
            layerObj.transform.localPosition = Vector3.zero;

            ParticleSystem ps = layerObj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = particleLifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(particleSpeed * 0.4f, particleSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(particleSize.x, particleSize.y);
            main.startColor = color;
            main.gravityModifier = -0.01f;
            main.maxParticles = 80;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = emissionRate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.radiusThickness = 0.5f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(color.a, 0.2f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            var renderer = layerObj.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            DreadwormVFXUtility.ConfigureRenderer(renderer, sortingOrder);

            return ps;
        }

        private void RefreshCocoonShield()
        {
            if (dreadworm == null || cocoonShieldRoot == null)
                return;

            Vector3 center = dreadworm.CenterPosition;
            cocoonShieldRoot.transform.position = center;

            float pulse = 0.94f + Mathf.Sin(Time.time * 6f) * 0.06f;
            float shieldScale = (3.4f + Mathf.Sin(Time.time * 4f) * 0.15f) * (0.65f + cocoonHealthRatio * 0.35f);

            if (cocoonCoreShield != null)
            {
                cocoonCoreShield.transform.localScale = new Vector3(shieldScale * 1.8f, shieldScale * 1.1f, 1f);
                Color c = new Color(1f, 1f, 1f, (0.55f + cocoonHealthRatio * 0.45f) * pulse);
                cocoonCoreShield.color = c;

                if (frozenCocoonMaterial != null)
                {
                    frozenCocoonMaterial.SetFloat("_HealthRatio", cocoonHealthRatio);
                    frozenCocoonMaterial.SetFloat("_HitFlash", hitFlash);
                    frozenCocoonMaterial.SetFloat("_Aspect", 1.63f);
                }
            }

            if (cocoonOuterRing != null)
            {
                cocoonOuterRing.transform.localScale = new Vector3(shieldScale * 2.05f * pulse, shieldScale * 1.25f * pulse, 1f);
                cocoonOuterRing.transform.rotation = Quaternion.Euler(0f, 0f, Time.time * 40f);
                Color c = new Color(0.35f, 0.8f, 1.1f, 0.6f * cocoonHealthRatio);
                cocoonOuterRing.color = c;
            }

            if (cocoonHexRing != null)
            {
                cocoonHexRing.transform.localScale = new Vector3(shieldScale * 1.55f, shieldScale * 0.95f, 1f);
                cocoonHexRing.transform.rotation = Quaternion.Euler(0f, 0f, -Time.time * 55f);
                Color c = new Color(0.2f, 0.55f, 0.85f, 0.45f * cocoonHealthRatio);
                cocoonHexRing.color = c;
            }

            if (cocoonOrbitCrystals != null)
            {
                for (int i = 0; i < cocoonOrbitCrystals.Length; i++)
                {
                    if (cocoonOrbitCrystals[i] == null) continue;
                    float angle = (i * 60f + Time.time * 80f) * Mathf.Deg2Rad;
                    Vector3 offset = new Vector3(Mathf.Cos(angle) * shieldScale * 1.15f, Mathf.Sin(angle) * shieldScale * 0.65f, 0f);
                    cocoonOrbitCrystals[i].transform.localPosition = offset;
                    cocoonOrbitCrystals[i].transform.rotation = Quaternion.Euler(0f, 0f, (angle * Mathf.Rad2Deg) - 90f);
                    cocoonOrbitCrystals[i].transform.localScale = new Vector3(0.45f, 1.1f, 1f) * pulse;
                    cocoonOrbitCrystals[i].color = new Color(0.45f, 0.85f, 1.15f, 0.85f * cocoonHealthRatio);
                }
            }
        }

        public void BeginStaggerVisuals(float duration)
        {
            staggerStartTime = Time.time;
            staggerDuration = Mathf.Max(0.5f, duration);
            staggerActive = true;

            if (staggerStarsRoot == null)
            {
                staggerStarsRoot = new GameObject("Dreadworm_StaggerStars");
                staggerStarsRoot.transform.SetParent(VFXContainerUtility.GetContainer(), true);
                DreadwormVFXUtility.ConfigureSortingGroup(staggerStarsRoot, 16);

                staggerStars = new SpriteRenderer[4];
                for (int i = 0; i < 4; i++)
                {
                    staggerStars[i] = CreateSpriteLayer(
                        staggerStarsRoot,
                        "StaggerStar_" + i,
                        DreadwormVFXUtility.ShardSprite,
                        brightAdditiveMaterial,
                        17);
                }
            }

            staggerStarsRoot.SetActive(true);
            RefreshStaggerStars();
        }

        public void EndStaggerVisuals()
        {
            staggerActive = false;
            if (staggerStarsRoot != null)
                staggerStarsRoot.SetActive(false);
        }

        private void RefreshStaggerStars()
        {
            if (dreadworm == null || staggerStarsRoot == null) return;

            Vector3 headPos = dreadworm.CenterPosition + Vector3.up * 1.8f;
            staggerStarsRoot.transform.position = headPos;

            if (staggerStars != null)
            {
                for (int i = 0; i < staggerStars.Length; i++)
                {
                    if (staggerStars[i] == null) continue;
                    float angle = (i * 90f + Time.time * 220f) * Mathf.Deg2Rad;
                    Vector3 offset = new Vector3(Mathf.Cos(angle) * 1.35f, Mathf.Sin(angle) * 0.45f + Mathf.Sin(Time.time * 6f + i) * 0.15f, 0f);
                    staggerStars[i].transform.localPosition = offset;
                    staggerStars[i].transform.rotation = Quaternion.Euler(0f, 0f, Time.time * 300f);
                    staggerStars[i].transform.localScale = new Vector3(0.45f, 0.45f, 1f);
                    staggerStars[i].color = new Color(1.8f, 1.9f, 2.5f, 0.95f);
                }
            }
        }

        public void TriggerHitFlash()
        {
            if (hitFlashCoroutine != null)
                StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
        }

        public void BeginPhaseTransition(float duration)
        {
            SetPhase(BossPhase.Phase2);
            SpawnGatherBurst(dreadworm.CenterPosition, Mathf.Max(0.5f, duration), true);
            ShakeCamera(Mathf.Min(0.5f, duration * 0.38f), 0.075f);
        }

        public void SetPhase(BossPhase phase)
        {
            float phaseLevel = phase == BossPhase.Phase2 ? 2f : 1f;
            if (bossMaterials == null)
                return;

            for (int i = 0; i < bossMaterials.Length; i++)
            {
                Material material = bossMaterials[i];
                if (material != null && material.HasProperty("_PhaseLevel"))
                    material.SetFloat("_PhaseLevel", phaseLevel);
            }
        }

        private void CreateEffectMaterials()
        {
            frostAdditiveMaterial = Track(DreadwormVFXUtility.CreateMaterial(
                crystalGlowColor, true, DreadwormVFXUtility.SoftDiscTexture, "Dreadworm_FrostAdditive"));
            frostAlphaMaterial = Track(DreadwormVFXUtility.CreateMaterial(
                new Color(0.18f, 0.72f, 0.78f, 0.68f), false, DreadwormVFXUtility.SoftDiscTexture, "Dreadworm_FrostAlpha"));
            brightAdditiveMaterial = Track(DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.CrystalWhite, true, DreadwormVFXUtility.SoftDiscTexture, "Dreadworm_BrightAdditive"));
            stormAdditiveMaterial = Track(DreadwormVFXUtility.CreateMaterial(
                stormGlowColor, true, DreadwormVFXUtility.RingTexture, "Dreadworm_StormAdditive"));
            dustAlphaMaterial = Track(DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.GroundDust, false, DreadwormVFXUtility.SoftDiscTexture, "Dreadworm_DustAlpha"));
            shardAdditiveMaterial = Track(DreadwormVFXUtility.CreateMaterial(
                crystalGlowColor, true, DreadwormVFXUtility.ShardTexture, "Dreadworm_ShardAdditive"));
            shardAlphaMaterial = Track(DreadwormVFXUtility.CreateMaterial(
                new Color(0.16f, 0.32f, 0.34f, 0.92f), false, DreadwormVFXUtility.ShardTexture, "Dreadworm_ShardAlpha"));
            streakMaterial = Track(DreadwormVFXUtility.CreateMaterial(
                crystalGlowColor, true, DreadwormVFXUtility.StreakTexture, "Dreadworm_Streak"));
            ghostMaterial = Track(DreadwormVFXUtility.CreateMaterial(
                new Color(0.22f, 1.1f, 1.3f, 0.7f), false, null, "Dreadworm_Ghost"));
            frozenCocoonMaterial = Track(DreadwormVFXUtility.CreateFrozenCocoonMaterial("Dreadworm_FrozenCocoonMat"));

            // Ruby & Crimson Materials
            rubyAdditiveMaterial = Track(DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.BloodRubyCore, true, DreadwormVFXUtility.RingTexture, "Dreadworm_RubyAdditive"));
            amberAdditiveMaterial = Track(DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.MoltenAmber, true, DreadwormVFXUtility.RingTexture, "Dreadworm_AmberAdditive"));
            maroonAlphaMaterial = Track(DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.DeepMaroonAlpha, false, DreadwormVFXUtility.SoftDiscTexture, "Dreadworm_MaroonAlpha"));
            terracottaDustMaterial = Track(DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.EruptionDust, false, DreadwormVFXUtility.SoftDiscTexture, "Dreadworm_TerracottaDust"));
            rubyShardAdditiveMaterial = Track(DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.BloodRubyCore, true, DreadwormVFXUtility.ShardTexture, "Dreadworm_RubyShardAdditive"));

            // Glacial Arctic Jade & Crystal Materials (Calibrated HDR & Alpha Clarity)
            glacialCrystalAlphaMaterial = Track(DreadwormVFXUtility.CreateMaterial(
                Color.white, false, null, "Dreadworm_GlacialCrystalAlpha"));
            if (glacialCrystalAlphaMaterial.HasProperty("_RimStrength"))
                glacialCrystalAlphaMaterial.SetFloat("_RimStrength", 0.45f);
            if (glacialCrystalAlphaMaterial.HasProperty("_PrismaticSpread"))
                glacialCrystalAlphaMaterial.SetFloat("_PrismaticSpread", 0.65f);
            if (glacialCrystalAlphaMaterial.HasProperty("_RimColor"))
                glacialCrystalAlphaMaterial.SetColor("_RimColor", DreadwormVFXUtility.GlacialApexDiamond);
            // Proposal 2: Crystal Body Shader — Vertical Gradient + Refraction + Pulse
            if (glacialCrystalAlphaMaterial.HasProperty("_GradientStrength"))
                glacialCrystalAlphaMaterial.SetFloat("_GradientStrength", 0.75f);
            if (glacialCrystalAlphaMaterial.HasProperty("_RefractionStrength"))
                glacialCrystalAlphaMaterial.SetFloat("_RefractionStrength", 0.45f);
            if (glacialCrystalAlphaMaterial.HasProperty("_PulseStrength"))
                glacialCrystalAlphaMaterial.SetFloat("_PulseStrength", 0.08f);
            if (glacialCrystalAlphaMaterial.HasProperty("_PulseSpeed"))
                glacialCrystalAlphaMaterial.SetFloat("_PulseSpeed", 5.5f);

            glacialJadeAdditiveMaterial = Track(DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.GlacialJadeCore, true, null, "Dreadworm_GlacialJadeAdditive"));
            if (glacialJadeAdditiveMaterial.HasProperty("_RimStrength"))
                glacialJadeAdditiveMaterial.SetFloat("_RimStrength", 0.6f);
            if (glacialJadeAdditiveMaterial.HasProperty("_PrismaticSpread"))
                glacialJadeAdditiveMaterial.SetFloat("_PrismaticSpread", 0.5f);
            if (glacialJadeAdditiveMaterial.HasProperty("_RimColor"))
                glacialJadeAdditiveMaterial.SetColor("_RimColor", DreadwormVFXUtility.GlacialApexDiamond);

            glacialDiamondAdditiveMaterial = Track(DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.GlacialApexDiamond, true, null, "Dreadworm_GlacialDiamondAdditive"));
            if (glacialDiamondAdditiveMaterial.HasProperty("_RimStrength"))
                glacialDiamondAdditiveMaterial.SetFloat("_RimStrength", 0.8f);

            basaltDebrisMaterial = Track(DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.BasaltRootDark, false, DreadwormVFXUtility.ShardTexture, "Dreadworm_BasaltDebris"));

            cryoMistAlphaMaterial = Track(DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.CryoMistCyan, false, DreadwormVFXUtility.SoftDiscTexture, "Dreadworm_CryoMistAlpha"));
        }

        private void SetupBossMaterials()
        {
            if (renderers == null || crystalBossShader == null)
                return;

            bossMaterials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer spriteRenderer = renderers[i];
                if (spriteRenderer == null)
                    continue;

                Material material = new Material(crystalBossShader)
                {
                    name = "Dreadworm_Crystal_" + spriteRenderer.gameObject.name,
                    hideFlags = HideFlags.DontSave
                };
                material.SetColor("_CrystalColor", crystalGlowColor);
                material.SetColor("_StormColor", stormGlowColor);
                material.SetColor("_EnrageVeinColor", enrageVeinColor);
                material.SetColor("_EnrageRimColor", enrageRimColor);
                material.SetFloat("_StateIntensity", 0f);
                material.SetFloat("_FrozenArmor", 0f);
                material.SetFloat("_ChargeProgress", 0f);
                material.SetFloat("_HitFlash", 0f);

                spriteRenderer.material = material;
                bossMaterials[i] = material;
                runtimeMaterials.Add(material);
            }
        }

        private void UpdateBossShader()
        {
            bool charging = dreadworm.CurrentState is DreadwormChargeState;
            bool slamming = dreadworm.CurrentState is DreadwormSlamState;
            bool erupting = dreadworm.CurrentState is DreadwormSpineEruptionState;
            bool sanctuary = dreadworm.CurrentState is DreadwormFrozenSanctuaryState;
            bool sanctuaryWindup = sanctuary && (dreadworm.CurrentState as DreadwormFrozenSanctuaryState).IsWindupPhase;
            bool sanctuaryShield = sanctuary && !sanctuaryWindup;
            bool casting = dreadworm.CurrentState is DreadwormIceArrowState ||
                           dreadworm.CurrentState is DreadwormSummonSphereState;

            float targetIntensity = charging ? 1f : slamming ? 0.78f : erupting ? 0.92f : sanctuaryShield ? 1.4f : sanctuaryWindup ? 1.15f : casting ? 0.58f : 0.08f;
            currentIntensity = Mathf.MoveTowards(
                currentIntensity,
                targetIntensity,
                Time.deltaTime * transitionSpeed);

            float targetFrozenArmor = (sanctuaryShield || cocoonShieldActive) ? (0.6f + cocoonHealthRatio * 0.4f) : (sanctuaryWindup ? 0.25f : 0f);
            currentFrozenArmor = Mathf.MoveTowards(
                currentFrozenArmor,
                targetFrozenArmor,
                Time.deltaTime * (sanctuaryWindup ? 1.5f : 3.5f));

            if (bossMaterials == null)
                return;

            for (int i = 0; i < bossMaterials.Length; i++)
            {
                Material material = bossMaterials[i];
                if (material == null)
                    continue;

                material.SetFloat("_StateIntensity", currentIntensity);
                material.SetFloat("_FrozenArmor", currentFrozenArmor);
                material.SetFloat("_ChargeProgress", charging ? chargeProgress : 0f);
                material.SetFloat("_HitFlash", hitFlash);
            }
        }

        private void BuildChargeTelegraph()
        {
            if (chargeTelegraphRoot != null)
                return;

            chargeTelegraphRoot = new GameObject("Dreadworm_ChargeTelegraph");
            chargeTelegraphRoot.transform.SetParent(VFXContainerUtility.GetContainer(), true);
            DreadwormVFXUtility.ConfigureSortingGroup(chargeTelegraphRoot, 5);

            chargeBackdropLine = CreateLine(
                chargeTelegraphRoot,
                "FissureGroundStain",
                maroonAlphaMaterial,
                6,
                chargeLaneWidth,
                chargeLaneWidth * 0.78f);
            chargeFissureLine = CreateLine(chargeTelegraphRoot, "CrystalFissure", rubyShardAdditiveMaterial, 9, 0.14f, 0.045f);
            chargeFissureBranchTop = CreateLine(chargeTelegraphRoot, "CrystalFissureBranch_Top", amberAdditiveMaterial, 8, 0.06f, 0.018f);
            chargeFissureBranchBottom = CreateLine(chargeTelegraphRoot, "CrystalFissureBranch_Bottom", amberAdditiveMaterial, 8, 0.06f, 0.018f);
            chargeOriginGlow = CreateSpriteLayer(
                chargeTelegraphRoot,
                "CrystalThroatGlow",
                DreadwormVFXUtility.ShardSprite,
                rubyShardAdditiveMaterial,
                10);
            chargeFissureEndGlow = CreateSpriteLayer(
                chargeTelegraphRoot,
                "FissureEndGlow",
                DreadwormVFXUtility.SoftDiscSprite,
                amberAdditiveMaterial,
                11);

            chargeFissureShards = new SpriteRenderer[Mathf.Max(3, chargeFissureShardCount)];
            for (int i = 0; i < chargeFissureShards.Length; i++)
            {
                chargeFissureShards[i] = CreateSpriteLayer(
                    chargeTelegraphRoot,
                    "FissureCrystal_" + i,
                    DreadwormVFXUtility.ShardSprite,
                    i % 2 == 0 ? rubyShardAdditiveMaterial : amberAdditiveMaterial,
                    10);
            }

            chargeTelegraphRoot.SetActive(false);
        }

        private void RefreshChargeTelegraph()
        {
            if (dreadworm == null || chargeTelegraphRoot == null)
                return;

            Bounds bounds = dreadworm.Collider2D != null
                ? dreadworm.Collider2D.bounds
                : new Bounds(transform.position, Vector3.one * 2f);
            float groundY = bounds.min.y + Mathf.Max(0.24f, bounds.extents.y * 0.2f);
            Vector3 start = new Vector3(
                bounds.center.x + chargeDirection * bounds.extents.x * 0.72f,
                groundY,
                transform.position.z);

            float displayedDistance = chargeTravelDistance;
            if (dreadworm.Target != null)
            {
                float projectedTargetDistance =
                    (dreadworm.Target.position.x - start.x) * chargeDirection;
                if (projectedTargetDistance > 0f)
                {
                    displayedDistance = Mathf.Clamp(
                        projectedTargetDistance + 1.35f,
                        3.2f,
                        chargeTravelDistance);
                }
            }
            Vector3 end = start + Vector3.right * chargeDirection * displayedDistance;

            float easedProgress = DreadwormVFXUtility.EaseInOut(chargeProgress);
            SetLine(chargeBackdropLine, start, end);
            SetFissureLine(chargeFissureLine, start, end, chargeLaneWidth * 0.12f, 1.8f);

            Vector3 topBranchStart = Vector3.Lerp(start, end, 0.27f);
            Vector3 topBranchEnd = topBranchStart + Vector3.up * chargeLaneWidth * 0.6f + Vector3.right * chargeDirection * 0.42f;
            Vector3 bottomBranchStart = Vector3.Lerp(start, end, 0.64f);
            Vector3 bottomBranchEnd = bottomBranchStart - Vector3.up * chargeLaneWidth * 0.48f + Vector3.right * chargeDirection * 0.32f;
            SetFissureLine(chargeFissureBranchTop, topBranchStart, topBranchEnd, 0.07f, 3.1f);
            SetFissureLine(chargeFissureBranchBottom, bottomBranchStart, bottomBranchEnd, 0.06f, 4.4f);

            chargeBackdropLine.startWidth = chargeLaneWidth * Mathf.Lerp(0.66f, 0.92f, easedProgress);
            chargeBackdropLine.endWidth = chargeLaneWidth * Mathf.Lerp(0.5f, 0.76f, easedProgress);
            SetLineAlpha(chargeBackdropLine, Mathf.Lerp(0.09f, 0.25f, easedProgress));
            SetLineAlpha(chargeFissureLine, Mathf.Lerp(0.3f, 0.86f, easedProgress));
            SetLineAlpha(chargeFissureBranchTop, Mathf.Lerp(0f, 0.52f, easedProgress));
            SetLineAlpha(chargeFissureBranchBottom, Mathf.Lerp(0f, 0.46f, easedProgress));
            chargeFissureLine.startWidth = Mathf.Lerp(0.05f, 0.15f, easedProgress);

            chargeOriginGlow.transform.position = start;
            chargeOriginGlow.transform.rotation = Quaternion.Euler(0f, chargeDirection < 0f ? 180f : 0f, -18f * chargeDirection);
            chargeOriginGlow.transform.localScale = new Vector3(
                chargeLaneWidth * Mathf.Lerp(0.28f, 0.52f, easedProgress),
                chargeLaneWidth * Mathf.Lerp(0.55f, 0.9f, easedProgress),
                1f);
            chargeOriginGlow.color = new Color(2.1f, 0.35f, 0.45f, Mathf.Lerp(0.25f, 0.95f, easedProgress));

            chargeFissureEndGlow.transform.position = end + Vector3.up * 0.02f;
            chargeFissureEndGlow.transform.localScale = new Vector3(
                chargeLaneWidth * Mathf.Lerp(0.45f, 0.82f, easedProgress),
                chargeLaneWidth * Mathf.Lerp(0.2f, 0.36f, easedProgress),
                1f);
            chargeFissureEndGlow.color = new Color(2.0f, 0.75f, 0.2f, Mathf.Lerp(0.15f, 0.85f, easedProgress));

            for (int i = 0; i < chargeFissureShards.Length; i++)
            {
                float normalized = (i + 1f) / (chargeFissureShards.Length + 1f);
                SpriteRenderer shard = chargeFissureShards[i];
                float sideOffset = (i % 2 == 0 ? 1f : -1f) * chargeLaneWidth * 0.18f;
                shard.transform.position = Vector3.Lerp(start, end, normalized) + Vector3.up * sideOffset;
                shard.transform.rotation = Quaternion.Euler(0f, chargeDirection < 0f ? 180f : 0f, (i % 2 == 0 ? -1f : 1f) * 16f);
                shard.transform.localScale = new Vector3(
                    chargeLaneWidth * Mathf.Lerp(0.22f, 0.38f, easedProgress),
                    chargeLaneWidth * Mathf.Lerp(0.38f, 0.68f, easedProgress),
                    1f);

                float unlocked = DreadwormVFXUtility.EaseInOut(
                    Mathf.InverseLerp(normalized - 0.2f, normalized + 0.04f, easedProgress));
                float pulse = 0.82f + Mathf.Sin(Time.time * 8.5f + i * 1.7f) * 0.25f;
                Color shardColor = i % 2 == 0
                    ? new Color(2.1f, 0.3f, 0.4f, (0.35f + pulse * 0.65f) * unlocked)
                    : new Color(2.0f, 0.8f, 0.25f, (0.35f + pulse * 0.65f) * unlocked);
                shard.color = shardColor;
            }
        }

        private void UpdateChargeDashVFX()
        {
            float now = Time.time;
            if (now - lastAfterimageTime >= afterimageInterval)
            {
                lastAfterimageTime = now;
                SpawnChargeAfterimage();
            }

            float movedDistance = Vector3.Distance(transform.position, lastDebrisPosition);
            if (now - lastDebrisTime >= emitInterval && movedDistance >= 0.32f)
            {
                lastDebrisTime = now;
                lastDebrisPosition = transform.position;
                SpawnChargeGroundTrail();
            }
        }

        private void SpawnChargeGroundTrail()
        {
            Bounds bounds = dreadworm.Collider2D != null
                ? dreadworm.Collider2D.bounds
                : new Bounds(transform.position, Vector3.one);
            Vector3 spawnPosition = new Vector3(
                bounds.center.x - chargeDirection * bounds.extents.x * 0.55f,
                bounds.min.y + 0.12f,
                transform.position.z);

            SpawnGroundBurst(spawnPosition, 7, 2.6f, -chargeDirection);

            GameObject streakRoot = new GameObject("Dreadworm_ChargeSpeedStreak");
            streakRoot.transform.SetParent(VFXContainerUtility.GetContainer(), true);
            streakRoot.transform.position = bounds.center + Vector3.up * Random.Range(-0.4f, 0.65f);
            streakRoot.transform.rotation = Quaternion.Euler(0f, chargeDirection < 0f ? 180f : 0f, 0f);

            SpriteRenderer streak = streakRoot.AddComponent<SpriteRenderer>();
            streak.sprite = DreadwormVFXUtility.SoftDiscSprite;
            streak.sharedMaterial = streakMaterial;
            streak.color = new Color(0.45f, 1f, 1f, 0.45f);
            streak.transform.localScale = new Vector3(Random.Range(2.2f, 4.4f), Random.Range(0.04f, 0.11f), 1f);
            DreadwormVFXUtility.ConfigureRenderer(streak, 7);
            StartCoroutine(FadeSprite(streakRoot, streak, 0.2f, 1.45f));
        }

        // =========================================================================
        // Proposal 3: Bone-attached Frost Trail Emitters (Body bone 1 to 5)
        // =========================================================================
        private void BuildChargeTrailEmitters()
        {
            chargeTrailBones.Clear();
            chargeTrailDustSystems.Clear();
            chargeTrailSmokeSystems.Clear();

            // Find Body bone 1 to 5 on the Dreadworm hierarchy
            for (int i = 1; i <= 5; i++)
            {
                Transform bone = FindChildRecursive(transform, "Body bone " + i);
                if (bone != null)
                {
                    chargeTrailBones.Add(bone);
                }
            }

            // Fallback: If no named bones found, use crystal bone anchors or root
            if (chargeTrailBones.Count == 0)
            {
                chargeTrailBones.Add(transform);
            }

            chargeTrailEmittersRoot = new GameObject("Dreadworm_ChargeTrailEmitters");
            chargeTrailEmittersRoot.transform.SetParent(transform, false);

            for (int i = 0; i < chargeTrailBones.Count; i++)
            {
                Transform targetBone = chargeTrailBones[i];
                GameObject emitterObj = new GameObject("TrailEmitter_" + targetBone.name);
                emitterObj.transform.SetParent(targetBone, false);
                emitterObj.transform.localPosition = Vector3.zero;

                // 1. Dusts Emitter (Fine Frost Debris)
                ParticleSystem dustPS = emitterObj.AddComponent<ParticleSystem>();
                dustPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var dMain = dustPS.main;
                dMain.duration = 1f;
                dMain.loop = true;
                dMain.playOnAwake = false;
                dMain.simulationSpace = ParticleSystemSimulationSpace.World;
                dMain.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
                dMain.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.8f);
                dMain.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
                dMain.startColor = new Color(0.75f, 1.4f, 2.2f, 0.8f);
                dMain.gravityModifier = -0.02f;
                dMain.maxParticles = 60;

                var dEmission = dustPS.emission;
                dEmission.enabled = true;
                dEmission.rateOverTime = 0f;
                dEmission.rateOverDistance = 18f; // Triggers per movement distance

                var dShape = dustPS.shape;
                dShape.shapeType = ParticleSystemShapeType.Circle;
                dShape.radius = 0.4f;

                var dColor = dustPS.colorOverLifetime;
                dColor.enabled = true;
                Gradient dGrad = new Gradient();
                dGrad.SetKeys(
                    new[] { new GradientColorKey(new Color(0.4f, 1.2f, 2.2f), 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.15f), new GradientAlphaKey(0f, 1f) }
                );
                dColor.color = dGrad;

                var dRenderer = emitterObj.GetComponent<ParticleSystemRenderer>();
                dRenderer.sharedMaterial = dustAlphaMaterial;
                DreadwormVFXUtility.ConfigureRenderer(dRenderer, 12);
                chargeTrailDustSystems.Add(dustPS);

                // 2. AlphaSmoke Emitter (Dense Lingering Frost Wake)
                GameObject smokeChild = new GameObject("SmokeEmitter");
                smokeChild.transform.SetParent(emitterObj.transform, false);
                ParticleSystem smokePS = smokeChild.AddComponent<ParticleSystem>();
                smokePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                var sMain = smokePS.main;
                sMain.duration = 1f;
                sMain.loop = true;
                sMain.playOnAwake = false;
                sMain.simulationSpace = ParticleSystemSimulationSpace.World;
                sMain.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.95f);
                sMain.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.0f);
                sMain.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
                sMain.startColor = new Color(0.6f, 1.5f, 2.0f, 0.55f);
                sMain.gravityModifier = -0.05f;
                sMain.maxParticles = 40;

                var sEmission = smokePS.emission;
                sEmission.enabled = true;
                sEmission.rateOverTime = 0f;
                sEmission.rateOverDistance = 12f;

                var sShape = smokePS.shape;
                sShape.shapeType = ParticleSystemShapeType.Sphere;
                sShape.radius = 0.35f;

                var sSize = smokePS.sizeOverLifetime;
                sSize.enabled = true;
                sSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.2f));

                var sColor = smokePS.colorOverLifetime;
                sColor.enabled = true;
                Gradient sGrad = new Gradient();
                sGrad.SetKeys(
                    new[] { new GradientColorKey(new Color(0.2f, 0.6f, 1.8f), 0f), new GradientColorKey(new Color(0.8f, 1.8f, 2.2f), 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.2f), new GradientAlphaKey(0f, 1f) }
                );
                sColor.color = sGrad;

                var sRenderer = smokeChild.GetComponent<ParticleSystemRenderer>();
                sRenderer.sharedMaterial = frostAlphaMaterial;
                DreadwormVFXUtility.ConfigureRenderer(sRenderer, 11);
                chargeTrailSmokeSystems.Add(smokePS);
            }
        }

        private void SetChargeTrailEmittersActive(bool active)
        {
            for (int i = 0; i < chargeTrailDustSystems.Count; i++)
            {
                if (chargeTrailDustSystems[i] == null) continue;
                if (active)
                {
                    chargeTrailDustSystems[i].Play();
                }
                else
                {
                    chargeTrailDustSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            for (int i = 0; i < chargeTrailSmokeSystems.Count; i++)
            {
                if (chargeTrailSmokeSystems[i] == null) continue;
                if (active)
                {
                    chargeTrailSmokeSystems[i].Play();
                }
                else
                {
                    chargeTrailSmokeSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        private static Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent == null) return null;
            if (parent.name.Equals(childName, System.StringComparison.OrdinalIgnoreCase)) return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform result = FindChildRecursive(parent.GetChild(i), childName);
                if (result != null) return result;
            }
            return null;
        }

        private void BuildSlamTelegraph()
        {
            if (slamTelegraphRoot != null)
                return;

            slamTelegraphRoot = new GameObject("Dreadworm_SlamTelegraph");
            slamTelegraphRoot.transform.SetParent(VFXContainerUtility.GetContainer(), true);
            DreadwormVFXUtility.ConfigureSortingGroup(slamTelegraphRoot, 5);

            slamAreaFill = CreateSpriteLayer(
                slamTelegraphRoot,
                "SlamAreaFill",
                DreadwormVFXUtility.SoftDiscSprite,
                maroonAlphaMaterial,
                5);
            slamAreaRing = CreateSpriteLayer(
                slamTelegraphRoot,
                "SlamAreaRing",
                DreadwormVFXUtility.RingSprite,
                amberAdditiveMaterial,
                7);

            slamCracks = new LineRenderer[8];
            for (int i = 0; i < slamCracks.Length; i++)
            {
                slamCracks[i] = CreateLine(
                    slamTelegraphRoot,
                    "SlamCrack_" + i,
                    i % 2 == 0 ? rubyShardAdditiveMaterial : amberAdditiveMaterial,
                    9,
                    0.09f,
                    0.02f);
                slamCracks[i].positionCount = 4;
            }

            slamTelegraphRoot.SetActive(false);
        }

        private void RefreshSlamTelegraph()
        {
            Vector3 center = GetSlamGroundPosition();
            float eased = DreadwormVFXUtility.EaseInOut(slamProgress);
            float pulse = 0.8f + Mathf.Sin(Time.time * 12f) * 0.22f;

            slamAreaFill.transform.position = center;
            slamAreaFill.transform.localScale = new Vector3(
                slamRadius * 2f,
                slamRadius * 0.48f,
                1f);
            slamAreaFill.color = new Color(0.85f, 0.10f, 0.14f, Mathf.Lerp(0.18f, 0.65f, eased));

            slamAreaRing.transform.position = center;
            slamAreaRing.transform.rotation = Quaternion.identity;
            slamAreaRing.transform.localScale = new Vector3(
                slamRadius * 2f * Mathf.Lerp(1.12f, 0.96f, eased),
                slamRadius * 0.54f * Mathf.Lerp(1.12f, 0.96f, eased),
                1f);
            slamAreaRing.color = new Color(2.35f, 0.85f, 0.2f, Mathf.Lerp(0.35f, 1f, eased) * pulse);

            for (int i = 0; i < slamCracks.Length; i++)
            {
                float normalized = i / (slamCracks.Length - 1f);
                float vertical = Mathf.Lerp(-0.72f, 0.72f, normalized);
                Vector2 direction = new Vector2(
                    slamDirection * Mathf.Sqrt(Mathf.Max(0.05f, 1f - vertical * vertical)),
                    vertical * 0.24f).normalized;
                Vector2 tangent = new Vector2(-direction.y, direction.x);
                float length = slamRadius * Mathf.Lerp(0.6f, 1.05f, Mathf.Repeat(i * 0.618f, 1f)) * eased;
                Vector3[] points =
                {
                    center,
                    center + (Vector3)(direction * length * 0.32f + tangent * Mathf.Sin(i * 2.4f + Time.time * 3f) * 0.18f),
                    center + (Vector3)(direction * length * 0.68f - tangent * Mathf.Cos(i * 1.7f + Time.time * 2f) * 0.22f),
                    center + (Vector3)(direction * length)
                };
                slamCracks[i].SetPositions(points);
                SetLineAlpha(slamCracks[i], Mathf.Lerp(0.1f, 0.95f, eased) * pulse);
            }
        }

        private void BuildCastAura()
        {
            if (castAuraRoot != null)
                return;

            castAuraRoot = new GameObject("Dreadworm_CastAura");
            castAuraRoot.transform.SetParent(VFXContainerUtility.GetContainer(), true);
            DreadwormVFXUtility.ConfigureSortingGroup(castAuraRoot, 8);

            castCore = CreateSpriteLayer(
                castAuraRoot,
                "CastCore",
                DreadwormVFXUtility.SoftDiscSprite,
                brightAdditiveMaterial,
                9);
            castRingA = CreateSpriteLayer(
                castAuraRoot,
                "CastRing_A",
                DreadwormVFXUtility.RingSprite,
                frostAdditiveMaterial,
                7);
            castRingB = CreateSpriteLayer(
                castAuraRoot,
                "CastRing_B",
                DreadwormVFXUtility.RingSprite,
                stormAdditiveMaterial,
                8);
            castRingC = CreateSpriteLayer(
                castAuraRoot,
                "CastRing_C",
                DreadwormVFXUtility.RingSprite,
                frostAdditiveMaterial,
                8);
            castAuraRoot.SetActive(false);
        }

        private void BeginCastAura(bool isSphereCast, float duration)
        {
            if (castAuraRoot == null)
                BuildCastAura();

            sphereCast = isSphereCast;
            castDuration = Mathf.Max(0.2f, duration);
            castStartTime = Time.time;
            castAuraActive = true;
            castAuraRoot.SetActive(true);
            RefreshCastAura();
            SpawnGatherBurst(GetCastAuraPosition(), castDuration, sphereCast);
        }

        private void RefreshCastAura()
        {
            float normalized = Mathf.Clamp01((Time.time - castStartTime) / castDuration);
            float eased = DreadwormVFXUtility.EaseOutCubic(normalized);
            float pulse = 0.82f + Mathf.Sin(Time.time * (sphereCast ? 11f : 8f)) * 0.18f;
            Vector3 position = GetCastAuraPosition();
            castAuraRoot.transform.position = position;

            float baseScale = sphereCast ? 3.1f : 2.35f;
            castCore.transform.localScale = Vector3.one * baseScale * Mathf.Lerp(0.18f, 0.72f, eased) * pulse;
            castCore.color = sphereCast
                ? new Color(0.45f, 0.68f, 1f, Mathf.Lerp(0.15f, 0.8f, eased))
                : new Color(0.72f, 1f, 1f, Mathf.Lerp(0.12f, 0.72f, eased));

            ConfigureCastRing(castRingA, baseScale * 1.2f, Time.time * 90f, eased, 0.68f);
            ConfigureCastRing(castRingB, baseScale * 1.65f, -Time.time * 64f, eased, 0.5f);
            ConfigureCastRing(castRingC, baseScale * 2.05f, Time.time * 42f, eased, 0.34f);
        }

        private void ConfigureCastRing(
            SpriteRenderer ring,
            float scale,
            float rotation,
            float progress,
            float maximumAlpha)
        {
            ring.transform.localScale = Vector3.one * scale * Mathf.Lerp(1.35f, 0.92f, progress);
            ring.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
            Color color = sphereCast
                ? new Color(0.25f, 0.55f, 1f, maximumAlpha * progress)
                : new Color(0.45f, 1f, 1f, maximumAlpha * progress);
            ring.color = color;
        }

        private Vector3 GetCastAuraPosition()
        {
            if (dreadworm == null)
                return transform.position;

            if (sphereCast)
                return dreadworm.GetSphereSpawnPosition();

            Bounds bounds = dreadworm.Collider2D != null
                ? dreadworm.Collider2D.bounds
                : new Bounds(dreadworm.CenterPosition, Vector3.one * 2f);
            return new Vector3(
                bounds.center.x - dreadworm.FacingDirection * bounds.extents.x * 0.15f,
                bounds.max.y + 0.8f,
                transform.position.z);
        }

        private void RefreshSpineEruptionTelegraph()
        {
            if (spineTelegraphRoot == null || spineTelegraphPoints == null)
                return;

            float normalized = Mathf.Clamp01((Time.time - spineTelegraphStartTime) / spineTelegraphDuration);
            float eased = DreadwormVFXUtility.EaseInOut(normalized);

            for (int i = 0; i < spineTelegraphPoints.Length; i++)
            {
                SpriteRenderer fill = spineTelegraphFills != null && i < spineTelegraphFills.Length
                    ? spineTelegraphFills[i]
                    : null;
                SpriteRenderer ring = spineTelegraphRings != null && i < spineTelegraphRings.Length
                    ? spineTelegraphRings[i]
                    : null;
                if (fill == null || ring == null || !fill.gameObject.activeSelf)
                    continue;

                float sequence = Mathf.Repeat(Time.time * 5.5f - i * 0.15f, 1f);
                float pulse = 0.88f + (1f - Mathf.Abs(sequence * 2f - 1f)) * 0.25f;
                Vector3 position = spineTelegraphPoints[i];
                fill.transform.position = position;
                ring.transform.position = position;

                float width = spineTelegraphRadius * 2.2f;
                fill.transform.localScale = new Vector3(width * 1.25f, width * 0.45f, 1f) * Mathf.Lerp(0.65f, 1.05f, eased);
                ring.transform.localScale = new Vector3(
                    width * Mathf.Lerp(1.5f, 0.95f, eased) * pulse,
                    width * 0.42f * Mathf.Lerp(1.5f, 0.95f, eased) * pulse,
                    1f);
                ring.transform.rotation = Quaternion.Euler(0f, 0f, Time.time * (80f + i * 8f) * (i % 2 == 0 ? 1f : -1f));

                // Glacial Jade Ground Telegraph Core Glow
                Color fillColor = DreadwormVFXUtility.GlacialJadeCore;
                fillColor.a = Mathf.Lerp(0.25f, 0.75f, eased) * pulse;
                fill.color = fillColor;

                // Glowing Cyan-Diamond Ring pulse
                Color ringColor = Color.Lerp(
                    DreadwormVFXUtility.GlacialJadeCore,
                    DreadwormVFXUtility.GlacialApexDiamond,
                    pulse - 0.88f);
                ringColor.a = Mathf.Lerp(0.4f, 1f, eased) * pulse;
                ring.color = ringColor;
            }

            // Proposal 1: Crack lines fade in → fade out cuối telegraph
            if (spineTelegraphCrackLines != null)
            {
                float crackAlpha = normalized < 0.5f
                    ? Mathf.Lerp(0f, 0.85f, normalized * 2f)
                    : Mathf.Lerp(0.85f, 0.2f, (normalized - 0.5f) * 2f);
                foreach (LineRenderer lr in spineTelegraphCrackLines)
                {
                    if (lr != null) SetLineAlpha(lr, crackAlpha);
                }
            }
        }

        private void ConsumeSpineEruptionPoint(int index)
        {
            if (index < 0)
                return;
            if (spineTelegraphFills != null && index < spineTelegraphFills.Length && spineTelegraphFills[index] != null)
                spineTelegraphFills[index].gameObject.SetActive(false);
            if (spineTelegraphRings != null && index < spineTelegraphRings.Length && spineTelegraphRings[index] != null)
                spineTelegraphRings[index].gameObject.SetActive(false);
        }

        private void SpawnCrystalSpikeVisual(Vector3 position, float radius, int index, int tier)
        {
            GameObject root = new GameObject("Dreadworm_GlacialSpike_" + index + "_T" + tier);
            root.transform.SetParent(VFXContainerUtility.GetContainer(), true);
            root.transform.position = position;

            // Randomized tilt angle based on configurable variance
            float randomRotation = Random.Range(
                Mathf.Min(spikeRotationVariance.x, spikeRotationVariance.y),
                Mathf.Max(spikeRotationVariance.x, spikeRotationVariance.y));
            root.transform.rotation = Quaternion.Euler(0f, 0f, randomRotation);

            // Enforce SpikeTier1Sprite across all spawned crystal pillars
            Sprite spikeSprite = DreadwormVFXUtility.SpikeTier1Sprite;

            // Single Primary Sprite Renderer with Alpha-Blend Clarity (Zero whiteout blowout)
            SpriteRenderer spike = root.AddComponent<SpriteRenderer>();
            spike.sprite = spikeSprite;
            spike.sharedMaterial = glacialCrystalAlphaMaterial != null ? glacialCrystalAlphaMaterial : shardAlphaMaterial;
            spike.color = Color.white;
            DreadwormVFXUtility.ConfigureRenderer(spike, 16);

            // Randomized scale multiplier within user-configured range
            float randomScale = Random.Range(
                Mathf.Min(spikeScaleVariance.x, spikeScaleVariance.y),
                Mathf.Max(spikeScaleVariance.x, spikeScaleVariance.y));

            // Target World Dimensions (Tier 1: ~1.85m, Tier 2: ~2.45m, Tier 3: ~3.05m tall)
            float baseTargetHeight = tier == 1 ? 1.85f : (tier == 2 ? 2.45f : 3.05f);
            float baseTargetWidth = tier == 1 ? 0.95f : (tier == 2 ? 1.25f : 1.55f);

            float desiredHeight = baseTargetHeight * randomScale;
            float desiredWidth = baseTargetWidth * randomScale;

            Vector2 spriteBounds = (spikeSprite != null && spikeSprite.bounds.size.y > 0.001f)
                ? (Vector2)spikeSprite.bounds.size
                : Vector2.one * 2.5f;

            Vector3 targetScale = new Vector3(
                desiredWidth / spriteBounds.x,
                desiredHeight / spriteBounds.y,
                1f);

            root.transform.localScale = new Vector3(targetScale.x * 0.3f, 0.01f, 1f);
            float lifetime = 0.90f + tier * 0.15f;
            StartCoroutine(AnimateCrystalSpike(root, spike, targetScale, desiredHeight, lifetime, tier, spikeShatterAnchorY));
        }

        private IEnumerator AnimateCrystalSpike(
            GameObject root,
            SpriteRenderer spike,
            Vector3 targetScale,
            float desiredHeight,
            float duration,
            int tier,
            float shatterAnchorY)
        {
            float elapsed = 0f;
            // Phase breakdown: Eruption Thrust (0 -> 0.16s), Shimmer Hold (0.16 -> 0.72s), Shatter Dissolve (0.72 -> 1.0s)
            while (elapsed < duration && root != null)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);

                // Stage 1: Snappy Upward Spring Eruption
                if (normalized <= 0.18f)
                {
                    float t = normalized / 0.18f;
                    float springY;
                    if (t < 0.75f)
                    {
                        springY = Mathf.Lerp(0.01f, 1.08f, t / 0.75f);
                    }
                    else
                    {
                        float settle = (t - 0.75f) / 0.25f;
                        springY = Mathf.Lerp(1.08f, 1.0f, settle);
                    }

                    root.transform.localScale = new Vector3(
                        Mathf.Lerp(targetScale.x * 0.3f, targetScale.x, Mathf.Clamp01(t * 1.4f)),
                        targetScale.y * springY,
                        1f);
                }
                // Stage 2: Living Apex Shimmer & Area Denial
                else if (normalized <= 0.72f)
                {
                    float shimmer = 1.0f + Mathf.Sin((elapsed - 0.18f) * 6f) * 0.02f;
                    root.transform.localScale = new Vector3(targetScale.x * shimmer, targetScale.y, 1f);

                    if (spike != null)
                    {
                        spike.color = Color.white;
                    }
                }
                // Stage 3: Shatter Dissolve Fade
                else
                {
                    float dissolveT = (normalized - 0.72f) / 0.28f;
                    float fade = 1.0f - dissolveT;

                    // Trigger Shatter Nova ngay đầu Stage 3 (dissolveT ≈ 0) — đồng bộ với lúc trụ bắt đầu vỡ
                    if (dissolveT < Time.deltaTime / (duration * 0.28f) + 0.001f && root != null)
                    {
                        // anchor Y thấp hơn: nhân thêm 0.7 để nova spawn thấp xuống (~70% chiều cao)
                        float clampedAnchor = Mathf.Clamp(shatterAnchorY, 0f, 1.5f) * 0.7f;
                        Vector3 apexPos = root.transform.position
                            + root.transform.up * desiredHeight * clampedAnchor;
                        SpawnRadialBurst(apexPos, 14 + tier * 4, 4.5f + tier * 1.0f, DreadwormVFXUtility.GlacialApexDiamond, true);
                        StartCoroutine(ShatterNovaLayers(apexPos, tier));
                    }

                    if (spike != null)
                    {
                        Color c = Color.white;
                        c.a = fade;
                        spike.color = c;
                    }

                    root.transform.localScale = new Vector3(
                        targetScale.x * (1f + dissolveT * 0.15f),
                        targetScale.y * (1f - dissolveT * 0.1f),
                        1f);
                }

                yield return null;
            }

            // Destroy trụ băng sau khi animation kết thúc (Shatter Nova đã trigger ở đầu Stage 3)
            if (root != null)
                DestroyRuntimeObject(root);
        }

        private IEnumerator ShatterNovaLayers(Vector3 apexPos, int tier)
        {
            // Crystal Shards with gravity (rơi theo trọng lực thực)
            SpawnCrystalShardGravity(apexPos, tier);

            // Layer 2: Jade Burst staggered +0.05s
            yield return new WaitForSeconds(0.05f);
            SpawnRadialBurst(apexPos, 10 + tier * 3, 2.8f + tier * 0.6f, DreadwormVFXUtility.GlacialJadeCore, true);

            // Frost Explosion Ring tại đỉnh
            SpawnShockwave(apexPos,
                new Vector2(1.8f + tier * 0.5f, 1.8f + tier * 0.5f),
                0.22f,
                DreadwormVFXUtility.GlacialApexDiamond);

            // Layer 3: Fine Cryo Dust +0.10s tổng
            yield return new WaitForSeconds(0.05f);
            SpawnRadialBurst(apexPos, 20 + tier * 6, 1.5f + tier * 0.3f, DreadwormVFXUtility.CryoMistCyan, true);

            // Echo Light Pulse: SoftDisc scale 0→maxSize fade 0.15s
            StartCoroutine(SpawnEchoLightPulse(apexPos, tier));
        }

        private IEnumerator SpawnEchoLightPulse(Vector3 pos, int tier)
        {
            GameObject pulseRoot = new GameObject("Dreadworm_EchoLightPulse");
            pulseRoot.transform.SetParent(VFXContainerUtility.GetContainer(), true);
            pulseRoot.transform.position = pos;

            SpriteRenderer disc = pulseRoot.AddComponent<SpriteRenderer>();
            disc.sprite = DreadwormVFXUtility.SoftDiscSprite;
            disc.sharedMaterial = glacialDiamondAdditiveMaterial != null
                ? glacialDiamondAdditiveMaterial : brightAdditiveMaterial;
            disc.color = DreadwormVFXUtility.GlacialApexDiamond;
            DreadwormVFXUtility.ConfigureRenderer(disc, 18);

            float maxSize = 1.8f + tier * 0.4f;
            float duration = 0.15f;
            float elapsed = 0f;

            while (elapsed < duration && pulseRoot != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                pulseRoot.transform.localScale = Vector3.one * Mathf.Lerp(0f, maxSize, t);
                Color c = disc.color;
                c.a = Mathf.Lerp(1f, 0f, t);
                disc.color = c;
                yield return null;
            }
            DestroyRuntimeObject(pulseRoot);
        }

        private void SpawnCrystalShardGravity(Vector3 position, int tier)
        {
            GameObject root = new GameObject("Dreadworm_CrystalShardNova");
            root.transform.SetParent(VFXContainerUtility.GetContainer(), true);
            root.transform.position = position;

            ParticleSystem ps = DreadwormVFXUtility.CreateParticleSystem(
                root, "GravityShards",
                glacialCrystalAlphaMaterial != null ? glacialCrystalAlphaMaterial : shardAlphaMaterial,
                17);
            var main = ps.main;
            main.duration = 0.12f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f + tier * 0.8f, 7.0f + tier * 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.25f + tier * 0.06f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = DreadwormVFXUtility.GlacialApexDiamond;
            main.gravityModifier = 2.8f;
            main.maxParticles = 60;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)(8 + tier * 3), (short)(14 + tier * 4))
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-25f, 25f);
            ConfigureParticleFade(ps, 0.04f, 0.55f);
            ps.Play();
            Destroy(root, 2.0f);
        }

        private void SpawnBasaltGroundBurst(Vector3 position, int count, float speed, float horizontalDirection)        {
            GameObject root = new GameObject("Dreadworm_BasaltGroundBurst");
            root.transform.SetParent(VFXContainerUtility.GetContainer(), true);
            root.transform.position = position;

            // 1. Dark Basalt Rock Chunks
            ParticleSystem shards = DreadwormVFXUtility.CreateParticleSystem(
                root,
                "BasaltRockChunks",
                basaltDebrisMaterial != null ? basaltDebrisMaterial : shardAlphaMaterial,
                11);
            var main = shards.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 2.4f;
            main.maxParticles = 80;

            var emission = shards.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)Mathf.Max(1, count - 4), (short)(count + 4))
            });

            var shape = shards.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 65f;
            shape.radius = 0.4f;
            root.transform.rotation = Quaternion.Euler(
                0f,
                0f,
                horizontalDirection >= 0f ? 30f : 150f);

            var rotation = shards.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-9f, 9f);
            ConfigureParticleFade(shards, 0.04f, 0.62f);

            // 2. Cyan Cryo Mist Floor Wave
            ParticleSystem mist = DreadwormVFXUtility.CreateParticleSystem(
                root,
                "CryoMistPuff",
                cryoMistAlphaMaterial != null ? cryoMistAlphaMaterial : dustAlphaMaterial,
                8);
            var mistMain = mist.main;
            mistMain.duration = 0.2f;
            mistMain.loop = false;
            mistMain.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.85f);
            mistMain.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.5f);
            mistMain.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.35f);
            mistMain.startColor = new Color(1f, 1f, 1f, 0.75f);
            mistMain.gravityModifier = -0.06f;

            var mistEmission = mist.emission;
            mistEmission.rateOverTime = 0f;
            mistEmission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)Mathf.Max(4, count / 2), (short)Mathf.Max(6, count))
            });
            var mistShape = mist.shape;
            mistShape.shapeType = ParticleSystemShapeType.Circle;
            mistShape.radius = 0.85f;
            mistShape.scale = new Vector3(1.2f, 0.25f, 1f);
            ConfigureParticleFade(mist, 0.02f, 0.45f);

            shards.Play();
            mist.Play();
            Destroy(root, 1.4f);
        }

        private void SpawnGatherBurst(Vector3 position, float duration, bool storm)
        {
            GameObject root = new GameObject(storm ? "Dreadworm_StormGather" : "Dreadworm_FrostGather");
            root.transform.SetParent(VFXContainerUtility.GetContainer(), true);
            root.transform.position = position;

            Material material = storm ? stormAdditiveMaterial : shardAdditiveMaterial;
            ParticleSystem particles = DreadwormVFXUtility.CreateParticleSystem(root, "InwardCrystals", material, 13);
            var main = particles.main;
            main.duration = Mathf.Max(0.2f, duration);
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(duration * 0.55f, duration * 0.92f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(-5.8f, -2.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.28f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;
            main.maxParticles = 180;

            var emission = particles.emission;
            emission.rateOverTime = Mathf.Clamp(70f / Mathf.Max(0.2f, duration), 35f, 90f);

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = storm ? 5.2f : 4.1f;
            shape.radiusThickness = 0.16f;

            var rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-5f, 5f);

            ConfigureParticleFade(particles, 0.08f, 0.48f);
            particles.Play();
            Destroy(root, duration + 1.2f);
        }

        private void SpawnGroundBurst(Vector3 position, int count, float speed, float horizontalDirection)
        {
            GameObject root = new GameObject("Dreadworm_GroundCrystalBurst");
            root.transform.SetParent(VFXContainerUtility.GetContainer(), true);
            root.transform.position = position;

            ParticleSystem shards = DreadwormVFXUtility.CreateParticleSystem(
                root,
                "CrystalAndRockShards",
                rubyShardAdditiveMaterial != null ? rubyShardAdditiveMaterial : shardAlphaMaterial,
                11);
            var main = shards.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.42f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.45f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.42f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 2.1f;
            main.maxParticles = 80;

            var emission = shards.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)Mathf.Max(1, count - 4), (short)(count + 4))
            });

            var shape = shards.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 68f;
            shape.radius = 0.35f;
            root.transform.rotation = Quaternion.Euler(
                0f,
                0f,
                horizontalDirection >= 0f ? 32f : 148f);

            var rotation = shards.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            ConfigureParticleFade(shards, 0.04f, 0.62f);

            ParticleSystem dust = DreadwormVFXUtility.CreateParticleSystem(
                root,
                "GroundDust",
                terracottaDustMaterial != null ? terracottaDustMaterial : dustAlphaMaterial,
                7);
            var dustMain = dust.main;
            dustMain.duration = 0.16f;
            dustMain.loop = false;
            dustMain.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.78f);
            dustMain.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
            dustMain.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.15f);
            dustMain.startColor = new Color(1f, 1f, 1f, 0.62f);
            dustMain.gravityModifier = -0.08f;

            var dustEmission = dust.emission;
            dustEmission.rateOverTime = 0f;
            dustEmission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)Mathf.Max(3, count / 3), (short)Mathf.Max(5, count / 2))
            });
            var dustShape = dust.shape;
            dustShape.shapeType = ParticleSystemShapeType.Circle;
            dustShape.radius = 0.7f;
            dustShape.scale = new Vector3(1f, 0.18f, 1f);
            ConfigureParticleFade(dust, 0.02f, 0.38f);

            shards.Play();
            dust.Play();
            Destroy(root, 1.35f);
        }

        private IEnumerator DelayedShockwave(Vector3 pos, Vector2 scale, float dur, Color col, float delay)
        {
            yield return new WaitForSeconds(delay);
            SpawnShockwave(pos, scale, dur, col);
        }

        private IEnumerator SpawnImpactFlash(Vector3 pos, int tier)
        {
            GameObject flashRoot = new GameObject("Dreadworm_ImpactFlash");
            flashRoot.transform.SetParent(VFXContainerUtility.GetContainer(), true);
            flashRoot.transform.position = pos;

            SpriteRenderer flash = flashRoot.AddComponent<SpriteRenderer>();
            flash.sprite = DreadwormVFXUtility.SoftDiscSprite;
            flash.sharedMaterial = glacialDiamondAdditiveMaterial != null
                ? glacialDiamondAdditiveMaterial : brightAdditiveMaterial;
            float size = 0.9f + tier * 0.35f;
            flashRoot.transform.localScale = Vector3.one * size;
            flash.color = DreadwormVFXUtility.GlacialApexDiamond;
            DreadwormVFXUtility.ConfigureRenderer(flash, 20);

            float elapsed = 0f;
            while (elapsed < 0.06f && flashRoot != null)
            {
                elapsed += Time.deltaTime;
                Color c = DreadwormVFXUtility.GlacialApexDiamond;
                c.a = Mathf.Lerp(1f, 0f, elapsed / 0.06f);
                flash.color = c;
                yield return null;
            }
            DestroyRuntimeObject(flashRoot);
        }

        private void SpawnGroundShatterChunks(Vector3 position, int tier)
        {
            GameObject root = new GameObject("Dreadworm_ShatterChunks");
            root.transform.SetParent(VFXContainerUtility.GetContainer(), true);
            root.transform.position = position;

            ParticleSystem ps = DreadwormVFXUtility.CreateParticleSystem(
                root, "IceChunks",
                glacialCrystalAlphaMaterial != null ? glacialCrystalAlphaMaterial : shardAlphaMaterial,
                13);
            var main = ps.main;
            main.duration = 0.15f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f + tier * 0.8f, 5.5f + tier * 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f + tier * 0.04f, 0.38f + tier * 0.08f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = DreadwormVFXUtility.GlacialApexDiamond;
            main.gravityModifier = 3.2f;
            main.maxParticles = 60;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)(8 + tier * 2), (short)(12 + tier * 2))
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.3f;

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-15f, 15f);
            ConfigureParticleFade(ps, 0.05f, 0.6f);
            ps.Play();
            Destroy(root, 1.5f);
        }

        private void SpawnRadialBurst(
            Vector3 position,
            int count,
            float speed,
            Color color,
            bool additive)
        {
            GameObject root = new GameObject("Dreadworm_CrystalRadialBurst");
            root.transform.SetParent(VFXContainerUtility.GetContainer(), true);
            root.transform.position = position;

            Material material = additive ? shardAdditiveMaterial : shardAlphaMaterial;
            ParticleSystem particles = DreadwormVFXUtility.CreateParticleSystem(root, "RadialShards", material, 15);
            var main = particles.main;
            main.duration = 0.18f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.72f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.55f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.34f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = color;
            main.maxParticles = 100;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)Mathf.Max(1, count - 3), (short)(count + 3))
            });

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.25f;
            shape.radiusThickness = 1f;

            var rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-10f, 10f);
            ConfigureParticleFade(particles, 0.02f, 0.52f);
            particles.Play();
            Destroy(root, 1.1f);
        }

        private void SpawnShockwave(Vector3 position, Vector2 targetScale, float duration, Color color)
        {
            GameObject root = new GameObject("Dreadworm_Shockwave");
            root.transform.SetParent(VFXContainerUtility.GetContainer(), true);
            root.transform.position = position;

            SpriteRenderer ring = root.AddComponent<SpriteRenderer>();
            ring.sprite = DreadwormVFXUtility.RingSprite;
            ring.sharedMaterial = brightAdditiveMaterial;
            ring.color = color;
            DreadwormVFXUtility.ConfigureRenderer(ring, 14);
            StartCoroutine(AnimateShockwave(root, ring, targetScale, duration, color));
        }

        private IEnumerator AnimateShockwave(
            GameObject root,
            SpriteRenderer renderer,
            Vector2 targetScale,
            float duration,
            Color color)
        {
            float elapsed = 0f;
            while (elapsed < duration && root != null)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float eased = DreadwormVFXUtility.EaseOutCubic(normalized);
                root.transform.localScale = new Vector3(
                    Mathf.Lerp(0.15f, targetScale.x, eased),
                    Mathf.Lerp(0.08f, targetScale.y, eased),
                    1f);
                root.transform.rotation = Quaternion.identity;
                renderer.color = new Color(color.r, color.g, color.b, 1f - normalized);
                yield return null;
            }

            DestroyRuntimeObject(root);
        }

        private IEnumerator FadeAfterimage(
            GameObject root,
            List<SpriteRenderer> ghosts,
            float duration)
        {
            float elapsed = 0f;
            Vector3 drift = Vector3.left * chargeDirection * 0.75f;

            while (elapsed < duration && root != null)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                root.transform.position += drift * Time.deltaTime;

                for (int i = 0; i < ghosts.Count; i++)
                {
                    SpriteRenderer ghost = ghosts[i];
                    if (ghost == null)
                        continue;
                    Color color = ghost.color;
                    color.a = Mathf.Lerp(0.34f, 0f, normalized);
                    ghost.color = color;
                }

                yield return null;
            }

            DestroyRuntimeObject(root);
        }

        private IEnumerator FadeSprite(
            GameObject root,
            SpriteRenderer sprite,
            float duration,
            float stretchMultiplier)
        {
            float elapsed = 0f;
            Vector3 initialScale = root.transform.localScale;
            Color initialColor = sprite.color;

            while (elapsed < duration && root != null)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                root.transform.localScale = new Vector3(
                    Mathf.Lerp(initialScale.x, initialScale.x * stretchMultiplier, normalized),
                    initialScale.y,
                    1f);
                sprite.color = new Color(
                    initialColor.r,
                    initialColor.g,
                    initialColor.b,
                    Mathf.Lerp(initialColor.a, 0f, normalized));
                yield return null;
            }

            DestroyRuntimeObject(root);
        }

        private IEnumerator HitFlashRoutine()
        {
            float duration = 0.14f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                hitFlash = normalized < 0.22f
                    ? normalized / 0.22f
                    : 1f - (normalized - 0.22f) / 0.78f;
                yield return null;
            }

            hitFlash = 0f;
            hitFlashCoroutine = null;
        }

        private Vector3 GetChargeHeadPosition()
        {
            if (dreadworm == null || dreadworm.Collider2D == null)
                return transform.position;

            Bounds bounds = dreadworm.Collider2D.bounds;
            return new Vector3(
                bounds.center.x + chargeDirection * bounds.extents.x * 0.88f,
                bounds.center.y + bounds.extents.y * 0.08f,
                transform.position.z);
        }

        private Vector3 GetSlamGroundPosition()
        {
            Vector3 center = dreadworm.CenterPosition + Vector3.right * slamDirection * 1.5f;
            return GetGroundAlignedPosition(center);
        }

        private Vector3 GetGroundAlignedPosition(Vector3 position)
        {
            if (dreadworm != null && dreadworm.Collider2D != null)
                position.y = dreadworm.Collider2D.bounds.min.y + 0.06f;
            return position;
        }

        private static void ConfigureParticleFade(
            ParticleSystem particleSystem,
            float fadeInTime,
            float fadeOutStart)
        {
            var colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, Mathf.Clamp01(fadeInTime)),
                    new GradientAlphaKey(1f, Mathf.Clamp01(fadeOutStart)),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.18f, 1f),
                new Keyframe(1f, 0.08f));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
        }

        private static LineRenderer CreateLine(
            GameObject parent,
            string objectName,
            Material material,
            int orderOffset,
            float startWidth,
            float endWidth)
        {
            GameObject lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(parent.transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.sharedMaterial = material;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 3;
            line.numCornerVertices = 2;
            line.startWidth = startWidth;
            line.endWidth = endWidth;
            line.startColor = Color.white;
            line.endColor = new Color(1f, 1f, 1f, 0.1f);
            DreadwormVFXUtility.ConfigureRenderer(line, orderOffset);
            return line;
        }

        private static SpriteRenderer CreateSpriteLayer(
            GameObject parent,
            string objectName,
            Sprite sprite,
            Material material,
            int orderOffset)
        {
            GameObject layerObject = new GameObject(objectName);
            layerObject.transform.SetParent(parent.transform, false);
            SpriteRenderer renderer = layerObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.color = Color.white;
            DreadwormVFXUtility.ConfigureRenderer(renderer, orderOffset);
            return renderer;
        }

        private static void SetLine(LineRenderer line, Vector3 start, Vector3 end)
        {
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }

        private static void SetFissureLine(
            LineRenderer line,
            Vector3 start,
            Vector3 end,
            float irregularity,
            float seed)
        {
            const int pointCount = 7;
            line.positionCount = pointCount;

            for (int i = 0; i < pointCount; i++)
            {
                float normalized = i / (pointCount - 1f);
                float envelope = Mathf.Sin(normalized * Mathf.PI);
                float offset =
                    (Mathf.Sin(normalized * 13.7f + seed) + Mathf.Sin(normalized * 27.1f + seed * 1.9f) * 0.45f) *
                    irregularity * envelope;
                line.SetPosition(i, Vector3.Lerp(start, end, normalized) + Vector3.up * offset);
            }
        }

        private static void SetLineAlpha(LineRenderer line, float alpha)
        {
            Color start = line.startColor;
            Color end = line.endColor;
            start.a = Mathf.Clamp01(alpha);
            end.a = Mathf.Clamp01(alpha * 0.55f);
            line.startColor = start;
            line.endColor = end;
        }

        private Material Track(Material material)
        {
            if (material != null)
                runtimeMaterials.Add(material);
            return material;
        }

        public static void ShakeCamera(float duration, float magnitude)
        {
            if (duration > 0f && magnitude > 0f && CameraController.Instance != null)
                CameraController.Instance.ShakeCamera(duration, magnitude);
        }

        private static void DestroyRuntimeObject(Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}
