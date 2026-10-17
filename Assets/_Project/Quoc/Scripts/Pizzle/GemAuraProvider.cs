using UnityEngine;

namespace Quoc.PuzzleQuest
{
    /// <summary>
    /// Tạo và quản lý các Material aura cho từng loại ngọc (Sword, Water, Fire, Wind).
    /// Gắn script này cùng GameObject với PuzzleQuestController.
    /// Kéo Sprite của từng nguyên tố vào để aura hiển thị đúng hình dạng viên ngọc.
    /// Nếu bạn đã tự tạo Material thủ công trong Editor thì hãy gán vào các ô Material tương ứng.
    /// Nếu để trống Material, script sẽ tự tạo với màu phù hợp từng nguyên tố.
    /// </summary>
    [DisallowMultipleComponent]
    public class GemAuraProvider : MonoBehaviour
    {
        [Header("Sprite ngọc (kéo vào để aura đúng hình dạng)")]
        [SerializeField] private Sprite swordSprite;
        [SerializeField] private Sprite waterSprite;
        [SerializeField] private Sprite fireSprite;
        [SerializeField] private Sprite windSprite;

        [Header("Aura Materials (Tự động tạo nếu để trống)")]
        [SerializeField] private Material swordAuraMaterial;
        [SerializeField] private Material waterAuraMaterial;
        [SerializeField] private Material fireAuraMaterial;
        [SerializeField] private Material windAuraMaterial;

        [Header("Cài đặt chung")]
        [SerializeField] private bool enableAura = true;
        [SerializeField] [Range(0.5f, 3f)] private float glowIntensity = 1.5f;
        [SerializeField] [Range(0f, 5f)] private float pulseSpeed = 2.0f;
        [Tooltip("Mở rộng aura thêm bao nhiêu pixel so với ô ngọc (X = chiều rộng, Y = chiều cao)")]
        [SerializeField] private Vector2 auraSizeOffset = new Vector2(8f, 8f);

        private Shader auraShader;
        private bool isInitialized;

        /// <summary>
        /// Lấy Material aura tương ứng với loại ngọc.
        /// Trả về null nếu aura đang bị tắt.
        /// </summary>
        public Material GetAuraMaterial(PuzzleGemType type)
        {
            if (!enableAura) return null;

            EnsureInitialized();

            switch (type)
            {
                case PuzzleGemType.Sword: return swordAuraMaterial;
                case PuzzleGemType.Water: return waterAuraMaterial;
                case PuzzleGemType.Fire:  return fireAuraMaterial;
                case PuzzleGemType.Wind:  return windAuraMaterial;
                default:                  return swordAuraMaterial;
            }
        }

        /// <summary>
        /// Lấy Sprite aura tương ứng với loại ngọc.
        /// Nếu chưa gán Sprite riêng thì trả về null (GemView sẽ dùng sprite gốc).
        /// </summary>
        public Sprite GetAuraSprite(PuzzleGemType type)
        {
            switch (type)
            {
                case PuzzleGemType.Sword: return swordSprite;
                case PuzzleGemType.Water: return waterSprite;
                case PuzzleGemType.Fire:  return fireSprite;
                case PuzzleGemType.Wind:  return windSprite;
                default:                  return swordSprite;
            }
        }

        /// <summary>
        /// Lấy offset mở rộng kích thước aura (pixel) theo chiều rộng (X) và chiều cao (Y).
        /// </summary>
        public Vector2 AuraSizeOffset { get { return auraSizeOffset; } }

        /// <summary>
        /// Aura có đang bật không.
        /// </summary>
        public bool IsEnabled { get { return enableAura; } }

        private void EnsureInitialized()
        {
            if (isInitialized) return;
            isInitialized = true;

            auraShader = Shader.Find("Custom/GemAura");
            if (auraShader == null)
            {
                Debug.LogWarning("[GemAuraProvider] Shader 'Custom/GemAura' không tìm thấy. Hãy đảm bảo file GemAura.shader đã được import.");
                return;
            }

            // Sword: Vàng kim hoàng gia — xoay chậm, shimmer kim loại
            if (swordAuraMaterial == null)
            {
                swordAuraMaterial = CreateAuraMaterial(
                    "GemAura_Sword", swordSprite,
                    new Color(1.0f, 0.85f, 0.3f, 1f),    // Vàng sáng
                    new Color(0.85f, 0.55f, 0.1f, 1f),    // Vàng cam
                    0.08f, 0.12f, glowIntensity * 1.2f,   // glow width, softness, brightness
                    pulseSpeed * 0.8f, 0.12f,              // pulse speed, amount
                    0.8f, 5.0f, 0.12f,                     // rotation, noiseScale, noiseAmount
                    0.7f, 3.0f,                             // shimmer, shimmerSpeed
                    0.15f, 0.6f                             // overlay, flow
                );
            }

            // Water: Xanh dương sâu — dòng nước chảy, shimmer phản chiếu
            if (waterAuraMaterial == null)
            {
                waterAuraMaterial = CreateAuraMaterial(
                    "GemAura_Water", waterSprite,
                    new Color(0.2f, 0.6f, 1.0f, 1f),      // Xanh sáng
                    new Color(0.0f, 0.3f, 0.85f, 1f),      // Xanh đậm
                    0.09f, 0.14f, glowIntensity,            // glow
                    pulseSpeed * 1.2f, 0.18f,               // pulse
                    1.2f, 6.0f, 0.18f,                      // rotation, noise
                    1.0f, 4.0f,                              // shimmer
                    0.2f, 1.2f                               // overlay, flow
                );
            }

            // Fire: Đỏ cam rực — xoay nhanh, pulse mạnh, lửa bùng
            if (fireAuraMaterial == null)
            {
                fireAuraMaterial = CreateAuraMaterial(
                    "GemAura_Fire", fireSprite,
                    new Color(1.0f, 0.4f, 0.05f, 1f),      // Cam đỏ
                    new Color(1.0f, 0.15f, 0.0f, 1f),       // Đỏ đậm
                    0.10f, 0.15f, glowIntensity * 1.4f,     // glow rộng hơn, sáng hơn
                    pulseSpeed * 1.5f, 0.25f,                // pulse mạnh
                    1.8f, 7.0f, 0.25f,                       // rotation nhanh, noise mạnh
                    0.5f, 2.5f,                               // shimmer thấp (lửa ít lấp lánh)
                    0.25f, 1.8f                               // overlay mạnh, flow nhanh
                );
            }

            // Wind: Xanh lá phát sáng — xoay nhanh nhất, shimmer cao
            if (windAuraMaterial == null)
            {
                windAuraMaterial = CreateAuraMaterial(
                    "GemAura_Wind", windSprite,
                    new Color(0.3f, 1.0f, 0.45f, 1f),      // Xanh lá sáng
                    new Color(0.1f, 0.8f, 0.3f, 1f),        // Xanh lá đậm
                    0.08f, 0.13f, glowIntensity * 1.1f,     // glow
                    pulseSpeed, 0.15f,                        // pulse
                    2.2f, 5.0f, 0.2f,                        // rotation nhanh nhất
                    1.2f, 5.0f,                               // shimmer cao nhất
                    0.18f, 2.0f                               // overlay, flow nhanh
                );
            }
        }

        private Material CreateAuraMaterial(
            string matName, Sprite sprite,
            Color color1, Color color2,
            float glowWidth, float glowSoftness, float glowBrightness,
            float pulseSpd, float pulseAmt,
            float rotSpeed, float noiseScl, float noiseAmt,
            float shimmer, float shimmerSpd,
            float overlay, float flowSpd)
        {
            Material mat = new Material(auraShader);
            mat.name = matName;

            if (sprite != null && sprite.texture != null)
                mat.SetTexture("_MainTex", sprite.texture);

            mat.SetColor("_AuraColor", color1);
            mat.SetColor("_AuraColor2", color2);
            mat.SetFloat("_GlowWidth", glowWidth);
            mat.SetFloat("_GlowSoftness", glowSoftness);
            mat.SetFloat("_GlowIntensity", glowBrightness);
            mat.SetFloat("_PulseSpeed", pulseSpd);
            mat.SetFloat("_PulseAmount", pulseAmt);
            mat.SetFloat("_RotationSpeed", rotSpeed);
            mat.SetFloat("_NoiseScale", noiseScl);
            mat.SetFloat("_NoiseAmount", noiseAmt);
            mat.SetFloat("_Shimmer", shimmer);
            mat.SetFloat("_ShimmerSpeed", shimmerSpd);
            mat.SetFloat("_OverlayBlend", overlay);
            mat.SetFloat("_FlowSpeed", flowSpd);
            return mat;
        }
    }
}
