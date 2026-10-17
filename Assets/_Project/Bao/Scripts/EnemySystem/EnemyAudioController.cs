using UnityEngine;
using Bao.Enemies;

namespace Bao.Enemies
{
    [RequireComponent(typeof(BaoEnemyBase))]
    public class EnemyAudioController : MonoBehaviour
    {
        [Header("Audio Clips")]
        [SerializeField] private AudioClip attackClip;
        [SerializeField] private AudioClip deathClip;

        [Header("Settings")]
        [SerializeField] private float pitchVariance = 0.1f;
        [SerializeField][Range(0f, 1f)] private float volumeScale = 1f;

        private BaoEnemyBase enemyBase;
        private BaoEnemyState previousState;
        private bool hasPlayedDeathSound;

        private void Awake()
        {
            enemyBase = GetComponent<BaoEnemyBase>();
        }

        private void Update()
        {
            if (enemyBase == null) return;

            // Xử lý âm thanh khi chết
            if (enemyBase.IsDead && !hasPlayedDeathSound)
            {
                hasPlayedDeathSound = true;
                PlayDeathSound();
                return;
            }

            // Xử lý âm thanh khi tấn công bằng cách kiểm tra sự thay đổi trạng thái
            if (enemyBase.StateMachine != null)
            {
                var currentState = enemyBase.StateMachine.CurrentState;
                if (currentState != previousState)
                {
                    if (IsAttackState(currentState))
                    {
                        PlayAttackSound();
                    }
                    previousState = currentState;
                }
            }
        }

        private bool IsAttackState(BaoEnemyState state)
        {
            if (state == null) return false;

            // Kiểm tra các class trạng thái tấn công của từng quái vật cụ thể
            if (state is PhantomAttackState ||
                state is DusthornRollState ||
                state is BonewheelAttackState ||
                state is WildrootCloseState ||
                state is BaoEnemyAttackState)
            {
                return true;
            }

            // Fallback: Kiểm tra thêm bằng tên class cho các quái có thể thêm vào sau này
            string stateName = state.GetType().Name.ToLower();
            return stateName.Contains("attack") ||
                   stateName.Contains("charge") ||
                   stateName.Contains("shoot") ||
                   stateName.Contains("roll") ||
                   stateName.Contains("explode") ||
                   stateName.Contains("slam") ||
                   stateName.Contains("dive") ||
                   stateName.Contains("close");
        }

        private void PlayAttackSound()
        {
            if (attackClip != null && AudioManager.Instance != null)
            {
                float pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
                // Hệ thống AudioManager đã có sẵn Object Pooling (sfxPool) để tối ưu
                AudioManager.Instance.PlaySFX(attackClip, transform.position, pitch);
            }
        }

        private void PlayDeathSound()
        {
            if (deathClip != null && AudioManager.Instance != null)
            {
                float pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
                // Hệ thống AudioManager đã có sẵn Object Pooling (sfxPool) để tối ưu
                AudioManager.Instance.PlaySFX(deathClip, transform.position, pitch);
            }
        }
    }
}
