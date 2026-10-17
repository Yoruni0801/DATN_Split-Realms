using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Quản lý toàn bộ visual liên quan đến skill của player:
/// - Skill visual state (event OnSkillVisualStarted / Ended)
/// - Bow skin tạm thời khi dùng skill cung
/// - Projectile preview (ghost đạn khi charge)
/// </summary>
public class PlayerSkillVisualController
{
    // ── Skill Visual State ────────────────────────────────────────────────
    private SkillData activeVisualSkill;
    public SkillData ActiveVisualSkill => activeVisualSkill;

    public event Action<SkillData> OnSkillVisualStarted;
    public event Action<SkillData> OnSkillVisualEnded;

    // ── Bow Skin ──────────────────────────────────────────────────────────
    private SimpleSpineSkinAssigner skinAssigner;
    private string originalLeftHandWeaponSkin;
    private string originalRightHandWeaponSkin;
    private bool temporaryBowSkinApplied;

    // ── Projectile Preview ────────────────────────────────────────────────
    private GameObject activeSkillProjectilePreview;
    private GameObject activeSkillProjectilePreviewPrefab;

    public PlayerSkillVisualController(SimpleSpineSkinAssigner skinAssigner)
    {
        this.skinAssigner = skinAssigner;
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Hiển thị đầy đủ presentation skill: visual state + bow skin + animation được xử lý bên ngoài.
    /// </summary>
    public void BeginSkillVisual(SkillData skill)
    {
        if (skill == null || activeVisualSkill == skill)
        {
            return;
        }

        EndSkillVisual(activeVisualSkill);
        activeVisualSkill = skill;
        OnSkillVisualStarted?.Invoke(skill);
    }

    public void EndSkillVisual(SkillData skill)
    {
        if (activeVisualSkill == null)
        {
            return;
        }

        if (skill != null && activeVisualSkill != skill)
        {
            return;
        }

        SkillData endedSkill = activeVisualSkill;
        activeVisualSkill = null;
        OnSkillVisualEnded?.Invoke(endedSkill);
    }

    public void EndActiveSkillVisual() => EndSkillVisual(activeVisualSkill);

    // ── Bow Skin ──────────────────────────────────────────────────────────

    public void ShowBowSkin(string bowSkinName)
    {
        HideBowSkin();
        ApplyTemporaryBowSkin(bowSkinName);
    }

    public void HideBowSkin()
    {
        RestoreTemporaryBowSkin();
    }

    // ── Projectile Preview ────────────────────────────────────────────────

    public void ShowProjectilePreview(GameObject previewPrefab, Vector3 position, Quaternion rotation)
    {
        if (previewPrefab == null)
        {
            HideProjectilePreview();
            return;
        }

        if (activeSkillProjectilePreview == null || activeSkillProjectilePreviewPrefab != previewPrefab)
        {
            HideProjectilePreview();
            activeSkillProjectilePreviewPrefab = previewPrefab;
            activeSkillProjectilePreview = UnityEngine.Object.Instantiate(previewPrefab, position, rotation);
            activeSkillProjectilePreview.name = $"{previewPrefab.name}_ChargePreview";
            ConfigurePreviewObject(activeSkillProjectilePreview);
        }

        activeSkillProjectilePreview.transform.SetPositionAndRotation(position, rotation);
    }

    public void HideProjectilePreview()
    {
        if (activeSkillProjectilePreview != null)
        {
            UnityEngine.Object.Destroy(activeSkillProjectilePreview);
        }

        activeSkillProjectilePreview = null;
        activeSkillProjectilePreviewPrefab = null;
    }

    /// <summary>Dọn dẹp tất cả visual khi player bị disable/destroy.</summary>
    public void Cleanup()
    {
        EndActiveSkillVisual();
        HideBowSkin();
        HideProjectilePreview();
    }

    // ── Private Helpers ───────────────────────────────────────────────────

    private void ApplyTemporaryBowSkin(string bowSkinName)
    {
        if (string.IsNullOrWhiteSpace(bowSkinName) || skinAssigner == null)
        {
            return;
        }

        if (!temporaryBowSkinApplied)
        {
            originalLeftHandWeaponSkin = skinAssigner.leftHandWeaponSkin;
            originalRightHandWeaponSkin = skinAssigner.rightHandWeaponSkin;
            temporaryBowSkinApplied = true;
        }

        ApplyWeaponSlot(bowSkinName);
        ApplySkinAssigner();
    }

    private void RestoreTemporaryBowSkin()
    {
        if (!temporaryBowSkinApplied || skinAssigner == null)
        {
            return;
        }

        skinAssigner.leftHandWeaponSkin = originalLeftHandWeaponSkin;
        skinAssigner.rightHandWeaponSkin = originalRightHandWeaponSkin;
        temporaryBowSkinApplied = false;
        ApplySkinAssigner();
    }

    private void ApplyWeaponSlot(string weaponSkinName)
    {
        if (weaponSkinName.StartsWith("RIGHTHAND/", StringComparison.OrdinalIgnoreCase))
        {
            skinAssigner.rightHandWeaponSkin = weaponSkinName;
            return;
        }

        if (weaponSkinName.StartsWith("LEFTHAND/", StringComparison.OrdinalIgnoreCase))
        {
            skinAssigner.leftHandWeaponSkin = weaponSkinName;
            return;
        }

        skinAssigner.leftHandWeaponSkin = weaponSkinName;
    }

    private void ApplySkinAssigner()
    {
        InvokeSkinAssignerMethod("InitializeSkeleton");
        InvokeSkinAssignerMethod("AssignSkins");
        InvokeSkinAssignerMethod("UpdateSkeletonPose");
    }

    private void InvokeSkinAssignerMethod(string methodName)
    {
        MethodInfo methodInfo = typeof(SimpleSpineSkinAssigner).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        methodInfo?.Invoke(skinAssigner, null);
    }

    private static void ConfigurePreviewObject(GameObject preview)
    {
        if (preview == null)
        {
            return;
        }

        foreach (Collider2D col in preview.GetComponentsInChildren<Collider2D>(true))
        {
            col.enabled = false;
        }

        foreach (Rigidbody2D body in preview.GetComponentsInChildren<Rigidbody2D>(true))
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }

        foreach (PlayerMagicProjectile2D projectile in preview.GetComponentsInChildren<PlayerMagicProjectile2D>(true))
        {
            projectile.enabled = false;
        }

        foreach (AudioSource audioSource in preview.GetComponentsInChildren<AudioSource>(true))
        {
            audioSource.Stop();
            audioSource.enabled = false;
        }
    }
}
