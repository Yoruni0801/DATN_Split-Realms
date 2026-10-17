using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bao.TempleWard
{
    /// <summary>
    /// Rebuilds the full Ancient Temple Ward rig (fitted barrier + particles + quads)
    /// on the "shield" object of the currently open scene. Safe to re-run any time —
    /// it tears down the previous shield_FX first. Assets must already exist in
    /// Assets/_Project/Bao/FX/TempleWard (SDF texture, materials, contour mesh).
    /// </summary>
    public static class TempleWardSetup
    {
        private const string Dir = "Assets/_Project/Bao/FX/TempleWard";
        private static readonly Vector3 TempleCenter = new Vector3(-189.4f, 11.5f, 0.0012f);
        private const float QuadScale = 25f;

        [MenuItem("Tools/Bao/Setup Temple Ward")]
        public static void Setup()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[TempleWard] Exit play mode first — setup must run in edit mode.");
                return;
            }

            var shield = GameObject.Find("shield");
            if (shield == null)
            {
                Debug.LogError("[TempleWard] 'shield' object not found in the active scene.");
                return;
            }

            var matBarrier = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/M_TempleBarrierFitted.mat");
            var matGlow = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/M_SoftGlow.mat");
            var matSpark = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/M_Spark4.mat");
            var matWisp = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/M_WispStreak.mat");
            var matMote = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/M_Mote.mat");
            var matRays = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/M_GodRays.mat");
            var ribbon = AssetDatabase.LoadAssetAtPath<Mesh>(Dir + "/TempleContourRibbon.asset");
            if (matBarrier == null || matGlow == null || matSpark == null || matWisp == null ||
                matMote == null || matRays == null || ribbon == null)
            {
                Debug.LogError("[TempleWard] Required assets are missing in " + Dir);
                return;
            }

            // ---- shield: fitted barrier over the temple silhouette ----
            Undo.RecordObject(shield.transform, "Setup Temple Ward");
            shield.transform.position = TempleCenter;
            shield.transform.localScale = new Vector3(QuadScale, QuadScale, 1f);
            var sr = shield.GetComponent<SpriteRenderer>();
            Undo.RecordObject(sr, "Setup Temple Ward");
            var square = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/Square.png");
            if (square != null) sr.sprite = square;
            sr.sharedMaterial = matBarrier;

            // ---- fresh FX root, counter-scaled so children use world units ----
            var old = shield.transform.Find("shield_FX");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var stray = GameObject.Find("shield_FX");
            if (stray != null) Undo.DestroyObjectImmediate(stray);

            var root = new GameObject("shield_FX");
            Undo.RegisterCreatedObjectUndo(root, "Setup Temple Ward");
            root.transform.SetParent(shield.transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localScale = new Vector3(1f / QuadScale, 1f / QuadScale, 1f);

            // ---- particles ----
            var embers = MakePS(root, "PS_EdgeEmbers", Vector3.zero, matMote, 4);
            {
                var m = embers.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.4f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.60f);
                m.startColor = new Color(1f, 0.55f, 0.14f, 1f);
                m.maxParticles = 200;
                var em = embers.emission; em.rateOverTime = 34f;
                UseRibbonShape(embers, ribbon);
                SetVerticalVelocity(embers, 0.10f, 0.30f);
                SetNoise(embers, 0.3f, 0.6f, 0.15f);
                var g = new Gradient();
                g.SetKeys(
                    new[] { new GradientColorKey(new Color(1f, 0.72f, 0.25f), 0f),
                            new GradientColorKey(new Color(1f, 0.42f, 0.06f), 0.6f),
                            new GradientColorKey(new Color(0.75f, 0.18f, 0.02f), 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f) });
                var col = embers.colorOverLifetime; col.enabled = true; col.color = g;
                SetSizeCurve(embers, new Keyframe(0f, 0.6f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f));
            }

            var flies = MakePS(root, "PS_Fireflies", Vector3.zero, matGlow, 3);
            {
                var m = flies.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 6.5f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.36f);
                m.startColor = new Color(0.30f, 1f, 0.55f, 1f);
                m.maxParticles = 120;
                var em = flies.emission; em.rateOverTime = 14f;
                var sh = flies.shape; sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Box;
                sh.scale = new Vector3(15f, 13f, 1f);
                sh.position = new Vector3(0f, -1.5f, 0f);
                SetVerticalVelocity(flies, 0.10f, 0.28f);
                SetNoise(flies, 0.5f, 0.3f, 0.08f);
                SetBlinkGradient(flies, new Color(0.30f, 1f, 0.55f));
            }

            var wisps = MakePS(root, "PS_EnergyWisps", Vector3.zero, matWisp, 3);
            {
                var m = wisps.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
                m.startSpeed = 0f;
                m.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.38f);
                m.startColor = new Color(0.18f, 0.95f, 0.55f, 0.5f);
                m.maxParticles = 40;
                var em = wisps.emission; em.rateOverTime = 3f;
                UseRibbonShape(wisps, ribbon);
                SetVerticalVelocity(wisps, 0.30f, 0.55f);
                SetFadeGradient(wisps, new Color(0.18f, 0.95f, 0.55f), 0.6f);
                var r = wisps.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.lengthScale = 6f;
                r.velocityScale = 0f;
            }

            var mist = MakePS(root, "PS_BaseMist", new Vector3(0f, -10.3f, 0f), matGlow, 1);
            {
                var m = mist.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 5.5f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.10f);
                m.startSize = new ParticleSystem.MinMaxCurve(2.4f, 4.6f);
                m.startColor = new Color(0.12f, 0.60f, 0.32f, 0.22f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.maxParticles = 60;
                var em = mist.emission; em.rateOverTime = 6f;
                var sh = mist.shape; sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Box;
                sh.scale = new Vector3(17f, 1.4f, 1f);
                SetVerticalVelocity(mist, 0.06f, 0.16f);
                SetFadeGradient(mist, new Color(0.12f, 0.60f, 0.32f), 1f);
                SetSizeCurve(mist, new Keyframe(0f, 0.7f), new Keyframe(1f, 1.5f));
                var rot = mist.rotationOverLifetime; rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            }

            var twinkles = MakePS(root, "PS_RuneTwinkles", Vector3.zero, matSpark, 4);
            {
                var m = twinkles.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
                m.startSpeed = 0f;
                m.startSize = new ParticleSystem.MinMaxCurve(0.50f, 1.05f);
                m.startColor = new Color(1f, 0.75f, 0.28f, 1f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.maxParticles = 30;
                var em = twinkles.emission; em.rateOverTime = 4.5f;
                UseRibbonShape(twinkles, ribbon);
                SetFadeGradient(twinkles, new Color(1f, 0.75f, 0.28f), 1f);
                SetSizeCurve(twinkles, new Keyframe(0f, 0f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f));
                var rot = twinkles.rotationOverLifetime; rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            }

            var dust = MakePS(root, "PS_CanopyDust", Vector3.zero, matGlow, 1);
            {
                var m = dust.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.05f);
                m.startSize = new ParticleSystem.MinMaxCurve(3.0f, 6.0f);
                m.startColor = new Color(0.30f, 0.80f, 0.50f, 0.085f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.maxParticles = 30;
                var em = dust.emission; em.rateOverTime = 1.6f;
                var sh = dust.shape; sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Box;
                sh.scale = new Vector3(14f, 11f, 1f);
                sh.position = new Vector3(0f, -1.5f, 0f);
                SetNoise(dust, 0.15f, 0.2f, 0f);
                SetFadeGradient(dust, new Color(0.30f, 0.80f, 0.50f), 1f);
            }

            // ---- shader-driven quads ----
            MakeQuad(root, "VFX_GodRays", Vector3.zero, new Vector3(QuadScale, QuadScale, 1f), matRays, 1);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(shield.scene);
            Debug.Log("[TempleWard] Setup complete: fitted barrier + 6 particle systems + god rays quad.");
        }

        private static ParticleSystem MakePS(GameObject root, string name, Vector3 localPos, Material mat, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.loop = true; m.prewarm = true; m.playOnAwake = true;
            m.gravityModifier = 0f;
            m.simulationSpace = ParticleSystemSimulationSpace.Local;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.sortingLayerName = "4";
            r.sortingOrder = order;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        private static void UseRibbonShape(ParticleSystem ps, Mesh ribbon)
        {
            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Mesh;
            sh.mesh = ribbon;
            sh.meshShapeType = ParticleSystemMeshShapeType.Triangle;
        }

        /// <summary>
        /// Vertical drift with EVERY curve in the module set to TwoConstants mode.
        /// Mixing constant and two-constant curves inside one module triggers
        /// "curves must share the same mode" errors in the inspector.
        /// </summary>
        private static void SetVerticalVelocity(ParticleSystem ps, float min, float max)
        {
            var v = ps.velocityOverLifetime;
            v.enabled = true;
            v.space = ParticleSystemSimulationSpace.Local;
            v.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            v.y = new ParticleSystem.MinMaxCurve(min, max);
            v.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            v.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            v.orbitalY = new ParticleSystem.MinMaxCurve(0f, 0f);
            v.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
            v.orbitalOffsetX = new ParticleSystem.MinMaxCurve(0f, 0f);
            v.orbitalOffsetY = new ParticleSystem.MinMaxCurve(0f, 0f);
            v.orbitalOffsetZ = new ParticleSystem.MinMaxCurve(0f, 0f);
            v.radial = new ParticleSystem.MinMaxCurve(0f, 0f);
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

        private static void MakeQuad(GameObject root, string name, Vector3 localPos, Vector3 localScale, Material mat, int order)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            var col = q.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            Undo.RegisterCreatedObjectUndo(q, "Setup Temple Ward");
            q.transform.SetParent(root.transform, false);
            q.transform.localPosition = localPos;
            q.transform.localScale = localScale;
            var mr = q.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.sortingLayerName = "4";
            mr.sortingOrder = order;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }
    }
}
