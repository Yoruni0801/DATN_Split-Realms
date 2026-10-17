using UnityEngine;

namespace Bao.Bosses.QuyVuongPhase4
{
    /// <summary>
    /// Tự động hủy GameObject sau khi Animation không lặp (non-looping clip) chạy xong toàn bộ frame.
    /// </summary>
    public class Phase4AnimationAutoDestroy : MonoBehaviour
    {
        [SerializeField] private float fallbackLifetime = 1.2f;

        private void Start()
        {
            float destroyTime = fallbackLifetime;

            var animator = GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                var clips = animator.runtimeAnimatorController.animationClips;
                if (clips != null && clips.Length > 0)
                {
                    float maxClipLength = 0f;
                    foreach (var clip in clips)
                    {
                        if (clip != null && clip.length > maxClipLength)
                        {
                            maxClipLength = clip.length;
                        }
                    }
                    if (maxClipLength > 0f)
                    {
                        destroyTime = maxClipLength + 0.05f;
                    }
                }
            }

            Destroy(gameObject, destroyTime);
        }
    }
}
