// ============================================================================
// SHADER: Bao/IcefinGlacialShader
// Tương thích 100% Unity 2D Animation SpriteSkin (2D Bones) & Sprites Default
// ============================================================================

Shader "Bao/IcefinGlacialShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // ── Crystal Base Palette (Sáng trong, tương phản cao, giữ nét gốc) ───
        [Header(Base Crystal Color)]
        _DeepIceColor   ("Deep Shadow Blue",  Color) = (0.12, 0.28, 0.58, 1)
        _GlacierColor   ("Glacial Crystal",   Color) = (0.35, 0.82, 1.00, 1)
        _FrostColor     ("Frost Highlight",   Color) = (0.82, 0.96, 1.00, 1)
        _SnowColor      ("Pure Sparkle",      Color) = (1.00, 1.00, 1.00, 1)
        _TextureMix     ("Texture Preserve",  Range(0.4, 1.0)) = 0.82
        _Brightness     ("Brightness Boost",  Range(0.8, 1.8)) = 1.15
        _Contrast       ("Contrast",          Range(0.8, 1.8)) = 1.20

        // ── Neon Glowing Veins & Bioluminescent Core ─────────────────────────
        [Header(Luminescent Veins and Core)]
        [HDR] _GlowColor    ("Neon Core Glow", Color) = (0.20, 1.25, 1.45, 1)
        [HDR] _AccentColor  ("Accent Amber/Cyan", Color) = (1.20, 0.75, 0.15, 1)
        _GlowIntensity      ("Core Pulse Str", Range(0, 2)) = 0.85
        _PulseSpeed         ("Pulse Speed", Float) = 2.4
        _IceVeinFreq        ("Vein Density", Float) = 14.0
        _IceVeinStr         ("Vein Intensity", Range(0, 3)) = 1.10
        _VeinSpeed          ("Vein Flow Speed", Float) = 0.55

        // ── Silhouette Contour (Tách khối khỏi nền tuyết) ───────────────────
        [Header(Silhouette and Rim)]
        _OutlineColor   ("Edge Border (Navy)", Color) = (0.06, 0.14, 0.32, 1)
        _OutlineWidth   ("Outline Width (px)", Range(0, 4)) = 1.8
        _OutlineOpacity ("Outline Opacity", Range(0, 1)) = 0.95
        [HDR] _RimColor ("Inner Crystal Rim", Color) = (0.65, 0.95, 1.35, 1)
        _RimPower       ("Rim Intensity", Range(0, 3)) = 1.20

        // ── State Dynamics (Anticipation & Dash) ──────────────────────────────
        [Header(Attack Dynamics)]
        _Charge         ("Charge Level", Range(0, 1)) = 0
        _Dash           ("Dash Level", Range(0, 1)) = 0
        _ImpactFlash    ("Impact Flash", Range(0, 1)) = 0
        _DashDirection  ("Dash Dir", Vector) = (1, 0, 0, 0)
        _ChargeFlicker  ("Charge Flicker Freq", Float) = 24.0
        _ChargeGlowStr  ("Charge Glow Str", Range(0, 3)) = 1.40
        _DashGlowStr    ("Dash Spear Glow", Range(0, 3)) = 1.60
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
        Blend One OneMinusSrcAlpha // Premultiplied Alpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"

            struct appdata_t
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
            float4 _DeepIceColor;
            float4 _GlacierColor;
            float4 _FrostColor;
            float4 _SnowColor;
            float4 _GlowColor;
            float4 _AccentColor;
            float4 _RimColor;
            float4 _OutlineColor;

            float _TextureMix;
            float _Brightness;
            float _Contrast;
            float _OutlineWidth;
            float _OutlineOpacity;
            float _RimPower;
            float _GlowIntensity;
            float _PulseSpeed;
            float _IceVeinFreq;
            float _IceVeinStr;
            float _VeinSpeed;
            float _Charge;
            float _Dash;
            float _ImpactFlash;
            float4 _DashDirection;
            float _ChargeFlicker;
            float _ChargeGlowStr;
            float _DashGlowStr;

            float Hash21(float2 v)
            {
                v = frac(v * float2(127.34, 311.17));
                v += dot(v, v + 45.32);
                return frac(v.x * v.y);
            }

            float SmoothNoise(float2 v)
            {
                float2 cell = floor(v);
                float2 f    = frac(v);
                float a = Hash21(cell);
                float b = Hash21(cell + float2(1, 0));
                float c = Hash21(cell + float2(0, 1));
                float d = Hash21(cell + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            float FBM(float2 v)
            {
                float val  = SmoothNoise(v)       * 0.58;
                      val += SmoothNoise(v * 2.1) * 0.28;
                      val += SmoothNoise(v * 4.5) * 0.14;
                return val;
            }

            float Luma(float3 rgb)
            {
                return dot(rgb, float3(0.299, 0.587, 0.114));
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // UnityObjectToClipPos nhận trực tiếp vertex biến dạng bởi SpriteSkin CPU Mesh deformation
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;

                #ifdef PIXELSNAP_ON
                    o.vertex = UnityPixelSnap(o.vertex);
                #endif

                return o;
            }

            float4 frag(v2f input) : SV_Target
            {
                float2 uv = input.texcoord;
                float4 tex = tex2D(_MainTex, uv);
                float centerA = tex.a * input.color.a;

                if (centerA <= 0.001)
                    discard;

                float t = _Time.y;
                float3 spriteRGB = tex.rgb;

                // 1. BASE TEXTURE GRADING
                float3 gradedRGB = ((spriteRGB - 0.5) * _Contrast) + 0.5;
                gradedRGB *= _Brightness;
                gradedRGB = max(gradedRGB, 0.0);

                float lum = saturate(Luma(gradedRGB));
                float topGrad = saturate(0.20 + (1.0 - uv.y) * 0.80);

                float3 crystalTint = lerp(_DeepIceColor.rgb, _GlacierColor.rgb, lum * 0.75 + topGrad * 0.25);
                float3 bodyColor = lerp(crystalTint, gradedRGB, _TextureMix);

                // 2. NEON GLOWING VEINS
                float2 noiseUV = uv * 6.5 + float2(t * 0.18, -t * 0.12);
                float fbm = FBM(noiseUV);

                float vein1 = abs(sin(uv.y * _IceVeinFreq + t * _VeinSpeed + fbm * 2.0));
                vein1 = pow(saturate(1.0 - vein1 * 1.05), 8.0);

                float vein2 = abs(sin((uv.x * 1.2 + uv.y) * (_IceVeinFreq * 0.8) - t * _VeinSpeed * 0.8 + fbm * 1.5));
                vein2 = pow(saturate(1.0 - vein2 * 1.05), 8.0);

                float veinMask = saturate(vein1 + vein2 * 0.7) * _IceVeinStr;
                float3 veinGlow = lerp(_GlowColor.rgb, _FrostColor.rgb, smoothstep(0.3, 0.85, veinMask));
                bodyColor += veinGlow * veinMask * 0.95;

                // 3. BIOLUMINESCENT PULSE & EYE/CORE ACCENT
                float pulse = sin(t * _PulseSpeed) * 0.5 + 0.5;
                float bioMask = (1.0 - _Charge * 0.8) * (1.0 - _Dash * 0.8);
                
                float coreDist = saturate(1.0 - length(uv - float2(0.50, 0.45)) * 2.1);
                bodyColor += _GlowColor.rgb * (pow(coreDist, 2.2) * pulse * _GlowIntensity * bioMask);

                float eyeSpot = smoothstep(0.78, 0.95, lum);
                bodyColor = lerp(bodyColor, _AccentColor.rgb * (1.0 + pulse * 0.3), eyeSpot * 0.65 * _AccentColor.a);

                // 4. CHARGE STATE
                if (_Charge > 0.001)
                {
                    float flicker = max(sin(t * _ChargeFlicker), sin(t * _ChargeFlicker * 1.6 + 1.0)) * 0.5 + 0.5;
                    float chargeHead = saturate(uv.y * 1.2 - 0.1);
                    
                    bodyColor += _GlowColor.rgb * (chargeHead * _Charge * _ChargeGlowStr * (0.6 + flicker * 0.4));
                    bodyColor += _FrostColor.rgb * (veinMask * _Charge * flicker * 0.85);
                }

                // 5. HIGH-CONTRAST DASH ATTACK
                if (_Dash > 0.001)
                {
                    float2 dashDir = normalize(_DashDirection.xy + float2(0.0001, 0));
                    float flow = dot(uv - 0.5, dashDir);

                    float spearHead = saturate(flow + 0.45);
                    bodyColor += _GlowColor.rgb * (pow(spearHead, 2.5) * _Dash * _DashGlowStr);
                    bodyColor += _SnowColor.rgb * (pow(spearHead, 4.0) * _Dash * 0.8);

                    float3 dashShadow = _DeepIceColor.rgb * 0.85;
                    float tailMask = saturate(-flow + 0.35);
                    bodyColor = lerp(bodyColor, dashShadow, tailMask * _Dash * 0.45);

                    float streak = sin(flow * 20.0 - t * 16.0) * 0.5 + 0.5;
                    streak = smoothstep(0.70, 0.95, streak);
                    bodyColor += _GlacierColor.rgb * (streak * _Dash * 0.40);
                }

                // 6. IMPACT FLASH
                if (_ImpactFlash > 0.001)
                {
                    bodyColor = lerp(bodyColor, _SnowColor.rgb * 1.2, _ImpactFlash * 0.85);
                }

                bodyColor *= input.color.rgb;

                // 7. MULTI-LAYER OUTLINE & INNER CRYSTAL RIM
                float2 px = _MainTex_TexelSize.xy * max(_OutlineWidth, 0.001);
                float a0 = tex2D(_MainTex, uv + float2( px.x,  0.0)).a;
                float a1 = tex2D(_MainTex, uv + float2(-px.x,  0.0)).a;
                float a2 = tex2D(_MainTex, uv + float2( 0.0,  px.y)).a;
                float a3 = tex2D(_MainTex, uv + float2( 0.0, -px.y)).a;
                float a4 = tex2D(_MainTex, uv + float2( px.x,  px.y)).a;
                float a5 = tex2D(_MainTex, uv + float2(-px.x,  px.y)).a;
                float a6 = tex2D(_MainTex, uv + float2( px.x, -px.y)).a;
                float a7 = tex2D(_MainTex, uv + float2(-px.x, -px.y)).a;

                float maxA = max(max(max(a0, a1), max(a2, a3)), max(max(a4, a5), max(a6, a7)));
                float minA = min(min(min(a0, a1), min(a2, a3)), min(min(a4, a5), min(a6, a7)));

                float outlineMask = saturate(maxA - tex.a) * _OutlineOpacity * _OutlineColor.a;
                
                float innerRimMask = saturate(maxA - minA) * saturate(tex.a * 3.0);
                float currentRim = innerRimMask * _RimPower * (0.50 + pulse * 0.35 + _Dash * 0.80);
                bodyColor += _RimColor.rgb * currentRim;

                // 8. PREMULTIPLIED COMPOSITING
                float bodyAlpha    = centerA;
                float outlineAlpha = outlineMask * (1.0 - bodyAlpha);
                float finalAlpha   = saturate(bodyAlpha + outlineAlpha);

                float3 finalRGB = (bodyColor * bodyAlpha) + (_OutlineColor.rgb * outlineAlpha);

                return float4(finalRGB, finalAlpha);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
