using UnityEngine;
using UnityEngine.Serialization;

public enum SkillSpawnRotationMode
{
    FaceDirection = 0,
    KeepPrefabRotation = 1
}

[CreateAssetMenu(fileName = "Skill", menuName = "Duy/Game Data/Skill/Skill Data")]
public class SkillData : ScriptableObject
{
    [SerializeField] private SkillElementType elementType;
    [SerializeField] private SkillAttackType attackType;
    [SerializeField] private SkillSpawnRotationMode spawnRotationMode = SkillSpawnRotationMode.FaceDirection;
    [SerializeField] private AnimationSpine.State animationName = AnimationSpine.State.Skill;
    [SerializeField] private string bowSkinName;
    [SerializeField] private string skillName;

    [Header("Core")]
    [SerializeField] private float damage = 50f;
    [SerializeField] private float cooldown = 1f;
    [SerializeField] private float manaCost;
    [SerializeField] private float pullPoseTime = 0.35f;
    [SerializeField] private Sprite icon;

    [SerializeField] private float spawnRotationOffset;

    [Header("Collider")]
    [SerializeField] private bool skillColliderEnabled;
    [SerializeField] private string colliderProfileName;
    [SerializeField] private string colliderAttachmentName;
    [SerializeField] private int colliderIndex = -1;
    [SerializeField, Min(0f)] private float colliderStartupDelay;
    [SerializeField, Min(0f)] private float colliderActiveDuration = 0.3f;
    [SerializeField] private bool colliderDealsDamage;

    [TextArea]
    [SerializeField] private string description;
    public string SkillName => string.IsNullOrEmpty(skillName) ? name : skillName;
    public SkillElementType ElementType => elementType;
    public SkillAttackType AttackType => attackType;
    public float Damage => damage;
    public float Cooldown => cooldown;
    public float ManaCost => manaCost;
    public float PullPoseTime => pullPoseTime;
    public AnimationSpine.State AnimationName => animationName;
    public string BowSkinName => bowSkinName;
    public Sprite Icon => icon;
    public string Description => description;
    public SkillSpawnRotationMode SpawnRotationMode => spawnRotationMode;
    public float SpawnRotationOffset => spawnRotationOffset;
    public bool SkillColliderEnabled => skillColliderEnabled;
    public string ColliderProfileName => colliderProfileName;
    public string ColliderAttachmentName => colliderAttachmentName;
    public int ColliderIndex => colliderIndex;
    public float ColliderStartupDelay => colliderStartupDelay;
    public float ColliderActiveDuration => colliderActiveDuration;
    public bool ColliderDealsDamage => colliderDealsDamage;
}
