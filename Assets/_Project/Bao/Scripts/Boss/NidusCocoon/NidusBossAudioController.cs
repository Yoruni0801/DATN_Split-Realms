using UnityEngine;
using Bao.Bosses;
using Bao.Enemies; // For AudioManager if it's there or just use global AudioManager

namespace Bao.Bosses
{
    [RequireComponent(typeof(NidusCocoonBoss))]
    public class NidusBossAudioController : MonoBehaviour
    {
        [Header("Death Audio")]
        [SerializeField] private AudioClip deathClip;

        [Header("Boss Fight Music (BGM)")]
        [Tooltip("Clip nhạc nền riêng của Nidus Cocoon Boss. Sẽ được phát khi Boss Engage (hiện thanh máu) và dừng/khôi phục khi Boss chết.")]
        [SerializeField] private AudioClip bossFightBGM;
        [SerializeField] private bool autoSyncWithDispatcher = true;

        [Header("Combat Skill Audio")]
        [SerializeField] private AudioClip meleeClip;
        [SerializeField] private AudioClip summonClip;
        [SerializeField] private AudioClip acidRainClip;
        [SerializeField] private AudioClip toxicDetonationClip;
        [SerializeField] private AudioClip burrowClip;
        [SerializeField] private AudioClip rootSpikeClip;
        [SerializeField] private AudioClip rootPrisonClip;

        [Header("Settings")]
        [SerializeField] private float pitchVariance = 0.1f;
        [SerializeField] [Range(0f, 1f)] private float volumeScale = 1f;

        public AudioClip BossFightBGM => bossFightBGM;

        private NidusCocoonBoss nidusBoss;
        private BossUIEventDispatcher uiEventDispatcher;
        private NidusBossState previousState;
        private bool hasPlayedDeathSound;

        private void Awake()
        {
            nidusBoss = GetComponent<NidusCocoonBoss>();
            uiEventDispatcher = GetComponent<BossUIEventDispatcher>();

            SyncBGM();
        }

        private void Start()
        {
            SyncBGM();
        }

        private void SyncBGM()
        {
            if (uiEventDispatcher == null) uiEventDispatcher = GetComponent<BossUIEventDispatcher>();
            if (uiEventDispatcher == null) return;

            if (bossFightBGM != null)
            {
                uiEventDispatcher.SetBossBGM(bossFightBGM);
            }
            else if (uiEventDispatcher.BossBGM != null)
            {
                bossFightBGM = uiEventDispatcher.BossBGM;
            }
        }

        private void Update()
        {
            if (nidusBoss == null) return;

            // Xử lý âm thanh khi Boss chết
            if (nidusBoss.IsDead && !hasPlayedDeathSound)
            {
                hasPlayedDeathSound = true;
                PlaySound(deathClip);
                return;
            }

            // Xử lý âm thanh khi Boss đổi trạng thái (sử dụng skill)
            var currentState = nidusBoss.CurrentState;
            if (currentState != previousState)
            {
                HandleStateChange(currentState);
                previousState = currentState;
            }
        }

        private void HandleStateChange(NidusBossState newState)
        {
            if (newState == null) return;

            if (newState is NidusMeleeState)
                PlaySound(meleeClip);
            else if (newState is NidusSummonState)
                PlaySound(summonClip);
            else if (newState is NidusAcidRainState)
                PlaySound(acidRainClip);
            else if (newState is NidusToxicDetonationState)
                PlaySound(toxicDetonationClip);
            else if (newState is NidusBurrowState)
                PlaySound(burrowClip);
            else if (newState is NidusRootSpikeWaveState)
                PlaySound(rootSpikeClip);
            else if (newState is NidusRootPrisonState)
                PlaySound(rootPrisonClip);
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && AudioManager.Instance != null)
            {
                float pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
                // Hệ thống AudioManager đã có sẵn Object Pooling (sfxPool) để tối ưu
                AudioManager.Instance.PlaySFX(clip, transform.position, pitch);
            }
        }
    }
}
