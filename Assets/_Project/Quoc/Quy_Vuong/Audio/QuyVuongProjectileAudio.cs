using UnityEngine;

namespace Quoc.QuyVuong.Audio
{
    /// <summary>
    /// Component âm thanh gắn độc lập trên các Prefab phi đạn, mũi tên, bẫy hoặc VFX hazard của Boss:
    /// - Quản lý âm thanh khi Spawn / OnEnable (phát ngay khi sinh ra).
    /// - Quản lý âm thanh bay rít liên tục (Flight Loop).
    /// - Quản lý âm thanh phát nổ va chạm (Impact / Hit / Destroy).
    /// - Tích hợp chặt chẽ với AudioManager.Instance và QuyVuongAudioController.Instance.
    /// </summary>
    [DisallowMultipleComponent]
    public class QuyVuongProjectileAudio : MonoBehaviour
    {
        [Header("Audio Clips")]
        [Tooltip("Âm thanh phát khi Projectile / Hazard vừa được sinh ra")]
        [SerializeField] private AudioClip spawnClip;

        [Tooltip("Âm thanh bay rít liên tục trong lúc di chuyển")]
        [SerializeField] private AudioClip flightLoopClip;

        [Tooltip("Âm thanh phát nổ va chạm khi trúng mục tiêu hoặc chạm đất")]
        [SerializeField] private AudioClip impactClip;

        [Header("Sub-Bass & Screen Shake Impact")]
        [Tooltip("Kích hoạt thêm tiếng Sub-bass siêu trầm khi phát nổ")]
        [SerializeField] private bool triggerSubBassOnImpact = false;

        [Header("Audio Settings")]
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0.25f;
        [SerializeField] private float minDistance = 5f;
        [SerializeField] private float maxDistance = 35f;
        [SerializeField] private bool playImpactOnDestroy = false;

        private AudioSource loopAudioSource;
        private bool hasPlayedImpact = false;

        public void SetClips(AudioClip spawn, AudioClip loop, AudioClip impact, bool subBass = false)
        {
            spawnClip = spawn;
            flightLoopClip = loop;
            impactClip = impact;
            triggerSubBassOnImpact = subBass;
        }

        private void Awake()
        {
            if (flightLoopClip != null)
            {
                loopAudioSource = gameObject.AddComponent<AudioSource>();
                loopAudioSource.playOnAwake = false;
                loopAudioSource.clip = flightLoopClip;
                loopAudioSource.loop = true;
                loopAudioSource.volume = volume;
                loopAudioSource.spatialBlend = spatialBlend;
                loopAudioSource.minDistance = minDistance;
                loopAudioSource.maxDistance = maxDistance;
                loopAudioSource.rolloffMode = AudioRolloffMode.Linear;
            }
        }

        private void OnEnable()
        {
            hasPlayedImpact = false;

            if (spawnClip != null)
            {
                PlayClip(spawnClip, volume);
            }

            if (loopAudioSource != null && flightLoopClip != null)
            {
                loopAudioSource.Play();
            }
        }

        private void OnDisable()
        {
            if (loopAudioSource != null && loopAudioSource.isPlaying)
            {
                loopAudioSource.Stop();
            }
        }

        private void OnDestroy()
        {
            if (playImpactOnDestroy && !hasPlayedImpact && impactClip != null)
            {
                TriggerImpactAudio();
            }
        }

        /// <summary>
        /// Kích hoạt âm thanh va chạm thủ công khi có sự kiện va chạm.
        /// </summary>
        public void TriggerImpactAudio()
        {
            if (hasPlayedImpact) return;
            hasPlayedImpact = true;

            if (loopAudioSource != null && loopAudioSource.isPlaying)
            {
                loopAudioSource.Stop();
            }

            if (impactClip != null)
            {
                PlayClipAtPoint(impactClip, transform.position, volume);
            }

            if (triggerSubBassOnImpact && QuyVuongAudioController.Instance != null)
            {
                QuyVuongAudioController.Instance.PlaySubBassImpact(0.7f);
            }
        }

        private void PlayClip(AudioClip clip, float vol)
        {
            if (clip == null) return;

            if (QuyVuongAudioController.Instance != null)
            {
                QuyVuongAudioController.Instance.PlaySFX(clip, vol);
            }
            else if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(clip, transform.position);
            }
            else
            {
                AudioSource.PlayClipAtPoint(clip, transform.position, vol);
            }
        }

        private void PlayClipAtPoint(AudioClip clip, Vector3 position, float vol)
        {
            if (clip == null) return;

            if (QuyVuongAudioController.Instance != null)
            {
                QuyVuongAudioController.Instance.PlaySFXAtPosition(clip, position, vol);
            }
            else if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(clip, position);
            }
            else
            {
                AudioSource.PlayClipAtPoint(clip, position, vol);
            }
        }
    }
}
