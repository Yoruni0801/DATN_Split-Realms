using System;
using System.Collections.Generic;
using UnityEngine;
using Spine;
using Spine.Unity;

namespace Quoc.QuyVuong.Audio
{
    [Serializable]
    public class SpineAnimationAudioBinding
    {
        [Tooltip("Tên của Animation Spine (ví dụ: 'Idle', 'Run', 'Attack', 'Skill', 'Gap closer', 'Death')")]
        public string animationName;

        [Tooltip("Âm thanh phát khi Animation này bắt đầu chạy")]
        public AudioClip onStartClip;

        [Tooltip("Âm lượng tùy chỉnh")]
        [Range(0f, 2f)] public float volume = 1f;

        [Tooltip("Chỉ phát 1 lần nếu animation đang loop")]
        public bool playOncePerLoop = true;

        [Tooltip("Thời gian trễ (giây) trước khi phát")]
        public float delay = 0f;
    }

    [Serializable]
    public class SpineEventAudioBinding
    {
        [Tooltip("Tên Spine Event trong file Spine (ví dụ: 'footstep', 'slash', 'shoot', 'roar', 'hit', 'slam')")]
        public string eventName;

        [Tooltip("Âm thanh phát khi Spine Event này được kích hoạt")]
        public AudioClip eventClip;

        [Tooltip("Âm lượng tùy chỉnh")]
        [Range(0f, 2f)] public float volume = 1f;
    }

    /// <summary>
    /// Component cầu nối tự động đồng bộ âm thanh với Spine Animation của Boss Quỷ Vương:
    /// - Lắng nghe sự kiện AnimationState.Start, AnimationState.Event từ SkeletonAnimation.
    /// - Cho phép cấu hình ánh xạ Animation Name hoặc Spine Event với AudioClip trên Inspector.
    /// - Tự động phát qua QuyVuongAudioController hoặc AudioSource cục bộ.
    /// </summary>
    [RequireComponent(typeof(SkeletonAnimation))]
    public class QuyVuongSpineAudioBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SkeletonAnimation skeletonAnimation;
        [SerializeField] private QuyVuongAudioController audioController;

        [Header("Animation to Audio Mappings")]
        [Tooltip("Danh sách ánh xạ tên Animation sang AudioClip")]
        [SerializeField] private List<SpineAnimationAudioBinding> animationBindings = new List<SpineAnimationAudioBinding>();

        [Header("Spine Event to Audio Mappings")]
        [Tooltip("Danh sách ánh xạ tên Spine Event sang AudioClip")]
        [SerializeField] private List<SpineEventAudioBinding> eventBindings = new List<SpineEventAudioBinding>();

        private readonly Dictionary<string, SpineAnimationAudioBinding> animLookup = new Dictionary<string, SpineAnimationAudioBinding>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SpineEventAudioBinding> eventLookup = new Dictionary<string, SpineEventAudioBinding>(StringComparer.OrdinalIgnoreCase);

        private void Awake()
        {
            if (skeletonAnimation == null)
            {
                skeletonAnimation = GetComponent<SkeletonAnimation>();
            }

            if (audioController == null)
            {
                audioController = GetComponentInParent<QuyVuongAudioController>();
            }

            BuildLookupDictionaries();
        }

        private void OnEnable()
        {
            if (skeletonAnimation != null && skeletonAnimation.AnimationState != null)
            {
                skeletonAnimation.AnimationState.Start += HandleAnimationStart;
                skeletonAnimation.AnimationState.Event += HandleSpineEvent;
            }
        }

        private void OnDisable()
        {
            if (skeletonAnimation != null && skeletonAnimation.AnimationState != null)
            {
                skeletonAnimation.AnimationState.Start -= HandleAnimationStart;
                skeletonAnimation.AnimationState.Event -= HandleSpineEvent;
            }
        }

        private void BuildLookupDictionaries()
        {
            animLookup.Clear();
            if (animationBindings != null)
            {
                foreach (var binding in animationBindings)
                {
                    if (!string.IsNullOrEmpty(binding.animationName) && !animLookup.ContainsKey(binding.animationName))
                    {
                        animLookup.Add(binding.animationName, binding);
                    }
                }
            }

            eventLookup.ClearEvent();
        }

        private void HandleAnimationStart(TrackEntry trackEntry)
        {
            if (trackEntry == null || trackEntry.Animation == null) return;
            string animName = trackEntry.Animation.Name;

            if (animLookup.TryGetValue(animName, out var binding))
            {
                if (binding.onStartClip != null)
                {
                    if (binding.delay > 0f)
                    {
                        StartCoroutine(PlayDelayedAudio(binding.onStartClip, binding.volume, binding.delay));
                    }
                    else
                    {
                        PlayClip(binding.onStartClip, binding.volume);
                    }
                }
            }
        }

        private void HandleSpineEvent(TrackEntry trackEntry, Spine.Event e)
        {
            if (e == null || e.Data == null) return;
            string eventName = e.Data.Name;

            if (eventLookup.TryGetValue(eventName, out var binding))
            {
                if (binding.eventClip != null)
                {
                    PlayClip(binding.eventClip, binding.volume);
                }
            }
        }

        private System.Collections.IEnumerator PlayDelayedAudio(AudioClip clip, float volume, float delay)
        {
            yield return new WaitForSeconds(delay);
            PlayClip(clip, volume);
        }

        private void PlayClip(AudioClip clip, float volume)
        {
            if (clip == null) return;

            QuyVuongAudioController controller = audioController != null ? audioController : QuyVuongAudioController.Instance;
            if (controller != null)
            {
                controller.PlaySFX(clip, volume);
            }
            else
            {
                AudioSource.PlayClipAtPoint(clip, transform.position, volume);
            }
        }
    }

    internal static class DictionaryExtensions
    {
        public static void ClearEvent(this Dictionary<string, SpineEventAudioBinding> dict)
        {
            dict.Clear();
        }
    }
}
