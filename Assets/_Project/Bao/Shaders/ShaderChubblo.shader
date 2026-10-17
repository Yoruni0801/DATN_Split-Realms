// ============================================================================
// SHADER: SpriteColorShiftPro (Chubbloo Living Ice - Amethyst & Amber Edition)
// Unity 6 / SpriteRenderer
//
// Mục tiêu:
// - Đột phá tương phản: Thân thạch anh tím (Frozen Amethyst) + Mắt vàng hổ phách (Amber Eye).
// - Viền tím đêm (Midnight Outline) dày và sắc nét giúp tách hẳn khỏi nền tuyết trắng/cyan.
// - Nổi bật ánh mắt và điểm năng lượng ấm áp (Amber/Gold) tạo Focal Point rõ ràng.
// - Giữ nguyên chi tiết texture gốc, không bệt màu, hỗ trợ HDR Bloom nhẹ.
// ============================================================================

Shader "Custom/Sprite/SpriteColorShiftPro"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // --------------------------------------------------------------------
        // BASE COLOR
        // --------------------------------------------------------------------
        [Header(Base Color Adjustments)]
        _HueShift ("Hue Shift", Range(-0.5, 0.5)) = 0.0
        _Saturation ("Saturation", Range(0, 2)) = 1.15
        _Brightness ("Brightness", Range(0.5, 2)) = 1.02
        _Contrast ("Contrast", Range(0.5, 2)) = 1.18

        // --------------------------------------------------------------------
        // FROZEN AMETHYST COLOR SEPARATION
        // --------------------------------------------------------------------
        [Header(Amethyst Monster Palette)]
        _ShadowTint ("Shadow Tint (Amethyst Violet)", Color) = (0.28, 0.10, 0.65, 1)
        _HighlightTint ("Spike Highlight Tint (Glacial Magenta)", Color) = (0.82, 0.38, 1.0, 1)
        _ToneStrength ("Tone Remap Strength", Range(0, 1)) = 0.68
        _ShadowEnd ("Shadow End Range", Range(0.05, 0.85)) = 0.58
        _HighlightStart ("Highlight Start Range", Range(0.15, 0.95)) = 0.52

        // --------------------------------------------------------------------
        // AMBER EYE & CORE ENERGY
        // --------------------------------------------------------------------
        [Header(Amber Eye and Core Highlight)]
        [HDR] _EyeTint ("Amber Eye Tint", Color) = (1.25, 0.72, 0.08, 1)
        _EyeThreshold ("Eye Luma Threshold", Range(0.5, 0.98)) = 0.72
        _EyeStrength ("Eye Tint Strength", Range(0, 1)) = 0.90

        // --------------------------------------------------------------------
        // OUTLINE (SEPARATION FROM SNOW MAP)
        // --------------------------------------------------------------------
        [Header(Outline Separation)]
        _OutlineColor ("Outline Color (Midnight Amethyst)", Color) = (0.06, 0.02, 0.16, 1)
        _OutlineSize ("Outline Size (px)", Range(0, 5)) = 2.2
        _OutlineOpacity ("Outline Opacity", Range(0, 1)) = 0.98

        // --------------------------------------------------------------------
        // SOFT OUTER GLOW
        // --------------------------------------------------------------------
        [Header(Outer Glow)]
        [HDR] _GlowColor ("Glow Color (Soft Violet)", Color) = (0.58, 0.22, 0.95, 1)
        _GlowSize ("Glow Size (px)", Range(0, 8)) = 3.2
        _GlowOpacity ("Glow Opacity", Range(0, 1)) = 0.22

        // --------------------------------------------------------------------
        // INNER RIM
        // --------------------------------------------------------------------
        [Header(Inner Rim Crystalline)]
        [HDR] _InnerRimColor ("Inner Rim Color", Color) = (0.65, 0.40, 1.25, 1)
        _InnerRimStrength ("Inner Rim Strength", Range(0, 1)) = 0.32

        // --------------------------------------------------------------------
        // ICE SPARKLE / ENERGY PULSE
        // --------------------------------------------------------------------
        [Header(Sparkle and Pulse)]
        [HDR] _SparkleColor ("Sparkle Color (Amber Sparkle)", Color) = (1.15, 0.85, 0.25, 1)
        _SparkleThreshold ("Sparkle Threshold", Range(0, 1)) = 0.78
        _SparkleStrength ("Sparkle Strength", Range(0, 1)) = 0.20
        _PulseStrength ("Subtle Pulse", Range(0, 0.5)) = 0.08
        _PulseSpeed ("Pulse Speed", Range(0, 8)) = 2.5

        // --------------------------------------------------------------------
        // OPTIONAL OVERLAY & ALPHA
        // --------------------------------------------------------------------
        [Header(Optional Overlay and Alpha)]
        _OverlayColor ("Overlay Color", Color) = (1,1,1,0)
        _OverlayBlend ("Overlay Blend", Range(0, 1)) = 0
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

        // Premultiplied alpha mode
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
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            float4 _Color;

            float _HueShift;
            float _Saturation;
            float _Brightness;
            float _Contrast;

            float4 _ShadowTint;
            float4 _HighlightTint;
            float _ToneStrength;
            float _ShadowEnd;
            float _HighlightStart;

            float4 _EyeTint;
            float _EyeThreshold;
            float _EyeStrength;

            float4 _OutlineColor;
            float _OutlineSize;
            float _OutlineOpacity;

            float4 _GlowColor;
            float _GlowSize;
            float _GlowOpacity;

            float4 _InnerRimColor;
            float _InnerRimStrength;

            float4 _SparkleColor;
            float _SparkleThreshold;
            float _SparkleStrength;
            float _PulseStrength;
            float _PulseSpeed;

            float4 _OverlayColor;
            float _OverlayBlend;

            float _AlphaMultiplier;

            // ----------------------------------------------------------------
            // Helpers
            // ----------------------------------------------------------------
            float Luma(float3 c)
            {
                return dot(c, float3(0.299, 0.587, 0.114));
            }

            float3 ScreenBlend(float3 baseColor, float3 blendColor)
            {
                baseColor  = saturate(baseColor);
                blendColor = saturate(blendColor);
                return 1.0 - (1.0 - baseColor) * (1.0 - blendColor);
            }

            float3 RGBtoHSV(float3 rgb)
            {
                float cmax  = max(rgb.r, max(rgb.g, rgb.b));
                float cmin  = min(rgb.r, min(rgb.g, rgb.b));
                float delta = cmax - cmin;

                float h = 0.0;

                if (delta > 0.0001)
                {
                    if (cmax == rgb.r)
                        h = fmod((rgb.g - rgb.b) / delta, 6.0);
                    else if (cmax == rgb.g)
                        h = ((rgb.b - rgb.r) / delta) + 2.0;
                    else
                        h = ((rgb.r - rgb.g) / delta) + 4.0;

                    h /= 6.0;
                    if (h < 0.0) h += 1.0;
                }

                float s = (cmax > 0.0001) ? (delta / cmax) : 0.0;
                return float3(h, s, cmax);
            }

            float3 HSVtoRGB(float3 hsv)
            {
                float h = frac(hsv.x) * 6.0;
                float s = saturate(hsv.y);
                float v = hsv.z;

                float c = v * s;
                float x = c * (1.0 - abs(fmod(h, 2.0) - 1.0));
                float m = v - c;

                float3 rgb;

                if      (h < 1.0) rgb = float3(c, x, 0);
                else if (h < 2.0) rgb = float3(x, c, 0);
                else if (h < 3.0) rgb = float3(0, c, x);
                else if (h < 4.0) rgb = float3(0, x, c);
                else if (h < 5.0) rgb = float3(x, 0, c);
                else              rgb = float3(c, 0, x);

                return rgb + m;
            }

            void SampleAlphaRing(float2 uv, float2 px, out float maxA, out float minA)
            {
                float a0 = tex2D(_MainTex, uv + float2( px.x,  0.0)).a;
                float a1 = tex2D(_MainTex, uv + float2(-px.x,  0.0)).a;
                float a2 = tex2D(_MainTex, uv + float2( 0.0,  px.y)).a;
                float a3 = tex2D(_MainTex, uv + float2( 0.0, -px.y)).a;

                float a4 = tex2D(_MainTex, uv + float2( px.x,  px.y)).a;
                float a5 = tex2D(_MainTex, uv + float2(-px.x,  px.y)).a;
                float a6 = tex2D(_MainTex, uv + float2( px.x, -px.y)).a;
                float a7 = tex2D(_MainTex, uv + float2(-px.x, -px.y)).a;

                maxA = max(max(max(a0, a1), max(a2, a3)),
                           max(max(a4, a5), max(a6, a7)));

                minA = min(min(min(a0, a1), min(a2, a3)),
                           min(min(a4, a5), min(a6, a7)));
            }

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

            float4 frag(v2f i) : SV_Target
            {
                float4 tex = tex2D(_MainTex, i.texcoord);

                float centerA = tex.a;
                float bodyA   = centerA * i.color.a * _AlphaMultiplier;

                if (bodyA <= 0.001)
                    discard;

                // ============================================================
                // 1) BASE COLOR GRADING & CORRECTION
                // ============================================================
                float3 rgb = tex.rgb;

                // Hue shift
                if (abs(_HueShift) > 0.0001)
                {
                    float3 hsv = RGBtoHSV(rgb);
                    hsv.x = frac(hsv.x + _HueShift);
                    rgb = HSVtoRGB(hsv);
                }

                // Saturation
                float lum = Luma(rgb);
                rgb = lerp(lum.xxx, rgb, _Saturation);

                // Contrast + brightness
                rgb = ((rgb - 0.5) * _Contrast) + 0.5;
                rgb *= _Brightness;
                rgb = max(rgb, 0.0);

                // ============================================================
                // 2) AMETHYST DUAL-TONE SEPARATION
                // ============================================================
                float gradeLum = saturate(Luma(rgb));

                float shadowMask = 1.0 - smoothstep(max(0.0, _ShadowEnd - 0.25), _ShadowEnd, gradeLum);
                float highlightMask = smoothstep(_HighlightStart, min(1.0, _HighlightStart + 0.28), gradeLum);

                float3 shadowColored = ScreenBlend(rgb, _ShadowTint.rgb);
                float3 highlightColored = ScreenBlend(rgb, _HighlightTint.rgb);

                rgb = lerp(rgb, shadowColored, shadowMask * _ToneStrength * _ShadowTint.a);
                rgb = lerp(rgb, highlightColored, highlightMask * _ToneStrength * 0.85 * _HighlightTint.a);

                // ============================================================
                // 3) AMBER EYE / ENERGY CORE FOCUS
                // Đổi phần mắt / điểm năng lượng sáng nhất thành vàng hổ phách
                // ============================================================
                float rawLum = Luma(tex.rgb);
                float eyeMask = smoothstep(_EyeThreshold, min(1.0, _EyeThreshold + 0.16), rawLum);
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseStrength;
                float3 amberColor = _EyeTint.rgb * (0.8 + gradeLum * 0.5) * pulse;

                rgb = lerp(rgb, amberColor, eyeMask * _EyeStrength * _EyeTint.a);

                // Optional overlay
                rgb = lerp(rgb, _OverlayColor.rgb, _OverlayBlend * _OverlayColor.a);

                // Vertex Tint
                rgb *= i.color.rgb;

                // ============================================================
                // 4) OUTLINE & INNER CRYSTAL RIM
                // ============================================================
                float maxOutlineA;
                float minOutlineA;

                float2 outlinePx = _MainTex_TexelSize.xy * max(_OutlineSize, 0.0001);
                SampleAlphaRing(i.texcoord, outlinePx, maxOutlineA, minOutlineA);

                float outlineMask = saturate(maxOutlineA - centerA) * _OutlineOpacity * _OutlineColor.a;

                float edgeVariation = saturate(maxOutlineA - minOutlineA);
                float innerRimMask = edgeVariation * saturate(centerA * 3.0) * _InnerRimStrength;

                rgb += _InnerRimColor.rgb * innerRimMask * _InnerRimColor.a;

                // ============================================================
                // 5) SPARKLE HIGHLIGHT
                // ============================================================
                float brightArea = smoothstep(_SparkleThreshold, min(1.0, _SparkleThreshold + 0.22), saturate(Luma(rgb)));
                rgb += _SparkleColor.rgb * brightArea * _SparkleStrength * pulse * _SparkleColor.a;

                // ============================================================
                // 6) SOFT OUTER GLOW
                // ============================================================
                float maxGlowA;
                float minGlowA_unused;

                float2 glowPx = _MainTex_TexelSize.xy * max(_GlowSize, 0.0001);
                SampleAlphaRing(i.texcoord, glowPx, maxGlowA, minGlowA_unused);

                float glowMask = saturate(maxGlowA - centerA) * _GlowOpacity * _GlowColor.a;
                glowMask *= (1.0 - saturate(outlineMask));

                // ============================================================
                // 7) COMPOSITING PREMULTIPLIED ALPHA
                // ============================================================
                float outlineA = outlineMask * (1.0 - bodyA);
                float glowA    = glowMask * (1.0 - bodyA) * (1.0 - outlineA);

                float outA = saturate(bodyA + outlineA + glowA);

                float3 bodyPremul    = rgb * bodyA;
                float3 outlinePremul = _OutlineColor.rgb * outlineA;
                float3 glowPremul    = _GlowColor.rgb * glowA;

                float3 outRGB = bodyPremul + outlinePremul + glowPremul;

                return float4(outRGB, outA);
            }

            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
