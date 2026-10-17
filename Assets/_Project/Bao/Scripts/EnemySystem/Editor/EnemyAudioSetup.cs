using UnityEngine;
using UnityEditor;
using Bao.Enemies;

namespace Bao.Enemies.EditorScripts
{
    public class EnemyAudioSetup : Editor
    {
        [MenuItem("Tools/Bao/Setup Enemy Audio Controllers")]
        public static void SetupEnemyAudio()
        {
            // Tìm tất cả các Prefab trong thư mục EnemySystem (bao gồm tất cả thư mục con như Phantom, Dusthorn, Ashwing, v.v...)
            string[] foldersToSearch = new string[]
            {
                "Assets/_Project/Bao/Prefabs/Enemy"
            };

            int addedCount = 0;
            int checkedCount = 0;

            string[] guids = AssetDatabase.FindAssets("t:Prefab", foldersToSearch);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                if (prefab != null)
                {
                    // Chỉ xử lý những Prefab có chứa class BaoEnemyBase
                    BaoEnemyBase enemyBase = prefab.GetComponent<BaoEnemyBase>();
                    if (enemyBase != null)
                    {
                        checkedCount++;
                        EnemyAudioController audioController = prefab.GetComponent<EnemyAudioController>();
                        
                        // Nếu chưa có EnemyAudioController thì mới thêm vào
                        if (audioController == null)
                        {
                            audioController = prefab.AddComponent<EnemyAudioController>();
                            EditorUtility.SetDirty(prefab);
                            addedCount++;
                            Debug.Log($"Đã tự động thêm EnemyAudioController cho {prefab.name} tại {path}");
                        }
                    }
                }
            }
            
            if (addedCount > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"<color=green>Thành công! Đã thêm tự động script âm thanh vào {addedCount} enemy prefabs.</color>");
            }
            else
            {
                Debug.Log($"<color=yellow>Đã kiểm tra {checkedCount} enemy, tất cả đều đã có sẵn script âm thanh, không cần thêm mới.</color>");
            }
            
            EditorUtility.DisplayDialog("Hoàn tất", $"Đã kiểm tra {checkedCount} enemy.\nThêm mới thành công vào {addedCount} enemy.", "OK");
        }
    }
}
