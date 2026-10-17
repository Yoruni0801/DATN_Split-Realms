// ============================================================================
// SHADER: SpriteColorShift
// Mô tả: Shader cho Sprite 2D cho phép tùy chỉnh màu sắc linh hoạt:
//   - Dịch chuyển Hue (xoay vòng màu HSV)
//   - Điều chỉnh Saturation (độ bão hòa)
//   - Điều chỉnh Brightness (độ sáng)
//   - Overlay Color: pha thêm 1 màu mục tiêu lên sprite
//   - Tint Color: nhân màu tổng thể
//   - Hỗ trợ Transparency / Alpha
// Dùng để đổi màu quái vật (ví dụ: tím → trắng/xanh băng) cho phù hợp map.
// ============================================================================
Shader "Custom/Sprite/SpriteColorShift"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // === HUE SHIFT ===
        [Header(Hue Shift)]
        _HueShift ("Hue Shift", Range(0, 1)) = 0

        // === SATURATION & BRIGHTNESS ===
        [Header(Saturation and Brightness)]
        _Saturation ("Saturation", Range(0, 2)) = 1
        _Brightness ("Brightness", Range(0, 3)) = 1

        // === OVERLAY COLOR ===
        [Header(Overlay Color)]
        _OverlayColor ("Overlay Color", Color) = (1,1,1,0)
        _OverlayBlend ("Overlay Blend", Range(0, 1)) = 0

        // === CONTRAST ===
        [Header(Contrast)]
        _Contrast ("Contrast", Range(0.5, 2)) = 1

        // === TRANSPARENCY ===
        [Header(Alpha)]
        _AlphaMultiplier ("Alpha Multiplier", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _HueShift;
            float _Saturation;
            float _Brightness;
            fixed4 _OverlayColor;
            float _OverlayBlend;
            float _Contrast;
            float _AlphaMultiplier;

            // ----------------------------------------------------------------
            // RGB ↔ HSV conversion
            // ----------------------------------------------------------------
            float3 RGBtoHSV(float3 rgb)
            {
                float cmax  = max(rgb.r, max(rgb.g, rgb.b));
                float cmin  = min(rgb.r, min(rgb.g, rgb.b));
                float delta = cmax - cmin;

                float h = 0;
                if (delta > 0.0001)
                {
                    if (cmax == rgb.r)
                        h = fmod((rgb.g - rgb.b) / delta, 6.0);
                    else if (cmax == rgb.g)
                        h = (rgb.b - rgb.r) / delta + 2.0;
                    else
                        h = (rgb.r - rgb.g) / delta + 4.0;
                    h /= 6.0;
                    if (h < 0) h += 1.0;
                }

                float s = (cmax > 0.0001) ? (delta / cmax) : 0;
                float v = cmax;

                return float3(h, s, v);
            }

            float3 HSVtoRGB(float3 hsv)
            {
                float h = hsv.x * 6.0;
                float s = hsv.y;
                float v = hsv.z;

                float c = v * s;
                float x = c * (1.0 - abs(fmod(h, 2.0) - 1.0));
                float m = v - c;

                float3 rgb;
                if      (h < 1) rgb = float3(c, x, 0);
                else if (h < 2) rgb = float3(x, c, 0);
                else if (h < 3) rgb = float3(0, c, x);
                else if (h < 4) rgb = float3(0, x, c);
                else if (h < 5) rgb = float3(x, 0, c);
                else            rgb = float3(c, 0, x);

                return rgb + m;
            }

            // ----------------------------------------------------------------
            // Vertex
            // ----------------------------------------------------------------
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex   = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color    = v.color * _Color;

                #ifdef PIXELSNAP_ON
                o.vertex = UnityPixelSnap(o.vertex);
                #endif

                return o;
            }

            // ----------------------------------------------------------------
            // Fragment
            // ----------------------------------------------------------------
            fixed4 frag(v2f i) : SV_Target
            {
                // Sample sprite texture
                fixed4 tex = tex2D(_MainTex, i.texcoord);
                float  originalAlpha = tex.a;

                // --- 1. HSV transform ---
                float3 hsv = RGBtoHSV(tex.rgb);

                // Hue shift (xoay vòng)
                hsv.x = frac(hsv.x + _HueShift);

                // Saturation
                hsv.y = saturate(hsv.y * _Saturation);

                // Brightness
                hsv.z = saturate(hsv.z * _Brightness);

                float3 rgb = HSVtoRGB(hsv);

                // --- 2. Contrast ---
                rgb = saturate(((rgb - 0.5) * _Contrast) + 0.5);

                // --- 3. Overlay color blend ---
                rgb = lerp(rgb, _OverlayColor.rgb, _OverlayBlend);

                // --- 4. Vertex color & tint ---
                fixed4 result;
                result.rgb = rgb * i.color.rgb;
                result.a   = originalAlpha * i.color.a * _AlphaMultiplier;

                // Pre-multiply alpha (standard Unity sprite blending)
                result.rgb *= result.a;

                return result;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
