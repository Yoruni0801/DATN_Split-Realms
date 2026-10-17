using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Cinemachine;

[DisallowMultipleComponent]
public class BeamReceptaclePuzzle : MonoBehaviour, IBeamReceiver
{
    [Header("Charge Settings")]
    [SerializeField] private float requiredChargeTime = 3f;
    [SerializeField] private float dischargeRate = 0.5f;

    [Header("Puzzle Elements")]
    [SerializeField] private List<Transform> pillars = new();
    [SerializeField] private float pillarInitialYOffset = -2.5f;
    [SerializeField] private float pillarRiseHeight = 3f;

    [Header("Pillar Animation Settings")]
    [SerializeField] private float pillarRiseDuration = 1.5f;
    [SerializeField] private float delayBetweenPillars = 0.5f;
    [SerializeField] private AnimationCurve riseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Audio Feedback")]
    [SerializeField] private AudioSource puzzleAudioSource;
    [SerializeField] private AudioSource rumbleAudioSource;
    [Tooltip("Âm thanh hoành tráng khi sạc đủ và giải xong minigame")]
    [SerializeField] private AudioClip puzzleSolvedStingerClip;
    [Range(0f, 1f)][SerializeField] private float puzzleSolvedVolume = 0.85f;

    [Tooltip("Tiếng cơ quan đá trồi lên và rung chuyển mặt đất rền vang")]
    [SerializeField] private AudioClip pillarRiseRumbleClip;
    [Range(0f, 1f)][SerializeField] private float pillarRiseVolume = 0.75f;

    [Tooltip("Tiếng trụ đá khóa vào vị trí cạch/rầm vững chãi khi nâng xong mỗi trụ")]
    [SerializeField] private AudioClip pillarLockImpactClip;
    [Range(0f, 1f)][SerializeField] private float pillarLockVolume = 0.7f;

    [Header("Camera Feedback")]
    [SerializeField] private float cameraShakeMagnitude = 0.2f;

    [Header("Camera Pan (Focus Puzzle)")]
    [SerializeField] private Transform puzzleCameraTarget;
    [SerializeField] private float puzzleZoomSize = 10f;
    [SerializeField] private float cameraPanDuration = 1f;
    [SerializeField] private float cameraHoldDuration = 1.5f;

    [Header("External References")]
    [SerializeField] private FairyPossessionController fairyPossessionController;
    [SerializeField] private CinemachineCamera activeCinemachineCamera;
    [SerializeField] private RedirectableBeamLauncher beamLauncher;

    [Header("Sprite Feedback")]
    [SerializeField] private SpriteRenderer targetSpriteRenderer;
    [Range(0f, 1f)][SerializeField] private float minAlpha = 0f;
    [Range(0f, 1f)][SerializeField] private float maxAlpha = 1f;

    [Header("Debug View")]
    [SerializeField] private float currentCharge = 0f;
    [SerializeField] private bool isFullyCharged = false;
    [SerializeField] private bool isPuzzleSolved = false;

    private float lastHitTime;
    private readonly List<Vector3> pillarBasePositions = new();
    private CancellationTokenSource destroyCts;

    private void Awake()
    {
        destroyCts = new CancellationTokenSource();

        if (targetSpriteRenderer == null)
            targetSpriteRenderer = GetComponent<SpriteRenderer>();

        if (fairyPossessionController == null)
            fairyPossessionController = FindFirstObjectByType<FairyPossessionController>();

        if (activeCinemachineCamera == null)
            activeCinemachineCamera = FindFirstObjectByType<CinemachineCamera>();

        if (beamLauncher == null)
            beamLauncher = FindFirstObjectByType<RedirectableBeamLauncher>();

        SetupAudioSources();
#if UNITY_EDITOR
        AutoAssignAudioClips();
#endif

        UpdateVisualFeedback(0f);

        pillarBasePositions.Clear();
        for (int i = 0; i < pillars.Count; i++)
        {
            Vector3 basePos = pillars[i] != null ? pillars[i].position : Vector3.zero;
            pillarBasePositions.Add(basePos);

            if (pillars[i] != null && !isPuzzleSolved)
            {
                pillars[i].position = basePos + new Vector3(0f, pillarInitialYOffset, 0f);
            }
        }
    }

    private void SetupAudioSources()
    {
        if (puzzleAudioSource == null)
        {
            puzzleAudioSource = gameObject.AddComponent<AudioSource>();
            puzzleAudioSource.playOnAwake = false;
            puzzleAudioSource.spatialBlend = 0.3f;
        }

        if (rumbleAudioSource == null)
        {
            rumbleAudioSource = gameObject.AddComponent<AudioSource>();
            rumbleAudioSource.playOnAwake = false;
            rumbleAudioSource.loop = true;
            rumbleAudioSource.spatialBlend = 0.3f;
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        AutoAssignAudioClips();
    }

    private void OnValidate()
    {
        AutoAssignAudioClips();
    }

    private void AutoAssignAudioClips()
    {
        if (puzzleSolvedStingerClip == null)
        {
            puzzleSolvedStingerClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Duy/Audio/Puzzle/puzzle_solved_stinger.wav");
        }
        if (pillarRiseRumbleClip == null)
        {
            pillarRiseRumbleClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Duy/Audio/Puzzle/puzzle_pillar_rise_rumble.wav");
        }
        if (pillarLockImpactClip == null)
        {
            pillarLockImpactClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Duy/Audio/Puzzle/puzzle_pillar_lock_impact.wav");
        }
    }
#endif

    private void OnDestroy()
    {
        StopRumbleSound();
        destroyCts?.Cancel();
        destroyCts?.Dispose();
        destroyCts = null;
    }

    private void Update()
    {
        if (isFullyCharged || isPuzzleSolved) return;

        if (Time.time - lastHitTime > 0.1f && dischargeRate > 0f && currentCharge > 0f)
        {
            currentCharge = Mathf.Max(0f, currentCharge - dischargeRate * Time.deltaTime);
            UpdateVisualFeedback(currentCharge / requiredChargeTime);
        }
    }

    public void ReceiveBeam(Vector3 hitPoint)
    {
        if (isFullyCharged || isPuzzleSolved) return;

        lastHitTime = Time.time;
        currentCharge = Mathf.Clamp(currentCharge + Time.deltaTime, 0f, requiredChargeTime);

        float progress = currentCharge / requiredChargeTime;
        UpdateVisualFeedback(progress);

        if (currentCharge >= requiredChargeTime)
        {
            isFullyCharged = true;
            isPuzzleSolved = true;
            if (beamLauncher != null)
            {
                beamLauncher.FireBeam(false);
            }

            // 1. Phát âm thanh chiến thắng / giải đố thành công
            PlaySound(puzzleSolvedStingerClip, puzzleSolvedVolume);

            // 2. Tắt hoàn toàn UI Popup hướng dẫn và vô hiệu hóa trigger vĩnh viễn
            FairyMinigameTutorialController.OnMinigameCompleted();

            FairyPossessionController.LockSwap();
            _ = RisePillarsAsync(destroyCts.Token);
        }
    }

    private void UpdateVisualFeedback(float progress)
    {
        if (targetSpriteRenderer == null) return;

        Color c = targetSpriteRenderer.color;
        c.a = Mathf.Lerp(minAlpha, maxAlpha, Mathf.Clamp01(progress));
        targetSpriteRenderer.color = c;
    }

    private async Task RisePillarsAsync(CancellationToken ct)
    {
        try
        {
            if (puzzleCameraTarget != null && fairyPossessionController != null)
            {
                fairyPossessionController.PanCameraTo(puzzleCameraTarget, puzzleZoomSize, cameraPanDuration);
                await DelaySecondsAsync(cameraPanDuration, ct);
                if (this == null || ct.IsCancellationRequested) return;
            }

            float totalRiseTime = 0f;
            for (int i = 0; i < pillars.Count; i++)
            {
                if (pillars[i] != null && pillarBasePositions[i] != Vector3.zero)
                {
                    totalRiseTime += pillarRiseDuration + (i < pillars.Count - 1 ? delayBetweenPillars : 0f);
                }
            }
            if (totalRiseTime <= 0f) totalRiseTime = 1f;

            TryShakeCamera(totalRiseTime, cameraShakeMagnitude, ct);

            // 2. Bắt đầu phát âm thanh cơ quan đá trồi lên và rung chuyển mặt đất
            StartRumbleSound();

            for (int i = 0; i < pillars.Count; i++)
            {
                if (pillars[i] == null || pillarBasePositions[i] == Vector3.zero)
                    continue;

                Vector3 startPos = pillars[i].position;
                Vector3 targetPos = startPos + new Vector3(0f, pillarRiseHeight, 0f);

                float elapsed = 0f;
                while (elapsed < pillarRiseDuration)
                {
                    if (this == null || ct.IsCancellationRequested) return;

                    elapsed += Time.deltaTime;
                    float t = riseCurve.Evaluate(Mathf.Clamp01(elapsed / pillarRiseDuration));
                    if (pillars[i] != null)
                    {
                        pillars[i].position = Vector3.Lerp(startPos, targetPos, t);
                    }

                    await Task.Yield();
                }

                if (pillars[i] != null)
                {
                    pillars[i].position = targetPos;
                    // 3. Phát âm thanh trụ đá khóa vào vị trí
                    PlaySound(pillarLockImpactClip, pillarLockVolume);
                }

                if (i < pillars.Count - 1 && delayBetweenPillars > 0f)
                {
                    await DelaySecondsAsync(delayBetweenPillars, ct);
                    if (this == null || ct.IsCancellationRequested) return;
                }
            }

            // Dừng âm thanh rung chuyển sau khi nâng xong tất cả trụ
            StopRumbleSound();

            if (cameraHoldDuration > 0f)
            {
                await DelaySecondsAsync(cameraHoldDuration, ct);
                if (this == null || ct.IsCancellationRequested) return;
            }

            if (fairyPossessionController != null)
            {
                fairyPossessionController.ReturnToPlayer();
            }

            var fairies = FairyPlayerCompanionController.AllActiveFairies;
            for (int i = 0; i < fairies.Count; i++)
            {
                if (fairies[i] != null)
                {
                    fairies[i].TeleportToPlayer();
                }
            }

            FairyPossessionController.UnlockSwap();
            FairyMinigameTutorialController.OnMinigameCompleted();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
        }
        finally
        {
            StopRumbleSound();
        }
    }

    private void PlaySound(AudioClip clip, float volume)
    {
        if (clip == null) return;
        if (puzzleAudioSource == null) SetupAudioSources();

        if (puzzleAudioSource != null)
        {
            puzzleAudioSource.PlayOneShot(clip, volume);
        }
    }

    private void StartRumbleSound()
    {
        if (pillarRiseRumbleClip == null) return;
        if (rumbleAudioSource == null) SetupAudioSources();

        if (rumbleAudioSource != null)
        {
            rumbleAudioSource.clip = pillarRiseRumbleClip;
            rumbleAudioSource.volume = pillarRiseVolume;
            rumbleAudioSource.loop = true;
            rumbleAudioSource.Play();
        }
    }

    private void StopRumbleSound()
    {
        if (rumbleAudioSource != null && rumbleAudioSource.isPlaying)
        {
            rumbleAudioSource.Stop();
        }
    }

    private void TryShakeCamera(float duration, float magnitude, CancellationToken ct)
    {
        if (fairyPossessionController != null)
        {
            fairyPossessionController.ShakeActiveCamera(duration, magnitude);
            return;
        }

        if (activeCinemachineCamera != null)
        {
            if (!activeCinemachineCamera.TryGetComponent(out CinemachineTimelineShakeExtension shake))
            {
                shake = activeCinemachineCamera.gameObject.AddComponent<CinemachineTimelineShakeExtension>();
            }
            _ = ShakeCameraAsync(shake, duration, magnitude, ct);
        }
    }

    private async Task ShakeCameraAsync(CinemachineTimelineShakeExtension shake, float duration, float magnitude, CancellationToken ct)
    {
        if (shake == null) return;

        try
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (this == null || ct.IsCancellationRequested) break;

                elapsed += Time.deltaTime;
                float t = 1f - Mathf.Clamp01(elapsed / duration);
                if (shake != null)
                {
                    shake.SetShake(magnitude * t, magnitude * 5f * t, 25f);
                }

                await Task.Yield();
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (shake != null)
            {
                shake.SetShake(0f, 0f, 0f);
            }
        }
    }

    private static async Task DelaySecondsAsync(float seconds, CancellationToken ct)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            ct.ThrowIfCancellationRequested();
            elapsed += Time.deltaTime;
            await Task.Yield();
        }
    }
}
