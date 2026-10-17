#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace Bao.Fluid2D
{
    public class Fluid2DTestSceneSetup : EditorWindow
    {
        [MenuItem("Tools/Bao/Fluid2D/Diagnostics/Legacy Demo Scene Setup")]
        public static void OpenWindow()
        {
            Fluid2DTestSceneSetup window = GetWindow<Fluid2DTestSceneSetup>("Fluid2D Demo Setup");
            window.minSize = new Vector2(300, 150);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("Fluid2D Demo Scene Setup Helper", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Công cụ này sẽ tự động tạo một cấu trúc demo đầy đủ bao gồm: Hệ thống chất lỏng GPU, Emitter phát nước, Drain hút nước, các tấm cản địa hình tĩnh và các vật thể nổi tương tác.", MessageType.Info);
            
            GUILayout.Space(15);
            if (GUILayout.Button("Tạo Scene Demo Tự Động", GUILayout.Height(40)))
            {
                CreateDemoScene();
                Close();
            }
        }

        private static void CreateDemoScene()
        {
            // 1. Tạo các thư mục cần thiết
            string matDir = "Assets/_Project/Bao/Fluid2D/Materials";
            if (!Directory.Exists(matDir))
            {
                Directory.CreateDirectory(matDir);
            }
            
            string settingsDir = "Assets/_Project/Bao/Fluid2D/Settings";
            if (!Directory.Exists(settingsDir))
            {
                Directory.CreateDirectory(settingsDir);
            }

            // 2. Tạo hoặc load ScriptableObject Settings
            string settingsPath = settingsDir + "/Fluid2DSettings_Default.asset";
            Fluid2DSettings settings = AssetDatabase.LoadAssetAtPath<Fluid2DSettings>(settingsPath);
            if (settings == null)
            {
                settings = CreateInstance<Fluid2DSettings>();
                // Cấu hình thông số mặc định cho nước mượt mà
                settings.maxParticles = 3000;
                settings.smoothingRadius = 0.25f;
                settings.restDensity = 3.0f;
                settings.pressureStiffness = 15.0f;
                settings.nearPressureStiffness = 30.0f;
                settings.viscosity = 0.05f;
                settings.gravity = new Vector2(0f, -9.81f);
                settings.worldBounds = new Vector4(-15f, -8f, 15f, 8f);
                settings.substeps = 4;
                settings.particleRadius = 0.08f;
                // Map_IceandSnow glacial defaults (hand-painted cyan/teal).
                settings.surfaceColor = new Color(0.55f, 0.88f, 0.92f, 0.62f);
                settings.deepColor = new Color(0.18f, 0.55f, 0.62f, 0.88f);
                settings.foamColor = new Color(0.92f, 0.98f, 1.0f, 0.55f);
                settings.outlineColor = new Color(0.88f, 0.97f, 0.99f, 0.85f);
                settings.outlineWidth = 0.045f;
                settings.edgeSoftness = 0.022f;
                settings.depthDarkening = 0.55f;
                settings.highlightStrength = 0.55f;
                settings.rippleStrength = 0.08f;
                settings.verticalGradient = 0.55f;
                settings.surfaceBandStrength = 0.65f;
                settings.surfaceBandWidth = 0.12f;
                settings.noiseStrength = 0.14f;
                settings.noiseScale = 9f;
                settings.noiseScaleB = 22f;
                settings.noiseSpeed = 0.08f;
                settings.fresnelStrength = 0.7f;
                settings.fresnelPower = 2.2f;
                settings.foamCurvature = 1.35f;
                settings.sparkleStrength = 0.18f;
                settings.bodyAlpha = 0.92f;
                
                AssetDatabase.CreateAsset(settings, settingsPath);
                AssetDatabase.SaveAssets();
            }

            // 3. Tạo hoặc load Materials
            Shader splatShader = Shader.Find("Custom/FluidParticleSplat");
            Shader metaballShader = Shader.Find("Custom/FluidMetaball");

            if (splatShader == null || metaballShader == null)
            {
                EditorUtility.DisplayDialog("Lỗi", "Vui lòng đảm bảo các shader 'Custom/FluidParticleSplat' và 'Custom/FluidMetaball' đã được import chính xác!", "OK");
                return;
            }

            string splatMatPath = matDir + "/FluidSplat_Mat.mat";
            Material splatMat = AssetDatabase.LoadAssetAtPath<Material>(splatMatPath);
            if (splatMat == null)
            {
                splatMat = new Material(splatShader);
                splatMat.SetColor("_Color", new Color(0.55f, 0.88f, 0.92f, 0.62f));
                splatMat.SetFloat("_ParticleRadius", 0.08f);
                AssetDatabase.CreateAsset(splatMat, splatMatPath);
            }

            string metaballMatPath = matDir + "/FluidMetaball_Mat.mat";
            Material metaballMat = AssetDatabase.LoadAssetAtPath<Material>(metaballMatPath);
            if (metaballMat == null)
            {
                metaballMat = new Material(metaballShader);
                metaballMat.SetColor("_WaterColor", new Color(0.55f, 0.88f, 0.92f, 0.62f));
                metaballMat.SetColor("_DeepColor", new Color(0.18f, 0.55f, 0.62f, 0.88f));
                metaballMat.SetColor("_FoamColor", new Color(0.92f, 0.98f, 1.0f, 0.55f));
                metaballMat.SetFloat("_Threshold", 0.38f);
                metaballMat.SetColor("_OutlineColor", new Color(0.88f, 0.97f, 0.99f, 0.85f));
                metaballMat.SetFloat("_OutlineWidth", 0.045f);
                AssetDatabase.CreateAsset(metaballMat, metaballMatPath);
            }

            // Load Compute Shader
            ComputeShader simCS = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/_Project/Bao/Fluid2D/Shaders/FluidSimulation.compute");

            // 4. Khởi tạo cấu trúc GameObject trong Scene
            GameObject fluidSystemObj = new GameObject("=== Fluid2D System ===");
            Undo.RegisterCreatedObjectUndo(fluidSystemObj, "Create Fluid2D System");

            Fluid2DSystem sys = fluidSystemObj.AddComponent<Fluid2DSystem>();
            sys.settings = settings;
            sys.simulationShader = simCS;
            sys.particleMaterial = splatMat;

            Fluid2DObstacleManager obsMgr = fluidSystemObj.AddComponent<Fluid2DObstacleManager>();
            obsMgr.fluidSystem = sys;
            obsMgr.obstacleLayer = LayerMask.GetMask("Default", "Water"); // Chọn layer va chạm địa hình mặc định
            sys.obstacleManager = obsMgr;

            Fluid2DMetaballRenderer metaRender = fluidSystemObj.AddComponent<Fluid2DMetaballRenderer>();
            metaRender.mainCamera = Camera.main;
            metaRender.metaballMaterial = metaballMat;
            metaRender.waterLayer = 16;

            fluidSystemObj.layer = 16;

            // 5. Tạo vòi phát nước (Emitter)
            GameObject emitterObj = new GameObject("Fluid Emitter");
            emitterObj.transform.position = new Vector3(-2f, 5.5f, 0f); // Phun nước lệch trái để nước chảy xuống dốc trái
            FluidEmitter2D emitter = emitterObj.AddComponent<FluidEmitter2D>();
            emitter.fluidSystem = sys;
            emitter.emitRate = 180f; // Tăng lượng nước
            emitter.initialVelocity = new Vector2(2f, -5f); // Phun chéo phải xuống dốc
            emitter.emitWidth = 0.4f;

            // 6. Tạo hố hút nước (Drain) ở đáy sâu bên ngoài bể để dọn dẹp hạt tràn
            GameObject drainObj = new GameObject("Fluid Drain");
            drainObj.transform.position = new Vector3(0f, -7.5f, 0f);
            BoxCollider2D drainCol = drainObj.AddComponent<BoxCollider2D>();
            drainCol.size = new Vector2(30f, 1f);
            drainCol.isTrigger = true;
            
            FluidDrain2D drain = drainObj.AddComponent<FluidDrain2D>();
            drain.fluidSystem = sys;

            // 7. Tạo vật liệu cho địa hình tĩnh và vật thể động để trực quan hóa
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");
            
            Material terrainMat = new Material(unlitShader);
            terrainMat.color = new Color(0.25f, 0.25f, 0.28f, 1f); // Màu xám đá tối

            Material boxMat = new Material(unlitShader);
            boxMat.color = new Color(0.8f, 0.45f, 0.15f, 1f); // Màu gỗ cam ấm

            Material ballMat = new Material(unlitShader);
            ballMat.color = new Color(0.9f, 0.2f, 0.3f, 1f); // Màu đỏ bóng

            // 8. Tạo chướng ngại vật tĩnh (Terrain) bao gồm bể chứa và các dốc dẫn nước
            GameObject terrainGroup = new GameObject("Static Obstacles (Terrain)");
            
            // Dốc trượt trên bên trái (để nước phun vào đây rồi chảy xuống)
            GameObject slopeL = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(slopeL.GetComponent<MeshCollider>());
            slopeL.name = "Upper Slope Left";
            slopeL.transform.SetParent(terrainGroup.transform);
            slopeL.transform.position = new Vector3(-4f, 2.5f, 0f);
            slopeL.transform.rotation = Quaternion.Euler(0f, 0f, -25f);
            slopeL.transform.localScale = new Vector3(7f, 0.4f, 1f);
            slopeL.GetComponent<MeshRenderer>().material = terrainMat;
            BoxCollider2D colL = slopeL.AddComponent<BoxCollider2D>();
            colL.size = Vector2.one;

            // Dốc trượt trên bên phải
            GameObject slopeR = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(slopeR.GetComponent<MeshCollider>());
            slopeR.name = "Upper Slope Right";
            slopeR.transform.SetParent(terrainGroup.transform);
            slopeR.transform.position = new Vector3(4f, 0.5f, 0f);
            slopeR.transform.rotation = Quaternion.Euler(0f, 0f, 20f);
            slopeR.transform.localScale = new Vector3(7f, 0.4f, 1f);
            slopeR.GetComponent<MeshRenderer>().material = terrainMat;
            BoxCollider2D colR = slopeR.AddComponent<BoxCollider2D>();
            colR.size = Vector2.one;

            // XÂY DỰNG BỂ CHỨA NƯỚC TĨNH HÌNH CHỮ U Ở ĐÁY (Đỡ nước và đỡ vật nổi)
            GameObject containerGroup = new GameObject("U-Shaped Container");
            containerGroup.transform.SetParent(terrainGroup.transform);

            // Đáy bể chứa
            GameObject bottomWall = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(bottomWall.GetComponent<MeshCollider>());
            bottomWall.name = "Container Bottom";
            bottomWall.transform.SetParent(containerGroup.transform);
            bottomWall.transform.position = new Vector3(0f, -4.5f, 0f);
            bottomWall.transform.localScale = new Vector3(12f, 0.4f, 1f);
            bottomWall.GetComponent<MeshRenderer>().material = terrainMat;
            BoxCollider2D bottomCol = bottomWall.AddComponent<BoxCollider2D>();
            bottomCol.size = Vector2.one;

            // Vách bể trái (làm cao hơn lên y = -1.0 để giữ vật thể tốt hơn)
            GameObject leftWall = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(leftWall.GetComponent<MeshCollider>());
            leftWall.name = "Container Wall Left";
            leftWall.transform.SetParent(containerGroup.transform);
            leftWall.transform.position = new Vector3(-6f, -1.0f, 0f);
            leftWall.transform.localScale = new Vector3(0.4f, 7.4f, 1f);
            leftWall.GetComponent<MeshRenderer>().material = terrainMat;
            BoxCollider2D leftCol = leftWall.AddComponent<BoxCollider2D>();
            leftCol.size = Vector2.one;

            // Vách bể phải (làm cao hơn lên y = -1.0)
            GameObject rightWall = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(rightWall.GetComponent<MeshCollider>());
            rightWall.name = "Container Wall Right";
            rightWall.transform.SetParent(containerGroup.transform);
            rightWall.transform.position = new Vector3(6f, -1.0f, 0f);
            rightWall.transform.localScale = new Vector3(0.4f, 7.4f, 1f);
            rightWall.GetComponent<MeshRenderer>().material = terrainMat;
            BoxCollider2D rightCol = rightWall.AddComponent<BoxCollider2D>();
            rightCol.size = Vector2.one;

            // Bức tường bảo hiểm biên trái màn hình (World Bound Wall Left)
            GameObject worldWallL = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(worldWallL.GetComponent<MeshCollider>());
            worldWallL.name = "World Safety Wall Left";
            worldWallL.transform.SetParent(terrainGroup.transform);
            worldWallL.transform.position = new Vector3(-12.5f, 0f, 0f);
            worldWallL.transform.localScale = new Vector3(0.5f, 16f, 1f);
            worldWallL.GetComponent<MeshRenderer>().material = terrainMat;
            BoxCollider2D worldWallLCol = worldWallL.AddComponent<BoxCollider2D>();
            worldWallLCol.size = Vector2.one;

            // Bức tường bảo hiểm biên phải màn hình (World Bound Wall Right)
            GameObject worldWallR = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(worldWallR.GetComponent<MeshCollider>());
            worldWallR.name = "World Safety Wall Right";
            worldWallR.transform.SetParent(terrainGroup.transform);
            worldWallR.transform.position = new Vector3(12.5f, 0f, 0f);
            worldWallR.transform.localScale = new Vector3(0.5f, 16f, 1f);
            worldWallR.GetComponent<MeshRenderer>().material = terrainMat;
            BoxCollider2D worldWallRCol = worldWallR.AddComponent<BoxCollider2D>();
            worldWallRCol.size = Vector2.one;

            // 9. Tạo vật thể nổi tương tác (Fluid2DObject) đặt trong bể nước
            GameObject floatGroup = new GameObject("Dynamic Objects (Floating)");
            
            // Hộp gỗ nổi (Wooden Box)
            GameObject boxObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(boxObj.GetComponent<MeshCollider>());
            boxObj.name = "Floating Wooden Box";
            boxObj.transform.SetParent(floatGroup.transform);
            boxObj.transform.position = new Vector3(2.0f, -3.8f, 0f); // Đặt ở đáy bể
            boxObj.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
            boxObj.GetComponent<MeshRenderer>().material = boxMat;
            
            BoxCollider2D boxCol = boxObj.AddComponent<BoxCollider2D>();
            boxCol.size = Vector2.one;
            
            Rigidbody2D boxRB = boxObj.AddComponent<Rigidbody2D>();
            boxRB.mass = 1.5f;
            boxRB.linearDamping = 0.5f;
            boxRB.angularDamping = 0.5f;
            
            Fluid2DObject fObj1 = boxObj.AddComponent<Fluid2DObject>();
            fObj1.fluidSystem = sys;
            fObj1.buoyancyMultiplier = 1.4f;

            // Quả cầu nổi (Ball)
            GameObject ballObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(ballObj.GetComponent<MeshCollider>());
            ballObj.name = "Floating Ball";
            ballObj.transform.SetParent(floatGroup.transform);
            ballObj.transform.position = new Vector3(-2.0f, -3.8f, 0f); // Đặt ở đáy bể
            ballObj.transform.localScale = new Vector3(1.0f, 1.0f, 1f);
            ballObj.GetComponent<MeshRenderer>().material = ballMat;
            
            CircleCollider2D ballCol = ballObj.AddComponent<CircleCollider2D>();
            ballCol.radius = 0.5f;
            
            Rigidbody2D ballRB = ballObj.AddComponent<Rigidbody2D>();
            ballRB.mass = 0.8f;
            ballRB.linearDamping = 0.3f;
            
            Fluid2DObject fObj2 = ballObj.AddComponent<Fluid2DObject>();
            fObj2.fluidSystem = sys;
            fObj2.buoyancyMultiplier = 1.8f;

            // Đăng ký các thay đổi để Undo hoạt động
            Undo.RegisterCreatedObjectUndo(emitterObj, "Create Emitter");
            Undo.RegisterCreatedObjectUndo(drainObj, "Create Drain");
            Undo.RegisterCreatedObjectUndo(terrainGroup, "Create Terrain");
            Undo.RegisterCreatedObjectUndo(floatGroup, "Create Floating Objects");

            // Kích hoạt quét chướng ngại vật ngay lập tức
            obsMgr.RefreshObstacles();

            Selection.activeGameObject = fluidSystemObj;
            EditorUtility.DisplayDialog("Thành công", "Đã tạo cấu hình demo hệ thống chất lỏng hạt GPU Fluid2D thành công trong Scene của bạn!", "OK");
        }
    }
}
#endif
