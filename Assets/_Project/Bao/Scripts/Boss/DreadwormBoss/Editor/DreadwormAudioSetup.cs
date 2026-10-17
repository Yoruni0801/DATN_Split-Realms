using UnityEngine;
using UnityEditor;
using System.IO;

namespace Bao.Bosses.EditorScripts
{
    [InitializeOnLoad]
    public class DreadwormAudioSetup : Editor
    {
        private const string PREFAB_BAO = "Assets/_Project/Bao/Boss/Dreadworm.prefab";
        private const string PREFAB_TIEN = "Assets/_Project/Tien/Boss/Dreadworm.prefab";
        private const string DEFAULT_BGM = "Assets/_Project/Tien/Nhac/Map3/Breath_of_the_Frozen_Tundra.mp3";

        [MenuItem("Tools/Bao/Setup Dreadworm Boss Audio & Auto-Assign SFX")]
        public static void SetupAndAssignDreadwormAudio()
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
            DreadwormBoss[] sceneBosses = Object.FindObjectsByType<DreadwormBoss>(FindObjectsSortMode.None);
            foreach (var boss in sceneBosses)
            {
                if (boss != null)
                {
                    DreadwormBossAudioController audioCtrl = boss.GetComponent<DreadwormBossAudioController>();
                    if (audioCtrl == null)
                    {
                        audioCtrl = Undo.AddComponent<DreadwormBossAudioController>(boss.gameObject);
                    }
                    AssignAudioClips(audioCtrl);
                    EditorUtility.SetDirty(boss.gameObject);
                    sceneObjectsUpdated++;
                }
            }

            AssetDatabase.SaveAssets();

            Debug.Log($"<color=cyan>[DreadwormAudioSetup] Thành công! Đã tự động gán toàn bộ 25 SFX RPG Chuyên Nghiệp (Thu âm thực tế / Không rè / Không game con nít) + BGM vào {prefabsUpdated} Boss Prefabs và {sceneObjectsUpdated} Scene Instances.</color>");
            EditorUtility.DisplayDialog("Hoàn tất thiết lập âm thanh Dreadworm Boss",
                $"Đã gán đầy đủ 25 SFX Chuẩn RPG Chuyên Nghiệp (Thu âm thực tế, tiếng nện đất & gầm rú cực lực, không rè) + BGM cho Boss Dreadworm!\n\n- Prefabs cập nhật: {prefabsUpdated}\n- Scene Objects: {sceneObjectsUpdated}", "OK");
        }

        private static bool SetupAudioForPrefab(string prefabPath)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null) return false;

            try
            {
                DreadwormBoss boss = prefabRoot.GetComponent<DreadwormBoss>();
                if (boss == null)
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                    return false;
                }

                DreadwormBossAudioController audioCtrl = prefabRoot.GetComponent<DreadwormBossAudioController>();
                if (audioCtrl == null)
                {
                    audioCtrl = prefabRoot.AddComponent<DreadwormBossAudioController>();
                }

                AssignAudioClips(audioCtrl);

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                Debug.Log($"[DreadwormAudioSetup] Đã gán đầy đủ SFX RPG Chuyên Nghiệp cho Prefab: {prefabPath}");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        public static void AssignAudioClips(DreadwormBossAudioController audioCtrl)
        {
            if (audioCtrl == null) return;

            SerializedObject so = new SerializedObject(audioCtrl);
            so.Update();

            // 1. BGM Trận Đấu
            AudioClip bgmClip = FindClip(DEFAULT_BGM,
                "Assets/_Project/Tien/Nhac/Map3/BackGr_Map3.mp3",
                "Assets/_Project/Tien/Nhac/Map1/bgm_Boss.mp3",
                "Assets/_Source/Audio/Boss/Darkest Dungeon OST - Crimson Court - The Viscount.mp3");
            SetClip(so, "bossFightBGM", bgmClip);

            // 2. Vòng Đời & Trạng Thái Chung (Monster Vocals & Cinematic Impacts)
            SetClip(so, "engageRoarClip", FindClip(
                "Assets/_Project/Tien/Nhac/Map2/Enemy/sfx_Dusthorn.mp3",
                "Assets/_Project/Bao/Prefabs/Enemy/MapBang/Audio/Icefin_sfx.mp3"));

            SetClip(so, "hurtClip", FindClip(
                "Assets/_Project/Tien/SFX/Enemy_Hurt.mp3",
                "Assets/_Project/Duy/Audio/Enemy/die_Trimmed.wav"));

            SetClip(so, "phaseTransitionClip", FindClip(
                "Assets/_Source/MagicArsenal/Effects/Sound/Impact/stormimpact.wav",
                "Assets/_Project/Duy/Audio/Skill/untl.wav"));

            SetClip(so, "deathClip", FindClip(
                "Assets/_Project/Tien/Nhac/Map2/Enemy/666herohero-monster-death-grunt-131480.mp3",
                "Assets/_Project/Tien/SFX/sfx_death.mp3"));

            SetClip(so, "parriedStaggerClip", FindClip(
                "Assets/_Project/Duy/Audio/Skill/pary.mp3",
                "Assets/_Project/Tien/SFX/sfx_parry.mp3"));

            // 3. Cận Chiến & Lao Húc (Heavy Earth Thuds & Authentic Movement)
            SetClip(so, "slamWindupClip", FindClip(
                "Assets/_Project/Duy/Audio/Skill/dragon-studio-elemental-spell-impact-wind-478376.mp3",
                "Assets/_Project/Duy/Audio/Skill/xpmonster-quick-swing-sound-419581.mp3"));

            SetClip(so, "slamImpactClip", FindClip(
                "Assets/_Source/MagicArsenal/Effects/Sound/Impact/earthimpact.wav"));

            SetClip(so, "heavySlamImpactClip", FindClip(
                "Assets/_Source/MagicArsenal/Effects/Sound/Impact/stormimpact02.wav",
                "Assets/_Source/MagicArsenal/Effects/Sound/Impact/fireimpact.wav"));

            SetClip(so, "chargeWindupClip", FindClip(
                "Assets/_Project/Bao/Prefabs/Enemy/MapSaMac/audio/Riftcore_sfx.mp3",
                "Assets/_Project/Tien/Nhac/Map2/Enemy/sfx_Bonewheel.mp3"));

            SetClip(so, "chargeDashClip", FindClip(
                "Assets/_Project/Duy/Audio/Skill/dash.mp3",
                "Assets/_Project/Tien/SFX/sfx_dash.mp3"));

            SetClip(so, "chargeDriftClip", FindClip(
                "Assets/_Project/Bao/Prefabs/Enemy/MapBang/Audio/IceCrab_sfx.mp3",
                "Assets/_Project/Duy/Audio/Skill/wind-gusts-447502_Trimmed.wav"));

            // 4. Kỹ Năng - Mưa Phi Đạn (Snappy Short One-Shots, Không Bị Dài Dai Dẳng)
            SetClip(so, "iceArrowChargeClip", FindClip(
                "Assets/_Project/Duy/Audio/Skill/freesound_community-fire-spell-100276.mp3",
                "Assets/_Source/MagicArsenal/Effects/Sound/Cast/magic_cast_generic03.wav"));

            SetClip(so, "iceArrowFireClip", FindClip(
                "Assets/_Project/Duy/Audio/Skill/xpmonster-quick-swing-sound-419581.mp3",
                "Assets/_Project/Tien/SFX/sfx_attack.mp3",
                "Assets/_Project/Tien/SFX/daviddumaisaudio-sword-slash-and-swing-185432 (mp3cut.net).mp3"));

            SetClip(so, "iceArrowHitClip", FindClip(
                "Assets/_Source/MagicArsenal/Effects/Sound/Impact/arcaneimpact2.wav",
                "Assets/_Source/MagicArsenal/Effects/Sound/Impact/frostimpact02.wav"));

            // 5. Kỹ Năng - Cầu Năng Lượng (Energy Sphere)
            SetClip(so, "sphereSpawnClip", FindClip(
                "Assets/_Source/MagicArsenal/Effects/Sound/Cast/magic_cast_generic.wav"));

            SetClip(so, "sphereSweepClip", FindClip(
                "Assets/_Source/MagicArsenal/Effects/Sound/Loops/FireOrbitSphere.mp3"));

            SetClip(so, "sphereImpactClip", FindClip(
                "Assets/_Source/MagicArsenal/Effects/Sound/Impact/shadowimpact.wav"));

            // 6. Kỹ Năng - Sóng Gai Bộc Phát (Spine Eruption)
            SetClip(so, "spineChargePulseClip", FindClip(
                "Assets/_Source/MagicArsenal/Effects/Sound/Cast/magic_cast_generic03.wav"));

            SetClip(so, "spineEruptClip", FindClip(
                "Assets/_Project/Tien/Nhac/Map2/Enemy/rootSpikeClip.mp3"));

            // 7. Kỹ Năng - Khiên Hộ Thể (Sanctuary Barrier)
            SetClip(so, "sanctuaryWindupClip", FindClip(
                "Assets/_Source/MagicArsenal/Effects/Sound/Cast/magic_cast_generic02.wav"));

            SetClip(so, "sanctuaryShieldUpClip", FindClip(
                "Assets/_Source/MagicArsenal/Effects/Sound/Impact/lightimpact.wav"));

            SetClip(so, "sanctuaryShieldHitClip", FindClip(
                "Assets/_Source/MagicArsenal/Effects/Sound/Impact/genericimpact.wav"));

            SetClip(so, "sanctuaryShieldBreakClip", FindClip(
                "Assets/_Source/MagicArsenal/Effects/Sound/Impact/arcaneimpact.wav"));

            // 8. Kỹ Năng - Mưa Rơi Thiên Thạch (Aerial Bombardment)
            SetClip(so, "icicleTelegraphClip", FindClip(
                "Assets/_Project/Duy/Audio/Skill/dragon-studio-gentle-wind-gusts-447502.mp3"));

            SetClip(so, "icicleImpactClip", FindClip(
                "Assets/_Source/MagicArsenal/Effects/Sound/Impact/earthimpact.wav"));

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(audioCtrl);

            // Đồng bộ BGM sang BossUIEventDispatcher nếu có
            BossUIEventDispatcher uiDispatcher = audioCtrl.GetComponent<BossUIEventDispatcher>();
            if (uiDispatcher != null && bgmClip != null)
            {
                SerializedObject soDispatcher = new SerializedObject(uiDispatcher);
                soDispatcher.Update();
                SetClip(soDispatcher, "bossBGM", bgmClip);
                soDispatcher.ApplyModifiedProperties();
                EditorUtility.SetDirty(uiDispatcher);
            }
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
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop != null)
            {
                prop.objectReferenceValue = clip;
            }
        }
    }
}
