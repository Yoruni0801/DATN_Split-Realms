using UnityEngine;
using UnityEngine.Playables;
using EnemySystem;

namespace Duy.Timeline
{
    [DisallowMultipleComponent]
    public class TimelineEnemyAggroController : MonoBehaviour
    {
        [Header("Enemy Target")]
        [SerializeField] private StandaloneFlyingShooterEnemy targetEnemy;

        [Header("Timeline Trigger (Optional)")]
        [SerializeField] private PlayableDirector director;

        private void Awake()
        {
            if (targetEnemy == null)
            {
                targetEnemy = GetComponent<StandaloneFlyingShooterEnemy>();
                if (targetEnemy == null)
                {
                    targetEnemy = GetComponentInChildren<StandaloneFlyingShooterEnemy>();
                }
            }

            if (targetEnemy != null)
            {
                targetEnemy.DisableAggro();
            }
        }

        private void OnEnable()
        {
            if (director != null)
            {
                director.stopped += OnTimelineStopped;
            }
        }

        private void OnDisable()
        {
            if (director != null)
            {
                director.stopped -= OnTimelineStopped;
            }
        }

        private void OnTimelineStopped(PlayableDirector pd)
        {
            EnableAggro();
        }

        public void EnableAggro()
        {
            if (targetEnemy != null)
            {
                targetEnemy.EnableAggro();
            }
        }

        public void DisableAggro()
        {
            if (targetEnemy != null)
            {
                targetEnemy.DisableAggro();
            }
        }
    }
}
