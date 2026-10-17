#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using Quoc.QuyVuong.Audio;

namespace Quoc.QuyVuong.Editor
{
    public static class QuyVuongAudioAutoSetup
    {
        [MenuItem("Tools/Quoc/Setup Quy Vuong Epic Audio (Final Boss - Full 5 Phases)", false, 100)]
        public static void SetupAudioOnSelectedOrPrefab()
        {
            GameObject target = Selection.activeGameObject;

            if (target == null)
            {
                // Thử load prefab Quy_Vuong
                string[] guids = AssetDatabase.FindAssets("Quy_Vuong t:Prefab");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    target = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
            }

            if (target == null)
            {
                EditorUtility.DisplayDialog("Lỗi", "Vui lòng chọn GameObject hoặc Prefab Quy_Vuong trong Project Window!", "OK");
                return;
            }

            ConfigureAudioOnGameObject(target);
            ConfigureAllProjectileAndVfxPrefabs();

            EditorUtility.DisplayDialog("Thành công", $"Đã cấu hình toàn bộ âm thanh (5 Phase + Toàn bộ Prefab VFX/Đạn) cho {target.name}!", "OK");
        }

        public static void ConfigureAudioOnGameObject(GameObject bossGo)
        {
            if (bossGo == null) return;

            Undo.RegisterCompleteObjectUndo(bossGo, "Setup Quy Vuong Epic Audio");

            // 1. Thêm QuyVuongAudioController
            QuyVuongAudioController audioController = bossGo.GetComponent<QuyVuongAudioController>();
            if (audioController == null)
            {
                audioController = bossGo.AddComponent<QuyVuongAudioController>();
            }

            // 2. Thêm QuyVuongEpicAudioEnhancer
            QuyVuongEpicAudioEnhancer epicEnhancer = bossGo.GetComponent<QuyVuongEpicAudioEnhancer>();
            if (epicEnhancer == null)
            {
                epicEnhancer = bossGo.AddComponent<QuyVuongEpicAudioEnhancer>();
            }

            // 3. Thêm QuyVuongAudioObserver
            QuyVuongAudioObserver observer = bossGo.GetComponent<QuyVuongAudioObserver>();
            if (observer == null)
            {
                observer = bossGo.AddComponent<QuyVuongAudioObserver>();
            }

            // 4. Thêm QuyVuongSkillAudioTrigger (Realtime Skill Sensor)
            QuyVuongSkillAudioTrigger skillTrigger = bossGo.GetComponent<QuyVuongSkillAudioTrigger>();
            if (skillTrigger == null)
            {
                skillTrigger = bossGo.AddComponent<QuyVuongSkillAudioTrigger>();
            }

            // 5. Tự động tìm và gán các Audio Clip có sẵn trong Project
            SerializedObject serializedAudio = new SerializedObject(audioController);

            // Lifecycle Audio
            SetClipProperty(serializedAudio, "lifecycleAudio.engageRoarClip", "dragon-studio-dinosaur-growl");
            SetClipProperty(serializedAudio, "lifecycleAudio.engageStingerClip", "bgm_Gap_boss");
            SetClipProperty(serializedAudio, "lifecycleAudio.hurtClip", "Enemy_Hurt");
            SetClipProperty(serializedAudio, "lifecycleAudio.parriedStaggerClip", "sfx_parry");
            SetClipProperty(serializedAudio, "lifecycleAudio.phaseTransitionClip", "dragon-studio-dinosaur-growl");
            SetClipProperty(serializedAudio, "lifecycleAudio.phaseTransitionShockwaveClip", "toxicDetonationClip");
            SetClipProperty(serializedAudio, "lifecycleAudio.deathClip", "666herohero-monster-death-grunt");
            SetClipProperty(serializedAudio, "lifecycleAudio.deathExplosionClip", "toxicDetonationClip");
            SetClipProperty(serializedAudio, "lifecycleAudio.footstepClip", "sfx_Walk");
            SetClipProperty(serializedAudio, "lifecycleAudio.wingFlapClip", "sfx_dash");

            // Epic Final Boss Enhancements
            SetClipProperty(serializedAudio, "epicAudio.subBassImpactClip", "toxicDetonationClip");
            SetClipProperty(serializedAudio, "epicAudio.magicRiserClip", "burrowClip");
            SetClipProperty(serializedAudio, "epicAudio.heartbeatTensionClip", "sfx_hurt");

            // Phase 0 Audio (MamaTick / Cổ Đại)
            SetClipProperty(serializedAudio, "phase0Audio.waterRainTelegraphClip", "acidRainClip");
            SetClipProperty(serializedAudio, "phase0Audio.waterRainShootClip", "sfx_attack");
            SetClipProperty(serializedAudio, "phase0Audio.waterRainImpactClip", "toxicDetonationClip");
            SetClipProperty(serializedAudio, "phase0Audio.waterBeamChargeClip", "burrowClip");
            SetClipProperty(serializedAudio, "phase0Audio.waterBeamBurstClip", "toxicDetonationClip");
            SetClipProperty(serializedAudio, "phase0Audio.waterBeamLoopClip", "acidRainClip");
            SetClipProperty(serializedAudio, "phase0Audio.horizontalWaterArrowFireClip", "Attack");
            SetClipProperty(serializedAudio, "phase0Audio.horizontalWaterArrowImpactClip", "toxicDetonationClip");
            SetClipProperty(serializedAudio, "phase0Audio.skyTeleportClip", "sfx_dash");
            SetClipProperty(serializedAudio, "phase0Audio.arenaLandingClip", "sfx_Walk");

            // Phase 1 Audio (Cung Thủ Nước)
            SetClipProperty(serializedAudio, "phase1Audio.rainArrowTelegraphClip", "acidRainClip");
            SetClipProperty(serializedAudio, "phase1Audio.rainArrowShootClip", "sfx_attack");
            SetClipProperty(serializedAudio, "phase1Audio.rainArrowFallingLoopClip", "acidRainClip");
            SetClipProperty(serializedAudio, "phase1Audio.rainArrowImpactClip", "toxicDetonationClip");
            SetClipProperty(serializedAudio, "phase1Audio.horizontalArrowChargeClip", "sfx_dash");
            SetClipProperty(serializedAudio, "phase1Audio.horizontalArrowFireClip", "Attack");
            SetClipProperty(serializedAudio, "phase1Audio.horizontalArrowImpactClip", "toxicDetonationClip");
            SetClipProperty(serializedAudio, "phase1Audio.grappleBowCastClip", "sfx_attack");
            SetClipProperty(serializedAudio, "phase1Audio.grappleArrowShootClip", "sfx_dash");
            SetClipProperty(serializedAudio, "phase1Audio.grapplePullClip", "rootPrisonClip");
            SetClipProperty(serializedAudio, "phase1Audio.cageTrapSpawnClip", "rootPrisonClip");
            SetClipProperty(serializedAudio, "phase1Audio.ramWindupClip", "dragon-studio-dinosaur-growl");
            SetClipProperty(serializedAudio, "phase1Audio.ramDashRushClip", "Dash");
            SetClipProperty(serializedAudio, "phase1Audio.ramImpactClip", "toxicDetonationClip");
            SetClipProperty(serializedAudio, "phase1Audio.enemySpawnChantClip", "summonClip");
            SetClipProperty(serializedAudio, "phase1Audio.enemySpawnBurstClip", "summonClip");
            SetClipProperty(serializedAudio, "phase1Audio.flyTakeOffClip", "sfx_jump");
            SetClipProperty(serializedAudio, "phase1Audio.flyTrailClip", "sfx_dash");
            SetClipProperty(serializedAudio, "phase1Audio.flyLandingClip", "sfx_Walk");

            // Phase 2 Audio (Tử Thần Liềm)
            SetClipProperty(serializedAudio, "phase2Audio.scytheWindupClip", "sfx_dash");
            SetClipProperty(serializedAudio, "phase2Audio.scytheHeavySwingClip", "daviddumaisaudio-sword-slash-and-swing-185432");
            SetClipProperty(serializedAudio, "phase2Audio.scytheWaveSweepClip", "meleeClip");
            SetClipProperty(serializedAudio, "phase2Audio.scytheHitClip", "daviddumaisaudio-sword-slash-and-swing-185432");
            SetClipProperty(serializedAudio, "phase2Audio.homingSummonChargeClip", "burrowClip");
            SetClipProperty(serializedAudio, "phase2Audio.homingMultiFireClip", "sfx_attack");
            SetClipProperty(serializedAudio, "phase2Audio.homingFlightLoopClip", "burrowClip");
            SetClipProperty(serializedAudio, "phase2Audio.homingExplosionClip", "toxicDetonationClip");
            SetClipProperty(serializedAudio, "phase2Audio.rageRoarClip", "dragon-studio-dinosaur-growl");
            SetClipProperty(serializedAudio, "phase2Audio.relocateTeleportClip", "sfx_dash");

            // Phase 3 Audio (Địa Chấn Thiên Thạch)
            SetClipProperty(serializedAudio, "phase3Audio.earthStompClip", "rootSpikeClip");
            SetClipProperty(serializedAudio, "phase3Audio.groundLineCrackRumbleClip", "rootSpikeClip");
            SetClipProperty(serializedAudio, "phase3Audio.earthEruptionDusthornClip", "sfx_Dusthorn");
            SetClipProperty(serializedAudio, "phase3Audio.skyLeapTakeoffClip", "sfx_jump");
            SetClipProperty(serializedAudio, "phase3Audio.skyLeapDescentSwooshClip", "Dash");
            SetClipProperty(serializedAudio, "phase3Audio.skyLeapHeavyMeteorImpactClip", "toxicDetonationClip");
            SetClipProperty(serializedAudio, "phase3Audio.shockwaveRumbleClip", "rootSpikeClip");

            // Phase 4 Audio (Hỏa Nham Ma)
            SetClipProperty(serializedAudio, "phase4Audio.skullTransformClip", "dragon-studio-dinosaur-growl");
            SetClipProperty(serializedAudio, "phase4Audio.skullDashRushClip", "Dash");
            SetClipProperty(serializedAudio, "phase4Audio.skullDashImpactClip", "toxicDetonationClip");
            SetClipProperty(serializedAudio, "phase4Audio.eruptionUppercutClip", "meleeClip");
            SetClipProperty(serializedAudio, "phase4Audio.fireTornadoVortexClip", "sfx_Cháy nhà");
            SetClipProperty(serializedAudio, "phase4Audio.obsidianSummonBombsClip", "burrowClip");
            SetClipProperty(serializedAudio, "phase4Audio.bombHomingLaunchClip", "sfx_attack");
            SetClipProperty(serializedAudio, "phase4Audio.bombExplosionClip", "toxicDetonationClip");
            SetClipProperty(serializedAudio, "phase4Audio.shrapnelBurstClip", "rootSpikeClip");
            SetClipProperty(serializedAudio, "phase4Audio.volcanicArtilleryFireClip", "Attack");
            SetClipProperty(serializedAudio, "phase4Audio.volcanicArtilleryImpactClip", "toxicDetonationClip");

            // BGM Tracks
            SetClipProperty(serializedAudio, "bgmData.phase0BGM", "bgm_boss 1");
            SetClipProperty(serializedAudio, "bgmData.phase1BGM", "D&D-RPG Combat Music Mix");
            SetClipProperty(serializedAudio, "bgmData.phase2BGM", "Where_Light_Fails");
            SetClipProperty(serializedAudio, "bgmData.phase3BGM", "bgm_Boss");
            SetClipProperty(serializedAudio, "bgmData.phase4BGM", "D&D-RPG Combat Music Mix");

            serializedAudio.ApplyModifiedProperties();

            // Setup SerializedObject cho Enhancer
            SerializedObject serializedEnhancer = new SerializedObject(epicEnhancer);
            SetClipProperty(serializedEnhancer, "epicAudioData.subBassImpactClip", "toxicDetonationClip");
            SetClipProperty(serializedEnhancer, "epicAudioData.heartbeatTensionClip", "sfx_hurt");
            serializedEnhancer.ApplyModifiedProperties();

            EditorUtility.SetDirty(bossGo);
        }

        /// <summary>
        /// Tự động gắn và cấu hình QuyVuongProjectileAudio lên tất cả Prefab VFX và Projectile trong Project
        /// </summary>
        public static void ConfigureAllProjectileAndVfxPrefabs()
        {
            SetupPrefabAudio("RainArrow_Impact", null, null, "toxicDetonationClip", true);
            SetupPrefabAudio("RainArrow_Projectile", null, "acidRainClip", "toxicDetonationClip", false);
            SetupPrefabAudio("RainArrow_Warning", "acidRainClip", null, null, false);

            SetupPrefabAudio("HorizontalArrow_Charge", "sfx_dash", null, null, false);
            SetupPrefabAudio("HorizontalArrow_Projectile", "Attack", null, "toxicDetonationClip", false);
            SetupPrefabAudio("HorizontalArrow_Impact", null, null, "toxicDetonationClip", true);

            SetupPrefabAudio("EnemySpawn", "summonClip", null, null, true);
            SetupPrefabAudio("EnemySpawn_Warning", "summonClip", null, null, false);

            SetupPrefabAudio("Fly_TakeOff", "sfx_jump", null, null, false);
            SetupPrefabAudio("Fly_Landing", "sfx_Walk", null, "toxicDetonationClip", true);
            SetupPrefabAudio("Fly_Trail", null, "sfx_dash", null, false);

            AssetDatabase.SaveAssets();
        }

        private static void SetupPrefabAudio(string prefabName, string spawnClipName, string loopClipName, string impactClipName, bool subBass)
        {
            string[] guids = AssetDatabase.FindAssets($"{prefabName} t:Prefab");
            if (guids == null || guids.Length == 0) return;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                QuyVuongProjectileAudio projAudio = prefab.GetComponent<QuyVuongProjectileAudio>();
                if (projAudio == null)
                {
                    projAudio = prefab.AddComponent<QuyVuongProjectileAudio>();
                }

                AudioClip spawnClip = FindClip(spawnClipName);
                AudioClip loopClip = FindClip(loopClipName);
                AudioClip impactClip = FindClip(impactClipName);

                projAudio.SetClips(spawnClip, loopClip, impactClip, subBass);
                EditorUtility.SetDirty(prefab);
            }
        }

        private static AudioClip FindClip(string searchPattern)
        {
            if (string.IsNullOrEmpty(searchPattern)) return null;
            string[] guids = AssetDatabase.FindAssets($"{searchPattern} t:AudioClip");
            if (guids != null && guids.Length > 0)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            }
            return null;
        }

        private static void SetClipProperty(SerializedObject serializedObject, string propertyPath, string searchPattern)
        {
            SerializedProperty prop = serializedObject.FindProperty(propertyPath);
            if (prop == null || prop.objectReferenceValue != null) return;

            AudioClip clip = FindClip(searchPattern);
            if (clip != null)
            {
                prop.objectReferenceValue = clip;
            }
        }
    }
}
#endif
