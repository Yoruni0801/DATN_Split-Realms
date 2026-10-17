using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class LaserMirrorSceneBootstrap2D : MonoBehaviour
{
    private static Sprite cachedCrystalGlowSprite;

    [System.Serializable]
    public struct CrystalSetup
    {
        public string name;
        // Scene object used as this crystal's position anchor.
        public GameObject positionObject;
        public Vector2 localPosition;
        // Optional sprite shown by this crystal.
        public Sprite sprite;
        public bool source;
        public bool goal;
        public bool reflector;
        public LaserMirrorCrystal2D.FaceDirection sourceDirection;
        public LaserMirrorCrystal2D.FaceDirection facing;
        public float[] customAngles;
        public Transform[] aimTargets;
    }

    [System.Serializable]
    public struct SwitchSetup
    {
        public string name;
        // Scene object used as this control pillar's position anchor.
        public GameObject positionObject;
        public Vector2 localPosition;
        // Stone pedestal base sprite. Kept as offSprite for prefab compatibility.
        public Sprite offSprite;
        // Rotary dial sprite. Kept as onSprite for prefab compatibility.
        public Sprite onSprite;
        public int controlledCrystalIndex;
    }

    [Header("Generation Settings")]
    [SerializeField] private bool buildOnAwake = true;
    [SerializeField] private Vector2 portalLocalPosition = new Vector2(4.5f, 2.35f);
    [SerializeField] private string targetSceneName;
    [SerializeField] private Material portalMaterial;
    [SerializeField] private Sprite switchOffSprite;
    [SerializeField] private Sprite switchOnSprite;
    [SerializeField] private Sprite crystalSprite;
    [SerializeField] private Sprite goalSprite;

    [Header("Ambient Atmosphere VFX")]
    [SerializeField] private bool enableAmbientDust = true;
    [SerializeField] private bool enableFloorMist = true;

    [Header("Nodes & Control Pillars Layout (Local Coordinates)")]
    [SerializeField] private CrystalSetup[] crystals;
    [SerializeField] private SwitchSetup[] switches;
    [SerializeField] private GameObject solvedObject;

    private void Reset()
    {
        SetupDefaultArchLayout();
    }

    [ContextMenu("Setup Arch Layout (Default Spacing)")]
    public void SetupDefaultArchLayout()
    {
        portalLocalPosition = new Vector2(4.5f, 2.35f);

        // Vòm cổng 7 điểm bên phải gốc Prefab
        crystals = new[]
        {
            new CrystalSetup
            {
                name = "Crystal_0_Source_LeftBase",
                localPosition = new Vector2(1.0f, 0.0f),
                source = true,
                reflector = false,
                customAngles = new[] { 76f, 0f, 180f, 270f },
            },
            new CrystalSetup
            {
                name = "Crystal_1_Arch_LeftLow",
                localPosition = new Vector2(1.5f, 2.0f),
                reflector = true,
                customAngles = new[] { 180f, 51f, 270f, 0f },
            },
            new CrystalSetup
            {
                name = "Crystal_2_Arch_LeftHigh",
                localPosition = new Vector2(2.8f, 3.6f),
                reflector = true,
                customAngles = new[] { 270f, 180f, 25f, 90f },
            },
            new CrystalSetup
            {
                name = "Crystal_3_Arch_TopPeak",
                localPosition = new Vector2(4.5f, 4.4f),
                reflector = true,
                customAngles = new[] { 90f, 180f, 270f, 335f },
            },
            new CrystalSetup
            {
                name = "Crystal_4_Arch_RightHigh",
                localPosition = new Vector2(6.2f, 3.6f),
                reflector = true,
                customAngles = new[] { 0f, 309f, 90f, 180f },
            },
            new CrystalSetup
            {
                name = "Crystal_5_Arch_RightLow",
                localPosition = new Vector2(7.5f, 2.0f),
                reflector = true,
                customAngles = new[] { 284f, 180f, 90f, 0f },
            },
            new CrystalSetup
            {
                name = "Crystal_6_Goal_RightBase",
                localPosition = new Vector2(8.0f, 0.0f),
                goal = true,
                reflector = false,
                customAngles = new[] { 0f }
            }
        };

        // 4 trụ điều khiển cách nhau rộng rãi (Khoảng cách 2.5m - 3m mỗi trụ)
        switches = new[]
        {
            new SwitchSetup
            {
                name = "Pillar_1_Rotate_LeftLow",
                localPosition = new Vector2(-10.0f, -1.0f),
                controlledCrystalIndex = 1
            },
            new SwitchSetup
            {
                name = "Pillar_2_Rotate_LeftHigh",
                localPosition = new Vector2(-7.0f, -1.0f),
                controlledCrystalIndex = 2
            },
            new SwitchSetup
            {
                name = "Pillar_3_Rotate_TopPeak",
                localPosition = new Vector2(-4.0f, -1.0f),
                controlledCrystalIndex = 3
            },
            new SwitchSetup
            {
                name = "Pillar_4_Rotate_RightHigh",
                localPosition = new Vector2(-1.5f, -1.0f),
                controlledCrystalIndex = 4
            }
        };
    }

    private void Awake()
    {
        Transform existing = transform.Find("LaserMirrorMiniGame_Runtime");
        if (existing != null)
        {
            // Upgrade prefabs/scenes that still contain the old pressure-plate controls.
            if (existing.GetComponentInChildren<LaserMirrorPressureSwitch2D>(true) != null)
            {
                Build();
                return;
            }

            // Nếu đã tồn tại cây object (người dùng đã đặt/chỉnh vị trí), KHÔNG ĐƯỢC XÓA TẠO LẠI
            LaserMirrorPuzzle2D puzzle = existing.GetComponent<LaserMirrorPuzzle2D>();
            if (puzzle != null)
            {
                puzzle.RebuildBeam();
            }
            return;
        }

        if (buildOnAwake)
        {
            Build();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            SyncPositionsFromChildren();
        }
    }

    [ContextMenu("Sync Positions From Hierarchy")]
    public void SyncPositionsFromChildren()
    {
        Transform runtime = transform.Find("LaserMirrorMiniGame_Runtime");
        if (runtime == null) return;

        // Đồng bộ vị trí pha lê nếu người dùng kéo trong Scene
        if (crystals != null)
        {
            for (int i = 0; i < crystals.Length; i++)
            {
                Transform t = runtime.Find(crystals[i].name);
                if (t != null)
                {
                    crystals[i].localPosition = t.localPosition;
                }
            }
        }

        // Đồng bộ vị trí công tắc nếu người dùng kéo trong Scene
        if (switches != null)
        {
            for (int i = 0; i < switches.Length; i++)
            {
                Transform t = runtime.Find(switches[i].name);
                if (t != null)
                {
                    switches[i].localPosition = t.localPosition;
                }
            }
        }

        Transform portal = runtime.Find("CavePortal_Center");
        if (portal != null)
        {
            portalLocalPosition = portal.localPosition;
        }
    }

    [ContextMenu("Rebuild Runtime Laser Mirror (Force Reset)")]
    public void Build()
    {
        Transform existing = transform.Find("LaserMirrorMiniGame_Runtime");
        if (existing != null)
        {
            if (Application.isPlaying)
            {
                Destroy(existing.gameObject);
            }
            else
            {
                DestroyImmediate(existing.gameObject);
            }
        }

        EnsureDefaults();

        GameObject root = new GameObject("LaserMirrorMiniGame_Runtime");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        Sprite fallbackCrystalSprite = crystalSprite != null ? crystalSprite : CreateDiamondSprite(false);
        Sprite fallbackGoalSprite = goalSprite != null ? goalSprite : CreateDiamondSprite(true);
        Sprite crystalGlowSprite = CreateCrystalGlowSprite();

        LaserMirrorPuzzle2D puzzle = root.AddComponent<LaserMirrorPuzzle2D>();
        List<LaserMirrorCrystal2D> nodes = new List<LaserMirrorCrystal2D>();

        // 1. Tạo các Crystal Nodes
        for (int i = 0; i < crystals.Length; i++)
        {
            Sprite spr = crystals[i].sprite != null
                ? crystals[i].sprite
                : crystals[i].goal ? fallbackGoalSprite : fallbackCrystalSprite;
            nodes.Add(CreateCrystalNode(root.transform, crystals[i], spr, crystalGlowSprite));
        }

        // 2. Tạo Portal Trung Tâm Vòm
        LaserMirrorCavePortal2D portal = CreateCavePortal(root.transform, portalLocalPosition, portalMaterial, targetSceneName);

        // 3. Tạo Solved Glow Effect
        GameObject revealObject = solvedObject != null
            ? solvedObject
            : CreateSolvedGlow(root.transform, nodes[nodes.Count - 1].transform.localPosition, crystalGlowSprite);
        revealObject.SetActive(false);

        puzzle.Configure(nodes[0], nodes[nodes.Count - 1], portal, revealObject, nodes);

        // 4. Tạo các trụ đá điều khiển bằng E hoặc click
        for (int i = 0; i < switches.Length; i++)
        {
            int crystalIdx = Mathf.Clamp(switches[i].controlledCrystalIndex, 0, nodes.Count - 1);
            CreateControlPillar(
                root.transform,
                switches[i],
                switches[i].offSprite != null ? switches[i].offSprite : switchOffSprite,
                switches[i].onSprite != null ? switches[i].onSprite : switchOnSprite,
                crystalGlowSprite,
                puzzle,
                nodes[crystalIdx]);
        }

        // 5. Tạo Khí quyển Hang Động: Dust Motes & Floor Mist
        if (enableAmbientDust)
        {
            CreateAmbientDustMotes(root.transform, portalLocalPosition);
        }

        if (enableFloorMist)
        {
            CreateFloorMist(root.transform, portalLocalPosition);
        }

        puzzle.RebuildBeam();
    }

    private void EnsureDefaults()
    {
        if (crystals == null || crystals.Length == 0)
        {
            SetupDefaultArchLayout();
        }
    }

    private static LaserMirrorCrystal2D CreateCrystalNode(Transform parent, CrystalSetup setup, Sprite sprite, Sprite glowSprite)
    {
        GameObject nodeObject = new GameObject(setup.name);
        nodeObject.transform.SetParent(parent, false);
        Vector3 pos = setup.positionObject != null ? parent.InverseTransformPoint(setup.positionObject.transform.position) : (Vector3)setup.localPosition;
        nodeObject.transform.localPosition = pos;

        GameObject visualObject = new GameObject("Visual");
        visualObject.transform.SetParent(nodeObject.transform, false);
        visualObject.transform.localScale = new Vector3(0.38f, 0.38f, 1f);

        SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 78;

        GameObject glow = new GameObject("Glow");
        glow.transform.SetParent(visualObject.transform, false);
        glow.transform.localScale = Vector3.one * 1.35f;
        SpriteRenderer glowRenderer = glow.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = glowSprite;
        glowRenderer.color = new Color(1f, 0.46f, 0.08f, 0.48f);
        glowRenderer.sortingOrder = 76;
        glow.SetActive(false);

        LaserMirrorCrystal2D crystal = nodeObject.AddComponent<LaserMirrorCrystal2D>();
        crystal.Configure(
            setup.source,
            setup.goal,
            setup.reflector,
            setup.sourceDirection,
            setup.facing,
            setup.customAngles,
            setup.aimTargets,
            visualObject.transform,
            renderer,
            glow);

        return crystal;
    }

    private static LaserMirrorCavePortal2D CreateCavePortal(Transform parent, Vector2 localPosition, Material mat, string targetScene)
    {
        GameObject portalObj = new GameObject("CavePortal_Center");
        portalObj.transform.SetParent(parent, false);
        portalObj.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
        visual.name = "PortalVisual";
        visual.transform.SetParent(portalObj.transform, false);
        visual.transform.localScale = new Vector3(7.8f, 4.9f, 1f);

        MeshRenderer mr = visual.GetComponent<MeshRenderer>();
        if (mat != null)
        {
            mr.material = mat;
        }
        else
        {
            Shader portalShader = Shader.Find("Bao/MiniGame/CaveArchFirePortal");
            Material portalMat;
            if (portalShader != null)
            {
                portalMat = new Material(portalShader);
            }
            else
            {
                portalMat = new Material(Shader.Find("Sprites/Default"));
            }
            mr.material = portalMat;
        }
        mr.sortingOrder = 75;

        Collider defaultCol = visual.GetComponent<Collider>();
        if (defaultCol != null) DestroyImmediate(defaultCol);

        BoxCollider2D boxCol = portalObj.AddComponent<BoxCollider2D>();
        boxCol.isTrigger = true;
        boxCol.size = new Vector2(5.0f, 3.5f);
        boxCol.offset = new Vector2(0f, -0.5f);
        boxCol.enabled = false;

        LaserMirrorCavePortal2D portalComp = portalObj.AddComponent<LaserMirrorCavePortal2D>();
        portalComp.TargetSceneName = targetScene;
        portalObj.SetActive(true);
        visual.transform.localScale = new Vector3(7.8f, 4.9f, 1f);

        return portalComp;
    }

    private static void CreateControlPillar(
        Transform parent,
        SwitchSetup setup,
        Sprite pedestalSprite,
        Sprite dialSprite,
        Sprite glowSprite,
        LaserMirrorPuzzle2D puzzle,
        LaserMirrorCrystal2D controlledCrystal)
    {
        GameObject pillarObject = new GameObject(setup.name);
        pillarObject.transform.SetParent(parent, false);
        Vector3 pos = setup.positionObject != null ? parent.InverseTransformPoint(setup.positionObject.transform.position) : (Vector3)setup.localPosition;
        pillarObject.transform.localPosition = pos;

        GameObject pedestalVisual = new GameObject("PedestalBase");
        pedestalVisual.transform.SetParent(pillarObject.transform, false);

        SpriteRenderer pedestalRenderer = pedestalVisual.AddComponent<SpriteRenderer>();
        pedestalRenderer.sprite = pedestalSprite != null ? pedestalSprite : dialSprite;
        pedestalRenderer.sortingOrder = 74;

        GameObject dialVisual = new GameObject("RotaryDial");
        dialVisual.transform.SetParent(pillarObject.transform, false);
        dialVisual.transform.localPosition = new Vector3(0f, 0.285f, 0f);
        dialVisual.transform.localScale = Vector3.one * 0.46f;

        SpriteRenderer dialRenderer = dialVisual.AddComponent<SpriteRenderer>();
        dialRenderer.sprite = controlledCrystal != null && controlledCrystal.VisualSprite != null
            ? controlledCrystal.VisualSprite
            : dialSprite;
        dialRenderer.sortingOrder = 76;

        if (controlledCrystal != null)
        {
            dialVisual.transform.localRotation = Quaternion.Euler(0f, 0f, controlledCrystal.TargetVisualAngle);
        }

        GameObject dialGlow = new GameObject("DialGlow");
        dialGlow.transform.SetParent(pillarObject.transform, false);
        dialGlow.transform.localPosition = dialVisual.transform.localPosition;
        dialGlow.transform.localScale = Vector3.one * 0.68f;

        SpriteRenderer glowRenderer = dialGlow.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = glowSprite;
        glowRenderer.color = new Color(1.05f, 0.2f, 0.62f, 0.42f);
        glowRenderer.sortingOrder = 75;
        dialGlow.SetActive(false);

        GameObject prompt = new GameObject("InteractionPrompt");
        prompt.transform.SetParent(pillarObject.transform, false);
        prompt.transform.localPosition = new Vector3(0f, 1.08f, 0f);

        TextMesh promptText = prompt.AddComponent<TextMesh>();
        promptText.text = "[E] / CLICK  XOAY";
        promptText.anchor = TextAnchor.MiddleCenter;
        promptText.alignment = TextAlignment.Center;
        promptText.fontSize = 48;
        promptText.characterSize = 0.028f;
        promptText.color = new Color(1f, 0.88f, 0.56f, 1f);
        promptText.fontStyle = FontStyle.Bold;

        MeshRenderer promptRenderer = prompt.GetComponent<MeshRenderer>();
        if (promptRenderer != null)
        {
            promptRenderer.sortingOrder = 96;
        }

        prompt.SetActive(false);

        BoxCollider2D interactionZone = pillarObject.AddComponent<BoxCollider2D>();
        interactionZone.isTrigger = true;
        interactionZone.size = new Vector2(2.2f, 2.2f);
        interactionZone.offset = new Vector2(0f, 0.12f);

        LaserMirrorStonePedestal2D pedestal = pillarObject.AddComponent<LaserMirrorStonePedestal2D>();
        pedestal.Configure(
            puzzle,
            controlledCrystal,
            dialVisual.transform,
            pedestalRenderer,
            dialRenderer,
            dialGlow,
            prompt);
    }

    private static GameObject CreateSolvedGlow(Transform parent, Vector3 localPosition, Sprite sprite)
    {
        GameObject glow = new GameObject("Solved_GoalGlow");
        glow.transform.SetParent(parent, false);
        glow.transform.localPosition = localPosition;
        glow.transform.localScale = Vector3.one * 1.1f;

        SpriteRenderer renderer = glow.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 77;
        renderer.color = new Color(1.05f, 0.2f, 0.62f, 0.6f);
        return glow;
    }

    private static Sprite CreateCrystalGlowSprite()
    {
        if (cachedCrystalGlowSprite != null)
        {
            return cachedCrystalGlowSprite;
        }

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "LaserCrystal_SoftGlow_Runtime",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 uv = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                float distance = Vector2.Distance(uv, Vector2.one * 0.5f) * 2f;
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.2f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        cachedCrystalGlowSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);
        cachedCrystalGlowSprite.name = "LaserCrystal_SoftGlow_Runtime";
        cachedCrystalGlowSprite.hideFlags = HideFlags.DontSave;
        return cachedCrystalGlowSprite;
    }

    private static Sprite CreateDiamondSprite(bool goal)
    {
        const int size = 192;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 uv = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                float diamond = Mathf.Abs(uv.x - 0.5f) * 1.25f + Mathf.Abs(uv.y - 0.5f) * 1.05f;
                if (diamond > 0.48f)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                float edge = Mathf.InverseLerp(0.48f, 0.36f, diamond);
                float shine = Mathf.Clamp01(1f - Vector2.Distance(uv, new Vector2(0.38f, 0.68f)) * 3.2f);
                Color baseColor = goal
                    ? new Color(0.95f, 0.2f, 0.58f, 0.95f)
                    : new Color(0.45f, 0.65f, 0.85f, 0.85f);
                Color color = Color.Lerp(baseColor, Color.white, Mathf.Max(edge * 0.5f, shine));
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 96f);
    }

    private static void CreateAmbientDustMotes(Transform parent, Vector2 centerPos)
    {
        GameObject dustObj = new GameObject("AmbientCaveDustMotes");
        dustObj.transform.SetParent(parent, false);
        dustObj.transform.localPosition = new Vector3(centerPos.x, centerPos.y, 0f);

        ParticleSystem ps = dustObj.AddComponent<ParticleSystem>();
        // Dừng hẳn trước khi cấu hình — Unity không cho gán duration khi đang play
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = true;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.065f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.45f, 0.75f, 0.45f), new Color(0.85f, 0.7f, 1f, 0.35f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 55;

        var emission = ps.emission;
        emission.rateOverTime = 8f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(12f, 8f, 2f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.15f;
        noise.frequency = 0.45f;
        noise.scrollSpeed = 0.2f;

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1.05f, 0.3f, 0.7f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.3f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLife.color = grad;

        var renderer = dustObj.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = 75;
        renderer.material = new Material(Shader.Find("Sprites/Default"));

        // Sau khi cấu hình xong, khởi động lại particle (đã bị Stop ở trên)
        ps.Play(true);
    }

    private static void CreateFloorMist(Transform parent, Vector2 centerPos)
    {
        GameObject mistObj = new GameObject("AmbientFloorMist");
        mistObj.transform.SetParent(parent, false);
        // Đặt sương mù ở cao độ chân vòm (y = -0.5)
        mistObj.transform.localPosition = new Vector3(centerPos.x, -0.5f, 0f);

        ParticleSystem ps = mistObj.AddComponent<ParticleSystem>();
        // Dừng hẳn trước khi cấu hình — Unity không cho gán duration khi đang play
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = true;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.35f, 0.08f, 0.28f, 0.12f), new Color(0.15f, 0.04f, 0.22f, 0.08f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 24;

        var emission = ps.emission;
        emission.rateOverTime = 3.5f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(10f, 0.6f, 0.5f);

        var velOverLife = ps.velocityOverLifetime;
        velOverLife.enabled = true;
        // Tất cả 3 trục phải cùng mode (TwoConstants) để Unity không lỗi
        velOverLife.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
        velOverLife.y = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
        velOverLife.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(0.4f, 0.1f, 0.3f), 0f), new GradientColorKey(new Color(0.2f, 0.05f, 0.25f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.14f, 0.4f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLife.color = grad;

        var renderer = mistObj.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = 74;
        renderer.material = new Material(Shader.Find("Sprites/Default"));

        // Sau khi cấu hình xong, khởi động lại particle (đã bị Stop ở trên)
        ps.Play(true);
    }
}
