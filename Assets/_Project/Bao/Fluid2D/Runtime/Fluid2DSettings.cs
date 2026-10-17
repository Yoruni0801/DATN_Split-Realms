using UnityEngine;

namespace Bao.Fluid2D
{
    [CreateAssetMenu(fileName = "Fluid2DSettings", menuName = "Bao/Fluid2D/Settings", order = 1)]
    public class Fluid2DSettings : ScriptableObject
    {
        [Header("=== Simulation Configuration ===")]
        [Tooltip("Số lượng hạt tối đa được phép tồn tại đồng thời trong mô phỏng.")]
        public int maxParticles = 4000;

        [Tooltip("Bán kính của mỗi hạt chất lỏng.")]
        public float particleRadius = 0.08f;

        [Tooltip("Bán kính tương tác/ảnh hưởng giữa các hạt (smoothing radius).")]
        public float smoothingRadius = 0.25f;

        [Tooltip("Mật độ nghỉ tiêu chuẩn của chất lỏng (rest density).")]
        public float restDensity = 10f;

        [Tooltip("Độ cứng áp suất (pressure stiffness) đẩy các hạt ra xa nhau.")]
        public float pressureStiffness = 5f;

        [Tooltip("Độ cứng áp suất tiệm cận (near pressure stiffness) để duy trì tính kết dính.")]
        public float nearPressureStiffness = 8f;

        [Tooltip("Hệ số nhớt (viscosity) làm mượt dòng chảy và kéo hạt lân cận cùng vận tốc.")]
        public float viscosity = 0.18f;

        [Tooltip("Lực kết dính / sức căng bề mặt: kéo hạt gần nhau để nước gom khối, chống atomization.")]
        [Range(0f, 3f)]
        public float surfaceTension = 0.75f;

        [Tooltip("Bán kính kết dính (world). 0 = dùng smoothingRadius. Hơi lớn hơn smoothing giúp giọt gần nhau tái hợp.")]
        [Min(0f)]
        public float cohesionRadius = 0f;

        [Tooltip("Hệ số tái hợp: kéo mạnh hơn khi mật độ lân cận thấp (hạt cô lập / rìa) về phía đám đông gần nhất.")]
        [Range(0f, 2f)]
        public float reunificationStrength = 0.55f;

        [Tooltip("Giảm động năng theo thời gian (implicit). Giảm splash kéo dài mà không làm nước đông cứng.")]
        [Range(0f, 3f)]
        public float velocityDamping = 0.16f;

        [Tooltip("Hệ số trọng lực tác dụng lên các hạt.")]
        public Vector2 gravity = new Vector2(0f, -9.81f);

        [Tooltip("Vận tốc tối đa của một hạt để tránh đứt gãy mô phỏng do xung lực quá lớn.")]
        public float maxVelocity = 14f;

        [Tooltip("Giới hạn không gian mô phỏng (AABB): x=minX, y=minY, z=maxX, w=maxY.")]
        public Vector4 worldBounds = new Vector4(-15f, -8f, 15f, 8f);

        [Tooltip("Số bước lặp vật lý phụ (substeps) trên mỗi frame để tăng độ ổn định va chạm.")]
        [Range(1, 4)]
        public int substeps = 2;

        [Header("=== Collision Settings ===")]
        [Tooltip("Hệ số ma sát khi hạt va chạm với bề mặt vật thể.")]
        [Range(0f, 1f)]
        public float collisionFriction = 0.22f;

        [Tooltip("Hệ số nảy khi va chạm tường/biên (thấp = bám địa hình, ít nảy).")]
        [Range(0f, 1f)]
        public float collisionRestitution = 0.05f;

        [Tooltip("Hệ số nảy khi va chạm chướng ngại vật/tĩnh (0 = hấp thụ, 1 = nảy mạnh).")]
        [Range(0f, 1f)]
        public float obstacleRestitution = 0.04f;

        [Tooltip("Hấp thụ vận tốc tiếp cận khi va chạm (0..1). Cao hơn = ít particle bắn ra sau impact.")]
        [Range(0f, 1f)]
        public float collisionNormalAbsorption = 0.55f;

        [Header("=== Rendering — Map_IceandSnow glacial palette V2 ===")]
        [Tooltip("Luminous glacial aqua at free surface (readable at Game View scale).")]
        public Color surfaceColor = new Color(0.38f, 0.94f, 0.97f, 0.50f);

        [Tooltip("Soft deep-teal lower/body layer (cool depth contrast, not gray).")]
        public Color deepColor = new Color(0.03f, 0.34f, 0.46f, 0.92f);

        [Tooltip("Soft icy foam only on splash crests (restrained).")]
        public Color foamColor = new Color(0.97f, 0.995f, 1.0f, 0.38f);

        [Tooltip("Bán kính nhòe (Blur) để làm mịn metaball.")]
        [Range(1, 10)]
        public int blurRadius = 3;

        [Tooltip("Ngưỡng mật độ (Density Threshold) để cắt khối metaball. Thấp hơn = mép liên tục hơn khi hạt hơi loãng.")]
        [Range(0.01f, 2f)]
        public float densityThreshold = 0.36f;

        [Tooltip("Độ dày dải highlight mép (silhouette). Keep thin — full white outline is wrong.")]
        public float outlineWidth = 0.022f;

        [Tooltip("Màu highlight mép / top ribbon (pale icy, not capsule white).")]
        public Color outlineColor = new Color(0.75f, 0.94f, 0.97f, 0.35f);

        [Tooltip("Độ mềm của mép metaball. Giá trị cao làm mép nước ít răng cưa hơn.")]
        [Range(0.002f, 0.08f)]
        public float edgeSoftness = 0.028f;

        [Tooltip("Cường độ surface→deep depth body (higher = clearer cool depth).")]
        [Range(0f, 1f)]
        public float depthDarkening = 0.88f;

        [Tooltip("Cường độ foam / edge energy (giữ thấp để tránh glow).")]
        [Range(0f, 2f)]
        public float highlightStrength = 0.50f;

        [Tooltip("Biến dạng UV gợn rất nhẹ (cao sẽ shimmer — giữ thấp).")]
        [Range(0f, 1f)]
        public float rippleStrength = 0.02f;

        [Header("=== Rendering — Painterly depth / motion V2 ===")]
        [Tooltip("Blend vertical/bottom cue into depth (lighter toward free surface).")]
        [Range(0f, 1f)]
        public float verticalGradient = 0.90f;

        [Tooltip("Free-surface pale ribbon strength (broken/organic, top only).")]
        [Range(0f, 2f)]
        public float surfaceBandStrength = 1.65f;

        [Tooltip("Thickness of free-surface band in density space.")]
        [Range(0.02f, 0.35f)]
        public float surfaceBandWidth = 0.20f;

        [Tooltip("Broad internal tonal/caustic band strength (readable at gameplay scale).")]
        [Range(0f, 1f)]
        public float noiseStrength = 0.92f;

        [Tooltip("Primary broad band frequency (low = large painterly shapes).")]
        [Range(1f, 40f)]
        public float noiseScale = 3.4f;

        [Tooltip("Secondary broad band frequency.")]
        [Range(1f, 60f)]
        public float noiseScaleB = 4.2f;

        [Tooltip("Band drift speed (keep near zero to avoid swimming/shimmer).")]
        [Range(0f, 1f)]
        public float noiseSpeed = 0.012f;

        [Tooltip("Soft silhouette Fresnel (low — bottom rim suppressed in shader).")]
        [Range(0f, 2f)]
        public float fresnelStrength = 0.18f;

        [Tooltip("Fresnel falloff sharpness.")]
        [Range(0.5f, 6f)]
        public float fresnelPower = 2.6f;

        [Tooltip("Foam response to high curvature / splash edges only.")]
        [Range(0f, 4f)]
        public float foamCurvature = 1.7f;

        [Tooltip("Sparse sparkle on energetic top edges only.")]
        [Range(0f, 1f)]
        public float sparkleStrength = 0.08f;

        [Tooltip("Global alpha scale for water body compositing over snow/caves.")]
        [Range(0.2f, 1.5f)]
        public float bodyAlpha = 1.0f;

        [Tooltip("Contrast of broad internal bands (keep moderate — high values look like glass slices).")]
        [Range(0f, 2f)]
        public float bandContrast = 1.32f;

        [Tooltip("Scales bottom/side rim brightness relative to top free-surface ribbon.")]
        [Range(0f, 1f)]
        public float bottomRimScale = 0.42f;
    }
}
