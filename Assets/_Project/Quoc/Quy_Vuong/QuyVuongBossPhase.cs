using UnityEngine;
using Spine.Unity;
using Spine;

namespace Quoc.QuyVuong
{
    public class QuyVuongBossPhase : MonoBehaviour
    {
        [Header("Spine Model References")]
        [SerializeField]
        private SkeletonAnimation skeletonAnimation;

        [Header("Animation Names")]
        [SerializeField]
        private string idleAnimName = "Idle";

        [SerializeField]
        private string moveAnimName = "Run";

        [SerializeField]
        private string attackAnimName = "Skill";

        [SerializeField]
        private string transitionAnimName = "Skill";

        [SerializeField]
        private string dieAnimName = "Death";

        [Header("Attack Transform Points")]
        [SerializeField]
        private Transform meleePoint;

        [SerializeField]
        private Transform rangedPoint;

        [SerializeField]
        private Transform groundPoint;

        public SkeletonAnimation SkeletonAnimation => skeletonAnimation;
        public Transform MeleePoint => meleePoint != null ? meleePoint : transform;
        public Transform RangedPoint => rangedPoint != null ? rangedPoint : transform;
        public Transform GroundPoint => groundPoint != null ? groundPoint : transform;

        private void Awake()
        {
            if (skeletonAnimation == null)
            {
                skeletonAnimation = GetComponent<SkeletonAnimation>();
            }
            if (skeletonAnimation == null)
            {
                skeletonAnimation = GetComponentInChildren<SkeletonAnimation>();
            }
        }

        public TrackEntry PlaySpineAnimation(string animName, bool loop = false, float timeScale = 1.0f, int trackIndex = 0, bool setToSetupPose = true, float mixDuration = -1f)
        {
            if (skeletonAnimation == null || string.IsNullOrEmpty(animName)) return null;
            if (skeletonAnimation.AnimationState == null || skeletonAnimation.Skeleton == null) return null;

            // Check if animation exists in Spine skeleton data
            if (skeletonAnimation.Skeleton.Data != null && skeletonAnimation.Skeleton.Data.FindAnimation(animName) == null)
            {
                Debug.LogWarning($"[QuyVuongBossPhase] Animation '{animName}' không tồn tại trên Skeleton '{skeletonAnimation.name}'. Bỏ qua animation này.");
                return null;
            }

            skeletonAnimation.AnimationState.ClearTrack(trackIndex);
            if (setToSetupPose)
            {
                skeletonAnimation.Skeleton?.SetToSetupPose();
            }
            TrackEntry entry = skeletonAnimation.AnimationState.SetAnimation(trackIndex, animName, loop);
            if (entry != null)
            {
                entry.TimeScale = timeScale > 0f ? timeScale : 1.0f;
                if (mixDuration >= 0f)
                {
                    entry.MixDuration = mixDuration;
                }
            }
            return entry;
        }

        public TrackEntry AddSpineAnimation(string animName, bool loop = false, float delay = 0f, float timeScale = 1.0f, int trackIndex = 0, float mixDuration = -1f)
        {
            if (skeletonAnimation == null || string.IsNullOrEmpty(animName)) return null;
            if (skeletonAnimation.AnimationState == null || skeletonAnimation.Skeleton == null) return null;

            if (skeletonAnimation.Skeleton.Data != null && skeletonAnimation.Skeleton.Data.FindAnimation(animName) == null)
            {
                Debug.LogWarning($"[QuyVuongBossPhase] Animation '{animName}' không tồn tại trên Skeleton '{skeletonAnimation.name}'. Bỏ qua animation này.");
                return null;
            }

            TrackEntry entry = skeletonAnimation.AnimationState.AddAnimation(trackIndex, animName, loop, delay);
            if (entry != null)
            {
                entry.TimeScale = timeScale > 0f ? timeScale : 1.0f;
                if (mixDuration >= 0f)
                {
                    entry.MixDuration = mixDuration;
                }
            }
            return entry;
        }

        public void PlayIdle() => PlaySpineAnimation(idleAnimName, true);
        public void PlayMove() => PlaySpineAnimation(moveAnimName, true);
        public void PlayAttack() => PlaySpineAnimation(attackAnimName, false);
        public void PlayTransition() => PlaySpineAnimation(transitionAnimName, false);
        public void PlayDeath() => PlaySpineAnimation(dieAnimName, false);

        public void SetColor(Color color)
        {
            if (skeletonAnimation != null && skeletonAnimation.Skeleton != null)
            {
                skeletonAnimation.Skeleton.SetColor(color);
            }
        }

        public void SetFacingDirection(float directionX)
        {
            if (Mathf.Abs(directionX) > 0.05f)
            {
                Vector3 scale = transform.localScale;
                scale.x = directionX > 0f ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
                transform.localScale = scale;
            }
        }

        public void FacePlayer(Transform playerTransform)
        {
            if (playerTransform == null) return;
            SetFacingDirection(playerTransform.position.x - transform.position.x);
        }
    }
}
