using System;
using UnityEngine;

namespace Quoc.PuzzleQuest
{
    public enum PuzzleGemType
    {
        Sword = 0,
        Water = 1,
        Fire = 2,
        Wind = 3
    }

    public enum PuzzleSkillTier
    {
        U = 3,
        I = 4,
        O = 5
    }

    [Serializable]
    public sealed class PuzzleQuestDamageTuning
    {
        [Min(0)] public int match3Damage = 18;
        [Min(0)] public int match4Damage = 30;
        [Min(0)] public int match5Damage = 48;
        [Min(0)] public int damagePerGemAfter5 = 8;

        public int Evaluate(int longestRun, int totalMatched)
        {
            int damage;
            if (longestRun >= 5)
            {
                damage = match5Damage + Mathf.Max(0, totalMatched - 5) * damagePerGemAfter5;
            }
            else if (longestRun == 4)
            {
                damage = match4Damage + Mathf.Max(0, totalMatched - 4) * damagePerGemAfter5;
            }
            else
            {
                damage = match3Damage + Mathf.Max(0, totalMatched - 3) * damagePerGemAfter5;
            }

            return Mathf.Max(0, damage);
        }
    }

    [Serializable]
    public sealed class PuzzleQuestElementTuning
    {
        public PuzzleGemType element = PuzzleGemType.Water;
        public string displayName = "NƯỚC";
        public Color color = new Color(0.16f, 0.7f, 1f, 1f);
        public PuzzleQuestDamageTuning damage = new PuzzleQuestDamageTuning();

        [Tooltip("Animator state used when a line of exactly 3 is matched.")]
        public string skillUAnimation = "Cast Spell 01";

        [Tooltip("Animator state used when a line of exactly 4 is matched.")]
        public string skillIAnimation = "Cast Spell 02";

        [Tooltip("Animator state used when a line of 5 or more is matched.")]
        public string skillOAnimation = "Projectile Attack";

        public PuzzleSkillTier GetTier(int longestRun)
        {
            if (longestRun >= 5)
            {
                return PuzzleSkillTier.O;
            }

            return longestRun == 4 ? PuzzleSkillTier.I : PuzzleSkillTier.U;
        }

        public string GetAnimation(PuzzleSkillTier tier)
        {
            switch (tier)
            {
                case PuzzleSkillTier.O:
                    return skillOAnimation;
                case PuzzleSkillTier.I:
                    return skillIAnimation;
                default:
                    return skillUAnimation;
            }
        }
    }

    [Serializable]
    public sealed class PuzzleQuestTuning
    {
        [Header("Board")]
        [Range(5, 10)] public int columns = 8;
        [Range(5, 10)] public int rows = 8;
        [Tooltip("Puzzle Quest rules use 3 as the minimum; lines of 4 and 5 select I and O.")]
        public int minimumMatch = 3;
        public int randomSeed;
        [Min(8f)] public float cellSpacing = 10f;

        [Header("Battle")]
        [Min(1)] public int playerMaxHealth = 120;
        [Min(1)] public int enemyMaxHealth = 500;
        public PuzzleQuestDamageTuning swordDamage = new PuzzleQuestDamageTuning
        {
            match3Damage = 18,
            match4Damage = 30,
            match5Damage = 48,
            damagePerGemAfter5 = 8
        };

        public PuzzleQuestElementTuning water = new PuzzleQuestElementTuning
        {
            element = PuzzleGemType.Water,
            displayName = "NƯỚC",
            color = new Color(0.12f, 0.68f, 1f, 1f),
            damage = new PuzzleQuestDamageTuning
            {
                match3Damage = 16,
                match4Damage = 30,
                match5Damage = 48,
                damagePerGemAfter5 = 7
            }
        };

        public PuzzleQuestElementTuning fire = new PuzzleQuestElementTuning
        {
            element = PuzzleGemType.Fire,
            displayName = "LỬA",
            color = new Color(1f, 0.31f, 0.12f, 1f),
            damage = new PuzzleQuestDamageTuning
            {
                match3Damage = 22,
                match4Damage = 40,
                match5Damage = 64,
                damagePerGemAfter5 = 10
            }
        };

        public PuzzleQuestElementTuning wind = new PuzzleQuestElementTuning
        {
            element = PuzzleGemType.Wind,
            displayName = "GIÓ",
            color = new Color(0.3f, 0.91f, 0.48f, 1f),
            damage = new PuzzleQuestDamageTuning
            {
                match3Damage = 18,
                match4Damage = 34,
                match5Damage = 54,
                damagePerGemAfter5 = 8
            }
        };

        [Range(0f, 1f)] public float cascadeDamageBonus = 0.25f;
        [Range(0, 8)] public int extraTurnMatchLength = 4;
        [Min(0)] public int enemyDamageMin = 10;
        [Min(0)] public int enemyDamageMax = 16;
        [Min(0)] public int enemyAoeDamage = 20;
        [Min(0)] public int enemyAoeEveryTurns = 3;

        [Header("Timing")]
        [Min(0.01f)] public float swapDuration = 0.18f;
        [Min(0.01f)] public float invalidSwapDuration = 0.14f;
        [Min(0.01f)] public float matchPopDuration = 0.24f;
        [Min(0.01f)] public float fallDuration = 0.28f;
        [Min(0.01f)] public float actionAnticipation = 0.3f;
        [Min(0.01f)] public float actionHitDelay = 0.42f;
        [Min(0.01f)] public float actionRecovery = 0.35f;
        [Min(0.01f)] public float enemyTurnDelay = 0.5f;

        public PuzzleQuestElementTuning GetElement(PuzzleGemType type)
        {
            switch (type)
            {
                case PuzzleGemType.Fire:
                    return fire;
                case PuzzleGemType.Wind:
                    return wind;
                default:
                    return water;
            }
        }

        public void ClampValues()
        {
            columns = Mathf.Clamp(columns, 5, 10);
            rows = Mathf.Clamp(rows, 5, 10);
            minimumMatch = 3;
            playerMaxHealth = Mathf.Max(1, playerMaxHealth);
            enemyMaxHealth = Mathf.Max(1, enemyMaxHealth);
            enemyDamageMin = Mathf.Max(0, enemyDamageMin);
            enemyDamageMax = Mathf.Max(enemyDamageMin, enemyDamageMax);
            enemyAoeDamage = Mathf.Max(0, enemyAoeDamage);
        }
    }

    [CreateAssetMenu(
        fileName = "PuzzleQuestSettings",
        menuName = "Quoc/Puzzle Quest/Settings")]
    public sealed class PuzzleQuestSettings : ScriptableObject
    {
        public PuzzleQuestTuning tuning = new PuzzleQuestTuning();

        private void OnValidate()
        {
            if (tuning == null)
            {
                tuning = new PuzzleQuestTuning();
            }

            tuning.ClampValues();
        }
    }
}
