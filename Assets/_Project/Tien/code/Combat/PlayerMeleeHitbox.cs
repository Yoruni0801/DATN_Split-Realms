using System;
using System.Collections;
using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PlayerMeleeHitbox : MonoBehaviour
{
    private enum HandHitbox
    {
        Left,
        Right,
        Both
    }

    [System.Serializable]
    private class SkillColliderProfile
    {
        public string profileName = string.Empty;
        public GameObject colliderObject = null;
    }

    [SerializeField] private GameObject leftBoundingBoxObject;
    [SerializeField] private GameObject rightBoundingBoxObject;
    [SerializeField] private List<SkillColliderProfile> skillColliderProfiles = new List<SkillColliderProfile>();
    [SerializeField] private SimpleSpineSkinAssigner skinAssigner;

    [SerializeField] private bool preferEquippedWeaponHand = true;
    [SerializeField] private bool useFacingWhenBothHandsAvailable = true;
    [SerializeField] private bool invertFacingHandSelection;

    [SerializeField] private bool debugLogs = true;
    [SerializeField] private bool showGizmos = true;
    private Color idleGizmoColor = new Color(0.2f, 0.8f, 1f, 0.45f);
    private Color activeGizmoColor = new Color(1f, 0.3f, 0.2f, 0.75f);

    private readonly HashSet<int> hitTargetIds = new HashSet<int>();
    private readonly List<Collider2D> activeSkillColliders = new List<Collider2D>();
    private readonly Collider2D[] overlapBuffer = new Collider2D[16];
    private ContactFilter2D attackTargetFilter;

    private Player owner;
    private Collider2D hitboxCollider;
    private Collider2D[] leftBoundingBoxColliders;
    private Collider2D[] rightBoundingBoxColliders;
    private BoundingBoxFollower[] leftBoundingBoxFollowers;
    private BoundingBoxFollower[] rightBoundingBoxFollowers;
    private BoneFollower[] leftBoneFollowers;
    private BoneFollower[] rightBoneFollowers;
    private Coroutine attackRoutine;
    private GameObject activeSkillColliderObject;
    private int currentDamage;
    private bool isActive;
    private bool processDamage;
    private bool hasBoundingBoxObjects;
    private Action<Collider2D> activeHitCallback;

    private void Awake()
    {
        hitboxCollider = GetComponent<Collider2D>();
        BuildAttackTargetFilter();
        CacheBoundingBoxObjects();
        SetAllBoundingBoxCollidersActive(false);
        SetAllSkillProfileCollidersActive(false);

        if (!hasBoundingBoxObjects && hitboxCollider != null)
        {
            hitboxCollider.isTrigger = true;
            hitboxCollider.enabled = false;
        }
    }

    public void Configure(Player player)
    {
        owner = player;
        hitTargetIds.Clear();
        isActive = false;
        currentDamage = 0;
        processDamage = false;

        if (hitboxCollider == null)
        {
            hitboxCollider = GetComponent<Collider2D>();
        }

        BuildAttackTargetFilter();

        if (skinAssigner == null)
        {
            skinAssigner = player != null ? player.GetComponent<SimpleSpineSkinAssigner>() : GetComponentInParent<SimpleSpineSkinAssigner>();
        }

        CacheBoundingBoxObjects();
        SetAllBoundingBoxCollidersActive(false);
        SetAllSkillProfileCollidersActive(false);

        if (!hasBoundingBoxObjects && hitboxCollider != null)
        {
            hitboxCollider.isTrigger = true;
            hitboxCollider.enabled = false;
        }
    }

    public void TriggerAttack(int damage, float startupDelay, float activeDuration, float animationSpeed = 1f)
    {
        if (owner == null || damage <= 0)
        {
            return;
        }

        StopAttackRoutineAndResetHitboxes();

        float timingScale = 1f / Mathf.Max(0.01f, animationSpeed);

        attackRoutine = StartCoroutine(AttackRoutine(
            damage,
            Mathf.Max(0f, startupDelay) * timingScale,
            Mathf.Max(0f, activeDuration) * timingScale,
            processHits: true,
            profileObject: null,
            useProfileObject: false,
            colliderAttachmentName: null,
            colliderIndex: -1));
    }

    public void TriggerSkillCollider(SkillData skill, Action<Collider2D> onHit = null)
    {
        if (owner == null || skill == null || !skill.SkillColliderEnabled)
        {
            return;
        }

        StopAttackRoutineAndResetHitboxes();
        activeHitCallback = onHit;

        GameObject profileObject = ResolveSkillColliderObject(skill.ColliderProfileName);
        bool useProfileObject = profileObject != null;
        int damage = Mathf.RoundToInt(skill.Damage);
        attackRoutine = StartCoroutine(AttackRoutine(
            damage,
            skill.ColliderStartupDelay,
            skill.ColliderActiveDuration,
            skill.ColliderDealsDamage,
            profileObject,
            useProfileObject,
            skill.ColliderAttachmentName,
            skill.ColliderIndex));
    }

    private IEnumerator AttackRoutine(
        int damage,
        float startup,
        float active,
        bool processHits,
        GameObject profileObject,
        bool useProfileObject,
        string colliderAttachmentName,
        int colliderIndex)
    {
        hitTargetIds.Clear();
        currentDamage = damage;
        processDamage = processHits;
        isActive = false;

        if (hasBoundingBoxObjects)
        {
            SetAllBoundingBoxCollidersActive(false);
        }
        else if (hitboxCollider != null)
        {
            hitboxCollider.enabled = false;
        }

        SetSkillColliderObjectActive(profileObject, false);
        activeSkillColliders.Clear();
        activeSkillColliderObject = useProfileObject ? profileObject : null;

        if (startup > 0f)
        {
            yield return new WaitForSeconds(startup);
        }

        if (useProfileObject)
        {
            bool activated = ActivateSkillProfileCollider(profileObject, colliderAttachmentName, colliderIndex);
            if (!activated && debugLogs)
            {
                Debug.LogWarning(
                    $"[PlayerMeleeHitbox] Skill collider profile '{profileObject.name}' has no matching collider. Attachment='{colliderAttachmentName}', Index={colliderIndex}.",
                    profileObject);
            }

            Physics2D.SyncTransforms();
        }
        else if (hasBoundingBoxObjects)
        {
            HandHitbox activeHand = ResolveActiveHandHitbox();
            SetBoundingBoxCollidersActive(activeHand, true);
            Physics2D.SyncTransforms();
        }
        else if (hitboxCollider != null)
        {
            hitboxCollider.enabled = true;
        }

        isActive = true;
        ProcessCurrentOverlaps();

        if (active > 0f)
        {
            float elapsed = 0f;
            while (elapsed < active)
            {
                ProcessCurrentOverlaps();
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        if (hasBoundingBoxObjects)
        {
            SetAllBoundingBoxCollidersActive(false);
        }
        else if (hitboxCollider != null)
        {
            hitboxCollider.enabled = false;
        }

        SetSkillColliderObjectActive(profileObject, false);
        activeSkillColliders.Clear();
        activeSkillColliderObject = null;
        isActive = false;
        currentDamage = 0;
        processDamage = false;
        activeHitCallback = null;
        attackRoutine = null;
    }

    private void OnDisable()
    {
        StopAttackRoutineAndResetHitboxes();
    }

    private void StopAttackRoutineAndResetHitboxes()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        if (hasBoundingBoxObjects)
        {
            SetAllBoundingBoxCollidersActive(false);
        }
        else if (hitboxCollider != null)
        {
            hitboxCollider.enabled = false;
        }

        SetAllSkillProfileCollidersActive(false);
        activeSkillColliders.Clear();
        activeSkillColliderObject = null;
        isActive = false;
        currentDamage = 0;
        processDamage = false;
        activeHitCallback = null;
        hitTargetIds.Clear();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        TryProcessHit(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        TryProcessHit(collision);
    }

    private void TryProcessHit(Collider2D collision)
    {
        if (!isActive || !processDamage || owner == null || collision == null)
        {
            return;
        }

        if (collision.transform.root == owner.transform.root)
        {
            return;
        }

        if (PlayerHitResolver.IsThreatSensorCollider(collision))
        {
            return;
        }

        int targetId = ResolveTargetId(collision);
        if (targetId == 0)
        {
            return;
        }

        if (hitTargetIds.Contains(targetId))
        {
            return;
        }

        bool wasHit = false;

        InazumaCubeHit cube = collision.GetComponentInParent<InazumaCubeHit>();
        if (cube != null)
        {
            if (debugLogs)
            {
                Debug.Log($"[PlayerMeleeHitbox] Melee hit Inazuma cube '{cube.name}'.", cube);
            }

            cube.OnMeleeHit();
            wasHit = true;
        }
        else if (PlayerHitResolver.TryApplyDamageToEnemy(collision, currentDamage, owner.transform))
        {
            wasHit = true;

            var baoEnemy = collision.GetComponentInParent<Bao.Enemies.BaoEnemyBase>();
            if (baoEnemy != null)
            {
                baoEnemy.ApplyKnockback(transform, 5f); // 5f is the knockback force
            }

            if (CombatFeedbackManager.Instance != null)
            {
                CombatFeedbackManager.Instance.PlayHitFeedback(collision.ClosestPoint(transform.position), false);
            }
        }

        if (wasHit)
        {
            hitTargetIds.Add(targetId);
            activeHitCallback?.Invoke(collision);
        }
    }

    private void ProcessCurrentOverlaps()
    {
        if (!isActive)
        {
            return;
        }

        if (activeSkillColliderObject != null)
        {
            EnforceActiveSkillColliders();
            ProcessColliderOverlaps(activeSkillColliders);
            return;
        }

        if (hasBoundingBoxObjects)
        {
            ProcessColliderOverlaps(leftBoundingBoxColliders);
            ProcessColliderOverlaps(rightBoundingBoxColliders);
            return;
        }

        if (hitboxCollider == null)
        {
            return;
        }

        ProcessColliderOverlaps(hitboxCollider);
    }

    private void ProcessColliderOverlaps(Collider2D[] colliders)
    {
        if (colliders == null)
        {
            return;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            ProcessColliderOverlaps(colliders[i]);
        }
    }

    private void ProcessColliderOverlaps(List<Collider2D> colliders)
    {
        if (colliders == null)
        {
            return;
        }

        for (int i = 0; i < colliders.Count; i++)
        {
            ProcessColliderOverlaps(colliders[i]);
        }
    }

    private void ProcessColliderOverlaps(Collider2D sourceCollider)
    {
        if (sourceCollider == null || !sourceCollider.enabled || !sourceCollider.gameObject.activeInHierarchy)
        {
            return;
        }

        int overlapCount = sourceCollider.Overlap(attackTargetFilter, overlapBuffer);

        for (int i = 0; i < overlapCount; i++)
        {
            TryProcessHit(overlapBuffer[i]);
        }
    }

    private static int ResolveTargetId(Collider2D collision)
    {
        if (collision == null)
        {
            return 0;
        }

        InazumaCubeHit cube = collision.GetComponentInParent<InazumaCubeHit>();
        if (cube != null)
        {
            return cube.GetInstanceID();
        }

        Transform root = collision.transform.root;
        return root != null ? root.GetInstanceID() : collision.GetInstanceID();
    }

    private static string BuildTargetLabel(Collider2D collision)
    {
        if (collision == null)
        {
            return "<null>";
        }

        Transform root = collision.transform.root;
        string rootName = root != null ? root.name : collision.name;
        return $"{collision.name} (root: {rootName}, tag: {collision.tag}, layer: {LayerMask.LayerToName(collision.gameObject.layer)})";
    }

    private void CacheBoundingBoxObjects()
    {
        if (leftBoundingBoxObject == null)
        {
            Transform left = transform.root.Find("boundingboxleft");
            if (left != null)
            {
                leftBoundingBoxObject = left.gameObject;
            }
        }

        if (rightBoundingBoxObject == null)
        {
            Transform right = transform.root.Find("boudingboxright");
            if (right == null)
            {
                right = transform.root.Find("boundingboxright");
            }

            if (right != null)
            {
                rightBoundingBoxObject = right.gameObject;
            }
        }

        leftBoundingBoxColliders = GetHandColliders(leftBoundingBoxObject);
        rightBoundingBoxColliders = GetHandColliders(rightBoundingBoxObject);
        leftBoundingBoxFollowers = GetHandComponents<BoundingBoxFollower>(leftBoundingBoxObject);
        rightBoundingBoxFollowers = GetHandComponents<BoundingBoxFollower>(rightBoundingBoxObject);
        leftBoneFollowers = GetHandComponents<BoneFollower>(leftBoundingBoxObject);
        rightBoneFollowers = GetHandComponents<BoneFollower>(rightBoundingBoxObject);
        hasBoundingBoxObjects = leftBoundingBoxObject != null || rightBoundingBoxObject != null;
    }

    private static Collider2D[] GetHandColliders(GameObject boxObject)
    {
        if (boxObject == null)
        {
            return null;
        }

        Collider2D[] colliders = boxObject.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].isTrigger = true;
                colliders[i].enabled = false;
            }
        }

        return colliders;
    }

    private static T[] GetHandComponents<T>(GameObject boxObject) where T : Component
    {
        return boxObject != null ? boxObject.GetComponentsInChildren<T>(true) : null;
    }

    private GameObject ResolveSkillColliderObject(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return null;
        }

        string normalizedProfile = profileName.Trim();
        if (string.Equals(normalizedProfile, "Left", System.StringComparison.OrdinalIgnoreCase))
        {
            return leftBoundingBoxObject;
        }

        if (string.Equals(normalizedProfile, "Right", System.StringComparison.OrdinalIgnoreCase))
        {
            return rightBoundingBoxObject;
        }

        for (int i = 0; i < skillColliderProfiles.Count; i++)
        {
            SkillColliderProfile profile = skillColliderProfiles[i];
            if (profile == null || profile.colliderObject == null || string.IsNullOrWhiteSpace(profile.profileName))
            {
                continue;
            }

            if (string.Equals(profile.profileName.Trim(), normalizedProfile, System.StringComparison.OrdinalIgnoreCase))
            {
                return profile.colliderObject;
            }
        }

        Transform child = transform.root.Find(normalizedProfile);
        return child != null ? child.gameObject : null;
    }

    private void SetSkillColliderObjectActive(GameObject profileObject, bool active)
    {
        if (profileObject == null)
        {
            return;
        }

        SetHandColliderActive(
            profileObject,
            GetHandColliders(profileObject),
            GetHandComponents<BoundingBoxFollower>(profileObject),
            GetHandComponents<BoneFollower>(profileObject),
            active);
    }

    private bool ActivateSkillProfileCollider(GameObject profileObject, string attachmentName, int colliderIndex)
    {
        if (profileObject == null)
        {
            return false;
        }

        Collider2D[] profileColliders = GetHandColliders(profileObject);
        BoundingBoxFollower[] boundingBoxFollowers = GetHandComponents<BoundingBoxFollower>(profileObject);
        BoneFollower[] boneFollowers = GetHandComponents<BoneFollower>(profileObject);

        SetHandColliderActive(profileObject, profileColliders, boundingBoxFollowers, boneFollowers, false);
        if (!profileObject.activeSelf)
        {
            profileObject.SetActive(true);
        }

        InitializeFollowers(boundingBoxFollowers, boneFollowers);
        activeSkillColliders.Clear();

        bool matched = TryActivateColliderByAttachmentName(boundingBoxFollowers, attachmentName);
        if (!matched)
        {
            matched = TryActivateColliderByIndex(profileColliders, colliderIndex);
        }

        if (!matched)
        {
            DisableColliders(profileColliders);
            profileObject.SetActive(false);
            return false;
        }

        EnforceActiveSkillColliders();
        return true;
    }

    private bool TryActivateColliderByAttachmentName(BoundingBoxFollower[] boundingBoxFollowers, string attachmentName)
    {
        if (boundingBoxFollowers == null || string.IsNullOrWhiteSpace(attachmentName))
        {
            return false;
        }

        string normalizedAttachment = attachmentName.Trim();
        bool matched = false;

        for (int i = 0; i < boundingBoxFollowers.Length; i++)
        {
            BoundingBoxFollower follower = boundingBoxFollowers[i];
            if (follower == null)
            {
                continue;
            }

            follower.Initialize(true);

            foreach (var item in follower.nameTable)
            {
                if (!string.Equals(item.Value, normalizedAttachment, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (follower.colliderTable.TryGetValue(item.Key, out PolygonCollider2D collider) && collider != null)
                {
                    collider.isTrigger = true;
                    if (!activeSkillColliders.Contains(collider))
                    {
                        activeSkillColliders.Add(collider);
                    }

                    matched = true;
                }
            }
        }

        return matched;
    }

    private bool TryActivateColliderByIndex(Collider2D[] profileColliders, int colliderIndex)
    {
        if (profileColliders == null || colliderIndex < 0 || colliderIndex >= profileColliders.Length)
        {
            return false;
        }

        Collider2D collider = profileColliders[colliderIndex];
        if (collider == null)
        {
            return false;
        }

        collider.isTrigger = true;
        activeSkillColliders.Add(collider);
        return true;
    }

    private void EnforceActiveSkillColliders()
    {
        if (activeSkillColliderObject == null)
        {
            return;
        }

        if (!activeSkillColliderObject.activeSelf)
        {
            activeSkillColliderObject.SetActive(true);
        }

        Collider2D[] profileColliders = activeSkillColliderObject.GetComponentsInChildren<Collider2D>(true);
        DisableColliders(profileColliders);

        for (int i = 0; i < activeSkillColliders.Count; i++)
        {
            Collider2D collider = activeSkillColliders[i];
            if (collider == null)
            {
                continue;
            }

            collider.isTrigger = true;
            collider.enabled = true;
        }
    }

    private static void DisableColliders(Collider2D[] colliders)
    {
        if (colliders == null)
        {
            return;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
            {
                continue;
            }

            colliders[i].isTrigger = true;
            colliders[i].enabled = false;
        }
    }

    private void SetAllSkillProfileCollidersActive(bool active)
    {
        if (skillColliderProfiles == null)
        {
            return;
        }

        for (int i = 0; i < skillColliderProfiles.Count; i++)
        {
            SkillColliderProfile profile = skillColliderProfiles[i];
            if (profile == null || profile.colliderObject == null)
            {
                continue;
            }

            SetSkillColliderObjectActive(profile.colliderObject, active);
        }
    }

    private HandHitbox ResolveActiveHandHitbox()
    {
        bool hasLeftWeapon = skinAssigner != null && IsRealWeaponSkin(skinAssigner.leftHandWeaponSkin);
        bool hasRightWeapon = skinAssigner != null && IsRealWeaponSkin(skinAssigner.rightHandWeaponSkin);

        if (preferEquippedWeaponHand)
        {
            if (hasLeftWeapon && !hasRightWeapon)
            {
                return HandHitbox.Left;
            }

            if (hasRightWeapon && !hasLeftWeapon)
            {
                return HandHitbox.Right;
            }
        }

        if (!useFacingWhenBothHandsAvailable)
        {
            return HandHitbox.Both;
        }

        bool facingRight = owner != null && owner.FacingDirectionSign >= 0f;
        if (invertFacingHandSelection)
        {
            facingRight = !facingRight;
        }

        return facingRight ? HandHitbox.Right : HandHitbox.Left;
    }

    private static bool IsRealWeaponSkin(string skinName)
    {
        return !string.IsNullOrWhiteSpace(skinName)
            && !string.Equals(skinName.Trim(), "none", System.StringComparison.OrdinalIgnoreCase);
    }

    private void BuildAttackTargetFilter()
    {
        attackTargetFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = PlayerLayerConfig.AttackTargetLayer,
            useTriggers = true
        };
    }

    private void SetBoundingBoxCollidersActive(HandHitbox hand, bool active)
    {
        SetHandColliderActive(leftBoundingBoxObject, leftBoundingBoxColliders, leftBoundingBoxFollowers, leftBoneFollowers,
            active && (hand == HandHitbox.Left || hand == HandHitbox.Both));
        SetHandColliderActive(rightBoundingBoxObject, rightBoundingBoxColliders, rightBoundingBoxFollowers, rightBoneFollowers,
            active && (hand == HandHitbox.Right || hand == HandHitbox.Both));
    }

    private void SetAllBoundingBoxCollidersActive(bool active)
    {
        SetHandColliderActive(leftBoundingBoxObject, leftBoundingBoxColliders, leftBoundingBoxFollowers, leftBoneFollowers, active);
        SetHandColliderActive(rightBoundingBoxObject, rightBoundingBoxColliders, rightBoundingBoxFollowers, rightBoneFollowers, active);
    }

    private static void SetHandColliderActive(
        GameObject boxObject,
        Collider2D[] colliders,
        BoundingBoxFollower[] boundingBoxFollowers,
        BoneFollower[] boneFollowers,
        bool colliderActive)
    {
        if (boxObject == null)
        {
            return;
        }

        if (!colliderActive)
        {
            if (colliders != null)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] == null)
                    {
                        continue;
                    }

                    colliders[i].isTrigger = true;
                    colliders[i].enabled = false;
                }
            }

            if (boxObject.activeSelf)
            {
                boxObject.SetActive(false);
            }

            return;
        }

        if (!boxObject.activeSelf)
        {
            boxObject.SetActive(true);
        }

        InitializeFollowers(boundingBoxFollowers, boneFollowers);

        if (colliders == null)
        {
            return;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
            {
                continue;
            }

            colliders[i].isTrigger = true;
            colliders[i].enabled = colliderActive;
        }
    }

    private static void InitializeFollowers(
        BoundingBoxFollower[] boundingBoxFollowers,
        BoneFollower[] boneFollowers)
    {
        if (boneFollowers != null)
        {
            for (int i = 0; i < boneFollowers.Length; i++)
            {
                if (boneFollowers[i] == null)
                {
                    continue;
                }

                boneFollowers[i].Initialize();
                boneFollowers[i].LateUpdate();
            }
        }

        if (boundingBoxFollowers != null)
        {
            for (int i = 0; i < boundingBoxFollowers.Length; i++)
            {
                boundingBoxFollowers[i]?.Initialize(true);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
        {
            return;
        }

        Collider2D targetCollider = hitboxCollider != null ? hitboxCollider : GetComponent<Collider2D>();
        if (targetCollider == null)
        {
            return;
        }

        Bounds bounds = targetCollider.bounds;
        Gizmos.color = isActive ? activeGizmoColor : idleGizmoColor;
        Gizmos.DrawWireCube(bounds.center, bounds.size);

        Color fillColor = Gizmos.color;
        fillColor.a *= 0.2f;
        Gizmos.color = fillColor;
        Gizmos.DrawCube(bounds.center, bounds.size);
    }
}
