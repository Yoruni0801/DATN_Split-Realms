using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bao.BossGate
{
    /// <summary>
    /// Rebuilds the boss-summon-portal VFX rig on the "BossGate" object of the
    /// currently open scene (scene TF gets discarded on merges, so the rig must be
    /// reproducible from this one menu item). Generates its own textures/materials
    /// in Assets/_Project/Bao/FX/BossGate on first run; reuses the TempleWard
    /// particle textures for glows/sparks/streaks. Safe to re-run any time —
    /// it tears down the previous BossGateVFX root first.
    /// </summary>
    public static class BossGateVFXSetup
    {
        private const string Dir = "Assets/_Project/Bao/FX/BossGate";
        private const string WardDir = "Assets/_Project/Bao/FX/TempleWard";

        // World-space geometry of the portal visual (root quad is 1x1 scaled x5,
        // ring sits at UV 0.405 of the half-size => ~2.0 world units).
        private const float RimRadius = 2.05f;

        [MenuItem("Tools/Bao/Setup Boss Gate VFX")]
        public static void Setup()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[BossGateVFX] Exit play mode first — setup must run in edit mode.");
                return;
            }

            var gate = GameObject.Find("BossGate");
            if (gate == null)
            {
                Debug.LogError("[BossGateVFX] 'BossGate' object not found in the active scene.");
                return;
            }

            var shaderAdd = Shader.Find("Custom/2D/Boss Gate Particle Additive");
            var shaderAlpha = Shader.Find("Custom/2D/Boss Gate Particle Alpha");
            if (shaderAdd == null || shaderAlpha == null)
            {
                Debug.LogError("[BossGateVFX] Boss Gate particle shaders not compiled yet.");
                return;
            }

            var texGlow = AssetDatabase.LoadAssetAtPath<Texture2D>(WardDir + "/T_SoftGlow.png");
            var texMote = AssetDatabase.LoadAssetAtPath<Texture2D>(WardDir + "/T_Mote.png");
            var texSpark = AssetDatabase.LoadAssetAtPath<Texture2D>(WardDir + "/T_Spark4.png");
            var texWisp = AssetDatabase.LoadAssetAtPath<Texture2D>(WardDir + "/T_WispStreak.png");
            if (texGlow == null || texMote == null || texSpark == null || texWisp == null)
            {
                Debug.LogError("[BossGateVFX] TempleWard particle textures are missing in " + WardDir);
                return;
            }
            var texLeaf = EnsureLeafTexture();

            EnsureFolder(Dir);
            var matVeil = EnsureMaterial("M_BG_VeilAlpha", shaderAlpha, texGlow, 1.0f);
            var matGlowAdd = EnsureMaterial("M_BG_GlowAdd", shaderAdd, texGlow, 1.8f);
            var matFlare = EnsureMaterial("M_BG_FlareAdd", shaderAdd, texGlow, 2.4f);
            var matWispAdd = EnsureMaterial("M_BG_WispAdd", shaderAdd, texWisp, 2.4f);
            var matMoteAdd = EnsureMaterial("M_BG_MoteAdd", shaderAdd, texMote, 2.6f);
            var matSparkAdd = EnsureMaterial("M_BG_SparkAdd", shaderAdd, texSpark, 2.2f);
            var matLeaf = EnsureMaterial("M_BG_LeafAlpha", shaderAlpha, texLeaf, 1.0f);

            // ---- fresh FX root, counter-scaled so children work in world units ----
            var old = gate.transform.Find("BossGateVFX");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var root = new GameObject("BossGateVFX");
            Undo.RegisterCreatedObjectUndo(root, "Setup Boss Gate VFX");
            root.transform.SetParent(gate.transform, false);
            root.transform.localPosition = Vector3.zero;
            var lossy = gate.transform.lossyScale;
            root.transform.localScale = new Vector3(
                1f / Mathf.Max(Mathf.Abs(lossy.x), 0.0001f),
                1f / Mathf.Max(Mathf.Abs(lossy.y), 0.0001f),
                1f);

            // ---- dark corruption veil behind everything ----
            var veil = MakePS(root, "PS_VoidVeil", matVeil, 1);
            {
                var m = veil.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
                m.startSize = new ParticleSystem.MinMaxCurve(2.8f, 4.6f);
                m.startColor = new Color(0.09f, 0.012f, 0.13f, 0.42f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.maxParticles = 40;
                var em = veil.emission; em.rateOverTime = 7f;
                SetCircleShape(veil, 1.6f, 1f);
                SetFadeGradient(veil, new Color(0.09f, 0.012f, 0.13f), 0.42f);
                SetSizeCurve(veil, new Keyframe(0f, 0.8f), new Keyframe(1f, 1.25f));
                var rot = veil.rotationOverLifetime; rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
            }

            // ---- breathing magenta halo hugging the ring (fake light) ----
            var bloom = MakePS(root, "PS_AuraBloom", matGlowAdd, 2);
            {
                var m = bloom.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
                m.startSpeed = 0f;
                m.startSize = new ParticleSystem.MinMaxCurve(1.7f, 2.5f);
                m.startColor = new Color(1f, 0.08f, 0.45f, 0.13f);
                m.maxParticles = 30;
                var em = bloom.emission; em.rateOverTime = 5f;
                SetCircleShape(bloom, 2.0f, 0.2f);
                SetFadeGradient(bloom, new Color(1f, 0.08f, 0.45f), 0.13f);
                SetSizeCurve(bloom, new Keyframe(0f, 0.8f), new Keyframe(0.5f, 1.05f), new Keyframe(1f, 0.8f));
            }

            // ---- hot core flare backing the void center ----
            var flare = MakePS(root, "PS_CoreFlare", matFlare, 2);
            {
                var m = flare.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 2.6f);
                m.startSpeed = 0f;
                m.startSize = new ParticleSystem.MinMaxCurve(1.3f, 1.8f);
                m.startColor = new Color(1f, 0.45f, 0.75f, 0.35f);
                m.maxParticles = 8;
                var em = flare.emission; em.rateOverTime = 2.5f;
                SetCircleShape(flare, 0.12f, 1f);
                SetFadeGradient(flare, new Color(1f, 0.45f, 0.75f), 0.35f);
                SetSizeCurve(flare, new Keyframe(0f, 0.75f), new Keyframe(0.5f, 1.1f), new Keyframe(1f, 0.75f));
            }

            // ---- energy wisps spiralling INTO the gate ----
            var wisps = MakePS(root, "PS_InfallWisps", matWispAdd, 5);
            {
                var m = wisps.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.9f);
                m.startSpeed = 0f;
                m.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.30f);
                m.startColor = new Color(1f, 0.07f, 0.50f, 0.70f);
                m.maxParticles = 80;
                var em = wisps.emission; em.rateOverTime = 26f;
                SetCircleShape(wisps, 3.7f, 0.15f);
                SetSpiralVelocity(wisps, -2.4f, -1.7f, 1.2f, 2.0f, 0f, 0f);
                SetFadeGradient(wisps, new Color(1f, 0.07f, 0.50f), 0.70f);
                var r = wisps.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.lengthScale = 5f;
                r.velocityScale = 0f;
            }

            // ---- orange embers swirling off the rim ----
            var embers = MakePS(root, "PS_Embers", matMoteAdd, 5);
            {
                var m = embers.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3.0f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
                m.startColor = new Color(1f, 0.42f, 0.06f, 1f);
                m.maxParticles = 120;
                var em = embers.emission; em.rateOverTime = 34f;
                SetCircleShape(embers, RimRadius, 0.12f);
                SetSpiralVelocity(embers, 0.05f, 0.15f, 0.5f, 0.9f, 0.15f, 0.35f);
                SetNoise(embers, 0.25f, 0.5f, 0.1f);
                SetBlinkGradient(embers, new Color(1f, 0.55f, 0.10f));
                SetSizeCurve(embers, new Keyframe(0f, 0.5f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0f));
            }

            // ---- electric glints popping on the ring ----
            var sparks = MakePS(root, "PS_ArcSparks", matSparkAdd, 6);
            {
                var m = sparks.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
                m.startSpeed = 0f;
                m.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.70f);
                m.startColor = new Color(1f, 0.50f, 0.80f, 1f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.maxParticles = 30;
                var em = sparks.emission; em.rateOverTime = 7f;
                SetCircleShape(sparks, RimRadius, 0.06f);
                SetFadeGradient(sparks, new Color(1f, 0.50f, 0.80f), 1f);
                SetSizeCurve(sparks, new Keyframe(0f, 0f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0f));
                var rot = sparks.rotationOverLifetime; rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            }

            // ---- forest leaves torn loose and sucked into the portal ----
            var leaves = MakePS(root, "PS_Leaves", matLeaf, 6);
            {
                var m = leaves.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
                m.startSpeed = 0f;
                m.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.16f);
                m.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.38f, 0.58f, 0.16f, 0.95f),
                    new Color(0.78f, 0.76f, 0.28f, 0.95f));
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.maxParticles = 60;
                var em = leaves.emission; em.rateOverTime = 9f;
                SetCircleShape(leaves, 5.4f, 0.45f);
                SetSpiralVelocity(leaves, -2.6f, -1.7f, 0.8f, 1.4f, 0f, 0f);
                SetNoise(leaves, 0.35f, 0.6f, 0.2f);
                SetFadeGradient(leaves, Color.white, 0.95f);
                SetSizeCurve(leaves, new Keyframe(0f, 1f), new Keyframe(0.75f, 0.9f), new Keyframe(1f, 0.35f));
                var rot = leaves.rotationOverLifetime; rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
            }

            // ---- timeline fade bridge on the gate root ----
            if (gate.GetComponent<BossGateTimelineFade>() == null)
                Undo.AddComponent<BossGateTimelineFade>(gate);

            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gate.scene);
            Debug.Log("[BossGateVFX] Setup complete: 7 particle systems + timeline fade bridge on BossGate.");
        }

        // ------------------------------------------------------------------ helpers

        private static ParticleSystem MakePS(GameObject root, string name, Material mat, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = Vector3.zero;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.loop = true; m.prewarm = true; m.playOnAwake = true;
            m.gravityModifier = 0f;
            m.simulationSpace = ParticleSystemSimulationSpace.Local;
            // Cutscene camera pans away while the gate stays alive — never freeze.
            m.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.sortingLayerName = "Default";
            r.sortingOrder = order;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        private static void SetCircleShape(ParticleSystem ps, float radius, float radiusThickness)
        {
            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = radius;
            sh.radiusThickness = radiusThickness;
        }

        /// <summary>
        /// Inward/orbital drift with EVERY curve in the module set to TwoConstants mode
        /// (mixing modes inside one module triggers inspector errors).
        /// </summary>
        private static void SetSpiralVelocity(ParticleSystem ps,
            float radialMin, float radialMax,
            float orbitalMin, float orbitalMax,
            float upMin, float upMax)
        {
            var v = ps.velocityOverLifetime;
            v.enabled = true;
            v.space = ParticleSystemSimulationSpace.Local;
            v.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            v.y = new ParticleSystem.MinMaxCurve(upMin, upMax);
            v.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            v.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            v.orbitalY = new ParticleSystem.MinMaxCurve(0f, 0f);
            v.orbitalZ = new ParticleSystem.MinMaxCurve(orbitalMin, orbitalMax);
            v.orbitalOffsetX = new ParticleSystem.MinMaxCurve(0f, 0f);
            v.orbitalOffsetY = new ParticleSystem.MinMaxCurve(0f, 0f);
            v.orbitalOffsetZ = new ParticleSystem.MinMaxCurve(0f, 0f);
            v.radial = new ParticleSystem.MinMaxCurve(radialMin, radialMax);
            v.speedModifier = new ParticleSystem.MinMaxCurve(1f, 1f);
        }

        private static void SetNoise(ParticleSystem ps, float strength, float frequency, float scroll)
        {
            var n = ps.noise;
            n.enabled = true;
            n.strength = strength;
            n.frequency = frequency;
            n.scrollSpeed = scroll;
            n.damping = true;
        }

        private static void SetSizeCurve(ParticleSystem ps, params Keyframe[] keys)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(keys));
        }

        private static void SetFadeGradient(ParticleSystem ps, Color c, float peak)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, 0.25f),
                        new GradientAlphaKey(peak, 0.75f), new GradientAlphaKey(0f, 1f) });
            var col = ps.colorOverLifetime; col.enabled = true; col.color = g;
        }

        private static void SetBlinkGradient(ParticleSystem ps, Color c)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f),
                        new GradientAlphaKey(0.25f, 0.45f), new GradientAlphaKey(0.9f, 0.70f),
                        new GradientAlphaKey(0f, 1f) });
            var col = ps.colorOverLifetime; col.enabled = true; col.color = g;
        }

        private static Material EnsureMaterial(string name, Shader shader, Texture2D tex, float intensity)
        {
            string path = Dir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(shader);
            mat.SetTexture("_MainTex", tex);
            mat.SetFloat("_Intensity", intensity);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>Procedural pointed-oval leaf with a darker center vein, white so
        /// the particle start color tints it to any foliage shade.</summary>
        private static Texture2D EnsureLeafTexture()
        {
            string path = Dir + "/T_Leaf.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            EnsureFolder(Dir);
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float f = Mathf.Pow(Mathf.Abs(u) / 0.42f, 1.35f)
                            + Mathf.Pow(Mathf.Abs(v) / 0.92f, 1.35f);
                    float alpha = Mathf.Clamp01((1f - f) / 0.15f);
                    float vein = 1f - 0.28f * Mathf.Exp(-u * u / 0.003f);
                    byte b = (byte)(Mathf.Clamp01(vein) * 255f);
                    pixels[y * size + x] = new Color32(b, b, b, (byte)(alpha * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
