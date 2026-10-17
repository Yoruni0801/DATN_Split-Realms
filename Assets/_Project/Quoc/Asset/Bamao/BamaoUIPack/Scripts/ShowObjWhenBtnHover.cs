using UnityEngine;
using UnityEngine.EventSystems;

namespace BamaoUIPack.Scripts
{
    [RequireComponent(typeof(AudioSource))]
    public class ShowObjWhenBtnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public GameObject FocusObj;

        public AudioClip HoverSound;
        public AudioClip ClickSound;
        public AudioClip ExitSound;

        public AudioSource Source;

        [Header("Sound Settings")]
        [SerializeField] private float hoverCooldown = 0.12f;
        [SerializeField] private bool playExitSound = false;

        private static float lastHoverTime;

        private void Awake()
        {
            if (Source == null)
                Source = GetComponent<AudioSource>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (FocusObj != null)
                FocusObj.SetActive(true);

            PlayHoverSound();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (FocusObj != null)
                FocusObj.SetActive(false);

            if (playExitSound)
                PlaySound(ExitSound);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            PlaySound(ClickSound);
        }

        private void PlayHoverSound()
        {
            if (Time.unscaledTime - lastHoverTime < hoverCooldown)
                return;

            lastHoverTime = Time.unscaledTime;
            PlaySound(HoverSound);
        }

        private void PlaySound(AudioClip clip)
        {
            if (Source == null || clip == null)
                return;

            Source.PlayOneShot(clip);
        }
    }
}