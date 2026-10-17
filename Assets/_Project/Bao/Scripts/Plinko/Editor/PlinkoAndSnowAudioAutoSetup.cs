#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bao.Plinko.Editor
{
    public static class PlinkoAndSnowAudioAutoSetup
    {
        [MenuItem("Tools/Audio/Tự Động Gán Toàn Bộ Âm Thanh (Minigame Plinko & Cầu Tuyết)", false, 50)]
        public static void SetupAllAudio()
        {
            int targetsConfigured = 0;
            int iceRollersConfigured = 0;
            int bouncersConfigured = 0;

            // 1. Cấu hình PlinkoBreakTarget trong Scene & Prefabs
            PlinkoBreakTarget[] targets = UnityEngine.Object.FindObjectsByType<PlinkoBreakTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in targets)
            {
                if (ConfigurePlinkoBreakTarget(t))
                {
                    targetsConfigured++;
                }
            }

            // 2. Cấu hình IceRockRoller trong Scene & Prefabs
            IceRockRoller[] rollers = UnityEngine.Object.FindObjectsByType<IceRockRoller>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var r in rollers)
            {
                if (ConfigureIceRockRoller(r))
                {
                    iceRollersConfigured++;
                }
            }

            // 3. Cấu hình PlinkoBouncer trong Scene & Prefabs
            PlinkoBouncer[] bouncers = UnityEngine.Object.FindObjectsByType<PlinkoBouncer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var b in bouncers)
            {
                if (ConfigurePlinkoBouncer(b))
                {
                    bouncersConfigured++;
                }
            }

            // 4. Cấu hình trong các Prefab Asset của Project
            string[] allPrefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in allPrefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                bool modified = false;

                var pTarget = prefab.GetComponentInChildren<PlinkoBreakTarget>(true);
                if (pTarget != null && ConfigurePlinkoBreakTarget(pTarget)) modified = true;

                var pRoller = prefab.GetComponentInChildren<IceRockRoller>(true);
                if (pRoller != null && ConfigureIceRockRoller(pRoller)) modified = true;

                var pBouncer = prefab.GetComponentInChildren<PlinkoBouncer>(true);
                if (pBouncer != null && ConfigurePlinkoBouncer(pBouncer)) modified = true;

                if (modified)
                {
                    EditorUtility.SetDirty(prefab);
                }
            }

            // Lưu Scene và Assets
            EditorSceneManager.MarkAllScenesDirty();
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "Thành Công!",
                $"Đã tự động gán đầy đủ âm thanh SFX cho Minigame Plinko & Cầu Tuyết:\n" +
                $"- {targetsConfigured} Plinko Break Target (Chạm nút + Chuỗi 8 vụ nổ băng liên hoàn)\n" +
                $"- {iceRollersConfigured} IceRockRoller (Lăn tuyết + Hoàn thành)\n" +
                $"- {bouncersConfigured} Plinko Bouncer (Bật nảy tưng tưng)",
                "Tuyệt vời");
        }

        public static bool ConfigurePlinkoBreakTarget(PlinkoBreakTarget target)
        {
            if (target == null) return false;

            Undo.RegisterCompleteObjectUndo(target, "Setup Plinko Audio");

            target.enableAudio = true;
            target.enableChainIceExplosionAudio = true;
            target.explosionCount = 8;
            target.explosionIntervalMin = 0.08f;
            target.explosionIntervalMax = 0.16f;
            target.randomizePitch = true;
            target.minPitch = 0.85f;
            target.maxPitch = 1.25f;
            target.triggerShakeOnEachExplosion = true;

            // Gán âm thanh bấm nút
            if (target.buttonHitSound == null)
            {
                target.buttonHitSound = FindClip("click-button") ?? FindClip("btn_down_01") ?? FindClip("sfx_parry");
            }

            // Gán danh sách các âm thanh nổ băng
            List<AudioClip> explosionClips = new List<AudioClip>();
            AddClipIfFound(explosionClips, "sfx_dreadworm_sanctuary_shield_break");
            AddClipIfFound(explosionClips, "sfx_dreadworm_icicle_impact");
            AddClipIfFound(explosionClips, "frostimpact");
            AddClipIfFound(explosionClips, "frostimpact02");
            AddClipIfFound(explosionClips, "sfx_dreadworm_slam_impact");
            AddClipIfFound(explosionClips, "toxicDetonationClip");
            AddClipIfFound(explosionClips, "arcaneimpact");
            AddClipIfFound(explosionClips, "genericimpact");

            if (explosionClips.Count > 0)
            {
                target.iceExplosionClips = explosionClips.ToArray();
            }

            EditorUtility.SetDirty(target);
            return true;
        }

        public static bool ConfigureIceRockRoller(IceRockRoller roller)
        {
            if (roller == null) return false;

            Undo.RegisterCompleteObjectUndo(roller, "Setup IceRockRoller Audio");

            roller.enableAudio = true;
            roller.rollingAudioVolume = 0.25f;
            roller.releaseAudioVolume = 0.4f;

            roller.rollingLoopSound = FindClip("Ditrenbang") ?? FindClip("Chaytrenco") ?? FindClip("windloop");
            roller.releaseSound = FindClip("Chaytrenco") ?? FindClip("sfx_walk");
            roller.completionSound = FindClip("sfx_dreadworm_sanctuary_shield_up") ?? FindClip("genericimpact") ?? FindClip("sfx_parry");

            EditorUtility.SetDirty(roller);
            return true;
        }

        public static bool ConfigurePlinkoBouncer(PlinkoBouncer bouncer)
        {
            if (bouncer == null) return false;

            Undo.RegisterCompleteObjectUndo(bouncer, "Setup PlinkoBouncer Audio");

            SerializedObject serialized = new SerializedObject(bouncer);
            SerializedProperty bounceSoundProp = serialized.FindProperty("bounceSound");
            if (bounceSoundProp != null && bounceSoundProp.objectReferenceValue == null)
            {
                bounceSoundProp.objectReferenceValue = FindClip("genericimpact") ?? FindClip("frostimpact");
            }

            SerializedProperty enableAudioProp = serialized.FindProperty("enableAudio");
            if (enableAudioProp != null) enableAudioProp.boolValue = true;

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(bouncer);
            return true;
        }

        private static void AddClipIfFound(List<AudioClip> list, string pattern)
        {
            AudioClip clip = FindClip(pattern);
            if (clip != null && !list.Contains(clip))
            {
                list.Add(clip);
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
    }
}
#endif
