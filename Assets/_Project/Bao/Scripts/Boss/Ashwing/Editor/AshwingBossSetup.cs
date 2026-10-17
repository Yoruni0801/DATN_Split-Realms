using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace Bao.Bosses.Ashwing.Editor
{
    public static class AshwingBossSetup
    {
        public const string BossPath="Assets/_Project/Bao/Boss/Ashwing.prefab";
        private const string AssetsPath="Assets/_Project/Bao/Boss/AshwingPresentation";

        [MenuItem("Tools/Ashwing/Install Cinder Sovereign upgrade")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Install in Edit Mode only.");
            Directory.CreateDirectory(AssetsPath);
            AssetDatabase.Refresh();
            var plumage=MaterialAt("AshwingPlumage.mat","Bao/AshwingBossPlumage");
            var filament=MaterialAt("AshwingFilament.mat","Bao/AshwingBossFilament");
            var projectile=CreateProjectile(plumage);
            var controller=CreateAnimator();
            var contents=PrefabUtility.LoadPrefabContents(BossPath);
            try
            {
                Configure(contents,plumage,filament,projectile,controller);
                PrefabUtility.SaveAsPrefabAsset(contents,BossPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(contents);}
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach(var root in scene.GetRootGameObjects())
            foreach(var enemy in root.GetComponentsInChildren<EnemySystem.StandaloneFlyingShooterEnemy>(true))
            {
                if(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(enemy.gameObject)!=BossPath)continue;
                Undo.RegisterFullObjectHierarchyUndo(enemy.gameObject,"Upgrade Ashwing boss presentation");
                Configure(enemy.gameObject,plumage,filament,projectile,controller);
                foreach(var c in enemy.GetComponents<Component>())if(c!=null)PrefabUtility.RecordPrefabInstancePropertyModifications(c);
            }
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Ashwing] Boss combat, plumage, feather projectile, animation and audio installed.");
        }

        private static Material MaterialAt(string filename,string shaderName)
        {
            string path=AssetsPath+"/"+filename;
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader=Shader.Find(shaderName);
            if(shader==null)throw new System.InvalidOperationException("Missing shader "+shaderName);
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
            else material.shader=shader;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static AshwingBossFeather CreateProjectile(Material material)
        {
            string path=AssetsPath+"/AshwingCinderFeather.prefab";
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(existing!=null)return existing.GetComponent<AshwingBossFeather>();
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Bao/Prefabs/longvu_projectile.prefab");
            var go=Object.Instantiate(source);go.name="AshwingCinderFeather";
            try
            {
                foreach(var c in go.GetComponents<MonoBehaviour>())Object.DestroyImmediate(c);
                go.transform.position=Vector3.zero;go.transform.rotation=Quaternion.identity;
                go.transform.localScale=Vector3.one*.60f;
                var rb=go.GetComponent<Rigidbody2D>();rb.gravityScale=0;rb.freezeRotation=true;
                rb.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
                var sr=go.GetComponent<SpriteRenderer>();sr.sharedMaterial=material;sr.color=new Color(1,.77f,.5f);sr.sortingOrder=25;
                go.AddComponent<AshwingBossFeather>();
                return PrefabUtility.SaveAsPrefabAsset(go,path).GetComponent<AshwingBossFeather>();
            }
            finally{Object.DestroyImmediate(go);}
        }

        private static AnimatorController CreateAnimator()
        {
            string path=AssetsPath+"/AshwingBoss.controller";
            var existing=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if(existing!=null)return existing;
            var ac=AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine=ac.layers[0].stateMachine;
            var fly=machine.AddState("Ashwing_Fly");fly.motion=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Bao/Animation/Ashwing_Fly.anim");fly.speed=.8f;
            var attack=machine.AddState("Ashwing_Attack");attack.motion=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Bao/Animation/Ashwing_Attack.anim");
            var transition=attack.AddTransition(fly);transition.hasExitTime=true;transition.exitTime=.95f;transition.duration=.08f;
            machine.defaultState=fly;
            return ac;
        }

        private static void Configure(GameObject go,Material plumage,Material filament,AshwingBossFeather projectile,AnimatorController animator)
        {
            var enemy=go.GetComponent<EnemySystem.StandaloneFlyingShooterEnemy>();
            var combat=go.GetComponent<AshwingBossCombat>();if(combat==null)combat=go.AddComponent<AshwingBossCombat>();
            var vfx=go.GetComponent<AshwingBossVFX>();if(vfx==null)vfx=go.AddComponent<AshwingBossVFX>();
            var health=new SerializedObject(enemy);
            health.FindProperty("maxHealth").intValue=240;
            health.FindProperty("playerLayer").intValue=1<<6;
            health.FindProperty("waitTimelineAggro").boolValue=true;
            var warning=go.transform.Find("ChamThan");
            if(warning!=null)health.FindProperty("warningObject").objectReferenceValue=warning.gameObject;
            health.ApplyModifiedPropertiesWithoutUndo();
            var settings=new SerializedObject(combat);settings.FindProperty("featherPrefab").objectReferenceValue=projectile;
            settings.FindProperty("arenaHalfWidth").floatValue=21;
            settings.FindProperty("arenaCenterOffsetX").floatValue=-8;
            settings.ApplyModifiedPropertiesWithoutUndo();
            go.GetComponent<Animator>().runtimeAnimatorController=animator;
            var body=go.GetComponent<Rigidbody2D>();body.gravityScale=0;body.freezeRotation=true;
            body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
            var shoot=go.transform.Find("ShootPoint");if(shoot!=null)shoot.localPosition=new Vector3(-1.5f,2.5f,0);
            // Only sprite skins belong to the owl. Never replace loot or warning materials.
            var skins=go.GetComponentsInChildren<SpriteSkin>(true);
            var renderers=skins.Select(s=>s.GetComponent<SpriteRenderer>()).Where(s=>s!=null).ToArray();
            foreach(var r in renderers){r.sharedMaterial=plumage;EditorUtility.SetDirty(r);}
            var fx=new SerializedObject(vfx);
            Set(fx,"plumageMaterial",plumage);Set(fx,"filamentMaterial",filament);
            Set(fx,"featherSprite",projectile.GetComponent<SpriteRenderer>().sprite);
            Set(fx,"rigRoot",go.transform.Find("bone_1"));
            var list=fx.FindProperty("plumage");list.arraySize=renderers.Length;
            for(int i=0;i<renderers.Length;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=renderers[i];
            string[] names={"Right up wing","Right down wing","Lefy up wing","left down wing"};
            var wings=fx.FindProperty("wingSprites");var bones=fx.FindProperty("wingBones");
            wings.arraySize=bones.arraySize=4;
            for(int i=0;i<4;i++)
            {
                var skin=skins.FirstOrDefault(s=>s.name==names[i]);
                if(skin==null)continue;
                wings.GetArrayElementAtIndex(i).objectReferenceValue=skin.GetComponent<SpriteRenderer>();
                var skinData=new SerializedObject(skin);
                bones.GetArrayElementAtIndex(i).objectReferenceValue=skinData.FindProperty("m_RootBone").objectReferenceValue;
            }
            Audio(fx,"callClip","Assets/_Project/Bao/Prefabs/Enemy/MapLua/audio/Ashwing_sfx.mp3");
            Audio(fx,"chargeClip","Assets/_Project/Bao/Sound/RedSlime/sfx_redslime_shoot_windup.wav");
            Audio(fx,"featherClip","Assets/_Project/Tien/SFX/daviddumaisaudio-sword-slash-and-swing-185432 (mp3cut.net).mp3");
            Audio(fx,"dashClip","Assets/_Project/Tien/SFX/sfx_dash.mp3");
            Audio(fx,"parryClip","Assets/_Project/Tien/SFX/sfx_parry.mp3");
            Audio(fx,"phaseClip","Assets/_Project/Bao/Sound/Dreadworm/sfx_dreadworm_phase_transition.wav");
            Audio(fx,"impactClip","Assets/_Project/Bao/Sound/RedSlime/sfx_redslime_shoot_impact.wav");
            fx.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(enemy);EditorUtility.SetDirty(combat);EditorUtility.SetDirty(vfx);
        }
        private static void Set(SerializedObject so,string field,Object value){so.FindProperty(field).objectReferenceValue=value;}
        private static void Audio(SerializedObject so,string field,string path){Set(so,field,AssetDatabase.LoadAssetAtPath<AudioClip>(path));}
    }
}
