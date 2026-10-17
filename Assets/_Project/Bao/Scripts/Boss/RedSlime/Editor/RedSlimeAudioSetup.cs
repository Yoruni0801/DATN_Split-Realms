using UnityEngine;
using UnityEditor;
using System.IO;

namespace Bao.Bosses.EditorScripts
{
    [InitializeOnLoad]
    public class RedSlimeAudioSetup : Editor
    {
        private const string AUDIO_FOLDER = "Assets/_Project/Bao/Sound/RedSlime";
        private const string PREFAB_BAO = "Assets/_Project/Bao/Boss/Red Slime.prefab";
        private const string PREFAB_TIEN = "Assets/_Project/Tien/Boss/Red Slime.prefab";
        private const string DEFAULT_BGM = "Assets/_Project/Tien/Nhac/Map1/bgm_Boss.mp3";

        [MenuItem("Tools/Bao/Setup Red Slime Audio & Auto-Assign SFX")]
        public static void SetupAndAssignRedSlimeAudio()
        {
            // 1. Đảm bảo toàn bộ asset âm thanh được import vào Unity
            AssetDatabase.Refresh();

            int prefabsUpdated = 0;
            string[] targetPrefabs = new string[] { PREFAB_BAO, PREFAB_TIEN };

            foreach (string prefabPath in targetPrefabs)
            {
                if (File.Exists(prefabPath))
                {
                    if (SetupAudioForPrefab(prefabPath))
                    {
                        prefabsUpdated++;
                    }
                }
            }

            // 2. Cập nhật cho cả các instance đang có trong Scene (nếu có)
            int sceneObjectsUpdated = 0;
            RedSlimeBoss[] sceneBosses = Object.FindObjectsByType<RedSlimeBoss>(FindObjectsSortMode.None);
            foreach (var boss in sceneBosses)
            {
                if (boss != null)
                {
                    RedSlimeBossAudioController audioCtrl = boss.GetComponent<RedSlimeBossAudioController>();
                    if (audioCtrl == null)
                    {
                        audioCtrl = Undo.AddComponent<RedSlimeBossAudioController>(boss.gameObject);
                    }
                    AssignAudioClips(audioCtrl);
                    EditorUtility.SetDirty(boss.gameObject);
                    sceneObjectsUpdated++;
                }
            }

            AssetDatabase.SaveAssets();

            Debug.Log($"<color=green>[RedSlimeAudioSetup] Thành công! Đã tự động gán toàn bộ 26 SFX + BGM vào {prefabsUpdated} Boss Prefabs và {sceneObjectsUpdated} Scene Instances.</color>");
            EditorUtility.DisplayDialog("Hoàn tất thiết lập âm thanh Boss Red Slime", 
                $"Đã gán đầy đủ 26 SFX + BGM cho Boss Red Slime!\n\n- Prefabs cập nhật: {prefabsUpdated}\n- Scene Objects: {sceneObjectsUpdated}\n\nThư mục âm thanh: {AUDIO_FOLDER}", "OK");
        }

        private static bool SetupAudioForPrefab(string prefabPath)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null) return false;

            try
            {
                RedSlimeBoss boss = prefabRoot.GetComponent<RedSlimeBoss>();
                if (boss == null)
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                    return false;
                }

                RedSlimeBossAudioController audioCtrl = prefabRoot.GetComponent<RedSlimeBossAudioController>();
                if (audioCtrl == null)
                {
                    audioCtrl = prefabRoot.AddComponent<RedSlimeBossAudioController>();
                }

                AssignAudioClips(audioCtrl);

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                Debug.Log($"[RedSlimeAudioSetup] Đã gán đầy đủ SFX cho Prefab: {prefabPath}");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        public static void AssignAudioClips(RedSlimeBossAudioController audioCtrl)
        {
            if (audioCtrl == null) return;

            SerializedObject so = new SerializedObject(audioCtrl);
            so.Update();

            // BGM
            SetClip(so, "bossFightBGM", FindClip(DEFAULT_BGM, "Assets/_Project/Tien/Nhac/Map1/bgm_boss 1.mp3", "Assets/_Project/Tien/Nhac/Map1/bgm_boss 2.mp3"));

            // Common / Life Cycle
            SetClip(so, "hurtClip", LoadSFX("sfx_redslime_hurt.wav"));
            SetClip(so, "deathClip", LoadSFX("sfx_redslime_death.wav"));
            SetClip(so, "splitPopClip", LoadSFX("sfx_redslime_split.wav"));
            SetClip(so, "mergeGatherClip", LoadSFX("sfx_redslime_merge_gather.wav"));
            SetClip(so, "mergeImpactClip", LoadSFX("sfx_redslime_merge_impact.wav"));

            // Form A
            SetClip(so, "meleeClip", LoadSFX("sfx_redslime_melee.wav"));
            SetClip(so, "meleeImpactClip", LoadSFX("sfx_redslime_melee_hit.wav"));
            SetClip(so, "slamJumpClip", LoadSFX("sfx_redslime_slam_jump.wav"));
            SetClip(so, "slamImpactClip", LoadSFX("sfx_redslime_slam_impact.wav"));
            SetClip(so, "spikeEruptClip", LoadSFX("sfx_redslime_spike_erupt.wav"));

            // Form B
            SetClip(so, "dashBWindupClip", LoadSFX("sfx_redslime_dash_windup.wav"));
            SetClip(so, "dashBLaunchClip", LoadSFX("sfx_redslime_dash_launch.wav"));
            SetClip(so, "dashBImpactClip", LoadSFX("sfx_redslime_dash_hit.wav"));
            SetClip(so, "shootBWindupClip", LoadSFX("sfx_redslime_shoot_windup.wav"));
            SetClip(so, "shootBFireClip", LoadSFX("sfx_redslime_shoot_fire.wav"));
            SetClip(so, "shootBImpactClip", LoadSFX("sfx_redslime_shoot_impact.wav"));
            SetClip(so, "dualCrossDashWarningClip", LoadSFX("sfx_redslime_dual_warning.wav"));
            SetClip(so, "dualCrossDashLaunchClip", LoadSFX("sfx_redslime_dual_launch.wav"));
            SetClip(so, "dualCrossDashClashClip", LoadSFX("sfx_redslime_dual_clash.wav"));
            SetClip(so, "dualCrossDashParriedClip", LoadSFX("sfx_redslime_dual_parried.wav"));

            // Form C
            SetClip(so, "pinballChargeClip", LoadSFX("sfx_redslime_pinball_charge.wav"));
            SetClip(so, "pinballBounceClip", LoadSFX("sfx_redslime_pinball_bounce.wav"));
            SetClip(so, "pinballLandClip", LoadSFX("sfx_redslime_pinball_land.wav"));
            SetClip(so, "laserChargeClip", LoadSFX("sfx_redslime_laser_charge.wav"));
            SetClip(so, "laserLoopClip", LoadSFX("sfx_redslime_laser_loop.wav"));
            SetClip(so, "laserBurstClip", LoadSFX("sfx_redslime_laser_burst.wav"));

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(audioCtrl);
        }

        private static AudioClip LoadSFX(string fileName)
        {
            string path = $"{AUDIO_FOLDER}/{fileName}";
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static AudioClip FindClip(params string[] possiblePaths)
        {
            foreach (var p in possiblePaths)
            {
                if (!string.IsNullOrEmpty(p) && File.Exists(p))
                {
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
                    if (clip != null) return clip;
                }
            }
            return null;
        }

        private static void SetClip(SerializedObject so, string propertyName, AudioClip clip)
        {
            if (clip == null) return;
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop != null)
            {
                prop.objectReferenceValue = clip;
            }
        }
    }
}
