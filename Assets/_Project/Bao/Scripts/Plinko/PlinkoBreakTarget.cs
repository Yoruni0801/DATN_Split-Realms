using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PlinkoBreakTarget : MonoBehaviour
{
    [Header("Targets to Destroy")]
    public Transform wallMain;
    public GameObject xoay1;
    public GameObject xoay2;

    [Header("Rock Settings")]
    public bool destroyRock = true;
    public string rockTag = "IceRock";

    [Header("Button Animation")]
    public Animator buttonAnimator;
    public string buttonAnimationName = "button";
    public float iceGroundBreakDelay = 1f;

    [Header("Audio Settings (Âm Thanh Chạm Nút & Băng Nổ)")]
    public bool enableAudio = true;

    [Tooltip("Âm thanh phát khi quả cầu tuyết chạm vào nút bấm")]
    public AudioClip buttonHitSound;
    [Range(0f, 1f)] public float buttonAudioVolume = 1f;

    [Header("Chain Ice Explosions Audio (Chuỗi 7-8 Vụ Nổ Băng)")]
    [Tooltip("Kích hoạt chuỗi 7-8 vụ nổ băng liên hoàn")]
    public bool enableChainIceExplosionAudio = true;
    [Tooltip("Số vụ nổ băng liên tiếp (mặc định 8 lần theo yêu cầu)")]
    [Range(4, 12)] public int explosionCount = 8;
    [Tooltip("Khoảng cách tối thiểu giữa các tiếng nổ (giây)")]
    public float explosionIntervalMin = 0.08f;
    [Tooltip("Khoảng cách tối đa giữa các tiếng nổ (giây)")]
    public float explosionIntervalMax = 0.16f;
    [Tooltip("Danh sách các âm thanh nổ/vỡ băng phong phú (nếu để trống script sẽ tự động tìm các clip có sẵn)")]
    public AudioClip[] iceExplosionClips;
    [Range(0f, 1f)] public float explosionVolume = 1f;
    [Tooltip("Biến thiên cao độ ngẫu nhiên giữa các tiếng nổ để âm thanh chân thực")]
    public bool randomizePitch = true;
    public float minPitch = 0.85f;
    public float maxPitch = 1.25f;
    [Tooltip("Rung camera nhẹ theo từng tiếng nổ")]
    public bool triggerShakeOnEachExplosion = true;

    [Header("Camera Cutaway")]
    public CinemachineCamera cinemachineCamera;
    public Transform cameraAwayTarget;
    public Transform cameraReturnTarget;
    public bool autoFindMissingReferences = true;
    public string playerTag = "Player";
    public string cameraAwayTargetName = "GameShow";
    public bool smoothCameraTransition = true;
    public float cameraBlendDuration = 0.75f;
    public float returnToPlayerDelay = 2f;
    public float returnBlendDuration = 0.5f;
    public bool restoreCameraFollowOnDisable = false;

    [Header("Camera Shake Settings")]
    public bool enableCameraShake = true;
    [Min(0f)] public float buttonHitShakeDuration = 0.4f;
    [Min(0f)] public float buttonHitShakeMagnitude = 0.15f;

    [Header("Break Effect")]
    public bool spawnBreakEffect = true;
    [Tooltip("Prefab Particle System/VFX tùy chỉnh cho hiệu ứng vỡ chính (nút va chạm)")]
    public GameObject customBreakParticlePrefab;
    public int shardCount = 18;
    public float effectLifetime = 0.75f;
    public Color shardColor = new Color(0.75f, 0.95f, 1f, 1f);

    [Header("Ice Ground Break")]
    public bool breakIceGroundObjects = true;
    public string iceGroundTag = "IceGround";
    public string alternateIceGroundTag = "iceground";
    public bool disableIceGroundColliders = true;
    public bool hideIceGroundRenderers = true;
    public bool destroyIceGroundObjects = true;
    public float destroyIceGroundDelay = 0.05f;
    public bool spawnIceGroundBreakEffect = true;
    [Tooltip("Prefab Particle System/VFX tùy chỉnh cho hiệu ứng vỡ IceGround")]
    public GameObject customIceGroundBreakParticlePrefab;
    public int iceGroundShardCount = 14;
    public float iceGroundEffectLifetime = 0.85f;
    public Color iceGroundShardColor = new Color(0.65f, 0.95f, 1f, 1f);

    [Header("Tutorial Popup Integration")]
    [Tooltip("Tự động tắt popup hướng dẫn khi hoàn thành minigame (đá chạm đích)")]
    public bool autoCloseTutorialPopup = true;
    [Tooltip("Kéo trực tiếp TutorialPopupUI vào đây nếu muốn chỉ định rõ (để trống script sẽ tự tìm)")]
    public TutorialPopupUI tutorialPopupUI;
    [Tooltip("Kéo CameraCutawayTrigger2D vào đây nếu muốn chỉ định rõ")]
    public CameraCutawayTrigger2D cutawayTrigger;
    [Tooltip("Kéo GameObject Panel Popup vào đây (tùy chọn)")]
    public GameObject tutorialPopupPanel;

    public static event System.Action OnPlinkoCompleted;
    public static bool GlobalHasBroken => globalHasBroken;
    public bool HasBroken => hasBroken || globalHasBroken;

    private static bool globalHasBroken;
    private bool hasBroken;
    private Transform previousCameraFollow;
    private bool previousCameraFollowCached;
    private GameObject cameraBlendProxyObject;
    private Transform cameraBlendProxy;

    void Awake()
    {
        globalHasBroken = false;
        GetComponent<Collider2D>().isTrigger = true;

        if (buttonAnimator == null)
            buttonAnimator = GetComponent<Animator>();

        ResolveCameraReferences();
        ResolveTutorialPopupReferences();
        ResolveAudioClips();
    }

    private void ResolveAudioClips()
    {
        // Tự động tìm âm thanh bấm nút nếu chưa gán
        if (buttonHitSound == null)
        {
            #if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("click-button t:AudioClip");
            if (guids.Length == 0) guids = UnityEditor.AssetDatabase.FindAssets("btn_down_01 t:AudioClip");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                buttonHitSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            #endif
        }

        // Tự động tìm âm thanh nổ băng nếu danh sách trống
        if (iceExplosionClips == null || iceExplosionClips.Length == 0)
        {
            #if UNITY_EDITOR
            List<AudioClip> foundClips = new List<AudioClip>();
            string[] patterns = new string[] {
                "sfx_dreadworm_sanctuary_shield_break",
                "sfx_dreadworm_icicle_impact",
                "frostimpact",
                "frostimpact02",
                "sfx_dreadworm_slam_impact",
                "toxicDetonationClip",
                "genericimpact",
                "arcaneimpact"
            };

            foreach (var p in patterns)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets($"{p} t:AudioClip");
                if (guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    AudioClip clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip != null && !foundClips.Contains(clip))
                    {
                        foundClips.Add(clip);
                    }
                }
            }

            if (foundClips.Count > 0)
            {
                iceExplosionClips = foundClips.ToArray();
            }
            #endif
        }
    }

    private void ResolveTutorialPopupReferences()
    {
        if (tutorialPopupUI == null && autoFindMissingReferences)
        {
            tutorialPopupUI = FindAnyObjectByType<TutorialPopupUI>(FindObjectsInactive.Include);
        }

        if (cutawayTrigger == null && autoFindMissingReferences)
        {
            cutawayTrigger = FindAnyObjectByType<CameraCutawayTrigger2D>(FindObjectsInactive.Include);
        }

        if (tutorialPopupPanel == null && tutorialPopupUI != null)
        {
            tutorialPopupPanel = tutorialPopupUI.gameObject;
        }
    }

    public void CloseTutorialPopups()
    {
        OnPlinkoCompleted?.Invoke();

        if (tutorialPopupUI != null)
        {
            tutorialPopupUI.Close();
        }

        if (cutawayTrigger != null)
        {
            cutawayTrigger.DismissPopup();
        }

        if (tutorialPopupPanel != null)
        {
            tutorialPopupPanel.SetActive(false);
        }

        TutorialPopupUI.CloseAllActivePopups();
        CameraCutawayTrigger2D.CloseAllActivePopups();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (globalHasBroken || hasBroken) return;

        GameObject rock = ResolveRock(other);
        if (rock == null) return;

        globalHasBroken = true;
        hasBroken = true;

        // 1. Phát âm thanh khi quả cầu tuyết chạm nút
        PlayButtonHitAudio();

        if (autoCloseTutorialPopup)
        {
            CloseTutorialPopups();
        }

        StartCoroutine(BreakSequence(rock));
    }

    private void OnDisable()
    {
        if (cinemachineCamera != null
            && restoreCameraFollowOnDisable
            && previousCameraFollowCached)
        {
            cinemachineCamera.Follow = previousCameraFollow;
        }

        DestroyCameraBlendProxy();
    }

    private IEnumerator BreakSequence(GameObject rock)
    {
        if (autoCloseTutorialPopup)
        {
            CloseTutorialPopups();
        }

        PlayButtonAnimation();
        yield return MoveCameraAwayFromPlayer();

        BreakEverything();

        if (destroyRock && rock != null)
            Destroy(rock);

        float delay = Mathf.Max(0f, iceGroundBreakDelay);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        // 2. Kích hoạt chuỗi 7-8 vụ nổ băng liên hoàn
        if (enableChainIceExplosionAudio)
        {
            yield return StartCoroutine(ChainIceExplosionsRoutine());
        }
        else
        {
            TriggerCameraShake();
            if (spawnBreakEffect)
                SpawnBreakEffect(transform.position, shardCount, effectLifetime, shardColor, customBreakParticlePrefab);
        }

        if (breakIceGroundObjects)
            BreakIceGroundObjects();

        if (autoCloseTutorialPopup)
        {
            CloseTutorialPopups();
        }

        if (returnToPlayerDelay > 0f)
            yield return new WaitForSeconds(returnToPlayerDelay);

        yield return ReturnCameraToPlayer();
    }

    /// <summary>
    /// Chuỗi nổ liên hoàn gồm khoảng 7 đến 8 vụ nổ băng liên tiếp dồn dập
    /// </summary>
    private IEnumerator ChainIceExplosionsRoutine()
    {
        List<GameObject> iceGrounds = FindIceGroundObjects();
        List<Vector3> explosionPositions = new List<Vector3>();

        explosionPositions.Add(transform.position);
        if (wallMain != null) explosionPositions.Add(wallMain.position);
        if (xoay1 != null) explosionPositions.Add(xoay1.transform.position);
        if (xoay2 != null) explosionPositions.Add(xoay2.transform.position);

        foreach (var ig in iceGrounds)
        {
            if (ig != null) explosionPositions.Add(ig.transform.position);
        }

        int count = Mathf.Max(4, explosionCount);

        for (int i = 0; i < count; i++)
        {
            // Chọn vị trí nổ
            Vector3 burstPos = transform.position;
            if (explosionPositions.Count > 0)
            {
                burstPos = explosionPositions[i % explosionPositions.Count];
                burstPos += (Vector3)(Random.insideUnitCircle * Random.Range(0.5f, 2.5f));
            }

            // Phát âm thanh vụ nổ thứ i
            PlaySingleIceExplosionAudio(burstPos, i, count);

            // Rung camera
            if (triggerShakeOnEachExplosion)
            {
                float shakeMagnitude = Mathf.Lerp(0.08f, 0.22f, (float)(i + 1) / count);
                TriggerCustomCameraShake(0.12f, shakeMagnitude);
            }

            // Sinh hiệu ứng vỡ hạt băng tại vị trí nổ
            if (spawnBreakEffect)
            {
                SpawnBreakEffect(burstPos, shardCount / 2 + 6, effectLifetime, shardColor, customBreakParticlePrefab);
            }

            // Chờ trước vụ nổ tiếp theo
            float interval = Random.Range(explosionIntervalMin, explosionIntervalMax);
            yield return new WaitForSeconds(interval);
        }
    }

    private void PlayButtonHitAudio()
    {
        if (!enableAudio) return;

        if (buttonHitSound != null)
        {
            PlayClipAtPoint(buttonHitSound, transform.position, buttonAudioVolume, 1.0f);
        }
        else if (AudioManager.Instance != null)
        {
            AudioManager.PlaySFXIfAvailable(buttonHitSound, transform.position);
        }
    }

    private void PlaySingleIceExplosionAudio(Vector3 position, int index, int total)
    {
        if (!enableAudio) return;

        AudioClip clipToPlay = null;

        if (iceExplosionClips != null && iceExplosionClips.Length > 0)
        {
            // Chọn ngẫu nhiên hoặc tuần tự clip nổ băng
            clipToPlay = iceExplosionClips[Random.Range(0, iceExplosionClips.Length)];
        }

        if (clipToPlay != null)
        {
            float pitch = 1.0f;
            if (randomizePitch)
            {
                // Tiếng nổ sau cao trào và trầm dần hoặc biến thiên
                pitch = Random.Range(minPitch, maxPitch);
            }

            float volume = explosionVolume;
            // Vụ nổ cuối cùng to nhất
            if (index == total - 1)
            {
                volume = Mathf.Clamp01(explosionVolume * 1.25f);
                pitch = Mathf.Max(0.75f, pitch * 0.9f);
            }

            PlayClipAtPoint(clipToPlay, position, volume, pitch);
        }
        else if (AudioManager.Instance != null)
        {
            AudioManager.PlaySFXIfAvailable(buttonHitSound, position);
        }
    }

    private void PlayClipAtPoint(AudioClip clip, Vector3 position, float volume, float pitch = 1.0f)
    {
        if (clip == null) return;

        GameObject tempAudio = new GameObject("TempAudio_IceExplosion_SFX");
        tempAudio.transform.position = position;

        AudioSource audioSource = tempAudio.AddComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.pitch = pitch;
        audioSource.spatialBlend = 0.2f;
        audioSource.minDistance = 5f;
        audioSource.maxDistance = 45f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;

        // Route vào SFX Mixer Group thông qua mainMixer gốc có sẵn
        if (AudioManager.Instance != null && AudioManager.Instance.mainMixer != null)
        {
            var sfxGroups = AudioManager.Instance.mainMixer.FindMatchingGroups("SFX");
            if (sfxGroups != null && sfxGroups.Length > 0)
            {
                audioSource.outputAudioMixerGroup = sfxGroups[0];
            }
        }

        audioSource.Play();
        Destroy(tempAudio, clip.length / Mathf.Max(0.1f, pitch) + 0.2f);
    }

    private IEnumerator MoveCameraAwayFromPlayer()
    {
        ResolveCameraReferences();

        if (cinemachineCamera == null || cameraAwayTarget == null)
        {
            yield break;
        }

        CachePreviousCameraFollow();

        if (!smoothCameraTransition || cameraBlendDuration <= 0f)
        {
            cinemachineCamera.Follow = cameraAwayTarget;
            yield break;
        }

        EnsureCameraBlendProxy();

        Vector3 startPosition = GetCurrentCameraPosition();
        Vector3 endPosition = cameraAwayTarget.position;
        Quaternion startRotation = cinemachineCamera.transform.rotation;
        Quaternion endRotation = cameraAwayTarget.rotation;
        float duration = Mathf.Max(0.01f, cameraBlendDuration);
        float elapsed = 0f;

        cameraBlendProxy.position = startPosition;
        cameraBlendProxy.rotation = Quaternion.identity;
        cinemachineCamera.Follow = cameraBlendProxy;
        cinemachineCamera.ForceCameraPosition(startPosition, startRotation);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            Vector3 cameraPosition = Vector3.Lerp(startPosition, endPosition, t);
            Quaternion cameraRotation = Quaternion.Slerp(startRotation, endRotation, t);

            cameraBlendProxy.position = cameraPosition;
            cinemachineCamera.ForceCameraPosition(cameraPosition, cameraRotation);
            yield return null;
        }

        cameraBlendProxy.position = endPosition;
        cinemachineCamera.ForceCameraPosition(endPosition, endRotation);
        cinemachineCamera.Follow = cameraAwayTarget;
        DestroyCameraBlendProxy();
    }

    private IEnumerator ReturnCameraToPlayer()
    {
        ResolveCameraReferences();

        Transform target = ResolveCameraReturnTarget();
        if (cinemachineCamera == null || target == null)
        {
            yield break;
        }

        if (!smoothCameraTransition || returnBlendDuration <= 0f)
        {
            cinemachineCamera.Follow = target;
            yield break;
        }

        EnsureCameraBlendProxy();

        Vector3 startPosition = GetCurrentCameraPosition();
        Vector3 endPosition = target.position;
        Quaternion startRotation = cinemachineCamera.transform.rotation;
        Quaternion endRotation = target.rotation;
        float duration = Mathf.Max(0.01f, returnBlendDuration);
        float elapsed = 0f;

        cameraBlendProxy.position = startPosition;
        cameraBlendProxy.rotation = Quaternion.identity;
        cinemachineCamera.Follow = cameraBlendProxy;
        cinemachineCamera.ForceCameraPosition(startPosition, startRotation);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            Vector3 cameraPosition = Vector3.Lerp(startPosition, endPosition, t);
            Quaternion cameraRotation = Quaternion.Slerp(startRotation, endRotation, t);

            cameraBlendProxy.position = cameraPosition;
            cinemachineCamera.ForceCameraPosition(cameraPosition, cameraRotation);
            yield return null;
        }

        cameraBlendProxy.position = endPosition;
        cinemachineCamera.ForceCameraPosition(endPosition, endRotation);
        cinemachineCamera.Follow = target;
        DestroyCameraBlendProxy();
    }

    private void TriggerCameraShake()
    {
        TriggerCustomCameraShake(buttonHitShakeDuration, buttonHitShakeMagnitude);
    }

    private void TriggerCustomCameraShake(float duration, float magnitude)
    {
        if (!enableCameraShake) return;

        if (cinemachineCamera != null)
        {
            StartCoroutine(ShakeCinemachineCameraProxy(duration, magnitude));
        }
        else if (CameraController.Instance != null)
        {
            CameraController.Instance.ShakeCamera(duration, magnitude);
        }
    }

    private IEnumerator ShakeCinemachineCameraProxy(float duration, float magnitude)
    {
        if (cinemachineCamera == null || duration <= 0f)
            yield break;

        EnsureCameraBlendProxy();

        Transform activeTarget = cinemachineCamera.Follow != null ? cinemachineCamera.Follow : cameraAwayTarget;
        Vector3 basePosition = activeTarget != null ? activeTarget.position : cinemachineCamera.transform.position;
        float elapsed = 0f;

        cinemachineCamera.Follow = cameraBlendProxy;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            Vector2 randomCircle = Random.insideUnitCircle * magnitude;
            Vector3 shakePos = basePosition + new Vector3(randomCircle.x, randomCircle.y, 0f);

            cameraBlendProxy.position = shakePos;
            cinemachineCamera.ForceCameraPosition(shakePos, cinemachineCamera.transform.rotation);
            yield return null;
        }

        if (activeTarget != null)
        {
            cameraBlendProxy.position = basePosition;
            cinemachineCamera.ForceCameraPosition(basePosition, cinemachineCamera.transform.rotation);
            cinemachineCamera.Follow = activeTarget;
        }
    }

    private void PlayButtonAnimation()
    {
        if (buttonAnimator == null || string.IsNullOrEmpty(buttonAnimationName))
            return;

        buttonAnimator.Play(buttonAnimationName, 0, 0f);
    }

    private void ResolveCameraReferences()
    {
        if (cinemachineCamera == null && autoFindMissingReferences)
        {
            cinemachineCamera = FindAnyObjectByType<CinemachineCamera>();
        }

        if (cameraReturnTarget == null && autoFindMissingReferences)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObject != null)
            {
                cameraReturnTarget = playerObject.transform;
            }
        }

        if (cameraAwayTarget == null
            && autoFindMissingReferences
            && !string.IsNullOrEmpty(cameraAwayTargetName))
        {
            GameObject targetObject = GameObject.Find(cameraAwayTargetName);
            if (targetObject != null)
            {
                cameraAwayTarget = targetObject.transform;
            }
        }
    }

    private GameObject ResolveRock(Collider2D other)
    {
        if (other == null || string.IsNullOrEmpty(rockTag)) return null;

        if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(rockTag))
            return other.attachedRigidbody.gameObject;

        Transform current = other.transform;
        while (current != null)
        {
            if (current.CompareTag(rockTag))
                return current.gameObject;

            current = current.parent;
        }

        return null;
    }

    private void BreakEverything()
    {
        // Destroy all Wall* children in WallMain
        if (wallMain != null)
        {
            for (int i = wallMain.childCount - 1; i >= 0; i--)
            {
                Transform child = wallMain.GetChild(i);
                if (child.name.StartsWith("Wall"))
                {
                    Destroy(child.gameObject);
                }
            }
        }

        // Destroy Xoay objects
        if (xoay1 != null) Destroy(xoay1);
        if (xoay2 != null) Destroy(xoay2);
    }

    private void BreakIceGroundObjects()
    {
        List<GameObject> iceGroundObjects = FindIceGroundObjects();
        if (iceGroundObjects.Count == 0) return;

        foreach (GameObject iceGround in iceGroundObjects)
        {
            if (iceGround == null) continue;

            if (spawnIceGroundBreakEffect)
            {
                SpawnBreakEffect(iceGround.transform.position, iceGroundShardCount, iceGroundEffectLifetime, iceGroundShardColor, customIceGroundBreakParticlePrefab);
            }

            if (disableIceGroundColliders)
            {
                Collider2D[] colliders = iceGround.GetComponentsInChildren<Collider2D>();
                foreach (Collider2D targetCollider in colliders)
                    targetCollider.enabled = false;
            }

            if (hideIceGroundRenderers)
            {
                Renderer[] renderers = iceGround.GetComponentsInChildren<Renderer>();
                foreach (Renderer targetRenderer in renderers)
                    targetRenderer.enabled = false;
            }

            if (destroyIceGroundObjects)
            {
                Destroy(iceGround, Mathf.Max(0f, destroyIceGroundDelay));
            }
        }
    }

    private List<GameObject> FindIceGroundObjects()
    {
        List<GameObject> results = new List<GameObject>();
        AddObjectsWithTag(results, iceGroundTag);

        if (!string.Equals(alternateIceGroundTag, iceGroundTag, System.StringComparison.Ordinal))
            AddObjectsWithTag(results, alternateIceGroundTag);

        return results;
    }

    private void AddObjectsWithTag(List<GameObject> results, string targetTag)
    {
        GameObject[] taggedObjects = FindObjectsWithTagSafe(targetTag);
        foreach (GameObject taggedObject in taggedObjects)
        {
            if (taggedObject != null && !results.Contains(taggedObject))
                results.Add(taggedObject);
        }
    }

    private GameObject[] FindObjectsWithTagSafe(string targetTag)
    {
        if (string.IsNullOrEmpty(targetTag))
            return System.Array.Empty<GameObject>();

        try
        {
            return GameObject.FindGameObjectsWithTag(targetTag);
        }
        catch (UnityException)
        {
            Debug.LogWarning($"Tag '{targetTag}' is not defined. Add it in Project Settings > Tags and Layers.", this);
            return System.Array.Empty<GameObject>();
        }
    }

    public static void SpawnBreakEffect(Vector3 position, int shardCount, float effectLifetime, Color shardColor, GameObject customPrefab = null)
    {
        if (customPrefab != null)
        {
            GameObject fx = Instantiate(customPrefab, position, Quaternion.identity);
            ParticleSystem[] particleSystems = fx.GetComponentsInChildren<ParticleSystem>();
            if (particleSystems.Length > 0)
            {
                float maxDelay = 0f;
                foreach (ParticleSystem ps in particleSystems)
                {
                    ps.Play();
                    float psDuration = ps.main.duration + ps.main.startLifetime.constantMax;
                    if (psDuration > maxDelay) maxDelay = psDuration;
                }
                float destroyDelay = effectLifetime > 0f ? effectLifetime : maxDelay;
                Destroy(fx, destroyDelay + 0.2f);
            }
            else
            {
                float destroyDelay = effectLifetime > 0f ? effectLifetime : 2f;
                Destroy(fx, destroyDelay);
            }
        }
    }

    private Transform ResolveCameraReturnTarget()
    {
        if (cameraReturnTarget != null)
        {
            return cameraReturnTarget;
        }

        if (previousCameraFollowCached && previousCameraFollow != null)
        {
            return previousCameraFollow;
        }

        return null;
    }

    private void CachePreviousCameraFollow()
    {
        if (cinemachineCamera == null || previousCameraFollowCached)
        {
            return;
        }

        previousCameraFollow = cinemachineCamera.Follow;
        previousCameraFollowCached = true;
    }

    private Vector3 GetCurrentCameraPosition()
    {
        if (cinemachineCamera.PreviousStateIsValid)
        {
            return cinemachineCamera.State.GetFinalPosition();
        }

        return cinemachineCamera.transform.position;
    }

    private void EnsureCameraBlendProxy()
    {
        if (cameraBlendProxy != null)
        {
            return;
        }

        cameraBlendProxyObject = new GameObject("Plinko Camera Blend Target");
        cameraBlendProxyObject.hideFlags = HideFlags.HideAndDontSave;
        cameraBlendProxy = cameraBlendProxyObject.transform;
    }

    private void DestroyCameraBlendProxy()
    {
        if (cameraBlendProxyObject == null)
        {
            cameraBlendProxy = null;
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(cameraBlendProxyObject);
        }
        else
        {
            DestroyImmediate(cameraBlendProxyObject);
        }

        cameraBlendProxyObject = null;
        cameraBlendProxy = null;
    }
}
