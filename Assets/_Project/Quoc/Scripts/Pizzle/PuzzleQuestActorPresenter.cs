using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Quoc.PuzzleQuest
{
    [Serializable]
    public sealed class PuzzleQuestActorLayout
    {
        [Header("Render Texture")]
        [Min(256)] public int textureWidth = 1280;
        [Min(128)] public int textureHeight = 384;
        [Range(1, 8)] public int antiAliasing = 2;
        [Range(0, 31)] public int renderLayer = 30;

        [Header("Stage Camera")]
        public Vector3 isolatedWorldOrigin = new Vector3(10000f, 10000f, 0f);
        public Vector3 cameraPosition = new Vector3(0f, 0.15f, -20f);
        [Min(0.1f)] public float orthographicSize = 3.25f;
        public Color clearColor = new Color(0f, 0f, 0f, 0f);
        public Color lightColor = new Color(1f, 0.88f, 0.72f, 1f);
        [Min(0f)] public float lightIntensity = 1.35f;
        public string actorSortingLayer = "1";
        public Color light2DColor = Color.white;
        [Min(0f)] public float light2DIntensity = 1f;

        [Header("Player")]
        [Tooltip("Point A: position used when the battle starts and after an attack finishes.")]
        public Vector3 playerPosition = new Vector3(-4.1f, -1.05f, 0f);
        [Tooltip("Point B: position used by attacks configured to step forward before hitting.")]
        public Vector3 playerAttackPosition = new Vector3(-1.3f, -1.05f, 0f);
        public Vector3 playerEulerAngles = Vector3.zero;
        public Vector3 playerScale = Vector3.one;
        public bool playerSwordMovesToPointB = true;
        public bool playerWaterMovesToPointB;
        public bool playerFireMovesToPointB;
        public bool playerWindMovesToPointB;

        [Header("Element Fairies")]
        public Vector3 waterPosition = new Vector3(-1.8f, 0.45f, -0.15f);
        public Vector3 waterEulerAngles = Vector3.zero;
        public Vector3 waterScale = Vector3.one;
        public Vector3 firePosition = new Vector3(-1.8f, 0.45f, -0.15f);
        public Vector3 fireEulerAngles = Vector3.zero;
        public Vector3 fireScale = Vector3.one;
        public Vector3 windPosition = new Vector3(-1.8f, 0.45f, -0.15f);
        public Vector3 windEulerAngles = Vector3.zero;
        public Vector3 windScale = Vector3.one;

        [Header("Enemy")]
        [Tooltip("Point A: position used when the battle starts and after an attack finishes.")]
        public Vector3 enemyPosition = new Vector3(3.75f, -0.55f, 0f);
        [Tooltip("Point B: position used by enemy attacks configured to step forward before hitting.")]
        public Vector3 enemyAttackPosition = new Vector3(1.35f, -0.55f, 0f);
        public Vector3 enemyEulerAngles = Vector3.zero;
        public Vector3 enemyScale = Vector3.one;
        public bool enemyNormalAttackMovesToPointB = true;
        public bool enemyAoeMovesToPointB;
        public string enemyWalkState = "Walk Monolith";

        [Header("Presentation")]
        [Min(0.01f)] public float moveToAttackDuration = 0.32f;
        [Min(0.01f)] public float returnToPointADuration = 0.28f;
        [Min(0.01f)] public float hitShakeDuration = 0.14f;
        [Min(0f)] public float hitShakeDistance = 0.09f;
        [Min(0.01f)] public float impactTrailDuration = 0.16f;
    }

    [Serializable]
    public class PuzzleQuestBossSkillVfxSet
    {
        [Tooltip("Hiệu ứng nổ hạt khi mảnh đá trúng mục tiêu (Rock Barrage Hit VFX)")]
        public GameObject rockHitVfx;

        [Tooltip("Hiệu ứng bộc phát tại tâm Boss khi bắt đầu chiêu Vành Đai Cổ Thạch (Orbit AOE Cast VFX)")]
        public GameObject orbitCastVfx;

        [Tooltip("Hiệu ứng nổ diện rộng tại mục tiêu khi chiêu Vành Đai trúng đích (Orbit AOE Impact VFX)")]
        public GameObject orbitImpactVfx;

        [Tooltip("Hiệu ứng địa chấn dưới chân Boss khi Hóa Đá bảng ngọc (Earthquake / Petrify VFX)")]
        public GameObject earthquakeVfx;

        [Tooltip("Hiệu ứng khiên bảo vệ quanh thân Boss khi bật Khiên Cổ Thạch (Stone Aegis Shield VFX)")]
        public GameObject shieldVfx;

        [Tooltip("Hiệu ứng bộc phát năng lượng khi chuyển Phase (Phase Transition Blast VFX)")]
        public GameObject phaseTransitionVfx;
    }

    [DisallowMultipleComponent]
    public sealed class PuzzleQuestActorPresenter : MonoBehaviour
    {
        private const string FairyIdleState = "Idle";
        private const string FairySkillUState = "Cast Spell 01";
        private const string FairySkillIState = "Cast Spell 02";
        private const string FairySkillOState = "Projectile Attack";
        private const string EnemyIdleState = "Idle Monolith";
        private const string EnemyAttackState = "Attack Monolith";
        private const string EnemyAoeState = "LithosMonolith_SkillAOE";
        private const string EnemyDeathState = "Death Monolith";
        private const string WaterBowSkin = "RIGHTHAND/Bow_Rare1";
        private const string FireSpearSkin = "RIGHTHAND/Spear_OneHand_Common1";
        private const string WindStaffSkin = "RIGHTHAND/Staff_Common1";
        private const float EnemyDeathFallbackDuration = 2.2667f;
        private static readonly FieldInfo Light2DAffectedSortingLayersField =
            typeof(Light2D).GetField("m_ApplyToSortingLayers", BindingFlags.Instance | BindingFlags.NonPublic);

        private sealed class ActorHandle
        {
            public GameObject Root;
            public Animator Animator;
            public AnimationSpine Spine;
            public SimpleSpineSkinAssigner SkinAssigner;
            public SkeletonRendererCustomMaterialsIgnoreSlotAlpha CustomMaterials;
            public FairyElementVfxController FairyElement;
            public PlayerAttackVfxController AttackVfx;
            public Bao.Bosses.LithosMonolithShaderController LithosShader;
            public Bao.Bosses.LithosMonolithBoss LithosBoss;
            public Vector3 BaseLocalPosition;
            public Coroutine ReactionRoutine;
            public Coroutine ShakeRoutine;
            public Coroutine DeathRoutine;

            public bool IsAvailable
            {
                get { return Root != null; }
            }
        }

        private readonly Dictionary<PuzzleGemType, ActorHandle> fairyActors =
            new Dictionary<PuzzleGemType, ActorHandle>();
        private readonly List<GameObject> spawnedActors = new List<GameObject>();

        private PuzzleQuestActorLayout actorLayout;
        private RawImage targetImage;
        private GameObject stageRoot;
        private GameObject inactiveSpawnRoot;
        private Camera stageCamera;
        private RenderTexture battleTexture;
        private LineRenderer impactTrail;
        private Material impactMaterial;
        private ActorHandle playerActor;
        private ActorHandle enemyActor;
        private PlayerSkillVisualController playerSkillVisual;
        private GameObject swordSkillVfxPrefab;
        private GameObject waterSkillVfxPrefab;
        private GameObject fireSkillVfxPrefab;
        private GameObject windSkillVfxPrefab;
        private bool playerDead;
        private bool enemyDead;
        private bool disposing;
        private int actorSortingLayerId;

        public bool IsInitialized
        {
            get { return battleTexture != null && stageCamera != null; }
        }

        public int EnemyMaxHealth
        {
            get
            {
                if (enemyActor != null && enemyActor.LithosBoss != null)
                {
                    return enemyActor.LithosBoss.MaxHealth;
                }
                return 0;
            }
        }

        private PuzzleQuestBossSkillVfxSet phase1BossVfx;
        private PuzzleQuestBossSkillVfxSet phase2BossVfx;

        public void Initialize(
            RawImage rawImage,
            GameObject playerPrefab,
            GameObject waterPrefab,
            GameObject firePrefab,
            GameObject windPrefab,
            GameObject enemyPrefab,
            PuzzleQuestActorLayout layout,
            GameObject swordVfxPrefab = null,
            GameObject waterVfxPrefab = null,
            GameObject fireVfxPrefab = null,
            GameObject windVfxPrefab = null,
            PuzzleQuestBossSkillVfxSet phase1BossVfxSet = null,
            PuzzleQuestBossSkillVfxSet phase2BossVfxSet = null)
        {
            Dispose();

            actorLayout = layout ?? new PuzzleQuestActorLayout();
            targetImage = rawImage;
            swordSkillVfxPrefab = swordVfxPrefab;
            waterSkillVfxPrefab = waterVfxPrefab;
            fireSkillVfxPrefab = fireVfxPrefab;
            windSkillVfxPrefab = windVfxPrefab;
            phase1BossVfx = phase1BossVfxSet;
            phase2BossVfx = phase2BossVfxSet;
            playerDead = false;
            enemyDead = false;

            CreateStage();

            playerActor = CreateActor(
                playerPrefab,
                "PuzzleQuest Player",
                actorLayout.playerPosition,
                actorLayout.playerEulerAngles,
                actorLayout.playerScale,
                null);

            if (playerActor != null && playerActor.SkinAssigner != null)
            {
                playerSkillVisual = new PlayerSkillVisualController(playerActor.SkinAssigner);
            }

            AddFairy(
                PuzzleGemType.Water,
                waterPrefab,
                "PuzzleQuest Water Fairy",
                actorLayout.waterPosition,
                actorLayout.waterEulerAngles,
                actorLayout.waterScale,
                FairyElement.Water);

            AddFairy(
                PuzzleGemType.Fire,
                firePrefab,
                "PuzzleQuest Fire Fairy",
                actorLayout.firePosition,
                actorLayout.fireEulerAngles,
                actorLayout.fireScale,
                FairyElement.Fire);

            AddFairy(
                PuzzleGemType.Wind,
                windPrefab,
                "PuzzleQuest Wind Fairy",
                actorLayout.windPosition,
                actorLayout.windEulerAngles,
                actorLayout.windScale,
                FairyElement.Wind);

            enemyActor = CreateActor(
                enemyPrefab,
                "PuzzleQuest Lithos",
                actorLayout.enemyPosition,
                actorLayout.enemyEulerAngles,
                actorLayout.enemyScale * 0.33f, // Giảm kích thước quái xuống 65%
                null);

            HideAllFairies();
            FacePlayerTowardEnemy();
            FaceEnemyTowardPlayer();
            PlayPlayerIdle();
            PlayAnimatorState(enemyActor, EnemyIdleState, 0f);

            if (targetImage != null)
            {
                targetImage.texture = battleTexture;
                targetImage.color = Color.white;
                targetImage.raycastTarget = false;
                Shader uiPreShader = Shader.Find("UI/Premultiplied");
                if (uiPreShader != null)
                {
                    Material uiPreMat = new Material(uiPreShader);
                    uiPreMat.hideFlags = HideFlags.DontSave;
                    targetImage.material = uiPreMat;
                }
            }
        }

        public IEnumerator PlayPlayerAttack(
            int attackIndex,
            float anticipation,
            Action onHit,
            float recovery)
        {
            CancelReaction(playerActor);
            FacePlayerTowardEnemy();
            RestorePlayerWeapon();
            bool moveToPointB = ShouldPlayerMoveToPointB(PuzzleGemType.Sword);

            if (moveToPointB)
            {
                yield return MoveActorTo(
                    playerActor,
                    actorLayout.playerAttackPosition,
                    actorLayout.moveToAttackDuration,
                    true);
                FacePlayerTowardEnemy();
            }

            AnimationSpine.State attackState = ResolveNormalAttackState(attackIndex);
            TrackEntry attackEntry = PlayPlayerSpineState(attackState, false);
            playerActor?.AttackVfx?.Play(attackState);

            yield return WaitUnscaled(anticipation);
            onHit?.Invoke();

            if (swordSkillVfxPrefab != null)
            {
                Vector3 targetPos = GetActorCenter(enemyActor, Vector3.up * 0.35f);
                GameObject swordVfx = SpawnSkillVfx(swordSkillVfxPrefab, targetPos, Quaternion.identity);
                if (swordVfx != null)
                {
                    DestroyPresentationObject(swordVfx, 2.5f);
                }
            }

            yield return WaitUnscaled(recovery);
            yield return WaitForPlayerAnimation(attackEntry);

            if (moveToPointB && !playerDead)
            {
                yield return MoveActorTo(
                    playerActor,
                    actorLayout.playerPosition,
                    actorLayout.returnToPointADuration,
                    true);
                FacePlayerTowardEnemy();
            }

            if (!playerDead && (moveToPointB || attackEntry == null || IsCurrentPlayerEntry(attackEntry)))
            {
                playerActor?.AttackVfx?.Stop();
                PlayPlayerIdle();
            }
        }

        public IEnumerator PlayElementSkill(
            PuzzleGemType type,
            PuzzleSkillTier tier,
            string state,
            Color color,
            float anticipation,
            Action onHit,
            float recovery)
        {
            CancelReaction(playerActor);
            FacePlayerTowardEnemy();
            ActorHandle fairy = GetFairy(type);
            HideAllFairies();
            bool moveToPointB = ShouldPlayerMoveToPointB(type);

            if (moveToPointB)
            {
                yield return MoveActorTo(
                    playerActor,
                    actorLayout.playerAttackPosition,
                    actorLayout.moveToAttackDuration,
                    true);
                FacePlayerTowardEnemy();
            }

            if (fairy != null && fairy.IsAvailable)
            {
                fairy.Root.SetActive(true);
                ApplyFairyElement(fairy, type);
                SetLayerRecursively(fairy.Root, actorLayout.renderLayer);
                SetSortingLayerRecursively(fairy.Root, actorSortingLayerId);
                Renderer[] fRenderers = fairy.Root.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < fRenderers.Length; i++)
                {
                    fRenderers[i].sortingLayerID = actorSortingLayerId;
                    fRenderers[i].sortingOrder = 2000;
                }
                PlayAnimatorState(fairy, ResolveFairySkillState(tier, state), 0.04f);
            }

            TrackEntry playerSkillEntry = PlayPlayerElementPose(type, tier);
            yield return WaitUnscaled(anticipation);
            onHit?.Invoke();

            GameObject startPrefab = null;
            GameObject endPrefab = null;
            if (fairy != null && fairy.FairyElement != null && fairy.FairyElement.TransitionProfile != null)
            {
                var setting = fairy.FairyElement.TransitionProfile.GetSetting(ToFairyElement(type));
                startPrefab = setting.startVfx.prefab;
                endPrefab = setting.endVfx.prefab;
            }
            if (startPrefab == null)
            {
                var fallbackProfile = FindAnyTransitionProfile();
                if (fallbackProfile != null)
                {
                    var setting = fallbackProfile.GetSetting(ToFairyElement(type));
                    startPrefab = setting.startVfx.prefab;
                    endPrefab = setting.endVfx.prefab;
                }
            }
            if (startPrefab == null) startPrefab = ResolveSkillVfxPrefab(type);

            Vector3 originPos = playerActor != null && playerActor.IsAvailable
                ? GetActorCenter(playerActor, Vector3.up * 0.65f)
                : actorLayout.playerPosition + Vector3.up * 0.65f;
            Vector3 targetPos = enemyActor != null && enemyActor.IsAvailable
                ? GetActorCenter(enemyActor, Vector3.up * 1.5f)
                : actorLayout.enemyPosition + Vector3.up * 1.5f;

            if (type == PuzzleGemType.Fire)
            {
                yield return WaitUnscaled(anticipation);

                Transform spearHitPoint = null;
                if (playerActor != null && playerActor.Root != null)
                {
                    Transform[] children = playerActor.Root.GetComponentsInChildren<Transform>(true);
                    foreach (var child in children)
                    {
                        if (child.name == "SpearHitPoint")
                        {
                            spearHitPoint = child;
                            break;
                        }
                    }
                }

                int beamCount = 3;
                float beamInterval = 1.0f / 3f; // 0.333s. 3 beams in 1 second.

                if (playerSkillEntry != null)
                {
                    playerSkillEntry.Loop = true;
                    float startOffset = 2f / 20f;
                    playerSkillEntry.AnimationStart = startOffset;

                    float animDuration = playerSkillEntry.AnimationEnd - startOffset;

                    // Force the animation to play exactly 3 times in 1 second
                    playerSkillEntry.TimeScale = (animDuration * 3f) / 1.0f;

                    int pokesCompleted = 0;
                    playerSkillEntry.Complete += (trackEntry) =>
                    {
                        pokesCompleted++;
                        // Stop looping after the 2nd loop completes so the 3rd poke is the last one
                        if (pokesCompleted >= 2)
                        {
                            trackEntry.Loop = false;
                        }
                    };
                }
                float beamVfxLifetime = 0.35f;
                bool parentBeamToSpear = false;
                bool mirrorBeamVfxWithPlayerFacing = true;
                Vector3 attackVfxLocalPosition = new Vector3(0f, 0.8f, 0f);
                Vector3 attackVfxLocalEuler = new Vector3(0f, 90f, 0f);
                Vector3 attackVfxLocalScale = new Vector3(0.5f, 0.5f, 0.5f);

                for (int i = 0; i < beamCount; i++)
                {
                    if (startPrefab != null)
                    {
                        Vector3 spawnPos = spearHitPoint != null
                            ? spearHitPoint.position
                            : (playerActor != null && playerActor.IsAvailable
                                ? GetActorCenter(playerActor, Vector3.up * 0.5f + (playerActor.Root.transform.localScale.x < 0f ? Vector3.left : Vector3.right) * 0.8f)
                                : originPos);
                        Quaternion spawnRot = spearHitPoint != null ? spearHitPoint.rotation : Quaternion.identity;

                        GameObject beam = SpawnSkillVfx(startPrefab, spawnPos, spawnRot);
                        if (beam != null)
                        {
                            if (spearHitPoint != null)
                            {
                                beam.transform.SetParent(spearHitPoint, false);
                                beam.transform.localPosition = attackVfxLocalPosition;
                                beam.transform.localRotation = Quaternion.Euler(attackVfxLocalEuler);

                                Vector3 scale = attackVfxLocalScale;
                                if (mirrorBeamVfxWithPlayerFacing)
                                {
                                    float facingSign = (playerActor != null && playerActor.Root.transform.localScale.x < 0f) ? 1f : -1f;
                                    float absoluteX = Mathf.Abs(scale.x);
                                    scale.x = facingSign >= 0f ? absoluteX : -absoluteX;
                                }
                                beam.transform.localScale = scale;

                                if (!parentBeamToSpear)
                                {
                                    beam.transform.SetParent(null, true);
                                }
                            }
                            else
                            {
                                beam.transform.localScale = attackVfxLocalScale;
                            }

                            DestroyPresentationObject(beam, beamVfxLifetime);
                        }
                    }

                    if (i < beamCount - 1)
                    {
                        yield return WaitUnscaled(beamInterval);
                    }
                }
            }
            else if (startPrefab != null)
            {
                Vector3 dir = targetPos - originPos;
                Quaternion rot = dir.sqrMagnitude > 0.001f
                    ? Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg)
                    : Quaternion.identity;

                GameObject missile = SpawnSkillVfx(startPrefab, originPos, rot);
                if (missile != null)
                {
                    float flyTime = Mathf.Max(0.35f, anticipation);
                    float elapsed = 0f;
                    while (elapsed < flyTime && missile != null)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        float t = Mathf.Clamp01(elapsed / flyTime);
                        missile.transform.position = Vector3.Lerp(originPos, targetPos, t);
                        yield return null;
                    }
                    DestroyPresentationObject(missile, 0.05f);
                }
            }
            else
            {
                yield return WaitUnscaled(anticipation);
            }

            if (endPrefab != null && endPrefab != startPrefab)
            {
                GameObject explosion = SpawnSkillVfx(endPrefab, targetPos, Quaternion.identity);
                if (explosion != null) DestroyPresentationObject(explosion, 2.5f);
            }

            onHit?.Invoke();

            yield return WaitUnscaled(recovery);
            yield return WaitForPlayerAnimation(playerSkillEntry);

            if (moveToPointB && !playerDead)
            {
                yield return MoveActorTo(
                    playerActor,
                    actorLayout.playerPosition,
                    actorLayout.returnToPointADuration,
                    true);
                FacePlayerTowardEnemy();
            }

            if (fairy != null && fairy.IsAvailable)
            {
                PlayAnimatorState(fairy, FairyIdleState, 0.04f);
                fairy.Root.SetActive(false);
            }

            if (!playerDead && (moveToPointB || playerSkillEntry == null || IsCurrentPlayerEntry(playerSkillEntry)))
            {
                PlayPlayerIdle();
            }
        }

        private Material phase2GlowMaterial;

        private GameObject ResolveBossSkillVfx(
            Func<PuzzleQuestBossSkillVfxSet, GameObject> selector,
            bool isPhase2,
            FairyElement defaultElement,
            bool useStartVfx = false)
        {
            PuzzleQuestBossSkillVfxSet activeSet = isPhase2 ? phase2BossVfx : phase1BossVfx;
            if (activeSet != null)
            {
                GameObject custom = selector(activeSet);
                if (custom != null) return custom;
            }

            var profile = FindAnyTransitionProfile();
            if (profile != null)
            {
                var setting = profile.GetSetting(defaultElement);
                if (useStartVfx && setting.startVfx.prefab != null) return setting.startVfx.prefab;
                if (!useStartVfx && setting.endVfx.prefab != null) return setting.endVfx.prefab;
                if (setting.startVfx.prefab != null) return setting.startVfx.prefab;
            }
            return null;
        }

        public IEnumerator PlayEnemyAttack(
            bool aoe,
            float anticipation,
            Action onHit,
            float recovery,
            bool isPhase2 = false)
        {
            if (aoe)
            {
                yield return PlayEnemyOrbitAoe(anticipation, onHit, recovery, isPhase2);
                yield break;
            }

            bool moveToPointB = actorLayout.enemyNormalAttackMovesToPointB;

            if (moveToPointB && !enemyDead)
            {
                yield return MoveActorTo(
                    enemyActor,
                    actorLayout.enemyAttackPosition,
                    actorLayout.moveToAttackDuration,
                    false);
                FaceEnemyTowardPlayer();
            }

            if (!enemyDead)
            {
                PlayAnimatorState(enemyActor, EnemyAttackState, 0.04f);
            }

            Vector3 rockOrigin = GetActorCenter(enemyActor, Vector3.up * 1.5f);
            Vector3 rockTarget = GetActorCenter(playerActor, Vector3.up * 0.6f);

            yield return WaitUnscaled(anticipation);

            enemyActor?.LithosShader?.SpawnThrowVFX(rockOrigin);
            StartCoroutine(AnimateBossRockProjectile(rockOrigin, rockTarget, 0.28f, delegate
            {
                onHit?.Invoke();
                GameObject exp = ResolveBossSkillVfx(s => s.rockHitVfx, isPhase2, isPhase2 ? FairyElement.Fire : FairyElement.Earth, false);
                if (exp != null) SpawnSkillVfx(exp, rockTarget, Quaternion.identity);
            }));

            yield return WaitUnscaled(recovery);

            if (moveToPointB && !enemyDead)
            {
                yield return MoveActorTo(
                    enemyActor,
                    actorLayout.enemyPosition,
                    actorLayout.returnToPointADuration,
                    false);
                FaceEnemyTowardPlayer();
            }

            if (!enemyDead)
            {
                PlayAnimatorState(enemyActor, EnemyIdleState, 0.06f);
            }
        }

        public IEnumerator PlayEnemyRockBarrage(
            int rockCount,
            float anticipation,
            Action onHitEach,
            float recovery,
            bool isPhase2 = false)
        {
            if (enemyDead || enemyActor == null || !enemyActor.IsAvailable) yield break;

            PlayAnimatorState(enemyActor, EnemyAttackState, 0.04f);
            enemyActor.LithosShader?.TriggerScreenShake(isPhase2 ? 0.35f : 0.2f, isPhase2 ? 0.08f : 0.05f);

            Vector3 origin = GetActorCenter(enemyActor, Vector3.up * 1.5f);
            Vector3 target = GetActorCenter(playerActor, Vector3.up * 0.6f);

            yield return WaitUnscaled(anticipation);

            int count = Mathf.Clamp(rockCount, 3, 8);
            float interval = isPhase2 ? 0.12f : 0.16f;

            for (int i = 0; i < count; i++)
            {
                if (enemyDead || playerDead) break;

                float spreadY = UnityEngine.Random.Range(-0.4f, 0.5f);
                Vector3 rockOrigin = origin + new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), spreadY, 0f);
                Vector3 rockTarget = target + new Vector3(0f, UnityEngine.Random.Range(-0.2f, 0.3f), 0f);

                enemyActor.LithosShader?.SpawnThrowVFX(rockOrigin);
                StartCoroutine(AnimateBossRockProjectile(rockOrigin, rockTarget, 0.24f, delegate
                {
                    onHitEach?.Invoke();
                    GameObject exp = ResolveBossSkillVfx(s => s.rockHitVfx, isPhase2, isPhase2 ? FairyElement.Fire : FairyElement.Earth, false);
                    if (exp != null) SpawnSkillVfx(exp, rockTarget, Quaternion.identity);
                }));

                if (i < count - 1)
                {
                    yield return WaitUnscaled(interval);
                }
            }

            yield return WaitUnscaled(recovery + 0.25f);

            if (!enemyDead)
            {
                PlayAnimatorState(enemyActor, EnemyIdleState, 0.06f);
            }
        }

        public IEnumerator PlayEnemyOrbitAoe(
            float anticipation,
            Action onHit,
            float recovery,
            bool isPhase2 = false)
        {
            if (enemyDead || enemyActor == null || !enemyActor.IsAvailable) yield break;

            PlayAnimatorState(enemyActor, EnemyAoeState, 0.04f);
            enemyActor.LithosShader?.TriggerOrbitGlow(true);
            enemyActor.LithosShader?.TriggerScreenShake(isPhase2 ? 0.5f : 0.35f, isPhase2 ? 0.16f : 0.1f);

            Vector3 origin = GetActorCenter(enemyActor, Vector3.up * 1.2f);
            Vector3 target = GetActorCenter(playerActor, Vector3.up * 0.6f);

            GameObject blast = ResolveBossSkillVfx(s => s.orbitCastVfx, isPhase2, isPhase2 ? FairyElement.Fire : FairyElement.Earth, true);
            if (blast != null) SpawnSkillVfx(blast, origin, Quaternion.identity);

            yield return WaitUnscaled(anticipation + 0.3f);

            StartCoroutine(AnimateOrbitShockwave(origin, target, 0.35f, delegate
            {
                onHit?.Invoke();
                GameObject exp = ResolveBossSkillVfx(s => s.orbitImpactVfx, isPhase2, isPhase2 ? FairyElement.Fire : FairyElement.Earth, false);
                if (exp != null) SpawnSkillVfx(exp, target, Quaternion.identity);
            }));

            yield return WaitUnscaled(0.38f);
            enemyActor.LithosShader?.TriggerOrbitGlow(false);
            enemyActor.LithosShader?.TriggerScreenShake(isPhase2 ? 0.6f : 0.45f, isPhase2 ? 0.22f : 0.16f);

            yield return WaitUnscaled(recovery);

            if (!enemyDead)
            {
                PlayAnimatorState(enemyActor, EnemyIdleState, 0.06f);
            }
        }

        public IEnumerator PlayEnemyEarthquake(
            float anticipation,
            Action onHit,
            float recovery,
            bool isPhase2 = false)
        {
            if (enemyDead || enemyActor == null || !enemyActor.IsAvailable) yield break;

            PlayAnimatorState(enemyActor, EnemyAttackState, 0.04f);
            yield return WaitUnscaled(anticipation);

            enemyActor.LithosShader?.TriggerScreenShake(isPhase2 ? 0.75f : 0.55f, isPhase2 ? 0.28f : 0.22f);
            Vector3 groundPos = GetActorCenter(enemyActor, Vector3.down * 0.8f);
            enemyActor.LithosShader?.SpawnThrowVFX(groundPos);

            GameObject blast = ResolveBossSkillVfx(s => s.earthquakeVfx, isPhase2, isPhase2 ? FairyElement.Fire : FairyElement.Earth, true);
            if (blast != null) SpawnSkillVfx(blast, groundPos, Quaternion.identity);

            onHit?.Invoke();

            yield return WaitUnscaled(recovery);

            if (!enemyDead)
            {
                PlayAnimatorState(enemyActor, EnemyIdleState, 0.06f);
            }
        }

        public IEnumerator PlayEnemyShield(
            float duration,
            Action onShieldActivated,
            bool isPhase2 = false)
        {
            if (enemyDead || enemyActor == null || !enemyActor.IsAvailable) yield break;

            Vector3 center = GetActorCenter(enemyActor, Vector3.up * 1.0f);
            enemyActor.LithosShader?.SpawnRuneRevealVFX(center);
            enemyActor.LithosShader?.TriggerOrbitGlow(true);
            enemyActor.LithosShader?.TriggerScreenShake(0.25f, 0.06f);

            GameObject shieldObj = ResolveBossSkillVfx(s => s.shieldVfx, isPhase2, isPhase2 ? FairyElement.Fire : FairyElement.Earth, true);
            if (shieldObj != null) SpawnSkillVfx(shieldObj, center, Quaternion.identity);

            onShieldActivated?.Invoke();

            yield return WaitUnscaled(duration);
            enemyActor.LithosShader?.TriggerOrbitGlow(false);
        }

        public IEnumerator PlayEnemyPhaseTransition(Action onPhaseSwitched)
        {
            if (enemyDead || enemyActor == null || !enemyActor.IsAvailable) yield break;

            CancelReaction(enemyActor);
            PlayAnimatorState(enemyActor, "LithosMonolith_PhaseTransition", 0.04f);

            Vector3 center = GetActorCenter(enemyActor, Vector3.up * 1.2f);
            enemyActor.LithosShader?.TriggerScreenShake(0.85f, 0.28f);
            enemyActor.LithosShader?.SpawnRuneRevealVFX(center);
            enemyActor.LithosShader?.TriggerOrbitGlow(true);

            GameObject phaseVfx = ResolveBossSkillVfx(s => s.phaseTransitionVfx, true, FairyElement.Fire, false);
            if (phaseVfx != null) SpawnSkillVfx(phaseVfx, center, Quaternion.identity);

            // TẠO VÀ ÁP DỤNG MATERIAL PHASE 2 TÍM RỰC RỠ CHO TẤT CẢ SPRITE CỦA BOSS
            if (phase2GlowMaterial == null)
            {
                Shader glowShader = Shader.Find("Custom/2D/SpriteGlowPulse");
                if (glowShader == null) glowShader = Shader.Find("Sprites/Default");
                phase2GlowMaterial = new Material(glowShader);
                phase2GlowMaterial.SetColor("_BaseColor", new Color(1.2f, 0.75f, 1.4f, 1f));
                phase2GlowMaterial.SetColor("_RimColor", new Color(3.5f, 1.2f, 5.0f, 1f)); // Radiant Purple HDR Rim
                phase2GlowMaterial.SetFloat("_EmissionIntensity", 4.0f);
                phase2GlowMaterial.SetFloat("_RimIntensity", 3.5f);
                phase2GlowMaterial.SetFloat("_GlowRimWidth", 4.0f);
                phase2GlowMaterial.SetFloat("_PulseSpeed", 3.0f);
                phase2GlowMaterial.SetFloat("_PulseAmount", 0.5f);
            }

            if (enemyActor.Root != null)
            {
                SpriteRenderer[] srs = enemyActor.Root.GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < srs.Length; i++)
                {
                    srs[i].sharedMaterial = phase2GlowMaterial;
                    srs[i].color = new Color(1.6f, 0.9f, 2.2f, 1f);
                    srs[i].gameObject.SetActive(true);
                }
            }

            if (enemyActor.LithosBoss != null)
            {
                enemyActor.LithosBoss.ForcePhase2();
            }

            yield return WaitUnscaled(1.5f);

            onPhaseSwitched?.Invoke();
            enemyActor.LithosShader?.TriggerOrbitGlow(false);

            if (!enemyDead)
            {
                PlayAnimatorState(enemyActor, EnemyIdleState, 0.06f);
            }
        }

        private IEnumerator AnimateBossRockProjectile(
            Vector3 start,
            Vector3 end,
            float duration,
            Action onHit)
        {
            GameObject rockPrefab = null;
            if (enemyActor != null && enemyActor.LithosBoss != null && enemyActor.LithosBoss.RockProjectiles != null && enemyActor.LithosBoss.RockProjectiles.Length > 0)
            {
                int idx = UnityEngine.Random.Range(0, enemyActor.LithosBoss.RockProjectiles.Length);
                rockPrefab = enemyActor.LithosBoss.RockProjectiles[idx];
            }

            GameObject rockObj;
            if (rockPrefab != null)
            {
                rockObj = Instantiate(rockPrefab, stageRoot.transform);
                rockObj.SetActive(true);
                Collider2D col = rockObj.GetComponent<Collider2D>();
                if (col != null) col.enabled = false;
                Bao.Bosses.LithosRockProjectile proj = rockObj.GetComponent<Bao.Bosses.LithosRockProjectile>();
                if (proj != null) proj.enabled = false;
            }
            else
            {
                rockObj = new GameObject("BossRockProjectile", typeof(SpriteRenderer));
                rockObj.transform.SetParent(stageRoot.transform, false);
                SpriteRenderer fallbackSr = rockObj.GetComponent<SpriteRenderer>();
                fallbackSr.color = new Color(0.85f, 0.75f, 0.55f, 1f);
            }

            rockObj.transform.position = WithEffectDepth(start);
            SetLayerRecursively(rockObj, actorLayout.renderLayer);

            Renderer[] renderers = rockObj.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                renderers[r].sortingLayerID = actorSortingLayerId;
                if (renderers[r].sortingOrder < 1000)
                {
                    renderers[r].sortingOrder += 2500;
                }
            }

            float elapsed = 0f;
            float rotSpeed = UnityEngine.Random.Range(360f, 720f) * (UnityEngine.Random.value > 0.5f ? 1f : -1f);

            while (elapsed < duration && rockObj != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float arc = Mathf.Sin(t * Mathf.PI) * 0.4f;
                rockObj.transform.position = WithEffectDepth(Vector3.Lerp(start, end, t) + Vector3.up * arc);
                rockObj.transform.Rotate(0f, 0f, rotSpeed * Time.unscaledDeltaTime);
                yield return null;
            }

            if (rockObj != null)
            {
                DestroyPresentationObject(rockObj, 0.02f);
            }

            onHit?.Invoke();
            enemyActor?.LithosShader?.TriggerScreenShake(0.14f, 0.05f);
        }

        private IEnumerator AnimateOrbitShockwave(
            Vector3 start,
            Vector3 end,
            float duration,
            Action onHit)
        {
            yield return WaitUnscaled(duration);
            onHit?.Invoke();
            enemyActor?.LithosShader?.TriggerScreenShake(0.42f, 0.18f);
        }

        public void PlayEnemyDeath()
        {
            if (enemyDead)
            {
                return;
            }

            enemyDead = true;
            CancelReaction(enemyActor);
            HideAllFairies();

            if (enemyActor == null || !enemyActor.IsAvailable)
            {
                return;
            }

            PlayAnimatorState(enemyActor, EnemyDeathState, 0.03f);
            if (enemyActor.DeathRoutine != null)
            {
                StopCoroutine(enemyActor.DeathRoutine);
            }

            enemyActor.DeathRoutine = StartCoroutine(FreezeEnemyDeathPose());
        }

        public void PlayPlayerDeath()
        {
            if (playerDead)
            {
                return;
            }

            playerDead = true;
            CancelReaction(playerActor);
            HideAllFairies();
            RestorePlayerWeapon();
            PlayPlayerSpineState(AnimationSpine.State.Die, false);
        }

        public void PlayEnemyHit()
        {
            if (enemyDead || enemyActor == null || !enemyActor.IsAvailable)
            {
                return;
            }

            enemyActor.LithosShader?.TriggerHitFlash();
            StartActorShake(enemyActor);
        }

        public void PlayPlayerHit()
        {
            if (playerDead || playerActor == null || !playerActor.IsAvailable)
            {
                return;
            }

            CancelReaction(playerActor);
            RestorePlayerWeapon();
            TrackEntry hurtEntry = PlayPlayerSpineState(AnimationSpine.State.Hurt, false);
            StartActorShake(playerActor);
            playerActor.ReactionRoutine = StartCoroutine(RestorePlayerIdleAfterHit(hurtEntry));
        }

        public void Dispose()
        {
            if (disposing)
            {
                return;
            }

            disposing = true;
            StopAllCoroutines();
            playerSkillVisual?.Cleanup();
            playerSkillVisual = null;

            if (targetImage != null && targetImage.texture == battleTexture)
            {
                targetImage.texture = null;
            }

            if (stageCamera != null)
            {
                stageCamera.targetTexture = null;
            }

            for (int i = spawnedActors.Count - 1; i >= 0; i--)
            {
                DestroyPresentationObject(spawnedActors[i]);
            }

            spawnedActors.Clear();
            fairyActors.Clear();

            if (stageRoot != null)
            {
                DestroyPresentationObject(stageRoot);
            }

            if (impactMaterial != null)
            {
                DestroyPresentationObject(impactMaterial);
            }

            if (phase2GlowMaterial != null)
            {
                DestroyPresentationObject(phase2GlowMaterial);
                phase2GlowMaterial = null;
            }

            if (battleTexture != null)
            {
                battleTexture.Release();
                DestroyPresentationObject(battleTexture);
            }

            actorLayout = null;
            targetImage = null;
            stageRoot = null;
            inactiveSpawnRoot = null;
            stageCamera = null;
            battleTexture = null;
            impactTrail = null;
            impactMaterial = null;
            playerActor = null;
            enemyActor = null;
            playerDead = false;
            enemyDead = false;
            disposing = false;
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private void CreateStage()
        {
            int width = Mathf.Max(256, actorLayout.textureWidth);
            int height = Mathf.Max(128, actorLayout.textureHeight);
            int antiAliasing = ResolveAntiAliasing(actorLayout.antiAliasing);

            battleTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "PuzzleQuest Battle Render",
                antiAliasing = antiAliasing,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                hideFlags = HideFlags.DontSave
            };
            battleTexture.Create();

            stageRoot = new GameObject("PuzzleQuest Render Stage");
            stageRoot.hideFlags = HideFlags.DontSave;
            stageRoot.transform.position = actorLayout.isolatedWorldOrigin;

            inactiveSpawnRoot = new GameObject("Inactive Actor Spawn Root");
            inactiveSpawnRoot.hideFlags = HideFlags.DontSave;
            inactiveSpawnRoot.transform.SetParent(stageRoot.transform, false);
            inactiveSpawnRoot.SetActive(false);

            GameObject cameraObject = new GameObject("PuzzleQuest Render Camera", typeof(Camera));
            cameraObject.hideFlags = HideFlags.DontSave;
            cameraObject.transform.SetParent(stageRoot.transform, false);
            cameraObject.transform.localPosition = actorLayout.cameraPosition;
            cameraObject.transform.localRotation = Quaternion.identity;
            cameraObject.layer = actorLayout.renderLayer;

            stageCamera = cameraObject.GetComponent<Camera>();
            stageCamera.orthographic = true;
            stageCamera.orthographicSize = Mathf.Max(0.1f, actorLayout.orthographicSize);
            stageCamera.aspect = (float)width / height;
            stageCamera.clearFlags = CameraClearFlags.SolidColor;
            stageCamera.backgroundColor = actorLayout.clearColor;
            stageCamera.cullingMask = (1 << actorLayout.renderLayer) | 1;
            stageCamera.nearClipPlane = 0.01f;
            stageCamera.farClipPlane = 100f;
            stageCamera.allowHDR = true;
            stageCamera.allowMSAA = antiAliasing > 1;
            stageCamera.targetTexture = battleTexture;

            GameObject lightObject = new GameObject("PuzzleQuest Render Light", typeof(Light));
            lightObject.hideFlags = HideFlags.DontSave;
            lightObject.transform.SetParent(stageRoot.transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(35f, -35f, 0f);
            Light stageLight = lightObject.GetComponent<Light>();
            stageLight.type = LightType.Directional;
            stageLight.color = Color.white;
            stageLight.intensity = 1.0f;
            stageLight.shadows = LightShadows.None;
            stageLight.cullingMask = (1 << actorLayout.renderLayer) | 1;

            CreateGlobalLight2D();
            CreateImpactTrail();
        }

        private void CreateGlobalLight2D()
        {
            actorSortingLayerId = SortingLayer.NameToID(actorLayout.actorSortingLayer);

            GameObject lightObject = new GameObject("PuzzleQuest Global Light 2D");
            lightObject.hideFlags = HideFlags.DontSave;
            lightObject.SetActive(false);
            lightObject.transform.SetParent(stageRoot.transform, false);
            lightObject.layer = actorLayout.renderLayer;

            Light2D stageLight = lightObject.AddComponent<Light2D>();
            List<int> layerIds = new List<int> { actorSortingLayerId, SortingLayer.NameToID("Default") };
            int[] allSortingLayers = SortingLayer.layers != null
                ? System.Array.ConvertAll(SortingLayer.layers, l => l.id)
                : layerIds.ToArray();
            Light2DAffectedSortingLayersField?.SetValue(stageLight, allSortingLayers);
            stageLight.lightType = Light2D.LightType.Global;
            stageLight.blendStyleIndex = 0;
            stageLight.color = Color.white;
            stageLight.intensity = 1f;
            stageLight.shadowsEnabled = false;
            stageLight.volumetricEnabled = false;
            lightObject.SetActive(true);
        }

        private void CreateImpactTrail()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                return;
            }

            impactMaterial = new Material(shader)
            {
                name = "PuzzleQuest Impact Material",
                hideFlags = HideFlags.DontSave
            };

            GameObject trailObject = new GameObject("PuzzleQuest Impact Trail", typeof(LineRenderer));
            trailObject.hideFlags = HideFlags.DontSave;
            trailObject.transform.SetParent(stageRoot.transform, false);
            trailObject.layer = actorLayout.renderLayer;

            impactTrail = trailObject.GetComponent<LineRenderer>();
            impactTrail.material = impactMaterial;
            impactTrail.useWorldSpace = true;
            impactTrail.alignment = LineAlignment.View;
            impactTrail.textureMode = LineTextureMode.Stretch;
            impactTrail.numCapVertices = 6;
            impactTrail.numCornerVertices = 4;
            impactTrail.sortingOrder = 32000;
            impactTrail.enabled = false;
        }

        private ActorHandle CreateActor(
            GameObject prefab,
            string instanceName,
            Vector3 localPosition,
            Vector3 localEulerAngles,
            Vector3 scaleMultiplier,
            FairyElement? fairyElement)
        {
            if (prefab == null || stageRoot == null)
            {
                return null;
            }

            GameObject instance = Instantiate(prefab, inactiveSpawnRoot.transform, false);
            instance.name = instanceName;
            spawnedActors.Add(instance);

            Vector3 baseScale = SafeScale(scaleMultiplier);
            instance.SetActive(false);
            instance.transform.SetParent(stageRoot.transform, false);
            SanitizeActor(instance);
            ApplyActorTransform(
                instance.transform,
                localPosition,
                localEulerAngles,
                baseScale);

            instance.SetActive(true);
            SanitizeActor(instance);

            // Re-assert the isolated stage transform after presentation OnEnable callbacks.
            instance.transform.SetParent(stageRoot.transform, false);
            ApplyActorTransform(
                instance.transform,
                localPosition,
                localEulerAngles,
                baseScale);

            if (fairyElement.HasValue)
            {
                FairyElementVfxController elementController =
                    instance.GetComponentInChildren<FairyElementVfxController>(true);
                if (elementController != null)
                {
                    elementController.enabled = true;
                    elementController.SetElementImmediate(fairyElement.Value);
                }
            }

            SetLayerRecursively(instance, actorLayout.renderLayer);
            SetSortingLayerRecursively(instance, actorSortingLayerId);
            ConfigureAnimation(instance);

            ActorHandle actor = new ActorHandle
            {
                Root = instance,
                Animator = instance.GetComponentInChildren<Animator>(true),
                Spine = instance.GetComponentInChildren<AnimationSpine>(true),
                SkinAssigner = instance.GetComponentInChildren<SimpleSpineSkinAssigner>(true),
                CustomMaterials = instance.GetComponentInChildren<SkeletonRendererCustomMaterialsIgnoreSlotAlpha>(true),
                FairyElement = instance.GetComponentInChildren<FairyElementVfxController>(true),
                AttackVfx = instance.GetComponentInChildren<PlayerAttackVfxController>(true),
                LithosShader = instance.GetComponentInChildren<Bao.Bosses.LithosMonolithShaderController>(true),
                LithosBoss = instance.GetComponentInChildren<Bao.Bosses.LithosMonolithBoss>(true),
                BaseLocalPosition = localPosition
            };

            return actor;
        }

        private void AddFairy(
            PuzzleGemType type,
            GameObject prefab,
            string instanceName,
            Vector3 localPosition,
            Vector3 localEulerAngles,
            Vector3 scaleMultiplier,
            FairyElement element)
        {
            ActorHandle fairy = CreateActor(
                prefab,
                instanceName,
                localPosition,
                localEulerAngles,
                scaleMultiplier,
                element);

            if (fairy != null)
            {
                fairyActors[type] = fairy;
                PlayAnimatorState(fairy, FairyIdleState, 0f);
            }
        }

        private void SanitizeActor(GameObject root)
        {
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour != null && !IsPresentationBehaviour(behaviour))
                {
                    // These are temporary render-only clones. Disabling gameplay
                    // behaviours avoids RequireComponent removal warnings and
                    // prevents runtime systems from registering globals.
                    behaviour.enabled = false;
                }
            }

            Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            Rigidbody2D[] bodies = root.GetComponentsInChildren<Rigidbody2D>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody2D body = bodies[i];
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                body.simulated = false;
            }

            AudioSource[] audioSources = root.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < audioSources.Length; i++)
            {
                audioSources[i].Stop();
                audioSources[i].enabled = false;
            }

            Camera[] cameras = root.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                cameras[i].enabled = false;
            }

            SetLayerRecursively(root, actorLayout.renderLayer);

            ParticleSystem[] particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                var main = particleSystems[i].main;
                main.useUnscaledTime = true;
            }
        }

        private static bool IsPresentationBehaviour(MonoBehaviour behaviour)
        {
            if (behaviour == null)
            {
                return false;
            }

            if (behaviour is PlayerSceneScaleController)
            {
                return false;
            }

            if (behaviour is SkeletonRenderer ||
                behaviour is AnimationSpine ||
                behaviour is FairyElementVfxController ||
                behaviour is PlayerAttackVfxController ||
                behaviour is Bao.Bosses.LithosMonolithShaderController ||
                behaviour is Bao.Bosses.LithosMonolithBoss)
            {
                return true;
            }

            string typeName = behaviour.GetType().Name;
            return typeName == "SpineRuntimeSkinBootstrap2D" ||
                   typeName == "SimpleSpineSkinAssigner" ||
                   typeName == "SkeletonRendererCustomMaterialsIgnoreSlotAlpha" ||
                   typeName.EndsWith("VFX", StringComparison.OrdinalIgnoreCase) ||
                   typeName.EndsWith("Visual", StringComparison.OrdinalIgnoreCase) ||
                   typeName.EndsWith("Effect", StringComparison.OrdinalIgnoreCase) ||
                   typeName.EndsWith("Particle", StringComparison.OrdinalIgnoreCase) ||
                   typeName.Contains("AutoDestroy") ||
                   typeName.Contains("Particle");
        }

        private static void ConfigureAnimation(GameObject root)
        {
            Animator[] animators = root.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                animator.enabled = true;
                animator.speed = 1f;
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                animator.Update(0f);
            }

            SkeletonAnimation[] skeletonAnimations = root.GetComponentsInChildren<SkeletonAnimation>(true);
            for (int i = 0; i < skeletonAnimations.Length; i++)
            {
                skeletonAnimations[i].enabled = true;
                skeletonAnimations[i].UnscaledTime = true;
                if (!skeletonAnimations[i].valid)
                {
                    skeletonAnimations[i].Initialize(false);
                }
            }
        }
        private GameObject ResolveSkillVfxPrefab(PuzzleGemType type)
        {
            GameObject assigned = null;
            switch (type)
            {
                case PuzzleGemType.Fire:
                    assigned = fireSkillVfxPrefab;
                    break;
                case PuzzleGemType.Wind:
                    assigned = windSkillVfxPrefab;
                    break;
                default:
                    assigned = waterSkillVfxPrefab;
                    break;
            }

            if (assigned != null)
            {
                return assigned;
            }

            ActorHandle fairy = GetFairy(type);
            var transitionProfile = fairy != null && fairy.FairyElement != null
                ? fairy.FairyElement.TransitionProfile
                : FindAnyTransitionProfile();

            if (transitionProfile != null)
            {
                FairyElement element = ToFairyElement(type);
                var setting = transitionProfile.GetSetting(element);
                if (setting.endVfx.prefab != null)
                {
                    return setting.endVfx.prefab;
                }

                if (setting.startVfx.prefab != null)
                {
                    return setting.startVfx.prefab;
                }
            }

            if (fairy != null && fairy.FairyElement != null)
            {
                FairyElement element = ToFairyElement(type);
                var vfxProfile = fairy.FairyElement.VfxProfile;
                if (vfxProfile != null)
                {
                    var setting = vfxProfile.GetSetting(element);
                    if (setting.prefab != null)
                    {
                        return setting.prefab;
                    }
                }
            }

            return null;
        }

        private FairyElementTransitionProfile FindAnyTransitionProfile()
        {
            foreach (var kvp in fairyActors)
            {
                if (kvp.Value != null && kvp.Value.FairyElement != null && kvp.Value.FairyElement.TransitionProfile != null)
                {
                    return kvp.Value.FairyElement.TransitionProfile;
                }
            }
            return null;
        }

        private GameObject SpawnSkillVfx(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null || stageRoot == null)
            {
                return null;
            }

            GameObject instance = Instantiate(prefab, stageRoot.transform, false);
            instance.transform.position = WithEffectDepth(position);
            instance.transform.rotation = rotation;

            Camera[] cams = instance.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cams.Length; i++) cams[i].enabled = false;

            Canvas[] canvases = instance.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++) canvases[i].enabled = false;

            SetLayerRecursively(instance, actorLayout.renderLayer);
            SetSortingLayerRecursively(instance, actorSortingLayerId);

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sortingLayerID = actorSortingLayerId;
                renderers[i].sortingOrder = 2000;
            }

            ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem ps = particleSystems[i];
                var main = ps.main;
                main.useUnscaledTime = true;
                ps.Play(true);
            }

            AudioSource[] audioSources = instance.GetComponentsInChildren<AudioSource>(true);
            if (audioSources.Length == 0)
            {
                Debug.LogWarning($"PuzzleQuestActorPresenter: Prefab VFX {prefab.name} không có component AudioSource nào!");
            }

            for (int i = 0; i < audioSources.Length; i++)
            {
                AudioSource audioSource = audioSources[i];
                if (audioSource != null && audioSource.enabled)
                {
                    if (audioSource.clip == null)
                    {
                        Debug.LogWarning($"PuzzleQuestActorPresenter: AudioSource trên {prefab.name} bị thiếu AudioClip!");
                    }
                    else
                    {
                        Debug.Log($"PuzzleQuestActorPresenter: Đang phát âm thanh kỹ năng {audioSource.clip.name} (Ép thành âm thanh 2D)");
                        audioSource.spatialBlend = 0f; // Ép thành 2D để dù Camera có ở xa vẫn nghe được tiếng
                        audioSource.Stop();
                        audioSource.Play();
                    }
                }
            }

            Animator[] animators = instance.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                Animator anim = animators[i];
                anim.enabled = true;
                anim.updateMode = AnimatorUpdateMode.UnscaledTime;
            }

            return instance;
        }

        private void ApplyFairyElement(ActorHandle fairy, PuzzleGemType type)
        {
            if (fairy == null || !fairy.IsAvailable)
            {
                return;
            }

            FairyElementVfxController controller = fairy.FairyElement;
            if (controller == null)
            {
                return;
            }

            controller.SetElementImmediate(ToFairyElement(type));
        }

        private static FairyElement ToFairyElement(PuzzleGemType type)
        {
            switch (type)
            {
                case PuzzleGemType.Fire:
                    return FairyElement.Fire;
                case PuzzleGemType.Wind:
                    return FairyElement.Wind;
                default:
                    return FairyElement.Water;
            }
        }

        private void HideAllFairies()
        {
            foreach (KeyValuePair<PuzzleGemType, ActorHandle> pair in fairyActors)
            {
                if (pair.Value != null && pair.Value.IsAvailable)
                {
                    pair.Value.Root.SetActive(false);
                }
            }
        }

        private ActorHandle GetFairy(PuzzleGemType type)
        {
            fairyActors.TryGetValue(type, out ActorHandle fairy);
            return fairy;
        }

        private TrackEntry PlayPlayerElementPose(PuzzleGemType type, PuzzleSkillTier tier)
        {
            if (playerDead)
            {
                return null;
            }

            ApplyPlayerSkillWeapon(type);

            AnimationSpine.State state;
            switch (type)
            {
                case PuzzleGemType.Fire:
                    state = tier == PuzzleSkillTier.U
                        ? AnimationSpine.State.Spear
                        : AnimationSpine.State.Skill;
                    break;
                case PuzzleGemType.Wind:
                    state = AnimationSpine.State.Magic;
                    break;
                default:
                    state = AnimationSpine.State.Range;
                    break;
            }

            return PlayPlayerSpineState(state, false);
        }

        private TrackEntry PlayPlayerSpineState(AnimationSpine.State state, bool loop)
        {
            if (playerActor == null || playerActor.Spine == null || state == AnimationSpine.State.None)
            {
                return null;
            }

            if (!playerActor.Spine.HasStateAnimation(state))
            {
                return null;
            }

            return playerActor.Spine.PlayForcedFromSetupPose(state, loop, 1);
        }

        private void PlayPlayerIdle()
        {
            if (playerDead || playerActor == null || playerActor.Spine == null)
            {
                return;
            }

            RestorePlayerWeapon();
            playerActor.Spine.StopAnimation(1);
            playerActor.Spine.PlayForced(AnimationSpine.State.Idle, true, 0);
        }

        private void PlayPlayerWalk()
        {
            if (playerDead || playerActor == null || playerActor.Spine == null)
            {
                return;
            }

            RestorePlayerWeapon();
            playerActor.Spine.StopAnimation(1);
            playerActor.Spine.PlayForced(AnimationSpine.State.Walk, true, 0);
        }

        private void ApplyPlayerSkillWeapon(PuzzleGemType type)
        {
            RestorePlayerWeapon();
            if (playerSkillVisual == null)
            {
                return;
            }

            string weaponSkin;
            switch (type)
            {
                case PuzzleGemType.Fire:
                    weaponSkin = FireSpearSkin;
                    break;
                case PuzzleGemType.Wind:
                    weaponSkin = WindStaffSkin;
                    break;
                default:
                    weaponSkin = WaterBowSkin;
                    break;
            }

            playerSkillVisual.ShowBowSkin(weaponSkin);
            if (playerActor != null && playerActor.CustomMaterials != null)
            {
                playerActor.CustomMaterials.BeginSkillMaterial(ToSkillElementType(type), weaponSkin);
            }
        }

        private void RestorePlayerWeapon()
        {
            if (playerActor != null && playerActor.CustomMaterials != null)
            {
                playerActor.CustomMaterials.EndSkillMaterial();
            }

            playerSkillVisual?.HideBowSkin();
        }

        private static SkillElementType ToSkillElementType(PuzzleGemType type)
        {
            switch (type)
            {
                case PuzzleGemType.Fire:
                    return SkillElementType.Fire;
                case PuzzleGemType.Wind:
                    return SkillElementType.Wind;
                default:
                    return SkillElementType.Water;
            }
        }

        private bool IsCurrentPlayerEntry(TrackEntry entry)
        {
            return entry != null
                   && playerActor != null
                   && playerActor.Spine != null
                   && playerActor.Spine.AnimationState != null
                   && playerActor.Spine.AnimationState.GetCurrent(entry.TrackIndex) == entry;
        }

        private IEnumerator WaitForPlayerAnimation(TrackEntry entry)
        {
            float timeout = entry != null
                ? Mathf.Max(2f, entry.AnimationEnd * 4f)
                : 0f;
            float elapsed = 0f;
            while (!playerDead
                   && IsCurrentPlayerEntry(entry)
                   && entry.Animation != null
                   && entry.TrackTime < entry.AnimationEnd
                   && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void FacePlayerTowardEnemy()
        {
            if (playerActor == null || enemyActor == null ||
                !playerActor.IsAvailable || !enemyActor.IsAvailable)
            {
                return;
            }

            Vector3 scale = playerActor.Root.transform.localScale;
            float magnitude = Mathf.Max(0.001f, Mathf.Abs(scale.x));
            bool enemyIsRight = enemyActor.Root.transform.position.x >= playerActor.Root.transform.position.x;
            scale.x = enemyIsRight ? -magnitude : magnitude;
            playerActor.Root.transform.localScale = scale;
        }

        private void FaceEnemyTowardPlayer()
        {
            if (playerActor == null || enemyActor == null ||
                !playerActor.IsAvailable || !enemyActor.IsAvailable)
            {
                return;
            }

            Vector3 scale = enemyActor.Root.transform.localScale;
            float magnitude = Mathf.Max(0.001f, Mathf.Abs(scale.x));
            bool playerIsRight = playerActor.Root.transform.position.x >= enemyActor.Root.transform.position.x;
            scale.x = playerIsRight ? magnitude : -magnitude;
            enemyActor.Root.transform.localScale = scale;
        }

        private bool ShouldPlayerMoveToPointB(PuzzleGemType type)
        {
            switch (type)
            {
                case PuzzleGemType.Water:
                    return actorLayout.playerWaterMovesToPointB;
                case PuzzleGemType.Fire:
                    return actorLayout.playerFireMovesToPointB;
                case PuzzleGemType.Wind:
                    return actorLayout.playerWindMovesToPointB;
                default:
                    return actorLayout.playerSwordMovesToPointB;
            }
        }

        private IEnumerator MoveActorTo(
            ActorHandle actor,
            Vector3 targetLocalPosition,
            float duration,
            bool usePlayerWalk)
        {
            if (actor == null || !actor.IsAvailable)
            {
                yield break;
            }

            Vector3 start = actor.Root.transform.localPosition;
            if ((targetLocalPosition - start).sqrMagnitude < 0.0001f)
            {
                actor.BaseLocalPosition = targetLocalPosition;
                actor.Root.transform.localPosition = targetLocalPosition;
                yield break;
            }

            if (usePlayerWalk)
            {
                PlayPlayerWalk();
            }
            else
            {
                PlayAnimatorState(actor, actorLayout.enemyWalkState, 0.04f);
            }

            float safeDuration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;
            while (elapsed < safeDuration && actor != null && actor.IsAvailable)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);
                float eased = t * t * (3f - 2f * t);
                actor.Root.transform.localPosition = Vector3.LerpUnclamped(start, targetLocalPosition, eased);
                yield return null;
            }

            if (actor != null && actor.IsAvailable)
            {
                actor.Root.transform.localPosition = targetLocalPosition;
                actor.BaseLocalPosition = targetLocalPosition;
            }
        }

        private static AnimationSpine.State ResolveNormalAttackState(int attackIndex)
        {
            int normalizedIndex = Mathf.Clamp(attackIndex - 1, 0, 2);
            switch (Mathf.Clamp(normalizedIndex, 0, 2))
            {
                case 1:
                    return AnimationSpine.State.NormalAttack2;
                case 2:
                    return AnimationSpine.State.NormalAttack3;
                default:
                    return AnimationSpine.State.NormalAttack1;
            }
        }

        private static string ResolveFairySkillState(PuzzleSkillTier tier, string requestedState)
        {
            if (!string.IsNullOrWhiteSpace(requestedState))
            {
                return requestedState.Trim();
            }

            switch (tier)
            {
                case PuzzleSkillTier.I:
                    return FairySkillIState;
                case PuzzleSkillTier.O:
                    return FairySkillOState;
                default:
                    return FairySkillUState;
            }
        }

        private static float ResolveSkillTrailWidth(PuzzleSkillTier tier)
        {
            switch (tier)
            {
                case PuzzleSkillTier.I:
                    return 0.18f;
                case PuzzleSkillTier.O:
                    return 0.26f;
                default:
                    return 0.12f;
            }
        }

        private static void PlayAnimatorState(ActorHandle actor, string stateName, float transitionDuration)
        {
            if (actor == null || actor.Animator == null || string.IsNullOrWhiteSpace(stateName))
            {
                return;
            }

            actor.Animator.enabled = true;
            actor.Animator.speed = 1f;
            actor.Animator.CrossFadeInFixedTime(stateName, Mathf.Max(0f, transitionDuration), 0, 0f);
            actor.Animator.Update(0f);
        }

        private IEnumerator RestorePlayerIdleAfterHit(TrackEntry hurtEntry)
        {
            yield return WaitForPlayerAnimation(hurtEntry);
            if (!playerDead && (hurtEntry == null || IsCurrentPlayerEntry(hurtEntry)))
            {
                PlayPlayerIdle();
            }

            if (playerActor != null)
            {
                playerActor.ReactionRoutine = null;
            }
        }

        private IEnumerator FreezeEnemyDeathPose()
        {
            float duration = ResolveAnimatorClipDuration(
                enemyActor != null ? enemyActor.Animator : null,
                "Death Monolith",
                EnemyDeathFallbackDuration);

            yield return WaitUnscaled(Mathf.Max(0.05f, duration - 0.04f));

            if (enemyActor != null && enemyActor.Animator != null)
            {
                enemyActor.Animator.Play(EnemyDeathState, 0, 0.98f);
                enemyActor.Animator.Update(0f);
                enemyActor.Animator.speed = 0f;
                enemyActor.DeathRoutine = null;
            }
        }

        private static float ResolveAnimatorClipDuration(
            Animator animator,
            string clipNameFragment,
            float fallback)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return fallback;
            }

            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip != null &&
                    clip.name.IndexOf(clipNameFragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return Mathf.Max(0.05f, clip.length);
                }
            }

            return fallback;
        }

        private void StartActorShake(ActorHandle actor)
        {
            if (actor == null || !actor.IsAvailable)
            {
                return;
            }

            if (actor.ShakeRoutine != null)
            {
                StopCoroutine(actor.ShakeRoutine);
                actor.Root.transform.localPosition = actor.BaseLocalPosition;
            }

            actor.ShakeRoutine = StartCoroutine(ShakeActor(actor));
        }

        private IEnumerator ShakeActor(ActorHandle actor)
        {
            float duration = Mathf.Max(0.01f, actorLayout.hitShakeDuration);
            float distance = Mathf.Max(0f, actorLayout.hitShakeDistance);
            float elapsed = 0f;

            while (elapsed < duration && actor != null && actor.IsAvailable)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float damping = 1f - normalized;
                float x = Mathf.Sin(normalized * Mathf.PI * 8f) * distance * damping;
                float y = Mathf.Sin(normalized * Mathf.PI * 5f) * distance * 0.45f * damping;
                actor.Root.transform.localPosition = actor.BaseLocalPosition + new Vector3(x, y, 0f);
                yield return null;
            }

            if (actor != null && actor.IsAvailable)
            {
                actor.Root.transform.localPosition = actor.BaseLocalPosition;
                actor.ShakeRoutine = null;
            }
        }

        private void CancelReaction(ActorHandle actor)
        {
            if (actor == null)
            {
                return;
            }

            if (actor.ReactionRoutine != null)
            {
                StopCoroutine(actor.ReactionRoutine);
                actor.ReactionRoutine = null;
            }

            if (actor.ShakeRoutine != null)
            {
                StopCoroutine(actor.ShakeRoutine);
                actor.ShakeRoutine = null;
            }

            if (actor.IsAvailable)
            {
                actor.Root.transform.localPosition = actor.BaseLocalPosition;
            }
        }

        private IEnumerator ShowAndFadeTrail(
            Vector3 start,
            Vector3 end,
            Color color,
            float width,
            float duration)
        {
            if (impactTrail == null || duration <= 0f)
            {
                yield break;
            }

            impactTrail.loop = false;
            impactTrail.positionCount = 2;
            impactTrail.widthMultiplier = Mathf.Max(0.01f, width);
            impactTrail.SetPosition(0, WithEffectDepth(start));
            impactTrail.SetPosition(1, WithEffectDepth(end));
            yield return FadeImpact(color, duration);
        }

        private IEnumerator ShowAndFadeRing(
            Vector3 center,
            Color color,
            float radius,
            float duration)
        {
            if (impactTrail == null || duration <= 0f)
            {
                yield break;
            }

            const int segments = 40;
            impactTrail.loop = true;
            impactTrail.positionCount = segments;
            impactTrail.widthMultiplier = 0.11f;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 point = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                impactTrail.SetPosition(i, WithEffectDepth(point));
            }

            yield return FadeImpact(color, duration);
            impactTrail.loop = false;
        }

        private IEnumerator FadeImpact(Color color, float duration)
        {
            float safeDuration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;
            impactTrail.enabled = true;

            while (elapsed < safeDuration && impactTrail != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / safeDuration);
                Color faded = color;
                faded.a *= alpha;
                impactTrail.startColor = faded;
                impactTrail.endColor = faded;
                yield return null;
            }

            if (impactTrail != null)
            {
                impactTrail.enabled = false;
            }
        }

        private static Vector3 GetActorCenter(ActorHandle actor, Vector3 fallbackOffset)
        {
            if (actor == null || !actor.IsAvailable)
            {
                return fallbackOffset;
            }

            Renderer[] renderers = actor.Root.GetComponentsInChildren<Renderer>(false);
            bool hasBounds = false;
            Bounds bounds = new Bounds(actor.Root.transform.position, Vector3.zero);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds ? bounds.center : actor.Root.transform.position + fallbackOffset;
        }

        private static Vector3 GetFairyCastOrigin(ActorHandle fairy)
        {
            if (fairy == null || !fairy.IsAvailable)
            {
                return Vector3.zero;
            }

            if (fairy.FairyElement != null && fairy.FairyElement.BeamOrigin != null)
            {
                return fairy.FairyElement.BeamOrigin.position;
            }

            return GetActorCenter(fairy, Vector3.zero);
        }

        private static Vector3 WithEffectDepth(Vector3 point)
        {
            point.z -= 0.6f;
            return point;
        }

        private static IEnumerator WaitUnscaled(float duration)
        {
            float remaining = Mathf.Max(0f, duration);
            while (remaining > 0f)
            {
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static void ApplyActorTransform(
            Transform actor,
            Vector3 localPosition,
            Vector3 localEulerAngles,
            Vector3 localScale)
        {
            actor.localPosition = localPosition;
            actor.localRotation = Quaternion.Euler(localEulerAngles);
            actor.localScale = localScale;
        }

        private static Vector3 SafeScale(Vector3 scale)
        {
            return new Vector3(
                Mathf.Approximately(scale.x, 0f) ? 1f : scale.x,
                Mathf.Approximately(scale.y, 0f) ? 1f : scale.y,
                Mathf.Approximately(scale.z, 0f) ? 1f : scale.z);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].gameObject.layer = layer;
            }
        }

        private static void SetSortingLayerRecursively(GameObject root, int sortingLayerId)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sortingLayerID = sortingLayerId;
            }
        }

        private static int ResolveAntiAliasing(int requested)
        {
            if (requested >= 8)
            {
                return 8;
            }

            if (requested >= 4)
            {
                return 4;
            }

            return requested >= 2 ? 2 : 1;
        }

        private static void DestroyPresentationObject(UnityEngine.Object target, float t = 0f)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                if (t > 0f)
                {
                    Destroy(target, t);
                }
                else
                {
                    Destroy(target);
                }
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
