using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Quoc.QuyVuong;
using Quoc.QuyVuong.Phase1;
using Quoc.QuyVuong.Boss_Phase1;
using Quoc.QuyVuong.Boss_Phase2;
using Quoc.QuyVuong.Boss_Phase3;

namespace Quoc.QuyVuong.Audio
{
    /// <summary>
    /// Component quản lý toàn bộ hệ thống âm thanh SFX và nhạc nền BGM cho Boss Quỷ Vương (Tất Cả 5 Dạng: Phase 0, 1, 2, 3, 4):
    /// - Kết nối và đồng bộ 100% với hệ thống âm thanh toàn cục AudioManager (Assets/_Project/Duy/Script/Audio).
    /// - Phân định rõ ràng: Nhạc nền là BGM (điều khiển qua BGM Slider/Mixer), Chiêu thức/Va chạm là SFX (điều khiển qua SFX Slider/Mixer).
    /// - 100% An toàn Null-Safety, không bao giờ bị NullReferenceException.
    /// </summary>
    [DisallowMultipleComponent]
    public class QuyVuongAudioController : MonoBehaviour
    {
        public static QuyVuongAudioController Instance { get; private set; }

        [Header("=== NHẠC NỀN TRẬN ĐẤU (BGM CHO TỪNG DẠNG) ===")]
        [SerializeField] private BossBGMAudioData bgmData = new BossBGMAudioData();

        [Header("=== VÒNG ĐỜI & TRẠNG THÁI CHUNG (SFX & VOICE) ===")]
        [SerializeField] private BossLifecycleAudioData lifecycleAudio = new BossLifecycleAudioData();

        [Header("=== NÂNG CẤP TRÙM CUỐI (EPIC FINAL BOSS SFX) ===")]
        [SerializeField] private EpicFinalBossAudioData epicAudio = new EpicFinalBossAudioData();

        [Header("=== KỸ NĂNG PHASE 0 (CỔ ĐẠI & THỦY TIỄN MAMATICK) ===")]
        [SerializeField] private Phase0AudioData phase0Audio = new Phase0AudioData();

        [Header("=== KỸ NĂNG PHASE 1 (CUNG THỦ NƯỚC & KHÔNG KÍCH) ===")]
        [SerializeField] private Phase1AudioData phase1Audio = new Phase1AudioData();

        [Header("=== KỸ NĂNG PHASE 2 (TỬ THẦN LIỀM & ĐẠN ĐUỔI) ===")]
        [SerializeField] private Phase2AudioData phase2Audio = new Phase2AudioData();

        [Header("=== KỸ NĂNG PHASE 3 (ĐỊA CHẤN & THIÊN THẠCH) ===")]
        [SerializeField] private Phase3AudioData phase3Audio = new Phase3AudioData();

        [Header("=== KỸ NĂNG PHASE 4 (HỎA NHAM MA & ĐẦU LÂU LỬA) ===")]
        [SerializeField] private Phase4AudioData phase4Audio = new Phase4AudioData();

        [Header("=== ĐỒNG BỘ VỚI HỆ THỐNG AUDIOMANAGER TOÀN CỤC ===")]
        [Tooltip("Tự động đồng bộ với AudioManager.Instance toàn cục của game")]
        [SerializeField] private bool syncWithGlobalAudioManager = true;

        [Header("=== CÀI ĐẶT ÂM LƯỢNG & KHÔNG GIAN (AUDIO SETTINGS) ===")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float voiceVolume = 1.1f;

        [Tooltip("Độ biến thiên cao độ nhẹ ngẫu nhiên (tránh mỏi tai khi nghe lặp lại)")]
        [SerializeField, Range(0f, 0.2f)] private float pitchVariation = 0.05f;

        [Tooltip("Hiệu ứng không gian 2D (0) hoặc 3D (1)")]
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0.25f;
        [SerializeField] private float minSoundDistance = 5f;
        [SerializeField] private float maxSoundDistance = 45f;

        [Header("=== TÙY CHỌN TỰ ĐỘNG (AUTO HOOKS) ===")]
        [Tooltip("Tự động tìm kiếm các component Boss trong cây đối tượng")]
        [SerializeField] private bool autoFindBossComponents = true;

        [Tooltip("Tự động phát tiếng gầm khi Boss xuất hiện (Start)")]
        [SerializeField] private bool playRoarOnStart = true;

        [Tooltip("Tự động bật nhạc nền Phase tương ứng khi Boss xuất hiện / chuyển Phase")]
        [SerializeField] private bool autoManageBGM = true;

        // References tới các component cũ của Boss (Read-only observation)
        private QuyVuongBossHealth bossHealth;
        private QuyVuongBossController bossController;
        private QuyVuongPhaseSwitcher phaseSwitcher;
        private QuyVuongEpicAudioEnhancer epicEnhancer;

        // AudioMixer Groups từ AudioManager toàn cục
        private AudioMixerGroup bgmMixerGroup;
        private AudioMixerGroup sfxMixerGroup;

        // AudioSource Channels cục bộ
        private AudioSource bgmSourceA;
        private AudioSource bgmSourceB;
        private bool isUsingSourceA = true;
        private Coroutine bgmCrossfadeCoroutine;

        private AudioSource voiceAudioSource;
        private AudioSource chargeAudioSource;
        private AudioSource movementAudioSource;
        private AudioSource subBassAudioSource;

        // SFX Pool
        private const int SFX_POOL_SIZE = 10;
        private readonly List<AudioSource> sfxPool = new List<AudioSource>();
        private int currentSfxPoolIndex = 0;

        private bool isDead = false;
        private int lastObservedPhaseIndex = -1;

        #region Unity Lifecycle

        private void Awake()
        {
            Instance = this;

            EnsureDataInstances();
            ResolveGlobalMixerGroups();
            InitializeAudioSources();

            if (autoFindBossComponents)
            {
                FindBossComponents();
            }
        }

        private void EnsureDataInstances()
        {
            if (bgmData == null) bgmData = new BossBGMAudioData();
            if (lifecycleAudio == null) lifecycleAudio = new BossLifecycleAudioData();
            if (epicAudio == null) epicAudio = new EpicFinalBossAudioData();
            if (phase0Audio == null) phase0Audio = new Phase0AudioData();
            if (phase1Audio == null) phase1Audio = new Phase1AudioData();
            if (phase2Audio == null) phase2Audio = new Phase2AudioData();
            if (phase3Audio == null) phase3Audio = new Phase3AudioData();
            if (phase4Audio == null) phase4Audio = new Phase4AudioData();
        }

        private void ResolveGlobalMixerGroups()
        {
            if (!syncWithGlobalAudioManager) return;

            try
            {
                if (AudioManager.Instance != null && AudioManager.Instance.mainMixer != null)
                {
                    AudioMixer mixer = AudioManager.Instance.mainMixer;
                    var bgmGroups = mixer.FindMatchingGroups("BGM");
                    if (bgmGroups != null && bgmGroups.Length > 0) bgmMixerGroup = bgmGroups[0];

                    var sfxGroups = mixer.FindMatchingGroups("SFX");
                    if (sfxGroups != null && sfxGroups.Length > 0) sfxMixerGroup = sfxGroups[0];
                }
            }
            catch { }
        }

        private void OnEnable()
        {
            RegisterBossEvents();
        }

        private void OnDisable()
        {
            UnregisterBossEvents();
            StopChargeSound();
            StopMovementSound();
        }

        private void Start()
        {
            ResolveGlobalMixerGroups();

            if (playRoarOnStart && lifecycleAudio != null && lifecycleAudio.engageRoarClip != null)
            {
                PlayEngageRoar();
            }

            if (autoManageBGM)
            {
                int currentPhase = bossHealth != null ? bossHealth.CurrentPhaseIndex : 0;
                PlayPhaseBGM(currentPhase);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region Initialization & Setup

        private void InitializeAudioSources()
        {
            sfxPool.Clear();

            // 1. Kênh BGM (Nhạc nền)
            bgmSourceA = CreateAudioSource("BGM_Source_A", loop: true, spatial: 0f, bgmMixerGroup);
            bgmSourceB = CreateAudioSource("BGM_Source_B", loop: true, spatial: 0f, bgmMixerGroup);

            // 2. Kênh Voice / Gầm thét (SFX Group)
            voiceAudioSource = CreateAudioSource("Voice_Roar_Source", loop: false, spatial: spatialBlend, sfxMixerGroup);

            // 3. Kênh Gồng tụ năng lượng (SFX Group)
            chargeAudioSource = CreateAudioSource("Charge_Loop_Source", loop: true, spatial: spatialBlend, sfxMixerGroup);

            // 4. Kênh Di chuyển / Vỗ cánh (SFX Group)
            movementAudioSource = CreateAudioSource("Movement_Source", loop: true, spatial: spatialBlend, sfxMixerGroup);

            // 5. Kênh Sub-Bass siêu trầm (SFX Group)
            subBassAudioSource = CreateAudioSource("SubBass_Source", loop: false, spatial: 0f, sfxMixerGroup);

            // 6. SFX Multi-Channel Pool (SFX Group)
            for (int i = 0; i < SFX_POOL_SIZE; i++)
            {
                AudioSource sfxSrc = CreateAudioSource($"SFX_Source_{i}", loop: false, spatial: spatialBlend, sfxMixerGroup);
                sfxPool.Add(sfxSrc);
            }

            // Tùy chọn Arena Reverb Filter
            if (epicAudio != null && epicAudio.enableArenaReverb)
            {
                AudioReverbFilter reverb = GetComponent<AudioReverbFilter>();
                if (reverb == null)
                {
                    reverb = gameObject.AddComponent<AudioReverbFilter>();
                }
                if (reverb != null)
                {
                    reverb.reverbPreset = AudioReverbPreset.Cave;
                    reverb.dryLevel = 0f;
                    reverb.room = Mathf.Lerp(-10000f, 0f, epicAudio.reverbIntensity);
                }
            }
        }

        private AudioSource CreateAudioSource(string sourceName, bool loop, float spatial, AudioMixerGroup mixerGroup = null)
        {
            Transform existingChild = transform.Find(sourceName);
            GameObject child = existingChild != null ? existingChild.gameObject : new GameObject(sourceName);
            child.transform.SetParent(transform);
            child.transform.localPosition = Vector3.zero;

            AudioSource src = child.GetComponent<AudioSource>();
            if (src == null)
            {
                src = child.AddComponent<AudioSource>();
            }
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = spatial;
            src.minDistance = minSoundDistance;
            src.maxDistance = maxSoundDistance;
            src.rolloffMode = AudioRolloffMode.Linear;

            if (mixerGroup != null)
            {
                src.outputAudioMixerGroup = mixerGroup;
            }

            return src;
        }

        private void FindBossComponents()
        {
            if (bossHealth == null)
            {
                bossHealth = GetComponent<QuyVuongBossHealth>() ?? GetComponentInParent<QuyVuongBossHealth>() ?? GetComponentInChildren<QuyVuongBossHealth>();
            }

            if (bossController == null)
            {
                bossController = GetComponent<QuyVuongBossController>() ?? GetComponentInParent<QuyVuongBossController>() ?? GetComponentInChildren<QuyVuongBossController>();
            }

            if (phaseSwitcher == null)
            {
                phaseSwitcher = GetComponent<QuyVuongPhaseSwitcher>() ?? GetComponentInParent<QuyVuongPhaseSwitcher>() ?? GetComponentInChildren<QuyVuongPhaseSwitcher>();
            }

            if (epicEnhancer == null)
            {
                epicEnhancer = GetComponent<QuyVuongEpicAudioEnhancer>() ?? GetComponentInParent<QuyVuongEpicAudioEnhancer>() ?? GetComponentInChildren<QuyVuongEpicAudioEnhancer>();
            }
        }

        private void RegisterBossEvents()
        {
            if (bossHealth != null)
            {
                bossHealth.OnHealthChanged -= HandleHealthChanged;
                bossHealth.OnHealthChanged += HandleHealthChanged;

                bossHealth.OnPhaseChanged -= HandlePhaseChanged;
                bossHealth.OnPhaseChanged += HandlePhaseChanged;

                bossHealth.OnPhaseTransitioning -= HandlePhaseTransitioning;
                bossHealth.OnPhaseTransitioning += HandlePhaseTransitioning;

                bossHealth.OnDied -= HandleDied;
                bossHealth.OnDied += HandleDied;

                lastObservedPhaseIndex = bossHealth.CurrentPhaseIndex;
            }
        }

        private void UnregisterBossEvents()
        {
            if (bossHealth != null)
            {
                bossHealth.OnHealthChanged -= HandleHealthChanged;
                bossHealth.OnPhaseChanged -= HandlePhaseChanged;
                bossHealth.OnPhaseTransitioning -= HandlePhaseTransitioning;
                bossHealth.OnDied -= HandleDied;
            }
        }

        #endregion

        #region Event Handlers (Observation from BossHealth)

        private void HandleHealthChanged(int currentHealth, int maxHealth)
        {
            if (isDead) return;

            if (lifecycleAudio != null && lifecycleAudio.hurtClip != null && currentHealth > 0)
            {
                PlayHurt();
            }
        }

        private void HandlePhaseChanged(int newPhaseIndex)
        {
            if (isDead) return;

            Debug.Log($"[QuyVuongAudioController] Boss chuyển sang Phase {newPhaseIndex}!");
            lastObservedPhaseIndex = newPhaseIndex;

            if (autoManageBGM)
            {
                PlayPhaseBGM(newPhaseIndex);
            }
        }

        private void HandlePhaseTransitioning()
        {
            if (isDead) return;

            Debug.Log("[QuyVuongAudioController] Phát âm thanh chuyển Phase bộc phát nộ khí!");
            PlayPhaseTransitionSound();
        }

        private void HandleDied()
        {
            isDead = true;
            Debug.Log("[QuyVuongAudioController] Boss Quỷ Vương đã bị đánh bại! Dừng BGM Boss và khôi phục nhạc nền Scene.");

            StopChargeSound();
            StopMovementSound();
            PlayDeathSound();

            FadeOutBGM(3.0f);

            // Thông báo cho AudioManager toàn cục khôi phục lại nhạc nền Scene
            if (syncWithGlobalAudioManager && AudioManager.Instance != null)
            {
                AudioManager.Instance.StopBossMusic(restorePrevious: true, fadeDuration: 2.0f);
            }
        }

        #endregion

        #region Core Audio Playing Logic (SFX & BGM)

        public void PlaySFX(AudioClip clip, float volumeMultiplier = 1f, float customPitch = 1f)
        {
            if (clip == null || isDead) return;

            AudioSource source = GetNextAvailableSFXSource();
            if (source == null)
            {
                if (syncWithGlobalAudioManager && AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySFX(clip, transform.position, customPitch);
                }
                else
                {
                    AudioSource.PlayClipAtPoint(clip, transform.position, Mathf.Clamp01(sfxVolume * masterVolume * volumeMultiplier));
                }
                return;
            }

            float randomPitchOffset = UnityEngine.Random.Range(-pitchVariation, pitchVariation);
            source.pitch = Mathf.Clamp(customPitch + randomPitchOffset, 0.5f, 2.0f);
            source.volume = Mathf.Clamp01(sfxVolume * masterVolume * volumeMultiplier);
            source.clip = clip;
            source.Play();
        }

        public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volumeMultiplier = 1f)
        {
            if (clip == null) return;
            if (syncWithGlobalAudioManager && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(clip, position);
            }
            else
            {
                float finalVol = Mathf.Clamp01(sfxVolume * masterVolume * volumeMultiplier);
                AudioSource.PlayClipAtPoint(clip, position, finalVol);
            }
        }

        public void PlayVoice(AudioClip clip, float volumeMultiplier = 1f)
        {
            if (clip == null) return;

            if (voiceAudioSource == null)
            {
                PlaySFX(clip, volumeMultiplier);
                return;
            }

            voiceAudioSource.pitch = 1f + UnityEngine.Random.Range(-0.03f, 0.03f);
            voiceAudioSource.volume = Mathf.Clamp01(voiceVolume * masterVolume * volumeMultiplier);
            voiceAudioSource.clip = clip;
            voiceAudioSource.Play();
        }

        public void PlaySubBassImpact(float volumeMultiplier = 1f)
        {
            if (epicAudio != null && epicAudio.subBassImpactClip != null && subBassAudioSource != null)
            {
                subBassAudioSource.pitch = UnityEngine.Random.Range(0.92f, 1.05f);
                subBassAudioSource.PlayOneShot(epicAudio.subBassImpactClip, Mathf.Clamp01(masterVolume * volumeMultiplier));
            }
            else if (epicEnhancer != null)
            {
                epicEnhancer.PlayHeavySubBassImpact(volumeMultiplier);
            }
        }

        public void StartChargeSound(AudioClip clip, float volumeMultiplier = 1f)
        {
            if (clip == null || chargeAudioSource == null) return;

            if (chargeAudioSource.isPlaying && chargeAudioSource.clip == clip) return;

            chargeAudioSource.clip = clip;
            chargeAudioSource.volume = Mathf.Clamp01(sfxVolume * masterVolume * volumeMultiplier);
            chargeAudioSource.pitch = 1f;
            chargeAudioSource.Play();
        }

        public void StopChargeSound()
        {
            if (chargeAudioSource != null && chargeAudioSource.isPlaying)
            {
                chargeAudioSource.Stop();
                chargeAudioSource.clip = null;
            }
        }

        public void StartMovementSound(AudioClip clip, float volumeMultiplier = 0.8f)
        {
            if (clip == null || movementAudioSource == null) return;
            if (movementAudioSource.isPlaying && movementAudioSource.clip == clip) return;

            movementAudioSource.clip = clip;
            movementAudioSource.volume = Mathf.Clamp01(sfxVolume * masterVolume * volumeMultiplier);
            movementAudioSource.Play();
        }

        public void StopMovementSound()
        {
            if (movementAudioSource != null && movementAudioSource.isPlaying)
            {
                movementAudioSource.Stop();
                movementAudioSource.clip = null;
            }
        }

        private AudioSource GetNextAvailableSFXSource()
        {
            if (sfxPool == null || sfxPool.Count == 0) return null;

            for (int i = 0; i < sfxPool.Count; i++)
            {
                int index = (currentSfxPoolIndex + i) % sfxPool.Count;
                if (sfxPool[index] != null && !sfxPool[index].isPlaying)
                {
                    currentSfxPoolIndex = (index + 1) % sfxPool.Count;
                    return sfxPool[index];
                }
            }

            AudioSource fallback = sfxPool[currentSfxPoolIndex % sfxPool.Count];
            currentSfxPoolIndex = (currentSfxPoolIndex + 1) % sfxPool.Count;
            return fallback;
        }

        #endregion

        #region BGM Management (Crossfade & Sync With Global AudioManager)

        public void PlayPhaseBGM(int phaseIndex)
        {
            if (bgmData == null) return;

            AudioClip targetClip = null;
            switch (phaseIndex)
            {
                case 0:
                    targetClip = bgmData.phase0BGM ?? bgmData.phase1BGM;
                    break;
                case 1:
                    targetClip = bgmData.phase1BGM;
                    break;
                case 2:
                    targetClip = bgmData.phase2BGM ?? bgmData.phase1BGM;
                    break;
                case 3:
                    targetClip = bgmData.phase3BGM ?? bgmData.phase2BGM ?? bgmData.phase1BGM;
                    break;
                case 4:
                    targetClip = bgmData.phase4BGM ?? bgmData.phase3BGM;
                    break;
                default:
                    targetClip = bgmData.phase1BGM;
                    break;
            }

            if (targetClip != null)
            {
                if (syncWithGlobalAudioManager && AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayBossMusic(targetClip, bgmData.loopBGM, bgmData.bgmCrossfadeDuration);
                }
                else
                {
                    CrossfadeBGM(targetClip, bgmData.bgmCrossfadeDuration);
                }
            }
        }

        public void CrossfadeBGM(AudioClip newClip, float duration)
        {
            if (newClip == null || bgmSourceA == null || bgmSourceB == null) return;

            AudioSource activeSource = isUsingSourceA ? bgmSourceA : bgmSourceB;
            AudioSource newSource = isUsingSourceA ? bgmSourceB : bgmSourceA;

            if (activeSource.isPlaying && activeSource.clip == newClip)
            {
                return;
            }

            if (bgmCrossfadeCoroutine != null)
            {
                StopCoroutine(bgmCrossfadeCoroutine);
                bgmCrossfadeCoroutine = null;
            }

            if (gameObject.activeInHierarchy)
            {
                bgmCrossfadeCoroutine = StartCoroutine(DoCrossfadeBGM(activeSource, newSource, newClip, duration));
            }
            else
            {
                newSource.clip = newClip;
                newSource.volume = bgmVolume * masterVolume;
                newSource.loop = bgmData != null ? bgmData.loopBGM : true;
                newSource.Play();
                if (activeSource.isPlaying)
                {
                    activeSource.Stop();
                    activeSource.clip = null;
                }
            }
            isUsingSourceA = !isUsingSourceA;
        }

        private IEnumerator DoCrossfadeBGM(AudioSource fadeOutSource, AudioSource fadeInSource, AudioClip newClip, float duration)
        {
            float targetVolume = bgmVolume * masterVolume;
            fadeInSource.clip = newClip;
            fadeInSource.volume = 0f;
            fadeInSource.loop = bgmData != null ? bgmData.loopBGM : true;
            fadeInSource.Play();

            float elapsed = 0f;
            float startFadeOutVol = fadeOutSource.isPlaying ? fadeOutSource.volume : 0f;

            if (duration <= 0f) duration = 0.1f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                fadeInSource.volume = Mathf.Lerp(0f, targetVolume, t);
                if (fadeOutSource.isPlaying)
                {
                    fadeOutSource.volume = Mathf.Lerp(startFadeOutVol, 0f, t);
                }

                yield return null;
            }

            fadeInSource.volume = targetVolume;
            if (fadeOutSource.isPlaying)
            {
                fadeOutSource.Stop();
                fadeOutSource.clip = null;
            }
        }

        public void FadeOutBGM(float duration)
        {
            AudioSource activeSource = isUsingSourceA ? bgmSourceA : bgmSourceB;
            if (activeSource != null && activeSource.isPlaying)
            {
                if (gameObject.activeInHierarchy)
                {
                    StartCoroutine(DoFadeOutSource(activeSource, duration));
                }
                else
                {
                    activeSource.volume = 0f;
                    activeSource.Stop();
                    activeSource.clip = null;
                }
            }
        }

        private IEnumerator DoFadeOutSource(AudioSource source, float duration)
        {
            if (source == null) yield break;
            float startVol = source.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (source == null) yield break;
                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
                yield return null;
            }
            if (source != null)
            {
                source.Stop();
                source.clip = null;
            }
        }

        #endregion

        #region Public API: Lifecycle SFX (Layered Final Boss)

        public void PlayEngageRoar()
        {
            if (lifecycleAudio == null) return;
            PlayVoice(lifecycleAudio.engageRoarClip, 1.3f);
            if (lifecycleAudio.engageStingerClip != null)
            {
                PlaySFX(lifecycleAudio.engageStingerClip, 1.2f);
            }
            PlaySubBassImpact(0.8f);
        }

        public void PlayHurt()
        {
            if (lifecycleAudio == null) return;
            PlaySFX(lifecycleAudio.hurtClip, 0.85f);
        }

        public void PlayParriedStagger()
        {
            if (lifecycleAudio == null) return;
            PlaySFX(lifecycleAudio.parriedStaggerClip, 1.2f);
            PlaySubBassImpact(0.6f);
        }

        public void PlayPhaseTransitionSound()
        {
            if (lifecycleAudio == null) return;
            PlayVoice(lifecycleAudio.phaseTransitionClip, 1.4f);
            if (lifecycleAudio.phaseTransitionShockwaveClip != null)
            {
                PlaySFX(lifecycleAudio.phaseTransitionShockwaveClip, 1.3f);
            }
            PlaySubBassImpact(1.2f);
            StopChargeSound();
        }

        public void PlayDeathSound()
        {
            if (lifecycleAudio == null) return;
            PlayVoice(lifecycleAudio.deathClip, 1.5f);
            if (lifecycleAudio.deathExplosionClip != null)
            {
                PlaySFX(lifecycleAudio.deathExplosionClip, 1.4f);
            }
            PlaySubBassImpact(1.4f);
        }

        public void PlayFootstep()
        {
            if (lifecycleAudio == null) return;
            PlaySFX(lifecycleAudio.footstepClip, 0.7f);
        }

        public void PlayWingFlap()
        {
            if (lifecycleAudio == null) return;
            PlaySFX(lifecycleAudio.wingFlapClip, 0.8f);
        }

        #endregion

        #region Public API: Phase 0 Skills (MamaTick / Thủy Tinh Cổ Đại)

        public void PlayPhase0WaterRainTelegraph() => PlaySFX(phase0Audio?.waterRainTelegraphClip);
        public void PlayPhase0WaterRainShoot() => PlaySFX(phase0Audio?.waterRainShootClip, 1.2f);
        public void PlayPhase0WaterRainImpact()
        {
            PlaySFX(phase0Audio?.waterRainImpactClip, 1.2f);
            PlaySubBassImpact(0.5f);
        }

        public void PlayPhase0WaterBeamCharge() => StartChargeSound(phase0Audio?.waterBeamChargeClip);
        public void PlayPhase0WaterBeamBurst()
        {
            StopChargeSound();
            PlaySFX(phase0Audio?.waterBeamBurstClip, 1.4f);
            PlaySubBassImpact(0.8f);
        }

        public void PlayPhase0HorizontalWaterArrowFire() => PlaySFX(phase0Audio?.horizontalWaterArrowFireClip, 1.2f);
        public void PlayPhase0HorizontalWaterArrowImpact()
        {
            PlaySFX(phase0Audio?.horizontalWaterArrowImpactClip, 1.2f);
            PlaySubBassImpact(0.5f);
        }

        public void PlayPhase0SkyTeleport() => PlaySFX(phase0Audio?.skyTeleportClip, 1.1f);
        public void PlayPhase0ArenaLanding()
        {
            PlaySFX(phase0Audio?.arenaLandingClip, 1.2f);
            PlaySubBassImpact(0.6f);
        }

        #endregion

        #region Public API: Phase 1 Skills (Cung Thủ Nước)

        public void PlayRainArrowTelegraph() => PlaySFX(phase1Audio?.rainArrowTelegraphClip);
        public void PlayRainArrowShoot() => PlaySFX(phase1Audio?.rainArrowShootClip, 1.2f);
        public void PlayRainArrowFallingLoop() => PlaySFX(phase1Audio?.rainArrowFallingLoopClip, 0.9f);
        public void PlayRainArrowImpact()
        {
            PlaySFX(phase1Audio?.rainArrowImpactClip, 1.3f);
            PlaySubBassImpact(0.5f);
        }

        public void PlayHorizontalArrowCharge() => StartChargeSound(phase1Audio?.horizontalArrowChargeClip);
        public void PlayHorizontalArrowFire()
        {
            StopChargeSound();
            PlaySFX(phase1Audio?.horizontalArrowFireClip, 1.2f);
        }
        public void PlayHorizontalArrowImpact()
        {
            PlaySFX(phase1Audio?.horizontalArrowImpactClip, 1.3f);
            PlaySubBassImpact(0.6f);
        }

        public void PlayGrappleBowCast() => PlaySFX(phase1Audio?.grappleBowCastClip);
        public void PlayGrappleArrowShoot() => PlaySFX(phase1Audio?.grappleArrowShootClip, 1.2f);
        public void PlayGrapplePull() => PlaySFX(phase1Audio?.grapplePullClip, 1.1f);
        public void PlayCageTrapSpawn()
        {
            PlaySFX(phase1Audio?.cageTrapSpawnClip, 1.2f);
            PlaySubBassImpact(0.6f);
        }

        public void PlayRamWindup() => StartChargeSound(phase1Audio?.ramWindupClip, 1.1f);
        public void PlayRamDashRush()
        {
            StopChargeSound();
            PlaySFX(phase1Audio?.ramDashRushClip, 1.3f);
        }
        public void PlayRamImpact()
        {
            PlaySFX(phase1Audio?.ramImpactClip, 1.5f);
            PlaySubBassImpact(1.1f);
        }

        public void PlayEnemySpawnChant() => PlaySFX(phase1Audio?.enemySpawnChantClip);
        public void PlayEnemySpawnBurst()
        {
            PlaySFX(phase1Audio?.enemySpawnBurstClip, 1.2f);
            PlaySubBassImpact(0.7f);
        }

        public void PlayFlyTakeOff()
        {
            PlaySFX(phase1Audio?.flyTakeOffClip, 1.1f);
            if (phase1Audio != null && phase1Audio.flyTrailClip != null)
            {
                StartMovementSound(phase1Audio.flyTrailClip, 0.8f);
            }
        }
        public void PlayFlyLanding()
        {
            StopMovementSound();
            PlaySFX(phase1Audio?.flyLandingClip, 1.2f);
            PlaySubBassImpact(0.7f);
        }

        #endregion

        #region Public API: Phase 2 Skills (Tử Thần Liềm)

        public void PlayScytheWindup() => StartChargeSound(phase2Audio?.scytheWindupClip);
        public void PlayScytheHeavySwing()
        {
            StopChargeSound();
            PlaySFX(phase2Audio?.scytheHeavySwingClip, 1.4f);
            if (phase2Audio != null && phase2Audio.scytheWaveSweepClip != null)
            {
                PlaySFX(phase2Audio.scytheWaveSweepClip, 1.2f);
            }
            PlaySubBassImpact(0.9f);
        }
        public void PlayScytheWaveSweep() => PlaySFX(phase2Audio?.scytheWaveSweepClip, 1.2f);
        public void PlayScytheHit()
        {
            PlaySFX(phase2Audio?.scytheHitClip, 1.4f);
            PlaySubBassImpact(0.8f);
        }

        public void PlayHomingSummonCharge() => StartChargeSound(phase2Audio?.homingSummonChargeClip);
        public void PlayHomingMultiFire()
        {
            StopChargeSound();
            PlaySFX(phase2Audio?.homingMultiFireClip, 1.3f);
        }
        public void PlayHomingFlightLoop() => PlaySFX(phase2Audio?.homingFlightLoopClip, 0.85f);
        public void PlayHomingExplosion()
        {
            PlaySFX(phase2Audio?.homingExplosionClip, 1.3f);
            PlaySubBassImpact(0.6f);
        }

        public void PlayRageRoar()
        {
            PlayVoice(phase2Audio?.rageRoarClip, 1.4f);
            PlaySubBassImpact(1.1f);
        }
        public void PlayRelocateTeleport() => PlaySFX(phase2Audio?.relocateTeleportClip, 1.2f);

        #endregion

        #region Public API: Phase 3 Skills (Địa Chấn Thiên Thạch)

        public void PlayEarthStomp()
        {
            PlaySFX(phase3Audio?.earthStompClip, 1.4f);
            PlaySubBassImpact(0.9f);
        }
        public void PlayGroundLineCrackRumble() => PlaySFX(phase3Audio?.groundLineCrackRumbleClip, 1.3f);
        public void PlayEarthEruptionDusthorn()
        {
            PlaySFX(phase3Audio?.earthEruptionDusthornClip, 1.4f);
            PlaySubBassImpact(1.1f);
        }

        public void PlaySkyLeapTakeoff()
        {
            PlaySFX(phase3Audio?.skyLeapTakeoffClip, 1.3f);
            PlaySubBassImpact(0.8f);
        }
        public void PlaySkyLeapDescentSwoosh() => PlaySFX(phase3Audio?.skyLeapDescentSwooshClip, 1.4f);
        public void PlaySkyLeapHeavyMeteorImpact()
        {
            PlaySFX(phase3Audio?.skyLeapHeavyMeteorImpactClip, 1.7f);
            if (phase3Audio != null && phase3Audio.shockwaveRumbleClip != null)
            {
                PlaySFX(phase3Audio.shockwaveRumbleClip, 1.3f);
            }
            PlaySubBassImpact(1.5f);
        }
        public void PlayShockwaveRumble() => PlaySFX(phase3Audio?.shockwaveRumbleClip, 1.3f);

        #endregion

        #region Public API: Phase 4 Skills (Hỏa Nham Ma - Magma Golem)

        public void PlayPhase4SkullTransform() => PlayVoice(phase4Audio?.skullTransformClip, 1.3f);
        public void PlayPhase4SkullDashRush() => PlaySFX(phase4Audio?.skullDashRushClip, 1.3f);
        public void PlayPhase4SkullDashImpact()
        {
            PlaySFX(phase4Audio?.skullDashImpactClip, 1.5f);
            PlaySubBassImpact(1.2f);
        }

        public void PlayPhase4EruptionUppercut()
        {
            PlaySFX(phase4Audio?.eruptionUppercutClip, 1.4f);
            PlaySubBassImpact(1.0f);
        }

        public void PlayPhase4FireTornadoVortex() => PlaySFX(phase4Audio?.fireTornadoVortexClip, 1.3f);

        public void PlayPhase4ObsidianSummonBombs()
        {
            StartChargeSound(phase4Audio?.obsidianSummonBombsClip);
        }

        public void PlayPhase4BombHomingLaunch()
        {
            StopChargeSound();
            PlaySFX(phase4Audio?.bombHomingLaunchClip, 1.2f);
        }

        public void PlayPhase4BombExplosion()
        {
            PlaySFX(phase4Audio?.bombExplosionClip, 1.5f);
            PlaySubBassImpact(1.3f);
        }

        public void PlayPhase4ShrapnelBurst() => PlaySFX(phase4Audio?.shrapnelBurstClip, 1.1f);

        public void PlayPhase4VolcanicArtilleryFire() => PlaySFX(phase4Audio?.volcanicArtilleryFireClip, 1.4f);
        public void PlayPhase4VolcanicArtilleryImpact()
        {
            PlaySFX(phase4Audio?.volcanicArtilleryImpactClip, 1.5f);
            PlaySubBassImpact(1.2f);
        }

        #endregion
    }
}
