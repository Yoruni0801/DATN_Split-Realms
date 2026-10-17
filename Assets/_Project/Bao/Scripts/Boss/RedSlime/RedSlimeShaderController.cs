using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Bao.Bosses
{
    /// <summary>
    /// Drives visual effects & shader parameters for the Red Slime Boss:
    /// - Hit flash (white/red burst on damage)
    /// - Pulse speed control for charging attacks (heartbeat / neon pulsation)
    /// - Windup gathering particles (energy converging on the boss)
    /// - Dive ghost trail (red glowing afterimages during dive slam)
    /// - Ground impact shockwaves & rock/slime debris
    /// - Melee impact particles
    /// - Screen shake simulation
    /// - Tentacle reveal & warning circles
    /// - Death dissolve
    /// 
    /// SETUP: Attach to the same GameObject as the RedSlimeBoss or child sprite root.
    /// </summary>
    public class RedSlimeShaderController : MonoBehaviour
    {
        [Header("Hit Flash")]
        [SerializeField] private float hitFlashDuration = 0.1f;
        [SerializeField] private Color hitFlashColor = Color.white;

        [Header("Phase Colors")]
        [ColorUsage(true, true)]
        [SerializeField] private Color phase1GlowColor = new Color(2f, 0.3f, 0.3f, 1f);
        [ColorUsage(true, true)]
        [SerializeField] private Color phase2GlowColor = new Color(3f, 0.1f, 0.5f, 1f);

        [Header("Impact VFX Settings")]
        [SerializeField] private Color impactColor = new Color(1.5f, 0.2f, 0.1f, 1f);
        [SerializeField] private int impactParticleCount = 12;

        [Header("Ground Impact Settings")]
        [SerializeField] private Color groundImpactColor = new Color(2.5f, 0.5f, 0.1f, 1f);
        [SerializeField] private int groundImpactParticleCount = 20;
        [SerializeField] private float groundImpactShockwaveSize = 3f;

        [Header("Ghost Trail & Energy Gathering Settings")]
        [ColorUsage(true, true)]
        [SerializeField] private Color trailColor = new Color(2.5f, 0.2f, 0.4f, 0.7f);
        [SerializeField] private float ghostLifetime = 0.35f;
        [SerializeField] private int windupParticleCount = 16;

        // Internal References & State
        private SpriteRenderer[] renderers;
        private RedSlimeBoss boss;
        private Material[] matInstances;
        private Coroutine hitFlashCoroutine;
        private Coroutine shakeCoroutine;
        private Coroutine windupCoroutine;
        private Camera mainCam;

        private static readonly int ID_HitFlash = Shader.PropertyToID("_HitFlash");
        private static readonly int ID_PulseSpeedMultiplier = Shader.PropertyToID("_PulseSpeedMultiplier");
        private static readonly int ID_BoilingRed = Shader.PropertyToID("_BoilingRed");
        private static readonly int ID_Color = Shader.PropertyToID("_Color");
        private static readonly int ID_CombatStress = Shader.PropertyToID("_CombatStress");
        private static readonly int ID_ChargeSuction = Shader.PropertyToID("_ChargeSuction");
        private static readonly int ID_WarningOverdrive = Shader.PropertyToID("_WarningOverdrive");

        private void Awake()
        {
            boss = GetComponentInParent<RedSlimeBoss>();
            if (boss == null) boss = GetComponent<RedSlimeBoss>();

            // Không được mượn Boss đầu tiên trong scene: mỗi clone sau khi tách phải
            // luôn sở hữu shader/VFX controller độc lập của chính nó.
            if (boss == null)
            {
                Debug.LogWarning("[RedSlimeShaderController] Không tìm thấy RedSlimeBoss parent; VFX sẽ không được gắn với một Slime khác.", this);
            }

            RefreshMaterials();
            mainCam = Camera.main;
        }

        /// <summary>Re-cache and instantiate materials for dynamic batching/color control.</summary>
        public void RefreshMaterials()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers != null && renderers.Length > 0)
            {
                matInstances = new Material[renderers.Length];
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null && renderers[i].sharedMaterial != null)
                    {
                        matInstances[i] = new Material(renderers[i].sharedMaterial);
                        renderers[i].material = matInstances[i];
                    }
                }
            }
        }

        private void Update()
        {
            if (boss == null) return;

            float healthRatio = (float)boss.CurrentHealth / Mathf.Max(boss.MaxHealth, 1);
            float pulse = (Mathf.Sin(Time.time * 3f) * 0.5f + 0.5f) * (1f - healthRatio);
            Color targetGlow = boss.CurrentPhase == BossPhase.Phase1 ? phase1GlowColor : phase2GlowColor;

            if (matInstances != null)
            {
                float stress = 1f - healthRatio;
                foreach (var mat in matInstances)
                {
                    if (mat != null && mat.HasProperty(ID_CombatStress))
                    {
                        mat.SetFloat(ID_CombatStress, stress);
                    }
                }
            }

            if (boss.CurrentPhase == BossPhase.Phase2 && renderers != null)
            {
                float tintIntensity = pulse * 0.15f;
                foreach (var r in renderers)
                {
                    if (r != null && r.material != null && r.material.HasProperty(ID_Color))
                    {
                        Color baseColor = Color.Lerp(Color.white, targetGlow, tintIntensity);
                        baseColor.a = r.color.a;
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════
        //  PUBLIC VFX & SHADER METHODS
        // ══════════════════════════════════════════════════

        /// <summary>Trigger a white hit flash on all renderers / shader materials.</summary>
        public void TriggerHitFlash()
        {
            if (hitFlashCoroutine != null) StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
        }

        /// <summary>Set charging pulse speed multiplier in shader for anticipation.</summary>
        public void SetChargingPulseSpeed(bool isCharging)
        {
            float speedMult = isCharging ? 3.0f : 1.0f;
            float boilAmount = isCharging ? 0.6f : 0.0f;

            if (matInstances != null)
            {
                foreach (var mat in matInstances)
                {
                    if (mat == null) continue;
                    if (mat.HasProperty(ID_PulseSpeedMultiplier))
                        mat.SetFloat(ID_PulseSpeedMultiplier, speedMult);
                    if (mat.HasProperty(ID_BoilingRed))
                        mat.SetFloat(ID_BoilingRed, boilAmount);
                }
            }
        }

        /// <summary>Spawn particles converging toward the boss center during jump windup.</summary>
        public void TriggerWindupGathering(Vector3 centerPos, float duration)
        {
            if (windupCoroutine != null) StopCoroutine(windupCoroutine);
            windupCoroutine = StartCoroutine(WindupGatheringRoutine(centerPos, duration));
        }

        /// <summary>Spawn ghost trail afterimage during dive slam motion.</summary>
        public void SpawnDiveGhostTrail(Vector3 position, Vector3 scale, float direction)
        {
            StartCoroutine(GhostTrailRoutine(position, scale, direction));
        }

        /// <summary>Spawn a burst of particles at melee impact point.</summary>
        public void SpawnImpactVFX(Vector2 position, float direction)
        {
            StartCoroutine(ImpactVFXRoutine(position, direction));
        }

        /// <summary>Spawn ground impact shockwave and debris when landing a slam.</summary>
        public void SpawnGroundImpactVFX(Vector3 position)
        {
            StartCoroutine(GroundImpactVFXRoutine(position));
        }

        /// <summary>Trigger screen shake effect.</summary>
        public void TriggerScreenShake(float duration, float intensity)
        {
            if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
            shakeCoroutine = StartCoroutine(ScreenShakeRoutine(duration, intensity));
        }

        /// <summary>Spawn a red warning circle at position for attacks.</summary>
        public void SpawnWarningCircle(Vector3 position, float duration, float radius)
        {
            StartCoroutine(WarningCircleRoutine(position, duration, radius));
        }

        /// <summary>Spawn a burst VFX when tentacles reveal during phase transitions.</summary>
        public void SpawnTentacleRevealVFX(Vector3 position)
        {
            StartCoroutine(TentacleRevealRoutine(position));
        }

        /// <summary>Set charge suction vortex intensity on core heart shader.</summary>
        public void SetCoreChargeSuction(float suction)
        {
            if (matInstances != null)
            {
                foreach (var mat in matInstances)
                {
                    if (mat != null && mat.HasProperty(ID_ChargeSuction))
                        mat.SetFloat(ID_ChargeSuction, suction);
                }
            }
        }

        /// <summary>Set warning overdrive alarm on core heart shader.</summary>
        public void SetCoreWarningOverdrive(float overdrive)
        {
            if (matInstances != null)
            {
                foreach (var mat in matInstances)
                {
                    if (mat != null && mat.HasProperty(ID_WarningOverdrive))
                        mat.SetFloat(ID_WarningOverdrive, overdrive);
                }
            }
        }

        /// <summary>Set combat stress overdrive on core heart shader.</summary>
        public void SetCombatStress(float stress)
        {
            if (matInstances != null)
            {
                foreach (var mat in matInstances)
                {
                    if (mat != null && mat.HasProperty(ID_CombatStress))
                        mat.SetFloat(ID_CombatStress, stress);
                }
            }
        }

        private Coroutine squashCoroutine;

        /// <summary>Procedural squash and stretch impulse for jelly elasticity.</summary>
        public void TriggerSquashAndStretch(float duration, float intensity)
        {
            if (squashCoroutine != null)
            {
                StopCoroutine(squashCoroutine);
            }
            squashCoroutine = StartCoroutine(SquashAndStretchRoutine(duration, intensity));
        }

        private IEnumerator SquashAndStretchRoutine(float duration, float intensity)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float wave = Mathf.Sin(t * Mathf.PI);
                float squashX = 1f + wave * intensity;
                float squashY = 1f - wave * intensity * 0.7f;

                Vector3 targetScale = (boss != null) ? boss.GetTargetLocalScale() : transform.localScale;
                transform.localScale = new Vector3(targetScale.x * squashX, targetScale.y * squashY, targetScale.z);
                yield return null;
            }

            if (boss != null)
            {
                transform.localScale = boss.GetTargetLocalScale();
            }
            squashCoroutine = null;
        }

        /// <summary>Spawn split burst VFX customized for the current boss variant transition.</summary>
        public void SpawnSplitVFX(SlimeForm prevForm, SlimeForm nextForm, Vector3 position)
        {
            Color burstColor = prevForm == SlimeForm.SlimeA ? new Color(2.5f, 0.3f, 0.4f, 1f) :
                               prevForm == SlimeForm.SlimeB ? new Color(2.5f, 1.6f, 0.2f, 1f) :
                               new Color(3.2f, 2.2f, 0.2f, 1f); // Radiant Solar Amber-Gold
            StartCoroutine(GenericBurstRoutine(position, burstColor, 28, 0.55f, 2.0f));
        }

        /// <summary>Spawn merge suction VFX customized for the current boss variant transition.</summary>
        public void SpawnMergeVFX(SlimeForm prevForm, SlimeForm mergedForm, Vector3 position)
        {
            Color mergeColor = mergedForm == SlimeForm.SlimeA ? new Color(2.5f, 0.3f, 0.4f, 1f) :
                               mergedForm == SlimeForm.SlimeB ? new Color(2.5f, 1.6f, 0.2f, 1f) :
                               new Color(3.5f, 2.5f, 0.3f, 1f); // Blinding Golden Core Merge
            StartCoroutine(GenericBurstRoutine(position, mergeColor, 36, 0.65f, 2.5f));
        }

        /// <summary>Slime B: Charge gathering motes for lightning dash.</summary>
        public void TriggerSlimeBChargeGathering(Vector3 centerPos, float duration)
        {
            StartCoroutine(GenericGatheringRoutine(centerPos, duration, new Color(2.5f, 1.8f, 0.3f, 1f), 18));
        }

        /// <summary>Slime B: Dash afterimage trail.</summary>
        public void SpawnSlimeBDashTrail(Vector3 position, Vector3 scale, float direction)
        {
            StartCoroutine(GenericTrailRoutine(position, scale, direction, new Color(2.5f, 1.6f, 0.2f, 0.75f), 0.25f));
        }

        /// <summary>Slime B: High-speed dash impact sparks and slice.</summary>
        public void SpawnSlimeBDashImpactVFX(Vector2 position, float direction)
        {
            StartCoroutine(GenericImpactRoutine(position, direction, new Color(2.5f, 1.8f, 0.3f, 1f), 16));
        }

        /// <summary>
        /// Slime B Dual Cross-Dash: AAA X-Cross targeting laser telegraph with accelerating pulse and lock-on reticle.
        /// </summary>
        public void SpawnSlimeBDualLaserCrossTelegraph(Vector3 startPoint, Vector3 targetPoint, float duration)
        {
            StartCoroutine(SlimeBDualLaserTelegraphRoutine(startPoint, targetPoint, duration));
        }

        /// <summary>
        /// Slime B Dual Cross-Dash: High-frequency supersonic radiant amber afterimage ghost stream.
        /// </summary>
        public void SpawnSlimeBDualDashGhostStream(Vector3 position, Vector3 scale, float direction)
        {
            StartCoroutine(SlimeBDualGhostStreamRoutine(position, scale, direction));
        }

        /// <summary>
        /// Slime B Dual Cross-Dash: AAA Guillotine Cross-Clash explosion with X-Slash flash, expanding shockwave ring and 20 amber shards.
        /// </summary>
        public void SpawnSlimeBCrossClashImpact(Vector3 centerPosition)
        {
            StartCoroutine(SlimeBCrossClashImpactRoutine(centerPosition));
        }

        /// <summary>Slime C: Multi-phase Arcane Implosion & Golden Spark Gathering.</summary>
        public void TriggerSlimeCChargeGathering(Vector3 centerPos, float duration)
        {
            StartCoroutine(SlimeCImplosionGatheringRoutine(centerPos, duration));
        }

        /// <summary>Slime C: Multi-layer solar ghost trail with radiant amber corona.</summary>
        public void SpawnSlimeCPinballTrail(Vector3 position, Vector3 scale, float direction)
        {
            StartCoroutine(GenericTrailRoutine(position, scale, direction, new Color(3.0f, 2.0f, 0.2f, 0.85f), 0.22f));
        }

        /// <summary>Slime C: Popcorn Shard Blast, expanding shockwave ring & isometric debris.</summary>
        public void SpawnSlimeCBounceImpact(Vector3 position)
        {
            StartCoroutine(SlimeCPopcornImpactRoutine(position));
        }

        /// <summary>Slime C: AAA Multi-Layer Prismatic Solar Laser Matrix connecting two Slime C units.</summary>
        public void SpawnSlimeCLaserBeam(Transform t1, Transform t2, float duration)
        {
            StartCoroutine(SlimeCLaserBeamRoutine(t1, t2, duration));
        }

        private IEnumerator SlimeCImplosionGatheringRoutine(Vector3 centerPos, float duration)
        {
            GameObject root = new GameObject("SlimeC_GatheringVFX");
            root.transform.position = centerPos;

            int count = 24;
            List<(Transform tr, Vector3 offset, SpriteRenderer sr, float rotSpeed)> list = new List<(Transform, Vector3, SpriteRenderer, float)>();

            for (int i = 0; i < count; i++)
            {
                GameObject p = new GameObject($"GatherMote_{i}");
                p.transform.SetParent(root.transform);
                float angle = (360f / count) * i + Random.Range(-12f, 12f);
                float dist = Random.Range(2.2f, 4.0f);
                Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * dist, Mathf.Sin(angle * Mathf.Deg2Rad) * dist, 0f);
                p.transform.position = centerPos + offset;

                SpriteRenderer sr = p.AddComponent<SpriteRenderer>();
                sr.sprite = CreateDiamondSprite();
                // Golden Amber, Molten Honey & Radiant Core Sparkles
                sr.color = (i % 3 == 0) ? new Color(4.0f, 3.2f, 0.6f, 1f) :
                           (i % 3 == 1) ? new Color(3.2f, 1.8f, 0.2f, 1f) :
                                          new Color(2.8f, 1.2f, 0.1f, 1f);
                sr.sortingOrder = 100;
                p.transform.localScale = Vector3.one * Random.Range(0.12f, 0.28f);

                list.Add((p.transform, offset, sr, Random.Range(180f, 540f) * (Random.value > 0.5f ? 1f : -1f)));
            }

            float elapsed = 0f;
            while (elapsed < duration && root != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = Mathf.Pow(t, 2.5f); // High-speed acceleration vortex

                for (int i = 0; i < list.Count; i++)
                {
                    var (tr, offset, sr, rotSpeed) = list[i];
                    if (tr != null)
                    {
                        // Spiral suction vortex motion
                        float spiralAngle = (1f - t) * 120f;
                        Quaternion rot = Quaternion.Euler(0f, 0f, spiralAngle);
                        Vector3 currentOffset = rot * Vector3.Lerp(offset, Vector3.zero, ease);
                        tr.position = centerPos + currentOffset;
                        tr.Rotate(0f, 0f, rotSpeed * Time.deltaTime);

                        if (sr != null)
                        {
                            Color c = sr.color;
                            c.a = Mathf.Lerp(0.3f, 1.0f, t);
                            sr.color = c;
                        }
                    }
                }
                yield return null;
            }

            if (root != null) Destroy(root);
        }

        private IEnumerator SlimeCPopcornImpactRoutine(Vector3 position)
        {
            GameObject root = new GameObject("SlimeC_PopcornImpactVFX");
            root.transform.position = position;

            // 1. Shockwave Ripple Ring
            GameObject ringObj = new GameObject("PopcornShockwave");
            ringObj.transform.SetParent(root.transform);
            ringObj.transform.position = position;
            SpriteRenderer ringSr = ringObj.AddComponent<SpriteRenderer>();
            ringSr.sprite = CreateCircleSprite();
            ringSr.color = new Color(3.5f, 2.5f, 0.3f, 0.9f);
            ringSr.sortingOrder = 95;

            // 2. High Velocity Popcorn Gemstone Shards
            int shardCount = 18;
            List<(Transform tr, Vector2 vel, SpriteRenderer sr, float rotSpeed)> shards = new List<(Transform, Vector2, SpriteRenderer, float)>();

            for (int i = 0; i < shardCount; i++)
            {
                GameObject shard = new GameObject($"Shard_{i}");
                shard.transform.SetParent(root.transform);
                shard.transform.position = position;

                SpriteRenderer sr = shard.AddComponent<SpriteRenderer>();
                sr.sprite = CreateDiamondSprite();
                sr.color = (i % 2 == 0) ? new Color(3.8f, 2.6f, 0.3f, 1f) : new Color(3.0f, 1.6f, 0.15f, 1f);
                sr.sortingOrder = 101;
                shard.transform.localScale = Vector3.one * Random.Range(0.15f, 0.35f);

                float angle = Random.Range(0f, 360f);
                float speed = Random.Range(4.5f, 12.0f);
                Vector2 vel = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * speed;
                float rotSpeed = Random.Range(-400f, 400f);

                shards.Add((shard.transform, vel, sr, rotSpeed));
            }

            float elapsed = 0f;
            float duration = 0.45f;

            while (elapsed < duration && root != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Animate Shockwave Ring
                if (ringSr != null)
                {
                    float ringScale = Mathf.Lerp(0.2f, 2.6f, Mathf.Sqrt(t));
                    ringObj.transform.localScale = new Vector3(ringScale, ringScale * 0.55f, 1f);
                    Color rc = ringSr.color;
                    rc.a = Mathf.Lerp(0.9f, 0f, t);
                    ringSr.color = rc;
                }

                // Animate Shards with Gravity & Spin
                for (int i = 0; i < shards.Count; i++)
                {
                    var (tr, vel, sr, rotSpeed) = shards[i];
                    if (tr != null)
                    {
                        tr.position += (Vector3)(vel * Time.deltaTime);
                        tr.Rotate(0f, 0f, rotSpeed * Time.deltaTime);

                        if (sr != null)
                        {
                            Color sc = sr.color;
                            sc.a = Mathf.Clamp01(1f - t * 1.3f);
                            sr.color = sc;
                        }
                    }
                }

                yield return null;
            }

            if (root != null) Destroy(root);
        }

        private IEnumerator SlimeCLaserBeamRoutine(Transform t1, Transform t2, float duration)
        {
            GameObject laserRoot = new GameObject("SlimeC_LaserMatrixRoot");

            // --- BEAM LAYER 1: OUTER PLASMA SHEATH (Rực rỡ vàng hổ phách tỏa quang) ---
            GameObject outerSheathObj = new GameObject("Laser_OuterSheath");
            outerSheathObj.transform.SetParent(laserRoot.transform);
            LineRenderer outerLr = outerSheathObj.AddComponent<LineRenderer>();
            outerLr.positionCount = 2;
            outerLr.startWidth = 0.38f;
            outerLr.endWidth = 0.38f;
            outerLr.material = new Material(Shader.Find("Sprites/Default"));
            outerLr.startColor = new Color(3.5f, 2.2f, 0.2f, 0.85f);
            outerLr.endColor = new Color(3.5f, 2.2f, 0.2f, 0.85f);
            outerLr.sortingOrder = 94;

            // --- BEAM LAYER 2: INTENSE INNER WHITE-GOLD CORE (Tâm chùm năng lượng laser sáng lóa) ---
            GameObject innerCoreObj = new GameObject("Laser_InnerCore");
            innerCoreObj.transform.SetParent(laserRoot.transform);
            LineRenderer innerLr = innerCoreObj.AddComponent<LineRenderer>();
            innerLr.positionCount = 2;
            innerLr.startWidth = 0.14f;
            innerLr.endWidth = 0.14f;
            innerLr.material = new Material(Shader.Find("Sprites/Default"));
            innerLr.startColor = new Color(4.5f, 4.2f, 2.8f, 1f);
            innerLr.endColor = new Color(4.5f, 4.2f, 2.8f, 1f);
            innerLr.sortingOrder = 96;

            // --- BEAM LAYER 3: DOUBLE-HELIX ORBITING PHOTON SPARKLES (Các hạt photon xoắn kép dọc thân laser) ---
            int helixCount = 16;
            List<(Transform tr, SpriteRenderer sr, float segmentProgress, float helixPhase)> helixParticles = new List<(Transform, SpriteRenderer, float, float)>();

            for (int i = 0; i < helixCount; i++)
            {
                GameObject hp = new GameObject($"HelixPhoton_{i}");
                hp.transform.SetParent(laserRoot.transform);
                SpriteRenderer sr = hp.AddComponent<SpriteRenderer>();
                sr.sprite = CreateDiamondSprite();
                sr.color = (i % 2 == 0) ? new Color(4.0f, 3.5f, 1.0f, 1f) : new Color(3.2f, 1.8f, 0.2f, 1f);
                sr.sortingOrder = 97;
                hp.transform.localScale = Vector3.one * Random.Range(0.14f, 0.22f);

                float prog = (float)i / helixCount;
                float phase = (i % 2 == 0) ? 0f : Mathf.PI;
                helixParticles.Add((hp.transform, sr, prog, phase));
            }

            // --- BEAM LAYER 4: ANCHOR PRISMATIC FOCUS RINGS (Vòng tụ năng lượng ở 2 đầu Slime) ---
            GameObject ring1 = new GameObject("AnchorRing1");
            ring1.transform.SetParent(laserRoot.transform);
            SpriteRenderer srRing1 = ring1.AddComponent<SpriteRenderer>();
            srRing1.sprite = CreateDiamondSprite();
            srRing1.color = new Color(4.0f, 3.0f, 0.5f, 0.95f);
            srRing1.sortingOrder = 98;
            ring1.transform.localScale = Vector3.one * 0.45f;

            GameObject ring2 = new GameObject("AnchorRing2");
            ring2.transform.SetParent(laserRoot.transform);
            SpriteRenderer srRing2 = ring2.AddComponent<SpriteRenderer>();
            srRing2.sprite = CreateDiamondSprite();
            srRing2.color = new Color(4.0f, 3.0f, 0.5f, 0.95f);
            srRing2.sortingOrder = 98;
            ring2.transform.localScale = Vector3.one * 0.45f;

            float elapsed = 0f;
            while (elapsed < duration && t1 != null && t2 != null)
            {
                elapsed += Time.deltaTime;
                float pulse = 1f + Mathf.Sin(elapsed * 28f) * 0.18f;

                Vector3 p1 = t1.position + Vector3.up * 0.25f;
                Vector3 p2 = t2.position + Vector3.up * 0.25f;
                Vector3 beamVec = p2 - p1;
                Vector3 beamNormal = new Vector3(-beamVec.y, beamVec.x, 0f).normalized;

                // Update Outer Sheath Line & Pulse Width
                outerLr.SetPosition(0, p1);
                outerLr.SetPosition(1, p2);
                outerLr.startWidth = 0.38f * pulse;
                outerLr.endWidth = 0.38f * pulse;

                // Update Inner Core Line
                innerLr.SetPosition(0, p1);
                innerLr.SetPosition(1, p2);
                innerLr.startWidth = 0.14f * pulse;
                innerLr.endWidth = 0.14f * pulse;

                // Animate Orbiting Double Helix Photons
                for (int i = 0; i < helixParticles.Count; i++)
                {
                    var (tr, sr, prog, phase) = helixParticles[i];
                    if (tr != null)
                    {
                        float currentProg = (prog + elapsed * 1.6f) % 1.0f;
                        Vector3 basePos = Vector3.Lerp(p1, p2, currentProg);
                        float sineOffset = Mathf.Sin(elapsed * 14f + currentProg * Mathf.PI * 4f + phase) * 0.32f;
                        tr.position = basePos + beamNormal * sineOffset;
                        tr.Rotate(0f, 0f, 360f * Time.deltaTime);
                    }
                }

                // Animate Gyrospin Anchor Rings
                if (ring1 != null)
                {
                    ring1.transform.position = p1;
                    ring1.transform.Rotate(0f, 0f, 240f * Time.deltaTime);
                    ring1.transform.localScale = Vector3.one * (0.45f * pulse);
                }
                if (ring2 != null)
                {
                    ring2.transform.position = p2;
                    ring2.transform.Rotate(0f, 0f, -240f * Time.deltaTime);
                    ring2.transform.localScale = Vector3.one * (0.45f * pulse);
                }

                yield return null;
            }

            Destroy(laserRoot);
        }

        // ══════════════════════════════════════════════════════════════════════════════════
        //  SLIME B: DUAL PINCER CROSS-DASH (GỌNG KÌM SẤM SÉT SONG SÁT) AAA VFX SUITE
        // ══════════════════════════════════════════════════════════════════════════════════

        private IEnumerator SlimeBDualLaserTelegraphRoutine(Vector3 startPoint, Vector3 targetPoint, float duration)
        {
            GameObject telegraphRoot = new GameObject("SlimeB_DualLaserTelegraph");

            // 1. Tia Laser Outer Glow Sheath (Vàng Hổ Phách Rực Rỡ)
            GameObject outerSheathObj = new GameObject("DualLaser_OuterSheath");
            outerSheathObj.transform.SetParent(telegraphRoot.transform);
            LineRenderer outerLr = outerSheathObj.AddComponent<LineRenderer>();
            outerLr.positionCount = 2;
            outerLr.startWidth = 0.28f;
            outerLr.endWidth = 0.12f;
            outerLr.material = new Material(Shader.Find("Sprites/Default"));
            outerLr.startColor = new Color(3.5f, 2.2f, 0.2f, 0.75f);
            outerLr.endColor = new Color(4.0f, 3.2f, 0.6f, 0.95f);
            outerLr.sortingOrder = 93;

            // 2. Tia Laser Inner Core (Lõi trắng vàng hội tụ năng lượng)
            GameObject innerCoreObj = new GameObject("DualLaser_InnerCore");
            innerCoreObj.transform.SetParent(telegraphRoot.transform);
            LineRenderer innerLr = innerCoreObj.AddComponent<LineRenderer>();
            innerLr.positionCount = 2;
            innerLr.startWidth = 0.08f;
            innerLr.endWidth = 0.04f;
            innerLr.material = new Material(Shader.Find("Sprites/Default"));
            innerLr.startColor = new Color(4.5f, 4.0f, 2.5f, 1f);
            innerLr.endColor = new Color(4.5f, 4.2f, 3.0f, 1f);
            innerLr.sortingOrder = 95;

            // 3. Tâm ngắm giao cắt (Target Lock Reticle) tại điểm cắt
            GameObject reticleObj = new GameObject("DualLaser_Reticle");
            reticleObj.transform.SetParent(telegraphRoot.transform);
            reticleObj.transform.position = targetPoint;
            SpriteRenderer reticleSr = reticleObj.AddComponent<SpriteRenderer>();
            reticleSr.sprite = CreateDiamondSprite();
            reticleSr.color = new Color(4.2f, 3.5f, 1.0f, 1.0f);
            reticleSr.sortingOrder = 97;
            reticleObj.transform.localScale = Vector3.one * 0.45f;

            // 4. Mote Particles chạy dọc theo tia laser hướng về điểm giao cắt
            int moteCount = 8;
            List<(Transform tr, SpriteRenderer sr, float prog)> motes = new List<(Transform, SpriteRenderer, float)>();
            for (int i = 0; i < moteCount; i++)
            {
                GameObject mote = new GameObject($"TelegraphMote_{i}");
                mote.transform.SetParent(telegraphRoot.transform);
                SpriteRenderer sr = mote.AddComponent<SpriteRenderer>();
                sr.sprite = CreateCircleSprite();
                sr.color = new Color(4.0f, 3.0f, 0.6f, 0.9f);
                sr.sortingOrder = 96;
                mote.transform.localScale = Vector3.one * Random.Range(0.12f, 0.22f);
                motes.Add((mote.transform, sr, (float)i / moteCount));
            }

            float elapsed = 0f;
            while (elapsed < duration && telegraphRoot != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Tần số nhấp nháy tăng tốc cảnh báo nguy hiểm sắp lao đến (Accelerating Strobe Pulse)
                float pulseFreq = Mathf.Lerp(8f, 28f, t * t);
                float strobe = 0.65f + 0.35f * Mathf.Sin(elapsed * pulseFreq);
                float currentWidthMult = Mathf.Lerp(1.0f, 1.6f, t) * strobe;

                // Update Outer Sheath
                outerLr.SetPosition(0, startPoint);
                outerLr.SetPosition(1, targetPoint);
                outerLr.startWidth = 0.26f * currentWidthMult;
                outerLr.endWidth = 0.12f * currentWidthMult;

                // Update Inner Core
                innerLr.SetPosition(0, startPoint);
                innerLr.SetPosition(1, targetPoint);
                innerLr.startWidth = 0.08f * currentWidthMult;
                innerLr.endWidth = 0.04f * currentWidthMult;

                // Animate Reticle Spin & Scale Pulse
                if (reticleObj != null)
                {
                    reticleObj.transform.Rotate(0f, 0f, (180f + t * 540f) * Time.deltaTime);
                    float reticleScale = Mathf.Lerp(0.55f, 0.25f, t) + 0.12f * Mathf.Sin(elapsed * pulseFreq);
                    reticleObj.transform.localScale = Vector3.one * reticleScale;
                }

                // Animate Motes streaming towards target
                for (int i = 0; i < motes.Count; i++)
                {
                    var (moteTr, moteSr, prog) = motes[i];
                    if (moteTr != null)
                    {
                        float currentProg = (prog + elapsed * (1.8f + t * 2.5f)) % 1.0f;
                        moteTr.position = Vector3.Lerp(startPoint, targetPoint, currentProg);
                        if (moteSr != null)
                        {
                            Color c = moteSr.color;
                            c.a = Mathf.Sin(currentProg * Mathf.PI) * strobe;
                            moteSr.color = c;
                        }
                    }
                }

                yield return null;
            }

            if (telegraphRoot != null) Destroy(telegraphRoot);
        }

        private IEnumerator SlimeBDualGhostStreamRoutine(Vector3 position, Vector3 scale, float direction)
        {
            GameObject ghostObj = new GameObject("SlimeB_DualGhostStream");
            ghostObj.transform.position = position;
            ghostObj.transform.localScale = scale;

            SpriteRenderer mainSr = GetComponentInChildren<SpriteRenderer>();
            SpriteRenderer ghostSr = ghostObj.AddComponent<SpriteRenderer>();

            if (mainSr != null && mainSr.sprite != null)
            {
                ghostSr.sprite = mainSr.sprite;
                ghostSr.flipX = mainSr.flipX;
            }
            else
            {
                ghostSr.sprite = CreateDiamondSprite();
            }

            ghostSr.sortingOrder = 88;

            Shader glowShader = Shader.Find("Custom/2D/RedSlimeGlow");
            if (glowShader == null) glowShader = Shader.Find("Sprites/Default");

            Material ghostMat = new Material(glowShader);
            ghostMat.SetColor("_Color", new Color(3.8f, 2.4f, 0.3f, 0.85f)); // Radiant Amber Ghost HDR
            ghostMat.SetFloat("_Intensity", 2.4f);
            ghostSr.material = ghostMat;

            // Spawn Spark Trail Particle tại vị trí dư ảnh
            GameObject spark = new GameObject("GhostSpark");
            spark.transform.SetParent(ghostObj.transform);
            spark.transform.localPosition = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f), 0f);
            SpriteRenderer sparkSr = spark.AddComponent<SpriteRenderer>();
            sparkSr.sprite = CreateDiamondSprite();
            sparkSr.color = new Color(4.2f, 3.6f, 1.2f, 1f);
            sparkSr.sortingOrder = 92;
            spark.transform.localScale = Vector3.one * Random.Range(0.15f, 0.3f);

            float elapsed = 0f;
            float duration = 0.24f;

            while (elapsed < duration && ghostObj != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                if (ghostSr != null)
                {
                    Color c = new Color(3.8f, 2.4f, 0.3f, Mathf.Lerp(0.85f, 0f, t));
                    ghostSr.color = c;
                    if (ghostMat.HasProperty("_Color")) ghostMat.SetColor("_Color", c);
                }

                if (spark != null)
                {
                    spark.transform.position += new Vector3(-direction * 2.5f * Time.deltaTime, Random.Range(-0.5f, 0.5f) * Time.deltaTime, 0f);
                    spark.transform.Rotate(0f, 0f, 360f * Time.deltaTime);
                    if (sparkSr != null)
                    {
                        Color sc = sparkSr.color;
                        sc.a = Mathf.Lerp(1f, 0f, t);
                        sparkSr.color = sc;
                    }
                }

                yield return null;
            }

            if (ghostObj != null) Destroy(ghostObj);
        }

        private IEnumerator SlimeBCrossClashImpactRoutine(Vector3 centerPosition)
        {
            GameObject clashRoot = new GameObject("SlimeB_CrossClashImpact");
            clashRoot.transform.position = centerPosition;

            // 1. Sóng Xung Kích Guillotine Mở Rộng Cực Đại (Massive Amber Shockwave Ring)
            GameObject ringObj = new GameObject("ClashShockwaveRing");
            ringObj.transform.SetParent(clashRoot.transform);
            ringObj.transform.position = centerPosition;
            SpriteRenderer ringSr = ringObj.AddComponent<SpriteRenderer>();
            ringSr.sprite = CreateCircleSprite();
            ringSr.color = new Color(4.2f, 3.2f, 0.6f, 1.0f); // Blinding Solar Amber
            ringSr.sortingOrder = 96;

            // 2. Vệt Chém Chéo Chữ X Phát Sáng (X-Slash Flash Flare)
            GameObject slash1 = new GameObject("CrossSlash_1");
            slash1.transform.SetParent(clashRoot.transform);
            slash1.transform.position = centerPosition;
            slash1.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            SpriteRenderer slashSr1 = slash1.AddComponent<SpriteRenderer>();
            slashSr1.sprite = CreateDiamondSprite();
            slashSr1.color = new Color(4.5f, 4.0f, 2.2f, 1.0f);
            slashSr1.sortingOrder = 98;

            GameObject slash2 = new GameObject("CrossSlash_2");
            slash2.transform.SetParent(clashRoot.transform);
            slash2.transform.position = centerPosition;
            slash2.transform.rotation = Quaternion.Euler(0f, 0f, -45f);
            SpriteRenderer slashSr2 = slash2.AddComponent<SpriteRenderer>();
            slashSr2.sprite = CreateDiamondSprite();
            slashSr2.color = new Color(4.5f, 4.0f, 2.2f, 1.0f);
            slashSr2.sortingOrder = 98;

            // 3. Vụ Nổ Bắn Tung 20 Mảnh Thạch Hổ Phách Phát Sáng (Amber Shard Nova)
            int shardCount = 20;
            List<(Transform tr, Vector2 vel, SpriteRenderer sr, float rotSpeed)> shards = new List<(Transform, Vector2, SpriteRenderer, float)>();

            for (int i = 0; i < shardCount; i++)
            {
                GameObject shard = new GameObject($"ClashShard_{i}");
                shard.transform.SetParent(clashRoot.transform);
                shard.transform.position = centerPosition;

                SpriteRenderer sr = shard.AddComponent<SpriteRenderer>();
                sr.sprite = CreateDiamondSprite();
                sr.color = (i % 3 == 0) ? new Color(4.5f, 3.8f, 1.2f, 1f) :
                           (i % 3 == 1) ? new Color(3.8f, 2.4f, 0.3f, 1f) :
                                          new Color(3.0f, 1.2f, 0.1f, 1f);
                sr.sortingOrder = 102;
                shard.transform.localScale = Vector3.one * Random.Range(0.18f, 0.42f);

                float angle = Random.Range(0f, 360f);
                float speed = Random.Range(5.5f, 15.0f);
                Vector2 vel = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * speed;
                float rotSpeed = Random.Range(-500f, 500f);

                shards.Add((shard.transform, vel, sr, rotSpeed));
            }

            float elapsed = 0f;
            float duration = 0.50f;

            while (elapsed < duration && clashRoot != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Animate Expanding Shockwave Ring (Mở rộng từ 0.3m lên 4.5m)
                if (ringSr != null)
                {
                    float ringScale = Mathf.Lerp(0.3f, 4.5f, Mathf.Sqrt(t));
                    ringObj.transform.localScale = new Vector3(ringScale, ringScale * 0.65f, 1f);
                    Color rc = ringSr.color;
                    rc.a = Mathf.Lerp(1.0f, 0f, t);
                    ringSr.color = rc;
                }

                // Animate Cross-Slash Flares
                if (slash1 != null && slash2 != null)
                {
                    float slashLength = Mathf.Lerp(0.5f, 5.0f, Mathf.Sqrt(t));
                    float slashWidth = Mathf.Lerp(0.4f, 0.02f, t * 1.4f);
                    slash1.transform.localScale = new Vector3(slashWidth, slashLength, 1f);
                    slash2.transform.localScale = new Vector3(slashWidth, slashLength, 1f);

                    float slashAlpha = Mathf.Clamp01(1f - t * 1.5f);
                    if (slashSr1 != null) slashSr1.color = new Color(4.5f, 4.0f, 2.2f, slashAlpha);
                    if (slashSr2 != null) slashSr2.color = new Color(4.5f, 4.0f, 2.2f, slashAlpha);
                }

                // Animate Flying Shards with Drag & Spin
                for (int i = 0; i < shards.Count; i++)
                {
                    var (tr, vel, sr, rotSpeed) = shards[i];
                    if (tr != null)
                    {
                        tr.position += (Vector3)(vel * Time.deltaTime);
                        tr.Rotate(0f, 0f, rotSpeed * Time.deltaTime);

                        if (sr != null)
                        {
                            Color sc = sr.color;
                            sc.a = Mathf.Clamp01(1f - t * 1.25f);
                            sr.color = sc;
                        }
                    }
                }

                yield return null;
            }

            if (clashRoot != null) Destroy(clashRoot);
        }

        private IEnumerator GenericBurstRoutine(Vector3 position, Color color, int count, float duration, float radius)
        {
            GameObject root = new GameObject("RedSlime_BurstVFX");
            root.transform.position = position;

            List<(Transform tr, Vector2 vel, SpriteRenderer sr)> list = new List<(Transform, Vector2, SpriteRenderer)>();
            for (int i = 0; i < count; i++)
            {
                GameObject p = new GameObject($"BurstPart_{i}");
                p.transform.SetParent(root.transform);
                p.transform.position = position;
                SpriteRenderer sr = p.AddComponent<SpriteRenderer>();
                sr.sprite = CreateDiamondSprite();
                sr.color = color;
                sr.sortingOrder = 100;
                p.transform.localScale = Vector3.one * Random.Range(0.12f, 0.3f);

                float angle = Random.Range(0f, 360f);
                float speed = Random.Range(3f, 9f);
                Vector2 vel = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * speed;
                list.Add((p.transform, vel, sr));
            }

            float elapsed = 0f;
            while (elapsed < duration && root != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                for (int i = 0; i < list.Count; i++)
                {
                    var (tr, vel, sr) = list[i];
                    if (tr != null)
                    {
                        tr.position += (Vector3)(vel * Time.deltaTime);
                        if (sr != null)
                        {
                            Color c = sr.color;
                            c.a = Mathf.Clamp01(1f - t);
                            sr.color = c;
                        }
                    }
                }
                yield return null;
            }

            if (root != null) Destroy(root);
        }

        private IEnumerator GenericGatheringRoutine(Vector3 centerPos, float duration, Color color, int count)
        {
            GameObject root = new GameObject("RedSlime_GatheringVFX");
            root.transform.position = centerPos;

            List<(Transform tr, Vector3 offset, SpriteRenderer sr)> list = new List<(Transform, Vector3, SpriteRenderer)>();
            for (int i = 0; i < count; i++)
            {
                GameObject p = new GameObject($"Gather_{i}");
                p.transform.SetParent(root.transform);
                float angle = (360f / count) * i + Random.Range(-10f, 10f);
                float dist = Random.Range(2.0f, 3.5f);
                Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * dist, Mathf.Sin(angle * Mathf.Deg2Rad) * dist, 0f);
                p.transform.position = centerPos + offset;

                SpriteRenderer sr = p.AddComponent<SpriteRenderer>();
                sr.sprite = CreateDiamondSprite();
                sr.color = color;
                sr.sortingOrder = 100;
                p.transform.localScale = Vector3.one * Random.Range(0.1f, 0.22f);

                list.Add((p.transform, offset, sr));
            }

            float elapsed = 0f;
            while (elapsed < duration && root != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = t * t;
                for (int i = 0; i < list.Count; i++)
                {
                    var (tr, offset, sr) = list[i];
                    if (tr != null)
                    {
                        tr.position = centerPos + Vector3.Lerp(offset, Vector3.zero, ease);
                    }
                }
                yield return null;
            }

            if (root != null) Destroy(root);
        }

        private IEnumerator GenericTrailRoutine(Vector3 position, Vector3 scale, float direction, Color color, float lifetime)
        {
            GameObject ghostObj = new GameObject("RedSlime_TrailGhost");
            ghostObj.transform.position = position;
            ghostObj.transform.localScale = scale;

            SpriteRenderer mainSr = GetComponentInChildren<SpriteRenderer>();
            if (mainSr != null && mainSr.sprite != null)
            {
                SpriteRenderer ghostSr = ghostObj.AddComponent<SpriteRenderer>();
                ghostSr.sprite = mainSr.sprite;
                ghostSr.flipX = mainSr.flipX;
                ghostSr.sortingOrder = mainSr.sortingOrder - 1;
                ghostSr.color = color;

                float elapsed = 0f;
                while (elapsed < lifetime)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / lifetime;
                    if (ghostSr != null)
                    {
                        Color c = color;
                        c.a = Mathf.Lerp(color.a, 0f, t);
                        ghostSr.color = c;
                    }
                    yield return null;
                }
            }

            Destroy(ghostObj);
        }

        private IEnumerator GenericImpactRoutine(Vector2 position, float direction, Color color, int count)
        {
            GameObject root = new GameObject("RedSlime_GenericImpact");
            root.transform.position = position;

            List<(SpriteRenderer sr, Vector2 vel, float lifetime)> particles = new List<(SpriteRenderer, Vector2, float)>();
            for (int i = 0; i < count; i++)
            {
                GameObject p = new GameObject($"Imp_{i}");
                p.transform.SetParent(root.transform);
                p.transform.position = position;

                SpriteRenderer sr = p.AddComponent<SpriteRenderer>();
                sr.sprite = CreateDiamondSprite();
                sr.color = color;
                sr.sortingOrder = 100;

                float angle = Random.Range(-60f, 60f) + (direction > 0 ? 0f : 180f);
                float speed = Random.Range(4f, 10f);
                Vector2 vel = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad) * speed, Mathf.Sin(angle * Mathf.Deg2Rad) * speed + Random.Range(1f, 3f));
                p.transform.localScale = Vector3.one * Random.Range(0.08f, 0.2f);

                particles.Add((sr, vel, Random.Range(0.2f, 0.45f)));
            }

            float elapsed = 0f;
            while (elapsed < 0.5f && root != null)
            {
                elapsed += Time.deltaTime;
                for (int i = 0; i < particles.Count; i++)
                {
                    var p = particles[i];
                    if (p.sr != null)
                    {
                        p.sr.transform.position += (Vector3)(p.vel * Time.deltaTime);
                        Color c = p.sr.color;
                        c.a = Mathf.Clamp01(1f - elapsed / p.lifetime);
                        p.sr.color = c;
                    }
                }
                yield return null;
            }

            if (root != null) Destroy(root);
        }

        /// <summary>Animate the boss dissolving and fading out on death.</summary>
        public Coroutine StartDeathDissolve(float duration)
        {
            return StartCoroutine(DeathDissolveRoutine(duration));
        }

        // ══════════════════════════════════════════════════
        //  COROUTINES
        // ══════════════════════════════════════════════════

        private IEnumerator HitFlashRoutine()
        {
            if (matInstances != null)
            {
                foreach (var mat in matInstances)
                {
                    if (mat != null && mat.HasProperty(ID_HitFlash))
                        mat.SetFloat(ID_HitFlash, 1.0f);
                }
            }

            if (renderers != null)
            {
                foreach (var r in renderers)
                {
                    if (r != null) r.color = hitFlashColor;
                }
            }

            yield return new WaitForSeconds(hitFlashDuration);

            if (matInstances != null)
            {
                foreach (var mat in matInstances)
                {
                    if (mat != null && mat.HasProperty(ID_HitFlash))
                        mat.SetFloat(ID_HitFlash, 0.0f);
                }
            }

            if (renderers != null)
            {
                foreach (var r in renderers)
                {
                    if (r != null) r.color = Color.white;
                }
            }
        }

        private IEnumerator WindupGatheringRoutine(Vector3 centerPos, float duration)
        {
            GameObject windupRoot = new GameObject("RedSlime_WindupVFX");
            windupRoot.transform.position = centerPos;

            List<(Transform tr, Vector3 startOffset, SpriteRenderer sr)> particles = new List<(Transform, Vector3, SpriteRenderer)>();

            for (int i = 0; i < windupParticleCount; i++)
            {
                GameObject p = new GameObject($"WindupPart_{i}");
                p.transform.SetParent(windupRoot.transform);

                float angle = (360f / windupParticleCount) * i + Random.Range(-15f, 15f);
                float dist = Random.Range(2.5f, 4.0f);
                Vector3 startOffset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * dist, Mathf.Sin(angle * Mathf.Deg2Rad) * dist, 0f);
                p.transform.position = centerPos + startOffset;

                SpriteRenderer sr = p.AddComponent<SpriteRenderer>();
                sr.sprite = CreateDiamondSprite();
                sr.color = Color.Lerp(phase1GlowColor, new Color(3f, 0.8f, 0.2f, 1f), Random.Range(0f, 0.5f));
                sr.sortingOrder = 100;
                p.transform.localScale = Vector3.one * Random.Range(0.12f, 0.25f);

                particles.Add((p.transform, startOffset, sr));
            }

            float elapsed = 0f;
            while (elapsed < duration && windupRoot != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easeIn = t * t;

                for (int i = 0; i < particles.Count; i++)
                {
                    var (tr, startOffset, sr) = particles[i];
                    if (tr != null)
                    {
                        tr.position = centerPos + Vector3.Lerp(startOffset, Vector3.zero, easeIn);
                        if (sr != null)
                        {
                            Color c = sr.color;
                            c.a = Mathf.Lerp(0.2f, 1.0f, t);
                            sr.color = c;
                        }
                    }
                }
                yield return null;
            }

            if (windupRoot != null) Destroy(windupRoot);
            windupCoroutine = null;
        }

        private IEnumerator GhostTrailRoutine(Vector3 position, Vector3 scale, float direction)
        {
            GameObject ghostObj = new GameObject("RedSlime_GhostTrail");
            ghostObj.transform.position = position;
            ghostObj.transform.localScale = scale;

            SpriteRenderer mainSr = GetComponentInChildren<SpriteRenderer>();
            if (mainSr != null && mainSr.sprite != null)
            {
                SpriteRenderer ghostSr = ghostObj.AddComponent<SpriteRenderer>();
                ghostSr.sprite = mainSr.sprite;
                ghostSr.flipX = mainSr.flipX;
                ghostSr.sortingOrder = mainSr.sortingOrder - 1;
                ghostSr.color = trailColor;

                float elapsed = 0f;
                while (elapsed < ghostLifetime)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / ghostLifetime;
                    if (ghostSr != null)
                    {
                        Color c = trailColor;
                        c.a = Mathf.Lerp(trailColor.a, 0f, t);
                        ghostSr.color = c;
                    }
                    yield return null;
                }
            }

            Destroy(ghostObj);
        }

        private IEnumerator ImpactVFXRoutine(Vector2 position, float direction)
        {
            GameObject vfxRoot = new GameObject("RedSlime_ImpactVFX");
            vfxRoot.transform.position = position;

            List<(SpriteRenderer sr, Vector2 velocity, float lifetime)> particles
                = new List<(SpriteRenderer, Vector2, float)>();

            for (int i = 0; i < impactParticleCount; i++)
            {
                GameObject particle = new GameObject($"Particle_{i}");
                particle.transform.SetParent(vfxRoot.transform);
                particle.transform.position = position;

                SpriteRenderer sr = particle.AddComponent<SpriteRenderer>();
                sr.sprite = CreateDiamondSprite();
                sr.color = Color.Lerp(impactColor, new Color(3f, 1f, 0.2f, 1f), Random.Range(0f, 0.5f));
                sr.sortingOrder = 100;

                float angle = Random.Range(-60f, 60f) + (direction > 0 ? 0f : 180f);
                float speed = Random.Range(4f, 10f);
                Vector2 vel = new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * speed,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * speed + Random.Range(1f, 4f)
                );

                float scale = Random.Range(0.08f, 0.25f);
                particle.transform.localScale = Vector3.one * scale;

                particles.Add((sr, vel, Random.Range(0.2f, 0.5f)));
            }

            float elapsed = 0f;
            float maxTime = 0.6f;
            while (elapsed < maxTime && vfxRoot != null)
            {
                elapsed += Time.deltaTime;
                for (int i = 0; i < particles.Count; i++)
                {
                    var p = particles[i];
                    if (p.sr != null)
                    {
                        p.sr.transform.position += (Vector3)(p.velocity * Time.deltaTime);
                        particles[i] = (p.sr, p.velocity + Vector2.down * 15f * Time.deltaTime, p.lifetime);

                        float alpha = Mathf.Clamp01(1f - elapsed / p.lifetime);
                        Color c = p.sr.color;
                        c.a = alpha;
                        p.sr.color = c;
                        p.sr.transform.localScale *= (1f - Time.deltaTime * 2f);
                    }
                }
                yield return null;
            }

            if (vfxRoot != null) Destroy(vfxRoot);
        }

        private IEnumerator GroundImpactVFXRoutine(Vector3 position)
        {
            GameObject vfxRoot = new GameObject("RedSlime_GroundImpactVFX");
            vfxRoot.transform.position = position;

            GameObject ringObj = new GameObject("ShockwaveRing");
            ringObj.transform.SetParent(vfxRoot.transform);
            ringObj.transform.position = position;

            SpriteRenderer ringSr = ringObj.AddComponent<SpriteRenderer>();
            ringSr.sprite = CreateCircleSprite();
            ringSr.color = new Color(groundImpactColor.r, groundImpactColor.g, groundImpactColor.b, 0.8f);
            ringSr.sortingOrder = 90;
            ringObj.transform.localScale = Vector3.one * 0.3f;

            List<(SpriteRenderer sr, Vector2 velocity)> debris = new List<(SpriteRenderer, Vector2)>();

            for (int i = 0; i < groundImpactParticleCount; i++)
            {
                GameObject part = new GameObject($"Debris_{i}");
                part.transform.SetParent(vfxRoot.transform);
                part.transform.position = position + Vector3.up * 0.1f;

                SpriteRenderer sr = part.AddComponent<SpriteRenderer>();
                sr.sprite = CreateDiamondSprite();
                sr.color = Color.Lerp(groundImpactColor, new Color(1f, 0.8f, 0.2f, 1f), Random.Range(0f, 0.4f));
                sr.sortingOrder = 95;

                float angle = Random.Range(30f, 150f);
                float speed = Random.Range(5f, 14f);
                Vector2 vel = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad) * speed, Mathf.Sin(angle * Mathf.Deg2Rad) * speed);

                float scale = Random.Range(0.1f, 0.35f);
                part.transform.localScale = Vector3.one * scale;

                debris.Add((sr, vel));
            }

            float elapsed = 0f;
            float maxTime = 0.8f;
            while (elapsed < maxTime && vfxRoot != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / maxTime;

                if (ringSr != null)
                {
                    float ringScale = Mathf.Lerp(0.3f, groundImpactShockwaveSize, Mathf.Sqrt(t));
                    ringObj.transform.localScale = new Vector3(ringScale, ringScale * 0.3f, 1f);
                    Color ringColor = ringSr.color;
                    ringColor.a = Mathf.Lerp(0.8f, 0f, t);
                    ringSr.color = ringColor;
                }

                for (int i = 0; i < debris.Count; i++)
                {
                    var d = debris[i];
                    if (d.sr != null)
                    {
                        d.sr.transform.position += (Vector3)(d.velocity * Time.deltaTime);
                        debris[i] = (d.sr, d.velocity + Vector2.down * 20f * Time.deltaTime);

                        Color c = d.sr.color;
                        c.a = Mathf.Clamp01(1f - t * 1.5f);
                        d.sr.color = c;
                    }
                }

                yield return null;
            }

            if (vfxRoot != null) Destroy(vfxRoot);
        }

        private IEnumerator ScreenShakeRoutine(float duration, float intensity)
        {
            if (mainCam == null) mainCam = Camera.main;
            if (mainCam == null) yield break;

            Vector3 originalPos = mainCam.transform.position;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float dampedIntensity = intensity * (1f - elapsed / duration);
                mainCam.transform.position = originalPos + (Vector3)(Random.insideUnitCircle * dampedIntensity);
                yield return null;
            }

            mainCam.transform.position = originalPos;
        }

        private IEnumerator WarningCircleRoutine(Vector3 position, float duration, float radius)
        {
            GameObject warningObj = new GameObject("RedSlime_WarningCircle");
            warningObj.transform.position = position;

            SpriteRenderer sr = warningObj.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite();
            sr.sortingOrder = -5;

            Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (urpUnlit != null)
            {
                Material mat = new Material(urpUnlit);
                mat.SetFloat("_Surface", 1);
                mat.SetFloat("_Blend", 0);
                sr.material = mat;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                float scale = Mathf.Lerp(0.1f, radius * 2f, 1f - Mathf.Pow(1f - Mathf.Min(t / 0.2f, 1f), 3f));
                warningObj.transform.localScale = new Vector3(scale, scale * 0.45f, 1f);

                float flash = Mathf.Sin(elapsed * Mathf.Lerp(8f, 35f, t * t)) * 0.5f + 0.5f;
                float baseAlpha = Mathf.Lerp(0.3f, 1f, t);
                Color col = new Color(3f, 0.3f, 0.3f, baseAlpha * (0.4f + 0.6f * flash));

                if (t > 0.85f)
                    col = Color.Lerp(col, new Color(3f, 3f, 3f, 1f), (t - 0.85f) / 0.15f);

                sr.color = col;
                yield return null;
            }

            Destroy(warningObj);
        }

        private IEnumerator TentacleRevealRoutine(Vector3 position)
        {
            GameObject burstRoot = new GameObject("TentacleRevealBurst");
            burstRoot.transform.position = position;

            int count = 8;
            List<SpriteRenderer> particles = new List<SpriteRenderer>();

            for (int i = 0; i < count; i++)
            {
                GameObject p = new GameObject($"RevealParticle_{i}");
                p.transform.SetParent(burstRoot.transform);
                p.transform.position = position;

                SpriteRenderer sr = p.AddComponent<SpriteRenderer>();
                sr.sprite = CreateDiamondSprite();
                sr.color = Color.Lerp(phase2GlowColor, new Color(3f, 0.5f, 1f, 1f), Random.Range(0f, 0.5f));
                sr.sortingOrder = 100;
                p.transform.localScale = Vector3.one * Random.Range(0.15f, 0.4f);

                particles.Add(sr);
            }

            float elapsed = 0f;
            float maxTime = 0.5f;
            while (elapsed < maxTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / maxTime;

                for (int i = 0; i < particles.Count; i++)
                {
                    if (particles[i] == null) continue;
                    float angle = (360f / count) * i;
                    float dist = Mathf.Lerp(0f, 1.5f, Mathf.Sqrt(t));
                    particles[i].transform.position = position + new Vector3(
                        Mathf.Cos(angle * Mathf.Deg2Rad) * dist,
                        Mathf.Sin(angle * Mathf.Deg2Rad) * dist, 0f);

                    Color c = particles[i].color;
                    c.a = Mathf.Lerp(1f, 0f, t);
                    particles[i].color = c;
                }
                yield return null;
            }

            Destroy(burstRoot);
        }

        private IEnumerator DeathDissolveRoutine(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                if (renderers != null)
                {
                    foreach (var r in renderers)
                    {
                        if (r != null)
                        {
                            Color c = r.color;
                            c.a = Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0f, 1f, t));
                            c.r = Mathf.Lerp(c.r, 2f, t * 0.5f);
                            r.color = c;
                            r.transform.localScale = Vector3.Lerp(r.transform.localScale, r.transform.localScale * 0.95f, t * 0.1f);
                        }
                    }
                }

                yield return null;
            }
        }

        /// <summary>
        /// Hiệu ứng tụ năng lượng vàng rực vào miệng Slime B trước khi phóng đạn Tri-Burst.
        /// </summary>
        public void TriggerSlimeBSpitGathering(Vector3 mouthPos, float duration)
        {
            StartCoroutine(SlimeBSpitGatheringRoutine(mouthPos, duration));
        }

        private IEnumerator SlimeBSpitGatheringRoutine(Vector3 mouthPos, float duration)
        {
            GameObject gatherRoot = new GameObject("SlimeB_SpitGatheringVFX");
            gatherRoot.transform.position = mouthPos;

            int count = 14;
            List<(Transform tr, Vector3 startOffset, SpriteRenderer sr)> particles = new List<(Transform, Vector3, SpriteRenderer)>();

            for (int i = 0; i < count; i++)
            {
                GameObject p = new GameObject($"Gather_{i}");
                p.transform.SetParent(gatherRoot.transform);

                float angle = (360f / count) * i + Random.Range(-20f, 20f);
                float dist = Random.Range(1.8f, 3.2f);
                Vector3 startOffset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * dist, Mathf.Sin(angle * Mathf.Deg2Rad) * dist, 0f);
                p.transform.position = mouthPos + startOffset;

                SpriteRenderer sr = p.AddComponent<SpriteRenderer>();
                sr.sprite = CreateDiamondSprite();
                sr.color = new Color(3.2f, 2.2f, 0.2f, 1f); // Radiant Solar Amber
                sr.sortingOrder = 90;
                p.transform.localScale = Vector3.one * Random.Range(0.12f, 0.26f);

                particles.Add((p.transform, startOffset, sr));
            }

            float elapsed = 0f;
            while (elapsed < duration && gatherRoot != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easeIn = t * t * t;

                foreach (var part in particles)
                {
                    if (part.tr == null) continue;
                    part.tr.position = mouthPos + Vector3.Lerp(part.startOffset, Vector3.zero, easeIn);
                    if (part.sr != null)
                    {
                        Color c = part.sr.color;
                        c.a = Mathf.Lerp(0.9f, 0.1f, easeIn);
                        part.sr.color = c;
                    }
                }
                yield return null;
            }

            if (gatherRoot != null) Destroy(gatherRoot);
        }

        /// <summary>
        /// Hiệu ứng tóe thạch hổ phách phát quang và mảnh vỡ khi đạn Slime B trúng đích.
        /// </summary>
        public void SpawnSlimeBElementImpact(Vector3 position)
        {
            StartCoroutine(SlimeBElementImpactRoutine(position));
        }

        private IEnumerator SlimeBElementImpactRoutine(Vector3 position)
        {
            GameObject root = new GameObject("SlimeB_ElementImpactVFX");
            root.transform.position = position;

            // 1. Vòng sóng xung kích chớp sáng (Amber Shockwave Ring)
            GameObject ringObj = new GameObject("AmberWaveRing");
            ringObj.transform.SetParent(root.transform);
            ringObj.transform.position = position;
            SpriteRenderer ringSr = ringObj.AddComponent<SpriteRenderer>();
            ringSr.sprite = CreateCircleSprite();
            ringSr.color = new Color(4.0f, 3.2f, 0.6f, 0.95f);
            ringSr.sortingOrder = 96;

            // 2. Chùm tia tóe thạch vàng óng & giọt mật hổ phách (Molten Droplets)
            int count = 12;
            List<(Transform tr, Vector2 vel, SpriteRenderer sr, float rotSpeed)> drops = new List<(Transform, Vector2, SpriteRenderer, float)>();
            for (int i = 0; i < count; i++)
            {
                GameObject drop = new GameObject($"AmberDrop_{i}");
                drop.transform.SetParent(root.transform);
                drop.transform.position = position;

                SpriteRenderer sr = drop.AddComponent<SpriteRenderer>();
                sr.sprite = CreateDiamondSprite();
                sr.color = (i % 2 == 0) ? new Color(4.0f, 3.2f, 0.6f, 1f) : new Color(3.0f, 1.4f, 0.1f, 1f);
                sr.sortingOrder = 101;
                drop.transform.localScale = Vector3.one * Random.Range(0.14f, 0.28f);

                float angle = Random.Range(0f, 360f);
                float speed = Random.Range(3.5f, 9.0f);
                Vector2 vel = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * speed;
                float rotSpeed = Random.Range(-360f, 360f);

                drops.Add((drop.transform, vel, sr, rotSpeed));
            }

            float elapsed = 0f;
            float duration = 0.35f;

            while (elapsed < duration && root != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                if (ringSr != null)
                {
                    float ringScale = Mathf.Lerp(0.2f, 1.8f, Mathf.Sqrt(t));
                    ringObj.transform.localScale = new Vector3(ringScale, ringScale * 0.7f, 1f);
                    Color rc = ringSr.color;
                    rc.a = Mathf.Lerp(0.95f, 0f, t);
                    ringSr.color = rc;
                }

                for (int i = 0; i < drops.Count; i++)
                {
                    var (tr, vel, sr, rotSpeed) = drops[i];
                    if (tr != null)
                    {
                        tr.position += (Vector3)(vel * Time.deltaTime);
                        tr.Rotate(0f, 0f, rotSpeed * Time.deltaTime);
                        if (sr != null)
                        {
                            Color sc = sr.color;
                            sc.a = Mathf.Clamp01(1f - t * 1.2f);
                            sr.color = sc;
                        }
                    }
                }

                yield return null;
            }

            if (root != null) Destroy(root);
        }

        /// <summary>
        /// Hiệu ứng lóe sáng miệng (Muzzle Flash) và sóng chấn động khi phóng đạn Tri-Burst.
        /// </summary>
        public void SpawnTriBurstMuzzleFlash(Vector3 mouthPos, float direction)
        {
            StartCoroutine(TriBurstMuzzleFlashRoutine(mouthPos, direction));
        }

        private IEnumerator TriBurstMuzzleFlashRoutine(Vector3 mouthPos, float direction)
        {
            GameObject flashObj = new GameObject("SlimeB_MuzzleFlash");
            flashObj.transform.position = mouthPos;

            SpriteRenderer sr = flashObj.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite();
            sr.color = new Color(3.5f, 2.5f, 0.3f, 0.9f); // Radiant Flash Gold
            sr.sortingOrder = 95;

            float elapsed = 0f;
            float dur = 0.2f;

            while (elapsed < dur && flashObj != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;

                float scale = Mathf.Lerp(0.2f, 1.8f, Mathf.Sqrt(t));
                flashObj.transform.localScale = new Vector3(scale * 1.2f, scale * 0.8f, 1f);

                Color c = sr.color;
                c.a = Mathf.Lerp(0.9f, 0f, t);
                sr.color = c;

                yield return null;
            }

            if (flashObj != null) Destroy(flashObj);
        }

        // ══════════════════════════════════════════════════
        //  SPRITE GENERATORS
        // ══════════════════════════════════════════════════

        private static Sprite _cachedCircle;
        private static Sprite CreateCircleSprite()
        {
            if (_cachedCircle != null) return _cachedCircle;

            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            float radius = size / 2f;
            Vector2 center = new Vector2(radius, radius);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist <= radius)
                    {
                        float alpha = 1f - Mathf.Pow(dist / radius, 2f);
                        colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                    else
                    {
                        colors[y * size + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            _cachedCircle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _cachedCircle;
        }

        private static Sprite _cachedDiamond;
        private static Sprite CreateDiamondSprite()
        {
            if (_cachedDiamond != null) return _cachedDiamond;

            int size = 16;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            float half = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - half) / half;
                    float dy = Mathf.Abs(y - half) / half;
                    if (dx + dy <= 1f)
                    {
                        float alpha = 1f - (dx + dy);
                        colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                    else
                    {
                        colors[y * size + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            _cachedDiamond = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _cachedDiamond;
        }
    }
}
