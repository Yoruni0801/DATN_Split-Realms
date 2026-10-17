using System.Collections.Generic;
using UnityEngine;

namespace Quoc.Boss
{
    public class BossAttackSelector : MonoBehaviour
    {
        public enum BossFlowStep
        {
            CheckJumpOrSlash = 0,
            TripleSlash = 1,
            Charge = 2
        }

        [Header("Sequential Flow Timing Settings")]
        [SerializeField, Tooltip("Ngưỡng khoảng cách được coi là Player ở xa (mét)")]
        public float farDistanceThreshold = 4.5f;

        [Header("Attacks Catalog")]
        [SerializeField, Tooltip("Danh sách tất cả các Attack Data của Boss")]
        private List<BossAttackData> availableAttacks = new List<BossAttackData>();

        [Header("Current Flow Debug")]
        [SerializeField, Tooltip("Bước hiện tại trong luồng combat")]
        private BossFlowStep currentStep = BossFlowStep.CheckJumpOrSlash;

        private BossAttackData jumpSlamData;
        private BossAttackData tripleSlashData;
        private BossAttackData chargeData;
        private BossController bossController;

        private void Awake()
        {
            bossController = GetComponent<BossController>();
            EnsureDefaultAttacksIfEmpty();
        }

        private void OnEnable()
        {
            if (bossController == null) bossController = GetComponent<BossController>();
            EnsureDefaultAttacksIfEmpty();
        }

        public void EnsureDefaultAttacksIfEmpty()
        {
            if (availableAttacks == null)
            {
                availableAttacks = new List<BossAttackData>();
            }

            availableAttacks.RemoveAll(a => a == null);

            string jumpAnim = (bossController != null && !string.IsNullOrEmpty(bossController.JumpSlamAnimName)) ? bossController.JumpSlamAnimName : "Skill";
            string slashAnim = (bossController != null && !string.IsNullOrEmpty(bossController.Slash1AnimName)) ? bossController.Slash1AnimName : "Attack";
            string chargeAnim = (bossController != null && !string.IsNullOrEmpty(bossController.ChargeAnimName)) ? bossController.ChargeAnimName : "Run";

            jumpSlamData = FindOrData("Jump Slam", jumpAnim, 0f, 30f, 0.3f, 0.2f, 30);
            tripleSlashData = FindOrData("Triple Slash", slashAnim, 0f, 30f, 0.2f, 0.2f, 15, 3);
            chargeData = FindOrData("Charge", chargeAnim, 0f, 30f, 0.3f, 0.2f, 30);
        }

        public void UpdateAttackAnimations(string jumpAnim, string slashAnim, string chargeAnim)
        {
            if (jumpSlamData != null && !string.IsNullOrEmpty(jumpAnim)) jumpSlamData.spineAnimationName = jumpAnim;
            if (tripleSlashData != null && !string.IsNullOrEmpty(slashAnim)) tripleSlashData.spineAnimationName = slashAnim;
            if (chargeData != null && !string.IsNullOrEmpty(chargeAnim)) chargeData.spineAnimationName = chargeAnim;
        }

        private BossAttackData FindOrData(string name, string anim, float minDist, float maxDist, float teleg, float recov, int dmg, int combo = 1)
        {
            BossAttackData data = availableAttacks.Find(a => a != null && a.attackName == name);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<BossAttackData>();
                data.attackName = name;
                data.allowedPhases = new List<BossPhase> { BossPhase.Phase1, BossPhase.Phase2, BossPhase.Phase3 };
                data.minDistance = minDist;
                data.maxDistance = maxDist;
                data.cooldown = 0f;
                data.weight = 1f;
                data.damage = dmg;
                data.comboCount = combo;
                data.telegraphDuration = teleg;
                data.recoveryDuration = recov;
                data.spineAnimationName = anim;
                availableAttacks.Add(data);
            }
            else if (!string.IsNullOrEmpty(anim))
            {
                data.spineAnimationName = anim;
            }
            return data;
        }

        public void ResetFlow()
        {
            currentStep = BossFlowStep.CheckJumpOrSlash;
        }

        public BossAttackData SelectAttack(BossPhase currentPhase, float distanceToPlayer)
        {
            if (bossController != null && !bossController.IsAggroEnabled)
            {
                return null;
            }

            EnsureDefaultAttacksIfEmpty();

            switch (currentStep)
            {
                case BossFlowStep.CheckJumpOrSlash:
                    // 1. Nếu Player ở xa -> Nhảy tới áp sát (Jump Slam), sau đó sang bước Triple Slash
                    if (distanceToPlayer >= farDistanceThreshold)
                    {
                        currentStep = BossFlowStep.TripleSlash;
                        Debug.Log($"[BossFlow] Player ở xa ({distanceToPlayer:F1}m >= {farDistanceThreshold}m) -> Tung đòn Jump Slam nhảy tới Player!");
                        return jumpSlamData;
                    }
                    else
                    {
                        // Player đã ở gần -> Chém ngay 3 phát (Triple Slash), chém xong chuyển thẳng sang Charge (không pause)
                        currentStep = BossFlowStep.Charge;
                        Debug.Log($"[BossFlow] Player ở gần ({distanceToPlayer:F1}m < {farDistanceThreshold}m) -> Tung đòn Triple Slash 3 phát chém!");
                        return tripleSlashData;
                    }

                case BossFlowStep.TripleSlash:
                    // 2. Chém 3 phát sau khi vừa Jump Slam áp sát -> Chém xong chuyển thẳng sang Charge (không pause)
                    currentStep = BossFlowStep.Charge;
                    Debug.Log("[BossFlow] Đã áp sát -> Tung đòn Triple Slash 3 phát chém!");
                    return tripleSlashData;

                case BossFlowStep.Charge:
                    // 3. Húc mạnh về phía Player -> Húc xong quay lại bước 1 bắt đầu chu kỳ combo mới
                    currentStep = BossFlowStep.CheckJumpOrSlash;
                    Debug.Log("[BossFlow] Chém xong -> Tung đòn HÚC (CHARGE ATTACK) -> Hoàn tất chu kỳ combo!");
                    return chargeData;

                default:
                    currentStep = BossFlowStep.CheckJumpOrSlash;
                    return jumpSlamData;
            }
        }

        public void SetAttacksList(List<BossAttackData> attacks)
        {
            availableAttacks = attacks;
            EnsureDefaultAttacksIfEmpty();
        }
    }
}
