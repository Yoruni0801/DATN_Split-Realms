using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

namespace Rota.VFX
{
    /// <summary>
    /// Bộ điều khiển Laser Carving & Cháy Cây chuẩn AAA:
    /// - Chỉ tương tác và chặn tia sáng khi tia THỰC SỰ CHẠM VÀO PHẦN GỖ (Sprite) của cây.
    /// - Nếu trước mặt tia là khoảng trống không có gỗ, tia sẽ xuyên thẳng qua ngay lập tức.
    /// - Khi chạm trúng cành gỗ, ngọn lửa và quầng nung nóng xuất hiện chính xác tại cành đó,
    ///   nung chảy và khoan sâu dần qua bề dày cành gỗ theo thời gian thực.
    /// - Quản lý hạt 4 tầng (Sparks Jet, Volumetric Smoke, Rising Embers, Flame Core) bám theo đầu khoan laser.
    /// </summary>
    [DisallowMultipleComponent]
    public class BurnDissolveController : MonoBehaviour
    {
        [System.Serializable]
        public struct LaserBoreChannel
        {
            public Vector2 entryPoint;
            public Vector2 exitPoint;
            public Vector2 currentHeadPoint;
            public Vector2 direction;
            public float totalThickness;
            public float currentDepth;
            public float boreRadius;
            public float maxBoreRadius;
            public float heat;
            public bool isIgnited;
            public bool isPenetrated;
            public bool isActive;
        }

        [Header("Laser Drilling Dynamics")]
        [Tooltip("Tốc độ khoan/nung chảy ăn sâu vào thân cây (m/s)")]
        [Range(0.2f, 15f)]
        [SerializeField] private float drillSpeed = 3.5f;

        [Tooltip("Tốc độ tích nhiệt trước khi bùng cháy")]
        [Range(0.5f, 15f)]
        [SerializeField] private float heatAbsorptionRate = 5.0f;

        [Tooltip("Tốc độ mở rộng bán kính rãnh khoét (m/s)")]
        [Range(0.2f, 8f)]
        [SerializeField] private float radialExpansionSpeed = 2.0f;

        [Tooltip("Bán kính tối đa của rãnh khoét để thiêu rụi toàn bộ cành cây")]
        [SerializeField] private float maxBurnRadius = 8.0f;

        [Tooltip("Bán kính rãnh khoét ban đầu của tia laser")]
        [SerializeField] private float initialBoreRadius = 0.4f;

        [Header("Shader Properties - Pattern & Shape")]
        [SerializeField] private float noiseScale = 1.4f;
        [SerializeField] private float noiseStrength = 0.8f;
        [SerializeField] private float burnEdgeWidth = 0.16f;
        [SerializeField] private float charWidth = 0.42f;
        [SerializeField] private float ashWidth = 0.05f;
        [SerializeField] private float upwardBuoyancy = 0.35f;

        [Header("Shader Properties - Balanced Fire Colors")]
        [SerializeField] private Color charColor = new Color(0.05f, 0.015f, 0.008f, 1f);
        [ColorUsage(true, true)] [SerializeField] private Color fireDark = new Color(0.85f, 0.08f, 0.01f, 1f);
        [ColorUsage(true, true)] [SerializeField] private Color fireHot = new Color(1.0f, 0.38f, 0.02f, 1f);
        [ColorUsage(true, true)] [SerializeField] private Color fireWarm = new Color(1.0f, 0.82f, 0.12f, 1f);
        [ColorUsage(true, true)] [SerializeField] private Color fireCore = new Color(1.0f, 0.98f, 0.85f, 1f);
        [SerializeField] private Color ashColor = new Color(0.24f, 0.22f, 0.20f, 1f);

        [Header("Shader Properties - Motion & Glow")]
        [SerializeField] private float flameFlickerSpeed = 3.5f;
        [SerializeField] private float flameTurbulence = 0.75f;
        [SerializeField] private float flameGlow = 2.2f;
        [SerializeField] private float voronoiScale = 12f;

        [Header("Dynamic Lighting")]
        [SerializeField] private bool enableDynamicLighting = true;
        [ColorUsage(false, true)] [SerializeField] private Color lightColor = new Color(1f, 0.55f, 0.15f, 1f);
        [SerializeField] private float maxLightIntensity = 3.5f;
        [SerializeField] private float lightRange = 7f;

        [Header("Particle VFX & Audio")]
        [SerializeField] private GameObject burnParticlePrefab;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip igniteSFX;
        [SerializeField] private AudioClip burningLoopSFX;
        [SerializeField] private AudioClip ashCollapseSFX;

        [Header("Material Override")]
        [SerializeField] private Material burnMaterialOverride;

        private const int MAX_CHANNELS = 8;
        private SpriteRenderer[] _renderers;
        private Collider2D[] _colliders;
        private MaterialPropertyBlock _block;
        private LaserBoreChannel[] _channels;
        private Vector4[] _shaderSegments;
        private Vector4[] _shaderSegmentData;
        private Vector4 _shaderLaserHit;

        // Shader Property IDs
        private int _idNoiseScale, _idNoiseStrength, _idBurnEdgeWidth, _idCharWidth, _idAshWidth, _idUpwardBuoyancy;
        private int _idCharColor, _idFireDark, _idFireHot, _idFireWarm, _idFireCore, _idAshColor;
        private int _idFlameFlickerSpeed, _idFlameTurbulence, _idFlameGlow, _idVoronoiScale;
        private int _idSegments, _idSegmentData, _idCenterCount, _idLaserHit;

        // Runtime State
        private Light _flickerLight;
        private GameObject _activeParticlesObj;
        private LaserWoodBurnVFX _activeLaserVFX;
        private ParticleSystem _activeLegacyParticles;
        private bool _isBurning;
        private bool _isFullyDestroyed;
        private float _lastHitTimestamp;
        private Vector2 _currentLaserHead;
        private float _currentLaserHeadHeat;
        private Vector2 _currentLaserDir;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            _colliders = GetComponentsInChildren<Collider2D>(true);
            _block = new MaterialPropertyBlock();
            _channels = new LaserBoreChannel[MAX_CHANNELS];
            _shaderSegments = new Vector4[MAX_CHANNELS];
            _shaderSegmentData = new Vector4[MAX_CHANNELS];

            CachePropertyIDs();
            EnsureBurnMaterial();
            SetupDynamicLight();
            SetupAudio();

            ApplyStaticProps();
        }

        private void CachePropertyIDs()
        {
            _idNoiseScale = Shader.PropertyToID("_NoiseScale");
            _idNoiseStrength = Shader.PropertyToID("_NoiseStrength");
            _idBurnEdgeWidth = Shader.PropertyToID("_BurnEdgeWidth");
            _idCharWidth = Shader.PropertyToID("_CharWidth");
            _idAshWidth = Shader.PropertyToID("_AshWidth");
            _idUpwardBuoyancy = Shader.PropertyToID("_UpwardBuoyancy");

            _idCharColor = Shader.PropertyToID("_CharColor");
            _idFireDark = Shader.PropertyToID("_FireColorDark");
            _idFireHot = Shader.PropertyToID("_FireColorHot");
            _idFireWarm = Shader.PropertyToID("_FireColorWarm");
            _idFireCore = Shader.PropertyToID("_FireColorCore");
            _idAshColor = Shader.PropertyToID("_AshColor");

            _idFlameFlickerSpeed = Shader.PropertyToID("_FlameFlickerSpeed");
            _idFlameTurbulence = Shader.PropertyToID("_FlameTurbulence");
            _idFlameGlow = Shader.PropertyToID("_FlameGlow");
            _idVoronoiScale = Shader.PropertyToID("_VoronoiScale");

            _idSegments = Shader.PropertyToID("_BurnSegments");
            _idSegmentData = Shader.PropertyToID("_BurnSegmentData");
            _idCenterCount = Shader.PropertyToID("_BurnCenterCount");
            _idLaserHit = Shader.PropertyToID("_LaserHitPoint");
        }

        private void SetupDynamicLight()
        {
            if (!enableDynamicLighting) return;

            GameObject lightObj = new GameObject("Burn_DynamicFlickerLight");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = new Vector3(0, 0, -0.5f);

            _flickerLight = lightObj.AddComponent<Light>();
            _flickerLight.type = LightType.Point;
            _flickerLight.color = lightColor;
            _flickerLight.range = lightRange;
            _flickerLight.intensity = 0f;
            _flickerLight.shadows = LightShadows.None;
            _flickerLight.enabled = false;
        }

        private void SetupAudio()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0.8f;
                    audioSource.minDistance = 3f;
                    audioSource.maxDistance = 25f;
                }
            }
        }

        private void EnsureBurnMaterial()
        {
            if (burnMaterialOverride == null) return;

            foreach (var r in _renderers)
            {
                if (r != null && r.sharedMaterial != burnMaterialOverride)
                {
                    r.sharedMaterial = burnMaterialOverride;
                }
            }
        }

        public struct WoodHitInfo
        {
            public Vector2 entryPoint;
            public Vector2 exitPoint;
            public float distance;
            public float thickness;
            public SpriteRenderer hitRenderer;
        }

        /// <summary>
        /// Bắn ray kiểm tra va chạm thực tế với từng cành cây (SpriteRenderers).
        /// Nếu tia đi qua khoảng trống không có gỗ, hàm trả về false để tia xuyên qua ngay.
        /// </summary>
        public bool RaycastVisibleWood(Vector2 rayOrigin, Vector2 rayDir, float maxDist, out WoodHitInfo hitInfo)
        {
            hitInfo = default;
            if (_isFullyDestroyed || _renderers == null || _renderers.Length == 0) return false;

            float closestDist = float.MaxValue;
            bool found = false;

            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null || !r.enabled || r.sprite == null) continue;

                Transform t = r.transform;
                Vector2 spriteSize = r.size;
                if (spriteSize.x <= 0.001f || spriteSize.y <= 0.001f)
                {
                    spriteSize = r.sprite.rect.size / r.sprite.pixelsPerUnit;
                }

                float halfW = spriteSize.x * 0.5f;
                float halfH = spriteSize.y * 0.5f;

                Vector3 localOrigin3D = t.InverseTransformPoint(new Vector3(rayOrigin.x, rayOrigin.y, t.position.z));
                Vector3 localDir3D = t.InverseTransformDirection(new Vector3(rayDir.x, rayDir.y, 0f)).normalized;

                Vector2 localOrigin = (Vector2)localOrigin3D;
                Vector2 localDir = ((Vector2)localDir3D).normalized;

                if (localDir.sqrMagnitude < 0.0001f) continue;

                float tx1 = (-halfW - localOrigin.x) / (Mathf.Abs(localDir.x) > 1e-5f ? localDir.x : 1e-5f);
                float tx2 = (halfW - localOrigin.x) / (Mathf.Abs(localDir.x) > 1e-5f ? localDir.x : 1e-5f);
                float tminX = Mathf.Min(tx1, tx2);
                float tmaxX = Mathf.Max(tx1, tx2);

                float ty1 = (-halfH - localOrigin.y) / (Mathf.Abs(localDir.y) > 1e-5f ? localDir.y : 1e-5f);
                float ty2 = (halfH - localOrigin.y) / (Mathf.Abs(localDir.y) > 1e-5f ? localDir.y : 1e-5f);
                float tminY = Mathf.Min(ty1, ty2);
                float tmaxY = Mathf.Max(ty1, ty2);

                float tNear = Mathf.Max(tminX, tminY);
                float tFar = Mathf.Min(tmaxX, tmaxY);

                if (tNear <= tFar && tFar > 0f)
                {
                    float hitT = tNear > 0f ? tNear : 0f;
                    Vector2 localEntry = localOrigin + localDir * hitT;
                    Vector2 localExit = localOrigin + localDir * tFar;

                    Vector2 worldEntry = (Vector2)t.TransformPoint(localEntry);
                    Vector2 worldExit = (Vector2)t.TransformPoint(localExit);

                    float dist = Vector2.Distance(rayOrigin, worldEntry);
                    if (dist < closestDist && dist <= maxDist)
                    {
                        if (!IsPointBurned(worldEntry))
                        {
                            closestDist = dist;
                            hitInfo = new WoodHitInfo
                            {
                                entryPoint = worldEntry,
                                exitPoint = worldExit,
                                distance = dist,
                                thickness = Mathf.Max(0.25f, Vector2.Distance(worldEntry, worldExit)),
                                hitRenderer = r
                            };
                            found = true;
                        }
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// Xử lý tia Laser tương tác và khoan cành gỗ theo thời gian thực.
        /// </summary>
        public bool ProcessLaserRay(Vector2 rayOrigin, Vector2 rayDir, float remainingDist, out Vector3 beamEndPos, out bool isPenetrated, out bool hasWoodHit)
        {
            beamEndPos = rayOrigin + rayDir * remainingDist;
            isPenetrated = true;
            hasWoodHit = false;

            if (_isFullyDestroyed) return false;

            // 1. Kiểm tra xem tia laser có thực sự chạm vào bất kỳ cành gỗ nào không
            if (!RaycastVisibleWood(rayOrigin, rayDir, remainingDist, out WoodHitInfo hit))
            {
                if (_activeLaserVFX != null)
                {
                    _activeLaserVFX.SetActiveState(false, 0f);
                }
                return false;
            }

            hasWoodHit = true;
            _isBurning = true;
            _lastHitTimestamp = Time.time;
            float dt = Time.deltaTime;
            _currentLaserDir = rayDir.normalized;

            // 2. Tìm hoặc tạo kênh khoan Laser cho cành gỗ này
            int channelIdx = FindOrCreateBoreChannel(hit.entryPoint, hit.exitPoint, rayDir, hit.thickness);
            if (channelIdx < 0)
            {
                beamEndPos = hit.entryPoint;
                isPenetrated = false;
                return true;
            }

            ref var ch = ref _channels[channelIdx];

            // 3. Tích nhiệt và bùng cháy
            ch.heat = Mathf.Clamp01(ch.heat + heatAbsorptionRate * dt);
            if (ch.heat >= 0.35f && !ch.isIgnited)
            {
                ch.isIgnited = true;
                if (audioSource != null && igniteSFX != null)
                {
                    audioSource.PlayOneShot(igniteSFX, 0.85f);
                }
            }

            // 4. Khoan sâu dần vào cành gỗ
            bool justBreakthrough = false;
            if (ch.isIgnited && !ch.isPenetrated)
            {
                ch.currentDepth += drillSpeed * dt;

                if (ch.currentDepth >= ch.totalThickness)
                {
                    ch.currentDepth = ch.totalThickness;
                    ch.isPenetrated = true;
                    justBreakthrough = true;
                }

                ch.currentHeadPoint = ch.entryPoint + ch.direction * ch.currentDepth;
            }

            // 5. Cập nhật hệ thống Particle 4 tầng bám theo đầu khoan
            UpdateParticleVFX(ch.currentHeadPoint, ch.direction, ch.heat, justBreakthrough, ch.exitPoint);

            // 6. Xác định vị trí đầu tia laser
            if (ch.isPenetrated)
            {
                beamEndPos = ch.exitPoint;
                isPenetrated = true;
            }
            else
            {
                beamEndPos = ch.currentHeadPoint;
                isPenetrated = false;
            }

            _currentLaserHead = ch.currentHeadPoint;
            _currentLaserHeadHeat = ch.heat;

            ApplyStaticProps();
            return true;
        }

        private void UpdateParticleVFX(Vector2 headPos, Vector2 dir, float heat, bool justBreakthrough, Vector2 exitPt)
        {
            if (burnParticlePrefab == null) return;

            if (_activeParticlesObj == null)
            {
                _activeParticlesObj = Instantiate(burnParticlePrefab, transform);
                _activeLaserVFX = _activeParticlesObj.GetComponent<LaserWoodBurnVFX>();
                _activeLegacyParticles = _activeParticlesObj.GetComponent<ParticleSystem>();
            }

            if (_activeLaserVFX != null)
            {
                _activeLaserVFX.SetPositionAndDirection(headPos, dir);
                _activeLaserVFX.SetActiveState(true, heat);

                if (justBreakthrough)
                {
                    _activeLaserVFX.TriggerBreakthrough(exitPt, dir);
                }
            }
            else if (_activeLegacyParticles != null)
            {
                _activeParticlesObj.transform.position = headPos;
                if (!_activeLegacyParticles.isPlaying) _activeLegacyParticles.Play();
            }
        }

        private int FindOrCreateBoreChannel(Vector2 entryPt, Vector2 exitPt, Vector2 laserDir, float thickness)
        {
            int nearestIdx = -1;
            float nearestDist = float.MaxValue;

            for (int i = 0; i < MAX_CHANNELS; i++)
            {
                if (_channels[i].isActive)
                {
                    float dist = Vector2.Distance(_channels[i].entryPoint, entryPt);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestIdx = i;
                    }
                }
            }

            if (nearestIdx >= 0 && nearestDist < 0.6f)
            {
                return nearestIdx;
            }

            int emptyIdx = -1;
            for (int i = 0; i < MAX_CHANNELS; i++)
            {
                if (!_channels[i].isActive)
                {
                    emptyIdx = i;
                    break;
                }
            }

            if (emptyIdx < 0)
            {
                emptyIdx = nearestIdx >= 0 ? nearestIdx : 0;
            }

            _channels[emptyIdx] = new LaserBoreChannel
            {
                entryPoint = entryPt,
                exitPoint = exitPt,
                currentHeadPoint = entryPt,
                direction = laserDir.normalized,
                totalThickness = thickness,
                currentDepth = 0f,
                boreRadius = initialBoreRadius,
                maxBoreRadius = maxBurnRadius,
                heat = 0.25f,
                isIgnited = false,
                isPenetrated = false,
                isActive = true
            };

            return emptyIdx;
        }

        private void Update()
        {
            if (_isFullyDestroyed || !_isBurning) return;

            float dt = Time.deltaTime;
            bool anyActive = false;
            bool allFullyConsumed = true;
            Vector2 dominantFlamePos = Vector2.zero;
            float maxIntensity = 0f;

            bool isCurrentlyHit = (Time.time - _lastHitTimestamp) < 0.1f;
            if (!isCurrentlyHit)
            {
                _currentLaserHeadHeat = Mathf.Max(0f, _currentLaserHeadHeat - dt * 2.5f);
                if (_activeLaserVFX != null)
                {
                    _activeLaserVFX.SetActiveState(false, 0f);
                }
            }

            for (int i = 0; i < MAX_CHANNELS; i++)
            {
                if (!_channels[i].isActive) continue;

                anyActive = true;

                if (_channels[i].isIgnited)
                {
                    if (_channels[i].boreRadius < _channels[i].maxBoreRadius)
                    {
                        _channels[i].boreRadius += radialExpansionSpeed * dt;
                        allFullyConsumed = false;

                        if (_channels[i].heat > maxIntensity)
                        {
                            maxIntensity = _channels[i].heat;
                            dominantFlamePos = _channels[i].currentHeadPoint;
                        }
                    }
                    else
                    {
                        _channels[i].boreRadius = _channels[i].maxBoreRadius;
                        _channels[i].heat = Mathf.Max(0f, _channels[i].heat - dt * 0.35f);
                    }
                }
                else
                {
                    if (!isCurrentlyHit)
                    {
                        _channels[i].heat = Mathf.Max(0f, _channels[i].heat - dt * 1.5f);
                    }
                    allFullyConsumed = false;
                }
            }

            UpdateFlickeringLight(anyActive && maxIntensity > 0.05f, dominantFlamePos, maxIntensity);
            UpdateAudioLoop(anyActive && maxIntensity > 0.05f);
            ApplyStaticProps();

            if (anyActive && allFullyConsumed)
            {
                CompleteBurn();
            }
        }

        private void UpdateFlickeringLight(bool hasFire, Vector2 firePos, float intensity)
        {
            if (_flickerLight == null) return;

            if (hasFire && intensity > 0.1f)
            {
                _flickerLight.enabled = true;
                _flickerLight.transform.position = new Vector3(firePos.x, firePos.y, -0.6f);

                float noise = Mathf.PerlinNoise(Time.time * 14f, 0.0f);
                _flickerLight.intensity = (maxLightIntensity * intensity) * (0.75f + 0.5f * noise);
            }
            else
            {
                _flickerLight.intensity = Mathf.Lerp(_flickerLight.intensity, 0f, Time.deltaTime * 6f);
                if (_flickerLight.intensity <= 0.05f) _flickerLight.enabled = false;
            }
        }

        private void UpdateAudioLoop(bool isFireActive)
        {
            if (audioSource == null || burningLoopSFX == null) return;

            if (isFireActive)
            {
                if (!audioSource.isPlaying)
                {
                    audioSource.clip = burningLoopSFX;
                    audioSource.loop = true;
                    audioSource.volume = 0f;
                    audioSource.Play();
                }
                audioSource.volume = Mathf.Lerp(audioSource.volume, 0.75f, Time.deltaTime * 2.5f);
            }
            else
            {
                if (audioSource.isPlaying)
                {
                    audioSource.volume = Mathf.Lerp(audioSource.volume, 0f, Time.deltaTime * 4f);
                    if (audioSource.volume <= 0.05f) audioSource.Stop();
                }
            }
        }

        public void ApplyStaticProps()
        {
            if (_renderers == null) return;

            if (_isFullyDestroyed)
            {
                for (int i = 0; i < MAX_CHANNELS; i++)
                {
                    if (i == 0)
                    {
                        _shaderSegments[0] = new Vector4(transform.position.x - 20f, transform.position.y, transform.position.x + 20f, transform.position.y);
                        _shaderSegmentData[0] = new Vector4(999f, 0f, 999f, 1f);
                    }
                    else
                    {
                        _shaderSegments[i] = Vector4.zero;
                        _shaderSegmentData[i] = Vector4.zero;
                    }
                }
                _shaderLaserHit = Vector4.zero;
            }
            else
            {
                for (int i = 0; i < MAX_CHANNELS; i++)
                {
                    if (_channels[i].isActive && (_channels[i].boreRadius > 0.001f || _channels[i].heat > 0.001f))
                    {
                        _shaderSegments[i] = new Vector4(
                            _channels[i].entryPoint.x,
                            _channels[i].entryPoint.y,
                            _channels[i].currentHeadPoint.x,
                            _channels[i].currentHeadPoint.y
                        );

                        _shaderSegmentData[i] = new Vector4(
                            _channels[i].boreRadius,
                            _channels[i].heat,
                            _channels[i].maxBoreRadius,
                            1f
                        );
                    }
                    else
                    {
                        _shaderSegments[i] = Vector4.zero;
                        _shaderSegmentData[i] = Vector4.zero;
                    }
                }

                _shaderLaserHit = new Vector4(_currentLaserHead.x, _currentLaserHead.y, 0.55f, _currentLaserHeadHeat);
            }

            foreach (var r in _renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_block);

                _block.SetFloat(_idNoiseScale, noiseScale);
                _block.SetFloat(_idNoiseStrength, noiseStrength);
                _block.SetFloat(_idBurnEdgeWidth, burnEdgeWidth);
                _block.SetFloat(_idCharWidth, charWidth);
                _block.SetFloat(_idAshWidth, ashWidth);
                _block.SetFloat(_idUpwardBuoyancy, upwardBuoyancy);

                _block.SetColor(_idCharColor, charColor);
                _block.SetColor(_idFireDark, fireDark);
                _block.SetColor(_idFireHot, fireHot);
                _block.SetColor(_idFireWarm, fireWarm);
                _block.SetColor(_idFireCore, fireCore);
                _block.SetColor(_idAshColor, ashColor);

                _block.SetFloat(_idFlameFlickerSpeed, flameFlickerSpeed);
                _block.SetFloat(_idFlameTurbulence, flameTurbulence);
                _block.SetFloat(_idFlameGlow, flameGlow);
                _block.SetFloat(_idVoronoiScale, voronoiScale);

                _block.SetVectorArray(_idSegments, _shaderSegments);
                _block.SetVectorArray(_idSegmentData, _shaderSegmentData);
                _block.SetInt(_idCenterCount, _isFullyDestroyed ? 1 : MAX_CHANNELS);
                _block.SetVector(_idLaserHit, _shaderLaserHit);

                r.SetPropertyBlock(_block);
            }
        }

        public bool IsPointBurned(Vector2 worldPos)
        {
            if (_isFullyDestroyed) return true;
            if (!_isBurning) return false;

            for (int i = 0; i < MAX_CHANNELS; i++)
            {
                if (_channels[i].isActive && _channels[i].isPenetrated)
                {
                    float dist = DistToSegment(worldPos, _channels[i].entryPoint, _channels[i].exitPoint);
                    if (dist <= _channels[i].boreRadius)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ba = b - a;
            float l2 = ba.sqrMagnitude;
            if (l2 < 0.0001f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ba) / l2);
            return Vector2.Distance(p, a + t * ba);
        }

        private void CompleteBurn()
        {
            _isBurning = false;
            _isFullyDestroyed = true;

            DisableColliders();
            ApplyStaticProps();

            if (_flickerLight != null) _flickerLight.enabled = false;

            if (audioSource != null)
            {
                audioSource.Stop();
                if (ashCollapseSFX != null)
                {
                    audioSource.PlayOneShot(ashCollapseSFX, 0.7f);
                }
            }

            if (_activeLaserVFX != null)
            {
                _activeLaserVFX.SetActiveState(false, 0f);
            }
            else if (_activeLegacyParticles != null)
            {
                var main = _activeLegacyParticles.main;
                main.loop = false;
            }

            DOVirtual.DelayedCall(0.5f, () =>
            {
                if (_renderers != null)
                {
                    foreach (var r in _renderers)
                    {
                        if (r != null) r.enabled = false;
                    }
                }
            });
        }

        private void DisableColliders()
        {
            if (_colliders != null)
            {
                foreach (var col in _colliders) if (col) col.enabled = false;
            }
        }

        [ContextMenu("Play Test Burn")]
        public void PlayBurn()
        {
            if (_isFullyDestroyed || _isBurning || !Application.isPlaying) return;
            if (_renderers == null || _renderers.Length == 0) return;

            Bounds b = new Bounds(_renderers[0].transform.position, Vector3.zero);
            foreach (var r in _renderers)
                if (r.sprite != null) b.Encapsulate(r.bounds);

            _channels[0] = new LaserBoreChannel
            {
                entryPoint = (Vector2)b.center - Vector2.right * 2f,
                exitPoint = (Vector2)b.center + Vector2.right * 2f,
                currentHeadPoint = (Vector2)b.center + Vector2.right * 2f,
                direction = Vector2.right,
                totalThickness = 4f,
                currentDepth = 4f,
                boreRadius = 1f,
                maxBoreRadius = maxBurnRadius,
                heat = 1f,
                isIgnited = true,
                isPenetrated = true,
                isActive = true
            };

            _isBurning = true;
            ApplyStaticProps();
        }

        [ContextMenu("Reset Burn")]
        public void ResetBurn()
        {
            if (!Application.isPlaying) return;
            DOTween.Kill(this);

            _isFullyDestroyed = false;
            _isBurning = false;

            for (int i = 0; i < MAX_CHANNELS; i++)
            {
                _channels[i] = default;
                _shaderSegments[i] = Vector4.zero;
                _shaderSegmentData[i] = Vector4.zero;
            }
            _shaderLaserHit = Vector4.zero;

            if (_renderers != null)
            {
                foreach (var r in _renderers)
                {
                    if (r != null) r.enabled = true;
                }
            }

            if (_colliders != null)
            {
                foreach (var col in _colliders) if (col) col.enabled = true;
            }

            if (_flickerLight != null) _flickerLight.enabled = false;
            if (audioSource != null) audioSource.Stop();
            if (_activeParticlesObj != null) Destroy(_activeParticlesObj);

            ApplyStaticProps();
        }

        public bool IsBurning => _isBurning;
        public bool IsFullyDestroyed => _isFullyDestroyed;
    }
}