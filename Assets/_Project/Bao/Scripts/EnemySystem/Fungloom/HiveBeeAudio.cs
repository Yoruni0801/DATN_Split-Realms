using UnityEngine;

namespace Bao.Enemies
{
    /// <summary>
    /// Chịu trách nhiệm phát âm thanh cho đạn ong (HiveBeeProjectile).
    /// Hoàn toàn tách biệt, không can thiệp vào logic bay hay sát thương cũ.
    /// </summary>
    [RequireComponent(typeof(HiveBeeProjectile))]
    public class HiveBeeAudio : MonoBehaviour
    {
        [Header("Audio Clips")]
        [Tooltip("Âm thanh khi con ong vừa được sinh ra / bắn ra")]
        [SerializeField] private AudioClip spawnClip;
        
        [Tooltip("Âm thanh bay (vo ve) liên tục")]
        [SerializeField] private AudioClip flyLoopClip;

        [Tooltip("Âm thanh khi ong chết / nổ / trúng mục tiêu")]
        [SerializeField] private AudioClip deathClip;

        [Header("Settings")]
        [SerializeField] private float pitchVariance = 0.1f;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;

        private AudioSource loopSource;
        private bool isShuttingDown;

        private void Start()
        {
            // Phát âm thanh khi spawn (một lần)
            if (spawnClip != null && AudioManager.Instance != null)
            {
                float pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
                AudioManager.Instance.PlaySFX(spawnClip, transform.position, pitch);
            }

            // Phát âm thanh bay vo ve (lặp lại liên tục)
            if (flyLoopClip != null)
            {
                loopSource = gameObject.AddComponent<AudioSource>();
                loopSource.clip = flyLoopClip;
                loopSource.loop = true;
                loopSource.volume = volume * (AudioManager.Instance != null ? AudioManager.Instance.GetSfxVolume() : 1f);
                loopSource.spatialBlend = 1f; // Âm thanh 3D/2D tùy ý
                loopSource.Play();
            }
        }

        private void OnDestroy()
        {
            if (isShuttingDown) return;
            isShuttingDown = true;

            // Phát âm thanh khi nổ/chết
            if (deathClip != null && AudioManager.Instance != null)
            {
                float pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
                // Dùng object pool của AudioManager để phát tiếng nổ tại vị trí hiện tại
                AudioManager.Instance.PlaySFX(deathClip, transform.position, pitch);
            }
        }
    }
}
