using System.Collections.Generic;
using UnityEngine;
using System;

// cái này là tổng hợp skill của một hệ. để gắn vô player

[CreateAssetMenu(fileName = "SkillSet", menuName = "Duy/Game Data/Skill Set")]
public class SkillSetSO : ScriptableObject
{
    [Serializable]
    public class SkillSlot
    {
        [SerializeField] private SkillData skillData;
        [SerializeField] private SkillExecutionConfig executionConfig;
        [SerializeField] private AudioClip skillSound;
        [SerializeField] private KeyCode activationKey = KeyCode.None;

        public SkillData SkillData => skillData;
        public SkillExecutionConfig ExecutionConfig => executionConfig;
        public AudioClip SkillSound => skillSound;
        public KeyCode ActivationKey => activationKey;
        public bool IsValid => skillData != null;
    }

    [Header("Dash")]
    public Color dashGhostColor = new Color(1f, 1f, 1f, 0.5f);
    public GameObject swimDashVfxOverride;

    [Header("Skills")]
    [SerializeField] private List<SkillSlot> skills = new List<SkillSlot>();

    public int SkillCount => skills?.Count ?? 0;

    public bool TryGetSkill(int index, out SkillData skill)
    {
        if (TryGetSkillSlot(index, out SkillSlot slot))
        {
            skill = slot.SkillData;
            return skill != null;
        }

        skill = null;
        return false;
    }

    public bool TryGetSkillSlot(int index, out SkillSlot slot)
    {
        slot = null;
        if (skills == null || index < 0 || index >= skills.Count) return false;
        slot = skills[index];
        return slot != null && slot.IsValid;
    }

    public AudioClip GetSkillSound(int index)
    {
        return TryGetSkillSlot(index, out SkillSlot slot) ? slot.SkillSound : null;
    }
}
